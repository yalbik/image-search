# MCP Image Fun App Health Check Script
Write-Host "=== MCP Image Fun App Health Check ===" -ForegroundColor Green

# Check Ollama
Write-Host "`nChecking Ollama..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -Method Get -TimeoutSec 5
    Write-Host "✓ Ollama is running" -ForegroundColor Green
    Write-Host "  Available models:" -ForegroundColor Cyan
    $requiredModels = @("llava", "nomic-embed-text", "gemma")
    $availableModels = $response.models | ForEach-Object { ($_.name -split ':')[0] } | Select-Object -Unique
    
    foreach ($model in $requiredModels) {
        if ($availableModels -contains $model) {
            Write-Host "    ✓ $model" -ForegroundColor Green
        } else {
            Write-Host "    ✗ $model (missing - run: ollama pull $model)" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "✗ Ollama is not running or not accessible" -ForegroundColor Red
    Write-Host "  Please start Ollama and ensure it's running on http://localhost:11434" -ForegroundColor Gray
}

# Check Redis
Write-Host "`nChecking Redis Stack..." -ForegroundColor Yellow
try {
    $redisCheck = docker exec redis-stack redis-cli ping 2>$null
    if ($redisCheck -eq "PONG") {
        Write-Host "✓ Redis Stack is running" -ForegroundColor Green
        
        # Check Redis Stack modules
        $modules = docker exec redis-stack redis-cli MODULE LIST 2>$null
        if ($modules -match "search") {
            Write-Host "  ✓ RediSearch module loaded" -ForegroundColor Green
        } else {
            Write-Host "  ✗ RediSearch module not found" -ForegroundColor Red
        }
    } else {
        Write-Host "✗ Redis Stack is not responding" -ForegroundColor Red
    }
} catch {
    Write-Host "✗ Redis Stack container not found" -ForegroundColor Red
    Write-Host "  Run: docker run -d --name redis-stack -p 6379:6379 -p 8001:8001 redis/redis-stack:latest" -ForegroundColor Gray
}

# Check .NET SDK
Write-Host "`nChecking .NET SDK..." -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version 2>$null
    if ($dotnetVersion) {
        Write-Host "✓ .NET SDK version: $dotnetVersion" -ForegroundColor Green
    } else {
        Write-Host "✗ .NET SDK not found" -ForegroundColor Red
    }
} catch {
    Write-Host "✗ .NET SDK not found" -ForegroundColor Red
}

# Check images directory
Write-Host "`nChecking images directory..." -ForegroundColor Yellow
if (Test-Path "my_images") {
    $imageFiles = Get-ChildItem "my_images" -Include *.jpg,*.jpeg,*.png -Recurse
    $imageCount = $imageFiles.Count
    if ($imageCount -gt 0) {
        Write-Host "✓ Images directory exists ($imageCount images)" -ForegroundColor Green
        Write-Host "  Sample files:" -ForegroundColor Cyan
        $imageFiles | Select-Object -First 3 | ForEach-Object { 
            Write-Host "    - $($_.Name)" -ForegroundColor Gray 
        }
        if ($imageCount -gt 3) {
            Write-Host "    ... and $($imageCount - 3) more" -ForegroundColor Gray
        }
    } else {
        Write-Host "⚠ Images directory exists but is empty" -ForegroundColor Yellow
        Write-Host "  Add some .jpg/.png files to the my_images folder" -ForegroundColor Gray
    }
} else {
    Write-Host "⚠ Images directory not found (will be created automatically)" -ForegroundColor Yellow
}

# Check project files
Write-Host "`nChecking project structure..." -ForegroundColor Yellow
$projectFile = "OllamaRedisImageSearch\OllamaRedisImageSearch.csproj"
if (Test-Path $projectFile) {
    Write-Host "✓ Project file found" -ForegroundColor Green
    
    # Check if restored
    if (Test-Path "OllamaRedisImageSearch\obj\project.assets.json") {
        Write-Host "✓ NuGet packages restored" -ForegroundColor Green
    } else {
        Write-Host "⚠ NuGet packages not restored" -ForegroundColor Yellow
        Write-Host "  Run: dotnet restore" -ForegroundColor Gray
    }
} else {
    Write-Host "✗ Project file not found" -ForegroundColor Red
}

# Summary
Write-Host "`n=== Health Check Summary ===" -ForegroundColor Green
Write-Host "If all items show ✓ (green), you're ready to run the application!" -ForegroundColor Cyan
Write-Host "To start the app: cd OllamaRedisImageSearch && dotnet run" -ForegroundColor Cyan
Write-Host "`nFor setup help, see SETUP.md" -ForegroundColor Gray
