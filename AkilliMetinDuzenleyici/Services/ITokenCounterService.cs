namespace AkilliMetinDuzenleyici.Services
{
    public interface ITokenCounterService
    {
        int CountWords(string? text);
        int EstimateTokens(string? text);
    }
}
