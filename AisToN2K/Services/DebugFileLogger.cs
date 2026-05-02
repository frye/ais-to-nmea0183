namespace AisToN2K.Services
{
    /// <summary>
    /// Handles debug file logging with rotation for WS, TCP, and UDP messages.
    /// Only active when debug mode is enabled. Writes formatted + raw logs.
    /// </summary>
    public class DebugFileLogger : IDisposable
    {
        private const int MaxRotatedFiles = 5;
        private const int FlushIntervalMs = 5000;

        private readonly string _logDirectory;
        private readonly bool _enabled;
        private StreamWriter? _wsLog;
        private StreamWriter? _wsRawLog;
        private StreamWriter? _tcpLog;
        private StreamWriter? _tcpRawLog;
        private StreamWriter? _udpLog;
        private StreamWriter? _udpRawLog;
        private Timer? _flushTimer;
        private bool _disposed;
        private readonly object _lock = new();

        public string LogDirectory => _logDirectory;

        /// <summary>
        /// Creates a DebugFileLogger. If enabled is false, all logging methods are no-ops.
        /// </summary>
        /// <param name="logDirectory">Directory for log files. Created if it doesn't exist.</param>
        /// <param name="enabled">Whether logging is active (tied to --debug flag).</param>
        public DebugFileLogger(string logDirectory, bool enabled)
        {
            _enabled = enabled;
            _logDirectory = string.IsNullOrWhiteSpace(logDirectory) ? "log" : logDirectory;

            if (!_enabled) return;

            Directory.CreateDirectory(_logDirectory);
            RotateAllLogs();
            OpenLogFiles();

            _flushTimer = new Timer(_ => FlushAll(), null, FlushIntervalMs, FlushIntervalMs);
        }

        private void RotateAllLogs()
        {
            string[] logFiles = { "WS.log", "WS.raw.log", "TCP.log", "TCP.raw.log", "UDP.log", "UDP.raw.log" };
            foreach (var logFile in logFiles)
            {
                RotateFile(Path.Combine(_logDirectory, logFile));
            }
        }

        private void RotateFile(string filePath)
        {
            if (!File.Exists(filePath)) return;

            // Delete oldest rotated file if it exists
            var oldest = $"{filePath}.{MaxRotatedFiles}";
            if (File.Exists(oldest))
                File.Delete(oldest);

            // Shift .4 → .5, .3 → .4, etc.
            for (int i = MaxRotatedFiles - 1; i >= 1; i--)
            {
                var source = $"{filePath}.{i}";
                var dest = $"{filePath}.{i + 1}";
                if (File.Exists(source))
                    File.Move(source, dest);
            }

            // Move current → .1
            File.Move(filePath, $"{filePath}.1");
        }

        private void OpenLogFiles()
        {
            _wsLog = new StreamWriter(Path.Combine(_logDirectory, "WS.log"), append: false) { AutoFlush = false };
            _wsRawLog = new StreamWriter(Path.Combine(_logDirectory, "WS.raw.log"), append: false) { AutoFlush = false };
            _tcpLog = new StreamWriter(Path.Combine(_logDirectory, "TCP.log"), append: false) { AutoFlush = false };
            _tcpRawLog = new StreamWriter(Path.Combine(_logDirectory, "TCP.raw.log"), append: false) { AutoFlush = false };
            _udpLog = new StreamWriter(Path.Combine(_logDirectory, "UDP.log"), append: false) { AutoFlush = false };
            _udpRawLog = new StreamWriter(Path.Combine(_logDirectory, "UDP.raw.log"), append: false) { AutoFlush = false };
        }

        // --- WebSocket logging ---

        public void LogWebSocket(int messageType, int mmsi, string vesselName, double lat, double lon)
        {
            if (!_enabled) return;
            var line = $"[{DateTime.UtcNow:O}] RX Type {messageType} | MMSI: {mmsi} | {vesselName} | {lat:F6}, {lon:F6}";
            WriteLineSafe(_wsLog, line);
        }

        public void LogWebSocketRaw(string rawJson)
        {
            if (!_enabled) return;
            WriteLineSafe(_wsRawLog, rawJson);
        }

        // --- TCP logging ---

        public void LogTcp(int mmsi, string nmeaMessage)
        {
            if (!_enabled) return;
            var line = $"[{DateTime.UtcNow:O}] TX TCP | MMSI: {mmsi} | {nmeaMessage.TrimEnd()}";
            WriteLineSafe(_tcpLog, line);
        }

        public void LogTcpRaw(string nmeaMessage)
        {
            if (!_enabled) return;
            WriteLineSafe(_tcpRawLog, nmeaMessage.TrimEnd());
        }

        // --- UDP logging ---

        public void LogUdp(int mmsi, string nmeaMessage)
        {
            if (!_enabled) return;
            var line = $"[{DateTime.UtcNow:O}] TX UDP | MMSI: {mmsi} | {nmeaMessage.TrimEnd()}";
            WriteLineSafe(_udpLog, line);
        }

        public void LogUdpRaw(string nmeaMessage)
        {
            if (!_enabled) return;
            WriteLineSafe(_udpRawLog, nmeaMessage.TrimEnd());
        }

        // --- Internal helpers ---

        private void WriteLineSafe(StreamWriter? writer, string line)
        {
            if (writer == null) return;
            lock (_lock)
            {
                try
                {
                    writer.WriteLine(line);
                }
                catch (ObjectDisposedException)
                {
                    // Logger is being disposed, ignore
                }
            }
        }

        private void FlushAll()
        {
            lock (_lock)
            {
                try
                {
                    _wsLog?.Flush();
                    _wsRawLog?.Flush();
                    _tcpLog?.Flush();
                    _tcpRawLog?.Flush();
                    _udpLog?.Flush();
                    _udpRawLog?.Flush();
                }
                catch (ObjectDisposedException)
                {
                    // Disposed during flush
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _flushTimer?.Dispose();
            _flushTimer = null;

            lock (_lock)
            {
                _wsLog?.Flush(); _wsLog?.Dispose(); _wsLog = null;
                _wsRawLog?.Flush(); _wsRawLog?.Dispose(); _wsRawLog = null;
                _tcpLog?.Flush(); _tcpLog?.Dispose(); _tcpLog = null;
                _tcpRawLog?.Flush(); _tcpRawLog?.Dispose(); _tcpRawLog = null;
                _udpLog?.Flush(); _udpLog?.Dispose(); _udpLog = null;
                _udpRawLog?.Flush(); _udpRawLog?.Dispose(); _udpRawLog = null;
            }
        }
    }
}
