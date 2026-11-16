using PawsPost.Features.Ai;

namespace PawsPost.Tests.Infrastructure.Fakes;

/// <summary>
/// Deterministic embeddings: one dimension per vocabulary word, 1 if the text contains the word.
/// Records every input text. Can be told to fail.
/// </summary>
public class FakeEmbeddingProvider : IEmbeddingProvider
{
    public static readonly string[] Vocabulary =
        ["dog", "cat", "rabbit", "calm", "children", "puppy", "kitten", "small", "free"];

    private readonly List<(string Text, EmbeddingPurpose Purpose)> _calls = [];

    public string ModelName => "fake-embedding";

    public IReadOnlyList<(string Text, EmbeddingPurpose Purpose)> Calls => _calls;

    public Exception? ExceptionToThrow { get; set; }

    public Task<float[]> EmbedAsync(string text, EmbeddingPurpose purpose, CancellationToken cancellationToken)
    {
        _calls.Add((text, purpose));

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        var vector = Vocabulary
            .Select(word => text.Contains(word, StringComparison.OrdinalIgnoreCase) ? 1f : 0f)
            .ToArray();
        return Task.FromResult(vector);
    }
}
