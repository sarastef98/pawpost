using System.Net.Http.Json;
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
    public static async Task<HttpClient> CreateLoggedInClientAsync(this WebApplicationFactory<Program> factory, string email)
    {
        var client = factory.CreateApiClient();
        await client.RegisterAsync(email);
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

    private static Dictionary<string, object?> Apply(Dictionary<string, object?> body, (string Key, object? Value)[] overrides)
    {
        foreach (var (key, value) in overrides)
        {
            body[key] = value;
        }
        return body;
    }
}
