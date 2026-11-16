namespace PawsPost.Features.Assistant;

/// <summary>Settings from the "Assistant" section of appsettings.json.</summary>
public class AssistantOptions
{
    public const string SectionName = "Assistant";

    public const int MaxResultsUpperLimit = 20;

    /// <summary>The most ads one answer can list.</summary>
    public int MaxResults { get; set; } = 5;

    /// <summary>Minimum cosine similarity for an ad to count as a match.</summary>
    public double MinSimilarity { get; set; } = 0.6;
}
