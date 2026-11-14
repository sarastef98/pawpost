using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PawsPost.Data;

namespace PawsPost.Tests.Infrastructure;

public class PawsPostFactoryTests
{
    [Fact]
    public async Task Factory_Startup_AppliesMigrationsAndServesRequests()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetAsync("/api/health");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pending = await db.Database.GetPendingMigrationsAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(pending);
    }
}
