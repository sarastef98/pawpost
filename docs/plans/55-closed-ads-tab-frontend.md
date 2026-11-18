# Plan: Add the Closed ads tab to the owner's profile (#55)

Status: Approved
Layer: frontend

Issue: https://github.com/sarastef98/pawpost/issues/55 (local copy: `docs/issues/closed-ads-tab-frontend.md`)
Depends on: #54 (`GET /api/pet-ads/mine/closed`, merged in the backend: `PetAdsController.ListMineClosedAsync`), #51 (`Tabs`, `CreatorProfilePage`, `MyAdsTab`, `useMyPetAds`, `parseMyAdsPage`, merged), #53 (`ClosePetAdButton` in `MyAdsTab`, merged).
**Blocking prerequisite:** `PawsPost.Web/src/api/schema.d.ts` does **not** contain `/api/pet-ads/mine/closed` yet (checked: only `/api/pet-ads/mine` at line 104). It must be regenerated with `npm run gen:api` before any code in this plan (step 1). The issue lists the regeneration itself as out of scope ("separate step after the backend merge"), see open question Q1.
Source: `docs/meeting-notes/2026-11-16-my-profile-tabs.md`, `docs/meeting-notes/2026-11-16-close-ad.md`.
Rules that apply: `CLAUDE.md`, `.claude/rules/frontend.md`, `.claude/rules/frontend-testing.md`, `.claude/rules/design.md`, `.claude/rules/creator-profile.md` (privacy: public name only, closed ads never shown to others), `docs/design.md`.

## Goal
- On `/creators/:id`, when the logged-in user's id equals the profile id, the existing tab list "My profile" shows a second tab "Closed ads" after "My ads".
- Opening the tab loads `GET /api/pet-ads/mine/closed` and lists the user's own closed ads in the order the API returns (newest created first), with Previous / Next paging.
- The closed cards show the same content as the "My ads" cards plus a "Closed" badge. They are plain cards: no link to `/pet-ads/:id`, not focusable, no click navigation. No "Close ad", reopen, reserve or edit action. No closed date or other new date.
- Loading shows a `Spinner`, no closed ads shows an `EmptyState`, a failure shows an `ErrorMessage` with Try again, a 401 sends the user to `/login`.
- The list is requested only when the tab is opened, and again every time it is opened, so an ad closed in "My ads" appears the next time the tab is opened.
- Visitors and other users see the profile exactly as today (no tabs, no request to the closed endpoint).

## Decisions
The issue decides: the wording "Closed ads", owner-only visibility, plain non-link cards, no actions on closed ads, newest-first order from the API, no extra date, the three states plus 401 to login, refresh on next open, tab accessibility from #51. Everything below is a plan default (D1 to D8), so the user can answer "go with suggestions" or change any of them.

- **D1. Tab order and id.** `items` in `CreatorProfilePage` becomes `[my-ads, closed-ads]`: `{ id: 'closed-ads', label: 'Closed ads', content: () => <ClosedAdsTab /> }`. "My ads" stays first and selected by default (meeting notes: "My ads" first). `Tabs.tsx` is not changed: it already supports any number of items, arrow keys, Home, End and renders only the selected panel.
- **D2. Request only when opened, fresh on every open.** `Tabs` renders `item.content()` only for the selected tab, so `ClosedAdsTab` (and its hook) mounts when the tab is opened and unmounts when another tab is selected. The hook keeps its result in component state only (no cache), so each open sends a new request. This alone covers "refreshed the next time the tab is opened" after a close in "My ads"; no shared refresh signal, context or event is needed, and `MyAdsTab` / `ClosePetAdButton` do not change.
- **D3. Own URL page key `closedAdsPage`.** Paging uses `?closedAdsPage=` (removed for page 1), next to the public `?page=` and `?myAdsPage=`, so the three never mix. New `parseClosedAdsPage(params)` in `creatorProfileData.ts`, same rules as `parseMyAdsPage` (anything that is not a positive whole number gives page 1). A page beyond the last moves to the last page (or removes the key) with `replace`, same as `MyAdsTab`.
- **D4. New hook `useMyClosedPetAds(page)`** in `src/features/petAds/useMyClosedPetAds.ts`, a sibling of `useMyPetAds` with the same states (`loading | unauthorized | error | success`), the same derived-loading key (`[page, attempt]`), the same cancelled flag and the same `toListData` conversion, calling `client.GET('/api/pet-ads/mine/closed', { params: { query: { page, pageSize: PAGE_SIZE } } })`. No `enabled` parameter: the tab is only mounted for the owner when opened. Why a sibling and not a path parameter on `useMyPetAds`: changing the #51/#53 hook is a refactor outside this issue (CLAUDE.md: no drive-by refactors); the cost is about 60 similar lines. Alternative: generalise `useMyPetAds` (see Q3).
- **D5. Plain card: a `closed` option on `PetAdCard`.** `PetAdCard` gets an optional prop `closed?: boolean` (default `false`, so every existing use is unchanged). When `closed` is true the card renders the same `Card` content **without** the `Link` wrapper and adds a "Closed" badge next to the type badge. One card component keeps "same card content as My ads" in one place. Alternative (not chosen): a separate `ClosedPetAdCard` that copies the markup.
- **D6. "Closed" badge (issue: "Exact wording and look are chosen in the plan").** `Badge` gets a third variant `closed` with label "Closed" and classes `bg-border text-text` (existing tokens `--color-border` and `--color-text`, a neutral grey pill; dark text on light grey passes contrast). Shared component, no new token, no hard-coded color. The type badge (Adoption / Sale) stays.
- **D7. Texts (plan wording):**
  - Empty: "You have no closed ads." (no call-to-action link; closing is done from "My ads").
  - Error: "We could not load your closed ads. Please try again." with Try again.
  - Badge: "Closed".
- **D8. Focus.** Same as `MyAdsTab`: after Previous / Next or Try again, once the new result has settled, focus moves to the tab panel (`closest('[role="tabpanel"]')`, which already has `tabIndex={0}`) and it is scrolled into view. On first open, focus stays on the tab (normal tabs behavior).

## Approach
- **Schema first (step 1).** Regenerate `schema.d.ts` so `/api/pet-ads/mine/closed` is typed (CLAUDE.md / frontend rules: never write API types by hand, never edit `schema.d.ts`). Prerequisites: the user stops any API already running; then the API is started on port 5280 **only for this step**, `npm run gen:api` is run in `PawsPost.Web`, and the API is stopped again. Expected diff: only the new `/api/pet-ads/mine/closed` path (GET, query `page`, `pageSize`, responses 200 `PetAdListResponse`, 400, 401). If the diff contains anything else, stop and report instead of continuing.
- **`ClosedAdsTab`** (`src/features/creators/ClosedAdsTab.tsx`) is modelled on `MyAdsTab` without the close parts:
  - `useSearchParams` + `parseClosedAdsPage` (D3), `useMyClosedPetAds(page)` (D4).
  - `unauthorized` renders `<Navigate to="/login" replace />`.
  - `loading` (or out of range) shows `Spinner`; `error` shows `ErrorMessage` (D7) with `onRetry`; empty `items` shows `EmptyState` (D7); otherwise a `ul` grid like `MyAdsTab` (`grid-cols-1` at 360px, `sm:grid-cols-2 lg:grid-cols-3`), each `li` holding `<PetAdCard ad={ad} closed />`, then the same `nav aria-label="Pages"` with Previous, "Page X of Y", Next (`Button variant="secondary"`).
  - No status message region (nothing changes in this tab), no `ClosePetAdButton`, no other button per card.
- **`CreatorProfilePage`**: add the second item to `Tabs` (D1). The owner check `isOwnProfile` is unchanged and still only decides what is shown. The visitor / other-user branch (`CreatorAdList`) is unchanged, so `ClosedAdsTab` is never mounted for them and the closed endpoint is never called.
- **Order**: the client does not sort; it shows `items` in API order.
- **Privacy**: the cards show only the fields `toListData` keeps (title, type, price, location, creator display name). Nothing new is read from the response; no status or date field is shown.
- Shared components and tokens only: `Tabs`, `Card`, `Badge`, `Button`, `Spinner`, `EmptyState`, `ErrorMessage`. No new npm package, no `dangerouslySetInnerHTML`, no hand-written API types.

## API endpoints
Existing endpoints used. No backend change.

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| GET | /api/pet-ads/mine/closed (#54, merged in the backend; **not yet in `schema.d.ts`, blocking until step 1 regenerates it**) | logged in; caller's own `Closed` ads only (owner from the login cookie) | query `page`, `pageSize` (max 100; the client sends `PAGE_SIZE` 20) | `PetAdListResponse` (same as `/mine`, no status or closed date field), newest created first | 200, 400 (`ValidationProblemDetails`), 401 (`ProblemDetails`) |
| GET | /api/creators/{id} (unchanged) | public | path `id`, query `page` | public profile (display name and `Active` ads) | 200, 400, 404 |
| GET | /api/auth/me (unchanged, through `useAuth` only) | - | none | `UserResponse`; only `id` is used for the owner check | 200, 401 |

`GET /api/pet-ads/{id}` is not called from this tab (it returns 404 for closed ads, also for the owner, and the details endpoint stays unchanged).

## Screens and components
- Route: `/creators/:id` (existing). No new route. New URL key `closedAdsPage` (D3).
- Components:
  - New: `src/features/creators/ClosedAdsTab.tsx`, `src/features/petAds/useMyClosedPetAds.ts`.
  - Changed: `src/features/creators/CreatorProfilePage.tsx` (second tab), `src/features/creators/creatorProfileData.ts` (`parseClosedAdsPage`), `src/features/petAds/PetAdCard.tsx` (`closed` prop), `src/components/Badge.tsx` (`closed` variant).
  - Unchanged: `Tabs.tsx`, `MyAdsTab.tsx`, `ClosePetAdButton.tsx`, `useClosePetAd.ts`, `useMyPetAds.ts`, `CreatorAdList.tsx`, `useCreatorProfile.ts`, `AuthContext.tsx`, `App.tsx`, `client.ts`.
- What each user sees:
  - Owner on own profile: tabs "My ads" (selected) and "Closed ads".
  - Other logged-in user, visitor: name and public `Active` ads, no tabs (unchanged, no login needed, no redirect).
- States (Closed ads tab): loading `Spinner`; empty `EmptyState` "You have no closed ads."; error `ErrorMessage` "We could not load your closed ads. Please try again." with Try again; 401 redirect to `/login`; success: grid of plain closed cards and the pager.
- Forms and validation: none. The only input is the page number from the URL, parsed defensively (D3); the server validates `page` / `pageSize`.
- Accessibility and 360px:
  - Tabs: `tab` / `tabpanel` roles, `aria-selected`, `aria-controls` / `aria-labelledby`, arrow keys (wrap), Home, End, visible focus from the global `:focus-visible`, `min-h-11` touch targets; all from the existing `Tabs`. At 360px the tab list wraps (`flex-wrap`).
  - Closed cards contain no link and no button, so Tab moves from the tab panel straight to the pager. The card title stays an `h3`.
  - Pager buttons are real `button` elements, disabled at the ends.

## Data changes
none

## Files to create or change
Create:
- `PawsPost.Web/src/features/petAds/useMyClosedPetAds.ts` and `useMyClosedPetAds.test.tsx`: the closed list request and its states (D4).
- `PawsPost.Web/src/features/creators/ClosedAdsTab.tsx` and `ClosedAdsTab.test.tsx`: the tab content.

Change:
- `PawsPost.Web/src/api/schema.d.ts`: regenerated only by `npm run gen:api` (step 1, never edited by hand).
- `PawsPost.Web/src/components/Badge.tsx` (+ `Badge.test.tsx`): `closed` variant (D6).
- `PawsPost.Web/src/features/petAds/PetAdCard.tsx` (+ `PetAdCard.test.tsx`): `closed` prop (D5).
- `PawsPost.Web/src/features/creators/creatorProfileData.ts` (+ `creatorProfileData.test.ts`): `parseClosedAdsPage` (D3).
- `PawsPost.Web/src/features/creators/CreatorProfilePage.tsx` (+ `CreatorProfilePage.test.tsx`): second tab (D1).

Not changed: `Tabs.tsx`, `MyAdsTab.tsx`, `ClosePetAdButton.tsx`, `useMyPetAds.ts`, `index.css` (no new token), `package.json`, the backend.

## Tests to write
Vitest and Testing Library in the style of the existing tests: `vi.mock('../../api/client')` (the API client is the only mock), `AuthContext.Provider` values per status and user, `MemoryRouter` with probe routes (`/login`, `/pet-ads/:id`, `/creators/:id`) and a URL display, `user-event`, `findBy` queries, elements by role, name or text. Results from `okResult`, `failResult`, `deferred`, `sampleListItem`. One behavior per test. Synthetic data only.

`Badge.test.tsx`:
- shows "Closed" for the closed variant.

`PetAdCard.test.tsx` (existing tests stay):
- a closed card shows the title, type badge, price and location like a normal card.
- a closed card shows a "Closed" badge.
- a closed card has no link (no element with role `link`).
- a normal card still links to `/pet-ads/:id` and shows no "Closed" badge.

`creatorProfileData.test.ts`:
- `parseClosedAdsPage` reads `closedAdsPage`, gives 1 when missing, zero, negative, not a number or too large, and ignores `page` and `myAdsPage`.

`useMyClosedPetAds.test.tsx`:
- requests `/api/pet-ads/mine/closed` with the page and page size.
- reports success with the converted list on 200.
- reports unauthorized on 401.
- reports error on 500, on a rejected request and on a malformed body.
- shows loading again for a new page and ignores a slow older response.
- `retry` sends the request again.

`ClosedAdsTab.test.tsx` (rendered inside `Tabs`, like `MyAdsTab.test.tsx`):
- shows a spinner while the closed ads load.
- lists the closed ads in the order the API returns them.
- shows "Closed" on each card.
- the closed cards are not links and clicking a card does not navigate (URL unchanged, details probe not shown).
- shows no "Close ad", reopen, reserve or edit button.
- shows no date on the cards.
- shows "You have no closed ads." when the list is empty.
- shows "We could not load your closed ads. Please try again." on a 500, without technical details.
- Try again requests the list again and shows the result.
- sends the user to the login page on 401.
- Next requests page 2 and sets `closedAdsPage=2` in the URL; Previous goes back and removes the key.
- Previous is disabled on page 1 and Next on the last page.
- reads the start page from `closedAdsPage` and leaves `page` and `myAdsPage` untouched.
- a page beyond the last moves to the last page (and removes the key when there are no closed ads).
- moves focus to the tab panel after changing page.

`CreatorProfilePage.test.tsx` (existing tests stay):
- shows the tabs "My ads" and "Closed ads" on the own profile, with "My ads" selected.
- recognises the own profile when the ids differ only in letter case.
- does not request the closed ads before the "Closed ads" tab is opened.
- opening "Closed ads" requests `/api/pet-ads/mine/closed` and shows the list in its tab panel.
- does not show the tabs on another user's profile and never requests the closed ads endpoint.
- does not show the tabs for a visitor, never requests the closed ads endpoint, and loads the public profile without a redirect to login.
- refreshes the closed list after an ad was closed: open "Closed ads" (empty), switch to "My ads", close an ad (mock `POST` 204), open "Closed ads" again, a second closed request is sent and the closed ad is shown.
- tab roles: two `tab` elements in a `tablist`, `aria-selected` follows the selection, each tab controls its `tabpanel` and the panel is labelled by its tab.
- keyboard: ArrowRight / ArrowLeft move between the two tabs and wrap, Home selects "My ads", End selects "Closed ads", focus follows the selected tab.

## Steps
1. Get approval of this plan, including D1 to D8 and Q1. Create branch `feature/55-closed-ads-tab-frontend` from `main`. Baseline from `PawsPost.Web`: `npm test` (record counts) and `npx tsc -b`.
2. Regenerate the schema (blocking prerequisite). The user first stops any API already running. Start the API on port 5280 only for this step, run `npm run gen:api` in `PawsPost.Web`, stop the API. Check that the diff only adds `/api/pet-ads/mine/closed`. Commit: `chore: regenerate the API schema for closed ads (#55)` (or a separate PR, depending on Q1).
3. `Badge` closed variant and `PetAdCard` `closed` prop, with tests. Commit: `feat: add a plain closed ad card (#55)`.
4. `parseClosedAdsPage` and `useMyClosedPetAds`, with tests. Commit: `feat: load my closed ads (#55)`.
5. `ClosedAdsTab` with tests. Commit: `feat: add the closed ads tab content (#55)`.
6. Add the tab to `CreatorProfilePage`, with tests (visibility, lazy load, refresh after close, keyboard and ARIA). Commit: `feat: show the Closed ads tab on my profile (#55)`.
7. Run `npx tsc -b`, `npm run lint`, `npm test`, `npx prettier --check --end-of-line auto src` (never `npm run format`, issue #42), `npm run build`. Check manually at 360px, keyboard only and touch emulation against the local API with synthetic users and ads (close an ad in "My ads", open "Closed ads", see it there, cannot open it). Open the PR with `Closes #55`, listing D1 to D8 and the new texts.

## Risks and open questions
Open questions:
- **Q1 (blocking until answered).** `schema.d.ts` lacks `/api/pet-ads/mine/closed`, and the issue lists the regeneration as out of scope ("separate step after the backend merge"). Plan default: do it as the first commit of this branch (step 2). Alternative: a separate small PR first, and this branch starts after it is merged. Either way the API must be started on port 5280 only for the regeneration, after the user has stopped any API already running.
- Q2. D6 / D7: the "Closed" badge look (neutral grey) and the texts "You have no closed ads." and "We could not load your closed ads. Please try again." are plan wording, not client wording.
- Q3. D4: a sibling hook duplicates most of `useMyPetAds`. Alternative: give `useMyPetAds` a path parameter (touches #51/#53 code). Please confirm the sibling hook is fine.
- Q4. Should the new "Closed" badge be shown on `/styleguide`? Not asked by the issue; not planned.

Dependencies and risks:
- The selected tab is not stored in the URL (unchanged `Tabs` behavior from #51). After a reload or Back with `?closedAdsPage=2`, "My ads" is selected again; opening "Closed ads" then shows page 2. The issue says the tabs need no change in how they work, so this is accepted, not changed here.
- `closedAdsPage` stays in the URL while "My ads" is selected. It does not affect "My ads" (own key).
- Each open of the tab sends a request (D2), so a slow network shows the spinner briefly on every open. This is the intended "no stale list" behavior.
- 401 sends the user to `/login`, but the auth context still says logged in (issue #41, open). Same behavior as `MyAdsTab` today; fixed by #41, not here.
- The owner check is a UI convenience. The server only ever returns the caller's own closed ads, so a wrong check cannot leak another user's closed ads.
- The regenerated schema could include other backend changes merged since the last generation. Step 2 stops and reports if the diff contains more than the new path.
- Working tree: many untracked files (other issues, meeting notes, `.pr-*-body.md`, `PawsPost/pawspost.db.bak`, `docs/plans/42-...`). None belong to this issue; stage only by explicit path.
