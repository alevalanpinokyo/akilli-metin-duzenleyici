using System.Threading.Tasks;
using AkilliMetinDuzenleyici.Models;

namespace AkilliMetinDuzenleyici.Services
{
    public interface IQuotaManagerService
    {
        Task<UsageData> GetUsageAsync();
        Task RecordUsageAsync(int wordCount, int tokenCount);
        Task<bool> CanMakeRequestAsync();
        Task ResetQuotaIfNewDayAsync();
    }
}
