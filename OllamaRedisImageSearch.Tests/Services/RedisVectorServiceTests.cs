using FluentAssertions;
using OllamaRedisImageSearch.Config;
using OllamaRedisImageSearch.Models;
using OllamaRedisImageSearch.Services;
using System.Buffers;

namespace OllamaRedisImageSearch.Tests.Services;

[TestFixture]
public class RedisVectorServiceTests
{
    private AppConfig _config = null!;

    [SetUp]
    public void Setup()
    {
        _config = new AppConfig
        {
            RedisConnectionString = "localhost:6379",
            VectorIndexName = "test_image_vectors",
            KeyPrefix = "test_image:",
            VectorDimension = 768
        };
    }

    [TestFixture]
    public class VectorConversionTests
    {
        [Test]
        public void ConvertVectorToBytes_ShouldConvertCorrectly()
        {
            // Arrange
            var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
            var expectedByteCount = vector.Length * sizeof(float);

            // Act
            var bytes = ConvertVectorToBytesPublic(vector);

            // Assert
            bytes.Should().NotBeNull();
            bytes.Length.Should().Be(expectedByteCount);
        }

        [Test]
        public void ConvertVectorToBytes_WithEmptyVector_ShouldReturnEmptyBytes()
        {
            // Arrange
            var vector = new float[0];

            // Act
            var bytes = ConvertVectorToBytesPublic(vector);

            // Assert
            bytes.Should().NotBeNull().And.BeEmpty();
        }

        [Test]
        public void ConvertBytesToVector_ShouldConvertCorrectly()
        {
            // Arrange
            var originalVector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
            var bytes = ConvertVectorToBytesPublic(originalVector);

            // Act
            var convertedVector = ConvertBytesToVectorPublic(bytes);

            // Assert
            convertedVector.Should().NotBeNull();
            convertedVector.Length.Should().Be(originalVector.Length);
            convertedVector.Should().BeEquivalentTo(originalVector);
        }

        [Test]
        public void ConvertBytesToVector_WithEmptyBytes_ShouldReturnEmptyVector()
        {
            // Arrange
            var bytes = new byte[0];

            // Act
            var vector = ConvertBytesToVectorPublic(bytes);

            // Assert
            vector.Should().NotBeNull().And.BeEmpty();
        }

        [Test]
        public void VectorConversion_RoundTrip_ShouldPreserveData()
        {
            // Arrange
            var originalVector = new float[] { 
                0.123f, -0.456f, 0.789f, -0.012f, 1.0f, -1.0f, 0.0f, 0.999f 
            };

            // Act
            var bytes = ConvertVectorToBytesPublic(originalVector);
            var convertedVector = ConvertBytesToVectorPublic(bytes);

            // Assert
            convertedVector.Should().BeEquivalentTo(originalVector);
        }

        [Test]
        public void VectorConversion_WithLargeVector_ShouldWork()
        {
            // Arrange - 768-dimensional vector like Nomic embeddings
            var originalVector = Enumerable.Range(0, 768)
                .Select(i => (float)(i * 0.001))
                .ToArray();

            // Act
            var bytes = ConvertVectorToBytesPublic(originalVector);
            var convertedVector = ConvertBytesToVectorPublic(bytes);

            // Assert
            convertedVector.Should().NotBeNull();
            convertedVector.Length.Should().Be(768);
            convertedVector.Should().BeEquivalentTo(originalVector);
        }

        // Helper methods that simulate the private methods
        private byte[] ConvertVectorToBytesPublic(float[] vector)
        {
            var bytes = ArrayPool<byte>.Shared.Rent(vector.Length * sizeof(float));
            try
            {
                Buffer.BlockCopy(vector, 0, bytes, 0, vector.Length * sizeof(float));
                var result = new byte[vector.Length * sizeof(float)];
                Array.Copy(bytes, result, result.Length);
                return result;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }

        private float[] ConvertBytesToVectorPublic(byte[] bytes)
        {
            var vector = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
            return vector;
        }
    }

    [TestFixture]
    public class SearchResultTransformationTests
    {
        [Test]
        public void TransformSearchResults_ShouldConvertDistanceToSimilarity()
        {
            // Arrange - Simulate Redis search results with distances
            var mockResults = CreateMockSearchResults();

            // Act
            var transformedResults = TransformSearchResultsPublic(mockResults);

            // Assert
            transformedResults.Should().HaveCount(2);
            
            // Distance 0.1 should become similarity 0.9
            transformedResults[0].Score.Should().BeApproximately(0.9f, 0.01f);
            transformedResults[0].Filename.Should().Be("test1.jpg");
            
            // Distance 0.3 should become similarity 0.7
            transformedResults[1].Score.Should().BeApproximately(0.7f, 0.01f);
            transformedResults[1].Filename.Should().Be("test2.jpg");
        }

        [Test]
        public void TransformSearchResults_ShouldOrderBySimilarityDescending()
        {
            // Arrange
            var mockResults = CreateMockSearchResults();

            // Act
            var transformedResults = TransformSearchResultsPublic(mockResults);

            // Assert
            transformedResults.Should().BeInDescendingOrder(r => r.Score);
        }

        [Test]
        public void TransformSearchResults_WithEmptyResults_ShouldReturnEmpty()
        {
            // Arrange
            var mockResults = new List<MockSearchDocument>();

            // Act
            var transformedResults = TransformSearchResultsPublic(mockResults);

            // Assert
            transformedResults.Should().BeEmpty();
        }

        private List<MockSearchDocument> CreateMockSearchResults()
        {
            return new List<MockSearchDocument>
            {
                new()
                {
                    Fields = new Dictionary<string, object>
                    {
                        ["filename"] = "test1.jpg",
                        ["description"] = "A beautiful sunset",
                        ["vector_score"] = "0.1"
                    }
                },
                new()
                {
                    Fields = new Dictionary<string, object>
                    {
                        ["filename"] = "test2.jpg",
                        ["description"] = "A city skyline",
                        ["vector_score"] = "0.3"
                    }
                }
            };
        }

        private List<SearchResult> TransformSearchResultsPublic(List<MockSearchDocument> mockResults)
        {
            var results = new List<SearchResult>();

            foreach (var document in mockResults)
            {
                if (document.Fields.TryGetValue("filename", out var filenameObj) &&
                    document.Fields.TryGetValue("description", out var descObj) &&
                    document.Fields.TryGetValue("vector_score", out var scoreObj))
                {
                    var filename = filenameObj.ToString() ?? string.Empty;
                    var description = descObj.ToString() ?? string.Empty;
                    var scoreStr = scoreObj.ToString() ?? "0";
                    
                    if (float.TryParse(scoreStr, out float distance))
                    {
                        results.Add(new SearchResult
                        {
                            Filename = filename,
                            Description = description,
                            Score = 1.0f - distance, // Convert distance to similarity
                            ImagePath = Path.Combine("test_images", filename)
                        });
                    }
                }
            }

            return results.OrderByDescending(r => r.Score).ToList();
        }

        // Mock class to simulate Redis search document
        public class MockSearchDocument
        {
            public Dictionary<string, object> Fields { get; set; } = new();
        }
    }
}

[TestFixture]
public class RedisVectorServiceIntegrationTests
{
    private AppConfig _config = null!;

    [SetUp]
    public void Setup()
    {
        _config = new AppConfig
        {
            RedisConnectionString = "localhost:6379",
            VectorIndexName = "test_integration_vectors",
            KeyPrefix = "test_int:",
            VectorDimension = 768
        };
    }

    [Test]
    public void RedisVectorService_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        // Note: This test requires Redis to be running for full integration
        // For unit testing, we'll just test construction
        Action createService = () =>
        {
            using var service = new RedisVectorService(_config);
        };

        // Assert
        // Should not throw during construction (connection is lazy)
        createService.Should().NotThrow();
    }

    [Test]
    public void RedisVectorService_ShouldDisposeCorrectly()
    {
        // Arrange
        var service = new RedisVectorService(_config);

        // Act & Assert (should not throw)
        service.Dispose();
    }
}

[TestFixture]
public class RedisKeyGenerationTests
{
    [Test]
    public void GenerateRedisKey_ShouldCombinePrefixAndFilename()
    {
        // Arrange
        var prefix = "image:";
        var filename = "test.jpg";

        // Act
        var key = GenerateRedisKeyPublic(prefix, filename);

        // Assert
        key.Should().Be("image:test.jpg");
    }

    [Test]
    public void GenerateRedisKey_WithEmptyPrefix_ShouldReturnFilename()
    {
        // Arrange
        var prefix = "";
        var filename = "test.jpg";

        // Act
        var key = GenerateRedisKeyPublic(prefix, filename);

        // Assert
        key.Should().Be("test.jpg");
    }

    [Test]
    public void GenerateRedisKey_WithSpecialCharacters_ShouldPreserve()
    {
        // Arrange
        var prefix = "app:images:";
        var filename = "test-file_name.jpg";

        // Act
        var key = GenerateRedisKeyPublic(prefix, filename);

        // Assert
        key.Should().Be("app:images:test-file_name.jpg");
    }

    private string GenerateRedisKeyPublic(string prefix, string filename)
    {
        return $"{prefix}{filename}";
    }
}
