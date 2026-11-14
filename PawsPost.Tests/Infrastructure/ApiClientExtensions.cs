using Microsoft.AspNetCore.Mvc.Testing;

namespace PawsPost.Tests.Infrastructure;

public static class ApiClientExtensions
{
    /// <summary>
    /// Creates a client with an https base address (the auth cookie is Secure) and no automatic redirects.
    /// Works for <see cref="PawsPostFactory"/> and for factories derived from it with WithWebHostBuilder.
    /// </summary>
    public static HttpClient CreateApiClient(this WebApplicationFactory<Program> factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }
}
