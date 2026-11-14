# Plan: Add a required title to pet ads (#21)

Status: Approved
Layer: backend

Issue: https://github.com/sarastef98/pawpost/issues/21 (local copy: `docs/issues/add-pet-ad-title-backend.md`)
Depends on: #1 (merged: `POST /api/pet-ads` exists)

## Goal
`PetAd`, `CreatePetAdRequest` and `PetAdResponse` have a required `title`. `POST /api/pet-ads` rejects a missing, blank or too-long title with 400 `ProblemDetails`, stores the trimmed title, and returns it in the 201 response. The OpenAPI document shows `title` as required in the request and in the response. The existing tests of #1 keep passing.

Out of scope (from the issue): editing the title, search by title, the list and details endpoints (#2, #18), and any frontend change (including regenerating `src/api/schema.d.ts`, see decision 6).

## Decisions
The user answered "go with your suggestions" for every open question and confirmed that no real ads exist anywhere (only local development databases). Each recommended default is accepted exactly as proposed.

1. Maximum title length: 100 characters, checked on the trimmed text (same as breed and location).
2. Existing rows: the migration adds the column with the empty-string default that EF scaffolds, and the model has no default, so new ads always need a title. The user confirmed that no real ads exist, so no data fix and no placeholder title are needed. Step 6 (manual migration check on a copy of an old local database) stays only as an optional check, if an old local database exists.
3. The title is public, shown to everyone including visitors who are not logged in, like the other fields.
4. The empty default is expressed by relying on EF's scaffolded `defaultValue: ""` in `AddColumn`, with no `HasDefaultValue` in `OnModelCreating`. If EF does not scaffold it, stop and ask. Do not hand-edit the migration (`.claude/rules/migrations.md`).
5. OpenAPI and DTOs stay in sync: write the two OpenAPI tests first. Fix the transformer only if they fail, with no new package and no behavior change. `Title` stays `string?` in the request, so a missing title still gives one clean `title` error. If it cannot be fixed cleanly, stop and report.
6. Frontend: after this PR is merged, open a separate frontend issue to regenerate `PawsPost.Web/src/api/schema.d.ts` and adapt the frontend. That is not part of this plan. #19 needs it.
7. `Title` is the second field of the `PetAdResponse` record, after `Id`.
8. A blank or missing title reuses the standard `[Required]` message. No custom message.

## Approach
Follow the way #1 already handles the other text fields, so nothing new is invented:
- Request: `[Required] public string? Title { get; init; }`, the same pattern as `Species` and `Description`. `[Required]` already rejects missing, empty and whitespace-only text with one error on `title`. The length rule is checked in `Validate` on the trimmed text, with a new `TitleMaxLength = 100` constant and the existing `TooLong` helper.
- Service: `Title = request.Title!.Trim()` in `PetAdService.CreateAsync`, and `ad.Title` added to the positional `PetAdResponse`.
- Response: add `string Title` to the `PetAdResponse` record, right after `Id`. Because the record is built positionally, the service and any test that constructs it must be updated together (the compiler will point to them).
- Entity and mapping: `public string Title { get; set; } = string.Empty;` in `PetAd` (same style as `Species`) and `ad.Property(p => p.Title).HasMaxLength(100).IsRequired()` in `AppDbContext`. No default value is configured in the model.
- Migration: generated with `dotnet ef migrations add AddPetAdTitle --project PawsPost`. For a new required string column EF Core scaffolds `AddColumn` with `defaultValue: ""` by itself. That empty default exists only in the migration, so the migration is not hand-edited. The generated file is read before committing (decision 4).
- No index on `Title`: search by title is out of scope, and `CLAUDE.md` only asks for indexes on fields used in filters and sorting.

## API endpoints
No new endpoint. One existing endpoint changes its contract.

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| POST | /api/pet-ads | logged in (owner = caller) | `CreatePetAdRequest` (new required `title`) | `PetAdResponse` (new `title`) | 201, 400, 401 (unchanged) |

Request: `title`, string, required, 1 to 100 characters after trimming. Missing, `null`, `""` and whitespace-only give 400 with an error on `title` only. Longer than 100 after trimming gives 400 with an error on `title` only. Exactly 100 is accepted. Surrounding spaces do not count and are removed before storing.

Response: `title` is the trimmed stored value. It is public. No owner email or phone is added.

This is a breaking change for any client that posts without a title. The only known client is the not-yet-built frontend (#19 depends on it), and the test suite.

## Screens and components
not applicable

## Data changes
- Entities and fields: `PetAd.Title` (string, required, not null, max length 100). No other entity changes.
- Migration name: `AddPetAdTitle`. Created with `dotnet ef migrations add AddPetAdTitle --project PawsPost`. `20261114155141_AddPetAds` is not edited. Expected content: one `AddColumn<string>` on `PetAds` with `nullable: false`, `maxLength: 100` and `defaultValue: ""`, and a matching `Down` that drops the column. If the generated file contains anything else (drops of other columns or tables, type changes, other columns), stop and ask.
- SQLite notes:
  - SQLite can add a `NOT NULL` column with `ALTER TABLE ... ADD COLUMN` only if the column has a non-null default. That is why the `defaultValue: ""` is needed, and why it works for existing rows without a table rebuild.
  - SQLite does not enforce `maxLength` (the type is `TEXT`). The 100 limit is enforced only by the request validation.
  - The `Down` method drops a column. EF Core on SQLite supports this (it rebuilds the table if needed). Only relevant when rolling back.

## Files to create or change
- `PawsPost/Data/PetAd.cs`: add `Title`.
- `PawsPost/Data/AppDbContext.cs`: map `Title` (required, max length 100).
- `PawsPost/Data/Migrations/<timestamp>_AddPetAdTitle.cs`, `.Designer.cs`, and `AppDbContextModelSnapshot.cs`: generated by EF only, never hand-written.
- `PawsPost/Features/PetAds/CreatePetAdRequest.cs`: `TitleMaxLength` constant, `Title` property with `[Required]`, length check in `Validate`.
- `PawsPost/Features/PetAds/PetAdResponse.cs`: add `Title` to the record, after `Id`.
- `PawsPost/Features/PetAds/PetAdService.cs`: trim and store `Title`, pass it to the response.
- `PawsPost/Features/PetAds/PetAdsController.cs`, `IPetAdService.cs`, `Program.cs`: no change expected. `Common/NonNullableRequestBodyTransformer.cs`: no change expected, only touched if the OpenAPI tests show `title` is not marked required (decision 5).
- `PawsPost.Tests/Infrastructure/PetAdTestHelpers.cs`: add `["title"] = "Friendly beagle looking for a home"` to `ValidAdoptionBody()` (`ValidSaleBody()` inherits it).
- `PawsPost.Tests/Features/PetAds/CreatePetAdTests.cs`: new and extended tests below.
- `PawsPost.Tests/Features/Swagger/OpenApiDocumentTests.cs`: OpenAPI tests below.
- Any other code or test that builds a `PetAd` or `PetAdResponse` directly: search for `new PetAd` and `new PetAdResponse` before starting. At planning time only the service builds them.
- No new NuGet packages.

## Tests to write
Endpoint: `POST /api/pet-ads`. All names follow `Method_Scenario_ExpectedResult`, with the style already used in `CreatePetAdTests` (own `PawsPostFactory` per test, assert response and stored data).

Success (new):
- `CreatePetAd_ValidRequest_ReturnsAndStoresTitle`: 201, `title` in the response equals the sent value, the stored ad has the same value.
- `CreatePetAd_TitleWithSurroundingSpaces_IsTrimmed`: send `"  Nice dog  "`, response and stored value are `"Nice dog"`. (Separate from the existing `CreatePetAd_TextWithSurroundingSpaces_IsTrimmed` so each test has one reason to fail.)
- `CreatePetAd_TitleAtMaxLength_Returns201`: exactly 100 characters, 201 and stored.
- `CreatePetAd_TitleAtMaxLengthPaddedWithSpaces_Returns201`: length is checked on the trimmed text.
- `CreatePetAd_Success_ResponseJsonContainsTitle`: the raw JSON has a string `title` property (camelCase). The existing `CreatePetAd_Success_ResponseHasNoEmailOrPhone` keeps covering the email and phone check.

Validation failure (new, each returns 400 `application/problem+json` with only `title` in `errors`, uses the existing `AssertBadRequestForOnlyAsync`, and stores no ad):
- `CreatePetAd_TitleMissing_Returns400WithOnlyThatField`.
- `CreatePetAd_TitleNull_Returns400WithOnlyThatField`.
- `CreatePetAd_TitleEmpty_Returns400WithOnlyThatField`.
- `CreatePetAd_TitleWhitespaceOnly_Returns400WithOnlyThatField`.
- `CreatePetAd_TitleOneOverMaxLength_Returns400WithOnlyThatField` (101 characters).

Existing theories extended with a `title` case (one `[InlineData]` line each):
- `CreatePetAd_RequiredFieldMissing_Returns400WithOnlyThatField`: add `"title"`. Then `CreatePetAd_TitleMissing_...` above is not written separately, and only the null and empty tests remain as new.
- `CreatePetAd_WhitespaceOnlyText_Returns400WithOnlyThatField`: add `"title"`. Then `CreatePetAd_TitleWhitespaceOnly_...` is not written separately.
- `CreatePetAd_TextAtMaxLength_Returns201` and `CreatePetAd_TextOneOverMaxLength_Returns400WithOnlyThatField`: add `("title", 100)`. Then the two stand-alone "at max" and "one over max" title tests are not written separately.
- `CreatePetAd_MaxLengthTextPaddedWithSpaces_Returns201`: unchanged, because its assertion uses a two-way ternary (species or description). The separate padded-title test above covers it.
- `CreatePetAd_SeveralInvalidFields_ReportsEachFieldOnce`: unchanged. It still expects 3 errors, because the helper body now includes a valid title.

Authorization:
- Not logged in: the existing `CreatePetAd_NotLoggedIn_Returns401ProblemDetailsWithoutRedirect`, `CreatePetAd_InvalidBodyAndNotLoggedIn_Returns401` and `CreatePetAd_TamperedCookie_Returns401` already cover 401. They use `ValidAdoptionBody()`, so they now test the valid-body-with-title-but-no-login case. No new test is needed.
- Not the owner: not applicable to create (the caller becomes the owner). `CreatePetAd_OwnerIdAndStatusInBody_AreIgnored` is the equivalent test and stays.

OpenAPI (in `OpenApiDocumentTests`, same helpers as the existing tests, written first, see decision 5):
- `GetOpenApiDocument_CreatePetAdRequestSchema_TitleIsRequired`: `components.schemas.CreatePetAdRequest.properties` contains `title`, and `required` contains `title`.
- `GetOpenApiDocument_PetAdResponseSchema_TitleIsRequired`: `components.schemas.PetAdResponse` has `title` in `properties` and in `required`. The 201 response of `POST /api/pet-ads` must reference this schema (check the `$ref`, as the existing test does for the request).

No test is planned for the migration itself (framework behavior). The test factory applies the schema, so every test above runs against the new column.

## Steps
1. Create the branch `feature/21-add-pet-ad-title` from `main`.
2. Add `Title` to `PetAd` and to the `AppDbContext` mapping, then generate `AddPetAdTitle` with `dotnet ef migrations add AddPetAdTitle --project PawsPost`. Read the generated migration (expected: one `AddColumn` with `defaultValue: ""`). If not, stop and ask (decision 4). Commit: `feat: add Title to PetAd with migration (#21)`.
3. Add `Title` to `CreatePetAdRequest` (required, length rule), `PetAdResponse` (after `Id`) and `PetAdService`. Update `ValidAdoptionBody()` in the same commit so the existing tests stay green. Commit: `feat: require a title when creating a pet ad (#21)`.
4. Add the OpenAPI tests first. If they fail because `title` is not marked required, fix it as described in decision 5. Commit: `test: cover title in the OpenAPI document (#21)`, plus a `fix:` commit if the transformer changes.
5. Add the success, validation and theory-extension tests. Commit: `test: cover pet ad title (#21)`.
6. Optional: only if an old local database exists, apply the migration to a copy of it with `dotnet ef database update --project PawsPost` and confirm that startup does not fail and old ads have `Title = ''`. Not required, because no real ads exist.
7. Run `dotnet build` (no warnings), `dotnet test`, `dotnet format --verify-no-changes`. Open the PR with `Closes #21`. In the PR description, note that `schema.d.ts` must be regenerated in a separate frontend issue (decision 6).
8. After the merge, open the separate frontend issue to regenerate `schema.d.ts` and adapt the frontend.

## Risks and open questions
Open questions: none. All were answered, see "Decisions".

Risks:
- Breaking change to the create contract of #1. Any client that sends no title now gets 400. Mitigated by the helper update and the note in the PR description. Decision 6 covers the frontend follow-up, and #19 depends on it.
- The empty default stays at database level. SQLite does not drop a column default, so a migrated database keeps `''` as the default of `Title`. A direct SQL insert (not through the API) would be accepted with an empty title. The API never does this, and no other code writes ads. Accepted, not planned to change.
- If EF does not scaffold `defaultValue: ""`, the migration would fail on a table that has rows. The plan stops and asks in that case (decision 4). With no real ads this would only affect old local databases.
- Same dependency as #1: `POST /api/pet-ads` needs a logged-in user. That exists (auth issue #3, merged), so nothing is blocked.
