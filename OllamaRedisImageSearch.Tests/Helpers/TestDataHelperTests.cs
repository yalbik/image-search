using FluentAssertions;
using OllamaRedisImageSearch.Tests.Helpers;

namespace OllamaRedisImageSearch.Tests.Helpers;

[TestFixture]
public class TestDataHelperTests
{
    [Test]
    public void CreateTestConfig_ShouldReturnValidConfiguration()
    {
        // Act
        var config = TestDataHelper.CreateTestConfig();

        // Assert
        config.Should().NotBeNull();
        config.OllamaBaseUrl.Should().StartWith("http");
        config.VectorDimension.Should().Be(768);
        config.MaxSearchResults.Should().BeGreaterThan(0);
        config.VectorIndexName.Should().Contain("test");
    }

    [Test]
    public void CreateSampleSearchResults_ShouldReturnValidResults()
    {
        // Act
        var results = TestDataHelper.CreateSampleSearchResults();

        // Assert
        results.Should().NotBeNull().And.HaveCount(3);
        
        foreach (var result in results)
        {
            result.ShouldBeValidSearchResult();
        }
        
        // Should be ordered by score descending
        results.Should().BeInDescendingOrder(r => r.Score);
    }

    [Test]
    public void CreateSampleEmbedding_ShouldReturnValidEmbedding()
    {
        // Act
        var embedding = TestDataHelper.CreateSampleEmbedding();

        // Assert
        embedding.ShouldBeValidEmbedding();
    }

    [Test]
    public void CreateSampleEmbedding_WithCustomDimension_ShouldRespectDimension()
    {
        // Arrange
        var customDimension = 256;

        // Act
        var embedding = TestDataHelper.CreateSampleEmbedding(customDimension);

        // Assert
        embedding.ShouldBeValidEmbedding(customDimension);
    }

    [Test]
    public void CreateSimilarEmbedding_ShouldBeActuallySimilar()
    {
        // Arrange
        var original = TestDataHelper.CreateSampleEmbedding();

        // Act
        var similar = TestDataHelper.CreateSimilarEmbedding(original, 0.95f);

        // Assert
        similar.ShouldBeValidEmbedding();
        
        var similarity = TestDataHelper.CalculateCosineSimilarity(original, similar);
        similarity.Should().BeGreaterThan(0.8f); // Should be quite similar
    }

    [Test]
    public void CreateLargeSearchResults_ShouldCreateRequestedCount()
    {
        // Arrange
        var count = 100;

        // Act
        var results = TestDataHelper.CreateLargeSearchResults(count);

        // Assert
        results.Should().HaveCount(count);
        results.Should().BeInDescendingOrder(r => r.Score);
        
        foreach (var result in results.Take(10)) // Check first 10
        {
            result.ShouldBeValidSearchResult();
        }
    }

    [Test]
    public void CreateSampleMetrics_ShouldReturnValidMetrics()
    {
        // Act
        var metrics = TestDataHelper.CreateSampleMetrics();

        // Assert
        metrics.ShouldBeValidMetrics();
    }

    [Test]
    public void CreateLongText_ShouldCreateTextWithRequestedWordCount()
    {
        // Arrange
        var wordCount = 50;

        // Act
        var text = TestDataHelper.CreateLongText(wordCount);

        // Assert
        text.Should().NotBeNullOrEmpty();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        words.Length.Should().Be(wordCount);
    }

    [Test]
    public void VectorConversion_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var originalVector = TestDataHelper.CreateSampleEmbedding();

        // Act
        var bytes = TestDataHelper.ConvertFloatArrayToByteArray(originalVector);
        var convertedVector = TestDataHelper.ConvertByteArrayToFloatArray(bytes);

        // Assert
        convertedVector.Should().BeEquivalentTo(originalVector);
    }

    [Test]
    public void CalculateCosineSimilarity_WithIdenticalVectors_ShouldReturnOne()
    {
        // Arrange
        var vector = TestDataHelper.CreateSampleEmbedding();

        // Act
        var similarity = TestDataHelper.CalculateCosineSimilarity(vector, vector);

        // Assert
        similarity.Should().BeApproximately(1.0f, 0.001f);
    }

    [Test]
    public void CalculateCosineSimilarity_WithOppositeVectors_ShouldReturnNegativeOne()
    {
        // Arrange
        var vector1 = new float[] { 1.0f, 0.0f, 0.0f };
        var vector2 = new float[] { -1.0f, 0.0f, 0.0f };

        // Act
        var similarity = TestDataHelper.CalculateCosineSimilarity(vector1, vector2);

        // Assert
        similarity.Should().BeApproximately(-1.0f, 0.001f);
    }

    [Test]
    public void CalculateCosineSimilarity_WithOrthogonalVectors_ShouldReturnZero()
    {
        // Arrange
        var vector1 = new float[] { 1.0f, 0.0f, 0.0f };
        var vector2 = new float[] { 0.0f, 1.0f, 0.0f };

        // Act
        var similarity = TestDataHelper.CalculateCosineSimilarity(vector1, vector2);

        // Assert
        similarity.Should().BeApproximately(0.0f, 0.001f);
    }

    [Test]
    public void CreateTestEmbeddingSet_ShouldCreateValidRelationships()
    {
        // Act
        var (query, similar, different) = TestDataHelper.CreateTestEmbeddingSet();

        // Assert
        query.ShouldBeValidEmbedding();
        similar.ShouldBeValidEmbedding();
        different.ShouldBeValidEmbedding();

        var querySimilarSimilarity = TestDataHelper.CalculateCosineSimilarity(query, similar);
        var queryDifferentSimilarity = TestDataHelper.CalculateCosineSimilarity(query, different);

        querySimilarSimilarity.Should().BeGreaterThan(queryDifferentSimilarity);
        querySimilarSimilarity.Should().BeGreaterThan(0.5f);
    }
}

[TestFixture]
public class TestAssertionsTests
{
    [Test]
    public void ShouldBeValidEmbedding_WithValidEmbedding_ShouldNotThrow()
    {
        // Arrange
        var validEmbedding = TestDataHelper.CreateSampleEmbedding();

        // Act & Assert
        validEmbedding.ShouldBeValidEmbedding();
    }

    [Test]
    public void ShouldBeValidEmbedding_WithInvalidEmbedding_ShouldThrow()
    {
        // Arrange
        var invalidEmbedding = new float[768]; // All zeros

        // Act & Assert
        Action act = () => invalidEmbedding.ShouldBeValidEmbedding();
        act.Should().Throw<Exception>();
    }

    [Test]
    public void ShouldBeValidSearchResult_WithValidResult_ShouldNotThrow()
    {
        // Arrange
        var validResult = TestDataHelper.CreateSampleSearchResults().First();

        // Act & Assert
        validResult.ShouldBeValidSearchResult();
    }

    [Test]
    public void ShouldBeValidMetrics_WithValidMetrics_ShouldNotThrow()
    {
        // Arrange
        var validMetrics = TestDataHelper.CreateSampleMetrics();

        // Act & Assert
        validMetrics.ShouldBeValidMetrics();
    }

    [Test]
    public void ShouldHaveReasonableTokenEstimate_WithValidEstimate_ShouldNotThrow()
    {
        // Arrange
        var text = "This is a test sentence with some words."; // ~10 words, ~42 characters
        var tokenEstimate = text.Length / 4; // ~10 tokens

        // Act & Assert
        tokenEstimate.ShouldHaveReasonableTokenEstimate(text.Length);
    }
}
