namespace AisToN2K.Tests.Utilities
{
    /// <summary>
    /// Utilities for testing coordinate conversions between decimal degrees and NMEA format.
    /// Provides high-precision validation for AIS-to-NMEA coordinate transformations.
    /// </summary>
    public static class CoordinateTestHelper
    {
        /// <summary>
        /// Converts decimal degrees to NMEA format with proper precision and directional indicators.
        /// </summary>
        /// <param name="decimalDegrees">Coordinate in decimal degrees</param>
        /// <param name="isLatitude">True for latitude (N/S), false for longitude (E/W)</param>
        /// <returns>NMEA format coordinate string (e.g., "4830.000,N")</returns>
        public static string ConvertToNmeaFormat(double decimalDegrees, bool isLatitude)
        {
            bool isNegative = decimalDegrees < 0;
            double absoluteValue = Math.Abs(decimalDegrees);
            
            int degrees = (int)absoluteValue;
            double minutes = (absoluteValue - degrees) * 60.0;
            
            string direction;
            if (isLatitude)
            {
                direction = isNegative ? "S" : "N";
            }
            else
            {
                direction = isNegative ? "W" : "E";
            }
            
            // Format according to NMEA standard
            if (isLatitude)
            {
                // Latitude: ddmm.mmmm,D (2 digits for degrees)
                return $"{degrees:D2}{minutes:06.3f},{direction}";
            }
            else
            {
                // Longitude: dddmm.mmmm,D (3 digits for degrees)
                return $"{degrees:D3}{minutes:06.3f},{direction}";
            }
        }

        /// <summary>
        /// Validates coordinate conversion with specified tolerance for floating-point precision.
        /// </summary>
        /// <param name="expected">Expected NMEA format coordinate</param>
        /// <param name="actual">Actual NMEA format coordinate</param>
        /// <param name="toleranceMinutes">Tolerance in minutes for precision comparison</param>
        /// <returns>True if coordinates match within tolerance</returns>
        public static bool ValidateCoordinateConversion(string expected, string actual, double toleranceMinutes = 0.001)
        {
            if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(actual))
                return false;

            var expectedParts = expected.Split(',');
            var actualParts = actual.Split(',');
            
            if (expectedParts.Length != 2 || actualParts.Length != 2)
                return false;

            // Check direction matches
            if (expectedParts[1] != actualParts[1])
                return false;

            // Parse and compare coordinate values
            if (!TryParseNmeaCoordinate(expectedParts[0], out double expectedMinutes) ||
                !TryParseNmeaCoordinate(actualParts[0], out double actualMinutes))
                return false;

            return Math.Abs(expectedMinutes - actualMinutes) <= toleranceMinutes;
        }

        /// <summary>
        /// Validates that coordinates are within valid ranges for maritime navigation.
        /// </summary>
        /// <param name="latitude">Latitude in decimal degrees</param>
        /// <param name="longitude">Longitude in decimal degrees</param>
        /// <returns>ValidationResult with range check results</returns>
        public static CoordinateValidationResult ValidateCoordinateRanges(double latitude, double longitude)
        {
            var result = new CoordinateValidationResult { IsValid = true };

            // Validate latitude range (-90 to +90)
            if (latitude < -90.0 || latitude > 90.0)
            {
                result.IsValid = false;
                result.Errors.Add($"Latitude {latitude} is outside valid range (-90.0 to +90.0)");
            }

            // Validate longitude range (-180 to +180)
            if (longitude < -180.0 || longitude > 180.0)
            {
                result.IsValid = false;
                result.Errors.Add($"Longitude {longitude} is outside valid range (-180.0 to +180.0)");
            }

            // Check for AIS "not available" values
            if (Math.Abs(latitude - 91.0) < 0.001)
            {
                result.IsValid = false;
                result.Errors.Add("Latitude 91.0 indicates 'not available' in AIS standard");
            }

            if (Math.Abs(longitude - 181.0) < 0.001)
            {
                result.IsValid = false;
                result.Errors.Add("Longitude 181.0 indicates 'not available' in AIS standard");
            }

            return result;
        }

        /// <summary>
        /// Checks if a coordinate is within a specified bounding box.
        /// </summary>
        /// <param name="latitude">Point latitude</param>
        /// <param name="longitude">Point longitude</param>
        /// <param name="north">Bounding box north boundary</param>
        /// <param name="south">Bounding box south boundary</param>
        /// <param name="east">Bounding box east boundary</param>
        /// <param name="west">Bounding box west boundary</param>
        /// <returns>True if point is within bounding box (inclusive of boundaries)</returns>
        public static bool IsWithinBoundingBox(double latitude, double longitude, 
            double north, double south, double east, double west)
        {
            // Handle potential longitude wraparound at dateline
            bool longitudeInRange;
            if (west <= east)
            {
                // Normal case: bounding box doesn't cross dateline
                longitudeInRange = longitude >= west && longitude <= east;
            }
            else
            {
                // Bounding box crosses dateline
                longitudeInRange = longitude >= west || longitude <= east;
            }

            return latitude >= south && latitude <= north && longitudeInRange;
        }

        /// <summary>
        /// Generates test cases for coordinate edge cases and boundary conditions.
        /// </summary>
        /// <returns>Collection of coordinate test cases covering edge conditions</returns>
        public static IEnumerable<CoordinateEdgeCaseTest> GetCoordinateEdgeCases()
        {
            return new[]
            {
                new CoordinateEdgeCaseTest
                {
                    Name = "Equator and Prime Meridian",
                    Latitude = 0.0,
                    Longitude = 0.0,
                    ExpectedValid = true
                },
                new CoordinateEdgeCaseTest
                {
                    Name = "North Pole",
                    Latitude = 90.0,
                    Longitude = 0.0,
                    ExpectedValid = true
                },
                new CoordinateEdgeCaseTest
                {
                    Name = "South Pole",
                    Latitude = -90.0,
                    Longitude = 0.0,
                    ExpectedValid = true
                },
                new CoordinateEdgeCaseTest
                {
                    Name = "International Date Line (East)",
                    Latitude = 0.0,
                    Longitude = 180.0,
                    ExpectedValid = true
                },
                new CoordinateEdgeCaseTest
                {
                    Name = "International Date Line (West)",
                    Latitude = 0.0,
                    Longitude = -180.0,
                    ExpectedValid = true
                },
                new CoordinateEdgeCaseTest
                {
                    Name = "AIS Not Available - Latitude",
                    Latitude = 91.0,
                    Longitude = 0.0,
                    ExpectedValid = false
                },
                new CoordinateEdgeCaseTest
                {
                    Name = "AIS Not Available - Longitude",
                    Latitude = 0.0,
                    Longitude = 181.0,
                    ExpectedValid = false
                },
                new CoordinateEdgeCaseTest
                {
                    Name = "Beyond North Pole",
                    Latitude = 91.0,
                    Longitude = 0.0,
                    ExpectedValid = false
                },
                new CoordinateEdgeCaseTest
                {
                    Name = "Beyond South Pole",
                    Latitude = -91.0,
                    Longitude = 0.0,
                    ExpectedValid = false
                },
                new CoordinateEdgeCaseTest
                {
                    Name = "Beyond East Longitude",
                    Latitude = 0.0,
                    Longitude = 181.0,
                    ExpectedValid = false
                },
                new CoordinateEdgeCaseTest
                {
                    Name = "Beyond West Longitude",
                    Latitude = 0.0,
                    Longitude = -181.0,
                    ExpectedValid = false
                },
                new CoordinateEdgeCaseTest
                {
                    Name = "High Precision Pacific Northwest",
                    Latitude = 48.123456789,
                    Longitude = -122.987654321,
                    ExpectedValid = true
                }
            };
        }

        /// <summary>
        /// Parses NMEA coordinate format (ddmm.mmmm or dddmm.mmmm) to total minutes.
        /// </summary>
        private static bool TryParseNmeaCoordinate(string coordinate, out double totalMinutes)
        {
            totalMinutes = 0;
            
            if (string.IsNullOrEmpty(coordinate))
                return false;

            try
            {
                // Determine if this is latitude (ddmm.mmmm) or longitude (dddmm.mmmm)
                bool isLongitude = coordinate.Length >= 8; // dddmm.mmm format
                
                int degreeDigits = isLongitude ? 3 : 2;
                
                if (coordinate.Length < degreeDigits + 3) // At least dd/dddmm format
                    return false;

                string degreesStr = coordinate.Substring(0, degreeDigits);
                string minutesStr = coordinate.Substring(degreeDigits);

                if (!int.TryParse(degreesStr, out int degrees) ||
                    !double.TryParse(minutesStr, out double minutes))
                    return false;

                totalMinutes = degrees * 60.0 + minutes;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Result of coordinate validation with detailed error information.
    /// </summary>
    public class CoordinateValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public string ErrorSummary => string.Join("; ", Errors);
    }

    /// <summary>
    /// Test case for coordinate edge conditions and boundary values.
    /// </summary>
    public class CoordinateEdgeCaseTest
    {
        public string Name { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool ExpectedValid { get; set; }
        public string Notes { get; set; } = "";
    }
}
