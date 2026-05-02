using AisToN2K.TUI;

namespace AisToN2K.Tests.Unit
{
    public class CommandParserTests
    {
        private readonly CommandParser _parser = new();

        [Fact]
        public void Parse_ValidSlashCommand_ReturnsCommandAndArgs()
        {
            var result = _parser.Parse("/connect");

            result.Should().NotBeNull();
            result!.Value.command.Should().Be("connect");
            result.Value.args.Should().BeEmpty();
        }

        [Fact]
        public void Parse_CommandWithArgs_ReturnsAllArgs()
        {
            var result = _parser.Parse("/config bbox 48.8 48.0 -122.19 -123.355");

            result.Should().NotBeNull();
            result!.Value.command.Should().Be("config");
            result.Value.args.Should().Equal("bbox", "48.8", "48.0", "-122.19", "-123.355");
        }

        [Fact]
        public void Parse_WithoutSlash_ReturnsNull()
        {
            var result = _parser.Parse("connect");
            result.Should().BeNull();
        }

        [Fact]
        public void Parse_EmptyString_ReturnsNull()
        {
            _parser.Parse("").Should().BeNull();
            _parser.Parse("   ").Should().BeNull();
        }

        [Fact]
        public void Parse_SlashOnly_ReturnsNull()
        {
            _parser.Parse("/").Should().BeNull();
        }

        [Fact]
        public void Parse_IsCaseInsensitive()
        {
            var result = _parser.Parse("/CONNECT");
            result.Should().NotBeNull();
            result!.Value.command.Should().Be("connect");
        }

        [Fact]
        public void GetAllCommands_ReturnsAllDefinedCommands()
        {
            var commands = _parser.GetAllCommands();

            commands.Should().NotBeEmpty();
            commands.Select(c => c.Name).Should().Contain("connect");
            commands.Select(c => c.Name).Should().Contain("disconnect");
            commands.Select(c => c.Name).Should().Contain("tcp");
            commands.Select(c => c.Name).Should().Contain("udp");
            commands.Select(c => c.Name).Should().Contain("config");
            commands.Select(c => c.Name).Should().Contain("track");
            commands.Select(c => c.Name).Should().Contain("export");
            commands.Select(c => c.Name).Should().Contain("units");
            commands.Select(c => c.Name).Should().Contain("status");
            commands.Select(c => c.Name).Should().Contain("clear");
            commands.Select(c => c.Name).Should().Contain("quit");
            commands.Select(c => c.Name).Should().Contain("help");
        }

        [Fact]
        public void FilterCommands_EmptyPrefix_ReturnsAll()
        {
            var all = _parser.GetAllCommands();
            var filtered = _parser.FilterCommands("");

            filtered.Count.Should().Be(all.Count);
        }

        [Fact]
        public void FilterCommands_PartialMatch_FiltersCorrectly()
        {
            var filtered = _parser.FilterCommands("co");

            filtered.Should().NotBeEmpty();
            filtered.Select(c => c.Name).Should().Contain("connect");
            filtered.Select(c => c.Name).Should().Contain("config");
            filtered.Select(c => c.Name).Should().NotContain("tcp");
        }

        [Fact]
        public void FilterCommands_WithSlashPrefix_StillWorks()
        {
            var filtered = _parser.FilterCommands("/tr");

            filtered.Should().NotBeEmpty();
            filtered.Select(c => c.Name).Should().Contain("track");
        }

        [Fact]
        public void GetCommand_ExistingCommand_ReturnsDefinition()
        {
            var cmd = _parser.GetCommand("connect");

            cmd.Should().NotBeNull();
            cmd!.Name.Should().Be("connect");
            cmd.ShortHelp.Should().NotBeEmpty();
            cmd.DetailedHelp.Should().NotBeEmpty();
            cmd.Syntax.Should().NotBeEmpty();
        }

        [Fact]
        public void GetCommand_NonExistentCommand_ReturnsNull()
        {
            _parser.GetCommand("nonexistent").Should().BeNull();
        }

        [Fact]
        public void GetCommand_WithSlashPrefix_StillWorks()
        {
            var cmd = _parser.GetCommand("/tcp");
            cmd.Should().NotBeNull();
            cmd!.Name.Should().Be("tcp");
        }

        [Fact]
        public void AllCommands_HaveRequiredMetadata()
        {
            foreach (var cmd in _parser.GetAllCommands())
            {
                cmd.Name.Should().NotBeNullOrWhiteSpace($"Command name should not be empty");
                cmd.ShortHelp.Should().NotBeNullOrWhiteSpace($"Command {cmd.Name} should have short help");
                cmd.DetailedHelp.Should().NotBeNullOrWhiteSpace($"Command {cmd.Name} should have detailed help");
                cmd.Syntax.Should().NotBeNullOrWhiteSpace($"Command {cmd.Name} should have syntax");
                cmd.Syntax.Should().StartWith($"/{cmd.Name}", $"Syntax for {cmd.Name} should start with /{cmd.Name}");
            }
        }
    }
}
