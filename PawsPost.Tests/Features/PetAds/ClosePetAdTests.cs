using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PawsPost.Data;
using PawsPost.Tests.Infrastructure;
using static PawsPost.Tests.Infrastructure.AssistantTestHelpers;
using static PawsPost.Tests.Infrastructure.PetAdTestHelpers;

namespace PawsPost.Tests.Features.PetAds;

public class ClosePetAdTests
{
    private const string OwnerEmail = "owner@x.com";
    private const string OtherEmail = "other@x.com";

    // Success

    [Fact]
    public async Task ClosePetAd_OwnerClosesActiveAd_Returns204WithNoBody()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (client, _, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail);

        // Act
        var response = await client.ClosePetAdAsync(ad.Id.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ClosePetAd_OwnerClosesActiveAd_StoresStatusClosed()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (client, _, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail);

        // Act
        await client.ClosePetAdAsync(ad.Id.ToString());

        // Assert
        var stored = await factory.GetStoredAdAsync(ad.Id);
        Assert.Equal(PetAdStatus.Closed, stored!.Status);
    }

    [Fact]
    public async Task ClosePetAd_OwnerClosesActiveAd_KeepsAdInDatabase()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (client, ownerId, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail);

        // Act
        await client.ClosePetAdAsync(ad.Id.ToString());

        // Assert
        var stored = await factory.GetStoredAdAsync(ad.Id);
        Assert.NotNull(stored);
        Assert.Equal(ad.Title, stored.Title);
        Assert.Equal(ad.Type, stored.Type);
        Assert.Equal(ad.Species, stored.Species);
        Assert.Equal(ad.Breed, stored.Breed);
        Assert.Equal(ad.AgeInMonths, stored.AgeInMonths);
        Assert.Equal(ad.Description, stored.Description);
        Assert.Equal(ad.Price, stored.Price);
        Assert.Equal(ad.Location, stored.Location);
        Assert.Equal(ad.CreatedAt, stored.CreatedAt);
        Assert.Equal(ownerId, stored.OwnerId);
        Assert.Single(await factory.GetStoredAdsAsync());
    }

    [Fact]
    public async Task ClosePetAd_OwnerClosesActiveAd_RemovesVectorFromSearchIndex()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync(OwnerEmail);
        var ad = BuildSearchAd(await client.GetMyIdAsync(), "Calm dog");
        await factory.SeedAndIndexAdsAsync(ad);
        Assert.True(factory.VectorStore.Contains(ad.Id));

        // Act
        await client.ClosePetAdAsync(ad.Id.ToString());

        // Assert
        Assert.False(factory.VectorStore.Contains(ad.Id));
        Assert.Equal([ad.Id], factory.VectorStore.RemovedIds);
    }

    // Validation

    [Theory]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("not-a-guid")]
    public async Task ClosePetAd_IdNotAGuid_Returns400WithOnlyThatField(string id)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync(OwnerEmail);

        // Act
        var response = await client.ClosePetAdAsync(id);

        // Assert
        await AssertBadRequestForOnlyAsync(response, "id");
    }

    // Authorization

    [Fact]
    public async Task ClosePetAd_NotLoggedIn_Returns401ProblemDetails()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (_, _, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail);
        var anonymous = factory.CreateApiClient();

        // Act
        var response = await anonymous.ClosePetAdAsync(ad.Id.ToString());

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
        Assert.Equal(PetAdStatus.Active, (await factory.GetStoredAdAsync(ad.Id))!.Status);
    }

    [Fact]
    public async Task ClosePetAd_NotLoggedInAndIdNotAGuid_Returns401()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var anonymous = factory.CreateApiClient();

        // Act
        var response = await anonymous.ClosePetAdAsync("not-a-guid");

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ClosePetAd_TamperedCookie_Returns401()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (_, _, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail);
        var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{PetAdsUrl}/{ad.Id}/close");
        request.Headers.Add("Cookie", "pawspost.auth=tampered-value-that-is-not-valid");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
        Assert.Equal(PetAdStatus.Active, (await factory.GetStoredAdAsync(ad.Id))!.Status);
    }

    [Fact]
    public async Task ClosePetAd_NotOwner_Returns403ProblemDetails()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (_, _, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail);
        var other = await factory.CreateLoggedInClientAsync(OtherEmail);

        // Act
        var response = await other.ClosePetAdAsync(ad.Id.ToString());

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ClosePetAd_NotOwner_LeavesAdActive()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var owner = await factory.CreateLoggedInClientAsync(OwnerEmail);
        var ad = BuildSearchAd(await owner.GetMyIdAsync(), "Calm dog");
        await factory.SeedAndIndexAdsAsync(ad);
        var other = await factory.CreateLoggedInClientAsync(OtherEmail);

        // Act
        await other.ClosePetAdAsync(ad.Id.ToString());

        // Assert
        Assert.Equal(PetAdStatus.Active, (await factory.GetStoredAdAsync(ad.Id))!.Status);
        Assert.True(factory.VectorStore.Contains(ad.Id));
        Assert.Empty(factory.VectorStore.RemovedIds);
    }

    [Fact]
    public async Task ClosePetAd_NotOwnerOnClosedAd_Returns403Not409()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (_, _, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail, PetAdStatus.Closed);
        var other = await factory.CreateLoggedInClientAsync(OtherEmail);

        // Act
        var response = await other.ClosePetAdAsync(ad.Id.ToString());

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Forbidden);
    }

    // Not found and conflict

    [Fact]
    public async Task ClosePetAd_UnknownId_Returns404ProblemDetails()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync(OwnerEmail);

        // Act
        var response = await client.ClosePetAdAsync(Guid.NewGuid().ToString());

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ClosePetAd_OwnerOnAlreadyClosedAd_Returns409ProblemDetails()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (client, _, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail, PetAdStatus.Closed);

        // Act
        var response = await client.ClosePetAdAsync(ad.Id.ToString());

        // Assert
        var problem = await AssertProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Equal("Ad cannot be closed", problem.Title);
    }

    [Fact]
    public async Task ClosePetAd_OwnerOnAlreadyClosedAd_LeavesAdUnchanged()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (client, ownerId, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail, PetAdStatus.Closed);

        // Act
        await client.ClosePetAdAsync(ad.Id.ToString());

        // Assert
        var stored = await factory.GetStoredAdAsync(ad.Id);
        Assert.Equal(PetAdStatus.Closed, stored!.Status);
        Assert.Equal(ad.Title, stored.Title);
        Assert.Equal(ownerId, stored.OwnerId);
        Assert.Empty(factory.VectorStore.RemovedIds);
    }

    [Theory]
    [InlineData(PetAdStatus.Draft)]
    [InlineData(PetAdStatus.Reserved)]
    public async Task ClosePetAd_OwnerOnNonActiveAd_Returns409(PetAdStatus status)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (client, _, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail, status);

        // Act
        var response = await client.ClosePetAdAsync(ad.Id.ToString());

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Equal(status, (await factory.GetStoredAdAsync(ad.Id))!.Status);
    }

    [Fact]
    public async Task ClosePetAd_SecondCloseBySameOwner_Returns409()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (client, _, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail);
        var first = await client.ClosePetAdAsync(ad.Id.ToString());

        // Act
        var second = await client.ClosePetAdAsync(ad.Id.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        await AssertProblemAsync(second, HttpStatusCode.Conflict);
    }

    // Error body shape: 403 and 409 have the same ProblemDetails fields as the other errors (type, title, status, traceId).

    [Fact]
    public async Task ClosePetAd_NotOwner_ReturnsStandardProblemShape()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (_, _, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail);
        var other = await factory.CreateLoggedInClientAsync(OtherEmail);

        // Act
        var response = await other.ClosePetAdAsync(ad.Id.ToString());

        // Assert
        var problem = await AssertStandardProblemShapeAsync(response, HttpStatusCode.Forbidden);
        Assert.Equal("Not the owner of this ad", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task ClosePetAd_OwnerOnAlreadyClosedAd_ReturnsStandardProblemShape()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (client, _, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail, PetAdStatus.Closed);

        // Act
        var response = await client.ClosePetAdAsync(ad.Id.ToString());

        // Assert
        var problem = await AssertStandardProblemShapeAsync(response, HttpStatusCode.Conflict);
        Assert.Equal("Ad cannot be closed", problem.GetProperty("title").GetString());
        Assert.Equal("Only Active ads can be closed.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ClosePetAd_UnknownId_ReturnsStandardProblemShape()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = await factory.CreateLoggedInClientAsync(OwnerEmail);

        // Act
        var response = await client.ClosePetAdAsync(Guid.NewGuid().ToString());

        // Assert
        await AssertStandardProblemShapeAsync(response, HttpStatusCode.NotFound);
    }

    // Search index failure and logging

    [Fact]
    public async Task ClosePetAd_IndexRemovalFails_StillReturns204()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (client, _, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail);
        factory.VectorStore.RemoveExceptionToThrow = new InvalidOperationException("storage down");

        // Act
        var response = await client.ClosePetAdAsync(ad.Id.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ClosePetAd_IndexRemovalFails_AdStaysClosed()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var (client, _, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail);
        factory.VectorStore.RemoveExceptionToThrow = new InvalidOperationException("storage down");

        // Act
        await client.ClosePetAdAsync(ad.Id.ToString());

        // Assert
        Assert.Equal(PetAdStatus.Closed, (await factory.GetStoredAdAsync(ad.Id))!.Status);
    }

    [Fact]
    public async Task ClosePetAd_IndexRemovalFails_ResponseHasNoErrorDetails()
    {
        // Arrange
        const string secretMessage = "Secret storage detail 7c1d";
        using var factory = new PawsPostFactory();
        var (client, _, ad) = await factory.CreateLoggedInOwnerWithAdAsync(OwnerEmail);
        factory.VectorStore.RemoveExceptionToThrow = new InvalidOperationException(secretMessage);

        // Act
        var response = await client.ClosePetAdAsync(ad.Id.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(secretMessage, response.Headers.ToString());
    }

    [Fact]
    public async Task ClosePetAd_IndexRemovalFails_LogsWarningWithAdIdOnly()
    {
        // Arrange
        const string secretMessage = "Secret storage detail 7c1d";
        const string displayName = "Privatename";
        var provider = new CapturingLoggerProvider();
        using var factory = new PawsPostFactory();
        using var loggedFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(provider)));
        var (client, ownerId, ad) = await loggedFactory.CreateLoggedInOwnerWithAdAsync(
            "owner.private@example.com", displayName: displayName);
        factory.VectorStore.RemoveExceptionToThrow = new InvalidOperationException(secretMessage);

        // Act
        await client.ClosePetAdAsync(ad.Id.ToString());

        // Assert
        var warning = Assert.Single(
            provider.Messages,
            message => message.StartsWith("Warning|") && message.Contains(ad.Id.ToString()));
        Assert.Contains(nameof(InvalidOperationException), warning);
        AssertNoPersonalData(provider, ad, ownerId, displayName, secretMessage);
    }

    [Fact]
    public async Task ClosePetAd_Success_LogsNoPersonalData()
    {
        // Arrange
        const string displayName = "Privatename";
        var provider = new CapturingLoggerProvider();
        using var factory = new PawsPostFactory();
        using var loggedFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(provider)));
        var (client, ownerId, ad) = await loggedFactory.CreateLoggedInOwnerWithAdAsync(
            "owner.private@example.com", displayName: displayName);

        // Act
        var response = await client.ClosePetAdAsync(ad.Id.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        AssertNoPersonalData(provider, ad, ownerId, displayName, "no-secret");
    }

    private static void AssertNoPersonalData(
        CapturingLoggerProvider provider, PetAd ad, Guid ownerId, string displayName, string secretMessage)
    {
        Assert.All(provider.Messages, message =>
        {
            Assert.DoesNotContain(secretMessage, message);
            Assert.DoesNotContain("owner.private@example.com", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(displayName, message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(ownerId.ToString(), message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(ad.Title, message);
            Assert.DoesNotContain(ad.Description, message);
        });
    }

    // The shape every error body in the app has: a type link, a title, the status and a traceId.
    private static async Task<JsonElement> AssertStandardProblemShapeAsync(
        HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.StartsWith("https://", problem.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.Equal((int)expected, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        return problem;
    }

    private static async Task<ProblemDetails> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal((int)expected, problem.Status);
        return problem;
    }
}
