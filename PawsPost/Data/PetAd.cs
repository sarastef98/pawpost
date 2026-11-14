namespace PawsPost.Data;

public class PetAd
{
    public Guid Id { get; set; }

    public PetAdType Type { get; set; }

    public string Species { get; set; } = string.Empty;

    public string? Breed { get; set; }

    public int AgeInMonths { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal? Price { get; set; }

    public string? Location { get; set; }

    public PetAdStatus Status { get; set; }

    public Guid OwnerId { get; set; }

    public User? Owner { get; set; }

    // Always UTC.
    public DateTime CreatedAt { get; set; }
}
