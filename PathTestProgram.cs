using OllamaRedisImageSearch.Config;
using OllamaRedisImageSearch.Services;

class PathTestProgram
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Testing Path Resolution ===");
        
        var config = new AppConfig();
        Console.WriteLine($"Config ImagesPath: {config.ImagesPath}");
        Console.WriteLine($"Current Directory: {Directory.GetCurrentDirectory()}");
        
        var redisService = new RedisVectorService(config);
        var imageSearchService = new ImageSearchService(redisService, null);
        
        // Test searching to see the debug output
        try
        {
            Console.WriteLine("Performing search...");
            var results = await redisService.SearchSimilarImagesAsync(new float[768], 3);
            Console.WriteLine($"Found {results.Count} results");
            
            foreach (var result in results)
            {
                Console.WriteLine($"Result: {result.Filename} -> {result.ImagePath}");
                Console.WriteLine($"File exists: {File.Exists(result.ImagePath)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during search: {ex.Message}");
        }
    }
}
