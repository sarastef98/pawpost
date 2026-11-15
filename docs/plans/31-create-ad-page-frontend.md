# Plan: Add a page to create a pet ad (#31)

Status: Approved
Layer: frontend

## Goal
A logged-in user opens a "Post an ad" page from the header, fills in a form (title, type, species, breed, age, description, location, and price for Sale), and posts the ad. On success they land on the new ad's details page and see a short success message. Logged-out visitors are sent to `/login`.

## Approach
Add one feature page, `CreatePetAdPage`, in `src/features/petAds/`, following the patterns already used by `RegisterPage` / `LoginPage` (controlled inputs, `noValidate` form, `submitting` ref plus `busy` state, focus on first invalid field) and `PetAdDetailsPage` (guard on `useAuth().status`: `loggedOut` -> `<Navigate to="/login" replace />`, `loading` -> Spinner). The request goes through `client.POST('/api/pet-ads')`; the types come from `schema.d.ts` (`CreatePetAdRequest`, `PetAdResponse`, `ValidationProblemDetails`). Pure logic (client checks, age conversion, building the request body, mapping 400 errors to fields) goes in small separate files so it can be unit tested, like `authErrors.ts` and `petAdListQuery.ts`.

Key decisions (all accepted by the user unless marked as open below):
- Client checks mirror the server limits in `PawsPost/Features/PetAds/CreatePetAdRequest.cs` (title 100, breed 100, description 2000, location 100, age 0..360 months, price > 0, at most 1,000,000, at most 2 decimals) as a convenience only. The server still decides and its messages are shown after submit. The limits live in one constants object.
- Species: a `Select` with the fixed list Dog, Cat, Bird, Rabbit, Other (backend #32, merged). The option values are exactly those names (the server accepts only the exact enum names, in any case, with no spaces; it rejects anything else with 400). The option list is typed from the regenerated `schema.d.ts` (see "Regenerating the API types") so a change in the backend enum becomes a compile error. A first empty option "Choose a species" makes the choice required, same as for type.
- Age: a number input plus a radio group "Age unit" with Months and Years (Months selected by default, see open question 3). The form converts to a whole number of months and sends `ageInMonths` (integer).
- Price: a text/number input shown only when type is `Sale`; when type is `Adoption` the `price` key is omitted from the body. When switching Sale -> Adoption the typed value is kept in state but not sent.
- Type select: shared `Select` with an empty first option "Choose a type". No currency symbol, label just "Price".
- 400 mapping: a new `mapCreateAdErrors` in `petAdFormErrors.ts` reads `errors`. Keys are normalised before matching: strip a leading `$` and `.` (model-binding/JSON errors can come as `$.species`; the backend tests accept both), and compare case-insensitively with title, type, species, breed, ageInMonths, description, price, location. `ageInMonths` errors are shown next to the age field. Unknown keys go to a general message. Server text is shown as sent, like `authErrors.ts`. Non-400 or network failure: general friendly message, values kept. 401: `<Navigate to="/login" replace />`.
- Important server behavior: when the species is invalid or missing, the server reports only `species` and does not report other field errors in the same response (the JSON read fails before the other checks run). The form must not treat the absence of other errors as "everything else is fine". It shows the species error, focuses the species select, and keeps all other values and any earlier client messages untouched. After the user fixes the species and posts again, the server may report more fields; those are mapped the same way. The same can happen for a bad `type` (also a strict converter).
- Success: `navigate('/pet-ads/' + data.id, { state: { notice: 'Your ad is live.' } })` (wording accepted). `PetAdDetailsPage` must read `location.state.notice` and show it in a `role="status"` region (same pattern as `LoginPage`). This is a small change to an existing page, needed by the acceptance criteria.
- Header: add a "Post an ad" link-styled button in `AuthNav` for the logged-in state only; nothing while loading or logged out. After login the user still goes to `/` (no change to `LoginPage`).

## Regenerating the API types
Checked in the current repo: `PawsPost.Web/src/api/schema.d.ts` is NOT up to date for #32. `species` is still `string` (`null | string` in `CreatePetAdRequest`, `string` in `PetAdResponse`, `PetAdDetailsResponse`, `PetAdListItemResponse`), and there is no `PetSpecies` schema. It is generated, never edited by hand (`.claude/rules/frontend.md`): `npm run gen:api` (script `node scripts/gen-api.ts`, uses `openapi-typescript`) reads `http://localhost:5280/openapi/v1.json` from the `http` launch profile. The process is the one in `docs/plans/25-regenerate-api-types.md` (check that port 5280 is free or belongs to the current `main`, start `dotnet run --project PawsPost --launch-profile http`, verify the OpenAPI content, generate, stop the API, check that only `schema.d.ts` changed).
- This is step 2 of this plan, in its own commit: `chore: regenerate API types for the species list (#31)`.
- Extra check before generating: the OpenAPI document must contain the species enum with the values Dog, Cat, Bird, Rabbit, Other (backend test `OpenApiDocumentTests` asserts this). If the generated `species` is still a plain `string`, stop and report: the typed select cannot be built from the schema, and that would be a backend problem.
- Expected diff: `species` changes from `string` to a reference to a new `PetSpecies` enum type (or an inline union) in the schemas above, and the species query parameter of `GET /api/pet-ads` may change too. Anything else unexpected: stop and ask, as in plan 25.
- Compile impact: code that reads `species` as `string` (for example the home list and details page) may need a trivial type fix. Any fix is a separate commit, and a larger fix stops the work and is reported (rule from plan 25).
- If the developer finds the file already regenerated when starting, this step is skipped and noted in the PR.

## API endpoints
Existing endpoint used (present in `PawsPost.Web/src/api/schema.d.ts`; no API change).

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| POST | /api/pet-ads | logged in (cookie) | `CreatePetAdRequest` (title, type, species, breed?, ageInMonths, description, price?, location?) | `PetAdResponse` | 201, 400 (`ValidationProblemDetails`), 401 (`ProblemDetails`) |

Also used already: `GET /api/auth/me` through `useAuth`.

## Screens and components
- Route: `/pet-ads/new`, declared under the shared `Layout` in `App.tsx` so it is not captured by `/pet-ads/:id` (React Router ranks static segments above dynamic ones; a test proves it).
- Components:
  - `CreatePetAdPage` (guard plus form), one component per file.
  - `CreatePetAdForm` (the form, state, submit), separate from the guard so the form is only mounted when logged in.
  - Shared, existing: `Button`, `TextField`, `Select`, `Card`, `ErrorMessage`, `Spinner`.
  - New shared component `TextArea` in `src/components/` (accepted): same label, helper/error text and `aria-invalid`/`aria-describedby` pattern as `TextField`, with its own test.
  - Age unit radio group: a `fieldset` with a `legend` "Age unit" and two radio inputs labelled "Months" and "Years". The age number input has the label "Age". Check first whether a shared radio component exists; if not, keep the radios inside the form file as plain labelled inputs (see open question 2).
  - `AuthNav`: add the "Post an ad" link.
- States:
  - Auth loading: heading plus `Spinner`, no form, no redirect flash.
  - Logged out: redirect to `/login`.
  - Saving: submit disabled, `aria-busy`, inputs `readOnly`/disabled, label "Please wait...".
  - Error: general `ErrorMessage` (no technical details), typed values kept.
  - Empty: not applicable (the form is not a list).
- Forms and validation:
  - Fields (each with a visible label): Title, Type (`Select`: Adoption / Sale), Species (`Select`: Dog, Cat, Bird, Rabbit, Other), Breed (optional), Age (number) with the "Age unit" radio group (Months / Years), Description (`TextArea`), Location (optional), Price (only for Sale, required).
  - Client checks before sending: required (title, type, species, age, description, price for Sale), trimmed maximum lengths, age: a number that is not negative; for Months a whole number; the converted months must be a whole number from 0 to 360; price greater than 0, at most 1,000,000, at most two decimals. Messages are short and friendly, next to the field. The age message names the unit, for example "Age can be at most 30 years (360 months)".
  - Age conversion (in `createPetAdForm.ts`, pure function): Months -> the value as is; Years -> `Math.round(years * 12)`, so 1.5 years is 18 months, 2 years is 24. Limits are checked on the converted value, so 30 years and 360 months are accepted and 30.1 years (361 months) is rejected. Changing the unit does not rewrite the typed number.
  - On failed client check or server 400: focus the first invalid field in form order (extend the selector to `input, select, textarea`; for the age group focus the age number input).
  - Double submit guard: `submitting` ref plus `busy`.
  - 360px first, design tokens only, no hard-coded colors or spacing.

## Data changes
none

## Files to create or change
- `PawsPost.Web/src/api/schema.d.ts`: regenerated with `npm run gen:api` only (step 2). Never edited by hand.
- `PawsPost.Web/src/features/petAds/CreatePetAdPage.tsx`: guard (loading, logged out, logged in).
- `PawsPost.Web/src/features/petAds/CreatePetAdForm.tsx`: the form and submit flow.
- `PawsPost.Web/src/features/petAds/createPetAdForm.ts`: form values type, limits, species options (typed from the schema), age conversion, client validation, building the typed request body (omits price for Adoption).
- `PawsPost.Web/src/features/petAds/petAdFormErrors.ts`: maps a 400 `ValidationProblemDetails` (keys normalised: leading `$.` removed, case-insensitive) and other failures to field messages.
- `PawsPost.Web/src/components/TextArea.tsx` (and test): shared multiline input.
- `PawsPost.Web/src/App.tsx`: add the route.
- `PawsPost.Web/src/features/auth/AuthNav.tsx`: add "Post an ad".
- `PawsPost.Web/src/features/petAds/PetAdDetailsPage.tsx`: show the success notice from router state.
- Tests next to each file (see below). `src/test/apiResponses.ts` may get a helper for a created ad.
- Possibly a trivial type fix in files that read `species` after regeneration (see above).

## Tests to write
Vitest and Testing Library, in the style of the existing tests (mock `client` or fetch as the neighbours do).
- `CreatePetAdPage.test.tsx`:
  - Logged out: redirected to `/login`.
  - Auth loading: Spinner shown, no form, no login page.
  - Logged in: all labelled fields shown; breed and location marked optional; the radio group is found by the name "Age unit" with the options Months and Years.
  - Species select: options are exactly the empty choice plus Dog, Cat, Bird, Rabbit, Other, and the sent value is the exact name (for example `Rabbit`).
  - Age: 8 with Months sends `ageInMonths: 8`; 2 with Years sends 24; 1.5 with Years sends 18; switching the unit changes the sent value but not the typed number.
  - Sale shows and requires the price; Adoption hides it and no `price` is sent; switching Sale -> Adoption -> Sale keeps the typed price.
  - Client validation messages next to each field (required including species not chosen, too long, age out of range for both units, price <= 0).
  - Success (201): navigates to `/pet-ads/:id` and the success message is visible there; request body has `ageInMonths` as a number and no status/photos fields.
  - Server 400: messages appear next to the matching fields, including keys like `$.species` and mixed-case keys; unknown keys go to the general message.
  - Server 400 with only `species`: the species message is shown and the species select gets focus; other fields keep their values and are not marked as errors; a second submit with a fixed species can show new field errors.
  - Other failure (500 and network error): friendly message, no technical details, typed values kept.
  - 401: redirected to `/login`.
  - Submit disabled while saving, and a double click sends one request.
  - Focus moves to the first invalid field.
- `createPetAdForm.test.ts`: validation rules, age conversion table (Months, Years, decimals, rounding, 360 months OK, 361 rejected, 30 years OK, 30.1 years rejected, negative, not a number), species option list, body building (trimming, empty optional fields omitted or null, price handling).
- `petAdFormErrors.test.ts`: case-insensitive key mapping, leading `$.` stripped, `ageInMonths` mapped to the age field, joined messages, unknown keys, fallback.
- `TextArea.test.tsx`: label, helper and error text, `aria-invalid`, `aria-describedby`.
- `AuthNav.test.tsx`: "Post an ad" link points to `/pet-ads/new` when logged in; not shown when logged out or loading.
- `PetAdDetailsPage.test.tsx`: shows the notice from router state; shows nothing without it.
- `App.test.tsx`: `/pet-ads/new` renders the create page, not the details page.

## Steps
1. Get answers to the open questions below (or acceptance of the defaults) and approval of this plan.
2. Regenerate `schema.d.ts` (see "Regenerating the API types"), verify the diff, fix compile errors only as described, one commit.
3. Add the shared `TextArea` component with tests (one commit).
4. Add `createPetAdForm.ts` (limits, species options, age conversion, validation, body builder) with unit tests.
5. Add `petAdFormErrors.ts` (400 mapping, `$.` keys, species-only response, fallbacks) with unit tests.
6. Add `CreatePetAdForm` and `CreatePetAdPage` (guard, states, radio group, submit, redirects) with page tests.
7. Add the route in `App.tsx` and its route test.
8. Show the success notice on `PetAdDetailsPage` with tests.
9. Add the "Post an ad" button in `AuthNav` with tests.
10. Run `npm run build`, lint, and the test suite (Windows: `npx prettier --check --end-of-line auto src`). Check manually at 360px, with keyboard only (including the radio group arrows), and focus visibility.

## Risks and open questions
Open questions (each with a proposed default; the plan uses the default until answered):
1. Age in years: are decimals allowed, and how is the value rounded? Proposed default: Years accepts decimals (for example 1.5) and is converted with `Math.round(years * 12)` to whole months; Months accepts whole numbers only; the maximum is 360 months (30 years) checked on the converted value. This is a requirement, so the client should confirm, but it does not block the other steps.
2. The Months/Years radio group has no shared component. Proposed default: keep plain labelled radios inside the form (a `fieldset` with a legend), no shared component yet. If a shared `RadioGroup` is wanted, it is added as its own step.
3. Default unit selected for age: proposed default Months selected (so the radio group is never empty).

Dependencies and risks:
- Depends on existing code: `POST /api/pet-ads` (#1), title (#21), species list (#32, merged), details page and route (#20, present), auth (`useAuth`, #14, present), layout (#11, present). All exist; the only gap is the stale `schema.d.ts` (species is `string`), fixed in step 2.
- Regeneration needs the API running locally from current `main`; if port 5280 is held by an old debug session the developer stops and asks (plan 25 rules).
- The server returns `price` and `ageInMonths` as `number | string` in the types; the form must send numbers and not rely on the response format.
- A 400 for `type`, `species` or a non-numeric `ageInMonths` comes from JSON/model binding and may read technically (for example "Unknown species."); the plan shows server text as sent, like login/register. The selects make these errors unlikely from the UI; if they still look bad in practice, a follow-up is needed.
- The client no longer needs a species max length (the list is fixed); the old 50-character check is removed.
- `PetAdDetailsPage` currently does not show any notice, so step 8 changes an existing page (needed by the acceptance criteria). The notice lives in router state, so it is lost on reload; acceptable for a short message.
- Draft status, photos, preview, edit and close stay out of scope. The form sends no status.
