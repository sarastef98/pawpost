using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace PawsPost.Tests.Infrastructure;

public static class AuthTestHelpers
{
    public const string ValidPassword = "Passw0rd!";

    /// <summary>A valid display name derived from the email: deterministic and unique per email.</summary>
    public static string DisplayNameFor(string email)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.ToUpperInvariant())));
        return "u" + hash[..12].ToLowerInvariant();
    }

    public static async Task RegisterAsync(
        this HttpClient client, string email, string password = ValidPassword, string? displayName = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register", new { email, password, displayName = displayName ?? DisplayNameFor(email) });
        response.EnsureSuccessStatusCode();
    }

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password = ValidPassword)
    {
        return client.PostAsJsonAsync("/api/auth/login", new { email, password });
    }
}
