namespace PawsPost.Features.Ai;

/// <summary>Settings from the "Ai" section of appsettings.json. No secrets here.</summary>
public class AiOptions
{
    public const string SectionName = "Ai";

    public const string GeminiProvider = "Gemini";

    /// <summary>Which provider implementation to use. Only "Gemini" exists.</summary>
    public string Provider { get; set; } = GeminiProvider;
}
