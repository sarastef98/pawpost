using Microsoft.Extensions.Options;

namespace PawsPost.Features.Ai.Gemini;

public class GeminiChatProvider(HttpClient httpClient, IOptions<GeminiOptions> options) : IChatProvider
{
    private readonly GeminiOptions _options = options.Value;

    public async Task<string> GenerateAnswerAsync(ChatPrompt prompt, CancellationToken cancellationToken)
    {
        var body = new GeminiGenerateRequest(
            new GeminiContent([new GeminiPart(prompt.SystemInstruction)]),
            [new GeminiContent([new GeminiPart(prompt.UserContent)], "user")]);

        var response = await GeminiHttp.PostAsync<GeminiGenerateResponse>(
            httpClient, _options, $"models/{_options.ChatModel}:generateContent", body, cancellationToken);

        var text = string.Concat(
            response.Candidates?.FirstOrDefault()?.Content?.Parts?.Select(part => part.Text) ?? []);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new AiProviderException("The chat provider returned no answer.");
        }

        return text.Trim();
    }
}
