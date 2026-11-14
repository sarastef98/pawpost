namespace PawsPost.Features.Auth;

public interface IAccountService
{
    Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    /// <summary>Signs the user in (sets the auth cookie). Returns null for every kind of failure.</summary>
    Task<UserResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}
