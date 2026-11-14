using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PawsPost.Data;

namespace PawsPost.Features.Auth;

public class AccountService(UserManager<User> userManager, SignInManager<User> signInManager) : IAccountService
{
    public async Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        // FindByEmailAsync and CreateAsync take no CancellationToken, so we check it once before starting.
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
        // FindByEmailAsync and PasswordSignInAsync take no CancellationToken, so we check it once before starting.
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
        // Read-only query: no tracking, and the token reaches the database call.
        return await userManager.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new UserResponse(user.Id, user.Email!))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        // SignOutAsync takes no CancellationToken, so we check it once before starting.
        cancellationToken.ThrowIfCancellationRequested();

        await signInManager.SignOutAsync();
    }

    private static Dictionary<string, string[]> MapErrors(IEnumerable<IdentityError> errors)
    {
        // Identity messages describe the rule that failed and never contain the password.
        return errors
            .GroupBy(error => FieldFor(error.Code))
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
    }

    // Explicit list of the Identity error codes that belong to a request field. The email is also the user name,
    // so user-name errors go to Email. Any other code is not about one field, so it uses the empty key,
    // which is the standard ModelState key for errors about the request as a whole.
    private static string FieldFor(string code) => code switch
    {
        nameof(IdentityErrorDescriber.PasswordTooShort)
            or nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric)
            or nameof(IdentityErrorDescriber.PasswordRequiresDigit)
            or nameof(IdentityErrorDescriber.PasswordRequiresLower)
            or nameof(IdentityErrorDescriber.PasswordRequiresUpper)
            or nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars)
            or nameof(IdentityErrorDescriber.PasswordMismatch) => nameof(RegisterRequest.Password),
        nameof(IdentityErrorDescriber.InvalidEmail)
            or nameof(IdentityErrorDescriber.DuplicateEmail)
            or nameof(IdentityErrorDescriber.InvalidUserName)
            or nameof(IdentityErrorDescriber.DuplicateUserName) => nameof(RegisterRequest.Email),
        _ => string.Empty
    };
}
