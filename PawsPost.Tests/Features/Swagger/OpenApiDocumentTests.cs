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

    [Fact]
    public async Task GetOpenApiDocument_PetAdsGet_HasPageSizeSpeciesAndTypeQueryParameters()
    {
        // Arrange
        using var document = await GetOpenApiDocumentAsync();

        // Act
        var parameters = GetPetAdsGet(document).GetProperty("parameters").EnumerateArray().ToList();

        // Assert
        Assert.Equal(["page", "pageSize", "species", "type"], parameters.Select(p => p.GetProperty("name").GetString()).Order());
        Assert.All(parameters, parameter =>
        {
            Assert.Equal("query", parameter.GetProperty("in").GetString());
            Assert.False(parameter.TryGetProperty("required", out var required) && required.GetBoolean());
        });
    }

    [Fact]
    public async Task GetOpenApiDocument_PetAdsGet_ResponseReferencesPetAdListResponse()
    {
        // Arrange
        using var document = await GetOpenApiDocumentAsync();

        // Act
        var ok = GetPetAdsGet(document).GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var schema = GetSchema(document, "PetAdListResponse");

        // Assert
        Assert.Equal("#/components/schemas/PetAdListResponse", ok.GetProperty("$ref").GetString());
        var names = schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).Order().ToList();
        Assert.Equal(["items", "page", "pageSize", "totalCount"], names);
        Assert.Equal(["items", "page", "pageSize", "totalCount"], GetRequiredNames(schema).Order());
    }

    [Fact]
    public async Task GetOpenApiDocument_PetAdListItemResponseSchema_HasCardFieldsAndNoOwnerId()
    {
        // Arrange
        using var document = await GetOpenApiDocumentAsync();

        // Act
        var schema = GetSchema(document, "PetAdListItemResponse");

        // Assert
        var names = schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).Order().ToList();
        Assert.Equal(["createdAt", "id", "location", "price", "species", "title", "type"], names);
        Assert.DoesNotContain("ownerId", names);
    }

    [Fact]
    public async Task GetOpenApiDocument_PetAdsIdGet_HasRequiredIdPathParameter()
    {
        // Arrange
        using var document = await GetOpenApiDocumentAsync();

        // Act
        var parameters = GetPetAdsIdGet(document).GetProperty("parameters").EnumerateArray().ToList();

        // Assert
        var parameter = Assert.Single(parameters);
        Assert.Equal("id", parameter.GetProperty("name").GetString());
        Assert.Equal("path", parameter.GetProperty("in").GetString());
        Assert.True(parameter.GetProperty("required").GetBoolean());
        Assert.Equal("uuid", parameter.GetProperty("schema").GetProperty("format").GetString());
    }

    [Fact]
    public async Task GetOpenApiDocument_PetAdsIdGet_ResponseReferencesPetAdDetailsResponse()
    {
        // Arrange
        using var document = await GetOpenApiDocumentAsync();

        // Act
        var responses = GetPetAdsIdGet(document).GetProperty("responses");
        var ok = responses.GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema");

        // Assert
        Assert.Equal("#/components/schemas/PetAdDetailsResponse", ok.GetProperty("$ref").GetString());
        Assert.True(responses.TryGetProperty("400", out _));
        Assert.True(responses.TryGetProperty("401", out _));
        Assert.True(responses.TryGetProperty("404", out _));
    }

    [Fact]
    public async Task GetOpenApiDocument_PetAdDetailsResponseSchema_HasExpectedFieldsAndNoOwnerId()
    {
        // Arrange
        using var document = await GetOpenApiDocumentAsync();

        // Act
        var schema = GetSchema(document, "PetAdDetailsResponse");

        // Assert
        var names = schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).Order().ToList();
        Assert.Equal(
            ["ageInMonths", "breed", "createdAt", "description", "id", "location", "price", "species", "status", "title", "type"],
            names);
        Assert.DoesNotContain("ownerId", names);
        var required = GetRequiredNames(schema);
        foreach (var name in new[] { "id", "title", "type", "species", "ageInMonths", "description", "status", "createdAt" })
        {
            Assert.Contains(name, required);
        }
    }

    [Fact]
    public async Task GetOpenApiDocument_AssistantSearch_HasRequestAndResponseSchemas()
    {
        // Arrange
        using var document = await GetOpenApiDocumentAsync();

        // Act
        var post = GetAssistantSearchPost(document);
        var requestSchema = post.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var responses = post.GetProperty("responses");
        var ok = responses.GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema");

        // Assert
        Assert.Equal("#/components/schemas/AssistantSearchRequest", requestSchema.GetProperty("$ref").GetString());
        Assert.Equal("#/components/schemas/AssistantSearchResponse", ok.GetProperty("$ref").GetString());
        Assert.True(responses.TryGetProperty("400", out _));
        Assert.True(responses.TryGetProperty("401", out _));
        Assert.True(responses.TryGetProperty("503", out _));
        Assert.Contains("question", GetSchema(document, "AssistantSearchRequest").GetProperty("properties").EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task GetOpenApiDocument_AssistantSearchResponse_AdsUsePetAdDetailsResponse()
    {
        // Arrange
        using var document = await GetOpenApiDocumentAsync();

        // Act
        var schema = GetSchema(document, "AssistantSearchResponse");
        var ads = schema.GetProperty("properties").GetProperty("ads");

        // Assert
        Assert.Equal("array", ads.GetProperty("type").GetString());
        Assert.Equal("#/components/schemas/PetAdDetailsResponse", ads.GetProperty("items").GetProperty("$ref").GetString());
        Assert.Contains("answer", GetRequiredNames(schema));
        Assert.Contains("ads", GetRequiredNames(schema));
    }

    private static JsonElement GetAssistantSearchPost(JsonDocument document) =>
        document.RootElement.GetProperty("paths").GetProperty("/api/assistant/search").GetProperty("post");

    private static readonly string[] AllowedSpecies = ["Dog", "Cat", "Bird", "Rabbit", "Other"];

    [Fact]
    public async Task GetOpenApiDocument_CreatePetAdRequestSchema_SpeciesListsExactlyTheAllowedValues()
    {
        // Arrange
        using var document = await GetOpenApiDocumentAsync();

        // Act
        var species = GetSchema(document, "CreatePetAdRequest").GetProperty("properties").GetProperty("species");

        // Assert
        Assert.Equal(AllowedSpecies.Order(), GetEnumValues(document, species).Order());
    }

    [Theory]
    [InlineData("PetAdResponse")]
    [InlineData("PetAdListItemResponse")]
    [InlineData("PetAdDetailsResponse")]
    public async Task GetOpenApiDocument_ResponseSchema_SpeciesListsExactlyTheAllowedValues(string schemaName)
    {
        // Arrange
        using var document = await GetOpenApiDocumentAsync();

        // Act
        var species = GetSchema(document, schemaName).GetProperty("properties").GetProperty("species");

        // Assert
        Assert.Equal(AllowedSpecies.Order(), GetEnumValues(document, species).Order());
    }

    [Fact]
    public async Task GetOpenApiDocument_PetAdsGet_SpeciesQueryParameterListsExactlyTheAllowedValues()
    {
        // Arrange
        using var document = await GetOpenApiDocumentAsync();

        // Act
        var parameter = GetPetAdsGet(document).GetProperty("parameters").EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "species");

        // Assert
        Assert.Equal(AllowedSpecies.Order(), GetEnumValues(document, parameter.GetProperty("schema")).Order());
    }

    // Follows a $ref (and the oneOf/allOf wrappers some generators add) to the enum values.
    private static List<string?> GetEnumValues(JsonDocument document, JsonElement schema)
    {
        if (schema.TryGetProperty("$ref", out var reference))
        {
            return GetEnumValues(document, GetSchema(document, reference.GetString()!.Split('/').Last()));
        }

        foreach (var wrapper in new[] { "oneOf", "allOf", "anyOf" })
        {
            if (schema.TryGetProperty(wrapper, out var parts))
            {
                return parts.EnumerateArray()
                    .Where(part => part.TryGetProperty("$ref", out _) || part.TryGetProperty("enum", out _))
                    .SelectMany(part => GetEnumValues(document, part))
                    .ToList();
            }
        }

        return schema.GetProperty("enum").EnumerateArray().Select(value => value.GetString()).ToList();
    }

    private static JsonElement GetPetAdsIdGet(JsonDocument document) =>
        document.RootElement.GetProperty("paths").GetProperty("/api/pet-ads/{id}").GetProperty("get");

    private static JsonElement GetPetAdsGet(JsonDocument document) =>
        document.RootElement.GetProperty("paths").GetProperty("/api/pet-ads").GetProperty("get");

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
