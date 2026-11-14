using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using PawsPost.Tests.Infrastructure;

namespace PawsPost.Tests.Features.Swagger;

public class OpenApiDocumentTests(PawsPostFactory factory) : IClassFixture<PawsPostFactory>
{
    [Fact]
    public async Task GetOpenApiDocument_PetAdsPost_BodyReferencesCreatePetAdRequest()
    {
        // Arrange
        using var document = await GetOpenApiDocumentAsync();

        // Act
        var schema = GetPetAdsPostBody(document).GetProperty("content").GetProperty("application/json").GetProperty("schema");

        // Assert
        Assert.Equal("#/components/schemas/CreatePetAdRequest", schema.GetProperty("$ref").GetString());
        Assert.False(schema.TryGetProperty("oneOf", out _));
    }

    [Fact]
    public async Task GetOpenApiDocument_PetAdsPost_BodyIsRequired()
    {
        // Arrange
        using var document = await GetOpenApiDocumentAsync();

        // Act
        var body = GetPetAdsPostBody(document);

        // Assert
        Assert.True(body.GetProperty("required").GetBoolean());
    }

    [Fact]
    public async Task GetOpenApiDocument_CreatePetAdRequest_ListsExpectedFields()
    {
        // Arrange
        using var document = await GetOpenApiDocumentAsync();

        // Act
        var properties = document.RootElement
            .GetProperty("components").GetProperty("schemas")
            .GetProperty("CreatePetAdRequest").GetProperty("properties");
        var names = properties.EnumerateObject().Select(property => property.Name).ToList();

        // Assert
        Assert.Contains("type", names);
        Assert.Contains("species", names);
        Assert.Contains("ageInMonths", names);
        Assert.Contains("description", names);
    }

    private async Task<JsonDocument> GetOpenApiDocumentAsync()
    {
        using var developmentFactory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        var client = developmentFactory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    private static JsonElement GetPetAdsPostBody(JsonDocument document) =>
        document.RootElement.GetProperty("paths").GetProperty("/api/pet-ads").GetProperty("post").GetProperty("requestBody");
}
