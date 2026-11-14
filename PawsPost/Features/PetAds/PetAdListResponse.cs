namespace PawsPost.Features.PetAds;

public record PetAdListResponse(
    IReadOnlyList<PetAdListItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
