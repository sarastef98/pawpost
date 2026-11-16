using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PawsPost.Data;
using PawsPost.Features.Assistant;
using PawsPost.Tests.Infrastructure;
using static PawsPost.Tests.Infrastructure.AssistantTestHelpers;
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
        Assert.Equal([second.Id, first.Id], ItemIds(json.RootElement));
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
        Assert.Equal([closed.Id], ItemIds(json.RootElement));
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
        Assert.Equal([closed.Id], ItemIds(json.RootElement));
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
        Assert.Equal([mine.Id], ItemIds(json.RootElement));
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
        Assert.Equal([newest.Id, middle.Id, oldest.Id], ItemIds(json.RootElement));
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
        Assert.Equal([highId.Id, lowId.Id], ItemIds(json.RootElement));
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
        // A raw comparison is safe: PetAdListItemResponse has no field that changes on close (no updated-at).
        using var json = await ReadJsonAsync(response);
        var closedItem = json.RootElement.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(activeRaw, closedItem.GetRawText());
    }

    [Fact]
    public async Task ListMyClosedPetAds_AdItem_HasCallerAsCreator()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var ownerId = await client.GetMyIdAsync();
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1, status: PetAdStatus.Closed));

        // Act
        var response = await client.GetMyClosedPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        var creator = json.RootElement.GetProperty("items").EnumerateArray().Single().GetProperty("creator");
        Assert.Equal(ownerId, creator.GetProperty("id").GetGuid());
        Assert.Equal("Anna K", creator.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task ListMyClosedPetAds_AdItem_HasNoStatusField()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var ownerId = await client.GetMyIdAsync();
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1, status: PetAdStatus.Closed));

        // Act
        var response = await client.GetMyClosedPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        var item = json.RootElement.GetProperty("items").EnumerateArray().Single();
        Assert.False(item.TryGetProperty("status", out _));
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
        Assert.Equal([adB.Id], ItemIds(json.RootElement));
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

    // Pagination and validation

    [Fact]
    public async Task ListMyClosedPetAds_NoPageSize_UsesDefaultOf20()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        await factory.SeedAdsAsync(SeedClosedAds(await client.GetMyIdAsync(), 25));

        // Act
        var response = await client.GetMyClosedPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal(20, json.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(25, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(20, json.RootElement.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task ListMyClosedPetAds_SecondPage_ReturnsRemainingAds()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var ads = SeedClosedAds(await client.GetMyIdAsync(), 25);
        await factory.SeedAdsAsync(ads);

        // Act
        var response = await client.GetMyClosedPetAdsAsync("page=2&pageSize=20");

        // Assert
        using var json = await ReadJsonAsync(response);
        var expected = ads.OrderBy(ad => ad.CreatedAt).Take(5).Select(ad => ad.Id).Reverse().ToList();
        Assert.Equal(expected, ItemIds(json.RootElement));
        Assert.Equal(2, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(25, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ListMyClosedPetAds_PageBeyondLastPage_ReturnsEmptyItemsWithTotalCount()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        await factory.SeedAdsAsync(SeedClosedAds(await client.GetMyIdAsync(), 3));

        // Act
        var response = await client.GetMyClosedPetAdsAsync("page=5&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(0, json.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(3, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ListMyClosedPetAds_PageSizeExactly100_Returns200()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");

        // Act
        var response = await client.GetMyClosedPetAdsAsync("pageSize=100");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(100, json.RootElement.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task ListMyClosedPetAds_PageSizeAbove100_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");

        // Act
        var response = await client.GetMyClosedPetAdsAsync("pageSize=101");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "pageSize");
    }

    [Fact]
    public async Task ListMyClosedPetAds_PageBelow1_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");

        // Act
        var response = await client.GetMyClosedPetAdsAsync("page=0");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "page");
    }

    [Fact]
    public async Task ListMyClosedPetAds_PageSizeBelow1_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");

        // Act
        var response = await client.GetMyClosedPetAdsAsync("pageSize=0");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "pageSize");
    }

    [Fact]
    public async Task ListMyClosedPetAds_PageNotANumber_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");

        // Act
        var response = await client.GetMyClosedPetAdsAsync("page=abc");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "page");
    }

    // Privacy and logging

    [Fact]
    public async Task ListMyClosedPetAds_Response_HasNoEmailPhoneOrUserName()
    {
        // Arrange
        const string email = "owner@x.com";
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync(email, "Anna K");
        var ownerId = await client.GetMyIdAsync();
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1, status: PetAdStatus.Closed));

        // Act
        var response = await client.GetMyClosedPetAdsAsync();

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Anna K", body);
        Assert.DoesNotContain(email, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phoneNumber", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("userName", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListMyClosedPetAds_Success_LogsNoPersonalData()
    {
        // Arrange
        const string email = "private.person@example.com";
        const string displayName = "Secret Display Name";
        var provider = new CapturingLoggerProvider();
        using var factory = new PawsPostFactory();
        using var loggedFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(provider)));
        var client = await loggedFactory.CreateLoggedInClientAsync(email, displayName);
        var ownerId = await client.GetMyIdAsync();
        await loggedFactory.SeedAdsAsync(BuildPetAd(ownerId, Day1, status: PetAdStatus.Closed));

        // Act
        var response = await client.GetMyClosedPetAdsAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(provider.Messages);
        Assert.All(provider.Messages, message =>
        {
            Assert.DoesNotContain(email, message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(displayName, message, StringComparison.OrdinalIgnoreCase);
        });
    }

    // Closing through the close endpoint and visibility elsewhere

    [Fact]
    public async Task ListMyClosedPetAds_AdClosedThroughEndpoint_AppearsInClosedList()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (client, _, ad) = await factory.CreateLoggedInOwnerWithAdAsync("owner@x.com");
        await client.ClosePetAdAsync(ad.Id.ToString());

        // Act
        var response = await client.GetMyClosedPetAdsAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal([ad.Id], ItemIds(json.RootElement));
        Assert.Equal(1, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ListMyClosedPetAds_AdClosedThroughEndpoint_DisappearsFromMyAds()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (client, ownerId, ad) = await factory.CreateLoggedInOwnerWithAdAsync("owner@x.com");
        var stillActive = BuildPetAd(ownerId, Day1.AddDays(1), title: "Still active");
        await factory.SeedAdsAsync(stillActive);
        await client.ClosePetAdAsync(ad.Id.ToString());

        // Act
        var response = await client.GetMyPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal([stillActive.Id], ItemIds(json.RootElement));
    }

    [Theory]
    [InlineData("")]
    [InlineData("species=Dog")]
    [InlineData("type=Adoption")]
    [InlineData("species=Dog&type=Adoption")]
    public async Task ListMyClosedPetAds_ClosedAd_IsNotInPublicList(string query)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (client, _, ad) = await factory.CreateLoggedInOwnerWithAdAsync("owner@x.com");
        await client.ClosePetAdAsync(ad.Id.ToString());

        // Act
        var response = await factory.CreateApiClient().GetPetAdsAsync(query);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Empty(ItemIds(json.RootElement));
        Assert.Equal(0, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ListMyClosedPetAds_ClosedAd_DetailsReturn404ForOwner()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (client, _, ad) = await factory.CreateLoggedInOwnerWithAdAsync("owner@x.com");
        await client.ClosePetAdAsync(ad.Id.ToString());

        // Act
        var response = await client.GetPetAdAsync(ad.Id.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListMyClosedPetAds_ClosedAd_DetailsReturn404ForOtherUser()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (client, _, ad) = await factory.CreateLoggedInOwnerWithAdAsync("owner@x.com");
        await client.ClosePetAdAsync(ad.Id.ToString());
        var other = await factory.CreateLoggedInClientAsync("other@x.com", "Boris");

        // Act
        var response = await other.GetPetAdAsync(ad.Id.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListMyClosedPetAds_ClosedAd_IsNotInCreatorProfile()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (client, ownerId, ad) = await factory.CreateLoggedInOwnerWithAdAsync("owner@x.com");
        await client.ClosePetAdAsync(ad.Id.ToString());

        // Act
        var anonymous = await factory.CreateApiClient().GetCreatorAsync(ownerId.ToString());
        var asOwner = await client.GetCreatorAsync(ownerId.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asOwner.StatusCode);
        using var anonymousJson = await ReadJsonAsync(anonymous);
        using var ownerJson = await ReadJsonAsync(asOwner);
        Assert.Empty(ItemIds(anonymousJson.RootElement.GetProperty("ads")));
        Assert.Empty(ItemIds(ownerJson.RootElement.GetProperty("ads")));
    }

    [Fact]
    public async Task ListMyClosedPetAds_ClosedAd_IsNotReturnedByAssistantSearch()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var ad = BuildSearchAd(await client.GetMyIdAsync(), "Calm dog", description: "Calm and quiet.");
        await factory.SeedAndIndexAdsAsync(ad);
        Assert.Contains(ad.Id, await SearchIdsAsync(client));
        await client.ClosePetAdAsync(ad.Id.ToString());

        // Act
        var ids = await SearchIdsAsync(client);

        // Assert
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

    private static PetAd[] SeedClosedAds(Guid ownerId, int count)
    {
        return Enumerable.Range(0, count)
            .Select(index => BuildPetAd(ownerId, Day1.AddMinutes(index), status: PetAdStatus.Closed))
            .ToArray();
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
