namespace AisToN2K.Interfaces
{
    /// <summary>
    /// Abstraction for log output, allowing redirection from Console to TUI pane.
    /// </summary>
    public interface ILogOutput
    {
        void WriteLine(string message);
        void WriteLine(string format, params object[] args);
    }

    /// <summary>
    /// Default implementation that writes to Console (used in headless mode).
    /// </summary>
    public class ConsoleLogOutput : ILogOutput
    {
        public void WriteLine(string message) => Console.WriteLine(message);
        public void WriteLine(string format, params object[] args) => Console.WriteLine(format, args);
    }
}
