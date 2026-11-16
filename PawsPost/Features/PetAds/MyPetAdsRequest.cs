using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace PawsPost.Features.PetAds;

public class MyPetAdsRequest : IValidatableObject
{
    [FromQuery(Name = "page")]
    public int Page { get; init; } = 1;

    [FromQuery(Name = "pageSize")]
    public int PageSize { get; init; } = ListPetAdsRequest.DefaultPageSize;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Page < 1)
        {
            yield return new ValidationResult("The Page field must be at least 1.", [nameof(Page)]);
        }

        if (PageSize < 1 || PageSize > ListPetAdsRequest.MaxPageSize)
        {
            yield return new ValidationResult(
                $"The PageSize field must be between 1 and {ListPetAdsRequest.MaxPageSize}.", [nameof(PageSize)]);
        }
    }
}
