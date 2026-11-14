using Microsoft.AspNetCore.Identity;
using PawsPost.Data;

namespace PawsPost.Features.Auth;

public class AccountService(UserManager<User> userManager) : IAccountService
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
