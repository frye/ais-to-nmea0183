using AisToN2K.Services;

namespace AisToN2K.TUI
{
    public record CommandDefinition(string Name, string ShortHelp, string DetailedHelp, string Syntax);

    public class CommandParser
    {
        private readonly Dictionary<string, CommandDefinition> _commands;

        public CommandParser()
        {
            _commands = BuildCommandDefinitions();
        }

        public IReadOnlyList<CommandDefinition> GetAllCommands()
        {
            return _commands.Values.OrderBy(c => c.Name).ToList();
        }

        public IReadOnlyList<CommandDefinition> FilterCommands(string prefix)
        {
            var normalized = prefix.TrimStart('/').ToLowerInvariant();
            return _commands.Values
                .Where(c => c.Name.StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Name)
                .ToList();
        }

        public CommandDefinition? GetCommand(string name)
        {
            var normalized = name.TrimStart('/').ToLowerInvariant();
            // Support multi-word lookup (e.g., "tcp start" -> "tcp")
            var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && _commands.TryGetValue(parts[0], out var cmd))
                return cmd;
            return null;
        }

        /// <summary>
        /// Parse a raw input line into (commandName, args[]).
        /// Returns null if the input is not a valid slash command.
        /// </summary>
        public (string command, string[] args)? Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            var trimmed = input.Trim();
            if (!trimmed.StartsWith('/'))
                return null;

            var withoutSlash = trimmed[1..];
            var parts = withoutSlash.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return null;

            var command = parts[0].ToLowerInvariant();
            var args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();
            return (command, args);
        }

        private static Dictionary<string, CommandDefinition> BuildCommandDefinitions()
        {
            return new Dictionary<string, CommandDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["help"] = new("help", "Show help or command details",
                    "Show all available commands, or detailed help for a specific command.\n\n" +
                    "Examples:\n  /help           — List all commands\n  /help connect   — Show help for /connect\n  /help track     — Show help for /track",
                    "/help [command]"),

                ["connect"] = new("connect", "Start WebSocket connection",
                    "Connects to the AIS Stream WebSocket API using the configured URL and API key.\n" +
                    "The bounding box filter is applied on connect.\n\n" +
                    "Requires a valid API key (configured via user secrets or environment variable).",
                    "/connect"),

                ["disconnect"] = new("disconnect", "Stop WebSocket connection",
                    "Gracefully disconnects from the AIS Stream WebSocket.\n" +
                    "Vessel data streaming will stop until you /connect again.",
                    "/disconnect"),

                ["tcp"] = new("tcp", "Control TCP server",
                    "Start or stop the TCP server for NMEA 0183 output.\n" +
                    "Marine navigation software (e.g., OpenCPN) connects to this server.\n\n" +
                    "Examples:\n  /tcp start   — Start the TCP server\n  /tcp stop    — Stop the TCP server",
                    "/tcp <start|stop>"),

                ["udp"] = new("udp", "Control UDP server",
                    "Start or stop the UDP broadcast server for NMEA 0183 output.\n" +
                    "Broadcasts messages to all listeners on the configured UDP port.\n\n" +
                    "Examples:\n  /udp start   — Start the UDP server\n  /udp stop    — Stop the UDP server",
                    "/udp <start|stop>"),

                ["config"] = new("config", "Configure settings",
                    "Update application configuration at runtime.\n\n" +
                    "Subcommands:\n" +
                    "  /config bbox <N> <S> <E> <W>   — Set the geographic bounding box\n" +
                    "  /config url <url>               — Set the WebSocket URL\n" +
                    "  /config show                    — Show current configuration\n\n" +
                    "Examples:\n  /config bbox 48.8 48.0 -122.19 -123.355\n  /config url wss://stream.aisstream.io/v0/stream",
                    "/config <bbox|url|show> [args...]"),

                ["track"] = new("track", "Track a specific vessel",
                    "Start tracking a vessel by name or MMSI number.\n" +
                    "If the identifier is all digits, it's treated as an MMSI.\n" +
                    "Otherwise, it's matched against vessel names (case-insensitive).\n" +
                    "If multiple vessels match, a selection list is shown.\n\n" +
                    "Examples:\n  /track 123456789         — Track by MMSI\n  /track Pacific Explorer  — Track by name\n  /track stop              — Stop tracking",
                    "/track <name|mmsi|stop>"),

                ["units"] = new("units", "Set display units",
                    "Switch between nautical and metric units for speed and distance.\n" +
                    "Default is nautical (knots / nautical miles).\n\n" +
                    "Examples:\n  /units knots    — Use knots and nautical miles\n  /units metric   — Use km/h and kilometers",
                    "/units <knots|metric>"),

                ["export"] = new("export", "Export vessel track",
                    "Export the current tracked vessel's route to a file.\n" +
                    "Supported formats: GPX and KML.\n" +
                    "If no path is given, you will be prompted for a file path.\n\n" +
                    "Examples:\n  /export gpx                    — Export to GPX (prompted for path)\n  /export gpx /tmp/track.gpx     — Export to GPX at specified path\n  /export kml ~/vessel.kml       — Export to KML at specified path",
                    "/export <gpx|kml> [path]"),

                ["status"] = new("status", "Show service status",
                    "Display the current status of all services:\n" +
                    "WebSocket connection, TCP server, UDP server, and statistics.",
                    "/status"),

                ["clear"] = new("clear", "Clear the log pane",
                    "Clears all messages from the log output pane on the right side.",
                    "/clear"),

                ["debug"] = new("debug", "Toggle debug messages in log pane",
                    "Toggle the display of debug messages (🔍 📥 📤) in the log pane.\n" +
                    "Debug messages are still captured in log files and the buffer;\n" +
                    "this only controls whether they appear in the TUI log pane.\n\n" +
                    "When running with --debug, messages are hidden by default.\n" +
                    "Use /debug to show them, /debug again to hide.",
                    "/debug"),

                ["quit"] = new("quit", "Exit the application",
                    "Gracefully shut down all services and exit.\n" +
                    "You can also press Ctrl+C twice to exit.",
                    "/quit"),

                ["targets"] = new("targets", "List or clear seen vessels",
                    "Display all vessels heard this session, or clear the list.\n\n" +
                    "Subcommands:\n" +
                    "  /targets list                — Alphabetical (A→Z)\n" +
                    "  /targets list reverse        — Alphabetical (Z→A)\n" +
                    "  /targets list time           — Most recently heard first\n" +
                    "  /targets list time reverse   — Oldest first\n" +
                    "  /targets list mmsi           — MMSI ascending\n" +
                    "  /targets list mmsi reverse   — MMSI descending\n" +
                    "  /targets clear               — Clear the session's heard-vessel list\n\n" +
                    "Results are paginated. Press Enter for more pages.\n" +
                    "Vessel name/MMSI pairs are persisted across restarts.\n" +
                    "The 'last heard' timestamps are session-only and reset on clear or restart.",
                    "/targets <list [time|mmsi] [reverse]|clear>"),
            };
        }
    }
}
