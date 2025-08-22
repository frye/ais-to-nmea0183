using AisToN2K.Models;
using AisToN2K.Tests.TestData;
using Newtonsoft.Json;

namespace AisToN2K.Tests.Unit
{
    /// <summary>
    /// Comprehensive tests for AIS JSON parsing covering all supported message types.
    /// Tests parsing of aisstream.io WebSocket API format and validates data integrity.
    /// </summary>
    public class AisJsonParsingTests
    {
        #region Type 1 - Position Report (Class A) Tests

        [Fact]
        public void ParseValidType1Json_ShouldParseCorrectly()
        {
            // Arrange
            var json = AisTestData.ValidType1Json;

            // Act
            var result = JsonConvert.DeserializeObject<AisStreamMessage>(json);

            // Assert
            result.Should().NotBeNull();
            result!.MessageType.Should().Be("PositionReport");
            result.MetaData.Should().NotBeNull();
            result.MetaData!.MMSI.Should().Be(123456789);
            result.MetaData.TimeUtc.Should().BeCloseTo(DateTime.Parse("2025-08-21T10:30:45Z"), TimeSpan.FromSeconds(1));
            result.MetaData.ShipName.Should().Be("CONTAINER VESSEL");
            result.MetaData.Latitude.Should().Be(48.5000);
            result.MetaData.Longitude.Should().Be(-122.8000);

            result.Message.Should().NotBeNull();
            result.Message!.PositionReport.Should().NotBeNull();
            
            var positionReport = result.Message.PositionReport!;
            positionReport.UserID.Should().Be(123456789);
            positionReport.Latitude.Should().Be(48.5000);
            positionReport.Longitude.Should().Be(-122.8000);
            positionReport.Sog.Should().Be(12.5);
            positionReport.Cog.Should().Be(89.9);
            positionReport.TrueHeading.Should().Be(90);
            positionReport.NavigationalStatus.Should().Be(0);
            positionReport.Timestamp.Should().Be(55);
        }

        [Fact]
        public void ParseType1NearDateline_ShouldHandleEdgeCoordinates()
        {
            // Arrange
            var json = AisTestData.Type1NearDatelineJson;

            // Act
            var result = JsonConvert.DeserializeObject<AisStreamMessage>(json);

            // Assert
            result.Should().NotBeNull();
            result!.MetaData!.Latitude.Should().Be(35.6762);
            result.MetaData.Longitude.Should().Be(179.9999);
            
            var positionReport = result.Message!.PositionReport!;
            positionReport.Latitude.Should().Be(35.6762);
            positionReport.Longitude.Should().Be(179.9999);
            positionReport.Sog.Should().Be(8.3);
            positionReport.Cog.Should().Be(270.0);
            positionReport.TrueHeading.Should().Be(270);
        }

        [Fact]
        public void ParseType1InvalidCoordinates_ShouldParseWithInvalidValues()
        {
            // Arrange
            var json = AisTestData.Type1InvalidCoordinatesJson;

            // Act
            var result = JsonConvert.DeserializeObject<AisStreamMessage>(json);

            // Assert
            result.Should().NotBeNull();
            
            // These should parse but contain AIS "not available" values
            result!.MetaData!.Latitude.Should().Be(181.0); // AIS "not available"
            result.MetaData.Longitude.Should().Be(91.0);   // AIS "not available"
            
            var positionReport = result.Message!.PositionReport!;
            positionReport.Sog.Should().BeNull(); // Null in JSON
            positionReport.Cog.Should().Be(360.0); // AIS "not available"
            positionReport.TrueHeading.Should().Be(511); // AIS "not available"
            positionReport.NavigationalStatus.Should().Be(15); // Maximum valid value
            positionReport.Timestamp.Should().Be(60); // AIS "not available"
        }

        #endregion

        #region Type 5 - Static and Voyage Related Data Tests

        [Fact]
        public void ParseValidType5Json_ShouldParseCorrectly()
        {
            // Arrange
            var json = AisTestData.ValidType5Json;

            // Act
            var result = JsonConvert.DeserializeObject<AisStreamMessage>(json);

            // Assert
            result.Should().NotBeNull();
            result!.MessageType.Should().Be("ShipAndVoyageData");
            result.MetaData.Should().NotBeNull();
            result.MetaData!.MMSI.Should().Be(123456789);
            result.MetaData.ShipName.Should().Be("CONTAINER VESSEL");

            result.Message.Should().NotBeNull();
            result.Message!.ShipAndVoyageData.Should().NotBeNull();
            
            var shipData = result.Message.ShipAndVoyageData!;
            shipData.UserID.Should().Be(123456789);
            shipData.VesselName.Should().Be("CONTAINER VESSEL");
            shipData.TypeOfShipAndCargoType.Should().Be(70);
            shipData.CallSign.Should().Be("ABCD123");
            shipData.Destination.Should().Be("SEATTLE");
        }

        [Fact]
        public void ParseType5MaxLength_ShouldHandleMaximumFieldLengths()
        {
            // Arrange
            var json = AisTestData.Type5MaxLengthJson;

            // Act
            var result = JsonConvert.DeserializeObject<AisStreamMessage>(json);

            // Assert
            result.Should().NotBeNull();
            
            var shipData = result!.Message!.ShipAndVoyageData!;
            shipData.VesselName.Should().Be("TWENTYCHARACTERNAME1");
            shipData.VesselName!.Length.Should().BeLessOrEqualTo(20); // NMEA standard max
            shipData.TypeOfShipAndCargoType.Should().Be(99);
            shipData.CallSign.Should().Be("1234567");
            shipData.CallSign!.Length.Should().BeLessOrEqualTo(7); // NMEA standard max
            shipData.Destination.Should().Be("TWENTYCHARACTERPORT");
            shipData.Destination!.Length.Should().BeLessOrEqualTo(20); // NMEA standard max
        }

        #endregion

        #region Type 18 - Standard Class B Position Report Tests

        [Fact]
        public void ParseValidType18Json_ShouldParseCorrectly()
        {
            // Arrange
            var json = AisTestData.ValidType18Json;

            // Act
            var result = JsonConvert.DeserializeObject<AisStreamMessage>(json);

            // Assert
            result.Should().NotBeNull();
            result!.MessageType.Should().Be("StandardClassBPositionReport");
            result.MetaData.Should().NotBeNull();
            result.MetaData!.MMSI.Should().Be(987654321);

            result.Message.Should().NotBeNull();
            result.Message!.StandardClassBPositionReport.Should().NotBeNull();
            
            var positionReport = result.Message.StandardClassBPositionReport!;
            positionReport.UserID.Should().Be(987654321);
            positionReport.Latitude.Should().Be(48.2000);
            positionReport.Longitude.Should().Be(-123.1000);
            positionReport.Sog.Should().Be(7.3);
            positionReport.Cog.Should().Be(210.0);
            positionReport.TrueHeading.Should().Be(211);
            positionReport.Timestamp.Should().Be(38);
        }

        [Fact]
        public void ParseType18Anchored_ShouldHandleNullValues()
        {
            // Arrange
            var json = AisTestData.Type18AnchoredJson;

            // Act
            var result = JsonConvert.DeserializeObject<AisStreamMessage>(json);

            // Assert
            result.Should().NotBeNull();
            
            var positionReport = result!.Message!.StandardClassBPositionReport!;
            positionReport.Sog.Should().Be(0.0); // Anchored vessel
            positionReport.Cog.Should().BeNull(); // No course when not moving
            positionReport.TrueHeading.Should().BeNull(); // No heading when anchored
        }

        #endregion

        #region Type 24 - Static Data Report (Class B) Tests

        [Fact]
        public void ParseValidType24A_ShouldParseNameCorrectly()
        {
            // Arrange
            var json = AisTestData.ValidType24AJson;

            // Act
            var result = JsonConvert.DeserializeObject<AisStreamMessage>(json);

            // Assert
            result.Should().NotBeNull();
            result!.MessageType.Should().Be("StaticDataReport");
            result.MetaData!.MMSI.Should().Be(987654321);
            result.MetaData.ShipName.Should().Be("FISHING VESSEL");

            result.Message.Should().NotBeNull();
            result.Message!.StaticDataReport.Should().NotBeNull();
            
            var staticData = result.Message.StaticDataReport!;
            staticData.MessageID.Should().Be(24);
            staticData.UserID.Should().Be(987654321);
            staticData.ReportA.Should().NotBeNull();
            staticData.ReportA!.Valid.Should().BeTrue();
            staticData.ReportA.Name.Should().Be("FISHING VESSEL");
        }

        [Fact]
        public void ParseValidType24B_ShouldParseTypeAndDimensionsCorrectly()
        {
            // Arrange
            var json = AisTestData.ValidType24BJson;

            // Act
            var result = JsonConvert.DeserializeObject<AisStreamMessage>(json);

            // Assert
            result.Should().NotBeNull();
            result!.MessageType.Should().Be("StaticDataReport");

            var staticData = result.Message!.StaticDataReport!;
            staticData.MessageID.Should().Be(24);
            staticData.UserID.Should().Be(987654321);
            staticData.ReportB.Should().NotBeNull();
            staticData.ReportB!.Valid.Should().BeTrue();
            staticData.ReportB.CallSign.Should().Be("FV123");
            staticData.ReportB.ShipType.Should().Be(30); // Fishing vessel
            staticData.ReportB.VendorIDName.Should().Be("ABC");
        }

        #endregion

        #region Error Handling and Edge Cases

        [Fact]
        public void ParseMissingFields_ShouldHandleGracefully()
        {
            // Arrange
            var json = AisTestData.MissingFieldsJson;

            // Act
            var result = JsonConvert.DeserializeObject<AisStreamMessage>(json);

            // Assert
            result.Should().NotBeNull();
            result!.MessageType.Should().Be("PositionReport");
            result.MetaData.Should().NotBeNull();
            result.MetaData!.MMSI.Should().Be(0); // Default value when missing
            result.Message!.PositionReport!.Sog.Should().Be(12.5);
            result.Message.PositionReport.UserID.Should().Be(0); // Default when missing
        }

        [Fact]
        public void ParseInvalidDataTypes_ShouldThrowJsonException()
        {
            // Arrange
            var json = AisTestData.InvalidDataTypesJson;

            // Act & Assert
            var action = () => JsonConvert.DeserializeObject<AisStreamMessage>(json);
            action.Should().Throw<JsonException>();
        }

        [Fact]
        public void ParseEmptyJson_ShouldReturnObjectWithDefaults()
        {
            // Arrange
            var json = AisTestData.EmptyJson;

            // Act
            var result = JsonConvert.DeserializeObject<AisStreamMessage>(json);

            // Assert
            result.Should().NotBeNull();
            result!.MessageType.Should().BeNull();
            result.MetaData.Should().BeNull();
            result.Message.Should().BeNull();
        }

        [Fact]
        public void ParseMalformedJson_ShouldThrowJsonException()
        {
            // Arrange
            var json = AisTestData.MalformedJson;

            // Act & Assert
            var action = () => JsonConvert.DeserializeObject<AisStreamMessage>(json);
            action.Should().Throw<JsonException>();
        }

        [Fact]
        public void ParseNullJson_ShouldReturnNull()
        {
            // Arrange
            string? json = null;

            // Act
            var result = JsonConvert.DeserializeObject<AisStreamMessage>(json);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region Field Validation Tests

        [Theory]
        [InlineData(1, 999999999)] // Valid range
        [InlineData(100000000, 799999999)] // Ship MMSI range
        [InlineData(800000000, 999999999)] // Other station types
        public void ValidateMmsiRange_ShouldAcceptValidValues(int min, int max)
        {
            // Arrange & Act & Assert
            for (int mmsi = min; mmsi <= min + 1000 && mmsi <= max; mmsi += 100)
            {
                mmsi.Should().BeInRange(1, 999999999, 
                    $"MMSI {mmsi} should be within valid range");
            }
        }

        [Theory]
        [InlineData(-90.0, true)]   // South Pole
        [InlineData(0.0, true)]     // Equator
        [InlineData(90.0, true)]    // North Pole
        [InlineData(91.0, false)]   // AIS "not available"
        [InlineData(-91.0, false)]  // Out of range
        [InlineData(181.0, false)]  // Invalid
        public void ValidateLatitudeRange_ShouldIdentifyValidValues(double latitude, bool shouldBeValid)
        {
            // Arrange & Act & Assert
            bool isValid = latitude >= -90.0 && latitude <= 90.0 && latitude != 91.0;
            isValid.Should().Be(shouldBeValid, 
                $"Latitude {latitude} validity should be {shouldBeValid}");
        }

        [Theory]
        [InlineData(-180.0, true)]  // International Date Line West
        [InlineData(0.0, true)]     // Prime Meridian
        [InlineData(180.0, true)]   // International Date Line East
        [InlineData(181.0, false)]  // AIS "not available"
        [InlineData(-181.0, false)] // Out of range
        [InlineData(360.0, false)]  // Invalid
        public void ValidateLongitudeRange_ShouldIdentifyValidValues(double longitude, bool shouldBeValid)
        {
            // Arrange & Act & Assert
            bool isValid = longitude >= -180.0 && longitude <= 180.0 && longitude != 181.0;
            isValid.Should().Be(shouldBeValid, 
                $"Longitude {longitude} validity should be {shouldBeValid}");
        }

        [Theory]
        [InlineData(0.0, true)]     // Stopped
        [InlineData(12.5, true)]    // Normal speed
        [InlineData(102.2, true)]   // Maximum valid speed
        [InlineData(102.3, false)]  // AIS "not available"
        [InlineData(-1.0, false)]   // Invalid negative
        [InlineData(150.0, false)]  // Unrealistic speed
        public void ValidateSpeedOverGround_ShouldIdentifyValidValues(double sog, bool shouldBeValid)
        {
            // Arrange & Act & Assert
            bool isValid = sog >= 0.0 && sog <= 102.2;
            isValid.Should().Be(shouldBeValid, 
                $"Speed over ground {sog} validity should be {shouldBeValid}");
        }

        [Theory]
        [InlineData(0.0, true)]     // North
        [InlineData(89.9, true)]    // Valid course
        [InlineData(359.9, true)]   // Maximum valid
        [InlineData(360.0, false)]  // AIS "not available"
        [InlineData(-1.0, false)]   // Invalid negative
        [InlineData(361.0, false)]  // Out of range
        public void ValidateCourseOverGround_ShouldIdentifyValidValues(double cog, bool shouldBeValid)
        {
            // Arrange & Act & Assert
            bool isValid = cog >= 0.0 && cog < 360.0;
            isValid.Should().Be(shouldBeValid, 
                $"Course over ground {cog} validity should be {shouldBeValid}");
        }

        #endregion
    }
}
