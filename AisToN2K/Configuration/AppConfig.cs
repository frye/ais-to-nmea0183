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

        /// <summary>
        /// Validates the configuration and returns any validation errors
        /// </summary>
        /// <returns>List of validation error messages, empty if valid</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            // Validate network configuration
            if (Network == null)
            {
                errors.Add("Network configuration is missing");
                return errors;
            }

            // Validate TCP configuration if enabled
            if (Network.EnableTcp)
            {
                if (Network.Tcp == null)
                {
                    errors.Add("TCP configuration is missing but TCP is enabled");
                }
                else
                {
                    if (Network.Tcp.Port <= 0 || Network.Tcp.Port > 65535)
                    {
                        errors.Add($"TCP port must be between 1 and 65535, got: {Network.Tcp.Port}");
                    }
                    if (string.IsNullOrWhiteSpace(Network.Tcp.Host))
                    {
                        errors.Add("TCP host cannot be empty");
                    }
                }
            }

            // Validate UDP configuration if enabled
            if (Network.EnableUdp)
            {
                if (Network.Udp == null)
                {
                    errors.Add("UDP configuration is missing but UDP is enabled");
                }
                else
                {
                    if (Network.Udp.Port <= 0 || Network.Udp.Port > 65535)
                    {
                        errors.Add($"UDP port must be between 1 and 65535, got: {Network.Udp.Port}");
                    }
                    if (string.IsNullOrWhiteSpace(Network.Udp.Host))
                    {
                        errors.Add("UDP host cannot be empty");
                    }
                }
            }

            // Ensure at least one network service is enabled
            if (!Network.EnableTcp && !Network.EnableUdp)
            {
                errors.Add("At least one network service (TCP or UDP) must be enabled");
            }

            return errors;
        }
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
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public int MaxConnections { get; set; } = 10;
    }

    public class UdpConfig
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
    }

    public class ApplicationLogging
    {
        public bool EnableDetailedLogging { get; set; } = true;
        public bool LogNmeaMessages { get; set; } = true;
        public int StatisticsReportingIntervalMinutes { get; set; } = 1;
    }
}
