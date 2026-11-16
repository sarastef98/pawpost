using PawsPost.Features.Ai;

namespace PawsPost.Tests.Infrastructure.Fakes;

/// <summary>In-memory vector store with the same contract as the real one (cosine similarity, best first).</summary>
public class FakeVectorStore : IVectorStore
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, (float[] Vector, string Model)> _vectors = [];

    public bool Contains(Guid petAdId)
    {
        lock (_lock)
        {
            return _vectors.ContainsKey(petAdId);
        }
    }

    public Task UpsertAsync(Guid petAdId, float[] vector, string model, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _vectors[petAdId] = (vector, model);
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid petAdId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _vectors.Remove(petAdId);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VectorMatch>> SearchAsync(
        float[] query, string model, int top, double minScore, CancellationToken cancellationToken)
    {
        List<VectorMatch> matches;
        lock (_lock)
        {
            matches = _vectors
                .Where(entry => entry.Value.Model == model && entry.Value.Vector.Length == query.Length)
                .Select(entry => new VectorMatch(entry.Key, Cosine(query, entry.Value.Vector)))
                .Where(match => match.Score >= minScore)
                .OrderByDescending(match => match.Score)
                .Take(top)
                .ToList();
        }

        return Task.FromResult<IReadOnlyList<VectorMatch>>(matches);
    }

    private static double Cosine(float[] a, float[] b)
    {
        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        return normA == 0 || normB == 0 ? 0 : dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
