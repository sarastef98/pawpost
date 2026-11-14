using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace PawsPost.Common;

/// <summary>
/// Cleans up the request body in the generated OpenAPI document.
/// A controller parameter like <c>CreatePetAdRequest? request</c> is described as
/// "null or CreatePetAdRequest". Swagger UI builds its example from the first option, so it shows
/// <c>null</c>. This keeps only the real type and marks the body as required.
/// </summary>
public sealed class NonNullableRequestBodyTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.RequestBody is not OpenApiRequestBody requestBody || requestBody.Content is null)
        {
            return Task.CompletedTask;
        }

        var changed = false;
        foreach (var mediaType in requestBody.Content.Values)
        {
            if (mediaType.Schema?.OneOf is { Count: 2 } options)
            {
                var realOptions = options.Where(option => option.Type != JsonSchemaType.Null).ToList();
                if (realOptions.Count == 1)
                {
                    mediaType.Schema = realOptions[0];
                    changed = true;
                }
            }
        }

        // A missing body is rejected with 400, so the body is required.
        if (changed)
        {
            requestBody.Required = true;
        }

        return Task.CompletedTask;
    }
}
