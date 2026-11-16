using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PawsPost.Data;
using PawsPost.Features.Ai;
using PawsPost.Features.Assistant;

namespace PawsPost.Features.PetAds;

public class PetAdService(
    AppDbContext dbContext,
    TimeProvider timeProvider,
    IPetAdSearchIndexer searchIndexer,
    ILogger<PetAdService> logger) : IPetAdService
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
            Species = request.Species!.Value,
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

        await TryIndexAsync(ad, cancellationToken);

        return new PetAdResponse(
            ad.Id, ad.Title, ad.Type, ad.Species, ad.Breed, ad.AgeInMonths, ad.Description,
            ad.Price, ad.Location, ad.Status, ad.OwnerId, ad.CreatedAt);
    }

    public async Task<PetAdListResponse> ListAsync(ListPetAdsRequest request, CancellationToken cancellationToken)
    {
        var query = dbContext.PetAds
            .AsNoTracking()
            .Where(petAd => petAd.Status == PetAdStatus.Active);

        var species = request.GetParsedSpecies();
        if (species is not null)
        {
            query = query.Where(petAd => petAd.Species == species);
        }

        var type = request.GetParsedType();
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
            .Select(ListItemSelector)
            .ToListAsync(cancellationToken);

        return new PetAdListResponse(rows.Select(ToListItem).ToList(), totalCount, request.Page, request.PageSize);
    }

    public async Task<PetAdListResponse> ListActiveByOwnerAsync(
        Guid ownerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.PetAds
            .AsNoTracking()
            .Where(petAd => petAd.OwnerId == ownerId && petAd.Status == PetAdStatus.Active);

        var totalCount = await query.CountAsync(cancellationToken);

        // long math: a huge page number must not overflow the int that Skip takes.
        var offset = ((long)page - 1) * pageSize;
        if (offset >= totalCount)
        {
            return new PetAdListResponse([], totalCount, page, pageSize);
        }

        var rows = await query
            .OrderByDescending(petAd => petAd.CreatedAt)
            .ThenByDescending(petAd => petAd.Id)
            .Skip((int)offset)
            .Take(pageSize)
            .Select(ListItemSelector)
            .ToListAsync(cancellationToken);

        return new PetAdListResponse(rows.Select(ToListItem).ToList(), totalCount, page, pageSize);
    }

    public async Task<PetAdDetailsResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        // Only Active ads are visible, to everyone. A missing ad and a non-Active ad look the same (null).
        var row = await dbContext.PetAds
            .AsNoTracking()
            .Where(petAd => petAd.Id == id && petAd.Status == PetAdStatus.Active)
            .Select(petAd => new
            {
                petAd.Id,
                petAd.Title,
                petAd.Type,
                petAd.Species,
                petAd.Breed,
                petAd.AgeInMonths,
                petAd.Description,
                petAd.Price,
                petAd.Location,
                petAd.Status,
                petAd.CreatedAt,
                CreatorId = petAd.OwnerId,
                CreatorDisplayName = petAd.Owner!.DisplayName
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        // SQLite returns DateTime with Kind Unspecified. Mark it as UTC so the JSON ends with "Z".
        return new PetAdDetailsResponse(
            row.Id, row.Title, row.Type, row.Species, row.Breed, row.AgeInMonths, row.Description,
            row.Price, row.Location, row.Status, DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc),
            new PetAdCreatorResponse(row.CreatorId, row.CreatorDisplayName));
    }

    public async Task<ClosePetAdOutcome> CloseAsync(
        Guid petAdId, Guid callerId, CancellationToken cancellationToken)
    {
        // Tracked on purpose: the ad is updated below.
        var ad = await dbContext.PetAds.SingleOrDefaultAsync(petAd => petAd.Id == petAdId, cancellationToken);
        if (ad is null)
        {
            return ClosePetAdOutcome.NotFound;
        }

        // Ownership is checked before the status, so a non-owner never learns the status.
        if (ad.OwnerId != callerId)
        {
            return ClosePetAdOutcome.NotOwner;
        }

        if (ad.Status != PetAdStatus.Active)
        {
            return ClosePetAdOutcome.NotActive;
        }

        // Soft close: the row stays.
        ad.Status = PetAdStatus.Closed;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ClosePetAdOutcome.Closed;
    }

    // One projection for every list of ads, so all list items have exactly the same public fields.
    // It reads only the creator's id and display name, never the email, phone or user name.
    private static readonly Expression<Func<PetAd, ListItemRow>> ListItemSelector = petAd => new ListItemRow(
        petAd.Id,
        petAd.Title,
        petAd.Type,
        petAd.Species,
        petAd.Price,
        petAd.Location,
        petAd.CreatedAt,
        petAd.OwnerId,
        petAd.Owner!.DisplayName);

    private static PetAdListItemResponse ToListItem(ListItemRow row)
    {
        // SQLite returns DateTime with Kind Unspecified. Mark it as UTC so the JSON ends with "Z".
        return new PetAdListItemResponse(
            row.Id, row.Title, row.Type, row.Species, row.Price, row.Location,
            DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc),
            new PetAdCreatorResponse(row.CreatorId, row.CreatorDisplayName));
    }

    private sealed record ListItemRow(
        Guid Id,
        string Title,
        PetAdType Type,
        PetSpecies Species,
        decimal? Price,
        string? Location,
        DateTime CreatedAt,
        Guid CreatorId,
        string CreatorDisplayName);

    // The ad is already saved. If the AI provider fails, the ad stays and is just not searchable yet.
    // Only provider failures are caught here. A storage (database) failure is unexpected and is not hidden.
    // Only the ad id and the exception type are logged, never ad text or provider details.
    private async Task TryIndexAsync(PetAd ad, CancellationToken cancellationToken)
    {
        try
        {
            await searchIndexer.IndexAsync(
                new PetAdSearchText(
                    ad.Id, ad.Title, ad.Type, ad.Species, ad.Breed, ad.AgeInMonths, ad.Description, ad.Location),
                cancellationToken);
        }
        catch (AiProviderException exception)
        {
            logger.LogWarning(
                "Could not embed pet ad {PetAdId} ({ExceptionType}): {Reason} It is not searchable yet.",
                ad.Id, exception.GetType().Name, exception.Message);
        }
    }

    private static string? NullIfEmpty(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
