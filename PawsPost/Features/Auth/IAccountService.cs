namespace PawsPost.Features.Auth;

public interface IAccountService
{
    Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
}
