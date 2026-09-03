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
