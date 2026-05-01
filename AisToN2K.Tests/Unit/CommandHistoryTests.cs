using AisToN2K.TUI;

namespace AisToN2K.Tests.Unit
{
    public class CommandHistoryTests
    {
        [Fact]
        public void NavigateUp_EmptyHistory_ReturnsNull()
        {
            var history = new CommandHistory();
            history.NavigateUp().Should().BeNull();
        }

        [Fact]
        public void Add_ThenNavigateUp_ReturnsLastCommand()
        {
            var history = new CommandHistory();
            history.Add("/connect");

            history.NavigateUp().Should().Be("/connect");
        }

        [Fact]
        public void MultipleCommands_NavigateUp_ReturnsInReverseOrder()
        {
            var history = new CommandHistory();
            history.Add("/connect");
            history.Add("/tcp start");
            history.Add("/status");

            history.NavigateUp().Should().Be("/status");
            history.NavigateUp().Should().Be("/tcp start");
            history.NavigateUp().Should().Be("/connect");
        }

        [Fact]
        public void NavigateUp_AtBeginning_StaysAtFirst()
        {
            var history = new CommandHistory();
            history.Add("/connect");
            history.Add("/status");

            history.NavigateUp(); // /status
            history.NavigateUp(); // /connect
            history.NavigateUp(); // should still be /connect

            history.NavigateUp().Should().Be("/connect");
        }

        [Fact]
        public void NavigateDown_AfterUp_MovesForward()
        {
            var history = new CommandHistory();
            history.Add("/connect");
            history.Add("/tcp start");
            history.Add("/status");

            history.NavigateUp(); // /status
            history.NavigateUp(); // /tcp start
            history.NavigateDown().Should().Be("/status");
        }

        [Fact]
        public void NavigateDown_AtEnd_ReturnsEmpty()
        {
            var history = new CommandHistory();
            history.Add("/connect");

            history.NavigateUp(); // /connect
            history.NavigateDown().Should().Be("");
        }

        [Fact]
        public void Add_DuplicateConsecutive_NotAdded()
        {
            var history = new CommandHistory();
            history.Add("/connect");
            history.Add("/connect");

            history.NavigateUp().Should().Be("/connect");
            history.NavigateUp().Should().Be("/connect"); // Only one entry
        }

        [Fact]
        public void Add_EmptyOrWhitespace_NotAdded()
        {
            var history = new CommandHistory();
            history.Add("");
            history.Add("   ");

            history.NavigateUp().Should().BeNull();
        }

        [Fact]
        public void ResetPosition_AllowsRestartFromEnd()
        {
            var history = new CommandHistory();
            history.Add("/connect");
            history.Add("/status");

            history.NavigateUp(); // /status
            history.NavigateUp(); // /connect
            history.ResetPosition();
            history.NavigateUp().Should().Be("/status"); // Reset to end
        }

        [Fact]
        public void MaxHistory_OldEntriesRemoved()
        {
            var history = new CommandHistory(maxHistory: 3);
            history.Add("/cmd1");
            history.Add("/cmd2");
            history.Add("/cmd3");
            history.Add("/cmd4"); // Should push /cmd1 out

            history.NavigateUp(); // /cmd4
            history.NavigateUp(); // /cmd3
            history.NavigateUp(); // /cmd2
            history.NavigateUp().Should().Be("/cmd2"); // /cmd1 is gone
        }
    }
}
