using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AkilliMetinDuzenleyici.Models;

namespace AkilliMetinDuzenleyici.Services
{
    public class GroqApiService : IGroqApiService
    {
        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(120)
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

            if (string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                return new GroqApiResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Groq Cloud API Key tanımlanmamış. Lütfen Ayarlar bölümünden API anahtarınızı girin."
                };
            }

            string targetModel = string.IsNullOrWhiteSpace(settings.Model) ? "groq/compound" : settings.Model;

            int maxRetries = 3;
            int currentRetry = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var requestPayload = new GroqChatRequest
                {
                    Model = targetModel,
                    Temperature = settings.Temperature,
                    MaxTokens = 8192,
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

                    using var response = await HttpClient.SendAsync(httpRequest, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                        var apiResponse = JsonSerializer.Deserialize<GroqChatResponse>(responseJson);

                        if (apiResponse?.Choices != null && apiResponse.Choices.Count > 0)
                        {
                            string rawContent = apiResponse.Choices[0].Message?.Content ?? string.Empty;
                            
                            // Regex filter to strip <think>...</think> reasoning blocks
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
                    else if (response.StatusCode == HttpStatusCode.TooManyRequests) // HTTP 429 Rate Limit
                    {
                        currentRetry++;

                        // Try model fallback on rate limit if available in chain
                        string[] modelFallbackChain = new[] { "groq/compound", "openai/gpt-oss-120b", "qwen/qwen3.8-27b" };
                        int currentIndex = System.Array.IndexOf(modelFallbackChain, targetModel);

                        if (currentIndex >= 0 && currentIndex < modelFallbackChain.Length - 1)
                        {
                            targetModel = modelFallbackChain[currentIndex + 1];
                            settings.Model = targetModel;
                            statusCallback?.Invoke($"HTTP 429 İstek Sınırı! Otomatik olarak yedek modele geçiliyor: '{targetModel}'...");
                            await Task.Delay(1000, cancellationToken);
                            continue;
                        }

                        if (currentRetry > maxRetries)
                        {
                            return new GroqApiResult
                            {
                                IsSuccess = false,
                                ErrorMessage = "API istek sınırı (Rate Limit - HTTP 429) aşıldı ve tüm yedek modeller denendi."
                            };
                        }

                        int delaySeconds = 5 * currentRetry;
                        if (response.Headers.RetryAfter?.Delta.HasValue == true)
                        {
                            delaySeconds = (int)Math.Ceiling(response.Headers.RetryAfter.Delta.Value.TotalSeconds);
                        }

                        statusCallback?.Invoke($"İstek sınırı (HTTP 429) algılandı. {delaySeconds} saniye beklenip yeniden denenecek... (Deneme {currentRetry}/{maxRetries})");
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                        continue;
                    }
                    else
                    {
                        string errorText = await response.Content.ReadAsStringAsync(cancellationToken);

                        // Auto-fallback if model is missing or decommissioned (HTTP 404 / model_not_found)
                        string[] modelFallbackChain = new[] { "groq/compound", "openai/gpt-oss-120b", "qwen/qwen3.8-27b" };
                        int currentIndex = System.Array.IndexOf(modelFallbackChain, targetModel);

                        if (currentIndex >= 0 && currentIndex < modelFallbackChain.Length - 1 && 
                            (response.StatusCode == HttpStatusCode.NotFound || errorText.Contains("model_not_found") || errorText.Contains("model_decommissioned")))
                        {
                            targetModel = modelFallbackChain[currentIndex + 1];
                            settings.Model = targetModel;

                            statusCallback?.Invoke($"Model kullanılamıyor (HTTP 404). Otomatik olarak yedek modele geçiliyor: '{targetModel}'...");
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
                    throw; // Propagate cancellation upwards
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
