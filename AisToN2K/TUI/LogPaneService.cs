using AisToN2K.Interfaces;

namespace AisToN2K.TUI
{
    /// <summary>
    /// A single log entry with optional vessel MMSI metadata for alert highlighting.
    /// </summary>
    public class LogEntry
    {
        public string Text { get; set; } = "";
        public int? Mmsi { get; set; }
    }

    /// <summary>
    /// Thread-safe log buffer that captures program output for the TUI log pane.
    /// Implements ILogOutput so services can write here instead of Console.
    /// Supports filtering debug messages from the display while retaining them in the buffer.
    /// Stores structured LogEntry objects with optional MMSI metadata for alert highlighting.
    /// </summary>
    public class LogPaneService : ILogOutput
    {
        private static readonly string[] DebugPrefixes = new[]
        {
            "🔍", "📥 RX:", "📤 TX:", "COORD DEBUG", "⚠️ Ignored message type:"
        };

        private readonly object _lock = new();
        private readonly List<LogEntry> _entries = new();
        private readonly int _maxLines;

        /// <summary>
        /// When false, debug messages (identified by known prefixes) are hidden from display
        /// but still retained in the buffer. Default: true.
        /// </summary>
        public bool ShowDebugMessages { get; set; } = true;

        public event EventHandler? LogUpdated;

        public LogPaneService(int maxLines = 1000)
        {
            _maxLines = maxLines;
        }

        public void WriteLine(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var line = $"{timestamp} {message}";
            lock (_lock)
            {
                _entries.Add(new LogEntry { Text = line, Mmsi = null });
                if (_entries.Count > _maxLines)
                {
                    _entries.RemoveAt(0);
                }
            }
            LogUpdated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Write a log line with vessel MMSI metadata (for alert highlighting).
        /// </summary>
        public void WriteLineWithMmsi(string message, int mmsi)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var line = $"{timestamp} {message}";
            lock (_lock)
            {
                _entries.Add(new LogEntry { Text = line, Mmsi = mmsi });
                if (_entries.Count > _maxLines)
                {
                    _entries.RemoveAt(0);
                }
            }
            LogUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void WriteLine(string format, params object[] args)
        {
            WriteLine(string.Format(format, args));
        }

        /// <summary>
        /// Get log lines as plain strings (backward-compatible).
        /// </summary>
        public List<string> GetLines()
        {
            lock (_lock)
            {
                if (ShowDebugMessages)
                    return _entries.Select(e => e.Text).ToList();
                return _entries.Where(e => !IsDebugLine(e.Text)).Select(e => e.Text).ToList();
            }
        }

        /// <summary>
        /// Get structured log entries (for alert-aware rendering).
        /// </summary>
        public List<LogEntry> GetEntries()
        {
            lock (_lock)
            {
                if (ShowDebugMessages)
                    return _entries.Select(e => new LogEntry { Text = e.Text, Mmsi = e.Mmsi }).ToList();
                return _entries
                    .Where(e => !IsDebugLine(e.Text))
                    .Select(e => new LogEntry { Text = e.Text, Mmsi = e.Mmsi })
                    .ToList();
            }
        }

        public List<string> GetLines(int lastN)
        {
            lock (_lock)
            {
                var source = ShowDebugMessages
                    ? _entries
                    : _entries.Where(e => !IsDebugLine(e.Text)).ToList();
                var list = source.ToList();
                if (lastN >= list.Count)
                    return list.Select(e => e.Text).ToList();
                return list.GetRange(list.Count - lastN, lastN).Select(e => e.Text).ToList();
            }
        }

        public int LineCount
        {
            get { lock (_lock) { return _entries.Count; } }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _entries.Clear();
            }
            LogUpdated?.Invoke(this, EventArgs.Empty);
        }

        private static bool IsDebugLine(string line)
        {
            // Lines are formatted as "HH:mm:ss message" — skip the timestamp prefix
            var msg = line.Length > 9 ? line.AsSpan(9) : line.AsSpan();
            foreach (var prefix in DebugPrefixes)
            {
                if (msg.StartsWith(prefix))
                    return true;
            }
            return false;
        }
    }
}
