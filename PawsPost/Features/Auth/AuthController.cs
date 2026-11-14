using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PawsPost.Common;

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

    [HttpPost("login")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await accountService.LoginAsync(request, cancellationToken);

        return user is null
            ? Problem(title: "Invalid credentials", statusCode: StatusCodes.Status401Unauthorized)
            : Ok(user);
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMeAsync(CancellationToken cancellationToken)
    {
        var user = await accountService.GetCurrentUserAsync(User.GetUserId(), cancellationToken);

        return user is null
            ? Problem(title: "Authentication required", statusCode: StatusCodes.Status401Unauthorized)
            : Ok(user);
    }
}
