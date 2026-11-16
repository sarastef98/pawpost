using PawsPost.Data;
using PawsPost.Features.Assistant;

namespace PawsPost.Tests.Features.Assistant;

public class PetAdEmbeddingTextTests
{
    private static readonly Guid AdId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Build_FullAd_ReturnsFixedLabeledText()
    {
        // Arrange
        var ad = new PetAdSearchText(
            AdId, "Calm beagle", PetAdType.Adoption, PetSpecies.Dog, "Beagle", 24, "Loves walks.", "Skopje");

        // Act
        var text = PetAdEmbeddingText.Build(ad);

        // Assert
        var expected = string.Join(
            Environment.NewLine,
            "Title: Calm beagle",
            "Type: Adoption",
            "Species: Dog",
            "Breed: Beagle",
            "Age in months: 24",
            "Location: Skopje",
            "Description: Loves walks.");
        Assert.Equal(expected, text);
    }

    [Fact]
    public void Build_NullBreedAndLocation_LeavesThemOut()
    {
        // Arrange
        var ad = new PetAdSearchText(
            AdId, "Calm cat", PetAdType.Adoption, PetSpecies.Cat, null, 6, "Quiet.", null);

        // Act
        var text = PetAdEmbeddingText.Build(ad);

        // Assert
        Assert.DoesNotContain("Breed:", text);
        Assert.DoesNotContain("Location:", text);
        Assert.Contains("Title: Calm cat", text);
        Assert.Contains("Description: Quiet.", text);
    }

    [Fact]
    public void Build_AnyAd_DoesNotContainTheAdId()
    {
        // Arrange
        var ad = new PetAdSearchText(
            AdId, "Calm cat", PetAdType.Adoption, PetSpecies.Cat, null, 6, "Quiet.", null);

        // Act
        var text = PetAdEmbeddingText.Build(ad);

        // Assert
        Assert.DoesNotContain(AdId.ToString(), text);
    }
}
