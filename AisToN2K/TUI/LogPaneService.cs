using AisToN2K.Interfaces;

namespace AisToN2K.TUI
{
    /// <summary>
    /// Thread-safe log buffer that captures program output for the TUI log pane.
    /// Implements ILogOutput so services can write here instead of Console.
    /// </summary>
    public class LogPaneService : ILogOutput
    {
        private readonly object _lock = new();
        private readonly List<string> _lines = new();
        private readonly int _maxLines;

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
                return new List<string>(_lines);
            }
        }

        public List<string> GetLines(int lastN)
        {
            lock (_lock)
            {
                if (lastN >= _lines.Count)
                    return new List<string>(_lines);
                return _lines.GetRange(_lines.Count - lastN, lastN);
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
    }
}
