namespace PawsPost.Features.Ai;

public interface IVectorStore
{
    Task UpsertAsync(Guid petAdId, float[] vector, string model, CancellationToken cancellationToken);

    Task RemoveAsync(Guid petAdId, CancellationToken cancellationToken);

    /// <summary>Returns at most <paramref name="top"/> matches of the given model with a score of at least <paramref name="minScore"/>, best first.</summary>
    Task<IReadOnlyList<VectorMatch>> SearchAsync(
        float[] query, string model, int top, double minScore, CancellationToken cancellationToken);
}
