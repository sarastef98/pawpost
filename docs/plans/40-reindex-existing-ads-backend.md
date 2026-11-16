# Plan: Add a re-embedding step for ads without a vector (#40)

Status: Approved
Layer: backend

Issue: https://github.com/sarastef98/pawpost/issues/40 (local copy: `docs/issues/reindex-existing-ads-backend.md`)
Depends on: #35 (`IPetAdSearchIndexer`, `IEmbeddingProvider`, `IVectorStore`, `SqliteVectorStore`, `PetAdEmbedding`, fakes in `PawsPostFactory`). All of these already exist in the code, so this issue is not blocked by #35.
Rules that apply: `CLAUDE.md`, `.claude/rules/assistant.md`, `.claude/rules/csharp-conventions.md`, `.claude/rules/testing.md`.

The open questions from the issue were answered by the user on 2026-11-16 (see "Resolved decisions"). The plan still needs the user's separate approval before implementation.

## Goal
A re-embedding step finds every `Active` ad that has no stored vector, creates its vector through `IPetAdSearchIndexer` (so through `IEmbeddingProvider` and `IVectorStore`, with only the public fields of `PetAdSearchText`), and stores it. Ads that are not `Active` and ads that already have a vector are not sent to the provider. A failure for one ad is counted, logged with only the ad id and exception type, and does not stop the others; that ad stays without a vector so a later run retries it. Running it twice gives the same result as running it once. The step returns a short result (how many ads were found without a vector, how many were indexed, how many failed) with no personal data. After the run, the newly indexed ads are found by `POST /api/assistant/search`.

Out of scope (from the issue): embedding on edit, close and delete; indexing non-`Active` ads; changing the vector store or AI provider; rate limiting the assistant endpoint. Also out of scope (user decision): changing the existing create-time log in `PetAdService.TryIndexAsync`.

## Resolved decisions
Answered by the user on 2026-11-16.

- **D1. Trigger: a command-line mode `reindex-ads` in `Program.cs`** (issue question on the mechanism). Running `dotnet run --project PawsPost -- reindex-ads` builds the app as usual (same configuration, same user-secrets, same DI), runs the step once in a service scope, prints the result to the console and exits. The web server is not started in that mode. No endpoint, so no admin role is needed (none exists: `AppDbContext` is `IdentityUserContext<User, Guid>`, there are no roles).
- **D2. Who may run it:** whoever can start the app with its configuration (and the Gemini key). There is no in-app authorization check.
- **D3. Vectors of older embedding models are not replaced.** "Has a vector" means a stored vector of any model.
- **D4. Quota handling: one ad at a time**, in a fixed order (oldest `CreatedAt` first, then `Id`), no batching, no pause, no maximum per run, and the run carries on after every provider failure.
- **D5. Failures:** only `AiProviderException` (provider down, quota, timeout, missing key) is caught per ad and counted. A database (storage) failure stops the whole run and is not hidden. Cancellation (`OperationCanceledException`) also stops the run.
- **D6. Exit code:** 0 when no ad failed, 1 when at least one ad failed. An unhandled exception (for example a database failure) also ends the process with a non-zero code.
- **D7. In-memory store at every start:** not applicable. `SqliteVectorStore` is the registered store and keeps vectors across restarts.
- **D8. No schema change.** The existing `PetAdEmbeddings` table (primary key `PetAdId`) and `IVectorStore.UpsertAsync` already guarantee at most one vector per ad. No migration.
- **D9. `PetAdService.TryIndexAsync` keeps logging `exception.Message`.** This is flagged as a possible follow-up only and is not changed in this issue. The new step itself logs only the ad id and the exception type, as the issue requires.

## Approach
Same flow as the rest of the code: the trigger only calls a service, the service holds the rules, data access through `AppDbContext` and the existing AI interfaces.

1. **Ask the vector store which ads already have a vector.** `IVectorStore` has no way to ask this today. The service cannot query `PetAdEmbeddings` directly, because then it would bypass `IVectorStore` and would not work with the fake store (or any other store). So `IVectorStore` gets one new method:
   - `GetIndexedIdsAsync(IReadOnlyCollection<Guid> petAdIds, CancellationToken)` returns `IReadOnlySet<Guid>`: the subset of the given ids that have a stored vector (any model, D3).
   - `SqliteVectorStore`: `AsNoTracking()`, `Where(ids.Contains(PetAdId))`, `Select(PetAdId)`. EF Core LINQ only.
   - `FakeVectorStore` implements the same contract.
2. **New service `IPetAdReindexService` / `PetAdReindexService`** in `PawsPost/Features/Assistant/` (next to `IPetAdSearchIndexer`, which it uses), method `ReindexMissingAsync(CancellationToken)` returning `PetAdReindexResult`:
   1. Load the ids of all `Active` ads with `AsNoTracking()`, ordered by `CreatedAt`, then `Id` (D4). Uses the existing `Status` / `(Status, CreatedAt)` indexes.
   2. Ask `IVectorStore.GetIndexedIdsAsync` which of them have a vector. The rest is the work list. Ads that already have a vector are never sent to the provider.
   3. For each id in the work list, load that ad again with `AsNoTracking()`, `Where(Id == id && Status == Active)`, projected straight into `PetAdSearchText` (public fields only: id, title, type, species, breed, age, description, location; never `OwnerId` or the `Owner` row). If it is no longer `Active` (closed between step 1 and now), skip it. Loading one ad at a time keeps memory small and re-checks the status.
   4. Call `IPetAdSearchIndexer.IndexAsync`. On `AiProviderException` (D5) count it as failed and log a warning with only the ad id and `exception.GetType().Name`. Not the exception message, not ad text, not provider details. The ad keeps no vector, so the next run retries it. Any other exception is not caught and stops the run (D5).
   5. Return `PetAdReindexResult(int Missing, int Indexed, int Failed)` and log it once at information level. Counts only, no ids of failed ads in the result (the ids are in the warnings).
   - Idempotent: a second run finds no ad without a vector and calls nothing. `UpsertAsync` is keyed by `PetAdId`, so a second row for the same ad cannot exist.
   - The service uses only `IPetAdSearchIndexer`, `IVectorStore`, `AppDbContext` and `ILogger`. No Gemini types.
3. **Trigger (D1):** `PetAdReindexCommand` in `Features/Assistant/` with `public const string Name = "reindex-ads"` and `RunAsync(IServiceProvider services, TextWriter output, CancellationToken)`: creates a scope, calls `IPetAdReindexService.ReindexMissingAsync`, writes one line such as `Re-indexed 12 of 14 ads without a vector. 2 failed; run again to retry.`, and returns the exit code (D6: 0 when `Failed == 0`, else 1). `Program.cs` after `builder.Build()`: if `args` contains `reindex-ads`, run `PetAdReindexCommand.RunAsync(app.Services, Console.Out, ...)` and return its exit code instead of calling `app.Run()`. The command contains no rules, it only calls the service. Migrations are not applied by the command (same as a normal start); the database must be up to date.

## API endpoints
None. The step is a command (D1), not an endpoint, so there is no new route, DTO or status code, and the 401/403 tests from the issue's last criterion do not apply. Existing endpoints are unchanged. `POST /api/assistant/search` is only used by a test to prove that re-indexed ads become searchable.

## Screens and components
not applicable

## Data changes
- Entities and fields: none (D8). `PetAdEmbedding` and `PetAd` are unchanged.
- Migration name: none.

## Files to create or change
Create:
- `PawsPost/Features/Assistant/IPetAdReindexService.cs`: interface with `ReindexMissingAsync`.
- `PawsPost/Features/Assistant/PetAdReindexService.cs`: the rules described in the approach.
- `PawsPost/Features/Assistant/PetAdReindexResult.cs`: record `(int Missing, int Indexed, int Failed)`.
- `PawsPost/Features/Assistant/PetAdReindexCommand.cs`: the trigger, calls the service, writes one result line, returns an exit code.

Change:
- `PawsPost/Features/Ai/IVectorStore.cs`: add `GetIndexedIdsAsync`.
- `PawsPost/Features/Ai/VectorStores/SqliteVectorStore.cs`: implement it with `AsNoTracking()` and LINQ.
- `PawsPost/Program.cs`: register `IPetAdReindexService` (scoped); handle the `reindex-ads` argument after `builder.Build()` (D1).

Not changed: `PawsPost/Features/PetAds/PetAdService.cs` (D9). `CLAUDE.md` "Commands" could list `reindex-ads`, but `CLAUDE.md` is not changed without the user's approval; the PR description explains how to run it.

Tests:
- `PawsPost.Tests/Infrastructure/Fakes/FakeVectorStore.cs`: implement `GetIndexedIdsAsync`.
- `PawsPost.Tests/Infrastructure/Fakes/FakeEmbeddingProvider.cs`: add an optional `FailWhen` predicate (`Func<string, bool>?`) so only the embedding of one chosen ad fails. Existing `ExceptionToThrow` behavior stays the same.
- `PawsPost.Tests/Infrastructure/AssistantTestHelpers.cs`: `ReindexAsync(this WebApplicationFactory<Program>)` that runs `IPetAdReindexService.ReindexMissingAsync` in a scope and returns the result. Reuse `BuildSearchAd`, `SeedAdsAsync`, `SeedAndIndexAdsAsync`, `SetStatusAsync`, `SeedOwnerAsync`, `CreateLoggedInClientAsync`, `PostAssistantSearchAsync`.
- `PawsPost.Tests/Features/Assistant/ReindexPetAdsTests.cs`: service behavior.
- `PawsPost.Tests/Features/Assistant/PetAdReindexCommandTests.cs`: the trigger.
- `PawsPost.Tests/Features/Assistant/SqliteVectorStoreTests.cs`: tests for `GetIndexedIdsAsync`.

No new NuGet packages.

## Tests to write
Names follow `Method_Scenario_ExpectedResult`, Arrange / Act / Assert. Each test has its own `PawsPostFactory` (own database, own fakes). Ads are synthetic, with fixed `CreatedAt`. Ads "without a vector" are seeded with `SeedAdsAsync` (which does not index), ads "with a vector" with `SeedAndIndexAdsAsync`. No test calls Gemini or needs a key.

Success (`ReindexPetAdsTests`):
- `ReindexMissing_ActiveAdWithoutVector_StoresVector`: afterwards `FakeVectorStore.Contains(id)` is true.
- `ReindexMissing_ActiveAdWithoutVector_BecomesSearchable`: after the run, a logged-in `POST /api/assistant/search` returns the ad.
- `ReindexMissing_ActiveAdWithoutVector_ReturnsIndexedCount`: result is `Missing = 1, Indexed = 1, Failed = 0`.
- `ReindexMissing_NoAds_ReturnsZeroCounts`.

Skipping:
- `ReindexMissing_AdWithVector_IsNotSentToProviderAgain`: `FakeEmbeddingProvider.Calls` holds no new text for that ad after the run (calls counted after seeding).
- `ReindexMissing_NonActiveAd_IsNotIndexed`: theory over `Draft`, `Reserved`, `Closed`; no vector and no provider call.
- `ReindexMissing_AdClosedBeforeItsTurn_IsNotIndexed`: only if it can be tested deterministically; otherwise covered by the status filter in the per-ad query and left out, so no flaky test is written.

Idempotency:
- `ReindexMissing_SecondRun_SendsNothingToProvider`.
- `ReindexMissing_SecondRun_ReturnsZeroMissing`.
- `ReindexMissing_SecondRun_KeepsOneVectorPerAd` (`SqliteVectorStoreTests` level, real store: `PetAdEmbeddings` has one row per ad after two runs).

Failure handling (the "validation failure" equivalent for a step without input):
- `ReindexMissing_OneAdFails_OtherAdsAreStillIndexed`: `FailWhen` matches one ad's title.
- `ReindexMissing_OneAdFails_FailedAdStaysWithoutVector`.
- `ReindexMissing_OneAdFails_ReturnsFailedCount`: `Missing = 2, Indexed = 1, Failed = 1`.
- `ReindexMissing_FailedAd_IsRetriedOnNextRun`: second run with the fake healthy again indexes it.
- `ReindexMissing_ProviderFails_LogsOnlyAdIdAndExceptionType`: with a capturing logger (same approach as the existing `LoggingAndPublicAccessTests`), the warning contains the id and `AiProviderException`, and not the exception message, ad title, description or owner email.
- `ReindexMissing_StorageFails_StopsTheRun`: `FakeVectorStore.UpsertExceptionToThrow` set to a non-provider exception, the exception is thrown and later ads are not indexed (D5).

Privacy:
- `ReindexMissing_EmbeddingInput_HasNoOwnerData`: recorded embedding texts do not contain the owner's email, the owner id or the words `email` / `phone`.

Authorization:
- Not applicable: there is no endpoint and no caller identity (D1, D2).

Vector store (`SqliteVectorStoreTests`):
- `GetIndexedIds_SomeIdsHaveVectors_ReturnsOnlyThose`.
- `GetIndexedIds_EmptyInput_ReturnsEmptySet`.
- `GetIndexedIds_VectorOfOtherModel_CountsAsIndexed` (documents D3).

Command (`PetAdReindexCommandTests`):
- `RunAsync_AllIndexed_ReturnsExitCodeZero`.
- `RunAsync_SomeFailed_ReturnsExitCodeOne` (D6).
- `RunAsync_Output_HasCountsAndNoPersonalData`: the written line contains the counts and no email, title or id.

Existing tests stay unchanged and must still pass.

## Steps
1. After the user approves the plan, create the branch `feature/40-reindex-existing-ads-backend` from `main`.
2. Add `GetIndexedIdsAsync` to `IVectorStore`, implement it in `SqliteVectorStore` and `FakeVectorStore`, add the `SqliteVectorStoreTests`. Commit: `feat: let the vector store report which ads have a vector (#40)`.
3. Add `FailWhen` to `FakeEmbeddingProvider` and the `ReindexAsync` test helper. Commit: `test: support per-ad embedding failures in fakes (#40)`.
4. Add `PetAdReindexResult`, `IPetAdReindexService`, `PetAdReindexService` and its registration in `Program.cs`. Commit: `feat: add re-embedding service for ads without a vector (#40)`.
5. Add the success, skipping and idempotency tests. Commit: `test: cover re-embedding of active ads without a vector (#40)`.
6. Add the failure, logging and privacy tests. Commit: `test: cover re-embedding failures and privacy (#40)`.
7. Add `PetAdReindexCommand` and the `reindex-ads` argument in `Program.cs`, with `PetAdReindexCommandTests`. Run it by hand once against a local database with synthetic ads (with and without a key) to check the output and exit code. Commit: `feat: add reindex-ads command (#40)`.
8. Run `dotnet build` (no warnings), `dotnet test`, `dotnet format --verify-no-changes`. Check the diff for Gemini types outside `Features/Ai/Gemini/` and for logged messages or personal data. Open the PR with `Closes #40`, explaining how to run the command and mentioning the `TryIndexAsync` logging follow-up (D9).

## Risks and open questions
Open questions: none blocking. All questions from the issue are answered (see "Resolved decisions").

Follow-up (out of scope, for a person to decide later):
- `PetAdService.TryIndexAsync` (from #35) logs `exception.Message` on create, while this issue's rule for the new step is id and exception type only (D9). The message is safe by the `AiProviderException` contract. A separate issue can align it if wanted.

Risks:
- Changing `IVectorStore` touches every implementation (one real, one fake). Small, but a future store must implement the new method.
- If an ad is created through `POST /api/pet-ads` while the step runs, both may embed the same ad. `UpsertAsync` reads and then inserts, so two parallel inserts for the same `PetAdId` can hit the primary key and throw a `DbUpdateException`, which stops the run (D5). The result is still one vector per ad; running again finishes the rest. Rare for a manually started command.
- Many ads without a vector means many paid provider calls in one run, with no limit (D4). Fine for demo data; revisit before running on a large real data set.
- Vectors from an older model are kept (D3), so ads with such vectors stay unsearchable after an embedding model change until a separate decision is made.
- `ids.Contains(...)` with a large id list relies on EF Core translating the collection for SQLite. Fine for a demo-sized data set.
- The command builds the full web app (configuration, DI, options validation), so a bad `Ai` or `Assistant` setting also stops the command, which is intended. Anyone who can start the app can run it (D2), so the Gemini key must stay in user-secrets or environment variables only.

Stop-and-ask rules:
- If the command needs a new NuGet package (for example a command-line parsing library), stop and ask.
- If any schema change turns out to be needed, stop and ask; this plan expects none.
- If tests still fail after two attempts, stop and report.
