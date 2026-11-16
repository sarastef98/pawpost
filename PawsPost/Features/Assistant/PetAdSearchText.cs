using PawsPost.Data;

namespace PawsPost.Features.Assistant;

/// <summary>
/// The public ad fields that may be embedded or sent to the chat provider. Owner data is not here,
/// so it cannot reach a provider by construction.
/// </summary>
public record PetAdSearchText(
    Guid Id,
    string Title,
    PetAdType Type,
    PetSpecies Species,
    string? Breed,
    int AgeInMonths,
    string Description,
    string? Location);
