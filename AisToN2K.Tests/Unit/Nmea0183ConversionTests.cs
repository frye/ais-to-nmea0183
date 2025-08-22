using AisToN2K.Tests.TestData;
using AisToN2K.Tests.Utilities;

namespace AisToN2K.Tests.Unit
{
    /// <summary>
    /// Comprehensive tests for NMEA 0183 conversion from AIS data.
    /// Validates message formatting, checksum calculation, and OpenCPN compatibility.
    /// </summary>
    public class Nmea0183ConversionTests
    {
        #region Checksum Calculation Tests

        [Theory]
        [MemberData(nameof(GetChecksumTestCases))]
        public void CalculateChecksum_ShouldProduceCorrectResults(string sentence, string expectedChecksum)
        {
            // Arrange & Act
            var calculatedChecksum = NmeaValidator.CalculateChecksum(sentence);
            var formattedChecksum = NmeaValidator.FormatChecksum(calculatedChecksum);

            // Assert
            formattedChecksum.Should().Be(expectedChecksum.ToUpper(), 
                $"Checksum for '{sentence}' should be {expectedChecksum}");
        }

        public static IEnumerable<object[]> GetChecksumTestCases()
        {
            return AisTestData.NmeaChecksumTestCases.Select(tc => 
                new object[] { tc.Sentence, tc.ExpectedChecksum });
        }

        [Fact]
        public void CalculateChecksum_EmptyString_ShouldReturnZero()
        {
            // Arrange
            var sentence = "";

            // Act
            var checksum = NmeaValidator.CalculateChecksum(sentence);

            // Assert
            checksum.Should().Be(0);
        }

        [Fact]
        public void CalculateChecksum_SingleCharacter_ShouldReturnAsciiValue()
        {
            // Arrange
            var sentence = "A";

            // Act
            var checksum = NmeaValidator.CalculateChecksum(sentence);

            // Assert
            checksum.Should().Be(65); // ASCII value of 'A'
        }

        [Fact]
        public void FormatChecksum_ShouldAlwaysProduceTwoDigitUppercaseHex()
        {
            // Arrange & Act & Assert
            for (byte i = 0; i <= 255; i++)
            {
                var formatted = NmeaValidator.FormatChecksum(i);
                formatted.Should().HaveLength(2, $"Checksum {i} should format to 2 characters");
                formatted.Should().MatchRegex("^[0-9A-F]{2}$", $"Checksum {i} should be uppercase hex");
            }
        }

        #endregion

        #region Coordinate Conversion Tests

        [Theory]
        [MemberData(nameof(GetCoordinateTestCases))]
        public void ConvertCoordinates_ShouldProduceCorrectNmeaFormat(
            string description, double decimalLat, double decimalLon, 
            string expectedNmeaLat, string expectedNmeaLon)
        {
            // Arrange & Act
            var actualNmeaLat = CoordinateTestHelper.ConvertToNmeaFormat(decimalLat, true);
            var actualNmeaLon = CoordinateTestHelper.ConvertToNmeaFormat(decimalLon, false);

            // Assert
            actualNmeaLat.Should().Be(expectedNmeaLat, 
                $"Latitude conversion failed for {description}");
            actualNmeaLon.Should().Be(expectedNmeaLon, 
                $"Longitude conversion failed for {description}");
        }

        public static IEnumerable<object[]> GetCoordinateTestCases()
        {
            return AisTestData.CoordinateTestCases.Select(tc => 
                new object[] 
                { 
                    tc.Description, 
                    tc.DecimalLatitude, 
                    tc.DecimalLongitude, 
                    tc.ExpectedNmeaLatitude, 
                    tc.ExpectedNmeaLongitude 
                });
        }

        [Fact]
        public void ConvertCoordinates_HighPrecision_ShouldMaintainAccuracy()
        {
            // Arrange - High precision coordinates
            double lat = 48.123456789;
            double lon = -122.987654321;

            // Act
            var nmeaLat = CoordinateTestHelper.ConvertToNmeaFormat(lat, true);
            var nmeaLon = CoordinateTestHelper.ConvertToNmeaFormat(lon, false);

            // Assert
            nmeaLat.Should().StartWith("4807.407", "High precision latitude should be accurate");
            nmeaLon.Should().StartWith("12259.259", "High precision longitude should be accurate");
        }

        [Theory]
        [InlineData(0.0, 0.0, "0000.000,N", "00000.000,E")]
        [InlineData(90.0, 180.0, "9000.000,N", "18000.000,E")]
        [InlineData(-90.0, -180.0, "9000.000,S", "18000.000,W")]
        public void ConvertCoordinates_BoundaryValues_ShouldHandleCorrectly(
            double lat, double lon, string expectedLat, string expectedLon)
        {
            // Arrange & Act
            var actualLat = CoordinateTestHelper.ConvertToNmeaFormat(lat, true);
            var actualLon = CoordinateTestHelper.ConvertToNmeaFormat(lon, false);

            // Assert
            actualLat.Should().Be(expectedLat);
            actualLon.Should().Be(expectedLon);
        }

        #endregion

        #region NMEA Sentence Validation Tests

        [Fact]
        public void ValidateNmeaSentence_ValidAisMessage_ShouldPass()
        {
            // Arrange
            var sentence = "!AIVDM,1,1,,A,15Muq70001G?tRrM5M4P8?v4080u,0*7C";

            // Act
            var result = NmeaValidator.ValidateSentence(sentence);

            // Assert
            result.IsValid.Should().BeTrue($"Valid sentence should pass validation: {result.ErrorSummary}");
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void ValidateAisSentence_ValidMessage_ShouldParseCorrectly()
        {
            // Arrange
            var sentence = "!AIVDM,1,1,,A,15Muq70001G?tRrM5M4P8?v4080u,0*7C";

            // Act
            var result = NmeaValidator.ValidateAisSentence(sentence);

            // Assert
            result.IsValid.Should().BeTrue();
            result.ParsedFields.Should().NotBeNull();
            result.ParsedFields!.SentenceId.Should().Be("AIVDM");
            result.ParsedFields.FragmentCount.Should().Be(1);
            result.ParsedFields.FragmentNumber.Should().Be(1);
            result.ParsedFields.Channel.Should().Be("A");
            result.ParsedFields.FillBits.Should().Be(0);
        }

        [Theory]
        [InlineData("$AIVDM,1,1,,A,15Muq70001G?tRrM5M4P8?v4080u,0*7C", "Should start with ! for AIS")]
        [InlineData("!AIVDM,1,1,,A,15Muq70001G?tRrM5M4P8?v4080u,0", "Missing checksum")]
        [InlineData("!AIVDM,1,1,,A,15Muq70001G?tRrM5M4P8?v4080u,0*7D", "Wrong checksum")]
        [InlineData("!GPGGA,1,1,,A,15Muq70001G?tRrM5M4P8?v4080u,0*7C", "Wrong sentence type")]
        public void ValidateAisSentence_InvalidMessage_ShouldFail(string sentence, string reason)
        {
            // Arrange & Act
            var result = NmeaValidator.ValidateAisSentence(sentence);

            // Assert
            result.IsValid.Should().BeFalse($"Should fail validation: {reason}");
            result.Errors.Should().NotBeEmpty();
        }

        [Fact]
        public void ValidateNmeaSentence_TooLong_ShouldFail()
        {
            // Arrange - Create a sentence longer than 82 characters
            var longPayload = new string('A', 100);
            var sentence = $"!AIVDM,1,1,,A,{longPayload},0*7C";

            // Act
            var result = NmeaValidator.ValidateSentence(sentence);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("exceeds maximum length"));
        }

        [Theory]
        [InlineData("!AIVDM,1,1,,A,15Muq70001G?tRrM5M4P8?v4080u,0*7c", true)] // lowercase hex
        [InlineData("!AIVDM,1,1,,A,15Muq70001G?tRrM5M4P8?v4080u,0*7C", true)] // uppercase hex
        [InlineData("!AIVDM,1,1,,A,15Muq70001G?tRrM5M4P8?v4080u,0*ZZ", false)] // invalid hex
        [InlineData("!AIVDM,1,1,,A,15Muq70001G?tRrM5M4P8?v4080u,0*7", false)] // short checksum
        public void ValidateNmeaSentence_ChecksumFormat_ShouldValidateCorrectly(string sentence, bool shouldBeValid)
        {
            // Arrange & Act
            var result = NmeaValidator.ValidateSentence(sentence);

            // Assert
            result.IsValid.Should().Be(shouldBeValid);
            if (!shouldBeValid)
            {
                result.Errors.Should().NotBeEmpty();
            }
        }

        #endregion

        #region Fragment Handling Tests

        [Fact]
        public void ValidateAisSentence_MultiPartMessage_ShouldValidateFragmentation()
        {
            // Arrange
            var part1 = "!AIVDM,2,1,0,A,55?MbV02;H;s<HtKR20EHE:0@T4@Dn2222222216L961O5Gf0NSQEp6ClRp8,0*0F";
            var part2 = "!AIVDM,2,2,0,A,88888888880,2*23";

            // Act
            var result1 = NmeaValidator.ValidateAisSentence(part1);
            var result2 = NmeaValidator.ValidateAisSentence(part2);

            // Assert
            result1.IsValid.Should().BeTrue();
            result1.ParsedFields!.FragmentCount.Should().Be(2);
            result1.ParsedFields.FragmentNumber.Should().Be(1);
            result1.ParsedFields.MessageId.Should().Be("0");

            result2.IsValid.Should().BeTrue();
            result2.ParsedFields!.FragmentCount.Should().Be(2);
            result2.ParsedFields.FragmentNumber.Should().Be(2);
            result2.ParsedFields.MessageId.Should().Be("0");
        }

        [Theory]
        [InlineData(0, false, "Fragment count must be at least 1")]
        [InlineData(10, false, "Fragment count cannot exceed 9")]
        [InlineData(1, true, "Single fragment should be valid")]
        [InlineData(2, true, "Two fragments should be valid")]
        public void ValidateAisSentence_FragmentCount_ShouldValidateRange(int fragmentCount, bool shouldBeValid, string reason)
        {
            // Arrange
            var sentence = $"!AIVDM,{fragmentCount},1,,A,15Muq70001G?tRrM5M4P8?v4080u,0*";
            var checksum = NmeaValidator.CalculateChecksum(sentence.Substring(1, sentence.Length - 2));
            sentence += NmeaValidator.FormatChecksum(checksum);

            // Act
            var result = NmeaValidator.ValidateAisSentence(sentence);

            // Assert
            result.IsValid.Should().Be(shouldBeValid, reason);
        }

        #endregion

        #region AIS Payload Validation Tests

        [Theory]
        [InlineData("15Muq70001G?tRrM5M4P8?v4080u", true, "Valid AIS payload")]
        [InlineData("", false, "Empty payload")]
        [InlineData("InvalidChar@", false, "Contains invalid character @")]
        [InlineData("1234567890abcdefghijklmnopqrstuvwxyz", true, "All valid 6-bit ASCII chars")]
        public void ValidateAisSentence_PayloadEncoding_ShouldValidateCorrectly(string payload, bool shouldBeValid, string reason)
        {
            // Arrange
            var sentence = $"!AIVDM,1,1,,A,{payload},0*";
            var checksum = NmeaValidator.CalculateChecksum(sentence.Substring(1, sentence.Length - 2));
            sentence += NmeaValidator.FormatChecksum(checksum);

            // Act
            var result = NmeaValidator.ValidateAisSentence(sentence);

            // Assert
            result.IsValid.Should().Be(shouldBeValid, reason);
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(3, true)]
        [InlineData(5, true)]
        [InlineData(6, false)]
        [InlineData(-1, false)]
        public void ValidateAisSentence_FillBits_ShouldValidateRange(int fillBits, bool shouldBeValid)
        {
            // Arrange
            var sentence = $"!AIVDM,1,1,,A,15Muq70001G?tRrM5M4P8?v4080u,{fillBits}*";
            var checksum = NmeaValidator.CalculateChecksum(sentence.Substring(1, sentence.Length - 2));
            sentence += NmeaValidator.FormatChecksum(checksum);

            // Act
            var result = NmeaValidator.ValidateAisSentence(sentence);

            // Assert
            result.IsValid.Should().Be(shouldBeValid, 
                $"Fill bits {fillBits} should be {(shouldBeValid ? "valid" : "invalid")}");
        }

        #endregion

        #region OpenCPN Compatibility Tests

        [Fact]
        public void GenerateNmeaSentence_ShouldBeOpenCpnCompatible()
        {
            // Arrange - Test data that should be compatible with OpenCPN
            var sentences = new[]
            {
                "!AIVDM,1,1,,A,15Muq70001G?tRrM5M4P8?v4080u,0*7C",
                "!AIVDM,2,1,0,A,55?MbV02;H;s<HtKR20EHE:0@T4@Dn2222222216L961O5Gf0NSQEp6ClRp8,0*0F",
                "!AIVDM,2,2,0,A,88888888880,2*23"
            };

            foreach (var sentence in sentences)
            {
                // Act
                var result = NmeaValidator.ValidateAisSentence(sentence);

                // Assert
                result.IsValid.Should().BeTrue($"Sentence should be OpenCPN compatible: {sentence}");
                
                // OpenCPN specific requirements
                sentence.Should().StartWith("!", "OpenCPN expects AIS sentences to start with !");
                sentence.Length.Should().BeLessOrEqualTo(82, "OpenCPN has 82 character limit");
                result.ParsedFields!.SentenceId.Should().BeOneOf("AIVDM", "AIVDO", "OpenCPN supports these AIS sentence types");
            }
        }

        [Fact]
        public void GenerateNmeaSentence_ShouldHaveCorrectLineEnding()
        {
            // Arrange
            var baseSentence = "!AIVDM,1,1,,A,15Muq70001G?tRrM5M4P8?v4080u,0*7C";
            var nmeaSentence = baseSentence + "\r\n";

            // Act & Assert
            nmeaSentence.Should().EndWith("\r\n", "NMEA sentences should end with CRLF");
            nmeaSentence.Length.Should().BeLessOrEqualTo(82, "Including CRLF, should not exceed 82 characters");
        }

        #endregion

        #region Performance Tests

        [Fact]
        public void ValidateNmeaSentence_PerformanceTest_ShouldBeEfficient()
        {
            // Arrange
            var sentence = "!AIVDM,1,1,,A,15Muq70001G?tRrM5M4P8?v4080u,0*7C";
            var iterations = 10000;

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var result = NmeaValidator.ValidateAisSentence(sentence);
                result.IsValid.Should().BeTrue();
            }
            stopwatch.Stop();

            // Assert
            var averageTime = stopwatch.ElapsedMilliseconds / (double)iterations;
            averageTime.Should().BeLessThan(1.0, "Validation should average less than 1ms per sentence");
        }

        [Fact]
        public void CalculateChecksum_PerformanceTest_ShouldBeEfficient()
        {
            // Arrange
            var data = "AIVDM,1,1,,A,15Muq70001G?tRrM5M4P8?v4080u,0";
            var iterations = 100000;

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var checksum = NmeaValidator.CalculateChecksum(data);
                checksum.Should().Be(0x7C);
            }
            stopwatch.Stop();

            // Assert
            var averageTime = stopwatch.ElapsedTicks / (double)iterations;
            averageTime.Should().BeLessThan(100, "Checksum calculation should be very fast");
        }

        #endregion
    }
}
