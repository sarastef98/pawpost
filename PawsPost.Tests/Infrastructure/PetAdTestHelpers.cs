using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PawsPost.Data;
using PawsPost.Features.Auth;

namespace PawsPost.Tests.Infrastructure;

public static class PetAdTestHelpers
{
    public const string PetAdsUrl = "/api/pet-ads";

    /// <summary>A valid Adoption request body. Pass overrides to change or add fields (null removes nothing, it sends null).</summary>
    public static Dictionary<string, object?> ValidAdoptionBody(params (string Key, object? Value)[] overrides)
    {
        var body = new Dictionary<string, object?>
        {
            ["title"] = "Friendly beagle looking for a home",
            ["type"] = "Adoption",
            ["species"] = "Dog",
            ["breed"] = "Beagle",
            ["ageInMonths"] = 24,
            ["description"] = "Friendly and healthy.",
            ["location"] = "Skopje"
        };
        return Apply(body, overrides);
    }

    /// <summary>A valid Sale request body.</summary>
    public static Dictionary<string, object?> ValidSaleBody(params (string Key, object? Value)[] overrides)
    {
        var body = ValidAdoptionBody();
        body["type"] = "Sale";
        body["price"] = 150.50m;
        return Apply(body, overrides);
    }

    public static Dictionary<string, object?> Without(this Dictionary<string, object?> body, string key)
    {
        body.Remove(key);
        return body;
    }

    /// <summary>Registers the user, logs in and returns the logged-in client (own cookie jar).</summary>
    public static async Task<HttpClient> CreateLoggedInClientAsync(
        this WebApplicationFactory<Program> factory, string email, string? displayName = null)
    {
        var client = factory.CreateApiClient();
        await client.RegisterAsync(email, displayName: displayName);
        var login = await client.LoginAsync(email);
        login.EnsureSuccessStatusCode();
        return client;
    }

    public static async Task<Guid> GetMyIdAsync(this HttpClient client)
    {
        var me = await client.GetFromJsonAsync<UserResponse>("/api/auth/me");
        return me!.Id;
    }

    public static Task<HttpResponseMessage> PostPetAdAsync(this HttpClient client, object body)
    {
        return client.PostAsJsonAsync(PetAdsUrl, body);
    }

    public static async Task<List<PetAd>> GetStoredAdsAsync(this WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PetAds.AsNoTracking().ToListAsync();
    }

    public static Task<HttpResponseMessage> GetPetAdsAsync(this HttpClient client, string? query = null)
    {
        return client.GetAsync(string.IsNullOrEmpty(query) ? PetAdsUrl : $"{PetAdsUrl}?{query}");
    }

    public static Task<HttpResponseMessage> GetPetAdAsync(this HttpClient client, string id)
    {
        return client.GetAsync($"{PetAdsUrl}/{id}");
    }

    /// <summary>A PetAd with fixed values. Pass the owner id of a seeded user.</summary>
    public static PetAd BuildPetAd(
        Guid ownerId,
        DateTime createdAt,
        string title = "Friendly beagle",
        PetAdType type = PetAdType.Adoption,
        PetSpecies species = PetSpecies.Dog,
        decimal? price = null,
        string? location = "Skopje",
        PetAdStatus status = PetAdStatus.Active,
        Guid? id = null)
    {
        return new PetAd
        {
            Id = id ?? Guid.NewGuid(),
            Title = title,
            Type = type,
            Species = species,
            Breed = "Beagle",
            AgeInMonths = 12,
            Description = "Friendly and healthy.",
            Price = price,
            Location = location,
            Status = status,
            OwnerId = ownerId,
            CreatedAt = createdAt
        };
    }

    /// <summary>Registers a user through the API and returns its id (the ads table needs a real owner).</summary>
    public static async Task<Guid> SeedOwnerAsync(
        this WebApplicationFactory<Program> factory, string email, string? displayName = null)
    {
        using var client = factory.CreateApiClient();
        await client.RegisterAsync(email, displayName: displayName);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(user => user.Email == email);
        return user.Id;
    }

    /// <summary>Inserts ads straight into the database (the API can only create Active ads dated now).</summary>
    public static async Task SeedAdsAsync(this WebApplicationFactory<Program> factory, params PetAd[] ads)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.PetAds.AddRange(ads);
        await db.SaveChangesAsync();
    }

    public static async Task AssertBadRequestForOnlyAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        var error = Assert.Single(problem.Errors);
        Assert.Equal(field, error.Key.TrimStart('$', '.'), ignoreCase: true);
        Assert.Single(error.Value);
    }

    private static Dictionary<string, object?> Apply(Dictionary<string, object?> body, (string Key, object? Value)[] overrides)
    {
        foreach (var (key, value) in overrides)
        {
            body[key] = value;
        }
        return body;
    }
}
