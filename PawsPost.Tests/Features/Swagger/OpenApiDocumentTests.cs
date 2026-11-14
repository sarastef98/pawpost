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

    [Fact]
    public async Task GetOpenApiDocument_CreatePetAdRequestSchema_TitleIsRequired()
    {
        // Arrange
        using var document = await GetOpenApiDocumentAsync();

        // Act
        var schema = GetSchema(document, "CreatePetAdRequest");

        // Assert
        Assert.True(schema.GetProperty("properties").TryGetProperty("title", out _));
        Assert.Contains("title", GetRequiredNames(schema));
    }

    [Fact]
    public async Task GetOpenApiDocument_PetAdResponseSchema_TitleIsRequired()
    {
        // Arrange
        using var document = await GetOpenApiDocumentAsync();

        // Act
        var created = document.RootElement.GetProperty("paths").GetProperty("/api/pet-ads").GetProperty("post")
            .GetProperty("responses").GetProperty("201").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var schema = GetSchema(document, "PetAdResponse");

        // Assert
        Assert.Equal("#/components/schemas/PetAdResponse", created.GetProperty("$ref").GetString());
        Assert.True(schema.GetProperty("properties").TryGetProperty("title", out _));
        Assert.Contains("title", GetRequiredNames(schema));
    }

    private static JsonElement GetSchema(JsonDocument document, string name) =>
        document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(name);

    private static List<string?> GetRequiredNames(JsonElement schema) =>
        schema.TryGetProperty("required", out var required)
            ? required.EnumerateArray().Select(item => item.GetString()).ToList()
            : [];

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
