namespace AkilliMetinDuzenleyici.Web.Services
{
    public interface ITokenCounterService
    {
        int CountWords(string text);
        int EstimateTokens(string text);
    }

    public class TokenCounterService : ITokenCounterService
    {
        public int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            char[] delimiters = new char[] { ' ', '\r', '\n', '\t' };
            return text.Split(delimiters, System.StringSplitOptions.RemoveEmptyEntries).Length;
        }

        public int EstimateTokens(string text)
        {
            int words = CountWords(text);
            if (words == 0) return 0;
            return (int)System.Math.Ceiling(words * 1.35);
        }
    }
}
