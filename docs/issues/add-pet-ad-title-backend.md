Issue: #21
URL: https://github.com/sarastef98/pawpost/issues/21

# Add a required title to pet ads

Layer: backend
Depends on: #1

## Summary
Add a required `title` to `PetAd`, `CreatePetAdRequest` and `PetAdResponse`. The home page cards (#19) show the ad's title, but today the entity, the create request and the response have none. This changes the create contract of #1.

## User story
As a user, I want to give my ad a title, so that visitors can recognise it quickly in the list.

## Current state
- `PawsPost/Data/PetAd.cs` has no title. Fields: type, species, breed, age in months, description, price, location, status, owner, created date.
- `CreatePetAdRequest` validates with `[Required]` plus length checks on trimmed text in `Validate`.
- `PetAdService.CreateAsync` trims text and builds the `PetAdResponse` positionally.
- One migration adds the table: `20261114155141_AddPetAds`. It must not be edited.
- `PawsPost.Tests/Features/PetAds/CreatePetAdTests.cs` and `PetAdTestHelpers.ValidAdoptionBody()` build bodies without a title.

## Acceptance criteria
- [ ] `PetAd` has a required `Title` (not null).
- [ ] `CreatePetAdRequest` has a required `Title`. A missing, empty or whitespace-only title returns 400 with `ProblemDetails`, reporting only the `title` field.
- [ ] A title longer than the maximum (proposed: 100 characters, checked on the trimmed text) returns 400 with `ProblemDetails`. A title of exactly the maximum is accepted.
- [ ] The title is trimmed before it is stored.
- [ ] `PetAdResponse` includes `title`, and the 201 response of `POST /api/pet-ads` returns it.
- [ ] A new EF migration adds the column. The existing `AddPetAds` migration is not edited.
- [ ] Existing rows, if any, are handled as decided in the open questions.
- [ ] The Swagger/OpenAPI document shows `title` as required in the create request and in the response, so the frontend can regenerate `src/api/schema.d.ts`.
- [ ] `PetAdTestHelpers.ValidAdoptionBody()` includes a valid title, so the existing tests of #1 keep passing.
- [ ] Tests cover: create returns the title and stores it, missing title (400), whitespace-only title (400), title at max length (201), title one over max (400), title is trimmed, and the OpenAPI test for the schema. The existing "required field missing" and "max length" theories get a `title` case.
- [ ] No owner email or phone appears in the response.

## Out of scope
- Editing the title (editing ads is a separate feature).
- Search by title (not now, per the notes).
- The list and details endpoints (#2, #18). They return the title once this issue is done.
- Any frontend change.

## Open questions
- What is the maximum title length? Recommended default: 100 characters (proposed value, same as breed and location).
- How are existing ads without a title handled? Recommended default: the migration adds the column with an empty-string default so it does not fail on existing rows, and the empty default is not kept in the model for new ads (new ads always require a title). If there are no real rows yet (nothing is deployed), no data fix is needed. Confirm whether any ads exist outside local development databases.
- Is the title shown to everyone, including visitors who are not logged in? Recommended default: yes, it is public like the other fields, because the home page list is public.

## Source
Meeting of 14 Nov: each card on the home page shows the ad's title. The ad has no title today. The user accepted the default of adding a required `title` field to pet ads.
