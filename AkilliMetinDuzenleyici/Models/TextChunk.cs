namespace AkilliMetinDuzenleyici.Models
{
    public class TextChunk
    {
        public int Index { get; set; }
        public string Text { get; set; } = string.Empty;
        public int WordCount { get; set; }
        public string? ProcessedText { get; set; }
        public bool IsSuccess { get; set; } = false;
        public string? ErrorMessage { get; set; }
    }
}
