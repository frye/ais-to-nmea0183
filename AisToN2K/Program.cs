using AisToN2K.Configuration;
using AisToN2K.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AisToN2K
{
    class Program
    {
        private static ServiceManager? _serviceManager;
        private static AppConfig? _config;
        private static bool _debugMode = false;
        
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
            
            Console.WriteLine("🚢 AIS to NMEA 0183 Converter");
            Console.WriteLine("===============================");
            
            if (webMode)
            {
                await RunWebModeAsync(args);
            }
            else
            {
                await RunConsoleModeAsync(args);
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
            Console.WriteLine("  -w, --web      Enable web UI mode (default: http://localhost:8080 or AIS_WEB_PORT env)");
            Console.WriteLine("  -d, --debug    Enable debug mode (shows all received and broadcast messages)");
            Console.WriteLine("  -h, --help     Show this help message");
            Console.WriteLine();
            Console.WriteLine("Console mode (default):");
            Console.WriteLine("  • Auto-starts all configured services");
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

        private static int GetConfiguredWebPortOrDefault()
        {
            var env = Environment.GetEnvironmentVariable("AIS_WEB_PORT");
            return (!string.IsNullOrEmpty(env) && int.TryParse(env, out var p) && p > 0 && p < 65536) ? p : 8080;
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
            builder.WebHost.UseUrls($"http://localhost:{webPort}");
            
            // Load configuration
            var config = await LoadConfigurationAsync();
            if (config == null)
            {
                return;
            }
            _config = config;
            
            // Initialize ServiceManager
            _serviceManager = new ServiceManager(_config, _debugMode);
            await _serviceManager.InitializeAsync();
            
            // Add services to the container
            builder.Services.AddSingleton(_serviceManager);
            
            var app = builder.Build();
            
            // Serve static files - order matters!
            app.UseDefaultFiles();
            app.UseStaticFiles();
            
            // API Endpoints
            app.MapGet("/api/status", () =>
            {
                var stats = _serviceManager.Statistics;
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
                        errors = stats?.TotalErrors ?? 0
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
            
            Console.WriteLine($"✅ Web UI mode enabled");
            Console.WriteLine($"🌐 Open browser to: http://localhost:{GetConfiguredWebPortOrDefault()}");
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
                _serviceManager = new ServiceManager(_config, _debugMode);
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
    }
}
