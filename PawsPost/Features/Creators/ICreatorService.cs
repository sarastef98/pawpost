namespace PawsPost.Features.Creators;

public interface ICreatorService
{
    /// <summary>Returns the public profile, or null when no such creator exists.</summary>
    Task<CreatorProfileResponse?> GetProfileAsync(
        Guid id, CreatorAdsRequest request, CancellationToken cancellationToken);
}
