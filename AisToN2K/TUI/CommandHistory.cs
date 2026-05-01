namespace AisToN2K.TUI
{
    /// <summary>
    /// Tracks command input history with Up/Down arrow navigation.
    /// </summary>
    public class CommandHistory
    {
        private readonly List<string> _history = new();
        private int _position = -1;
        private readonly int _maxHistory;

        public CommandHistory(int maxHistory = 100)
        {
            _maxHistory = maxHistory;
        }

        public void Add(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return;

            // Don't add duplicates of the last command
            if (_history.Count > 0 && _history[^1] == command)
            {
                _position = -1;
                return;
            }

            _history.Add(command);
            if (_history.Count > _maxHistory)
            {
                _history.RemoveAt(0);
            }
            _position = -1;
        }

        /// <summary>
        /// Navigate up (older commands). Returns null if at the beginning.
        /// </summary>
        public string? NavigateUp()
        {
            if (_history.Count == 0)
                return null;

            if (_position == -1)
            {
                _position = _history.Count - 1;
            }
            else if (_position > 0)
            {
                _position--;
            }

            return _history[_position];
        }

        /// <summary>
        /// Navigate down (newer commands). Returns empty string if at the end.
        /// </summary>
        public string? NavigateDown()
        {
            if (_history.Count == 0 || _position == -1)
                return null;

            if (_position < _history.Count - 1)
            {
                _position++;
                return _history[_position];
            }
            else
            {
                _position = -1;
                return "";
            }
        }

        public void ResetPosition()
        {
            _position = -1;
        }
    }
}
