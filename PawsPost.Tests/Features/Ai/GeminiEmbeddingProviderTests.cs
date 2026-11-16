using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using PawsPost.Features.Ai;
using PawsPost.Features.Ai.Gemini;

namespace PawsPost.Tests.Features.Ai;

public class GeminiEmbeddingProviderTests
{
    [Fact]
    public async Task EmbedAsync_NonJsonResponse_ThrowsAiProviderException()
    {
        // Arrange
        var handler = new StubHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>proxy error</html>", Encoding.UTF8, "text/html")
        });
        using var httpClient = new HttpClient(handler);
        var options = Options.Create(new GeminiOptions
        {
            ApiKey = "test-key",
            BaseUrl = "https://example.test/v1beta",
            EmbeddingModel = "test-model"
        });
        var provider = new GeminiEmbeddingProvider(httpClient, options);

        // Act
        var exception = await Record.ExceptionAsync(
            () => provider.EmbedAsync("A calm dog.", EmbeddingPurpose.Document, CancellationToken.None));

        // Assert
        var providerException = Assert.IsType<AiProviderException>(exception);
        Assert.DoesNotContain("proxy error", providerException.Message);
        Assert.DoesNotContain("example.test", providerException.Message);
    }

    private sealed class StubHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond());
    }
}
