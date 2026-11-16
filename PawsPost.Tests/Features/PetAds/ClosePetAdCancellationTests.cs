using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PawsPost.Data;
using PawsPost.Features.PetAds;
using PawsPost.Tests.Infrastructure;
using static PawsPost.Tests.Infrastructure.AssistantTestHelpers;
using static PawsPost.Tests.Infrastructure.PetAdTestHelpers;

namespace PawsPost.Tests.Features.PetAds;

/// <summary>
/// A client that disconnects right after the close is saved cancels the request token.
/// The close is already committed, so the index removal must still finish, and nothing may be logged
/// as an index failure. The service is called directly with a token that an EF interceptor cancels
/// right after the save, because the exact moment of a real disconnect cannot be timed over HTTP.
/// </summary>
public class ClosePetAdCancellationTests
{
    private const string OwnerEmail = "owner@x.com";

    [Fact]
    public async Task CloseAsync_RequestCancelledAfterSave_ReturnsClosed()
    {
        // Arrange
        using var factory = new CancelFactory(new CancelAfterSaveInterceptor());
        var (scope, ownerId, ad) = await ArrangeOwnerWithIndexedAdAsync(factory, factory);
        using var disposeScope = scope;
        var service = scope.ServiceProvider.GetRequiredService<IPetAdService>();

        // Act
        var outcome = await service.CloseAsync(ad.Id, ownerId, factory.Interceptor.Token);

        // Assert
        Assert.True(factory.Interceptor.HasRun);
        Assert.Equal(ClosePetAdOutcome.Closed, outcome);
        Assert.Equal(PetAdStatus.Closed, (await factory.GetStoredAdAsync(ad.Id))!.Status);
    }

    [Fact]
    public async Task CloseAsync_RequestCancelledAfterSave_StillRemovesFromIndex()
    {
        // Arrange
        using var factory = new CancelFactory(new CancelAfterSaveInterceptor());
        var (scope, ownerId, ad) = await ArrangeOwnerWithIndexedAdAsync(factory, factory);
        using var disposeScope = scope;
        var service = scope.ServiceProvider.GetRequiredService<IPetAdService>();

        // Act
        await service.CloseAsync(ad.Id, ownerId, factory.Interceptor.Token);

        // Assert
        Assert.True(factory.Interceptor.HasRun);
        Assert.False(factory.VectorStore.Contains(ad.Id));
    }

    [Fact]
    public async Task CloseAsync_RequestCancelledAfterSave_LogsNoIndexWarning()
    {
        // Arrange
        var provider = new CapturingLoggerProvider();
        using var factory = new CancelFactory(new CancelAfterSaveInterceptor());
        using var loggedFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(provider)));
        var (scope, ownerId, ad) = await ArrangeOwnerWithIndexedAdAsync(factory, loggedFactory);
        using var disposeScope = scope;
        var service = scope.ServiceProvider.GetRequiredService<IPetAdService>();

        // Act
        await service.CloseAsync(ad.Id, ownerId, factory.Interceptor.Token);

        // Assert
        Assert.True(factory.Interceptor.HasRun);
        Assert.DoesNotContain(provider.Messages, message => message.StartsWith("Warning|"));
    }

    // Seeds an owner with an indexed Active ad, arms the interceptor and creates a scope for the service.
    private static async Task<(IServiceScope Scope, Guid OwnerId, PetAd Ad)> ArrangeOwnerWithIndexedAdAsync(
        CancelFactory factory, WebApplicationFactory<Program> host)
    {
        var owner = await host.CreateLoggedInClientAsync(OwnerEmail);
        var ownerId = await owner.GetMyIdAsync();
        var ad = BuildSearchAd(ownerId, "Calm dog");
        await host.SeedAndIndexAdsAsync(ad);
        factory.Interceptor.Arm();

        var scope = host.Services.CreateScope();
        return (scope, ownerId, ad);
    }

    private sealed class CancelFactory(CancelAfterSaveInterceptor interceptor) : PawsPostFactory
    {
        public CancelAfterSaveInterceptor Interceptor { get; } = interceptor;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
                services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(Interceptor)));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                Interceptor.Dispose();
            }
        }
    }

    /// <summary>
    /// Once armed, cancels <see cref="Token"/> right after the first save that changes rows, like a client
    /// that disconnects after the close is committed. Not armed during test setup.
    /// </summary>
    private sealed class CancelAfterSaveInterceptor : SaveChangesInterceptor, IDisposable
    {
        private readonly CancellationTokenSource _cancellation = new();
        private volatile bool _armed;
        private volatile bool _hasRun;

        public CancellationToken Token => _cancellation.Token;

        public bool HasRun => _hasRun;

        public void Arm() => _armed = true;

        public void Dispose() => _cancellation.Dispose();

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (_armed && !_hasRun && result > 0)
            {
                _hasRun = true;
                _cancellation.Cancel();
            }

            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }
    }
}
