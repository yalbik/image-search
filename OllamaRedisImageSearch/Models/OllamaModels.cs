using Newtonsoft.Json;

namespace OllamaRedisImageSearch.Models;

// Ollama API Request/Response Models
public class OllamaChatRequest
{
    [JsonProperty("model")]
    public string Model { get; set; } = string.Empty;
    
    [JsonProperty("messages")]
    public List<OllamaMessage> Messages { get; set; } = new();
    
    [JsonProperty("stream")]
    public bool Stream { get; set; } = false;
}

public class OllamaMessage
{
    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;
    
    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;
    
    [JsonProperty("images")]
    public List<string>? Images { get; set; }
}

public class OllamaChatResponse
{
    [JsonProperty("message")]
    public OllamaMessage? Message { get; set; }
    
    [JsonProperty("done")]
    public bool Done { get; set; }
}

public class OllamaEmbeddingRequest
{
    [JsonProperty("model")]
    public string Model { get; set; } = string.Empty;
    
    [JsonProperty("prompt")]
    public string Prompt { get; set; } = string.Empty;
}

public class OllamaEmbeddingResponse
{
    [JsonProperty("embedding")]
    public float[] Embedding { get; set; } = Array.Empty<float>();
}
