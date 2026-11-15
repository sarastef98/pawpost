using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PawsPost.Data;
using PawsPost.Tests.Infrastructure;
using static PawsPost.Tests.Infrastructure.PetAdTestHelpers;

namespace PawsPost.Tests.Features.PetAds;

public class ListPetAdsTests
{
    private static readonly DateTime Day1 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ListPetAds_ActiveAdsExist_Returns200WithItemsAndPagingValues()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var first = BuildPetAd(ownerId, Day1, title: "Old dog", species: PetSpecies.Dog);
        var second = BuildPetAd(ownerId, Day1.AddDays(1), title: "Kitten for sale", type: PetAdType.Sale,
            species: PetSpecies.Cat, price: 99.5m, location: "Bitola");
        await factory.SeedAdsAsync(first, second);
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var root = json.RootElement;
        Assert.Equal(2, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(20, root.GetProperty("pageSize").GetInt32());
        var items = root.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        var newest = items[0];
        Assert.Equal(second.Id, newest.GetProperty("id").GetGuid());
        Assert.Equal("Kitten for sale", newest.GetProperty("title").GetString());
        Assert.Equal("Sale", newest.GetProperty("type").GetString());
        Assert.Equal("Cat", newest.GetProperty("species").GetString());
        Assert.Equal(99.5m, newest.GetProperty("price").GetDecimal());
        Assert.Equal("Bitola", newest.GetProperty("location").GetString());
        Assert.Equal(second.CreatedAt, newest.GetProperty("createdAt").GetDateTime().ToUniversalTime());
    }

    [Fact]
    public async Task ListPetAds_NoAds_Returns200WithEmptyItemsAndZeroTotal()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Empty(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(0, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ListPetAds_DraftReservedAndClosedAds_AreNotListed()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var active = BuildPetAd(ownerId, Day1);
        await factory.SeedAdsAsync(
            active,
            BuildPetAd(ownerId, Day1, status: PetAdStatus.Draft),
            BuildPetAd(ownerId, Day1, status: PetAdStatus.Reserved),
            BuildPetAd(ownerId, Day1, status: PetAdStatus.Closed));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal(1, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal([active.Id], ItemIds(json));
    }

    [Fact]
    public async Task ListPetAds_AdsWithDifferentDates_ReturnsNewestFirst()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var oldest = BuildPetAd(ownerId, Day1);
        var middle = BuildPetAd(ownerId, Day1.AddDays(1));
        var newest = BuildPetAd(ownerId, Day1.AddDays(2));
        await factory.SeedAdsAsync(middle, oldest, newest);
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal([newest.Id, middle.Id, oldest.Id], ItemIds(json));
    }

    [Fact]
    public async Task ListPetAds_AdsWithSameCreatedAt_AreOrderedByIdDescending()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var low = BuildPetAd(ownerId, Day1, id: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var mid = BuildPetAd(ownerId, Day1, id: Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var high = BuildPetAd(ownerId, Day1, id: Guid.Parse("00000000-0000-0000-0000-000000000003"));
        await factory.SeedAdsAsync(mid, low, high);
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal([high.Id, mid.Id, low.Id], ItemIds(json));
    }

    [Fact]
    public async Task ListPetAds_SecondPage_ReturnsRemainingItemsAndFullTotalCount()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var ads = Enumerable.Range(0, 5).Select(day => BuildPetAd(ownerId, Day1.AddDays(day))).ToArray();
        await factory.SeedAdsAsync(ads);
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync("pageSize=2&page=2");

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal([ads[2].Id, ads[1].Id], ItemIds(json));
        Assert.Equal(5, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task ListPetAds_FirstAndSecondPage_DoNotOverlap()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var ads = Enumerable.Range(0, 5).Select(day => BuildPetAd(ownerId, Day1.AddDays(day))).ToArray();
        await factory.SeedAdsAsync(ads);
        var client = factory.CreateApiClient();

        // Act
        using var page1 = await ReadJsonAsync(await client.GetPetAdsAsync("pageSize=2&page=1"));
        using var page2 = await ReadJsonAsync(await client.GetPetAdsAsync("pageSize=2&page=2"));

        // Assert
        Assert.Equal([ads[4].Id, ads[3].Id], ItemIds(page1));
        Assert.Empty(ItemIds(page1).Intersect(ItemIds(page2)));
    }

    [Fact]
    public async Task ListPetAds_PageBeyondLastPage_Returns200WithEmptyItemsAndRealTotalCount()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1), BuildPetAd(ownerId, Day1.AddDays(1)), BuildPetAd(ownerId, Day1.AddDays(2)));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync("pageSize=2&page=3");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Empty(ItemIds(json));
        Assert.Equal(3, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, json.RootElement.GetProperty("page").GetInt32());
    }

    [Fact]
    public async Task ListPetAds_HugePageNumber_Returns200WithEmptyItems()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync("page=2147483647&pageSize=100");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Empty(ItemIds(json));
        Assert.Equal(1, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ListPetAds_PageSizeAtMaximum_Returns200()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync("pageSize=100");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(100, json.RootElement.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task ListPetAds_MoreAdsThanDefaultPageSize_ReturnsTwentyItems()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var ads = Enumerable.Range(0, 25).Select(day => BuildPetAd(ownerId, Day1.AddDays(day))).ToArray();
        await factory.SeedAdsAsync(ads);
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal(20, ItemIds(json).Count);
        Assert.Equal(25, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ListPetAds_SpeciesInDifferentCase_MatchesIgnoringCase()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var dog = BuildPetAd(ownerId, Day1, species: PetSpecies.Dog);
        await factory.SeedAdsAsync(dog, BuildPetAd(ownerId, Day1, species: PetSpecies.Cat));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync("species=dOG");

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal([dog.Id], ItemIds(json));
    }

    [Fact]
    public async Task ListPetAds_NonAsciiSpecies_Returns400WithOnlyThatField()
    {
        // Arrange (free text is no longer a valid filter, even for a name that was once stored as legacy text)
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1, species: PetSpecies.Other));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync($"species={Uri.EscapeDataString("куче")}");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "species");
    }

    [Fact]
    public async Task ListPetAds_SpeciesWithSurroundingSpaces_IsTrimmed()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var dog = BuildPetAd(ownerId, Day1, species: PetSpecies.Dog);
        await factory.SeedAdsAsync(dog);
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync("species=%20Dog%20");

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal([dog.Id], ItemIds(json));
    }

    [Fact]
    public async Task ListPetAds_SpeciesIsOnlyPartOfAName_Returns400WithOnlyThatField()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1, species: PetSpecies.Dog));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync("species=Do");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "species");
    }

    [Theory]
    [InlineData("species=")]
    [InlineData("species=%20%20")]
    public async Task ListPetAds_SpeciesEmptyOrWhitespace_ReturnsAllActiveAds(string query)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1, species: PetSpecies.Dog), BuildPetAd(ownerId, Day1.AddDays(1), species: PetSpecies.Cat));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync(query);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(2, ItemIds(json).Count);
        Assert.Equal(2, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListPetAds_LongUnknownSpecies_Returns400WithOnlyThatField(bool padWithSpaces)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var species = new string('a', 50);
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1, species: PetSpecies.Other));
        var client = factory.CreateApiClient();
        var query = padWithSpaces ? $"species=%20%20{species}%20%20" : $"species={species}";

        // Act
        var response = await client.GetPetAdsAsync(query);

        // Assert
        await AssertBadRequestForOnlyAsync(response, "species");
    }

    [Fact]
    public async Task ListPetAds_TypeAdoption_ReturnsOnlyAdoptionAds()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var adoption = BuildPetAd(ownerId, Day1);
        await factory.SeedAdsAsync(adoption, BuildPetAd(ownerId, Day1, type: PetAdType.Sale, price: 10m));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync("type=Adoption");

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal([adoption.Id], ItemIds(json));
    }

    [Fact]
    public async Task ListPetAds_TypeSale_ReturnsOnlySaleAds()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var sale = BuildPetAd(ownerId, Day1, type: PetAdType.Sale, price: 10m);
        await factory.SeedAdsAsync(sale, BuildPetAd(ownerId, Day1));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync("type=Sale");

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal([sale.Id], ItemIds(json));
    }

    [Theory]
    [InlineData("adoption", "Adoption")]
    [InlineData("SALE", "Sale")]
    public async Task ListPetAds_TypeInDifferentCase_IsAccepted(string query, string expectedType)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1), BuildPetAd(ownerId, Day1, type: PetAdType.Sale, price: 10m));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync($"type={query}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(expectedType, item.GetProperty("type").GetString());
    }

    [Fact]
    public async Task ListPetAds_SpeciesAndTypeTogether_ReturnsOnlyAdsMatchingBoth()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var match = BuildPetAd(ownerId, Day1, species: PetSpecies.Dog, type: PetAdType.Sale, price: 10m);
        await factory.SeedAdsAsync(
            match,
            BuildPetAd(ownerId, Day1, species: PetSpecies.Dog),
            BuildPetAd(ownerId, Day1, species: PetSpecies.Cat, type: PetAdType.Sale, price: 10m));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync("species=dog&type=sale");

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal([match.Id], ItemIds(json));
    }

    [Fact]
    public async Task ListPetAds_FilterMatchesNothing_Returns200WithEmptyItemsAndZeroTotal()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1, species: PetSpecies.Dog));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync("species=Cat");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Empty(ItemIds(json));
        Assert.Equal(0, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ListPetAds_FilterAndPaging_TotalCountCountsOnlyMatchingAds()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var dogs = Enumerable.Range(0, 3).Select(day => BuildPetAd(ownerId, Day1.AddDays(day), species: PetSpecies.Dog)).ToArray();
        await factory.SeedAdsAsync(dogs);
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1, species: PetSpecies.Cat), BuildPetAd(ownerId, Day1, species: PetSpecies.Cat));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync("species=Dog&pageSize=2&page=2");

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal([dogs[0].Id], ItemIds(json));
        Assert.Equal(3, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ListPetAds_SaleAd_ReturnsPrice()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1, type: PetAdType.Sale, price: 250.75m));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(250.75m, item.GetProperty("price").GetDecimal());
    }

    [Fact]
    public async Task ListPetAds_AdoptionAd_HasNullPrice()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.True(item.TryGetProperty("price", out var price));
        Assert.Equal(JsonValueKind.Null, price.ValueKind);
    }

    [Fact]
    public async Task ListPetAds_Success_JsonUsesStringEnumsAndUtcCreatedAt()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1, type: PetAdType.Sale, price: 10m));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.String, item.GetProperty("type").ValueKind);
        Assert.Equal("Sale", item.GetProperty("type").GetString());
        var createdAt = item.GetProperty("createdAt");
        Assert.EndsWith("Z", createdAt.GetString());
        Assert.Equal(DateTimeKind.Utc, createdAt.GetDateTime().Kind);
        Assert.Equal(Day1, createdAt.GetDateTime());
    }

    [Fact]
    public async Task ListPetAds_Success_ItemsContainOnlyTheCardFields()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
        var names = item.EnumerateObject().Select(property => property.Name).Order().ToList();
        Assert.Equal(["createdAt", "id", "location", "price", "species", "title", "type"], names);
    }

    [Fact]
    public async Task ListPetAds_Success_ResponseHasNoOwnerEmailPhoneOrOwnerId()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("secret.owner@x.com");
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync();

        // Assert
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"items\"", text);
        Assert.DoesNotContain("secret.owner@x.com", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ownerId", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ownerId.ToString(), text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task ListPetAds_PageBelowOne_Returns400WithOnlyThatField(string page)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync($"page={page}");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "page");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("99999999999")]
    public async Task ListPetAds_PageNotANumber_Returns400(string page)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync($"page={page}");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "page");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("101")]
    public async Task ListPetAds_PageSizeOutOfRange_Returns400WithOnlyThatField(string pageSize)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync($"pageSize={pageSize}");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "pageSize");
    }

    [Fact]
    public async Task ListPetAds_PageSizeNotANumber_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync("pageSize=abc");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "pageSize");
    }

    [Fact]
    public async Task ListPetAds_TypeUnknownName_Returns400WithOnlyThatField()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync("type=Barter");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "type");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("99")]
    public async Task ListPetAds_TypeNumeric_Returns400WithOnlyThatField(string type)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync($"type={type}");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "type");
    }

    [Fact]
    public async Task ListPetAds_SpeciesOneOverMaxLength_Returns400WithOnlyThatField()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync($"species={new string('a', 51)}");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "species");
    }

    [Fact]
    public async Task ListPetAds_SeveralInvalidParameters_ReportsEachFieldOnce()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync($"page=0&pageSize=101&species={new string('a', 51)}&type=Barter");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(["page", "pagesize", "species", "type"], problem.Errors.Keys.Select(key => key.ToLowerInvariant()).Order());
        Assert.All(problem.Errors.Values, messages => Assert.Single(messages));
    }

    [Fact]
    public async Task ListPetAds_TypeRepeated_UsesFirstValue()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var sale = BuildPetAd(ownerId, Day1, type: PetAdType.Sale, price: 10m);
        await factory.SeedAdsAsync(sale, BuildPetAd(ownerId, Day1.AddDays(1)));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync("type=Sale&type=Adoption");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal([sale.Id], ItemIds(json));
        Assert.Equal(1, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ListPetAds_PageRepeated_UsesFirstValue()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var older = BuildPetAd(ownerId, Day1);
        var newer = BuildPetAd(ownerId, Day1.AddDays(1));
        await factory.SeedAdsAsync(older, newer);
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync("pageSize=1&page=1&page=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal([newer.Id], ItemIds(json));
        Assert.Equal(1, json.RootElement.GetProperty("page").GetInt32());
    }

    [Fact]
    public async Task ListPetAds_TypeEmpty_ReturnsAllActiveAds()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        await factory.SeedAdsAsync(
            BuildPetAd(ownerId, Day1),
            BuildPetAd(ownerId, Day1.AddDays(1), type: PetAdType.Sale, price: 10m));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync("type=");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(2, ItemIds(json).Count);
        Assert.Equal(2, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("species=%00")]
    [InlineData("species=Dog%00")]
    public async Task ListPetAds_SpeciesWithNullCharacter_Returns400WithOnlyThatField(string query)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1, species: PetSpecies.Dog));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync(query);

        // Assert
        await AssertBadRequestForOnlyAsync(response, "species");
    }

    [Fact]
    public async Task ListPetAds_SpeciesUnknownName_Returns400WithOnlyThatField()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync("species=Hamster");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "species");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("4")]
    [InlineData("99")]
    public async Task ListPetAds_SpeciesNumeric_Returns400WithOnlyThatField(string species)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync($"species={species}");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "species");
    }

    [Theory]
    [InlineData(PetSpecies.Dog)]
    [InlineData(PetSpecies.Cat)]
    [InlineData(PetSpecies.Bird)]
    [InlineData(PetSpecies.Rabbit)]
    [InlineData(PetSpecies.Other)]
    public async Task ListPetAds_EachAllowedSpecies_ReturnsOnlyActiveAdsOfThatSpecies(PetSpecies species)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var matching = Enumerable.Range(0, 3)
            .Select(day => BuildPetAd(ownerId, Day1.AddDays(day), species: species))
            .ToArray();
        var others = Enum.GetValues<PetSpecies>().Where(other => other != species)
            .Select(other => BuildPetAd(ownerId, Day1, species: other))
            .ToArray();
        await factory.SeedAdsAsync(matching);
        await factory.SeedAdsAsync(others);
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1, species: species, status: PetAdStatus.Closed));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync($"species={species}&pageSize=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal([matching[2].Id, matching[1].Id], ItemIds(json));
        Assert.Equal(3, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.All(json.RootElement.GetProperty("items").EnumerateArray(),
            item => Assert.Equal(species.ToString(), item.GetProperty("species").GetString()));
    }

    [Fact]
    public async Task ListPetAds_NotLoggedInWithValidSpecies_Returns200()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var rabbit = BuildPetAd(ownerId, Day1, species: PetSpecies.Rabbit);
        await factory.SeedAdsAsync(rabbit);
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync("species=Rabbit");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal([rabbit.Id], ItemIds(json));
    }

    [Fact]
    public async Task ListPetAds_ClosedAdOfFilteredSpecies_IsNotListed()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        await factory.SeedAdsAsync(BuildPetAd(ownerId, Day1, species: PetSpecies.Bird, status: PetAdStatus.Closed));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync("species=Bird");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Empty(ItemIds(json));
        Assert.Equal(0, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ListPetAds_NotLoggedIn_Returns200()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetPetAdsAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ListPetAds_TamperedCookie_Returns200()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, PetAdsUrl);
        request.Headers.Add("Cookie", "pawspost.auth=tampered-value-that-is-not-valid");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ListPetAds_LoggedInOwnerWithClosedAd_ClosedAdIsNotListed()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerClient = await factory.CreateLoggedInClientAsync("owner@x.com");
        var ownerId = await ownerClient.GetMyIdAsync();
        var active = BuildPetAd(ownerId, Day1);
        await factory.SeedAdsAsync(active, BuildPetAd(ownerId, Day1.AddDays(1), status: PetAdStatus.Closed));

        // Act
        var response = await ownerClient.GetPetAdsAsync();

        // Assert
        using var json = await ReadJsonAsync(response);
        Assert.Equal([active.Id], ItemIds(json));
        Assert.Equal(1, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task CreatePetAd_NotLoggedInAfterPublicList_StillReturns401()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var listResponse = await client.GetPetAdsAsync();
        var createResponse = await client.PostPetAdAsync(ValidAdoptionBody());

        // Assert
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, createResponse.StatusCode);
        Assert.Equal("application/problem+json", createResponse.Content.Headers.ContentType?.MediaType);
        Assert.Empty(await factory.GetStoredAdsAsync());
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static List<Guid> ItemIds(JsonDocument json)
    {
        return json.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid()).ToList();
    }
}
