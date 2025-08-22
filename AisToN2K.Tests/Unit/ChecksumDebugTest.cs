using System;
using Xunit;
using AisToN2K.Tests.Utilities;

namespace AisToN2K.Tests.Unit
{
    public class ChecksumDebugTest
    {
        [Fact]
        [Trait("Category", "Debug")]
        public void DebugOpenCpnChecksums()
        {
            var messages = new[]
            {
                "!AIVDM,1,1,,A,15Muq70001G?tRrM5M4P8?v4080u,0*28",
                "!AIVDM,1,1,,B,B5Muq70001G?tRrM5M4P8?v4080u,0*1F"
            };

            foreach (var message in messages)
            {
                // Calculate checksum manually
                var checksumIndex = message.LastIndexOf('*');
                var dataToCheck = message.Substring(1, checksumIndex - 1);
                byte checksum = 0;
                
                foreach (char c in dataToCheck)
                {
                    checksum ^= (byte)c;
                }
                
                var expectedChecksum = message.Substring(checksumIndex + 1);
                var calculatedChecksum = checksum.ToString("X2");
                
                Console.WriteLine($"Message: {message}");
                Console.WriteLine($"Data: {dataToCheck}");
                Console.WriteLine($"Calculated: {calculatedChecksum}, Expected: {expectedChecksum}");
                Console.WriteLine($"Match: {calculatedChecksum.Equals(expectedChecksum, StringComparison.OrdinalIgnoreCase)}");
                
                // Test with validator
                var result = NmeaValidator.ValidateAisSentence(message);
                Console.WriteLine($"Validator result: {result.IsValid}");
                if (!result.IsValid)
                {
                    Console.WriteLine($"Errors: {string.Join(", ", result.Errors)}");
                }
                Console.WriteLine("---");
            }
        }
    }
}
