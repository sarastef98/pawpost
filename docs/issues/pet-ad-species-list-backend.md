Issue: #32
URL: https://github.com/sarastef98/pawpost/issues/32

# Limit pet ad species to a fixed list

Layer: backend
Depends on: #1, #2, #21

## Summary
Species stops being free text. The server accepts only Dog, Cat, Bird, Rabbit or Other when creating an ad, and the species filter of the browse list uses the same list. This changes the create contract of #1 and the filter of #2. The create-ad page (#31) needs it for its species select.

## User story
As a user, I want to pick the species of my animal from a fixed list, so that ads are consistent and easy to filter.

## Current state
- `PetAd.Species` is a `string` (max 50, required, indexed with `IX_PetAds_Species`), set in `AppDbContext` and the `AddPetAds` migration. That migration must not be edited.
- `CreatePetAdRequest.Species` is a `string?` with `[Required]` and a trimmed max length of 50 (`SpeciesMaxLength`) checked in `Validate`. Any text is accepted.
- `PetAdService.CreateAsync` trims the species and stores it as typed.
- `ListPetAdsRequest.Species` is a free-text query parameter (`species`), validated only for length. `PetAdService.ListAsync` compares it with `EF.Functions.Collate(..., "NOCASE")`, which does not use the species index.
- `PetAdResponse`, `PetAdListItemResponse` and `PetAdDetailsResponse` return `species` as a string.
- `Type` already shows the pattern for a strict enum: `PetAdType` with `StrictPetAdTypeConverter` on create and a hand-parsed string on the list filter.
- Existing rows may hold any free text. It is not known whether real data exists outside local databases.
- The frontend types in `PawsPost.Web/src/api/schema.d.ts` are generated from the OpenAPI document and show `species` as a string today.

## Acceptance criteria
- [ ] `POST /api/pet-ads` accepts only the species Dog, Cat, Bird, Rabbit and Other. Any other value returns 400 `ValidationProblemDetails` reporting only the `species` field.
- [ ] A missing, empty or whitespace-only species still returns 400, reporting only `species`.
- [ ] The 201 response, the list items and the details response return the species as one of the five values.
- [ ] `GET /api/pet-ads?species=<value>` accepts only the five values. Any other value returns 400 `ValidationProblemDetails` reporting `species`. No `species` parameter still means no filter.
- [ ] The species filter returns only Active ads with that species (the existing Active-only and pagination rules do not change).
- [ ] The list of allowed values is defined in one place on the server and used by create, the filter and the stored data.
- [ ] A new EF migration makes any needed schema change. The existing `AddPetAds` migration is not edited.
- [ ] Existing rows with free-text species are handled as decided in the open questions.
- [ ] The Swagger/OpenAPI document shows the allowed species values, so the frontend can regenerate `src/api/schema.d.ts`.
- [ ] Tests cover: each of the five values is accepted on create, an unknown value (400), a missing value (400), whitespace-only (400), the filter with each valid value, the filter with an invalid value (400), the filter without a value, and the OpenAPI test for the schema. Existing tests that create ads with free-text species (for example "Dog" variants, non-ASCII or long species) are updated to the new rules.
- [ ] No owner email or phone appears in any response.

## Out of scope
- Any frontend change. The species select is in #31, and regenerating `schema.d.ts` is done in the frontend.
- Adding or editing species values later (for example through an admin screen).
- A separate "other species" free-text field when Other is chosen.
- Editing the species of an existing ad (editing ads is a separate feature).

## Open questions
- Enum or string in the API contract? Option A: a C# enum serialized as names (like `PetAdType`), which shows the allowed values in OpenAPI and gives the frontend a union type, but needs a strict converter and a hand-parsed query parameter. Option B: keep a string and validate against the list, which is simpler but the OpenAPI schema only shows the values if described by hand. Which one should we use? This also decides how the column is stored (text name or integer), so it needs a decision before the migration.
- How are existing ads with free-text species handled? Options: map known texts (for example "dog", "Dog ") to the new values and everything else to Other, leave them as they are, or delete them if only test data exists. Nothing is decided and no backfill is assumed. Is there any real data outside local development databases?
- Are the values case-sensitive on create and in the filter? Today the filter ignores case for ASCII letters. For example, is `dog` accepted, or only `Dog`?
- Should `species` in the response and the filter keep the same names (Dog, Cat, Bird, Rabbit, Other, in English), or is another form wanted? Display wording for users is a frontend matter.
- Does the maximum length of 50 and the `SpeciesMaxLength` constant go away, and does the species index stay? The index may help the filter once the NOCASE comparison is no longer needed.
- Should the filter for an invalid species return 400 (as proposed here, like an invalid `type`), or an empty list?
- Should the species values be exposed through an endpoint (for example a list of values), or only through the OpenAPI document? Nothing in the notes asks for it.

## Source
Developer decision after reviewing #31 (create-ad page): species becomes a fixed list of Dog, Cat, Bird, Rabbit and Other, and the server must accept only these values. Impact on create validation, response shape, browse filter, existing data and the generated frontend types is described above. Migration and backfill choices are not decided.
