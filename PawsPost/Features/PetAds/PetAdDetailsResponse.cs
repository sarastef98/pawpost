using System.Text.Json.Serialization;
using PawsPost.Data;

namespace PawsPost.Features.PetAds;

// The creator is only the public id and display name (client decision in #44). Never add owner email, phone or user name here.
public record PetAdDetailsResponse(
    Guid Id,
    string Title,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PetAdType>))] PetAdType Type,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PetSpecies>))] PetSpecies Species,
    string? Breed,
    int AgeInMonths,
    string Description,
    decimal? Price,
    string? Location,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PetAdStatus>))] PetAdStatus Status,
    DateTime CreatedAt,
    PetAdCreatorResponse Creator);
