# MCP Image Fun App

A powerful .NET application that combines AI vision models with Redis vector search to provide intelligent image analysis and semantic search capabilities. Features both console and GUI interfaces for flexible usage.

## Features

- **AI-Powered Image Analysis**: Uses Ollama vision models (llava:34b) to generate detailed descriptions
- **Semantic Search**: Vector-based similarity search using Redis with nomic-embed-text embeddings
- **Smart Summarization**: Contextual summaries with gemma3:4b model
- **Dual Interface**: Console application and Windows Forms GUI
- **VRAM Management**: Automatic GPU memory optimization for model switching
- **Deduplication**: Intelligent handling of duplicate images
- **Batch Processing**: Concurrent operations for improved performance

## Prerequisites

- **Windows 10/11** (for GUI application)
- **.NET 10** or later
- **Ollama** with required models
- **Redis server**

## Installation

### 1. Install Dependencies

**Ollama Models:**
```bash
ollama pull llava:34b
ollama pull nomic-embed-text:latest
ollama pull gemma3:4b
```

**Redis:**
- Install Redis for Windows or use Docker:
```bash
docker run -d -p 6379:6379 redis:latest
```

### 2. Clone and Build

```bash
git clone <repository-url>
cd MCP-ImageFunApp
dotnet build
```

## Configuration

The application uses `AppConfig.cs` for configuration. Key settings:

```csharp
// Server connections
OllamaBaseUrl = "http://localhost:11434"
RedisConnectionString = "localhost:6379"

// Models
VisionModel = "llava:34b"
EmbeddingModel = "nomic-embed-text:latest"
SummarizationModel = "gemma3:4b"

// Paths and limits
ImagesPath = "..\\my_images"
MaxSearchResults = 5
MaxConcurrentOperations = 5
```

## Usage

### Console Application

```bash
cd OllamaRedisImageSearch
dotnet run
```

**Available Commands:**
- `index` - Process and index all images in the configured directory
- `search <query>` - Search for images using natural language
- `clear` - Clear the Redis vector index
- `health` - Check system component status
- `exit` - Quit the application

**Examples:**
```
> index
> search "cats playing in a garden"
> search "blue cars"
> health
```

### GUI Application

```bash
cd OllamaRedisImageSearch.GUI
dotnet run
```

The GUI provides:
- **Index Images**: Button to process your image collection
- **Search Interface**: Text input for natural language queries
- **Results Display**: Visual grid showing matching images with descriptions
- **Status Updates**: Real-time progress and system information

## Image Setup

1. Create an `my_images` folder in the project root
2. Add your images (supports common formats: JPG, PNG, BMP, GIF)
3. Run indexing to process and vectorize your collection

## Performance Tips

- **VRAM Management**: Enable `AutoFlushVramOnModelSwitch` for systems with limited GPU memory
- **Batch Size**: Adjust `MaxConcurrentOperations` based on your hardware (1-5 recommended)
- **Model Selection**: Use smaller models like `llava:13b` if experiencing memory issues
- **Redis Optimization**: Consider Redis persistence settings for large image collections

## Troubleshooting

**Common Issues:**

1. **Ollama Connection Failed**
   - Verify Ollama is running: `ollama list`
   - Check URL in configuration

2. **Redis Connection Error**
   - Confirm Redis server is running
   - Test connection: `redis-cli ping`

3. **Out of Memory Errors**
   - Enable VRAM auto-flush
   - Reduce concurrent operations
   - Use smaller vision models

4. **No Images Found**
   - Verify `ImagesPath` configuration
   - Check image file permissions
   - Ensure supported image formats

**Health Check:**
Run the health command to diagnose system status:
```
> health
```

## Architecture

- **Vision Processing**: Ollama llava models analyze image content
- **Embeddings**: Text descriptions converted to vectors using nomic-embed-text
- **Vector Storage**: Redis with RediSearch for similarity matching
- **Summarization**: Context-aware summaries using gemma3 models
- **Interfaces**: Shared core logic with console and GUI frontends

## Development

**Project Structure:**
```
MCP-ImageFunApp/
├── OllamaRedisImageSearch/          # Console application
├── OllamaRedisImageSearch.GUI/      # Windows Forms GUI
├── my_images/                       # Image collection directory
└── README.md                        # This file
```

**Building:**
```bash
dotnet build                         # Build entire solution
dotnet run --project OllamaRedisImageSearch      # Run console
dotnet run --project OllamaRedisImageSearch.GUI  # Run GUI
```

## License

This project is available under standard software licensing terms.
