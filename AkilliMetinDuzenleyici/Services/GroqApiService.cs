using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AkilliMetinDuzenleyici.Models;

namespace AkilliMetinDuzenleyici.Services
{
    public class GroqApiService : IGroqApiService
    {
        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        public async Task<GroqApiResult> CorrectTextAsync(
            string inputText,
            AppSettings settings,
            Action<string>? statusCallback = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(inputText))
            {
                return new GroqApiResult
                {
                    IsSuccess = true,
                    CorrectedText = string.Empty
                };
            }

            string provider = (settings.Provider ?? "groq").ToLowerInvariant();

            if (provider == "gemini")
            {
                string cleanGeminiKey = settings.GeminiApiKey?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(cleanGeminiKey))
                {
                    return new GroqApiResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Google Gemini API Key tanımlanmamış. Lütfen Ayarlar bölümünden Gemini API anahtarınızı girin."
                    };
                }
                if (!cleanGeminiKey.StartsWith("AIza"))
                {
                    string prefix = cleanGeminiKey.Substring(0, Math.Min(8, cleanGeminiKey.Length));
                    return new GroqApiResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Girdiğiniz anahtar ('{prefix}...') bir Google Gemini API anahtarı değildir.\n\nGoogle Gemini API anahtarları her zaman 'AIza' ile başlar.\nLütfen https://aistudio.google.com/app/apikey adresinden ücretsiz 'AIza...' anahtarınızı alın."
                    };
                }
                return await CallGeminiNativeAsync(inputText, settings, statusCallback, cancellationToken);
            }
            else
            {
                string cleanGroqKey = settings.GroqApiKey?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(cleanGroqKey))
                {
                    return new GroqApiResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Groq API Key tanımlanmamış. Lütfen Ayarlar bölümünden Groq API anahtarınızı girin."
                    };
                }
                if (!cleanGroqKey.StartsWith("gsk_"))
                {
                    string prefix = cleanGroqKey.Substring(0, Math.Min(8, cleanGroqKey.Length));
                    return new GroqApiResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Girdiğiniz anahtar ('{prefix}...') bir Groq API anahtarı değildir.\n\nGroq API anahtarları her zaman 'gsk_' ile başlar.\nLütfen https://console.groq.com/keys adresinden ücretsiz 'gsk_...' anahtarınızı alın."
                    };
                }
                return await CallGroqOpenAiAsync(inputText, settings, statusCallback, cancellationToken);
            }
        }

        private async Task<GroqApiResult> CallGeminiNativeAsync(
            string inputText,
            AppSettings settings,
            Action<string>? statusCallback,
            CancellationToken cancellationToken)
        {
            string targetModel = string.IsNullOrWhiteSpace(settings.GeminiModel) || settings.GeminiModel.Contains("llama") || settings.GeminiModel.Contains("groq") || settings.GeminiModel.Contains("qwen")
                ? "gemini-3.6-flash" 
                : settings.GeminiModel;

            string[] geminiFallbackChain = new[] { "gemini-3.6-flash", "gemini-2.0-flash", "gemini-1.5-flash" };

            int maxRetries = 2;
            int currentRetry = 0;

            int estimatedPromptTokens = (inputText.Length / 3) + (settings.SystemPrompt?.Length / 3 ?? 500);
            int safeMaxTokens = Math.Min(3500, Math.Max(1024, estimatedPromptTokens + 500));

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string apiKeyClean = settings.GeminiApiKey.Trim();
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/{targetModel}:generateContent?key={apiKeyClean}";

                var requestPayload = new
                {
                    systemInstruction = new
                    {
                        parts = new[] { new { text = settings.SystemPrompt } }
                    },
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[] { new { text = inputText } }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = settings.Temperature,
                        topP = 0.95,
                        maxOutputTokens = safeMaxTokens
                    }
                };

                string jsonBody = JsonSerializer.Serialize(requestPayload);

                try
                {
                    using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
                    httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                    statusCallback?.Invoke($"Google Gemini API ({targetModel}) sunucusuna istek gönderiliyor...");

                    using var response = await HttpClient.SendAsync(httpRequest, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                        using var doc = JsonDocument.Parse(responseJson);

                        string corrected = string.Empty;
                        int promptTokens = 0;
                        int completionTokens = 0;

                        if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                        {
                            var firstCand = candidates[0];
                            if (firstCand.TryGetProperty("content", out var content) &&
                                content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                            {
                                corrected = parts[0].GetProperty("text").GetString() ?? string.Empty;
                            }
                        }

                        if (doc.RootElement.TryGetProperty("usageMetadata", out var usage))
                        {
                            if (usage.TryGetProperty("promptTokenCount", out var pt)) promptTokens = pt.GetInt32();
                            if (usage.TryGetProperty("candidatesTokenCount", out var ct)) completionTokens = ct.GetInt32();
                        }

                        corrected = System.Text.RegularExpressions.Regex.Replace(
                            corrected,
                            @"<think>[\s\S]*?<\/think>",
                            "",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

                        return new GroqApiResult
                        {
                            IsSuccess = true,
                            CorrectedText = corrected,
                            PromptTokens = promptTokens,
                            CompletionTokens = completionTokens,
                            TotalTokens = promptTokens + completionTokens
                        };
                    }
                    else
                    {
                        string errorText = await response.Content.ReadAsStringAsync(cancellationToken);

                        if (response.StatusCode == HttpStatusCode.Forbidden || 
                            response.StatusCode == HttpStatusCode.Unauthorized ||
                            errorText.Contains("leaked") || 
                            errorText.Contains("PERMISSION_DENIED") || 
                            errorText.Contains("API_KEY_INVALID") ||
                            errorText.Contains("INVALID_ARGUMENT"))
                        {
                            return new GroqApiResult
                            {
                                IsSuccess = false,
                                ErrorMessage = "Google Gemini API Anahtarınız geçersiz veya engellenmiş (Invalid/Blocked Key). Lütfen aistudio.google.com/app/apikey adresinden 'AIza...' ile başlayan ücretsiz yeni bir API anahtarı alıp Ayarlar menüsüne girin."
                            };
                        }

                        currentRetry++;

                        if (currentRetry <= maxRetries && (response.StatusCode == HttpStatusCode.NotFound || errorText.Contains("404") || errorText.Contains("NOT_FOUND")))
                        {
                            int currentIndex = Array.IndexOf(geminiFallbackChain, targetModel);
                            if (currentIndex >= 0 && currentIndex < geminiFallbackChain.Length - 1)
                            {
                                targetModel = geminiFallbackChain[currentIndex + 1];
                            }
                            else
                            {
                                targetModel = "gemini-3.6-flash";
                            }

                            statusCallback?.Invoke($"Gemini Model Uyarısı. Otomatik aktif modele geçiliyor: '{targetModel}'...");
                            await Task.Delay(300, cancellationToken);
                            continue;
                        }

                        return new GroqApiResult
                        {
                            IsSuccess = false,
                            ErrorMessage = $"Google Gemini API Hatası (HTTP {(int)response.StatusCode}): {errorText}"
                        };
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    currentRetry++;
                    if (currentRetry > maxRetries)
                    {
                        return new GroqApiResult
                        {
                            IsSuccess = false,
                            ErrorMessage = $"Gemini Bağlantı Hatası: {ex.Message}"
                        };
                    }

                    statusCallback?.Invoke($"Gemini bağlantı hatası, tekrar deneniyor ({currentRetry}/{maxRetries})...");
                    await Task.Delay(TimeSpan.FromSeconds(currentRetry), cancellationToken);
                }
            }
        }

        private async Task<GroqApiResult> CallGroqOpenAiAsync(
            string inputText,
            AppSettings settings,
            Action<string>? statusCallback,
            CancellationToken cancellationToken)
        {
            string targetModel = string.IsNullOrWhiteSpace(settings.GroqModel) || settings.GroqModel.Contains("gemini")
                ? "qwen/qwen3.8-27b" 
                : settings.GroqModel;

            string endpoint = string.IsNullOrWhiteSpace(settings.GroqEndpoint) || settings.GroqEndpoint.Contains("googleapis.com") 
                ? "https://api.groq.com/openai/v1/chat/completions" 
                : settings.GroqEndpoint;

            string[] groqFallbackChain = new[] { "qwen/qwen3.8-27b", "llama-3.3-70b-versatile", "llama-3.1-8b-instant" };

            int maxRetries = 2;
            int currentRetry = 0;

            int estimatedPromptTokens = (inputText.Length / 3) + (settings.SystemPrompt?.Length / 3 ?? 500);
            int safeMaxTokens = Math.Min(3500, Math.Max(1024, estimatedPromptTokens + 500));

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var requestPayload = new GroqChatRequest
                {
                    Model = targetModel,
                    Temperature = settings.Temperature,
                    MaxTokens = safeMaxTokens,
                    Messages = new System.Collections.Generic.List<GroqChatMessage>
                    {
                        new GroqChatMessage
                        {
                            Role = "system",
                            Content = settings.SystemPrompt
                        },
                        new GroqChatMessage
                        {
                            Role = "user",
                            Content = inputText
                        }
                    }
                };

                string jsonBody = JsonSerializer.Serialize(requestPayload);

                try
                {
                    using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.GroqApiKey.Trim());
                    httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                    statusCallback?.Invoke($"Groq Cloud API ({targetModel}) sunucusuna istek gönderiliyor...");

                    using var response = await HttpClient.SendAsync(httpRequest, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                        var apiResponse = JsonSerializer.Deserialize<GroqChatResponse>(responseJson);

                        if (apiResponse?.Choices != null && apiResponse.Choices.Count > 0)
                        {
                            string rawContent = apiResponse.Choices[0].Message?.Content ?? string.Empty;
                            
                            string corrected = System.Text.RegularExpressions.Regex.Replace(
                                rawContent,
                                @"<think>[\s\S]*?<\/think>",
                                "",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

                            int promptTokens = apiResponse.Usage?.PromptTokens ?? 0;
                            int completionTokens = apiResponse.Usage?.CompletionTokens ?? 0;

                            return new GroqApiResult
                            {
                                IsSuccess = true,
                                CorrectedText = corrected,
                                PromptTokens = promptTokens,
                                CompletionTokens = completionTokens,
                                TotalTokens = promptTokens + completionTokens
                            };
                        }
                        else
                        {
                            return new GroqApiResult
                            {
                                IsSuccess = false,
                                ErrorMessage = "API'den boş yanıt döndü."
                            };
                        }
                    }
                    else if (response.StatusCode == HttpStatusCode.TooManyRequests || response.StatusCode == HttpStatusCode.RequestEntityTooLarge)
                    {
                        currentRetry++;
                        int currentIndex = Array.IndexOf(groqFallbackChain, targetModel);

                        if (currentIndex >= 0 && currentIndex < groqFallbackChain.Length - 1)
                        {
                            targetModel = groqFallbackChain[currentIndex + 1];
                        }
                        else
                        {
                            targetModel = "llama-3.1-8b-instant";
                        }

                        if (currentRetry > maxRetries)
                        {
                            return new GroqApiResult
                            {
                                IsSuccess = false,
                                ErrorMessage = $"Groq istek/token sınırı (HTTP {(int)response.StatusCode}) aşıldı."
                            };
                        }

                        int delaySeconds = 2 * currentRetry;
                        statusCallback?.Invoke($"Groq limit uyarısı. {delaySeconds} saniye beklenip '{targetModel}' ile deneniyor...");
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                        continue;
                    }
                    else
                    {
                        string errorText = await response.Content.ReadAsStringAsync(cancellationToken);
                        currentRetry++;

                        if (currentRetry <= maxRetries && (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.BadRequest || errorText.Contains("model_not_found") || errorText.Contains("model_decommissioned") || errorText.Contains("decommissioned")))
                        {
                            int currentIndex = Array.IndexOf(groqFallbackChain, targetModel);
                            if (currentIndex >= 0 && currentIndex < groqFallbackChain.Length - 1)
                            {
                                targetModel = groqFallbackChain[currentIndex + 1];
                            }
                            else
                            {
                                targetModel = "qwen/qwen3.8-27b";
                            }

                            statusCallback?.Invoke($"Model Uyarısı. Otomatik aktif modele geçiliyor: '{targetModel}'...");
                            await Task.Delay(300, cancellationToken);
                            continue;
                        }

                        return new GroqApiResult
                        {
                            IsSuccess = false,
                            ErrorMessage = $"Groq API Hatası (HTTP {(int)response.StatusCode}): {errorText}"
                        };
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    currentRetry++;
                    if (currentRetry > maxRetries)
                    {
                        return new GroqApiResult
                        {
                            IsSuccess = false,
                            ErrorMessage = $"Groq Bağlantı Hatası: {ex.Message}"
                        };
                    }

                    statusCallback?.Invoke($"Groq bağlantı hatası, tekrar deneniyor ({currentRetry}/{maxRetries})...");
                    await Task.Delay(TimeSpan.FromSeconds(currentRetry), cancellationToken);
                }
            }
        }
    }
}
