using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using PawsPost.Data;
using PawsPost.Features.Ai;
using PawsPost.Features.Assistant;
using PawsPost.Tests.Infrastructure;
using static PawsPost.Tests.Infrastructure.AssistantTestHelpers;
using static PawsPost.Tests.Infrastructure.PetAdTestHelpers;

namespace PawsPost.Tests.Features.Assistant;

public class ReindexPetAdsTests
{
    private const string OwnerEmail = "owner.private@example.com";

    [Fact]
    public async Task ReindexMissing_ActiveAdWithoutVector_StoresVector()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync(OwnerEmail);
        var ad = BuildSearchAd(ownerId, "Calm dog");
        await factory.SeedAdsAsync(ad);

        // Act
        await factory.ReindexAsync();

        // Assert
        Assert.True(factory.VectorStore.Contains(ad.Id));
    }

    [Fact]
    public async Task ReindexMissing_ActiveAdWithoutVector_BecomesSearchable()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync(OwnerEmail);
        var ad = BuildSearchAd(ownerId, "Calm dog", description: "Calm and quiet.");
        await factory.SeedAdsAsync(ad);
        var client = await factory.CreateLoggedInClientAsync("user@x.com");
        var before = await client.PostAssistantSearchAsync(new { question = "a calm dog" });
        var beforeBody = await before.Content.ReadFromJsonAsync<AssistantSearchResponse>();
        Assert.Empty(beforeBody!.Ads);

        // Act
        await factory.ReindexAsync();
        var response = await client.PostAssistantSearchAsync(new { question = "a calm dog" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AssistantSearchResponse>();
        Assert.Equal(ad.Id, Assert.Single(body!.Ads).Id);
    }

    [Fact]
    public async Task ReindexMissing_ActiveAdWithoutVector_ReturnsIndexedCount()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync(OwnerEmail);
        await factory.SeedAdsAsync(BuildSearchAd(ownerId, "Calm dog"));

        // Act
        var result = await factory.ReindexAsync();

        // Assert
        Assert.Equal(new PetAdReindexResult(Missing: 1, Indexed: 1, Failed: 0), result);
    }

    [Fact]
    public async Task ReindexMissing_NoAds_ReturnsZeroCounts()
    {
        // Arrange
        using var factory = new PawsPostFactory();

        // Act
        var result = await factory.ReindexAsync();

        // Assert
        Assert.Equal(new PetAdReindexResult(0, 0, 0), result);
        Assert.Empty(factory.EmbeddingProvider.Calls);
    }

    [Fact]
    public async Task ReindexMissing_AdWithVector_IsNotSentToProviderAgain()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync(OwnerEmail);
        var indexed = BuildSearchAd(ownerId, "Indexedtitle dog", dayOffset: 0);
        var missing = BuildSearchAd(ownerId, "Missingtitle dog", dayOffset: 1);
        await factory.SeedAndIndexAdsAsync(indexed);
        await factory.SeedAdsAsync(missing);
        var callsBefore = factory.EmbeddingProvider.Calls.Count;

        // Act
        var result = await factory.ReindexAsync();

        // Assert
        var newCalls = factory.EmbeddingProvider.Calls.Skip(callsBefore).ToList();
        var call = Assert.Single(newCalls);
        Assert.Contains("Missingtitle", call.Text);
        Assert.DoesNotContain(newCalls, item => item.Text.Contains("Indexedtitle"));
        Assert.Equal(new PetAdReindexResult(1, 1, 0), result);
    }

    [Theory]
    [InlineData(PetAdStatus.Draft)]
    [InlineData(PetAdStatus.Reserved)]
    [InlineData(PetAdStatus.Closed)]
    public async Task ReindexMissing_NonActiveAd_IsNotIndexed(PetAdStatus status)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync(OwnerEmail);
        var ad = BuildSearchAd(ownerId, "Calm dog", status: status);
        await factory.SeedAdsAsync(ad);

        // Act
        var result = await factory.ReindexAsync();

        // Assert
        Assert.False(factory.VectorStore.Contains(ad.Id));
        Assert.Empty(factory.EmbeddingProvider.Calls);
        Assert.Equal(new PetAdReindexResult(0, 0, 0), result);
    }

    [Fact]
    public async Task ReindexMissing_SecondRun_SendsNothingToProvider()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync(OwnerEmail);
        await factory.SeedAdsAsync(
            BuildSearchAd(ownerId, "Calm dog"),
            BuildSearchAd(ownerId, "Calm cat", PetSpecies.Cat, dayOffset: 1));
        await factory.ReindexAsync();
        var callsAfterFirstRun = factory.EmbeddingProvider.Calls.Count;

        // Act
        await factory.ReindexAsync();

        // Assert
        Assert.Equal(2, callsAfterFirstRun);
        Assert.Equal(callsAfterFirstRun, factory.EmbeddingProvider.Calls.Count);
    }

    [Fact]
    public async Task ReindexMissing_SecondRun_ReturnsZeroMissing()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync(OwnerEmail);
        await factory.SeedAdsAsync(BuildSearchAd(ownerId, "Calm dog"));
        await factory.ReindexAsync();

        // Act
        var result = await factory.ReindexAsync();

        // Assert
        Assert.Equal(new PetAdReindexResult(0, 0, 0), result);
    }

    [Fact]
    public async Task ReindexMissing_OneAdFails_OtherAdsAreStillIndexed()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync(OwnerEmail);
        var failing = BuildSearchAd(ownerId, "Failingtitle dog", dayOffset: 0);
        var healthy = BuildSearchAd(ownerId, "Healthytitle dog", dayOffset: 1);
        await factory.SeedAdsAsync(failing, healthy);
        factory.EmbeddingProvider.FailWhen = text => text.Contains("Failingtitle");

        // Act
        await factory.ReindexAsync();

        // Assert
        Assert.True(factory.VectorStore.Contains(healthy.Id));
    }

    [Fact]
    public async Task ReindexMissing_OneAdFails_FailedAdStaysWithoutVector()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync(OwnerEmail);
        var failing = BuildSearchAd(ownerId, "Failingtitle dog", dayOffset: 0);
        var healthy = BuildSearchAd(ownerId, "Healthytitle dog", dayOffset: 1);
        await factory.SeedAdsAsync(failing, healthy);
        factory.EmbeddingProvider.FailWhen = text => text.Contains("Failingtitle");

        // Act
        await factory.ReindexAsync();

        // Assert
        Assert.False(factory.VectorStore.Contains(failing.Id));
    }

    [Fact]
    public async Task ReindexMissing_OneAdFails_ReturnsFailedCount()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync(OwnerEmail);
        await factory.SeedAdsAsync(
            BuildSearchAd(ownerId, "Failingtitle dog", dayOffset: 0),
            BuildSearchAd(ownerId, "Healthytitle dog", dayOffset: 1));
        factory.EmbeddingProvider.FailWhen = text => text.Contains("Failingtitle");

        // Act
        var result = await factory.ReindexAsync();

        // Assert
        Assert.Equal(new PetAdReindexResult(Missing: 2, Indexed: 1, Failed: 1), result);
    }

    [Fact]
    public async Task ReindexMissing_FailedAd_IsRetriedOnNextRun()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync(OwnerEmail);
        var failing = BuildSearchAd(ownerId, "Failingtitle dog", dayOffset: 0);
        var healthy = BuildSearchAd(ownerId, "Healthytitle dog", dayOffset: 1);
        await factory.SeedAdsAsync(failing, healthy);
        factory.EmbeddingProvider.FailWhen = text => text.Contains("Failingtitle");
        await factory.ReindexAsync();
        factory.EmbeddingProvider.FailWhen = null;

        // Act
        var result = await factory.ReindexAsync();

        // Assert
        Assert.True(factory.VectorStore.Contains(failing.Id));
        Assert.Equal(new PetAdReindexResult(1, 1, 0), result);
    }

    [Fact]
    public async Task ReindexMissing_ProviderFails_LogsOnlyAdIdAndExceptionType()
    {
        // Arrange
        const string secretMessage = "Secret provider detail 9f3a";
        var provider = new CapturingLoggerProvider();
        using var factory = new PawsPostFactory();
        using var loggedFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(provider)));
        var ownerId = await factory.SeedOwnerAsync(OwnerEmail);
        var ad = BuildSearchAd(ownerId, "Privatetitle dog", description: "Privatedescription text.");
        await factory.SeedAdsAsync(ad);
        factory.EmbeddingProvider.ExceptionToThrow = new AiProviderException(secretMessage);

        // Act
        await loggedFactory.ReindexAsync();

        // Assert
        var warning = Assert.Single(
            provider.Messages,
            message => message.StartsWith("Warning|") && message.Contains(ad.Id.ToString()));
        Assert.Contains(nameof(AiProviderException), warning);
        Assert.All(provider.Messages, message =>
        {
            Assert.DoesNotContain(secretMessage, message);
            Assert.DoesNotContain("Privatetitle", message);
            Assert.DoesNotContain("Privatedescription", message);
            Assert.DoesNotContain(OwnerEmail, message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task ReindexMissing_StorageFails_StopsTheRun()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync(OwnerEmail);
        var first = BuildSearchAd(ownerId, "First dog", dayOffset: 0);
        var second = BuildSearchAd(ownerId, "Second dog", dayOffset: 1);
        await factory.SeedAdsAsync(first, second);
        factory.VectorStore.UpsertExceptionToThrow = new InvalidOperationException("storage down");

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => factory.ReindexAsync());

        // Assert
        Assert.Equal("storage down", exception.Message);
        Assert.False(factory.VectorStore.Contains(first.Id));
        Assert.False(factory.VectorStore.Contains(second.Id));
        Assert.Single(factory.EmbeddingProvider.Calls);
    }

    [Fact]
    public async Task ReindexMissing_EmbeddingInput_HasNoOwnerData()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync(OwnerEmail);
        await factory.SeedAdsAsync(
            BuildSearchAd(ownerId, "Calm dog"),
            BuildSearchAd(ownerId, "Calm cat", PetSpecies.Cat, dayOffset: 1));

        // Act
        await factory.ReindexAsync();

        // Assert
        Assert.Equal(2, factory.EmbeddingProvider.Calls.Count);
        Assert.All(factory.EmbeddingProvider.Calls, call =>
        {
            Assert.DoesNotContain(OwnerEmail, call.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(ownerId.ToString(), call.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("email", call.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("phone", call.Text, StringComparison.OrdinalIgnoreCase);
        });
    }
}
