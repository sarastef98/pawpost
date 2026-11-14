using System.Text.Json.Serialization;
using PawsPost.Data;

namespace PawsPost.Features.PetAds;

// Never add owner email, phone or other user data here.
public record PetAdResponse(
    Guid Id,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PetAdType>))] PetAdType Type,
    string Species,
    string? Breed,
    int AgeInMonths,
    string Description,
    decimal? Price,
    string? Location,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PetAdStatus>))] PetAdStatus Status,
    Guid OwnerId,
    DateTime CreatedAt);
