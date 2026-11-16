using Microsoft.Extensions.Options;

namespace PawsPost.Features.Ai.Gemini;

public class GeminiEmbeddingProvider(HttpClient httpClient, IOptions<GeminiOptions> options) : IEmbeddingProvider
{
    private readonly GeminiOptions _options = options.Value;

    public string ModelName => _options.EmbeddingModel;

    public async Task<float[]> EmbedAsync(string text, EmbeddingPurpose purpose, CancellationToken cancellationToken)
    {
        var taskType = purpose == EmbeddingPurpose.Query ? "RETRIEVAL_QUERY" : "RETRIEVAL_DOCUMENT";
        var body = new GeminiEmbedRequest(new GeminiContent([new GeminiPart(text)]), taskType);

        var response = await GeminiHttp.PostAsync<GeminiEmbedResponse>(
            httpClient, _options, $"models/{_options.EmbeddingModel}:embedContent", body, cancellationToken);

        var values = response.Embedding?.Values;
        if (values is null || values.Length == 0)
        {
            throw new AiProviderException("The embedding provider returned no vector.");
        }

        return values;
    }
}
