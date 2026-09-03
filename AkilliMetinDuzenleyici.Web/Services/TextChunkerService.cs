using System;
using System.Collections.Generic;
using System.Text;
using AkilliMetinDuzenleyici.Web.Services;

namespace AkilliMetinDuzenleyici.Web.Models
{
    public class TextChunk
    {
        public int Index { get; set; }
        public string Text { get; set; } = string.Empty;
        public int WordCount { get; set; }
        public string ProcessedText { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
    }
}

namespace AkilliMetinDuzenleyici.Web.Services
{
    public interface ITextChunkerService
    {
        List<AkilliMetinDuzenleyici.Web.Models.TextChunk> ChunkText(string text, int maxWordsPerChunk = 2000);
        string RecombineChunks(List<AkilliMetinDuzenleyici.Web.Models.TextChunk> chunks);
    }

    public class TextChunkerService : ITextChunkerService
    {
        private readonly ITokenCounterService _tokenCounterService;

        public TextChunkerService(ITokenCounterService tokenCounterService)
        {
            _tokenCounterService = tokenCounterService;
        }

        public List<AkilliMetinDuzenleyici.Web.Models.TextChunk> ChunkText(string text, int maxWordsPerChunk = 2000)
        {
            var result = new List<AkilliMetinDuzenleyici.Web.Models.TextChunk>();
            if (string.IsNullOrWhiteSpace(text)) return result;

            int totalWords = _tokenCounterService.CountWords(text);
            if (totalWords <= maxWordsPerChunk)
            {
                result.Add(new AkilliMetinDuzenleyici.Web.Models.TextChunk
                {
                    Index = 0,
                    Text = text,
                    WordCount = totalWords
                });
                return result;
            }

            string[] paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.None);
            var currentChunkText = new StringBuilder();
            int currentChunkWordCount = 0;
            int chunkIndex = 0;

            foreach (var paragraph in paragraphs)
            {
                int pWordCount = _tokenCounterService.CountWords(paragraph);

                if (currentChunkWordCount + pWordCount > maxWordsPerChunk && currentChunkText.Length > 0)
                {
                    result.Add(new AkilliMetinDuzenleyici.Web.Models.TextChunk
                    {
                        Index = chunkIndex++,
                        Text = currentChunkText.ToString().TrimEnd(),
                        WordCount = currentChunkWordCount
                    });
                    currentChunkText.Clear();
                    currentChunkWordCount = 0;
                }

                if (pWordCount > maxWordsPerChunk)
                {
                    string[] sentences = System.Text.RegularExpressions.Regex.Split(paragraph, @"(?<=[.!?])\s+");
                    foreach (var sentence in sentences)
                    {
                        int sWordCount = _tokenCounterService.CountWords(sentence);
                        if (currentChunkWordCount + sWordCount > maxWordsPerChunk && currentChunkText.Length > 0)
                        {
                            result.Add(new AkilliMetinDuzenleyici.Web.Models.TextChunk
                            {
                                Index = chunkIndex++,
                                Text = currentChunkText.ToString().TrimEnd(),
                                WordCount = currentChunkWordCount
                            });
                            currentChunkText.Clear();
                            currentChunkWordCount = 0;
                        }

                        currentChunkText.Append(sentence).Append(' ');
                        currentChunkWordCount += sWordCount;
                    }
                    currentChunkText.AppendLine().AppendLine();
                }
                else
                {
                    currentChunkText.Append(paragraph).Append("\n\n");
                    currentChunkWordCount += pWordCount;
                }
            }

            if (currentChunkText.Length > 0)
            {
                result.Add(new AkilliMetinDuzenleyici.Web.Models.TextChunk
                {
                    Index = chunkIndex,
                    Text = currentChunkText.ToString().TrimEnd(),
                    WordCount = currentChunkWordCount
                });
            }

            return result;
        }

        public string RecombineChunks(List<AkilliMetinDuzenleyici.Web.Models.TextChunk> chunks)
        {
            if (chunks == null || chunks.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            for (int i = 0; i < chunks.Count; i++)
            {
                string text = string.IsNullOrWhiteSpace(chunks[i].ProcessedText)
                    ? chunks[i].Text
                    : chunks[i].ProcessedText;

                sb.Append(text);
                if (i < chunks.Count - 1)
                {
                    sb.Append("\n\n");
                }
            }
            return sb.ToString();
        }
    }
}
