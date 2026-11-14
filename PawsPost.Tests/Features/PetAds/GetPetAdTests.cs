using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PawsPost.Data;
using PawsPost.Tests.Infrastructure;
using static PawsPost.Tests.Infrastructure.PetAdTestHelpers;

namespace PawsPost.Tests.Features.PetAds;

public class GetPetAdTests
{
    private static readonly DateTime Day1 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetPetAd_ActiveSaleAd_Returns200WithAllFields()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var ad = BuildPetAd(ownerId, Day1, title: "Kitten for sale", type: PetAdType.Sale,
            species: "Cat", price: 99.5m, location: "Bitola");
        await factory.SeedAdsAsync(ad);
        var client = await factory.CreateLoggedInClientAsync("viewer@x.com");

        // Act
        var response = await client.GetPetAdAsync(ad.Id.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var root = json.RootElement;
        Assert.Equal(ad.Id, root.GetProperty("id").GetGuid());
        Assert.Equal("Kitten for sale", root.GetProperty("title").GetString());
        Assert.Equal("Sale", root.GetProperty("type").GetString());
        Assert.Equal("Cat", root.GetProperty("species").GetString());
        Assert.Equal("Beagle", root.GetProperty("breed").GetString());
        Assert.Equal(12, root.GetProperty("ageInMonths").GetInt32());
        Assert.Equal("Friendly and healthy.", root.GetProperty("description").GetString());
        Assert.Equal(99.5m, root.GetProperty("price").GetDecimal());
        Assert.Equal("Bitola", root.GetProperty("location").GetString());
        Assert.Equal("Active", root.GetProperty("status").GetString());
        Assert.Equal(ad.CreatedAt, root.GetProperty("createdAt").GetDateTime().ToUniversalTime());
    }

    [Fact]
    public async Task GetPetAd_ActiveAdoptionAd_ReturnsNullPrice()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var ad = BuildPetAd(ownerId, Day1, type: PetAdType.Adoption, price: null);
        await factory.SeedAdsAsync(ad);
        var client = await factory.CreateLoggedInClientAsync("viewer@x.com");

        // Act
        var response = await client.GetPetAdAsync(ad.Id.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal("Adoption", json.RootElement.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("price").ValueKind);
    }

    [Fact]
    public async Task GetPetAd_AdWithoutBreedAndLocation_ReturnsNullForThem()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var ad = BuildPetAd(ownerId, Day1, location: null);
        ad.Breed = null;
        await factory.SeedAdsAsync(ad);
        var client = await factory.CreateLoggedInClientAsync("viewer@x.com");

        // Act
        var response = await client.GetPetAdAsync(ad.Id.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("breed").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("location").ValueKind);
    }

    [Fact]
    public async Task GetPetAd_Success_JsonUsesStringEnumsAndUtcCreatedAt()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var ad = BuildPetAd(ownerId, Day1);
        await factory.SeedAdsAsync(ad);
        var client = await factory.CreateLoggedInClientAsync("viewer@x.com");

        // Act
        var response = await client.GetPetAdAsync(ad.Id.ToString());

        // Assert
        using var json = await ReadJsonAsync(response);
        var root = json.RootElement;
        Assert.Equal(JsonValueKind.String, root.GetProperty("type").ValueKind);
        Assert.Equal(JsonValueKind.String, root.GetProperty("status").ValueKind);
        var createdAt = root.GetProperty("createdAt").GetString();
        Assert.EndsWith("Z", createdAt);
        Assert.Equal(DateTimeKind.Utc, root.GetProperty("createdAt").GetDateTime().Kind);
    }

    [Fact]
    public async Task GetPetAd_AdCreatedThroughPost_CanBeReadBackWithTheSameFields()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");
        var createResponse = await client.PostPetAdAsync(ValidSaleBody());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using var created = await ReadJsonAsync(createResponse);

        // Act
        var response = await client.GetPetAdAsync(created.RootElement.GetProperty("id").GetGuid().ToString());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var read = await ReadJsonAsync(response);
        foreach (var property in read.RootElement.EnumerateObject())
        {
            var expected = created.RootElement.GetProperty(property.Name);
            if (property.Value.ValueKind == JsonValueKind.Number)
            {
                Assert.Equal(expected.GetDecimal(), property.Value.GetDecimal());
            }
            else
            {
                Assert.Equal(expected.ToString(), property.Value.ToString());
            }
        }
        Assert.True(created.RootElement.TryGetProperty("ownerId", out _));
        Assert.False(read.RootElement.TryGetProperty("ownerId", out _));
    }

    [Fact]
    public async Task GetPetAd_UnknownId_Returns404ProblemDetails()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("viewer@x.com");

        // Act
        var response = await client.GetPetAdAsync(Guid.NewGuid().ToString());

        // Assert
        await AssertNotFoundProblemAsync(response);
    }

    [Fact]
    public async Task GetPetAd_EmptyGuid_Returns404()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("viewer@x.com");

        // Act
        var response = await client.GetPetAdAsync(Guid.Empty.ToString());

        // Assert
        await AssertNotFoundProblemAsync(response);
    }

    [Theory]
    [InlineData(PetAdStatus.Draft)]
    [InlineData(PetAdStatus.Reserved)]
    [InlineData(PetAdStatus.Closed)]
    public async Task GetPetAd_DraftReservedOrClosedAd_Returns404(PetAdStatus status)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var ad = BuildPetAd(ownerId, Day1, status: status);
        await factory.SeedAdsAsync(ad);
        var client = await factory.CreateLoggedInClientAsync("viewer@x.com");

        // Act
        var response = await client.GetPetAdAsync(ad.Id.ToString());

        // Assert
        await AssertNotFoundProblemAsync(response);
    }

    [Fact]
    public async Task GetPetAd_OtherAdsExist_ReturnsOnlyTheRequestedAd()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var wanted = BuildPetAd(ownerId, Day1, title: "Wanted ad");
        var other = BuildPetAd(ownerId, Day1.AddDays(1), title: "Other ad");
        await factory.SeedAdsAsync(other, wanted);
        var client = await factory.CreateLoggedInClientAsync("viewer@x.com");

        // Act
        var response = await client.GetPetAdAsync(wanted.Id.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(wanted.Id, json.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("Wanted ad", json.RootElement.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("not-a-guid")]
    public async Task GetPetAd_IdNotAGuid_Returns400WithOnlyThatField(string id)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("viewer@x.com");

        // Act
        var response = await client.GetPetAdAsync(id);

        // Assert
        await AssertBadRequestForOnlyAsync(response, "id");
    }

    [Fact]
    public async Task GetPetAd_NotLoggedIn_Returns401ProblemDetails()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var ad = BuildPetAd(ownerId, Day1);
        await factory.SeedAdsAsync(ad);
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdAsync(ad.Id.ToString());

        // Assert
        await AssertUnauthorizedProblemAsync(response);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task GetPetAd_NotLoggedInAndUnknownId_Returns401()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdAsync(Guid.NewGuid().ToString());

        // Assert
        await AssertUnauthorizedProblemAsync(response);
    }

    [Fact]
    public async Task GetPetAd_NotLoggedInAndIdNotAGuid_Returns401()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdAsync("not-a-guid");

        // Assert
        await AssertUnauthorizedProblemAsync(response);
    }

    [Fact]
    public async Task GetPetAd_TamperedCookie_Returns401()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var ad = BuildPetAd(ownerId, Day1);
        await factory.SeedAdsAsync(ad);
        var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{PetAdsUrl}/{ad.Id}");
        request.Headers.Add("Cookie", "pawspost.auth=tampered-value-that-is-not-valid");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        await AssertUnauthorizedProblemAsync(response);
    }

    [Fact]
    public async Task GetPetAd_LoggedInUserWhoIsNotTheOwner_Returns200()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var ad = BuildPetAd(ownerId, Day1);
        await factory.SeedAdsAsync(ad);
        var client = await factory.CreateLoggedInClientAsync("other@x.com");
        Assert.NotEqual(ownerId, await client.GetMyIdAsync());

        // Act
        var response = await client.GetPetAdAsync(ad.Id.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(ad.Id, json.RootElement.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task GetPetAd_OwnerReadsOwnActiveAd_Returns200()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com");
        var ownerId = await client.GetMyIdAsync();
        var ad = BuildPetAd(ownerId, Day1);
        await factory.SeedAdsAsync(ad);

        // Act
        var response = await client.GetPetAdAsync(ad.Id.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(ad.Id, json.RootElement.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task GetPetAd_OwnerReadsOwnClosedAd_Returns404()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com");
        var ownerId = await client.GetMyIdAsync();
        var ad = BuildPetAd(ownerId, Day1, status: PetAdStatus.Closed);
        await factory.SeedAdsAsync(ad);

        // Act
        var response = await client.GetPetAdAsync(ad.Id.ToString());

        // Assert
        await AssertNotFoundProblemAsync(response);
    }

    [Fact]
    public async Task GetPetAd_AnonymousClientAfterPublicList_StillGets401()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var ad = BuildPetAd(ownerId, Day1);
        await factory.SeedAdsAsync(ad);
        var client = factory.CreateApiClient();

        // Act
        var listResponse = await client.GetPetAdsAsync();
        var response = await client.GetPetAdAsync(ad.Id.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        await AssertUnauthorizedProblemAsync(response);
    }

    [Fact]
    public async Task GetPetAd_Success_ResponseHasNoOwnerEmailPhoneOrOwnerId()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var ad = BuildPetAd(ownerId, Day1);
        await factory.SeedAdsAsync(ad);
        var client = await factory.CreateLoggedInClientAsync("viewer@x.com");

        // Act
        var response = await client.GetPetAdAsync(ad.Id.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("owner@x.com", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ownerId", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ownerId.ToString(), text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetPetAd_Success_ContainsOnlyTheDetailFields()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var ad = BuildPetAd(ownerId, Day1);
        await factory.SeedAdsAsync(ad);
        var client = await factory.CreateLoggedInClientAsync("viewer@x.com");

        // Act
        var response = await client.GetPetAdAsync(ad.Id.ToString());

        // Assert
        using var json = await ReadJsonAsync(response);
        var names = json.RootElement.EnumerateObject().Select(property => property.Name).Order().ToList();
        Assert.Equal(
            ["ageInMonths", "breed", "createdAt", "description", "id", "location", "price", "species", "status", "title", "type"],
            names);
    }

    private static async Task AssertUnauthorizedProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(401, problem.Status);
    }

    private static async Task AssertNotFoundProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
