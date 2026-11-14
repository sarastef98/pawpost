using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace PawsPost.Features.Auth;

// Auth actions are not a plural-noun resource, so this route uses verbs on purpose.
[ApiController]
[Route("api/auth")]
public class AuthController(IAccountService accountService) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await accountService.RegisterAsync(request, cancellationToken);

        return result.Outcome switch
        {
            RegisterOutcome.Success => StatusCode(StatusCodes.Status201Created, result.User),
            RegisterOutcome.EmailAlreadyRegistered => Problem(
                title: "Email already registered", statusCode: StatusCodes.Status409Conflict),
            _ => ValidationProblem(new ValidationProblemDetails(
                result.Errors!.ToDictionary(pair => pair.Key, pair => pair.Value)))
        };
    }
}
