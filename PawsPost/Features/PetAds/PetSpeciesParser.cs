using PawsPost.Data;

namespace PawsPost.Features.PetAds;

public static class PetSpeciesParser
{
    // Accepts only the names of PetSpecies, in any case, ignoring surrounding spaces. Numbers are rejected.
    public static PetSpecies? Parse(string? value) => ParseExact(value?.Trim());

    // Same as Parse, but the value is not trimmed, so padded names are rejected.
    public static PetSpecies? ParseExact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        foreach (var species in Enum.GetValues<PetSpecies>())
        {
            if (string.Equals(value, species.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return species;
            }
        }

        return null;
    }
}
