# Plan: Add public display name, ad creator and public creator profile API (#44)

Status: Approved
Layer: backend

Issue: https://github.com/sarastef98/pawpost/issues/44 (local copy: `docs/issues/ad-creator-profile-backend.md`)
Depends on: #3 (accounts, `AccountService`, `RegisterRequest`), #2 (`GET /api/pet-ads`, `ListPetAdsRequest`, `PetAdListResponse`), #18 (`GET /api/pet-ads/{id}`, `PetAdDetailsResponse`). All of these already exist in the code, so the issue is not blocked by them.
Follow-up: #45 (frontend for the creator profile and the register form field) should ship right after this issue, see risks.
Rules that apply: `CLAUDE.md`, `.claude/rules/creator-profile.md`, `.claude/rules/csharp-conventions.md`, `.claude/rules/migrations.md`, `.claude/rules/testing.md`.

## Goal
Every user has a required, unique public display name. New users choose it in `POST /api/auth/register` (3 to 30 characters after trimming; Unicode letters, Unicode digits, single spaces, hyphen, underscore). Uniqueness is case-insensitive and guaranteed by a unique database index, so two parallel requests cannot both win. Existing users get a generated name "Pet lover <4 digits>" in the same migration, so no user is left without a name. The list (`GET /api/pet-ads`) and details (`GET /api/pet-ads/{id}`) responses show a `creator` object with only the creator's id and display name. A new public endpoint `GET /api/creators/{id}` returns the creator's id, display name and a paginated, newest-first list of their `Active` ads. Email, phone and `UserName` are never in the list, details or profile responses.

Out of scope (from the issue): editing the display name, other profile content, showing non-`Active` ads to anyone, frontend work and regenerating `schema.d.ts`, messaging, adding the creator to the 201 response of `POST /api/pet-ads` and to `GET /api/auth/me`.

## Decisions
Decided by the user on 2026-11-16 (were open questions Q1 to Q3):

- **D1. Backfill in the migration, runtime rules in the service.** The one-time backfill of default names for existing users is a hand-added `migrationBuilder.Sql` step inside the generated migration. The user accepts this exception to `.claude/rules/migrations.md`, the same way as in plan #32 (`LimitPetAdSpeciesToList`). Only that one constant `UPDATE` is added; nothing else in the generated file is edited. All runtime rules (validation, normalization, uniqueness, the 409) live in the service layer (`AccountService` with `DisplayNameRules`), never in the controller or the database layer.
- **D2. Allowed characters.**
  - A letter or digit is any Unicode letter or digit (`\p{L}`, `\p{Nd}`), so Cyrillic and other alphabets are allowed.
  - Spaces are allowed, but only single spaces: two spaces in a row inside the name give a 400. The name is trimmed before every check.
  - Look-alike letters from different alphabets (for example Latin "a" and Cyrillic "а") count as different names. There is no confusable detection.
- **D3. Register response unchanged.** `UserResponse` of `POST /api/auth/register` stays as it is (`Id`, `Email`, the caller's own data). The "no email" tests cover only the list, details and profile responses.

## Approach
Same flow as the rest of the code: thin controllers, rules in services, data through `AppDbContext`.

1. **Display name on the user, case-insensitive uniqueness.** `User` gets two required columns:
   - `DisplayName` (max 30): the name as the user typed it, after trimming. This is what responses show.
   - `NormalizedDisplayName` (max 30): `DisplayName.ToUpperInvariant()`, set by the service. A **unique index** is placed on this column. This is the same pattern Identity already uses for `NormalizedEmail` in this project (see `AppDbContext`).
   - Why not `COLLATE NOCASE` on `DisplayName`: SQLite's `NOCASE` only folds the ASCII letters A to Z. Because non-ASCII letters are allowed (D2), "Ана" and "ана" would count as different names, which breaks the rule. A normalized column works for every letter and does not depend on SQLite collations. It is still enforced by the database, so the race case is covered.
   - `NormalizedDisplayName` is never in any DTO.
2. **Validation of the new register field (400).** `RegisterRequest` gets `DisplayName` (`[Required]`, so a missing or empty value gives a `DisplayName` error). The rest is checked in the existing `Validate` method on the trimmed value: length 3 to 30, only allowed characters, and no two spaces in a row. The rules (min, max, allowed characters, single spaces, trim, normalize) live in one small static class `DisplayNameRules` in `Features/Auth/`, so the request and the service use the same rules. Each failing case gives exactly one `DisplayName` error. Character rule (D2): a regular expression `^[\p{L}\p{Nd}_-]+( [\p{L}\p{Nd}_-]+)*$` on the trimmed value. It allows Unicode letters, Unicode decimal digits, underscore and hyphen, separated by single spaces only; tabs and other whitespace inside the name are not allowed. Length is counted as `string.Length` after `Trim()`. No confusable (look-alike) detection (D2). No new package.
3. **Uniqueness rule in `AccountService` (409).** `RegisterAsync` trims and normalizes the name, then:
   - keeps the existing email check first (unchanged contract from #3);
   - checks `Users.AsNoTracking().AnyAsync(u => u.NormalizedDisplayName == normalized)`; if taken, returns a new outcome `RegisterOutcome.DisplayNameTaken`;
   - creates the user with `DisplayName` and `NormalizedDisplayName` set;
   - in the existing `catch (DbUpdateException)`: if the email now exists, `EmailAlreadyRegistered` (as today); else if the normalized display name now exists, `DisplayNameTaken`; else rethrow, so an unrelated database error is not hidden (the existing behavior).
   - `AuthController.RegisterAsync` maps `DisplayNameTaken` to `Problem(title: "Display name already taken", detail: "Choose a different display name.", statusCode: 409)`. The controller holds no rule, it only maps the outcome. The display name is not written to logs.
   - The 201 body stays `UserResponse` (D3).
4. **Migration with backfill (D1).** One migration, `AddUserDisplayNameAndCreatorAdsIndex`, generated with `dotnet ef migrations add`. The generated parts are: two `AddColumn` calls on `AspNetUsers` (non-null; EF adds them with a default of `''` for existing rows), the unique index `IX_AspNetUsers_NormalizedDisplayName`, and the new `PetAds` index (step 7). Between `AddColumn` and `CreateIndex` one constant `migrationBuilder.Sql` backfill is added by hand (accepted exception, D1):
   ```sql
   UPDATE AspNetUsers
   SET DisplayName = 'Pet lover ' || printf('%04d',
           (SELECT COUNT(*) FROM AspNetUsers AS other WHERE other.Id <= AspNetUsers.Id)),
       NormalizedDisplayName = 'PET LOVER ' || printf('%04d',
           (SELECT COUNT(*) FROM AspNetUsers AS other WHERE other.Id <= AspNetUsers.Id))
   WHERE DisplayName = '';
   ```
   - How uniqueness works: every existing user gets its own position (1, 2, 3 ...) in the order of `Id`, so the numbers are different by construction, no retry loop is needed. This is the "adjusting the number" option of the issue. Before the migration no user has a name, so a generated name cannot collide with a chosen one. Ordering by `Id` (a random Guid) means the number does not reveal registration order.
   - The normalized value is written in upper case directly, so case-insensitive uniqueness holds for generated names too. All characters are ASCII, so no SQLite case function is needed.
   - The generated names follow the D2 rules (letters, digits, single spaces).
   - The unique index is created after the backfill, so it never sees two empty names.
   - After the migration both columns are non-null and filled for every user (criterion "no user without a display name").
   - Limit: 4 digits allow 9,999 existing users. The demo database is far below that (see risks).
   - `Down`: the generated `DropIndex` and `DropColumn` calls; generated names are lost, which is fine because they are derived data.
   - Constant SQL, no user input. `AddPetAds` and all other migrations already in `main` are not edited.
   - This one-time generation is the only place a name is created outside `AccountService`. A migration cannot call a service, and after the migration no user without a name can exist, so no runtime default-name logic is needed (D1).
5. **Creator in list and details.** New DTO `PetAdCreatorResponse(Guid Id, string DisplayName)` in `Features/PetAds/`. `PetAdListItemResponse` and `PetAdDetailsResponse` get a new last property `Creator`. `OwnerId` is not public in these two responses today, so the id is exposed only as `creator.id` (as the issue requires). The `PetAdService` projections add `petAd.OwnerId` and `petAd.Owner!.DisplayName` (EF translates this into a join; only those two columns are read, never email, phone or `UserName`). The comments "Never add owner data here" / "Never add owner email, phone or owner id here" are updated to say that only the public creator object (id and display name) is allowed, per the client decision in #44. Filters, sort, pagination, the `[AllowAnonymous]` on the list and the login requirement on details (#18) do not change.
6. **Public profile.** New feature folder `Features/Creators/`:
   - `CreatorsController`, route `api/creators`, action `GET {id}` with `[AllowAnonymous]` (there is no class-level `[Authorize]` needed, since the controller has only this public action; the attribute makes the intent explicit). It calls `ICreatorService.GetProfileAsync` and returns 200 or 404 `ProblemDetails`.
   - `CreatorAdsRequest` (query `page`, `pageSize`) with the same defaults and limits as #2 (`ListPetAdsRequest.DefaultPageSize` = 20, `ListPetAdsRequest.MaxPageSize` = 100, reused, not copied) and the same `Validate` messages: page below 1 or `pageSize` outside 1 to 100 gives 400 `ValidationProblemDetails`. No species/type filter (not in the issue).
   - `CreatorService.GetProfileAsync(Guid id, CreatorAdsRequest, CancellationToken)`: loads `Id` and `DisplayName` of the user with `AsNoTracking()`; returns null if there is none (404). Otherwise it calls a new `IPetAdService.ListActiveByOwnerAsync(ownerId, page, pageSize, ct)` and returns `CreatorProfileResponse(Guid Id, string DisplayName, PetAdListResponse Ads)`.
   - `PetAdService.ListActiveByOwnerAsync`: `AsNoTracking()`, `Where(OwnerId == ownerId && Status == Active)`, count, then the same newest-first order as the list (`CreatedAt` desc, then `Id` desc), `Skip`/`Take` with the same long-offset guard, and the same item projection as `ListAsync`. The projection is moved into one private method in `PetAdService` so list and profile items have exactly the same public fields (this is required by the issue, not a drive-by refactor). Only `Active` ads are returned, also when the owner is logged in and views their own profile (the service never looks at the caller).
   - A creator with no `Active` ads: 200 with an empty `items` list and `totalCount` 0.
   - An id that is not a Guid: the route uses `{id}` with a `Guid` parameter, the same as the details endpoint, so model binding returns 400 `ValidationProblemDetails`.
7. **Index for the profile query.** `PetAds` gets a composite index `(OwnerId, Status, CreatedAt)` (`IX_PetAds_OwnerId_Status_CreatedAt`), which serves both the filter and the sort. The existing `IX_PetAds_OwnerId` is kept (it is explicitly configured and backs the foreign key; removing it would be a drive-by change).

## API endpoints

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| POST | `/api/auth/register` (changed) | public | `RegisterRequest` + new required `displayName` | `UserResponse` (unchanged, D3) | 201, 400 (`ValidationProblemDetails` on `DisplayName`), 409 (email taken, unchanged; display name taken, new) |
| GET | `/api/pet-ads` (changed) | public (unchanged) | `ListPetAdsRequest` (unchanged) | `PetAdListResponse`; each `PetAdListItemResponse` gets `creator: { id, displayName }` | 200, 400 (unchanged) |
| GET | `/api/pet-ads/{id}` (changed) | logged in (unchanged, #18) | route `id` | `PetAdDetailsResponse` + `creator: { id, displayName }` | 200, 400, 401, 404 (unchanged) |
| GET | `/api/creators/{id}` (new) | public | route `id` (Guid), query `page` (default 1), `pageSize` (default 20, max 100) as `CreatorAdsRequest` | `CreatorProfileResponse { id, displayName, ads: { items, totalCount, page, pageSize } }`, items are `PetAdListItemResponse` | 200, 400 (bad `page`/`pageSize`/id), 404 (`ProblemDetails`, unknown creator) |

No endpoint in this issue changes ad data, so there is no ownership check to add. The register endpoint creates the caller's own account and stays public as in #3.

## Screens and components
not applicable

## Data changes
- Entities and fields:
  - `User`: `DisplayName` (string, required, max 30), `NormalizedDisplayName` (string, required, max 30, unique index `IX_AspNetUsers_NormalizedDisplayName`). Configured in `AppDbContext.OnModelCreating` next to the existing `NormalizedEmail` index.
  - `PetAd`: no new fields. New index `(OwnerId, Status, CreatedAt)`.
  - Backfill for existing users as described in Approach step 4 (D1).
- Migration name: `AddUserDisplayNameAndCreatorAdsIndex` (one migration for the issue). Read the generated file before committing; it must only add columns and indexes (no table rebuild, no drop). If it drops or rebuilds anything, stop and ask.
- After merge, every developer's local database needs `dotnet ef database update --project PawsPost`; until then the app fails on the missing columns.

## Files to create or change
Create:
- `PawsPost/Features/Auth/DisplayNameRules.cs`: min/max length, allowed-character and single-space check, `Trim`, `Normalize` (one place for the rules).
- `PawsPost/Features/PetAds/PetAdCreatorResponse.cs`: record `(Guid Id, string DisplayName)`.
- `PawsPost/Features/Creators/CreatorsController.cs`: `GET /api/creators/{id}`, `[AllowAnonymous]`.
- `PawsPost/Features/Creators/ICreatorService.cs`, `CreatorService.cs`: profile lookup, 404 rule.
- `PawsPost/Features/Creators/CreatorAdsRequest.cs`: `page`, `pageSize`, validation.
- `PawsPost/Features/Creators/CreatorProfileResponse.cs`: record `(Guid Id, string DisplayName, PetAdListResponse Ads)`.
- `PawsPost/Data/Migrations/<timestamp>_AddUserDisplayNameAndCreatorAdsIndex.cs` and `.Designer.cs` (generated, plus the hand-added backfill, D1).

Change:
- `PawsPost/Data/User.cs`: `DisplayName`, `NormalizedDisplayName`.
- `PawsPost/Data/AppDbContext.cs`: column config, unique index, new `PetAds` index.
- `PawsPost/Data/Migrations/AppDbContextModelSnapshot.cs`: generated.
- `PawsPost/Features/Auth/RegisterRequest.cs`: `DisplayName` field and its checks in `Validate`.
- `PawsPost/Features/Auth/RegisterResult.cs`: `RegisterOutcome.DisplayNameTaken` and factory method.
- `PawsPost/Features/Auth/AccountService.cs`: set and check the display name, extend the `DbUpdateException` handling.
- `PawsPost/Features/Auth/AuthController.cs`: map `DisplayNameTaken` to 409.
- `PawsPost/Features/PetAds/PetAdListItemResponse.cs`, `PetAdDetailsResponse.cs`: `Creator` property, updated comments.
- `PawsPost/Features/PetAds/IPetAdService.cs`, `PetAdService.cs`: creator in projections, shared item projection, `ListActiveByOwnerAsync`.
- `PawsPost/Program.cs`: register `ICreatorService` (scoped).

Not changed: `UserResponse` (D3), `GET /api/auth/me`, `PetAdResponse` and `POST /api/pet-ads` (out of scope), `PawsPost.Web` (issue #45), `CLAUDE.md`.

Tests:
- `PawsPost.Tests/Infrastructure/AuthTestHelpers.cs`: `RegisterAsync` gets an optional `displayName`; when not given, a deterministic valid name is derived from the email (for example `"u" + first 12 hex chars of SHA-256(email)`), so existing callers keep working and names stay unique per email without random data.
- `PawsPost.Tests/Infrastructure/PetAdTestHelpers.cs`: `CreateLoggedInClientAsync` / `SeedOwnerAsync` pass the optional display name through; add `GetCreatorAsync(this HttpClient, string id, string? query = null)`.
- `PawsPost.Tests/Features/Auth/RegisterTests.cs`, `LoginTests.cs`, `MeTests.cs`, `LogoutTests.cs`, `LoggingAndPublicAccessTests.cs`, `SwaggerUiTests.cs`: request bodies that post to `/api/auth/register` directly add a valid `displayName` (the field is now required). Assertions are not weakened.
- `PawsPost.Tests/Features/Auth/RegisterRaceTests.cs`: the competing user gets a `DisplayName`/`NormalizedDisplayName` (now required); new race test for the display name.
- `PawsPost.Tests/Features/Auth/RegisterDisplayNameTests.cs` (new).
- `PawsPost.Tests/Features/PetAds/ListPetAdsTests.cs`, `GetPetAdTests.cs`: creator tests added.
- `PawsPost.Tests/Features/Creators/GetCreatorProfileTests.cs` (new).
- `PawsPost.Tests/Features/Data/AddUserDisplayNameMigrationTests.cs` (new, same approach as `LimitPetAdSpeciesMigrationTests`: migrate to `20261116095158_AddPetAdEmbeddings`, insert legacy users with raw SQL, apply the new migration, check).

No new NuGet packages.

## Tests to write
Names follow `Method_Scenario_ExpectedResult`, Arrange / Act / Assert, one reason to fail each. Every test class uses its own `PawsPostFactory` database. Only synthetic users (`a@x.com` style) and ads with fixed `CreatedAt`.

Register, success (`RegisterDisplayNameTests`):
- `Register_ValidDisplayName_Returns201`
- `Register_ValidDisplayName_StoresDisplayName` (checks `DisplayName` and `NormalizedDisplayName` in the database)
- `Register_DisplayNameWithSurroundingSpaces_StoresTrimmedName`
- `Register_DisplayNameWithAllowedCharacters_Returns201` (theory: letters, digits, single space, `-`, `_`, exactly 3 and exactly 30 characters)
- `Register_DisplayNameWithCyrillicLetters_Returns201` (theory: for example `"Ана"`, `"Мария 2"`; D2)
- `Register_DisplayNameWithNonLatinDigits_Returns201` (for example Arabic-Indic digits `"Ana ٣"`; D2: any Unicode decimal digit)
- `Register_DisplayNameWithSingleSpacesBetweenWords_Returns201` (for example `"Pet lover 1"`)
- `Register_DisplayNameCyrillic_StoresUpperCaseNormalizedName` (`"Ана"` stored with `NormalizedDisplayName` `"АНА"`)

Register, validation failure (400 with a single `DisplayName` error, via `AssertBadRequestForOnlyAsync`):
- `Register_DisplayNameMissing_Returns400`
- `Register_DisplayNameEmpty_Returns400`
- `Register_DisplayNameOnlySpaces_Returns400`
- `Register_DisplayNameTooShortAfterTrim_Returns400` (for example `"  ab  "`)
- `Register_DisplayNameTooLong_Returns400` (31 characters)
- `Register_DisplayNameWithDisallowedCharacters_Returns400` (theory: `@`, `.`, `!`, `<`, emoji)
- `Register_DisplayNameWithTwoSpacesInARow_Returns400` (theory: `"Pet  lover"`, `"Ана  Мария"`; D2)
- `Register_DisplayNameWithTabInside_Returns400` (`"Pet\tlover"`; only the plain space is allowed between words)
- `Register_InvalidDisplayName_StoresNoUser`

Register, conflict (409 `ProblemDetails`):
- `Register_DisplayNameTaken_Returns409`
- `Register_DisplayNameTakenWithDifferentCase_Returns409` ("Anna" then "anna")
- `Register_CyrillicDisplayNameTakenWithDifferentCase_Returns409` ("Ана" then "АНА"; proves case-insensitivity beyond ASCII)
- `Register_DisplayNameTakenWithSurroundingSpaces_Returns409` ("Anna" then "  Anna  ")
- `Register_DisplayNameTaken_StoresNoSecondUser`
- `Register_SameDisplayNameSavedByAnotherRequestBeforeInsert_Returns409ProblemDetails` (`RegisterRaceTests`, interceptor inserts a user with the same normalized name and a different email)
- Existing `Register_UnrelatedDatabaseErrorOnInsert_IsNotReportedAsConflict` stays and must still pass.

Register, look-alike names (D2, no confusable detection):
- `Register_LookAlikeNameFromOtherAlphabet_Returns201` (Latin `"Anna"` exists, then `"Аnna"` with a Cyrillic "А" is accepted as a different name)

Register, privacy:
- `Register_WithDisplayName_LogsNoEmailOrDisplayName` (capturing logger, as in `LoggingAndPublicAccessTests`).
- The register 201 body is not checked for "no email" (D3: `UserResponse` is unchanged and holds the caller's own email).

Authorization for register: not applicable (public by design in #3, it creates the caller's own account).

Migration (`AddUserDisplayNameMigrationTests`):
- `AddUserDisplayName_ExistingUsers_GetPetLoverDefaultName` (format `^Pet lover \d{4}$`)
- `AddUserDisplayName_ExistingUsers_GetUniqueNames` (several legacy users, all names distinct, also case-insensitively)
- `AddUserDisplayName_ExistingUsers_GetMatchingNormalizedName`
- `AddUserDisplayName_AfterMigration_NoUserWithoutDisplayName`
- `AddUserDisplayName_AfterMigration_DuplicateNormalizedNameIsRejected` (raw insert of a second user with the same normalized name throws; proves the index exists. This tests our schema rule, not EF.)

List (`ListPetAdsTests`):
- `ListPetAds_ActiveAd_IncludesCreatorIdAndDisplayName`
- `ListPetAds_AnyAd_HasNoEmailPhoneOrUserName` (raw JSON does not contain the owner's email, `phoneNumber`, `userName`, `email`)
- Existing list tests (filters, pagination, anonymous access) stay and must pass.

Details (`GetPetAdTests`):
- `GetPetAd_ActiveAd_IncludesCreatorIdAndDisplayName`
- `GetPetAd_ActiveAd_HasNoEmailPhoneOrUserName`
- `GetPetAd_NotLoggedIn_Returns401` already exists and stays (login rule of #18 unchanged).

Profile (`GetCreatorProfileTests`):
- Success: `GetCreator_NotLoggedIn_Returns200`, `GetCreator_KnownCreator_ReturnsIdAndDisplayName`, `GetCreator_WithActiveAds_ReturnsOnlyThatCreatorsAds` (ads of another owner are not listed), `GetCreator_WithAds_SortsNewestFirst`, `GetCreator_AdItem_HasSameFieldsAsListItem` (including `creator`).
- Only `Active`: `GetCreator_NonActiveAds_AreNotListed` (theory over `Draft`, `Reserved`, `Closed`), `GetCreator_OwnerLoggedIn_StillSeesOnlyActiveAds`.
- Pagination: `GetCreator_NoPageSize_UsesDefaultOf20` (25 ads seeded, 20 items, `totalCount` 25), `GetCreator_SecondPage_ReturnsRemainingAds`, `GetCreator_PageSizeAbove100_Returns400`, `GetCreator_PageBelow1_Returns400`, `GetCreator_PageSizeBelow1_Returns400`.
- Not found and empty: `GetCreator_UnknownId_Returns404ProblemDetails`, `GetCreator_InvalidGuid_Returns400`, `GetCreator_NoActiveAds_ReturnsEmptyListWithTotalCountZero`.
- Privacy: `GetCreator_Response_HasNoEmailPhoneOrUserName`.
- Authorization: the endpoint is public by the client decision, so the "not logged in" test is the success test above; there is no "not the owner" case because nothing is changed and the result is the same for every caller (`GetCreator_OtherLoggedInUser_SeesSameResultAsAnonymous`).

## Steps
Each step is one commit. Branch `feature/44-ad-creator-profile-backend` from `main`, only after the plan is approved.

1. Add `DisplayName` and `NormalizedDisplayName` to `User`, configure them and the unique index plus the `PetAds (OwnerId, Status, CreatedAt)` index in `AppDbContext`, generate `AddUserDisplayNameAndCreatorAdsIndex`, add the backfill SQL (D1), review the generated file. Update `RegisterRaceTests` and the test helpers so the existing suite compiles and stays green (the helper derives a display name; `AccountService` sets a name in the same commit only if needed to keep tests green, otherwise in step 3). Commit: `feat: add public display name to users (#44)`.
2. Add `AddUserDisplayNameMigrationTests`. Commit: `test: cover display name backfill for existing users (#44)`.
3. Add `DisplayNameRules` (Unicode letters and digits, single spaces, trim, normalize; D2), the `DisplayName` field in `RegisterRequest`, the uniqueness rule and `DisplayNameTaken` in `AccountService` / `RegisterResult` / `AuthController`; update existing register request bodies in tests. Commit: `feat: require a unique display name at registration (#44)`.
4. Add `RegisterDisplayNameTests` (including the Cyrillic, Unicode digit, double space and look-alike cases) and the display name race test. Commit: `test: cover display name validation and uniqueness (#44)`.
5. Add `PetAdCreatorResponse`, the `Creator` property in list and details, the shared projection in `PetAdService`, and the list/details tests. Commit: `feat: show the ad creator in list and details (#44)`.
6. Add `ListActiveByOwnerAsync`, `Features/Creators/` (controller, service, request, response), registration in `Program.cs`, and `GetCreatorProfileTests`. Commit: `feat: add public creator profile endpoint (#44)`.
7. Run `dotnet build` (no warnings), `dotnet test`, `dotnet format --verify-no-changes`. Check the diff for email, phone, `UserName` or `NormalizedDisplayName` in any public DTO and for display names or emails in log calls. Run the migration once by hand on a copy of a local database with a few synthetic users to check the generated names. Open the PR with `Closes #44`. The PR description must say: (a) after merge, run `dotnet ef database update --project PawsPost` on the dev database; (b) the frontend register form returns 400 until #45 ships, so #45 should follow right away.
8. After merge (not a commit): run `dotnet ef database update --project PawsPost` on the dev database, then start #45.

## Risks and open questions
Open questions:
None.

Not decided by the issue, plan default (no answer needed unless you disagree):
- Profile route `GET /api/creators/{id}` (the issue leaves the route to the plan).
- Response shape `{ id, displayName, ads: { items, totalCount, page, pageSize } }`.
- 409 title "Display name already taken". When both email and name are taken, the email conflict is reported (the existing check runs first).
- The register 201 body does not get the display name (contract unchanged, D3).

Risks:
- **Frontend register breaks until #45 ships.** After this merge, the current register form does not send `displayName`, so registration from the web app returns 400 until #45 is done and `schema.d.ts` is regenerated. #45 should follow right away.
- **Dev database must be migrated after merge.** The new columns do not exist in an existing local database until `dotnet ef database update --project PawsPost` is run; until then the app fails when it reads users or ads.
- **Accepted rule exception (D1).** The migration contains one hand-added `migrationBuilder.Sql` backfill, an exception to `.claude/rules/migrations.md` approved by the user, as in plan #32. Nothing else in the generated file is edited.
- The 4-digit default name allows 9,999 existing users. Above that `printf('%04d', ...)` produces 5 digits and breaks the format. The demo database is tiny, so this is accepted; check the user count before running the migration on any larger database.
- The backfill numbering uses a correlated `COUNT(*)` per user (quadratic). Fine for a demo-sized table.
- `ToUpperInvariant` does not handle every Unicode case-folding edge case (for example German "ß"). Acceptable for this app now that Unicode letters are allowed (D2).
- Look-alike names from different alphabets (Latin "Anna" and Cyrillic-"А" "Аnna") are accepted as different names (D2), so users could pick names that look identical to someone else's. Accepted by the user.
- Adding `creator` to list and details changes the API contract for the frontend (new field only, no removed field). The issue says regenerating `schema.d.ts` is a separate step.
- Exposing the creator id in public responses lets anyone list all `Active` ads of a user. This is the client's decision in #44 and contains no contact data.
- All existing register calls in tests must send the new field; missing one gives a 400 and a failing test, not a silent pass.

Stop-and-ask rules:
- If the generated migration drops or rebuilds a table or changes a column type, stop and ask.
- If any new NuGet package seems needed, stop and ask.
- If tests still fail after two attempts, stop and report.
