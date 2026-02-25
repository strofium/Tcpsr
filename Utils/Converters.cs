using System.Security.Cryptography;
using System.Text;

namespace StandRiseServer.Utils;

public static class Converters
{
    /// <summary>
    /// Convert GUID string to UInt64 for protobuf messages
    /// </summary>
    public static ulong ConvertGuidToInt(string guid)
    {
        var parsedGuid = Guid.Parse(guid);
        var bytes = parsedGuid.ToByteArray();
        return BitConverter.ToUInt64(bytes, 0);
    }

    /// <summary>
    /// Convert Int32 to byte array (Little Endian)
    /// </summary>
    public static byte[] Int32ToBytes(int value)
    {
        return BitConverter.GetBytes(value);
    }

    /// <summary>
    /// Convert byte array to Int32 (Little Endian)
    /// </summary>
    public static int BytesToInt32(byte[] bytes)
    {
        if (bytes.Length < 4)
            throw new ArgumentException("Byte array must be at least 4 bytes");
        return BitConverter.ToInt32(bytes, 0);
    }

    /// <summary>
    /// Convert byte array to Int32 from Big Endian
    /// </summary>
    public static int BytesToInt32BigEndian(byte[] bytes)
    {
        if (bytes.Length < 4)
            throw new ArgumentException("Byte array must be at least 4 bytes");
        
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        
        return BitConverter.ToInt32(bytes, 0);
    }

    /// <summary>
    /// Calculate MD5 hash of string
    /// </summary>
    public static string CalculateMD5(string input)
    {
        using var md5 = MD5.Create();
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = md5.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes).ToLower();
    }

    /// <summary>
    /// Get random integer between 0 and max (exclusive)
    /// </summary>
    public static int GetRandomInt(int max)
    {
        return Random.Shared.Next(max);
    }

    /// <summary>
    /// Get current Unix timestamp in milliseconds
    /// </summary>
    public static long GetCurrentTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
