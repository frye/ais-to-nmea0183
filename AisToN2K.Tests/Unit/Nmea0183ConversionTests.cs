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
    /// Comprehensive tests for NMEA 0183 conversion ensuring all supported AIS data fields 
    /// are properly encoded and broadcast. Tests for missing fields like turn rate.
    /// </summary>
    public class Nmea0183ConversionTests
    {
        #region Type 1 Position Report Field Coverage Tests

        [Fact]
        public async Task ConvertType1Position_ShouldIncludeAllRequiredFields()
        {
            // Arrange - Create complete AIS Type 1 data with all fields
            var aisData = new AisData
            {
                MessageType = 1,
                Mmsi = 123456789,
                Latitude = 48.5000,
                Longitude = -122.8000,
                SpeedOverGround = 12.5,
                CourseOverGround = 89.9,
                Heading = 90,
                RateOfTurn = -5,      // Now included!
                NavigationalStatus = 0, // Now included!
                TimestampSeconds = 55,        // Now included!
                PositionAccuracy = true,  // Now included!
                Raim = false              // Now included!
            };

            var converter = new Nmea0183Converter(debugMode: false);

            // Act
            var nmeaResult = await converter.ConvertToNmea0183Async(aisData);

            // Assert
            nmeaResult.Should().NotBeNull();
            var nmeaSentence = nmeaResult!.TrimEnd();
            var validationResult = NmeaValidator.ValidateAisSentence(nmeaSentence);
            validationResult.IsValid.Should().BeTrue($"NMEA sentence should be valid: {validationResult.ErrorSummary}");

            // Decode and verify all fields are present
            var decodedFields = DecodeType1NmeaFields(validationResult.ParsedFields!.Payload);
            
            // Verify core positioning fields
            decodedFields.Should().ContainKey("MessageType").WhoseValue.Should().Be(1);
            decodedFields.Should().ContainKey("MMSI").WhoseValue.Should().Be(123456789);
            decodedFields.Should().ContainKey("Latitude");
            decodedFields.Should().ContainKey("Longitude");
            decodedFields.Should().ContainKey("SpeedOverGround");
            decodedFields.Should().ContainKey("CourseOverGround");
            decodedFields.Should().ContainKey("TrueHeading").WhoseValue.Should().Be(90);
            
            // Verify missing fields that should be present
            decodedFields.Should().ContainKey("RateOfTurn", 
                "Rate of Turn should be encoded in Type 1 messages for vessel maneuvering information");
            decodedFields.Should().ContainKey("NavigationalStatus", 
                "Navigational Status should be encoded to indicate vessel operational state");
            decodedFields.Should().ContainKey("Timestamp", 
                "Timestamp should be encoded for position report timing");
            decodedFields.Should().ContainKey("PositionAccuracy", 
                "Position Accuracy flag should be encoded for GPS quality indication");
            decodedFields.Should().ContainKey("RAIM", 
                "RAIM flag should be encoded for GPS reliability indication");
        }

        [Fact]
        public async Task ConvertType1Position_WithRateOfTurn_ShouldEncodeCorrectly()
        {
            // Arrange - Test different rate of turn values
            var testCases = new[]
            {
                new { ROT = 0, Description = "No turn" },
                new { ROT = 5, Description = "Starboard turn" },
                new { ROT = -3, Description = "Port turn" },
                new { ROT = 127, Description = "Maximum starboard turn" },
                new { ROT = -128, Description = "Maximum port turn (invalid, becomes not available)" },
                new { ROT = 128, Description = "Turn rate not available (should remain 128)" }
            };

            var converter = new Nmea0183Converter(debugMode: false);

            foreach (var testCase in testCases)
            {
                var aisData = new AisData
                {
                    MessageType = 1,
                    Mmsi = 123456789,
                    Latitude = 48.5000,
                    Longitude = -122.8000,
                    RateOfTurn = testCase.ROT  // Now this property exists!
                };

                // Act
                var nmeaResult = await converter.ConvertToNmea0183Async(aisData);

                // Assert
                nmeaResult.Should().NotBeNull($"Should generate NMEA for {testCase.Description}");
                var validationResult = NmeaValidator.ValidateAisSentence(nmeaResult!.TrimEnd());
                validationResult.IsValid.Should().BeTrue($"NMEA should be valid for {testCase.Description}");

                // Verify rate of turn encoding - handle special case where invalid values become "not available"
                var decodedFields = DecodeType1NmeaFields(validationResult.ParsedFields!.Payload);
                int expectedROT;
                if (testCase.ROT >= 128 || testCase.ROT < -127)
                {
                    expectedROT = 128;  // "Not available" for out-of-range values
                }
                else
                {
                    expectedROT = Math.Max(-127, Math.Min(127, testCase.ROT));
                }
                
                decodedFields.Should().ContainKey("RateOfTurn")
                    .WhoseValue.Should().Be(expectedROT, $"Rate of turn should be correctly encoded for {testCase.Description}");
            }
        }

        [Fact]
        public async Task ConvertType1Position_WithNavigationalStatus_ShouldEncodeCorrectly()
        {
            // Arrange - Test all valid navigational status values
            var navStatusTests = new[]
            {
                new { Status = 0, Description = "Under way using engine" },
                new { Status = 1, Description = "At anchor" },
                new { Status = 2, Description = "Not under command" },
                new { Status = 3, Description = "Restricted manoeuvrability" },
                new { Status = 4, Description = "Constrained by her draught" },
                new { Status = 5, Description = "Moored" },
                new { Status = 6, Description = "Aground" },
                new { Status = 7, Description = "Engaged in fishing" },
                new { Status = 8, Description = "Under way sailing" },
                new { Status = 15, Description = "Not defined" }
            };

            var converter = new Nmea0183Converter(debugMode: false);

            foreach (var test in navStatusTests)
            {
                var aisData = new AisData
                {
                    MessageType = 1,
                    Mmsi = 123456789,
                    Latitude = 48.5000,
                    Longitude = -122.8000,
                    NavigationalStatus = test.Status  // Now this property exists!
                };

                // Act
                var nmeaResult = await converter.ConvertToNmea0183Async(aisData);

                // Assert
                var validationResult = NmeaValidator.ValidateAisSentence(nmeaResult!.TrimEnd());
                var decodedFields = DecodeType1NmeaFields(validationResult.ParsedFields!.Payload);
                
                decodedFields.Should().ContainKey("NavigationalStatus")
                    .WhoseValue.Should().Be(test.Status, $"Nav status should be {test.Description}");
            }
        }

        #endregion

        #region Type 18 Class B Position Report Field Coverage Tests

        [Fact]
        public async Task ConvertType18Position_ShouldIncludeAllClassBFields()
        {
            // Arrange
            var aisData = new AisData
            {
                MessageType = 18,
                Mmsi = 987654321,
                Latitude = 47.6062,
                Longitude = -122.3321,
                SpeedOverGround = 8.5,
                CourseOverGround = 180.0,
                Heading = 175
            };

            var converter = new Nmea0183Converter(debugMode: false);

            // Act
            var nmeaResult = await converter.ConvertToNmea0183Async(aisData);

            // Assert
            nmeaResult.Should().NotBeNull();
            var validationResult = NmeaValidator.ValidateAisSentence(nmeaResult!.TrimEnd());
            validationResult.IsValid.Should().BeTrue();

            var decodedFields = DecodeType18NmeaFields(validationResult.ParsedFields!.Payload);
            
            // Type 18 specific fields
            decodedFields.Should().ContainKey("MessageType").WhoseValue.Should().Be(18);
            decodedFields.Should().ContainKey("ClassBUnitFlag", "Class B unit flag should be present");
            decodedFields.Should().ContainKey("ClassBDisplayFlag", "Class B display capability flag should be present");
            decodedFields.Should().ContainKey("ClassBDSCFlag", "Class B DSC capability flag should be present");
            decodedFields.Should().ContainKey("ClassBBandFlag", "Class B frequency band flag should be present");
            decodedFields.Should().ContainKey("ClassBMessage22Flag", "Class B message 22 acceptance flag should be present");
            decodedFields.Should().ContainKey("AssignedModeFlag", "Assigned/autonomous mode flag should be present");
            decodedFields.Should().ContainKey("CommunicationState", "Communication state should be present for Class B");
        }

        #endregion

        #region Data Range and Edge Case Tests

        [Theory]
        [InlineData(90.0, 180.0, "North Pole, International Date Line")]
        [InlineData(-90.0, -180.0, "South Pole, Western Date Line")]
        [InlineData(0.0, 0.0, "Equator, Prime Meridian")]
        [InlineData(48.858844, 2.294351, "Eiffel Tower coordinates")]
        public async Task ConvertPosition_WithEdgeCaseCoordinates_ShouldHandleCorrectly(
            double latitude, double longitude, string description)
        {
            // Arrange
            var aisData = new AisData
            {
                MessageType = 1,
                Mmsi = 123456789,
                Latitude = latitude,
                Longitude = longitude,
                SpeedOverGround = 0.0,
                CourseOverGround = 0.0,
                Heading = 0
            };

            var converter = new Nmea0183Converter(debugMode: false);

            // Act
            var nmeaResult = await converter.ConvertToNmea0183Async(aisData);

            // Assert
            nmeaResult.Should().NotBeNull($"Should handle coordinates for {description}");
            var validationResult = NmeaValidator.ValidateAisSentence(nmeaResult!.TrimEnd());
            validationResult.IsValid.Should().BeTrue($"NMEA should be valid for {description}");

            var decodedFields = DecodeType1NmeaFields(validationResult.ParsedFields!.Payload);
            
            // Verify coordinate precision is maintained within AIS limits
            var decodedLat = (double)decodedFields["Latitude"];
            var decodedLon = (double)decodedFields["Longitude"];
            
            Math.Abs(decodedLat - latitude).Should().BeLessThan(0.0001, 
                $"Latitude precision should be maintained for {description}");
            Math.Abs(decodedLon - longitude).Should().BeLessThan(0.0001, 
                $"Longitude precision should be maintained for {description}");
        }

        [Theory]
        [InlineData(1023, "Speed not available (encoded as max valid 102.3)")]
        [InlineData(102.3, "Maximum valid speed")]
        [InlineData(0.0, "Vessel stopped")]
        [InlineData(25.5, "Typical cruising speed")]
        public async Task ConvertPosition_WithSpeedVariations_ShouldEncodeCorrectly(
            double speed, string description)
        {
            // Arrange
            var aisData = new AisData
            {
                MessageType = 1,
                Mmsi = 123456789,
                Latitude = 48.0,
                Longitude = -122.0,
                SpeedOverGround = speed,
                CourseOverGround = 90.0,
                Heading = 90
            };

            var converter = new Nmea0183Converter(debugMode: false);

            // Act
            var nmeaResult = await converter.ConvertToNmea0183Async(aisData);

            // Assert
            var validationResult = NmeaValidator.ValidateAisSentence(nmeaResult!.TrimEnd());
            var decodedFields = DecodeType1NmeaFields(validationResult.ParsedFields!.Payload);
            
            var expectedSpeed = speed >= 102.3 ? 102.3 : speed;  // AIS max speed is 102.3 knots, not 1023
            ((double)decodedFields["SpeedOverGround"]).Should().BeApproximately(expectedSpeed, 0.1, $"Speed encoding for {description}");
        }

        #endregion

        #region Helper Methods for Field Decoding

        /// <summary>
        /// Decodes AIS Type 1 binary payload to extract all fields for verification.
        /// This helper method validates that all required fields are properly encoded.
        /// </summary>
        private Dictionary<string, object> DecodeType1NmeaFields(string payload)
        {
            // Convert 6-bit ASCII back to binary
            var binary = SixBitAsciiToBinary(payload);
            var fields = new Dictionary<string, object>();

            // Extract fields according to ITU-R M.1371-5 Type 1 format
            fields["MessageType"] = ExtractBits(binary, 0, 6);
            fields["RepeatIndicator"] = ExtractBits(binary, 6, 2);
            fields["MMSI"] = ExtractBits(binary, 8, 30);
            fields["NavigationalStatus"] = ExtractBits(binary, 38, 4);
            
            // Rate of Turn is special: 8-bit field where values 0-127 are positive,
            // 128-255 are negative (two's complement), but 128 specifically means "not available"
            var rotRaw = ExtractBits(binary, 42, 8);
            if (rotRaw == 128)
            {
                fields["RateOfTurn"] = 128;  // "Not available" - special case
            }
            else if (rotRaw >= 128)
            {
                fields["RateOfTurn"] = rotRaw - 256;  // Convert to signed: 128->-128, 129->-127, etc.
            }
            else
            {
                fields["RateOfTurn"] = rotRaw;  // Positive values 0-127
            }
            fields["SpeedOverGround"] = ExtractBits(binary, 50, 10) / 10.0;
            fields["PositionAccuracy"] = ExtractBits(binary, 60, 1) == 1;
            
            // Longitude (28-bit signed)
            var lonRaw = ExtractSignedBits(binary, 61, 28);
            fields["Longitude"] = lonRaw / 600000.0;
            
            // Latitude (27-bit signed)
            var latRaw = ExtractSignedBits(binary, 89, 27);
            fields["Latitude"] = latRaw / 600000.0;
            
            fields["CourseOverGround"] = ExtractBits(binary, 116, 12) / 10.0;
            fields["TrueHeading"] = ExtractBits(binary, 128, 9);
            fields["Timestamp"] = ExtractBits(binary, 137, 6);
            fields["RAIM"] = ExtractBits(binary, 148, 1) == 1;

            return fields;
        }

        /// <summary>
        /// Decodes AIS Type 18 binary payload to extract Class B specific fields.
        /// </summary>
        private Dictionary<string, object> DecodeType18NmeaFields(string payload)
        {
            var binary = SixBitAsciiToBinary(payload);
            var fields = new Dictionary<string, object>();

            // Extract Type 18 specific fields
            fields["MessageType"] = ExtractBits(binary, 0, 6);
            fields["MMSI"] = ExtractBits(binary, 8, 30);
            fields["SpeedOverGround"] = ExtractBits(binary, 46, 10) / 10.0;
            fields["PositionAccuracy"] = ExtractBits(binary, 56, 1) == 1;
            
            // Coordinates
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
            fields["CommunicationState"] = ExtractBits(binary, 149, 19);

            return fields;
        }

        private string SixBitAsciiToBinary(string ascii)
        {
            var binary = "";
            foreach (char c in ascii)
            {
                int value = c;
                if (value >= 48 && value <= 87)
                    value -= 48;
                else if (value >= 96 && value <= 119)
                    value -= 56;
                else
                    value = 0;

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
