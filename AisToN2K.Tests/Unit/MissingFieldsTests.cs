using AisToN2K.Models;
using AisToN2K.Services;
using AisToN2K.Tests.TestData;
using AisToN2K.Tests.Utilities;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace AisToN2K.Tests.Unit
{
    /// <summary>
    /// Tests specifically for missing data fields in NMEA broadcast,
    /// particularly rate of turn and navigational status.
    /// </summary>
    public class MissingFieldsTests
    {
        [Fact]
        public async Task ConvertType1_WithRateOfTurn_ShouldIncludeInNmeaBroadcast()
        {
            // Arrange - Test that rate of turn is properly included
            var aisData = new AisData
            {
                MessageType = 1,
                Mmsi = 123456789,
                Latitude = 48.5000,
                Longitude = -122.8000,
                SpeedOverGround = 12.5,
                CourseOverGround = 89.9,
                Heading = 90,
                RateOfTurn = -5,  // Port turn
                NavigationalStatus = 0,
                TimestampSeconds = 55,
                PositionAccuracy = true,
                Raim = false
            };

            var converter = new Nmea0183Converter(debugMode: true);

            // Act
            var nmeaResult = await converter.ConvertToNmea0183Async(aisData);

            // Assert
            nmeaResult.Should().NotBeNull("NMEA conversion should succeed");
            nmeaResult.Should().StartWith("!AIVDM", "Should produce valid AIS VDM sentence");
            
            var nmeaSentence = nmeaResult!.TrimEnd();
            var validationResult = NmeaValidator.ValidateAisSentence(nmeaSentence);
            validationResult.IsValid.Should().BeTrue($"NMEA sentence should be valid: {validationResult.ErrorSummary}");

            // Decode the payload to verify rate of turn is included
            var payload = validationResult.ParsedFields!.Payload;
            var decodedFields = DecodeAisType1Fields(payload);
            
            // Verify rate of turn is properly encoded
            decodedFields.Should().ContainKey("RateOfTurn")
                .WhoseValue.Should().Be(-5, "Rate of turn should be correctly encoded in NMEA payload");
            
            // Verify other previously missing fields
            decodedFields.Should().ContainKey("NavigationalStatus")
                .WhoseValue.Should().Be(0, "Navigational status should be included");
            decodedFields.Should().ContainKey("Timestamp")
                .WhoseValue.Should().Be(55, "Timestamp should be included");
            decodedFields.Should().ContainKey("PositionAccuracy")
                .WhoseValue.Should().Be(true, "Position accuracy flag should be included");
            decodedFields.Should().ContainKey("RAIM")
                .WhoseValue.Should().Be(false, "RAIM flag should be included");
        }

        [Fact]
        public async Task ConvertFromAisStreamJson_ShouldExtractAllFields()
        {
            // Arrange - Use complete test data with all fields
            var aisJson = AisTestData.ValidType1Json;
            var aisMessage = JsonConvert.DeserializeObject<AisStreamMessage>(aisJson);
            
            var positionReport = aisMessage!.Message!.PositionReport!;
            var aisData = new AisData
            {
                MessageType = 1,
                Mmsi = positionReport.UserID,
                Latitude = positionReport.Latitude,
                Longitude = positionReport.Longitude,
                SpeedOverGround = positionReport.Sog,
                CourseOverGround = positionReport.Cog,
                Heading = positionReport.TrueHeading,
                RateOfTurn = positionReport.RateOfTurn,
                NavigationalStatus = positionReport.NavigationalStatus,
                TimestampSeconds = positionReport.Timestamp,
                PositionAccuracy = positionReport.PositionAccuracy,
                Raim = positionReport.RAIM
            };

            var converter = new Nmea0183Converter(debugMode: false);

            // Act
            var nmeaResult = await converter.ConvertToNmea0183Async(aisData);

            // Assert
            var validationResult = NmeaValidator.ValidateAisSentence(nmeaResult!.TrimEnd());
            var decodedFields = DecodeAisType1Fields(validationResult.ParsedFields!.Payload);
            
            // Verify all fields from JSON are preserved in NMEA
            decodedFields["RateOfTurn"].Should().Be(-3, "ROT from JSON should be preserved");
            decodedFields["NavigationalStatus"].Should().Be(0, "Nav status from JSON should be preserved");
            decodedFields["Timestamp"].Should().Be(55, "Timestamp from JSON should be preserved");
            decodedFields["PositionAccuracy"].Should().Be(true, "Position accuracy from JSON should be preserved");
            decodedFields["RAIM"].Should().Be(false, "RAIM flag from JSON should be preserved");
        }

        [Theory]
        [InlineData(-128, "Maximum port turn rate")]
        [InlineData(127, "Maximum starboard turn rate")]
        [InlineData(0, "No turn")]
        [InlineData(128, "Rate of turn not available")]
        [InlineData(-200, "Extreme port turn (should be clamped to -127)")]
        [InlineData(200, "Extreme starboard turn (should be clamped to 127)")]
        public async Task ConvertWithVariousRateOfTurn_ShouldHandleAllValues(int inputROT, string description)
        {
            // Arrange
            var aisData = new AisData
            {
                MessageType = 1,
                Mmsi = 123456789,
                Latitude = 48.0,
                Longitude = -122.0,
                RateOfTurn = inputROT
            };

            var converter = new Nmea0183Converter(debugMode: false);

            // Act
            var nmeaResult = await converter.ConvertToNmea0183Async(aisData);

            // Assert
            var validationResult = NmeaValidator.ValidateAisSentence(nmeaResult!.TrimEnd());
            var decodedFields = DecodeAisType1Fields(validationResult.ParsedFields!.Payload);
            
            // Verify ROT is properly handled according to AIS standard
            int expectedROT;
            if (inputROT >= 128 || inputROT < -127)
            {
                // Values outside valid range are encoded as 128 (0x80) "not available"
                // When decoded as signed 8-bit, this becomes -128
                expectedROT = -128;
            }
            else
            {
                // Valid range values are preserved
                expectedROT = Math.Max(-127, Math.Min(127, inputROT));
            }
            
            decodedFields["RateOfTurn"].Should().Be(expectedROT, 
                $"Rate of turn should be correctly handled for {description}");
        }

        [Theory]
        [InlineData(0, "Under way using engine")]
        [InlineData(1, "At anchor")]
        [InlineData(5, "Moored")]
        [InlineData(7, "Engaged in fishing")]
        [InlineData(15, "Not defined")]
        public async Task ConvertWithNavigationalStatus_ShouldEncodeCorrectly(int navStatus, string description)
        {
            // Arrange
            var aisData = new AisData
            {
                MessageType = 1,
                Mmsi = 123456789,
                Latitude = 48.0,
                Longitude = -122.0,
                NavigationalStatus = navStatus
            };

            var converter = new Nmea0183Converter(debugMode: false);

            // Act
            var nmeaResult = await converter.ConvertToNmea0183Async(aisData);

            // Assert
            var validationResult = NmeaValidator.ValidateAisSentence(nmeaResult!.TrimEnd());
            var decodedFields = DecodeAisType1Fields(validationResult.ParsedFields!.Payload);
            
            decodedFields["NavigationalStatus"].Should().Be(navStatus, 
                $"Navigational status should be correctly encoded for {description}");
        }

        [Fact]
        public async Task ConvertType18_WithClassBFields_ShouldIncludeAllFlags()
        {
            // Arrange - Type 18 Class B Position Report
            var aisData = new AisData
            {
                MessageType = 18,
                Mmsi = 987654321,
                Latitude = 47.6062,
                Longitude = -122.3321,
                SpeedOverGround = 8.5,
                CourseOverGround = 180.0,
                Heading = 175,
                TimestampSeconds = 30,
                PositionAccuracy = false,
                Raim = true
            };

            var converter = new Nmea0183Converter(debugMode: false);

            // Act
            var nmeaResult = await converter.ConvertToNmea0183Async(aisData);

            // Assert
            var validationResult = NmeaValidator.ValidateAisSentence(nmeaResult!.TrimEnd());
            var decodedFields = DecodeAisType18Fields(validationResult.ParsedFields!.Payload);
            
            // Verify Class B specific fields are present
            decodedFields.Should().ContainKey("ClassBUnitFlag", "Class B unit flag should be present");
            decodedFields.Should().ContainKey("ClassBDisplayFlag", "Class B display flag should be present");
            decodedFields.Should().ContainKey("ClassBDSCFlag", "Class B DSC flag should be present");
            decodedFields.Should().ContainKey("ClassBBandFlag", "Class B band flag should be present");
            decodedFields.Should().ContainKey("AssignedModeFlag", "Assigned mode flag should be present");
            
            // Verify position accuracy and RAIM are correctly set
            decodedFields["PositionAccuracy"].Should().Be(false, "Position accuracy should match input");
            decodedFields["RAIM"].Should().Be(true, "RAIM flag should match input");
        }

        #region Helper Methods

        private Dictionary<string, object> DecodeAisType1Fields(string payload)
        {
            var binary = SixBitAsciiToBinary(payload);
            var fields = new Dictionary<string, object>();

            fields["MessageType"] = ExtractBits(binary, 0, 6);
            fields["MMSI"] = ExtractBits(binary, 8, 30);
            fields["NavigationalStatus"] = ExtractBits(binary, 38, 4);
            fields["RateOfTurn"] = ExtractSignedBits(binary, 42, 8);
            fields["SpeedOverGround"] = ExtractBits(binary, 50, 10) / 10.0;
            fields["PositionAccuracy"] = ExtractBits(binary, 60, 1) == 1;
            
            var lonRaw = ExtractSignedBits(binary, 61, 28);
            fields["Longitude"] = lonRaw / 600000.0;
            
            var latRaw = ExtractSignedBits(binary, 89, 27);
            fields["Latitude"] = latRaw / 600000.0;
            
            fields["CourseOverGround"] = ExtractBits(binary, 116, 12) / 10.0;
            fields["TrueHeading"] = ExtractBits(binary, 128, 9);
            fields["Timestamp"] = ExtractBits(binary, 137, 6);
            fields["RAIM"] = ExtractBits(binary, 148, 1) == 1;

            return fields;
        }

        private Dictionary<string, object> DecodeAisType18Fields(string payload)
        {
            var binary = SixBitAsciiToBinary(payload);
            var fields = new Dictionary<string, object>();

            fields["MessageType"] = ExtractBits(binary, 0, 6);
            fields["MMSI"] = ExtractBits(binary, 8, 30);
            fields["SpeedOverGround"] = ExtractBits(binary, 46, 10) / 10.0;
            fields["PositionAccuracy"] = ExtractBits(binary, 56, 1) == 1;
            
            var lonRaw = ExtractSignedBits(binary, 57, 28);
            fields["Longitude"] = lonRaw / 600000.0;
            var latRaw = ExtractSignedBits(binary, 85, 27);
            fields["Latitude"] = latRaw / 600000.0;
            
            fields["CourseOverGround"] = ExtractBits(binary, 112, 12) / 10.0;
            fields["TrueHeading"] = ExtractBits(binary, 124, 9);
            fields["Timestamp"] = ExtractBits(binary, 133, 6);
            
            // Class B specific flags
            fields["ClassBUnitFlag"] = ExtractBits(binary, 141, 1) == 1;
            fields["ClassBDisplayFlag"] = ExtractBits(binary, 142, 1) == 1;
            fields["ClassBDSCFlag"] = ExtractBits(binary, 143, 1) == 1;
            fields["ClassBBandFlag"] = ExtractBits(binary, 144, 1) == 1;
            fields["ClassBMessage22Flag"] = ExtractBits(binary, 145, 1) == 1;
            fields["AssignedModeFlag"] = ExtractBits(binary, 146, 1) == 1;
            fields["RAIM"] = ExtractBits(binary, 147, 1) == 1;

            return fields;
        }

        private string SixBitAsciiToBinary(string ascii)
        {
            var binary = "";
            foreach (char c in ascii)
            {
                int value = c;
                if (value >= 48 && value <= 87) value -= 48;
                else if (value >= 96 && value <= 119) value -= 56;
                else value = 0;
                binary += Convert.ToString(value, 2).PadLeft(6, '0');
            }
            return binary;
        }

        private int ExtractBits(string binary, int start, int length)
        {
            if (start + length > binary.Length) return 0;
            var bits = binary.Substring(start, length);
            return Convert.ToInt32(bits, 2);
        }

        private int ExtractSignedBits(string binary, int start, int length)
        {
            var value = ExtractBits(binary, start, length);
            var signBit = 1 << (length - 1);
            if ((value & signBit) != 0)
            {
                value = value - (1 << length);
            }
            return value;
        }

        #endregion
    }
}
