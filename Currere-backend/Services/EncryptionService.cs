using System.Security.Cryptography;
using System.Text;

namespace Currere_backend.Services
{
    public class EncryptionService : IEncryptionService
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;

        public EncryptionService(IConfiguration config)
        {
            // appsettings.json'dan 32 karakterlik bir gizli anahtar
            var secretKey = config["Encryption:SecretKey"]
                ?? throw new Exception("Encryption SecretKey bulunamadı!");

            // AES için 32 byte Key ve 16 byte IV 
            using var sha256 = SHA256.Create();
            _key = sha256.ComputeHash(Encoding.UTF8.GetBytes(secretKey));

            using var md5 = MD5.Create();
            _iv = md5.ComputeHash(Encoding.UTF8.GetBytes(secretKey));
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            using var aesAlg = Aes.Create();
            aesAlg.Key = _key;
            aesAlg.IV = _iv;

            var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
            using var msEncrypt = new MemoryStream();
            using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
            using (var swEncrypt = new StreamWriter(csEncrypt))
            {
                swEncrypt.Write(plainText);
            }
            return Convert.ToBase64String(msEncrypt.ToArray());
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            using var aesAlg = Aes.Create();
            aesAlg.Key = _key;
            aesAlg.IV = _iv;

            var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
            using var msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText));
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);
            return srDecrypt.ReadToEnd();
        }
    }
}