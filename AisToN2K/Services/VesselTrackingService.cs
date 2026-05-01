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
    /// </summary>
    public class VesselTrackingService
    {
        private readonly object _lock = new();
        private readonly List<TrackPoint> _trackPoints = new();

        public int? TrackedMmsi { get; private set; }
        public string? TrackedVesselName { get; private set; }
        public bool IsTracking => TrackedMmsi.HasValue;
        public DisplayUnits Units { get; set; } = DisplayUnits.Nautical;

        public double TotalDistanceNm { get; private set; }
        public double? CurrentSogKnots { get; private set; }
        public DateTime? TrackingStartTime { get; private set; }

        // Known vessels seen in the data stream (MMSI -> last known name)
        private readonly Dictionary<int, string> _knownVessels = new();

        public event EventHandler? TrackingUpdated;

        /// <summary>
        /// Get list of known vessels (seen in the data stream).
        /// </summary>
        public Dictionary<int, string> GetKnownVessels()
        {
            lock (_lock)
            {
                return new Dictionary<int, string>(_knownVessels);
            }
        }

        /// <summary>
        /// Register a vessel as seen in the data stream.
        /// </summary>
        public void RegisterVessel(int mmsi, string? name)
        {
            lock (_lock)
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    _knownVessels[mmsi] = name.Trim();
                }
                else if (!_knownVessels.ContainsKey(mmsi))
                {
                    _knownVessels[mmsi] = mmsi.ToString();
                }
            }
        }

        /// <summary>
        /// Find vessels matching a search string (by MMSI or name).
        /// </summary>
        public List<(int Mmsi, string Name)> FindVessels(string search)
        {
            lock (_lock)
            {
                // If all digits, try MMSI match first
                if (int.TryParse(search, out var mmsi))
                {
                    if (_knownVessels.TryGetValue(mmsi, out var name))
                        return new List<(int, string)> { (mmsi, name) };
                    return new List<(int, string)>();
                }

                // Name search (case-insensitive contains)
                var searchLower = search.ToLowerInvariant();
                return _knownVessels
                    .Where(kv => kv.Value.ToLowerInvariant().Contains(searchLower))
                    .Select(kv => (kv.Key, kv.Value))
                    .OrderBy(v => v.Value)
                    .ToList();
            }
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
                TotalDistanceNm = 0;
                CurrentSogKnots = null;
                TrackingStartTime = DateTime.UtcNow;
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
            // Always register vessels we see
            RegisterVessel(data.Mmsi, data.VesselName);

            if (!IsTracking || data.Mmsi != TrackedMmsi)
                return;

            // Update vessel name if we get a better one
            if (!string.IsNullOrWhiteSpace(data.VesselName))
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
