using System.Buffers;
using System.Net.Http.Json;
using Newtonsoft.Json;
using OllamaRedisImageSearch.Config;
using OllamaRedisImageSearch.Models;

namespace OllamaRedisImageSearch.Services;

public interface IOllamaService : IDisposable
{
    Task<string> DescribeImageWithLlavaAsync(string imagePath);
    Task<float[]> GetTextEmbeddingAsync(string text);
    Task<string> SummarizeWithGemmaAsync(string query, List<SearchResult> results);
}

public class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly AppConfig _config;
    private readonly SemaphoreSlim _semaphore;

    public OllamaService(AppConfig config)
    {
        _config = config;
        _httpClient = new HttpClient 
        { 
            Timeout = TimeSpan.FromMinutes(5),
            BaseAddress = new Uri(_config.OllamaBaseUrl)
        };
        _semaphore = new SemaphoreSlim(_config.MaxConcurrentOperations);
    }

    public async Task<string> DescribeImageWithLlavaAsync(string imagePath)
    {
        await _semaphore.WaitAsync();
        try
        {
            var base64Image = await ConvertImageToBase64Async(imagePath);
            
            var request = new OllamaChatRequest
            {
                Model = _config.VisionModel,
                Messages = new List<OllamaMessage>
                {
                    new()
                    {
                        Role = "user",
                        Content = "Describe this image in detail, focusing on objects, people, activities, colors, and setting. Be specific and descriptive.",
                        Images = new List<string> { base64Image }
                    }
                }
            };

            return await ExecuteWithRetryAsync(async () =>
            {
                var response = await _httpClient.PostAsJsonAsync("/api/chat", request);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var chatResponse = JsonConvert.DeserializeObject<OllamaChatResponse>(responseContent);
                
                return chatResponse?.Message?.Content ?? "Unable to describe image";
            });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<float[]> GetTextEmbeddingAsync(string text)
    {
        await _semaphore.WaitAsync();
        try
        {
            var request = new OllamaEmbeddingRequest
            {
                Model = _config.EmbeddingModel,
                Prompt = text
            };

            return await ExecuteWithRetryAsync(async () =>
            {
                var response = await _httpClient.PostAsJsonAsync("/api/embeddings", request);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var embeddingResponse = JsonConvert.DeserializeObject<OllamaEmbeddingResponse>(responseContent);
                
                return embeddingResponse?.Embedding ?? Array.Empty<float>();
            });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<string> SummarizeWithGemmaAsync(string query, List<SearchResult> results)
    {
        await _semaphore.WaitAsync();
        try
        {
            // Implement MCP-aware context management
            var optimizedResults = OptimizeResultsForContext(results);
            var contextPrompt = BuildContextPrompt(query, optimizedResults);

            var request = new OllamaChatRequest
            {
                Model = _config.SummarizationModel,
                Messages = new List<OllamaMessage>
                {
                    new()
                    {
                        Role = "user",
                        Content = contextPrompt
                    }
                }
            };

            return await ExecuteWithRetryAsync(async () =>
            {
                var response = await _httpClient.PostAsJsonAsync("/api/chat", request);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var chatResponse = JsonConvert.DeserializeObject<OllamaChatResponse>(responseContent);
                
                return chatResponse?.Message?.Content ?? "Unable to summarize results";
            });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<string> ConvertImageToBase64Async(string imagePath)
    {
        var imageBytes = await File.ReadAllBytesAsync(imagePath);
        return Convert.ToBase64String(imageBytes);
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
    {
        Exception? lastException = null;
        
        for (int attempt = 0; attempt < _config.MaxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (HttpRequestException ex) when (attempt < _config.MaxRetries - 1)
            {
                lastException = ex;
                var delay = TimeSpan.FromMilliseconds(_config.BaseRetryDelayMs * Math.Pow(2, attempt));
                Console.WriteLine($"Retry attempt {attempt + 1} after {delay.TotalSeconds}s: {ex.Message}");
                await Task.Delay(delay);
            }
        }
        
        throw lastException ?? new InvalidOperationException("Operation failed after retries");
    }

    private List<SearchResult> OptimizeResultsForContext(List<SearchResult> results)
    {
        // Estimate tokens and optimize for MCP
        const int basePromptTokens = 200;
        int estimatedTokens = basePromptTokens;
        var optimizedResults = new List<SearchResult>();

        foreach (var result in results.OrderByDescending(r => r.Score))
        {
            int resultTokens = EstimateTokens(result.Description) + 50; // metadata overhead
            
            if (estimatedTokens + resultTokens <= _config.MaxContextTokens)
            {
                optimizedResults.Add(result);
                estimatedTokens += resultTokens;
            }
            else
            {
                break;
            }
        }

        return optimizedResults;
    }

    private string BuildContextPrompt(string query, List<SearchResult> results)
    {
        var prompt = "Here are descriptions of relevant images:\n\n";
        
        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            prompt += $"{i + 1}. **{result.Filename}** (Similarity: {result.Score:F3})\n";
            prompt += $"   Description: {result.Description}\n\n";
        }
        
        prompt += $"Based on these images and the user's query: \"{query}\", ";
        prompt += "provide a comprehensive answer describing the most relevant images and how they relate to the query. ";
        prompt += "Be specific about which images match best and explain why.";
        
        return prompt;
    }

    private int EstimateTokens(string text)
    {
        // Rough estimation: ~4 characters per token for English text
        return Math.Max(1, text.Length / 4);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _semaphore?.Dispose();
    }
}
