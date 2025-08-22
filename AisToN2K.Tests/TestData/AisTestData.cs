using AisToN2K.Models;

namespace AisToN2K.Tests.TestData
{
    /// <summary>
    /// Provides realistic AIS Stream API JSON test data for all supported message types.
    /// Based on aisstream.io WebSocket API format and industry standards.
    /// </summary>
    public static class AisTestData
    {
        #region Type 1 - Position Report (Class A)
        
        /// <summary>
        /// Valid AIS Type 1 Position Report - Container Ship in Pacific Northwest
        /// </summary>
        public static readonly string ValidType1Json = """
        {
            "MessageType": "PositionReport",
            "MetaData": {
                "MMSI": 123456789,
                "time_utc": "2025-08-21T10:30:45Z",
                "ShipName": "CONTAINER VESSEL",
                "latitude": 48.5000,
                "longitude": -122.8000
            },
            "Message": {
                "PositionReport": {
                    "UserID": 123456789,
                    "Latitude": 48.5000,
                    "Longitude": -122.8000,
                    "Sog": 12.5,
                    "Cog": 89.9,
                    "TrueHeading": 90,
                    "NavigationalStatus": 0,
                    "Timestamp": 55
                }
            }
        }
        """;

        /// <summary>
        /// AIS Type 1 with edge case coordinates (near dateline)
        /// </summary>
        public static readonly string Type1NearDatelineJson = """
        {
            "MessageType": "PositionReport",
            "MetaData": {
                "MMSI": 987654321,
                "time_utc": "2025-08-21T10:30:45Z",
                "latitude": 35.6762,
                "longitude": 179.9999
            },
            "Message": {
                "PositionReport": {
                    "UserID": 987654321,
                    "Latitude": 35.6762,
                    "Longitude": 179.9999,
                    "Sog": 8.3,
                    "Cog": 270.0,
                    "TrueHeading": 270,
                    "NavigationalStatus": 0,
                    "Timestamp": 30
                }
            }
        }
        """;

        /// <summary>
        /// AIS Type 1 with invalid coordinates (out of range)
        /// </summary>
        public static readonly string Type1InvalidCoordinatesJson = """
        {
            "MessageType": "PositionReport",
            "MetaData": {
                "MMSI": 111222333,
                "time_utc": "2025-08-21T10:30:45Z",
                "latitude": 181.0,
                "longitude": 91.0
            },
            "Message": {
                "PositionReport": {
                    "UserID": 111222333,
                    "Latitude": 181.0,
                    "Longitude": 91.0,
                    "Sog": null,
                    "Cog": 360.0,
                    "TrueHeading": 511,
                    "NavigationalStatus": 15,
                    "Timestamp": 60
                }
            }
        }
        """;

        #endregion

        #region Type 5 - Static and Voyage Related Data

        /// <summary>
        /// Valid AIS Type 5 Static and Voyage Data
        /// </summary>
        public static readonly string ValidType5Json = """
        {
            "MessageType": "ShipAndVoyageData",
            "MetaData": {
                "MMSI": 123456789,
                "time_utc": "2025-08-21T10:30:45Z",
                "ShipName": "CONTAINER VESSEL"
            },
            "Message": {
                "ShipAndVoyageData": {
                    "UserID": 123456789,
                    "VesselName": "CONTAINER VESSEL",
                    "TypeOfShipAndCargoType": 70,
                    "CallSign": "ABCD123",
                    "Destination": "SEATTLE"
                }
            }
        }
        """;

        /// <summary>
        /// AIS Type 5 with maximum field lengths
        /// </summary>
        public static readonly string Type5MaxLengthJson = """
        {
            "MessageType": "ShipAndVoyageData",
            "MetaData": {
                "MMSI": 999888777,
                "time_utc": "2025-08-21T10:30:45Z",
                "ShipName": "TWENTYCHARACTERNAME1"
            },
            "Message": {
                "ShipAndVoyageData": {
                    "UserID": 999888777,
                    "VesselName": "TWENTYCHARACTERNAME1",
                    "TypeOfShipAndCargoType": 99,
                    "CallSign": "1234567",
                    "Destination": "TWENTYCHARACTERPORT"
                }
            }
        }
        """;

        #endregion

        #region Type 18 - Standard Class B Position Report

        /// <summary>
        /// Valid AIS Type 18 Class B Position Report
        /// </summary>
        public static readonly string ValidType18Json = """
        {
            "MessageType": "StandardClassBPositionReport",
            "MetaData": {
                "MMSI": 987654321,
                "time_utc": "2025-08-21T10:30:45Z",
                "latitude": 48.2000,
                "longitude": -123.1000
            },
            "Message": {
                "StandardClassBPositionReport": {
                    "UserID": 987654321,
                    "Latitude": 48.2000,
                    "Longitude": -123.1000,
                    "Sog": 7.3,
                    "Cog": 210.0,
                    "TrueHeading": 211,
                    "Timestamp": 38
                }
            }
        }
        """;

        /// <summary>
        /// AIS Type 18 with minimal speed (anchored)
        /// </summary>
        public static readonly string Type18AnchoredJson = """
        {
            "MessageType": "StandardClassBPositionReport",
            "MetaData": {
                "MMSI": 555666777,
                "time_utc": "2025-08-21T10:30:45Z",
                "latitude": 48.7500,
                "longitude": -122.3000
            },
            "Message": {
                "StandardClassBPositionReport": {
                    "UserID": 555666777,
                    "Latitude": 48.7500,
                    "Longitude": -122.3000,
                    "Sog": 0.0,
                    "Cog": null,
                    "TrueHeading": null,
                    "Timestamp": 15
                }
            }
        }
        """;

        #endregion

        #region Type 24 - Static Data Report (Class B)

        /// <summary>
        /// Valid AIS Type 24 Part A (Name)
        /// </summary>
        public static readonly string ValidType24AJson = """
        {
            "MessageType": "StaticDataReport",
            "MetaData": {
                "MMSI": 987654321,
                "time_utc": "2025-08-21T10:30:45Z",
                "ShipName": "FISHING VESSEL"
            },
            "Message": {
                "StaticDataReport": {
                    "MessageID": 24,
                    "UserID": 987654321,
                    "ReportA": {
                        "Valid": true,
                        "Name": "FISHING VESSEL"
                    }
                }
            }
        }
        """;

        /// <summary>
        /// Valid AIS Type 24 Part B (Type and Dimensions)
        /// </summary>
        public static readonly string ValidType24BJson = """
        {
            "MessageType": "StaticDataReport",
            "MetaData": {
                "MMSI": 987654321,
                "time_utc": "2025-08-21T10:30:45Z"
            },
            "Message": {
                "StaticDataReport": {
                    "MessageID": 24,
                    "UserID": 987654321,
                    "ReportB": {
                        "Valid": true,
                        "CallSign": "FV123",
                        "ShipType": 30,
                        "VendorIDName": "ABC"
                    }
                }
            }
        }
        """;

        #endregion

        #region Malformed and Edge Cases

        /// <summary>
        /// JSON with missing required fields
        /// </summary>
        public static readonly string MissingFieldsJson = """
        {
            "MessageType": "PositionReport",
            "MetaData": {
                "time_utc": "2025-08-21T10:30:45Z"
            },
            "Message": {
                "PositionReport": {
                    "Sog": 12.5
                }
            }
        }
        """;

        /// <summary>
        /// JSON with invalid data types
        /// </summary>
        public static readonly string InvalidDataTypesJson = """
        {
            "MessageType": "PositionReport",
            "MetaData": {
                "MMSI": "not_a_number",
                "time_utc": "invalid_date",
                "latitude": "invalid_coordinate",
                "longitude": true
            },
            "Message": {
                "PositionReport": {
                    "UserID": "string_instead_of_int",
                    "Latitude": "not_a_double",
                    "Longitude": [],
                    "Sog": "fast",
                    "Cog": {},
                    "TrueHeading": "north"
                }
            }
        }
        """;

        /// <summary>
        /// Empty JSON object
        /// </summary>
        public static readonly string EmptyJson = "{}";

        /// <summary>
        /// Malformed JSON (invalid syntax)
        /// </summary>
        public static readonly string MalformedJson = """
        {
            "MessageType": "PositionReport",
            "MetaData": {
                "MMSI": 123456789,
                "time_utc": "2025-08-21T10:30:45Z"
                // Missing closing brace
        """;

        #endregion

        #region Expected NMEA0183 Output Examples

        /// <summary>
        /// Expected NMEA0183 sentence for ValidType1Json
        /// Format: !AIVDM,fragment_count,fragment_number,message_id,channel,data,fill_bits*checksum
        /// </summary>
        public static readonly string ExpectedType1Nmea = "!AIVDM,1,1,,A,15Muq70001G?tRrM5M4P8?v4080u,0*7C";

        /// <summary>
        /// Expected NMEA0183 sentence for ValidType5Json
        /// Type 5 messages are typically longer and may require fragmentation
        /// </summary>
        public static readonly string ExpectedType5NmeaPart1 = "!AIVDM,2,1,0,A,55?MbV02;H;s<HtKR20EHE:0@T4@Dn2222222216L961O5Gf0NSQEp6ClRp8,0*0F";
        public static readonly string ExpectedType5NmeaPart2 = "!AIVDM,2,2,0,A,88888888880,2*23";

        #endregion

        #region Coordinate Conversion Test Cases

        public static readonly List<CoordinateTestCase> CoordinateTestCases = new()
        {
            new CoordinateTestCase
            {
                Description = "Standard Pacific Northwest coordinates",
                DecimalLatitude = 48.5000,
                DecimalLongitude = -122.8000,
                ExpectedNmeaLatitude = "4830.000,N",
                ExpectedNmeaLongitude = "12248.000,W"
            },
            new CoordinateTestCase
            {
                Description = "Equator and Prime Meridian",
                DecimalLatitude = 0.0,
                DecimalLongitude = 0.0,
                ExpectedNmeaLatitude = "0000.000,N",
                ExpectedNmeaLongitude = "00000.000,E"
            },
            new CoordinateTestCase
            {
                Description = "Maximum precision coordinates",
                DecimalLatitude = 59.123456,
                DecimalLongitude = 10.987654,
                ExpectedNmeaLatitude = "5907.407,N",
                ExpectedNmeaLongitude = "01059.259,E"
            },
            new CoordinateTestCase
            {
                Description = "Southern hemisphere",
                DecimalLatitude = -33.856159,
                DecimalLongitude = 151.215256,
                ExpectedNmeaLatitude = "3351.370,S",
                ExpectedNmeaLongitude = "15112.915,E"
            },
            new CoordinateTestCase
            {
                Description = "Near dateline (positive)",
                DecimalLatitude = 35.6762,
                DecimalLongitude = 179.9999,
                ExpectedNmeaLatitude = "3540.572,N",
                ExpectedNmeaLongitude = "17959.994,E"
            },
            new CoordinateTestCase
            {
                Description = "Near dateline (negative)",
                DecimalLatitude = 35.6762,
                DecimalLongitude = -179.9999,
                ExpectedNmeaLatitude = "3540.572,N",
                ExpectedNmeaLongitude = "17959.994,W"
            }
        };

        #endregion

        #region Bounding Box Test Cases

        public static readonly List<BoundingBoxTestCase> BoundingBoxTestCases = new()
        {
            new BoundingBoxTestCase
            {
                Description = "Point inside Pacific Northwest bounding box",
                Latitude = 48.5000,
                Longitude = -122.8000,
                BoundingBox = new() { North = 48.8000, South = 48.0000, East = -122.1900, West = -123.3550 },
                ExpectedInside = true
            },
            new BoundingBoxTestCase
            {
                Description = "Point on northern boundary",
                Latitude = 48.8000,
                Longitude = -122.8000,
                BoundingBox = new() { North = 48.8000, South = 48.0000, East = -122.1900, West = -123.3550 },
                ExpectedInside = true
            },
            new BoundingBoxTestCase
            {
                Description = "Point just outside northern boundary",
                Latitude = 48.8001,
                Longitude = -122.8000,
                BoundingBox = new() { North = 48.8000, South = 48.0000, East = -122.1900, West = -123.3550 },
                ExpectedInside = false
            },
            new BoundingBoxTestCase
            {
                Description = "Point outside eastern boundary",
                Latitude = 48.5000,
                Longitude = -122.1800,
                BoundingBox = new() { North = 48.8000, South = 48.0000, East = -122.1900, West = -123.3550 },
                ExpectedInside = false
            },
            new BoundingBoxTestCase
            {
                Description = "Point in different ocean (Atlantic)",
                Latitude = 40.7128,
                Longitude = -74.0060,
                BoundingBox = new() { North = 48.8000, South = 48.0000, East = -122.1900, West = -123.3550 },
                ExpectedInside = false
            }
        };

        #endregion

        #region NMEA Checksum Test Cases

        public static readonly List<NmeaChecksumTestCase> NmeaChecksumTestCases = new()
        {
            new NmeaChecksumTestCase
            {
                Description = "Standard AIS position report",
                Sentence = "AIVDM,1,1,,A,15Muq70001G?tRrM5M4P8?v4080u,0",
                ExpectedChecksum = "7C"
            },
            new NmeaChecksumTestCase
            {
                Description = "Multi-fragment message part 1",
                Sentence = "AIVDM,2,1,0,A,55?MbV02;H;s<HtKR20EHE:0@T4@Dn2222222216L961O5Gf0NSQEp6ClRp8,0",
                ExpectedChecksum = "0F"
            },
            new NmeaChecksumTestCase
            {
                Description = "Multi-fragment message part 2",
                Sentence = "AIVDM,2,2,0,A,88888888880,2",
                ExpectedChecksum = "23"
            },
            new NmeaChecksumTestCase
            {
                Description = "Simple test sentence",
                Sentence = "GPGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,46.9,M,,",
                ExpectedChecksum = "47"
            }
        };

        #endregion
    }

    #region Test Case Classes

    public class CoordinateTestCase
    {
        public string Description { get; set; } = "";
        public double DecimalLatitude { get; set; }
        public double DecimalLongitude { get; set; }
        public string ExpectedNmeaLatitude { get; set; } = "";
        public string ExpectedNmeaLongitude { get; set; } = "";
    }

    public class BoundingBoxTestCase
    {
        public string Description { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public Configuration.BoundingBox BoundingBox { get; set; } = new();
        public bool ExpectedInside { get; set; }
    }

    public class NmeaChecksumTestCase
    {
        public string Description { get; set; } = "";
        public string Sentence { get; set; } = "";
        public string ExpectedChecksum { get; set; } = "";
    }

    #endregion
}
