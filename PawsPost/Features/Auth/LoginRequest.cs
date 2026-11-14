using System.ComponentModel.DataAnnotations;

namespace PawsPost.Features.Auth;

public class LoginRequest
{
    // Same limits as RegisterRequest, so oversized input is rejected before any lookup or hashing.
    [Required]
    [StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Password { get; init; } = string.Empty;
}
