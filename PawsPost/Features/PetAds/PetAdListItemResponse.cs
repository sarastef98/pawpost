using System.Text.Json.Serialization;
using PawsPost.Data;

namespace PawsPost.Features.PetAds;

// Only the fields a list card needs. Never add owner data here.
public record PetAdListItemResponse(
    Guid Id,
    string Title,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PetAdType>))] PetAdType Type,
    string Species,
    decimal? Price,
    string? Location,
    DateTime CreatedAt);
