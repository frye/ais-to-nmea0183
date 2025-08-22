using AisToN2K.Models;
using AisToN2K.Services;
using FluentAssertions;
using Xunit;
using System;

namespace AisToN2K.Tests.Unit
{
    public class CoordinateConversionTests
    {
        private readonly Nmea0183Converter _converter;

        public CoordinateConversionTests()
        {
            _converter = new Nmea0183Converter();
        }

        [Fact]
        public void ConvertAisPosition_ValidType1Position_ShouldWork()
        {
            // Arrange
            var aisData = new AisData
            {
                MessageType = 1,
                Mmsi = 123456789,
                Latitude = 47.6062095,
                Longitude = -122.3320708,
                SpeedOverGround = 5.5,
                CourseOverGround = 180.0,
                Heading = 180,
                Timestamp = DateTime.Parse("2024-01-01T12:00:00.000Z")
            };

            // Act
            var result = _converter.ConvertAisPosition(aisData);

            // Assert
            result.Should().NotBeNullOrEmpty();
        }
    }
}