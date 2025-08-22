using AisToN2K.Services;
using Xunit;

namespace AisToN2K.Tests.Unit
{
    public class NetworkServerConstructorTests
    {
        [Fact]
        public void TcpServer_ValidParameters_CreatesSuccessfully()
        {
            // Arrange & Act
            var server = new TcpServer("0.0.0.0", 2004, false);

            // Assert
            Assert.NotNull(server);
        }

        [Fact]
        public void TcpServer_InvalidPort_ThrowsArgumentOutOfRangeException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => new TcpServer("0.0.0.0", 0, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TcpServer("0.0.0.0", -1, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TcpServer("0.0.0.0", 65536, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TcpServer("0.0.0.0", 99999, false));
        }

        [Fact]
        public void TcpServer_NullHost_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TcpServer(null!, 2004, false));
        }

        [Fact]
        public void TcpServer_EmptyHost_ThrowsArgumentException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentException>(() => new TcpServer("", 2004, false));
            Assert.Throws<ArgumentException>(() => new TcpServer("   ", 2004, false));
        }

        [Fact]
        public void UdpServer_ValidParameters_CreatesSuccessfully()
        {
            // Arrange & Act
            var server = new UdpServer("127.0.0.1", 2005);

            // Assert
            Assert.NotNull(server);
        }

        [Fact]
        public void UdpServer_InvalidPort_ThrowsArgumentOutOfRangeException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => new UdpServer("127.0.0.1", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new UdpServer("127.0.0.1", -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new UdpServer("127.0.0.1", 65536));
            Assert.Throws<ArgumentOutOfRangeException>(() => new UdpServer("127.0.0.1", 99999));
        }

        [Fact]
        public void UdpServer_NullHost_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new UdpServer(null!, 2005));
        }

        [Fact]
        public void UdpServer_EmptyHost_ThrowsArgumentException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentException>(() => new UdpServer("", 2005));
            Assert.Throws<ArgumentException>(() => new UdpServer("   ", 2005));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(80)]
        [InlineData(443)]
        [InlineData(2000)]
        [InlineData(8080)]
        [InlineData(65535)]
        public void TcpServer_ValidPortRange_CreatesSuccessfully(int port)
        {
            // Arrange & Act
            var server = new TcpServer("0.0.0.0", port, false);

            // Assert
            Assert.NotNull(server);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(80)]
        [InlineData(443)]
        [InlineData(2000)]
        [InlineData(8080)]
        [InlineData(65535)]
        public void UdpServer_ValidPortRange_CreatesSuccessfully(int port)
        {
            // Arrange & Act
            var server = new UdpServer("127.0.0.1", port);

            // Assert
            Assert.NotNull(server);
        }
    }
}
