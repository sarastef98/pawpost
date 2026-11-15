using PawsPost.Data;

namespace PawsPost.Features.PetAds;

public static class PetSpeciesParser
{
    // Accepts only the names of PetSpecies, in any case, ignoring surrounding spaces. Numbers are rejected.
    public static PetSpecies? Parse(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        foreach (var species in Enum.GetValues<PetSpecies>())
        {
            if (string.Equals(trimmed, species.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return species;
            }
        }

        return null;
    }
}
