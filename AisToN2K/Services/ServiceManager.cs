using AisToN2K.Configuration;
using AisToN2K.Models;

namespace AisToN2K.Services
{
    /// <summary>
    /// Manages the lifecycle of all AIS services (WebSocket, TCP, UDP, Converter, Statistics)
    /// </summary>
    public class ServiceManager : IDisposable
    {
        private AppConfig _config;
        private AisWebSocketService? _webSocketService;
        private Nmea0183Converter? _converter;
        private TcpServer? _tcpServer;
        private UdpServer? _udpServer;
        private StatisticsService? _statistics;
        private readonly bool _debugMode;
        private bool _disposed = false;

        public bool IsWebSocketConnected => _webSocketService?.IsConnected ?? false;
        public bool IsTcpServerRunning { get; private set; }
        public bool IsUdpServerRunning { get; private set; }
        public AppConfig CurrentConfig => _config;
        public StatisticsService? Statistics => _statistics;

        public event EventHandler<string>? StatusChanged;

        public ServiceManager(AppConfig config, bool debugMode = false)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _debugMode = debugMode;
        }

        public async Task InitializeAsync()
        {
            // Initialize statistics service with debug mode and 30-second reporting
            _statistics = new StatisticsService(_debugMode, 30);

            // Initialize NMEA converter
            _converter = new Nmea0183Converter(_debugMode);

            StatusChanged?.Invoke(this, "Services initialized");
        }

        public async Task<bool> StartWebSocketAsync()
        {
            if (_webSocketService != null && _webSocketService.IsConnected)
            {
                return true; // Already connected
            }

            try
            {
                // Initialize WebSocket service
                _webSocketService = new AisWebSocketService(_config.WebSocketUrl, _config.ApiKey, _debugMode);
                _webSocketService.VesselDataReceived += OnVesselDataReceived;

                var boundingBox = new double[]
                {
                    _config.BoundingBox.North,
                    _config.BoundingBox.West,
                    _config.BoundingBox.South,
                    _config.BoundingBox.East
                };

                var connected = await _webSocketService.ConnectAsync(boundingBox);
                if (connected)
                {
                    StatusChanged?.Invoke(this, "WebSocket connected");
                }
                return connected;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"WebSocket connection failed: {ex.Message}");
                _statistics?.IncrementError();
                return false;
            }
        }

        public async Task StopWebSocketAsync()
        {
            if (_webSocketService != null)
            {
                await _webSocketService.DisconnectAsync();
                _webSocketService.Dispose();
                _webSocketService = null;
                StatusChanged?.Invoke(this, "WebSocket disconnected");
            }
        }

        public async Task<bool> StartTcpServerAsync()
        {
            if (!_config.Network.EnableTcp)
            {
                return false;
            }

            if (_tcpServer != null)
            {
                return true; // Already running
            }

            try
            {
                _tcpServer = new TcpServer(_config.Network.Tcp.Host, _config.Network.Tcp.Port, _debugMode);
                var started = await _tcpServer.StartAsync();
                IsTcpServerRunning = started;
                if (started)
                {
                    StatusChanged?.Invoke(this, $"TCP server started on {_config.Network.Tcp.Host}:{_config.Network.Tcp.Port}");
                }
                return started;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"TCP server failed to start: {ex.Message}");
                return false;
            }
        }

        public async Task StopTcpServerAsync()
        {
            if (_tcpServer != null)
            {
                await _tcpServer.StopAsync();
                _tcpServer.Dispose();
                _tcpServer = null;
                IsTcpServerRunning = false;
                StatusChanged?.Invoke(this, "TCP server stopped");
            }
        }

        public async Task<bool> StartUdpServerAsync()
        {
            if (!_config.Network.EnableUdp)
            {
                return false;
            }

            if (_udpServer != null)
            {
                return true; // Already running
            }

            try
            {
                _udpServer = new UdpServer(_config.Network.Udp.Host, _config.Network.Udp.Port);
                var started = await _udpServer.StartAsync();
                IsUdpServerRunning = started;
                if (started)
                {
                    StatusChanged?.Invoke(this, $"UDP server started on {_config.Network.Udp.Host}:{_config.Network.Udp.Port}");
                }
                return started;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"UDP server failed to start: {ex.Message}");
                return false;
            }
        }

        public async Task StopUdpServerAsync()
        {
            if (_udpServer != null)
            {
                await _udpServer.StopAsync();
                _udpServer.Dispose();
                _udpServer = null;
                IsUdpServerRunning = false;
                StatusChanged?.Invoke(this, "UDP server stopped");
            }
        }

        public async Task UpdateConfigurationAsync(BoundingBox boundingBox)
        {
            // Update configuration
            _config.BoundingBox = boundingBox;

            // Restart WebSocket with new bounding box
            if (_webSocketService != null && _webSocketService.IsConnected)
            {
                await StopWebSocketAsync();
                await Task.Delay(1000); // Give it a moment
                await StartWebSocketAsync();
                StatusChanged?.Invoke(this, "Configuration updated and WebSocket reconnected");
            }
        }

        private async void OnVesselDataReceived(object? sender, AisData vesselData)
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
                if (_config.ApplicationLogging.LogNmeaMessages)
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

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    _statistics?.PrintSummary();

                    StopWebSocketAsync().Wait(TimeSpan.FromSeconds(5));
                    StopTcpServerAsync().Wait(TimeSpan.FromSeconds(2));
                    StopUdpServerAsync().Wait(TimeSpan.FromSeconds(1));

                    _statistics?.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error during ServiceManager disposal: {ex.Message}");
                }
                finally
                {
                    _disposed = true;
                }
            }
        }
    }
}
