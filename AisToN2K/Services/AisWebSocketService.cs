using AisToN2K.Configuration;
using AisToN2K.Models;
using Newtonsoft.Json;
using System.Net.WebSockets;
using System.Text;

namespace AisToN2K.Services
{
    public class AisWebSocketService : IDisposable
    {
        private readonly ClientWebSocket _webSocket;
        private readonly string _webSocketUrl;
        private readonly string _apiKey;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly bool _debugMode;
        private bool _disposed = false;
        private int _debugMessageCount = 0;
        
        public event EventHandler<AisData>? VesselDataReceived;
        public event EventHandler<string>? ErrorOccurred;
        
        public bool IsConnected => _webSocket.State == WebSocketState.Open;
        
        public AisWebSocketService(string webSocketUrl, string apiKey, bool debugMode = false)
        {
            _webSocket = new ClientWebSocket();
            _webSocketUrl = webSocketUrl;
            _apiKey = apiKey;
            _debugMode = debugMode;
            _cancellationTokenSource = new CancellationTokenSource();
        }
        
        public async Task<bool> ConnectAsync(double[]? boundingBox = null)
        {
            try
            {
                Console.WriteLine("🔌 Connecting to AIS Stream WebSocket...");
                var uri = new Uri(_webSocketUrl);
                await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);
                
                if (_webSocket.State == WebSocketState.Open)
                {
                    Console.WriteLine("✅ WebSocket connection established");
                    
                    // If bounding box provided, send subscription within 3 seconds as required by AIS Stream
                    if (boundingBox != null)
                    {
                        Console.WriteLine("📡 Sending subscription message...");
                        var subscriptionTask = SubscribeToStreamAsync(boundingBox);
                        var timeoutTask = Task.Delay(3000);
                        
                        var completedTask = await Task.WhenAny(subscriptionTask, timeoutTask);
                        if (completedTask == timeoutTask)
                        {
                            throw new TimeoutException("Failed to send subscription within required 3 seconds");
                        }
                        
                        await subscriptionTask; // Ensure any exceptions are propagated
                        Console.WriteLine("✅ Subscription sent successfully");
                    }
                    
                    // Start receiving messages
                    Console.WriteLine("👂 Starting to listen for messages...");
                    _ = Task.Run(ReceiveMessagesAsync);
                    
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to connect to AIS WebSocket: {ex.Message}");
                if (ex.Message.Contains("concurrent"))
                {
                    Console.WriteLine("💡 This usually means another instance is using the same API key");
                    Console.WriteLine("   Please close other applications or wait 60 seconds before retrying");
                }
                ErrorOccurred?.Invoke(this, ex.Message);
                return false;
            }
        }

        public async Task SubscribeToStreamAsync(double[] boundingBox)
        {
            try
            {
                if (boundingBox.Length != 4)
                {
                    throw new ArgumentException("Bounding box must contain exactly 4 values: [North, West, South, East]");
                }

                // AIS Stream expects bounding boxes as [[lat_min, lon_min], [lat_max, lon_max]]
                // Convert from [North, West, South, East] to proper format
                var subscription = new
                {
                    APIKey = _apiKey,
                    BoundingBoxes = new double[][][]
                    {
                        new double[][]
                        {
                            new double[] { boundingBox[2], boundingBox[1] }, // [South, West] - SW corner
                            new double[] { boundingBox[0], boundingBox[3] }  // [North, East] - NE corner
                        }
                    }
                };

                var message = JsonConvert.SerializeObject(subscription);
                var bytes = Encoding.UTF8.GetBytes(message);
                var arraySegment = new ArraySegment<byte>(bytes);

                await _webSocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to send subscription: {ex.Message}");
                throw;
            }
        }
        
        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[8192];
            var messageBuffer = new StringBuilder();
            
            try
            {
                while (_webSocket.State == WebSocketState.Open && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
                    
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        messageBuffer.Append(message);
                        
                        if (result.EndOfMessage)
                        {
                            var completeMessage = messageBuffer.ToString();
                            messageBuffer.Clear();
                            
                            await ProcessMessageAsync(completeMessage);
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        // AIS Stream should send JSON text, but if binary is received, 
                        // try to decode as UTF-8 encoded JSON
                        if (result.EndOfMessage)
                        {
                            try
                            {
                                var binaryMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                                await ProcessMessageAsync(binaryMessage);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"⚠️  Failed to decode binary message: {ex.Message}");
                            }
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                }
                
                // Connection ended
                ErrorOccurred?.Invoke(this, "WebSocket connection ended");
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, no logging needed
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving WebSocket messages: {ex.Message}");
                ErrorOccurred?.Invoke(this, ex.Message);
            }
        }
        
        private async Task ProcessMessageAsync(string message)
        {
            try
            {
                if (_debugMode)
                {
                    Console.WriteLine($"🔍 ProcessMessageAsync called with message length: {message.Length}");
                }

                // Debug logging for received messages
                if (_debugMode)
                {
                    _debugMessageCount++;
                    if (_debugMessageCount <= 3)
                    {
                        Console.WriteLine($"🔍 Full Message #{_debugMessageCount}: {message}");
                    }
                    else if (message.Length > 50)
                    {
                        Console.WriteLine($"🔍 Received: {message.Substring(0, Math.Min(150, message.Length))}...");
                    }
                }
                
                // Parse the AIS Stream message
                if (_debugMode) Console.WriteLine($"🔍 About to deserialize JSON...");
                AisStreamMessage? aisStreamMessage = null;
                try
                {
                    aisStreamMessage = JsonConvert.DeserializeObject<AisStreamMessage>(message);
                    if (_debugMode) Console.WriteLine($"🔍 JSON deserialized successfully");
                }
                catch (Exception jsonEx)
                {
                    if (_debugMode) Console.WriteLine($"❌ JSON deserialization failed: {jsonEx.Message}");
                    return; // Skip processing this message
                }
                
                if (_debugMode && _debugMessageCount <= 15)
                {
                    Console.WriteLine($"🔍 Parsed MessageType: '{aisStreamMessage?.MessageType}'");
                    Console.WriteLine($"🔍 Has Message: {aisStreamMessage?.Message != null}");
                    Console.WriteLine($"🔍 Has Message.PositionReport: {aisStreamMessage?.Message?.PositionReport != null}");
                    Console.WriteLine($"🔍 Has Message.StandardClassBPositionReport: {aisStreamMessage?.Message?.StandardClassBPositionReport != null}");
                    Console.WriteLine($"🔍 Has Message.ShipStaticData: {aisStreamMessage?.Message?.ShipStaticData != null}");
                    Console.WriteLine($"🔍 Has Message.StaticDataReport: {aisStreamMessage?.Message?.StaticDataReport != null}");
                }
                
                // The message type is determined by which property is present in the Message object
                if (aisStreamMessage?.Message?.PositionReport != null)
                {
                    var positionReport = aisStreamMessage.Message.PositionReport;
                    var metaData = aisStreamMessage.MetaData;
                    
                    // Create AisData object from position report
                    var aisData = new AisData
                    {
                        Mmsi = positionReport.UserID,
                        Latitude = positionReport.Latitude,
                        Longitude = positionReport.Longitude,
                        SpeedOverGround = positionReport.Sog,
                        CourseOverGround = positionReport.Cog,
                        Heading = positionReport.TrueHeading,
                        Timestamp = metaData?.TimeUtc ?? DateTime.UtcNow,
                        VesselName = metaData?.ShipName,
                        MessageType = 1, // Position Report
                        
                        // Extract additional fields that were missing
                        RateOfTurn = positionReport.RateOfTurn,
                        NavigationalStatus = positionReport.NavigationalStatus,
                        TimestampSeconds = positionReport.Timestamp,
                        PositionAccuracy = positionReport.PositionAccuracy,
                        Raim = positionReport.RAIM
                    };
                    
                    if (_debugMode)
                    {
                        Console.WriteLine($"🚢 Parsed Position Report: MMSI {aisData.Mmsi}, {aisData.Latitude:F4},{aisData.Longitude:F4}");
                    }
                    
                    VesselDataReceived?.Invoke(this, aisData);
                }
                else if (aisStreamMessage?.Message?.ShipAndVoyageData != null)
                {
                    var shipData = aisStreamMessage.Message.ShipAndVoyageData;
                    var metaData = aisStreamMessage.MetaData;
                    
                    // Handle static ship data - create AisData with available position from metadata
                    if (metaData?.Latitude.HasValue == true && metaData.Longitude.HasValue)
                    {
                        var aisData = new AisData
                        {
                            Mmsi = shipData.UserID,
                            Latitude = metaData.Latitude.Value,
                            Longitude = metaData.Longitude.Value,
                            Timestamp = metaData.TimeUtc,
                            VesselName = shipData.VesselName,
                            VesselType = shipData.TypeOfShipAndCargoType,
                            MessageType = 5 // Static and Voyage Data
                        };
                        
                        VesselDataReceived?.Invoke(this, aisData);
                    }
                }
                else if (aisStreamMessage?.Message?.ShipStaticData != null)
                {
                    var shipStaticData = aisStreamMessage.Message.ShipStaticData;
                    var metaData = aisStreamMessage.MetaData;
                    
                    // Handle Type 5 static ship data
                    var aisData = new AisData
                    {
                        Mmsi = shipStaticData.UserID,
                        Latitude = metaData?.Latitude ?? 0.0, // Default to 0 if no position in metadata
                        Longitude = metaData?.Longitude ?? 0.0,
                        Timestamp = metaData?.TimeUtc ?? DateTime.UtcNow,
                        VesselName = shipStaticData.Name,
                        VesselType = shipStaticData.Type,
                        CallSign = shipStaticData.CallSign,
                        MessageType = 5 // Ship Static Data
                    };
                    
                    VesselDataReceived?.Invoke(this, aisData);
                }
                else if (aisStreamMessage?.Message?.StaticDataReport != null)
                {
                    var staticDataReport = aisStreamMessage.Message.StaticDataReport;
                    var metaData = aisStreamMessage.MetaData;
                    
                    // Handle Type 24 Class B static data
                    var aisData = new AisData
                    {
                        Mmsi = staticDataReport.UserID,
                        Latitude = metaData?.Latitude ?? 0.0, // Default to 0 if no position in metadata
                        Longitude = metaData?.Longitude ?? 0.0,
                        Timestamp = metaData?.TimeUtc ?? DateTime.UtcNow,
                        VesselName = staticDataReport.ReportA?.Name,
                        VesselType = staticDataReport.ReportB?.ShipType,
                        CallSign = staticDataReport.ReportB?.CallSign,
                        MessageType = 24 // Class B Static Data
                    };
                    
                    VesselDataReceived?.Invoke(this, aisData);
                }
                else if (aisStreamMessage?.Message?.StandardClassBPositionReport != null)
                {
                    // Handle Class B position reports
                    var classBReport = aisStreamMessage.Message.StandardClassBPositionReport;
                    var metaData = aisStreamMessage.MetaData;
                    
                    var aisData = new AisData
                    {
                        Mmsi = classBReport.UserID,
                        Latitude = classBReport.Latitude,
                        Longitude = classBReport.Longitude,
                        SpeedOverGround = classBReport.Sog,
                        CourseOverGround = classBReport.Cog,
                        Heading = classBReport.TrueHeading,
                        Timestamp = metaData?.TimeUtc ?? DateTime.UtcNow,
                        VesselName = metaData?.ShipName,
                        MessageType = 18, // Class B Position Report
                        
                        // Extract additional fields for Class B
                        TimestampSeconds = classBReport.Timestamp,
                        PositionAccuracy = classBReport.PositionAccuracy,
                        Raim = classBReport.RAIM
                    };
                    
                    if (_debugMode)
                    {
                        Console.WriteLine($"🚢 Parsed Class B Report: MMSI {aisData.Mmsi}, {aisData.Latitude:F4},{aisData.Longitude:F4}");
                    }
                    
                    VesselDataReceived?.Invoke(this, aisData);
                }
                else
                {
                    // Debug logging for ignored message types
                    if (_debugMode)
                    {
                        Console.WriteLine($"⚠️ Ignored message type: {aisStreamMessage?.MessageType ?? "Unknown"}");
                    }
                    // Silently ignore unhandled message types to reduce noise
                    // Only handle: PositionReport, ShipStaticData, StandardClassBPositionReport, StaticDataReport
                }
            }
            catch (JsonException ex)
            {
                ErrorOccurred?.Invoke(this, $"JSON parsing error: {ex.Message}");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error processing AIS message: {ex.Message}");
            }
            
            await Task.CompletedTask;
        }
        
        public async Task DisconnectAsync()
        {
            try
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing WebSocket: {ex.Message}");
            }
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _cancellationTokenSource.Cancel();
                    
                    try
                    {
                        DisconnectAsync().Wait(TimeSpan.FromSeconds(5));
                    }
                    catch
                    {
                        // Ignore errors during disposal
                    }
                    
                    _webSocket?.Dispose();
                    _cancellationTokenSource?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
