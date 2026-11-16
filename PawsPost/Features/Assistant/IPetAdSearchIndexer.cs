namespace PawsPost.Features.Assistant;

public interface IPetAdSearchIndexer
{
    /// <exception cref="PawsPost.Features.Ai.AiProviderException">The embedding provider failed.</exception>
    Task IndexAsync(PetAdSearchText ad, CancellationToken cancellationToken);

    Task RemoveAsync(Guid petAdId, CancellationToken cancellationToken);
}
