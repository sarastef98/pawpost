using PawsPost.Features.PetAds;

namespace PawsPost.Features.Creators;

// Public fields only: id, display name and the creator's Active ads. Never add email, phone or user name here.
public record CreatorProfileResponse(Guid Id, string DisplayName, PetAdListResponse Ads);
