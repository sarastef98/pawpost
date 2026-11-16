using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace PawsPost.Features.Creators;

[ApiController]
[Route("api/creators")]
public class CreatorsController(ICreatorService creatorService) : ControllerBase
{
    // Public on purpose: the creator profile is open to everyone (client decision in #44).
    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType<CreatorProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfileAsync(
        Guid id, [FromQuery] CreatorAdsRequest request, CancellationToken cancellationToken)
    {
        var profile = await creatorService.GetProfileAsync(id, request, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        return Ok(profile);
    }
}
