using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using PawsPost.Data;

namespace PawsPost.Features.PetAds;

public class CreatePetAdRequest : IValidatableObject
{
    public const int TitleMaxLength = 100;
    public const int BreedMaxLength = 100;
    public const int DescriptionMaxLength = 2000;
    public const int LocationMaxLength = 100;
    public const int AgeInMonthsMax = 360;
    public const decimal PriceMax = 1_000_000m;

    [Required]
    public string? Title { get; init; }

    // Nullable so a missing value is reported instead of being read as the first enum value.
    [Required]
    [JsonConverter(typeof(StrictPetAdTypeConverter))]
    public PetAdType? Type { get; init; }

    // Nullable so a missing value is reported instead of being read as the first enum value.
    [Required]
    [JsonConverter(typeof(StrictPetSpeciesConverter))]
    public PetSpecies? Species { get; init; }

    public string? Breed { get; init; }

    // Nullable so a missing value is reported instead of being read as 0.
    [Required]
    [Range(0, AgeInMonthsMax)]
    public int? AgeInMonths { get; init; }

    [Required]
    public string? Description { get; init; }

    public decimal? Price { get; init; }

    public string? Location { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var title = Title?.Trim() ?? string.Empty;
        if (title.Length > TitleMaxLength)
        {
            yield return TooLong(nameof(Title), TitleMaxLength);
        }

        if (Breed?.Trim().Length > BreedMaxLength)
        {
            yield return TooLong(nameof(Breed), BreedMaxLength);
        }

        var description = Description?.Trim() ?? string.Empty;
        if (description.Length > DescriptionMaxLength)
        {
            yield return TooLong(nameof(Description), DescriptionMaxLength);
        }

        if (Location?.Trim().Length > LocationMaxLength)
        {
            yield return TooLong(nameof(Location), LocationMaxLength);
        }

        if (Type == PetAdType.Adoption && Price is not null)
        {
            yield return new ValidationResult("An adoption ad must not have a price.", [nameof(Price)]);
        }

        if (Type == PetAdType.Sale)
        {
            if (Price is null)
            {
                yield return new ValidationResult("A sale ad requires a price.", [nameof(Price)]);
            }
            else if (Price <= 0 || Price > PriceMax)
            {
                yield return new ValidationResult(
                    $"The Price field must be greater than 0 and at most {PriceMax:0}.", [nameof(Price)]);
            }
            else if (decimal.Round(Price.Value, 2) != Price.Value)
            {
                yield return new ValidationResult(
                    "The Price field can have at most 2 decimal places.", [nameof(Price)]);
            }
        }
    }

    private static ValidationResult TooLong(string field, int maxLength) =>
        new($"The {field} field must be at most {maxLength} characters.", [field]);

    // Accepts only the names of PetSpecies, in any case. Numbers are rejected.
    private sealed class StrictPetSpeciesConverter : JsonStringEnumConverter<PetSpecies>
    {
        public StrictPetSpeciesConverter()
            : base(namingPolicy: null, allowIntegerValues: false)
        {
        }
    }

    // Accepts only the names "Adoption" and "Sale". Numbers are rejected.
    private sealed class StrictPetAdTypeConverter : JsonStringEnumConverter<PetAdType>
    {
        public StrictPetAdTypeConverter()
            : base(namingPolicy: null, allowIntegerValues: false)
        {
        }
    }
}
