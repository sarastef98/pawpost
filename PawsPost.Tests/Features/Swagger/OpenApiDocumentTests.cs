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
    public async Task GetOpenApiDocument_PetAdsPostBody_ResolvesToSchemaWithExpectedFields()
    {
        // Arrange
        using var document = await GetOpenApiDocumentAsync();
        var bodySchema = GetPetAdsPostBody(document).GetProperty("content").GetProperty("application/json").GetProperty("schema");

        // Act: follow the $ref from the POST body, the same way Swagger UI does.
        var schemaName = bodySchema.GetProperty("$ref").GetString()!.Split('/').Last();
        var properties = document.RootElement
            .GetProperty("components").GetProperty("schemas")
            .GetProperty(schemaName).GetProperty("properties");
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
        using var client = developmentFactory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static JsonElement GetPetAdsPostBody(JsonDocument document) =>
        document.RootElement.GetProperty("paths").GetProperty("/api/pet-ads").GetProperty("post").GetProperty("requestBody");
}
