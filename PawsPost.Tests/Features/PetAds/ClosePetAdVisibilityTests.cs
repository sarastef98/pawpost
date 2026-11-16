using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PawsPost.Features.Assistant;
using PawsPost.Tests.Infrastructure;
using static PawsPost.Tests.Infrastructure.AssistantTestHelpers;
using static PawsPost.Tests.Infrastructure.PetAdTestHelpers;

namespace PawsPost.Tests.Features.PetAds;

public class ClosePetAdVisibilityTests
{
    private const string OwnerEmail = "owner@x.com";
    private static readonly DateTime Day2 = new(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ClosePetAd_ClosedAd_IsNotInPublicList()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (client, ownerId, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail);
        var stillActive = BuildPetAd(ownerId, Day2, title: "Still active");
        await factory.SeedAdsAsync(stillActive);
        await client.ClosePetAdAsync(ad.Id.ToString());

        // Act
        var response = await factory.CreateApiClient().GetPetAdsAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal([stillActive.Id], ItemIds(json.RootElement));
        Assert.Equal(1, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ClosePetAd_ClosedAd_DetailsReturn404()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (client, _, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail);
        await client.ClosePetAdAsync(ad.Id.ToString());

        // Act
        var ownerResponse = await client.GetPetAdAsync(ad.Id.ToString());
        var otherUser = await factory.CreateLoggedInClientAsync("other@x.com");
        var otherResponse = await otherUser.GetPetAdAsync(ad.Id.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, ownerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);
    }

    [Fact]
    public async Task ClosePetAd_ClosedAd_IsNotInCreatorProfile()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (client, ownerId, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail);
        var stillActive = BuildPetAd(ownerId, Day2, title: "Still active");
        await factory.SeedAdsAsync(stillActive);
        await client.ClosePetAdAsync(ad.Id.ToString());

        // Act
        var response = await factory.CreateApiClient().GetCreatorAsync(ownerId.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal([stillActive.Id], ItemIds(json.RootElement.GetProperty("ads")));
    }

    [Fact]
    public async Task ClosePetAd_ClosedAd_IsNotInMyAds()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (client, ownerId, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail);
        var stillActive = BuildPetAd(ownerId, Day2, title: "Still active");
        await factory.SeedAdsAsync(stillActive);
        await client.ClosePetAdAsync(ad.Id.ToString());

        // Act
        var response = await client.GetMyPetAdsAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal([stillActive.Id], ItemIds(json.RootElement));
    }

    [Fact]
    public async Task ClosePetAd_ClosedAd_IsNotReturnedByAssistantSearch()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync(OwnerEmail);
        var ad = BuildSearchAd(await client.GetMyIdAsync(), "Calm dog", description: "Calm and quiet.");
        await factory.SeedAndIndexAdsAsync(ad);
        var before = await SearchIdsAsync(client);
        Assert.Contains(ad.Id, before);
        await client.ClosePetAdAsync(ad.Id.ToString());

        // Act
        var after = await SearchIdsAsync(client);

        // Assert
        Assert.DoesNotContain(ad.Id, after);
    }

    [Fact]
    public async Task ClosePetAd_IndexRemovalFailed_AssistantSearchStillSkipsClosedAd()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync(OwnerEmail);
        var ad = BuildSearchAd(await client.GetMyIdAsync(), "Calm dog", description: "Calm and quiet.");
        await factory.SeedAndIndexAdsAsync(ad);
        factory.VectorStore.RemoveExceptionToThrow = new InvalidOperationException("storage down");
        await client.ClosePetAdAsync(ad.Id.ToString());

        // Act
        var ids = await SearchIdsAsync(client);

        // Assert
        Assert.True(factory.VectorStore.Contains(ad.Id));
        Assert.DoesNotContain(ad.Id, ids);
    }

    private static async Task<List<Guid>> SearchIdsAsync(HttpClient client)
    {
        var response = await client.PostAssistantSearchAsync(new { question = "a calm dog" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AssistantSearchResponse>();
        return body!.Ads.Select(ad => ad.Id).ToList();
    }

    private static List<Guid> ItemIds(JsonElement list) =>
        list.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToList();

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());
}
