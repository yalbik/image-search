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

public class OllamaModelsResponse
{
    [JsonProperty("models")]
    public List<OllamaModelInfo> Models { get; set; } = new();
}

public class OllamaModelInfo
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonProperty("model")]
    public string Model { get; set; } = string.Empty;
    
    [JsonProperty("modified_at")]
    public string ModifiedAt { get; set; } = string.Empty;
    
    [JsonProperty("size")]
    public long Size { get; set; }
    
    [JsonProperty("digest")]
    public string Digest { get; set; } = string.Empty;
}

public class OllamaPsResponse
{
    [JsonProperty("models")]
    public List<OllamaLoadedModelInfo> Models { get; set; } = new();
}

public class OllamaLoadedModelInfo
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonProperty("model")]
    public string Model { get; set; } = string.Empty;
    
    [JsonProperty("size")]
    public long Size { get; set; }
    
    [JsonProperty("digest")]
    public string Digest { get; set; } = string.Empty;
    
    [JsonProperty("details")]
    public OllamaModelDetails? Details { get; set; }
}

public class OllamaModelDetails
{
    [JsonProperty("parent_model")]
    public string ParentModel { get; set; } = string.Empty;
    
    [JsonProperty("format")]
    public string Format { get; set; } = string.Empty;
    
    [JsonProperty("family")]
    public string Family { get; set; } = string.Empty;
    
    [JsonProperty("families")]
    public List<string> Families { get; set; } = new();
    
    [JsonProperty("parameter_size")]
    public string ParameterSize { get; set; } = string.Empty;
    
    [JsonProperty("quantization_level")]
    public string QuantizationLevel { get; set; } = string.Empty;
}
