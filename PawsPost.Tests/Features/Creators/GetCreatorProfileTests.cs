using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PawsPost.Data;
using PawsPost.Tests.Infrastructure;
using static PawsPost.Tests.Infrastructure.PetAdTestHelpers;

namespace PawsPost.Tests.Features.Creators;

public class GetCreatorProfileTests
{
    private static readonly DateTime Day1 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    // Success

    [Fact]
    public async Task GetCreator_NotLoggedIn_Returns200()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com", "Anna K");
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetCreatorAsync(ownerId.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(1, json.RootElement.GetProperty("ads").GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task GetCreator_KnownCreator_ReturnsIdAndDisplayName()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com", "Anna K");
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetCreatorAsync(ownerId.ToString());

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal(ownerId, json.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("Anna K", json.RootElement.GetProperty("displayName").GetString());
        Assert.Equal(["ads", "displayName", "id"], json.RootElement.EnumerateObject().Select(p => p.Name).Order().ToList());
    }

    [Fact]
    public async Task GetCreator_WithActiveAds_ReturnsOnlyThatCreatorsAds()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com", "Anna K");
        var otherId = await factory.SeedOwnerAsync("other@x.com", "Boris");
        var mine = BuildPetAd(ownerId, Day1, title: "Mine");
        await factory.SeedAdsAsync(mine, BuildPetAd(otherId, Day1, title: "Not mine"));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetCreatorAsync(ownerId.ToString());

        // Assert
        using var json = await ReadJsonAsync(response);
        var ads = json.RootElement.GetProperty("ads");
        var item = Assert.Single(ads.GetProperty("items").EnumerateArray());
        Assert.Equal(mine.Id, item.GetProperty("id").GetGuid());
        Assert.Equal(1, ads.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task GetCreator_WithAds_SortsNewestFirst()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com", "Anna K");
        var oldest = BuildPetAd(ownerId, Day1);
        var middle = BuildPetAd(ownerId, Day1.AddDays(1));
        var newest = BuildPetAd(ownerId, Day1.AddDays(2));
        await factory.SeedAdsAsync(middle, oldest, newest);
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetCreatorAsync(ownerId.ToString());

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal([newest.Id, middle.Id, oldest.Id], ItemIds(json));
    }

    [Fact]
    public async Task GetCreator_AdItem_HasSameFieldsAsListItem()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com", "Anna K");
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1, type: PetAdType.Sale, price: 10m));
        var client = factory.CreateApiClient();

        // Act
        var profileResponse = await client.GetCreatorAsync(ownerId.ToString());
        var listResponse = await client.GetPetAdsAsync();

        // Assert
        using var profile = await ReadJsonAsync(profileResponse);
        using var list = await ReadJsonAsync(listResponse);
        var profileItem = profile.RootElement.GetProperty("ads").GetProperty("items").EnumerateArray().Single();
        var listItem = list.RootElement.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(listItem.GetRawText(), profileItem.GetRawText());
        Assert.Equal(ownerId, profileItem.GetProperty("creator").GetProperty("id").GetGuid());
    }

    // Only Active ads

    [Theory]
    [InlineData(PetAdStatus.Draft)]
    [InlineData(PetAdStatus.Reserved)]
    [InlineData(PetAdStatus.Closed)]
    public async Task GetCreator_NonActiveAds_AreNotListed(PetAdStatus status)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com", "Anna K");
        var active = BuildPetAd(ownerId, Day1);
        await factory.SeedAdsAsync(active, BuildPetAd(ownerId, Day1, status: status));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetCreatorAsync(ownerId.ToString());

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal([active.Id], ItemIds(json));
        Assert.Equal(1, json.RootElement.GetProperty("ads").GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task GetCreator_OwnerLoggedIn_StillSeesOnlyActiveAds()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com", "Anna K");
        var ownerId = await client.GetMyIdAsync();
        var active = BuildPetAd(ownerId, Day1);
        await factory.SeedAdsAsync(
            active,
            BuildPetAd(ownerId, Day1, status: PetAdStatus.Draft),
            BuildPetAd(ownerId, Day1, status: PetAdStatus.Reserved),
            BuildPetAd(ownerId, Day1, status: PetAdStatus.Closed));

        // Act
        var response = await client.GetCreatorAsync(ownerId.ToString());

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal([active.Id], ItemIds(json));
    }

    [Fact]
    public async Task GetCreator_OtherLoggedInUser_SeesSameResultAsAnonymous()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com", "Anna K");
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1), BuildPetAd(ownerId, Day1, status: PetAdStatus.Closed));
        var anonymous = factory.CreateApiClient();
        var other = await factory.CreateLoggedInClientAsync("other@x.com");

        // Act
        var anonymousText = await (await anonymous.GetCreatorAsync(ownerId.ToString())).Content.ReadAsStringAsync();
        var otherText = await (await other.GetCreatorAsync(ownerId.ToString())).Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(anonymousText, otherText);
    }

    // Pagination

    [Fact]
    public async Task GetCreator_NoPageSize_UsesDefaultOf20()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com", "Anna K");
        await factory.SeedAdsAsync(
            Enumerable.Range(0, 25).Select(day => BuildPetAd(ownerId, Day1.AddDays(day))).ToArray());
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetCreatorAsync(ownerId.ToString());

        // Assert
        using var json = await ReadJsonAsync(response);
        var ads = json.RootElement.GetProperty("ads");
        Assert.Equal(20, ads.GetProperty("items").GetArrayLength());
        Assert.Equal(25, ads.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, ads.GetProperty("page").GetInt32());
        Assert.Equal(20, ads.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task GetCreator_SecondPage_ReturnsRemainingAds()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com", "Anna K");
        var ads = Enumerable.Range(0, 25).Select(day => BuildPetAd(ownerId, Day1.AddDays(day))).ToArray();
        await factory.SeedAdsAsync(ads);
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetCreatorAsync(ownerId.ToString(), "page=2");

        // Assert
        using var json = await ReadJsonAsync(response);
        var expected = ads.OrderBy(ad => ad.CreatedAt).Take(5).Select(ad => ad.Id).Reverse().ToList();
        Assert.Equal(expected, ItemIds(json));
        Assert.Equal(2, json.RootElement.GetProperty("ads").GetProperty("page").GetInt32());
    }

    [Fact]
    public async Task GetCreator_PageSizeAbove100_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com", "Anna K");
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetCreatorAsync(ownerId.ToString(), "pageSize=101");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "pageSize");
    }

    [Fact]
    public async Task GetCreator_PageBelow1_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com", "Anna K");
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetCreatorAsync(ownerId.ToString(), "page=0");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "page");
    }

    [Fact]
    public async Task GetCreator_PageSizeBelow1_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com", "Anna K");
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetCreatorAsync(ownerId.ToString(), "pageSize=0");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "pageSize");
    }

    [Fact]
    public async Task GetCreator_PageSizeExactly100_Returns200()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com", "Anna K");
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetCreatorAsync(ownerId.ToString(), "pageSize=100");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Not found and empty

    [Fact]
    public async Task GetCreator_UnknownId_Returns404ProblemDetails()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetCreatorAsync(Guid.NewGuid().ToString());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("123")]
    public async Task GetCreator_InvalidGuid_Returns400(string id)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetCreatorAsync(id);

        // Assert
        await AssertBadRequestForOnlyAsync(response, "id");
    }

    [Fact]
    public async Task GetCreator_NoActiveAds_ReturnsEmptyListWithTotalCountZero()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com", "Anna K");
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1, status: PetAdStatus.Closed));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetCreatorAsync(ownerId.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var ads = json.RootElement.GetProperty("ads");
        Assert.Equal(0, ads.GetProperty("items").GetArrayLength());
        Assert.Equal(0, ads.GetProperty("totalCount").GetInt32());
    }

    // Privacy

    [Fact]
    public async Task GetCreator_Response_HasNoEmailPhoneOrUserName()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("secret.owner@x.com", "Anna K");
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetCreatorAsync(ownerId.ToString());

        // Assert
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"items\"", text);
        Assert.DoesNotContain("secret.owner@x.com", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("userName", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("normalized", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ownerId", text, StringComparison.OrdinalIgnoreCase);
    }

    private static List<Guid> ItemIds(JsonDocument json)
    {
        return json.RootElement.GetProperty("ads").GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid()).ToList();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
