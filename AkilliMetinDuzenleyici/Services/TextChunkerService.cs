using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using AkilliMetinDuzenleyici.Models;

namespace AkilliMetinDuzenleyici.Services
{
    public class TextChunkerService : ITextChunkerService
    {
        private readonly ITokenCounterService _tokenCounterService;
        private static readonly Regex SentenceBoundaryRegex = new Regex(@"(?<=[.!?])\s+", RegexOptions.Compiled);

        public TextChunkerService(ITokenCounterService tokenCounterService)
        {
            _tokenCounterService = tokenCounterService;
        }

        public List<TextChunk> ChunkText(string text, int maxWordsPerChunk = 2000)
        {
            var chunks = new List<TextChunk>();

            if (string.IsNullOrWhiteSpace(text))
                return chunks;

            int totalWords = _tokenCounterService.CountWords(text);
            if (totalWords <= maxWordsPerChunk)
            {
                chunks.Add(new TextChunk
                {
                    Index = 1,
                    Text = text,
                    WordCount = totalWords
                });
                return chunks;
            }

            // Split into paragraphs preserving paragraph structure
            string[] rawParagraphs = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.None);
            var paragraphUnits = new List<string>();

            foreach (var para in rawParagraphs)
            {
                int paraWords = _tokenCounterService.CountWords(para);
                if (paraWords > maxWordsPerChunk)
                {
                    // Paragraph is too large: split into sentence blocks
                    string[] sentences = SentenceBoundaryRegex.Split(para);
                    var sentenceBuffer = new StringBuilder();
                    int bufferWords = 0;

                    foreach (var sentence in sentences)
                    {
                        int sentenceWords = _tokenCounterService.CountWords(sentence);
                        if (bufferWords + sentenceWords > maxWordsPerChunk && sentenceBuffer.Length > 0)
                        {
                            paragraphUnits.Add(sentenceBuffer.ToString().Trim());
                            sentenceBuffer.Clear();
                            bufferWords = 0;
                        }

                        if (sentenceBuffer.Length > 0)
                            sentenceBuffer.Append(" ");

                        sentenceBuffer.Append(sentence);
                        bufferWords += sentenceWords;
                    }

                    if (sentenceBuffer.Length > 0)
                    {
                        paragraphUnits.Add(sentenceBuffer.ToString().Trim());
                    }
                }
                else
                {
                    paragraphUnits.Add(para);
                }
            }

            // Group paragraph units into chunks up to maxWordsPerChunk
            var currentChunkBuilder = new StringBuilder();
            int currentChunkWords = 0;
            int chunkIndex = 1;

            for (int i = 0; i < paragraphUnits.Count; i++)
            {
                string unit = paragraphUnits[i];
                int unitWords = _tokenCounterService.CountWords(unit);

                if (currentChunkWords + unitWords > maxWordsPerChunk && currentChunkBuilder.Length > 0)
                {
                    chunks.Add(new TextChunk
                    {
                        Index = chunkIndex++,
                        Text = currentChunkBuilder.ToString().TrimEnd(),
                        WordCount = currentChunkWords
                    });

                    currentChunkBuilder.Clear();
                    currentChunkWords = 0;
                }

                if (currentChunkBuilder.Length > 0)
                {
                    currentChunkBuilder.Append("\n\n");
                }

                currentChunkBuilder.Append(unit);
                currentChunkWords += unitWords;
            }

            if (currentChunkBuilder.Length > 0)
            {
                chunks.Add(new TextChunk
                {
                    Index = chunkIndex,
                    Text = currentChunkBuilder.ToString().TrimEnd(),
                    WordCount = currentChunkWords
                });
            }

            return chunks;
        }

        public string RecombineChunks(List<TextChunk> chunks)
        {
            if (chunks == null || chunks.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            for (int i = 0; i < chunks.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append("\n\n");
                }
                sb.Append(chunks[i].ProcessedText ?? chunks[i].Text);
            }
            return sb.ToString();
        }
    }
}
