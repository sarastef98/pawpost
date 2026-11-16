using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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

    // Pagination and validation

    [Fact]
    public async Task ListMyPetAds_NoPageSize_UsesDefaultOf20()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var ownerId = await client.GetMyIdAsync();
        await factory.SeedAdsAsync(SeedOwnAds(ownerId, 25));

        // Act
        var response = await client.GetMyPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal(20, json.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(25, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(20, json.RootElement.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task ListMyPetAds_SecondPage_ReturnsRemainingAds()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var ownerId = await client.GetMyIdAsync();
        var ads = SeedOwnAds(ownerId, 25);
        await factory.SeedAdsAsync(ads);

        // Act
        var response = await client.GetMyPetAdsAsync("page=2&pageSize=20");

        // Assert
        using var json = await ReadJsonAsync(response);
        var expected = ads.OrderBy(ad => ad.CreatedAt).Take(5).Select(ad => ad.Id).Reverse().ToList();
        Assert.Equal(expected, ItemIds(json));
        Assert.Equal(2, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(25, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ListMyPetAds_PageBeyondLastPage_ReturnsEmptyItemsWithTotalCount()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var ownerId = await client.GetMyIdAsync();
        await factory.SeedAdsAsync(SeedOwnAds(ownerId, 3));

        // Act
        var response = await client.GetMyPetAdsAsync("page=5&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(0, json.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(3, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ListMyPetAds_PageSizeExactly100_Returns200()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");

        // Act
        var response = await client.GetMyPetAdsAsync("pageSize=100");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(100, json.RootElement.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task ListMyPetAds_PageSizeAbove100_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");

        // Act
        var response = await client.GetMyPetAdsAsync("pageSize=101");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "pageSize");
    }

    [Fact]
    public async Task ListMyPetAds_PageBelow1_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");

        // Act
        var response = await client.GetMyPetAdsAsync("page=0");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "page");
    }

    [Fact]
    public async Task ListMyPetAds_PageSizeBelow1_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");

        // Act
        var response = await client.GetMyPetAdsAsync("pageSize=0");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "pageSize");
    }

    [Fact]
    public async Task ListMyPetAds_PageNotANumber_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");

        // Act
        var response = await client.GetMyPetAdsAsync("page=abc");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "page");
    }

    // Privacy

    [Fact]
    public async Task ListMyPetAds_Response_HasNoEmailPhoneOrUserName()
    {
        // Arrange
        const string email = "owner@x.com";
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync(email, "Anna K");
        var ownerId = await client.GetMyIdAsync();
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1));

        // Act
        var response = await client.GetMyPetAdsAsync();

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Anna K", body);
        Assert.DoesNotContain(email, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phoneNumber", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("userName", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListMyPetAds_Success_LogsNoEmailOrDisplayName()
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
        await loggedFactory.SeedAdsAsync(BuildPetAd(ownerId, Day1));

        // Act
        var response = await client.GetMyPetAdsAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(provider.Messages);
        Assert.All(provider.Messages, message =>
        {
            Assert.DoesNotContain(email, message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(displayName, message, StringComparison.OrdinalIgnoreCase);
        });
    }

    // Routing

    [Fact]
    public async Task ListMyPetAds_MineRoute_IsNotHandledAsAdId()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");

        // Act
        var response = await client.GetAsync($"{PetAdsUrl}/mine");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("items").ValueKind);
        Assert.Equal(0, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    private static PetAd[] SeedOwnAds(Guid ownerId, int count)
    {
        return Enumerable.Range(0, count)
            .Select(index => BuildPetAd(ownerId, Day1.AddMinutes(index)))
            .ToArray();
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
