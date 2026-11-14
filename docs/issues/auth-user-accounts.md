Issue: #3 https://github.com/sarastef98/pawpost/issues/3

# Add user accounts and authentication

## Summary
A visitor can sign up with an email and password, then log in, and the API can identify the logged-in user on later requests. A `User` entity with a stable id exists so other features (starting with pet ads) can reference an owner.

## User story
As a visitor, I want to sign up with my email and password and log in, so that I can post an ad as myself.

## Acceptance criteria
- [ ] A `User` entity exists with a stable unique id, created through an EF migration.
- [ ] A visitor can sign up by sending an email and a password.
- [ ] Sign-up with invalid or missing input (for example malformed email, missing password) returns 400 as `ProblemDetails`.
- [ ] Sign-up with an email that is already registered returns 409 as `ProblemDetails`.
- [ ] A registered user can log in with email and password and receives whatever the chosen mechanism uses to prove identity on later requests.
- [ ] Login with wrong credentials returns 401 as `ProblemDetails`, without revealing whether the account exists.
- [ ] An authenticated request exposes the user's id as a stable user id claim that services and controllers can read.
- [ ] A request to a protected endpoint without valid authentication returns 401.
- [ ] Passwords are never stored in plain text and never returned in any response.
- [ ] No response from these endpoints exposes another user's email or phone.
- [ ] Personal data (email, password, tokens) is not written to logs.
- [ ] Browsing ads stays possible without an account (no authentication required on read endpoints).
- [ ] Tests cover success, validation failure and authorization cases for each new endpoint.

## Out of scope
- Login with Google (client has not decided).
- Email verification (client has not decided).
- Password reset (client has not decided).
- Enforcing "only the person who posted an ad can edit or close it". This belongs to the pet ad edit/close issues. This issue only provides the authenticated user id they need.
- Creating, editing, closing, or browsing pet ads (separate issues).
- Messaging between buyer and owner ("maybe later" in earlier notes).
- Roles/admin, profile editing, account deletion (not requested; add later only if the client asks).

## Open questions
Questions marked BLOCKS PLAN must be answered before a development plan can be written.

- BLOCKS PLAN: Which authentication mechanism: cookie or JWT bearer? The frontend is React and built later, so this affects it.
- BLOCKS PLAN: ASP.NET Core Identity or a custom implementation? Identity may need a new NuGet package (for example the EF Core store), and JWT bearer needs a package too. Packages need approval before use, so the developer must approve them and the reason.
- Password rules: minimum length and complexity? Any lockout after failed attempts?
- Is a display name or phone number collected at sign-up? The notes only mention email and password. Contact details stay private for now, so if collected, when and to whom may they be shown?
- Will Google login, email verification and password reset be needed, and if so in which version? The client has not decided. If email verification is required, must it happen before a user can post ads?
- Are roles needed (for example admin or moderator), or are all users equal?
- If JWT: token lifetime, and is a refresh token needed? Where does the signing key live (user-secrets locally, environment variable in production)?
- Should a user be able to log out or delete their account in the first version?

## Source
Client meetings of 13 Nov (`docs/meeting-notes/2026-11-13-first-meeting.md`) and 14 Nov (`docs/meeting-notes/2026-11-14-accounts.md`). On 13 Nov the client said users post ads and anyone can browse without an account. On 14 Nov the client confirmed that people must have an account to post an ad, browsing stays public, and users sign up with email and password and then log in. Only the person who posted an ad can edit or close it. Login with Google, email verification and password reset are not decided yet. This issue is also needed now because issue #1 (create a pet ad, `docs/plans/1-create-pet-ad.md`) is blocked: it needs an authenticated user with a stable user id claim and a `User` entity that `PetAd.OwnerId` can reference by foreign key.
