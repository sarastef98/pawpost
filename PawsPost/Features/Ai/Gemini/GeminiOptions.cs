namespace PawsPost.Features.Ai.Gemini;

/// <summary>
/// Settings from "Ai:Gemini". The ApiKey comes only from user-secrets or the environment variable
/// Ai__Gemini__ApiKey, never from appsettings.json.
/// </summary>
public class GeminiOptions
{
    public const string SectionName = "Ai:Gemini";

    public string EmbeddingModel { get; set; } = string.Empty;

    public string ChatModel { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;

    public string? ApiKey { get; set; }

    // The key must never be printed by accident.
    public override string ToString() => "GeminiOptions";
}
