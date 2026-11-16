using System.Net.Http.Json;
using System.Text.Json;

namespace PawsPost.Features.Ai.Gemini;

internal static class GeminiHttp
{
    // The key goes in a header, never in the URL, so it cannot end up in request logs.
    private const string ApiKeyHeader = "x-goog-api-key";

    // Every failure becomes an AiProviderException with a neutral message. Response bodies are never copied into it.
    public static async Task<TResponse> PostAsync<TResponse>(
        HttpClient httpClient, GeminiOptions options, string path, object body, CancellationToken cancellationToken)
        where TResponse : class
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new AiProviderException("The AI provider is not configured.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.BaseUrl.TrimEnd('/')}/{path}")
            {
                Content = JsonContent.Create(body, body.GetType())
            };
            request.Headers.Add(ApiKeyHeader, options.ApiKey);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new AiProviderException($"The AI provider answered with status {(int)response.StatusCode}.");
            }

            return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken)
                ?? throw new AiProviderException("The AI provider returned an empty response.");
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // The inner message is not used: it can hold the request URL.
            throw new AiProviderException($"The AI provider call failed ({exception.GetType().Name}).");
        }
    }
}
