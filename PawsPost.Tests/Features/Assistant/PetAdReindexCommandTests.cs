using PawsPost.Features.Assistant;
using PawsPost.Tests.Infrastructure;
using static PawsPost.Tests.Infrastructure.AssistantTestHelpers;
using static PawsPost.Tests.Infrastructure.PetAdTestHelpers;

namespace PawsPost.Tests.Features.Assistant;

public class PetAdReindexCommandTests
{
    private const string OwnerEmail = "owner.private@example.com";

    [Fact]
    public async Task RunAsync_AllIndexed_ReturnsExitCodeZero()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync(OwnerEmail);
        var ad = BuildSearchAd(ownerId, "Calm dog");
        await factory.SeedAdsAsync(ad);
        var output = new StringWriter();

        // Act
        var exitCode = await PetAdReindexCommand.RunAsync(factory.Services, output, CancellationToken.None);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(factory.VectorStore.Contains(ad.Id));
        Assert.Contains("Re-indexed 1 of 1", output.ToString());
    }

    [Fact]
    public async Task RunAsync_SomeFailed_ReturnsExitCodeOne()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync(OwnerEmail);
        await factory.SeedAdsAsync(
            BuildSearchAd(ownerId, "Failingtitle dog", dayOffset: 0),
            BuildSearchAd(ownerId, "Healthytitle dog", dayOffset: 1));
        factory.EmbeddingProvider.FailWhen = text => text.Contains("Failingtitle");
        var output = new StringWriter();

        // Act
        var exitCode = await PetAdReindexCommand.RunAsync(factory.Services, output, CancellationToken.None);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("Re-indexed 1 of 2", output.ToString());
        Assert.Contains("1 failed", output.ToString());
    }

    [Fact]
    public async Task RunAsync_Output_HasCountsAndNoPersonalData()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync(OwnerEmail);
        var failing = BuildSearchAd(ownerId, "Failingtitle dog", dayOffset: 0);
        var healthy = BuildSearchAd(ownerId, "Healthytitle dog", dayOffset: 1);
        await factory.SeedAdsAsync(failing, healthy);
        factory.EmbeddingProvider.FailWhen = text => text.Contains("Failingtitle");
        var output = new StringWriter();

        // Act
        await PetAdReindexCommand.RunAsync(factory.Services, output, CancellationToken.None);

        // Assert
        var text = output.ToString();
        Assert.Contains("Re-indexed 1 of 2", text);
        Assert.DoesNotContain(OwnerEmail, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Failingtitle", text);
        Assert.DoesNotContain("Healthytitle", text);
        Assert.DoesNotContain(failing.Id.ToString(), text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(healthy.Id.ToString(), text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ownerId.ToString(), text, StringComparison.OrdinalIgnoreCase);
    }
}
