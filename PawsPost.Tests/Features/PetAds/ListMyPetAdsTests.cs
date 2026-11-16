using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PawsPost.Data;
using PawsPost.Tests.Infrastructure;
using static PawsPost.Tests.Infrastructure.PetAdTestHelpers;

namespace PawsPost.Tests.Features.PetAds;

public class ListMyPetAdsTests
{
    private static readonly DateTime Day1 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    // Success

    [Fact]
    public async Task ListMyPetAds_OwnActiveAds_Returns200WithThoseAds()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var ownerId = await client.GetMyIdAsync();
        var first = BuildPetAd(ownerId, Day1, title: "First");
        var second = BuildPetAd(ownerId, Day1.AddDays(1), title: "Second");
        await factory.SeedAdsAsync(first, second);

        // Act
        var response = await client.GetMyPetAdsAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal([second.Id, first.Id], ItemIds(json));
        Assert.Equal(["Second", "First"], json.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("title").GetString()).ToList());
        Assert.Equal(2, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ListMyPetAds_OtherUsersActiveAds_AreNotListed()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var ownerId = await client.GetMyIdAsync();
        var otherId = await factory.SeedOwnerAsync("other@x.com", "Boris");
        var mine = BuildPetAd(ownerId, Day1);
        await factory.SeedAdsAsync(mine, BuildPetAd(otherId, Day1));

        // Act
        var response = await client.GetMyPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal([mine.Id], ItemIds(json));
        Assert.Equal(1, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData(PetAdStatus.Draft)]
    [InlineData(PetAdStatus.Reserved)]
    [InlineData(PetAdStatus.Closed)]
    public async Task ListMyPetAds_OwnNonActiveAds_AreNotListed(PetAdStatus status)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var ownerId = await client.GetMyIdAsync();
        var active = BuildPetAd(ownerId, Day1);
        await factory.SeedAdsAsync(active, BuildPetAd(ownerId, Day1, status: status));

        // Act
        var response = await client.GetMyPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal([active.Id], ItemIds(json));
        Assert.Equal(1, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ListMyPetAds_SeveralAds_SortsNewestFirst()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var ownerId = await client.GetMyIdAsync();
        var oldest = BuildPetAd(ownerId, Day1);
        var middle = BuildPetAd(ownerId, Day1.AddDays(1));
        var newest = BuildPetAd(ownerId, Day1.AddDays(2));
        await factory.SeedAdsAsync(middle, oldest, newest);

        // Act
        var response = await client.GetMyPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal([newest.Id, middle.Id, oldest.Id], ItemIds(json));
    }

    [Fact]
    public async Task ListMyPetAds_AdItem_HasSameFieldsAsListItem()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var ownerId = await client.GetMyIdAsync();
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1, type: PetAdType.Sale, price: 10m));

        // Act
        var mineResponse = await client.GetMyPetAdsAsync();
        var listResponse = await client.GetPetAdsAsync();

        // Assert
        using var mine = await ReadJsonAsync(mineResponse);
        using var list = await ReadJsonAsync(listResponse);
        var mineItem = mine.RootElement.GetProperty("items").EnumerateArray().Single();
        var listItem = list.RootElement.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(listItem.GetRawText(), mineItem.GetRawText());
        Assert.Equal(ownerId, mineItem.GetProperty("creator").GetProperty("id").GetGuid());
        Assert.Equal("Anna K", mineItem.GetProperty("creator").GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task ListMyPetAds_OnlyClosedAd_ReturnsEmptyListWithTotalCountZero()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var ownerId = await client.GetMyIdAsync();
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1, status: PetAdStatus.Closed));

        // Act
        var response = await client.GetMyPetAdsAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(0, json.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(0, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ListMyPetAds_NoAdsAtAll_ReturnsEmptyListWithTotalCountZero()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");

        // Act
        var response = await client.GetMyPetAdsAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(0, json.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(0, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    // Authorization

    [Fact]
    public async Task ListMyPetAds_NotLoggedIn_Returns401ProblemDetails()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetMyPetAdsAsync();

        // Assert
        await AssertUnauthorizedProblemAsync(response);
    }

    [Fact]
    public async Task ListMyPetAds_NotLoggedInWithInvalidPage_Returns401()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetMyPetAdsAsync("page=0&pageSize=500");

        // Assert
        await AssertUnauthorizedProblemAsync(response);
    }

    [Fact]
    public async Task ListMyPetAds_TamperedCookie_Returns401()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{PetAdsUrl}/mine");
        request.Headers.Add("Cookie", "pawspost.auth=tampered-value-that-is-not-valid");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        await AssertUnauthorizedProblemAsync(response);
    }

    [Fact]
    public async Task ListMyPetAds_TwoUsersWithAds_EachSeesOnlyOwnAds()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var clientA = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var clientB = await factory.CreateLoggedInClientAsync("other@x.com", "Boris");
        var adA = BuildPetAd(await clientA.GetMyIdAsync(), Day1, title: "A ad");
        var adB = BuildPetAd(await clientB.GetMyIdAsync(), Day1, title: "B ad");
        await factory.SeedAdsAsync(adA, adB);

        // Act
        var responseA = await clientA.GetMyPetAdsAsync();
        var responseB = await clientB.GetMyPetAdsAsync();

        // Assert
        using var jsonA = await ReadJsonAsync(responseA);
        using var jsonB = await ReadJsonAsync(responseB);
        Assert.Equal([adA.Id], ItemIds(jsonA));
        Assert.Equal([adB.Id], ItemIds(jsonB));
    }

    [Fact]
    public async Task ListMyPetAds_OwnerIdInQuery_IsIgnored()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var clientA = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var clientB = await factory.CreateLoggedInClientAsync("other@x.com", "Boris");
        var idA = await clientA.GetMyIdAsync();
        var adA = BuildPetAd(idA, Day1);
        var adB = BuildPetAd(await clientB.GetMyIdAsync(), Day1);
        await factory.SeedAdsAsync(adA, adB);

        // Act
        var response = await clientB.GetMyPetAdsAsync($"ownerId={idA}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal([adB.Id], ItemIds(json));
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
