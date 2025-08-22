using System;

class ChecksumCalculator
{
    static void Main()
    {
        var messages = new[]
        {
            "!AIVDM,1,1,,A,15Muq70001G?tRrM5M4P8?v4080u,0*28",
            "!AIVDM,1,1,,B,B5Muq70001G?tRrM5M4P8?v4080u,0*1E"
        };

        foreach (var message in messages)
        {
            var checksumIndex = message.LastIndexOf('*');
            var dataToCheck = message.Substring(1, checksumIndex - 1);
            byte checksum = 0;
            
            foreach (char c in dataToCheck)
            {
                checksum ^= (byte)c;
            }
            
            var expected = message.Substring(checksumIndex + 1);
            var calculated = checksum.ToString("X2");
            
            Console.WriteLine($"Message: {message}");
            Console.WriteLine($"Data: {dataToCheck}");
            Console.WriteLine($"Calculated: {calculated}, Expected: {expected}, Match: {calculated.Equals(expected, StringComparison.OrdinalIgnoreCase)}");
            Console.WriteLine();
        }
    }
}
