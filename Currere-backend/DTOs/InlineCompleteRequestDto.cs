namespace Currere_backend.DTOs
{
    public class InlineCompleteRequestDto
    {
        public string Code { get; set; } = string.Empty;
        public int CursorLine { get; set; }
        public int CursorCol { get; set; }
    }
}
