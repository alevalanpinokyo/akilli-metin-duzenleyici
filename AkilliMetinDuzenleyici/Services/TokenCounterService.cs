using System;
using System.Text.RegularExpressions;

namespace AkilliMetinDuzenleyici.Services
{
    public class TokenCounterService : ITokenCounterService
    {
        private static readonly Regex WordSplitRegex = new Regex(@"\s+", RegexOptions.Compiled);

        public int CountWords(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            string trimmed = text.Trim();
            string[] words = WordSplitRegex.Split(trimmed);
            return words.Length;
        }

        public int EstimateTokens(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            int wordCount = CountWords(text);
            int charCount = text.Length;

            // Turkish token estimation factor ~ 1.35 tokens per word + punctuation allowance
            int estimated = (int)Math.Ceiling(wordCount * 1.35) + (charCount / 20);
            return Math.Max(estimated, 1);
        }
    }
}
