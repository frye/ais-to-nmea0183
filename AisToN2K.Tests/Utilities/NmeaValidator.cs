using System.Text;

namespace AisToN2K.Tests.Utilities
{
    /// <summary>
    /// Utilities for validating NMEA 0183 message format and checksums.
    /// Implements industry-standard NMEA validation rules for marine navigation systems.
    /// </summary>
    public static class NmeaValidator
    {
        /// <summary>
        /// Validates a complete NMEA 0183 sentence including format, checksum, and character encoding.
        /// </summary>
        /// <param name="sentence">Complete NMEA sentence including start character and checksum</param>
        /// <returns>ValidationResult with success status and detailed error information</returns>
        public static NmeaValidationResult ValidateSentence(string sentence)
        {
            var result = new NmeaValidationResult { IsValid = true };

            if (string.IsNullOrEmpty(sentence))
            {
                result.IsValid = false;
                result.Errors.Add("Sentence is null or empty");
                return result;
            }

            // Check sentence start character
            if (!sentence.StartsWith('$') && !sentence.StartsWith('!'))
            {
                result.IsValid = false;
                result.Errors.Add("Sentence must start with '$' or '!' character");
            }

            // Check sentence length (NMEA standard maximum is 82 characters including CRLF)
            if (sentence.Length > 82)
            {
                result.IsValid = false;
                result.Errors.Add($"Sentence exceeds maximum length of 82 characters (actual: {sentence.Length})");
            }

            // Check for checksum separator
            var checksumIndex = sentence.LastIndexOf('*');
            if (checksumIndex == -1)
            {
                result.IsValid = false;
                result.Errors.Add("Missing checksum separator '*'");
                return result;
            }

            // Validate ASCII character range (0x20 to 0x7E for printable characters)
            var dataSection = sentence.Substring(1, checksumIndex - 1); // Between start char and '*'
            foreach (char c in dataSection)
            {
                if (c < 0x20 || c > 0x7E)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Invalid character in data section: 0x{(int)c:X2} (must be 0x20-0x7E)");
                }
            }

            // Extract and validate checksum
            if (checksumIndex + 3 <= sentence.Length)
            {
                var checksumStr = sentence.Substring(checksumIndex + 1, 2);
                if (!IsValidHexString(checksumStr))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Invalid checksum format: '{checksumStr}' (must be 2-digit hex)");
                }
                else
                {
                    var providedChecksum = Convert.ToByte(checksumStr, 16);
                    var calculatedChecksum = CalculateChecksum(dataSection);
                    
                    if (providedChecksum != calculatedChecksum)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Checksum mismatch: provided {providedChecksum:X2}, calculated {calculatedChecksum:X2}");
                    }
                }
            }
            else
            {
                result.IsValid = false;
                result.Errors.Add("Missing or incomplete checksum");
            }

            return result;
        }

        /// <summary>
        /// Calculates NMEA 0183 checksum using XOR of all characters between start and checksum separator.
        /// </summary>
        /// <param name="data">Data section of NMEA sentence (without start character and checksum)</param>
        /// <returns>Calculated checksum as byte value</returns>
        public static byte CalculateChecksum(string data)
        {
            byte checksum = 0;
            foreach (char c in data)
            {
                checksum ^= (byte)c;
            }
            return checksum;
        }

        /// <summary>
        /// Formats checksum as two-digit uppercase hex string for NMEA sentences.
        /// </summary>
        /// <param name="checksum">Checksum byte value</param>
        /// <returns>Two-digit uppercase hex string</returns>
        public static string FormatChecksum(byte checksum)
        {
            return checksum.ToString("X2");
        }

        /// <summary>
        /// Validates AIS-specific NMEA sentence format (!AIVDM or !AIVDO).
        /// </summary>
        /// <param name="sentence">Complete AIS NMEA sentence</param>
        /// <returns>AIS-specific validation result</returns>
        public static AisNmeaValidationResult ValidateAisSentence(string sentence)
        {
            var result = new AisNmeaValidationResult { IsValid = true };
            var baseResult = ValidateSentence(sentence);
            
            result.IsValid = baseResult.IsValid;
            result.Errors.AddRange(baseResult.Errors);

            if (!result.IsValid)
                return result;

            // Parse AIS sentence fields
            var parts = ParseAisSentence(sentence);
            if (parts == null)
            {
                result.IsValid = false;
                result.Errors.Add("Failed to parse AIS sentence structure");
                return result;
            }

            // Validate AIS sentence identifier
            if (parts.SentenceId != "AIVDM" && parts.SentenceId != "AIVDO")
            {
                result.IsValid = false;
                result.Errors.Add($"Invalid AIS sentence identifier: {parts.SentenceId} (expected AIVDM or AIVDO)");
            }

            // Validate fragment information
            if (parts.FragmentCount < 1 || parts.FragmentCount > 9)
            {
                result.IsValid = false;
                result.Errors.Add($"Invalid fragment count: {parts.FragmentCount} (must be 1-9)");
            }

            if (parts.FragmentNumber < 1 || parts.FragmentNumber > parts.FragmentCount)
            {
                result.IsValid = false;
                result.Errors.Add($"Invalid fragment number: {parts.FragmentNumber} (must be 1-{parts.FragmentCount})");
            }

            // Validate channel
            if (parts.Channel != "A" && parts.Channel != "B" && !string.IsNullOrEmpty(parts.Channel))
            {
                result.IsValid = false;
                result.Errors.Add($"Invalid AIS channel: '{parts.Channel}' (must be 'A' or 'B' or empty)");
            }

            // Validate fill bits
            if (parts.FillBits < 0 || parts.FillBits > 5)
            {
                result.IsValid = false;
                result.Errors.Add($"Invalid fill bits: {parts.FillBits} (must be 0-5)");
            }

            // Validate payload (basic 6-bit ASCII encoding check)
            if (!IsValid6BitAscii(parts.Payload))
            {
                result.IsValid = false;
                result.Errors.Add("Invalid AIS payload encoding (must be 6-bit ASCII)");
            }

            result.ParsedFields = parts;
            return result;
        }

        /// <summary>
        /// Parses AIS NMEA sentence into component fields.
        /// </summary>
        private static AisSentenceFields? ParseAisSentence(string sentence)
        {
            try
            {
                var checksumIndex = sentence.LastIndexOf('*');
                var dataSection = sentence.Substring(1, checksumIndex - 1);
                var fields = dataSection.Split(',');

                if (fields.Length < 6)
                    return null;

                return new AisSentenceFields
                {
                    SentenceId = fields[0],
                    FragmentCount = int.Parse(fields[1]),
                    FragmentNumber = int.Parse(fields[2]),
                    MessageId = fields[3],
                    Channel = fields[4],
                    Payload = fields[5],
                    FillBits = fields.Length > 6 ? int.Parse(fields[6]) : 0
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Validates that a string contains only valid 6-bit ASCII characters for AIS payload.
        /// </summary>
        private static bool IsValid6BitAscii(string payload)
        {
            foreach (char c in payload)
            {
                // Valid 6-bit ASCII range for AIS: 0x30-0x77 (characters '0' through 'w')
                if (c < '0' || c > 'w')
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Checks if a string is a valid 2-character hex string.
        /// </summary>
        private static bool IsValidHexString(string value)
        {
            if (value.Length != 2)
                return false;

            return value.All(c => char.IsDigit(c) || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f'));
        }
    }

    /// <summary>
    /// Result of NMEA sentence validation with detailed error information.
    /// </summary>
    public class NmeaValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public string ErrorSummary => string.Join("; ", Errors);
    }

    /// <summary>
    /// Extended validation result for AIS-specific NMEA sentences.
    /// </summary>
    public class AisNmeaValidationResult : NmeaValidationResult
    {
        public AisSentenceFields? ParsedFields { get; set; }
    }

    /// <summary>
    /// Parsed fields from an AIS NMEA sentence.
    /// </summary>
    public class AisSentenceFields
    {
        public string SentenceId { get; set; } = "";
        public int FragmentCount { get; set; }
        public int FragmentNumber { get; set; }
        public string MessageId { get; set; } = "";
        public string Channel { get; set; } = "";
        public string Payload { get; set; } = "";
        public int FillBits { get; set; }
    }
}
