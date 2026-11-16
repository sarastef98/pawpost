using Microsoft.EntityFrameworkCore;
using PawsPost.Data;
using PawsPost.Features.Ai;

namespace PawsPost.Features.Assistant;

public class PetAdReindexService(
    AppDbContext dbContext,
    IVectorStore vectorStore,
    IPetAdSearchIndexer indexer,
    ILogger<PetAdReindexService> logger) : IPetAdReindexService
{
    /// <summary>
    /// How many ids are sent to <see cref="IVectorStore.GetIndexedIdsAsync"/> per call.
    /// The SQLite store turns them into an <c>IN (...)</c> list, and SQLite limits bound parameters
    /// (999 in older builds), so a big catalogue is checked in chunks.
    /// </summary>
    public const int IndexedIdsBatchSize = 400;

    public async Task<PetAdReindexResult> ReindexMissingAsync(CancellationToken cancellationToken)
    {
        var activeIds = await dbContext.PetAds
            .AsNoTracking()
            .Where(ad => ad.Status == PetAdStatus.Active)
            .OrderBy(ad => ad.CreatedAt)
            .ThenBy(ad => ad.Id)
            .Select(ad => ad.Id)
            .ToListAsync(cancellationToken);

        var indexedIds = new HashSet<Guid>();
        foreach (var chunk in activeIds.Chunk(IndexedIdsBatchSize))
        {
            indexedIds.UnionWith(await vectorStore.GetIndexedIdsAsync(chunk, cancellationToken));
        }

        var workList = activeIds.Where(id => !indexedIds.Contains(id)).ToList();

        var indexed = 0;
        var failed = 0;

        foreach (var id in workList)
        {
            // Loaded again, one at a time, so an ad closed since the first query is skipped.
            // This is one query per missing ad (N+1) on purpose: the re-check of Active matters more
            // than speed for a one-off maintenance command (plan decision D4).
            // Only the public fields are read; owner data never leaves the database here.
            var ad = await dbContext.PetAds
                .AsNoTracking()
                .Where(item => item.Id == id && item.Status == PetAdStatus.Active)
                .Select(item => new PetAdSearchText(
                    item.Id, item.Title, item.Type, item.Species, item.Breed,
                    item.AgeInMonths, item.Description, item.Location))
                .SingleOrDefaultAsync(cancellationToken);

            if (ad is null)
            {
                continue;
            }

            try
            {
                await indexer.IndexAsync(ad, cancellationToken);
                indexed++;
            }
            catch (AiProviderException exception)
            {
                failed++;
                logger.LogWarning(
                    "Could not index pet ad {PetAdId}: {ExceptionType}", id, exception.GetType().Name);
            }
        }

        var result = new PetAdReindexResult(workList.Count, indexed, failed);
        logger.LogInformation(
            "Re-index finished: {Missing} without a vector, {Indexed} indexed, {Failed} failed",
            result.Missing, result.Indexed, result.Failed);
        return result;
    }
}
