using AisToN2K.Models;

namespace AisToN2K.Services
{
    public record TrackPoint(
        double Latitude,
        double Longitude,
        double? SpeedOverGround,
        double? CourseOverGround,
        DateTime Timestamp);

    public enum DisplayUnits
    {
        Nautical,
        Metric
    }

    /// <summary>
    /// Tracks a specific vessel's position over time, calculating distance and speed.
    /// Maintains a persistent vessel name/MMSI registry and runtime last-heard timestamps.
    /// </summary>
    public class VesselTrackingService
    {
        private readonly object _lock = new();
        private readonly List<TrackPoint> _trackPoints = new();

        public int? TrackedMmsi { get; private set; }
        public string? TrackedVesselName { get; private set; }
        public bool IsTracking => TrackedMmsi.HasValue;
        /// <summary>
        /// True when tracking is set for an MMSI but no data has been received yet.
        /// </summary>
        public bool IsPendingTracking { get; private set; }
        public DisplayUnits Units { get; set; } = DisplayUnits.Nautical;

        public double TotalDistanceNm { get; private set; }
        public double? CurrentSogKnots { get; private set; }
        public DateTime? TrackingStartTime { get; private set; }

        /// <summary>
        /// Fired when pending tracking activates (vessel first heard).
        /// Args: (mmsi, vesselName)
        /// </summary>
        public event EventHandler<(int Mmsi, string Name)>? PendingTrackingActivated;

        // Persistent: vessel name/MMSI pairs (disk-backed)
        private Dictionary<int, string> _persistentVessels = new();
        private readonly VesselRegistryStore _registryStore;

        // Runtime: last-heard timestamps (in-memory only, cleared by /targets clear)
        private readonly Dictionary<int, DateTime> _lastHeard = new();

        public event EventHandler? TrackingUpdated;

        public VesselTrackingService(VesselRegistryStore? registryStore = null)
        {
            _registryStore = registryStore ?? VesselRegistryStore.CreateInMemory();
            _persistentVessels = _registryStore.Load();
        }

        /// <summary>
        /// Get list of known vessels (persistent name/MMSI pairs).
        /// </summary>
        public Dictionary<int, string> GetKnownVessels()
        {
            lock (_lock)
            {
                return new Dictionary<int, string>(_persistentVessels);
            }
        }

        /// <summary>
        /// Register a vessel as seen in the data stream.
        /// Updates both persistent name/MMSI and runtime last-heard timestamp.
        /// </summary>
        public void RegisterVessel(int mmsi, string? name, DateTime? timestamp = null)
        {
            bool isNew = false;
            lock (_lock)
            {
                var ts = timestamp ?? DateTime.UtcNow;
                _lastHeard[mmsi] = ts;

                if (!string.IsNullOrWhiteSpace(name))
                {
                    if (!_persistentVessels.TryGetValue(mmsi, out var existing) || existing != name.Trim())
                    {
                        _persistentVessels[mmsi] = name.Trim();
                        isNew = true;
                    }
                }
                else if (!_persistentVessels.ContainsKey(mmsi))
                {
                    _persistentVessels[mmsi] = mmsi.ToString();
                    isNew = true;
                }
            }

            if (isNew)
            {
                Dictionary<int, string> snapshot;
                lock (_lock) { snapshot = new Dictionary<int, string>(_persistentVessels); }
                _registryStore.MarkDirty(snapshot);
            }
        }

        /// <summary>
        /// Look up a vessel name by MMSI from the persistent registry.
        /// Returns null if the vessel is unknown or only stored as its MMSI string.
        /// </summary>
        public string? LookupName(int mmsi)
        {
            lock (_lock)
            {
                if (_persistentVessels.TryGetValue(mmsi, out var name) && name != mmsi.ToString())
                    return name;
                return null;
            }
        }

        /// <summary>
        /// Find vessels matching a search string (by MMSI or name).
        /// Searches persistent vessel registry (unaffected by /targets clear).
        /// </summary>
        public List<(int Mmsi, string Name)> FindVessels(string search)
        {
            lock (_lock)
            {
                if (int.TryParse(search, out var mmsi))
                {
                    if (_persistentVessels.TryGetValue(mmsi, out var name))
                        return new List<(int, string)> { (mmsi, name) };
                    return new List<(int, string)>();
                }

                var searchLower = search.ToLowerInvariant();
                return _persistentVessels
                    .Where(kv => kv.Value.ToLowerInvariant().Contains(searchLower))
                    .Select(kv => (kv.Key, kv.Value))
                    .OrderBy(v => v.Value)
                    .ToList();
            }
        }

        /// <summary>
        /// Get all targets heard this session (have a LastHeard timestamp).
        /// Sorted by most recently heard first.
        /// </summary>
        public List<(int Mmsi, string Name, DateTime LastHeard)> GetAllTargets()
        {
            lock (_lock)
            {
                return _lastHeard
                    .Select(kv => (
                        Mmsi: kv.Key,
                        Name: _persistentVessels.TryGetValue(kv.Key, out var n) ? n : kv.Key.ToString(),
                        LastHeard: kv.Value))
                    .OrderByDescending(t => t.LastHeard)
                    .ToList();
            }
        }

        /// <summary>
        /// Clear runtime last-heard timestamps. Persistent name/MMSI data is unaffected.
        /// </summary>
        public void ClearTargets()
        {
            lock (_lock)
            {
                _lastHeard.Clear();
            }
        }

        /// <summary>
        /// Flush persistent vessel registry to disk (call on shutdown).
        /// </summary>
        public void FlushRegistry()
        {
            Dictionary<int, string> snapshot;
            lock (_lock) { snapshot = new Dictionary<int, string>(_persistentVessels); }
            _registryStore.Flush(snapshot);
        }

        /// <summary>
        /// Start tracking a specific vessel.
        /// </summary>
        public void StartTracking(int mmsi, string vesselName)
        {
            lock (_lock)
            {
                TrackedMmsi = mmsi;
                TrackedVesselName = vesselName;
                IsPendingTracking = false;
                TotalDistanceNm = 0;
                CurrentSogKnots = null;
                TrackingStartTime = DateTime.UtcNow;
                _trackPoints.Clear();
            }
            TrackingUpdated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Start pending tracking for an MMSI that hasn't been heard yet.
        /// Tracking will activate automatically when the vessel first transmits.
        /// </summary>
        public void StartPendingTracking(int mmsi)
        {
            lock (_lock)
            {
                TrackedMmsi = mmsi;
                TrackedVesselName = mmsi.ToString();
                IsPendingTracking = true;
                TotalDistanceNm = 0;
                CurrentSogKnots = null;
                TrackingStartTime = null;
                _trackPoints.Clear();
            }
            TrackingUpdated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Stop tracking and return the accumulated track points.
        /// </summary>
        public List<TrackPoint> StopTracking()
        {
            List<TrackPoint> points;
            lock (_lock)
            {
                points = new List<TrackPoint>(_trackPoints);
                TrackedMmsi = null;
                TrackedVesselName = null;
                IsPendingTracking = false;
                TotalDistanceNm = 0;
                CurrentSogKnots = null;
                TrackingStartTime = null;
                _trackPoints.Clear();
            }
            TrackingUpdated?.Invoke(this, EventArgs.Empty);
            return points;
        }

        /// <summary>
        /// Process an incoming AIS data point. If it matches the tracked vessel, accumulate it.
        /// </summary>
        public void ProcessVesselData(AisData data)
        {
            // Always register vessels we see (updates both persistent name and runtime timestamp)
            var ts = data.Timestamp != default ? data.Timestamp : DateTime.UtcNow;
            RegisterVessel(data.Mmsi, data.VesselName, ts);

            if (!IsTracking || data.Mmsi != TrackedMmsi)
                return;

            // Transition from pending to active tracking on first data
            bool justActivated = false;
            if (IsPendingTracking)
            {
                string vesselName;
                lock (_lock)
                {
                    IsPendingTracking = false;
                    TrackingStartTime = DateTime.UtcNow;
                    vesselName = !string.IsNullOrWhiteSpace(data.VesselName)
                        ? data.VesselName
                        : TrackedVesselName ?? data.Mmsi.ToString();
                    TrackedVesselName = vesselName;
                }
                justActivated = true;
                PendingTrackingActivated?.Invoke(this, (data.Mmsi, vesselName));
            }

            // Update vessel name if we get a better one
            if (!justActivated && !string.IsNullOrWhiteSpace(data.VesselName))
            {
                lock (_lock)
                {
                    TrackedVesselName = data.VesselName;
                }
            }

            var point = new TrackPoint(
                data.Latitude,
                data.Longitude,
                data.SpeedOverGround,
                data.CourseOverGround,
                data.Timestamp != default ? data.Timestamp : DateTime.UtcNow);

            lock (_lock)
            {
                if (_trackPoints.Count > 0)
                {
                    var lastPoint = _trackPoints[^1];
                    var distance = HaversineDistanceNm(
                        lastPoint.Latitude, lastPoint.Longitude,
                        point.Latitude, point.Longitude);

                    // Only add distance if it's reasonable (< 50nm between reports to filter bad data)
                    if (distance < 50.0 && distance > 0.001)
                    {
                        TotalDistanceNm += distance;
                    }
                }

                CurrentSogKnots = data.SpeedOverGround;
                _trackPoints.Add(point);
            }

            TrackingUpdated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Get a copy of all track points.
        /// </summary>
        public List<TrackPoint> GetTrackPoints()
        {
            lock (_lock)
            {
                return new List<TrackPoint>(_trackPoints);
            }
        }

        /// <summary>
        /// Get average speed in knots since tracking started.
        /// </summary>
        public double GetAverageSpeedKnots()
        {
            if (!TrackingStartTime.HasValue || TotalDistanceNm < 0.001)
                return 0;

            var elapsed = DateTime.UtcNow - TrackingStartTime.Value;
            if (elapsed.TotalHours < 0.001)
                return 0;

            return TotalDistanceNm / elapsed.TotalHours;
        }

        // Display value helpers

        public string FormatDistance()
        {
            return Units switch
            {
                DisplayUnits.Nautical => $"{TotalDistanceNm:F1} nm",
                DisplayUnits.Metric => $"{TotalDistanceNm * 1.852:F1} km",
                _ => $"{TotalDistanceNm:F1} nm"
            };
        }

        public string FormatCurrentSpeed()
        {
            if (!CurrentSogKnots.HasValue)
                return "N/A";

            return Units switch
            {
                DisplayUnits.Nautical => $"{CurrentSogKnots.Value:F1} kn",
                DisplayUnits.Metric => $"{CurrentSogKnots.Value * 1.852:F1} km/h",
                _ => $"{CurrentSogKnots.Value:F1} kn"
            };
        }

        public string FormatAverageSpeed()
        {
            var avg = GetAverageSpeedKnots();
            return Units switch
            {
                DisplayUnits.Nautical => $"{avg:F1} kn",
                DisplayUnits.Metric => $"{avg * 1.852:F1} km/h",
                _ => $"{avg:F1} kn"
            };
        }

        /// <summary>
        /// Calculate the great-circle distance between two points using the Haversine formula.
        /// Returns distance in nautical miles.
        /// </summary>
        public static double HaversineDistanceNm(double lat1, double lon1, double lat2, double lon2)
        {
            const double EarthRadiusNm = 3440.065; // Earth radius in nautical miles

            var dLat = DegreesToRadians(lat2 - lat1);
            var dLon = DegreesToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return EarthRadiusNm * c;
        }

        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
    }
}
