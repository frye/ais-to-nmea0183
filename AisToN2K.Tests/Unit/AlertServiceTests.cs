using AisToN2K.Services;

namespace AisToN2K.Tests.Unit
{
    public class AlertServiceTests
    {
        private AlertService CreateService(TimeSpan? repeatDelay = null, TimeSpan? absenceThreshold = null)
        {
            return new AlertService(AlertStore.CreateInMemory(), repeatDelay, absenceThreshold);
        }

        [Fact]
        public void AddAlert_NewMmsi_ReturnsTrue()
        {
            var service = CreateService();
            var result = service.AddAlert(123456789, "Test Vessel");
            result.Should().BeTrue();
            service.AlertCount.Should().Be(1);
        }

        [Fact]
        public void AddAlert_DuplicateMmsi_ReturnsFalse()
        {
            var service = CreateService();
            service.AddAlert(123456789, "Test Vessel");
            var result = service.AddAlert(123456789, "Test Vessel");
            result.Should().BeFalse();
            service.AlertCount.Should().Be(1);
        }

        [Fact]
        public void AddAlert_MultipleDifferentMmsi_AllAdded()
        {
            var service = CreateService();
            service.AddAlert(111111111, "Vessel A");
            service.AddAlert(222222222, "Vessel B");
            service.AddAlert(333333333, "Vessel C");
            service.AlertCount.Should().Be(3);
        }

        [Fact]
        public void RemoveAlert_ExistingMmsi_ReturnsTrue()
        {
            var service = CreateService();
            service.AddAlert(123456789, "Test Vessel");
            var result = service.RemoveAlert(123456789);
            result.Should().BeTrue();
            service.AlertCount.Should().Be(0);
        }

        [Fact]
        public void RemoveAlert_NonExistentMmsi_ReturnsFalse()
        {
            var service = CreateService();
            var result = service.RemoveAlert(123456789);
            result.Should().BeFalse();
        }

        [Fact]
        public void ClearAlerts_RemovesAll()
        {
            var service = CreateService();
            service.AddAlert(111111111, "A");
            service.AddAlert(222222222, "B");
            service.ClearAlerts();
            service.AlertCount.Should().Be(0);
            service.GetAlerts().Should().BeEmpty();
        }

        [Fact]
        public void IsAlerted_ReturnsTrueForAlertedMmsi()
        {
            var service = CreateService();
            service.AddAlert(123456789, "Test Vessel");
            service.IsAlerted(123456789).Should().BeTrue();
        }

        [Fact]
        public void IsAlerted_ReturnsFalseForNonAlertedMmsi()
        {
            var service = CreateService();
            service.IsAlerted(123456789).Should().BeFalse();
        }

        [Fact]
        public void IsAlerted_ReturnsFalseAfterRemoval()
        {
            var service = CreateService();
            service.AddAlert(123456789, "Test Vessel");
            service.RemoveAlert(123456789);
            service.IsAlerted(123456789).Should().BeFalse();
        }

        [Fact]
        public void CheckAndFire_AlertedVessel_FiresImmediately()
        {
            var service = CreateService();
            service.AddAlert(123456789, "Test Vessel");
            var result = service.CheckAndFire(123456789, "Test Vessel");
            result.Should().BeTrue();
        }

        [Fact]
        public void CheckAndFire_NonAlertedVessel_ReturnsFalse()
        {
            var service = CreateService();
            var result = service.CheckAndFire(123456789, "Test Vessel");
            result.Should().BeFalse();
        }

        [Fact]
        public void CheckAndFire_SecondCallWithinRepeatDelay_DoesNotFire()
        {
            // repeatDelay = 2 min, so immediate second call should not fire
            var service = CreateService(repeatDelay: TimeSpan.FromMinutes(2));
            service.AddAlert(123456789, "Test Vessel");

            service.CheckAndFire(123456789, "Test Vessel").Should().BeTrue();
            service.CheckAndFire(123456789, "Test Vessel").Should().BeFalse();
        }

        [Fact]
        public void CheckAndFire_AfterRepeatDelay_FiresOnceMore()
        {
            // Use a very short repeat delay so it fires on the next call
            var service = CreateService(repeatDelay: TimeSpan.Zero);
            service.AddAlert(123456789, "Test Vessel");

            service.CheckAndFire(123456789, "Test Vessel").Should().BeTrue();  // first fire
            service.CheckAndFire(123456789, "Test Vessel").Should().BeTrue();  // repeat fire (delay=0)
        }

        [Fact]
        public void CheckAndFire_AfterRepeatFire_NoMoreFires()
        {
            // After both fires, no more until absence threshold
            var service = CreateService(repeatDelay: TimeSpan.Zero, absenceThreshold: TimeSpan.FromHours(1));
            service.AddAlert(123456789, "Test Vessel");

            service.CheckAndFire(123456789, "Test Vessel").Should().BeTrue();  // fire 1
            service.CheckAndFire(123456789, "Test Vessel").Should().BeTrue();  // fire 2 (repeat)
            service.CheckAndFire(123456789, "Test Vessel").Should().BeFalse(); // no more
            service.CheckAndFire(123456789, "Test Vessel").Should().BeFalse(); // still no
        }

        [Fact]
        public void CheckAndFire_FiresEventOnTrigger()
        {
            var service = CreateService();
            service.AddAlert(123456789, "Test Vessel");

            (int Mmsi, string? Name)? received = null;
            service.AlertTriggered += (s, e) => received = e;

            service.CheckAndFire(123456789, "Test Vessel");

            received.Should().NotBeNull();
            received!.Value.Mmsi.Should().Be(123456789);
            received.Value.Name.Should().Be("Test Vessel");
        }

        [Fact]
        public void CheckAndFire_DoesNotFireEventWhenCoolingDown()
        {
            var service = CreateService(repeatDelay: TimeSpan.FromMinutes(5));
            service.AddAlert(123456789, "Test Vessel");

            int fireCount = 0;
            service.AlertTriggered += (s, e) => fireCount++;

            service.CheckAndFire(123456789, "Test Vessel");
            service.CheckAndFire(123456789, "Test Vessel");

            fireCount.Should().Be(1);
        }

        [Fact]
        public void GetAlerts_ReturnsDefensiveCopy()
        {
            var service = CreateService();
            service.AddAlert(123456789, "Test Vessel");

            var alerts = service.GetAlerts();
            alerts.Clear();

            service.AlertCount.Should().Be(1);
        }

        [Fact]
        public void GetAlerts_ContainsCorrectData()
        {
            var service = CreateService();
            service.AddAlert(123456789, "Test Vessel");

            var alerts = service.GetAlerts();
            alerts.Should().HaveCount(1);
            alerts[0].Mmsi.Should().Be(123456789);
            alerts[0].Name.Should().Be("Test Vessel");
        }

        [Fact]
        public void UpdateAlertName_UpdatesExistingAlert()
        {
            var service = CreateService();
            service.AddAlert(123456789);

            service.UpdateAlertName(123456789, "Now Known");

            var alerts = service.GetAlerts();
            alerts[0].Name.Should().Be("Now Known");
        }

        [Fact]
        public void UpdateAlertName_NonExistentMmsi_NoOp()
        {
            var service = CreateService();
            service.UpdateAlertName(123456789, "No Such Alert");
            service.AlertCount.Should().Be(0);
        }

        [Fact]
        public void ClearAlerts_ResetsCooldownState()
        {
            var service = CreateService(repeatDelay: TimeSpan.FromMinutes(5));
            service.AddAlert(123456789, "Test");
            service.CheckAndFire(123456789, "Test");

            service.ClearAlerts();
            service.AddAlert(123456789, "Test");

            // Should fire again since cooldown state was cleared
            service.CheckAndFire(123456789, "Test").Should().BeTrue();
        }

        [Fact]
        public void RemoveAlert_ResetsCooldownForThatVessel()
        {
            var service = CreateService(repeatDelay: TimeSpan.FromMinutes(5));
            service.AddAlert(123456789, "Test");
            service.CheckAndFire(123456789, "Test");

            service.RemoveAlert(123456789);
            service.AddAlert(123456789, "Test");

            // Should fire again since cooldown was removed with the alert
            service.CheckAndFire(123456789, "Test").Should().BeTrue();
        }

        [Fact]
        public void CheckAndFire_AbsenceResetsFireCycle()
        {
            // absenceThreshold = 0 so any gap counts as "absent"
            var service = CreateService(repeatDelay: TimeSpan.FromHours(1), absenceThreshold: TimeSpan.Zero);
            service.AddAlert(123456789, "Test");

            service.CheckAndFire(123456789, "Test").Should().BeTrue();  // first fire

            // With absenceThreshold=0, next call sees lastHeard == now, gap >= 0 → resets
            service.CheckAndFire(123456789, "Test").Should().BeTrue();  // absence reset
        }

        [Fact]
        public void CheckAndFire_TwoFiresThenSilent_WithLargeAbsence()
        {
            // Verify the full cycle: fire1, fire2 (repeat), then silence
            var service = CreateService(repeatDelay: TimeSpan.Zero, absenceThreshold: TimeSpan.FromHours(24));
            service.AddAlert(123456789, "Test");

            int fireCount = 0;
            service.AlertTriggered += (s, e) => fireCount++;

            service.CheckAndFire(123456789, "Test"); // fire 1
            service.CheckAndFire(123456789, "Test"); // fire 2 (repeat, delay=0)
            service.CheckAndFire(123456789, "Test"); // no fire
            service.CheckAndFire(123456789, "Test"); // no fire

            fireCount.Should().Be(2);
        }
    }
}
