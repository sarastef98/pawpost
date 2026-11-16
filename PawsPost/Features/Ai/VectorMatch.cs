namespace PawsPost.Features.Ai;

/// <summary>A stored vector that matched a query. Score is the cosine similarity (higher is closer).</summary>
public record VectorMatch(Guid PetAdId, double Score);
