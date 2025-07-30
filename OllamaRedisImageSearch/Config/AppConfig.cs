namespace OllamaRedisImageSearch.Config;

public class AppConfig
{
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public string RedisConnectionString { get; set; } = "localhost:6379";
    public int MaxSearchResults { get; set; } = 5;
    public int MaxContextTokens { get; set; } = 6000;
    public string ImagesPath { get; set; } = "..\\my_images";
    public string VectorIndexName { get; set; } = "image_vectors";
    public string KeyPrefix { get; set; } = "image:";
    public int VectorDimension { get; set; } = 768;
    public int MaxConcurrentOperations { get; set; } = 5;
    
    // Model configurations
    public string VisionModel { get; set; } = "llava:34b";
    public string EmbeddingModel { get; set; } = "nomic-embed-text:latest";
    public string SummarizationModel { get; set; } = "gemma3:4b";
    
    // Retry configurations
    public int MaxRetries { get; set; } = 3;
    public int BaseRetryDelayMs { get; set; } = 1000;
    
    // Deduplication configurations
    public bool EnableDeduplication { get; set; } = true;
    public bool ForceReindex { get; set; } = false;
    
    // GPU/VRAM management
    public bool AutoFlushVramOnModelSwitch { get; set; } = true;
}
