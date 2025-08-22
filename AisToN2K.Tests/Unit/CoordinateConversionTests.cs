using AisToN2K.Tests.TestData;
using AisToN2K.Tests.Utilities;

namespace AisToN2K.Tests.Unit
{
    /// <summary>
    /// Comprehensive tests for coordinate conversion between decimal degrees and NMEA format.
    /// Validates precision, edge cases, and maritime navigation requirements.
    /// </summary>
    public class CoordinateConversionTests
    {
        #region Basic Conversion Tests

        [Theory]
        [InlineData(48.5000, true, "4830.000,N")]
        [InlineData(-48.5000, true, "4830.000,S")]
        [InlineData(122.8000, false, "12248.000,E")]
        [InlineData(-122.8000, false, "12248.000,W")]
        public void ConvertToNmeaFormat_BasicCoordinates_ShouldConvertCorrectly(
            double decimalDegrees, bool isLatitude, string expected)
        {
            // Arrange & Act
            var result = CoordinateTestHelper.ConvertToNmeaFormat(decimalDegrees, isLatitude);

            // Assert
            result.Should().Be(expected, 
                $"Conversion of {decimalDegrees} ({(isLatitude ? "lat" : "lon")}) should produce {expected}");
        }

        [Fact]
        public void ConvertToNmeaFormat_ZeroCoordinates_ShouldProduceZeroWithCorrectFormat()
        {
            // Arrange
            double latitude = 0.0;
            double longitude = 0.0;

            // Act
            var latResult = CoordinateTestHelper.ConvertToNmeaFormat(latitude, true);
            var lonResult = CoordinateTestHelper.ConvertToNmeaFormat(longitude, false);

            // Assert
            latResult.Should().Be("0000.000,N", "Zero latitude should default to North");
            lonResult.Should().Be("00000.000,E", "Zero longitude should default to East");
        }

        [Fact]
        public void ConvertToNmeaFormat_EquatorAndPrimeMeridian_ShouldHandleCorrectly()
        {
            // Arrange - Intersection of Equator and Prime Meridian
            double latitude = 0.0;
            double longitude = 0.0;

            // Act
            var latResult = CoordinateTestHelper.ConvertToNmeaFormat(latitude, true);
            var lonResult = CoordinateTestHelper.ConvertToNmeaFormat(longitude, false);

            // Assert
            latResult.Should().Be("0000.000,N");
            lonResult.Should().Be("00000.000,E");
        }

        #endregion

        #region High Precision Tests

        [Theory]
        [InlineData(48.123456, "4807.407,N")]
        [InlineData(48.987654, "4859.259,N")]
        [InlineData(122.123456, "12207.407,E")]
        [InlineData(122.987654, "12259.259,E")]
        public void ConvertToNmeaFormat_HighPrecision_ShouldMaintainAccuracy(double decimalDegrees, string expected)
        {
            // Arrange & Act
            var result = CoordinateTestHelper.ConvertToNmeaFormat(decimalDegrees, decimalDegrees < 90);

            // Assert
            // Allow small tolerance for floating point precision
            var isValid = CoordinateTestHelper.ValidateCoordinateConversion(expected, result, 0.001);
            isValid.Should().BeTrue($"High precision conversion of {decimalDegrees} should be accurate. Expected: {expected}, Actual: {result}");
        }

        [Fact]
        public void ConvertToNmeaFormat_MaximumPrecision_ShouldHandleCorrectly()
        {
            // Arrange - Test with maximum decimal precision
            double lat = 59.123456789123456;
            double lon = 10.987654321987654;

            // Act
            var latResult = CoordinateTestHelper.ConvertToNmeaFormat(lat, true);
            var lonResult = CoordinateTestHelper.ConvertToNmeaFormat(lon, false);

            // Assert
            latResult.Should().StartWith("5907.407", "Maximum precision latitude should be accurate to 3 decimal places");
            lonResult.Should().StartWith("1059.259", "Maximum precision longitude should be accurate to 3 decimal places");
        }

        [Fact]
        public void ConvertToNmeaFormat_SubMeterAccuracy_ShouldMaintainPrecision()
        {
            // Arrange - Test coordinates with sub-meter accuracy requirements
            double lat1 = 48.500000; // Exact half degree
            double lat2 = 48.500001; // 1 meter difference approximately
            
            // Act
            var result1 = CoordinateTestHelper.ConvertToNmeaFormat(lat1, true);
            var result2 = CoordinateTestHelper.ConvertToNmeaFormat(lat2, true);

            // Assert
            result1.Should().NotBe(result2, "Sub-meter differences should be detectable in NMEA format");
        }

        #endregion

        #region Edge Case Tests

        [Theory]
        [InlineData(90.0, true, "9000.000,N")]    // North Pole
        [InlineData(-90.0, true, "9000.000,S")]   // South Pole
        [InlineData(180.0, false, "18000.000,E")] // International Date Line East
        [InlineData(-180.0, false, "18000.000,W")] // International Date Line West
        public void ConvertToNmeaFormat_BoundaryValues_ShouldHandleCorrectly(
            double decimalDegrees, bool isLatitude, string expected)
        {
            // Arrange & Act
            var result = CoordinateTestHelper.ConvertToNmeaFormat(decimalDegrees, isLatitude);

            // Assert
            result.Should().Be(expected, $"Boundary value {decimalDegrees} should convert correctly");
        }

        [Theory]
        [MemberData(nameof(GetEdgeCaseTestData))]
        public void ValidateCoordinateRanges_EdgeCases_ShouldIdentifyValidAndInvalid(
            string name, double lat, double lon, bool expectedValid)
        {
            // Arrange & Act
            var result = CoordinateTestHelper.ValidateCoordinateRanges(lat, lon);

            // Assert
            result.IsValid.Should().Be(expectedValid, 
                $"Edge case '{name}' (lat: {lat}, lon: {lon}) should be {(expectedValid ? "valid" : "invalid")}");
            
            if (!expectedValid)
            {
                result.Errors.Should().NotBeEmpty($"Invalid case '{name}' should have error messages");
            }
        }

        public static IEnumerable<object[]> GetEdgeCaseTestData()
        {
            return CoordinateTestHelper.GetCoordinateEdgeCases()
                .Select(tc => new object[] { tc.Name, tc.Latitude, tc.Longitude, tc.ExpectedValid });
        }

        #endregion

        #region Directional Indicator Tests

        [Theory]
        [InlineData(1.0, "N")]
        [InlineData(0.0, "N")]
        [InlineData(-1.0, "S")]
        [InlineData(90.0, "N")]
        [InlineData(-90.0, "S")]
        public void ConvertToNmeaFormat_LatitudeDirection_ShouldUseCorrectIndicator(double lat, string expectedDirection)
        {
            // Arrange & Act
            var result = CoordinateTestHelper.ConvertToNmeaFormat(lat, true);

            // Assert
            result.Should().EndWith($",{expectedDirection}", 
                $"Latitude {lat} should use direction indicator {expectedDirection}");
        }

        [Theory]
        [InlineData(1.0, "E")]
        [InlineData(0.0, "E")]
        [InlineData(-1.0, "W")]
        [InlineData(180.0, "E")]
        [InlineData(-180.0, "W")]
        public void ConvertToNmeaFormat_LongitudeDirection_ShouldUseCorrectIndicator(double lon, string expectedDirection)
        {
            // Arrange & Act
            var result = CoordinateTestHelper.ConvertToNmeaFormat(lon, false);

            // Assert
            result.Should().EndWith($",{expectedDirection}", 
                $"Longitude {lon} should use direction indicator {expectedDirection}");
        }

        #endregion

        #region Format Validation Tests

        [Fact]
        public void ConvertToNmeaFormat_LatitudeFormat_ShouldUseTwoDigitDegrees()
        {
            // Arrange & Act
            var result = CoordinateTestHelper.ConvertToNmeaFormat(5.5, true);

            // Assert
            result.Should().StartWith("05", "Latitude should use 2-digit degree format");
            result.Should().MatchRegex(@"^\d{2}\d{2}\.\d{3},[NS]$", "Should match NMEA latitude format");
        }

        [Fact]
        public void ConvertToNmeaFormat_LongitudeFormat_ShouldUseThreeDigitDegrees()
        {
            // Arrange & Act
            var result = CoordinateTestHelper.ConvertToNmeaFormat(5.5, false);

            // Assert
            result.Should().StartWith("005", "Longitude should use 3-digit degree format");
            result.Should().MatchRegex(@"^\d{3}\d{2}\.\d{3},[EW]$", "Should match NMEA longitude format");
        }

        [Theory]
        [InlineData(48.123456)]
        [InlineData(0.999999)]
        [InlineData(89.999999)]
        public void ConvertToNmeaFormat_MinutesFormat_ShouldUseCorrectPrecision(double decimalDegrees)
        {
            // Arrange & Act
            var result = CoordinateTestHelper.ConvertToNmeaFormat(decimalDegrees, true);

            // Assert
            var parts = result.Split(',');
            var coordinate = parts[0];
            var minutesPart = coordinate.Substring(2); // Skip degree digits
            
            minutesPart.Should().MatchRegex(@"^\d{2}\.\d{3}$", "Minutes should have format MM.mmm");
        }

        #endregion

        #region Bounding Box Tests

        [Theory]
        [MemberData(nameof(GetBoundingBoxTestCases))]
        public void IsWithinBoundingBox_ShouldValidateCorrectly(
            string description, double lat, double lon, 
            double north, double south, double east, double west, bool expectedInside)
        {
            // Arrange & Act
            var result = CoordinateTestHelper.IsWithinBoundingBox(lat, lon, north, south, east, west);

            // Assert
            result.Should().Be(expectedInside, 
                $"Bounding box test '{description}' should be {(expectedInside ? "inside" : "outside")}");
        }

        public static IEnumerable<object[]> GetBoundingBoxTestCases()
        {
            return AisTestData.BoundingBoxTestCases.Select(tc => 
                new object[] 
                { 
                    tc.Description, 
                    tc.Latitude, 
                    tc.Longitude, 
                    tc.BoundingBox.North, 
                    tc.BoundingBox.South, 
                    tc.BoundingBox.East, 
                    tc.BoundingBox.West, 
                    tc.ExpectedInside 
                });
        }

        [Fact]
        public void IsWithinBoundingBox_DatelineCrossing_ShouldHandleCorrectly()
        {
            // Arrange - Bounding box that crosses the International Date Line
            double lat = 35.0;
            double lon1 = 179.5; // East of date line
            double lon2 = -179.5; // West of date line
            double north = 40.0, south = 30.0;
            double east = -170.0, west = 170.0; // Crosses dateline

            // Act
            var result1 = CoordinateTestHelper.IsWithinBoundingBox(lat, lon1, north, south, east, west);
            var result2 = CoordinateTestHelper.IsWithinBoundingBox(lat, lon2, north, south, east, west);

            // Assert
            result1.Should().BeTrue("Point east of dateline should be in crossing bounding box");
            result2.Should().BeTrue("Point west of dateline should be in crossing bounding box");
        }

        [Fact]
        public void IsWithinBoundingBox_ExactBoundaries_ShouldBeInclusive()
        {
            // Arrange
            double north = 50.0, south = 40.0, east = -120.0, west = -130.0;

            // Act & Assert - Test all four boundaries
            CoordinateTestHelper.IsWithinBoundingBox(north, -125.0, north, south, east, west)
                .Should().BeTrue("Point on north boundary should be included");
            
            CoordinateTestHelper.IsWithinBoundingBox(south, -125.0, north, south, east, west)
                .Should().BeTrue("Point on south boundary should be included");
            
            CoordinateTestHelper.IsWithinBoundingBox(45.0, east, north, south, east, west)
                .Should().BeTrue("Point on east boundary should be included");
            
            CoordinateTestHelper.IsWithinBoundingBox(45.0, west, north, south, east, west)
                .Should().BeTrue("Point on west boundary should be included");
        }

        #endregion

        #region Validation Tolerance Tests

        [Theory]
        [InlineData("4830.000,N", "4830.001,N", 0.002, true)]
        [InlineData("4830.000,N", "4830.001,N", 0.0005, false)]
        [InlineData("4830.000,N", "4830.000,S", 0.1, false)] // Different direction
        public void ValidateCoordinateConversion_WithTolerance_ShouldRespectTolerance(
            string expected, string actual, double toleranceMinutes, bool shouldMatch)
        {
            // Arrange & Act
            var result = CoordinateTestHelper.ValidateCoordinateConversion(expected, actual, toleranceMinutes);

            // Assert
            result.Should().Be(shouldMatch, 
                $"Coordinates should {(shouldMatch ? "match" : "not match")} within tolerance {toleranceMinutes}");
        }

        [Fact]
        public void ValidateCoordinateConversion_InvalidFormat_ShouldReturnFalse()
        {
            // Arrange
            var validCoord = "4830.000,N";
            var invalidFormats = new[]
            {
                "",
                "4830.000",      // Missing direction
                "4830.000,X",   // Invalid direction
                "ABC30.000,N",  // Invalid numeric format
                "4830.000,N,Extra" // Too many parts
            };

            foreach (var invalidCoord in invalidFormats)
            {
                // Act
                var result = CoordinateTestHelper.ValidateCoordinateConversion(validCoord, invalidCoord);

                // Assert
                result.Should().BeFalse($"Invalid coordinate format '{invalidCoord}' should not validate");
            }
        }

        #endregion

        #region AIS Special Values Tests

        [Theory]
        [InlineData(91.0, "AIS latitude not available")]
        [InlineData(181.0, "AIS longitude not available")]
        [InlineData(-91.0, "Invalid latitude below range")]
        [InlineData(-181.0, "Invalid longitude below range")]
        [InlineData(92.0, "Invalid latitude above range")]
        [InlineData(182.0, "Invalid longitude above range")]
        public void ValidateCoordinateRanges_InvalidValues_ShouldDetectCorrectly(double value, string description)
        {
            // Arrange
            double lat = value <= 90 ? value : 0; // Use value for lat if in lat range
            double lon = value > 90 ? value : 0;  // Use value for lon if in lon range

            // Act
            var result = CoordinateTestHelper.ValidateCoordinateRanges(lat, lon);

            // Assert
            result.IsValid.Should().BeFalse($"Should detect invalid value: {description}");
            result.Errors.Should().NotBeEmpty($"Should provide error message for: {description}");
        }

        [Fact]
        public void ValidateCoordinateRanges_AisNotAvailableValues_ShouldHaveSpecificMessages()
        {
            // Arrange & Act
            var latResult = CoordinateTestHelper.ValidateCoordinateRanges(91.0, 0);
            var lonResult = CoordinateTestHelper.ValidateCoordinateRanges(0, 181.0);

            // Assert
            latResult.Errors.Should().Contain(e => e.Contains("not available"));
            lonResult.Errors.Should().Contain(e => e.Contains("not available"));
        }

        #endregion

        #region Performance Tests

        [Fact]
        public void ConvertToNmeaFormat_PerformanceTest_ShouldBeEfficient()
        {
            // Arrange
            const int iterations = 100000;
            double lat = 48.123456;
            double lon = -122.987654;

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var latResult = CoordinateTestHelper.ConvertToNmeaFormat(lat, true);
                var lonResult = CoordinateTestHelper.ConvertToNmeaFormat(lon, false);
            }
            stopwatch.Stop();

            // Assert
            var averageTime = stopwatch.ElapsedMilliseconds / (double)iterations;
            averageTime.Should().BeLessThan(0.01, "Coordinate conversion should be very fast");
        }

        [Fact]
        public void IsWithinBoundingBox_PerformanceTest_ShouldBeEfficient()
        {
            // Arrange
            const int iterations = 1000000;
            double lat = 48.5, lon = -122.8;
            double north = 49.0, south = 48.0, east = -122.0, west = -123.0;

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var result = CoordinateTestHelper.IsWithinBoundingBox(lat, lon, north, south, east, west);
            }
            stopwatch.Stop();

            // Assert
            var averageTime = stopwatch.ElapsedTicks / (double)iterations;
            averageTime.Should().BeLessThan(10, "Bounding box check should be extremely fast");
        }

        #endregion
    }
}
