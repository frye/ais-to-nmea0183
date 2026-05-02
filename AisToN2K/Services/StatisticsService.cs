using System.Collections.Concurrent;
using AisToN2K.Interfaces;

namespace AisToN2K.Services
{
    public class StatisticsService
    {
        private readonly ConcurrentDictionary<int, int> _messageTypesCounts;
        private readonly Timer _reportingTimer;
        private readonly bool _debugMode;
        private DateTime _startTime;

        public int TotalMessagesReceived { get; private set; }
        public int TotalMessagesConverted { get; private set; }
        public int TotalMessagesBroadcast { get; private set; }
        public int TotalErrors { get; private set; }

        /// <summary>
        /// Log output target. When set, statistics are reported here instead of Console.
        /// </summary>
        public ILogOutput? LogOutput { get; set; }

        public StatisticsService(bool debugMode = false, int reportingIntervalSeconds = 30)
        {
            _messageTypesCounts = new ConcurrentDictionary<int, int>();
            _startTime = DateTime.Now;
            _debugMode = debugMode;

            // Report statistics at specified interval (default 30 seconds)
            var interval = TimeSpan.FromSeconds(reportingIntervalSeconds);
            _reportingTimer = new Timer(ReportStatistics, null, interval, interval);
        }

        private void Log(string message)
        {
            if (LogOutput != null)
                LogOutput.WriteLine(message);
            else
                Console.WriteLine(message);
        }

        public void IncrementMessageReceived(int messageType)
        {
            TotalMessagesReceived++;
            _messageTypesCounts.AddOrUpdate(messageType, 1, (key, oldValue) => oldValue + 1);
        }

        public void IncrementMessageConverted()
        {
            TotalMessagesConverted++;
        }

        public void IncrementMessageBroadcast()
        {
            TotalMessagesBroadcast++;
        }

        public void IncrementError()
        {
            TotalErrors++;
        }

        private void ReportStatistics(object? state)
        {
            var uptime = DateTime.Now - _startTime;
            var messagesPerMinute = TotalMessagesReceived / Math.Max(1, uptime.TotalMinutes);

            Log($"📊 === STATISTICS REPORT ===");
            Log($"🕐 Uptime: {uptime:hh\\:mm\\:ss}");
            Log($"📥 Total Messages Received: {TotalMessagesReceived:N0}");
            Log($"🔄 Total Messages Converted: {TotalMessagesConverted:N0}");
            Log($"📤 Total Messages Broadcast: {TotalMessagesBroadcast:N0}");
            Log($"❌ Total Errors: {TotalErrors:N0}");
            Log($"📈 Rate: {messagesPerMinute:F1} msg/min");

            if (_messageTypesCounts.Any())
            {
                Log($"📋 Message Types:");
                foreach (var kvp in _messageTypesCounts.OrderBy(x => x.Key))
                {
                    var messageTypeName = GetMessageTypeName(kvp.Key);
                    Log($"   Type {kvp.Key} ({messageTypeName}): {kvp.Value:N0}");
                }
            }

            Log($"========================");
        }

        public void LogMessageDetails(int messageType, string vesselName, double latitude, double longitude, string nmeaMessage)
        {
            // Message details logging removed for production use
        }

        private static string GetMessageTypeName(int messageType)
        {
            return messageType switch
            {
                1 => "Position Report Class A",
                2 => "Position Report Class A (Assigned Schedule)",
                3 => "Position Report Class A (Response to Interrogation)",
                4 => "Base Station Report",
                5 => "Static and Voyage Related Data",
                18 => "Standard Class B CS Position Report",
                19 => "Extended Class B CS Position Report",
                24 => "Static Data Report",
                27 => "Long Range AIS Broadcast",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Get a snapshot of message type counts for API consumers.
        /// </summary>
        public Dictionary<int, int> GetMessageTypeCounts()
        {
            return new Dictionary<int, int>(_messageTypesCounts);
        }

        /// <summary>
        /// Get the human-readable name for a message type (public accessor).
        /// </summary>
        public static string GetMessageTypeDisplayName(int messageType)
        {
            return GetMessageTypeName(messageType);
        }

        public void PrintSummary()
        {
            ReportStatistics(null);
        }

        public void Dispose()
        {
            _reportingTimer?.Dispose();
        }
    }
}
