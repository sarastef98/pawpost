namespace PawsPost.Features.Ai;

public interface IChatProvider
{
    /// <exception cref="AiProviderException">The provider call failed.</exception>
    Task<string> GenerateAnswerAsync(ChatPrompt prompt, CancellationToken cancellationToken);
}
