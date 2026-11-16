using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PawsPost.Data;
using PawsPost.Tests.Infrastructure;
using static PawsPost.Tests.Infrastructure.PetAdTestHelpers;

namespace PawsPost.Tests.Features.PetAds;

/// <summary>
/// A real race cannot be timed reliably. An EF interceptor runs just before the service saves the close,
/// after the ad was loaded as Active, and closes the ad through a separate context. That is exactly the
/// window in which a competing request wins in a real race.
/// </summary>
public class ClosePetAdRaceTests
{
    private const string OwnerEmail = "owner@x.com";

    [Fact]
    public async Task ClosePetAd_ClosedByAnotherRequestBeforeSave_Returns409()
    {
        // Arrange
        using var factory = new RaceFactory(new BeforeCloseSaveInterceptor());
        var (client, _, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail);
        factory.Interceptor.Arm();

        // Act
        var response = await client.ClosePetAdAsync(ad.Id.ToString());

        // Assert
        Assert.True(factory.Interceptor.HasRun);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Ad cannot be closed", problem!.Title);
        Assert.Equal(PetAdStatus.Closed, (await factory.GetStoredAdAsync(ad.Id))!.Status);
    }

    [Fact]
    public async Task ClosePetAd_ClosedByAnotherRequestBeforeSave_DoesNotCallRemove()
    {
        // Arrange
        using var factory = new RaceFactory(new BeforeCloseSaveInterceptor());
        var (client, _, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail);
        factory.Interceptor.Arm();

        // Act
        await client.ClosePetAdAsync(ad.Id.ToString());

        // Assert
        Assert.True(factory.Interceptor.HasRun);
        Assert.Empty(factory.VectorStore.RemovedIds);
    }

    [Fact]
    public async Task ClosePetAd_TwoParallelCloses_OneReturns204AndOneReturns409()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (client, _, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail);

        // Act
        var responses = await Task.WhenAll(
            client.ClosePetAdAsync(ad.Id.ToString()),
            client.ClosePetAdAsync(ad.Id.ToString()));

        // Assert
        Assert.Equal(
            [HttpStatusCode.NoContent, HttpStatusCode.Conflict],
            responses.Select(response => response.StatusCode).Order().ToList());
        Assert.Equal(PetAdStatus.Closed, (await factory.GetStoredAdAsync(ad.Id))!.Status);
    }

    private sealed class RaceFactory(BeforeCloseSaveInterceptor interceptor) : PawsPostFactory
    {
        public BeforeCloseSaveInterceptor Interceptor { get; } = interceptor;

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
                services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(Interceptor)));
        }
    }

    /// <summary>
    /// Once armed, runs once right before the first save that modifies an ad: closes that ad in a separate context.
    /// Not armed during test setup, so seeding is not affected.
    /// </summary>
    private sealed class BeforeCloseSaveInterceptor : SaveChangesInterceptor
    {
        private volatile bool _armed;
        private volatile bool _hasRun;

        public bool HasRun => _hasRun;

        public void Arm() => _armed = true;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            var entry = context?.ChangeTracker.Entries<PetAd>().FirstOrDefault(e => e.State == EntityState.Modified);
            if (_armed && !_hasRun && context is not null && entry is not null)
            {
                _hasRun = true;
                var options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite(context.Database.GetConnectionString()!).Options;
                await using var otherRequestDb = new AppDbContext(options);
                var sameAd = await otherRequestDb.PetAds.SingleAsync(petAd => petAd.Id == entry.Entity.Id, cancellationToken);
                sameAd.Status = PetAdStatus.Closed;
                await otherRequestDb.SaveChangesAsync(cancellationToken);
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
