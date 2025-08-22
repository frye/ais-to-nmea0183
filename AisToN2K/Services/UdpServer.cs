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

        public UdpServer(string host = "127.0.0.1", int port = 2001)
        {
            _host = host;
            _port = port;
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
            _udpClient?.Close();
            _udpClient?.Dispose();
            _udpClient = null;

            Console.WriteLine("🛑 UDP server stopped");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                StopAsync().Wait(1000);
                _disposed = true;
            }
        }
    }
}
