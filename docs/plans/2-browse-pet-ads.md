# Plan: Browse pet ads without an account (#2)

Status: Approved
Layer: backend

Issue: https://github.com/sarastef98/pawpost/issues/2 (local copy: `docs/issues/browse-pet-ads.md`)
Depends on: #21 (merged: `PetAd.Title` exists), #1 (merged: `POST /api/pet-ads` and the `PetAds` table exist)

## Goal
`GET /api/pet-ads` works without login. It returns one page of `Active` ads, newest first, optionally filtered by `species` (case-insensitive, exact on the trimmed value) and `type` (`Adoption` or `Sale`). The response has `items`, `totalCount`, `page` and `pageSize`. Bad `page`, `pageSize`, `species` or `type` give 400 `ProblemDetails`. A new migration adds an index on status and created date. `POST /api/pet-ads` stays protected (401 when not logged in).

Out of scope (from the issue): messaging, creating/editing/closing ads, the single-ad endpoint (#18), photos, text search, sorting options, favorites, listing non-`Active` ads (also not for their owner), the home page screen (#19), price and location filters.

## Decisions
The user answered "go with your suggestions" for every question. Each recommended default is accepted exactly as proposed. The plan stays `Status: Draft` until a person approves it.

1. Species list for the frontend filter: the client supplies it. It does not block the backend. Species stays free text in the backend, matched exactly but case-insensitively.
2. Maximum page size is 100. The default page size is 20.
3. Species matching is case-insensitive for ASCII letters only (SQLite `NOCASE` and `lower()` only fold ASCII). This limit is accepted for now and documented in the PR description. The user did not say whether non-Latin species (for example Cyrillic) will be used. If the client later needs Cyrillic or other non-Latin species, a normalized species column is a separate future issue, not part of this one.
4. `OwnerId` is left out of the list items.
5. No price or location filters.
6. The buyer-contact question is not part of this issue. No contact field, no change here. It needs a client decision before any contact feature.
7. Names: query parameters `page`, `pageSize`, `species`, `type`. Response `PetAdListResponse` with `items`, `totalCount`, `page`, `pageSize`. Item record `PetAdListItemResponse`.
8. Keep the old single `Status` index (even though the new composite makes it redundant). Add `IX_PetAds_Status_CreatedAt` on `Status` and `CreatedAt`, with no `Id` in the index.
9. `type` is accepted in any case (consistent with `POST`). Numbers and unknown names are rejected with 400.
10. `type` appears as a plain `string` parameter in the generated `schema.d.ts`. Accepted. The frontend narrows the value itself. No custom model binder.
11. Regenerating the frontend `PawsPost.Web/src/api/schema.d.ts` is a separate step after the merge (same approach as #21). #19 (the home page) stays blocked until then. Not part of this plan.
12. `price` is sent as `null` for `Adoption` items (consistent with `PetAdResponse`), not omitted.
13. An empty or whitespace-only `species` means "no species filter".
14. A page beyond the last page returns 200 with empty `items` and the real `totalCount`.

## Approach
Same flow as the existing code: `Controller -> Service -> AppDbContext`, DTOs only, `[ApiController]` automatic 400 `ValidationProblemDetails`.

- Controller: add one `[HttpGet]` action to `PetAdsController` with `[AllowAnonymous]` on that action only. The class keeps `[Authorize]`, so `POST` stays protected. The action binds the query string into a request class with `[FromQuery]`, calls `IPetAdService.ListAsync`, returns 200. No logic in the controller.
- Query string binding: a new class `ListPetAdsRequest` (properties `Page` default 1, `PageSize` default 20, `Species`, `Type`), same pattern as `CreatePetAdRequest` (DataAnnotations plus `IValidatableObject`). A page that is not a number, or does not fit in an int, is rejected by model binding with 400.
  - `Page`: `[Range(1, int.MaxValue)]`. `PageSize`: `[Range(1, MaxPageSize)]` with a public constant `MaxPageSize = 100` and `DefaultPageSize = 20` (decision 2).
  - `Species`: trimmed length checked in `Validate` against the same 50 as `CreatePetAdRequest.SpeciesMaxLength` (reuse the constant). Over 50 gives 400 on `species` only.
  - `Type` is bound as a string, not as `PetAdType?`. MVC's enum binder accepts numbers (`type=1`, even `type=99`), and the issue says numbers must be rejected. `Validate` accepts only the names `Adoption` and `Sale`, in any case (decision 9), and reports one error on `type` for anything else. A small helper on the request class gives the parsed `PetAdType?`, used by `Validate` and by the service, so the rule is in one place.
- Service (`PetAdService.ListAsync`), business rules here:
  1. Start from `dbContext.PetAds.AsNoTracking()` and `Where(Status == Active)`. Non-`Active` ads can never appear.
  2. Apply the `species` filter if present (trimmed, not empty, decision 13) and the `type` filter if present.
  3. `CountAsync` for `totalCount` (the count of ads that match the filters, not of all ads).
  4. Compute the offset as `long` `(page - 1) * pageSize`. If it is at or beyond `totalCount`, return empty `items` without a second query (decision 14). This avoids an int overflow for a huge `page` (`Skip` takes an int) and needs no invented maximum page number.
  5. Otherwise `OrderByDescending(CreatedAt).ThenByDescending(Id)`, `Skip`, `Take(pageSize)`, project to the item fields in the query (only the card columns are read), `ToListAsync`.
  6. Map to `PetAdListItemResponse` after materializing and mark `CreatedAt` as UTC with `DateTime.SpecifyKind(value, DateTimeKind.Utc)`. Reason: SQLite returns `DateTime` with `Kind = Unspecified`, so the JSON would have no `Z` and a browser would read the time as local. The create endpoint is fine because it returns the in-memory value. This needs no model change and no migration.
- Ordering: `CreatedAt` descending, then `Id` descending as a tiebreaker so equal timestamps give a stable order and pages never repeat or skip an ad. `Guid` is stored as text in SQLite, so ordering by it is deterministic.
- Species matching, case-insensitive without string-built SQL: use `EF.Functions.Collate(petAd.Species, "NOCASE") == species` in LINQ. It translates to `"Species" COLLATE NOCASE = @species` with a parameter. No schema change. Effects, to be confirmed while implementing (read the generated SQL with logging or `ToQueryString()` in a throwaway check, not committed):
  - SQLite `NOCASE` folds only ASCII letters. `Dog` matches `dog`, but a non-Latin species such as Cyrillic `Куче` will not match `куче`. `ToLower()` in LINQ (`lower(Species)`) has the same ASCII-only limit in SQLite, so it is no better. This is accepted (decision 3).
  - The existing `IX_PetAds_Species` index is built with the default binary collation, so the `NOCASE` comparison does not use it. The `Status`/`CreatedAt` composite index narrows the rows first, and the species comparison runs on those. That is fine at the expected size. Making the column `NOCASE` in the model would use the index but rewrites the table in the migration and changes every comparison on `Species`, which is more than this issue asks for.
  - Stop-and-ask rule: if `Collate` does not translate as expected on the installed provider, do not improvise. Stop and report. The only pre-approved fallback is comparing `ToLower()` of the column with the lowered input in LINQ (still parameterized, never string-built SQL), noted in the PR. Anything else (raw SQL, a schema change) needs the user.
- Index: add `ad.HasIndex(petAd => new { petAd.Status, petAd.CreatedAt })` in `AppDbContext`. The model snapshot already has single-column indexes `IX_PetAds_CreatedAt`, `IX_PetAds_OwnerId`, `IX_PetAds_Species`, `IX_PetAds_Status`, `IX_PetAds_Type`, so by EF naming the new one is `IX_PetAds_Status_CreatedAt`. The existing single-column indexes are not touched (decision 8).
- Response DTOs (never entities, never owner data):
  - `PetAdListItemResponse(Guid Id, string Title, PetAdType Type, string Species, decimal? Price, string? Location, DateTime CreatedAt)`. `Type` uses `JsonStringEnumConverter<PetAdType>` as in `PetAdResponse`. No `OwnerId` (decision 4), no description, no breed, no age, no status (all items are `Active`). `Price` is `null` for `Adoption` (decision 12).
  - `PetAdListResponse(IReadOnlyList<PetAdListItemResponse> Items, int TotalCount, int Page, int PageSize)`. Non-generic on purpose, so the OpenAPI schema name (and the frontend's generated type) is a readable `PetAdListResponse`, not `PagedResponseOfPetAdListItemResponse`.
  - `Page` and `PageSize` in the response echo the values used.
- OpenAPI: a GET with query parameters and no body, so `NonNullableRequestBodyTransformer` is not involved. The document gets a new `get` operation on `/api/pet-ads` with 200 `PetAdListResponse` and 400 `ValidationProblemDetails` declared through `ProducesResponseType` (same as the POST action). `type` is documented as a plain string parameter because it is bound as a string (decision 10).

## API endpoints

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| GET | /api/pet-ads | public (`[AllowAnonymous]` on the action only) | query: `page` (default 1, min 1), `pageSize` (default 20, 1 to 100), `species` (optional, max 50 trimmed), `type` (optional, `Adoption` or `Sale`) | `PetAdListResponse` | 200, 400 |
| POST | /api/pet-ads | logged in (owner = caller) | `CreatePetAdRequest` | `PetAdResponse` | 201, 400, 401 (unchanged) |

Rules for GET:
- Only `Active` ads. Newest first by `CreatedAt`, ties by `Id` descending.
- 200 with empty `items` and `totalCount` 0 when nothing matches. A page beyond the last page is also 200 with empty `items` and the real `totalCount`.
- `species`: trimmed, matched case-insensitively (ASCII) and exactly (no partial match). An empty or whitespace-only `species` means "no species filter".
- 400 `ValidationProblemDetails` (`application/problem+json`) for: `page` below 1 or not a number, `pageSize` below 1, above 100 or not a number, `species` over 50 characters after trimming, `type` other than `Adoption` or `Sale` in any case (any number included). One error entry per bad parameter.
- No 401 and no 403: the endpoint is public, also with an invalid or missing cookie.
- Never returns owner email, phone or `ownerId`.

## Screens and components
not applicable

## Data changes
- Entities and fields: no new fields and no entity change. One new composite index on `PetAds (Status, CreatedAt)`.
- Migration name: `AddPetAdStatusCreatedAtIndex`. Created with `dotnet ef migrations add AddPetAdStatusCreatedAtIndex --project PawsPost` after the `AppDbContext` change. Expected content: one `CreateIndex` named `IX_PetAds_Status_CreatedAt` on columns `Status` and `CreatedAt`, and a `Down` with the matching `DropIndex`. If the generated file contains anything else (dropped or altered columns, other indexes, anything unexpected), stop and ask. Never hand-edit a migration or the snapshot (`.claude/rules/migrations.md`). Existing migrations are not edited.
- SQLite note: `CREATE INDEX` is supported directly, no table rebuild.

## Files to create or change
- `PawsPost/Data/AppDbContext.cs`: add the `Status` + `CreatedAt` index.
- `PawsPost/Data/Migrations/<timestamp>_AddPetAdStatusCreatedAtIndex.cs`, `.Designer.cs`, `AppDbContextModelSnapshot.cs`: generated by EF only, never hand-written.
- `PawsPost/Features/PetAds/ListPetAdsRequest.cs` (new): query class, constants (`DefaultPageSize`, `MaxPageSize`), validation, type parsing helper.
- `PawsPost/Features/PetAds/PetAdListItemResponse.cs` (new): item record.
- `PawsPost/Features/PetAds/PetAdListResponse.cs` (new): page record.
- `PawsPost/Features/PetAds/IPetAdService.cs`: add `Task<PetAdListResponse> ListAsync(ListPetAdsRequest request, CancellationToken cancellationToken)`.
- `PawsPost/Features/PetAds/PetAdService.cs`: implement `ListAsync` as described above.
- `PawsPost/Features/PetAds/PetAdsController.cs`: add the `[HttpGet]` `[AllowAnonymous]` action with `ProducesResponseType` for 200 and 400. Do not touch the class-level `[Authorize]` or the POST action.
- `PawsPost/Program.cs`, `Common/NonNullableRequestBodyTransformer.cs`: no change expected (the service is already registered).
- `PawsPost.Tests/Infrastructure/PetAdTestHelpers.cs`: add shared helpers: `GetPetAdsAsync(this HttpClient, string? query)`, a `PetAd` builder with fixed values (title, type, species, price, location, status, `CreatedAt`), and a seeding helper that registers an owner user and inserts ads straight into the database through `AppDbContext`. Direct seeding is needed because the API can only create `Active` ads dated "now", so `Draft`, `Reserved`, `Closed` and fixed `CreatedAt` values cannot be produced through `POST`. The `PetAds.OwnerId` foreign key needs a real user.
- `PawsPost.Tests/Features/PetAds/ListPetAdsTests.cs` (new): tests below.
- `PawsPost.Tests/Features/Swagger/OpenApiDocumentTests.cs`: OpenAPI tests below.
- No new NuGet packages.

## Tests to write
All in `ListPetAdsTests` unless noted. Names are `Method_Scenario_ExpectedResult`, each test has its own `PawsPostFactory` (own database), fixed `CreatedAt` values in seeds (no `DateTime.Now` in assertions), and asserts the response content, not only the status.

Success:
- `ListPetAds_ActiveAdsExist_Returns200WithItemsAndPagingValues`: item fields `id`, `title`, `type`, `species`, `price`, `location`, `createdAt` match the seed; `page` 1, `pageSize` 20, `totalCount` equals the number of seeded active ads.
- `ListPetAds_NoAds_Returns200WithEmptyItemsAndZeroTotal`.
- `ListPetAds_DraftReservedAndClosedAds_AreNotListed`: only the `Active` ad is returned and counted in `totalCount`.
- `ListPetAds_AdsWithDifferentDates_ReturnsNewestFirst`.
- `ListPetAds_AdsWithSameCreatedAt_AreOrderedByIdDescending`.
- `ListPetAds_SecondPage_ReturnsRemainingItemsAndFullTotalCount`: for example 5 ads with `pageSize=2&page=2` gives the 3rd and 4th newest, `totalCount` 5, `page` 2, `pageSize` 2. A second check in the same arrange (or a separate test) that page 1 and page 2 do not overlap.
- `ListPetAds_PageBeyondLastPage_Returns200WithEmptyItemsAndRealTotalCount`.
- `ListPetAds_HugePageNumber_Returns200WithEmptyItems`: `page=2147483647` (no overflow, no 500).
- `ListPetAds_PageSizeAtMaximum_Returns200`: `pageSize=100`.
- `ListPetAds_SpeciesInDifferentCase_MatchesIgnoringCase`: stored `Dog`, query `species=dOG`.
- `ListPetAds_SpeciesWithSurroundingSpaces_IsTrimmed`: `species=%20Dog%20` matches.
- `ListPetAds_SpeciesIsOnlyPartOfStoredValue_DoesNotMatch`: `species=Do` does not return `Dog`.
- `ListPetAds_SpeciesEmptyOrWhitespace_ReturnsAllActiveAds` (decision 13).
- `ListPetAds_SpeciesAtMaxLength_Returns200`: 50 characters, also 50 padded with spaces.
- `ListPetAds_TypeAdoption_ReturnsOnlyAdoptionAds` and `ListPetAds_TypeSale_ReturnsOnlySaleAds`.
- `ListPetAds_TypeInDifferentCase_IsAccepted` (decision 9).
- `ListPetAds_SpeciesAndTypeTogether_ReturnsOnlyAdsMatchingBoth`.
- `ListPetAds_FilterMatchesNothing_Returns200WithEmptyItemsAndZeroTotal`.
- `ListPetAds_FilterAndPaging_TotalCountCountsOnlyMatchingAds`.
- `ListPetAds_SaleAd_ReturnsPrice` and `ListPetAds_AdoptionAd_HasNullPrice` (JSON `price` is `null`, decision 12).
- `ListPetAds_Success_JsonUsesStringEnumsAndUtcCreatedAt`: `type` is `"Sale"` not a number, and `createdAt` is a UTC time (read back as `DateTimeKind.Utc`, string ends with `Z`).
- `ListPetAds_Success_ItemsContainOnlyTheCardFields`: the set of item property names is exactly `id`, `title`, `type`, `species`, `price`, `location`, `createdAt`.
- `ListPetAds_Success_ResponseHasNoOwnerEmailPhoneOrOwnerId`: response text does not contain the owner's email, `email`, `phone` or `ownerId`.

Validation failure (each is 400 with `application/problem+json`, error on only the named field, uses a shared assert helper like `AssertBadRequestForOnlyAsync` in `CreatePetAdTests`; move that helper to `PetAdTestHelpers` rather than copying it, or add an equivalent one there):
- `ListPetAds_PageBelowOne_Returns400WithOnlyThatField` (theory: `0`, `-1`).
- `ListPetAds_PageNotANumber_Returns400` (theory: `abc`, `99999999999`).
- `ListPetAds_PageSizeOutOfRange_Returns400WithOnlyThatField` (theory: `0`, `-1`, `101`).
- `ListPetAds_PageSizeNotANumber_Returns400`.
- `ListPetAds_TypeUnknownName_Returns400WithOnlyThatField` (`Barter`).
- `ListPetAds_TypeNumeric_Returns400WithOnlyThatField` (theory: `0`, `1`, `99`).
- `ListPetAds_SpeciesOneOverMaxLength_Returns400WithOnlyThatField` (51 characters).
- `ListPetAds_SeveralInvalidParameters_ReportsEachFieldOnce`.

Authorization:
- `ListPetAds_NotLoggedIn_Returns200`: the endpoint is public.
- `ListPetAds_TamperedCookie_Returns200`: an invalid cookie does not turn a public read into 401.
- `ListPetAds_LoggedInOwnerWithClosedAd_ClosedAdIsNotListed`: non-`Active` ads are not shown even to their owner.
- `CreatePetAd_NotLoggedInAfterPublicList_StillReturns401` (in `ListPetAdsTests`): the same anonymous client gets 200 on `GET /api/pet-ads` and 401 `ProblemDetails` on `POST /api/pet-ads`, and no ad is stored. This guards against `[AllowAnonymous]` spreading beyond the one action.
- The three existing 401 tests in `CreatePetAdTests` (`NotLoggedIn`, `InvalidBodyAndNotLoggedIn`, `TamperedCookie`) must still pass unchanged.
- "Not the owner" does not apply: the endpoint has no owner-only data and changes nothing.

OpenAPI (in `OpenApiDocumentTests`, same helpers as the existing tests):
- `GetOpenApiDocument_PetAdsGet_HasPageSizeSpeciesAndTypeQueryParameters`: the `get` operation on `/api/pet-ads` lists `page`, `pageSize`, `species`, `type` as `in: query`, none required.
- `GetOpenApiDocument_PetAdsGet_ResponseReferencesPetAdListResponse`: the 200 response is a `$ref` to `PetAdListResponse`, and the schema has `items`, `totalCount`, `page`, `pageSize`, all required.
- `GetOpenApiDocument_PetAdListItemResponseSchema_HasCardFieldsAndNoOwnerId`: properties are exactly the card fields, no `ownerId`.
- Existing POST OpenAPI tests stay unchanged and must still pass.

Not tested: that EF can query SQLite, or the index itself (framework behavior). The test factory runs migrations, so every test runs against the new index.

## Steps
1. Create the branch `feature/2-browse-pet-ads` from `main`.
2. Add the `Status` + `CreatedAt` index to `AppDbContext` and generate `AddPetAdStatusCreatedAtIndex`. Read the generated migration (expected: one `CreateIndex`). If anything else appears, stop and ask. Commit: `feat: add status and created date index for pet ads (#2)`.
3. Add `ListPetAdsRequest`, `PetAdListItemResponse`, `PetAdListResponse`, the `IPetAdService.ListAsync` member and the `PetAdService` implementation. Check the generated SQL once for the species comparison (see Approach, including the stop-and-ask rule if `Collate` does not translate). Commit: `feat: add pet ad list service (#2)`.
4. Add the `[HttpGet]` `[AllowAnonymous]` action to `PetAdsController`. Confirm by hand that the class still has `[Authorize]` and that POST is unchanged. Commit: `feat: add browse pet ads endpoint (#2)`.
5. Add the test helpers (seeding, query helper, shared bad-request assert) and the success tests. Commit: `test: cover browsing pet ads (#2)`.
6. Add the validation and authorization tests, including the POST-still-401 test. Commit: `test: cover browse validation and public access (#2)`.
7. Add the OpenAPI tests. Commit: `test: cover browse endpoint in the OpenAPI document (#2)`.
8. Run `dotnet build` (no warnings), `dotnet test`, `dotnet format --verify-no-changes`. Open the PR with `Closes #2`. In the PR description note (a) the ASCII-only case-insensitive species matching (decision 3) and (b) that the frontend's `schema.d.ts` must be regenerated in a separate frontend step (decision 11).

## Risks and open questions
Open questions: none. All were answered, see "Decisions".

Stop-and-ask rules that stay in force:
- If `EF.Functions.Collate` does not translate as expected, stop and report (only the `ToLower()` LINQ fallback is pre-approved, see Approach).
- If EF scaffolds anything unexpected in the migration, stop and ask. Never hand-edit a migration or the model snapshot.
- If an OpenAPI test shows the new operation or schemas are described differently than planned and a fix would need a new package or a behavior change, stop and report.

Risks:
- `[AllowAnonymous]` on the wrong scope would open `POST`. Mitigation: it goes on the GET action only, and a dedicated test checks that POST still returns 401 for the same anonymous client.
- `createdAt` kind: SQLite returns `Unspecified` kind. Mitigated by `SpecifyKind(Utc)` in the service and a test on the JSON.
- Species matching is ASCII-only case-insensitive (decision 3). The user did not say whether non-Latin species will be used. If the client later needs Cyrillic or other non-Latin species, a normalized column is a separate future issue.
- The species comparison does not use `IX_PetAds_Species`. Acceptable at the expected size, the composite index narrows rows first.
- `totalCount` and the page are two queries, so a new ad between them can make a page shifted by one. Acceptable for a public browse list, no transaction planned.
- Anyone, including bots, can page through every active ad. No rate limiting is requested, so none is planned.
- #19 (the home page) is blocked until the frontend `schema.d.ts` is regenerated in the separate step (decision 11). The backend itself is not blocked: user accounts and login exist (#3) and `PetAd.Title` exists (#21).
