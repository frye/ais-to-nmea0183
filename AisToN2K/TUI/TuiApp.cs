using AisToN2K.Configuration;
using AisToN2K.Interfaces;
using AisToN2K.Models;
using AisToN2K.Services;
using Terminal.Gui;

namespace AisToN2K.TUI
{
    /// <summary>
    /// Main TUI application using Terminal.Gui v1.x.
    /// Layout: tracking bar (top, conditional), command output (left), status + log (right), input (bottom).
    /// </summary>
    public class TuiApp
    {
        private readonly AppConfig _config;
        private readonly bool _debugMode;

        private ServiceManager? _serviceManager;
        private VesselTrackingService _trackingService = new();
        private LogPaneService _logService = new();
        private CommandParser _commandParser = new();
        private CommandHistory _commandHistory = new();

        // UI elements
        private Toplevel? _top;
        private Window? _mainWindow;
        private FrameView? _trackingBar;
        private Label? _trackingLabel;
        private FrameView? _commandPane;
        private TextView? _commandOutput;
        private FrameView? _statusFrame;
        private Label? _wsStatusLabel;
        private Label? _tcpStatusLabel;
        private Label? _udpStatusLabel;
        private Label? _statsLabel;
        private FrameView? _logFrame;
        private TextView? _logOutput;
        private TextField? _inputField;
        private ListView? _slashMenu;
        private FrameView? _slashMenuFrame;

        // State
        private bool _ctrlCPressed = false;
        private DateTime _lastCtrlC = DateTime.MinValue;
        private bool _slashMenuVisible = false;
        private List<CommandDefinition> _filteredCommands = new();
        private bool _isAwaitingInput = false;
        private string? _pendingPromptContext;
        private Action<string>? _pendingPromptCallback;

        // Auto-start flags
        private bool _autoStartWs = true;
        private bool _autoStartTcp = true;
        private bool _autoStartUdp = true;
        private readonly string? _logPathOverride;

        public TuiApp(AppConfig config, bool debugMode, bool autoStartWs = true, bool autoStartTcp = true, bool autoStartUdp = true, string? logPathOverride = null)
        {
            _config = config;
            _debugMode = debugMode;
            _autoStartWs = autoStartWs;
            _autoStartTcp = autoStartTcp;
            _autoStartUdp = autoStartUdp;
            _logPathOverride = logPathOverride;
        }

        public async Task RunAsync()
        {
            // Initialize services
            _serviceManager = new ServiceManager(_config, _debugMode, _logPathOverride);
            await _serviceManager.InitializeAsync();

            // Hook vessel data for tracking
            _serviceManager.StatusChanged += OnStatusChanged;

            // Subscribe to the VesselDataReceived on the ServiceManager
            SubscribeToVesselData();

            // In debug mode, suppress debug messages from TUI log pane by default
            if (_debugMode)
            {
                _logService.ShowDebugMessages = false;
            }

            Application.Init();
            _top = Application.Top;

            BuildLayout();
            SetupInputHandling();
            SetupLogUpdates();
            SetupTrackingUpdates();

            // Show welcome message
            AppendCommandOutput("🚢 AIS to NMEA 0183 Converter — TUI Mode");
            AppendCommandOutput("Type / to see available commands, or /help for detailed help.");
            AppendCommandOutput("");

            // Auto-start services after UI is ready
            AutoStartServices();

            // Periodic status refresh (every 2 seconds)
            var statusTimer = new System.Threading.Timer(_ =>
            {
                Application.MainLoop?.Invoke(() => RefreshStatusPanel());
            }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));

            Application.Run(_top);

            statusTimer.Dispose();
            Application.Shutdown();

            // Cleanup
            if (_serviceManager != null)
            {
                await _serviceManager.DisposeAsync();
            }
        }

        private void SubscribeToVesselData()
        {
            // We need to hook into the ServiceManager's vessel data pipeline.
            // The ServiceManager fires StatusChanged events, but we also need raw vessel data
            // for tracking. We'll add this via a public event we expose from ServiceManager.
            // For now, we use the existing OnVesselDataReceived indirectly via a custom hook.
            _serviceManager!.VesselDataForTracking += (sender, data) =>
            {
                _trackingService.ProcessVesselData(data);
            };
        }

        private void AutoStartServices()
        {
            // Fire-and-forget auto-start on a background thread so the UI stays responsive
            Task.Run(async () =>
            {
                if (_autoStartTcp && _config.Network.EnableTcp)
                {
                    var result = await _serviceManager!.StartTcpServerAsync();
                    Application.MainLoop?.Invoke(() =>
                    {
                        AppendCommandOutput(result
                            ? $"✅ TCP server auto-started on {_config.Network.Tcp.Host}:{_config.Network.Tcp.Port}"
                            : "⚠️  TCP server failed to auto-start. Use /tcp start to retry.");
                        RefreshStatusPanel();
                    });
                }

                if (_autoStartUdp && _config.Network.EnableUdp)
                {
                    var result = await _serviceManager!.StartUdpServerAsync();
                    Application.MainLoop?.Invoke(() =>
                    {
                        AppendCommandOutput(result
                            ? $"✅ UDP server auto-started on {_config.Network.Udp.Host}:{_config.Network.Udp.Port}"
                            : "⚠️  UDP server failed to auto-start. Use /udp start to retry.");
                        RefreshStatusPanel();
                    });
                }

                if (_autoStartWs)
                {
                    if (string.IsNullOrEmpty(_config.ApiKey))
                    {
                        Application.MainLoop?.Invoke(() =>
                        {
                            AppendCommandOutput("⚠️  API key not configured — WebSocket not started.");
                            AppendCommandOutput("  Set via: dotnet user-secrets set \"AisApi:ApiKey\" \"your-key\"");
                            AppendCommandOutput("  Then use /connect to start manually.");
                        });
                    }
                    else
                    {
                        Application.MainLoop?.Invoke(() => AppendCommandOutput("🔌 Connecting to WebSocket..."));
                        var result = await _serviceManager!.StartWebSocketAsync();
                        Application.MainLoop?.Invoke(() =>
                        {
                            AppendCommandOutput(result
                                ? "✅ WebSocket connected."
                                : "⚠️  WebSocket connection failed. Use /connect to retry.");
                            RefreshStatusPanel();
                        });
                    }
                }
            });
        }

        private void BuildLayout()
        {
            _mainWindow = new Window("AIS to NMEA 0183")
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            // Tracking bar (top, initially hidden — height 0)
            _trackingBar = new FrameView("Tracking")
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = 3,
                Visible = false
            };

            _trackingLabel = new Label("")
            {
                X = 1,
                Y = 0,
                Width = Dim.Fill() - 2,
                Height = 1
            };
            _trackingBar.Add(_trackingLabel);

            // Content area Y depends on tracking bar visibility
            int contentY = 0;

            // Left pane: command output (scrollable)
            _commandPane = new FrameView("Commands")
            {
                X = 0,
                Y = contentY,
                Width = Dim.Percent(40),
                Height = Dim.Fill() - 3 // Leave room for input
            };

            _commandOutput = new TextView()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                ReadOnly = true,
                WordWrap = true
            };
            _commandPane.Add(_commandOutput);

            // Right side top: status box
            _statusFrame = new FrameView("Service Status")
            {
                X = Pos.Percent(40),
                Y = contentY,
                Width = Dim.Fill(),
                Height = 5
            };

            _wsStatusLabel = new Label("WS:  ○ Disconnected")
            {
                X = 1,
                Y = 0
            };
            _tcpStatusLabel = new Label("TCP: ○ Stopped")
            {
                X = 1,
                Y = 1
            };
            _udpStatusLabel = new Label("UDP: ○ Stopped")
            {
                X = 1,
                Y = 2
            };
            _statsLabel = new Label("Messages: 0 received, 0 broadcast")
            {
                X = Pos.Percent(50),
                Y = 0,
                Width = Dim.Fill() - 1,
                Height = 3
            };
            _statusFrame.Add(_wsStatusLabel, _tcpStatusLabel, _udpStatusLabel, _statsLabel);

            // Right side bottom: log output
            _logFrame = new FrameView("Log Output")
            {
                X = Pos.Percent(40),
                Y = contentY + 5,
                Width = Dim.Fill(),
                Height = Dim.Fill() - 3
            };

            _logOutput = new TextView()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                ReadOnly = true,
                WordWrap = true
            };
            _logFrame.Add(_logOutput);

            // Input field at bottom
            var inputLabel = new Label("> ")
            {
                X = 0,
                Y = Pos.AnchorEnd(2),
                Width = 2
            };

            _inputField = new TextField("")
            {
                X = 2,
                Y = Pos.AnchorEnd(2),
                Width = Dim.Fill(),
                Height = 1
            };

            // Slash menu (overlay, initially hidden)
            _slashMenuFrame = new FrameView("Commands")
            {
                X = 2,
                Y = Pos.AnchorEnd(14),
                Width = Dim.Percent(40),
                Height = 12,
                Visible = false
            };

            _slashMenu = new ListView()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                AllowsMarking = false
            };
            _slashMenuFrame.Add(_slashMenu);

            _mainWindow.Add(_trackingBar, _commandPane, _statusFrame, _logFrame,
                           inputLabel, _inputField, _slashMenuFrame);
            _top!.Add(_mainWindow);

            _inputField.SetFocus();
        }

        private void SetupInputHandling()
        {
            _inputField!.KeyPress += (e) =>
            {
                var key = e.KeyEvent.Key;

                if (key == Key.Enter)
                {
                    // If slash menu is visible, Enter selects the highlighted command
                    if (_slashMenuVisible && _filteredCommands.Count > 0)
                    {
                        var selected = _filteredCommands[_slashMenu!.SelectedItem];
                        _inputField.Text = $"/{selected.Name} ";
                        _inputField.CursorPosition = _inputField.Text.Length;
                        HideSlashMenu();
                        e.Handled = true;
                        return;
                    }

                    var text = _inputField.Text?.ToString() ?? "";
                    _inputField.Text = "";
                    HideSlashMenu();
                    _commandHistory.Add(text);

                    if (_isAwaitingInput && _pendingPromptCallback != null)
                    {
                        _isAwaitingInput = false;
                        var cb = _pendingPromptCallback;
                        _pendingPromptCallback = null;
                        _pendingPromptContext = null;
                        cb(text);
                    }
                    else
                    {
                        ProcessCommand(text);
                    }
                    e.Handled = true;
                    return;
                }

                if (key == Key.Esc)
                {
                    if (_slashMenuVisible)
                    {
                        HideSlashMenu();
                        e.Handled = true;
                    }
                    return;
                }

                if (key == Key.CursorUp)
                {
                    if (_slashMenuVisible && _slashMenu!.SelectedItem > 0)
                    {
                        _slashMenu.SelectedItem--;
                        _slashMenu.SetNeedsDisplay();
                        e.Handled = true;
                        return;
                    }
                    var prev = _commandHistory.NavigateUp();
                    if (prev != null)
                    {
                        _inputField.Text = prev;
                        _inputField.CursorPosition = prev.Length;
                    }
                    e.Handled = true;
                    return;
                }

                if (key == Key.CursorDown)
                {
                    if (_slashMenuVisible && _slashMenu!.SelectedItem < _filteredCommands.Count - 1)
                    {
                        _slashMenu.SelectedItem++;
                        _slashMenu.SetNeedsDisplay();
                        e.Handled = true;
                        return;
                    }
                    var next = _commandHistory.NavigateDown();
                    if (next != null)
                    {
                        _inputField.Text = next;
                        _inputField.CursorPosition = next.Length;
                    }
                    e.Handled = true;
                    return;
                }

                if (key == Key.Tab && _slashMenuVisible && _filteredCommands.Count > 0)
                {
                    var selected = _filteredCommands[_slashMenu!.SelectedItem];
                    _inputField.Text = $"/{selected.Name} ";
                    _inputField.CursorPosition = _inputField.Text.Length;
                    HideSlashMenu();
                    e.Handled = true;
                    return;
                }

                // Handle Ctrl+C for double-press exit
                if (key == (Key.C | Key.CtrlMask))
                {
                    var now = DateTime.Now;
                    if (_ctrlCPressed && (now - _lastCtrlC).TotalSeconds < 2)
                    {
                        Application.RequestStop();
                        e.Handled = true;
                        return;
                    }
                    _ctrlCPressed = true;
                    _lastCtrlC = now;
                    AppendCommandOutput("⚠️  Press Ctrl+C again within 2 seconds to exit.");
                    e.Handled = true;
                    return;
                }

                _ctrlCPressed = false;

                // After key processing, update slash menu
                Application.MainLoop?.AddIdle(() =>
                {
                    UpdateSlashMenu();
                    return false;
                });
            };

            // Handle slash menu item selection via Enter in list
            _slashMenu!.OpenSelectedItem += (e) =>
            {
                if (_filteredCommands.Count > 0 && e.Item >= 0 && e.Item < _filteredCommands.Count)
                {
                    var selected = _filteredCommands[e.Item];
                    _inputField!.Text = $"/{selected.Name} ";
                    _inputField.CursorPosition = _inputField.Text.Length;
                    HideSlashMenu();
                    _inputField.SetFocus();
                }
            };
        }

        private void UpdateSlashMenu()
        {
            var text = _inputField?.Text?.ToString() ?? "";

            if (text.StartsWith("/") && !text.Contains(' '))
            {
                var prefix = text[1..];
                _filteredCommands = _commandParser.FilterCommands(prefix).ToList();

                if (_filteredCommands.Count > 0)
                {
                    var items = _filteredCommands
                        .Select(c => $"/{c.Name,-15} {c.ShortHelp}")
                        .ToList();

                    _slashMenu!.SetSource(items);
                    _slashMenu.SelectedItem = 0;
                    ShowSlashMenu();
                }
                else
                {
                    HideSlashMenu();
                }
            }
            else
            {
                HideSlashMenu();
            }
        }

        private void ShowSlashMenu()
        {
            if (!_slashMenuVisible)
            {
                _slashMenuVisible = true;
                _slashMenuFrame!.Visible = true;
                _mainWindow!.SetNeedsDisplay();
            }
        }

        private void HideSlashMenu()
        {
            if (_slashMenuVisible)
            {
                _slashMenuVisible = false;
                _slashMenuFrame!.Visible = false;
                _mainWindow!.SetNeedsDisplay();
            }
        }

        private void SetupLogUpdates()
        {
            _logService.LogUpdated += (s, e) =>
            {
                Application.MainLoop?.Invoke(() =>
                {
                    RefreshLogPane();
                });
            };

            // Redirect ServiceManager console output to our log service
            _serviceManager!.LogOutput = _logService;
        }

        private void SetupTrackingUpdates()
        {
            _trackingService.TrackingUpdated += (s, e) =>
            {
                Application.MainLoop?.Invoke(() =>
                {
                    RefreshTrackingBar();
                    RefreshStatusPanel();
                });
            };
        }

        private void RefreshLogPane()
        {
            var lines = _logService.GetLines();
            var text = string.Join("\n", lines);
            _logOutput!.Text = text;

            // Auto-scroll to bottom
            if (lines.Count > 0)
            {
                _logOutput.MoveEnd();
            }
        }

        private void RefreshTrackingBar()
        {
            if (_trackingService.IsTracking)
            {
                _trackingBar!.Visible = true;
                var name = _trackingService.TrackedVesselName ?? "Unknown";
                var dist = _trackingService.FormatDistance();
                var sog = _trackingService.FormatCurrentSpeed();
                var avg = _trackingService.FormatAverageSpeed();
                _trackingLabel!.Text = $"🚢 {name}  |  Distance: {dist}  |  SOG: {sog}  |  Avg: {avg}";

                // Adjust layout when tracking bar is visible
                _commandPane!.Y = 3;
                _statusFrame!.Y = 3;
                _logFrame!.Y = 3 + 5;
            }
            else
            {
                _trackingBar!.Visible = false;
                _commandPane!.Y = 0;
                _statusFrame!.Y = 0;
                _logFrame!.Y = 5;
            }
            _mainWindow!.SetNeedsDisplay();
        }

        private void RefreshStatusPanel()
        {
            var wsConnected = _serviceManager?.IsWebSocketConnected ?? false;
            var tcpRunning = _serviceManager?.IsTcpServerRunning ?? false;
            var udpRunning = _serviceManager?.IsUdpServerRunning ?? false;

            _wsStatusLabel!.Text = wsConnected
                ? "WS:  ● Connected"
                : "WS:  ○ Disconnected";

            _tcpStatusLabel!.Text = tcpRunning
                ? $"TCP: ● Running ({_config.Network.Tcp.Host}:{_config.Network.Tcp.Port})"
                : "TCP: ○ Stopped";

            _udpStatusLabel!.Text = udpRunning
                ? $"UDP: ● Running ({_config.Network.Udp.Host}:{_config.Network.Udp.Port})"
                : "UDP: ○ Stopped";

            var stats = _serviceManager?.Statistics;
            if (stats != null)
            {
                _statsLabel!.Text = $"Rx: {stats.TotalMessagesReceived}  Tx: {stats.TotalMessagesBroadcast}  Err: {stats.TotalErrors}";
            }

            _statusFrame!.SetNeedsDisplay();
        }

        private void OnStatusChanged(object? sender, string status)
        {
            _logService.WriteLine(status);
            Application.MainLoop?.Invoke(() =>
            {
                RefreshStatusPanel();
            });
        }

        private void AppendCommandOutput(string text)
        {
            var current = _commandOutput?.Text?.ToString() ?? "";
            if (current.Length > 0)
                current += "\n";
            current += text;
            _commandOutput!.Text = current;
            _commandOutput.MoveEnd();
        }

        private void PromptForInput(string prompt, Action<string> callback)
        {
            _isAwaitingInput = true;
            _pendingPromptCallback = callback;
            _pendingPromptContext = prompt;
            AppendCommandOutput(prompt);
        }

        // ── Command Execution ──────────────────────────────────────────

        private void ProcessCommand(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return;

            // If not a slash command, show hint
            if (!input.TrimStart().StartsWith('/'))
            {
                AppendCommandOutput($"> {input}");
                AppendCommandOutput("Commands start with /. Type / to see available commands.");
                return;
            }

            AppendCommandOutput($"> {input}");

            var parsed = _commandParser.Parse(input);
            if (parsed == null)
            {
                AppendCommandOutput("Invalid command. Type / to see available commands.");
                return;
            }

            var (cmd, args) = parsed.Value;

            switch (cmd)
            {
                case "help":
                    ExecuteHelp(args);
                    break;
                case "connect":
                    _ = ExecuteConnectAsync();
                    break;
                case "disconnect":
                    _ = ExecuteDisconnectAsync();
                    break;
                case "tcp":
                    _ = ExecuteTcpAsync(args);
                    break;
                case "udp":
                    _ = ExecuteUdpAsync(args);
                    break;
                case "config":
                    _ = ExecuteConfigAsync(args);
                    break;
                case "track":
                    ExecuteTrack(args);
                    break;
                case "units":
                    ExecuteUnits(args);
                    break;
                case "export":
                    _ = ExecuteExportAsync(args);
                    break;
                case "status":
                    ExecuteStatus();
                    break;
                case "clear":
                    _logService.Clear();
                    AppendCommandOutput("Log cleared.");
                    break;
                case "debug":
                    _logService.ShowDebugMessages = !_logService.ShowDebugMessages;
                    AppendCommandOutput(_logService.ShowDebugMessages
                        ? "Debug messages shown in log pane."
                        : "Debug messages hidden from log pane.");
                    RefreshLogPane();
                    break;
                case "quit":
                    Application.RequestStop();
                    break;
                default:
                    AppendCommandOutput($"Unknown command: /{cmd}. Type / to see available commands.");
                    break;
            }
        }

        private void ExecuteHelp(string[] args)
        {
            if (args.Length == 0)
            {
                AppendCommandOutput("Available commands:");
                AppendCommandOutput("");
                foreach (var cmd in _commandParser.GetAllCommands())
                {
                    AppendCommandOutput($"  /{cmd.Name,-15} {cmd.ShortHelp}");
                }
                AppendCommandOutput("");
                AppendCommandOutput("Type /help <command> for detailed help.");
            }
            else
            {
                var cmdDef = _commandParser.GetCommand(args[0]);
                if (cmdDef != null)
                {
                    AppendCommandOutput($"/{cmdDef.Name}");
                    AppendCommandOutput($"  Syntax: {cmdDef.Syntax}");
                    AppendCommandOutput("");
                    AppendCommandOutput(cmdDef.DetailedHelp);
                }
                else
                {
                    AppendCommandOutput($"Unknown command: {args[0]}");
                }
            }
        }

        private async Task ExecuteConnectAsync()
        {
            AppendCommandOutput("Connecting to WebSocket...");
            var result = await _serviceManager!.StartWebSocketAsync();
            Application.MainLoop?.Invoke(() =>
            {
                if (result)
                    AppendCommandOutput("✅ WebSocket connected.");
                else
                    AppendCommandOutput("❌ WebSocket connection failed. Check API key and configuration.");
                RefreshStatusPanel();
            });
        }

        private async Task ExecuteDisconnectAsync()
        {
            await _serviceManager!.StopWebSocketAsync();
            Application.MainLoop?.Invoke(() =>
            {
                AppendCommandOutput("WebSocket disconnected.");
                RefreshStatusPanel();
            });
        }

        private async Task ExecuteTcpAsync(string[] args)
        {
            if (args.Length == 0)
            {
                AppendCommandOutput("Usage: /tcp <start|stop>");
                return;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "start":
                    AppendCommandOutput("Starting TCP server...");
                    var tcpResult = await _serviceManager!.StartTcpServerAsync();
                    Application.MainLoop?.Invoke(() =>
                    {
                        AppendCommandOutput(tcpResult
                            ? $"✅ TCP server started on {_config.Network.Tcp.Host}:{_config.Network.Tcp.Port}"
                            : "❌ TCP server failed to start.");
                        RefreshStatusPanel();
                    });
                    break;
                case "stop":
                    await _serviceManager!.StopTcpServerAsync();
                    Application.MainLoop?.Invoke(() =>
                    {
                        AppendCommandOutput("TCP server stopped.");
                        RefreshStatusPanel();
                    });
                    break;
                default:
                    AppendCommandOutput("Usage: /tcp <start|stop>");
                    break;
            }
        }

        private async Task ExecuteUdpAsync(string[] args)
        {
            if (args.Length == 0)
            {
                AppendCommandOutput("Usage: /udp <start|stop>");
                return;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "start":
                    AppendCommandOutput("Starting UDP server...");
                    var udpResult = await _serviceManager!.StartUdpServerAsync();
                    Application.MainLoop?.Invoke(() =>
                    {
                        AppendCommandOutput(udpResult
                            ? $"✅ UDP server started on {_config.Network.Udp.Host}:{_config.Network.Udp.Port}"
                            : "❌ UDP server failed to start.");
                        RefreshStatusPanel();
                    });
                    break;
                case "stop":
                    await _serviceManager!.StopUdpServerAsync();
                    Application.MainLoop?.Invoke(() =>
                    {
                        AppendCommandOutput("UDP server stopped.");
                        RefreshStatusPanel();
                    });
                    break;
                default:
                    AppendCommandOutput("Usage: /udp <start|stop>");
                    break;
            }
        }

        private async Task ExecuteConfigAsync(string[] args)
        {
            if (args.Length == 0)
            {
                AppendCommandOutput("Usage: /config <bbox|url|show>");
                return;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "show":
                    AppendCommandOutput("Current configuration:");
                    AppendCommandOutput($"  WebSocket URL: {_config.WebSocketUrl}");
                    AppendCommandOutput($"  Bounding Box: N:{_config.BoundingBox.North}, S:{_config.BoundingBox.South}, E:{_config.BoundingBox.East}, W:{_config.BoundingBox.West}");
                    AppendCommandOutput($"  TCP: {(_config.Network.EnableTcp ? $"{_config.Network.Tcp.Host}:{_config.Network.Tcp.Port}" : "Disabled")}");
                    AppendCommandOutput($"  UDP: {(_config.Network.EnableUdp ? $"{_config.Network.Udp.Host}:{_config.Network.Udp.Port}" : "Disabled")}");
                    break;

                case "bbox":
                    if (args.Length < 5)
                    {
                        AppendCommandOutput("Usage: /config bbox <North> <South> <East> <West>");
                        AppendCommandOutput($"  Current: N:{_config.BoundingBox.North} S:{_config.BoundingBox.South} E:{_config.BoundingBox.East} W:{_config.BoundingBox.West}");
                        return;
                    }
                    if (double.TryParse(args[1], out var n) && double.TryParse(args[2], out var s) &&
                        double.TryParse(args[3], out var e) && double.TryParse(args[4], out var w))
                    {
                        var bbox = new BoundingBox { North = n, South = s, East = e, West = w };
                        await _serviceManager!.UpdateConfigurationAsync(bbox);
                        Application.MainLoop?.Invoke(() =>
                        {
                            AppendCommandOutput($"✅ Bounding box updated: N:{n} S:{s} E:{e} W:{w}");
                            RefreshStatusPanel();
                        });
                    }
                    else
                    {
                        AppendCommandOutput("❌ Invalid coordinates. Use decimal degrees (e.g., /config bbox 48.8 48.0 -122.19 -123.355)");
                    }
                    break;

                case "url":
                    if (args.Length < 2)
                    {
                        AppendCommandOutput("Usage: /config url <websocket-url>");
                        AppendCommandOutput($"  Current: {_config.WebSocketUrl}");
                        return;
                    }
                    _config.WebSocketUrl = args[1];
                    AppendCommandOutput($"✅ WebSocket URL updated to: {args[1]}");
                    AppendCommandOutput("  Use /disconnect and /connect to apply.");
                    break;

                default:
                    AppendCommandOutput("Usage: /config <bbox|url|show>");
                    break;
            }
        }

        private void ExecuteTrack(string[] args)
        {
            if (args.Length == 0)
            {
                AppendCommandOutput("Usage: /track <name|mmsi|stop>");
                if (_trackingService.IsTracking)
                {
                    AppendCommandOutput($"  Currently tracking: {_trackingService.TrackedVesselName} (MMSI: {_trackingService.TrackedMmsi})");
                    AppendCommandOutput($"  Distance: {_trackingService.FormatDistance()}, SOG: {_trackingService.FormatCurrentSpeed()}, Avg: {_trackingService.FormatAverageSpeed()}");
                }
                return;
            }

            if (args[0].Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                if (!_trackingService.IsTracking)
                {
                    AppendCommandOutput("Not currently tracking any vessel.");
                    return;
                }
                var points = _trackingService.StopTracking();
                AppendCommandOutput($"Tracking stopped. {points.Count} track points recorded.");
                RefreshTrackingBar();
                return;
            }

            var search = string.Join(" ", args);
            var matches = _trackingService.FindVessels(search);

            if (matches.Count == 0)
            {
                AppendCommandOutput($"No vessel found matching '{search}'.");
                AppendCommandOutput("Vessels appear after receiving AIS data. Try /connect first.");
                return;
            }

            if (matches.Count == 1)
            {
                var (mmsi, name) = matches[0];
                _trackingService.StartTracking(mmsi, name);
                AppendCommandOutput($"🚢 Now tracking: {name} (MMSI: {mmsi})");
                RefreshTrackingBar();
                return;
            }

            // Multiple matches — show selection
            AppendCommandOutput($"Multiple vessels match '{search}':");
            for (int i = 0; i < matches.Count && i < 20; i++)
            {
                AppendCommandOutput($"  [{i + 1}] {matches[i].Name} (MMSI: {matches[i].Mmsi})");
            }
            if (matches.Count > 20)
            {
                AppendCommandOutput($"  ... and {matches.Count - 20} more. Try a more specific search.");
                return;
            }

            PromptForInput("Enter number to select vessel: ", (input) =>
            {
                if (int.TryParse(input.Trim(), out var idx) && idx >= 1 && idx <= matches.Count)
                {
                    var (mmsi, name) = matches[idx - 1];
                    _trackingService.StartTracking(mmsi, name);
                    AppendCommandOutput($"🚢 Now tracking: {name} (MMSI: {mmsi})");
                    RefreshTrackingBar();
                }
                else
                {
                    AppendCommandOutput("Selection cancelled.");
                }
            });
        }

        private void ExecuteUnits(string[] args)
        {
            if (args.Length == 0)
            {
                var current = _trackingService.Units == DisplayUnits.Nautical ? "knots/nm" : "km/h/km";
                AppendCommandOutput($"Current units: {current}");
                AppendCommandOutput("Usage: /units <knots|metric>");
                return;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "knots":
                case "nautical":
                case "nm":
                    _trackingService.Units = DisplayUnits.Nautical;
                    AppendCommandOutput("✅ Units set to nautical (knots / nautical miles).");
                    RefreshTrackingBar();
                    break;
                case "metric":
                case "km":
                case "kmh":
                    _trackingService.Units = DisplayUnits.Metric;
                    AppendCommandOutput("✅ Units set to metric (km/h / kilometers).");
                    RefreshTrackingBar();
                    break;
                default:
                    AppendCommandOutput("Usage: /units <knots|metric>");
                    break;
            }
        }

        private async Task ExecuteExportAsync(string[] args)
        {
            if (!_trackingService.IsTracking && _trackingService.GetTrackPoints().Count == 0)
            {
                AppendCommandOutput("No track data to export. Start tracking a vessel first with /track.");
                return;
            }

            if (args.Length == 0)
            {
                AppendCommandOutput("Usage: /export <gpx|kml> [path]");
                return;
            }

            var format = args[0].ToLowerInvariant();
            if (format != "gpx" && format != "kml")
            {
                AppendCommandOutput("Supported formats: gpx, kml");
                return;
            }

            var points = _trackingService.GetTrackPoints();
            var vesselName = _trackingService.TrackedVesselName ?? "Unknown";
            var mmsi = _trackingService.TrackedMmsi ?? 0;

            if (points.Count == 0)
            {
                AppendCommandOutput("No track points recorded yet.");
                return;
            }

            if (args.Length >= 2)
            {
                var path = args[1];
                await DoExport(format, path, points, vesselName, mmsi);
            }
            else
            {
                var defaultName = $"{vesselName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.{format}";
                PromptForInput($"Enter file path (default: ./{defaultName}): ", async (input) =>
                {
                    var path = string.IsNullOrWhiteSpace(input) ? defaultName : input.Trim();
                    await DoExport(format, path, points, vesselName, mmsi);
                });
            }
        }

        private async Task DoExport(string format, string path, List<TrackPoint> points, string vesselName, int mmsi)
        {
            try
            {
                // Expand ~ to home directory
                if (path.StartsWith("~"))
                {
                    path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..].TrimStart('/'));
                }

                // Ensure directory exists
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                if (format == "gpx")
                {
                    await GpxExporter.ExportToFileAsync(points, vesselName, mmsi, path);
                }
                else
                {
                    await KmlExporter.ExportToFileAsync(points, vesselName, mmsi, path);
                }

                Application.MainLoop?.Invoke(() =>
                {
                    AppendCommandOutput($"✅ Exported {points.Count} track points to {path}");
                });
            }
            catch (Exception ex)
            {
                Application.MainLoop?.Invoke(() =>
                {
                    AppendCommandOutput($"❌ Export failed: {ex.Message}");
                });
            }
        }

        private void ExecuteStatus()
        {
            var wsConnected = _serviceManager?.IsWebSocketConnected ?? false;
            var tcpRunning = _serviceManager?.IsTcpServerRunning ?? false;
            var udpRunning = _serviceManager?.IsUdpServerRunning ?? false;
            var stats = _serviceManager?.Statistics;

            AppendCommandOutput("Service Status:");
            AppendCommandOutput($"  WebSocket: {(wsConnected ? "● Connected" : "○ Disconnected")} ({_config.WebSocketUrl})");
            AppendCommandOutput($"  TCP:       {(tcpRunning ? $"● Running ({_config.Network.Tcp.Host}:{_config.Network.Tcp.Port})" : "○ Stopped")}");
            AppendCommandOutput($"  UDP:       {(udpRunning ? $"● Running ({_config.Network.Udp.Host}:{_config.Network.Udp.Port})" : "○ Stopped")}");

            if (stats != null)
            {
                AppendCommandOutput($"  Messages:  {stats.TotalMessagesReceived} received, {stats.TotalMessagesConverted} converted, {stats.TotalMessagesBroadcast} broadcast");
                AppendCommandOutput($"  Errors:    {stats.TotalErrors}");
            }

            if (_trackingService.IsTracking)
            {
                AppendCommandOutput($"  Tracking:  {_trackingService.TrackedVesselName} (MMSI: {_trackingService.TrackedMmsi})");
                AppendCommandOutput($"  Distance:  {_trackingService.FormatDistance()}, SOG: {_trackingService.FormatCurrentSpeed()}, Avg: {_trackingService.FormatAverageSpeed()}");
            }

            AppendCommandOutput($"  Units:     {(_trackingService.Units == DisplayUnits.Nautical ? "Nautical (knots/nm)" : "Metric (km/h/km)")}");
            AppendCommandOutput($"  Bbox:      N:{_config.BoundingBox.North} S:{_config.BoundingBox.South} E:{_config.BoundingBox.East} W:{_config.BoundingBox.West}");
        }
    }
}
