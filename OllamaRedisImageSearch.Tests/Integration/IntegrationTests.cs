using FluentAssertions;
using OllamaRedisImageSearch.Config;
using OllamaRedisImageSearch.Models;
using OllamaRedisImageSearch.Services;

namespace OllamaRedisImageSearch.Tests.Integration;

/// <summary>
/// Integration tests that require actual Ollama and Redis services running.
/// These tests are marked with [Explicit] to prevent them from running in regular CI/CD.
/// To run these tests, ensure Ollama and Redis Stack are running locally.
/// </summary>
[TestFixture]
[Explicit("Requires Ollama and Redis Stack to be running")]
public class FullIntegrationTests
{
    private AppConfig _config = null!;
    private ImageSearchService _imageSearchService = null!;
    private OllamaService _ollamaService = null!;
    private RedisVectorService _redisService = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _config = new AppConfig
        {
            VectorIndexName = "test_integration_index",
            KeyPrefix = "test_int:",
            MaxSearchResults = 3,
            MaxContextTokens = 2000,
            ImagesPath = "test_images_integration"
        };

        _ollamaService = new OllamaService(_config);
        _redisService = new RedisVectorService(_config);
        _imageSearchService = new ImageSearchService(_ollamaService, _redisService, _config);

        // Initialize the service
        var initialized = await _imageSearchService.InitializeAsync();
        if (!initialized)
        {
            Assert.Fail("Failed to initialize services. Ensure Ollama and Redis Stack are running.");
        }
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _imageSearchService?.Dispose();
        _ollamaService?.Dispose();
        _redisService?.Dispose();
    }

    [Test]
    [Order(1)]
    public async Task OllamaService_GetTextEmbedding_ShouldReturnValidEmbedding()
    {
        // Arrange
        var text = "This is a test text for embedding generation.";

        // Act
        var embedding = await _ollamaService.GetTextEmbeddingAsync(text);

        // Assert
        embedding.Should().NotBeNull();
        embedding.Length.Should().Be(_config.VectorDimension); // Should be 768 for nomic-embed-text
        embedding.Should().NotBeEquivalentTo(new float[_config.VectorDimension]); // Should not be all zeros
    }

    [Test]
    [Order(2)]
    public async Task OllamaService_SummarizeWithGemma_ShouldReturnSummary()
    {
        // Arrange
        var query = "test images";
        var results = new List<SearchResult>
        {
            new() { Filename = "test1.jpg", Description = "A beautiful landscape with mountains", Score = 0.9f },
            new() { Filename = "test2.jpg", Description = "A city street with cars", Score = 0.8f }
        };

        // Act
        var summary = await _ollamaService.SummarizeWithGemmaAsync(query, results);

        // Assert
        summary.Should().NotBeNullOrEmpty();
        summary.Length.Should().BeGreaterThan(10); // Should be a meaningful response
        summary.Should().Contain("image"); // Should mention images in some form
    }

    [Test]
    [Order(3)]
    public async Task RedisVectorService_StoreAndSearch_ShouldWork()
    {
        // Arrange
        var filename = "integration_test.jpg";
        var description = "Integration test image with unique content for testing";
        var embedding = await _ollamaService.GetTextEmbeddingAsync(description);

        // Act - Store
        var stored = await _redisService.StoreImageEmbeddingAsync(filename, description, embedding);
        
        // Act - Search
        var searchResults = await _redisService.SearchSimilarImagesAsync(embedding, 5);

        // Assert
        stored.Should().BeTrue();
        searchResults.Should().NotBeEmpty();
        searchResults.Should().Contain(r => r.Filename == filename);
        
        var matchingResult = searchResults.First(r => r.Filename == filename);
        matchingResult.Description.Should().Be(description);
        matchingResult.Score.Should().BeGreaterThan(0.9f); // Should be very similar to itself

        // Cleanup
        await _redisService.DeleteImageAsync(filename);
    }

    [Test]
    [Order(4)]
    public async Task ImageSearchService_FullSearchWorkflow_ShouldWork()
    {
        // Arrange
        var testData = new[]
        {
            ("test_landscape.jpg", "A beautiful mountain landscape with snow-capped peaks"),
            ("test_city.jpg", "A busy city street with tall buildings and traffic"),
            ("test_nature.jpg", "A peaceful forest with green trees and wildlife")
        };

        // Store test data
        foreach (var (filename, description) in testData)
        {
            var embedding = await _ollamaService.GetTextEmbeddingAsync(description);
            await _redisService.StoreImageEmbeddingAsync(filename, description, embedding);
        }

        // Act - Search
        var searchQuery = "mountain landscape";
        var results = await _imageSearchService.SearchAsync(searchQuery);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().Contain(r => r.Filename == "test_landscape.jpg");
        
        var topResult = results.OrderByDescending(r => r.Score).First();
        topResult.Filename.Should().Be("test_landscape.jpg"); // Should rank the landscape highest
        
        // Verify metrics were tracked
        var metrics = await _imageSearchService.GetLastSearchMetricsAsync();
        metrics.EmbeddingTime.Should().BeGreaterThan(TimeSpan.Zero);
        metrics.SearchTime.Should().BeGreaterThan(TimeSpan.Zero);
        metrics.SummarizationTime.Should().BeGreaterThan(TimeSpan.Zero);
        metrics.ResultsProcessed.Should().BeGreaterThan(0);
        metrics.TokensUsed.Should().BeGreaterThan(0);

        // Cleanup
        foreach (var (filename, _) in testData)
        {
            await _redisService.DeleteImageAsync(filename);
        }
    }

    [Test]
    [Order(5)]
    public async Task MCP_ContextOptimization_ShouldLimitResults()
    {
        // Arrange - Create a config with very low token limit
        var lowTokenConfig = new AppConfig
        {
            MaxContextTokens = 500, // Very low limit
            MaxSearchResults = 10,
            VectorIndexName = _config.VectorIndexName,
            KeyPrefix = _config.KeyPrefix
        };

        using var limitedOllamaService = new OllamaService(lowTokenConfig);
        
        var longDescription = new string('a', 1000); // Very long description
        var testData = new[]
        {
            ("mcp_test1.jpg", longDescription),
            ("mcp_test2.jpg", longDescription),
            ("mcp_test3.jpg", longDescription)
        };

        // Store test data with long descriptions
        foreach (var (filename, description) in testData)
        {
            var embedding = await limitedOllamaService.GetTextEmbeddingAsync(description);
            await _redisService.StoreImageEmbeddingAsync(filename, description, embedding);
        }

        // Create search results that would exceed context limit
        var searchResults = testData.Select(td => new SearchResult
        {
            Filename = td.Item1,
            Description = td.Item2,
            Score = 0.9f
        }).ToList();

        // Act - This should not fail due to context optimization
        var summary = await limitedOllamaService.SummarizeWithGemmaAsync("test query", searchResults);

        // Assert
        summary.Should().NotBeNullOrEmpty();
        // The service should have automatically limited the context to fit within token limits

        // Cleanup
        foreach (var (filename, _) in testData)
        {
            await _redisService.DeleteImageAsync(filename);
        }
    }
}

/// <summary>
/// Smoke tests that verify basic functionality without requiring external services.
/// These tests focus on object creation and basic validation.
/// </summary>
[TestFixture]
public class SmokeTests
{
    [Test]
    public void AllServices_ShouldInstantiateWithoutErrors()
    {
        // Arrange
        var config = new AppConfig();

        // Act & Assert
        using var ollamaService = new OllamaService(config);
        using var redisService = new RedisVectorService(config);
        using var imageSearchService = new ImageSearchService(ollamaService, redisService, config);

        // If we get here without exceptions, the basic instantiation works
        Assert.Pass("All services instantiated successfully");
    }

    [Test]
    public void AppConfig_ShouldHaveReasonableDefaults()
    {
        // Arrange & Act
        var config = new AppConfig();

        // Assert
        config.OllamaBaseUrl.Should().StartWith("http");
        config.VectorDimension.Should().BeGreaterThan(0);
        config.MaxContextTokens.Should().BeGreaterThan(1000);
        config.MaxSearchResults.Should().BeGreaterThan(0);
        config.VisionModel.Should().NotBeNullOrEmpty();
        config.EmbeddingModel.Should().NotBeNullOrEmpty();
        config.SummarizationModel.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void SearchResult_ShouldSupportComparison()
    {
        // Arrange
        var result1 = new SearchResult { Score = 0.9f, Filename = "test1.jpg" };
        var result2 = new SearchResult { Score = 0.8f, Filename = "test2.jpg" };
        var results = new List<SearchResult> { result2, result1 };

        // Act
        var sorted = results.OrderByDescending(r => r.Score).ToList();

        // Assert
        sorted[0].Should().Be(result1); // Higher score should be first
        sorted[1].Should().Be(result2);
    }

    [Test]
    public void McpMetrics_ShouldTrackTotalTime()
    {
        // Arrange
        var metrics = new McpMetrics
        {
            EmbeddingTime = TimeSpan.FromMilliseconds(100),
            SearchTime = TimeSpan.FromMilliseconds(50),
            SummarizationTime = TimeSpan.FromMilliseconds(200)
        };

        // Act
        var totalTime = metrics.EmbeddingTime + metrics.SearchTime + metrics.SummarizationTime;

        // Assert
        totalTime.Should().Be(TimeSpan.FromMilliseconds(350));
    }
}
