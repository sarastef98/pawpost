using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace PawsPost.Features.Assistant;

[ApiController]
[Route("api/assistant")]
[Authorize]
public class AssistantController(IAssistantService assistantService) : ControllerBase
{
    // Any logged-in user may search. Nothing is changed and no user-owned data is read.
    [HttpPost("search")]
    [ProducesResponseType<AssistantSearchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SearchAsync(AssistantSearchRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            ModelState.AddModelError(string.Empty, "A request body is required.");
            return ValidationProblem(ModelState);
        }

        var result = await assistantService.SearchAsync(request.Question!, cancellationToken);
        if (result.Outcome == AssistantSearchOutcome.Unavailable)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "The assistant is temporarily unavailable.");
        }

        return Ok(result.Response);
    }
}
