using PawsPost.Data;
using PawsPost.Features.PetAds;

namespace PawsPost.Tests.Features.PetAds;

public class PetSpeciesParserTests
{
    [Theory]
    [InlineData("Dog", PetSpecies.Dog)]
    [InlineData("Cat", PetSpecies.Cat)]
    [InlineData("Bird", PetSpecies.Bird)]
    [InlineData("Rabbit", PetSpecies.Rabbit)]
    [InlineData("Other", PetSpecies.Other)]
    public void Parse_CanonicalName_ReturnsThatSpecies(string value, PetSpecies expected)
    {
        // Act
        var result = PetSpeciesParser.Parse(value);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("dog", PetSpecies.Dog)]
    [InlineData("CAT", PetSpecies.Cat)]
    [InlineData("bIrD", PetSpecies.Bird)]
    [InlineData("rabbit", PetSpecies.Rabbit)]
    [InlineData("OTHER", PetSpecies.Other)]
    public void Parse_NameInDifferentCase_ReturnsThatSpecies(string value, PetSpecies expected)
    {
        // Act
        var result = PetSpeciesParser.Parse(value);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(" Dog")]
    [InlineData("Dog ")]
    [InlineData("  dog  ")]
    [InlineData("\tDog\n")]
    public void Parse_NameWithSurroundingWhitespace_IsTrimmed(string value)
    {
        // Act
        var result = PetSpeciesParser.Parse(value);

        // Assert
        Assert.Equal(PetSpecies.Dog, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_NullEmptyOrWhitespace_ReturnsNull(string? value)
    {
        // Act
        var result = PetSpeciesParser.Parse(value);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("Hamster")]
    [InlineData("Do")]
    [InlineData("Dogs")]
    [InlineData("D og")]
    [InlineData("Dog\0")]
    [InlineData("куче")]
    public void Parse_UnknownName_ReturnsNull(string value)
    {
        // Act
        var result = PetSpeciesParser.Parse(value);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("4")]
    [InlineData("99")]
    [InlineData("-1")]
    public void Parse_Number_ReturnsNull(string value)
    {
        // Act
        var result = PetSpeciesParser.Parse(value);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("Dog, Cat")]
    [InlineData("Dog|Cat")]
    public void Parse_CombinedNames_ReturnsNull(string value)
    {
        // Act
        var result = PetSpeciesParser.Parse(value);

        // Assert
        Assert.Null(result);
    }
}
