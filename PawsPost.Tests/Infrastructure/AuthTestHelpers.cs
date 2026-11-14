using System.Net.Http.Json;

namespace PawsPost.Tests.Infrastructure;

public static class AuthTestHelpers
{
    public const string ValidPassword = "Passw0rd!";

    public static async Task RegisterAsync(this HttpClient client, string email, string password = ValidPassword)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        response.EnsureSuccessStatusCode();
    }

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password = ValidPassword)
    {
        return client.PostAsJsonAsync("/api/auth/login", new { email, password });
    }
}
