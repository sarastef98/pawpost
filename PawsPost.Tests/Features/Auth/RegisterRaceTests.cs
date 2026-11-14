using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PawsPost.Data;
using PawsPost.Tests.Infrastructure;

namespace PawsPost.Tests.Features.Auth;

/// <summary>
/// A real race between two sign-ups cannot be timed reliably in a test. Instead, an EF interceptor runs
/// just before the new user is saved, after the service's own check and Identity's duplicate check have
/// already passed. That is exactly the window in which a competing request wins in a real race.
/// </summary>
public class RegisterRaceTests
{
    private const string Email = "a@x.com";

    [Fact]
    public async Task Register_SameEmailSavedByAnotherRequestBeforeInsert_Returns409ProblemDetails()
    {
        // Arrange
        using var factory = new WebApplicationFactoryWithInterceptor(new BeforeUserInsertInterceptor(InsertCompetingUserAsync));
        var client = factory.CreateApiClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = Email, password = AuthTestHelpers.ValidPassword });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Register_UnrelatedDatabaseErrorOnInsert_IsNotReportedAsConflict()
    {
        // Arrange
        var interceptor = new BeforeUserInsertInterceptor(
            (_, _) => throw new DbUpdateException("Simulated database failure."));
        using var factory = new WebApplicationFactoryWithInterceptor(interceptor);
        var client = factory.CreateApiClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = Email, password = AuthTestHelpers.ValidPassword });

        // Assert
        // The email does not exist, so the error is not a duplicate and must not be hidden behind a 409.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    // Saves a user with the same email through a separate context, like a second request would.
    private static async Task InsertCompetingUserAsync(string connectionString, CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
        await using var otherRequestDb = new AppDbContext(options);
        otherRequestDb.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            UserName = Email,
            NormalizedUserName = Email.ToUpperInvariant(),
            Email = Email,
            NormalizedEmail = Email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString()
        });
        await otherRequestDb.SaveChangesAsync(cancellationToken);
    }

    private sealed class WebApplicationFactoryWithInterceptor(BeforeUserInsertInterceptor interceptor) : PawsPostFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
                services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(interceptor)));
        }
    }

    /// <summary>Runs an action once, right before the first save that inserts a new user.</summary>
    private sealed class BeforeUserInsertInterceptor(Func<string, CancellationToken, Task> beforeInsert) : SaveChangesInterceptor
    {
        private bool _hasRun;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            if (!_hasRun && context is not null
                && context.ChangeTracker.Entries<User>().Any(entry => entry.State == EntityState.Added))
            {
                _hasRun = true;
                await beforeInsert(context.Database.GetConnectionString()!, cancellationToken);
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
