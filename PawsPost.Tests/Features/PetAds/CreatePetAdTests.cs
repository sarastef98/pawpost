using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PawsPost.Data;
using PawsPost.Features.PetAds;
using PawsPost.Tests.Infrastructure;

namespace PawsPost.Tests.Features.PetAds;

public class CreatePetAdTests
{
    [Fact]
    public async Task CreatePetAd_ValidAdoption_Returns201AndActiveAd()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");

        // Act
        var response = await client.PostPetAdAsync(PetAdTestHelpers.ValidAdoptionBody());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var ad = await response.Content.ReadFromJsonAsync<PetAdResponse>();
        Assert.NotNull(ad);
        Assert.NotEqual(Guid.Empty, ad.Id);
        Assert.Equal(PetAdType.Adoption, ad.Type);
        Assert.Equal(PetAdStatus.Active, ad.Status);
        Assert.Null(ad.Price);
        Assert.Equal("Dog", ad.Species);
        Assert.Equal("Beagle", ad.Breed);
        Assert.Equal(24, ad.AgeInMonths);
        Assert.Equal("Friendly and healthy.", ad.Description);
        Assert.Equal("Skopje", ad.Location);
        var stored = Assert.Single(await factory.GetStoredAdsAsync());
        Assert.Equal(ad.Id, stored.Id);
        Assert.Equal(PetAdStatus.Active, stored.Status);
        Assert.Null(stored.Price);
    }

    [Fact]
    public async Task CreatePetAd_ValidSale_Returns201WithPrice()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");

        // Act
        var response = await client.PostPetAdAsync(PetAdTestHelpers.ValidSaleBody());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var ad = await response.Content.ReadFromJsonAsync<PetAdResponse>();
        Assert.Equal(PetAdType.Sale, ad?.Type);
        Assert.Equal(150.50m, ad?.Price);
        var stored = Assert.Single(await factory.GetStoredAdsAsync());
        Assert.Equal(150.50m, stored.Price);
        Assert.Equal(PetAdType.Sale, stored.Type);
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("1000000")]
    public async Task CreatePetAd_SalePriceAtBoundary_Returns201(string price)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");
        var value = decimal.Parse(price, CultureInfo.InvariantCulture);

        // Act
        var response = await client.PostPetAdAsync(PetAdTestHelpers.ValidSaleBody(("price", value)));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var stored = Assert.Single(await factory.GetStoredAdsAsync());
        Assert.Equal(value, stored.Price);
    }

    [Fact]
    public async Task CreatePetAd_ValidRequest_StoresLoggedInUserAsOwner()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var other = await factory.CreateLoggedInClientAsync("other@x.com");
        var client = await factory.CreateLoggedInClientAsync("a@x.com");
        var myId = await client.GetMyIdAsync();
        var otherId = await other.GetMyIdAsync();

        // Act
        var response = await client.PostPetAdAsync(PetAdTestHelpers.ValidAdoptionBody());

        // Assert
        var ad = await response.Content.ReadFromJsonAsync<PetAdResponse>();
        Assert.Equal(myId, ad?.OwnerId);
        var stored = Assert.Single(await factory.GetStoredAdsAsync());
        Assert.Equal(myId, stored.OwnerId);
        Assert.NotEqual(otherId, stored.OwnerId);
    }

    [Fact]
    public async Task CreatePetAd_OwnerIdAndStatusInBody_AreIgnored()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var other = await factory.CreateLoggedInClientAsync("other@x.com");
        var client = await factory.CreateLoggedInClientAsync("a@x.com");
        var myId = await client.GetMyIdAsync();
        var otherId = await other.GetMyIdAsync();
        var body = PetAdTestHelpers.ValidAdoptionBody(("ownerId", otherId), ("status", "Closed"));

        // Act
        var response = await client.PostPetAdAsync(body);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var ad = await response.Content.ReadFromJsonAsync<PetAdResponse>();
        Assert.Equal(myId, ad?.OwnerId);
        Assert.Equal(PetAdStatus.Active, ad?.Status);
        var stored = Assert.Single(await factory.GetStoredAdsAsync());
        Assert.Equal(myId, stored.OwnerId);
        Assert.Equal(PetAdStatus.Active, stored.Status);
    }

    [Fact]
    public async Task CreatePetAd_OptionalBreedAndLocationOmitted_Returns201()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");
        var body = PetAdTestHelpers.ValidAdoptionBody().Without("breed").Without("location");

        // Act
        var response = await client.PostPetAdAsync(body);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var ad = await response.Content.ReadFromJsonAsync<PetAdResponse>();
        Assert.Null(ad?.Breed);
        Assert.Null(ad?.Location);
        var stored = Assert.Single(await factory.GetStoredAdsAsync());
        Assert.Null(stored.Breed);
        Assert.Null(stored.Location);
    }

    [Fact]
    public async Task CreatePetAd_AgeZero_Returns201()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");

        // Act
        var response = await client.PostPetAdAsync(PetAdTestHelpers.ValidAdoptionBody(("ageInMonths", 0)));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var stored = Assert.Single(await factory.GetStoredAdsAsync());
        Assert.Equal(0, stored.AgeInMonths);
    }

    [Fact]
    public async Task CreatePetAd_AgeAtMaximum_Returns201()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");

        // Act
        var response = await client.PostPetAdAsync(PetAdTestHelpers.ValidAdoptionBody(("ageInMonths", 360)));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var stored = Assert.Single(await factory.GetStoredAdsAsync());
        Assert.Equal(360, stored.AgeInMonths);
    }

    [Fact]
    public async Task CreatePetAd_TextWithSurroundingSpaces_IsTrimmed()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");
        var body = PetAdTestHelpers.ValidAdoptionBody(
            ("species", "  Dog  "), ("breed", "  Beagle "), ("description", "  Nice  "), ("location", " Skopje  "));

        // Act
        var response = await client.PostPetAdAsync(body);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var ad = await response.Content.ReadFromJsonAsync<PetAdResponse>();
        Assert.Equal("Dog", ad?.Species);
        Assert.Equal("Beagle", ad?.Breed);
        Assert.Equal("Nice", ad?.Description);
        Assert.Equal("Skopje", ad?.Location);
        var stored = Assert.Single(await factory.GetStoredAdsAsync());
        Assert.Equal("Dog", stored.Species);
        Assert.Equal("Beagle", stored.Breed);
        Assert.Equal("Nice", stored.Description);
        Assert.Equal("Skopje", stored.Location);
    }

    [Fact]
    public async Task CreatePetAd_WhitespaceOnlyBreedAndLocation_AreStoredAsNull()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");
        var body = PetAdTestHelpers.ValidAdoptionBody(("breed", "   "), ("location", ""));

        // Act
        var response = await client.PostPetAdAsync(body);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var stored = Assert.Single(await factory.GetStoredAdsAsync());
        Assert.Null(stored.Breed);
        Assert.Null(stored.Location);
    }

    [Theory]
    [InlineData("species", 50)]
    [InlineData("breed", 100)]
    [InlineData("description", 2000)]
    [InlineData("location", 100)]
    public async Task CreatePetAd_TextAtMaxLength_Returns201(string field, int maxLength)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");
        var body = PetAdTestHelpers.ValidAdoptionBody((field, new string('a', maxLength)));

        // Act
        var response = await client.PostPetAdAsync(body);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Single(await factory.GetStoredAdsAsync());
    }

    [Theory]
    [InlineData("species", 50)]
    [InlineData("description", 2000)]
    public async Task CreatePetAd_MaxLengthTextPaddedWithSpaces_Returns201(string field, int maxLength)
    {
        // Arrange (length is checked on the trimmed text)
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");
        var body = PetAdTestHelpers.ValidAdoptionBody((field, "  " + new string('a', maxLength) + "  "));

        // Act
        var response = await client.PostPetAdAsync(body);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var stored = Assert.Single(await factory.GetStoredAdsAsync());
        Assert.Equal(maxLength, (field == "species" ? stored.Species : stored.Description).Length);
    }

    [Fact]
    public async Task CreatePetAd_Success_ResponseHasNoEmailOrPhone()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com");

        // Act
        var response = await client.PostPetAdAsync(PetAdTestHelpers.ValidAdoptionBody());

        // Assert
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("owner@x.com", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreatePetAd_Success_ResponseUsesStringEnumsAndUtcCreatedAt()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");

        // Act
        var response = await client.PostPetAdAsync(PetAdTestHelpers.ValidSaleBody());

        // Assert
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Sale", json.GetProperty("type").GetString());
        Assert.Equal("Active", json.GetProperty("status").GetString());
        Assert.Equal(DateTimeKind.Utc, json.GetProperty("createdAt").GetDateTime().Kind);
    }

    // VALIDATION_AND_AUTH_TESTS_BELOW

    private static async Task AssertBadRequestForOnlyAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        var error = Assert.Single(problem.Errors);
        Assert.Equal(field, error.Key.TrimStart('$', '.'), ignoreCase: true);
        Assert.Single(error.Value);
    }
}
