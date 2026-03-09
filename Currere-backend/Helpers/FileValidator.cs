namespace Currere_backend.Helpers
{
    public static class FileValidator
    {
        // mb siniri
        private const int MaxFileSize = 50 * 1024 * 1024;

        // whitelist
        private static readonly string[] AllowedExtensions = { ".csv", ".xlsx", ".json", ".txt" };

        // MIME Tipi Kontrolu
        private static readonly string[] AllowedMimeTypes = {
            "text/csv",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", // Excel
            "application/json",
            "text/plain"
        };

        public static (bool IsValid, string ErrorMessage) ValidateFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return (false, "Dosya boş veya seçilmedi.");

            if (file.Length > MaxFileSize)
                return (false, $"Dosya boyutu çok büyük. Maksimum {MaxFileSize / (1024 * 1024)} MB yükleyebilirsiniz.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
                return (false, "Geçersiz dosya uzantısı. Sadece .csv, .xlsx, .json ve .txt desteklenir.");

            if (!AllowedMimeTypes.Contains(file.ContentType))
                return (false, "Dosya içeriği (MIME Type) güvenilir değil. Dosya uzantısı değiştirilmiş olabilir.");

            return (true, string.Empty);
        }
    }
}