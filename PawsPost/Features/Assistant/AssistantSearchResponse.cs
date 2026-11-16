using PawsPost.Features.PetAds;

namespace PawsPost.Features.Assistant;

// Ads reuse the public details DTO, so no owner data can appear here.
public record AssistantSearchResponse(string Answer, IReadOnlyList<PetAdDetailsResponse> Ads);
