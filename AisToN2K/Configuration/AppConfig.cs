using Microsoft.Extensions.Configuration;

namespace AisToN2K.Configuration
{
    public class AppConfig
    {
        public string ApiKey { get; set; } = string.Empty;
        public string WebSocketUrl { get; set; } = "wss://stream.aisstream.io/v0/stream";
        public BoundingBox BoundingBox { get; set; } = new();
        public NetworkConfig Network { get; set; } = new();
        public ApplicationLogging ApplicationLogging { get; set; } = new();
    }

    public class BoundingBox
    {
        public double North { get; set; } = 48.8000;  // Pacific Northwest bounding box
        public double South { get; set; } = 48.0000;
        public double East { get; set; } = -122.1900;
        public double West { get; set; } = -123.3550;
    }

    public class NetworkConfig
    {
        public TcpConfig Tcp { get; set; } = new();
        public UdpConfig Udp { get; set; } = new();
        public bool EnableTcp { get; set; } = true;
        public bool EnableUdp { get; set; } = true;
    }

    public class TcpConfig
    {
        public string Host { get; set; } = "0.0.0.0";
        public int Port { get; set; } = 2000;
        public int MaxConnections { get; set; } = 10;
    }

    public class UdpConfig
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 2001;
    }

    public class ApplicationLogging
    {
        public bool EnableDetailedLogging { get; set; } = true;
        public bool LogNmeaMessages { get; set; } = true;
        public int StatisticsReportingIntervalMinutes { get; set; } = 1;
    }
}
