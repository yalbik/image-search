using System;
using System.IO;
using OllamaRedisImageSearch.Services;
using OllamaRedisImageSearch.Config;

class TestPathResolution
{
    static void Main()
    {
        var config = new AppConfig();
        var redisService = new RedisVectorService(config);
        
        // Test the GetAbsoluteImagePath method via reflection
        var method = typeof(RedisVectorService).GetMethod("GetAbsoluteImagePath", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (method != null)
        {
            var testFileName = "PXL_20250204_041240755.jpg";
            var result = method.Invoke(redisService, new object[] { testFileName });
            
            Console.WriteLine($"Test filename: {testFileName}");
            Console.WriteLine($"Resolved path: {result}");
            Console.WriteLine($"File exists: {File.Exists(result?.ToString())}");
            
            // Also test current directory
            Console.WriteLine($"Current directory: {Directory.GetCurrentDirectory()}");
            Console.WriteLine($"Images path from config: {config.ImagesPath}");
        }
        else
        {
            Console.WriteLine("Could not find GetAbsoluteImagePath method");
        }
    }
}
