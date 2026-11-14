using System.ComponentModel.DataAnnotations;

namespace PawsPost.Features.Auth;

public class LoginRequest
{
    [Required]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}
