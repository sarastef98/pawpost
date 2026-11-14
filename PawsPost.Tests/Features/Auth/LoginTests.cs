using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PawsPost.Features.Auth;
using PawsPost.Tests.Infrastructure;

namespace PawsPost.Tests.Features.Auth;

public class LoginTests
{
    [Fact]
    public async Task Login_ValidCredentials_Returns200AndSetsAuthCookie()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        await client.RegisterAsync("a@x.com");

        // Act
        var response = await client.LoginAsync("a@x.com");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal("a@x.com", body?.Email);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("pawspost.auth=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Login_ValidCredentials_CookieIsHttpOnlySecureAndSameSite()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        await client.RegisterAsync("a@x.com");

        // Act
        var response = await client.LoginAsync("a@x.com");

        // Assert
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("pawspost.auth=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_EmailDifferentCase_Succeeds()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        await client.RegisterAsync("a@x.com");

        // Act
        var response = await client.LoginAsync("A@X.COM");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(null, AuthTestHelpers.ValidPassword)]
    [InlineData("a@x.com", null)]
    [InlineData("", "")]
    public async Task Login_MissingFields_Returns400(string? email, string? password)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_EmptyBody_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new { });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_EmailTooLong_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        var email = new string('a', 250) + "@x.com";

        // Act
        var response = await client.LoginAsync(email);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.True(body?.Errors.ContainsKey(nameof(LoginRequest.Email)));
    }

    [Fact]
    public async Task Login_PasswordTooLong_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        var password = "Aa1!" + new string('x', 125);

        // Act
        var response = await client.LoginAsync("a@x.com", password);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.True(body?.Errors.ContainsKey(nameof(LoginRequest.Password)));
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        await client.RegisterAsync("a@x.com");

        // Act
        var response = await client.LoginAsync("a@x.com", "Wrong1234!");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.LoginAsync("nobody@x.com");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownEmail_ReturnSameBody()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        await client.RegisterAsync("a@x.com");

        // Act
        var wrongPassword = await client.LoginAsync("a@x.com", "Wrong1234!");
        var unknownEmail = await client.LoginAsync("nobody@x.com");

        // Assert
        Assert.Equal(wrongPassword.StatusCode, unknownEmail.StatusCode);
        Assert.Equal("application/problem+json", wrongPassword.Content.Headers.ContentType?.MediaType);
        var wrongBody = await wrongPassword.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        var unknownBody = await unknownEmail.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        Assert.Equal(wrongBody?.Title, unknownBody?.Title);
        Assert.Equal(wrongBody?.Detail, unknownBody?.Detail);
        Assert.Equal(wrongBody?.Status, unknownBody?.Status);
    }

    [Fact]
    public async Task Login_AfterTooManyFailures_Returns401EvenWithCorrectPassword()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        await client.RegisterAsync("a@x.com");
        for (var i = 0; i < 5; i++)
        {
            await client.LoginAsync("a@x.com", "Wrong1234!");
        }

        // Act
        var response = await client.LoginAsync("a@x.com");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }
}
