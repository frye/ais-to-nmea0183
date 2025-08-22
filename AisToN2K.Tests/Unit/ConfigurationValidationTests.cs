using AisToN2K.Configuration;
using Xunit;

namespace AisToN2K.Tests.Unit
{
    public class ConfigurationValidationTests
    {
        [Fact]
        public void Validate_ValidConfiguration_ReturnsNoErrors()
        {
            // Arrange
            var config = new AppConfig
            {
                Network = new NetworkConfig
                {
                    EnableTcp = true,
                    EnableUdp = true,
                    Tcp = new TcpConfig
                    {
                        Host = "0.0.0.0",
                        Port = 2004,
                        MaxConnections = 10
                    },
                    Udp = new UdpConfig
                    {
                        Host = "127.0.0.1",
                        Port = 2005
                    }
                }
            };

            // Act
            var errors = config.Validate();

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_MissingTcpPort_ReturnsValidationError()
        {
            // Arrange
            var config = new AppConfig
            {
                Network = new NetworkConfig
                {
                    EnableTcp = true,
                    EnableUdp = false,
                    Tcp = new TcpConfig
                    {
                        Host = "0.0.0.0",
                        Port = 0, // Invalid port
                        MaxConnections = 10
                    }
                }
            };

            // Act
            var errors = config.Validate();

            // Assert
            Assert.Contains(errors, error => error.Contains("TCP port must be between 1 and 65535"));
        }

        [Fact]
        public void Validate_MissingUdpPort_ReturnsValidationError()
        {
            // Arrange
            var config = new AppConfig
            {
                Network = new NetworkConfig
                {
                    EnableTcp = false,
                    EnableUdp = true,
                    Udp = new UdpConfig
                    {
                        Host = "127.0.0.1",
                        Port = 0 // Invalid port
                    }
                }
            };

            // Act
            var errors = config.Validate();

            // Assert
            Assert.Contains(errors, error => error.Contains("UDP port must be between 1 and 65535"));
        }

        [Fact]
        public void Validate_InvalidPortRange_ReturnsValidationError()
        {
            // Arrange
            var config = new AppConfig
            {
                Network = new NetworkConfig
                {
                    EnableTcp = true,
                    EnableUdp = true,
                    Tcp = new TcpConfig
                    {
                        Host = "0.0.0.0",
                        Port = 99999, // Invalid port (too high)
                        MaxConnections = 10
                    },
                    Udp = new UdpConfig
                    {
                        Host = "127.0.0.1",
                        Port = -1 // Invalid port (negative)
                    }
                }
            };

            // Act
            var errors = config.Validate();

            // Assert
            Assert.Contains(errors, error => error.Contains("TCP port must be between 1 and 65535"));
            Assert.Contains(errors, error => error.Contains("UDP port must be between 1 and 65535"));
        }

        [Fact]
        public void Validate_EmptyHost_ReturnsValidationError()
        {
            // Arrange
            var config = new AppConfig
            {
                Network = new NetworkConfig
                {
                    EnableTcp = true,
                    EnableUdp = true,
                    Tcp = new TcpConfig
                    {
                        Host = "", // Empty host
                        Port = 2004,
                        MaxConnections = 10
                    },
                    Udp = new UdpConfig
                    {
                        Host = "   ", // Whitespace only host
                        Port = 2005
                    }
                }
            };

            // Act
            var errors = config.Validate();

            // Assert
            Assert.Contains(errors, error => error.Contains("TCP host cannot be empty"));
            Assert.Contains(errors, error => error.Contains("UDP host cannot be empty"));
        }

        [Fact]
        public void Validate_BothServicesDisabled_ReturnsValidationError()
        {
            // Arrange
            var config = new AppConfig
            {
                Network = new NetworkConfig
                {
                    EnableTcp = false,
                    EnableUdp = false
                }
            };

            // Act
            var errors = config.Validate();

            // Assert
            Assert.Contains(errors, error => error.Contains("At least one network service (TCP or UDP) must be enabled"));
        }

        [Fact]
        public void Validate_MissingNetworkConfig_ReturnsValidationError()
        {
            // Arrange
            var config = new AppConfig
            {
                Network = null!
            };

            // Act
            var errors = config.Validate();

            // Assert
            Assert.Contains(errors, error => error.Contains("Network configuration is missing"));
        }

        [Fact]
        public void Validate_MissingTcpConfigWhenEnabled_ReturnsValidationError()
        {
            // Arrange
            var config = new AppConfig
            {
                Network = new NetworkConfig
                {
                    EnableTcp = true,
                    EnableUdp = false,
                    Tcp = null!
                }
            };

            // Act
            var errors = config.Validate();

            // Assert
            Assert.Contains(errors, error => error.Contains("TCP configuration is missing but TCP is enabled"));
        }

        [Fact]
        public void Validate_MissingUdpConfigWhenEnabled_ReturnsValidationError()
        {
            // Arrange
            var config = new AppConfig
            {
                Network = new NetworkConfig
                {
                    EnableTcp = false,
                    EnableUdp = true,
                    Udp = null!
                }
            };

            // Act
            var errors = config.Validate();

            // Assert
            Assert.Contains(errors, error => error.Contains("UDP configuration is missing but UDP is enabled"));
        }
    }
}
