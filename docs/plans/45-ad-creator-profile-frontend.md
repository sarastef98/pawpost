# Plan: Show the ad creator and add the creator profile page (#45)

Status: Approved
Layer: frontend

Issue: https://github.com/sarastef98/pawpost/issues/45 (local copy: `docs/issues/ad-creator-profile-frontend.md`)
Depends on: #44 (display name, `creator` in list and details, `GET /api/creators/{id}`; merged on `main` as 611fd38), #19 (home list, `PetAdCard`, `PetAdList`), #20 (details page `/pet-ads/:id`), #14 (register form, `AuthContext`). All four exist in the code. The only gap: `PawsPost.Web/src/api/schema.d.ts` does not contain the #44 changes yet (checked: no `creator`, no `displayName`, no `/api/creators/{id}` in the file), so it must be regenerated first (step 2).
Rules that apply: `CLAUDE.md`, `.claude/rules/frontend.md`, `.claude/rules/design.md`, `.claude/rules/frontend-testing.md`, `.claude/rules/creator-profile.md` (privacy of the profile; written for the backend, its "public name only" rule also guides what the screens show), `docs/design.md`.

## Goal
- The register form has a required "Public name" field with a short rules hint. The name is sent to `POST /api/auth/register` as `displayName`. Server 400 errors for the name are shown next to the field as one fixed friendly sentence (D3), and a taken name (409) shows "This name is already taken. Please choose another." next to the field.
- Each card on the home page shows the creator's display name as plain text "By <name>" (no link). The same card on the profile page and in the assistant results (#36) shows it too (D7, D8).
- The ad details page shows the creator's display name as a "Posted by" link to `/creators/<id>`. The details page login rule stays as it is.
- A new public page `/creators/:id` shows, inside the shared layout, the creator's display name and their `Active` ads as cards in the API order (newest first), with Previous and Next paging. It has loading, empty, not found (404) and error (Try again) states, a back link to the home page, and works without login.

Note: the backend now requires `displayName`, so registration from the web app returns 400 on `main` until this issue ships (risk in plan #44).

## Decisions
The issue left these details to the plan. The plan proposed them as P1 to P10 and the user accepted all of them; D1 to D10 below match P1 to P10.

- **D1. Wording.** All short and friendly (`docs/design.md`):
  - Register field label: "Public name"
  - Register hint (helper text): "3 to 30 characters. Letters, digits, spaces, hyphens (-) and underscores (_). Other people will see this name."
  - Empty name (client check): "Please choose a public name."
  - Any server 400 on the name: "Please use 3 to 30 characters: letters, digits, single spaces, hyphens or underscores." (see D3)
  - Name taken (409): "This name is already taken. Please choose another."
  - Card line: "By Anna" (the name as plain text, `text-sm text-muted`).
  - Details page row: label "Posted by", value is a link with the name as its text, for example "Anna".
  - Profile loading heading: "Creator profile" plus Spinner.
  - Profile heading (`h1`): the display name. Ads section heading (`h2`): "Ads".
  - Profile empty: "Anna has no ads right now. Please check back soon." with a link "Browse all ads" to `/`.
  - Profile not found: "We could not find this person." with a link "Go to the home page" to `/`.
  - Profile error: "We could not load this profile. Please try again." with Try again.
  - Back link: "Back to all ads" (same text as the details page).
- **D2. Client check for the name: only "not empty after trimming".** The length and character rules (3 to 30) are enforced by the server only; they are not copied to the client (the regex uses Unicode classes and single-space rules; copying it risks the two sides drifting). The server decides and its 400 is shown as the D3 sentence. The input has no `maxLength` attribute (same reason as plan #36: it would count surrounding spaces the server trims). The trimmed name is sent.
- **D3. Fixed friendly 400 text for the name.** The server sends technical text such as "The DisplayName field must be between 3 and 30 characters.", which contains the field name `DisplayName`. Any 400 message under the key `displayName` (matched case-insensitively, like `email` and `password` in `authErrors.ts`) is replaced by the one fixed friendly sentence from D1, instead of being shown as sent. Matching the exact server sentences was rejected because it breaks silently when the backend wording changes. Email and password errors keep today's behavior (shown as sent).
- **D4. Telling the two 409s apart by the ProblemDetails `title`.** The backend sends 409 for both a taken email (`title: "Email already registered"`) and a taken name (`title: "Display name already taken"`), with no other machine-readable difference. The client matches `title === "Display name already taken"` exactly: that gives the name-taken message next to the name field (`displayNameTaken`). Any other 409 keeps today's email-taken general message with the "Log in" link. When both are taken, the backend reports the email (plan #44), so the user fixes the email first. The coupling to a backend string is accepted as a known risk (see "Risks and open questions"); an error code from the backend is a possible follow-up, out of scope for this issue.
- **D5. Missing or unreadable `creator` hides the creator line.** The converters read `creator` defensively: if it is missing, not an object, has an empty `id` or a non-string `displayName`, the ad is still shown without the creator line (card) or without the "Posted by" row (details). The creator is extra information; an old or partial response should not hide the ad itself.
- **D6. Profile paging lives in the URL** (`/creators/:id?page=2`), like the home page (`?page=`). Reload and Back keep the page. Page size is the existing `PAGE_SIZE` (20, the backend default, within the maximum of 100). Previous is disabled on page 1, Next on the last page, "Page X of Y" in between (same `nav` as `PetAdList`). A page beyond the last one is replaced in the URL by the last page (same rule as `PetAdList`). After Previous or Next, focus moves to the "Ads" heading once the new page has settled.
- **D7. The profile cards are the same `PetAdCard`**, so they also show "By <name>" on every card. No extra prop on `PetAdCard`.
- **D8. The assistant results (#36) also show "By <name>"** (user confirmed), because they use the same `PetAdCard` and the assistant response contains `PetAdDetailsResponse`, which now has `creator`. No extra prop and no change to `toAssistantResult` beyond what `toPetAdDetails` already does.
- **D9. A non-Guid id in the URL (400) shows the "not found" state**, same rule as `usePetAd` ("a bad id can never be fixed by trying again"). The `page` and `pageSize` the client sends are always valid, so a 400 can only come from the id.
- **D10. Feature folder `src/features/creators/`**, mirroring the backend `Features/Creators` (`.claude/rules/frontend.md`). The profile reuses `PetAdCard`, `totalPages`, `PAGE_SIZE` and `toListData` from `features/petAds` (cross-feature imports, same as the assistant in #36).

Also decided: the "Public name" field is the last field on the register form (after Email and Password).

## Approach
- Regenerate `schema.d.ts` first (own commit), then build on the types. Types are always derived from `components['schemas'][...]`, never written by hand.
- **Shared creator type and converter.** A small file `features/petAds/petAdCreator.ts`: type `PublicCreator = Pick<components['schemas']['PetAdCreatorResponse'], 'id' | 'displayName'>` and `toPublicCreator(value: unknown): PublicCreator | null` (D5). Used by `toListData` (list items), `toPetAdDetails` (details) and the profile converter.
- **List and card.** `PetAdSummary` gets `creator: PublicCreator | null`; `toItem` in `petAdListQuery.ts` fills it. `PetAdCard` renders `<p>By {name}</p>` as plain text inside the existing card link when `creator` is not null (no nested link, D1). `PetAdDetails` (the data type) also gets `creator`, so it still fits `PetAdCard` where the assistant passes it (D8).
- **Details page.** `toPetAdDetails` fills `creator`. `PetAdDetails.tsx` adds a "Posted by" row to its existing `dl` (D1) with a `Link` to `/creators/${encodeURIComponent(creator.id)}`, using the same link classes as the page's other links. The guard in `PetAdDetailsPage` is not touched.
- **Profile data.** `features/creators/creatorProfileData.ts`: type `CreatorProfile = { id, displayName, ads: PetAdListData }` and `toCreatorProfile(value: unknown)`, which requires a non-empty string `id`, a string `displayName` and a valid `ads` object (through the existing `toListData`); anything else gives null, which shows the error state (same rule as `toListData`).
- **Profile hook.** `features/creators/useCreatorProfile.ts`, modeled on `usePetAd` and `usePetAdList`: key `[id, page, attempt]`, derived loading, cancelled flag for late responses, `retry`. Calls `client.GET('/api/creators/{id}', { params: { path: { id }, query: { page, pageSize: PAGE_SIZE } } })`. Outcomes: `success(profile)`, `notFound` (404 or 400, D9), `error` (other statuses, network failure, unreadable body). No auth involved: the hook never calls `useAuth` and always runs.
- **Profile page.** `CreatorProfilePage` (route element, no login guard): reads `id` from `useParams` and `page` from the URL (D6, a small `parsePage` reusing the same rules as `parseListQuery`), shows the back link and renders by state. `CreatorAdList` (presentational) renders the "Ads" heading, the card grid (same classes as `PetAdList`), the empty state, and the pager.
- **Register form.** `AuthContextValue.register` becomes `register(email, password, displayName)` and sends `{ email, password, displayName }` through the typed client. `FormErrors` gets `displayName?: string`. `mapFormErrors('register', ...)` maps a `displayName` 400 key to the D3 text and the name-taken 409 to `displayName` (D4). `RegisterPage` adds the third `TextField` (label, `helperText` hint, `errorText`, `autoComplete="nickname"`) as the last field, the empty check (D2), and keeps the existing focus-to-first-invalid-field logic, which then also covers the name field.
- Shared components and tokens only: `TextField`, `Button`, `Card`, `Spinner`, `EmptyState`, `ErrorMessage`, `PetAdCard`. No new shared component, no new npm package, no `dangerouslySetInnerHTML` (names are rendered as React text).

## Regenerating the API types
Checked in the current repo: `schema.d.ts` has no `creator`, no `displayName`, no `PetAdCreatorResponse`, no `CreatorProfileResponse` and no `/api/creators/{id}`. The file is generated, never edited by hand: `npm run gen:api` (script `node scripts/gen-api.ts`, `openapi-typescript`) reads `http://localhost:5280/openapi/v1.json` from the `http` launch profile. Follow `docs/plans/25-regenerate-api-types.md`:
1. Confirm the checkout is current `main` (contains 611fd38, the #44 merge) with no changes to tracked files. Note: `.claude/rules/design.md` currently shows as modified in the working tree; that change belongs to someone else and must not be staged or reverted. If tracked changes block the branch switch, stop and ask.
2. The local dev database must have the #44 migration (`dotnet ef database update --project PawsPost`), otherwise the API may fail on the new columns. Generation itself only reads `/openapi/v1.json`.
3. Check port 5280 read-only (`Get-NetTCPConnection -LocalPort 5280 -State Listen -ErrorAction SilentlyContinue`). If it is in use, do not kill anything: stop and ask.
4. Start the API for this step only: `dotnet run --project PawsPost --launch-profile http`.
5. Fetch `/openapi/v1.json` (read-only) and verify it contains: `displayName` in `RegisterRequest`; schema `PetAdCreatorResponse` with `id` and `displayName`; `creator` in `PetAdListItemResponse` and `PetAdDetailsResponse`; path `/api/creators/{id}` with `get`, query `page` and `pageSize`, a 200 `CreatorProfileResponse` (`id`, `displayName`, `ads` as `PetAdListResponse`), 400 and 404. If anything is missing, the API is stale: stop and ask.
6. From `PawsPost.Web` run `npm run gen:api`, optionally a second time to confirm the output is stable. Stop the API with Ctrl+C in its own terminal and confirm the port is free.
7. Read `git diff -- PawsPost.Web/src/api/schema.d.ts`. Expected changes only: `displayName` in `RegisterRequest` (it may be typed `string` or `null | string`; both are fine); new schemas `PetAdCreatorResponse` and `CreatorProfileResponse`; `creator` in `PetAdListItemResponse` and `PetAdDetailsResponse`; the new path `/api/creators/{id}` with its operation, parameters and 200/400/404 responses. Any other change (other paths, auth or assistant schemas beyond the above, `ProblemDetails`, header or formatting drift) is a stop-and-ask, not a fix.
8. Run `npx tsc -b`. Expected to fail only where the register body now misses `displayName` (`AuthContext.tsx`) if the field is required in the generated type. That fix belongs to the register step, so: if `tsc` fails, commit the regenerated file together with the smallest compile fix in a separate following commit, as plan #25 allows (`fix: adapt to regenerated API types (#45)`), or, if the fix is more than trivial, stop and report. Then `npm test` (same counts as before), `npm run lint`, `npx prettier --check --end-of-line auto src`, `npm run build`. Do not run Prettier `--write` on `schema.d.ts`.
9. Commit only `schema.d.ts` by explicit path: `chore: regenerate API types for the creator profile (#45)`. The untracked docs files in the working tree (other issues, meeting notes, rules, `.pr-44-body.md`, `pawspost.db.bak`) must not be staged.

If the file is already regenerated when work starts, this step is skipped and noted in the PR.

## API endpoints
Existing endpoints used (from merged #44; in `schema.d.ts` after step 2). No API change.

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| POST | /api/auth/register (changed in #44) | public | `RegisterRequest { email, password, displayName }`; `displayName` required, 3 to 30 characters after trimming, Unicode letters and digits, single spaces, `-`, `_` | `UserResponse` (unchanged) | 201; 400 `ValidationProblemDetails` (key `DisplayName`, also `Email`, `Password`, empty key); 409 `ProblemDetails` with `title` "Email already registered" or "Display name already taken" (email is checked first) |
| GET | /api/pet-ads (changed in #44) | public | query `page`, `pageSize`, `type`, `species` (unchanged) | `PetAdListResponse`; each item now has `creator: { id, displayName }` | 200, 400 (unchanged) |
| GET | /api/pet-ads/{id} (changed in #44) | logged in (unchanged, #18/#20) | path `id` | `PetAdDetailsResponse` + `creator: { id, displayName }` | 200, 400, 401, 404 (unchanged) |
| GET | /api/creators/{id} (new in #44) | public (`[AllowAnonymous]`) | path `id` (Guid); query `page` (default 1, at least 1), `pageSize` (default 20, 1 to 100) | `CreatorProfileResponse { id, displayName, ads: PetAdListResponse }`; items are `PetAdListItemResponse` (only `Active`, newest first, same fields as the home list) | 200 (also with empty `items`, `totalCount` 0), 400 (bad id, page or pageSize), 404 (unknown creator) |
| GET | /api/auth/me | used through `useAuth` only (header) | none | `UserResponse` | unchanged; the profile page does not depend on it |

Real backend behavior the screens rely on:
- `creator` contains only `id` and `displayName`; no email, phone or user name is in any of these responses (tested in #44). The screens show only `displayName` and use `id` only to build the profile link.
- Users who existed before #44 have generated names like "Pet lover 0001".
- The profile lists only `Active` ads, for every caller, including the owner.

## Screens and components
- Routes:
  - New: `/creators/:id`, declared inside the `Layout` route in `App.tsx`, public (no guard), optional `?page=`.
  - Changed screens: `/register`, `/` (cards), `/pet-ads/:id` (details). The assistant results (#36) change only through the shared `PetAdCard` (D8).
- Components:
  - New in `src/features/creators/`:
    - `CreatorProfilePage` (default export, route element): back link, reads `id` and `page`, calls `useCreatorProfile`, renders the states, keeps the URL page in range, moves focus after paging and Try again.
    - `CreatorAdList` (presentational): `h2` "Ads" (`tabIndex={-1}` for focus), `ul` of `PetAdCard` (`grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3`, as in `PetAdList`), or the empty `EmptyState`; the pager `nav aria-label="Pages"` with Previous, "Page X of Y", Next.
  - Changed: `PetAdCard` (creator line), `PetAdDetails` (Posted by row with link), `RegisterPage` (name field), `App.tsx` (route).
  - Shared, existing: `TextField`, `Button`, `Card`, `Spinner`, `EmptyState`, `ErrorMessage`.
- Hooks: new `useCreatorProfile(id, page)` returning `{ state, retry }` with state `loading | notFound | error | success(profile)`. Existing `useAuth` gets the new `register` signature.
- States of the profile page:
  - Loading (first load, page change, Try again): `h1` "Creator profile" plus `Spinner` while there is no settled result for the current key. An old page is never shown for a new page.
  - Success with ads: `h1` with the display name (`break-words`), then `CreatorAdList`. Every card shows "By <name>" (D7).
  - Empty (200, `totalCount` 0): `h1` with the name, `h2` "Ads", `EmptyState` with the D1 text and "Browse all ads" link. No pager.
  - Out-of-range page (200, no items, page above the last): replace the URL with the last page (or page 1), Spinner meanwhile (same as `PetAdList`).
  - Not found (404, or 400 per D9): `h1` "Creator profile", `EmptyState` "We could not find this person." with "Go to the home page".
  - Error (500, other status, network failure, unreadable body): `h1` "Creator profile", `ErrorMessage` with the D1 text and Try again. No status code or server text.
  - Logged out, logged in or auth still loading: all identical. No redirect to `/login`, no wait for `/api/auth/me`.
- Details page: one new row "Posted by" in the existing `dl`, value is a `Link` (name as text) to `/creators/<encoded id>`. Shown only when `creator` is valid; otherwise the row is hidden (D5). Nothing else changes, including the login guard and the 401 redirect.
- Cards (home, profile and assistant results): one new plain-text line "By <name>" under the location (or under the price/badge when there is no location), `text-sm break-words text-muted`. No link, no button. The whole card remains the single link to `/pet-ads/:id`, so the name becomes part of the card link's text. When `creator` is missing or unreadable, the line is hidden (D5).
- Forms and validation (register):
  - Field order: Email, Password, Public name (decided: the new field is last). Label "Public name", `helperText` with the rules hint (D1), `autoComplete="nickname"`, `readOnly` while busy, `errorText` from `errors.displayName`.
  - `TextField` shows either the helper text or the error, not both. So while an error is shown, the hint is replaced by the error; the D3 error text repeats the rules, so the user does not lose them.
  - Client check (D2): trimmed empty gives "Please choose a public name." and no request; the existing email and password checks stay. All empty fields get their message at once. Length 3 to 30 is left to the server.
  - Server 400: `displayName` key gives the D3 text next to the field; focus goes to the first invalid field (existing effect).
  - Server 409 with the name-taken title (D4): D1 message next to the name field, focus on it. The password is cleared after every submit, as today.
  - Double-submit guard: existing `submitting` ref plus `busy`.
- Accessibility and 360px:
  - Mobile first: one card column at 360px; long names and titles wrap (`break-words`); the pager uses `flex justify-between gap-4` and fits three items at 360px (checked manually, step 9).
  - Keyboard: back link, card links, Previous, Next, Try again and the details "Posted by" link are reachable with Tab and have the existing visible focus style. Disabled pager buttons are real `button` elements with `disabled`.
  - Focus: after Previous/Next and after Try again, focus goes to the "Ads" heading (or the `h1` in the not found / error state) once the result settles, as in `PetAdList` and `PetAdDetailsPage`. On first load focus is not moved.
  - The register hint is linked to the input through `aria-describedby` (built into `TextField`); errors set `aria-invalid`.

## Data changes
none

## Files to create or change
Create:
- `PawsPost.Web/src/features/petAds/petAdCreator.ts`: `PublicCreator` type, `toPublicCreator` (D5).
- `PawsPost.Web/src/features/petAds/petAdCreator.test.ts`
- `PawsPost.Web/src/features/creators/creatorProfileData.ts`: `CreatorProfile` type, `toCreatorProfile`, `parseProfilePage` (page from the URL, same rules as `parseListQuery`).
- `PawsPost.Web/src/features/creators/creatorProfileData.test.ts`
- `PawsPost.Web/src/features/creators/useCreatorProfile.ts` and `useCreatorProfile.test.tsx`
- `PawsPost.Web/src/features/creators/CreatorAdList.tsx` and `CreatorAdList.test.tsx`
- `PawsPost.Web/src/features/creators/CreatorProfilePage.tsx` and `CreatorProfilePage.test.tsx`

Change:
- `PawsPost.Web/src/api/schema.d.ts`: regenerated with `npm run gen:api` only (step 2). Never edited by hand.
- `PawsPost.Web/src/features/petAds/petAdListQuery.ts` (+ test): `creator` in `PetAdSummary` and `toItem`.
- `PawsPost.Web/src/features/petAds/petAdDetailsData.ts` (+ test): `creator` in `PetAdDetails` and `toPetAdDetails`.
- `PawsPost.Web/src/features/petAds/PetAdCard.tsx` (+ test): plain-text creator line, hidden when `creator` is null.
- `PawsPost.Web/src/features/petAds/PetAdList.test.tsx`: the home list shows the creator name on cards.
- `PawsPost.Web/src/features/petAds/PetAdDetails.tsx` (+ test) and `PetAdDetailsPage.test.tsx`: "Posted by" row and link.
- `PawsPost.Web/src/features/auth/useAuth.ts`: `register(email, password, displayName)`.
- `PawsPost.Web/src/features/auth/AuthContext.tsx` (+ test): send `displayName`.
- `PawsPost.Web/src/features/auth/authErrors.ts` (+ test): `displayName` in `FormErrors`, 400 key mapping (D3), name-taken 409 (D4), new message constants.
- `PawsPost.Web/src/features/auth/RegisterPage.tsx` (+ test): the new field (last), hint, client check.
- `PawsPost.Web/src/App.tsx` (+ `App.test.tsx`): `/creators/:id` route.
- `PawsPost.Web/src/test/apiResponses.ts`: `sampleAd` gets a synthetic `creator` (for example `{ id: 'creator-1', displayName: 'Pet lover 0001' }`); new helpers `sampleListItem` and `creatorProfileBody(displayName, items, totalCount, page, pageSize)`.
- `PawsPost.Web/src/features/assistant/AssistantAnswer.test.tsx`: tests only (D8): an assistant card shows "By <name>", and an assistant card without a readable creator shows no creator line. No change to `AssistantAnswer.tsx`.
- `PawsPost.Web/src/features/assistant/assistantResult.test.ts`: tests only (D8, D5): `toAssistantResult` keeps the creator of each ad, and keeps an ad with an unreadable creator (creator `null`). No change to `assistantResult.ts`.

Not changed: `client.ts`, any shared component in `src/components`, `PetAdDetailsPage.tsx` guard logic, `AssistantAnswer.tsx`, `assistantResult.ts`, `LoginPage`, `AuthNav`, the backend.

## Tests to write
Vitest and Testing Library in the style of the existing page tests: `vi.mock('../../api/client')` with `GET`/`POST` mocked (the API client is the only mock), an `AuthContext.Provider` value per status where the page reads auth, `MemoryRouter` with probe routes (`/`, `/pet-ads/:id`, `/creators/:id`, `/login`), `user-event`, `findBy` queries, elements by role, label or visible text. Results built with `okResult`, `failResult`, `deferred`. Synthetic data only (no real names or emails).

`CreatorProfilePage.test.tsx` (one behavior per test):
- shows the creator's name as the page heading and their ads as cards.
- each card on the profile shows "By <name>" (D7).
- shows the ads in the order the API returns them.
- each card links to its details page `/pet-ads/<id>`.
- requests `/api/creators/{id}` with the id from the URL, page 1 and page size 20.
- loads and shows the profile for a visitor who is not logged in, without redirecting to `/login`.
- loads the profile while the login state is still loading (does not wait for it).
- shows a Spinner while the profile is loading (`deferred`).
- shows the empty state with a link to all ads when the creator has no ads, and shows no pager.
- shows "We could not find this person." with a link to the home page on 404.
- shows the not found state on 400 (invalid id).
- shows an ErrorMessage with Try again on a server error, without technical details (no "500", no server title).
- shows an ErrorMessage on a network failure (client rejects).
- shows an ErrorMessage when the response has the wrong shape.
- Try again loads the profile again and shows it when it succeeds.
- disables Previous on the first page.
- Next requests page 2, shows its ads and updates the URL to `?page=2`.
- disables Next on the last page.
- opens the page from `?page=2` directly and requests page 2.
- replaces an out-of-range page with the last page.
- moves focus to the "Ads" heading after Next.
- shows only the name and the ads: no email, phone or other data even if the response contained extra fields (the mock adds an `email` field; the text is not on screen).
- the back link "Back to all ads" leads to the home page.
- renders a name containing HTML characters (for example `<b>Anna</b>`) as visible text, with no `b` element created.

`useCreatorProfile.test.tsx`: success, 404 and 400 give not found, 500 and rejection give error, a late response for an old page is ignored, retry sends a new request.

`creatorProfileData.test.ts`: valid body; missing or empty `id` gives null; non-string `displayName` gives null; `ads` missing or invalid gives null; empty `items` is valid; `parseProfilePage` accepts positive integers and falls back to 1 for anything else.

`petAdCreator.test.ts`: valid creator; missing, null, non-object, empty id, non-string name each give null.

`PetAdCard.test.tsx` and `PetAdList.test.tsx`:
- shows "By <name>" on the card as plain text, and the name is not a separate link (the card has exactly one link, to `/pet-ads/<id>`).
- hides the creator line when the creator is missing (`creator: null`), and still shows the rest of the card (D5).
- the home list shows the creator name on each card.
- the home list shows an ad whose `creator` is unreadable, without the creator line (D5).

`petAdListQuery.test.ts` / `petAdDetailsData.test.ts`: `creator` is read; a missing or bad `creator` gives `null` and the ad is still returned (D5).

`PetAdDetails.test.tsx` / `PetAdDetailsPage.test.tsx`:
- shows "Posted by" with the creator's name as a link to `/creators/<id>`, and clicking it opens the profile probe route.
- encodes the creator id in the link.
- hides the "Posted by" row when the creator is missing or unreadable, and still shows the ad (D5).
- existing tests that the details page redirects a logged-out visitor to `/login` stay and must pass (login rule unchanged).

`AssistantAnswer.test.tsx` (D8):
- each assistant result card shows "By <name>" as plain text, and the card still has exactly one link, to `/pet-ads/<id>`.
- an assistant result whose creator is missing shows no creator line and the rest of the card (D5).
- existing assistant tests stay green.

`assistantResult.test.ts` (D8, D5): the creator of each ad is kept; an ad with an unreadable creator is kept with `creator: null`.

`RegisterPage.test.tsx`:
- shows the "Public name" field with a label and the rules hint, as the last field after Email and Password.
- sends email, password and the trimmed public name to the register call.
- does not send the form when the public name is empty or only spaces, and shows "Please choose a public name." next to the field.
- sends a short name (for example 2 characters) to the server without a client length error (D2: the server enforces 3 to 30).
- shows the fixed friendly message next to the name field when the server rejects the name (400, keys `DisplayName` and `displayName`), and does not show the server's technical text (D3).
- shows "This name is already taken. Please choose another." next to the name field on a 409 with the title "Display name already taken", and moves focus to the field (D4).
- still shows the email-taken message with the "Log in" link on a 409 for the email (regression).
- the name field is reachable with the keyboard and the form submits with Enter.

`authErrors.test.ts`: `displayName` key mapping (any case), D3 text replaces the server text, name-taken 409 by exact title, other 409 stays email-taken, login 409 unaffected.
`AuthContext.test.tsx`: `register` posts `displayName` in the body.
`App.test.tsx`: `/creators/:id` renders the profile page inside the layout.

## Steps
1. Get approval of this plan (decisions D1 to D10 are confirmed). Create branch `feature/45-ad-creator-profile-frontend` from `main`.
2. Regenerate `schema.d.ts` as described in "Regenerating the API types". Commit: `chore: regenerate API types for the creator profile (#45)`. (If `tsc` needs a trivial fix, it goes in the following commit `fix: adapt to regenerated API types (#45)`; otherwise it is done in step 3.)
3. Register form: `useAuth`, `AuthContext`, `authErrors`, `RegisterPage` and their tests. Commit: `feat: ask for a public name on the register form (#45)`. This step makes registration work again.
4. Creator data: `petAdCreator.ts`, `creator` in `petAdListQuery.ts` and `petAdDetailsData.ts`, test helpers in `apiResponses.ts`, with tests (including `assistantResult.test.ts`). Commit: `feat: read the ad creator from the API (#45)`.
5. Card: creator line in `PetAdCard`, with card, list and assistant card tests (D7, D8, hidden-creator case D5). Commit: `feat: show the creator name on ad cards (#45)`.
6. Details page: "Posted by" row and link, with tests. Commit: `feat: link the ad creator on the details page (#45)`.
7. Profile data and hook: `creatorProfileData.ts`, `useCreatorProfile.ts`, with tests. Commit: `feat: load the public creator profile (#45)`.
8. Profile page: `CreatorAdList`, `CreatorProfilePage`, `/creators/:id` route in `App.tsx`, with page and app tests. Commit: `feat: add the creator profile page (#45)`.
9. Run `npx tsc -b`, `npm run lint`, `npm test`, `npx prettier --check --end-of-line auto src`, `npm run build`. Check manually at 360px (register form with hint, card with name, details row, profile with pager, assistant results with name), with keyboard only, and with visible focus, against the local API with synthetic users and ads. Open the PR with `Closes #45`, listing decisions D1 to D10.

## Risks and open questions
Open questions: None.

Dependencies and risks:
- Known and accepted risk (D4): the client tells the two 409s apart by the exact `title` "Display name already taken". If the backend title changes, a taken name would be shown as "email already registered"; the D4 tests catch that only on the client side. A sturdier option (an error code or `type` URI in the 409 body) is a backend change and a possible follow-up issue, out of scope here.
- Blocking until step 2: `schema.d.ts` lacks all #44 changes. Regeneration needs the API from current `main` on port 5280 and a migrated local database; if the port is held by another process, the developer stops and asks (plan #25 rules), never kills it.
- Registration from the web app is broken on `main` right now (the backend requires `displayName`). Step 3 fixes it; this issue should be finished without delay.
- The details page needs login (#18/#20, unchanged), so the only link to a profile is behind login, while the profile page itself is public. A visitor can still open a shared `/creators/<id>` URL directly. This follows the client decisions; noted so nobody "fixes" it by accident.
- Changing `PetAdSummary`/`PetAdDetails` affects the assistant feature (#36) through `PetAdCard` (D8). Its existing tests must stay green.
- `TextField` shows the hint or the error, not both; while an error is shown the hint is hidden. Accepted because the D3 error repeats the rules. Changing `TextField` to show both would be a shared-component change, not planned.
- Names are user-chosen and can be up to 30 characters of any alphabet; `break-words` keeps them inside the card at 360px. They are rendered as text, never as HTML.
- Look-alike names (Latin and Cyrillic letters) are accepted by the backend (#44 D2), so two cards can show names that look the same but link to different profiles. Accepted in #44; nothing to do on the client.
- The client sends `pageSize` 20 (`PAGE_SIZE`). If the backend lowers its maximum below 20, the profile would get a 400 and show "not found" (D9). Unlikely; the limits come from #44 (default 20, max 100).
- Working tree: `.claude/rules/design.md` is modified and several docs files are untracked. None of them belong to this issue; stage files only by explicit path.
