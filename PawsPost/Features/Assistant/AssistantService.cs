using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PawsPost.Data;
using PawsPost.Features.Ai;
using PawsPost.Features.PetAds;

namespace PawsPost.Features.Assistant;

public class AssistantService(
    AppDbContext dbContext,
    IEmbeddingProvider embeddingProvider,
    IChatProvider chatProvider,
    IVectorStore vectorStore,
    IOptions<AssistantOptions> options,
    ILogger<AssistantService> logger) : IAssistantService
{
    public const string NoMatchAnswer = "Sorry, I could not find any ads that match your question.";

    // Extra candidates leave room for ads that are filtered out because they are no longer Active.
    private const int CandidateFactor = 4;

    public async Task<AssistantSearchResult> SearchAsync(string question, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var trimmed = question.Trim();

        try
        {
            var queryVector = await embeddingProvider.EmbedAsync(trimmed, EmbeddingPurpose.Query, cancellationToken);
            var matches = await vectorStore.SearchAsync(
                queryVector, embeddingProvider.ModelName, settings.MaxResults * CandidateFactor,
                settings.MinSimilarity, cancellationToken);

            var ads = await LoadActiveAdsAsync(matches, settings.MaxResults, cancellationToken);
            if (ads.Count == 0)
            {
                // Nothing fits: no provider call, so no ad can be invented.
                return AssistantSearchResult.Success(new AssistantSearchResponse(NoMatchAnswer, []));
            }

            var searchTexts = ads
                .Select(ad => new PetAdSearchText(
                    ad.Id, ad.Title, ad.Type, ad.Species, ad.Breed, ad.AgeInMonths, ad.Description, ad.Location))
                .ToList();
            var prompt = AssistantPromptBuilder.Build(trimmed, searchTexts);
            var answer = await chatProvider.GenerateAnswerAsync(prompt, cancellationToken);

            // The ad list always comes from the database, never from the text the model wrote.
            return AssistantSearchResult.Success(new AssistantSearchResponse(answer, ads));
        }
        catch (AiProviderException exception)
        {
            // Only the exception type and its neutral message are logged (for example "answered with status 404").
            // Never the question, ad text, URL or response body: GeminiHttp keeps those out of the message.
            logger.LogWarning(
                "The AI provider failed during an assistant search ({ExceptionType}): {Reason}",
                exception.GetType().Name, exception.Message);
            return AssistantSearchResult.Unavailable();
        }
    }

    private async Task<List<PetAdDetailsResponse>> LoadActiveAdsAsync(
        IReadOnlyList<VectorMatch> matches, int maxResults, CancellationToken cancellationToken)
    {
        if (matches.Count == 0)
        {
            return [];
        }

        var ids = matches.Select(match => match.PetAdId).ToList();

        // Only Active ads: this holds even if a vector was never removed. Only the creator id and display name are read from the owner.
        var rows = await dbContext.PetAds
            .AsNoTracking()
            .Where(petAd => ids.Contains(petAd.Id) && petAd.Status == PetAdStatus.Active)
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
            .ToListAsync(cancellationToken);

        var byId = rows.ToDictionary(row => row.Id);

        // Keep the vector store order (best match first).
        return ids
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .Take(maxResults)
            .Select(row => new PetAdDetailsResponse(
                row.Id, row.Title, row.Type, row.Species, row.Breed, row.AgeInMonths, row.Description,
                row.Price, row.Location, row.Status, DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc),
                new PetAdCreatorResponse(row.CreatorId, row.CreatorDisplayName)))
            .ToList();
    }
}
