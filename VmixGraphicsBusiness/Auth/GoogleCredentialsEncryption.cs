using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Pubg_Ranking_System
{
    public static class GoogleCredentialsEncryption
    {
        private static readonly byte[] Key = Convert.FromHexString("a7f3c9e2b5d8f1a4c6e9b2d5f8a1c4e7b9d2f5a8c1e4b7d0f3a6c9e2b5d8f1a4"); // 32 bytes
        private static readonly byte[] IV = Convert.FromHexString("3f8a2c5e7b9d1f4a6c8e0b2d4f6a8c0e"); // 16 bytes
        /// <summary>
        /// Encrypts JSON string to Base64
        /// </summary>
        public static string Encrypt(string plainText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Key;
                aes.IV = IV;

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var writer = new StreamWriter(cs))
                    {
                        writer.Write(plainText);
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        /// <summary>
        /// Decrypts Base64 string back to JSON
        /// </summary>
        public static string Decrypt(string encryptedText)
        {
            byte[] buffer = Convert.FromBase64String(encryptedText);

            using (Aes aes = Aes.Create())
            {
                aes.Key = Key;
                aes.IV = IV;

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream(buffer))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var reader = new StreamReader(cs))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Helper: Encrypts a JSON file
        /// </summary>
        public static string EncryptFile(string jsonFilePath)
        {
            string jsonContent = File.ReadAllText(jsonFilePath);
            return Encrypt(jsonContent);
        }
    }
}