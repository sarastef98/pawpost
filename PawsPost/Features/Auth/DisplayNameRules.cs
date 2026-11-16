using System.Text.RegularExpressions;

namespace PawsPost.Features.Auth;

/// <summary>The one place for the public display name rules, shared by the request and the service.</summary>
public static partial class DisplayNameRules
{
    public const int MinLength = 3;
    public const int MaxLength = 30;

    // Unicode letters and digits, underscore and hyphen, separated by single plain spaces only.
    [GeneratedRegex(@"^[\p{L}\p{Nd}_-]+( [\p{L}\p{Nd}_-]+)*$")]
    private static partial Regex AllowedPattern();

    public static string Trim(string value) => value.Trim();

    // Upper case copy used for the case-insensitive unique index.
    public static string Normalize(string trimmedValue) => trimmedValue.ToUpperInvariant();

    /// <summary>Returns an error message for a trimmed name, or null when the name is valid.</summary>
    public static string? Validate(string trimmedValue)
    {
        if (trimmedValue.Length < MinLength || trimmedValue.Length > MaxLength)
        {
            return $"The DisplayName field must be between {MinLength} and {MaxLength} characters.";
        }

        if (!AllowedPattern().IsMatch(trimmedValue))
        {
            return "The DisplayName field may contain only letters, digits, single spaces, hyphens and underscores.";
        }

        return null;
    }
}
