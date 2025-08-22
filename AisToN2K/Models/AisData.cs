using Newtonsoft.Json;

namespace AisToN2K.Models
{
    public class AisData
    {
        [JsonProperty("mmsi")]
        public int Mmsi { get; set; }
        
        [JsonProperty("longitude")]
        public double Longitude { get; set; }
        
        [JsonProperty("latitude")]
        public double Latitude { get; set; }
        
        [JsonProperty("sog")]
        public double? SpeedOverGround { get; set; }
        
        [JsonProperty("cog")]
        public double? CourseOverGround { get; set; }
        
        [JsonProperty("heading")]
        public double? Heading { get; set; }
        
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }
        
        [JsonProperty("vessel_name")]
        public string? VesselName { get; set; }
        
        [JsonProperty("vessel_type")]
        public int? VesselType { get; set; }
        
        [JsonProperty("call_sign")]
        public string? CallSign { get; set; }
        
        // AIS message type (1=Position Report, 4=Base Station Report, 5=Static and Voyage Data, etc.)
        public int MessageType { get; set; } = 1; // Default to Position Report
    }
    
    public class AisApiResponse
    {
        [JsonProperty("vessels")]
        public List<AisData> Vessels { get; set; } = new List<AisData>();
    }

    // AIS Stream WebSocket message models
    public class AisStreamMessage
    {
        [JsonProperty("MessageType")]
        public string? MessageType { get; set; }
        
        [JsonProperty("MetaData")]
        public AisStreamMetaData? MetaData { get; set; }
        
        [JsonProperty("Message")]
        public AisStreamMessageContent? Message { get; set; }
    }

    public class AisStreamMetaData
    {
        [JsonProperty("MMSI")]
        public int MMSI { get; set; }
        
        [JsonProperty("time_utc")]
        public string TimeUtcString { get; set; } = "";
        
        public DateTime TimeUtc 
        { 
            get 
            {
                if (DateTime.TryParse(TimeUtcString, out DateTime result))
                    return result;
                return DateTime.UtcNow;
            }
        }
        
        [JsonProperty("ShipName")]
        public string? ShipName { get; set; }
        
        [JsonProperty("latitude")]
        public double? Latitude { get; set; }
        
        [JsonProperty("longitude")]
        public double? Longitude { get; set; }
    }

    public class AisStreamMessageContent
    {
        [JsonProperty("PositionReport")]
        public AisPositionReport? PositionReport { get; set; }
        
        [JsonProperty("ShipAndVoyageData")]
        public AisShipAndVoyageData? ShipAndVoyageData { get; set; }
        
        [JsonProperty("StandardClassBPositionReport")]
        public AisStandardClassBPositionReport? StandardClassBPositionReport { get; set; }
        
        [JsonProperty("ShipStaticData")]
        public AisShipStaticData? ShipStaticData { get; set; }
        
        [JsonProperty("StaticDataReport")]
        public AisStaticDataReport? StaticDataReport { get; set; }
    }

    public class AisPositionReport
    {
        [JsonProperty("UserID")]
        public int UserID { get; set; }
        
        [JsonProperty("Latitude")]
        public double Latitude { get; set; }
        
        [JsonProperty("Longitude")]
        public double Longitude { get; set; }
        
        [JsonProperty("Sog")]
        public double? Sog { get; set; }
        
        [JsonProperty("Cog")]
        public double? Cog { get; set; }
        
        [JsonProperty("TrueHeading")]
        public int? TrueHeading { get; set; }
        
        [JsonProperty("NavigationalStatus")]
        public int? NavigationalStatus { get; set; }
        
        [JsonProperty("Timestamp")]
        public int? Timestamp { get; set; }
    }

    public class AisShipAndVoyageData
    {
        [JsonProperty("UserID")]
        public int UserID { get; set; }
        
        [JsonProperty("VesselName")]
        public string? VesselName { get; set; }
        
        [JsonProperty("TypeOfShipAndCargoType")]
        public int? TypeOfShipAndCargoType { get; set; }
        
        [JsonProperty("CallSign")]
        public string? CallSign { get; set; }
        
        [JsonProperty("Destination")]
        public string? Destination { get; set; }
    }

    public class AisStandardClassBPositionReport
    {
        [JsonProperty("UserID")]
        public int UserID { get; set; }
        
        [JsonProperty("Latitude")]
        public double Latitude { get; set; }
        
        [JsonProperty("Longitude")]
        public double Longitude { get; set; }
        
        [JsonProperty("Sog")]
        public double? Sog { get; set; }
        
        [JsonProperty("Cog")]
        public double? Cog { get; set; }
        
        [JsonProperty("TrueHeading")]
        public int? TrueHeading { get; set; }
        
        [JsonProperty("Timestamp")]
        public int? Timestamp { get; set; }
    }

    public class AisShipStaticData
    {
        [JsonProperty("UserID")]
        public int UserID { get; set; }
        
        [JsonProperty("Name")]
        public string? Name { get; set; }
        
        [JsonProperty("Type")]
        public int? Type { get; set; }
        
        [JsonProperty("CallSign")]
        public string? CallSign { get; set; }
        
        [JsonProperty("Destination")]
        public string? Destination { get; set; }
    }

    public class AisStaticDataReport
    {
        [JsonProperty("MessageID")]
        public int MessageID { get; set; }
        
        [JsonProperty("UserID")]
        public int UserID { get; set; }
        
        [JsonProperty("ReportA")]
        public AisStaticDataReportA? ReportA { get; set; }
        
        [JsonProperty("ReportB")]
        public AisStaticDataReportB? ReportB { get; set; }
    }

    public class AisStaticDataReportA
    {
        [JsonProperty("Valid")]
        public bool Valid { get; set; }
        
        [JsonProperty("Name")]
        public string? Name { get; set; }
    }

    public class AisStaticDataReportB
    {
        [JsonProperty("Valid")]
        public bool Valid { get; set; }
        
        [JsonProperty("CallSign")]
        public string? CallSign { get; set; }
        
        [JsonProperty("ShipType")]
        public int? ShipType { get; set; }
        
        [JsonProperty("VendorIDName")]
        public string? VendorIDName { get; set; }
    }
}
