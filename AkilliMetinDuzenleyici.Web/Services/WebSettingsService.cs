using System.Text.Json;
using System.Threading.Tasks;
using AkilliMetinDuzenleyici.Web.Models;
using Microsoft.JSInterop;

namespace AkilliMetinDuzenleyici.Web.Services
{
    public interface ISettingsService
    {
        Task<AppSettings> LoadSettingsAsync();
        Task SaveSettingsAsync(AppSettings settings);
        void SanitizeSettings(AppSettings settings);
    }

    public class WebSettingsService : ISettingsService
    {
        private readonly IJSRuntime _jsRuntime;
        private const string LocalStorageKey = "akilli_metin_ayarlar";

        public WebSettingsService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task<AppSettings> LoadSettingsAsync()
        {
            try
            {
                string json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", LocalStorageKey);
                if (!string.IsNullOrEmpty(json))
                {
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        SanitizeSettings(settings);
                        return settings;
                    }
                }
            }
            catch
            {
                // Fallback for SSR
            }

            var defaultSettings = new AppSettings();
            SanitizeSettings(defaultSettings);
            await SaveSettingsAsync(defaultSettings);
            return defaultSettings;
        }

        public async Task SaveSettingsAsync(AppSettings settings)
        {
            try
            {
                SanitizeSettings(settings);
                string json = JsonSerializer.Serialize(settings);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", LocalStorageKey, json);
            }
            catch
            {
                // Ignore JS Interop errors
            }
        }

        public void SanitizeSettings(AppSettings settings)
        {
            if (settings == null) return;

            settings.ApiKey = settings.ApiKey?.Trim() ?? string.Empty;
            settings.Endpoint = settings.Endpoint?.Trim() ?? string.Empty;
            settings.Model = settings.Model?.Trim() ?? string.Empty;
            settings.SystemPrompt = settings.SystemPrompt?.Trim() ?? string.Empty;
            settings.SelectedPromptName = settings.SelectedPromptName?.Trim() ?? string.Empty;

            if (settings.SavedPrompts != null)
            {
                foreach (var item in settings.SavedPrompts)
                {
                    item.Name = item.Name?.Trim() ?? string.Empty;
                    item.Content = item.Content?.Trim() ?? string.Empty;
                }
            }

            if (settings.MaxWordsPerChunk <= 0) settings.MaxWordsPerChunk = 2000;
            if (settings.DelayBetweenChunksMs < 0) settings.DelayBetweenChunksMs = 1500;
        }
    }
}
