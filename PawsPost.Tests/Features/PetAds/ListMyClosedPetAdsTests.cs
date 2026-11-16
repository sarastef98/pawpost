using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PawsPost.Data;
using PawsPost.Tests.Infrastructure;
using static PawsPost.Tests.Infrastructure.PetAdTestHelpers;

namespace PawsPost.Tests.Features.PetAds;

public class ListMyClosedPetAdsTests
{
    private static readonly DateTime Day1 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    // Success

    [Fact]
    public async Task ListMyClosedPetAds_OwnClosedAds_Returns200WithThoseAds()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var ownerId = await client.GetMyIdAsync();
        var first = BuildPetAd(ownerId, Day1, title: "First", status: PetAdStatus.Closed);
        var second = BuildPetAd(ownerId, Day1.AddDays(1), title: "Second", status: PetAdStatus.Closed);
        await factory.SeedAdsAsync(first, second);

        // Act
        var response = await client.GetMyClosedPetAdsAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal([second.Id, first.Id], ItemIds(json));
        Assert.Equal(["Second", "First"], json.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("title").GetString()).ToList());
        Assert.Equal(2, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ListMyClosedPetAds_OwnActiveAds_AreNotListed()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var ownerId = await client.GetMyIdAsync();
        var closed = BuildPetAd(ownerId, Day1, status: PetAdStatus.Closed);
        await factory.SeedAdsAsync(closed, BuildPetAd(ownerId, Day1, status: PetAdStatus.Active));

        // Act
        var response = await client.GetMyClosedPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal([closed.Id], ItemIds(json));
        Assert.Equal(1, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData(PetAdStatus.Draft)]
    [InlineData(PetAdStatus.Reserved)]
    public async Task ListMyClosedPetAds_OwnDraftOrReservedAds_AreNotListed(PetAdStatus status)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var ownerId = await client.GetMyIdAsync();
        var closed = BuildPetAd(ownerId, Day1, status: PetAdStatus.Closed);
        await factory.SeedAdsAsync(closed, BuildPetAd(ownerId, Day1, status: status));

        // Act
        var response = await client.GetMyClosedPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal([closed.Id], ItemIds(json));
        Assert.Equal(1, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ListMyClosedPetAds_OtherUsersClosedAds_AreNotListed()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var ownerId = await client.GetMyIdAsync();
        var otherId = await factory.SeedOwnerAsync("other@x.com", "Boris");
        var mine = BuildPetAd(ownerId, Day1, status: PetAdStatus.Closed);
        await factory.SeedAdsAsync(mine, BuildPetAd(otherId, Day1, status: PetAdStatus.Closed));

        // Act
        var response = await client.GetMyClosedPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal([mine.Id], ItemIds(json));
        Assert.Equal(1, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ListMyClosedPetAds_SeveralAds_SortsNewestFirstByCreatedAt()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var ownerId = await client.GetMyIdAsync();
        var oldest = BuildPetAd(ownerId, Day1, status: PetAdStatus.Closed);
        var middle = BuildPetAd(ownerId, Day1.AddDays(1), status: PetAdStatus.Closed);
        var newest = BuildPetAd(ownerId, Day1.AddDays(2), status: PetAdStatus.Closed);
        await factory.SeedAdsAsync(middle, oldest, newest);

        // Act
        var response = await client.GetMyClosedPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal([newest.Id, middle.Id, oldest.Id], ItemIds(json));
    }

    [Fact]
    public async Task ListMyClosedPetAds_SameCreatedAt_SortsByIdDescending()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var ownerId = await client.GetMyIdAsync();
        var lowId = BuildPetAd(ownerId, Day1, status: PetAdStatus.Closed,
            id: new Guid("00000000-0000-0000-0000-000000000001"));
        var highId = BuildPetAd(ownerId, Day1, status: PetAdStatus.Closed,
            id: new Guid("00000000-0000-0000-0000-000000000002"));
        await factory.SeedAdsAsync(lowId, highId);

        // Act
        var response = await client.GetMyClosedPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal([highId.Id, lowId.Id], ItemIds(json));
    }

    [Fact]
    public async Task ListMyClosedPetAds_NoClosedAds_ReturnsEmptyListWithTotalCountZero()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var ownerId = await client.GetMyIdAsync();
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1));

        // Act
        var response = await client.GetMyClosedPetAdsAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(0, json.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(0, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ListMyClosedPetAds_NoAdsAtAll_ReturnsEmptyListWithTotalCountZero()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");

        // Act
        var response = await client.GetMyClosedPetAdsAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(0, json.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(0, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ListMyClosedPetAds_AdItem_HasSameFieldsAsMyAdsItem()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var ownerId = await client.GetMyIdAsync();
        var ad = BuildPetAd(ownerId, Day1, type: PetAdType.Sale, price: 10m);
        await factory.SeedAdsAsync(ad);
        using var activeJson = await ReadJsonAsync(await client.GetMyPetAdsAsync());
        var activeItem = activeJson.RootElement.GetProperty("items").EnumerateArray().Single();
        var activeRaw = activeItem.GetRawText();
        await client.ClosePetAdAsync(ad.Id.ToString());

        // Act
        var response = await client.GetMyClosedPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        var closedItem = json.RootElement.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(activeRaw, closedItem.GetRawText());
        Assert.Equal(ownerId, closedItem.GetProperty("creator").GetProperty("id").GetGuid());
        Assert.Equal("Anna K", closedItem.GetProperty("creator").GetProperty("displayName").GetString());
        Assert.False(closedItem.TryGetProperty("status", out _));
    }

    // Authorization

    [Fact]
    public async Task ListMyClosedPetAds_NotLoggedIn_Returns401ProblemDetails()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetMyClosedPetAdsAsync();

        // Assert
        await AssertUnauthorizedProblemAsync(response);
    }

    [Fact]
    public async Task ListMyClosedPetAds_NotLoggedInWithInvalidPaging_Returns401()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetMyClosedPetAdsAsync("page=0&pageSize=500");

        // Assert
        await AssertUnauthorizedProblemAsync(response);
    }

    [Fact]
    public async Task ListMyClosedPetAds_TamperedCookie_Returns401()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{PetAdsUrl}/mine/closed");
        request.Headers.Add("Cookie", "pawspost.auth=tampered-value-that-is-not-valid");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        await AssertUnauthorizedProblemAsync(response);
    }

    [Fact]
    public async Task ListMyClosedPetAds_TwoUsersWithClosedAds_EachSeesOnlyOwnClosedAds()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var clientA = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var clientB = await factory.CreateLoggedInClientAsync("other@x.com", "Boris");
        var adA = BuildPetAd(await clientA.GetMyIdAsync(), Day1, title: "A ad", status: PetAdStatus.Closed);
        var adB = BuildPetAd(await clientB.GetMyIdAsync(), Day1, title: "B ad", status: PetAdStatus.Closed);
        await factory.SeedAdsAsync(adA, adB);

        // Act
        var responseA = await clientA.GetMyClosedPetAdsAsync();
        var responseB = await clientB.GetMyClosedPetAdsAsync();

        // Assert
        using var jsonA = await ReadJsonAsync(responseA);
        using var jsonB = await ReadJsonAsync(responseB);
        Assert.Equal([adA.Id], ItemIds(jsonA));
        Assert.Equal([adB.Id], ItemIds(jsonB));
    }

    [Fact]
    public async Task ListMyClosedPetAds_OwnerIdInQuery_IsIgnored()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var clientA = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var clientB = await factory.CreateLoggedInClientAsync("other@x.com", "Boris");
        var idA = await clientA.GetMyIdAsync();
        var adA = BuildPetAd(idA, Day1, status: PetAdStatus.Closed);
        var adB = BuildPetAd(await clientB.GetMyIdAsync(), Day1, status: PetAdStatus.Closed);
        await factory.SeedAdsAsync(adA, adB);

        // Act
        var response = await clientB.GetMyClosedPetAdsAsync($"ownerId={idA}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal([adB.Id], ItemIds(json));
    }

    // Routing

    [Fact]
    public async Task ListMyClosedPetAds_ClosedRoute_ReturnsList()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");

        // Act
        var response = await client.GetAsync($"{PetAdsUrl}/mine/closed");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("items").ValueKind);
        Assert.Equal(0, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    private static List<Guid> ItemIds(JsonDocument json)
    {
        return json.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid()).ToList();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static async Task AssertUnauthorizedProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(401, problem.Status);
    }
}
