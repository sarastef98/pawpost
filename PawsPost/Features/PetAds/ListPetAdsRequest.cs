using System.ComponentModel.DataAnnotations;
using PawsPost.Data;

namespace PawsPost.Features.PetAds;

public class ListPetAdsRequest : IValidatableObject
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;

    public string? Species { get; init; }

    // Bound as a string on purpose: the enum binder would accept numbers.
    public string? Type { get; init; }

    // The parsed type filter, or null when no filter is given or the value is not valid.
    public PetAdType? ParsedType => ParseType(Type);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Checked here (not with [Range]) so every bad parameter is reported once, together with the others.
        if (Page < 1)
        {
            yield return new ValidationResult("The Page field must be at least 1.", [nameof(Page)]);
        }

        if (PageSize < 1 || PageSize > MaxPageSize)
        {
            yield return new ValidationResult(
                $"The PageSize field must be between 1 and {MaxPageSize}.", [nameof(PageSize)]);
        }

        if (Species?.Trim().Length > CreatePetAdRequest.SpeciesMaxLength)
        {
            yield return new ValidationResult(
                $"The Species field must be at most {CreatePetAdRequest.SpeciesMaxLength} characters.",
                [nameof(Species)]);
        }

        if (Type is not null && ParsedType is null)
        {
            yield return new ValidationResult(
                "The Type field must be Adoption or Sale.", [nameof(Type)]);
        }
    }

    // Accepts only the names "Adoption" and "Sale", in any case. Numbers are rejected.
    private static PetAdType? ParseType(string? value)
    {
        var trimmed = value?.Trim();
        if (string.Equals(trimmed, nameof(PetAdType.Adoption), StringComparison.OrdinalIgnoreCase))
        {
            return PetAdType.Adoption;
        }

        if (string.Equals(trimmed, nameof(PetAdType.Sale), StringComparison.OrdinalIgnoreCase))
        {
            return PetAdType.Sale;
        }

        return null;
    }
}
