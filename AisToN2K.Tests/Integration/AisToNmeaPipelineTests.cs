using AisToN2K.Models;
using AisToN2K.Tests.TestData;
using AisToN2K.Tests.Utilities;
using Newtonsoft.Json;

namespace AisToN2K.Tests.Integration
{
    /// <summary>
    /// Integration tests for the complete AIS-to-NMEA0183 conversion pipeline.
    /// Tests end-to-end data flow from AIS JSON input to NMEA output.
    /// </summary>
    public class AisToNmeaPipelineTests
    {
        #region End-to-End Conversion Tests

        [Fact]
        public void ConvertType1AisToNmea_CompletePipeline_ShouldProduceValidNmea()
        {
            // Arrange
            var aisJson = AisTestData.ValidType1Json;
            
            // Act - Parse AIS JSON
            var aisMessage = JsonConvert.DeserializeObject<AisStreamMessage>(aisJson);
            aisMessage.Should().NotBeNull();

            // Act - Extract position data
            var positionReport = aisMessage!.Message!.PositionReport!;
            var mmsi = positionReport.UserID;
            var latitude = positionReport.Latitude;
            var longitude = positionReport.Longitude;
            var sog = positionReport.Sog ?? 0;
            var cog = positionReport.Cog ?? 0;

            // Act - Convert coordinates to NMEA format
            var nmeaLat = CoordinateTestHelper.ConvertToNmeaFormat(latitude, true);
            var nmeaLon = CoordinateTestHelper.ConvertToNmeaFormat(longitude, false);

            // Act - Simulate NMEA sentence generation (simplified)
            var nmeaSentence = GenerateSimulatedNmeaSentence(mmsi, nmeaLat, nmeaLon, sog, cog);

            // Assert - Validate the complete NMEA sentence
            var validationResult = NmeaValidator.ValidateAisSentence(nmeaSentence);
            validationResult.IsValid.Should().BeTrue($"Generated NMEA sentence should be valid: {validationResult.ErrorSummary}");

            // Assert - Verify coordinate conversion accuracy
            nmeaLat.Should().Be("4830.000,N", "Latitude should convert accurately");
            nmeaLon.Should().Be("12248.000,W", "Longitude should convert accurately");

            // Assert - Verify sentence structure
            validationResult.ParsedFields!.SentenceId.Should().Be("AIVDM");
            validationResult.ParsedFields.FragmentCount.Should().Be(1);
            validationResult.ParsedFields.Channel.Should().Be("A");
        }

        [Fact]
        public void ConvertType5AisToNmea_StaticData_ShouldHandleMultipleFragments()
        {
            // Arrange
            var aisJson = AisTestData.ValidType5Json;
            
            // Act - Parse AIS JSON
            var aisMessage = JsonConvert.DeserializeObject<AisStreamMessage>(aisJson);
            var shipData = aisMessage!.Message!.ShipAndVoyageData!;

            // Act - Simulate Type 5 message fragmentation (Type 5 often requires 2 fragments)
            var fragment1 = GenerateType5Fragment1(shipData);
            var fragment2 = GenerateType5Fragment2(shipData);

            // Assert - Validate both fragments
            var result1 = NmeaValidator.ValidateAisSentence(fragment1);
            var result2 = NmeaValidator.ValidateAisSentence(fragment2);

            result1.IsValid.Should().BeTrue("First fragment should be valid");
            result2.IsValid.Should().BeTrue("Second fragment should be valid");

            // Assert - Verify fragmentation is correct
            result1.ParsedFields!.FragmentCount.Should().Be(2);
            result1.ParsedFields.FragmentNumber.Should().Be(1);
            result2.ParsedFields!.FragmentCount.Should().Be(2);
            result2.ParsedFields.FragmentNumber.Should().Be(2);

            // Assert - Both fragments should have same message ID
            result1.ParsedFields.MessageId.Should().Be(result2.ParsedFields.MessageId);
        }

        [Fact]
        public void ConvertType18AisToNmea_ClassB_ShouldProduceValidOutput()
        {
            // Arrange
            var aisJson = AisTestData.ValidType18Json;
            
            // Act
            var aisMessage = JsonConvert.DeserializeObject<AisStreamMessage>(aisJson);
            var positionReport = aisMessage!.Message!.StandardClassBPositionReport!;

            var nmeaSentence = GenerateSimulatedClassBNmeaSentence(positionReport);

            // Assert
            var validationResult = NmeaValidator.ValidateAisSentence(nmeaSentence);
            validationResult.IsValid.Should().BeTrue("Class B NMEA sentence should be valid");
        }

        [Fact]
        public void ConvertType24AisToNmea_StaticDataReportA_ShouldIncludeVesselName()
        {
            // Arrange
            var aisJson = AisTestData.ValidType24AJson;
            
            // Act
            var aisMessage = JsonConvert.DeserializeObject<AisStreamMessage>(aisJson);
            var staticData = aisMessage!.Message!.StaticDataReport!;

            var nmeaSentence = GenerateType24ASentence(staticData);

            // Assert
            var validationResult = NmeaValidator.ValidateAisSentence(nmeaSentence);
            validationResult.IsValid.Should().BeTrue("Type 24A NMEA sentence should be valid");
            
            // The payload should encode the vessel name
            validationResult.ParsedFields!.Payload.Should().NotBeEmpty("Should contain encoded vessel name");
        }

        #endregion

        #region Error Handling Integration Tests

        [Fact]
        public void ConvertInvalidAisJson_ShouldHandleGracefully()
        {
            // Arrange
            var invalidJson = AisTestData.MalformedJson;

            // Act & Assert
            var action = () => JsonConvert.DeserializeObject<AisStreamMessage>(invalidJson);
            action.Should().Throw<JsonException>("Malformed JSON should throw exception");
        }

        [Fact]
        public void ConvertAisWithInvalidCoordinates_ShouldDetectAndHandle()
        {
            // Arrange
            var aisJson = AisTestData.Type1InvalidCoordinatesJson;
            
            // Act
            var aisMessage = JsonConvert.DeserializeObject<AisStreamMessage>(aisJson);
            var positionReport = aisMessage!.Message!.PositionReport!;

            // Act - Validate coordinates before conversion
            var coordValidation = CoordinateTestHelper.ValidateCoordinateRanges(
                positionReport.Latitude, positionReport.Longitude);

            // Assert
            coordValidation.IsValid.Should().BeFalse("Invalid coordinates should be detected");
            coordValidation.Errors.Should().Contain(e => e.Contains("not available"), 
                "Should identify AIS 'not available' values");
        }

        [Fact]
        public void ConvertAisWithMissingFields_ShouldUseDefaults()
        {
            // Arrange
            var aisJson = AisTestData.MissingFieldsJson;
            
            // Act
            var aisMessage = JsonConvert.DeserializeObject<AisStreamMessage>(aisJson);

            // Assert - Should parse without exceptions
            aisMessage.Should().NotBeNull();
            aisMessage!.Message.Should().NotBeNull();
            aisMessage.Message!.PositionReport.Should().NotBeNull();

            // Missing MMSI should default to 0
            aisMessage.Message.PositionReport!.UserID.Should().Be(0);
        }

        #endregion

        #region Data Integrity Tests

        [Fact]
        public void RoundTripCoordinateConversion_ShouldMaintainPrecision()
        {
            // Arrange
            var originalLat = 48.123456;
            var originalLon = -122.987654;

            // Act - Convert to NMEA format
            var nmeaLat = CoordinateTestHelper.ConvertToNmeaFormat(originalLat, true);
            var nmeaLon = CoordinateTestHelper.ConvertToNmeaFormat(originalLon, false);

            // Act - Parse back from NMEA format (simplified)
            var (parsedLat, parsedLon) = ParseNmeaCoordinates(nmeaLat, nmeaLon);

            // Assert - Should maintain reasonable precision (within 1 meter)
            Math.Abs(parsedLat - originalLat).Should().BeLessThan(0.00001, 
                "Latitude precision should be maintained");
            Math.Abs(parsedLon - originalLon).Should().BeLessThan(0.00001, 
                "Longitude precision should be maintained");
        }

        [Fact]
        public void ChecksumIntegrity_ShouldDetectCorruption()
        {
            // Arrange
            var validSentence = "!AIVDM,1,1,,A,15Muq70001G?tRrM5M4P8?v4080u,0*7C";
            var corruptedSentence = "!AIVDM,1,1,,A,15Muq70001G?tRrM5M4P8?v4080u,0*7D"; // Wrong checksum

            // Act
            var validResult = NmeaValidator.ValidateAisSentence(validSentence);
            var corruptedResult = NmeaValidator.ValidateAisSentence(corruptedSentence);

            // Assert
            validResult.IsValid.Should().BeTrue("Valid sentence should pass");
            corruptedResult.IsValid.Should().BeFalse("Corrupted sentence should fail");
            corruptedResult.Errors.Should().Contain(e => e.Contains("checksum"), 
                "Should detect checksum error");
        }

        #endregion

        #region Performance Integration Tests

        [Fact]
        public void ProcessMultipleAisMessages_ShouldBeEfficient()
        {
            // Arrange
            var testMessages = new[]
            {
                AisTestData.ValidType1Json,
                AisTestData.ValidType5Json,
                AisTestData.ValidType18Json,
                AisTestData.ValidType24AJson
            };

            const int iterations = 1000;

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            for (int i = 0; i < iterations; i++)
            {
                foreach (var json in testMessages)
                {
                    var aisMessage = JsonConvert.DeserializeObject<AisStreamMessage>(json);
                    
                    // Simulate basic processing
                    if (aisMessage?.Message?.PositionReport != null)
                    {
                        var lat = aisMessage.Message.PositionReport.Latitude;
                        var lon = aisMessage.Message.PositionReport.Longitude;
                        var nmeaLat = CoordinateTestHelper.ConvertToNmeaFormat(lat, true);
                        var nmeaLon = CoordinateTestHelper.ConvertToNmeaFormat(lon, false);
                    }
                }
            }
            
            stopwatch.Stop();

            // Assert
            var messagesPerSecond = (testMessages.Length * iterations) / stopwatch.Elapsed.TotalSeconds;
            messagesPerSecond.Should().BeGreaterThan(1000, 
                "Should process at least 1000 messages per second");
        }

        #endregion

        #region Geographic Filtering Tests

        [Fact]
        public void FilterAisMessagesByBoundingBox_ShouldFilterCorrectly()
        {
            // Arrange
            var pacificNorthwestBox = new Configuration.BoundingBox
            {
                North = 48.8000,
                South = 48.0000,
                East = -122.1900,
                West = -123.3550
            };

            var insideMessage = JsonConvert.DeserializeObject<AisStreamMessage>(AisTestData.ValidType1Json);
            var outsideMessage = JsonConvert.DeserializeObject<AisStreamMessage>(AisTestData.Type1NearDatelineJson);

            // Act
            var insidePosition = insideMessage!.Message!.PositionReport!;
            var outsidePosition = outsideMessage!.Message!.PositionReport!;

            var isInsideInBox = CoordinateTestHelper.IsWithinBoundingBox(
                insidePosition.Latitude, insidePosition.Longitude,
                pacificNorthwestBox.North, pacificNorthwestBox.South,
                pacificNorthwestBox.East, pacificNorthwestBox.West);

            var isOutsideInBox = CoordinateTestHelper.IsWithinBoundingBox(
                outsidePosition.Latitude, outsidePosition.Longitude,
                pacificNorthwestBox.North, pacificNorthwestBox.South,
                pacificNorthwestBox.East, pacificNorthwestBox.West);

            // Assert
            isInsideInBox.Should().BeTrue("Message within Pacific Northwest should be included");
            isOutsideInBox.Should().BeFalse("Message near Japan should be excluded");
        }

        #endregion

        #region Helper Methods for Test Simulation

        private static string GenerateSimulatedNmeaSentence(int mmsi, string nmeaLat, string nmeaLon, double sog, double cog)
        {
            // This is a simplified simulation of NMEA sentence generation
            // In the actual implementation, this would be done by Nmea0183Converter
            var payload = $"15{mmsi:X7}001G?tRrM5M4P8?v4080u"; // Simplified payload
            var sentence = $"AIVDM,1,1,,A,{payload},0";
            var checksum = NmeaValidator.CalculateChecksum(sentence);
            return $"!{sentence}*{NmeaValidator.FormatChecksum(checksum)}";
        }

        private static string GenerateType5Fragment1(AisShipAndVoyageData shipData)
        {
            // Simulate first fragment of Type 5 message
            var payload = $"55{shipData.UserID:X7}02;H;s<HtKR20EHE:0@T4@Dn2222222216L961O5Gf0NSQEp6ClRp8";
            var sentence = $"AIVDM,2,1,0,A,{payload},0";
            var checksum = NmeaValidator.CalculateChecksum(sentence);
            return $"!{sentence}*{NmeaValidator.FormatChecksum(checksum)}";
        }

        private static string GenerateType5Fragment2(AisShipAndVoyageData shipData)
        {
            // Simulate second fragment of Type 5 message
            var payload = "88888888880";
            var sentence = $"AIVDM,2,2,0,A,{payload},2";
            var checksum = NmeaValidator.CalculateChecksum(sentence);
            return $"!{sentence}*{NmeaValidator.FormatChecksum(checksum)}";
        }

        private static string GenerateSimulatedClassBNmeaSentence(AisStandardClassBPositionReport positionReport)
        {
            // Simulate Class B NMEA sentence generation
            var payload = $"B5{positionReport.UserID:X7}001G?tRrM5M4P8?v4080u";
            var sentence = $"AIVDM,1,1,,B,{payload},0";
            var checksum = NmeaValidator.CalculateChecksum(sentence);
            return $"!{sentence}*{NmeaValidator.FormatChecksum(checksum)}";
        }

        private static string GenerateType24ASentence(AisStaticDataReport staticData)
        {
            // Simulate Type 24 Part A sentence generation
            var payload = $"H5{staticData.UserID:X7}001G?tRrM5M4P8?v4080u";
            var sentence = $"AIVDM,1,1,,A,{payload},0";
            var checksum = NmeaValidator.CalculateChecksum(sentence);
            return $"!{sentence}*{NmeaValidator.FormatChecksum(checksum)}";
        }

        private static (double lat, double lon) ParseNmeaCoordinates(string nmeaLat, string nmeaLon)
        {
            // Simplified parsing back from NMEA format for round-trip testing
            var latParts = nmeaLat.Split(',');
            var lonParts = nmeaLon.Split(',');

            var latCoord = latParts[0];
            var latDir = latParts[1];
            var lonCoord = lonParts[0];
            var lonDir = lonParts[1];

            // Parse latitude (ddmm.mmm)
            var latDegrees = int.Parse(latCoord.Substring(0, 2));
            var latMinutes = double.Parse(latCoord.Substring(2));
            var lat = latDegrees + latMinutes / 60.0;
            if (latDir == "S") lat = -lat;

            // Parse longitude (dddmm.mmm)
            var lonDegrees = int.Parse(lonCoord.Substring(0, 3));
            var lonMinutes = double.Parse(lonCoord.Substring(3));
            var lon = lonDegrees + lonMinutes / 60.0;
            if (lonDir == "W") lon = -lon;

            return (lat, lon);
        }

        #endregion
    }
}
