using System.Text.Json;

namespace AisToN2K.Services
{
    /// <summary>
    /// Persists vessel alerts to a local JSON file.
    /// Saves immediately on every mutation (alerts are infrequent user actions).
    /// File location: ~/.local/share/ais-to-nmea0183/alerts.json
    /// </summary>
    public class AlertStore
    {
        private static readonly string DefaultDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ais-to-nmea0183");

        private readonly string? _filePath;

        public AlertStore(string? dataDir = null)
        {
            var dir = dataDir ?? DefaultDataDir;
            _filePath = Path.Combine(dir, "alerts.json");
        }

        private AlertStore(bool inMemory)
        {
            _filePath = null;
        }

        /// <summary>
        /// Create an in-memory-only store for tests.
        /// </summary>
        public static AlertStore CreateInMemory() => new(inMemory: true);

        /// <summary>
        /// Load persisted alerts from disk.
        /// </summary>
        public List<AlertEntry> Load()
        {
            if (_filePath == null)
                return new List<AlertEntry>();

            try
            {
                if (!File.Exists(_filePath))
                    return new List<AlertEntry>();

                var json = File.ReadAllText(_filePath);
                var data = JsonSerializer.Deserialize<AlertFileData>(json);
                return data?.Alerts ?? new List<AlertEntry>();
            }
            catch
            {
                return new List<AlertEntry>();
            }
        }

        /// <summary>
        /// Save alerts to disk immediately.
        /// </summary>
        public void Save(List<AlertEntry> alerts)
        {
            if (_filePath == null) return;

            try
            {
                var dir = Path.GetDirectoryName(_filePath)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var data = new AlertFileData { Alerts = alerts };
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
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

        private class AlertFileData
        {
            public List<AlertEntry> Alerts { get; set; } = new();
        }
    }

    public class AlertEntry
    {
        public int Mmsi { get; set; }
        public string? Name { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
