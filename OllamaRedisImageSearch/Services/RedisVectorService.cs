using System.Buffers;
using NRedisStack.RedisStackCommands;
using NRedisStack.Search;
using NRedisStack.Search.Literals.Enums;
using OllamaRedisImageSearch.Config;
using StackExchange.Redis;
using SearchResult = OllamaRedisImageSearch.Models.SearchResult;

namespace OllamaRedisImageSearch.Services;

public interface IRedisVectorService : IDisposable
{
    Task<bool> EnsureRedisIndexExistsAsync();
    Task<bool> StoreImageEmbeddingAsync(string filename, string description, float[] embedding);
    Task<bool> StoreImageEmbeddingAsync(string filename, string description, float[] embedding, DateTime lastModified);
    Task<List<SearchResult>> SearchSimilarImagesAsync(float[] queryEmbedding, int topK = 5);
    Task<bool> DeleteImageAsync(string filename);
    Task<int> GetIndexedImageCountAsync();
    Task<bool> ImageExistsAsync(string filename);
    Task<bool> IsImageUpToDateAsync(string filename, DateTime fileLastModified);
}

public class RedisVectorService : IRedisVectorService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly AppConfig _config;

    public RedisVectorService(AppConfig config)
    {
        _config = config;
        _redis = ConnectionMultiplexer.Connect(_config.RedisConnectionString);
        _database = _redis.GetDatabase();
    }

    public async Task<bool> EnsureRedisIndexExistsAsync()
    {
        try
        {
            var ft = _database.FT();
            
            // Check if index already exists
            try
            {
                await ft.InfoAsync(_config.VectorIndexName);
                Console.WriteLine($"Index '{_config.VectorIndexName}' already exists.");
                return true;
            }
            catch (RedisException)
            {
                // Index doesn't exist, create it
            }

            // Create the index
            var schema = new Schema()
                .AddTextField("filename", 1.0)
                .AddTextField("description", 1.0)
                .AddVectorField("embedding", 
                    Schema.VectorField.VectorAlgo.FLAT,
                    new Dictionary<string, object>
                    {
                        ["TYPE"] = "FLOAT32",
                        ["DIM"] = _config.VectorDimension,
                        ["DISTANCE_METRIC"] = "COSINE"
                    });

            var indexParams = new FTCreateParams()
                .On(IndexDataType.HASH)
                .Prefix(_config.KeyPrefix);

            await ft.CreateAsync(_config.VectorIndexName, indexParams, schema);
            Console.WriteLine($"Successfully created index '{_config.VectorIndexName}'");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error ensuring Redis index exists: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> StoreImageEmbeddingAsync(string filename, string description, float[] embedding)
    {
        try
        {
            var key = $"{_config.KeyPrefix}{filename}";
            var embeddingBytes = ConvertVectorToBytes(embedding);
            
            var hash = new HashEntry[]
            {
                new("filename", filename),
                new("description", description),
                new("embedding", embeddingBytes),
                new("indexed_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await _database.HashSetAsync(key, hash);
            Console.WriteLine($"Stored embedding for: {filename}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error storing embedding for {filename}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> StoreImageEmbeddingAsync(string filename, string description, float[] embedding, DateTime lastModified)
    {
        try
        {
            var key = $"{_config.KeyPrefix}{filename}";
            var embeddingBytes = ConvertVectorToBytes(embedding);
            
            var hash = new HashEntry[]
            {
                new("filename", filename),
                new("description", description),
                new("embedding", embeddingBytes),
                new("indexed_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                new("file_modified_at", ((DateTimeOffset)lastModified).ToUnixTimeSeconds())
            };

            await _database.HashSetAsync(key, hash);
            Console.WriteLine($"Stored embedding for: {filename}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error storing embedding for {filename}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ImageExistsAsync(string filename)
    {
        try
        {
            var key = $"{_config.KeyPrefix}{filename}";
            return await _database.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking if image exists {filename}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> IsImageUpToDateAsync(string filename, DateTime fileLastModified)
    {
        try
        {
            var key = $"{_config.KeyPrefix}{filename}";
            
            // Check if key exists first
            if (!await _database.KeyExistsAsync(key))
                return false;

            // Get the stored file modification time
            var storedModifiedTime = await _database.HashGetAsync(key, "file_modified_at");
            
            if (!storedModifiedTime.HasValue)
            {
                // If no modification time stored, consider it outdated
                return false;
            }

            var storedModifiedUnix = (long)storedModifiedTime;
            var storedModifiedDateTime = DateTimeOffset.FromUnixTimeSeconds(storedModifiedUnix).DateTime;
            
            // Compare with a small tolerance (1 second) to account for file system precision
            var timeDifference = Math.Abs((fileLastModified - storedModifiedDateTime).TotalSeconds);
            return timeDifference <= 1.0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking if image is up to date {filename}: {ex.Message}");
            return false;
        }
    }

    public async Task<List<SearchResult>> SearchSimilarImagesAsync(float[] queryEmbedding, int topK = 5)
    {
        try
        {
            var ft = _database.FT();
            var queryBytes = ConvertVectorToBytes(queryEmbedding);
            
            var query = new Query($"*=>[KNN {topK} @embedding $query_vector AS vector_score]")
                .AddParam("query_vector", queryBytes)
                .SetSortBy("vector_score")
                .Dialect(2)
                .Limit(0, topK);

            var searchResult = await ft.SearchAsync(_config.VectorIndexName, query);
            var results = new List<SearchResult>();

            foreach (var document in searchResult.Documents)
            {
                var filename = document["filename"].ToString();
                var description = document["description"].ToString();
                var scoreStr = document["vector_score"].ToString();
                
                if (float.TryParse(scoreStr, out float score))
                {
                    var imagePath = GetAbsoluteImagePath(filename);
                    results.Add(new SearchResult
                    {
                        Filename = filename,
                        Description = description,
                        Score = 1.0f - score, // Convert distance to similarity
                        ImagePath = imagePath
                    });
                }
            }

            return results.OrderByDescending(r => r.Score).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error searching similar images: {ex.Message}");
            return new List<SearchResult>();
        }
    }

    public async Task<bool> DeleteImageAsync(string filename)
    {
        try
        {
            var key = $"{_config.KeyPrefix}{filename}";
            var result = await _database.KeyDeleteAsync(key);
            
            if (result)
            {
                Console.WriteLine($"Deleted embedding for: {filename}");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting image {filename}: {ex.Message}");
            return false;
        }
    }

    public async Task<int> GetIndexedImageCountAsync()
    {
        try
        {
            var ft = _database.FT();
            var info = await ft.InfoAsync(_config.VectorIndexName);
            
            // Parse the index info to get document count
            // NRedisStack returns InfoResult which has different access patterns
            var infoStr = info?.ToString();
            if (!string.IsNullOrEmpty(infoStr) && infoStr.Contains("num_docs"))
            {
                // Parse the info string to extract document count
                var lines = infoStr.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("num_docs"))
                    {
                        var parts = line.Split();
                        for (int i = 0; i < parts.Length - 1; i++)
                        {
                            if (parts[i] == "num_docs" && int.TryParse(parts[i + 1], out int count))
                            {
                                return count;
                            }
                        }
                    }
                }
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting indexed image count: {ex.Message}");
            return 0;
        }
    }

    private byte[] ConvertVectorToBytes(float[] vector)
    {
        var bytes = ArrayPool<byte>.Shared.Rent(vector.Length * sizeof(float));
        try
        {
            Buffer.BlockCopy(vector, 0, bytes, 0, vector.Length * sizeof(float));
            var result = new byte[vector.Length * sizeof(float)];
            Array.Copy(bytes, result, result.Length);
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    private float[] ConvertBytesToVector(byte[] bytes)
    {
        var vector = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
    }

    private string GetAbsoluteImagePath(string filename)
    {
        // If filename is already an absolute path, return it
        if (Path.IsPathRooted(filename))
        {
            return filename;
        }
        
        // Find the solution root directory
        var currentDirectory = Directory.GetCurrentDirectory();
        var solutionRoot = FindSolutionRoot(currentDirectory) ?? currentDirectory;
        
        // Construct path directly from solution root + my_images + filename
        var absolutePath = Path.Combine(solutionRoot, "my_images", filename);
        
        return absolutePath;
    }
    
    private string? FindSolutionRoot(string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        
        while (directory != null)
        {
            // Look for .sln file or my_images folder
            if (directory.GetFiles("*.sln").Length > 0 || 
                directory.GetDirectories("my_images").Length > 0)
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        
        return null;
    }

    public void Dispose()
    {
        _redis?.Dispose();
    }
}
