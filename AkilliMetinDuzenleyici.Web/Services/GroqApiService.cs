using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AkilliMetinDuzenleyici.Web.Models;

namespace AkilliMetinDuzenleyici.Web.Services
{
    public interface IGroqApiService
    {
        Task<GroqApiResult> CorrectTextAsync(
            string inputText,
            AppSettings settings,
            Action<string>? statusCallback = null,
            CancellationToken cancellationToken = default);
    }

    public class GroqApiService : IGroqApiService
    {
        private readonly HttpClient _httpClient;

        public GroqApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

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

            if (string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                return new GroqApiResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Groq Cloud API Key tanımlanmamış. Lütfen Ayarlar bölümünden API anahtarınızı girin."
                };
            }

            string targetModel = string.IsNullOrWhiteSpace(settings.Model) ? "llama-3.3-70b-versatile" : settings.Model;
            int maxRetries = 3;
            int currentRetry = 0;

            string[] modelFallbackChain = new[] { "llama-3.3-70b-versatile", "llama-3.1-8b-instant", "mixtral-8x7b-32768" };

            // Calculate safe MaxTokens to avoid HTTP 413 TPM limits
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
                    using var httpRequest = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint);
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey.Trim());
                    httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                    statusCallback?.Invoke($"Groq Cloud API'sine istek gönderiliyor ({targetModel})...");

                    using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

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
                            int totalTokens = apiResponse.Usage?.TotalTokens ?? (promptTokens + completionTokens);

                            return new GroqApiResult
                            {
                                IsSuccess = true,
                                CorrectedText = corrected,
                                PromptTokens = promptTokens,
                                CompletionTokens = completionTokens,
                                TotalTokens = totalTokens
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
                        int currentIndex = Array.IndexOf(modelFallbackChain, targetModel);

                        if (currentIndex >= 0 && currentIndex < modelFallbackChain.Length - 1)
                        {
                            targetModel = modelFallbackChain[currentIndex + 1];
                            settings.Model = targetModel;
                            statusCallback?.Invoke($"Groq Limiti (HTTP {(int)response.StatusCode})! Otomatik olarak daha hızlı yedek modele geçiliyor: '{targetModel}'...");
                            await Task.Delay(1000, cancellationToken);
                            continue;
                        }

                        if (currentRetry > maxRetries)
                        {
                            return new GroqApiResult
                            {
                                IsSuccess = false,
                                ErrorMessage = $"API istek/token sınırı (HTTP {(int)response.StatusCode}) aşıldı ve tüm yedek modeller denendi."
                            };
                        }

                        int delaySeconds = 4 * currentRetry;
                        statusCallback?.Invoke($"İstek sınırı algılandı. {delaySeconds} saniye beklenip yeniden denenecek...");
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                        continue;
                    }
                    else
                    {
                        string errorText = await response.Content.ReadAsStringAsync(cancellationToken);
                        int currentIndex = Array.IndexOf(modelFallbackChain, targetModel);

                        if (currentIndex >= 0 && currentIndex < modelFallbackChain.Length - 1 && 
                            (response.StatusCode == HttpStatusCode.NotFound || errorText.Contains("model_not_found") || errorText.Contains("model_decommissioned") || errorText.Contains("rate_limit_exceeded")))
                        {
                            targetModel = modelFallbackChain[currentIndex + 1];
                            settings.Model = targetModel;

                            statusCallback?.Invoke($"Model uyarısı. Otomatik olarak yedek modele geçiliyor: '{targetModel}'...");
                            await Task.Delay(500, cancellationToken);
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
                            ErrorMessage = $"Bağlantı Hatası: {ex.Message}"
                        };
                    }

                    statusCallback?.Invoke($"Bağlantı hatası oluştu, {3 * currentRetry} saniye sonra tekrar deneniyor...");
                    await Task.Delay(TimeSpan.FromSeconds(3 * currentRetry), cancellationToken);
                }
            }
        }
    }
}
