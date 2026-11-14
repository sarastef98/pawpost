using Microsoft.EntityFrameworkCore;
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
            Title = request.Title!.Trim(),
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
            ad.Id, ad.Title, ad.Type, ad.Species, ad.Breed, ad.AgeInMonths, ad.Description,
            ad.Price, ad.Location, ad.Status, ad.OwnerId, ad.CreatedAt);
    }

    public async Task<PetAdListResponse> ListAsync(ListPetAdsRequest request, CancellationToken cancellationToken)
    {
        var query = dbContext.PetAds
            .AsNoTracking()
            .Where(petAd => petAd.Status == PetAdStatus.Active);

        var species = request.Species?.Trim();
        if (!string.IsNullOrEmpty(species))
        {
            query = query.Where(petAd => EF.Functions.Collate(petAd.Species, "NOCASE") == species);
        }

        var type = request.ParsedType;
        if (type is not null)
        {
            query = query.Where(petAd => petAd.Type == type);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // long math: a huge page number must not overflow the int that Skip takes.
        var offset = ((long)request.Page - 1) * request.PageSize;
        if (offset >= totalCount)
        {
            return new PetAdListResponse([], totalCount, request.Page, request.PageSize);
        }

        var rows = await query
            .OrderByDescending(petAd => petAd.CreatedAt)
            .ThenByDescending(petAd => petAd.Id)
            .Skip((int)offset)
            .Take(request.PageSize)
            .Select(petAd => new
            {
                petAd.Id,
                petAd.Title,
                petAd.Type,
                petAd.Species,
                petAd.Price,
                petAd.Location,
                petAd.CreatedAt
            })
            .ToListAsync(cancellationToken);

        // SQLite returns DateTime with Kind Unspecified. Mark it as UTC so the JSON ends with "Z".
        var items = rows
            .Select(row => new PetAdListItemResponse(
                row.Id, row.Title, row.Type, row.Species, row.Price, row.Location,
                DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc)))
            .ToList();

        return new PetAdListResponse(items, totalCount, request.Page, request.PageSize);
    }

    private static string? NullIfEmpty(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
