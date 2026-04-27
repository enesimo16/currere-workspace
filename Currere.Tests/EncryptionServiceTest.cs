using System;
using System.Collections.Generic;
using Xunit;
using Microsoft.Extensions.Configuration;
using Currere_backend.Services;

namespace Currere.Tests
{
    /// <summary>
    /// BÖLÜM 3 — Saldırı 8: AES-256 Randomized IV Kriptografi Testi
    /// 
    /// EncryptionService v2'nin her şifrelemede farklı IV ürettiğini kanıtlar.
    /// Aynı plaintext arka arkaya şifrelendiğinde farklı ciphertext oluşmalıdır.
    /// 
    /// Çalıştırma:
    ///   dotnet test Currere.Tests --verbosity normal
    /// </summary>
    public class EncryptionServiceTest
    {
        private readonly EncryptionService _sut;

        public EncryptionServiceTest()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Encryption:SecretKey", "Currere_Test_Key_2026_RedTeam_Fortress" }
                })
                .Build();

            _sut = new EncryptionService(config);
        }

        [Fact]
        public void Encrypt_SamePlaintext_ProducesDifferentCiphertext()
        {
            // Arrange
            const string plaintext = "test_secret";

            // Act — Aynı metni iki kez şifrele
            var cipher1 = _sut.Encrypt(plaintext);
            var cipher2 = _sut.Encrypt(plaintext);

            // Assert — Randomized IV sayesinde iki sonuç FARKLI olmalı
            Assert.NotEqual(cipher1, cipher2);
        }

        [Fact]
        public void Decrypt_ReturnsOriginalPlaintext()
        {
            const string plaintext = "hf_SuperSecretHuggingFaceToken_12345";

            var encrypted = _sut.Encrypt(plaintext);
            var decrypted = _sut.Decrypt(encrypted);

            Assert.Equal(plaintext, decrypted);
        }

        [Fact]
        public void Decrypt_MultipleEncryptions_AllDecryptCorrectly()
        {
            const string plaintext = "api_key_for_groq_2026";

            var ciphertexts = new HashSet<string>();
            for (int i = 0; i < 10; i++)
            {
                var cipher = _sut.Encrypt(plaintext);
                ciphertexts.Add(cipher);
                Assert.Equal(plaintext, _sut.Decrypt(cipher));
            }

            // 10 şifrelemenin hepsi benzersiz olmalı
            Assert.Equal(10, ciphertexts.Count);
        }

        [Fact]
        public void Encrypt_EmptyString_ReturnsEmpty()
        {
            Assert.Equal("", _sut.Encrypt(""));
        }

        [Fact]
        public void Encrypt_CiphertextContainsIVPrefix()
        {
            var cipher = _sut.Encrypt("iv_prefix_test");
            var bytes = Convert.FromBase64String(cipher);

            // Şifreli metin en az 16 byte (IV) + 16 byte (AES blok) olmalı
            Assert.True(bytes.Length > 16,
                $"Ciphertext çok kısa ({bytes.Length} byte) — IV prefix eksik olabilir!");
        }
    }
}
