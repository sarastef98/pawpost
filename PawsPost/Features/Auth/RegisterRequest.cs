using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

namespace PawsPost.Features.Auth;

public class RegisterRequest : IValidatableObject
{
    [Required]
    [StringLength(254)]
    public string Email { get; init; } = string.Empty;

    // Length rules are checked here. Character rules (upper, lower, digit, symbol) are enforced by Identity.
    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var email = Email.Trim();

        // Skip when empty or too long: the attributes above already report those cases.
        if (email.Length == 0 || email.Length > 254)
        {
            yield break;
        }

        // [EmailAddress] alone is weak, so also parse the address and require it to match exactly.
        if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
        {
            yield return new ValidationResult("The Email field is not a valid email address.", [nameof(Email)]);
        }
    }
}
