using AisToN2K.Services;
using FluentAssertions;

namespace AisToN2K.Tests.Unit
{
    public class TrackingStoreTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly TrackingStore _store;

        public TrackingStoreTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"tracking-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _store = new TrackingStore(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        [Fact]
        public void Load_ReturnsNull_WhenNoFileExists()
        {
            _store.Load().Should().BeNull();
        }

        [Fact]
        public void Save_And_Load_RoundTrips_ActiveTracking()
        {
            var state = new TrackingState
            {
                Mmsi = 123456789,
                VesselName = "Test Vessel",
                IsPendingTracking = false,
                TrackingStartTime = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
                TotalDistanceNm = 5.5,
                TrackPoints = new List<TrackPointData>
                {
                    new() { Latitude = 48.5, Longitude = -122.5, SpeedOverGround = 7.5, Timestamp = DateTime.UtcNow },
                    new() { Latitude = 48.51, Longitude = -122.51, CourseOverGround = 180.0, Timestamp = DateTime.UtcNow }
                }
            };

            _store.Save(state);
            var loaded = _store.Load();

            loaded.Should().NotBeNull();
            loaded!.Mmsi.Should().Be(123456789);
            loaded.VesselName.Should().Be("Test Vessel");
            loaded.IsPendingTracking.Should().BeFalse();
            loaded.TotalDistanceNm.Should().Be(5.5);
            loaded.TrackPoints.Should().HaveCount(2);
            loaded.TrackPoints[0].Latitude.Should().Be(48.5);
            loaded.TrackPoints[1].CourseOverGround.Should().Be(180.0);
        }

        [Fact]
        public void Save_And_Load_RoundTrips_PendingTracking()
        {
            var state = new TrackingState
            {
                Mmsi = 999999999,
                VesselName = "999999999",
                IsPendingTracking = true,
                TrackingStartTime = null,
                TotalDistanceNm = 0,
                TrackPoints = new()
            };

            _store.Save(state);
            var loaded = _store.Load();

            loaded.Should().NotBeNull();
            loaded!.Mmsi.Should().Be(999999999);
            loaded.IsPendingTracking.Should().BeTrue();
            loaded.TrackingStartTime.Should().BeNull();
        }

        [Fact]
        public void Clear_DeletesPersistedState()
        {
            var state = new TrackingState { Mmsi = 123456789 };
            _store.Save(state);

            _store.Clear();

            _store.Load().Should().BeNull();
        }

        [Fact]
        public void Load_ReturnsNull_OnCorruptedFile()
        {
            var filePath = Path.Combine(_tempDir, "tracking.json");
            File.WriteAllText(filePath, "not valid json {{{");

            _store.Load().Should().BeNull();
        }

        [Fact]
        public void InMemoryStore_SaveAndLoad_Works()
        {
            var memStore = TrackingStore.CreateInMemory();
            memStore.Load().Should().BeNull();

            var state = new TrackingState { Mmsi = 111, VesselName = "Mem" };
            memStore.Save(state);

            var loaded = memStore.Load();
            loaded.Should().NotBeNull();
            loaded!.Mmsi.Should().Be(111);
        }

        [Fact]
        public void InMemoryStore_Clear_Works()
        {
            var memStore = TrackingStore.CreateInMemory();
            memStore.Save(new TrackingState { Mmsi = 111 });
            memStore.Clear();
            memStore.Load().Should().BeNull();
        }
    }
}
