using System;
using System.Text.Json;
using System.Threading.Tasks;
using AkilliMetinDuzenleyici.Web.Models;
using Microsoft.JSInterop;

namespace AkilliMetinDuzenleyici.Web.Services
{
    public interface IQuotaManagerService
    {
        Task<UsageData> GetUsageAsync();
        Task<bool> CanMakeRequestAsync();
        Task RecordUsageAsync(int wordsProcessed, int tokensUsed);
    }

    public class WebQuotaManagerService : IQuotaManagerService
    {
        private readonly IJSRuntime _jsRuntime;
        private const string LocalStorageKey = "akilli_metin_kullanim";

        public WebQuotaManagerService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task<UsageData> GetUsageAsync()
        {
            try
            {
                string json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", LocalStorageKey);
                if (!string.IsNullOrEmpty(json))
                {
                    var data = JsonSerializer.Deserialize<UsageData>(json);
                    if (data != null)
                    {
                        string today = DateTime.Now.ToString("yyyy-MM-dd");
                        if (data.SonTarih != today)
                        {
                            data.SonTarih = today;
                            data.GunlukIstekSayisi = 0;
                            data.ToplamIslenanKelime = 0;
                            data.ToplamHarcananToken = 0;
                            _ = SaveUsageAsync(data);
                        }
                        return data;
                    }
                }
            }
            catch
            {
                // Fallback for SSR / initial render
            }

            return new UsageData();
        }

        public async Task<bool> CanMakeRequestAsync()
        {
            var usage = await GetUsageAsync();
            return usage.GunlukIstekSayisi < usage.GunlukMaxIstek;
        }

        public async Task RecordUsageAsync(int wordsProcessed, int tokensUsed)
        {
            var usage = await GetUsageAsync();
            usage.GunlukIstekSayisi++;
            usage.ToplamIslenanKelime += wordsProcessed;
            usage.ToplamHarcananToken += tokensUsed;
            await SaveUsageAsync(usage);
        }

        private async Task SaveUsageAsync(UsageData data)
        {
            try
            {
                string json = JsonSerializer.Serialize(data);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", LocalStorageKey, json);
            }
            catch
            {
                // Ignore JS Interop errors
            }
        }
    }
}
