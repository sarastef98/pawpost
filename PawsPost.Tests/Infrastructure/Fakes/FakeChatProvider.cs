using PawsPost.Features.Ai;

namespace PawsPost.Tests.Infrastructure.Fakes;

/// <summary>Returns a fixed answer and records every prompt. Can be told to fail.</summary>
public class FakeChatProvider : IChatProvider
{
    public const string FixedAnswer = "Here are some ads that fit your question.";

    private readonly List<ChatPrompt> _prompts = [];

    public IReadOnlyList<ChatPrompt> Prompts => _prompts;

    public Exception? ExceptionToThrow { get; set; }

    public Task<string> GenerateAnswerAsync(ChatPrompt prompt, CancellationToken cancellationToken)
    {
        _prompts.Add(prompt);

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        return Task.FromResult(FixedAnswer);
    }
}
