namespace PawsPost.Features.Auth;

/// <summary>Settings from the "Auth" section of appsettings.json. No secrets here.</summary>
public class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>How long the auth cookie stays valid. Sliding: each active use extends it.</summary>
    public TimeSpan CookieLifetime { get; set; } = TimeSpan.FromDays(14);
}
