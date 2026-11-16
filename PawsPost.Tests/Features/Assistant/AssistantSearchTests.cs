using System.Net;
using System.Net.Http.Json;
using PawsPost.Data;
using PawsPost.Features.Assistant;
using PawsPost.Tests.Infrastructure;
using PawsPost.Tests.Infrastructure.Fakes;
using static PawsPost.Tests.Infrastructure.AssistantTestHelpers;
using static PawsPost.Tests.Infrastructure.PetAdTestHelpers;

namespace PawsPost.Tests.Features.Assistant;

public class AssistantSearchTests
{
    [Fact]
    public async Task Search_MatchingActiveAds_Returns200WithAnswerAndAds()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var first = BuildSearchAd(ownerId, "Calm dog one", description: "Calm and quiet.");
        var second = BuildSearchAd(ownerId, "Calm dog two", description: "Calm and sweet.");
        await factory.SeedAndIndexAdsAsync(first, second);
        var client = await factory.CreateLoggedInClientAsync("user@x.com");

        // Act
        var response = await client.PostAssistantSearchAsync(new { question = "a calm dog" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AssistantSearchResponse>();
        Assert.NotNull(body);
        Assert.Equal(FakeChatProvider.FixedAnswer, body.Answer);
        Assert.Equal(new[] { first.Id, second.Id }.Order(), body.Ads.Select(ad => ad.Id).Order());
        var ad = Assert.Single(body.Ads, item => item.Id == first.Id);
        Assert.Equal("Calm dog one", ad.Title);
        Assert.Equal(PetAdType.Adoption, ad.Type);
        Assert.Equal(PetSpecies.Dog, ad.Species);
        Assert.Equal("Beagle", ad.Breed);
        Assert.Equal(12, ad.AgeInMonths);
        Assert.Equal("Calm and quiet.", ad.Description);
        Assert.Null(ad.Location);
        Assert.Equal(PetAdStatus.Active, ad.Status);
        Assert.Single(factory.ChatProvider.Prompts);
    }

    [Fact]
    public async Task Search_MoreMatchesThanMax_ReturnsAtMostMaxResults()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var ads = Enumerable.Range(1, 7).Select(i => BuildSearchAd(ownerId, $"Dog number {i}", dayOffset: i)).ToArray();
        await factory.SeedAndIndexAdsAsync(ads);
        var client = await factory.CreateLoggedInClientAsync("user@x.com");

        // Act
        var response = await client.PostAssistantSearchAsync(new { question = "dog" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AssistantSearchResponse>();
        Assert.Equal(5, body!.Ads.Count);
    }

    [Fact]
    public async Task Search_Matches_AreOrderedByBestMatchFirst()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var partial = BuildSearchAd(ownerId, "Partial match", description: "Friendly and healthy.");
        var best = BuildSearchAd(ownerId, "Best match", description: "Calm and quiet.");
        await factory.SeedAndIndexAdsAsync(partial, best);
        var client = await factory.CreateLoggedInClientAsync("user@x.com");

        // Act
        var response = await client.PostAssistantSearchAsync(new { question = "calm dog" });

        // Assert
        var body = await response.Content.ReadFromJsonAsync<AssistantSearchResponse>();
        Assert.Equal([best.Id, partial.Id], body!.Ads.Select(ad => ad.Id));
    }

    [Fact]
    public async Task Search_QuestionWithSurroundingSpaces_IsTrimmedBeforeEmbedding()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("user@x.com");

        // Act
        var response = await client.PostAssistantSearchAsync(new { question = "   a calm dog  " });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var call = Assert.Single(factory.EmbeddingProvider.Calls);
        Assert.Equal("a calm dog", call.Text);
        Assert.Equal(PawsPost.Features.Ai.EmbeddingPurpose.Query, call.Purpose);
    }

    [Fact]
    public async Task Search_NoAdAboveThreshold_ReturnsFixedAnswerAndEmptyList()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        await factory.SeedAndIndexAdsAsync(BuildSearchAd(ownerId, "A rabbit", species: PetSpecies.Rabbit));
        var client = await factory.CreateLoggedInClientAsync("user@x.com");

        // Act
        var response = await client.PostAssistantSearchAsync(new { question = "cat" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AssistantSearchResponse>();
        Assert.Equal(AssistantService.NoMatchAnswer, body!.Answer);
        Assert.Empty(body.Ads);
    }

    [Fact]
    public async Task Search_NoAdAboveThreshold_DoesNotCallChatProvider()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        await factory.SeedAndIndexAdsAsync(BuildSearchAd(ownerId, "A rabbit", species: PetSpecies.Rabbit));
        var client = await factory.CreateLoggedInClientAsync("user@x.com");

        // Act
        await client.PostAssistantSearchAsync(new { question = "cat" });

        // Assert
        Assert.Empty(factory.ChatProvider.Prompts);
    }

    [Fact]
    public async Task Search_NoAdsAtAll_ReturnsEmptyList()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("user@x.com");

        // Act
        var response = await client.PostAssistantSearchAsync(new { question = "dog" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AssistantSearchResponse>();
        Assert.Empty(body!.Ads);
    }

    [Theory]
    [InlineData(PetAdStatus.Draft)]
    [InlineData(PetAdStatus.Reserved)]
    [InlineData(PetAdStatus.Closed)]
    public async Task Search_NotActiveAdWithStoredVector_IsNotReturned(PetAdStatus status)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var ad = BuildSearchAd(ownerId, "Dog that was active");
        await factory.SeedAndIndexAdsAsync(ad);
        await factory.SetStatusAsync(ad.Id, status);
        var client = await factory.CreateLoggedInClientAsync("user@x.com");

        // Act
        var response = await client.PostAssistantSearchAsync(new { question = "dog" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AssistantSearchResponse>();
        Assert.Empty(body!.Ads);
        Assert.Empty(factory.ChatProvider.Prompts);
    }

    [Fact]
    public async Task Search_VectorOfRemovedAd_IsNotReturned()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var ghost = BuildSearchAd(ownerId, "Ghost dog");
        await factory.IndexAdsAsync(ghost); // vector only, no ad row
        var client = await factory.CreateLoggedInClientAsync("user@x.com");

        // Act
        var response = await client.PostAssistantSearchAsync(new { question = "dog" });

        // Assert
        var body = await response.Content.ReadFromJsonAsync<AssistantSearchResponse>();
        Assert.Empty(body!.Ads);
    }

    [Fact]
    public async Task Search_AdRemovedFromIndex_IsNotReturned()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var removed = BuildSearchAd(ownerId, "Removed dog");
        var kept = BuildSearchAd(ownerId, "Kept dog");
        await factory.SeedAndIndexAdsAsync(removed, kept);
        await factory.RemoveFromIndexAsync(removed.Id);
        var client = await factory.CreateLoggedInClientAsync("user@x.com");

        // Act
        var response = await client.PostAssistantSearchAsync(new { question = "dog" });

        // Assert
        var body = await response.Content.ReadFromJsonAsync<AssistantSearchResponse>();
        var ad = Assert.Single(body!.Ads);
        Assert.Equal(kept.Id, ad.Id);
    }

    [Fact]
    public async Task Search_EmptyQuestion_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("user@x.com");

        // Act
        var response = await client.PostAssistantSearchAsync(new { question = "" });

        // Assert
        await AssertBadRequestForOnlyAsync(response, "question");
        Assert.Empty(factory.EmbeddingProvider.Calls);
    }

    [Fact]
    public async Task Search_WhitespaceOnlyQuestion_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("user@x.com");

        // Act
        var response = await client.PostAssistantSearchAsync(new { question = "   " });

        // Assert
        await AssertBadRequestForOnlyAsync(response, "question");
        Assert.Empty(factory.EmbeddingProvider.Calls);
    }

    [Fact]
    public async Task Search_MissingQuestion_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("user@x.com");

        // Act
        var response = await client.PostAssistantSearchAsync(new { });

        // Assert
        await AssertBadRequestForOnlyAsync(response, "question");
        Assert.Empty(factory.EmbeddingProvider.Calls);
    }

    [Fact]
    public async Task Search_QuestionLongerThanMax_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("user@x.com");

        // Act
        var response = await client.PostAssistantSearchAsync(
            new { question = new string('a', AssistantSearchRequest.QuestionMaxLength + 1) });

        // Assert
        await AssertBadRequestForOnlyAsync(response, "question");
        Assert.Empty(factory.EmbeddingProvider.Calls);
    }

    [Fact]
    public async Task Search_QuestionOfExactlyMaxLength_Returns200()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("user@x.com");

        // Act
        var response = await client.PostAssistantSearchAsync(
            new { question = new string('a', AssistantSearchRequest.QuestionMaxLength) });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(factory.EmbeddingProvider.Calls);
    }

    [Fact]
    public async Task Search_NullBody_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("user@x.com");
        using var content = new StringContent("null", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(SearchUrl, content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(factory.EmbeddingProvider.Calls);
    }
}
