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
        
        // Additional AIS fields that were missing from broadcast data
        
        /// <summary>
        /// Rate of Turn (ROT) in degrees per minute.
        /// Range: -128 to +127 degrees/minute, with special encoding.
        /// Positive values indicate starboard (right) turn, negative values indicate port (left) turn.
        /// Special values: -128 = turning left at more than 5°/30s, +127 = turning right at more than 5°/30s,
        /// 128 (0x80) = no turn information available.
        /// </summary>
        [JsonProperty("rate_of_turn")]
        public int? RateOfTurn { get; set; }
        
        /// <summary>
        /// Navigational Status indicating the vessel's current operational state.
        /// 0=under way using engine, 1=at anchor, 2=not under command, 3=restricted manoeuvrability,
        /// 4=constrained by her draught, 5=moored, 6=aground, 7=engaged in fishing,
        /// 8=under way sailing, 9-14=reserved, 15=not defined.
        /// </summary>
        [JsonProperty("navigational_status")]
        public int? NavigationalStatus { get; set; }
        
        /// <summary>
        /// Position Accuracy flag. True = high accuracy (&lt;10m), False = low accuracy (&gt;10m).
        /// Indicates the accuracy of the GNSS position fix.
        /// </summary>
        [JsonProperty("position_accuracy")]
        public bool? PositionAccuracy { get; set; }
        
        /// <summary>
        /// RAIM (Receiver Autonomous Integrity Monitoring) flag.
        /// True = RAIM in use for this position, False = RAIM not in use.
        /// Indicates GPS/GNSS integrity monitoring status.
        /// </summary>
        [JsonProperty("raim")]
        public bool? Raim { get; set; }
        
        /// <summary>
        /// AIS timestamp in seconds (0-59) indicating when the position was obtained.
        /// 60 = not available, 61 = positioning system is in manual input mode,
        /// 62 = electronic position fixing system operates in estimated mode,
        /// 63 = position of vessel is not available.
        /// </summary>
        [JsonProperty("timestamp_seconds")]
        public int? TimestampSeconds { get; set; }
        
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
        
        /// <summary>
        /// Rate of Turn in degrees per minute (-128 to +127).
        /// Special encoding for extreme turn rates and "not available" status.
        /// </summary>
        [JsonProperty("RateOfTurn")]
        public int? RateOfTurn { get; set; }
        
        /// <summary>
        /// Position accuracy flag. True = high accuracy (&lt;10m), False = low accuracy (&gt;10m).
        /// </summary>
        [JsonProperty("PositionAccuracy")]
        public bool? PositionAccuracy { get; set; }
        
        /// <summary>
        /// RAIM (Receiver Autonomous Integrity Monitoring) flag.
        /// </summary>
        [JsonProperty("RAIM")]
        public bool? RAIM { get; set; }
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
        
        /// <summary>
        /// Position accuracy flag for Class B transponders.
        /// </summary>
        [JsonProperty("PositionAccuracy")]
        public bool? PositionAccuracy { get; set; }
        
        /// <summary>
        /// RAIM flag for Class B transponders.
        /// </summary>
        [JsonProperty("RAIM")]
        public bool? RAIM { get; set; }
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
