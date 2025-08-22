using System.Collections.Concurrent;

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

        public StatisticsService(bool debugMode = false, int reportingIntervalSeconds = 30)
        {
            _messageTypesCounts = new ConcurrentDictionary<int, int>();
            _startTime = DateTime.Now;
            _debugMode = debugMode;

            // Report statistics at specified interval (default 30 seconds)
            var interval = TimeSpan.FromSeconds(reportingIntervalSeconds);
            _reportingTimer = new Timer(ReportStatistics, null, interval, interval);
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

            Console.WriteLine($"📊 === STATISTICS REPORT ===");
            Console.WriteLine($"🕐 Uptime: {uptime:hh\\:mm\\:ss}");
            Console.WriteLine($"📥 Total Messages Received: {TotalMessagesReceived:N0}");
            Console.WriteLine($"🔄 Total Messages Converted: {TotalMessagesConverted:N0}");
            Console.WriteLine($"📤 Total Messages Broadcast: {TotalMessagesBroadcast:N0}");
            Console.WriteLine($"❌ Total Errors: {TotalErrors:N0}");
            Console.WriteLine($"📈 Rate: {messagesPerMinute:F1} msg/min");

            if (_messageTypesCounts.Any())
            {
                Console.WriteLine($"📋 Message Types:");
                foreach (var kvp in _messageTypesCounts.OrderBy(x => x.Key))
                {
                    var messageTypeName = GetMessageTypeName(kvp.Key);
                    Console.WriteLine($"   Type {kvp.Key} ({messageTypeName}): {kvp.Value:N0}");
                }
            }

            Console.WriteLine($"========================");
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
