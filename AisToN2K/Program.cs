using AisToN2K.Configuration;
using AisToN2K.Models;
using AisToN2K.Services;
using Microsoft.Extensions.Configuration;

namespace AisToN2K
{
    class Program
    {
        private static AppConfig? _config;
        private static AisWebSocketService? _webSocketService;
        private static Nmea0183Converter? _converter;
        private static TcpServer? _tcpServer;
        private static UdpServer? _udpServer;
        private static StatisticsService? _statistics;
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
            
            if (_debugMode)
            {
                Console.WriteLine("🐛 Debug mode enabled - showing all received and broadcast messages");
            }
            
            Console.WriteLine("🚢 AIS to NMEA 0183 Converter");
            Console.WriteLine("===============================");

            try
            {
                // Load configuration
                if (!await LoadConfigurationAsync())
                {
                    return;
                }

                // Initialize services
                await InitializeServicesAsync();

                // Start servers
                await StartServersAsync();

                // Start AIS streaming
                await StartAisStreamingAsync();

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
                await CleanupAsync();
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
            Console.WriteLine("  -d, --debug    Enable debug mode (shows all received and broadcast messages)");
            Console.WriteLine("  -h, --help     Show this help message");
            Console.WriteLine();
            Console.WriteLine("Debug mode features:");
            Console.WriteLine("  • Shows all received AIS messages");
            Console.WriteLine("  • Shows all converted NMEA 0183 messages");
            Console.WriteLine("  • Statistics reports every 30 seconds");
            Console.WriteLine("  • Detailed message type breakdowns");
            Console.WriteLine();
        }

        private static async Task<bool> LoadConfigurationAsync()
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
                _config = new AppConfig();
                configuration.Bind(_config);

                // Get API key from secure sources
                var secureConfigService = new SecureConfigurationService(configuration);
                _config.ApiKey = secureConfigService.GetApiKey();

                if (string.IsNullOrEmpty(_config.ApiKey))
                {
                    Console.WriteLine("❌ API key not found. Please set it using:");
                    Console.WriteLine("   dotnet user-secrets set \"AisApi:ApiKey\" \"your-api-key-here\"");
                    Console.WriteLine("   OR set the AIS_API_KEY environment variable");
                    return false;
                }

                Console.WriteLine($"✅ Configuration loaded from: {basePath}");
                Console.WriteLine($"📡 WebSocket URL: {_config.WebSocketUrl}");
                Console.WriteLine($"🌐 Bounding Box: N:{_config.BoundingBox.North}, S:{_config.BoundingBox.South}, E:{_config.BoundingBox.East}, W:{_config.BoundingBox.West}");
                Console.WriteLine($"🔌 TCP Server: {(_config.Network.EnableTcp ? $"Enabled on {_config.Network.Tcp.Host}:{_config.Network.Tcp.Port}" : "Disabled")}");
                Console.WriteLine($"📡 UDP Broadcast: {(_config.Network.EnableUdp ? $"Enabled to {_config.Network.Udp.Host}:{_config.Network.Udp.Port}" : "Disabled")}");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load configuration: {ex.Message}");
                return false;
            }
        }
        private static async Task InitializeServicesAsync()
        {
            // Initialize statistics service with debug mode and 30-second reporting
            _statistics = new StatisticsService(_debugMode, 30);

            // Initialize NMEA converter
            _converter = new Nmea0183Converter(_debugMode);

            // Initialize WebSocket service
            _webSocketService = new AisWebSocketService(_config!.WebSocketUrl, _config.ApiKey, _debugMode);
            _webSocketService.VesselDataReceived += OnVesselDataReceived;

            Console.WriteLine("✅ Services initialized");
        }

        private static async Task StartServersAsync()
        {
            // Start TCP server if enabled
            if (_config!.Network.EnableTcp)
            {
                _tcpServer = new TcpServer(_config.Network.Tcp.Host, _config.Network.Tcp.Port, _debugMode);
                var tcpStarted = await _tcpServer.StartAsync();
                if (!tcpStarted)
                {
                    Console.WriteLine("⚠️ TCP server failed to start");
                }
            }

            // Start UDP server if enabled
            if (_config.Network.EnableUdp)
            {
                _udpServer = new UdpServer(_config.Network.Udp.Host, _config.Network.Udp.Port);
                var udpStarted = await _udpServer.StartAsync();
                if (!udpStarted)
                {
                    Console.WriteLine("⚠️ UDP server failed to start");
                }
            }

            Console.WriteLine("✅ Network servers started");
        }

        private static async Task StartAisStreamingAsync()
        {
            try
            {
                var boundingBox = new double[]
                {
                    _config!.BoundingBox.North,
                    _config.BoundingBox.West,
                    _config.BoundingBox.South,
                    _config.BoundingBox.East
                };

                await _webSocketService!.ConnectAsync(boundingBox);

                Console.WriteLine("✅ AIS streaming started");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to start AIS streaming: {ex.Message}");
                _statistics?.IncrementError();
            }
        }

        private static async void OnVesselDataReceived(object? sender, AisData vesselData)
        {
            try
            {
                // Extract vessel information from AisData object
                string vesselName = vesselData.VesselName ?? vesselData.Mmsi.ToString();
                double latitude = vesselData.Latitude;
                double longitude = vesselData.Longitude;
                int messageType = vesselData.MessageType;

                _statistics?.IncrementMessageReceived(messageType);

                // Debug logging for received vessel data
                if (_debugMode)
                {
                    Console.WriteLine($"📥 RX: Type {messageType} | MMSI: {vesselData.Mmsi} | {vesselName} | {latitude:F4}, {longitude:F4}");
                }

                // Show occasional progress indicators when not in debug mode
                if (!_debugMode && _statistics!.TotalMessagesReceived % 10 == 0)
                {
                    Console.WriteLine($"📊 Processed {_statistics.TotalMessagesReceived} messages (Type {messageType}: {vesselName})");
                }

                // Convert to NMEA 0183
                var nmeaMessage = await _converter!.ConvertToNmea0183Async(vesselData);
                if (string.IsNullOrEmpty(nmeaMessage))
                {
                    if (_debugMode)
                    {
                        Console.WriteLine($"⚠️ Failed to convert message type {messageType}");
                    }
                    _statistics?.IncrementError();
                    return;
                }

                _statistics?.IncrementMessageConverted();

                // Debug logging for converted NMEA message
                if (_debugMode)
                {
                    Console.WriteLine($"📤 TX: {nmeaMessage.Trim()}");
                }

                // Log message details if enabled
                if (_config!.ApplicationLogging.LogNmeaMessages)
                {
                    _statistics?.LogMessageDetails(messageType, vesselName, latitude, longitude, nmeaMessage);
                }

                // Broadcast via TCP
                if (_tcpServer != null)
                {
                    var tcpSent = await _tcpServer.BroadcastMessageAsync(nmeaMessage + "\r\n");
                    if (tcpSent)
                    {
                        _statistics?.IncrementMessageBroadcast();
                    }
                }

                // Broadcast via UDP
                if (_udpServer != null)
                {
                    var udpSent = await _udpServer.BroadcastMessageAsync(nmeaMessage + "\r\n");
                    if (udpSent)
                    {
                        _statistics?.IncrementMessageBroadcast();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error processing vessel data: {ex.Message}");
                _statistics?.IncrementError();
            }
        }

        private static async void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true; // Prevent immediate termination
            Console.WriteLine("\n🛑 Shutting down gracefully...");
            await CleanupAsync();
            Environment.Exit(0);
        }

        private static async Task CleanupAsync()
        {
            try
            {
                _statistics?.PrintSummary();

                if (_webSocketService != null)
                {
                    await _webSocketService.DisconnectAsync();
                    _webSocketService.Dispose();
                }

                if (_tcpServer != null)
                {
                    await _tcpServer.StopAsync();
                    _tcpServer.Dispose();
                }

                if (_udpServer != null)
                {
                    await _udpServer.StopAsync();
                    _udpServer.Dispose();
                }

                _statistics?.Dispose();

                Console.WriteLine("✅ Cleanup completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error during cleanup: {ex.Message}");
            }
        }
    }
}
