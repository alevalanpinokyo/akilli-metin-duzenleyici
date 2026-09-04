using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AkilliMetinDuzenleyici.Models;

namespace AkilliMetinDuzenleyici.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly string _filePath;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public SettingsService(string? customPath = null)
        {
            _filePath = customPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        }

        public async Task<AppSettings> LoadSettingsAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                if (!File.Exists(_filePath))
                {
                    var defaultSettings = new AppSettings();
                    SanitizeSettings(defaultSettings);
                    await SaveInternalAsync(defaultSettings);
                    return defaultSettings;
                }

                string json = await File.ReadAllTextAsync(_filePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                var result = settings ?? new AppSettings();
                SanitizeSettings(result);
                return result;
            }
            catch
            {
                var fallback = new AppSettings();
                SanitizeSettings(fallback);
                return fallback;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task SaveSettingsAsync(AppSettings settings)
        {
            await _semaphore.WaitAsync();
            try
            {
                SanitizeSettings(settings);
                await SaveInternalAsync(settings);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void SanitizeSettings(AppSettings settings)
        {
            if (settings == null) return;

            settings.Provider = (settings.Provider?.Trim() ?? "groq").ToLowerInvariant();
            
            // Legacy JSON migration for old "api_key" property
            if (!string.IsNullOrWhiteSpace(settings.LegacyApiKey))
            {
                string legacyKey = settings.LegacyApiKey.Trim();
                if (legacyKey.StartsWith("gsk_") && string.IsNullOrWhiteSpace(settings.GroqApiKey))
                {
                    settings.GroqApiKey = legacyKey;
                }
                else if (legacyKey.StartsWith("AIza") && string.IsNullOrWhiteSpace(settings.GeminiApiKey))
                {
                    settings.GeminiApiKey = legacyKey;
                }
            }

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

        private async Task SaveInternalAsync(AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, JsonOptions);
                await File.WriteAllTextAsync(_filePath, json);
            }
            catch
            {
                // Silently ignore file write locks
            }
        }
    }
}
