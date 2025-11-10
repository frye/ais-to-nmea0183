using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AisToN2K.Services
{
    public class TcpServer : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly bool _debugMode;
        private TcpListener? _listener;
        private readonly ConcurrentDictionary<string, ClientConnection> _clients;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isRunning;
        private bool _disposed;

        // Statistics
        public int ConnectedClients => _clients.Count;
        public int TotalMessagesSent { get; private set; }
        public int TotalBytesSent { get; private set; }

        public TcpServer(string host, int port, bool debugMode = false)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;
            _debugMode = debugMode;
            _clients = new ConcurrentDictionary<string, ClientConnection>();

            // Validate port range
            if (port <= 0 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(port), $"Port must be between 1 and 65535, got: {port}");
            }

            // Validate host
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentException("Host cannot be null or empty", nameof(host));
            }
        }

        public async Task<bool> StartAsync()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Parse(_host), _port);
                _listener.Start();
                _isRunning = true;
                _cancellationTokenSource = new CancellationTokenSource();

                Console.WriteLine($"✅ TCP server started on {_host}:{_port}");

                // Start accepting clients in background
                _ = Task.Run(async () => await AcceptClientsAsync(_cancellationTokenSource.Token));

                // Start periodic cleanup
                _ = Task.Run(async () => await PeriodicCleanupAsync(_cancellationTokenSource.Token));

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to start TCP server: {ex.Message}");
                return false;
            }
        }

        private async Task AcceptClientsAsync(CancellationToken cancellationToken)
        {
            while (_isRunning && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var tcpClient = await _listener!.AcceptTcpClientAsync();
                    var clientEndpoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown";
                    var clientId = $"tcp_{clientEndpoint}_{DateTime.Now.Ticks}";

                    var clientConnection = new ClientConnection(clientId, tcpClient);
                    _clients.TryAdd(clientId, clientConnection);

                    // Handle client in background
                    _ = Task.Run(async () => await HandleClientAsync(clientConnection, cancellationToken));
                }
                catch (ObjectDisposedException)
                {
                    // Server is being shut down
                    break;
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Console.WriteLine($"⚠️ Error accepting TCP client: {ex.Message}");
                    }
                }
            }
        }

        private async Task HandleClientAsync(ClientConnection client, CancellationToken cancellationToken)
        {
            try
            {
                // Keep connection alive and monitor for disconnection
                var buffer = new byte[1024];
                var stream = client.TcpClient.GetStream();
                
                // Set read timeout to prevent hanging
                stream.ReadTimeout = 1000;

                while (client.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Use a shorter timeout for the read operation
                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(500));
                        
                        // Check if client is still connected by attempting to read (non-blocking)
                        if (client.TcpClient.Available > 0)
                        {
                            await stream.ReadAsync(buffer, 0, buffer.Length, timeoutCts.Token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Timeout or cancellation - check if it's due to our timeout or shutdown
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                        // Otherwise, continue monitoring
                    }
                    catch (Exception)
                    {
                        // Client disconnected or other error
                        break;
                    }

                    await Task.Delay(100, cancellationToken); // Check more frequently
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🔌 TCP client {client.Id} disconnected: {ex.Message}");
            }
            finally
            {
                await RemoveClientAsync(client.Id);
            }
        }

        public async Task<bool> BroadcastMessageAsync(string message)
        {
            if (_clients.IsEmpty)
            {
                return false;
            }

            var messageBytes = Encoding.ASCII.GetBytes(message);
            var successfulSends = 0;
            var failedClients = new List<string>();

            var sendTasks = _clients.Values.Select(async client =>
            {
                try
                {
                    if (client.IsConnected && client.TcpClient.Connected)
                    {
                        var stream = client.TcpClient.GetStream();
                        await stream.WriteAsync(messageBytes, 0, messageBytes.Length);
                        await stream.FlushAsync();

                        Interlocked.Increment(ref successfulSends);
                        client.MessagesSent++;
                        client.BytesSent += messageBytes.Length;
                        TotalMessagesSent++;
                        TotalBytesSent += messageBytes.Length;

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Failed to send to TCP client {client.Id}: {ex.Message}");
                    failedClients.Add(client.Id);
                }
                return false;
            });

            await Task.WhenAll(sendTasks);

            // Remove failed clients
            foreach (var clientId in failedClients)
            {
                await RemoveClientAsync(clientId);
            }

            if (successfulSends > 0)
            {
                if (_debugMode)
                {
                    Console.WriteLine($"📤 TCP broadcast to {successfulSends} clients ({messageBytes.Length} bytes)");
                }
                return true;
            }

            return false;
        }

        private async Task RemoveClientAsync(string clientId)
        {
            if (_clients.TryRemove(clientId, out var client))
            {
                try
                {
                    client.TcpClient.Close();
                    Console.WriteLine($"🔌 TCP client removed: {clientId} (Remaining: {_clients.Count})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error removing TCP client {clientId}: {ex.Message}");
                }
            }
        }

        private async Task PeriodicCleanupAsync(CancellationToken cancellationToken)
        {
            while (_isRunning && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var disconnectedClients = new List<string>();

                    foreach (var kvp in _clients)
                    {
                        var client = kvp.Value;
                        if (!client.IsConnected || !client.TcpClient.Connected)
                        {
                            disconnectedClients.Add(kvp.Key);
                        }
                    }

                    foreach (var clientId in disconnectedClients)
                    {
                        await RemoveClientAsync(clientId);
                    }

                    // Use shorter delay for tests - 5 seconds instead of 30
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                    break;
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Console.WriteLine($"⚠️ TCP cleanup error: {ex.Message}");
                    }
                }
            }
        }

        public async Task StopAsync()
        {
            _isRunning = false;
            _cancellationTokenSource?.Cancel();

            // Close all client connections with timeout
            var closeTasks = _clients.Values.Select(async client =>
            {
                try
                {
                    client.TcpClient.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error closing TCP client: {ex.Message}");
                }
            });

            // Wait for client cleanup with timeout
            try
            {
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(2));
                var completedTask = await Task.WhenAny(Task.WhenAll(closeTasks), timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    Console.WriteLine("⚠️ TCP client cleanup timed out");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error during TCP client cleanup: {ex.Message}");
            }
            
            _clients.Clear();

            // Stop listener
            try
            {
                _listener?.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error stopping TCP listener: {ex.Message}");
            }
            finally
            {
                _listener = null;
            }

            Console.WriteLine("🛑 TCP server stopped");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    // Only attempt graceful stop if still running to avoid duplicate stop log
                    if (_isRunning)
                    {
                        var stopTask = StopAsync();
                        if (!stopTask.Wait(2000)) // Wait up to 2 seconds for graceful shutdown
                        {
                            Console.WriteLine("⚠️ TCP server dispose timed out");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error during TCP server dispose: {ex.Message}");
                }
                finally
                {
                    _cancellationTokenSource?.Dispose();
                    _disposed = true;
                }
            }
        }
    }

    public class ClientConnection
    {
        public string Id { get; }
        public TcpClient TcpClient { get; }
        public DateTime ConnectedAt { get; }
        public int MessagesSent { get; set; }
        public int BytesSent { get; set; }

        public bool IsConnected => TcpClient?.Connected == true;

        public ClientConnection(string id, TcpClient tcpClient)
        {
            Id = id;
            TcpClient = tcpClient;
            ConnectedAt = DateTime.Now;
            MessagesSent = 0;
            BytesSent = 0;
        }
    }
}
