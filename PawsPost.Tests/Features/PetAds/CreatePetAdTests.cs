using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PawsPost.Data;
using PawsPost.Features.PetAds;
using PawsPost.Tests.Infrastructure;
using static PawsPost.Tests.Infrastructure.PetAdTestHelpers;

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
    public async Task CreatePetAd_Success_ReturnsLocationHeaderOfTheDetailsEndpoint()
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
        Assert.NotEqual(Guid.Empty, ad.OwnerId);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.Equal($"/api/pet-ads/{ad.Id}", location.AbsolutePath);
        var detailsResponse = await client.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        using var json = JsonDocument.Parse(await detailsResponse.Content.ReadAsStringAsync());
        Assert.Equal(ad.Id, json.RootElement.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task CreatePetAd_ValidRequest_ReturnsAndStoresTitle()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");

        // Act
        var response = await client.PostPetAdAsync(PetAdTestHelpers.ValidAdoptionBody(("title", "Sweet cat needs a home")));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var ad = await response.Content.ReadFromJsonAsync<PetAdResponse>();
        Assert.Equal("Sweet cat needs a home", ad?.Title);
        var stored = Assert.Single(await factory.GetStoredAdsAsync());
        Assert.Equal("Sweet cat needs a home", stored.Title);
    }

    [Fact]
    public async Task CreatePetAd_TitleWithSurroundingSpaces_IsTrimmed()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");

        // Act
        var response = await client.PostPetAdAsync(PetAdTestHelpers.ValidAdoptionBody(("title", "  Nice dog  ")));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var ad = await response.Content.ReadFromJsonAsync<PetAdResponse>();
        Assert.Equal("Nice dog", ad?.Title);
        var stored = Assert.Single(await factory.GetStoredAdsAsync());
        Assert.Equal("Nice dog", stored.Title);
    }

    [Fact]
    public async Task CreatePetAd_TitleAtMaxLengthPaddedWithSpaces_Returns201()
    {
        // Arrange (length is checked on the trimmed text)
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");
        var body = PetAdTestHelpers.ValidAdoptionBody(("title", "  " + new string('a', 100) + "  "));

        // Act
        var response = await client.PostPetAdAsync(body);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var stored = Assert.Single(await factory.GetStoredAdsAsync());
        Assert.Equal(new string('a', 100), stored.Title);
    }

    [Fact]
    public async Task CreatePetAd_Success_ResponseJsonContainsTitle()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");

        // Act
        var response = await client.PostPetAdAsync(PetAdTestHelpers.ValidAdoptionBody());

        // Assert
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var title = json.GetProperty("title");
        Assert.Equal(JsonValueKind.String, title.ValueKind);
        Assert.Equal("Friendly beagle looking for a home", title.GetString());
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
    [InlineData("title", 100)]
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

    // ---- Validation ----

    [Theory]
    [InlineData("title")]
    [InlineData("species")]
    [InlineData("ageInMonths")]
    [InlineData("description")]
    [InlineData("type")]
    public async Task CreatePetAd_RequiredFieldMissing_Returns400WithOnlyThatField(string field)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");
        var body = PetAdTestHelpers.ValidAdoptionBody().Without(field);

        // Act
        var response = await client.PostPetAdAsync(body);

        // Assert
        await AssertBadRequestForOnlyAsync(response, field);
        Assert.Empty(await factory.GetStoredAdsAsync());
    }

    [Theory]
    [InlineData("title")]
    [InlineData("species")]
    [InlineData("description")]
    public async Task CreatePetAd_WhitespaceOnlyText_Returns400WithOnlyThatField(string field)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");
        var body = PetAdTestHelpers.ValidAdoptionBody((field, "   "));

        // Act
        var response = await client.PostPetAdAsync(body);

        // Assert
        await AssertBadRequestForOnlyAsync(response, field);
        Assert.Empty(await factory.GetStoredAdsAsync());
    }

    [Fact]
    public async Task CreatePetAd_TitleNull_Returns400WithOnlyThatField()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");

        // Act
        var response = await client.PostPetAdAsync(PetAdTestHelpers.ValidAdoptionBody(("title", null)));

        // Assert
        await AssertBadRequestForOnlyAsync(response, "title");
        Assert.Empty(await factory.GetStoredAdsAsync());
    }

    [Fact]
    public async Task CreatePetAd_TitleEmpty_Returns400WithOnlyThatField()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");

        // Act
        var response = await client.PostPetAdAsync(PetAdTestHelpers.ValidAdoptionBody(("title", "")));

        // Assert
        await AssertBadRequestForOnlyAsync(response, "title");
        Assert.Empty(await factory.GetStoredAdsAsync());
    }

    [Fact]
    public async Task CreatePetAd_AgeNull_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");

        // Act
        var response = await client.PostPetAdAsync(PetAdTestHelpers.ValidAdoptionBody(("ageInMonths", null)));

        // Assert
        await AssertBadRequestForOnlyAsync(response, "ageInMonths");
        Assert.Empty(await factory.GetStoredAdsAsync());
    }

    [Fact]
    public async Task CreatePetAd_PriceMissingOnSale_ReturnsBadRequest()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");
        var body = PetAdTestHelpers.ValidSaleBody().Without("price");

        // Act
        var response = await client.PostPetAdAsync(body);

        // Assert
        await AssertBadRequestForOnlyAsync(response, "price");
        Assert.Empty(await factory.GetStoredAdsAsync());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task CreatePetAd_PriceZeroOrNegativeOnSale_ReturnsBadRequest(string price)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");
        var value = decimal.Parse(price, CultureInfo.InvariantCulture);

        // Act
        var response = await client.PostPetAdAsync(PetAdTestHelpers.ValidSaleBody(("price", value)));

        // Assert
        await AssertBadRequestForOnlyAsync(response, "price");
        Assert.Empty(await factory.GetStoredAdsAsync());
    }

    [Fact]
    public async Task CreatePetAd_PriceOnAdoption_ReturnsBadRequest()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");

        // Act
        var response = await client.PostPetAdAsync(PetAdTestHelpers.ValidAdoptionBody(("price", 10m)));

        // Assert
        await AssertBadRequestForOnlyAsync(response, "price");
        Assert.Empty(await factory.GetStoredAdsAsync());
    }

    [Fact]
    public async Task CreatePetAd_PriceMoreThanTwoDecimals_ReturnsBadRequest()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");

        // Act
        var response = await client.PostPetAdAsync(PetAdTestHelpers.ValidSaleBody(("price", 10.123m)));

        // Assert
        await AssertBadRequestForOnlyAsync(response, "price");
        Assert.Empty(await factory.GetStoredAdsAsync());
    }

    [Fact]
    public async Task CreatePetAd_PriceAboveMaximum_ReturnsBadRequest()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");

        // Act
        var response = await client.PostPetAdAsync(PetAdTestHelpers.ValidSaleBody(("price", 1_000_000.01m)));

        // Assert
        await AssertBadRequestForOnlyAsync(response, "price");
        Assert.Empty(await factory.GetStoredAdsAsync());
    }

    [Fact]
    public async Task CreatePetAd_UnknownTypeName_ReturnsBadRequest()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");

        // Act
        var response = await client.PostPetAdAsync(PetAdTestHelpers.ValidAdoptionBody(("type", "Barter")));

        // Assert
        await AssertBadRequestForOnlyAsync(response, "type");
        Assert.Empty(await factory.GetStoredAdsAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task CreatePetAd_NumericType_ReturnsBadRequest(int type)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");

        // Act
        var response = await client.PostPetAdAsync(PetAdTestHelpers.ValidAdoptionBody(("type", type)));

        // Assert
        await AssertBadRequestForOnlyAsync(response, "type");
        Assert.Empty(await factory.GetStoredAdsAsync());
    }

    [Theory]
    [InlineData("title", 100)]
    [InlineData("species", 50)]
    [InlineData("breed", 100)]
    [InlineData("description", 2000)]
    [InlineData("location", 100)]
    public async Task CreatePetAd_TextOneOverMaxLength_Returns400WithOnlyThatField(string field, int maxLength)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");
        var body = PetAdTestHelpers.ValidAdoptionBody((field, new string('a', maxLength + 1)));

        // Act
        var response = await client.PostPetAdAsync(body);

        // Assert
        await AssertBadRequestForOnlyAsync(response, field);
        Assert.Empty(await factory.GetStoredAdsAsync());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(361)]
    public async Task CreatePetAd_AgeOutOfRange_Returns400WithOnlyThatField(int age)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");

        // Act
        var response = await client.PostPetAdAsync(PetAdTestHelpers.ValidAdoptionBody(("ageInMonths", age)));

        // Assert
        await AssertBadRequestForOnlyAsync(response, "ageInMonths");
        Assert.Empty(await factory.GetStoredAdsAsync());
    }

    [Fact]
    public async Task CreatePetAd_MalformedJson_ReturnsBadRequest()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");
        using var content = new StringContent("{ \"type\": \"Adoption\", ", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(PetAdTestHelpers.PetAdsUrl, content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(await factory.GetStoredAdsAsync());
    }

    [Fact]
    public async Task CreatePetAd_LiteralNullBody_Returns400ProblemDetailsAndStoresNothing()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");
        using var content = new StringContent("null", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(PetAdTestHelpers.PetAdsUrl, content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(await factory.GetStoredAdsAsync());
    }

    [Fact]
    public async Task CreatePetAd_EmptyBody_Returns400ProblemDetailsAndStoresNothing()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");
        using var content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(PetAdTestHelpers.PetAdsUrl, content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(await factory.GetStoredAdsAsync());
    }

    [Fact]
    public async Task CreatePetAd_SeveralInvalidFields_ReportsEachFieldOnce()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");
        var body = PetAdTestHelpers.ValidSaleBody(
            ("species", new string('a', 51)), ("breed", new string('b', 101)), ("price", 0m));

        // Act
        var response = await client.PostPetAdAsync(body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(3, problem.Errors.Count);
        Assert.All(problem.Errors, e => Assert.Single(e.Value));
    }

    // ---- Authorization ----

    [Fact]
    public async Task CreatePetAd_NotLoggedIn_Returns401ProblemDetailsWithoutRedirect()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.PostPetAdAsync(PetAdTestHelpers.ValidAdoptionBody());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(await factory.GetStoredAdsAsync());
    }

    [Fact]
    public async Task CreatePetAd_InvalidBodyAndNotLoggedIn_Returns401()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.PostPetAdAsync(new { });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreatePetAd_TamperedCookie_Returns401()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, PetAdTestHelpers.PetAdsUrl)
        {
            Content = JsonContent.Create(PetAdTestHelpers.ValidAdoptionBody())
        };
        request.Headers.Add("Cookie", "pawspost.auth=tampered-value-that-is-not-valid");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await factory.GetStoredAdsAsync());
    }

}
