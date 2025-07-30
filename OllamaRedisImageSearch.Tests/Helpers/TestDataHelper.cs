using FluentAssertions;
using OllamaRedisImageSearch.Config;
using OllamaRedisImageSearch.Models;

namespace OllamaRedisImageSearch.Tests.Helpers;

/// <summary>
/// Helper class for creating test data and common test utilities.
/// </summary>
public static class TestDataHelper
{
    public static AppConfig CreateTestConfig()
    {
        return new AppConfig
        {
            OllamaBaseUrl = "http://localhost:11434",
            RedisConnectionString = "localhost:6379",
            VectorIndexName = "test_vectors",
            KeyPrefix = "test:",
            MaxSearchResults = 5,
            MaxContextTokens = 2000,
            VectorDimension = 768,
            MaxConcurrentOperations = 2,
            ImagesPath = "test_images",
            MaxRetries = 1,
            BaseRetryDelayMs = 10
        };
    }

    public static List<SearchResult> CreateSampleSearchResults()
    {
        return new List<SearchResult>
        {
            new()
            {
                Filename = "landscape.jpg",
                Description = "A beautiful mountain landscape with snow-capped peaks and blue sky",
                Score = 0.95f,
                ImagePath = "test_images/landscape.jpg"
            },
            new()
            {
                Filename = "city.jpg",
                Description = "A busy city street with tall buildings and cars",
                Score = 0.87f,
                ImagePath = "test_images/city.jpg"
            },
            new()
            {
                Filename = "nature.jpg",
                Description = "A peaceful forest scene with green trees and wildlife",
                Score = 0.82f,
                ImagePath = "test_images/nature.jpg"
            }
        };
    }

    public static List<SearchResult> CreateLargeSearchResults(int count)
    {
        var results = new List<SearchResult>();
        var random = new Random(42); // Fixed seed for reproducible tests

        for (int i = 0; i < count; i++)
        {
            results.Add(new SearchResult
            {
                Filename = $"test_image_{i:D3}.jpg",
                Description = $"Test image {i} with some descriptive content that varies in length. " +
                             $"This is iteration {i} of the test data generation process.",
                Score = (float)(random.NextDouble() * 0.5 + 0.5), // Score between 0.5 and 1.0
                ImagePath = $"test_images/test_image_{i:D3}.jpg"
            });
        }

        return results.OrderByDescending(r => r.Score).ToList();
    }

    public static McpMetrics CreateSampleMetrics()
    {
        return new McpMetrics
        {
            EmbeddingTime = TimeSpan.FromMilliseconds(150),
            SearchTime = TimeSpan.FromMilliseconds(75),
            SummarizationTime = TimeSpan.FromMilliseconds(300),
            TokensUsed = 1250,
            ResultsProcessed = 3,
            Timestamp = DateTime.UtcNow.AddMinutes(-5)
        };
    }

    public static float[] CreateSampleEmbedding(int dimension = 768)
    {
        var random = new Random(42); // Fixed seed for reproducible tests
        var embedding = new float[dimension];
        
        for (int i = 0; i < dimension; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2.0 - 1.0); // Values between -1 and 1
        }
        
        return embedding;
    }

    public static float[] CreateSimilarEmbedding(float[] original, float similarity = 0.9f)
    {
        var random = new Random(42);
        var similar = new float[original.Length];
        
        for (int i = 0; i < original.Length; i++)
        {
            // Add small random variation to create similar but not identical embedding
            var noise = (float)(random.NextDouble() * 0.2 - 0.1) * (1 - similarity);
            similar[i] = original[i] + noise;
        }
        
        return similar;
    }

    public static ImageMetadata CreateSampleImageMetadata()
    {
        return new ImageMetadata
        {
            Filename = "sample.jpg",
            Description = "A sample image for testing purposes",
            Embedding = ConvertFloatArrayToByteArray(CreateSampleEmbedding()),
            FileSize = 1024 * 50, // 50KB
            IndexedAt = DateTime.UtcNow.AddHours(-1)
        };
    }

    public static string CreateLongText(int wordCount)
    {
        var words = new[]
        {
            "beautiful", "landscape", "mountain", "forest", "city", "building", "car", "people",
            "nature", "wildlife", "trees", "sky", "cloud", "water", "river", "lake",
            "sunset", "sunrise", "peaceful", "busy", "colorful", "bright", "dark", "light"
        };

        var random = new Random(42);
        var result = new List<string>();

        for (int i = 0; i < wordCount; i++)
        {
            result.Add(words[random.Next(words.Length)]);
        }

        return string.Join(" ", result);
    }

    public static byte[] ConvertFloatArrayToByteArray(float[] floatArray)
    {
        var byteArray = new byte[floatArray.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray, 0, byteArray, 0, byteArray.Length);
        return byteArray;
    }

    public static float[] ConvertByteArrayToFloatArray(byte[] byteArray)
    {
        var floatArray = new float[byteArray.Length / sizeof(float)];
        Buffer.BlockCopy(byteArray, 0, floatArray, 0, byteArray.Length);
        return floatArray;
    }

    /// <summary>
    /// Calculate cosine similarity between two vectors for testing purposes.
    /// </summary>
    public static float CalculateCosineSimilarity(float[] vector1, float[] vector2)
    {
        if (vector1.Length != vector2.Length)
            throw new ArgumentException("Vectors must have the same length");

        float dotProduct = 0;
        float magnitude1 = 0;
        float magnitude2 = 0;

        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            magnitude1 += vector1[i] * vector1[i];
            magnitude2 += vector2[i] * vector2[i];
        }

        magnitude1 = (float)Math.Sqrt(magnitude1);
        magnitude2 = (float)Math.Sqrt(magnitude2);

        if (magnitude1 == 0 || magnitude2 == 0)
            return 0;

        return dotProduct / (magnitude1 * magnitude2);
    }

    /// <summary>
    /// Create test embeddings with known similarity relationships.
    /// </summary>
    public static (float[] query, float[] similar, float[] different) CreateTestEmbeddingSet()
    {
        var query = CreateSampleEmbedding();
        var similar = CreateSimilarEmbedding(query, 0.9f);
        
        // Create a different embedding by inverting values
        var different = new float[query.Length];
        for (int i = 0; i < query.Length; i++)
        {
            different[i] = -query[i] * 0.5f; // Opposite direction, different magnitude
        }

        return (query, similar, different);
    }
}

/// <summary>
/// Helper class for assertion utilities specific to this project.
/// </summary>
public static class TestAssertions
{
    public static void ShouldBeValidEmbedding(this float[] embedding, int expectedDimension = 768)
    {
        embedding.Should().NotBeNull();
        embedding.Length.Should().Be(expectedDimension);
        embedding.Should().NotBeEquivalentTo(new float[expectedDimension]); // Should not be all zeros
        embedding.Should().Contain(x => x != 0); // Should have some non-zero values
    }

    public static void ShouldBeValidSearchResult(this SearchResult result)
    {
        result.Should().NotBeNull();
        result.Filename.Should().NotBeNullOrEmpty();
        result.Description.Should().NotBeNullOrEmpty();
        result.Score.Should().BeInRange(0f, 1f);
    }

    public static void ShouldBeValidMetrics(this McpMetrics metrics)
    {
        metrics.Should().NotBeNull();
        metrics.EmbeddingTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        metrics.SearchTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        metrics.SummarizationTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        metrics.TokensUsed.Should().BeGreaterThanOrEqualTo(0);
        metrics.ResultsProcessed.Should().BeGreaterThanOrEqualTo(0);
    }

    public static void ShouldHaveReasonableTokenEstimate(this int tokenCount, int textLength)
    {
        // Token count should be roughly textLength/4, but at least 1
        var expectedMin = Math.Max(1, textLength / 6); // Allow some variation
        var expectedMax = Math.Max(1, textLength / 2);
        
        tokenCount.Should().BeInRange(expectedMin, expectedMax);
    }
}
