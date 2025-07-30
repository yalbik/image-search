namespace OllamaRedisImageSearch.Models;

public class SearchResult
{
    public string Filename { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public float Score { get; set; }
    public string ImagePath { get; set; } = string.Empty;
}

public class McpMetrics
{
    public TimeSpan EmbeddingTime { get; set; }
    public TimeSpan SearchTime { get; set; }
    public TimeSpan SummarizationTime { get; set; }
    public int TokensUsed { get; set; }
    public int ResultsProcessed { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ImageMetadata
{
    public string Filename { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public byte[] Embedding { get; set; } = Array.Empty<byte>();
    public long FileSize { get; set; }
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
}
