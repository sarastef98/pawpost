using Microsoft.AspNetCore.Identity;
using PawsPost.Data;

namespace PawsPost.Features.Auth;

public class AccountService(UserManager<User> userManager, SignInManager<User> signInManager) : IAccountService
{
    public async Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var email = request.Email.Trim();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return RegisterResult.EmailAlreadyRegistered();
        }

        // The email is also the user name. Identity stores only a hash of the password.
        var user = new User { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, request.Password);

        if (result.Succeeded)
        {
            return RegisterResult.Success(new UserResponse(user.Id, email));
        }

        // Another request may have registered the same email between our check and CreateAsync.
        if (result.Errors.Any(error => error.Code is nameof(IdentityErrorDescriber.DuplicateEmail)
            or nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return RegisterResult.EmailAlreadyRegistered();
        }

        return RegisterResult.ValidationFailed(MapErrors(result.Errors));
    }

    public async Task<UserResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return null;
        }

        // lockoutOnFailure counts failed attempts. Unknown email, wrong password and lockout all give null,
        // so the response never reveals anything about the account.
        var result = await signInManager.PasswordSignInAsync(user, request.Password, isPersistent: false, lockoutOnFailure: true);

        return result.Succeeded ? new UserResponse(user.Id, user.Email!) : null;
    }

    public async Task<UserResponse?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByIdAsync(userId.ToString());

        return user is null ? null : new UserResponse(user.Id, user.Email!);
    }

    private static Dictionary<string, string[]> MapErrors(IEnumerable<IdentityError> errors)
    {
        // Identity messages describe the rule that failed and never contain the password.
        return errors
            .GroupBy(error => error.Code.StartsWith("Password", StringComparison.Ordinal)
                ? "Password"
                : error.Code.Contains("Email", StringComparison.Ordinal) ? "Email" : string.Empty)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
    }
}
