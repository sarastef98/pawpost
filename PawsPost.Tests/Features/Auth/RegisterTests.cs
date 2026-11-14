using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using PawsPost.Data;
using PawsPost.Features.Auth;
using PawsPost.Tests.Infrastructure;

namespace PawsPost.Tests.Features.Auth;

public class RegisterTests
{
    private const string ValidPassword = "Passw0rd!";

    [Fact]
    public async Task Register_ValidRequest_Returns201WithIdAndEmail()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = "a@x.com", password = ValidPassword });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.Equal("a@x.com", body.Email);
    }

    [Fact]
    public async Task Register_ValidRequest_StoresHashedPasswordNotPlainText()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        await client.PostAsJsonAsync("/api/auth/register", new { email = "a@x.com", password = ValidPassword });

        // Assert
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = db.Users.Single();
        Assert.False(string.IsNullOrEmpty(user.PasswordHash));
        Assert.NotEqual(ValidPassword, user.PasswordHash);
    }

    [Fact]
    public async Task Register_ValidRequest_ResponseDoesNotContainPasswordOrHash()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = "a@x.com", password = ValidPassword });

        // Assert
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(ValidPassword, text);
        Assert.DoesNotContain("hash", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_EmailDifferentCase_TreatedAsSameEmail()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        await client.PostAsJsonAsync("/api/auth/register", new { email = "A@x.com", password = ValidPassword });

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = "a@x.com", password = ValidPassword });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_PasswordExactlyEightChars_Returns201()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = "a@x.com", password = "Abcdef1!" });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_EmailAlreadyRegistered_Returns409ProblemDetails()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        await client.PostAsJsonAsync("/api/auth/register", new { email = "a@x.com", password = ValidPassword });

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = "a@x.com", password = ValidPassword });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(null, ValidPassword)]
    [InlineData("", ValidPassword)]
    [InlineData("not-an-email", ValidPassword)]
    [InlineData("   ", ValidPassword)]
    [InlineData("Name <a@x.com>", ValidPassword)]
    [InlineData("a@x.com", null)]
    [InlineData("a@x.com", "Abcde1!")]
    [InlineData("a@x.com", "passw0rd!")]
    [InlineData("a@x.com", "PASSW0RD!")]
    [InlineData("a@x.com", "Password!")]
    [InlineData("a@x.com", "Passw0rd1")]
    public async Task Register_InvalidInput_Returns400ProblemDetails(string? email, string? password)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("passw0rd!")]
    [InlineData("PASSW0RD!")]
    [InlineData("Password!")]
    [InlineData("Passw0rd1")]
    public async Task Register_PasswordFailsIdentityRule_ReturnsErrorUnderPasswordField(string password)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = "a@x.com", password });

        // Assert
        var body = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>();
        Assert.NotNull(body);
        Assert.Equal([nameof(RegisterRequest.Password)], body.Errors.Keys);
    }

    [Fact]
    public async Task Register_EmailTooLong_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        var email = new string('a', 250) + "@x.com";

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password = ValidPassword });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_PasswordTooLong_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        var password = "Aa1!" + new string('x', 125);

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = "a@x.com", password });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_EmailWithSurroundingWhitespace_TrimsAndReturns201()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = "  a@x.com  ", password = ValidPassword });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal("a@x.com", body?.Email);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("a@x.com", db.Users.Single().Email);
    }

    [Fact]
    public async Task Register_NonJsonContentType_Returns415()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        using var content = new StringContent("email=a@x.com&password=Passw0rd!", Encoding.UTF8, "application/x-www-form-urlencoded");

        // Act
        var response = await client.PostAsync("/api/auth/register", content);

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }
}
