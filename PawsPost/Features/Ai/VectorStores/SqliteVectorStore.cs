using System.Buffers.Binary;
using Microsoft.EntityFrameworkCore;
using PawsPost.Data;

namespace PawsPost.Features.Ai.VectorStores;

/// <summary>
/// Stores vectors in the existing SQLite database. Similarity (cosine) is computed in C# over all
/// vectors of one model, which is fine for a demo-sized data set.
/// </summary>
public class SqliteVectorStore(AppDbContext dbContext, TimeProvider timeProvider) : IVectorStore
{
    public async Task UpsertAsync(Guid petAdId, float[] vector, string model, CancellationToken cancellationToken)
    {
        var bytes = ToBytes(vector);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var existing = await dbContext.PetAdEmbeddings
            .SingleOrDefaultAsync(item => item.PetAdId == petAdId, cancellationToken);

        if (existing is null)
        {
            dbContext.PetAdEmbeddings.Add(new PetAdEmbedding
            {
                PetAdId = petAdId,
                Vector = bytes,
                Model = model,
                Dimensions = vector.Length,
                UpdatedAt = now
            });
        }
        else
        {
            existing.Vector = bytes;
            existing.Model = model;
            existing.Dimensions = vector.Length;
            existing.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid petAdId, CancellationToken cancellationToken)
    {
        await dbContext.PetAdEmbeddings
            .Where(item => item.PetAdId == petAdId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> GetIndexedIdsAsync(
        IReadOnlyCollection<Guid> petAdIds, CancellationToken cancellationToken)
    {
        if (petAdIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var ids = petAdIds.ToList();
        var found = await dbContext.PetAdEmbeddings
            .AsNoTracking()
            .Where(item => ids.Contains(item.PetAdId))
            .Select(item => item.PetAdId)
            .ToListAsync(cancellationToken);

        return found.ToHashSet();
    }

    public async Task<IReadOnlyList<VectorMatch>> SearchAsync(
        float[] query, string model, int top, double minScore, CancellationToken cancellationToken)
    {
        var rows = await dbContext.PetAdEmbeddings
            .AsNoTracking()
            .Where(item => item.Model == model && item.Dimensions == query.Length)
            .Select(item => new { item.PetAdId, item.Vector })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new VectorMatch(row.PetAdId, Cosine(query, FromBytes(row.Vector))))
            .Where(match => match.Score >= minScore)
            .OrderByDescending(match => match.Score)
            .Take(top)
            .ToList();
    }

    private static byte[] ToBytes(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        for (var i = 0; i < vector.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * sizeof(float)), vector[i]);
        }

        return bytes;
    }

    private static float[] FromBytes(byte[] bytes)
    {
        var vector = new float[bytes.Length / sizeof(float)];
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(i * sizeof(float)));
        }

        return vector;
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
