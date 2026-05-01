namespace AisToN2K.Interfaces
{
    /// <summary>
    /// Abstraction for log output, allowing redirection from Console to TUI pane.
    /// </summary>
    public interface ILogOutput
    {
        void WriteLine(string message);
        void WriteLine(string format, params object[] args);

        /// <summary>
        /// Write a log line with vessel MMSI metadata (for alert highlighting).
        /// Default implementation ignores the MMSI.
        /// </summary>
        void WriteLineWithMmsi(string message, int mmsi) => WriteLine(message);
    }

    /// <summary>
    /// Default implementation that writes to Console (used in headless mode).
    /// </summary>
    public class ConsoleLogOutput : ILogOutput
    {
        public void WriteLine(string message) => Console.WriteLine(message);
        public void WriteLine(string format, params object[] args) => Console.WriteLine(format, args);
        public void WriteLineWithMmsi(string message, int mmsi) => Console.WriteLine(message);
    }
}
