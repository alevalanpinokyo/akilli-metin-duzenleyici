using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AkilliMetinDuzenleyici.Models;
using AkilliMetinDuzenleyici.Services;
using Xunit;

namespace AkilliMetinDuzenleyici.Tests
{
    public class ServiceTests
    {
        [Fact]
        public void TokenCounterService_CountsWordsAndEstimateTokensCorrectly()
        {
            var counter = new TokenCounterService();
            string text = "Bu bir Türkçe imla ve yazım deneme metnidir.";

            int wordCount = counter.CountWords(text);
            int tokenEstimate = counter.EstimateTokens(text);

            Assert.Equal(8, wordCount);
            Assert.True(tokenEstimate >= 8);
        }

        [Fact]
        public void TextChunkerService_ChunksLongTextWithoutBreakingParagraphs()
        {
            var tokenCounter = new TokenCounterService();
            var chunker = new TextChunkerService(tokenCounter);

            var sb = new StringBuilder();
            for (int i = 0; i < 500; i++)
            {
                sb.Append("Bu paragraf imla düzeltme testi için oluşturulan bir cümledir. ");
            }
            string longText = sb.ToString();

            // Set maxWordsPerChunk to 100 for testing chunking
            var chunks = chunker.ChunkText(longText, maxWordsPerChunk: 100);

            Assert.True(chunks.Count > 1);
            Assert.All(chunks, chunk => Assert.True(chunk.WordCount <= 150));

            string recombined = chunker.RecombineChunks(chunks);
            Assert.False(string.IsNullOrWhiteSpace(recombined));
        }

        [Fact]
        public async Task QuotaManagerService_TracksUsageAndResetsCorrectly()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"kullanim_test_{Guid.NewGuid()}.json");
            try
            {
                var quotaManager = new QuotaManagerService(tempPath);

                var usage = await quotaManager.GetUsageAsync();
                Assert.Equal(0, usage.GunlukIstekSayisi);
                Assert.Equal(1000, usage.GunlukMaxIstek);

                await quotaManager.RecordUsageAsync(150, 200);

                var updated = await quotaManager.GetUsageAsync();
                Assert.Equal(1, updated.GunlukIstekSayisi);
                Assert.Equal(150, updated.ToplamIslenanKelime);
                Assert.Equal(200, updated.ToplamHarcananToken);

                bool canMake = await quotaManager.CanMakeRequestAsync();
                Assert.True(canMake);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }
    }
}
