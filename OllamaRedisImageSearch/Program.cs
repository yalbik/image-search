using OllamaRedisImageSearch.Config;
using OllamaRedisImageSearch.Services;
using StackExchange.Redis;
using Newtonsoft.Json;

namespace OllamaRedisImageSearch;

class Program
{
    private static AppConfig _config = new();
    
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== MCP Image Fun App - Natural Language Image Search ===");
        Console.WriteLine("Integrating Ollama (LLaVA, Nomic, Gemma) with Redis Stack\n");

        // Check Redis connectivity first
        Console.WriteLine("Checking Redis connectivity...");
        if (!await CheckRedisConnectivityAsync(_config))
        {
            Console.WriteLine("❌ Redis connection failed. Please ensure Redis Stack is running.");
            Console.WriteLine("You can start Redis Stack with: docker run -d --name redis-stack -p 6379:6379 -p 8001:8001 redis/redis-stack:latest");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return;
        }
        Console.WriteLine("✅ Redis connection successful!");

        // Check Ollama connectivity and models
        Console.WriteLine("Checking Ollama connectivity and models...");
        var ollamaCheck = await CheckOllamaConnectivityAsync(_config);
        if (!ollamaCheck.isConnected)
        {
            Console.WriteLine("❌ Ollama connection failed. Please ensure Ollama is running.");
            Console.WriteLine("You can start Ollama with: ollama serve");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return;
        }
        
        if (!ollamaCheck.hasRequiredModels)
        {
            Console.WriteLine("⚠️  Warning: Some required models are missing. Please install them:");
            if (!ollamaCheck.hasVisionModel)
                Console.WriteLine($"  ollama pull {_config.VisionModel}");
            if (!ollamaCheck.hasEmbeddingModel)
                Console.WriteLine($"  ollama pull {_config.EmbeddingModel}");
            if (!ollamaCheck.hasSummarizationModel)
                Console.WriteLine($"  ollama pull {_config.SummarizationModel}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return;
        }
        Console.WriteLine("✅ Ollama and required models are available!");

        // Initialize services
        using var ollamaService = new OllamaService(_config);
        using var redisService = new RedisVectorService(_config);
        using var imageSearchService = new ImageSearchService(ollamaService, redisService, _config);

        // Flush VRAM at startup to ensure clean GPU state
        if (_config.AutoFlushVramOnModelSwitch)
        {
            Console.WriteLine("Flushing GPU memory at startup...");
            try
            {
                await ollamaService.UnloadAllModelsAsync();
                Console.WriteLine("✅ VRAM flushed at application startup");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Warning: Could not flush VRAM at startup: {ex.Message}");
            }
        }

        // Initialize the search service
        var initialized = await imageSearchService.InitializeAsync();
        if (!initialized)
        {
            Console.WriteLine("Failed to initialize services. Please check your Ollama and Redis connections.");
            return;
        }

        // Check if images directory exists
        if (!Directory.Exists(_config.ImagesPath))
        {
            Console.WriteLine($"Creating images directory: {_config.ImagesPath}");
            Directory.CreateDirectory(_config.ImagesPath);
            Console.WriteLine("Please add some .jpg/.png images to the 'my_images' folder and restart the application.");
            return;
        }

        // Show main menu
        await ShowMainMenuAsync(imageSearchService);
    }

    private static async Task ShowMainMenuAsync(IImageSearchService imageSearchService)
    {
        while (true)
        {
            Console.WriteLine("\n=== Main Menu ===");
            Console.WriteLine("1. Index images from folder");
            Console.WriteLine("2. Search images");
            Console.WriteLine("3. Show performance metrics");
            Console.WriteLine("4. Configuration status");
            Console.WriteLine("5. Exit");
            Console.Write("\nSelect an option (1-5): ");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await IndexImagesAsync(imageSearchService);
                    break;
                case "2":
                    await SearchImagesAsync(imageSearchService);
                    break;
                case "3":
                    await ShowMetricsAsync(imageSearchService);
                    break;
                case "4":
                    ShowConfigurationStatus();
                    break;
                case "5":
                    Console.WriteLine("Goodbye!");
                    return;
                default:
                    Console.WriteLine("Invalid option. Please try again.");
                    break;
            }
        }
    }

    private static async Task IndexImagesAsync(IImageSearchService imageSearchService)
    {
        Console.WriteLine($"\n=== Indexing Images from {_config.ImagesPath} ===");
        
        var imageFiles = Directory.GetFiles(_config.ImagesPath, "*.jpg", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(_config.ImagesPath, "*.jpeg", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(_config.ImagesPath, "*.png", SearchOption.AllDirectories))
            .ToArray();

        if (imageFiles.Length == 0)
        {
            Console.WriteLine("No images found. Please add some .jpg/.png files to the images folder.");
            return;
        }

        Console.WriteLine($"Found {imageFiles.Length} image(s). This may take a while...");
        
        // Show indexing statistics
        var stats = await imageSearchService.GetIndexingStatsAsync(_config.ImagesPath);
        Console.WriteLine($"Indexing Status: {stats.indexed} already indexed, {stats.skipped} need processing");
        
        if (stats.skipped == 0)
        {
            Console.WriteLine("All images are already indexed and up-to-date!");
            return;
        }
        
        Console.Write($"Proceed with indexing {stats.skipped} image(s)? (y/n): ");
        
        var confirm = Console.ReadLine();
        if (confirm?.ToLower() != "y")
        {
            Console.WriteLine("Indexing cancelled.");
            return;
        }

        var startTime = DateTime.UtcNow;
        var success = await imageSearchService.IndexImagesAsync(_config.ImagesPath);
        var duration = DateTime.UtcNow - startTime;

        if (success)
        {
            Console.WriteLine($"Indexing completed successfully in {duration.TotalSeconds:F1} seconds!");
        }
        else
        {
            Console.WriteLine("Indexing failed. Please check your Ollama and Redis connections.");
        }
    }

    private static async Task SearchImagesAsync(IImageSearchService imageSearchService)
    {
        Console.WriteLine("\n=== Natural Language Image Search ===");
        Console.WriteLine("Enter a search query (e.g., 'people walking in a park', 'red cars', 'sunset landscape')");
        Console.Write("Search query: ");
        
        var query = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(query))
        {
            Console.WriteLine("Please enter a valid search query.");
            return;
        }

        Console.WriteLine("\nSearching... (This involves embedding generation, vector search, and AI summarization)");
        
        var results = await imageSearchService.SearchAsync(query);
        
        if (results.Count == 0)
        {
            Console.WriteLine("No matching images found. Try a different query or index more images.");
        }
        else
        {
            Console.WriteLine($"\nSearch completed! Found {results.Count} relevant image(s).");
            Console.WriteLine("Check the console output above for the AI-generated summary.");
        }
    }

    private static async Task ShowMetricsAsync(IImageSearchService imageSearchService)
    {
        Console.WriteLine("\n=== Performance Metrics ===");
        
        var metrics = await imageSearchService.GetLastSearchMetricsAsync();
        
        if (metrics.Timestamp == default)
        {
            Console.WriteLine("No search metrics available. Perform a search first.");
            return;
        }

        Console.WriteLine($"Last Search Timestamp: {metrics.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"Embedding Generation: {metrics.EmbeddingTime.TotalMilliseconds:F0}ms");
        Console.WriteLine($"Vector Search: {metrics.SearchTime.TotalMilliseconds:F0}ms");
        Console.WriteLine($"AI Summarization: {metrics.SummarizationTime.TotalMilliseconds:F0}ms");
        Console.WriteLine($"Results Processed: {metrics.ResultsProcessed}");
        Console.WriteLine($"Estimated Tokens Used: {metrics.TokensUsed}");
        
        var totalTime = metrics.EmbeddingTime + metrics.SearchTime + metrics.SummarizationTime;
        Console.WriteLine($"Total Processing Time: {totalTime.TotalMilliseconds:F0}ms");
    }

    private static void ShowConfigurationStatus()
    {
        Console.WriteLine("\n=== Configuration Status ===");
        Console.WriteLine($"Ollama Base URL: {_config.OllamaBaseUrl}");
        Console.WriteLine($"Redis Connection: {_config.RedisConnectionString}");
        Console.WriteLine($"Images Path: {_config.ImagesPath}");
        Console.WriteLine($"Max Search Results: {_config.MaxSearchResults}");
        Console.WriteLine($"Max Context Tokens: {_config.MaxContextTokens}");
        Console.WriteLine($"Vector Dimension: {_config.VectorDimension}");
        Console.WriteLine($"Max Concurrent Operations: {_config.MaxConcurrentOperations}");
        Console.WriteLine("\n=== Model Configuration ===");
        Console.WriteLine($"Vision Model (LLaVA): {_config.VisionModel}");
        Console.WriteLine($"Embedding Model (Nomic): {_config.EmbeddingModel}");
        Console.WriteLine($"Summarization Model (Gemma): {_config.SummarizationModel}");
        
        Console.WriteLine("\n=== MCP (Model Context Protocol) Settings ===");
        Console.WriteLine($"Max Context Tokens: {_config.MaxContextTokens}");
        Console.WriteLine("These settings help manage context window limits when sending");
        Console.WriteLine("search results to the Gemma model for summarization.");
        
        Console.WriteLine("\n=== Deduplication Settings ===");
        Console.WriteLine($"Enable Deduplication: {_config.EnableDeduplication}");
        Console.WriteLine($"Force Reindex: {_config.ForceReindex}");
        Console.WriteLine("Deduplication prevents re-processing unchanged images.");
    }

    private static async Task<bool> CheckRedisConnectivityAsync(AppConfig config)
    {
        IConnectionMultiplexer? redis = null;
        try
        {
            // Set a short timeout for the connection test
            var options = ConfigurationOptions.Parse(config.RedisConnectionString);
            options.ConnectTimeout = 3000; // 3 seconds
            options.SyncTimeout = 2000; // 2 seconds
            options.AbortOnConnectFail = false;

            redis = await ConnectionMultiplexer.ConnectAsync(options);
            
            if (!redis.IsConnected)
            {
                return false;
            }

            // Test basic connectivity with a simple ping
            var database = redis.GetDatabase();
            var pong = await database.PingAsync();
            
            // Test if Redis Stack (with search capabilities) is available
            try
            {
                var server = redis.GetServer(redis.GetEndPoints().First());
                var info = await server.InfoAsync("modules");
                var hasSearchModule = info.Any(line => line.Key.Contains("search", StringComparison.OrdinalIgnoreCase));
                
                if (!hasSearchModule)
                {
                    Console.WriteLine("⚠️  Warning: Redis is running but Redis Stack (search module) is not detected.");
                    Console.WriteLine("Please use Redis Stack instead of standard Redis for vector search capabilities.");
                }
            }
            catch
            {
                // If we can't check modules, assume it's fine and continue
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis connection error: {ex.Message}");
            return false;
        }
        finally
        {
            redis?.Dispose();
        }
    }

    private static async Task<(bool isConnected, bool hasRequiredModels, bool hasVisionModel, bool hasEmbeddingModel, bool hasSummarizationModel)> CheckOllamaConnectivityAsync(AppConfig config)
    {
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(5);

        try
        {
            // Test basic connectivity
            var response = await client.GetAsync($"{config.OllamaBaseUrl}/api/tags");
            if (!response.IsSuccessStatusCode)
            {
                return (false, false, false, false, false);
            }

            var content = await response.Content.ReadAsStringAsync();
            var modelsData = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(content);
            
            var availableModels = new List<string>();
            if (modelsData?.models != null)
            {
                foreach (var model in modelsData.models)
                {
                    availableModels.Add(model.name.ToString());
                }
            }

            Console.WriteLine($"Found {availableModels.Count} Ollama models installed");

            // Check for required models with more flexible matching
            var hasVision = availableModels.Any(m => m.StartsWith(config.VisionModel, StringComparison.OrdinalIgnoreCase));
            var hasEmbedding = availableModels.Any(m => m.StartsWith(config.EmbeddingModel, StringComparison.OrdinalIgnoreCase));
            var hasSummarization = availableModels.Any(m => m.StartsWith(config.SummarizationModel, StringComparison.OrdinalIgnoreCase));
            
            // If exact match fails, check for similar models
            if (!hasVision)
            {
                hasVision = availableModels.Any(m => m.Contains("llava", StringComparison.OrdinalIgnoreCase));
            }
            if (!hasSummarization)
            {
                hasSummarization = availableModels.Any(m => m.Contains("gemma", StringComparison.OrdinalIgnoreCase));
            }

            // Print detailed model status
            Console.WriteLine($"Vision model ({config.VisionModel}): {(hasVision ? "✅" : "❌")}");
            Console.WriteLine($"Embedding model ({config.EmbeddingModel}): {(hasEmbedding ? "✅" : "❌")}");
            Console.WriteLine($"Summarization model ({config.SummarizationModel}): {(hasSummarization ? "✅" : "❌")}");

            var hasAllModels = hasVision && hasEmbedding && hasSummarization;

            return (true, hasAllModels, hasVision, hasEmbedding, hasSummarization);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ollama connection error: {ex.Message}");
            return (false, false, false, false, false);
        }
    }
}
