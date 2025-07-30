using FluentAssertions;
using Newtonsoft.Json;
using OllamaRedisImageSearch.Models;

namespace OllamaRedisImageSearch.Tests.Models;

[TestFixture]
public class OllamaModelsTests
{
    [TestFixture]
    public class OllamaChatRequestTests
    {
        [Test]
        public void OllamaChatRequest_ShouldInitializeWithDefaults()
        {
            // Arrange & Act
            var request = new OllamaChatRequest();

            // Assert
            request.Model.Should().Be(string.Empty);
            request.Messages.Should().NotBeNull().And.BeEmpty();
            request.Stream.Should().BeFalse();
        }

        [Test]
        public void OllamaChatRequest_ShouldSerializeCorrectly()
        {
            // Arrange
            var request = new OllamaChatRequest
            {
                Model = "llava",
                Messages = new List<OllamaMessage>
                {
                    new() { Role = "user", Content = "Test message" }
                },
                Stream = false
            };

            // Act
            var json = JsonConvert.SerializeObject(request);
            var deserialized = JsonConvert.DeserializeObject<OllamaChatRequest>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Model.Should().Be("llava");
            deserialized.Messages.Should().HaveCount(1);
            deserialized.Messages[0].Role.Should().Be("user");
            deserialized.Messages[0].Content.Should().Be("Test message");
            deserialized.Stream.Should().BeFalse();
        }
    }

    [TestFixture]
    public class OllamaMessageTests
    {
        [Test]
        public void OllamaMessage_ShouldInitializeWithDefaults()
        {
            // Arrange & Act
            var message = new OllamaMessage();

            // Assert
            message.Role.Should().Be(string.Empty);
            message.Content.Should().Be(string.Empty);
            message.Images.Should().BeNull();
        }

        [Test]
        public void OllamaMessage_WithImages_ShouldSerializeCorrectly()
        {
            // Arrange
            var message = new OllamaMessage
            {
                Role = "user",
                Content = "Describe this image",
                Images = new List<string> { "base64imagedata" }
            };

            // Act
            var json = JsonConvert.SerializeObject(message);
            var deserialized = JsonConvert.DeserializeObject<OllamaMessage>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Role.Should().Be("user");
            deserialized.Content.Should().Be("Describe this image");
            deserialized.Images.Should().NotBeNull().And.HaveCount(1);
            deserialized.Images![0].Should().Be("base64imagedata");
        }
    }

    [TestFixture]
    public class OllamaChatResponseTests
    {
        [Test]
        public void OllamaChatResponse_ShouldDeserializeCorrectly()
        {
            // Arrange
            var json = """
                {
                    "message": {
                        "role": "assistant",
                        "content": "This is a test response"
                    },
                    "done": true
                }
                """;

            // Act
            var response = JsonConvert.DeserializeObject<OllamaChatResponse>(json);

            // Assert
            response.Should().NotBeNull();
            response!.Message.Should().NotBeNull();
            response.Message!.Role.Should().Be("assistant");
            response.Message.Content.Should().Be("This is a test response");
            response.Done.Should().BeTrue();
        }
    }

    [TestFixture]
    public class OllamaEmbeddingRequestTests
    {
        [Test]
        public void OllamaEmbeddingRequest_ShouldInitializeWithDefaults()
        {
            // Arrange & Act
            var request = new OllamaEmbeddingRequest();

            // Assert
            request.Model.Should().Be(string.Empty);
            request.Prompt.Should().Be(string.Empty);
        }

        [Test]
        public void OllamaEmbeddingRequest_ShouldSerializeCorrectly()
        {
            // Arrange
            var request = new OllamaEmbeddingRequest
            {
                Model = "nomic-embed-text",
                Prompt = "Test prompt for embedding"
            };

            // Act
            var json = JsonConvert.SerializeObject(request);
            var deserialized = JsonConvert.DeserializeObject<OllamaEmbeddingRequest>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Model.Should().Be("nomic-embed-text");
            deserialized.Prompt.Should().Be("Test prompt for embedding");
        }
    }

    [TestFixture]
    public class OllamaEmbeddingResponseTests
    {
        [Test]
        public void OllamaEmbeddingResponse_ShouldInitializeWithDefaults()
        {
            // Arrange & Act
            var response = new OllamaEmbeddingResponse();

            // Assert
            response.Embedding.Should().NotBeNull().And.BeEmpty();
        }

        [Test]
        public void OllamaEmbeddingResponse_ShouldDeserializeCorrectly()
        {
            // Arrange
            var json = """
                {
                    "embedding": [0.1, 0.2, 0.3, 0.4, 0.5]
                }
                """;

            // Act
            var response = JsonConvert.DeserializeObject<OllamaEmbeddingResponse>(json);

            // Assert
            response.Should().NotBeNull();
            response!.Embedding.Should().NotBeNull().And.HaveCount(5);
            response.Embedding.Should().BeEquivalentTo(new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f });
        }

        [Test]
        public void OllamaEmbeddingResponse_WithLargeEmbedding_ShouldDeserializeCorrectly()
        {
            // Arrange - Simulate a 768-dimensional embedding
            var embedding = Enumerable.Range(0, 768).Select(i => i * 0.001f).ToArray();
            var embeddingJson = JsonConvert.SerializeObject(embedding);
            var json = $"{{\"embedding\": {embeddingJson}}}";

            // Act
            var response = JsonConvert.DeserializeObject<OllamaEmbeddingResponse>(json);

            // Assert
            response.Should().NotBeNull();
            response!.Embedding.Should().NotBeNull().And.HaveCount(768);
            response.Embedding[0].Should().Be(0f);
            response.Embedding[767].Should().Be(0.767f);
        }
    }
}
