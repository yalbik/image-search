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
    Task<List<string>> GetAvailableModelsAsync();
    Task<bool> UnloadModelAsync(string modelName);
    Task<bool> UnloadAllModelsAsync();
}

public class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly AppConfig _config;
    private readonly SemaphoreSlim _semaphore;
    private string? _lastUsedModel;

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

    private async Task EnsureModelLoadedAsync(string modelName, string modelType)
    {
        if (_config.AutoFlushVramOnModelSwitch && _lastUsedModel != modelName)
        {
            Console.WriteLine($"Switching to {modelType} model: {modelName} (was: {_lastUsedModel ?? "none"})");
            await UnloadAllModelsAsync();
            await Task.Delay(300); // Allow time for VRAM to clear
            _lastUsedModel = modelName;
        }
    }

    public async Task<string> DescribeImageWithLlavaAsync(string imagePath)
    {
        await _semaphore.WaitAsync();
        try
        {
            // Ensure vision model is loaded with VRAM management
            await EnsureModelLoadedAsync(_config.VisionModel, "vision");

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
            // Ensure embedding model is loaded with VRAM management
            await EnsureModelLoadedAsync(_config.EmbeddingModel, "embedding");

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
            // Ensure summarization model is loaded with VRAM management
            await EnsureModelLoadedAsync(_config.SummarizationModel, "summarization");

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

    public async Task<List<string>> GetAvailableModelsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags");
            response.EnsureSuccessStatusCode();
            
            var jsonContent = await response.Content.ReadAsStringAsync();
            var modelsResponse = JsonConvert.DeserializeObject<OllamaModelsResponse>(jsonContent);
            
            if (modelsResponse?.Models != null)
            {
                return modelsResponse.Models
                    .Select(m => m.Name)
                    .OrderBy(name => name)
                    .ToList();
            }
            
            return new List<string>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting available models: {ex.Message}");
            return new List<string>();
        }
    }

    public async Task<bool> UnloadModelAsync(string modelName)
    {
        try
        {
            // Use the correct Ollama API to unload a model by setting keep_alive to 0
            var request = new
            {
                model = modelName,
                keep_alive = 0  // Setting to 0 unloads immediately
            };

            var jsonContent = JsonConvert.SerializeObject(request);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/api/generate", content);
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Unloaded model: {modelName}");
                return true;
            }
            else
            {
                Console.WriteLine($"Failed to unload model {modelName}: {response.StatusCode}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error unloading model {modelName}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UnloadAllModelsAsync()
    {
        try
        {
            // Get list of currently loaded models first
            var loadedModels = await GetLoadedModelsAsync();
            
            bool allSuccess = true;
            foreach (var model in loadedModels)
            {
                var success = await UnloadModelAsync(model);
                if (!success) allSuccess = false;
            }
            
            Console.WriteLine("Attempted to unload all models from VRAM");
            return allSuccess;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error unloading all models: {ex.Message}");
            return false;
        }
    }

    private async Task<List<string>> GetLoadedModelsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/ps");
            response.EnsureSuccessStatusCode();
            
            var jsonContent = await response.Content.ReadAsStringAsync();
            var psResponse = JsonConvert.DeserializeObject<OllamaPsResponse>(jsonContent);
            
            return psResponse?.Models?.Select(m => m.Name).ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting loaded models: {ex.Message}");
            return new List<string>();
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _semaphore?.Dispose();
    }
}
