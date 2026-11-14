using System.Security.Claims;

namespace PawsPost.Common;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns the logged-in user's id from the NameIdentifier claim.
    /// Throws if the claim is missing or not a Guid: callers must be behind [Authorize].
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
        {
            throw new InvalidOperationException("The user id claim is missing or invalid.");
        }

        return userId;
    }
}
