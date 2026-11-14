using System.Net;
using Microsoft.AspNetCore.Hosting;
using PawsPost.Tests.Infrastructure;

namespace PawsPost.Tests.Features.Swagger;

public class SwaggerUiTests(PawsPostFactory factory) : IClassFixture<PawsPostFactory>
{
    [Fact]
    public async Task GetSwaggerUi_InDevelopment_ReturnsHtmlPage()
    {
        // Arrange
        using var developmentFactory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        var client = developmentFactory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/index.html");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetSwaggerUi_InProduction_ReturnsNotFound()
    {
        // Arrange
        using var productionFactory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        var client = productionFactory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/index.html");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOpenApiDocument_InDevelopment_ListsAuthPaths()
    {
        // Arrange
        using var developmentFactory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        var client = developmentFactory.CreateClient();

        // Act
        var response = await client.GetAsync("/openapi/v1.json");
        var document = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"/api/auth/register\"", document);
        Assert.Contains("\"/api/auth/login\"", document);
    }
}
