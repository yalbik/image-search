using OllamaRedisImageSearch.Config;
using OllamaRedisImageSearch.Models;

namespace OllamaRedisImageSearch.Services;

public interface IImageSearchService : IDisposable
{
    Task<bool> InitializeAsync();
    Task<bool> IndexImagesAsync(string imagesPath);
    Task<List<SearchResult>> SearchAsync(string query);
    Task<SearchResultWithSummary> SearchWithSummaryAsync(string query);
    Task<McpMetrics> GetLastSearchMetricsAsync();
    Task<(int total, int indexed, int skipped)> GetIndexingStatsAsync(string imagesPath);
}

public class ImageSearchService : IImageSearchService
{
    private readonly IOllamaService _ollamaService;
    private readonly IRedisVectorService _redisService;
    private readonly AppConfig _config;
    private McpMetrics _lastMetrics = new();

    public ImageSearchService(IOllamaService ollamaService, IRedisVectorService redisService, AppConfig config)
    {
        _ollamaService = ollamaService;
        _redisService = redisService;
        _config = config;
    }

    public async Task<bool> InitializeAsync()
    {
        Console.WriteLine("Initializing Image Search Service...");
        
        var indexCreated = await _redisService.EnsureRedisIndexExistsAsync();
        if (!indexCreated)
        {
            Console.WriteLine("Failed to create or verify Redis index.");
            return false;
        }

        var imageCount = await _redisService.GetIndexedImageCountAsync();
        Console.WriteLine($"Current indexed images: {imageCount}");
        
        return true;
    }

    public async Task<bool> IndexImagesAsync(string imagesPath)
    {
        if (!Directory.Exists(imagesPath))
        {
            Console.WriteLine($"Images directory not found: {imagesPath}");
            return false;
        }

        var imageFiles = Directory.GetFiles(imagesPath, "*.jpg", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(imagesPath, "*.jpeg", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(imagesPath, "*.png", SearchOption.AllDirectories))
            .ToArray();

        if (imageFiles.Length == 0)
        {
            Console.WriteLine($"No image files found in: {imagesPath}");
            return false;
        }

        Console.WriteLine($"Found {imageFiles.Length} images to process...");

        var semaphore = new SemaphoreSlim(_config.MaxConcurrentOperations);
        var processedCount = 0;
        var failedCount = 0;

        var tasks = imageFiles.Select(async imagePath =>
        {
            await semaphore.WaitAsync();
            try
            {
                var filename = Path.GetFileName(imagePath);
                var fileInfo = new FileInfo(imagePath);
                var lastModified = fileInfo.LastWriteTime;
                
                // Check if image is already indexed and up-to-date (unless force reindex is enabled)
                if (_config.EnableDeduplication && !_config.ForceReindex && 
                    await _redisService.IsImageUpToDateAsync(filename, lastModified))
                {
                    Console.WriteLine($"Skipping {filename} - already indexed and up-to-date");
                    Interlocked.Increment(ref processedCount);
                    return;
                }
                
                Console.WriteLine($"Processing {filename}...");
                
                var description = await _ollamaService.DescribeImageWithLlavaAsync(imagePath);
                if (string.IsNullOrEmpty(description))
                {
                    Console.WriteLine($"Failed to get description for: {filename}");
                    Interlocked.Increment(ref failedCount);
                    return;
                }

                var embedding = await _ollamaService.GetTextEmbeddingAsync(description);
                if (embedding.Length == 0)
                {
                    Console.WriteLine($"Failed to get embedding for: {filename}");
                    Interlocked.Increment(ref failedCount);
                    return;
                }

                var stored = await _redisService.StoreImageEmbeddingAsync(filename, description, embedding, lastModified);
                if (stored)
                {
                    Interlocked.Increment(ref processedCount);
                    Console.WriteLine($"Processed ({processedCount}/{imageFiles.Length}): {filename}");
                }
                else
                {
                    Interlocked.Increment(ref failedCount);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {Path.GetFileName(imagePath)}: {ex.Message}");
                Interlocked.Increment(ref failedCount);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        semaphore.Dispose();

        Console.WriteLine($"Indexing complete. Processed: {processedCount}, Failed: {failedCount}");
        return processedCount > 0;
    }

    public async Task<List<SearchResult>> SearchAsync(string query)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var metrics = new McpMetrics();

        try
        {
            Console.WriteLine($"Searching for: {query}");

            // Generate query embedding
            var embeddingStart = stopwatch.Elapsed;
            var queryEmbedding = await _ollamaService.GetTextEmbeddingAsync(query);
            metrics.EmbeddingTime = stopwatch.Elapsed - embeddingStart;

            if (queryEmbedding.Length == 0)
            {
                Console.WriteLine("Failed to generate query embedding");
                return new List<SearchResult>();
            }

            // Perform vector search
            var searchStart = stopwatch.Elapsed;
            var searchResults = await _redisService.SearchSimilarImagesAsync(queryEmbedding, _config.MaxSearchResults);
            metrics.SearchTime = stopwatch.Elapsed - searchStart;
            metrics.ResultsProcessed = searchResults.Count;

            if (searchResults.Count == 0)
            {
                Console.WriteLine("No similar images found");
                return searchResults;
            }

            Console.WriteLine($"Found {searchResults.Count} similar images:");
            foreach (var result in searchResults)
            {
                Console.WriteLine($"  - {result.Filename} (Score: {result.Score:F3})");
            }

            // Summarize with Gemma using MCP-aware processing
            var summarizationStart = stopwatch.Elapsed;
            var summary = await _ollamaService.SummarizeWithGemmaAsync(query, searchResults);
            metrics.SummarizationTime = stopwatch.Elapsed - summarizationStart;
            metrics.TokensUsed = EstimateTokensUsed(query, searchResults, summary);

            Console.WriteLine("\n=== AI Summary ===");
            Console.WriteLine(summary);
            Console.WriteLine("==================");

            return searchResults;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during search: {ex.Message}");
            return new List<SearchResult>();
        }
        finally
        {
            stopwatch.Stop();
            _lastMetrics = metrics;
            
            Console.WriteLine($"\n=== Performance Metrics ===");
            Console.WriteLine($"Embedding Time: {metrics.EmbeddingTime.TotalMilliseconds:F0}ms");
            Console.WriteLine($"Search Time: {metrics.SearchTime.TotalMilliseconds:F0}ms");
            Console.WriteLine($"Summarization Time: {metrics.SummarizationTime.TotalMilliseconds:F0}ms");
            Console.WriteLine($"Total Time: {stopwatch.Elapsed.TotalMilliseconds:F0}ms");
            Console.WriteLine($"Results Processed: {metrics.ResultsProcessed}");
            Console.WriteLine($"Estimated Tokens Used: {metrics.TokensUsed}");
            Console.WriteLine("============================");
        }
    }

    public async Task<SearchResultWithSummary> SearchWithSummaryAsync(string query)
    {
        var result = new SearchResultWithSummary();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var metrics = new McpMetrics { Timestamp = DateTime.UtcNow };

        try
        {
            Console.WriteLine($"Searching for: {query}");

            // Generate query embedding
            var embeddingStart = stopwatch.Elapsed;
            var queryEmbedding = await _ollamaService.GetTextEmbeddingAsync(query);
            metrics.EmbeddingTime = stopwatch.Elapsed - embeddingStart;

            if (queryEmbedding.Length == 0)
            {
                Console.WriteLine("Failed to generate query embedding");
                return result;
            }

            // Perform vector search
            var searchStart = stopwatch.Elapsed;
            var searchResults = await _redisService.SearchSimilarImagesAsync(queryEmbedding, _config.MaxSearchResults);
            metrics.SearchTime = stopwatch.Elapsed - searchStart;
            metrics.ResultsProcessed = searchResults.Count;

            result.Results = searchResults;

            if (searchResults.Count == 0)
            {
                Console.WriteLine("No similar images found");
                result.Summary = "No matching images found. Try a different query or index more images.";
                return result;
            }

            Console.WriteLine($"Found {searchResults.Count} similar images:");
            foreach (var searchResult in searchResults)
            {
                Console.WriteLine($"  - {searchResult.Filename} (Score: {searchResult.Score:F3})");
            }

            // Summarize with Gemma using MCP-aware processing
            var summarizationStart = stopwatch.Elapsed;
            result.Summary = await _ollamaService.SummarizeWithGemmaAsync(query, searchResults);
            metrics.SummarizationTime = stopwatch.Elapsed - summarizationStart;
            metrics.TokensUsed = EstimateTokensUsed(query, searchResults, result.Summary);

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during search: {ex.Message}");
            result.Summary = $"Search failed: {ex.Message}";
            return result;
        }
        finally
        {
            stopwatch.Stop();
            result.Metrics = metrics;
            _lastMetrics = metrics;
        }
    }

    public async Task<McpMetrics> GetLastSearchMetricsAsync()
    {
        return await Task.FromResult(_lastMetrics);
    }

    private int EstimateTokensUsed(string query, List<SearchResult> results, string summary)
    {
        var queryTokens = Math.Max(1, query.Length / 4);
        var resultsTokens = results.Sum(r => Math.Max(1, r.Description.Length / 4));
        var summaryTokens = Math.Max(1, summary.Length / 4);
        
        return queryTokens + resultsTokens + summaryTokens + 200; // Add overhead for prompt structure
    }

    public async Task<(int total, int indexed, int skipped)> GetIndexingStatsAsync(string imagesPath)
    {
        if (!Directory.Exists(imagesPath))
        {
            return (0, 0, 0);
        }

        var imageFiles = Directory.GetFiles(imagesPath, "*.jpg", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(imagesPath, "*.jpeg", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(imagesPath, "*.png", SearchOption.AllDirectories))
            .ToArray();

        var total = imageFiles.Length;
        var indexed = 0;
        var skipped = 0;

        foreach (var imagePath in imageFiles)
        {
            var filename = Path.GetFileName(imagePath);
            var fileInfo = new FileInfo(imagePath);
            var lastModified = fileInfo.LastWriteTime;

            if (_config.EnableDeduplication && 
                await _redisService.IsImageUpToDateAsync(filename, lastModified))
            {
                indexed++;
            }
            else
            {
                skipped++;
            }
        }

        return (total, indexed, skipped);
    }

    public void Dispose()
    {
        _ollamaService?.Dispose();
        _redisService?.Dispose();
    }
}
