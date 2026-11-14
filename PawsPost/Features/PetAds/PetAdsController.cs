using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PawsPost.Common;

namespace PawsPost.Features.PetAds;

[ApiController]
[Route("api/pet-ads")]
[Authorize]
public class PetAdsController(IPetAdService petAdService) : ControllerBase
{
    // Route name of the details action. CreateAsync uses it for the Location header.
    private const string GetByIdRouteName = "GetPetAdById";

    // Public on purpose: anyone can browse. The class-level [Authorize] still protects every other action.
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<PetAdListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] ListPetAdsRequest request, CancellationToken cancellationToken)
    {
        var page = await petAdService.ListAsync(request, cancellationToken);
        return Ok(page);
    }

    // No [AllowAnonymous]: the class-level [Authorize] applies, so login is required.
    // Unknown ids and ads that are not Active both give 404.
    [HttpGet("{id}", Name = GetByIdRouteName)]
    [ProducesResponseType<PetAdDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var ad = await petAdService.GetByIdAsync(id, cancellationToken);
        if (ad is null)
        {
            return NotFound();
        }

        return Ok(ad);
    }

    [HttpPost]
    [ProducesResponseType<PetAdResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateAsync(CreatePetAdRequest? request, CancellationToken cancellationToken)
    {
        // Nullable so a bad JSON value (for example an unknown type) reports only its own error,
        // not an extra "request is required" error. A null body with no other error is rejected here.
        if (request is null)
        {
            ModelState.AddModelError(string.Empty, "A request body is required.");
            return ValidationProblem(ModelState);
        }

        // The owner always comes from the login cookie, never from the request body.
        var ad = await petAdService.CreateAsync(User.GetUserId(), request, cancellationToken);

        // No Location header yet: there is no "get one ad" endpoint.
        return StatusCode(StatusCodes.Status201Created, ad);
    }
}
