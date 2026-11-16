using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PawsPost.Data;
using PawsPost.Tests.Infrastructure;
using static PawsPost.Tests.Infrastructure.PetAdTestHelpers;

namespace PawsPost.Tests.Features.Auth;

public class RegisterDisplayNameTests
{
    private static Task<HttpResponseMessage> PostRegisterAsync(HttpClient client, string email, object? displayName)
    {
        return client.PostAsJsonAsync(
            "/api/auth/register", new { email, password = AuthTestHelpers.ValidPassword, displayName });
    }

    private static async Task<List<User>> GetUsersAsync(PawsPostFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.AsNoTracking().ToListAsync();
    }

    // Success

    [Fact]
    public async Task Register_ValidDisplayName_Returns201()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await PostRegisterAsync(client, "a@x.com", "Anna");

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_ValidDisplayName_StoresDisplayName()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        await PostRegisterAsync(client, "a@x.com", "Anna K");

        // Assert
        var user = Assert.Single(await GetUsersAsync(factory));
        Assert.Equal("Anna K", user.DisplayName);
        Assert.Equal("ANNA K", user.NormalizedDisplayName);
    }

    [Fact]
    public async Task Register_DisplayNameWithSurroundingSpaces_StoresTrimmedName()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await PostRegisterAsync(client, "a@x.com", "  Anna  ");

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = Assert.Single(await GetUsersAsync(factory));
        Assert.Equal("Anna", user.DisplayName);
        Assert.Equal("ANNA", user.NormalizedDisplayName);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("Anna2")]
    [InlineData("Anna K")]
    [InlineData("anna-k")]
    [InlineData("anna_k")]
    [InlineData("123")]
    [InlineData("123456789012345678901234567890")]
    public async Task Register_DisplayNameWithAllowedCharacters_Returns201(string displayName)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await PostRegisterAsync(client, "a@x.com", displayName);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = Assert.Single(await GetUsersAsync(factory));
        Assert.Equal(displayName, user.DisplayName);
    }

    [Theory]
    [InlineData("Ана")]
    [InlineData("Мария 2")]
    public async Task Register_DisplayNameWithCyrillicLetters_Returns201(string displayName)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await PostRegisterAsync(client, "a@x.com", displayName);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = Assert.Single(await GetUsersAsync(factory));
        Assert.Equal(displayName, user.DisplayName);
    }

    [Fact]
    public async Task Register_DisplayNameWithNonLatinDigits_Returns201()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await PostRegisterAsync(client, "a@x.com", "Ana ٣");

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_DisplayNameWithSingleSpacesBetweenWords_Returns201()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await PostRegisterAsync(client, "a@x.com", "Pet lover 1");

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_DisplayNameCyrillic_StoresUpperCaseNormalizedName()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        await PostRegisterAsync(client, "a@x.com", "Ана");

        // Assert
        var user = Assert.Single(await GetUsersAsync(factory));
        Assert.Equal("Ана", user.DisplayName);
        Assert.Equal("АНА", user.NormalizedDisplayName);
    }

    // Validation failure

    [Fact]
    public async Task Register_DisplayNameMissing_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/auth/register", new { email = "a@x.com", password = AuthTestHelpers.ValidPassword });

        // Assert
        await AssertBadRequestForOnlyAsync(response, "DisplayName");
    }

    [Fact]
    public async Task Register_DisplayNameEmpty_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await PostRegisterAsync(client, "a@x.com", "");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "DisplayName");
    }

    [Fact]
    public async Task Register_DisplayNameOnlySpaces_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await PostRegisterAsync(client, "a@x.com", "     ");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "DisplayName");
    }

    [Fact]
    public async Task Register_DisplayNameTooShortAfterTrim_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await PostRegisterAsync(client, "a@x.com", "  ab  ");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "DisplayName");
    }

    [Fact]
    public async Task Register_DisplayNameTooLong_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await PostRegisterAsync(client, "a@x.com", new string('a', 31));

        // Assert
        await AssertBadRequestForOnlyAsync(response, "DisplayName");
    }

    [Theory]
    [InlineData("Anna@home")]
    [InlineData("Anna.K")]
    [InlineData("Anna!")]
    [InlineData("<Anna>")]
    [InlineData("Anna \U0001F436")]
    public async Task Register_DisplayNameWithDisallowedCharacters_Returns400(string displayName)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await PostRegisterAsync(client, "a@x.com", displayName);

        // Assert
        await AssertBadRequestForOnlyAsync(response, "DisplayName");
    }

    [Theory]
    [InlineData("Pet  lover")]
    [InlineData("Ана  Мария")]
    public async Task Register_DisplayNameWithTwoSpacesInARow_Returns400(string displayName)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await PostRegisterAsync(client, "a@x.com", displayName);

        // Assert
        await AssertBadRequestForOnlyAsync(response, "DisplayName");
    }

    [Fact]
    public async Task Register_DisplayNameWithTabInside_Returns400()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await PostRegisterAsync(client, "a@x.com", "Pet\tlover");

        // Assert
        await AssertBadRequestForOnlyAsync(response, "DisplayName");
    }

    [Fact]
    public async Task Register_InvalidDisplayName_StoresNoUser()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        await PostRegisterAsync(client, "a@x.com", "ab");

        // Assert
        Assert.Empty(await GetUsersAsync(factory));
    }

    // Conflict

    [Fact]
    public async Task Register_DisplayNameTaken_Returns409()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        await PostRegisterAsync(client, "a@x.com", "Anna");

        // Act
        var response = await PostRegisterAsync(client, "b@x.com", "Anna");

        // Assert
        await AssertDisplayNameConflictAsync(response);
    }

    [Fact]
    public async Task Register_DisplayNameTakenWithDifferentCase_Returns409()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        await PostRegisterAsync(client, "a@x.com", "Anna");

        // Act
        var response = await PostRegisterAsync(client, "b@x.com", "anna");

        // Assert
        await AssertDisplayNameConflictAsync(response);
    }

    [Fact]
    public async Task Register_CyrillicDisplayNameTakenWithDifferentCase_Returns409()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        await PostRegisterAsync(client, "a@x.com", "Ана");

        // Act
        var response = await PostRegisterAsync(client, "b@x.com", "АНА");

        // Assert
        await AssertDisplayNameConflictAsync(response);
    }

    [Fact]
    public async Task Register_DisplayNameTakenWithSurroundingSpaces_Returns409()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        await PostRegisterAsync(client, "a@x.com", "Anna");

        // Act
        var response = await PostRegisterAsync(client, "b@x.com", "  Anna  ");

        // Assert
        await AssertDisplayNameConflictAsync(response);
    }

    [Fact]
    public async Task Register_DisplayNameTaken_StoresNoSecondUser()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        await PostRegisterAsync(client, "a@x.com", "Anna");

        // Act
        await PostRegisterAsync(client, "b@x.com", "Anna");

        // Assert
        var user = Assert.Single(await GetUsersAsync(factory));
        Assert.Equal("a@x.com", user.Email);
    }

    // Look-alike names (no confusable detection, plan decision D2)

    [Fact]
    public async Task Register_LookAlikeNameFromOtherAlphabet_Returns201()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();
        await PostRegisterAsync(client, "a@x.com", "Anna");

        // Act: the first letter is a Cyrillic "А"
        var response = await PostRegisterAsync(client, "b@x.com", "Аnna");

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(2, (await GetUsersAsync(factory)).Count);
    }

    // Privacy

    [Fact]
    public async Task Register_WithDisplayName_LogsNoEmailOrDisplayName()
    {
        // Arrange
        const string email = "private.person@example.com";
        const string displayName = "Secret Display Name";
        var provider = new CapturingLoggerProvider();
        using var factory = new PawsPostFactory();
        using var loggedFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(provider)));
        var client = loggedFactory.CreateApiClient();

        // Act
        await PostRegisterAsync(client, email, displayName);
        await PostRegisterAsync(client, "other@example.com", displayName);

        // Assert
        Assert.NotEmpty(provider.Messages);
        Assert.All(provider.Messages, message =>
        {
            Assert.DoesNotContain(email, message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(displayName, message, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static async Task AssertDisplayNameConflictAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(409, problem.Status);
        Assert.Equal("Display name already taken", problem.Title);
    }
}
