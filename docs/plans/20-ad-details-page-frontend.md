# Plan: Add ad details page for logged-in users (#20)

Status: Approved
Layer: frontend

## Goal
A logged-in user can open `/pet-ads/:id` (the card links from #19 already point there) and see all information of one ad inside the shared layout: title, Badge, price (Sale only), species, breed, age, description and location. A visitor who is not logged in is sent to `/login` and the ad is not requested. Loading, not found and error states are designed, and a back link leads to the home list.

## Approach
- Keep the page and its data hook in `src/features/petAds/`, next to the list code from #19, and copy its proven pattern: a hook with derived loading state, a defensive converter for the API response (the schema types numbers as `number | string`), a retry counter.
- The login guard lives inside the page, not in a new router layer (no extra abstraction for one page). The page reads `useAuth().status`: `loading` shows a Spinner and makes no request, `loggedOut` renders `<Navigate to="/login" replace />`, `loggedIn` renders the content and only then the hook runs the request. To make "not requested" real, the page is split into a small outer component (guard) and an inner component that calls `usePetAd` (hooks cannot be called conditionally).
- A 401 from the API also navigates to `/login`. Other results map to a small state union: `loading | notFound | error | success`.
- Reuse `formatPrice` (Sale only, euro), `Badge`, `Card`, `Spinner`, `EmptyState`, `ErrorMessage`, `Button`. No new components or packages. No new colors or font sizes; design tokens and existing Tailwind token classes only.
- The `/login` redirect carries no return target, because that is undecided (issue, open questions). Login goes where #14 sends it today (`/`).

## API endpoints
Existing endpoint used, taken from `PawsPost.Web/src/api/schema.d.ts` (operation `GetPetAdById`, already regenerated, `PetAdDetailsResponse` exists):

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| GET | /api/pet-ads/{id} | logged in (cookie) | path `id` (uuid string) | `PetAdDetailsResponse`: id, title, type, species, breed (nullable), ageInMonths, description, price (nullable), location (nullable), status, createdAt | 200, 400, 401, 404 |

No owner email, phone or owner id is in this response. The page must not show any.

## Screens and components
- Route: `/pet-ads/:id`, added inside the `Layout` route in `App.tsx`. The id comes from `useParams`, and is passed to the typed client as the path param (never concatenated by hand).
- Components:
  - `PetAdDetailsPage` (default export, route element): guard + back link + state switch.
  - `PetAdDetails` (presentational): the content. Layout: `Card` containing `h1` title, `Badge`, price line (only when `formatPrice` returns text), then a description list (`dl`) with Species, Breed (only if present), Age, Location (only if present), then the description as a paragraph with `whitespace-pre-line` and `break-words` (plain text, never `dangerouslySetInnerHTML`).
  - Back link: a real `Link` to `/` with text "Back to all ads", shown above the content in every state (loading excepted is fine, but kept visible so keyboard users are never stuck). Visible focus comes from the existing focus style in the design tokens (verify when building).
  - Hook `usePetAd(id, enabled)` and converter `toPetAdDetails` (new files below).
- States:
  - Auth loading: `Spinner` (no flash of the login page, no request).
  - Logged out: redirect to `/login`, no request.
  - Ad loading: `Spinner`.
  - Empty (404): `EmptyState` "We could not find this ad. It may have been removed." with a `Link` to `/` ("Go to the home page") inside it, styled like the back link.
  - Error (network, 5xx, malformed body): `ErrorMessage` "We could not load this ad. Please try again." with `onRetry`; no technical details. After Try again, focus moves to the page heading or the error container once it settles, as done in `PetAdList` (a `tabIndex={-1}` heading with a ref).
  - API 401: redirect to `/login`.
  - Success: the details.
  - Page `h1` is the ad title. In loading, not found and error states the `h1` is a neutral "Ad details" so every state has one heading.
- Forms and validation: none. The only input is the route `id`; the server decides validity (see open questions for 400).
- Responsive: single column at 360px, long titles, descriptions and locations wrap (`break-words`), the details list stacks label over value on small screens and may sit side by side from `sm:` up.

## Data changes
none

## Files to create or change
- `PawsPost.Web/src/features/petAds/petAdDetails.ts`: type `PetAdDetails` (id, title, type, species, breed, ageInMonths, description, price, location) and `toPetAdDetails(value: unknown)` that returns null for a wrong shape, uses `toFiniteNumber` for price and age, trims empty breed and location to null. Types are derived from `components['schemas']['PetAdDetailsResponse']` where possible, not written by hand. Also `formatAge(months)`.
- `PawsPost.Web/src/features/petAds/petAdDetails.test.ts`: converter and `formatAge` tests.
- `PawsPost.Web/src/features/petAds/usePetAd.ts`: calls `client.GET('/api/pet-ads/{id}', { params: { path: { id } } })` only when `enabled`; maps result to `loading | unauthorized | notFound | error | success`; derived loading by request key; `retry`; ignores late results after unmount or id change.
- `PawsPost.Web/src/features/petAds/usePetAd.test.tsx`: hook tests (client mocked).
- `PawsPost.Web/src/features/petAds/PetAdDetails.tsx`: presentational content.
- `PawsPost.Web/src/features/petAds/PetAdDetails.test.tsx`
- `PawsPost.Web/src/features/petAds/PetAdDetailsPage.tsx`: guard, back link, state switch.
- `PawsPost.Web/src/features/petAds/PetAdDetailsPage.test.tsx`: page tests with a `MemoryRouter`, an `AuthContext.Provider` value (or `AuthProvider` with mocked client) and a `/login` probe route.
- `PawsPost.Web/src/App.tsx`: one new `<Route path="/pet-ads/:id" element={<PetAdDetailsPage />} />` inside the Layout route.
- `PawsPost.Web/src/App.test.tsx`: one test that the route renders the page inside the layout.

Not changed: `schema.d.ts`, `client.ts`, `LoginPage`, `PetAdCard`, any shared component, backend.

## Tests to write
Mock only the API client (`client.GET` / `client.POST`), use helpers from `src/test/apiResponses.ts`, `user-event`, `findBy` queries, find by role or text.

Page (`PetAdDetailsPage.test.tsx`):
- shows all fields of a Sale ad: title heading, "Sale" badge, formatted price, species, breed, age, description, location.
- shows an Adoption ad without a price and without any price text.
- hides breed and location rows when the API sends null.
- requests the ad with the id from the URL.
- redirects a logged-out visitor to `/login` and does not call the ad endpoint.
- shows a Spinner while the login state is still loading, shows no login page, and makes no ad request yet.
- shows a Spinner while the ad is loading.
- redirects to `/login` when the API answers 401 although the app thinks the user is logged in.
- shows the "ad not found" EmptyState with a link to the home page on 404, and the link points to `/`.
- shows an ErrorMessage with Try again on a server error, without technical text (no status code, no "500").
- shows an ErrorMessage on a network failure (client rejects).
- shows an ErrorMessage when the body has the wrong shape.
- Try again requests the ad again and shows it when the second answer succeeds.
- the back link "Back to all ads" points to `/` and is reachable with the keyboard (Tab).
- a description with HTML characters is shown as text, not as markup.
- renders a new ad when the route id changes (no stale ad shown).

Hook (`usePetAd.test.tsx`): does nothing while `enabled` is false; maps 200, 401, 404, 400 (per decision), 500, rejection, malformed body; ignores a slow older response after the id changes.

Converter: valid body, numeric strings for price and age, missing required field gives null, negative or invalid price shows no price, empty breed to null. `formatAge`: 0, 1, 11, 12, 13, 24, 27 months and invalid input.

Route (`App.test.tsx`): `/pet-ads/<id>` renders the details page within the layout header.

## Steps
1. Add `petAdDetails.ts` (type, `toPetAdDetails`, `formatAge`) with its tests. Commit: `feat: add ad details converter (#20)`.
2. Add `usePetAd` with tests. Commit: `feat: add hook to load one ad (#20)`.
3. Add `PetAdDetails` presentational component with tests. Commit: `feat: show all ad fields on the details view (#20)`.
4. Add `PetAdDetailsPage` (guard, redirects, loading, 404, error, back link) with tests. Commit: `feat: add ad details page with login guard (#20)`.
5. Register the route in `App.tsx` and add the route test. Commit: `feat: add /pet-ads/:id route (#20)`.
6. Run `npm run build`, `npm run lint`, `npm test`, and a manual check at 360px, with keyboard only, and with a screen reader pass on state changes. Fix findings in small commits. Open the PR with `Closes #20`.

## Risks and open questions
Each item has a recommended default, which the plan uses until a person decides.

1. Return to the ad after login (issue open question, not decided). Default: not done in this issue. `<Navigate to="/login" replace />` carries no state; login goes where #14 sends it (`/`). A later issue adds a `returnTo` mechanism in router state and `LoginPage`. Changing this later is small: add `state` to the `Navigate` and read it in `LoginPage`.
2. Does the details API require login (not decided with the client; #18 implemented it as required). Default: yes, as the issue and #18 say. If the client makes details public, the logged-out redirect and the 401 handling here must be removed.
3. Message on the login page after a redirect ("Please log in to see this ad", wording not given). Default: no message; the visitor lands on the plain login form. `LoginPage` already supports a `notice` in router state, so a message could be added later without a new mechanism. Needs wording from the client.
4. How to show age. The API sends `ageInMonths` only. The issue says "age" without a format. Default: `formatAge` gives "3 months" under 12 months, "2 years", or "2 years 3 months", with correct singular and plural. Needs confirmation of the wording.
5. Title "if it exists". The schema says `title` is a required string, so it should always exist. Default: if it is empty, the `h1` falls back to the species. Not invented as new UI beyond that fallback.
6. API 400 (id is not a valid uuid, for example `/pet-ads/abc`). The issue only names 401, 404 and "other errors". Default: treat 400 like 404 (friendly "ad not found"), since Try again can never fix it. Needs a person to confirm, because the issue literally says other errors show Try again.
7. Fields returned but not listed in the issue: `status` and `createdAt`. The issue lists the fields to show, and `status` is always `Active` here per #18. Default: do not show them. "Posted on" date would need a decision on date format.
8. Currency and price format: no currency in the API. Default: reuse the existing fixed euro format from `formatPrice`, same as the list.
9. Stale login state: if the cookie expires while the app still says `loggedIn`, the API gives 401 and we go to `/login`. `LoginPage` does not redirect logged-in users, so the form shows and works. The header may show the old logged-in state until the next login or logout. Accepted for this issue; not fixed here because `AuthContext` is out of scope.
10. Layout focus and skip behavior: after the route changes, focus handling on navigation (moving focus to the heading) is not part of the existing layout. Default: follow the `PetAdList` pattern only for Try again; no global focus management.
11. Dependencies: #18 (backend endpoint, merged, schema regenerated in `schema.d.ts`), #19 (card links to `/pet-ads/:id`), #11 (layout and router), #14 (`useAuth`, `/login`) all exist in the code. Nothing blocks this plan.
12. The guard is page-local. If more protected pages follow, a shared `RequireAuth` wrapper may be worth extracting then. Not planned now (stay inside the issue).
