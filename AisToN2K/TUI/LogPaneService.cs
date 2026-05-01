using AisToN2K.Interfaces;

namespace AisToN2K.TUI
{
    /// <summary>
    /// Thread-safe log buffer that captures program output for the TUI log pane.
    /// Implements ILogOutput so services can write here instead of Console.
    /// Supports filtering debug messages from the display while retaining them in the buffer.
    /// </summary>
    public class LogPaneService : ILogOutput
    {
        private static readonly string[] DebugPrefixes = new[]
        {
            "🔍", "📥 RX:", "📤 TX:", "COORD DEBUG", "⚠️ Ignored message type:"
        };

        private readonly object _lock = new();
        private readonly List<string> _lines = new();
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
                _lines.Add(line);
                if (_lines.Count > _maxLines)
                {
                    _lines.RemoveAt(0);
                }
            }
            LogUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void WriteLine(string format, params object[] args)
        {
            WriteLine(string.Format(format, args));
        }

        public List<string> GetLines()
        {
            lock (_lock)
            {
                if (ShowDebugMessages)
                    return new List<string>(_lines);
                return _lines.Where(l => !IsDebugLine(l)).ToList();
            }
        }

        public List<string> GetLines(int lastN)
        {
            lock (_lock)
            {
                var source = ShowDebugMessages ? _lines : _lines.Where(l => !IsDebugLine(l)).ToList();
                var list = source.ToList();
                if (lastN >= list.Count)
                    return list;
                return list.GetRange(list.Count - lastN, lastN);
            }
        }

        public int LineCount
        {
            get { lock (_lock) { return _lines.Count; } }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _lines.Clear();
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
