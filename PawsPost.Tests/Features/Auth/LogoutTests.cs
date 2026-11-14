using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PawsPost.Tests.Infrastructure;

namespace PawsPost.Tests.Features.Auth;

public class LogoutTests
{
    [Fact]
    public async Task Logout_LoggedIn_Returns204AndClearsCookie()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        await client.RegisterAsync("a@x.com");
        await client.LoginAsync("a@x.com");

        // Act
        var response = await client.PostAsync("/api/auth/logout", null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("pawspost.auth=", StringComparison.Ordinal));
        Assert.Contains("expires=Thu, 01 Jan 1970", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Me_AfterLogout_Returns401()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        await client.RegisterAsync("a@x.com");
        await client.LoginAsync("a@x.com");
        var before = await client.GetAsync("/api/auth/me");

        // Act
        await client.PostAsync("/api/auth/logout", null);
        var after = await client.GetAsync("/api/auth/me");

        // Assert
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        var problem = await after.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(401, problem?.Status);
    }

    [Fact]
    public async Task Logout_ThenLoginAgain_Works()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        await client.RegisterAsync("a@x.com");
        await client.LoginAsync("a@x.com");
        await client.PostAsync("/api/auth/logout", null);

        // Act
        var login = await client.LoginAsync("a@x.com");
        var me = await client.GetAsync("/api/auth/me");

        // Assert
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Logout_NotLoggedIn_Returns401ProblemDetails_NoRedirect()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.PostAsync("/api/auth/logout", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Logout_TamperedCookie_Returns401()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("Cookie", "pawspost.auth=tampered-value");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
