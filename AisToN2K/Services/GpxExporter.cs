using System.Globalization;
using System.Text;

namespace AisToN2K.Services
{
    /// <summary>
    /// Exports vessel track points to GPX 1.1 format.
    /// </summary>
    public static class GpxExporter
    {
        public static string Export(List<TrackPoint> points, string vesselName, int mmsi)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<gpx version=\"1.1\" creator=\"AIS-to-NMEA0183\"");
            sb.AppendLine("  xmlns=\"http://www.topografix.com/GPX/1/1\"");
            sb.AppendLine("  xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"");
            sb.AppendLine("  xsi:schemaLocation=\"http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/1/1/gpx.xsd\">");

            sb.AppendLine("  <metadata>");
            sb.AppendLine($"    <name>{EscapeXml(vesselName)} (MMSI: {mmsi})</name>");
            sb.AppendLine($"    <desc>AIS track for vessel {EscapeXml(vesselName)}</desc>");
            sb.AppendLine($"    <time>{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</time>");
            sb.AppendLine("  </metadata>");

            sb.AppendLine("  <trk>");
            sb.AppendLine($"    <name>{EscapeXml(vesselName)}</name>");
            sb.AppendLine($"    <desc>MMSI: {mmsi}</desc>");
            sb.AppendLine("    <trkseg>");

            foreach (var pt in points)
            {
                sb.Append($"      <trkpt lat=\"{pt.Latitude.ToString("F6", CultureInfo.InvariantCulture)}\" lon=\"{pt.Longitude.ToString("F6", CultureInfo.InvariantCulture)}\">");
                sb.Append($"<time>{pt.Timestamp:yyyy-MM-ddTHH:mm:ssZ}</time>");
                if (pt.SpeedOverGround.HasValue)
                {
                    // GPX speed is in m/s, convert from knots
                    var speedMs = pt.SpeedOverGround.Value * 0.514444;
                    sb.Append($"<speed>{speedMs.ToString("F2", CultureInfo.InvariantCulture)}</speed>");
                }
                if (pt.CourseOverGround.HasValue)
                {
                    sb.Append($"<course>{pt.CourseOverGround.Value.ToString("F1", CultureInfo.InvariantCulture)}</course>");
                }
                sb.AppendLine("</trkpt>");
            }

            sb.AppendLine("    </trkseg>");
            sb.AppendLine("  </trk>");
            sb.AppendLine("</gpx>");

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
