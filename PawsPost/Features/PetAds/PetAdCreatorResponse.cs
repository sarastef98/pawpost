namespace PawsPost.Features.PetAds;

// The only public creator data: id and display name. Never add email, phone or user name here.
public record PetAdCreatorResponse(Guid Id, string DisplayName);
