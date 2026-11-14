using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PawsPost.Tests.Infrastructure;

namespace PawsPost.Tests.Features.Auth;

public class AuthOptionsTests
{
    [Fact]
    public void Startup_CookieLifetimeConfigured_AppliesToAuthCookie()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        using var configured = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Auth:CookieLifetime", "3.00:00:00"));

        // Act
        var cookieOptions = configured.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        // Assert
        Assert.Equal(TimeSpan.FromDays(3), cookieOptions.ExpireTimeSpan);
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-1.00:00:00")]
    public void Startup_CookieLifetimeNotPositive_FailsToStart(string lifetime)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        using var configured = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Auth:CookieLifetime", lifetime));

        // Act
        var exception = Record.Exception(() => configured.CreateClient());

        // Assert
        var validation = Assert.IsType<OptionsValidationException>(exception);
        Assert.Equal(typeof(PawsPost.Features.Auth.AuthOptions), validation.OptionsType);
    }
}
