using System;
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
                // Fallback for SSR / initial render
            }

            var defaultSettings = new AppSettings();
            SanitizeSettings(defaultSettings);
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

            settings.Provider = (settings.Provider?.Trim() ?? "groq").ToLowerInvariant();
            settings.ApiKey = settings.ApiKey?.Trim() ?? string.Empty;
            settings.Endpoint = settings.Endpoint?.Trim() ?? string.Empty;
            settings.Model = settings.Model?.Trim() ?? string.Empty;
            settings.SystemPrompt = settings.SystemPrompt?.Trim() ?? string.Empty;
            settings.SelectedPromptName = settings.SelectedPromptName?.Trim() ?? string.Empty;

            if (settings.Provider == "gemini")
            {
                if (string.IsNullOrWhiteSpace(settings.ApiKey))
                {
                    settings.ApiKey = "AIzaSyCbglc_iOFvDx1qBo8kPVs116XWGFZTE4s";
                }
                if (string.IsNullOrWhiteSpace(settings.Endpoint) || settings.Endpoint.Contains("groq.com"))
                {
                    settings.Endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent";
                }
                if (string.IsNullOrWhiteSpace(settings.Model) || settings.Model.Contains("llama") || settings.Model.Contains("groq") || settings.Model.Contains("qwen"))
                {
                    settings.Model = "gemini-3.6-flash";
                }
            }
            else // groq
            {
                if (string.IsNullOrWhiteSpace(settings.Endpoint) || settings.Endpoint.Contains("googleapis.com"))
                {
                    settings.Endpoint = "https://api.groq.com/openai/v1/chat/completions";
                }
                if (string.IsNullOrWhiteSpace(settings.Model) || 
                    settings.Model.Contains("mixtral") || 
                    settings.Model.Contains("gemma2") || 
                    settings.Model.Contains("compound") || 
                    settings.Model.Contains("gpt-oss"))
                {
                    settings.Model = "qwen/qwen3.8-27b";
                }
            }

            if (settings.SavedPrompts != null)
            {
                foreach (var item in settings.SavedPrompts)
                {
                    item.Name = item.Name?.Trim() ?? string.Empty;
                    item.Content = item.Content?.Trim() ?? string.Empty;
                }
            }

            if (settings.MaxWordsPerChunk <= 0 || settings.MaxWordsPerChunk > 1000) settings.MaxWordsPerChunk = 600;
            if (settings.DelayBetweenChunksMs < 0) settings.DelayBetweenChunksMs = 1500;
        }
    }
}
