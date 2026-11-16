namespace PawsPost.Features.Ai;

public interface IEmbeddingProvider
{
    /// <summary>The embedding model name. Stored with each vector so vectors of other models are ignored.</summary>
    string ModelName { get; }

    /// <exception cref="AiProviderException">The provider call failed.</exception>
    Task<float[]> EmbedAsync(string text, EmbeddingPurpose purpose, CancellationToken cancellationToken);
}
