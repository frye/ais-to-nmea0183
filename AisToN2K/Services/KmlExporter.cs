using System.Globalization;
using System.Text;

namespace AisToN2K.Services
{
    /// <summary>
    /// Exports vessel track points to KML format.
    /// </summary>
    public static class KmlExporter
    {
        public static string Export(List<TrackPoint> points, string vesselName, int mmsi)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<kml xmlns=\"http://www.opengis.net/kml/2.2\">");
            sb.AppendLine("  <Document>");
            sb.AppendLine($"    <name>{EscapeXml(vesselName)} (MMSI: {mmsi})</name>");
            sb.AppendLine($"    <description>AIS track for vessel {EscapeXml(vesselName)}</description>");

            // Line style
            sb.AppendLine("    <Style id=\"trackLine\">");
            sb.AppendLine("      <LineStyle>");
            sb.AppendLine("        <color>ff0000ff</color>");
            sb.AppendLine("        <width>3</width>");
            sb.AppendLine("      </LineStyle>");
            sb.AppendLine("    </Style>");

            // Track as LineString
            sb.AppendLine("    <Placemark>");
            sb.AppendLine($"      <name>{EscapeXml(vesselName)} Track</name>");
            sb.AppendLine("      <styleUrl>#trackLine</styleUrl>");
            sb.AppendLine("      <LineString>");
            sb.AppendLine("        <tessellate>1</tessellate>");
            sb.AppendLine("        <coordinates>");

            foreach (var pt in points)
            {
                sb.AppendLine($"          {pt.Longitude.ToString("F6", CultureInfo.InvariantCulture)},{pt.Latitude.ToString("F6", CultureInfo.InvariantCulture)},0");
            }

            sb.AppendLine("        </coordinates>");
            sb.AppendLine("      </LineString>");
            sb.AppendLine("    </Placemark>");

            // Individual waypoints with timestamps
            if (points.Count > 0)
            {
                sb.AppendLine("    <Folder>");
                sb.AppendLine("      <name>Track Points</name>");

                // Add start and end points, plus periodic points
                var step = Math.Max(1, points.Count / 50); // At most ~50 waypoint markers
                for (int i = 0; i < points.Count; i += step)
                {
                    var pt = points[i];
                    sb.AppendLine("      <Placemark>");
                    sb.AppendLine($"        <name>Point {i + 1}</name>");
                    sb.AppendLine($"        <description>Time: {pt.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
                    if (pt.SpeedOverGround.HasValue)
                        sb.AppendLine($"SOG: {pt.SpeedOverGround.Value:F1} kn");
                    if (pt.CourseOverGround.HasValue)
                        sb.AppendLine($"COG: {pt.CourseOverGround.Value:F1}°");
                    sb.AppendLine("</description>");
                    sb.AppendLine($"        <TimeStamp><when>{pt.Timestamp:yyyy-MM-ddTHH:mm:ssZ}</when></TimeStamp>");
                    sb.AppendLine("        <Point>");
                    sb.AppendLine($"          <coordinates>{pt.Longitude.ToString("F6", CultureInfo.InvariantCulture)},{pt.Latitude.ToString("F6", CultureInfo.InvariantCulture)},0</coordinates>");
                    sb.AppendLine("        </Point>");
                    sb.AppendLine("      </Placemark>");
                }

                // Always include the last point
                if (points.Count > 1 && (points.Count - 1) % step != 0)
                {
                    var pt = points[^1];
                    sb.AppendLine("      <Placemark>");
                    sb.AppendLine($"        <name>Point {points.Count}</name>");
                    sb.AppendLine($"        <description>Time: {pt.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
                    if (pt.SpeedOverGround.HasValue)
                        sb.AppendLine($"SOG: {pt.SpeedOverGround.Value:F1} kn");
                    sb.AppendLine("</description>");
                    sb.AppendLine($"        <TimeStamp><when>{pt.Timestamp:yyyy-MM-ddTHH:mm:ssZ}</when></TimeStamp>");
                    sb.AppendLine("        <Point>");
                    sb.AppendLine($"          <coordinates>{pt.Longitude.ToString("F6", CultureInfo.InvariantCulture)},{pt.Latitude.ToString("F6", CultureInfo.InvariantCulture)},0</coordinates>");
                    sb.AppendLine("        </Point>");
                    sb.AppendLine("      </Placemark>");
                }

                sb.AppendLine("    </Folder>");
            }

            sb.AppendLine("  </Document>");
            sb.AppendLine("</kml>");

            return sb.ToString();
        }

        public static async Task ExportToFileAsync(List<TrackPoint> points, string vesselName, int mmsi, string filePath)
        {
            var content = Export(points, vesselName, mmsi);
            await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
        }

        private static string EscapeXml(string text)
        {
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }
}
