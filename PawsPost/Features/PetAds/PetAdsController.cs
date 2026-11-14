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
    [HttpPost]
    [ProducesResponseType<PetAdResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateAsync(CreatePetAdRequest request, CancellationToken cancellationToken)
    {
        // The owner always comes from the login cookie, never from the request body.
        var ad = await petAdService.CreateAsync(User.GetUserId(), request, cancellationToken);

        // No Location header yet: there is no "get one ad" endpoint.
        return StatusCode(StatusCodes.Status201Created, ad);
    }
}
