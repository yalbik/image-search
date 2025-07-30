namespace OllamaRedisImageSearch.Models;

public class SearchResultWithSummary
{
    public List<SearchResult> Results { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public McpMetrics Metrics { get; set; } = new();
}
