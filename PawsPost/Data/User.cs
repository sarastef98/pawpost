using Microsoft.AspNetCore.Identity;

namespace PawsPost.Data;

public class User : IdentityUser<Guid>
{
    /// <summary>The public name shown on ads and the creator profile, as the user typed it (trimmed).</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Upper-case copy of <see cref="DisplayName"/>. A unique index on it makes names unique ignoring case.</summary>
    public string NormalizedDisplayName { get; set; } = string.Empty;
}
