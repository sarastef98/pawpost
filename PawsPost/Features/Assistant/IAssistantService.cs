namespace PawsPost.Features.Assistant;

public interface IAssistantService
{
    Task<AssistantSearchResult> SearchAsync(string question, CancellationToken cancellationToken);
}
