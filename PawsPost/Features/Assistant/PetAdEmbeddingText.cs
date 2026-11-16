using System.Globalization;
using System.Text;

namespace PawsPost.Features.Assistant;

/// <summary>Builds the text that is embedded for an ad: fixed labeled lines, empty fields left out.</summary>
public static class PetAdEmbeddingText
{
    public static string Build(PetAdSearchText ad)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Title: {ad.Title}");
        builder.AppendLine($"Type: {ad.Type}");
        builder.AppendLine($"Species: {ad.Species}");

        if (!string.IsNullOrWhiteSpace(ad.Breed))
        {
            builder.AppendLine($"Breed: {ad.Breed}");
        }

        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Age in months: {ad.AgeInMonths}"));

        if (!string.IsNullOrWhiteSpace(ad.Location))
        {
            builder.AppendLine($"Location: {ad.Location}");
        }

        builder.Append($"Description: {ad.Description}");
        return builder.ToString();
    }
}
