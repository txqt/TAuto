using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TAuto.Core;

/// <summary>
/// Handles encryption and decryption of bot bundles using AES-256-CBC.
/// Shared between Developer App (for export) and VibeBot (for execution).
/// </summary>
public static class BundleCryptoService
{
    private static byte[] DefaultKey = Encoding.UTF8.GetBytes("VibeBot_AES256_K");
    
    /// <summary>
    /// Decrypt an encrypted bundle.
    /// Format: [VBOT header (4 bytes)][IV (16 bytes)][ciphertext]
    /// </summary>
    public static string Decrypt(byte[] encryptedData, byte[]? key = null)
    {
        if (encryptedData.Length < 20)
        {
            return Encoding.UTF8.GetString(encryptedData);
        }
        
        // Detect VBOT header
        var header = Encoding.ASCII.GetString(encryptedData, 0, 4);
        if (header != "VBOT")
        {
            return Encoding.UTF8.GetString(encryptedData);
        }
        
        // Extract IV and ciphertext
        byte[] iv = new byte[16];
        Array.Copy(encryptedData, 4, iv, 0, 16);
        
        byte[] ciphertext = new byte[encryptedData.Length - 20];
        Array.Copy(encryptedData, 20, ciphertext, 0, ciphertext.Length);
        
        byte[] actualKey = key ?? GetDefaultKey();
        
        using var aes = Aes.Create();
        aes.Key = actualKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        
        using var decryptor = aes.CreateDecryptor();
        byte[] decrypted = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        
        return Encoding.UTF8.GetString(decrypted);
    }
    
    /// <summary>
    /// Encrypt a script JSON to a bundle format.
    /// </summary>
    public static byte[] Encrypt(string json, byte[]? key = null)
    {
        byte[] actualKey = key ?? GetDefaultKey();
        byte[] plaintext = Encoding.UTF8.GetBytes(json);
        
        using var aes = Aes.Create();
        aes.Key = actualKey;
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        
        using var encryptor = aes.CreateEncryptor();
        byte[] ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
        
        // Format: [VBOT header (4 bytes)][IV (16 bytes)][ciphertext]
        byte[] result = new byte[4 + 16 + ciphertext.Length];
        Encoding.ASCII.GetBytes("VBOT").CopyTo(result, 0);
        aes.IV.CopyTo(result, 4);
        ciphertext.CopyTo(result, 20);
        
        return result;
    }
    
    private static byte[] GetDefaultKey()
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(DefaultKey);
    }
    
    public static void SetKeyFromString(string keySource)
    {
        using var sha = SHA256.Create();
        DefaultKey = sha.ComputeHash(Encoding.UTF8.GetBytes(keySource));
    }
}
