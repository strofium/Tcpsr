using System.Security.Cryptography;
using System.Text;

namespace StandRiseServer.Utils;

public static class EncryptionHelper
{
    /// <summary>
    /// AES-256-CBC encryption
    /// </summary>
    public static string EncryptAES256(string plainText, string key, string iv)
    {
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(key);
        aes.IV = Encoding.UTF8.GetBytes(iv);
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
        using var sw = new StreamWriter(cs);
        
        sw.Write(plainText);
        sw.Flush();
        cs.FlushFinalBlock();
        
        return Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>
    /// AES-256-CBC decryption
    /// </summary>
    public static string DecryptAES256(string cipherText, string key, string iv)
    {
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(key);
        aes.IV = Encoding.UTF8.GetBytes(iv);
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(Convert.FromBase64String(cipherText));
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        
        return sr.ReadToEnd();
    }

    /// <summary>
    /// Generate random IV for AES
    /// </summary>
    public static byte[] GenerateRandomIV(int size = 16)
    {
        var iv = new byte[size];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(iv);
        return iv;
    }
}
