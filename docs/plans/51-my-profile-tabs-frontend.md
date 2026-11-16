# Plan: Add profile tabs with My ads and a My profile header button (#51)

Status: Approved
Layer: frontend

Issue: https://github.com/sarastef98/pawpost/issues/51 (local copy: `docs/issues/my-profile-tabs-frontend.md`)
Depends on: #50 (`GET /api/pet-ads/mine`, merged on `main`: `[HttpGet("mine")]` in `PetAdsController`), #45 (`/creators/:id`, `CreatorProfilePage`, `CreatorAdList`, `useCreatorProfile`, merged), #14 (`AuthContext`, `AuthNav`, `useAuth`). All exist in the code. The only gap: `PawsPost.Web/src/api/schema.d.ts` does not contain `/api/pet-ads/mine` yet (checked: the only pet-ads paths are `/api/pet-ads` and `/api/pet-ads/{id}`), so it is regenerated first (step 2).
Related, not done here: #53 (`close-pet-ad-frontend.md`, adds "Close ad" to this tab), #55 (`closed-ads-tab-frontend.md`, adds a second tab), #41 (`auth-context-on-401-frontend.md`, auth state after a 401).
Source: `docs/meeting-notes/2026-11-16-my-profile-tabs.md`.
Rules that apply: `CLAUDE.md`, `.claude/rules/frontend.md`, `.claude/rules/design.md`, `.claude/rules/frontend-testing.md`, `docs/design.md`.

## Goal
- On `/creators/:id`, when the logged-in user's id equals the profile id, the page shows the user's name and a tab list with one tab, "My ads". The tab lists the user's own `Active` ads from `GET /api/pet-ads/mine` as the same cards as the home page (links to `/pet-ads/:id`), newest first (API order), with Previous and Next paging. It has a Spinner, an EmptyState, an ErrorMessage with Try again, and sends the user to `/login` on 401.
- A visitor, or a logged-in user on someone else's profile, sees the page exactly as in #45: name, "Ads" section, public paging with `?page=`. No tabs and no request to `/api/pet-ads/mine`.
- The tabs are an accessible, reusable shared component (`src/components/Tabs.tsx`) with `tablist` / `tab` / `tabpanel` roles, `aria-selected`, linked ids, Left/Right/Home/End keys, visible focus, touch-friendly at 360px, built so more tabs can be added by adding an item.
- The header (`AuthNav`) shows a "My profile" link for logged-in users, next to "Log out", pointing to `/creators/<own id>`. Not shown to visitors or while the login state is loading.

## Decisions
The issue decides the wording ("My ads", "My profile", "Close ad"), `Active` only, and no `/profile` route. The details below are not decided by the issue; they are proposed as plan defaults P1 to P10, so the user can answer "go with suggestions" or change any of them.

- **P1. Who is "the owner" on the page.** `isOwnProfile = status === 'loggedIn' && user !== null && user.id.toLowerCase() === id.toLowerCase()`. `/api/auth/me` and login both return `UserResponse { id, email }` (checked in `schema.d.ts`), and `AuthProvider` keeps it as `user`, so the id is already available through `useAuth()`. The comparison ignores case because GUIDs are case-insensitive and the backend accepts `/creators/<UPPERCASE>` too. This check only decides what is shown; the server decides what `/mine` returns. `email` is never read or shown.
- **P2. Owner view replaces the public "Ads" section.** For the owner, the page shows the back link, the `h1` with the display name (from the public profile request, as today), then the tabs. The public "Ads" `h2`, card grid and public pager are not rendered for the owner, because the "My ads" tab lists the same `Active` ads; showing both would duplicate the list. The public profile request is still made (it provides the name and the not found / error states). Please confirm (see open questions).
- **P3. While the login state is loading.** The page behaves exactly like the visitor view (#45): the public request starts immediately, the public "Ads" section is shown, and `/mine` is not called. When auth settles as the owner, the "Ads" section is replaced by the tabs. Reasons: the visitor view stays "exactly as today" and never waits for `/api/auth/me` (existing test "loads the profile while the login state is still loading" keeps passing), and the main path to the own profile is the header button, which is only shown once logged in, so the swap only happens on a reload of the own profile URL. Alternative (not chosen): a Spinner in place of the ads area until auth settles.
- **P4. Tabs only in the success state.** The tabs are shown only when the public profile loaded successfully and P1 is true. The profile loading, not found and error states stay as today (an own profile cannot normally be "not found").
- **P5. My ads paging in the URL under its own key `myAdsPage`** (`/creators/<id>?myAdsPage=2`), parsed with the same rules as the public `page` (positive whole number, otherwise 1; reuses `parseListQuery` without changing it). Reload and Back keep the page, like the home page and the public profile. A separate key means the owner's paging never interacts with the public `?page=`: each writer updates only its own key and keeps the other query keys (small change to `goToPage` and the out-of-range effect in `CreatorProfilePage`, which today replace the whole query). Page 1 is left out of the URL. A `myAdsPage` beyond the last page is replaced (`replace: true`) by the last page, or removed when there are no ads (same rule as #45; also covers #53 closing the last ad on a page). A visitor's URL with `myAdsPage` is ignored. Page size is the existing `PAGE_SIZE` (20; backend default 20, max 100). A future tab (#55) gets its own key.
- **P6. Selected tab lives in component state, not the URL.** There is only one tab now. The `Tabs` component keeps the selected tab in its own state (first tab by default). Only the selected panel's content is rendered, so a later tab's data is requested only when it is opened (#55 needs this).
- **P7. Tab keyboard model: automatic activation.** Left/Right move focus to the previous/next tab and select it (wrapping at the ends), Home/End go to the first/last tab, Enter and Space also select (native `button`). Roving `tabIndex`: the selected tab has `0`, the others `-1`, so Tab from the tab list goes into the panel. The panel has `tabIndex={0}` so Tab reaches it even when it has no focusable content (loading, empty). This is the WAI-ARIA tabs pattern with automatic activation. Alternative (manual activation, Enter/Space to select) would matter only if switching tabs became slow; with one tab it makes no difference.
- **P8. Wording not given by the issue (proposed):**
  - Tab list accessible name: `aria-label="My profile"`.
  - Empty: "You have no active ads right now." with one action, a link "Post an ad" to `/pet-ads/new` (design: one clear action).
  - Error: "We could not load your ads. Please try again." with Try again.
  - Loading: Spinner inside the panel (the `h1` with the name stays).
  - No extra heading inside the panel; the tab label names it.
- **P9. "My profile" is a link, not a `button`.** It navigates, so it is a `Link` (anchor) styled like the other header links ("Ask the assistant", "Post an ad"), placed right before "Log out". The client word "button" is about the look and place, not the HTML element. The href is `/creators/${encodeURIComponent(user.id)}`. The logged-in header row gets `flex-wrap` so four items fit at 360px.
- **P10. File placement.** The hook `useMyPetAds` goes to `src/features/petAds/` (the endpoint is in the backend `PetAds` feature, next to `usePetAdList`). The tab UI (`MyAdsTab`) goes to `src/features/creators/` (it is part of the profile page). The tab list is a new shared component `src/components/Tabs.tsx` (none exists today; `.claude/rules/design.md`: add missing shared components to the shared components).

## Approach
- Regenerate `schema.d.ts` first, in its own commit (see "Regenerating the API types"). Types are derived from `paths` / `components['schemas']`, never written by hand.
- **Shared `Tabs` component.** Props: `label: string` (tablist name) and `items: { id: string; label: string; content: () => ReactNode }[]`. It keeps the selected index in state, renders `div role="tablist" aria-label` with one native `button role="tab"` per item (`id`, `aria-selected`, `aria-controls`, roving `tabIndex`), and one `div role="tabpanel"` per item (`id`, `aria-labelledby`, `tabIndex={0}`, `hidden` when not selected). Only the selected panel calls its `content()`. Ids come from `useId()` plus the item id, so two tab lists never clash. `onKeyDown` on the tablist handles ArrowLeft, ArrowRight, Home, End (prevent default, select, move focus to the new tab through refs). Adding a tab later = adding one item; nothing in `Tabs` changes (#55, favorites).
- **Tab styles (tokens only).** Tablist: `flex flex-wrap gap-2 border-b border-border`. Tab: `min-h-11 px-4 text-base font-semibold rounded-t-card`; selected `border-b-2 border-primary text-primary bg-primary-soft`; not selected `text-muted` with hover `text-primary`. Focus uses the global `:focus-visible` outline from `index.css`. `min-h-11` gives a 44px touch target. Selected state is shown by more than colour (underline border plus background).
- **Hook `useMyPetAds(page, enabled)`** in `features/petAds/`, modeled on `usePetAd` / `useCreatorProfile`: key `[page, attempt]`, derived loading, cancelled flag for late responses, `retry`. When `enabled` is false it makes no request and reports loading. Calls `client.GET('/api/pet-ads/mine', { params: { query: { page, pageSize: PAGE_SIZE } } })`. Outcomes: `success(data)` (through the existing `toListData`), `unauthorized` (401), `error` (any other status including 400, network failure, unreadable body). A 400 is not "not found" here: there is no id, and the page values the client sends are always valid.
- **`MyAdsTab`** in `features/creators/`: reads `myAdsPage` from the URL (P5), calls `useMyPetAds(page, true)` (it is only rendered for the owner, so `enabled` is true when mounted), and renders by state: Spinner; `<Navigate to="/login" replace />` on 401 (same pattern as `PetAdDetailsPage`); `ErrorMessage` with Try again; `EmptyState` (P8); or a `ul` of `PetAdCard` in the same grid classes as `CreatorAdList`, followed by the same pager markup (`nav aria-label="Pages"`, Previous, "Page X of Y", Next). Each `li` is a `flex flex-col gap-2` wrapper around the card, so #53 can add a "Close ad" button next to the card (outside the card link, no nested interactive elements). No placeholder button is added now. After Previous/Next or Try again, focus moves to the tab panel once the result settles (the pager buttons are replaced by the Spinner while loading).
- **`CreatorProfilePage`** reads `useAuth()` for `status` and `user` and computes `isOwnProfile` (P1). Success state: `h1` with the name, then either `<Tabs label="My profile" items={[{ id: 'my-ads', label: 'My ads', content: () => <MyAdsTab /> }]} />` (owner) or the existing `CreatorAdList` (everyone else, unchanged). Visitors never mount `MyAdsTab`, so `/mine` is never requested for them. The public out-of-range effect and `goToPage` keep other query keys (P5). The comment "the login state is not read" is updated.
- **`AuthNav`**: in the logged-in branch, add the "My profile" `Link` before "Log out" when `user` is not null (P9), and `flex-wrap` on the row. Loading still shows only the Spinner; logged out still shows Log in / Register.
- **401 on `/mine`**: redirect to `/login` only. Updating the auth state after a 401 is issue #41 and is not done here (see risks).
- Shared components and tokens only: `Button`, `Card` (via `PetAdCard`), `Spinner`, `EmptyState`, `ErrorMessage`, new `Tabs`. No new npm package, no `dangerouslySetInnerHTML`.

## Regenerating the API types
Checked in the current repo: `schema.d.ts` has no `/api/pet-ads/mine` and no `/api/pet-ads/{id}/close`. Both backends are merged (`PetAdsController`: `[HttpGet("mine")]` and `[HttpPost("{id}/close")]`). Generated with `npm run gen:api` from `http://localhost:5280/openapi/v1.json`. Follow `docs/plans/25-regenerate-api-types.md` and `docs/plans/45-ad-creator-profile-frontend.md`:
1. On current `main` with no changes to tracked files (untracked docs, `.pr-*-body.md`, `PawsPost/pawspost.db.bak` and other issues' files are fine and must never be staged). Baseline from `PawsPost.Web`: `npm test` (record file and test counts) and `npx tsc -b`.
2. The local dev database already has the migrations (user confirmed). Generation only reads `/openapi/v1.json`.
3. Check port 5280 read-only (`Get-NetTCPConnection -LocalPort 5280 -State Listen -ErrorAction SilentlyContinue`). If it is in use, do not kill anything: stop and ask.
4. Start the API for this step only: `dotnet run --project PawsPost --launch-profile http`, wait for "Now listening on: http://localhost:5280".
5. Fetch `/openapi/v1.json` (read-only) and verify: path `/api/pet-ads/mine` with `get`, query `page` and `pageSize`, 200 `PetAdListResponse`, 400, 401; path `/api/pet-ads/{id}/close` with `post`, path `id`, 204, 400, 401, 403, 404, 409. If anything is missing, the API is stale: stop and ask.
6. From `PawsPost.Web` run `npm run gen:api`, then a second time to confirm stable output. Stop the API with Ctrl+C in its own terminal and confirm the port is free.
7. Read `git diff -- PawsPost.Web/src/api/schema.d.ts`. Expected only: the new path `/api/pet-ads/mine` (and its operation, parameters, 200/400/401 responses) and the new path `/api/pet-ads/{id}/close` (operation, path parameter, 204/400/401/403/404/409 responses). The close path is expected because #52 is merged; it is not used by this issue (it is for #53). Any other change (existing paths or schemas, auth, `ProblemDetails`, header or formatting drift, whole-file line-ending rewrite) is a stop-and-ask, not a fix.
8. Run `npx tsc -b`, `npm test` (same counts as the baseline), `npm run lint`, `npx prettier --check --end-of-line auto src`, `npm run build`. No compile break is expected (only new paths are added). If one appears: smallest fix in a separate commit `fix: adapt to regenerated API types (#51)`, or stop and report if it is more than trivial. Do not run Prettier `--write` on `schema.d.ts`.
9. Commit only `PawsPost.Web/src/api/schema.d.ts` by explicit path: `chore: regenerate API types for my ads and close (#51)`.

If the file is already regenerated when work starts, this step is skipped and noted in the PR.

## API endpoints
Existing endpoints used (no API change). `/api/pet-ads/mine` is in `schema.d.ts` after step 2.

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| GET | /api/pet-ads/mine (#50) | logged in; returns only the caller's own `Active` ads (caller from the login cookie, no id in the route) | query `page` (default 1, min 1), `pageSize` (default 20, 1 to 100); client sends `page` from `myAdsPage` and `pageSize` 20 | `PetAdListResponse { items: PetAdListItemResponse[], totalCount, page, pageSize }`, newest first | 200 (also empty `items`, `totalCount` 0), 400 (`ValidationProblemDetails`, bad paging), 401 (`ProblemDetails`) |
| GET | /api/creators/{id} (#44, unchanged) | public | path `id`, query `page`, `pageSize` | `CreatorProfileResponse { id, displayName, ads }` | 200, 400, 404 |
| GET | /api/auth/me (#3, unchanged) | through `useAuth` only | none | `UserResponse { id, email }`; only `id` is used | 200, 401 |

Real backend behavior the screen relies on: `/mine` has no 403 (no id in the route); the items are the same public list items as the home list, with `creator` = the caller; no email or phone in any of these responses.

## Screens and components
- Route: `/creators/:id` (existing, public, no guard). Query keys: `page` (public list, unchanged) and new `myAdsPage` (owner's My ads tab, P5). No new route, no `/profile` route.
- Components:
  - New shared: `src/components/Tabs.tsx` (P6, P7, styles in "Approach").
  - New: `src/features/creators/MyAdsTab.tsx` (panel content of "My ads").
  - New hook: `src/features/petAds/useMyPetAds.ts` returning `{ state, retry }`, state `loading | unauthorized | error | success(data)`.
  - New helper: `parseMyAdsPage(params)` in `src/features/creators/creatorProfileData.ts`.
  - Changed: `CreatorProfilePage.tsx` (owner check, tabs, query keys kept), `AuthNav.tsx` (My profile link, wrap).
  - Unchanged: `CreatorAdList`, `useCreatorProfile`, `PetAdCard`, `PetAdDetails` (the "Posted by" link already goes to `/creators/<id>`), `App.tsx`, `AuthContext`, `useAuth`.
- What each user sees on `/creators/:id`:
  - Visitor, other user, or login state loading: exactly #45 (P3). No tabs, no `/mine` request.
  - Owner (logged in, id matches): back link, `h1` name, tab list "My ads" (selected), panel with the list.
  - Public profile loading / not found / error: as today, no tabs (P4).
- States of the "My ads" panel:
  - Loading (first load, page change, Try again): Spinner inside the panel. An old page is never shown for a new page.
  - Success: cards in API order, each linking to `/pet-ads/<id>`; pager with Previous disabled on page 1, Next disabled on the last page, "Page X of Y".
  - Empty (`totalCount` 0): EmptyState "You have no active ads right now." with "Post an ad" link (P8). No pager.
  - Out-of-range `myAdsPage`: Spinner, URL replaced with the last page (or key removed).
  - Error (500, 400, other status, network failure, wrong shape): ErrorMessage "We could not load your ads. Please try again." with Try again, no technical details.
  - 401: `Navigate` to `/login` with `replace`.
- Header (`AuthNav`): logged in: "Ask the assistant", "Post an ad", "My profile", "Log out". Logged out: unchanged (no My profile). Loading: Spinner only (unchanged).
- Forms and validation: none (no form). The only input is `myAdsPage` from the URL, sanitized by P5.
- Accessibility and 360px:
  - Tabs: roles, `aria-selected`, `aria-controls` / `aria-labelledby`, roving `tabIndex`, keys per P7, visible focus from the global `:focus-visible` style, tabs are real `button`s with `min-h-11`. The tab list wraps (`flex-wrap`), so future tabs ("My favorite ads", "Closed ads") stay reachable at 360px without horizontal scrolling.
  - Panel: `tabIndex={0}` so Tab reaches it; focus goes to the panel after paging and Try again.
  - Mobile first: one card column at 360px (same grid as the public list), long names `break-words`, pager `flex justify-between gap-4` as in #45, header row wraps with four items.

## Data changes
none

## Files to create or change
Create:
- `PawsPost.Web/src/components/Tabs.tsx` and `Tabs.test.tsx`: shared accessible tab list (P6, P7).
- `PawsPost.Web/src/features/petAds/useMyPetAds.ts` and `useMyPetAds.test.tsx`: loads `/api/pet-ads/mine`.
- `PawsPost.Web/src/features/creators/MyAdsTab.tsx` and `MyAdsTab.test.tsx`: the panel content, states, paging.

Change:
- `PawsPost.Web/src/api/schema.d.ts`: regenerated with `npm run gen:api` only (step 2). Never edited by hand.
- `PawsPost.Web/src/features/creators/creatorProfileData.ts` (+ `creatorProfileData.test.ts`): `parseMyAdsPage`.
- `PawsPost.Web/src/features/creators/CreatorProfilePage.tsx` (+ `CreatorProfilePage.test.tsx`): owner check, tabs, query keys kept. The test helper `renderPage` must always wrap the page in an `AuthContext.Provider` (default `loggedOut`, `user: null`), because the page now calls `useAuth()`, which throws without a provider. The existing tests keep their assertions; only the helper changes.
- `PawsPost.Web/src/features/auth/AuthNav.tsx` (+ `AuthNav.test.tsx`): My profile link, `flex-wrap`. The test router gets a `/creators/:id` probe route.
- `PawsPost.Web/src/App.test.tsx`: only if needed for the "My profile" link opening the profile inside the layout (see tests).

Not changed: `client.ts`, `App.tsx`, `AuthContext.tsx`, `useAuth.ts`, `CreatorAdList.tsx`, `useCreatorProfile.ts`, `PetAdCard.tsx`, `PetAdDetails.tsx`, `petAdListQuery.ts`, `StyleGuidePage.tsx` (see open questions), the backend, `package.json`.

## Tests to write
Vitest and Testing Library in the style of the existing tests: `vi.mock('../../api/client')` with `GET` mocked (the API client is the only mock), `AuthContext.Provider` values per status and user (synthetic `testUser` `{ id: 'user-1', ... }`), `MemoryRouter` with probe routes (`/`, `/login`, `/pet-ads/:id`, `/pet-ads/new`, `/creators/:id`), `user-event`, `findBy` queries, elements by role, label or text. Results built with `okResult`, `failResult`, `deferred`, `sampleListItem`, `creatorProfileBody`. One behavior per test. Synthetic data only.

`Tabs.test.tsx` (rendered with two or three test items, so keyboard behavior is meaningful):
- renders a tablist with the given accessible name, one tab per item and one visible tabpanel.
- the first tab is selected by default (`aria-selected="true"`), the others `false`.
- each tab's `aria-controls` points to its panel and the panel's `aria-labelledby` points back to the tab.
- only the selected tab has `tabIndex` 0; the others have -1.
- clicking a tab selects it and shows its panel.
- ArrowRight moves focus to the next tab and selects it.
- ArrowLeft moves focus to the previous tab and selects it.
- ArrowRight on the last tab wraps to the first; ArrowLeft on the first wraps to the last.
- Home selects and focuses the first tab; End the last.
- Tab from the selected tab moves focus into the panel.
- renders only the selected panel's content (a not-selected item's `content` is not called).
- tabs are `button` elements.
- with a single item, the arrow keys keep focus and selection on it.

`useMyPetAds.test.tsx`:
- requests `/api/pet-ads/mine` with the page and page size 20, and returns the list on 200.
- returns unauthorized on 401.
- returns error on 500, on 400, on a rejected request and on a body with the wrong shape.
- makes no request while disabled.
- ignores a late response for an old page.
- retry sends a new request.

`creatorProfileData.test.ts`: `parseMyAdsPage` accepts positive whole numbers from `myAdsPage`, ignores `page`, and falls back to 1 for missing, 0, negative, decimal and non-numeric values.

`MyAdsTab.test.tsx`:
- shows the user's ads as cards in the order the API returns them.
- each card links to `/pet-ads/<id>`.
- shows a Spinner while the ads load (`deferred`).
- shows "You have no active ads right now." with a "Post an ad" link and no pager when there are no ads.
- shows "We could not load your ads. Please try again." with Try again on a server error, without technical details.
- shows the error on a network failure and on a wrong-shaped body.
- Try again loads the ads again and shows them.
- sends the user to the login page on 401.
- disables Previous on the first page and Next on the last page.
- Next requests page 2, shows its ads and sets `?myAdsPage=2` in the URL, keeping other query keys.
- Previous from page 2 goes back to page 1 and removes `myAdsPage` from the URL.
- opens `?myAdsPage=2` directly and requests page 2.
- replaces an out-of-range `myAdsPage` with the last page.
- moves focus to the tab panel after Next.

`CreatorProfilePage.test.tsx` (new tests; all existing tests stay and pass):
- shows the "My ads" tab (tablist, one tab, selected) and the user's ads when the logged-in user opens their own profile.
- requests `/api/pet-ads/mine` on the own profile.
- recognises the own profile when the URL id differs only in letter case.
- does not show the public "Ads" heading or public pager on the own profile (P2).
- shows no tabs and makes no `/api/pet-ads/mine` request for a visitor (logged out).
- shows no tabs and makes no `/api/pet-ads/mine` request for a logged-in user on another person's profile.
- shows the public view and makes no `/api/pet-ads/mine` request while the login state is loading (P3).
- shows the tabs once the login state settles as the owner (provider value changes from loading to logged in).
- shows no tabs in the not found and error states of the public profile, even for the owner (P4).
- sends the owner to the login page when `/api/pet-ads/mine` answers 401, while the public profile itself never redirects.
- public paging with `?page=2` still works for a visitor (existing tests) and a `myAdsPage` key in a visitor's URL is ignored.
- the tablist and tab have the ARIA roles and `aria-selected`; Home and End keep focus on the single "My ads" tab (detailed keyboard behavior is covered in `Tabs.test.tsx`).

`AuthNav.test.tsx`:
- shows a "My profile" link for a logged-in user, linking to `/creators/<own id>`.
- clicking "My profile" opens the profile probe route for the own id.
- does not show "My profile" to a visitor.
- does not show "My profile" while the login state is loading.
- the "My profile" link sits before "Log out" and the existing links and Log out still work (existing tests stay green).

`CreatorProfilePage.test.tsx` / existing details tests: the "Posted by" link on the details page is unchanged (existing `PetAdDetails` tests stay green; no new test needed).

## Steps
1. Get approval of this plan, including P1 to P10. Create branch `feature/51-my-profile-tabs-frontend` from `main`.
2. Regenerate `schema.d.ts` as in "Regenerating the API types". Commit: `chore: regenerate API types for my ads and close (#51)`.
3. Shared `Tabs` component with `Tabs.test.tsx`. Commit: `feat: add an accessible tabs component (#51)`.
4. `useMyPetAds` and `parseMyAdsPage` with tests. Commit: `feat: load my own active ads (#51)`.
5. `MyAdsTab` with tests (states, paging, 401, focus). Commit: `feat: add the My ads tab content (#51)`.
6. `CreatorProfilePage`: owner check, tabs for the owner, query keys kept; update the `renderPage` test helper to always provide auth; add the new page tests. Commit: `feat: show the My ads tab on the own profile (#51)`.
7. `AuthNav`: "My profile" link and wrapping, with tests. Commit: `feat: add the My profile link to the header (#51)`.
8. Run `npx tsc -b`, `npm run lint`, `npm test`, `npx prettier --check --end-of-line auto src`, `npm run build`. Check manually at 360px (header with four items, tabs, cards, pager), keyboard only (Tab into tabs, arrows, Home, End, Tab into panel, visible focus) and by touch emulation, against the local API with synthetic users and ads. Open the PR with `Closes #51`, listing P1 to P10 and noting that the close path was also regenerated for #53.

## Risks and open questions
Open questions (none blocking if the user accepts the plan defaults):
- P2: should the owner see only the tabs (proposed), or the public "Ads" section and the tabs? The notes say only that others see no tabs; they do not say what replaces the public list for the owner.
- P3: while the login state loads, show the public view (proposed) or a Spinner in the ads area? The issue only rules out the tabs and the `/mine` request for non-owners.
- P8: the empty and error texts, the tablist name "My profile" and the "Post an ad" action in the empty state are plan wording, not client wording.
- The issue lists "Regenerating the frontend `schema.d.ts`" as out of scope ("separate step after the backend merge"). As asked, this plan does it as its own first commit on this branch (same as #45). Confirm this is fine, or regenerate in a separate issue/PR first.
- Should the new `Tabs` component also be shown on `/styleguide`? Not asked by the issue; not planned.

Dependencies and risks:
- Blocking until step 2: `/api/pet-ads/mine` is not in `schema.d.ts`, so the typed client cannot call it. Regeneration needs the API from current `main` on port 5280; if the port is taken, stop and ask, never kill a process.
- The regenerated file will also contain `/api/pet-ads/{id}/close` (#52). It is unused here; #53 will use it. This is expected, not a stop-and-ask.
- 401 on `/mine` redirects to `/login`, but the auth context still says logged in (issue #41, open). The header then still shows the logged-in links on the login page. Same behavior as the create-ad, assistant and details pages today; fixed by #41, not here.
- The owner check is a UI convenience only. If the ids ever stop matching (for example a different id format from `/me`), the owner simply sees the public view; no data leaks, because `/mine` only returns the caller's own ads.
- For the owner, the public profile request still loads the public ads (page `?page` or 1) only to get the name; the ads in that response are not shown. A small extra payload, accepted for simplicity.
- The cards in "My ads" show "By <own name>" because they are the same `PetAdCard` (#45 D7). Accepted; hiding it would need a new prop on `PetAdCard`, not asked.
- `CreatorProfilePage` now calls `useAuth()`, which throws outside `AuthProvider`. In the app it is always inside `AuthProvider` (`main.tsx`); the page tests must wrap it (step 6).
- Changing `goToPage` and the out-of-range effect to keep other query keys touches #45 code; the existing paging tests must stay green.
- #53 will add "Close ad" next to each card in this tab and refresh the list (`retry`) after a close; #55 will add a second tab item. Both should need no change to `Tabs`.
- Working tree: many untracked files (other issues, meeting notes, `.pr-*-body.md`, `PawsPost/pawspost.db.bak`, `docs/plans/42-...`). None belong to this issue; stage only by explicit path.
