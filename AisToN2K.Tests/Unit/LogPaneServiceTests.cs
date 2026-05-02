using AisToN2K.TUI;

namespace AisToN2K.Tests.Unit
{
    public class LogPaneServiceTests
    {
        [Fact]
        public void WriteLine_AddsTimestampedLine()
        {
            var service = new LogPaneService();

            service.WriteLine("Test message");

            service.LineCount.Should().Be(1);
            var lines = service.GetLines();
            lines[0].Should().Contain("Test message");
            // Should have HH:mm:ss timestamp prefix
            lines[0].Should().MatchRegex(@"^\d{2}:\d{2}:\d{2} .*");
        }

        [Fact]
        public void WriteLine_FormatString_FormatsCorrectly()
        {
            var service = new LogPaneService();

            service.WriteLine("Value is {0}", 42);

            var lines = service.GetLines();
            lines[0].Should().Contain("Value is 42");
        }

        [Fact]
        public void GetLines_LastN_ReturnsOnlyLastN()
        {
            var service = new LogPaneService();
            service.WriteLine("Line 1");
            service.WriteLine("Line 2");
            service.WriteLine("Line 3");

            var lines = service.GetLines(2);

            lines.Should().HaveCount(2);
            lines[0].Should().Contain("Line 2");
            lines[1].Should().Contain("Line 3");
        }

        [Fact]
        public void Clear_RemovesAllLines()
        {
            var service = new LogPaneService();
            service.WriteLine("Line 1");
            service.WriteLine("Line 2");

            service.Clear();

            service.LineCount.Should().Be(0);
            service.GetLines().Should().BeEmpty();
        }

        [Fact]
        public void MaxLines_OldLinesRemoved()
        {
            var service = new LogPaneService(maxLines: 3);
            service.WriteLine("Line 1");
            service.WriteLine("Line 2");
            service.WriteLine("Line 3");
            service.WriteLine("Line 4");

            service.LineCount.Should().Be(3);
            var lines = service.GetLines();
            lines[0].Should().Contain("Line 2"); // Line 1 evicted
        }

        [Fact]
        public void LogUpdated_EventFired()
        {
            var service = new LogPaneService();
            int eventCount = 0;
            service.LogUpdated += (s, e) => eventCount++;

            service.WriteLine("Test");

            eventCount.Should().Be(1);
        }

        [Fact]
        public void LogUpdated_FiredOnClear()
        {
            var service = new LogPaneService();
            service.WriteLine("Test");

            int eventCount = 0;
            service.LogUpdated += (s, e) => eventCount++;

            service.Clear();

            eventCount.Should().Be(1);
        }

        [Fact]
        public void GetLines_ReturnsDefensiveCopy()
        {
            var service = new LogPaneService();
            service.WriteLine("Line 1");

            var lines = service.GetLines();
            lines.Add("should not affect service");

            service.LineCount.Should().Be(1);
        }
    }
}
