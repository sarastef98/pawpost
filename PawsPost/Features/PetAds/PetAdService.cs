using PawsPost.Data;

namespace PawsPost.Features.PetAds;

public class PetAdService(AppDbContext dbContext, TimeProvider timeProvider) : IPetAdService
{
    public async Task<PetAdResponse> CreateAsync(
        Guid ownerId, CreatePetAdRequest request, CancellationToken cancellationToken)
    {
        var type = request.Type!.Value;

        var ad = new PetAd
        {
            Id = Guid.NewGuid(),
            Type = type,
            Species = request.Species!.Trim(),
            Breed = NullIfEmpty(request.Breed),
            AgeInMonths = request.AgeInMonths!.Value,
            Description = request.Description!.Trim(),
            // The validator already rejects a price on Adoption. Only Sale keeps one.
            Price = type == PetAdType.Sale ? request.Price : null,
            Location = NullIfEmpty(request.Location),
            Status = PetAdStatus.Active,
            OwnerId = ownerId,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        dbContext.PetAds.Add(ad);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new PetAdResponse(
            ad.Id, ad.Type, ad.Species, ad.Breed, ad.AgeInMonths, ad.Description,
            ad.Price, ad.Location, ad.Status, ad.OwnerId, ad.CreatedAt);
    }

    private static string? NullIfEmpty(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
