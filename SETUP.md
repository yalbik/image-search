# Setup Scripts for MCP Image Fun App

## Quick Setup (Windows PowerShell)

### 1. Install Ollama Models
```powershell
# Make sure Ollama is installed first: https://ollama.ai
ollama pull llava
ollama pull nomic-embed-text  
ollama pull gemma
```

### 2. Start Redis Stack
```powershell
# Using Docker Desktop (recommended)
docker run -d --name redis-stack -p 6379:6379 -p 8001:8001 redis/redis-stack:latest

# Alternative: Redis Stack on Windows
# Download from: https://redis.io/download
```

### 3. Verify Services
```powershell
# Test Ollama
curl http://localhost:11434/api/tags

# Test Redis  
docker exec redis-stack redis-cli ping
```

### 4. Build and Run
```powershell
cd OllamaRedisImageSearch
dotnet run
```

## Sample Images Setup

Create some test images in the `my_images` folder:

```powershell
# Create directory
New-Item -ItemType Directory -Force -Path "my_images"

# Copy some sample images (update paths as needed)
# Copy-Item "C:\Users\YourName\Pictures\*.jpg" -Destination "my_images\"
```

## Environment Variables (Optional)

Set these environment variables for custom configuration:

```powershell
$env:OLLAMA_BASE_URL = "http://localhost:11434"
$env:REDIS_CONNECTION_STRING = "localhost:6379"  
$env:IMAGES_PATH = "my_images"
$env:MAX_SEARCH_RESULTS = "5"
$env:MAX_CONTEXT_TOKENS = "6000"
```

## Health Check Script

```powershell
# health-check.ps1
Write-Host "=== MCP Image Fun App Health Check ===" -ForegroundColor Green

# Check Ollama
try {
    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -Method Get
    Write-Host "✓ Ollama is running" -ForegroundColor Green
    Write-Host "  Available models:" -ForegroundColor Yellow
    $response.models | ForEach-Object { Write-Host "    - $($_.name)" -ForegroundColor Gray }
} catch {
    Write-Host "✗ Ollama is not running or not accessible" -ForegroundColor Red
}

# Check Redis
try {
    $redisCheck = docker exec redis-stack redis-cli ping 2>$null
    if ($redisCheck -eq "PONG") {
        Write-Host "✓ Redis Stack is running" -ForegroundColor Green
    } else {
        Write-Host "✗ Redis Stack is not responding" -ForegroundColor Red
    }
} catch {
    Write-Host "✗ Redis Stack container not found" -ForegroundColor Red
}

# Check images directory
if (Test-Path "my_images") {
    $imageCount = (Get-ChildItem "my_images" -Include *.jpg,*.jpeg,*.png -Recurse).Count
    Write-Host "✓ Images directory exists ($imageCount images)" -ForegroundColor Green
} else {
    Write-Host "✗ Images directory not found" -ForegroundColor Red
}

Write-Host "`n=== Setup Complete! ===" -ForegroundColor Green
```

Save this as `health-check.ps1` and run with:
```powershell
PowerShell -ExecutionPolicy Bypass -File health-check.ps1
```
