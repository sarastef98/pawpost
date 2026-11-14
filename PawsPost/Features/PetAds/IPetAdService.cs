namespace PawsPost.Features.PetAds;

public interface IPetAdService
{
    Task<PetAdResponse> CreateAsync(Guid ownerId, CreatePetAdRequest request, CancellationToken cancellationToken);

    Task<PetAdListResponse> ListAsync(ListPetAdsRequest request, CancellationToken cancellationToken);

    Task<PetAdDetailsResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
