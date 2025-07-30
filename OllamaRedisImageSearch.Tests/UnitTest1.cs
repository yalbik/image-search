using FluentAssertions;
using OllamaRedisImageSearch.Config;
using OllamaRedisImageSearch.Models;

namespace OllamaRedisImageSearch.Tests;

[TestFixture]
public class AppConfigTests
{
    [Test]
    public void AppConfig_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var config = new AppConfig();

        // Assert
        config.OllamaBaseUrl.Should().Be("http://localhost:11434");
        config.RedisConnectionString.Should().Be("localhost:6379");
        config.MaxSearchResults.Should().Be(5);
        config.MaxContextTokens.Should().Be(6000);
        config.ImagesPath.Should().Be("my_images");
        config.VectorIndexName.Should().Be("image_vectors");
        config.KeyPrefix.Should().Be("image:");
        config.VectorDimension.Should().Be(768);
        config.MaxConcurrentOperations.Should().Be(5);
        config.VisionModel.Should().Be("llava");
        config.EmbeddingModel.Should().Be("nomic-embed-text");
        config.SummarizationModel.Should().Be("gemma");
        config.MaxRetries.Should().Be(3);
        config.BaseRetryDelayMs.Should().Be(1000);
    }

    [Test]
    public void AppConfig_ShouldAllowPropertyModification()
    {
        // Arrange
        var config = new AppConfig();

        // Act
        config.MaxSearchResults = 10;
        config.MaxContextTokens = 8000;
        config.VisionModel = "custom-llava";

        // Assert
        config.MaxSearchResults.Should().Be(10);
        config.MaxContextTokens.Should().Be(8000);
        config.VisionModel.Should().Be("custom-llava");
    }
}

[TestFixture]
public class SearchResultTests
{
    [Test]
    public void SearchResult_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var result = new SearchResult();

        // Assert
        result.Filename.Should().Be(string.Empty);
        result.Description.Should().Be(string.Empty);
        result.Score.Should().Be(0f);
        result.ImagePath.Should().Be(string.Empty);
    }

    [Test]
    public void SearchResult_ShouldSetPropertiesCorrectly()
    {
        // Arrange & Act
        var result = new SearchResult
        {
            Filename = "test.jpg",
            Description = "A test image",
            Score = 0.95f,
            ImagePath = "/path/to/test.jpg"
        };

        // Assert
        result.Filename.Should().Be("test.jpg");
        result.Description.Should().Be("A test image");
        result.Score.Should().Be(0.95f);
        result.ImagePath.Should().Be("/path/to/test.jpg");
    }
}

[TestFixture]
public class McpMetricsTests
{
    [Test]
    public void McpMetrics_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var metrics = new McpMetrics();

        // Assert
        metrics.EmbeddingTime.Should().Be(TimeSpan.Zero);
        metrics.SearchTime.Should().Be(TimeSpan.Zero);
        metrics.SummarizationTime.Should().Be(TimeSpan.Zero);
        metrics.TokensUsed.Should().Be(0);
        metrics.ResultsProcessed.Should().Be(0);
        metrics.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Test]
    public void McpMetrics_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var embeddingTime = TimeSpan.FromMilliseconds(100);
        var searchTime = TimeSpan.FromMilliseconds(50);
        var summarizationTime = TimeSpan.FromMilliseconds(200);
        var timestamp = DateTime.UtcNow.AddMinutes(-1);

        // Act
        var metrics = new McpMetrics
        {
            EmbeddingTime = embeddingTime,
            SearchTime = searchTime,
            SummarizationTime = summarizationTime,
            TokensUsed = 1500,
            ResultsProcessed = 3,
            Timestamp = timestamp
        };

        // Assert
        metrics.EmbeddingTime.Should().Be(embeddingTime);
        metrics.SearchTime.Should().Be(searchTime);
        metrics.SummarizationTime.Should().Be(summarizationTime);
        metrics.TokensUsed.Should().Be(1500);
        metrics.ResultsProcessed.Should().Be(3);
        metrics.Timestamp.Should().Be(timestamp);
    }
}

[TestFixture]
public class ImageMetadataTests
{
    [Test]
    public void ImageMetadata_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var metadata = new ImageMetadata();

        // Assert
        metadata.Filename.Should().Be(string.Empty);
        metadata.Description.Should().Be(string.Empty);
        metadata.Embedding.Should().BeEmpty();
        metadata.FileSize.Should().Be(0);
        metadata.IndexedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Test]
    public void ImageMetadata_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var embedding = new byte[] { 1, 2, 3, 4, 5 };
        var indexedAt = DateTime.UtcNow.AddHours(-1);

        // Act
        var metadata = new ImageMetadata
        {
            Filename = "test.jpg",
            Description = "Test description",
            Embedding = embedding,
            FileSize = 1024,
            IndexedAt = indexedAt
        };

        // Assert
        metadata.Filename.Should().Be("test.jpg");
        metadata.Description.Should().Be("Test description");
        metadata.Embedding.Should().BeEquivalentTo(embedding);
        metadata.FileSize.Should().Be(1024);
        metadata.IndexedAt.Should().Be(indexedAt);
    }
}
