namespace PawsPost.Features.Assistant;

/// <summary>Counts only, no ids and no ad text.</summary>
/// <param name="Missing">Active ads found without a stored vector.</param>
/// <param name="Indexed">Ads that got a vector in this run.</param>
/// <param name="Failed">Ads the provider failed for; they stay without a vector.</param>
public record PetAdReindexResult(int Missing, int Indexed, int Failed);
