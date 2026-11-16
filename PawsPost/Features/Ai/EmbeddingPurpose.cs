namespace PawsPost.Features.Ai;

/// <summary>Why a text is embedded. Some providers tune the vector for the purpose.</summary>
public enum EmbeddingPurpose
{
    Document,
    Query
}
