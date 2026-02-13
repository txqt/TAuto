using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TAuto.Core;

/// <summary>
/// Handles encryption and decryption of bot bundles using AES-256-CBC.
/// Supports both legacy default key and license-derived PBKDF2 keys.
/// 
/// Bundle format: [VBOT header (4 bytes)][IV (16 bytes)][ciphertext]
/// </summary>
public static class BundleCryptoService
{
    private const int Pbkdf2Iterations = 100_000;
    private const int KeySizeBytes = 32; // AES-256
    
    // ========================================
    // PBKDF2 License-Based Key Derivation
    // ========================================
    
    /// <summary>
    /// Derive a 256-bit AES key from a license key + server salt using PBKDF2.
    /// Both client and server use this to encrypt/decrypt bot packages.
    /// </summary>
    public static byte[] DeriveKeyFromLicense(string licenseKey, string salt)
    {
        var keyBytes = Encoding.UTF8.GetBytes(licenseKey);
        var saltBytes = Encoding.UTF8.GetBytes(salt);
        
        using var pbkdf2 = new Rfc2898DeriveBytes(keyBytes, saltBytes, Pbkdf2Iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeySizeBytes);
    }
    
    // ========================================
    // Encrypt / Decrypt
    // ========================================
    
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
        
        byte[] actualKey = key ?? throw new InvalidOperationException(
            "No encryption key provided. Use DeriveKeyFromLicense() to generate a key.");
        
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
    public static byte[] Encrypt(string json, byte[] key)
    {
        byte[] plaintext = Encoding.UTF8.GetBytes(json);
        
        using var aes = Aes.Create();
        aes.Key = key;
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
}
