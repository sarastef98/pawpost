using PawsPost.Features.Ai;

namespace PawsPost.Features.Assistant;

public class PetAdSearchIndexer(IEmbeddingProvider embeddingProvider, IVectorStore vectorStore) : IPetAdSearchIndexer
{
    public async Task IndexAsync(PetAdSearchText ad, CancellationToken cancellationToken)
    {
        var text = PetAdEmbeddingText.Build(ad);
        var vector = await embeddingProvider.EmbedAsync(text, EmbeddingPurpose.Document, cancellationToken);
        await vectorStore.UpsertAsync(ad.Id, vector, embeddingProvider.ModelName, cancellationToken);
    }

    public Task RemoveAsync(Guid petAdId, CancellationToken cancellationToken) =>
        vectorStore.RemoveAsync(petAdId, cancellationToken);
}
