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
            
            // Sanitize Groq settings
            settings.GroqApiKey = settings.GroqApiKey?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(settings.GroqEndpoint) || settings.GroqEndpoint.Contains("googleapis"))
            {
                settings.GroqEndpoint = "https://api.groq.com/openai/v1/chat/completions";
            }
            if (string.IsNullOrWhiteSpace(settings.GroqModel) || 
                settings.GroqModel.Contains("mixtral") || 
                settings.GroqModel.Contains("gemma2") || 
                settings.GroqModel.Contains("gemini"))
            {
                settings.GroqModel = "qwen/qwen3.8-27b";
            }

            // Sanitize Gemini settings
            if (string.IsNullOrWhiteSpace(settings.GeminiApiKey))
            {
                settings.GeminiApiKey = "AIzaSyCbglc_iOFvDx1qBo8kPVs116XWGFZTE4s";
            }
            else
            {
                settings.GeminiApiKey = settings.GeminiApiKey.Trim();
            }

            if (string.IsNullOrWhiteSpace(settings.GeminiEndpoint) || settings.GeminiEndpoint.Contains("groq"))
            {
                settings.GeminiEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent";
            }

            if (string.IsNullOrWhiteSpace(settings.GeminiModel) || 
                settings.GeminiModel.Contains("llama") || 
                settings.GeminiModel.Contains("groq") || 
                settings.GeminiModel.Contains("qwen") || 
                settings.GeminiModel.Contains("2.0") || 
                settings.GeminiModel.Contains("1.5"))
            {
                settings.GeminiModel = "gemini-3.6-flash";
            }

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

            if (settings.MaxWordsPerChunk <= 0 || settings.MaxWordsPerChunk > 1000) settings.MaxWordsPerChunk = 600;
            if (settings.DelayBetweenChunksMs < 0) settings.DelayBetweenChunksMs = 1500;
        }
    }
}
