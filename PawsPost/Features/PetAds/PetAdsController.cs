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

    // The owner always comes from the login cookie, never from the request.
    // The service returns only Active ads. The literal "mine" wins over the {id} route.
    [HttpGet("mine")]
    [ProducesResponseType<PetAdListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListMineAsync(
        [FromQuery] MyPetAdsRequest request, CancellationToken cancellationToken)
    {
        var page = await petAdService.ListActiveByOwnerAsync(
            User.GetUserId(), request.Page, request.PageSize, cancellationToken);
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

    // POST with a verb segment (like AuthController): closing is an action that is not idempotent,
    // a second close gives 409. The caller comes from the login cookie, never from the request.
    [HttpPost("{id}/close")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CloseAsync(Guid id, CancellationToken cancellationToken)
    {
        var outcome = await petAdService.CloseAsync(id, User.GetUserId(), cancellationToken);

        return outcome switch
        {
            ClosePetAdOutcome.Closed => NoContent(),
            ClosePetAdOutcome.NotFound => NotFound(),
            ClosePetAdOutcome.NotOwner => Problem(
                title: "Not the owner of this ad", statusCode: StatusCodes.Status403Forbidden),
            _ => Problem(
                title: "Ad cannot be closed",
                detail: "Only Active ads can be closed.",
                statusCode: StatusCodes.Status409Conflict)
        };
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

        // 201 with a Location header that points at the details endpoint.
        return CreatedAtRoute(GetByIdRouteName, new { id = ad.Id }, ad);
    }
}
