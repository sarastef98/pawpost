using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PawsPost.Features.Ai;
using PawsPost.Tests.Infrastructure;

namespace PawsPost.Tests.Features.Ai;

public class AiOptionsTests
{
    [Fact]
    public void Startup_ProviderConfigured_BindsAiOptions()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        using var configured = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Ai:Provider", AiOptions.GeminiProvider));

        // Act
        var options = configured.Services.GetRequiredService<IOptions<AiOptions>>().Value;

        // Assert
        Assert.Equal(AiOptions.GeminiProvider, options.Provider);
    }

    [Theory]
    [InlineData("OpenAi")]
    [InlineData("")]
    public void Startup_UnknownProvider_FailsToStart(string provider)
    {
        // Arrange
        using var factory = new PawsPostFactory();
        using var configured = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Ai:Provider", provider));

        // Act
        var exception = Record.Exception(() => configured.CreateClient());

        // Assert
        var validation = Assert.IsType<OptionsValidationException>(exception);
        Assert.Equal(typeof(AiOptions), validation.OptionsType);
    }
}
