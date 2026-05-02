using AisToN2K.Services;

namespace AisToN2K.Tests.Unit
{
    public class AlertStoreTests : IDisposable
    {
        private readonly string _tempDir;

        public AlertStoreTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"alert-store-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        [Fact]
        public void InMemoryStore_LoadReturnsEmpty()
        {
            var store = AlertStore.CreateInMemory();
            var alerts = store.Load();
            alerts.Should().BeEmpty();
        }

        [Fact]
        public void InMemoryStore_SaveDoesNotThrow()
        {
            var store = AlertStore.CreateInMemory();
            store.Save(new List<AlertEntry>
            {
                new AlertEntry { Mmsi = 123456789, Name = "Test" }
            });
            // Should not throw — just a no-op
        }

        [Fact]
        public void DiskStore_SaveAndLoad_RoundTrips()
        {
            var store = new AlertStore(_tempDir);
            var original = new List<AlertEntry>
            {
                new AlertEntry { Mmsi = 111111111, Name = "Alpha", CreatedAt = DateTime.UtcNow },
                new AlertEntry { Mmsi = 222222222, Name = "Bravo", CreatedAt = DateTime.UtcNow },
            };

            store.Save(original);

            var loaded = store.Load();
            loaded.Should().HaveCount(2);
            loaded[0].Mmsi.Should().Be(111111111);
            loaded[0].Name.Should().Be("Alpha");
            loaded[1].Mmsi.Should().Be(222222222);
            loaded[1].Name.Should().Be("Bravo");
        }

        [Fact]
        public void DiskStore_Load_MissingFile_ReturnsEmpty()
        {
            var store = new AlertStore(_tempDir);
            var loaded = store.Load();
            loaded.Should().BeEmpty();
        }

        [Fact]
        public void DiskStore_SaveEmpty_ClearsFile()
        {
            var store = new AlertStore(_tempDir);
            store.Save(new List<AlertEntry>
            {
                new AlertEntry { Mmsi = 123456789, Name = "Test" }
            });
            store.Save(new List<AlertEntry>());

            var loaded = store.Load();
            loaded.Should().BeEmpty();
        }

        [Fact]
        public void DiskStore_CorruptFile_ReturnsEmpty()
        {
            var filePath = Path.Combine(_tempDir, "alerts.json");
            File.WriteAllText(filePath, "{ not valid json ]]]");

            var store = new AlertStore(_tempDir);
            var loaded = store.Load();
            loaded.Should().BeEmpty();
        }

        [Fact]
        public void DiskStore_NullName_Preserved()
        {
            var store = new AlertStore(_tempDir);
            store.Save(new List<AlertEntry>
            {
                new AlertEntry { Mmsi = 123456789, Name = null }
            });

            var loaded = store.Load();
            loaded.Should().HaveCount(1);
            loaded[0].Name.Should().BeNull();
        }
    }
}
