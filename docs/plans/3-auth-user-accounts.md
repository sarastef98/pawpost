# Plan: Add user accounts and authentication (#3)

Issue: https://github.com/sarastef98/pawpost/issues/3

Status: Approved

## Goal
A visitor can sign up with an email and password, log in, and later requests are recognised as that user through an HttpOnly cookie. A logged-in user can log out. A `User` entity with a stable id exists. Services and controllers can read the logged-in user's id from a claim. Protected endpoints answer 401 (never a redirect) with `ProblemDetails`. This unblocks issue #1 (`docs/plans/1-create-pet-ad.md`), which needs the `User` entity and the user id claim.

## Approach
Decisions already made by the developer:
- **Cookie authentication** (HttpOnly cookie), not JWT.
- **ASP.NET Core Identity** with EF Core stores. The NuGet package `Microsoft.AspNetCore.Identity.EntityFrameworkCore` is approved.
- **Logout** is part of this version (`POST /api/auth/logout`).
- **`GET /api/auth/me`** is part of this version (confirmed).
- **Password minimum length 8** (developer decision, pending client confirmation).

How it fits the project:
- Use Identity's building blocks (`UserManager<User>`, `SignInManager<User>`, password hashing, lockout, cookie handler) but write our own thin controller and service. We do not use `MapIdentityApi`, because its responses and error shapes do not match our rules (thin controllers, `ProblemDetails`, our status codes, no extra endpoints like email confirmation or password reset that the client has not decided).
- Flow: `AuthController` -> `IAccountService` -> `UserManager` / `SignInManager` -> `AppDbContext`. Business rules (duplicate email check, mapping Identity errors, login result) live in `AccountService`.
- `AppDbContext` changes to inherit from `IdentityUserContext<User>` (users, claims, logins, tokens tables only). We choose this over `IdentityDbContext` because roles are out of scope, so no roles tables are created.
- `User` extends `IdentityUser<Guid>`. A Guid id is stable, unique and not guessable. Email is also the user name. Identity already stores only a password hash, never the password.
- Cookie scheme: `AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies()`, plus `AddIdentityCore<User>().AddEntityFrameworkStores<AppDbContext>().AddSignInManager()`.
- The user id claim is `ClaimTypes.NameIdentifier` (what Identity sets). A small helper `GetUserId()` in `Common/` reads it so nobody repeats claim-type strings.

### Password rules (developer decision, pending client confirmation)
- Minimum length 8 (`Password.RequiredLength = 8`), instead of Identity's default 6.
- Identity default character rules kept: at least one uppercase, one lowercase, one digit and one non-alphanumeric character.
- Maximum 128 characters, enforced in `RegisterRequest` (stops huge-password hashing abuse).

### 401 instead of a redirect (important)
Cookie authentication by default redirects to `/Account/Login` (a 302), which is wrong for an API. We configure `ConfigureApplicationCookie` so that:
- `OnRedirectToLogin` sets status 401 and writes a `ProblemDetails` body through `IProblemDetailsService` (no redirect).
- `OnRedirectToAccessDenied` sets 403 with `ProblemDetails` (not used yet, needed by later owner checks).
- `AddProblemDetails()` is registered so other errors (400 from model validation, unhandled errors) also use that format.

### Cookie settings and CSRF
Cookie authentication is automatically sent by the browser, so CSRF (a malicious site making the browser send a request with the user's cookie) must be considered.
- `HttpOnly = true` (JavaScript cannot read the cookie, limits damage from XSS). Because of this, only the server can remove the cookie, which is why logout is an endpoint.
- `SecurePolicy = Always` (HTTPS only). `UseHttpsRedirection` already exists.
- `SameSite = Lax` (**ASSUMPTION**, see risks). The browser does not send the cookie on cross-site POST/PUT/DELETE requests, which blocks classic CSRF. This also protects logout (a POST, so another site cannot log a user out silently) and means GET endpoints must never change data (REST rule already followed).
- Controllers use `[ApiController]` and JSON bodies. Non-JSON content types are rejected with 415, so a cross-site HTML form cannot post to the auth endpoints.
- Fixed cookie name (for example `pawspost.auth`) so it is predictable in the frontend and tests.
- **Not added now:** CORS and antiforgery tokens. The frontend does not exist, so we do not know if it will be on the same site as the API. If it ends up on a different site, `SameSite=None` plus CORS plus an antiforgery or custom-header check would be required. This needs a decision when the frontend is planned. Note for the pet ad plan: its create endpoint is `multipart/form-data` (form-like), so it relies on `SameSite` staying `Lax` or stricter.

## API endpoints
Base route `/api/auth`. Auth actions are not a plural-noun resource, so verbs are used for these; this is a deliberate exception to the plural-noun rule.

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| POST | /api/auth/register | public | `RegisterRequest` | `UserResponse` | 201, 400, 409 |
| POST | /api/auth/login | public | `LoginRequest` | `UserResponse` + `Set-Cookie` | 200, 400, 401 |
| POST | /api/auth/logout | logged in | none | none | 204, 401 |
| GET | /api/auth/me | logged in | none | `UserResponse` | 200, 401 |

Notes:
- `GET /api/auth/me` returns only the caller's own data. It is also the protected endpoint used to prove the 401 rule and the user id claim in tests.
- `POST /api/auth/logout` signs the caller out with `SignInManager.SignOutAsync()`, which sends a `Set-Cookie` that expires the auth cookie. It returns 204 with no body. Without a valid cookie the cookie handler answers 401 (`ProblemDetails`, no redirect).
- Read endpoints for ads stay public (`[AllowAnonymous]` is the default, no global authorization policy is added).
- Ownership: these endpoints only act on the caller's own account or create a new one. There is no other user's resource, so 403 and "not the owner" do not apply (stated deliberately). They will matter in the pet ad edit/close issues.
- Register does not log the user in (the notes say "sign up, then log in"). **ASSUMPTION.**
- No `Location` header on 201 (there is no `GET /users/{id}` and none is requested).

### DTOs
`RegisterRequest`
- `Email`: required, trimmed, max 254 chars, valid email format (checked with `System.Net.Mail.MailAddress` parsing in addition to DataAnnotations, because `[EmailAddress]` alone is weak).
- `Password`: required, 8 to 128 chars. Character rules (upper, lower, digit, non-alphanumeric) are enforced by Identity.

`LoginRequest`
- `Email`, `Password`: both required (non-empty). Missing or empty gives 400. Any other bad value (unknown email, wrong password, malformed email, locked out) gives the same 401, so nothing about the account is revealed.

`UserResponse`
- `Id` (Guid), `Email`. Always the caller's own data (on register: the new account; on login and me: the logged-in user). Never password, hash, security stamp, phone or another user's data.

Errors:
- 400: `ValidationProblemDetails` with field errors, including Identity password rule failures (mapped from `IdentityResult` errors by the service result). Error messages must not echo the password.
- 409: `ProblemDetails` "email already registered".
- 401: `ProblemDetails` with a generic title such as "Invalid credentials" (login) or "Authentication required" (protected endpoints).

## Data changes
- Entities and fields:
  - `User : IdentityUser<Guid>` in `Data/User.cs`. No extra fields (no display name or phone collected, see assumptions). Inherited fields used: `Id`, `Email`, `NormalizedEmail`, `UserName`, `NormalizedUserName`, `PasswordHash`, `SecurityStamp`, `ConcurrencyStamp`, lockout fields. Unused inherited fields (`PhoneNumber`, `EmailConfirmed`, two-factor) stay null/false and are never returned.
  - `AppDbContext : IdentityUserContext<User, Guid>`. Identity tables created: `AspNetUsers`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens` (default names; renaming is not worth the churn).
  - Unique index on normalized email: Identity's `UserNameIndex` (unique on `NormalizedUserName`) already covers it because user name = email. Additionally set `RequireUniqueEmail = true` so Identity rejects duplicates, and add a unique index on `NormalizedEmail` in `OnModelCreating` (Identity's default email index is not unique) so the database also guarantees it.
  - SQLite stores Guids as TEXT. Fine for now.
- Migration name: `AddIdentityUsers` (`dotnet ef migrations add AddIdentityUsers --project PawsPost`). This is the first migration in the repo (none exists yet). The `AddPetAds` migration from issue #1 must come after it. Review the generated SQL before committing.

## Files to create or change
Paths are relative to the `PawsPost` project unless noted.
- `PawsPost.csproj`: add `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (approved by the developer), same version family as the other packages (10.0.x, currently 10.0.15). **No other package is planned.** See "Packages" in risks.
- `Data/User.cs`: the entity.
- `Data/AppDbContext.cs`: change base class, add the unique email index.
- `Data/Migrations/<timestamp>_AddIdentityUsers.cs` (+ designer + snapshot): generated by EF.
- `Features/Auth/AuthController.cs`: `register`, `login`, `logout`, `me`. Thin: receives the request, calls the service, maps the result to a status code. `[ApiController]`, `[Route("api/auth")]`, `[Authorize]` on `logout` and `me`.
- `Features/Auth/IAccountService.cs`, `Features/Auth/AccountService.cs`: `RegisterAsync`, `LoginAsync`, `LogoutAsync`, `GetCurrentUserAsync`, all with `CancellationToken` where applicable. Rules: duplicate email check (and handle Identity's duplicate error for the race condition, both to 409), map password failures to 400, login via `SignInManager.PasswordSignInAsync` with `lockoutOnFailure: true`, same failure result for every login failure, logout via `SignInManager.SignOutAsync`.
- `Features/Auth/AccountResult.cs` (or similar small result types): outcome returned by the service so the controller picks the status code without holding rules.
- `Features/Auth/RegisterRequest.cs`, `LoginRequest.cs`, `UserResponse.cs`: DTOs.
- `Common/ClaimsPrincipalExtensions.cs`: `GetUserId()` reading `ClaimTypes.NameIdentifier`; throws or returns null if missing (decide in implementation, covered by a test).
- `Program.cs`: register Identity (password options: `RequiredLength = 8` with the default character rules, lockout, `RequireUniqueEmail`), cookie scheme and cookie options, `ConfigureApplicationCookie` events (401/403 as `ProblemDetails`), `AddProblemDetails()`, `IAccountService`; add `app.UseAuthentication()` before the existing `app.UseAuthorization()`. Settings that are likely to change (cookie lifetime) go in an `Auth` section of `appsettings.json` bound to an options class, no secrets.
- `appsettings.json`: `Auth` section (cookie lifetime). No secrets are needed for cookie auth. Data Protection keys are discussed in risks.
- `PawsPost.Tests/Infrastructure/PawsPostFactory.cs`: `WebApplicationFactory<Program>` subclass. Uses a separate SQLite database file per factory instance (temp folder, deleted on dispose) by overriding `ConnectionStrings:DefaultConnection`, and applies migrations at startup in tests only (this also proves the migration works). Client options: `BaseAddress = https://localhost` (the cookie is `Secure`, the test cookie container would not send it over http) and `AllowAutoRedirect = false`.
- `PawsPost.Tests/Features/Auth/AuthControllerTests.cs`: API tests.
- `PawsPost.Tests/Features/Auth/ClaimsPrincipalExtensionsTests.cs`: small unit tests for `GetUserId()`.
- Existing `PawsPost.Tests/Features/Health/HealthControllerTests.cs` should keep working unchanged. Leave it as is.
- `PawsPost.Tests/UnitTest1.cs` is the template placeholder; leave it (not part of this issue).

## Tests to write
xUnit with plain `Assert`, names `Method_Scenario_ExpectedResult`, Arrange/Act/Assert, each test gets its own factory/database and registers its own users (no order dependency). No new test packages.

`POST /api/auth/register`
- Success:
  - `Register_ValidRequest_Returns201WithIdAndEmail`
  - `Register_ValidRequest_StoresHashedPasswordNotPlainText` (read the user from the database in the test: hash is not equal to the password and not empty)
  - `Register_ValidRequest_ResponseDoesNotContainPasswordOrHash`
  - `Register_EmailDifferentCase_TreatedAsSameEmail` (sign up `A@x.com` then `a@x.com` gives 409)
  - `Register_PasswordExactlyEightChars_Returns201` (boundary of the new minimum)
- Validation failure (400, `application/problem+json`):
  - Missing email, empty email, malformed email, email too long
  - Missing password, password of 7 chars (too short), password missing each character class (upper, lower, digit, non-alphanumeric), password too long (129)
  - `Register_NonJsonContentType_Returns415`
- Conflict:
  - `Register_EmailAlreadyRegistered_Returns409ProblemDetails`
- Authorization: public endpoint. Stated deliberately: not-logged-in and not-owner cases do not apply to a public create. The success test already runs with no cookie.

`POST /api/auth/login`
- Success:
  - `Login_ValidCredentials_Returns200AndSetsAuthCookie`
  - `Login_ValidCredentials_CookieIsHttpOnlySecureAndSameSite` (inspect `Set-Cookie` header)
  - `Login_EmailDifferentCase_Succeeds`
- Validation failure (400): missing email, missing password, empty body.
- Authentication failure (401, `ProblemDetails`):
  - `Login_WrongPassword_Returns401`
  - `Login_UnknownEmail_Returns401`
  - `Login_WrongPasswordAndUnknownEmail_ReturnSameBody` (no account enumeration: same status and same problem body)
  - `Login_AfterTooManyFailures_Returns401EvenWithCorrectPassword` (lockout, same response shape)
  - No `Set-Cookie` header on failure.
- Authorization: public endpoint, same reasoning as register.

`POST /api/auth/logout`
- Success:
  - `Logout_LoggedIn_Returns204AndClearsCookie` (response has a `Set-Cookie` that expires the auth cookie, empty body)
  - `Me_AfterLogout_Returns401` (log in, call `/me` = 200, log out, call `/me` again with the same client = 401 `ProblemDetails`)
  - `Logout_ThenLoginAgain_Works`
- Authorization:
  - `Logout_NotLoggedIn_Returns401ProblemDetails_NoRedirect`
  - `Logout_TamperedCookie_Returns401`
  - Not-owner case: not applicable (logout only affects the caller's own session; there is no other resource), stated deliberately.
- Validation failure: no input, not applicable (stated deliberately).

`GET /api/auth/me`
- Success:
  - `Me_AfterLogin_ReturnsOwnIdAndEmail` (id equals the registered user's id, proving the id claim works)
  - `Me_TwoUsers_EachSeesOnlyOwnData` (user B's cookie never returns user A's email)
- Authorization:
  - `Me_NotLoggedIn_Returns401ProblemDetails_NoRedirect` (status is exactly 401, no `Location` header, body is `ProblemDetails`)
  - `Me_TamperedCookie_Returns401`
- Validation failure: no input, not applicable (stated deliberately).

Cross-cutting
- `PublicEndpoint_NoCookie_StillReachable` using the existing health endpoint (read endpoints stay public).
- Log check: a test with a capturing logger provider asserts that after register, failed login and logout, no log message contains the email or the password.

## Steps
1. Create branch `feature/3-auth-user-accounts` from `main`. Set this plan to `Status: Approved` after the developer reviews it. (`chore`)
2. Add the package `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, add `User`, change `AppDbContext`, add the unique email index, generate migration `AddIdentityUsers`, review it, run `dotnet ef database update` locally. Build passes without warnings. (`feat: add User entity and Identity tables (#3)`)
3. Add the test infrastructure (`PawsPostFactory`, temp database, https base address) with one smoke test that starts the app and applies migrations. (`test: add API test factory (#3)`)
4. Register Identity (min password length 8, default character rules, lockout), cookie scheme and options in `Program.cs`, add `UseAuthentication()`, `AddProblemDetails()`, cookie events returning 401/403 `ProblemDetails`. Add `ClaimsPrincipalExtensions.GetUserId()` with unit tests. (`feat: configure cookie authentication (#3)`)
5. Add DTOs, `IAccountService` / `AccountService.RegisterAsync` and `POST /api/auth/register` with its tests (success, validation, 409). (`feat: add sign-up endpoint (#3)`)
6. Add `LoginAsync` and `POST /api/auth/login` with tests (success, cookie flags, 400, 401, no enumeration, lockout). (`feat: add login endpoint (#3)`)
7. Add `GET /api/auth/me` with tests (success, two users, 401 without redirect, tampered cookie). (`feat: add current user endpoint (#3)`)
8. Add `LogoutAsync` and `POST /api/auth/logout` with tests (204 and cleared cookie, 401 when not logged in, `/me` returns 401 after logout, login again works). (`feat: add logout endpoint (#3)`)
9. Add the no-personal-data-in-logs test and the public-endpoint test. Check by hand that no code logs request bodies or emails. (`test: verify no personal data in logs (#3)`)
10. Run `dotnet build` (no warnings), `dotnet test`, `dotnet format --verify-no-changes`. Open the PR from the template with `Closes #3`.

## Risks and open questions
Decided by the developer (no longer open): cookie instead of JWT (so token lifetime, refresh token and signing key questions are dropped); ASP.NET Core Identity with `Microsoft.AspNetCore.Identity.EntityFrameworkCore`; logout is included; `GET /api/auth/me` is included; password minimum length is 8.

Pending client confirmation:
- **Password rules:** minimum 8 and the Identity default character rules (upper, lower, digit, non-alphanumeric), max 128. This is a developer decision; the client has not confirmed it.

Assumptions in this plan (the issue leaves them open; I picked the conservative option, please confirm or change):
- **Lockout:** Identity defaults (5 failed attempts, 5 minute lockout), enabled on login. A locked-out login returns the same 401 as a wrong password. Confirm the client accepts this.
- **No display name or phone** collected at sign-up. Only email and password. Nothing beyond the user's own email is ever returned, and only to that user.
- **Email is the user name**, compared case-insensitively, trimmed. No email verification (`EmailConfirmed` stays false and is not enforced).
- **No Google login, email verification, password reset, roles** (all unanswered, all out of scope in the issue). If email verification becomes required before posting, it will need an email-sending service and probably a new package: a future issue, and it needs approval then.
- **Register does not auto-login.**
- **Cookie lifetime:** session cookie (not persistent across browser restarts) with Identity's default 14-day sliding expiry, kept in configuration. No "remember me" option. Confirm.
- **`SameSite=Lax`** and no CORS for now (see CSRF section). Revisit when the frontend hosting is decided.
- **Guid user id** (`IdentityUser<Guid>`). Issue #1's plan assumed an int or Guid ad id and an `OwnerId` foreign key; `PetAd.OwnerId` must be a `Guid` to match.
- **Verb routes** (`/api/auth/register`, `/api/auth/login`, `/api/auth/logout`) instead of plural nouns.
- **Account deletion** stays out of scope (the issue asked about logout and deletion; only logout was decided).

Technical risks:
- **Logout only clears the cookie.** `SignOutAsync` removes the cookie from the browser but does not invalidate a copy of the cookie someone already stole; it stays valid until it expires. Rotating the security stamp on logout would invalidate it, but that also affects other sessions of the same user and Identity only re-checks the stamp periodically (default every 30 minutes). Not planned; note for later if needed.
- **Packages:** only `Microsoft.AspNetCore.Identity.EntityFrameworkCore` is requested, and it is approved. `Microsoft.AspNetCore.Identity` itself is part of the ASP.NET Core shared framework, and the test project already has `Microsoft.AspNetCore.Mvc.Testing`, so no other package is needed. If a validation library or similar is wanted later, ask first.
- **Data Protection keys:** the cookie is encrypted with ASP.NET Core Data Protection keys. By default they live on the local machine. In production (restarts, several servers) keys must be stored persistently and shared, otherwise all users are logged out or logins fail. Not solved here; needs a decision at deployment. No secrets are committed.
- **Account enumeration on sign-up:** the issue requires 409 for an existing email, which by nature tells a visitor that an email is registered. Login does not reveal it. Acceptable per the issue; consider rate limiting later (not planned, no package or feature added).
- **Timing differences** between unknown email and wrong password on login can leak account existence. Identity does not fully hide this. Low risk for now; note only.
- **Brute force:** lockout slows guessing per account only. There is no per-IP rate limiting in this plan.
- **Logging:** Identity and ASP.NET Core can log user ids but should not log emails or passwords. `EnableSensitiveDataLogging` must stay off. The planned log test covers our own code only. Keep default log levels.
- **Migration:** this is the first migration. `AddPetAds` (issue #1) depends on it. Once merged to `main` it must not be edited; fixes go in a new migration.
- **`Program.cs` overlap with issue #1:** both plans touch `Program.cs` (`AddProblemDetails`, `UseAuthentication`). Whichever is built first adds them; the second should not duplicate. Issue #1 is blocked until this lands.
- **Tests and Secure cookie:** test clients must use an `https://` base address or the cookie is not sent back. Included in the factory design.
- **`IdentityUserContext` choice:** if roles are ever needed, switching to `IdentityDbContext` means a migration that adds roles tables. Acceptable.
- Out of scope, not planned: Google login, email verification, password reset, roles, profile editing, account deletion, messaging, anything about pet ads, including enforcing ad ownership.
