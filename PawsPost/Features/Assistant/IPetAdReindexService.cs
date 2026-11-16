namespace PawsPost.Features.Assistant;

public interface IPetAdReindexService
{
    /// <summary>Creates a vector for every <c>Active</c> ad that has none. Safe to run more than once.</summary>
    Task<PetAdReindexResult> ReindexMissingAsync(CancellationToken cancellationToken);
}
