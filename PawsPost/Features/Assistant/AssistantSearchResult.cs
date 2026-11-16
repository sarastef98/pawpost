namespace PawsPost.Features.Assistant;

public enum AssistantSearchOutcome
{
    Success,
    Unavailable
}

/// <summary>Outcome of a search, so the controller can pick the status code without holding rules.</summary>
public class AssistantSearchResult
{
    private AssistantSearchResult(AssistantSearchOutcome outcome, AssistantSearchResponse? response)
    {
        Outcome = outcome;
        Response = response;
    }

    public AssistantSearchOutcome Outcome { get; }

    public AssistantSearchResponse? Response { get; }

    public static AssistantSearchResult Success(AssistantSearchResponse response) =>
        new(AssistantSearchOutcome.Success, response);

    public static AssistantSearchResult Unavailable() => new(AssistantSearchOutcome.Unavailable, null);
}
