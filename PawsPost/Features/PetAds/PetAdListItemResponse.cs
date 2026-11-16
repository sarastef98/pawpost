using System.Text.Json.Serialization;
using PawsPost.Data;

namespace PawsPost.Features.PetAds;

// Only the fields a list card needs. The creator is only the public id and display name (client decision in #44).
// Never add owner email, phone or user name here.
public record PetAdListItemResponse(
    Guid Id,
    string Title,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PetAdType>))] PetAdType Type,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PetSpecies>))] PetSpecies Species,
    decimal? Price,
    string? Location,
    DateTime CreatedAt,
    PetAdCreatorResponse Creator);
