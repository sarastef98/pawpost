using Microsoft.Extensions.Options;

namespace PawsPost.Features.Ai.Gemini;

public static class GeminiServiceCollectionExtensions
{
    public static IServiceCollection AddGeminiProviders(this IServiceCollection services, IConfiguration configuration)
    {
        // Checked at startup. A missing ApiKey is allowed: the providers then fail at call time.
        services
            .AddOptions<GeminiOptions>()
            .Bind(configuration.GetSection(GeminiOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.EmbeddingModel), "Ai:Gemini:EmbeddingModel is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ChatModel), "Ai:Gemini:ChatModel is required.")
            .Validate(
                options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
                "Ai:Gemini:BaseUrl must be an absolute https URL.")
            .Validate(options => options.TimeoutSeconds is >= 1 and <= 300, "Ai:Gemini:TimeoutSeconds must be between 1 and 300.")
            .ValidateOnStart();

        services.AddHttpClient<IEmbeddingProvider, GeminiEmbeddingProvider>(ConfigureClient);
        services.AddHttpClient<IChatProvider, GeminiChatProvider>(ConfigureClient);
        return services;
    }

    private static void ConfigureClient(IServiceProvider services, HttpClient client) =>
        client.Timeout = TimeSpan.FromSeconds(services.GetRequiredService<IOptions<GeminiOptions>>().Value.TimeoutSeconds);
}
