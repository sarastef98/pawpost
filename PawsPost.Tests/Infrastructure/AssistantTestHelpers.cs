using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PawsPost.Data;
using PawsPost.Features.Assistant;

namespace PawsPost.Tests.Infrastructure;

public static class AssistantTestHelpers
{
    public const string SearchUrl = "/api/assistant/search";

    public static Task<HttpResponseMessage> PostAssistantSearchAsync(this HttpClient client, object body)
    {
        return client.PostAsJsonAsync(SearchUrl, body);
    }

    /// <summary>
    /// A synthetic ad for search tests. The fake embedding provider only knows a few words
    /// (see <c>FakeEmbeddingProvider.Vocabulary</c>), so the species and description decide what matches.
    /// </summary>
    public static PetAd BuildSearchAd(
        Guid ownerId,
        string title,
        PetSpecies species = PetSpecies.Dog,
        string description = "Friendly and healthy.",
        PetAdStatus status = PetAdStatus.Active,
        int dayOffset = 0)
    {
        var ad = PetAdTestHelpers.BuildPetAd(
            ownerId, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(dayOffset),
            title: title, species: species, status: status,
            location: null); // the label "Location" contains the vocabulary word "cat", so it is left out
        ad.Description = description;
        return ad;
    }

    /// <summary>Indexes the ads through the real indexer (fake provider and fake store).</summary>
    public static async Task IndexAdsAsync(this WebApplicationFactory<Program> factory, params PetAd[] ads)
    {
        using var scope = factory.Services.CreateScope();
        var indexer = scope.ServiceProvider.GetRequiredService<IPetAdSearchIndexer>();
        foreach (var ad in ads)
        {
            await indexer.IndexAsync(ToSearchText(ad), CancellationToken.None);
        }
    }

    public static async Task RemoveFromIndexAsync(this WebApplicationFactory<Program> factory, Guid petAdId)
    {
        using var scope = factory.Services.CreateScope();
        var indexer = scope.ServiceProvider.GetRequiredService<IPetAdSearchIndexer>();
        await indexer.RemoveAsync(petAdId, CancellationToken.None);
    }

    /// <summary>Saves the ads in the database and indexes them.</summary>
    public static async Task SeedAndIndexAdsAsync(this WebApplicationFactory<Program> factory, params PetAd[] ads)
    {
        await factory.SeedAdsAsync(ads);
        await factory.IndexAdsAsync(ads);
    }

    public static async Task SetStatusAsync(this WebApplicationFactory<Program> factory, Guid petAdId, PetAdStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ad = await db.PetAds.FindAsync(petAdId);
        ad!.Status = status;
        await db.SaveChangesAsync();
    }

    /// <summary>Runs the re-embedding step once, in its own scope.</summary>
    public static async Task<PetAdReindexResult> ReindexAsync(this WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPetAdReindexService>();
        return await service.ReindexMissingAsync(CancellationToken.None);
    }

    public static PetAdSearchText ToSearchText(PetAd ad) =>
        new(ad.Id, ad.Title, ad.Type, ad.Species, ad.Breed, ad.AgeInMonths, ad.Description, ad.Location);
}
