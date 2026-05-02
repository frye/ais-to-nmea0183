using AisToN2K.Configuration;
using AisToN2K.Services;
using AisToN2K.TUI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace AisToN2K
{
    class Program
    {
        private static ServiceManager? _serviceManager;
        private static AppConfig? _config;
        private static bool _debugMode = false;
        private static string? _logPathOverride = null;

        // Web mode: tracking, alerts, and SSE infrastructure
        private static VesselTrackingService? _trackingService;
        private static AlertService? _alertService;
        private static readonly ConcurrentDictionary<string, Channel<string>> _sseClients = new();
        
        static async Task Main(string[] args)
        {
            // Parse command line arguments
            if (args.Contains("--help") || args.Contains("-h"))
            {
                ShowHelp();
                return;
            }
            
            _debugMode = args.Contains("--debug") || args.Contains("-d");
            bool webMode = args.Contains("--web") || args.Contains("-w");
            bool headlessMode = args.Contains("--headless");
            bool noWs = args.Contains("--no-ws");
            bool noTcp = args.Contains("--no-tcp");
            bool noUdp = args.Contains("--no-udp");
            
            // Parse --log-path <directory>
            var logPathIdx = Array.IndexOf(args, "--log-path");
            if (logPathIdx >= 0 && logPathIdx < args.Length - 1)
            {
                _logPathOverride = args[logPathIdx + 1];
            }
            
            if (webMode)
            {
                Console.WriteLine("🚢 AIS to NMEA 0183 Converter");
                Console.WriteLine("===============================");
                await RunWebModeAsync(args);
            }
            else if (headlessMode)
            {
                Console.WriteLine("🚢 AIS to NMEA 0183 Converter");
                Console.WriteLine("===============================");
                await RunConsoleModeAsync(args);
            }
            else
            {
                await RunTuiModeAsync(args, autoStartWs: !noWs, autoStartTcp: !noTcp, autoStartUdp: !noUdp);
            }
        }
        
        private static void ShowHelp()
        {
            Console.WriteLine("🚢 AIS to NMEA 0183 Converter");
            Console.WriteLine("===============================");
            Console.WriteLine();
            Console.WriteLine("Usage: dotnet run [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -w, --web         Enable web UI mode (default: http://localhost:8080 or AIS_WEB_PORT env)");
            Console.WriteLine("  -d, --debug       Enable debug mode (shows all received and broadcast messages)");
            Console.WriteLine("  --log-path <dir>  Override log file directory (default: ./log/ or ApplicationLogging.LogPath)");
            Console.WriteLine("  -h, --help        Show this help message");
            Console.WriteLine("  --headless        Run in headless console mode (no TUI, log streamed to stdout)");
            Console.WriteLine("  --no-ws           Don't auto-start WebSocket connection");
            Console.WriteLine("  --no-tcp          Don't auto-start TCP server");
            Console.WriteLine("  --no-udp          Don't auto-start UDP server");
            Console.WriteLine("  --public, --external  Bind web UI to 0.0.0.0 for external subnet access (use with --web)");
            Console.WriteLine();
            Console.WriteLine("TUI mode (default):");
            Console.WriteLine("  • Interactive terminal UI with command input");
            Console.WriteLine("  • Type / to see available commands");
            Console.WriteLine("  • Ctrl+C twice to exit");
            Console.WriteLine();
            Console.WriteLine("Headless mode:");
            Console.WriteLine("  • Auto-starts all configured services");
            Console.WriteLine("  • Log streamed to stdout");
            Console.WriteLine("  • Runs until Ctrl+C is pressed");
            Console.WriteLine();
            Console.WriteLine("Web mode:");
            Console.WriteLine("  • Provides web UI for service control");
            Console.WriteLine("  • Access at http://localhost:8080 (or configured port)");
            Console.WriteLine("  • Manually start/stop services via UI");
            Console.WriteLine("  • Configure bounding box via UI");
            Console.WriteLine();
            Console.WriteLine("Debug mode features:");
            Console.WriteLine("  • Shows all received AIS messages");
            Console.WriteLine("  • Shows all converted NMEA 0183 messages");
            Console.WriteLine("  • Statistics reports every 30 seconds");
            Console.WriteLine("  • Detailed message type breakdowns");
            Console.WriteLine();
        }

        private static async Task RunTuiModeAsync(string[] args, bool autoStartWs = true, bool autoStartTcp = true, bool autoStartUdp = true)
        {
            try
            {
                // Load configuration
                var config = await LoadConfigurationSilentAsync();
                if (config == null)
                {
                    // Fall back to console error
                    Console.WriteLine("❌ Failed to load configuration. Run with --headless to see details.");
                    return;
                }

                var tuiApp = new TuiApp(config, _debugMode, autoStartWs, autoStartTcp, autoStartUdp, _logPathOverride);
                await tuiApp.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fatal error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private static int GetConfiguredWebPortOrDefault()
        {
            var env = Environment.GetEnvironmentVariable("AIS_WEB_PORT");
            return (!string.IsNullOrEmpty(env) && int.TryParse(env, out var p) && p > 0 && p < 65536) ? p : 8080;
        }

        private static string GetLocalIpAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(ip))
                        return ip.ToString();
                }
            }
            catch { }
            return "localhost";
        }

        private static bool IsPortInUse(int port)
        {
            try
            {
                var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return false;
            }
            catch
            {
                return true;
            }
        }

        private static async Task RunWebModeAsync(string[] args)
        {
            bool externalAccess = args.Contains("--public") || args.Contains("--external");
            // Compute project root from build output (bin/Debug/net9.0) for static file serving
            var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = projectRoot,
                WebRootPath = Path.Combine(projectRoot, "wwwroot")
            });
            // Determine web UI port (configurable via env AIS_WEB_PORT) default 8080
            var webPortEnv = Environment.GetEnvironmentVariable("AIS_WEB_PORT");
            int webPort = 8080;
            if (!string.IsNullOrEmpty(webPortEnv) && int.TryParse(webPortEnv, out var parsed) && parsed > 0 && parsed < 65536)
            {
                webPort = parsed;
            }
            // Pre-flight port availability check (only in web mode)
            if (IsPortInUse(webPort))
            {
                Console.WriteLine($"❌ Web UI port {webPort} is already in use. Set AIS_WEB_PORT to a free port or stop conflicting process.");
                Environment.Exit(1);
            }
            var bindHost = externalAccess ? "0.0.0.0" : "localhost";
            builder.WebHost.UseUrls($"http://{bindHost}:{webPort}");
            
            // Load configuration
            var config = await LoadConfigurationAsync();
            if (config == null)
            {
                return;
            }
            _config = config;
            
            // Initialize ServiceManager
            _serviceManager = new ServiceManager(_config, _debugMode, _logPathOverride);
            await _serviceManager.InitializeAsync();
            
            // Initialize tracking and alert services for web mode
            var registryStore = new VesselRegistryStore();
            var trackingStore = new TrackingStore();
            _trackingService = new VesselTrackingService(registryStore, trackingStore);
            var alertStore = new AlertStore();
            _alertService = new AlertService(alertStore);

            // Wire vessel data from ServiceManager into tracking + alerts (same pattern as TUI)
            _serviceManager.VesselDataForTracking += (sender, data) =>
            {
                _trackingService.ProcessVesselData(data);
                if (!string.IsNullOrWhiteSpace(data.VesselName))
                    _alertService.UpdateAlertName(data.Mmsi, data.VesselName);
                bool fired = _alertService.CheckAndFire(data.Mmsi, data.VesselName);
                if (fired)
                    BroadcastSseEvent("alert-fired", new { mmsi = data.Mmsi, name = data.VesselName ?? data.Mmsi.ToString() });
            };

            // Wire name lookup so broadcast messages can resolve vessel names
            _serviceManager.VesselNameLookup = mmsi => _trackingService.LookupName(mmsi);

            // Wire tracking updates to SSE
            _trackingService.TrackingUpdated += (s, e) => BroadcastSseEvent("tracking-updated", BuildTrackingStatus());
            _trackingService.PendingTrackingActivated += (s, e) => BroadcastSseEvent("tracking-activated", new { mmsi = e.Mmsi, name = e.Name });

            // Wire status changes to SSE
            _serviceManager.StatusChanged += (s, msg) => BroadcastSseEvent("status-changed", new { message = msg });
            
            // Add services to the container
            builder.Services.AddSingleton(_serviceManager);
            
            var app = builder.Build();

            // Ensure graceful shutdown of background services in web mode
            var lifetime = app.Lifetime;
            bool servicesDisposed = false;
            lifetime.ApplicationStopping.Register(() =>
            {
                // Flush tracking/alert stores
                try { _trackingService?.FlushRegistry(); } catch { }

                if (!servicesDisposed && _serviceManager != null)
                {
                    try
                    {
                        _serviceManager.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Error disposing services during web shutdown: {ex.Message}");
                    }
                    finally
                    {
                        servicesDisposed = true;
                    }
                }
            });
            
            // Serve static files - order matters!
            app.UseDefaultFiles();
            app.UseStaticFiles();
            
            // API Endpoints
            app.MapGet("/api/status", () =>
            {
                var stats = _serviceManager.Statistics;
                var typeCounts = stats?.GetMessageTypeCounts() ?? new Dictionary<int, int>();
                var messageTypes = typeCounts.OrderBy(kv => kv.Key).Select(kv => new
                {
                    type = kv.Key,
                    name = StatisticsService.GetMessageTypeDisplayName(kv.Key),
                    count = kv.Value
                });

                return Results.Json(new
                {
                    webSocket = new
                    {
                        isConnected = _serviceManager.IsWebSocketConnected,
                        url = _config.WebSocketUrl
                    },
                    tcp = new
                    {
                        isRunning = _serviceManager.IsTcpServerRunning,
                        host = _config.Network.Tcp.Host,
                        port = _config.Network.Tcp.Port
                    },
                    udp = new
                    {
                        isRunning = _serviceManager.IsUdpServerRunning,
                        host = _config.Network.Udp.Host,
                        port = _config.Network.Udp.Port
                    },
                    statistics = new
                    {
                        totalReceived = stats?.TotalMessagesReceived ?? 0,
                        totalConverted = stats?.TotalMessagesConverted ?? 0,
                        totalBroadcast = stats?.TotalMessagesBroadcast ?? 0,
                        errors = stats?.TotalErrors ?? 0,
                        messageTypes = messageTypes
                    },
                    config = new
                    {
                        north = _config.BoundingBox.North,
                        south = _config.BoundingBox.South,
                        east = _config.BoundingBox.East,
                        west = _config.BoundingBox.West
                    }
                });
            });

            app.MapPost("/api/websocket/start", async () =>
            {
                var result = await _serviceManager!.StartWebSocketAsync();
                return Results.Json(new { success = result, message = result ? "Connected" : "Failed to connect" });
            });

            app.MapPost("/api/websocket/stop", async () =>
            {
                await _serviceManager!.StopWebSocketAsync();
                return Results.Json(new { success = true, message = "Disconnected" });
            });

            app.MapPost("/api/tcp/start", async () =>
            {
                var result = await _serviceManager!.StartTcpServerAsync();
                return Results.Json(new { success = result, message = result ? "Started" : "Failed to start" });
            });

            app.MapPost("/api/tcp/stop", async () =>
            {
                await _serviceManager!.StopTcpServerAsync();
                return Results.Json(new { success = true, message = "Stopped" });
            });

            app.MapPost("/api/udp/start", async () =>
            {
                var result = await _serviceManager!.StartUdpServerAsync();
                return Results.Json(new { success = result, message = result ? "Started" : "Failed to start" });
            });

            app.MapPost("/api/udp/stop", async () =>
            {
                await _serviceManager!.StopUdpServerAsync();
                return Results.Json(new { success = true, message = "Stopped" });
            });

            app.MapPost("/api/config/update", async (HttpContext context) =>
            {
                try
                {
                    var config = await context.Request.ReadFromJsonAsync<BoundingBox>();
                    if (config != null)
                    {
                        await _serviceManager!.UpdateConfigurationAsync(config);
                        return Results.Json(new { success = true, message = "Configuration updated" });
                    }
                    return Results.Json(new { success = false, message = "Invalid configuration" });
                }
                catch (System.Text.Json.JsonException jsonEx)
                {
                    return Results.Json(new { success = false, message = "JSON parse error: " + jsonEx.Message });
                }
                catch (InvalidOperationException invOpEx)
                {
                    return Results.Json(new { success = false, message = "Invalid operation: " + invOpEx.Message });
                }
                catch (ArgumentException argEx)
                {
                    return Results.Json(new { success = false, message = "Argument error: " + argEx.Message });
                }
                catch (Exception ex) when (!(ex is OutOfMemoryException) && !(ex is StackOverflowException))
                {
                    return Results.Json(new { success = false, message = "Unexpected error: " + ex.Message });
                }
            });

            // --- Tracking endpoints ---

            app.MapGet("/api/tracking/status", () =>
            {
                return Results.Json(BuildTrackingStatus());
            });

            app.MapPost("/api/tracking/start", async (HttpContext context) =>
            {
                try
                {
                    var body = await context.Request.ReadFromJsonAsync<TrackingRequest>();
                    if (body == null || string.IsNullOrWhiteSpace(body.Query))
                        return Results.Json(new { success = false, message = "Missing 'query' field (vessel name or MMSI)" });

                    var query = body.Query.Trim();

                    // Try MMSI first
                    if (int.TryParse(query, out var mmsi))
                    {
                        var name = _trackingService!.LookupName(mmsi);
                        if (name != null)
                            _trackingService.StartTracking(mmsi, name);
                        else
                            _trackingService.StartPendingTracking(mmsi);
                        return Results.Json(new { success = true, message = $"Tracking MMSI {mmsi}", mmsi, pending = name == null });
                    }

                    // Search by name
                    var matches = _trackingService!.FindVessels(query);
                    if (matches.Count == 0)
                        return Results.Json(new { success = false, message = $"No vessels found matching '{query}'" });
                    if (matches.Count > 1)
                        return Results.Json(new { success = false, message = $"Multiple matches ({matches.Count}). Be more specific.", matches = matches.Select(m => new { m.Mmsi, m.Name }) });

                    var match = matches[0];
                    _trackingService.StartTracking(match.Mmsi, match.Name);
                    return Results.Json(new { success = true, message = $"Tracking {match.Name}", mmsi = match.Mmsi, pending = false });
                }
                catch (Exception ex) when (!(ex is OutOfMemoryException) && !(ex is StackOverflowException))
                {
                    return Results.Json(new { success = false, message = ex.Message });
                }
            });

            app.MapPost("/api/tracking/stop", () =>
            {
                _trackingService!.StopTracking();
                return Results.Json(new { success = true, message = "Tracking stopped" });
            });

            app.MapGet("/api/tracking/targets", () =>
            {
                var targets = _trackingService!.GetAllTargets()
                    .Select(t => new { t.Mmsi, t.Name, lastHeard = t.LastHeard.ToString("o") });
                return Results.Json(new { targets, count = targets.Count() });
            });

            // --- Alert endpoints ---

            app.MapGet("/api/alerts", () =>
            {
                var alerts = _alertService!.GetAlerts()
                    .Select(a => new { a.Mmsi, a.Name, createdAt = a.CreatedAt.ToString("o") });
                return Results.Json(new { alerts, count = alerts.Count() });
            });

            app.MapPost("/api/alerts", async (HttpContext context) =>
            {
                try
                {
                    var body = await context.Request.ReadFromJsonAsync<AlertRequest>();
                    if (body == null || string.IsNullOrWhiteSpace(body.Query))
                        return Results.Json(new { success = false, message = "Missing 'query' field" });

                    var query = body.Query.Trim();

                    if (int.TryParse(query, out var mmsi))
                    {
                        var name = _trackingService!.LookupName(mmsi);
                        var added = _alertService!.AddAlert(mmsi, name);
                        return Results.Json(new { success = added, message = added ? $"Alert added for MMSI {mmsi}" : "Already alerted", mmsi });
                    }

                    var matches = _trackingService!.FindVessels(query);
                    if (matches.Count == 0)
                        return Results.Json(new { success = false, message = $"No vessels found matching '{query}'" });
                    if (matches.Count > 1)
                        return Results.Json(new { success = false, message = $"Multiple matches ({matches.Count}). Be more specific.", matches = matches.Select(m => new { m.Mmsi, m.Name }) });

                    var match = matches[0];
                    var result = _alertService!.AddAlert(match.Mmsi, match.Name);
                    return Results.Json(new { success = result, message = result ? $"Alert added for {match.Name}" : "Already alerted", mmsi = match.Mmsi });
                }
                catch (Exception ex) when (!(ex is OutOfMemoryException) && !(ex is StackOverflowException))
                {
                    return Results.Json(new { success = false, message = ex.Message });
                }
            });

            app.MapDelete("/api/alerts/{mmsi:int}", (int mmsi) =>
            {
                var removed = _alertService!.RemoveAlert(mmsi);
                return Results.Json(new { success = removed, message = removed ? "Alert removed" : "Alert not found" });
            });

            app.MapDelete("/api/alerts", () =>
            {
                _alertService!.ClearAlerts();
                return Results.Json(new { success = true, message = "All alerts cleared" });
            });

            // --- Export endpoints ---

            app.MapGet("/api/export/gpx", () =>
            {
                if (!_trackingService!.IsTracking || _trackingService.TrackedMmsi == null)
                    return Results.Json(new { success = false, message = "No active tracking" });

                var points = _trackingService.GetTrackPoints();
                if (points.Count == 0)
                    return Results.Json(new { success = false, message = "No track points recorded yet" });

                var gpx = GpxExporter.Export(points, _trackingService.TrackedVesselName ?? "Unknown", _trackingService.TrackedMmsi.Value);
                return Results.Text(gpx, "application/gpx+xml");
            });

            app.MapGet("/api/export/kml", () =>
            {
                if (!_trackingService!.IsTracking || _trackingService.TrackedMmsi == null)
                    return Results.Json(new { success = false, message = "No active tracking" });

                var points = _trackingService.GetTrackPoints();
                if (points.Count == 0)
                    return Results.Json(new { success = false, message = "No track points recorded yet" });

                var kml = KmlExporter.Export(points, _trackingService.TrackedVesselName ?? "Unknown", _trackingService.TrackedMmsi.Value);
                return Results.Text(kml, "application/vnd.google-earth.kml+xml");
            });

            // --- Settings endpoints ---

            app.MapGet("/api/settings", () =>
            {
                return Results.Json(new
                {
                    units = _trackingService!.Units.ToString().ToLowerInvariant()
                });
            });

            app.MapPost("/api/settings/units", async (HttpContext context) =>
            {
                try
                {
                    var body = await context.Request.ReadFromJsonAsync<UnitsRequest>();
                    if (body == null || string.IsNullOrWhiteSpace(body.Units))
                        return Results.Json(new { success = false, message = "Missing 'units' field" });

                    var unitsValue = body.Units.ToLowerInvariant() switch
                    {
                        "metric" => (DisplayUnits?)DisplayUnits.Metric,
                        "nautical" or "knots" => (DisplayUnits?)DisplayUnits.Nautical,
                        _ => null
                    };
                    if (unitsValue == null)
                        return Results.Json(new { success = false, message = $"Unsupported units '{body.Units}'. Valid values: metric, nautical, knots" });

                    _trackingService!.Units = unitsValue.Value;
                    BroadcastSseEvent("settings-changed", new { units = _trackingService.Units.ToString().ToLowerInvariant() });
                    return Results.Json(new { success = true, units = _trackingService.Units.ToString().ToLowerInvariant() });
                }
                catch (Exception ex) when (!(ex is OutOfMemoryException) && !(ex is StackOverflowException))
                {
                    return Results.Json(new { success = false, message = ex.Message });
                }
            });

            // --- SSE endpoint ---

            app.MapGet("/api/events", async (HttpContext context) =>
            {
                context.Response.ContentType = "text/event-stream";
                context.Response.Headers["Cache-Control"] = "no-cache";
                context.Response.Headers["Connection"] = "keep-alive";

                var clientId = Guid.NewGuid().ToString();
                var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(100)
                {
                    FullMode = BoundedChannelFullMode.DropOldest
                });
                _sseClients[clientId] = channel;

                try
                {
                    // Send initial state
                    await context.Response.WriteAsync($"event: connected\ndata: {{\"clientId\":\"{clientId}\"}}\n\n", context.RequestAborted);
                    await context.Response.Body.FlushAsync(context.RequestAborted);

                    await foreach (var msg in channel.Reader.ReadAllAsync(context.RequestAborted))
                    {
                        await context.Response.WriteAsync(msg, context.RequestAborted);
                        await context.Response.Body.FlushAsync(context.RequestAborted);
                    }
                }
                catch (OperationCanceledException) { }
                finally
                {
                    _sseClients.TryRemove(clientId, out _);
                }
            });
            
            Console.WriteLine($"✅ Web UI mode enabled");
            var displayHost = externalAccess ? GetLocalIpAddress() : "localhost";
            Console.WriteLine($"🌐 Open browser to: http://{displayHost}:{GetConfiguredWebPortOrDefault()}");
            if (externalAccess)
            {
                Console.WriteLine("⚠️ External access enabled - ensure you trust the network and have set any necessary firewall rules.");
            }
            Console.WriteLine($"📱 Press Ctrl+C to stop...");
            
            await app.RunAsync();
        }

        private static async Task RunConsoleModeAsync(string[] args)
        {
            if (_debugMode)
            {
                Console.WriteLine("🐛 Debug mode enabled - showing all received and broadcast messages");
            }

            try
            {
                // Load configuration
                var config = await LoadConfigurationAsync();
                if (config == null)
                {
                    return;
                }
                _config = config;

                // Initialize ServiceManager
                _serviceManager = new ServiceManager(_config, _debugMode, _logPathOverride);
                await _serviceManager.InitializeAsync();

                // Start all services automatically in console mode
                await _serviceManager.StartTcpServerAsync();
                await _serviceManager.StartUdpServerAsync();
                await _serviceManager.StartWebSocketAsync();

                // Keep application running
                Console.WriteLine("📱 Press Ctrl+C to stop...");
                Console.CancelKeyPress += OnCancelKeyPress;

                // Wait indefinitely
                await Task.Delay(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fatal error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                if (_serviceManager != null)
                {
                    await _serviceManager.DisposeAsync();
                }
            }
        }

        private static async Task<AppConfig?> LoadConfigurationAsync()
        {
            try
            {
                // Use the directory where the executable is located, not the current working directory
                var basePath = AppContext.BaseDirectory;
                
                var configBuilder = new ConfigurationBuilder()
                    .SetBasePath(basePath)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddUserSecrets("ais-to-n2k-secrets")
                    .AddEnvironmentVariables();

                var configuration = configBuilder.Build();
                var config = new AppConfig();
                configuration.Bind(config);

                // Get API key from secure sources
                var secureConfigService = new SecureConfigurationService(configuration);
                config.ApiKey = secureConfigService.GetApiKey();

                if (string.IsNullOrEmpty(config.ApiKey))
                {
                    Console.WriteLine("❌ API key not found. Please set it using:");
                    Console.WriteLine("   dotnet user-secrets set \"AisApi:ApiKey\" \"your-api-key-here\"");
                    Console.WriteLine("   OR set the AIS_API_KEY environment variable");
                    return null;
                }

                Console.WriteLine($"✅ Configuration loaded from: {basePath}");
                Console.WriteLine($"📡 WebSocket URL: {config.WebSocketUrl}");
                Console.WriteLine($"🌐 Bounding Box: N:{config.BoundingBox.North}, S:{config.BoundingBox.South}, E:{config.BoundingBox.East}, W:{config.BoundingBox.West}");
                Console.WriteLine($"🔌 TCP Server: {(config.Network.EnableTcp ? $"Enabled on {config.Network.Tcp.Host}:{config.Network.Tcp.Port}" : "Disabled")}");
                Console.WriteLine($"📡 UDP Broadcast: {(config.Network.EnableUdp ? $"Enabled to {config.Network.Udp.Host}:{config.Network.Udp.Port}" : "Disabled")}");

                // Validate configuration
                var validationErrors = config.Validate();
                if (validationErrors.Any())
                {
                    Console.WriteLine("❌ Configuration validation failed:");
                    foreach (var error in validationErrors)
                    {
                        Console.WriteLine($"   • {error}");
                    }
                    Console.WriteLine();
                    Console.WriteLine("Please check your appsettings.json file and ensure all required network configuration is provided.");
                    return null;
                }

                return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load configuration: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Load configuration without Console output (for TUI mode where Terminal.Gui owns the screen).
        /// </summary>
        private static async Task<AppConfig?> LoadConfigurationSilentAsync()
        {
            try
            {
                var basePath = AppContext.BaseDirectory;
                
                var configBuilder = new ConfigurationBuilder()
                    .SetBasePath(basePath)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddUserSecrets("ais-to-n2k-secrets")
                    .AddEnvironmentVariables();

                var configuration = configBuilder.Build();
                var config = new AppConfig();
                configuration.Bind(config);

                var secureConfigService = new SecureConfigurationService(configuration);
                config.ApiKey = secureConfigService.GetApiKey();

                // Don't fail on missing API key — TUI will show the error in the command pane
                // Validate configuration
                var validationErrors = config.Validate();
                if (validationErrors.Any())
                {
                    // Still return config — TUI can display errors
                }

                return config;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static async void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true; // Prevent immediate termination
            Console.WriteLine("\n🛑 Shutting down gracefully...");
            if (_serviceManager != null)
            {
                await _serviceManager.DisposeAsync();
            }
            Environment.Exit(0);
        }

        // --- SSE helpers ---

        private static void BroadcastSseEvent(string eventType, object data)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            var message = $"event: {eventType}\ndata: {json}\n\n";
            foreach (var kvp in _sseClients)
            {
                kvp.Value.Writer.TryWrite(message);
            }
        }

        // --- Tracking status builder ---

        private static object BuildTrackingStatus()
        {
            if (_trackingService == null || !_trackingService.IsTracking)
                return new { isTracking = false };

            var points = _trackingService.GetTrackPoints();
            var lastPoint = points.LastOrDefault();
            return new
            {
                isTracking = true,
                isPending = _trackingService.IsPendingTracking,
                mmsi = _trackingService.TrackedMmsi,
                vesselName = _trackingService.TrackedVesselName,
                distance = _trackingService.FormatDistance(),
                currentSpeed = _trackingService.FormatCurrentSpeed(),
                averageSpeed = _trackingService.FormatAverageSpeed(),
                trackPoints = points.Count,
                startTime = _trackingService.TrackingStartTime?.ToString("o"),
                lastPosition = lastPoint != null ? new
                {
                    latitude = lastPoint.Latitude,
                    longitude = lastPoint.Longitude,
                    timestamp = lastPoint.Timestamp.ToString("o")
                } : null
            };
        }

        // --- Request DTOs ---

        private record TrackingRequest(string? Query);
        private record AlertRequest(string? Query);
        private record UnitsRequest(string? Units);
    }
}
