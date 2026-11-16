namespace PawsPost.Features.Auth;

public enum RegisterOutcome
{
    Success,
    ValidationFailed,
    EmailAlreadyRegistered,
    DisplayNameTaken
}

/// <summary>Outcome of a sign-up, so the controller can pick the status code without holding rules.</summary>
public class RegisterResult
{
    private RegisterResult(RegisterOutcome outcome, UserResponse? user, IReadOnlyDictionary<string, string[]>? errors)
    {
        Outcome = outcome;
        User = user;
        Errors = errors;
    }

    public RegisterOutcome Outcome { get; }

    public UserResponse? User { get; }

    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    public static RegisterResult Success(UserResponse user) => new(RegisterOutcome.Success, user, null);

    public static RegisterResult ValidationFailed(IReadOnlyDictionary<string, string[]> errors) =>
        new(RegisterOutcome.ValidationFailed, null, errors);

    public static RegisterResult EmailAlreadyRegistered() => new(RegisterOutcome.EmailAlreadyRegistered, null, null);

    public static RegisterResult DisplayNameTaken() => new(RegisterOutcome.DisplayNameTaken, null, null);
}
