using AisToN2K.Services;

namespace AisToN2K.Tests.Unit
{
    public class ExporterTests
    {
        private readonly List<TrackPoint> _samplePoints;

        public ExporterTests()
        {
            _samplePoints = new List<TrackPoint>
            {
                new(48.0, -122.0, 8.5, 180.0, new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc)),
                new(48.01, -122.01, 9.0, 185.0, new DateTime(2025, 1, 1, 12, 5, 0, DateTimeKind.Utc)),
                new(48.02, -122.02, 7.5, 190.0, new DateTime(2025, 1, 1, 12, 10, 0, DateTimeKind.Utc)),
            };
        }

        // GPX Tests

        [Fact]
        public void GpxExport_ProducesValidXml()
        {
            var output = GpxExporter.Export(_samplePoints, "Test Vessel", 123456789);

            output.Should().Contain("<?xml version=\"1.0\"");
            output.Should().Contain("<gpx version=\"1.1\"");
            output.Should().Contain("</gpx>");
        }

        [Fact]
        public void GpxExport_ContainsVesselMetadata()
        {
            var output = GpxExporter.Export(_samplePoints, "Test Vessel", 123456789);

            output.Should().Contain("Test Vessel");
            output.Should().Contain("123456789");
        }

        [Fact]
        public void GpxExport_ContainsTrackPoints()
        {
            var output = GpxExporter.Export(_samplePoints, "Test Vessel", 123456789);

            output.Should().Contain("<trkseg>");
            output.Should().Contain("<trkpt");
            output.Should().Contain("lat=\"48.000000\"");
            output.Should().Contain("lon=\"-122.000000\"");
            output.Should().Contain("lat=\"48.010000\"");
        }

        [Fact]
        public void GpxExport_ContainsTimestamps()
        {
            var output = GpxExporter.Export(_samplePoints, "Test Vessel", 123456789);

            output.Should().Contain("<time>2025-01-01T12:00:00Z</time>");
            output.Should().Contain("<time>2025-01-01T12:05:00Z</time>");
        }

        [Fact]
        public void GpxExport_ContainsSpeedData()
        {
            var output = GpxExporter.Export(_samplePoints, "Test Vessel", 123456789);

            // SOG 8.5 kn = ~4.37 m/s
            output.Should().Contain("<speed>");
        }

        [Fact]
        public void GpxExport_EmptyPoints_ProducesValidXml()
        {
            var output = GpxExporter.Export(new List<TrackPoint>(), "Test", 123);

            output.Should().Contain("<gpx");
            output.Should().Contain("</gpx>");
            output.Should().Contain("<trkseg>");
        }

        [Fact]
        public void GpxExport_EscapesXmlCharacters()
        {
            var output = GpxExporter.Export(_samplePoints, "Test <Vessel> & \"Name\"", 123456789);

            output.Should().Contain("Test &lt;Vessel&gt; &amp; &quot;Name&quot;");
        }

        [Fact]
        public async Task GpxExport_WritesToFile()
        {
            var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.gpx");
            try
            {
                await GpxExporter.ExportToFileAsync(_samplePoints, "Test Vessel", 123456789, path);

                File.Exists(path).Should().BeTrue();
                var content = await File.ReadAllTextAsync(path);
                content.Should().Contain("<gpx");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        // KML Tests

        [Fact]
        public void KmlExport_ProducesValidXml()
        {
            var output = KmlExporter.Export(_samplePoints, "Test Vessel", 123456789);

            output.Should().Contain("<?xml version=\"1.0\"");
            output.Should().Contain("<kml xmlns=");
            output.Should().Contain("</kml>");
        }

        [Fact]
        public void KmlExport_ContainsVesselMetadata()
        {
            var output = KmlExporter.Export(_samplePoints, "Test Vessel", 123456789);

            output.Should().Contain("Test Vessel");
            output.Should().Contain("123456789");
        }

        [Fact]
        public void KmlExport_ContainsLineString()
        {
            var output = KmlExporter.Export(_samplePoints, "Test Vessel", 123456789);

            output.Should().Contain("<LineString>");
            output.Should().Contain("<coordinates>");
            output.Should().Contain("-122.000000,48.000000,0");
        }

        [Fact]
        public void KmlExport_ContainsTrackStyle()
        {
            var output = KmlExporter.Export(_samplePoints, "Test Vessel", 123456789);

            output.Should().Contain("<Style id=\"trackLine\">");
            output.Should().Contain("#trackLine");
        }

        [Fact]
        public void KmlExport_ContainsWaypoints()
        {
            var output = KmlExporter.Export(_samplePoints, "Test Vessel", 123456789);

            output.Should().Contain("<Folder>");
            output.Should().Contain("Track Points");
            output.Should().Contain("<Point>");
        }

        [Fact]
        public void KmlExport_EscapesXmlCharacters()
        {
            var output = KmlExporter.Export(_samplePoints, "Test <Vessel> & \"Name\"", 123456789);

            output.Should().Contain("Test &lt;Vessel&gt; &amp; &quot;Name&quot;");
        }

        [Fact]
        public async Task KmlExport_WritesToFile()
        {
            var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.kml");
            try
            {
                await KmlExporter.ExportToFileAsync(_samplePoints, "Test Vessel", 123456789, path);

                File.Exists(path).Should().BeTrue();
                var content = await File.ReadAllTextAsync(path);
                content.Should().Contain("<kml");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void KmlExport_EmptyPoints_ProducesValidXml()
        {
            var output = KmlExporter.Export(new List<TrackPoint>(), "Test", 123);

            output.Should().Contain("<kml");
            output.Should().Contain("</kml>");
        }
    }
}
