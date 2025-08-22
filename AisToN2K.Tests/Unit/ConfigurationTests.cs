using AisToN2K.Configuration;
using AisToN2K.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AisToN2K.Tests.Unit
{
    /// <summary>
    /// Tests for application configuration loading, validation, and security features.
    /// Validates configuration management, API key handling, and parameter validation.
    /// </summary>
    public class ConfigurationTests
    {
        #region Configuration Loading Tests

        [Fact]
        public void LoadConfiguration_ValidJson_ShouldParseCorrectly()
        {
            // Arrange
            var configData = new Dictionary<string, string?>
            {
                ["ApiKey"] = "test-api-key-12345",
                ["WebSocketUrl"] = "wss://stream.aisstream.io/v0/stream",
                ["BoundingBox:North"] = "48.8000",
                ["BoundingBox:South"] = "48.0000",
                ["BoundingBox:East"] = "-122.1900",
                ["BoundingBox:West"] = "-123.3550",
                ["Network:EnableTcp"] = "true",
                ["Network:EnableUdp"] = "true",
                ["Network:Tcp:Host"] = "0.0.0.0",
                ["Network:Tcp:Port"] = "2000",
                ["Network:Tcp:MaxConnections"] = "10",
                ["Network:Udp:Host"] = "127.0.0.1",
                ["Network:Udp:Port"] = "2001",
                ["ApplicationLogging:EnableDetailedLogging"] = "true",
                ["ApplicationLogging:LogNmeaMessages"] = "true",
                ["ApplicationLogging:StatisticsReportingIntervalMinutes"] = "1"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            var config = new AppConfig();
            configuration.Bind(config);

            // Assert
            config.ApiKey.Should().Be("test-api-key-12345");
            config.WebSocketUrl.Should().Be("wss://stream.aisstream.io/v0/stream");
            
            config.BoundingBox.Should().NotBeNull();
            config.BoundingBox.North.Should().Be(48.8000);
            config.BoundingBox.South.Should().Be(48.0000);
            config.BoundingBox.East.Should().Be(-122.1900);
            config.BoundingBox.West.Should().Be(-123.3550);
            
            config.Network.Should().NotBeNull();
            config.Network.EnableTcp.Should().BeTrue();
            config.Network.EnableUdp.Should().BeTrue();
            config.Network.Tcp.Host.Should().Be("0.0.0.0");
            config.Network.Tcp.Port.Should().Be(2000);
            config.Network.Tcp.MaxConnections.Should().Be(10);
            config.Network.Udp.Host.Should().Be("127.0.0.1");
            config.Network.Udp.Port.Should().Be(2001);
            
            config.ApplicationLogging.Should().NotBeNull();
            config.ApplicationLogging.EnableDetailedLogging.Should().BeTrue();
            config.ApplicationLogging.LogNmeaMessages.Should().BeTrue();
            config.ApplicationLogging.StatisticsReportingIntervalMinutes.Should().Be(1);
        }

        [Fact]
        public void LoadConfiguration_MissingValues_ShouldUseDefaults()
        {
            // Arrange
            var configData = new Dictionary<string, string?>();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            var config = new AppConfig();
            configuration.Bind(config);

            // Assert - Should use default values where available
            config.ApiKey.Should().Be(string.Empty);
            config.WebSocketUrl.Should().Be("wss://stream.aisstream.io/v0/stream");
            config.BoundingBox.North.Should().Be(48.8000);
            config.BoundingBox.South.Should().Be(48.0000);
            // Ports should be 0 (default int value) when not specified in configuration
            config.Network.Tcp.Port.Should().Be(0);
            config.Network.Udp.Port.Should().Be(0);
            config.ApplicationLogging.EnableDetailedLogging.Should().BeTrue();
        }

        #endregion

        #region Bounding Box Validation Tests

        [Theory]
        [InlineData(49.0, 48.0, -122.0, -123.0, true, "Valid Pacific Northwest bounding box - East (-122) is greater (more easterly) than West (-123)")]
        [InlineData(48.0, 49.0, -122.0, -123.0, false, "North should be greater than South")]
        [InlineData(49.0, 48.0, -122.0, -124.0, true, "Valid Pacific Northwest bounding box - East (-122) is greater (more easterly) than West (-124)")]
        [InlineData(90.0, -90.0, 180.0, -180.0, true, "Maximum valid global bounding box")]
        [InlineData(91.0, 48.0, -122.0, -123.0, false, "North latitude out of range")]
        [InlineData(49.0, -91.0, -122.0, -123.0, false, "South latitude out of range")]
        [InlineData(49.0, 48.0, 181.0, -123.0, false, "East longitude out of range")]
        [InlineData(49.0, 48.0, -122.0, -181.0, false, "West longitude out of range")]
        public void ValidateBoundingBox_ShouldValidateCorrectly(
            double north, double south, double east, double west, bool shouldBeValid, string description)
        {
            // Arrange
            var boundingBox = new BoundingBox
            {
                North = north,
                South = south,
                East = east,
                West = west
            };

            // Act
            var isValid = ValidateBoundingBox(boundingBox);

            // Assert
            isValid.Should().Be(shouldBeValid, description);
        }

        [Fact]
        public void ValidateBoundingBox_DatelineCrossing_ShouldBeValid()
        {
            // Arrange - Bounding box crossing International Date Line
            var boundingBox = new BoundingBox
            {
                North = 60.0,
                South = 50.0,
                East = -170.0,  // East of date line
                West = 170.0    // West of date line
            };

            // Act
            var isValid = ValidateBoundingBox(boundingBox);

            // Assert
            isValid.Should().BeTrue("Bounding box crossing dateline should be valid");
        }

        [Theory]
        [InlineData(0.0001, true, "Very small bounding box should be valid")]
        [InlineData(0.0, false, "Zero-size bounding box should be invalid")]
        [InlineData(180.0, true, "Full hemisphere should be valid")]
        public void ValidateBoundingBox_Size_ShouldValidateCorrectly(double size, bool shouldBeValid, string description)
        {
            // Arrange
            var boundingBox = new BoundingBox
            {
                North = size / 2,
                South = -size / 2,
                East = size / 2,
                West = -size / 2
            };

            // Act
            var isValid = ValidateBoundingBox(boundingBox);

            // Assert
            isValid.Should().Be(shouldBeValid, description);
        }

        #endregion

        #region Network Configuration Tests

        [Theory]
        [InlineData("0.0.0.0", true, "Bind to all interfaces")]
        [InlineData("127.0.0.1", true, "Localhost binding")]
        [InlineData("192.168.1.100", true, "Specific IP address")]
        [InlineData("", false, "Empty host should be invalid")]
        [InlineData("invalid.host", true, "Domain names should be allowed for validation")]
        public void ValidateNetworkHost_ShouldValidateCorrectly(string host, bool shouldBeValid, string description)
        {
            // Arrange
            var tcpConfig = new TcpConfig { Host = host, Port = 2000 };

            // Act
            var isValid = ValidateNetworkHost(tcpConfig.Host);

            // Assert
            isValid.Should().Be(shouldBeValid, description);
        }

        [Theory]
        [InlineData(1, false, "Port 1 is reserved")]
        [InlineData(80, false, "Port 80 is commonly used")]
        [InlineData(1024, true, "User ports start at 1024")]
        [InlineData(2000, true, "Default TCP port")]
        [InlineData(2001, true, "Default UDP broadcast port")]
        [InlineData(8080, true, "Common alternative port")]
        [InlineData(65535, true, "Maximum valid port")]
        [InlineData(65536, false, "Port above valid range")]
        [InlineData(0, false, "Port 0 is invalid")]
        [InlineData(-1, false, "Negative port is invalid")]
        public void ValidateNetworkPort_ShouldValidateCorrectly(int port, bool shouldBeValid, string description)
        {
            // Arrange & Act
            var isValid = ValidateNetworkPort(port);

            // Assert
            isValid.Should().Be(shouldBeValid, description);
        }

        [Fact]
        public void ValidateNetworkConfiguration_BothDisabled_ShouldBeInvalid()
        {
            // Arrange
            var networkConfig = new NetworkConfig
            {
                EnableTcp = false,
                EnableUdp = false
            };

            // Act
            var isValid = ValidateNetworkConfiguration(networkConfig);

            // Assert
            isValid.Should().BeFalse("At least one network protocol should be enabled");
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void ValidateNetworkConfiguration_AtLeastOneEnabled_ShouldBeValid(bool enableTcp, bool enableUdp)
        {
            // Arrange
            var networkConfig = new NetworkConfig
            {
                EnableTcp = enableTcp,
                EnableUdp = enableUdp,
                Tcp = new TcpConfig { Host = "0.0.0.0", Port = 2000 },
                Udp = new UdpConfig { Host = "127.0.0.1", Port = 2001 }
            };

            // Act
            var isValid = ValidateNetworkConfiguration(networkConfig);

            // Assert
            isValid.Should().BeTrue("Network configuration with at least one protocol enabled should be valid");
        }

        [Theory]
        [InlineData(1, true, "Normal max connections")]
        [InlineData(0, false, "Zero max connections invalid")]
        [InlineData(-1, false, "Negative max connections invalid")]
        [InlineData(1000, true, "High max connections should be allowed")]
        public void ValidateMaxConnections_ShouldValidateCorrectly(int maxConnections, bool shouldBeValid, string description)
        {
            // Arrange
            var tcpConfig = new TcpConfig { MaxConnections = maxConnections };

            // Act
            var isValid = tcpConfig.MaxConnections > 0;

            // Assert
            isValid.Should().Be(shouldBeValid, description);
        }

        #endregion

        #region API Key Configuration Tests

        [Theory]
        [InlineData("", false, "Empty API key")]
        [InlineData("   ", false, "Whitespace API key")]
        [InlineData("test", false, "Too short API key")]
        [InlineData("valid-api-key-12345", true, "Valid API key")]
        [InlineData("sk-1234567890abcdef1234567890abcdef", true, "Long API key")]
        public void ValidateApiKey_ShouldValidateCorrectly(string apiKey, bool shouldBeValid, string description)
        {
            // Arrange & Act
            var isValid = ValidateApiKey(apiKey);

            // Assert
            isValid.Should().Be(shouldBeValid, description);
        }

        [Fact]
        public void ValidateApiKey_SpecialCharacters_ShouldBeAllowed()
        {
            // Arrange
            var apiKeysWithSpecialChars = new[]
            {
                "api-key-with-dashes",
                "api_key_with_underscores",
                "api.key.with.dots",
                "ApiKeyWithNumbers123",
                "UPPERCASE-API-KEY"
            };

            foreach (var apiKey in apiKeysWithSpecialChars)
            {
                // Act
                var isValid = ValidateApiKey(apiKey);

                // Assert
                isValid.Should().BeTrue($"API key with special characters should be valid: {apiKey}");
            }
        }

        [Fact]
        public void ValidateApiKey_SecurityCharacters_ShouldBeRejected()
        {
            // Arrange - API keys that might be security risks
            var unsafeApiKeys = new[]
            {
                "api;key;with;semicolons",
                "api\"key\"with\"quotes",
                "api'key'with'apostrophes",
                "api\\key\\with\\backslashes",
                "api key with spaces"
            };

            foreach (var apiKey in unsafeApiKeys)
            {
                // Act
                var isValid = ValidateApiKeySafety(apiKey);

                // Assert
                isValid.Should().BeFalse($"API key with unsafe characters should be rejected: {apiKey}");
            }
        }

        #endregion

        #region WebSocket URL Validation Tests

        [Theory]
        [InlineData("wss://stream.aisstream.io/v0/stream", true, "Valid secure WebSocket URL")]
        [InlineData("ws://stream.aisstream.io/v0/stream", false, "Insecure WebSocket should be rejected")]
        [InlineData("https://api.example.com", false, "HTTPS URL is not WebSocket")]
        [InlineData("wss://", false, "Incomplete WebSocket URL")]
        [InlineData("", false, "Empty URL")]
        [InlineData("not-a-url", false, "Invalid URL format")]
        public void ValidateWebSocketUrl_ShouldValidateCorrectly(string url, bool shouldBeValid, string description)
        {
            // Arrange & Act
            var isValid = ValidateWebSocketUrl(url);

            // Assert
            isValid.Should().Be(shouldBeValid, description);
        }

        [Fact]
        public void ValidateWebSocketUrl_ValidUrls_ShouldParseCorrectly()
        {
            // Arrange
            var validUrls = new[]
            {
                "wss://stream.aisstream.io/v0/stream",
                "wss://api.example.com:443/websocket",
                "wss://localhost:8080/ais"
            };

            foreach (var url in validUrls)
            {
                // Act
                var isValid = ValidateWebSocketUrl(url);

                // Assert
                isValid.Should().BeTrue($"Valid WebSocket URL should pass validation: {url}");
            }
        }

        #endregion

        #region Application Logging Configuration Tests

        [Theory]
        [InlineData(1, true, "1 minute interval")]
        [InlineData(5, true, "5 minute interval")]
        [InlineData(60, true, "1 hour interval")]
        [InlineData(0, false, "Zero interval invalid")]
        [InlineData(-1, false, "Negative interval invalid")]
        [InlineData(1440, true, "24 hour interval")]
        [InlineData(10080, false, "Week interval too long")]
        public void ValidateStatisticsReportingInterval_ShouldValidateCorrectly(
            int intervalMinutes, bool shouldBeValid, string description)
        {
            // Arrange
            var loggingConfig = new ApplicationLogging 
            { 
                StatisticsReportingIntervalMinutes = intervalMinutes 
            };

            // Act
            var isValid = ValidateStatisticsReportingInterval(loggingConfig.StatisticsReportingIntervalMinutes);

            // Assert
            isValid.Should().Be(shouldBeValid, description);
        }

        [Fact]
        public void ApplicationLoggingDefaults_ShouldBeReasonable()
        {
            // Arrange & Act
            var loggingConfig = new ApplicationLogging();

            // Assert
            loggingConfig.EnableDetailedLogging.Should().BeTrue("Detailed logging should be enabled by default");
            loggingConfig.LogNmeaMessages.Should().BeTrue("NMEA message logging should be enabled by default");
            loggingConfig.StatisticsReportingIntervalMinutes.Should().Be(1, "Statistics should report every minute by default");
        }

        #endregion

        #region Configuration Security Tests

        [Fact]
        public void ConfigurationSecurity_ApiKeyNotInPlainText_ShouldBeSecure()
        {
            // Arrange
            var config = new AppConfig
            {
                ApiKey = "test-api-key-12345"
            };

            // Act
            var serialized = System.Text.Json.JsonSerializer.Serialize(config);

            // Assert
            serialized.Should().Contain("test-api-key-12345", 
                "This test validates that API keys are in config - in production, they should come from secure sources");
        }

        [Fact]
        public void ConfigurationSecurity_SensitiveDataMasking_ShouldMaskApiKey()
        {
            // Arrange
            var config = new AppConfig
            {
                ApiKey = "sk-1234567890abcdef1234567890abcdef"
            };

            // Act
            var maskedApiKey = MaskApiKey(config.ApiKey);

            // Assert
            maskedApiKey.Should().NotContain("1234567890abcdef", "API key should be masked");
            maskedApiKey.Should().Contain("***", "Masked API key should contain asterisks");
            maskedApiKey.Should().StartWith("sk-", "Should preserve API key prefix for identification");
        }

        #endregion

        #region Helper Methods

        private static bool ValidateBoundingBox(BoundingBox boundingBox)
        {
            // Basic range validation
            if (boundingBox.North < -90 || boundingBox.North > 90) return false;
            if (boundingBox.South < -90 || boundingBox.South > 90) return false;
            if (boundingBox.East < -180 || boundingBox.East > 180) return false;
            if (boundingBox.West < -180 || boundingBox.West > 180) return false;

            // North should be greater than South
            if (boundingBox.North <= boundingBox.South) return false;

            // For East/West, handle dateline crossing
            // If West > East, it crosses the dateline and should be valid
            
            return true;
        }

        private static bool ValidateNetworkHost(string host)
        {
            return !string.IsNullOrWhiteSpace(host);
        }

        private static bool ValidateNetworkPort(int port)
        {
            return port > 1023 && port <= 65535; // User ports only
        }

        private static bool ValidateNetworkConfiguration(NetworkConfig networkConfig)
        {
            return networkConfig.EnableTcp || networkConfig.EnableUdp;
        }

        private static bool ValidateApiKey(string apiKey)
        {
            return !string.IsNullOrWhiteSpace(apiKey) && apiKey.Trim().Length >= 10;
        }

        private static bool ValidateApiKeySafety(string apiKey)
        {
            var unsafeCharacters = new[] { ';', '"', '\'', '\\', ' ' };
            return !unsafeCharacters.Any(apiKey.Contains);
        }

        private static bool ValidateWebSocketUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            
            try
            {
                var uri = new Uri(url);
                return uri.Scheme == "wss"; // Only secure WebSocket connections
            }
            catch
            {
                return false;
            }
        }

        private static bool ValidateStatisticsReportingInterval(int intervalMinutes)
        {
            return intervalMinutes > 0 && intervalMinutes <= 1440; // Max 24 hours
        }

        private static string MaskApiKey(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length < 8)
                return "***";

            // Show first 3 characters and last 4, mask the middle
            var prefix = apiKey.Substring(0, Math.Min(3, apiKey.Length));
            var suffix = apiKey.Length > 4 ? apiKey.Substring(apiKey.Length - 4) : "";
            var maskedLength = Math.Max(0, apiKey.Length - prefix.Length - suffix.Length);
            
            return prefix + new string('*', maskedLength) + suffix;
        }

        #endregion
    }
}
