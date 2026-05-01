using System.Text.Json;

namespace AisToN2K.Services
{
    /// <summary>
    /// Persists vessel name/MMSI pairs to a local JSON file.
    /// File location: ~/.local/share/ais-to-nmea0183/known-vessels.json
    /// </summary>
    public class VesselRegistryStore
    {
        private static readonly string DefaultDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ais-to-nmea0183");

        private readonly string _filePath;
        private readonly object _lock = new();
        private DateTime _lastSaveTime = DateTime.MinValue;
        private bool _dirty = false;
        private Timer? _debounceTimer;

        public VesselRegistryStore(string? dataDir = null)
        {
            var dir = dataDir ?? DefaultDataDir;
            _filePath = Path.Combine(dir, "known-vessels.json");
        }

        /// <summary>
        /// Load persisted vessels from disk. Returns empty dictionary if file doesn't exist.
        /// </summary>
        public Dictionary<int, string> Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return new Dictionary<int, string>();

                var json = File.ReadAllText(_filePath);
                var data = JsonSerializer.Deserialize<VesselRegistryData>(json);
                if (data?.Vessels == null)
                    return new Dictionary<int, string>();

                // Convert string keys back to int
                var result = new Dictionary<int, string>();
                foreach (var kv in data.Vessels)
                {
                    if (int.TryParse(kv.Key, out var mmsi))
                        result[mmsi] = kv.Value;
                }
                return result;
            }
            catch
            {
                return new Dictionary<int, string>();
            }
        }

        /// <summary>
        /// Mark the registry as dirty and schedule a debounced save (at most once every 10 seconds).
        /// </summary>
        public void MarkDirty(Dictionary<int, string> vessels)
        {
            lock (_lock)
            {
                _dirty = true;
                _debounceTimer?.Dispose();
                _debounceTimer = new Timer(_ => FlushIfDirty(vessels), null,
                    TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);
            }
        }

        /// <summary>
        /// Force an immediate save (call on shutdown).
        /// </summary>
        public void Flush(Dictionary<int, string> vessels)
        {
            lock (_lock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = null;
                _dirty = false;
            }
            SaveToDisk(vessels);
        }

        private void FlushIfDirty(Dictionary<int, string> vessels)
        {
            lock (_lock)
            {
                if (!_dirty) return;
                _dirty = false;
            }
            SaveToDisk(vessels);
        }

        private void SaveToDisk(Dictionary<int, string> vessels)
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var data = new VesselRegistryData
                {
                    Vessels = vessels.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_filePath, json);
                _lastSaveTime = DateTime.UtcNow;
            }
            catch
            {
                // Best effort — don't crash on write failure
            }
        }

        private class VesselRegistryData
        {
            public Dictionary<string, string> Vessels { get; set; } = new();
        }
    }
}
