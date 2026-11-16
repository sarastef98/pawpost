namespace PawsPost.Data;

/// <summary>The stored embedding vector of one pet ad.</summary>
public class PetAdEmbedding
{
    public Guid PetAdId { get; set; }

    // The float[] as little-endian bytes.
    public byte[] Vector { get; set; } = [];

    // The embedding model name. Vectors of another model are ignored in searches.
    public string Model { get; set; } = string.Empty;

    public int Dimensions { get; set; }

    // Always UTC.
    public DateTime UpdatedAt { get; set; }
}
