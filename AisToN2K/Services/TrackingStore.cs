using System.Text.Json;

namespace AisToN2K.Services
{
    /// <summary>
    /// Persists vessel tracking state to a local JSON file.
    /// Immediate save for start/stop/activation; debounced save for track points.
    /// File location: ~/.local/share/ais-to-nmea0183/tracking.json
    /// </summary>
    public class TrackingStore
    {
        private static readonly string DefaultDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ais-to-nmea0183");

        private readonly string? _filePath;
        private readonly object _lock = new();
        private bool _dirty;
        private Timer? _debounceTimer;

        // In-memory state for in-memory-only mode (tests)
        private TrackingState? _memoryState;

        public TrackingStore(string? dataDir = null)
        {
            var dir = dataDir ?? DefaultDataDir;
            _filePath = Path.Combine(dir, "tracking.json");
        }

        private TrackingStore(bool inMemory)
        {
            _filePath = null;
        }

        /// <summary>
        /// Create an in-memory-only store for tests.
        /// </summary>
        public static TrackingStore CreateInMemory() => new(inMemory: true);

        /// <summary>
        /// Load persisted tracking state from disk. Returns null if no active tracking.
        /// </summary>
        public TrackingState? Load()
        {
            if (_filePath == null)
                return _memoryState;

            try
            {
                if (!File.Exists(_filePath))
                    return null;

                var json = File.ReadAllText(_filePath);
                var state = JsonSerializer.Deserialize<TrackingState>(json);
                return state;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Save tracking state immediately (for start/stop/activation).
        /// </summary>
        public void Save(TrackingState state)
        {
            if (_filePath == null)
            {
                _memoryState = state;
                return;
            }

            lock (_lock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = null;
                _dirty = false;
            }
            SaveToDisk(state);
        }

        /// <summary>
        /// Mark state as dirty and schedule a debounced save (10 seconds).
        /// Used for frequent track point additions.
        /// </summary>
        public void MarkDirty(TrackingState state)
        {
            if (_filePath == null)
            {
                _memoryState = state;
                return;
            }

            lock (_lock)
            {
                _dirty = true;
                _debounceTimer?.Dispose();
                _debounceTimer = new Timer(_ => FlushIfDirty(state), null,
                    TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);
            }
        }

        /// <summary>
        /// Force flush any pending debounced save (call on shutdown).
        /// </summary>
        public void Flush(TrackingState? state)
        {
            if (_filePath == null || state == null) return;

            lock (_lock)
            {
                if (!_dirty) return;
                _debounceTimer?.Dispose();
                _debounceTimer = null;
                _dirty = false;
            }
            SaveToDisk(state);
        }

        /// <summary>
        /// Clear persisted tracking state (delete file).
        /// </summary>
        public void Clear()
        {
            if (_filePath == null)
            {
                _memoryState = null;
                return;
            }

            lock (_lock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = null;
                _dirty = false;
            }

            try
            {
                if (File.Exists(_filePath))
                    File.Delete(_filePath);
            }
            catch
            {
                // Best effort
            }
        }

        private void FlushIfDirty(TrackingState state)
        {
            lock (_lock)
            {
                if (!_dirty) return;
                _dirty = false;
            }
            SaveToDisk(state);
        }

        private void SaveToDisk(TrackingState state)
        {
            if (_filePath == null) return;

            try
            {
                var dir = Path.GetDirectoryName(_filePath)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_filePath, json);
            }
            catch
            {
                // Best effort — don't crash on write failure
            }
        }
    }

    /// <summary>
    /// Serializable tracking state for persistence.
    /// </summary>
    public class TrackingState
    {
        public int Mmsi { get; set; }
        public string? VesselName { get; set; }
        public bool IsPendingTracking { get; set; }
        public DateTime? TrackingStartTime { get; set; }
        public double TotalDistanceNm { get; set; }
        public List<TrackPointData> TrackPoints { get; set; } = new();
    }

    /// <summary>
    /// Serializable track point for JSON storage.
    /// </summary>
    public class TrackPointData
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? SpeedOverGround { get; set; }
        public double? CourseOverGround { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
