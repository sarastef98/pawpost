# Plan: Create a pet ad (#1)

Status: Approved

Issue: https://github.com/sarastef98/pawpost/issues/1 (local copy: `docs/issues/create-pet-ad.md`)

## Goal
A logged-in user can post an ad for an animal (`Adoption` or `Sale`). The ad is stored as `Active` with the caller as owner, and the API answers 201 with a response that never exposes the owner's email or phone. Anyone who is not logged in gets 401.

Confirmed decisions (from the user, 2026-11-14): plan from the local issue file as written. A photo is not required and photo upload is out of scope. New ads start `Active`. `Adoption` ads have no price. `Sale` ads need price > 0.

Out of scope (do not build): photo upload, browsing/listing/getting ads, editing, closing, messaging.

## Approach
Follow the existing Auth feature: `Controller -> Service -> AppDbContext`, request and response DTOs, DataAnnotations plus `IValidatableObject` on the request (same pattern as `RegisterRequest`), and `[ApiController]` automatic 400 `ValidationProblemDetails`.

- `PetAdsController` is thin and `[Authorize]`. It reads the caller's id with the existing `User.GetUserId()` (`Common/ClaimsPrincipalExtensions.cs`, NameIdentifier claim) and calls `IPetAdService.CreateAsync`.
- `PetAdService` holds the business rules: trim text, force `Status = Active`, set `OwnerId` from the caller (never from the request body), keep `Price` only for `Sale`, set `CreatedAt` from an injected `TimeProvider` (built into .NET, no new package, so tests can use a fixed time).
- The owner is never taken from the request. There is no "owner id" field in the request DTO, so a client cannot create an ad for someone else.
- Ownership check for this endpoint: create only needs authentication, because the caller becomes the owner. The "not the owner" case applies to edit and close, which are other issues.

Assumptions used so the plan can proceed (each is listed again under open questions, and each is a small change):
- `Breed` and `Location` are optional, max 100 characters each.
- `Species` is free text, 1 to 50 characters after trimming.
- `Age` is a whole number of months, field `AgeInMonths`, range 0 to 360.
- `Description` is 1 to 2000 characters after trimming.
- `Price` has at most 2 decimals, > 0, and at most 1,000,000. There is no currency field.
- An `Adoption` request that includes a price is rejected with 400 (not silently ignored).

## API endpoints
| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| POST | /api/pet-ads | logged in (owner = caller) | `CreatePetAdRequest` | `PetAdResponse` | 201, 400, 401 |

`CreatePetAdRequest` (JSON, camelCase):
- `type`: string, required, `Adoption` or `Sale`. Bound as a string enum; numeric values and unknown names are rejected with 400.
- `species`: string, required, 1-50.
- `breed`: string, optional, max 100.
- `ageInMonths`: integer, required, 0-360. It must be required in a way that a missing value is not read as 0 (use `int?` with `[Required]`).
- `description`: string, required, 1-2000.
- `price`: decimal, optional. Required and > 0 when `type` is `Sale`. Must be absent (null) when `type` is `Adoption`.
- `location`: string, optional, max 100.

`PetAdResponse`: `id`, `type`, `species`, `breed`, `ageInMonths`, `description`, `price` (null for Adoption), `location`, `status` (always `Active` on create), `ownerId`, `createdAt` (UTC). It must not contain owner email, phone, or any other user data. `ownerId` is only the opaque Guid, already visible to the owner via `/api/auth/me`.

Responses:
- 201 with `PetAdResponse` in the body. There is no `Location` header yet because there is no "get one ad" endpoint (browsing is a separate issue). Add the header when that endpoint exists.
- 400 `ValidationProblemDetails` for missing or invalid fields, bad enum, malformed JSON, price rules.
- 401 `ProblemDetails` when not logged in (already produced by the cookie events in `Program.cs`, no redirect).

## Data changes
- Entities and fields:
  - New enums in `Data/`: `PetAdType { Adoption, Sale }` and `PetAdStatus { Draft, Active, Reserved, Closed }`. All four statuses are defined now because `CLAUDE.md` defines them. Only `Active` is used by this issue. Stored as strings so the database is readable and reordering the enum cannot corrupt data.
  - New entity `Data/PetAd.cs`: `Id` (Guid), `Type`, `Species` (max 50, required), `Breed` (max 100, nullable), `AgeInMonths` (int), `Description` (max 2000, required), `Price` (decimal?, nullable), `Location` (max 100, nullable), `Status`, `OwnerId` (Guid, required, FK to `User`, delete behavior `Restrict` so deleting a user never silently deletes ads), `CreatedAt` (`DateTime`, UTC).
  - Why `DateTime` and not `DateTimeOffset`: the SQLite EF provider cannot sort by `DateTimeOffset`, and the later browse feature will sort by created date.
  - Photos are not modelled in this issue.
  - `AppDbContext`: add `DbSet<PetAd> PetAds`, enum-to-string conversions, max lengths, required fields, `Price` precision (`decimal(18,2)`), FK to `User`.
  - Indexes (per `CLAUDE.md`, for the later browse filters and sorting): `Species`, `Type`, `Status`, `CreatedAt`, and `OwnerId` (needed for the FK).
- Migration name: `AddPetAds`. Created with `dotnet ef migrations add AddPetAds --project PawsPost`. The existing `AddIdentityUsers` migration is not edited.
- A database check constraint for the price rule is not added. The rule lives in the service (`CLAUDE.md`: business rules not in the database layer).

## Files to create or change
- `PawsPost/Data/PetAd.cs`: new entity.
- `PawsPost/Data/PetAdType.cs`, `PawsPost/Data/PetAdStatus.cs`: new enums.
- `PawsPost/Data/AppDbContext.cs`: add `PetAds`, mapping and indexes.
- `PawsPost/Data/Migrations/<timestamp>_AddPetAds.cs`, `.Designer.cs`, and `AppDbContextModelSnapshot.cs`: generated by EF.
- `PawsPost/Features/PetAds/CreatePetAdRequest.cs`: request DTO with DataAnnotations and `IValidatableObject` for the type and price rules.
- `PawsPost/Features/PetAds/PetAdResponse.cs`: response DTO (record).
- `PawsPost/Features/PetAds/IPetAdService.cs`: `Task<PetAdResponse> CreateAsync(Guid ownerId, CreatePetAdRequest request, CancellationToken cancellationToken)`.
- `PawsPost/Features/PetAds/PetAdService.cs`: the rules listed under Approach.
- `PawsPost/Features/PetAds/PetAdsController.cs`: `[ApiController]`, `[Route("api/pet-ads")]`, `[Authorize]`, `POST`, with `ProducesResponseType` attributes like `AuthController`.
- `PawsPost/Program.cs`: register `IPetAdService` (scoped) and `TimeProvider.System` (singleton).
- `PawsPost.Tests/Features/PetAds/CreatePetAdTests.cs`: tests below.
- `PawsPost.Tests/Infrastructure/PetAdTestHelpers.cs`: shared helpers, for example a valid request body builder and "register, log in, return client". Reuse `AuthTestHelpers.RegisterAsync`/`LoginAsync` and `CreateApiClient`. Do not copy helpers into test classes (`.claude/rules/testing.md`).
- No new NuGet packages.

## Tests to write
All in `CreatePetAdTests`, each with its own `PawsPostFactory` (own database), names like `CreatePetAd_PriceMissingOnSale_ReturnsBadRequest`. Assert behavior, and for writes check the stored data through `AppDbContext` from `factory.Services`.

Success:
- `CreatePetAd_ValidAdoption_Returns201AndActiveAd`: status 201, response `type` Adoption, `status` Active, `price` null, fields match the request.
- `CreatePetAd_ValidSale_Returns201WithPrice`: price returned and stored.
- `CreatePetAd_ValidRequest_StoresLoggedInUserAsOwner`: stored `OwnerId` equals the id from `/api/auth/me`.
- `CreatePetAd_OwnerIdInBody_IsIgnored`: an extra `ownerId`/`status` in the JSON does not change the owner or status (the second user's id is not used, status stays Active).
- `CreatePetAd_OptionalBreedAndLocationOmitted_Returns201`.
- `CreatePetAd_TextWithSurroundingSpaces_IsTrimmed`.
- `CreatePetAd_Success_ResponseHasNoEmailOrPhone`: the response text does not contain the owner's email.

Validation failure (each returns 400 with `application/problem+json` and the field name in `errors`):
- `CreatePetAd_SpeciesMissing`, `..._AgeMissing` (proves missing is not read as 0), `..._DescriptionMissing`, `..._TypeMissing`, plus whitespace-only species and description.
- `CreatePetAd_PriceMissingOnSale`, `..._PriceZeroOnSale`, `..._PriceNegativeOnSale`.
- `CreatePetAd_PriceOnAdoption_ReturnsBadRequest` (based on the stated assumption).
- `CreatePetAd_PriceMoreThanTwoDecimals` and `..._PriceAboveMaximum`.
- `CreatePetAd_UnknownTypeName`, `..._NumericType`.
- `CreatePetAd_SpeciesTooLong`, `..._DescriptionTooLong`, `..._BreedTooLong`, `..._LocationTooLong`, `..._AgeNegative`, `..._AgeAboveMaximum` (boundary values: the max length is accepted, max+1 is rejected).
- `CreatePetAd_MalformedJson_ReturnsBadRequest`.
- A rejected request stores no ad.

Authorization:
- `CreatePetAd_NotLoggedIn_Returns401ProblemDetails_NoRedirect`, and no ad is stored.
- `CreatePetAd_TamperedCookie_Returns401`.
- "Not the owner" does not apply to create (the caller becomes the owner). The owner-based tests belong to the edit and close issues. The test `CreatePetAd_OwnerIdInBody_IsIgnored` covers the equivalent risk here.

## Steps
1. Add the enums, `PetAd` entity, and `AppDbContext` mapping and indexes. Generate the `AddPetAds` migration and check it by reading it. Commit: `feat: add PetAd entity and migration (#1)`.
2. Add `CreatePetAdRequest`, `PetAdResponse`, `IPetAdService`, and `PetAdService`, and register them in `Program.cs`. Commit: `feat: add pet ad service (#1)`.
3. Add `PetAdsController` with `POST /api/pet-ads`. Commit: `feat: add create pet ad endpoint (#1)`.
4. Add test helpers and the success tests. Commit: `test: cover creating pet ads (#1)`.
5. Add the validation and authorization tests. Commit: `test: cover pet ad validation and authorization (#1)`.
6. Run `dotnet build` (no warnings), `dotnet test`, and `dotnet format --verify-no-changes`. Fix anything. Open the PR with `Closes #1`.

## Risks and open questions
Resolved:
- "How do users sign up and log in?" is answered by issue #3 (merged): `POST /api/auth/register`, `POST /api/auth/login`, cookie authentication, `[Authorize]`, and `User.GetUserId()`. This plan builds on it. Nothing needs to be created for accounts.

Open questions from the issue. None of them blocks starting. Each uses the assumption stated, and the client should confirm before the PR is merged:
1. Are breed and location required? Does not block. Assumption: both optional (max 100). Changing it means one attribute on the request DTO and `IsRequired()` in the mapping plus a new migration if the column must become non-null.
2. Is species a fixed list or free text? Does not block. Assumption: free text, 1-50 characters. Moving to a fixed list later needs a data decision for ads already stored (this is the costlier one to change later, so ask the client early).
3. Which currency is used for the price? Does not block. Assumption: a single unspecified currency, no currency field, no currency in the response. If the client wants multiple currencies or a displayed symbol, a field and migration are added later.
4. Description length limits? Does not block. Assumption: 1-2000 characters.
5. Age format? Does not block for the backend, but this is the most likely to change. Assumption: whole months in `ageInMonths`, 0-360. The issue only says "age" and gives no unit or format. If the client wants years, a birth date, or free text such as "2 years", the DTO, the column, and the tests change.

Questions the issue does not mention but the plan had to decide (confirm with the client):
6. Should an `Adoption` request that includes a price be rejected (400) or should the price be ignored? The issue says "do not include a price". Assumption: reject with 400. One condition in the validator and one test to change.
7. Maximum price and decimal places (assumed 1,000,000 and 2 decimals).

Other risks:
- SQLite stores `decimal` as text. That is fine for storing and returning a price, but sorting or filtering by price in the later browse issue needs care (convert in the query or choose another representation then).
- A valid login cookie can outlive a deleted user (see `Me_ValidCookieButUserDeleted_Returns401`). Creating an ad then fails on the foreign key and returns 500. Nothing in the issue covers it, and users cannot be deleted by any current endpoint. Handling it is left out and noted here. If the reviewer wants it handled, the service can check that the owner exists and return 401.
- No rate limiting or duplicate-ad protection: not requested in the issue, so not planned.
- No `Location` header on 201 until a "get one ad" endpoint exists.
