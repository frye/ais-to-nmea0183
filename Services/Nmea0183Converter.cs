using AisToN2K.Models;
using System.Text;

namespace AisToN2K.Services
{
    public class Nmea0183Converter
    {
        private int _sequence = 0;
        private readonly bool _debugMode;

        public Nmea0183Converter(bool debugMode = false)
        {
            _debugMode = debugMode;
        }

        public async Task<string?> ConvertToNmea0183Async(dynamic vesselData)
        {
            try
            {
                AisData aisData;
                int messageType;
                
                // Handle different input types
                if (vesselData is AisData aisDataObject)
                {
                    // Direct AisData object
                    aisData = aisDataObject;
                    messageType = aisData.MessageType;
                }
                else
                {
                    // Dynamic object from AIS Stream
                    messageType = vesselData.MessageType ?? 1;
                    
                    // Create AisData from dynamic data
                    aisData = new AisData
                    {
                        Mmsi = vesselData.UserID ?? 0,
                        Latitude = vesselData.MetaData?.latitude ?? 0,
                        Longitude = vesselData.MetaData?.longitude ?? 0,
                        SpeedOverGround = vesselData.Sog,
                        CourseOverGround = vesselData.Cog,
                        Heading = vesselData.Heading,
                        VesselName = vesselData.ShipName?.ToString()
                    };
                }

                // Convert based on message type
                return messageType switch
                {
                    1 or 2 or 3 => ConvertAisPosition(aisData),
                    5 => ConvertAisStatic(aisData),
                    18 or 19 => ConvertAisPosition(aisData),
                    24 => ConvertAisStatic(aisData),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error converting to NMEA 0183: {ex.Message}");
                return null;
            }
        }

        public string? ConvertAisPosition(AisData aisData)
        {
            if (aisData.Mmsi == 0)
                return null;

            // Use the actual message type from the AIS data
            int messageType = aisData.MessageType;
            
            // Build AIS binary message (168 bits total)
            var aisPayload = EncodeAisPositionBinary(
                messageType, 
                aisData.Mmsi, 
                aisData.Latitude, 
                aisData.Longitude,
                aisData.SpeedOverGround ?? 102.3,   // Use max valid value when not available
                aisData.CourseOverGround ?? 360.0,  // 360 = not available  
                (int)(aisData.Heading ?? 511),      // 511 = not available
                0, // Navigation status - default to "under way using engine"
                60, // Timestamp - default to "not available"
                0, // Rate of turn - default
                false, // Position accuracy
                false // RAIM flag
            );

            // Create NMEA 0183 AIVDM sentence
            // Format: !AIVDM,m,n,s,c,payload,p*hh
            var sentence = $"!AIVDM,1,1,,A,{aisPayload},0";
            var checksum = CalculateNmeaChecksum(sentence);
            var nmeaMessage = $"{sentence}*{checksum:X2}\r\n";

            if (_debugMode)
            {
                Console.WriteLine($"🔍 NMEA Message for MMSI {aisData.Mmsi}: {nmeaMessage.Trim()}");
            }
            
            return nmeaMessage;
        }

        public string? ConvertAisStatic(AisData aisData)
        {
            if (aisData.Mmsi == 0)
                return null;

            // For Type 24, we need to send both Part A (vessel name) and Part B (ship type, call sign, dimensions)
            // OpenCPN requires Part B to display vessel type information
            
            var vesselName = aisData.VesselName ?? "";
            var callSign = aisData.CallSign ?? "";
            var shipType = aisData.VesselType ?? 0;
            
            // Type 24 Part A for vessel name
            var aisPayloadPartA = EncodeAisType24PartA(24, aisData.Mmsi, vesselName);
            
            // Type 24 Part B for ship type and other static data
            var aisPayloadPartB = EncodeAisType24PartB(24, aisData.Mmsi, shipType, callSign);

            // Create NMEA sentences for both Part A and Part B
            var sentenceA = $"!AIVDM,1,1,,A,{aisPayloadPartA},0";
            var checksumA = CalculateNmeaChecksum(sentenceA);
            var nmeaPartA = $"{sentenceA}*{checksumA:X2}\r\n";
            
            var sentenceB = $"!AIVDM,1,1,,B,{aisPayloadPartB},0";
            var checksumB = CalculateNmeaChecksum(sentenceB);
            var nmeaPartB = $"{sentenceB}*{checksumB:X2}\r\n";

            // Return both messages concatenated
            return nmeaPartA + nmeaPartB;
        }

        private string EncodeAisPositionBinary(int msgType, int mmsi, double lat, double lon,
            double sog, double cog, int heading, int navStatus, int timestamp,
            int rot, bool accuracy, bool raim)
        {
            // Convert values to AIS binary format
            // Latitude: 1/10000 minute resolution, signed 27-bit field
            // Range: ±90 degrees = ±54000000 units (90 * 60 * 10000)
            var latRaw = (int)Math.Round(lat * 600000); // Convert degrees to 1/10000 minutes
            if (latRaw > 54000000) latRaw = 54000000;     // North limit
            if (latRaw < -54000000) latRaw = -54000000;   // South limit
            if (latRaw < 0)
                latRaw = (1 << 27) + latRaw; // Two's complement for 27 bits
            latRaw = latRaw & ((1 << 27) - 1);

            // Longitude: 1/10000 minute resolution, signed 28-bit field  
            // Range: ±180 degrees = ±108000000 units (180 * 60 * 10000)
            var lonRaw = (int)Math.Round(lon * 600000); // Convert degrees to 1/10000 minutes
            if (lonRaw > 108000000) lonRaw = 108000000;   // East limit
            if (lonRaw < -108000000) lonRaw = -108000000; // West limit
            if (lonRaw < 0)
                lonRaw = (1 << 28) + lonRaw; // Two's complement for 28 bits
            lonRaw = lonRaw & ((1 << 28) - 1);

            // Debug output for coordinate conversion
            if (_debugMode)
            {
                var latOriginalRaw = (int)Math.Round(lat * 600000);
                var lonOriginalRaw = (int)Math.Round(lon * 600000);
                Console.WriteLine($"🔍 COORD DEBUG: Original Lat {lat:F6} -> {latOriginalRaw}, Lon {lon:F6} -> {lonOriginalRaw}");
                Console.WriteLine($"🔍 COORD DEBUG: After limits/encoding Lat -> {latRaw}, Lon -> {lonRaw}");
            }
            
            // Test decoding to verify round-trip accuracy
            var testLatRaw = latRaw;
            var testLonRaw = lonRaw;
            if ((testLatRaw & (1 << 26)) != 0) // Check sign bit for 27-bit field
                testLatRaw = testLatRaw - (1 << 27);
            if ((testLonRaw & (1 << 27)) != 0) // Check sign bit for 28-bit field  
                testLonRaw = testLonRaw - (1 << 28);
            
            var decodedLat = testLatRaw / 600000.0;
            var decodedLon = testLonRaw / 600000.0;
            
            if (_debugMode)
            {
                Console.WriteLine($"🔍 COORD DEBUG: Decoded back to Lat {decodedLat:F6}, Lon {decodedLon:F6}");
                Console.WriteLine($"🔍 COORD DEBUG: Difference Lat {Math.Abs(lat - decodedLat):F8}, Lon {Math.Abs(lon - decodedLon):F8}");
            }

            // Speed Over Ground: 0.1 knot resolution, 10-bit field (0-102.3 knots, 1023 = not available)
            var sogRaw = (sog >= 1023) ? 1023 : Math.Min(1023, (int)Math.Round(sog * 10));
            
            // Course Over Ground: 0.1 degree resolution, 12-bit field (0-359.9 degrees, 3600 = not available)
            var cogRaw = (cog >= 360 || double.IsNaN(cog)) ? 3600 : (int)Math.Round(cog * 10);
            
            // True Heading: 1 degree resolution, 9-bit field (0-359 degrees, 511 = not available)
            var headingRaw = (heading >= 360 || heading < 0) ? 511 : heading;
            
            // Rate of Turn: encoded value, 8-bit field (-128 to +127, with special encoding)
            var rotRaw = Math.Max(-128, Math.Min(127, rot)) & 0xFF;

            // Choose encoding based on message type
            if (msgType == 18 || msgType == 19)
            {
                return EncodeAisType18Binary(msgType, mmsi, latRaw, lonRaw, sogRaw, cogRaw, headingRaw, timestamp, accuracy, raim);
            }
            else
            {
                return EncodeAisType1Binary(msgType, mmsi, latRaw, lonRaw, sogRaw, cogRaw, headingRaw, navStatus, rotRaw, timestamp, accuracy, raim);
            }
        }

        private string EncodeAisType1Binary(int msgType, int mmsi, int latRaw, int lonRaw, int sogRaw, int cogRaw, int headingRaw, int navStatus, int rotRaw, int timestamp, bool accuracy, bool raim)
        {
            // Build 168-bit binary message for Type 1/2/3 (Class A Position Report)
            var bits = new char[168];
            for (int i = 0; i < 168; i++)
                bits[i] = '0';

            // Message Type (6 bits, 0-5)
            SetBits(bits, 0, 6, msgType);
            // Repeat Indicator (2 bits, 6-7)
            SetBits(bits, 6, 2, 0);
            // MMSI (30 bits, 8-37)
            SetBits(bits, 8, 30, mmsi);
            // Navigation Status (4 bits, 38-41)
            SetBits(bits, 38, 4, navStatus);
            // Rate of Turn (8 bits, 42-49)
            SetBits(bits, 42, 8, rotRaw);
            // Speed Over Ground (10 bits, 50-59)
            SetBits(bits, 50, 10, sogRaw);
            // Position Accuracy (1 bit, 60)
            SetBits(bits, 60, 1, accuracy ? 1 : 0);
            // Longitude (28 bits, 61-88)
            SetBits(bits, 61, 28, lonRaw);
            // Latitude (27 bits, 89-115)
            SetBits(bits, 89, 27, latRaw);
            // Course Over Ground (12 bits, 116-127)
            SetBits(bits, 116, 12, cogRaw);
            // True Heading (9 bits, 128-136)
            SetBits(bits, 128, 9, headingRaw);
            // Timestamp (6 bits, 137-142)
            SetBits(bits, 137, 6, Math.Min(63, timestamp));
            // RAIM flag (1 bit, 148)
            SetBits(bits, 148, 1, raim ? 1 : 0);

            // Convert 168 bits to 6-bit ASCII (28 characters)
            var binaryString = new string(bits);
            return BinaryTo6BitAscii(binaryString);
        }

        private string EncodeAisType18Binary(int msgType, int mmsi, int latRaw, int lonRaw, int sogRaw, int cogRaw, int headingRaw, int timestamp, bool accuracy, bool raim)
        {
            // Build 168-bit binary message for Type 18 (Class B Position Report)
            var bits = new char[168];
            for (int i = 0; i < 168; i++)
                bits[i] = '0';

            // Message Type (6 bits, 0-5)
            SetBits(bits, 0, 6, msgType);
            // Repeat Indicator (2 bits, 6-7)  
            SetBits(bits, 6, 2, 0);
            // MMSI (30 bits, 8-37)
            SetBits(bits, 8, 30, mmsi);
            // Reserved (8 bits, 38-45) - set to 0
            SetBits(bits, 38, 8, 0);
            // Speed Over Ground (10 bits, 46-55)
            SetBits(bits, 46, 10, sogRaw);
            // Position Accuracy (1 bit, 56)
            SetBits(bits, 56, 1, accuracy ? 1 : 0);
            // Longitude (28 bits, 57-84)
            SetBits(bits, 57, 28, lonRaw);
            // Latitude (27 bits, 85-111)
            SetBits(bits, 85, 27, latRaw);
            // Course Over Ground (12 bits, 112-123)
            SetBits(bits, 112, 12, cogRaw);
            // True Heading (9 bits, 124-132)
            SetBits(bits, 124, 9, headingRaw);
            // Timestamp (6 bits, 133-138)
            SetBits(bits, 133, 6, Math.Min(63, timestamp));
            // Regional Reserved (2 bits, 139-140)
            SetBits(bits, 139, 2, 0);
            // Class B Unit Flag (1 bit, 141)
            SetBits(bits, 141, 1, 1); // 1 = Class B "SO" unit
            // Class B Display Flag (1 bit, 142)
            SetBits(bits, 142, 1, 0); // 0 = No display available
            // Class B DSC Flag (1 bit, 143)
            SetBits(bits, 143, 1, 1); // 1 = DSC equipment available
            // Class B Band Flag (1 bit, 144)
            SetBits(bits, 144, 1, 1); // 1 = Can operate in 75kHz band
            // Class B Message 22 Flag (1 bit, 145)
            SetBits(bits, 145, 1, 1); // 1 = Can accept message 22 channel assignment
            // Assigned Mode Flag (1 bit, 146)
            SetBits(bits, 146, 1, 0); // 0 = Autonomous mode
            // RAIM Flag (1 bit, 147)
            SetBits(bits, 147, 1, raim ? 1 : 0);
            // Communication State Selector Flag (1 bit, 148)
            SetBits(bits, 148, 1, 1); // 1 = ITDMA communication state follows
            // Communication State (19 bits, 149-167)
            SetBits(bits, 149, 19, 0); // Default ITDMA state

            // Convert 168 bits to 6-bit ASCII (28 characters)
            var binaryString = new string(bits);
            return BinaryTo6BitAscii(binaryString);
        }

        private string EncodeAisType24PartA(int msgType, int mmsi, string vesselName)
        {
            var bits = new char[168];
            for (int i = 0; i < 168; i++)
                bits[i] = '0';

            // Message Type (6 bits, 0-5)
            SetBits(bits, 0, 6, msgType);
            // Repeat Indicator (2 bits, 6-7)
            SetBits(bits, 6, 2, 0);
            // MMSI (30 bits, 8-37)
            SetBits(bits, 8, 30, mmsi);
            // Part Number (2 bits, 38-39) - Part A = 0
            SetBits(bits, 38, 2, 0);

            // Vessel Name (120 bits, 40-159) - 20 characters, 6 bits each
            var vesselNamePadded = vesselName.PadRight(20).Substring(0, 20);
            for (int i = 0; i < 20; i++)
            {
                var charVal = CharTo6Bit(vesselNamePadded[i]);
                SetBits(bits, 40 + i * 6, 6, charVal);
            }

            // Spare (8 bits, 160-167) - set to 0 for Part A
            SetBits(bits, 160, 8, 0);

            var binaryString = new string(bits);
            return BinaryTo6BitAscii(binaryString);
        }

        private string EncodeAisType24PartB(int msgType, int mmsi, int shipType, string callSign)
        {
            var bits = new char[168];
            for (int i = 0; i < 168; i++)
                bits[i] = '0';

            // Message Type (6 bits, 0-5)
            SetBits(bits, 0, 6, msgType);
            // Repeat Indicator (2 bits, 6-7)
            SetBits(bits, 6, 2, 0);
            // MMSI (30 bits, 8-37)
            SetBits(bits, 8, 30, mmsi);
            // Part Number (2 bits, 38-39) - Part B = 1
            SetBits(bits, 38, 2, 1);

            // Ship and Cargo Type (8 bits, 40-47)
            SetBits(bits, 40, 8, shipType);
            
            // Vendor ID (42 bits, 48-89) - typically 7 characters, 6 bits each
            // Using "GENERIC" as default vendor ID
            var vendorId = "GENERIC".PadRight(7).Substring(0, 7);
            for (int i = 0; i < 7; i++)
            {
                var charVal = CharTo6Bit(vendorId[i]);
                SetBits(bits, 48 + i * 6, 6, charVal);
            }

            // Call Sign (42 bits, 90-131) - 7 characters, 6 bits each
            var callSignPadded = callSign.PadRight(7).Substring(0, 7);
            for (int i = 0; i < 7; i++)
            {
                var charVal = CharTo6Bit(callSignPadded[i]);
                SetBits(bits, 90 + i * 6, 6, charVal);
            }

            // Dimension to Bow (9 bits, 132-140)
            SetBits(bits, 132, 9, 0); // Default to 0 (unknown)
            // Dimension to Stern (9 bits, 141-149)
            SetBits(bits, 141, 9, 0); // Default to 0 (unknown)
            // Dimension to Port (6 bits, 150-155)
            SetBits(bits, 150, 6, 0); // Default to 0 (unknown)
            // Dimension to Starboard (6 bits, 156-161)
            SetBits(bits, 156, 6, 0); // Default to 0 (unknown)
            // Type of Electronic Position Fixing Device (4 bits, 162-165)
            SetBits(bits, 162, 4, 1); // 1 = GPS
            // Spare (2 bits, 166-167)
            SetBits(bits, 166, 2, 0);

            var binaryString = new string(bits);
            return BinaryTo6BitAscii(binaryString);
        }

        private void SetBits(char[] bits, int start, int length, int value)
        {
            var binaryStr = Convert.ToString(value, 2).PadLeft(length, '0');
            for (int i = 0; i < length && i < binaryStr.Length; i++)
            {
                if (start + i < bits.Length)
                    bits[start + i] = binaryStr[i];
            }
        }

        private int CharTo6Bit(char c)
        {
            if (c == ' ') return 32; // Space
            if (c == '@') return 0;  // Not available
            if (c >= 'A' && c <= 'Z') return c - 'A' + 1;
            if (c >= '0' && c <= '9') return c - '0' + 16;
            return 0; // Default to not available
        }

        private string BinaryTo6BitAscii(string binaryString)
        {
            var result = new StringBuilder();

            // Process 6 bits at a time
            for (int i = 0; i < binaryString.Length; i += 6)
            {
                var sixBits = binaryString.Substring(i, Math.Min(6, binaryString.Length - i));
                if (sixBits.Length < 6)
                    sixBits = sixBits.PadRight(6, '0');

                var value = Convert.ToInt32(sixBits, 2);

                // Convert to AIS 6-bit ASCII
                char aisChar;
                if (value < 40)
                    aisChar = (char)(value + 48); // '0' to 'W' (ASCII 48-87)
                else
                    aisChar = (char)(value + 56); // '`' to 'w' (ASCII 96-119)

                result.Append(aisChar);
            }

            return result.ToString();
        }

        private int CalculateNmeaChecksum(string sentence)
        {
            var checksum = 0;
            // Skip the initial '!' and calculate XOR of all characters
            for (int i = 1; i < sentence.Length; i++)
            {
                checksum ^= sentence[i];
            }
            return checksum;
        }
    }
}
