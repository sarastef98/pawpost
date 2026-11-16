using PawsPost.Features.Ai;

namespace PawsPost.Tests.Infrastructure.Fakes;

/// <summary>In-memory vector store with the same contract as the real one (cosine similarity, best first).</summary>
public class FakeVectorStore : IVectorStore
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, (float[] Vector, string Model)> _vectors = [];

    /// <summary>When set, <see cref="UpsertAsync"/> throws it, to simulate a storage failure.</summary>
    public Exception? UpsertExceptionToThrow { get; set; }

    /// <summary>When set, <see cref="RemoveAsync"/> throws it, to simulate a storage failure.</summary>
    public Exception? RemoveExceptionToThrow { get; set; }

    private readonly List<Guid> _removedIds = [];

    /// <summary>The ids passed to <see cref="RemoveAsync"/>, in call order.</summary>
    public IReadOnlyList<Guid> RemovedIds
    {
        get
        {
            lock (_lock)
            {
                return _removedIds.ToList();
            }
        }
    }

    private readonly List<int> _indexedIdsCallSizes = [];

    /// <summary>How many ids each <see cref="GetIndexedIdsAsync"/> call received, in call order.</summary>
    public IReadOnlyList<int> IndexedIdsCallSizes
    {
        get
        {
            lock (_lock)
            {
                return _indexedIdsCallSizes.ToList();
            }
        }
    }

    public bool Contains(Guid petAdId)
    {
        lock (_lock)
        {
            return _vectors.ContainsKey(petAdId);
        }
    }

    public Task UpsertAsync(Guid petAdId, float[] vector, string model, CancellationToken cancellationToken)
    {
        if (UpsertExceptionToThrow is not null)
        {
            throw UpsertExceptionToThrow;
        }

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
            _removedIds.Add(petAdId);
        }

        if (RemoveExceptionToThrow is not null)
        {
            throw RemoveExceptionToThrow;
        }

        lock (_lock)
        {
            _vectors.Remove(petAdId);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlySet<Guid>> GetIndexedIdsAsync(
        IReadOnlyCollection<Guid> petAdIds, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _indexedIdsCallSizes.Add(petAdIds.Count);
            IReadOnlySet<Guid> found = petAdIds.Where(_vectors.ContainsKey).ToHashSet();
            return Task.FromResult(found);
        }
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
