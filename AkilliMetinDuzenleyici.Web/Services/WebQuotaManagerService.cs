using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace AkilliMetinDuzenleyici.Web.Models
{
    public class UsageData
    {
        [JsonPropertyName("SonTarih")]
        public string SonTarih { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

        [JsonPropertyName("GunlukIstekSayisi")]
        public int GunlukIstekSayisi { get; set; } = 0;

        [JsonPropertyName("GunlukMaxIstek")]
        public int GunlukMaxIstek { get; set; } = 1000;

        [JsonPropertyName("ToplamIslenanKelime")]
        public int ToplamIslenanKelime { get; set; } = 0;

        [JsonPropertyName("ToplamHarcananToken")]
        public int ToplamHarcananToken { get; set; } = 0;
    }
}

namespace AkilliMetinDuzenleyici.Web.Services
{
    public interface IQuotaManagerService
    {
        Task<AkilliMetinDuzenleyici.Web.Models.UsageData> GetUsageAsync();
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

        public async Task<AkilliMetinDuzenleyici.Web.Models.UsageData> GetUsageAsync()
        {
            try
            {
                string json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", LocalStorageKey);
                if (!string.IsNullOrEmpty(json))
                {
                    var data = JsonSerializer.Deserialize<AkilliMetinDuzenleyici.Web.Models.UsageData>(json);
                    if (data != null)
                    {
                        string today = DateTime.Now.ToString("yyyy-MM-dd");
                        if (data.SonTarih != today)
                        {
                            data.SonTarih = today;
                            data.GunlukIstekSayisi = 0;
                            data.ToplamIslenanKelime = 0;
                            data.ToplamHarcananToken = 0;
                            await SaveUsageAsync(data);
                        }
                        return data;
                    }
                }
            }
            catch
            {
                // Fallback for SSG / initial render
            }

            var defaultData = new AkilliMetinDuzenleyici.Web.Models.UsageData();
            await SaveUsageAsync(defaultData);
            return defaultData;
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

        private async Task SaveUsageAsync(AkilliMetinDuzenleyici.Web.Models.UsageData data)
        {
            try
            {
                string json = JsonSerializer.Serialize(data);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", LocalStorageKey, json);
            }
            catch
            {
                // Ignore JS Interop errors on pre-render
            }
        }
    }
}
