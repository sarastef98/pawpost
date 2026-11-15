using System.Text.Json.Serialization;
using PawsPost.Data;

namespace PawsPost.Features.PetAds;

// Never add owner email, phone or owner id here.
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
    DateTime CreatedAt);
