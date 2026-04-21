namespace Currere_backend.DTOs
{
    public class GenerateSyntheticDataDto
    {
        public string Prompt { get; set; } = string.Empty;
        public int RowCount { get; set; } = 50;
    }
}
