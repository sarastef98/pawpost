using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PawsPost.Data;
using PawsPost.Features.Auth;
using PawsPost.Tests.Infrastructure;

namespace PawsPost.Tests.Features.Auth;

public class MeTests
{
    [Fact]
    public async Task Me_AfterLogin_ReturnsOwnIdAndEmail()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        var registered = await client.PostAsJsonAsync("/api/auth/register", new { displayName = "Anna", email = "a@x.com", password = AuthTestHelpers.ValidPassword });
        var registeredUser = await registered.Content.ReadFromJsonAsync<UserResponse>();
        await client.LoginAsync("a@x.com");

        // Act
        var response = await client.GetAsync("/api/auth/me");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal(registeredUser?.Id, me?.Id);
        Assert.Equal("a@x.com", me?.Email);
    }

    [Fact]
    public async Task Me_ValidCookieButUserDeleted_Returns401()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        await client.RegisterAsync("a@x.com");
        await client.LoginAsync("a@x.com");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.RemoveRange(db.Users);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await client.GetAsync("/api/auth/me");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_TwoUsers_EachSeesOnlyOwnData()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var clientA = factory.CreateApiClient();
        var clientB = factory.CreateApiClient();
        await clientA.RegisterAsync("a@x.com");
        await clientA.RegisterAsync("b@x.com");
        await clientA.LoginAsync("a@x.com");
        await clientB.LoginAsync("b@x.com");

        // Act
        var responseB = await clientB.GetAsync("/api/auth/me");

        // Assert
        var text = await responseB.Content.ReadAsStringAsync();
        Assert.Contains("b@x.com", text);
        Assert.DoesNotContain("a@x.com", text);
    }

    [Fact]
    public async Task Me_NotLoggedIn_Returns401ProblemDetails_NoRedirect()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetAsync("/api/auth/me");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(401, problem?.Status);
    }

    [Fact]
    public async Task Me_TamperedCookie_Returns401()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add("Cookie", "pawspost.auth=tampered-value");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }
}
