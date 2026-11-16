using System.Net;
using System.Net.Http.Json;
using PawsPost.Features.Ai;
using PawsPost.Features.Assistant;
using PawsPost.Features.PetAds;
using PawsPost.Tests.Infrastructure;
using static PawsPost.Tests.Infrastructure.AssistantTestHelpers;
using static PawsPost.Tests.Infrastructure.PetAdTestHelpers;

namespace PawsPost.Tests.Features.PetAds;

public class CreatePetAdEmbeddingTests
{
    [Fact]
    public async Task CreatePetAd_Success_StoresVectorForTheNewAd()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");

        // Act
        var response = await client.PostPetAdAsync(ValidAdoptionBody());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var ad = await response.Content.ReadFromJsonAsync<PetAdResponse>();
        Assert.True(factory.VectorStore.Contains(ad!.Id));
        var call = Assert.Single(factory.EmbeddingProvider.Calls);
        Assert.Equal(EmbeddingPurpose.Document, call.Purpose);
    }

    [Fact]
    public async Task CreatePetAd_NewAd_BecomesSearchable()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");
        var created = await (await client.PostPetAdAsync(ValidAdoptionBody(("description", "A calm dog."))))
            .Content.ReadFromJsonAsync<PetAdResponse>();

        // Act
        var response = await client.PostAssistantSearchAsync(new { question = "calm dog" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AssistantSearchResponse>();
        Assert.Equal(created!.Id, Assert.Single(body!.Ads).Id);
    }

    [Fact]
    public async Task CreatePetAd_EmbeddingText_ContainsOnlyPublicFields()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");

        // Act
        await client.PostPetAdAsync(ValidAdoptionBody());

        // Assert
        var text = Assert.Single(factory.EmbeddingProvider.Calls).Text;
        Assert.Contains("Title: Friendly beagle looking for a home", text);
        Assert.Contains("Type: Adoption", text);
        Assert.Contains("Species: Dog", text);
        Assert.Contains("Breed: Beagle", text);
        Assert.Contains("Age in months: 24", text);
        Assert.Contains("Description: Friendly and healthy.", text);
        Assert.Contains("Location: Skopje", text);
        Assert.DoesNotContain("Price", text);
        Assert.DoesNotContain("Status", text);
    }

    [Fact]
    public async Task CreatePetAd_Success_EmbeddingInputHasNoOwnerData()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("owner@x.com");
        var ownerId = await client.GetMyIdAsync();

        // Act
        await client.PostPetAdAsync(ValidAdoptionBody());

        // Assert
        var text = Assert.Single(factory.EmbeddingProvider.Calls).Text;
        Assert.DoesNotContain("owner@x.com", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ownerId.ToString(), text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreatePetAd_EmbeddingFails_StillReturns201AndStoresTheAd()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");
        factory.EmbeddingProvider.ExceptionToThrow = new AiProviderException("secret-provider-detail");

        // Act
        var response = await client.PostPetAdAsync(ValidAdoptionBody());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var ad = await response.Content.ReadFromJsonAsync<PetAdResponse>();
        var stored = Assert.Single(await factory.GetStoredAdsAsync());
        Assert.Equal(ad!.Id, stored.Id);
        Assert.False(factory.VectorStore.Contains(ad.Id));
    }

    [Fact]
    public async Task CreatePetAd_EmbeddingFails_ResponseHasNoProviderDetails()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");
        factory.EmbeddingProvider.ExceptionToThrow = new AiProviderException("secret-provider-detail");

        // Act
        var response = await client.PostPetAdAsync(ValidAdoptionBody());

        // Assert
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret-provider-detail", text);
        Assert.DoesNotContain("AiProviderException", text);
    }

    [Fact]
    public async Task CreatePetAd_InvalidRequest_DoesNotEmbedAnything()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync("a@x.com");

        // Act
        var response = await client.PostPetAdAsync(ValidAdoptionBody(("title", "")));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.EmbeddingProvider.Calls);
    }
}
