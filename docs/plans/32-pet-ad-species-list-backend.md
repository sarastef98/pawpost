# Plan: Limit pet ad species to a fixed list (#32)

Status: Approved
Layer: backend

Issue: https://github.com/sarastef98/pawpost/issues/32
Depends on: #1 (create ad), #2 (browse list), #21 (details). All three already exist in the code.

## Goal
Species is one of Dog, Cat, Bird, Rabbit or Other. `POST /api/pet-ads` and the `species` filter of `GET /api/pet-ads` accept only these values, everything stored and returned is one of them, and the OpenAPI document lists them so the frontend (#31) can regenerate `schema.d.ts`.

## Decisions
The user confirmed these (they were open questions in the issue):
1. Contract: a C# enum serialized as names, stored in the database as text (like `Type`).
2. Existing rows: the migration trims and lowercases the old text, maps dog, cat, bird and rabbit to the new values, and maps everything else to `Other`.
3. Case: names are case-insensitive on create and in the filter (`dog` is accepted and stored as `Dog`). Responses always use the canonical name.
4. Names: the same English names (Dog, Cat, Bird, Rabbit, Other) in requests, filter and responses. Display wording is a frontend matter.
5. Length and index: `SpeciesMaxLength` is removed, the column max length becomes 20, `IX_PetAds_Species` stays.
6. Invalid species in the filter returns 400 reporting `species`, like an invalid `type`.
7. No endpoint for the values. They are exposed only through the OpenAPI document.

Assumption (not confirmed): the user did not say whether real data exists outside local development databases. The plan keeps the data-keeping backfill from decision 2, which is safe for both cases. If real data exists, review the migration on a copy of it before release.

## Approach
Use the same pattern as `PetAdType`:

- A new C# enum `PetSpecies { Dog, Cat, Bird, Rabbit, Other }` in `PawsPost/Data/` is the single list of allowed values. Create, the filter, the entity, the responses and the migration backfill all use it.
- Stored as the text name (`HasConversion<string>()`, max length 20), like `Type` and `Status`.
- Create: `CreatePetAdRequest.Species` becomes `PetSpecies?` with a strict JSON converter (names only, no numbers) and `[Required]`, like `Type`. A missing value is reported by `[Required]`, an unknown, empty or whitespace-only value by the converter. Only `species` is reported.
- Filter: `ListPetAdsRequest.Species` stays a bound `string?` (the enum binder would accept numbers), parsed by a hand-written method like `GetParsedType()`, trimmed and case-insensitive. A non-empty value that does not parse gives a `species` validation error (400). The service filters with `petAd.Species == species`, so the existing `IX_PetAds_Species` index can be used and `EF.Functions.Collate(..., "NOCASE")` goes away.
- Responses: `species` becomes `PetSpecies` with `JsonStringEnumConverter<PetSpecies>`, so OpenAPI shows the five values.
- `SpeciesMaxLength` and its length checks are removed.
- Existing rows are converted in the new migration with a constant SQL backfill (no user input).

## API endpoints
Both endpoints exist already. Only their contract changes. No new endpoints (decision 7).

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| POST | /api/pet-ads | logged in (no ownership check: the owner is set from the cookie) | `CreatePetAdRequest` with `species` as `PetSpecies` | `PetAdResponse` with `species` as `PetSpecies` | 201, 400, 401 |
| GET | /api/pet-ads | public | `ListPetAdsRequest`, query `species` limited to the five values, `type`, `page`, `pageSize` | `PetAdListResponse` of `PetAdListItemResponse` (species as `PetSpecies`) | 200, 400 |
| GET | /api/pet-ads/{id} | logged in | none | `PetAdDetailsResponse` (species as `PetSpecies`) | 200, 400, 401, 404 (contract unchanged apart from the species type) |

Rules:
- Create: missing, empty, whitespace-only or unknown species returns 400 `ValidationProblemDetails` with only the `species` key. Other field errors are still reported as today. Surrounding spaces around a name (for example " Dog") are not accepted on create.
- List: no `species` parameter (or an empty/whitespace one, as today) means no filter. Any other value that is not one of the five (case-insensitive, trimmed) returns 400 reporting `species`. The Active-only rule, ordering and pagination do not change.
- No owner email or phone in any response (the response DTOs keep their existing fields).

## Screens and components
not applicable

## Data changes
- Entities and fields: `PetAd.Species` changes from `string` to `PetSpecies`. In `AppDbContext`: `HasConversion<string>().HasMaxLength(20).IsRequired()`. The `Species` index stays.
- New enum `PetSpecies` (Dog, Cat, Bird, Rabbit, Other).
- Migration name: `LimitPetAdSpeciesToList`. It changes the column length (SQLite rebuilds the table) and runs the backfill SQL with `migrationBuilder.Sql`. `AddPetAds` and the other existing migrations are not edited. Generate it with `dotnet ef migrations add LimitPetAdSpeciesToList --project PawsPost`, then add the backfill by hand and review the generated table rebuild so it keeps data, the foreign key and the other indexes.

## Files to create or change
- `PawsPost/Data/PetSpecies.cs` (create): the enum, the one list of allowed values.
- `PawsPost/Data/PetAd.cs`: `Species` becomes `PetSpecies`.
- `PawsPost/Data/AppDbContext.cs`: string conversion and max length 20 for `Species`.
- `PawsPost/Data/Migrations/<timestamp>_LimitPetAdSpeciesToList.cs`, `.Designer.cs` and `AppDbContextModelSnapshot.cs` (generated, plus the hand-written backfill).
- `PawsPost/Features/PetAds/CreatePetAdRequest.cs`: `PetSpecies?` with a strict converter, remove `SpeciesMaxLength` and its length check.
- `PawsPost/Features/PetAds/ListPetAdsRequest.cs`: replace the length check with a parse check, add `GetParsedSpecies()`, keep the parameter name `species`.
- `PawsPost/Features/PetAds/PetAdService.cs`: store `request.Species!.Value`, filter with an equality check on the parsed species, remove the NOCASE comment and call.
- `PawsPost/Features/PetAds/PetAdResponse.cs`, `PetAdListItemResponse.cs`, `PetAdDetailsResponse.cs`: `Species` becomes `PetSpecies` with `JsonStringEnumConverter<PetSpecies>`.
- One small shared parsing helper in `PawsPost/Features/PetAds/` (or `Common/`) so create and filter cannot drift apart.
- OpenAPI: make the `species` query parameter show the five values. Add a small transformer in `PawsPost/Common/`, registered in `Program.cs` (an edit of the existing `AddOpenApi` call, no new package).
- `PawsPost.Tests/Infrastructure/PetAdTestHelpers.cs`: `BuildPetAd` takes a `PetSpecies`.
- `PawsPost.Tests/Features/PetAds/CreatePetAdTests.cs`, `ListPetAdsTests.cs`, `GetPetAdTests.cs`: update to the new rules.
- `PawsPost.Tests/Features/Swagger/OpenApiDocumentTests.cs`: add species enum assertions.

## Tests to write
POST /api/pet-ads
- Success: each of the five values creates an ad (201), the response and the stored row hold that value (theory). A lowercase name (`dog`) is stored as `Dog`.
- Validation failure: unknown value (for example "Hamster"), a number, missing, empty and whitespace-only each give 400 with only `species` reported (existing free-text, non-ASCII and over-long species tests are changed into these cases).
- Authorization: not logged in returns 401 and nothing is stored. Not-the-owner does not apply to create (no existing resource), so the test class states this explicitly.

GET /api/pet-ads
- Success: filter with each valid value returns only Active ads of that species and keeps total count and paging right (theory). A lowercase or space-padded value works. No `species` returns all Active ads. A Closed ad of the species is not returned.
- Validation failure: `species=Hamster` returns 400 reporting only `species`. A numeric value returns 400.
- Authorization: public, an anonymous client gets 200 with a valid filter.

GET /api/pet-ads/{id}
- Success: the details response returns the stored species as one of the five names (update the existing test).
- Authorization and 404 tests stay as they are.

OpenAPI
- `CreatePetAdRequest.species`, the `species` of the three response schemas and the `species` query parameter list exactly Dog, Cat, Bird, Rabbit, Other.

Migration and data
- A test that applies the backfill SQL to legacy rows (for example "dog", " Dog ", "Hamster", "куче") and asserts Dog, Dog, Other, Other. Do not test that EF can save.
- No owner email or phone appears in the responses (existing checks stay green).

## Steps
1. Add `PetSpecies` enum and the shared parsing helper, with unit tests for the helper (commit: feat).
2. Change `PetAd`, `AppDbContext` and add the migration `LimitPetAdSpeciesToList` with the backfill, plus the backfill test (commit: feat).
3. Change the three response DTOs and the service mapping (commit: feat).
4. Change `CreatePetAdRequest` and `PetAdService.CreateAsync`, update the create tests and test helpers (commit: feat).
5. Change `ListPetAdsRequest` and the service filter, update the list tests (commit: feat).
6. Show the allowed values in OpenAPI for the query parameter, add the OpenAPI tests (commit: feat).
7. Update the details tests, run `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes` (commit: test or chore).

## Risks and open questions
No blocking open questions remain. All seven questions from the issue are decided above.

Assumption to confirm later: whether real data exists outside local development databases (see Decisions). It does not change the plan.

Risks:
- The migration rebuilds the `PetAds` table on SQLite. Review the generated code so the foreign key to users, all indexes and existing data survive, and run it on a copy of a database with sample rows.
- A JSON converter error on the `species` property produces a model state key like `$.species`. The existing test helper already trims `$.`. Check the key is reported once.
- The OpenAPI `species` query parameter is a string in code (like `type`), so showing the values needs a transformer. If it proves fragile, fall back to a description text. `type` has the same gap but is out of scope.
- This is a breaking contract change for #1 and #2. The frontend (#31) must regenerate `schema.d.ts` after this merges. That is frontend work, not part of this plan.
- Out of scope: any frontend change, editing species values later, a free-text field for Other, editing the species of an existing ad.
