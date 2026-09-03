using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AkilliMetinDuzenleyici.Models;

namespace AkilliMetinDuzenleyici.Services
{
    public class QuotaManagerService : IQuotaManagerService
    {
        private readonly string _filePath;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public QuotaManagerService(string? customPath = null)
        {
            _filePath = customPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "kullanim.json");
        }

        public async Task<UsageData> GetUsageAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                UsageData usage = await LoadInternalAsync();
                bool changed = ResetIfNewDay(usage);
                if (changed)
                {
                    await SaveInternalAsync(usage);
                }
                return usage;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task RecordUsageAsync(int wordCount, int tokenCount)
        {
            await _semaphore.WaitAsync();
            try
            {
                UsageData usage = await LoadInternalAsync();
                ResetIfNewDay(usage);

                usage.GunlukIstekSayisi += 1;
                usage.ToplamIslenanKelime += wordCount;
                usage.ToplamHarcananToken += tokenCount;

                await SaveInternalAsync(usage);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<bool> CanMakeRequestAsync()
        {
            UsageData usage = await GetUsageAsync();
            return usage.GunlukIstekSayisi < usage.GunlukMaxIstek;
        }

        public async Task ResetQuotaIfNewDayAsync()
        {
            await GetUsageAsync();
        }

        private async Task<UsageData> LoadInternalAsync()
        {
            if (!File.Exists(_filePath))
            {
                var newUsage = new UsageData();
                await SaveInternalAsync(newUsage);
                return newUsage;
            }

            try
            {
                string json = await File.ReadAllTextAsync(_filePath);
                var data = JsonSerializer.Deserialize<UsageData>(json, JsonOptions);
                return data ?? new UsageData();
            }
            catch
            {
                var fallback = new UsageData();
                await SaveInternalAsync(fallback);
                return fallback;
            }
        }

        private async Task SaveInternalAsync(UsageData usage)
        {
            try
            {
                string json = JsonSerializer.Serialize(usage, JsonOptions);
                await File.WriteAllTextAsync(_filePath, json);
            }
            catch
            {
                // Silently ignore write failures if file is temporarily locked
            }
        }

        private bool ResetIfNewDay(UsageData usage)
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (usage.Tarih != today)
            {
                usage.Tarih = today;
                usage.GunlukIstekSayisi = 0;
                usage.ToplamIslenanKelime = 0;
                usage.ToplamHarcananToken = 0;
                return true;
            }
            return false;
        }
    }
}
