using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using PawsPost.Data;

namespace PawsPost.Common;

/// <summary>
/// Lists the allowed species in the OpenAPI document for the <c>species</c> query parameter.
/// The parameter is a string in code (the enum binder would accept numbers), so the values
/// are added here from <see cref="PetSpecies"/>.
/// </summary>
public sealed class SpeciesQueryParameterTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Parameters is null)
        {
            return Task.CompletedTask;
        }

        foreach (var parameter in operation.Parameters)
        {
            if (parameter.Name == "species" && parameter.In == ParameterLocation.Query && parameter.Schema is OpenApiSchema schema)
            {
                schema.Enum = Enum.GetNames<PetSpecies>()
                    .Select(name => (JsonNode)JsonValue.Create(name))
                    .ToList();
            }
        }

        return Task.CompletedTask;
    }
}
