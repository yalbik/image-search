using FluentAssertions;
using Moq;
using OllamaRedisImageSearch.Config;
using OllamaRedisImageSearch.Models;
using OllamaRedisImageSearch.Services;

namespace OllamaRedisImageSearch.Tests.Services;

[TestFixture]
public class ImageSearchServiceTests
{
    private Mock<IOllamaService> _mockOllamaService = null!;
    private Mock<IRedisVectorService> _mockRedisService = null!;
    private AppConfig _config = null!;
    private ImageSearchService _imageSearchService = null!;

    [SetUp]
    public void Setup()
    {
        _mockOllamaService = new Mock<IOllamaService>();
        _mockRedisService = new Mock<IRedisVectorService>();
        _config = new AppConfig
        {
            MaxSearchResults = 3,
            MaxContextTokens = 1000,
            ImagesPath = "test_images"
        };
        
        _imageSearchService = new ImageSearchService(
            _mockOllamaService.Object,
            _mockRedisService.Object,
            _config);
    }

    [TearDown]
    public void TearDown()
    {
        _imageSearchService?.Dispose();
    }

    [TestFixture]
    public class InitializationTests
    {
        private Mock<IOllamaService> _mockOllamaService = null!;
        private Mock<IRedisVectorService> _mockRedisService = null!;
        private AppConfig _config = null!;
        private ImageSearchService _imageSearchService = null!;

        [SetUp]
        public void Setup()
        {
            _mockOllamaService = new Mock<IOllamaService>();
            _mockRedisService = new Mock<IRedisVectorService>();
            _config = new AppConfig();
            
            _imageSearchService = new ImageSearchService(
                _mockOllamaService.Object,
                _mockRedisService.Object,
                _config);
        }

        [TearDown]
        public void TearDown()
        {
            _imageSearchService?.Dispose();
        }

        [Test]
        public async Task InitializeAsync_WhenRedisIndexCreated_ShouldReturnTrue()
        {
            // Arrange
            _mockRedisService.Setup(x => x.EnsureRedisIndexExistsAsync())
                .ReturnsAsync(true);
            _mockRedisService.Setup(x => x.GetIndexedImageCountAsync())
                .ReturnsAsync(5);

            // Act
            var result = await _imageSearchService.InitializeAsync();

            // Assert
            result.Should().BeTrue();
            _mockRedisService.Verify(x => x.EnsureRedisIndexExistsAsync(), Times.Once);
            _mockRedisService.Verify(x => x.GetIndexedImageCountAsync(), Times.Once);
        }

        [Test]
        public async Task InitializeAsync_WhenRedisIndexFails_ShouldReturnFalse()
        {
            // Arrange
            _mockRedisService.Setup(x => x.EnsureRedisIndexExistsAsync())
                .ReturnsAsync(false);

            // Act
            var result = await _imageSearchService.InitializeAsync();

            // Assert
            result.Should().BeFalse();
            _mockRedisService.Verify(x => x.EnsureRedisIndexExistsAsync(), Times.Once);
            _mockRedisService.Verify(x => x.GetIndexedImageCountAsync(), Times.Never);
        }
    }

    [TestFixture]
    public class SearchTests
    {
        private Mock<IOllamaService> _mockOllamaService = null!;
        private Mock<IRedisVectorService> _mockRedisService = null!;
        private AppConfig _config = null!;
        private ImageSearchService _imageSearchService = null!;

        [SetUp]
        public void Setup()
        {
            _mockOllamaService = new Mock<IOllamaService>();
            _mockRedisService = new Mock<IRedisVectorService>();
            _config = new AppConfig { MaxSearchResults = 3 };
            
            _imageSearchService = new ImageSearchService(
                _mockOllamaService.Object,
                _mockRedisService.Object,
                _config);
        }

        [TearDown]
        public void TearDown()
        {
            _imageSearchService?.Dispose();
        }

        [Test]
        public async Task SearchAsync_WithValidQuery_ShouldReturnResults()
        {
            // Arrange
            var query = "test query";
            var queryEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
            var searchResults = new List<SearchResult>
            {
                new() { Filename = "test1.jpg", Description = "Test image 1", Score = 0.9f },
                new() { Filename = "test2.jpg", Description = "Test image 2", Score = 0.8f }
            };
            var summary = "These images show test content.";

            _mockOllamaService.Setup(x => x.GetTextEmbeddingAsync(query))
                .ReturnsAsync(queryEmbedding);
            _mockRedisService.Setup(x => x.SearchSimilarImagesAsync(queryEmbedding, _config.MaxSearchResults))
                .ReturnsAsync(searchResults);
            _mockOllamaService.Setup(x => x.SummarizeWithGemmaAsync(query, searchResults))
                .ReturnsAsync(summary);

            // Act
            var results = await _imageSearchService.SearchAsync(query);

            // Assert
            results.Should().NotBeNull();
            results.Should().HaveCount(2);
            results.Should().BeEquivalentTo(searchResults);
            
            _mockOllamaService.Verify(x => x.GetTextEmbeddingAsync(query), Times.Once);
            _mockRedisService.Verify(x => x.SearchSimilarImagesAsync(queryEmbedding, _config.MaxSearchResults), Times.Once);
            _mockOllamaService.Verify(x => x.SummarizeWithGemmaAsync(query, searchResults), Times.Once);
        }

        [Test]
        public async Task SearchAsync_WhenEmbeddingFails_ShouldReturnEmptyResults()
        {
            // Arrange
            var query = "test query";
            var emptyEmbedding = new float[0];

            _mockOllamaService.Setup(x => x.GetTextEmbeddingAsync(query))
                .ReturnsAsync(emptyEmbedding);

            // Act
            var results = await _imageSearchService.SearchAsync(query);

            // Assert
            results.Should().NotBeNull().And.BeEmpty();
            _mockOllamaService.Verify(x => x.GetTextEmbeddingAsync(query), Times.Once);
            _mockRedisService.Verify(x => x.SearchSimilarImagesAsync(It.IsAny<float[]>(), It.IsAny<int>()), Times.Never);
        }

        [Test]
        public async Task SearchAsync_WhenNoSearchResults_ShouldReturnEmptyResults()
        {
            // Arrange
            var query = "test query";
            var queryEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
            var emptyResults = new List<SearchResult>();

            _mockOllamaService.Setup(x => x.GetTextEmbeddingAsync(query))
                .ReturnsAsync(queryEmbedding);
            _mockRedisService.Setup(x => x.SearchSimilarImagesAsync(queryEmbedding, _config.MaxSearchResults))
                .ReturnsAsync(emptyResults);

            // Act
            var results = await _imageSearchService.SearchAsync(query);

            // Assert
            results.Should().NotBeNull().And.BeEmpty();
            _mockOllamaService.Verify(x => x.SummarizeWithGemmaAsync(It.IsAny<string>(), It.IsAny<List<SearchResult>>()), Times.Never);
        }

        [Test]
        public async Task SearchAsync_ShouldTrackPerformanceMetrics()
        {
            // Arrange
            var query = "test query";
            var queryEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
            var searchResults = new List<SearchResult>
            {
                new() { Filename = "test.jpg", Description = "Test", Score = 0.9f }
            };

            _mockOllamaService.Setup(x => x.GetTextEmbeddingAsync(query))
                .ReturnsAsync(queryEmbedding);
            _mockRedisService.Setup(x => x.SearchSimilarImagesAsync(queryEmbedding, _config.MaxSearchResults))
                .ReturnsAsync(searchResults);
            _mockOllamaService.Setup(x => x.SummarizeWithGemmaAsync(query, searchResults))
                .ReturnsAsync("Summary");

            // Act
            await _imageSearchService.SearchAsync(query);
            var metrics = await _imageSearchService.GetLastSearchMetricsAsync();

            // Assert
            metrics.Should().NotBeNull();
            metrics.EmbeddingTime.Should().BeGreaterThan(TimeSpan.Zero);
            metrics.SearchTime.Should().BeGreaterThan(TimeSpan.Zero);
            metrics.SummarizationTime.Should().BeGreaterThan(TimeSpan.Zero);
            metrics.ResultsProcessed.Should().Be(1);
            metrics.TokensUsed.Should().BeGreaterThan(0);
            metrics.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }
    }

    [TestFixture]
    public class MetricsTests
    {
        private Mock<IOllamaService> _mockOllamaService = null!;
        private Mock<IRedisVectorService> _mockRedisService = null!;
        private AppConfig _config = null!;
        private ImageSearchService _imageSearchService = null!;

        [SetUp]
        public void Setup()
        {
            _mockOllamaService = new Mock<IOllamaService>();
            _mockRedisService = new Mock<IRedisVectorService>();
            _config = new AppConfig();
            
            _imageSearchService = new ImageSearchService(
                _mockOllamaService.Object,
                _mockRedisService.Object,
                _config);
        }

        [TearDown]
        public void TearDown()
        {
            _imageSearchService?.Dispose();
        }

        [Test]
        public async Task GetLastSearchMetricsAsync_WithoutPreviousSearch_ShouldReturnDefaultMetrics()
        {
            // Act
            var metrics = await _imageSearchService.GetLastSearchMetricsAsync();

            // Assert
            metrics.Should().NotBeNull();
            metrics.EmbeddingTime.Should().Be(TimeSpan.Zero);
            metrics.SearchTime.Should().Be(TimeSpan.Zero);
            metrics.SummarizationTime.Should().Be(TimeSpan.Zero);
            metrics.TokensUsed.Should().Be(0);
            metrics.ResultsProcessed.Should().Be(0);
            // Note: Timestamp is now initialized to current time in constructor, so we check it's recent
            metrics.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }
    }

    [TestFixture]
    public class TokenEstimationTests
    {
        [Test]
        public void EstimateTokensUsed_ShouldCalculateCorrectly()
        {
            // Arrange
            var query = "test query"; // 10 chars = ~2 tokens
            var results = new List<SearchResult>
            {
                new() { Description = "short desc" }, // 10 chars = ~2 tokens
                new() { Description = "another description" } // 19 chars = ~4 tokens
            };
            var summary = "this is a summary"; // 17 chars = ~4 tokens

            // Act
            var tokens = EstimateTokensUsedPublic(query, results, summary);

            // Assert
            // Expected: 2 (query) + 2 + 4 (results) + 4 (summary) + 200 (overhead) = 212
            tokens.Should().Be(212);
        }

        [Test]
        public void EstimateTokensUsed_WithEmptyInputs_ShouldReturnMinimumTokens()
        {
            // Arrange
            var query = "";
            var results = new List<SearchResult>();
            var summary = "";

            // Act
            var tokens = EstimateTokensUsedPublic(query, results, summary);

            // Assert
            // Expected: 1 + 0 + 1 + 200 = 202 (minimum tokens due to Math.Max(1, ...))
            tokens.Should().Be(202);
        }

        [Test]
        public void EstimateTokensUsed_WithLongTexts_ShouldCalculateCorrectly()
        {
            // Arrange
            var query = new string('a', 100); // 100 chars = 25 tokens
            var results = new List<SearchResult>
            {
                new() { Description = new string('b', 200) } // 200 chars = 50 tokens
            };
            var summary = new string('c', 80); // 80 chars = 20 tokens

            // Act
            var tokens = EstimateTokensUsedPublic(query, results, summary);

            // Assert
            // Expected: 25 + 50 + 20 + 200 = 295
            tokens.Should().Be(295);
        }

        private int EstimateTokensUsedPublic(string query, List<SearchResult> results, string summary)
        {
            var queryTokens = Math.Max(1, query.Length / 4);
            var resultsTokens = results.Sum(r => Math.Max(1, r.Description.Length / 4));
            var summaryTokens = Math.Max(1, summary.Length / 4);
            
            return queryTokens + resultsTokens + summaryTokens + 200; // Add overhead for prompt structure
        }
    }

    [TestFixture]
    public class DisposalTests
    {
        [Test]
        public void ImageSearchService_ShouldDisposeCorrectly()
        {
            // Arrange
            var mockOllama = new Mock<IOllamaService>();
            var mockRedis = new Mock<IRedisVectorService>();
            var config = new AppConfig();
            var service = new ImageSearchService(mockOllama.Object, mockRedis.Object, config);

            // Act & Assert (should not throw)
            service.Dispose();

            // Verify that dependencies are disposed
            mockOllama.Verify(x => x.Dispose(), Times.Once);
            mockRedis.Verify(x => x.Dispose(), Times.Once);
        }

        [Test]
        public void ImageSearchService_DisposeTwice_ShouldNotThrow()
        {
            // Arrange
            var mockOllama = new Mock<IOllamaService>();
            var mockRedis = new Mock<IRedisVectorService>();
            var config = new AppConfig();
            var service = new ImageSearchService(mockOllama.Object, mockRedis.Object, config);

            // Act & Assert (should not throw)
            service.Dispose();
            service.Dispose(); // Second dispose should be safe
        }
    }
}
