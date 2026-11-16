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

    // [Required] reports a missing or empty value. Whitespace-only values are checked in Validate after trimming.
    [Required]
    public string DisplayName { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var displayName = DisplayNameRules.Trim(DisplayName);

        // Skip when empty: [Required] already reports it. Whitespace-only is reported here as too short.
        if (DisplayName.Length > 0)
        {
            var error = DisplayNameRules.Validate(displayName);
            if (error is not null)
            {
                yield return new ValidationResult(error, [nameof(DisplayName)]);
            }
        }

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
