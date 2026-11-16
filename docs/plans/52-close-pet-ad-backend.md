# Plan: Add the close pet ad endpoint (#52)

Status: Approved
Layer: backend

Issue: https://github.com/sarastef98/pawpost/issues/52 (local copy: `docs/issues/close-pet-ad-backend.md`)
Depends on: #3 (accounts, login cookie, `ClaimsPrincipalExtensions.GetUserId`), #35 (`IPetAdSearchIndexer.RemoveAsync`, assistant search that only returns `Active` ads). Both already exist in the code, so the issue is not blocked by them. `RemoveAsync` exists but nothing calls it from the app yet; this issue adds the first real caller.
Source: `docs/meeting-notes/2026-11-16-close-ad.md`.
Follow-up: `close-pet-ad-frontend.md` uses this endpoint after `schema.d.ts` is regenerated (separate step, out of scope here).
Rules that apply: `CLAUDE.md`, `.claude/rules/csharp-conventions.md`, `.claude/rules/assistant.md`, `.claude/rules/creator-profile.md`, `.claude/rules/migrations.md`, `.claude/rules/testing.md`.

## Goal
The logged-in owner of an `Active` ad can call `POST /api/pet-ads/{id}/close`. The ad's status becomes `Closed`, the row stays in the database (soft close, which is also how an ad is "deleted"), the response is 204 with no body, and the ad's vector is removed from the assistant search. After that the ad no longer appears in `GET /api/pet-ads`, `GET /api/pet-ads/{id}` (404), `GET /api/creators/{id}`, `GET /api/pet-ads/mine` and the assistant search.

Errors: not logged in gives 401, a malformed id gives 400 (`ValidationProblemDetails`), an unknown id gives 404, a non-owner gets 403 (also when the ad is already `Closed`), and the owner closing a non-`Active` ad gets 409 with a clear message and no change. Two closes at the same time end with one 204 and one 409. If removing the vector fails, the close still returns 204 and only the ad id (and exception type) is logged.

Out of scope (from the issue): screens and dialogs, removing the ad from favorites (belongs to the favorites feature, notes `2026-11-16-favorite-ads`), the owner's "Closed ads" list, any way to set `Reserved`, reopening, a separate delete endpoint or hard delete, editing an ad, regenerating `schema.d.ts`.

## Approach
1. **Route and verb: `POST /api/pet-ads/{id}/close`, no request body.** The issue lets the plan choose. Options compared:
   - `DELETE /api/pet-ads/{id}`: rejected. DELETE says "the resource is gone", but the row stays and a later issue shows closed ads to the owner. DELETE is also expected to be idempotent (a repeated DELETE normally gives 204 or 404), while the issue requires 409 on a second close. It would also suggest a hard delete, which CLAUDE.md forbids.
   - `PATCH /api/pet-ads/{id}` with `{ "status": "Closed" }`: rejected. It needs a body and status validation, and it invites other values (`Active` = reopen, `Reserved`), which the issue forbids. A generic PATCH also overlaps with the future "edit ad" feature.
   - `PUT /api/pet-ads/{id}/close` or `/status`: rejected. PUT is idempotent by definition, but the second call must give 409.
   - `POST /api/pet-ads/{id}/close`: chosen. POST is the standard verb for a non-idempotent action on a resource. It needs no body, the intent is clear, it can only ever move `Active -> Closed`, and the URL stays under `/api/pet-ads` as the issue asks. The action segment `close` is a verb; CLAUDE.md asks for plural nouns for resources, and `AuthController` already uses verb segments for actions with a comment explaining why. The new action gets a similar short comment.
   - Routing: `{id}/close` has two segments, so it does not clash with `GET {id}`, `GET mine` or `POST` (create). `id` is bound as `Guid`, like `GetByIdAsync`.
2. **Authentication.** `PetAdsController` already has a class-level `[Authorize]`; the new action gets no `[AllowAnonymous]`. A missing or invalid cookie gives 401 `ProblemDetails` through the existing `OnRedirectToLogin` handler in `Program.cs`. Authentication runs before model binding, so an anonymous call with a malformed id also gets 401. The caller id is taken with `User.GetUserId()`, never from the route, query or body.
3. **Malformed id.** Same as the details endpoint: a non-Guid `id` fails model binding and `[ApiController]` returns 400 `ValidationProblemDetails` with only the `id` field. Nothing extra to build.
4. **Service rules (in `PetAdService`, behind `IPetAdService`).** New method `Task<ClosePetAdOutcome> CloseAsync(Guid petAdId, Guid callerId, CancellationToken cancellationToken)` and a new enum `ClosePetAdOutcome { Closed, NotFound, NotOwner, NotActive }` in `Features/PetAds/` (same idea as `RegisterOutcome` in the auth feature). Steps, in this order:
   1. Load the ad tracked (no `AsNoTracking`, because it is updated) by id. Not found gives `NotFound`.
   2. `OwnerId != callerId` gives `NotOwner`. This is checked before the status, so a non-owner gets 403 even for a `Closed` ad (issue decision).
   3. `Status != Active` gives `NotActive`. Nothing is changed.
   4. Set `Status = Closed` and `SaveChangesAsync`. This is a normal tracked EF write, no raw SQL, no `ExecuteUpdate`. The row is never removed.
   5. If the save throws `DbUpdateConcurrencyException` (see step 6), return `NotActive`; the other request already closed the ad.
   6. After the save, call `TryRemoveFromIndexAsync(ad.Id, ...)` and return `Closed`.
   Nothing in this method sets `Reserved` or moves a `Closed` ad back.
5. **Controller.** New action `CloseAsync(Guid id, CancellationToken)` with `[HttpPost("{id}/close")]`. It calls `petAdService.CloseAsync(id, User.GetUserId(), cancellationToken)` and maps the outcome only:
   - `Closed` -> `NoContent()` (204, no body)
   - `NotFound` -> `NotFound()` (404; `[ApiController]` turns it into `ProblemDetails`, same as `GetByIdAsync`)
   - `NotOwner` -> `Problem(title: "Not the owner of this ad", statusCode: 403)`
   - `NotActive` -> `Problem(title: "Ad cannot be closed", detail: "Only Active ads can be closed.", statusCode: 409)`
   Attributes: `[ProducesResponseType(StatusCodes.Status204NoContent)]`, `[ProducesResponseType<ValidationProblemDetails>(400)]`, `[ProducesResponseType<ProblemDetails>(401)]`, `403`, `404`, `409`. The titles and detail text are plan defaults (the issue only asks for "a clear message").
6. **Concurrency (two closes at once).** Without protection, both requests could read `Active`, both save `Closed` and both return 204. Fix: mark `PetAd.Status` as an EF concurrency token (`ad.Property(petAd => petAd.Status).IsConcurrencyToken()` in `AppDbContext`). EF then writes `UPDATE ... SET Status = 'Closed' WHERE Id = @id AND Status = 'Active'`. The second request's update matches 0 rows, EF throws `DbUpdateConcurrencyException`, and the service returns `NotActive` (409). Result: exactly one 204, one 409, and the ad is `Closed` once. If the second request starts after the first has committed, it simply reads `Closed` and gets 409 from step 4.3.
   - New concept, in plain words: a concurrency token is a column EF adds to the `WHERE` of an `UPDATE`, with the value it read earlier. If someone else changed that column in between, the update finds no row and EF reports a conflict instead of silently overwriting.
   - Why this and not something else: it keeps the "normal tracked EF write" the issue asks for, works on any database (not only SQLite), and needs no new column. Alternatives considered: a conditional `ExecuteUpdateAsync` (not a tracked write, against the issue wording); relying on SQLite's write lock and an explicit transaction (SQLite-specific, harder to reason about).
7. **Removing the vector.** New private helper `TryRemoveFromIndexAsync(Guid petAdId, CancellationToken)` next to `TryIndexAsync`. It calls `searchIndexer.RemoveAsync(petAdId, cancellationToken)` after the status is saved. Any exception is caught and logged as a warning with the ad id and the exception type name only, for example `"Could not remove pet ad {PetAdId} from the search index ({ExceptionType}). Search already skips closed ads."`. The exception object and its message are not passed to the logger (a database exception message could contain data). The method then returns normally, so the close still gives 204 and the status stays `Closed`.
   - Why catch all exceptions here, when `TryIndexAsync` only catches `AiProviderException`: `RemoveAsync` does not call the AI provider, it only deletes from the vector store (`SqliteVectorStore.RemoveAsync`), so its failures are storage failures. The issue explicitly decides that a failed removal must not fail the close, and the assistant search already filters on `Active` (#35), so a leftover vector is never returned.
   - Cancellation: if the request is cancelled after the save, the close is already committed. The helper also catches that and logs it like any other failure (plan default, see open points).
   - The losing request in a race (409) and every error path (404, 403, 409) do not call `RemoveAsync`.
8. **Visibility after close.** `ListAsync`, `GetByIdAsync`, `ListActiveByOwnerAsync`, `CreatorService` and the assistant search already filter on `Status == Active`. No change there; tests prove it for a closed ad.
9. **Favorites.** Removing a closed ad from favorites lists belongs to the favorites feature (issue out of scope, notes `2026-11-16-favorite-ads`). This plan adds no favorites code and no hook for it.
10. **Logging.** The only new log line is the warning in step 7, with the ad id and exception type. No email, display name, user id, title or description is logged.

## API endpoints

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| POST | `/api/pet-ads/{id}/close` (new) | logged in, and the caller (from the login cookie) must be the ad's owner | route `id` (Guid), no body | none (204 has no body); errors as `ProblemDetails` / `ValidationProblemDetails` | 204 (closed), 400 (`ValidationProblemDetails`, `id` is not a Guid), 401 (`ProblemDetails`, not logged in or invalid cookie), 403 (`ProblemDetails`, not the owner, also when the ad is already `Closed`), 404 (`ProblemDetails`, unknown id), 409 (`ProblemDetails`, owner closing an ad that is not `Active`, ad unchanged) |

Order of checks: 401 (authentication middleware) -> 400 (model binding of `id`) -> 404 (ad does not exist) -> 403 (not the owner) -> 409 (not `Active`) -> 204.

Unchanged: `GET /api/pet-ads`, `GET /api/pet-ads/mine`, `GET /api/pet-ads/{id}`, `POST /api/pet-ads`, `GET /api/creators/{id}`, `POST /api/assistant/search`.

## Screens and components
not applicable

## Data changes
- Entities and fields: no new entity or column. `PetAd.Status` is configured as a concurrency token in `AppDbContext` (`IsConcurrencyToken()`), see Approach step 6. No new index: the close looks up one row by primary key, and the lists that hide closed ads already use the existing `Status` indexes.
- Migration name: `MakePetAdStatusConcurrencyToken`.
  - Why a migration is needed although the table does not change: the concurrency token is part of the EF model, so the model snapshot changes. EF Core refuses to run `Database.Migrate()` (which `PawsPostFactory` and the app use) when the model has changes that no migration covers. The generated migration is expected to have empty `Up` and `Down` and only update `AppDbContextModelSnapshot`.
  - Create it only with `dotnet ef migrations add MakePetAdStatusConcurrencyToken --project PawsPost`, never hand-edit it. Read it before committing: if it contains any table or column operation, stop and ask (migrations rule).

## Files to create or change
Create:
- `PawsPost/Features/PetAds/ClosePetAdOutcome.cs`: enum `Closed`, `NotFound`, `NotOwner`, `NotActive`.
- `PawsPost/Data/Migrations/<timestamp>_MakePetAdStatusConcurrencyToken.cs` and `.Designer.cs`: generated by `dotnet ef`.
- `PawsPost.Tests/Features/PetAds/ClosePetAdTests.cs`: success, validation, authorization, 409, indexer and logging tests.
- `PawsPost.Tests/Features/PetAds/ClosePetAdVisibilityTests.cs`: the closed ad is gone from the list, details, creator profile, my ads and the assistant search.
- `PawsPost.Tests/Features/PetAds/ClosePetAdRaceTests.cs`: concurrency tests (interceptor approach as in `RegisterRaceTests`).

Change:
- `PawsPost/Features/PetAds/IPetAdService.cs`: add `CloseAsync(Guid petAdId, Guid callerId, CancellationToken cancellationToken)`.
- `PawsPost/Features/PetAds/PetAdService.cs`: implement `CloseAsync` (rules in Approach step 4) and the private `TryRemoveFromIndexAsync` helper.
- `PawsPost/Features/PetAds/PetAdsController.cs`: new action `CloseAsync` (`POST {id}/close`) that maps the outcome to 204/403/404/409, with a short comment on the verb choice and that the caller comes from the cookie.
- `PawsPost/Data/AppDbContext.cs`: `IsConcurrencyToken()` on `PetAd.Status`.
- `PawsPost/Data/Migrations/AppDbContextModelSnapshot.cs`: updated by `dotnet ef` only.
- `PawsPost.Tests/Infrastructure/Fakes/FakeVectorStore.cs`: add `RemoveExceptionToThrow` (like `UpsertExceptionToThrow`) so a test can make `RemoveAsync` fail, and a thread-safe `RemovedIds` list so a test can prove `RemoveAsync` was called with the ad id.
- `PawsPost.Tests/Infrastructure/PetAdTestHelpers.cs`: add `ClosePetAdAsync(this HttpClient client, string id)` (posts to `{PetAdsUrl}/{id}/close` with no body) and `GetStoredAdAsync(this WebApplicationFactory<Program> factory, Guid id)`.

Not changed: `CreatorService`, `CreatorsController`, `AssistantService`, `IPetAdSearchIndexer`, `PetAdSearchIndexer`, `SqliteVectorStore`, existing DTOs, `Program.cs` (no new service to register), `PawsPost.Web`. If the diff touches any of these, stop and check why.

No new NuGet packages.

## Tests to write
Names follow `Method_Scenario_ExpectedResult`, Arrange / Act / Assert, one reason to fail each. Each test creates its own `PawsPostFactory` (own database, own fakes). Synthetic users only (`owner@x.com`, `other@x.com`), ads seeded with `BuildPetAd` / `SeedAdsAsync` / `BuildSearchAd` and fixed dates. Logged-in clients from `CreateLoggedInClientAsync`, ids from `GetMyIdAsync` or `SeedOwnerAsync`. The fake embedding provider and fake vector store are used everywhere, so no test reaches a real AI provider.

`ClosePetAdTests`

Success:
- `ClosePetAd_OwnerClosesActiveAd_Returns204WithNoBody` (status 204, empty content)
- `ClosePetAd_OwnerClosesActiveAd_StoresStatusClosed` (stored ad has `Closed`)
- `ClosePetAd_OwnerClosesActiveAd_KeepsAdInDatabase` (row still exists, other fields unchanged)
- `ClosePetAd_OwnerClosesActiveAd_RemovesVectorFromSearchIndex` (ad was indexed before, `VectorStore.Contains(id)` is false after, `RemovedIds` contains exactly the ad id)

Validation:
- `ClosePetAd_IdNotAGuid_Returns400WithOnlyThatField` (theory over `abc`, `123`, `not-a-guid`; uses `AssertBadRequestForOnlyAsync(response, "id")`)

Authorization:
- `ClosePetAd_NotLoggedIn_Returns401ProblemDetails` (status and `application/problem+json`; ad stays `Active`)
- `ClosePetAd_NotLoggedInAndIdNotAGuid_Returns401` (authentication runs before validation)
- `ClosePetAd_TamperedCookie_Returns401` (same approach as `GetPetAd_TamperedCookie_Returns401`)
- `ClosePetAd_NotOwner_Returns403ProblemDetails` (another logged-in user; status and `application/problem+json`)
- `ClosePetAd_NotOwner_LeavesAdActive` (stored status still `Active`, vector still in the store)
- `ClosePetAd_NotOwnerOnClosedAd_Returns403Not409` (another user's ad already `Closed`; ownership comes before status)

Not found and conflict:
- `ClosePetAd_UnknownId_Returns404ProblemDetails`
- `ClosePetAd_OwnerOnAlreadyClosedAd_Returns409ProblemDetails` (status, `application/problem+json`, title "Ad cannot be closed")
- `ClosePetAd_OwnerOnAlreadyClosedAd_LeavesAdUnchanged` (stored ad identical; `RemoveAsync` not called)
- `ClosePetAd_OwnerOnNonActiveAd_Returns409` (theory over `Draft` and `Reserved`, seeded directly; nothing in the app sets them, but the rule is "only `Active`")
- `ClosePetAd_SecondCloseBySameOwner_Returns409` (close once via the API, then again)

Search index failure and logging:
- `ClosePetAd_IndexRemovalFails_StillReturns204` (`VectorStore.RemoveExceptionToThrow` set)
- `ClosePetAd_IndexRemovalFails_AdStaysClosed`
- `ClosePetAd_IndexRemovalFails_ResponseHasNoErrorDetails` (204 body is empty; the exception message does not leak)
- `ClosePetAd_IndexRemovalFails_LogsWarningWithAdIdOnly` (capturing logger as in `ReindexMissing_ProviderFails_LogsOnlyAdIdAndExceptionType`: one warning containing the ad id; no log line contains the exception's secret message, the owner's email, display name or user id, the ad title or description)
- `ClosePetAd_Success_LogsNoPersonalData` (no log line contains the owner's email, display name or user id)

`ClosePetAdVisibilityTests` (each test closes the ad through the API first)
- `ClosePetAd_ClosedAd_IsNotInPublicList` (`GET /api/pet-ads`, another active ad is still listed)
- `ClosePetAd_ClosedAd_DetailsReturn404` (`GET /api/pet-ads/{id}`, also for the owner)
- `ClosePetAd_ClosedAd_IsNotInCreatorProfile` (`GET /api/creators/{ownerId}`)
- `ClosePetAd_ClosedAd_IsNotInMyAds` (`GET /api/pet-ads/mine` for the owner)
- `ClosePetAd_ClosedAd_IsNotReturnedByAssistantSearch` (ad seeded and indexed with `SeedAndIndexAdsAsync`, question that matched it before the close, no longer returned after)
- `ClosePetAd_IndexRemovalFailed_AssistantSearchStillSkipsClosedAd` (removal fails, vector stays, search still does not return the ad because it filters on `Active`)

`ClosePetAdRaceTests`
- `ClosePetAd_ClosedByAnotherRequestBeforeSave_Returns409` (deterministic: a `SaveChangesInterceptor`, as in `RegisterRaceTests`, sets the ad to `Closed` through a separate `AppDbContext` right before the service saves; the concurrency token makes the save fail and the response is 409 `ProblemDetails`)
- `ClosePetAd_ClosedByAnotherRequestBeforeSave_DoesNotCallRemove` (the losing request does not touch the index)
- `ClosePetAd_TwoParallelCloses_OneReturns204AndOneReturns409` (two requests by the owner with `Task.WhenAll`; the set of status codes is exactly {204, 409} whichever wins, and the stored ad is `Closed`)

Existing tests that must keep passing unchanged: `GetPetAdTests`, `ListPetAdsTests`, `ListMyPetAdsTests`, `GetCreatorProfileTests`, the assistant tests (they use `SetStatusAsync`, a normal tracked update that the concurrency token does not affect), `LimitPetAdSpeciesMigrationTests`.

## Steps
Each step is one commit. Branch `feature/52-close-pet-ad-backend` from `main`, only after the plan is approved.

1. Add `ClosePetAdOutcome`, `IPetAdService.CloseAsync`, the implementation in `PetAdService` (load, owner check, status check, tracked save; no index call yet), the `CloseAsync` action in `PetAdsController`, the `ClosePetAdAsync` / `GetStoredAdAsync` test helpers, and the success, validation, authorization, 404 and 409 tests. Commit: `feat: add closing a pet ad by its owner (#52)`.
2. Add `TryRemoveFromIndexAsync` and call it after the save; add `RemoveExceptionToThrow` and `RemovedIds` to `FakeVectorStore`; add the index removal, index failure and logging tests. Commit: `feat: remove a closed ad from the assistant search (#52)`.
3. Mark `PetAd.Status` as a concurrency token in `AppDbContext`, generate the `MakePetAdStatusConcurrencyToken` migration with `dotnet ef` (check that it has no table or column operations), handle `DbUpdateConcurrencyException` as `NotActive`, and add `ClosePetAdRaceTests`. Commit: `feat: reject a second concurrent close with 409 (#52)`.
4. Add `ClosePetAdVisibilityTests` (list, details, creator profile, my ads, assistant search). Commit: `test: prove a closed ad is hidden everywhere (#52)`.
5. Run `dotnet build` (no warnings), `dotnet test`, `dotnet format --verify-no-changes`. Check the diff: no hard delete, no `Reserved`, no reopen path, no raw SQL, no personal data in logs or responses, no changes in `Features/Creators/` or `Features/Assistant/`. Open the PR with `Closes #52`; the description notes that `schema.d.ts` must be regenerated for `close-pet-ad-frontend.md` and that favorites removal is left to the favorites feature.

## Risks and open questions
Open questions:
None that block the work. The issue and the notes decide all behavior; the points below are plan defaults.

Not decided by the issue, plan default (no answer needed unless you disagree):
- Route and verb `POST /api/pet-ads/{id}/close` (reasons in Approach step 1). If the team prefers `DELETE /api/pet-ads/{id}`, only the controller attribute and the tests change, but the 409-on-repeat behavior would then be unusual for DELETE.
- Error wording: 403 title "Not the owner of this ad"; 409 title "Ad cannot be closed" with detail "Only Active ads can be closed." The frontend issue may want to align the text.
- The failed-removal warning includes the exception type name next to the ad id (same as the existing `TryIndexAsync` warning). The issue says "ad id only"; the type name is not personal data, but if it should be dropped, say so.
- Every exception from `RemoveAsync`, including a cancellation after the save, is caught and logged, because the close is already committed and the issue says the close must still succeed.
- No `ClosedAt` field is added; the issue does not ask for it. The later "Closed ads" tab issue may want one for sorting; that issue decides and adds its own migration.

Risks:
- **Migration for a model-only change.** `IsConcurrencyToken()` updates the model snapshot, so a migration is required even though the table does not change. If the generated migration contains any table or column operation, stop and ask before committing.
- **Concurrency token affects future updates.** Every later tracked update of a `PetAd` (for example the future "edit ad" feature) will also include `Status` in its `WHERE`. If an edit and a close race, the edit will get a `DbUpdateConcurrencyException`. That is the correct behavior, but the edit feature must handle it (likely as 409). Worth mentioning in that issue's plan.
- **Leftover vectors.** If removal fails, the vector stays in `PetAdEmbeddings`. The assistant search filters on `Active`, so it is never returned, and the re-index step (#40) only adds missing vectors for `Active` ads. There is no clean-up job for leftovers; none is planned (not in the issue).
- **Existence of another user's ad is visible.** Because 403 comes before the status check (issue decision), a logged-in user can tell that an id belongs to an existing ad (403) rather than an unknown one (404), even for a `Closed` ad that the details endpoint hides with 404. Ids are random Guids, and the issue chose this order explicitly, so it is accepted.
- **Favorites coordination.** The meeting notes say closing removes the ad from favorites and that "the favorites feature and this one must agree on who does it". The issue assigns it to the favorites feature. That feature must therefore either hook into `CloseAsync` or filter non-`Active` ads itself; this plan adds nothing for it.
- **Parallel test timing.** `ClosePetAd_TwoParallelCloses_OneReturns204AndOneReturns409` asserts only the set of outcomes, not which request wins, so it is deterministic. The interceptor test is the precise proof of the concurrency token. If SQLite reports a busy database instead of serializing, the parallel test will show it; stop and report rather than adding retries.
- **Frontend contract.** The new endpoint appears in the OpenAPI document; `schema.d.ts` regeneration is a separate step after merge, and `close-pet-ad-frontend.md` cannot use the endpoint until then.

Stop-and-ask rules:
- If a schema change beyond the empty concurrency-token migration, a change in the creators or assistant feature, or a new package seems needed, stop and ask.
- If tests still fail after two attempts, stop and report.
