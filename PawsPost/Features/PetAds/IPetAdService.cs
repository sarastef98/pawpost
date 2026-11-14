namespace PawsPost.Features.PetAds;

public interface IPetAdService
{
    Task<PetAdResponse> CreateAsync(Guid ownerId, CreatePetAdRequest request, CancellationToken cancellationToken);
}
