using System.Security.Cryptography;
using System.Text;

namespace Currere_backend.Services
{
    /// <summary>
    /// AES-256 şifreleme servisi — Randomized IV (v2).
    /// Her şifreleme çağrısında rastgele IV üretilir ve ciphertext'in başına eklenir.
    /// Eski statik IV ile şifrelenmiş verileri de geriye uyumlu olarak çözebilir.
    /// </summary>
    public class EncryptionService : IEncryptionService
    {
        private readonly byte[] _key;

        // Eski (statik) IV — sadece geriye uyumluluk (fallback) için tutulur
        private readonly byte[] _legacyIv;

        public EncryptionService(IConfiguration config)
        {
            // appsettings.json'dan 32 karakterlik bir gizli anahtar
            var secretKey = config["Encryption:SecretKey"]
                ?? throw new Exception("Encryption SecretKey bulunamadı!");

            // AES için 32 byte Key
            using var sha256 = SHA256.Create();
            _key = sha256.ComputeHash(Encoding.UTF8.GetBytes(secretKey));

            // Eski statik IV (fallback için)
            using var md5 = MD5.Create();
            _legacyIv = md5.ComputeHash(Encoding.UTF8.GetBytes(secretKey));
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV(); // Her çağrıda rastgele IV üret

            var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();

            // IV'yi (16 byte) ciphertext'in başına yaz
            ms.Write(aes.IV, 0, aes.IV.Length);

            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }

            return Convert.ToBase64String(ms.ToArray());
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            var fullCipher = Convert.FromBase64String(cipherText);

            // Yeni format: ilk 16 byte IV + geri kalanı şifreli metin
            // Eski format: tamamı şifreli metin (statik IV ile)
            // Karar: Eğer 16 byte'dan uzunsa önce yeni formatı dene, başarısız olursa eski formatı dene
            if (fullCipher.Length > 16)
            {
                try
                {
                    return DecryptWithRandomizedIv(fullCipher);
                }
                catch (CryptographicException)
                {
                    // Yeni format başarısız — eski statik IV ile dene (fallback)
                    return DecryptWithLegacyStaticIv(fullCipher);
                }
            }

            // 16 byte veya daha kısa veri — eski format
            return DecryptWithLegacyStaticIv(fullCipher);
        }

        /// <summary>
        /// Yeni format: İlk 16 byte IV, geri kalanı ciphertext
        /// </summary>
        private string DecryptWithRandomizedIv(byte[] fullCipher)
        {
            var iv = fullCipher[..16];
            var cipher = fullCipher[16..];

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = iv;

            var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(cipher);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }

        /// <summary>
        /// Eski format (fallback): Statik IV ile şifrelenmiş veriyi çöz
        /// </summary>
        private string DecryptWithLegacyStaticIv(byte[] fullCipher)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _legacyIv;

            var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(fullCipher);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }
    }
}