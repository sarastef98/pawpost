using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PawsPost.Data;
using PawsPost.Features.Ai.VectorStores;
using PawsPost.Tests.Infrastructure;
using static PawsPost.Tests.Infrastructure.AssistantTestHelpers;
using static PawsPost.Tests.Infrastructure.PetAdTestHelpers;

namespace PawsPost.Tests.Features.Assistant;

public class SqliteVectorStoreTests
{
    private const string Model = "model-a";

    private static async Task<(PawsPostFactory Factory, PetAd Ad)> CreateFactoryWithAdAsync()
    {
        var factory = new PawsPostFactory();
        var ownerId = await factory.SeedOwnerAsync("owner@x.com");
        var ad = BuildSearchAd(ownerId, "Dog");
        await factory.SeedAdsAsync(ad);
        return (factory, ad);
    }

    private static SqliteVectorStore CreateStore(IServiceScope scope) =>
        new(scope.ServiceProvider.GetRequiredService<AppDbContext>(), TimeProvider.System);

    [Fact]
    public async Task Upsert_SameAdTwice_KeepsOneVector()
    {
        // Arrange
        var (factory, ad) = await CreateFactoryWithAdAsync();
        using var _ = factory;
        using var scope = factory.Services.CreateScope();
        var store = CreateStore(scope);

        // Act
        await store.UpsertAsync(ad.Id, [1f, 0f], Model, CancellationToken.None);
        await store.UpsertAsync(ad.Id, [0f, 1f], Model, CancellationToken.None);

        // Assert
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.PetAdEmbeddings.CountAsync());
        var matches = await store.SearchAsync([0f, 1f], Model, 5, 0.9, CancellationToken.None);
        Assert.Equal(ad.Id, Assert.Single(matches).PetAdId);
        Assert.Empty(await store.SearchAsync([1f, 0f], Model, 5, 0.9, CancellationToken.None));
    }

    [Fact]
    public async Task Search_VectorOfOtherModel_IsIgnored()
    {
        // Arrange
        var (factory, ad) = await CreateFactoryWithAdAsync();
        using var _ = factory;
        using var scope = factory.Services.CreateScope();
        var store = CreateStore(scope);
        await store.UpsertAsync(ad.Id, [1f, 0f], "other-model", CancellationToken.None);

        // Act
        var matches = await store.SearchAsync([1f, 0f], Model, 5, 0.0, CancellationToken.None);

        // Assert
        Assert.Empty(matches);
    }

    [Fact]
    public async Task Search_ScoreBelowMinimum_IsNotReturned()
    {
        // Arrange
        var (factory, ad) = await CreateFactoryWithAdAsync();
        using var _ = factory;
        using var scope = factory.Services.CreateScope();
        var store = CreateStore(scope);
        await store.UpsertAsync(ad.Id, [1f, 1f], Model, CancellationToken.None);

        // Act (cosine of [1,1] and [1,0] is about 0.707)
        var below = await store.SearchAsync([1f, 0f], Model, 5, 0.8, CancellationToken.None);
        var above = await store.SearchAsync([1f, 0f], Model, 5, 0.7, CancellationToken.None);

        // Assert
        Assert.Empty(below);
        Assert.Equal(ad.Id, Assert.Single(above).PetAdId);
    }

    [Fact]
    public async Task Remove_ExistingVector_IsNoLongerFound()
    {
        // Arrange
        var (factory, ad) = await CreateFactoryWithAdAsync();
        using var _ = factory;
        using var scope = factory.Services.CreateScope();
        var store = CreateStore(scope);
        await store.UpsertAsync(ad.Id, [1f, 0f], Model, CancellationToken.None);

        // Act
        await store.RemoveAsync(ad.Id, CancellationToken.None);

        // Assert
        Assert.Empty(await store.SearchAsync([1f, 0f], Model, 5, 0.0, CancellationToken.None));
    }
}
