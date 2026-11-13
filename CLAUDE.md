# PawPost

A marketplace for posting ads to **adopt out or sell animals**.

```
client meeting notes -> GitHub issue -> dev plan -> implementation -> tests -> PR review -> code review
```

## Stack

- Backend: C#, ASP.NET Core Web API (controllers) on .NET 10 LTS (`src/PawPost.Api`)
- Data access: EF Core with SQLite for now, schema changes through EF migrations
- Tests: xUnit, FluentAssertions, `WebApplicationFactory` for API tests (`tests/PawPost.Api.Tests`)
- Frontend, built after the backend: React + TypeScript + Vite (`src/PawPost.Web`)
- Source control: GitHub. Issues and PRs are the source of truth for work.

Nothing is built yet. Everything arrives through the pipeline, one small feature at a time.

## Commands

```bash
dotnet build
dotnet test
dotnet format --verify-no-changes
dotnet ef migrations add <Name> --project src/PawPost.Api
dotnet ef database update --project src/PawPost.Api
```

## Domain (defaults, change when the client says otherwise)

- A **PetAd** has: type (`Adoption` or `Sale`), species, breed, age, description, price (required for `Sale`, absent for `Adoption`), location, photos, status (`Draft`, `Active`, `Reserved`, `Closed`), owner.
- Only the owner can edit or close their ad. Everyone can browse `Active` ads.
- Contact details are private until the design says otherwise. Never expose an owner's email or phone in a public response.
- Business rules live in services, never in controllers or the database layer.

## Architecture

Keep it simple. This is a straightforward CRUD app, so no CQRS and no extra layers unless a real need appears.

- Organize by feature. Folders inside `PawPost.Api`:
  - `Features/PetAds/` holds the controller, service, interface, DTOs and validator for that feature
  - `Data/` holds `AppDbContext`, entities and migrations
  - `Common/` holds shared helpers
- Request flow: Controller -> Service -> `AppDbContext`. Controllers stay thin: receive the request, call the service, return the result.
- Never return EF entities from the API. Use separate request and response DTOs.
- API shape: REST, plural nouns (`/api/pet-ads`), correct status codes (200, 201, 204, 400, 401, 403, 404, 409), errors as `ProblemDetails`.
- Lists are always paginated and have a maximum page size.

## Most important rules

### Security (never skip)
- Every endpoint that changes data requires authentication, and checks that the caller owns the resource. Authentication alone is not enough.
- Validate all input on the server: lengths, ranges, enums, required fields. Never trust the client.
- Use EF Core LINQ or parameterized queries. No string-built SQL.
- No secrets, tokens or connection strings in code or in git. Use user-secrets locally and environment variables in production.
- File uploads (photos): check size, content type and extension, generate your own file names, never use the uploaded name for a path.
- Don't log personal data (emails, phones, tokens).

### Code quality
- Nullable reference types on. Fix warnings, don't suppress them.
- Async all the way, methods end in `Async`, pass `CancellationToken`.
- Constructor injection. Services have interfaces.
- Naming: `PascalCase` public, `_camelCase` private fields, `I` prefix for interfaces.
- No new NuGet or npm package without asking first and explaining why.
- Change only what the issue asks for. No drive-by refactors.

### Database
- Schema changes only through EF migrations. Never edit a migration that is already in `main`.
- Use `AsNoTracking()` for read-only queries.
- Add indexes for fields used in filters and sorting (species, type, status, created date).
- Soft close ads (status `Closed`), don't hard-delete them.

### Testing
- Tests ship in the same PR as the code. No green tests, no PR.
- Every endpoint gets: a success test, a validation-failure test, and an authorization test (not logged in, and not the owner).
- Test names: `Method_Scenario_ExpectedResult`. Use Arrange / Act / Assert.
- Tests must not depend on each other or on test order.

### Git and workflow
- No work without a GitHub issue. No code without an approved plan in `docs/plans/`.
- One issue = one branch = one PR. Branch: `feature/<issue#>-<slug>`.
- Never commit to `main`. Never force-push.
- Commits: `type: summary (#issue)`, types: feat, fix, test, refactor, docs, chore.
- PR description includes `Closes #<issue>`.

## Working with me

- The developer is learning. Work in small steps and explain new concepts in a few plain sentences: what and why.
- Never invent requirements. If something is unclear, ask, or list it under "Open questions" in the issue.
- Before big changes, show the plan and wait for approval.
- If a task fails twice, stop and explain what you tried instead of retrying forever.

## Definition of done

1. `dotnet build` has no warnings.
2. `dotnet test` is green.
3. `dotnet format --verify-no-changes` is clean.
4. New behavior has tests, including authorization cases.
5. PR is opened from the template and links the issue.

## Where things live

- `.claude/agents/` subagents
- `.claude/rules/` extra rules, some scoped by file path
- `.claude/skills/` reusable workflows, invoked as `/name`
- `.claude/hooks/` automatic guards
- `.mcp.json` MCP servers (GitHub)
- `templates/` meeting notes, issue, PR and plan templates
- `docs/meeting-notes/` raw client notes (pipeline input)
- `docs/plans/` development plans, one per issue
