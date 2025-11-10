using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AisToN2K.Services
{
    public class UdpServer : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private UdpClient? _udpClient;
        private IPEndPoint? _broadcastEndpoint;
        private bool _isRunning;
        private bool _disposed;

        // Statistics
        public int TotalMessagesSent { get; private set; }
        public int TotalBytesSent { get; private set; }

        public UdpServer(string host, int port)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;

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
                _udpClient = new UdpClient();
                _udpClient.EnableBroadcast = true;
                _broadcastEndpoint = new IPEndPoint(IPAddress.Parse(_host), _port);
                _isRunning = true;

                Console.WriteLine($"✅ UDP server started, broadcasting to {_host}:{_port}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to start UDP server: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> BroadcastMessageAsync(string message)
        {
            if (!_isRunning || _udpClient == null || _broadcastEndpoint == null)
            {
                return false;
            }

            try
            {
                var messageBytes = Encoding.ASCII.GetBytes(message);
                await _udpClient.SendAsync(messageBytes, messageBytes.Length, _broadcastEndpoint);

                TotalMessagesSent++;
                TotalBytesSent += messageBytes.Length;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Failed to send UDP broadcast: {ex.Message}");
                return false;
            }
        }

        public async Task StopAsync()
        {
            _isRunning = false;
            
            try
            {
                _udpClient?.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error closing UDP client: {ex.Message}");
            }
            finally
            {
                _udpClient?.Dispose();
                _udpClient = null;
            }

            Console.WriteLine("🛑 UDP server stopped");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    // Avoid duplicate stop log if already stopped
                    if (_isRunning)
                    {
                        var stopTask = StopAsync();
                        if (!stopTask.Wait(1000)) // Wait up to 1 second for graceful shutdown
                        {
                            Console.WriteLine("⚠️ UDP server dispose timed out");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error during UDP server dispose: {ex.Message}");
                }
                finally
                {
                    _disposed = true;
                }
            }
        }
    }
}
