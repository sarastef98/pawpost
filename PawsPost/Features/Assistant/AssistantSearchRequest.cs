using System.ComponentModel.DataAnnotations;

namespace PawsPost.Features.Assistant;

public class AssistantSearchRequest : IValidatableObject
{
    public const int QuestionMaxLength = 500;

    [Required]
    public string? Question { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Counted after trimming. A missing or whitespace-only question is reported by [Required].
        if (Question?.Trim().Length > QuestionMaxLength)
        {
            yield return new ValidationResult(
                $"The Question field must be at most {QuestionMaxLength} characters.", [nameof(Question)]);
        }
    }
}
