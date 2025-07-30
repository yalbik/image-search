@echo off
echo === MCP Image Fun App - Quick Start ===
echo.

echo Running health check...
PowerShell -ExecutionPolicy Bypass -File health-check.ps1

echo.
echo Building project...
cd OllamaRedisImageSearch
dotnet build

echo.
echo Setup complete! To run the application:
echo cd OllamaRedisImageSearch
echo dotnet run
echo.
pause
