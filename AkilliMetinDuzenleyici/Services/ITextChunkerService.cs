using System.Collections.Generic;
using AkilliMetinDuzenleyici.Models;

namespace AkilliMetinDuzenleyici.Services
{
    public interface ITextChunkerService
    {
        List<TextChunk> ChunkText(string text, int maxWordsPerChunk = 2000);
        string RecombineChunks(List<TextChunk> chunks);
    }
}
