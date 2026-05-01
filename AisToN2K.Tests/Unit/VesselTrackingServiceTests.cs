using AisToN2K.Models;
using AisToN2K.Services;

namespace AisToN2K.Tests.Unit
{
    public class VesselTrackingServiceTests
    {
        private readonly VesselTrackingService _service = new();

        [Fact]
        public void IsTracking_InitiallyFalse()
        {
            _service.IsTracking.Should().BeFalse();
        }

        [Fact]
        public void StartTracking_SetsTrackingState()
        {
            _service.StartTracking(123456789, "Test Vessel");

            _service.IsTracking.Should().BeTrue();
            _service.TrackedMmsi.Should().Be(123456789);
            _service.TrackedVesselName.Should().Be("Test Vessel");
            _service.TotalDistanceNm.Should().Be(0);
            _service.TrackingStartTime.Should().NotBeNull();
        }

        [Fact]
        public void StopTracking_ClearsState_ReturnsPoints()
        {
            _service.StartTracking(123456789, "Test Vessel");
            var data = CreateAisData(123456789, 48.0, -122.0);
            _service.ProcessVesselData(data);

            var points = _service.StopTracking();

            _service.IsTracking.Should().BeFalse();
            _service.TrackedMmsi.Should().BeNull();
            points.Should().HaveCount(1);
        }

        [Fact]
        public void ProcessVesselData_TrackedVessel_AccumulatesPoints()
        {
            _service.StartTracking(123456789, "Test Vessel");

            _service.ProcessVesselData(CreateAisData(123456789, 48.0, -122.0));
            _service.ProcessVesselData(CreateAisData(123456789, 48.01, -122.01));
            _service.ProcessVesselData(CreateAisData(123456789, 48.02, -122.02));

            _service.GetTrackPoints().Should().HaveCount(3);
        }

        [Fact]
        public void ProcessVesselData_DifferentVessel_IgnoredForTracking()
        {
            _service.StartTracking(123456789, "Test Vessel");

            _service.ProcessVesselData(CreateAisData(999999999, 48.0, -122.0));

            _service.GetTrackPoints().Should().BeEmpty();
        }

        [Fact]
        public void ProcessVesselData_AccumulatesDistance()
        {
            _service.StartTracking(123456789, "Test Vessel");

            // ~0.6 nm apart
            _service.ProcessVesselData(CreateAisData(123456789, 48.0, -122.0));
            _service.ProcessVesselData(CreateAisData(123456789, 48.01, -122.0));

            _service.TotalDistanceNm.Should().BeGreaterThan(0);
            _service.TotalDistanceNm.Should().BeLessThan(2.0); // Sanity check
        }

        [Fact]
        public void ProcessVesselData_UpdatesCurrentSog()
        {
            _service.StartTracking(123456789, "Test Vessel");

            var data = CreateAisData(123456789, 48.0, -122.0, sog: 8.5);
            _service.ProcessVesselData(data);

            _service.CurrentSogKnots.Should().Be(8.5);
        }

        [Fact]
        public void RegisterVessel_TracksKnownVessels()
        {
            _service.RegisterVessel(123456789, "Vessel A");
            _service.RegisterVessel(987654321, "Vessel B");

            var known = _service.GetKnownVessels();
            known.Should().HaveCount(2);
            known[123456789].Should().Be("Vessel A");
            known[987654321].Should().Be("Vessel B");
        }

        [Fact]
        public void FindVessels_ByMmsi_ReturnsExactMatch()
        {
            _service.RegisterVessel(123456789, "Test Vessel");

            var results = _service.FindVessels("123456789");

            results.Should().HaveCount(1);
            results[0].Mmsi.Should().Be(123456789);
        }

        [Fact]
        public void FindVessels_ByName_ReturnsCaseInsensitiveMatch()
        {
            _service.RegisterVessel(123456789, "Pacific Explorer");
            _service.RegisterVessel(987654321, "Pacific Runner");
            _service.RegisterVessel(111111111, "Atlantic Voyager");

            var results = _service.FindVessels("pacific");

            results.Should().HaveCount(2);
            results.Select(v => v.Name).Should().Contain("Pacific Explorer");
            results.Select(v => v.Name).Should().Contain("Pacific Runner");
        }

        [Fact]
        public void FindVessels_NoMatch_ReturnsEmpty()
        {
            _service.RegisterVessel(123456789, "Test Vessel");

            var results = _service.FindVessels("nonexistent");
            results.Should().BeEmpty();
        }

        [Fact]
        public void HaversineDistance_KnownValues()
        {
            // Seattle to Portland: ~145 nm
            var distance = VesselTrackingService.HaversineDistanceNm(
                47.6062, -122.3321, // Seattle
                45.5152, -122.6784  // Portland
            );

            distance.Should().BeApproximately(128.0, 5.0);
        }

        [Fact]
        public void HaversineDistance_SamePoint_ReturnsZero()
        {
            var distance = VesselTrackingService.HaversineDistanceNm(48.0, -122.0, 48.0, -122.0);
            distance.Should().Be(0);
        }

        [Fact]
        public void FormatDistance_NauticalUnits()
        {
            _service.Units = DisplayUnits.Nautical;
            _service.StartTracking(123456789, "Test");
            _service.ProcessVesselData(CreateAisData(123456789, 48.0, -122.0));
            _service.ProcessVesselData(CreateAisData(123456789, 48.1, -122.0));

            var formatted = _service.FormatDistance();
            formatted.Should().EndWith("nm");
        }

        [Fact]
        public void FormatDistance_MetricUnits()
        {
            _service.Units = DisplayUnits.Metric;
            _service.StartTracking(123456789, "Test");
            _service.ProcessVesselData(CreateAisData(123456789, 48.0, -122.0));
            _service.ProcessVesselData(CreateAisData(123456789, 48.1, -122.0));

            var formatted = _service.FormatDistance();
            formatted.Should().EndWith("km");
        }

        [Fact]
        public void FormatCurrentSpeed_NauticalUnits()
        {
            _service.Units = DisplayUnits.Nautical;
            _service.StartTracking(123456789, "Test");
            _service.ProcessVesselData(CreateAisData(123456789, 48.0, -122.0, sog: 8.5));

            _service.FormatCurrentSpeed().Should().Be("8.5 kn");
        }

        [Fact]
        public void FormatCurrentSpeed_MetricUnits()
        {
            _service.Units = DisplayUnits.Metric;
            _service.StartTracking(123456789, "Test");
            _service.ProcessVesselData(CreateAisData(123456789, 48.0, -122.0, sog: 8.5));

            _service.FormatCurrentSpeed().Should().Be("15.7 km/h");
        }

        [Fact]
        public void FormatCurrentSpeed_NoData_ReturnsNA()
        {
            _service.FormatCurrentSpeed().Should().Be("N/A");
        }

        [Fact]
        public void ProcessVesselData_AlwaysRegistersVessels()
        {
            // Don't start tracking — just process data
            _service.ProcessVesselData(CreateAisData(123456789, 48.0, -122.0, name: "Vessel A"));
            _service.ProcessVesselData(CreateAisData(987654321, 48.1, -122.1, name: "Vessel B"));

            var known = _service.GetKnownVessels();
            known.Should().HaveCount(2);
        }

        [Fact]
        public void TrackingUpdated_EventFired_OnStartStop()
        {
            int eventCount = 0;
            _service.TrackingUpdated += (s, e) => eventCount++;

            _service.StartTracking(123456789, "Test");
            _service.StopTracking();

            eventCount.Should().Be(2);
        }

        private static AisData CreateAisData(int mmsi, double lat, double lon, double? sog = null, string? name = null)
        {
            return new AisData
            {
                Mmsi = mmsi,
                Latitude = lat,
                Longitude = lon,
                SpeedOverGround = sog,
                VesselName = name,
                Timestamp = DateTime.UtcNow
            };
        }

        // ===== Pending Tracking Tests =====

        [Fact]
        public void StartPendingTracking_SetsStateCorrectly()
        {
            _service.StartPendingTracking(123456789);

            _service.IsTracking.Should().BeTrue();
            _service.IsPendingTracking.Should().BeTrue();
            _service.TrackedMmsi.Should().Be(123456789);
            _service.TrackedVesselName.Should().Be("123456789");
            _service.TrackingStartTime.Should().BeNull();
        }

        [Fact]
        public void StartPendingTracking_ProcessVesselData_ActivatesTracking()
        {
            _service.StartPendingTracking(123456789);

            _service.ProcessVesselData(CreateAisData(123456789, 48.5, -122.5, name: "Test Vessel"));

            _service.IsTracking.Should().BeTrue();
            _service.IsPendingTracking.Should().BeFalse();
            _service.TrackedVesselName.Should().Be("Test Vessel");
            _service.TrackingStartTime.Should().NotBeNull();
        }

        [Fact]
        public void StartPendingTracking_FiresPendingTrackingActivatedEvent()
        {
            _service.StartPendingTracking(123456789);

            (int Mmsi, string Name)? activated = null;
            _service.PendingTrackingActivated += (s, e) => activated = e;

            _service.ProcessVesselData(CreateAisData(123456789, 48.5, -122.5, name: "Test Vessel"));

            activated.Should().NotBeNull();
            activated!.Value.Mmsi.Should().Be(123456789);
            activated.Value.Name.Should().Be("Test Vessel");
        }

        [Fact]
        public void StartPendingTracking_IgnoresNonMatchingVessels()
        {
            _service.StartPendingTracking(123456789);

            _service.ProcessVesselData(CreateAisData(999999999, 48.5, -122.5, name: "Other"));

            _service.IsPendingTracking.Should().BeTrue();
            _service.TrackedVesselName.Should().Be("123456789");
        }

        [Fact]
        public void StartPendingTracking_ActivatesWithMmsiStringIfNoName()
        {
            _service.StartPendingTracking(123456789);

            _service.ProcessVesselData(CreateAisData(123456789, 48.5, -122.5));

            _service.IsPendingTracking.Should().BeFalse();
            _service.TrackedVesselName.Should().Be("123456789");
        }

        [Fact]
        public void StartPendingTracking_StopTrackingClearsPendingState()
        {
            _service.StartPendingTracking(123456789);

            var points = _service.StopTracking();

            _service.IsTracking.Should().BeFalse();
            _service.IsPendingTracking.Should().BeFalse();
            points.Should().BeEmpty();
        }

        [Fact]
        public void StartPendingTracking_TrackPointsAccumulateAfterActivation()
        {
            _service.StartPendingTracking(123456789);

            _service.ProcessVesselData(CreateAisData(123456789, 48.5, -122.5, name: "Test"));
            _service.ProcessVesselData(CreateAisData(123456789, 48.51, -122.51, name: "Test"));

            var points = _service.StopTracking();
            points.Should().HaveCount(2);
            _service.TotalDistanceNm.Should().Be(0); // Reset by StopTracking
        }

        [Fact]
        public void StartTracking_IsNotPending()
        {
            _service.StartTracking(123456789, "Known Vessel");

            _service.IsPendingTracking.Should().BeFalse();
        }
    }

    public class VesselTrackingServicePersistenceTests
    {
        [Fact]
        public void RestoresActiveTracking_FromStore()
        {
            var store = TrackingStore.CreateInMemory();
            store.Save(new TrackingState
            {
                Mmsi = 123456789,
                VesselName = "Persisted Vessel",
                IsPendingTracking = false,
                TrackingStartTime = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
                TotalDistanceNm = 3.2,
                TrackPoints = new List<TrackPointData>
                {
                    new() { Latitude = 48.5, Longitude = -122.5, SpeedOverGround = 5.0, Timestamp = DateTime.UtcNow }
                }
            });

            var service = new VesselTrackingService(trackingStore: store);

            service.IsTracking.Should().BeTrue();
            service.TrackedMmsi.Should().Be(123456789);
            service.TrackedVesselName.Should().Be("Persisted Vessel");
            service.IsPendingTracking.Should().BeFalse();
            service.TotalDistanceNm.Should().Be(3.2);
            service.GetTrackPoints().Should().HaveCount(1);
        }

        [Fact]
        public void RestoresPendingTracking_FromStore()
        {
            var store = TrackingStore.CreateInMemory();
            store.Save(new TrackingState
            {
                Mmsi = 999999999,
                VesselName = "999999999",
                IsPendingTracking = true,
                TrackingStartTime = null,
                TotalDistanceNm = 0,
                TrackPoints = new()
            });

            var service = new VesselTrackingService(trackingStore: store);

            service.IsTracking.Should().BeTrue();
            service.IsPendingTracking.Should().BeTrue();
            service.TrackedMmsi.Should().Be(999999999);
        }

        [Fact]
        public void StopTracking_ClearsPersistedState()
        {
            var store = TrackingStore.CreateInMemory();
            var service = new VesselTrackingService(trackingStore: store);
            service.StartTracking(123456789, "Test");

            store.Load().Should().NotBeNull();

            service.StopTracking();

            store.Load().Should().BeNull();
        }

        [Fact]
        public void StartTracking_PersistsState()
        {
            var store = TrackingStore.CreateInMemory();
            var service = new VesselTrackingService(trackingStore: store);

            service.StartTracking(123456789, "My Boat");

            var state = store.Load();
            state.Should().NotBeNull();
            state!.Mmsi.Should().Be(123456789);
            state.VesselName.Should().Be("My Boat");
            state.IsPendingTracking.Should().BeFalse();
        }

        [Fact]
        public void StartPendingTracking_PersistsState()
        {
            var store = TrackingStore.CreateInMemory();
            var service = new VesselTrackingService(trackingStore: store);

            service.StartPendingTracking(888888888);

            var state = store.Load();
            state.Should().NotBeNull();
            state!.Mmsi.Should().Be(888888888);
            state.IsPendingTracking.Should().BeTrue();
        }

        [Fact]
        public void ProcessVesselData_PersistsTrackPoints()
        {
            var store = TrackingStore.CreateInMemory();
            var service = new VesselTrackingService(trackingStore: store);
            service.StartTracking(123456789, "Test");

            service.ProcessVesselData(new AisData
            {
                Mmsi = 123456789,
                Latitude = 48.5,
                Longitude = -122.5,
                SpeedOverGround = 5.0,
                Timestamp = DateTime.UtcNow
            });

            var state = store.Load();
            state.Should().NotBeNull();
            state!.TrackPoints.Should().HaveCount(1);
            state.TrackPoints[0].Latitude.Should().Be(48.5);
        }

        [Fact]
        public void NoTracking_StoreRemainsEmpty()
        {
            var store = TrackingStore.CreateInMemory();
            var service = new VesselTrackingService(trackingStore: store);

            store.Load().Should().BeNull();
        }
    }
}
