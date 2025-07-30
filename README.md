# MCP Image Fun App - Natural Language Image Search

A powerful .NET application that combines **Ollama AI models** with **Redis Stack** to provide semantic image search capabilities using natural language queries. Built with **Model Context Protocol (MCP)** principles for efficient AI model coordination and context window management.

## 🚀 Features

- **Natural Language Search**: Query your images using everyday language like "beautiful woman", "disc golf", or "sunset landscape"
- **AI-Powered Image Analysis**: Uses LLaVA vision model to automatically describe images
- **Semantic Vector Search**: Employs Nomic embeddings for similarity matching
- **Smart Summarization**: Gemma model provides intelligent result summaries
- **Deduplication**: Prevents re-processing unchanged images
- **MCP Context Management**: Optimizes AI model context windows for better performance

## 🛠️ Prerequisites

### Required Software

1. **Docker** - For running Redis Stack
2. **Ollama** - For running AI models locally
3. **.NET 10 Preview** - For running the application

### System Requirements

- **OS**: Windows 10/11, macOS, or Linux
- **RAM**: 16GB+ recommended (AI models are memory-intensive)
- **Storage**: 50GB+ free space for models
- **Network**: Internet connection for initial setup

## 📦 Installation & Setup

### 1. Install Docker

Download and install Docker from [docker.com](https://www.docker.com/products/docker-desktop/)

### 2. Install Ollama

Download and install Ollama from [ollama.ai](https://ollama.ai/)

### 3. Install .NET 10 Preview

Download from [Microsoft .NET](https://dotnet.microsoft.com/download/dotnet/10.0)

### 4. Start Redis Stack

```bash
docker run -d --name redis-stack -p 6379:6379 -p 8001:8001 redis/redis-stack:latest
```

**Verify Redis is running:**
```bash
docker ps
```
You should see `redis-stack` container running.

### 5. Start Ollama Service

```bash
ollama serve
```

Keep this terminal open - Ollama needs to run as a service.

### 6. Pull Required AI Models

Open a **new terminal** and pull the required models:

```bash
# Vision model for image description (choose one based on your hardware)
ollama pull llava:7b      # Faster, less memory (4.7GB)
ollama pull llava:13b     # Better quality (8.0GB)
ollama pull llava:34b     # Best quality, requires powerful hardware (20GB)

# Embedding model for semantic search
ollama pull nomic-embed-text:latest

# Summarization model (choose one)
ollama pull gemma3n:e4b   # Good performance (7.5GB)
ollama pull gemma3:12b    # Better quality (8.1GB)
```

**⚠️ Model Selection:** Update `Config/AppConfig.cs` if you choose different model versions:

```csharp
public string VisionModel { get; set; } = "llava:7b";           // or llava:13b, llava:34b
public string EmbeddingModel { get; set; } = "nomic-embed-text:latest";
public string SummarizationModel { get; set; } = "gemma3n:e4b"; // or gemma3:12b
```

**Verify models are installed:**
```bash
ollama list
```

### 7. Clone and Build the Application

```bash
git clone <your-repo-url>
cd MCP-ImageFunApp
dotnet restore
dotnet build
```

## 🏃‍♂️ Running the Application

### 1. Prepare Your Images

Place your image files (.jpg, .jpeg, .png) in the `my_images` folder:

```
MCP-ImageFunApp/
├── my_images/
│   ├── vacation_photo.jpg
│   ├── family_dinner.png
│   └── sunset_beach.jpeg
└── ...
```

### 2. Start the Application

```bash
cd OllamaRedisImageSearch
dotnet run
```

### 3. Using the Application

The app will perform connectivity checks and show a menu:

```
=== MCP Image Fun App - Natural Language Image Search ===
Integrating Ollama (LLaVA, Nomic, Gemma) with Redis Stack

Checking Redis connectivity...
✅ Redis connection successful!
Checking Ollama connectivity and models...
Found 43 Ollama models installed
Vision model (llava:7b): ✅
Embedding model (nomic-embed-text:latest): ✅
Summarization model (gemma3n:e4b): ✅
✅ Ollama and required models are available!

=== Main Menu ===
1. Index images from folder
2. Search images
3. Show performance metrics
4. Configuration status
5. Exit
```

**First Time Setup:**
1. Choose option `1` to index your images
2. Wait for processing to complete (this takes time on first run)
3. Choose option `2` to search your images

**Example Searches:**
- "beautiful woman"
- "disc golf"
- "people eating dinner"
- "red sports car"
- "sunset over water"

## ⚙️ Configuration

Edit `Config/AppConfig.cs` to customize settings:

```csharp
public class AppConfig
{
    // Service endpoints
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public string RedisConnectionString { get; set; } = "localhost:6379";
    
    // Performance settings
    public int MaxSearchResults { get; set; } = 5;
    public int MaxContextTokens { get; set; } = 6000;  // MCP context window
    public int MaxConcurrentOperations { get; set; } = 5;
    
    // Deduplication
    public bool EnableDeduplication { get; set; } = true;
    public bool ForceReindex { get; set; } = false;
}
```

## 🧪 Testing

Run the test suite:

```bash
cd OllamaRedisImageSearch.Tests
dotnet test
```

**Note:** Integration tests require live Ollama and Redis services.

## 🔧 Troubleshooting

### Common Issues

**❌ "Redis connection failed"**
- Ensure Docker is running: `docker ps`
- Start Redis Stack: `docker run -d --name redis-stack -p 6379:6379 -p 8001:8001 redis/redis-stack:latest`

**❌ "Ollama connection failed"**
- Ensure Ollama is running: `ollama serve`
- Check if service is accessible: `curl http://localhost:11434/api/tags`

**❌ "Required models not found"**
- Pull missing models: `ollama pull <model-name>`
- Verify installation: `ollama list`

**❌ "404 errors during image processing"**
- Check model names in `AppConfig.cs` match exactly what's installed
- Use `ollama list` to see exact model names with versions

**❌ "Out of memory errors"**
- Use smaller models (llava:7b instead of llava:34b)
- Reduce `MaxConcurrentOperations` in config
- Close other applications to free RAM

### Performance Tips

- **For better speed**: Use smaller models (llava:7b, gemma3n:e4b)
- **For better quality**: Use larger models (llava:34b, gemma3:12b)
- **For large image collections**: Increase `MaxConcurrentOperations` if you have sufficient RAM
- **Memory optimization**: Enable deduplication to avoid re-processing images

## 📊 Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   .NET 10 App   │────│  Ollama Models  │    │  Redis Stack    │
│                 │    │                 │    │                 │
│ • Image Search  │    │ • LLaVA (Vision)│    │ • Vector Store  │
│ • MCP Logic     │────│ • Nomic (Embed) │────│ • Search Index  │
│ • Deduplication │    │ • Gemma (Summary│    │ • Persistence   │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

**Key Components:**
- **OllamaService**: AI model integration with retry logic
- **RedisVectorService**: Vector storage and similarity search
- **ImageSearchService**: Orchestrates the search pipeline
- **MCP Context Management**: Optimizes token usage for AI models

## 🚀 Model Context Protocol (MCP)

This application implements MCP principles:

- **Context Window Management**: Automatically optimizes content for model token limits
- **Adaptive Filtering**: Prioritizes highest-scoring results when context is limited
- **Token Estimation**: Predicts and manages token usage across model calls
- **Retry Logic**: Handles model failures gracefully

## 📝 License

MIT License - see LICENSE file for details.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests
5. Submit a pull request

## 🙋‍♂️ Support

For issues and questions:
1. Check the troubleshooting section above
2. Review existing GitHub issues
3. Create a new issue with detailed information about your problem

---

**Enjoy exploring your images with natural language! 🖼️✨**

1. **Vision Processing**: LLaVA analyzes images and generates text descriptions
2. **Embedding Generation**: Nomic converts text to 768-dimensional vectors  
3. **Vector Storage**: Redis Stack stores embeddings with metadata
4. **Semantic Search**: Vector similarity search using cosine distance
5. **MCP-Aware Summarization**: Gemma processes search results with context management

## 🔧 Prerequisites

### 1. Ollama Installation & Models

```bash
# Install Ollama (see https://ollama.ai)
# Then pull required models:
ollama pull llava          # Vision model for image description
ollama pull nomic-embed-text # Text embedding model  
ollama pull gemma          # Text summarization model
```

### 2. Redis Stack (Docker)

```bash
# Run Redis Stack with vector search capabilities
docker run -d --name redis-stack \
  -p 6379:6379 -p 8001:8001 \
  redis/redis-stack:latest
```

### 3. .NET 8 SDK

Download from [Microsoft .NET](https://dotnet.microsoft.com/download)

## 🚀 Getting Started

### 1. Clone and Build

```bash
git clone <your-repo>
cd MCP-ImageFunApp
dotnet restore
dotnet build
```

### 2. Prepare Images

```bash
# Create images directory
mkdir my_images

# Add your JPG/PNG images to this folder
# The app will process these for searching
```

### 3. Run the Application

```bash
cd OllamaRedisImageSearch
dotnet run
```

## 🎮 Usage

The application provides an interactive menu:

1. **Index Images**: Process images from `my_images` folder
   - LLaVA describes each image
   - Nomic generates embeddings  
   - Store in Redis with metadata

2. **Search Images**: Natural language queries
   - "people walking in a park"
   - "red cars at sunset" 
   - "dogs playing outside"

3. **Performance Metrics**: View MCP processing times
4. **Configuration Status**: Check system settings

## 🧠 MCP (Model Context Protocol) Focus

This project demonstrates MCP concepts through:

### Context Window Management
```csharp
// Adaptive context selection based on token limits
private List<SearchResult> OptimizeResultsForContext(List<SearchResult> results)
{
    int estimatedTokens = basePromptTokens;
    var optimizedResults = new List<SearchResult>();

    foreach (var result in results.OrderByDescending(r => r.Score))
    {
        int resultTokens = EstimateTokens(result.Description) + 50;
        
        if (estimatedTokens + resultTokens <= _config.MaxContextTokens)
        {
            optimizedResults.Add(result);
            estimatedTokens += resultTokens;
        }
        else break; // Stop before context overflow
    }
    
    return optimizedResults;
}
```

### Token Estimation & Management
- Rough token counting (~4 chars per token)
- Dynamic result filtering based on available context
- Performance metrics tracking

### Chunked Processing
- Handle large result sets by processing in chunks
- Synthesize final answers from partial summaries
- Prevent context window overflow

## 📊 Performance Metrics

The app tracks MCP-relevant metrics:

- **Embedding Time**: Query → vector conversion
- **Search Time**: Vector similarity search  
- **Summarization Time**: Context processing by Gemma
- **Tokens Used**: Estimated context consumption
- **Results Processed**: Number of images analyzed

## ⚙️ Configuration

Key settings in `AppConfig.cs`:

```csharp
public class AppConfig
{
    public int MaxContextTokens { get; set; } = 6000;    // MCP limit
    public int MaxSearchResults { get; set; } = 5;       // Results to process
    public int VectorDimension { get; set; } = 768;      // Nomic embedding size
    public int MaxConcurrentOperations { get; set; } = 5; // Parallel processing
}
```

## 🔍 Example Queries

Try these natural language searches:

- **Objects**: "red cars", "wooden chairs", "laptops on desk"
- **People**: "children playing", "people in business attire"  
- **Scenes**: "sunset landscape", "city at night", "beach waves"
- **Activities**: "cooking in kitchen", "reading books", "dogs running"

## 🎯 MCP Learning Outcomes

1. **Context Management**: How to handle limited model context windows
2. **Token Budgeting**: Estimating and managing token usage
3. **Adaptive Processing**: Dynamically adjusting input based on constraints
4. **Performance Monitoring**: Tracking context-related metrics
5. **Multi-Model Coordination**: Passing context between different AI models

## 🛠️ Troubleshooting

### Common Issues

1. **"Failed to initialize services"**
   - Check Ollama is running: `ollama list`
   - Verify Redis Stack: `docker ps`

2. **"No similar images found"** 
   - Ensure images are indexed first
   - Try broader search terms

3. **Slow Performance**
   - Reduce `MaxSearchResults` in config
   - Lower `MaxConcurrentOperations`
   - Use smaller images (< 2MB recommended)

### Health Checks

```bash
# Test Ollama
curl http://localhost:11434/api/tags

# Test Redis
redis-cli ping

# Check models
ollama list
```

## 📚 Project Structure

```
MCP-ImageFunApp/
├── OllamaRedisImageSearch/
│   ├── Config/
│   │   └── AppConfig.cs           # Configuration settings
│   ├── Models/
│   │   ├── OllamaModels.cs       # API request/response models
│   │   └── SearchModels.cs       # Core data models
│   ├── Services/
│   │   ├── OllamaService.cs      # Ollama AI integration
│   │   ├── RedisVectorService.cs # Redis vector operations
│   │   └── ImageSearchService.cs # Main orchestration
│   └── Program.cs                # Console interface
├── my_images/                    # Your image files
└── README.md
```

## 🔮 Future Enhancements

- **Advanced MCP Strategies**: Hierarchical context compression
- **Streaming Responses**: Real-time result processing
- **Web Interface**: Replace console with web UI
- **Multiple Vector Indices**: Separate indices for different image types
- **Semantic Caching**: Cache frequently used embeddings

## 📄 License

MIT License - Feel free to experiment and learn!

---

**Happy Learning!** This project combines practical AI integration with important MCP concepts for managing model context windows effectively.
