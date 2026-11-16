# Plan: Add the close ad action for the owner (#53)

Status: Approved
Layer: frontend

Issue: https://github.com/sarastef98/pawpost/issues/53 (local copy: `docs/issues/close-pet-ad-frontend.md`)
Depends on: #52 (`POST /api/pet-ads/{id}/close`, merged), #51 (`MyAdsTab`, `useMyPetAds`, `Tabs`, merged on `main`), #20 (`PetAdDetailsPage`, `PetAdDetails`, `usePetAd`), #14 (`AuthContext`, `useAuth`, `user.id`). All exist in the code. `PawsPost.Web/src/api/schema.d.ts` already contains `/api/pet-ads/{id}/close` with `post`, path `id`, responses 204 (no content), 400 (`ValidationProblemDetails`), 401, 403, 404, 409 (`ProblemDetails`) (checked, lines 182-265). No regeneration is needed.
Related, not done here: #55 (`closed-ads-tab-frontend.md`, the "Closed ads" tab), #41 (`auth-context-on-401-frontend.md`, auth state after a 401), favorites issues (removing a closed ad from favorites).
Source: `docs/meeting-notes/2026-11-16-close-ad.md`, `docs/meeting-notes/2026-11-16-my-profile-tabs.md`.
Rules that apply: `CLAUDE.md`, `.claude/rules/frontend.md`, `.claude/rules/design.md`, `.claude/rules/frontend-testing.md`, `docs/design.md`.

## Goal
- On `/pet-ads/:id`, the logged-in owner of the ad sees one "Close ad" button below the ad. Other logged-in users do not see it. Visitors never reach the page (it already redirects logged-out users to `/login`), so they never see it either.
- In the "My ads" tab on the own profile, every ad has a "Close ad" button below its card.
- The button opens an accessible confirmation dialog (new shared `Dialog` component). Cancel or Escape closes it and sends nothing. Confirm sends `POST /api/pet-ads/{id}/close` once, even on a double click.
- Success on the details page: go to the home page, which shows "Your ad was closed." The closed ad is not in the home list (the list is loaded fresh).
- Success in the tab: stay on the tab, show "Your ad was closed.", reload the list. If the page is now empty, the existing rules show the empty state (page 1) or move to the last page (page > 1).
- 401 sends the user to `/login`. 403, 404 and 409 show a friendly message in the dialog. Other errors show an `ErrorMessage` with Try again in the dialog. No technical details anywhere.
- No reserve option, no other status action.

## Decisions
The issue decides: the button text "Close ad", where it shows (details page for the owner, each ad in "My ads"), a confirmation that says the ad is removed for everyone and cannot be undone, redirect to home with a short message after a close from the details page, refresh in place after a close from the tab, 401 to login, friendly 403/404/409 messages, ErrorMessage with Try again for other errors. Everything below is a plan default (D1 to D12), so the user can answer "go with suggestions" or change any of them.

- **D1. Owner check on the details page.** `isOwner = status === 'loggedIn' && user !== null && ad.creator !== null && user.id.toLowerCase() === ad.creator.id.toLowerCase()`. Same case-insensitive Guid comparison as `CreatorProfilePage` (#51 P1). A missing creator means no button. The check only decides what is shown; the server decides (403). `email` is never read.
- **D2. "My ads" tab: no owner check per card.** Every item comes from `GET /api/pet-ads/mine`, which only returns the caller's own ads, and the tab is only mounted for the owner. So every card gets the button.
- **D3. Placement and look.** Details page: the button sits below the ad card (`PetAdDetails` stays a pure view, unchanged), left-aligned, `variant="secondary"`. Tab: the button sits in the existing `li` wrapper below the card, outside the card link (no nested interactive elements), full width at 360px (`w-full sm:w-auto`). The trigger is `secondary`, the confirm button in the dialog is `danger` (design: danger for destructive actions; one primary button per screen is kept, there is no primary button here).
- **D4. Accessible name of the trigger in the tab.** There are many "Close ad" buttons in the list, so each has visible text "Close ad" plus a visually hidden suffix with the ad title: accessible name "Close ad: Friendly Rex" (`<span className="sr-only">: {title}</span>`). On the details page there is only one, so the name is just "Close ad". `PetAdCard` does not change.
- **D5. Dialog wording (plan text):**
  - Title (`h2`, the dialog's accessible name): "Close this ad?"
  - Text (the dialog's description): "Closing removes this ad for everyone. This cannot be undone."
  - Buttons: "Cancel" (secondary) and "Close ad" (danger). While the request runs, the confirm button reads "Closing..." (progress).
- **D6. Dialog behavior.** New shared `src/components/Dialog.tsx`, a custom modal (not native `<dialog>`, see Approach for why):
  - `role="dialog"`, `aria-modal="true"`, `aria-labelledby` (title) and `aria-describedby` (text), rendered through `createPortal` into `document.body`.
  - On open, focus moves to the **Cancel** button (the safe choice for a destructive, final action). Tab and Shift+Tab are trapped inside the dialog. While open, every other child of `document.body` gets `inert` (so the page behind is not reachable by keyboard, mouse or screen reader), and `document.body` gets `overflow: hidden` (no scroll behind). Both are restored on close.
  - Escape = Cancel. Clicking the backdrop does nothing (avoids an accidental dismiss on touch; Cancel is always visible).
  - On cancel, focus returns to the element that opened the dialog.
  - While the request runs, Cancel and Escape do nothing (the request cannot be taken back, and closing the dialog mid-request would hide its result).
- **D7. One request only.** A `useRef` "in flight" flag in the close hook is set synchronously before the request, so a second click in the same tick is ignored even before React re-renders. The confirm button uses `aria-disabled="true"` (not `disabled`) while running, so it keeps keyboard focus (a disabled button drops focus to `body`, which would escape the focus trap). The `Button` component already styles `aria-disabled`.
- **D8. Result handling (messages are plan text):**

  | Response | What happens |
  |---|---|
  | 204 | Details page: `navigate('/', { replace: true, state: { notice: 'Your ad was closed.' } })`. `replace` so Back does not return to the page of a closed ad. Tab: dialog closes, status message "Your ad was closed.", list reloads. |
  | 401 | `navigate('/login', { replace: true })` (same as the other pages). |
  | 403 | Dialog stays open with an alert: "You can only close your own ads." The confirm button is removed; one button "OK" closes the dialog. Nothing reloads. |
  | 404, 409, 400 | Dialog stays open with an alert: "This ad is already closed or no longer exists." The confirm button is removed; "OK" closes the dialog. Then the page reloads its data: the details page calls `retry()` (the ad now answers 404, so the existing text "We could not find this ad. It may have been removed." shows), the tab reloads the list (the ad disappears). 400 (malformed id) is grouped here because trying again cannot fix it, same as `usePetAd`. |
  | Other status, network failure | `ErrorMessage` inside the dialog: "We could not close this ad. Please try again." with Try again (sends the close again, same one-request guard). The dialog's own confirm button is hidden while the error shows, so there is one action; Cancel stays. |

  New words in total: "Close this ad?", "Closing removes this ad for everyone. This cannot be undone.", "Cancel", "Close ad", "Closing...", "OK", "Your ad was closed.", "You can only close your own ads.", "This ad is already closed or no longer exists.", "We could not close this ad. Please try again.".
- **D9. Feedback on the home page.** The home page gets the same router-state notice that the details page already shows after posting an ad (`Notice` in `PetAdDetailsPage.tsx`: read once, router state cleared, `role="status"` filled on the next tick). To reuse it, `Notice` moves unchanged into a shared `src/components/Notice.tsx`, used by `PetAdDetailsPage` (behavior unchanged, existing tests prove it) and `HomePage` (new, above the list). Alternative (not chosen): a copy in `HomePage`, which duplicates logic. `LoginPage` keeps its own version (no drive-by refactor).
- **D10. Feedback in the tab.** `MyAdsTab` keeps an always-rendered `p role="status"` above the list (empty and `sr-only` when there is no message, so the later text is announced). After a 204 it shows "Your ad was closed." in `text-success`. It is cleared when the user changes page. It stays while the list reloads, so it is visible together with the refreshed list, the empty state or the last page.
- **D11. Focus after the dialog closes.**
  - Cancel / Escape / OK after 403: back to the trigger.
  - OK after 404/409: back to the trigger, then the reload replaces the content; focus then goes to the heading "Ad details" (details, existing `pendingFocus` + `headingRef`) or to the tab panel (tab, existing `pendingFocus` in `MyAdsTab`).
  - 204 in the tab: the trigger is gone, so focus goes to the tab panel once the reloaded list settles (existing `pendingFocus`). The status message is announced by the live region.
  - 204 on the details page: the app navigates to `/`; focus is not moved, like every other navigation in the app; the notice is announced through `role="status"`.
- **D12. File placement.** `Dialog` is shared (`src/components/`, design rule: add missing shared components there). The close request hook `useClosePetAd` and the `ClosePetAdButton` (trigger + dialog content) are in `src/features/petAds/` (backend feature `PetAds`), used by `PetAdDetailsPage` and by `MyAdsTab` (same cross-feature import that `MyAdsTab` already does for `PetAdCard` and `useMyPetAds`).

## Approach
- **Why a custom dialog and not native `<dialog>`.** Native `showModal()` gives focus containment, Escape and a top layer for free, but jsdom (used by Vitest here) has historically not implemented `showModal()`/`close()`, so the tests would need a polyfill of a browser API in `src/test/setup.ts`. That conflicts with "mock only the API client". A small custom modal is fully testable as is. If the implementer confirms that the installed jsdom (`^31.0.1`) implements `showModal()` with Escape (`cancel` event) and focus, native `<dialog>` is an acceptable swap with the same props and tests; record the choice in the PR.
- **Shared `Dialog` component** (`src/components/Dialog.tsx`). Props: `title: string`, `description: ReactNode`, `children: ReactNode` (the buttons and any message), `onCancel: () => void`, `cancelDisabled?: boolean` (D6, ignores Escape while true), `initialFocusRef: RefObject<HTMLElement | null>`. Mounted only while open (the parent renders it conditionally), so no `open` prop. Behavior:
  - On mount: remember `document.activeElement`, set `inert` on the other `body` children, set `body.style.overflow = 'hidden'`, focus `initialFocusRef` (or the first focusable element). On unmount: undo both and focus the remembered element if it is still in the document.
  - `onKeyDown` on the dialog: Escape calls `onCancel` unless `cancelDisabled`; Tab / Shift+Tab wrap between the first and last focusable elements inside the dialog.
  - Ids from `useId()`.
  - Markup and tokens: overlay `fixed inset-0 z-50 flex items-center justify-center bg-text/50 p-4`; panel `w-full max-w-md rounded-card bg-surface p-6 shadow-card flex flex-col gap-4`; title `text-xl break-words`; description `text-base`; button row `flex flex-col-reverse gap-2 sm:flex-row sm:justify-end` (stacked, full-width buttons at 360px, confirm on top; side by side from `sm`). No hex colors and no arbitrary-value classes (`designTokens.test.ts` checks shared components). Focus styles come from the global `:focus-visible`.
- **Hook `useClosePetAd()`** (`src/features/petAds/useClosePetAd.ts`). Returns `{ state, close(id), reset() }`, state `idle | pending | closed | unauthorized | forbidden | gone | error`. `close` returns immediately if the in-flight ref is set (D7). Calls `client.POST('/api/pet-ads/{id}/close', { params: { path: { id } } })`. Mapping: `response.ok` (204) -> `closed`; 401 -> `unauthorized`; 403 -> `forbidden`; 404, 409, 400 -> `gone`; anything else or a thrown error -> `error`. The response body is never shown or logged. Ignores a result that arrives after unmount (cancelled flag, like the other hooks).
- **`ClosePetAdButton`** (`src/features/petAds/ClosePetAdButton.tsx`). Props: `adId: string`, `adTitle?: string` (for the hidden name suffix, D4), `className?: string`, `onClosed: () => void`, `onGone: () => void`. Renders the trigger `Button` (secondary, "Close ad"), and while open the `Dialog` with the content from D5/D8. Uses `useClosePetAd`. On `closed` it calls `onClosed()`; on `unauthorized` it renders `<Navigate to="/login" replace />`; on "OK" after `gone` it closes the dialog and calls `onGone()`; on "OK" after `forbidden` it only closes the dialog. Opening the dialog again resets the hook state.
- **`PetAdDetailsPage`** (`LoadedAd`): read `user` from `useAuth()`, compute `isOwner` (D1), and in the success state render `<ClosePetAdButton adId={ad.id} onClosed={goHome} onGone={reloadAd} />` below `PetAdDetails` when `isOwner`. `goHome` navigates as in D8, `reloadAd` sets `pendingFocus` and calls `retry()`. The `Notice` function moves to the shared component (D9).
- **`HomePage`**: render the shared `Notice` between the welcome text and `PetAdList`.
- **`MyAdsTab`**: inside each `li`, below `PetAdCard`, `<ClosePetAdButton adId={ad.id} adTitle={ad.title} className="w-full sm:w-auto self-start" onClosed={handleClosed} onGone={reloadList} />`. `handleClosed` sets the status message (D10), sets `pendingFocus` and calls `retry()`; `reloadList` sets `pendingFocus` and calls `retry()`. The existing out-of-range effect already moves a `myAdsPage` beyond the last page to the last page (or removes the key), and the existing empty state shows when page 1 is empty. `goToPage` also clears the message.
- **Reserve.** Nothing in this work sets or offers `Reserved`; tests assert that no "Reserve" control exists.
- Shared components and tokens only: `Button`, `Card`, `Spinner`, `EmptyState`, `ErrorMessage`, `Tabs`, new `Dialog`, moved `Notice`. No new npm package, no `dangerouslySetInnerHTML`, no hand-written API types.

## API endpoints
Existing endpoints used (no API change, no schema regeneration).

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| POST | /api/pet-ads/{id}/close (#52) | logged in and owner (caller from the login cookie) | path `id`, no body | none | 204 (closed), 400 (`ValidationProblemDetails`, id not a Guid), 401 (`ProblemDetails`), 403 (`ProblemDetails`, not the owner, also for an already closed ad of someone else), 404 (`ProblemDetails`, unknown or already gone id), 409 (`ProblemDetails`, owner closing an ad that is not `Active`) |
| GET | /api/pet-ads/{id} (#20, unchanged) | logged in | path `id` | `PetAdDetailsResponse` incl. `creator { id, displayName }`; used for the owner check | 200, 400, 401, 404 |
| GET | /api/pet-ads/mine (#50, unchanged) | logged in, own ads only | query `page`, `pageSize` | `PetAdListResponse` | 200, 400, 401 |
| GET | /api/pet-ads (unchanged) | public | query | `PetAdListResponse`; home list after the redirect, closed ads are not in it | 200, 400 |
| GET | /api/auth/me (#3, unchanged, through `useAuth` only) | - | none | `UserResponse { id, email }`; only `id` is used | 200, 401 |

The client never reads the `ProblemDetails` title or detail of the close endpoint; it maps the status code to its own friendly text (D8).

## Screens and components
- Routes: `/pet-ads/:id` (existing, logged in), `/creators/:id` "My ads" tab (existing, owner only), `/` (existing; gets the notice). No new route.
- Components:
  - New shared: `src/components/Dialog.tsx` (D6, Approach).
  - Moved to shared: `src/components/Notice.tsx` (from `PetAdDetailsPage.tsx`, unchanged logic, D9).
  - New: `src/features/petAds/ClosePetAdButton.tsx`, `src/features/petAds/useClosePetAd.ts`.
  - Changed: `PetAdDetailsPage.tsx`, `HomePage.tsx`, `MyAdsTab.tsx`.
  - Unchanged: `PetAdDetails.tsx`, `PetAdCard.tsx`, `usePetAd.ts`, `useMyPetAds.ts`, `Tabs.tsx`, `CreatorProfilePage.tsx`, `AuthContext.tsx`, `useAuth.ts`, `App.tsx`, `client.ts`, `schema.d.ts`.
- What each user sees:
  - Details page, owner: ad card, then "Close ad".
  - Details page, other logged-in user: ad card only. Visitor: redirected to `/login` (existing).
  - Details page loading / not found / error: no button (only rendered in the success state).
  - My ads tab: each card with "Close ad" below it. Loading, empty, error states unchanged.
- Dialog states:
  - Confirm: title, text, Cancel, "Close ad".
  - Pending: "Closing..." on the confirm button (`aria-disabled`), Cancel inactive, Escape ignored.
  - Forbidden / gone: alert text, single "OK".
  - Error: `ErrorMessage` with Try again, plus Cancel.
- Home page: "Your ad was closed." in the `role="status"` region (success color) when arriving from a close; nothing otherwise; not shown again on reload or Back (router state is cleared, existing `Notice` logic).
- Forms and validation: none. The only input is the ad id from the loaded data; the server validates it.
- Accessibility and 360px:
  - Trigger and dialog buttons are real `button` elements with `min-h-11` (44px targets), visible focus from the global `:focus-visible`.
  - Dialog: labelled and described, focus in on open (Cancel), focus trapped, Escape cancels, focus returns to the trigger, background inert, no scroll behind.
  - At 360px: dialog is the viewport width minus `p-4`, buttons stacked full width; long titles wrap (`break-words`); the tab's "Close ad" button is full width under each card.
  - Messages: success via `role="status"`, dialog errors via `role="alert"`.

## Data changes
none

## Files to create or change
Create:
- `PawsPost.Web/src/components/Dialog.tsx` and `Dialog.test.tsx`: shared accessible modal (D6).
- `PawsPost.Web/src/components/Notice.tsx` and `Notice.test.tsx`: router-state notice moved from `PetAdDetailsPage.tsx` (D9).
- `PawsPost.Web/src/features/petAds/useClosePetAd.ts` and `useClosePetAd.test.tsx`: the close request and outcome mapping.
- `PawsPost.Web/src/features/petAds/ClosePetAdButton.tsx` and `ClosePetAdButton.test.tsx`: trigger, dialog content, results.

Change:
- `PawsPost.Web/src/features/petAds/PetAdDetailsPage.tsx` (+ `PetAdDetailsPage.test.tsx`): owner check, button, redirect, reload on gone; import `Notice` from the shared component. Existing tests use `user.id 'u1'` and ads without a creator, so they do not show the button and stay unchanged.
- `PawsPost.Web/src/features/home/HomePage.tsx` (+ `HomePage.test.tsx`): render `Notice`. The tests may need router state in `MemoryRouter` `initialEntries`.
- `PawsPost.Web/src/features/creators/MyAdsTab.tsx` (+ `MyAdsTab.test.tsx`): button per card, status message, reload, focus. Its test mock must add `POST: vi.fn()` to the mocked client.

Not changed: `PetAdDetails.tsx`, `PetAdCard.tsx`, `usePetAd.ts`, `useMyPetAds.ts`, `Tabs.tsx`, `CreatorProfilePage.tsx`, `LoginPage.tsx`, `App.tsx`, `client.ts`, `schema.d.ts`, `index.css` (no new token), `package.json`, the backend.

## Tests to write
Vitest and Testing Library in the style of the existing tests: `vi.mock('../../api/client')` with `GET` and `POST` mocked (the API client is the only mock), `AuthContext.Provider` values per status and user, `MemoryRouter` with probe routes (`/`, `/login`, `/pet-ads/:id`, `/creators/:id`), `user-event`, `findBy` queries, elements by role, name or text. Results from `okResult`, `noContentResult`, `failResult`, `deferred`, `sampleAd`, `sampleListItem`, `testUser`. One behavior per test. Synthetic data only.

`Dialog.test.tsx` (rendered from a small test harness with an "Open" button):
- renders a dialog with `aria-modal`, named by its title and described by its text.
- moves focus to the given initial element when it opens.
- Tab from the last button moves focus to the first, Shift+Tab from the first moves to the last.
- Escape calls `onCancel`.
- Escape does nothing while `cancelDisabled` is true.
- returns focus to the opening button when it closes.
- makes the page behind inert while open and restores it after closing (the harness "Open" button is not reachable / has `inert` on its container).
- stops the page from scrolling while open and restores it after closing.
- clicking the backdrop does not close the dialog.

`Notice.test.tsx`: the moved tests' behavior in isolation (shows the router-state notice in a status region, clears the router state, shows nothing without state). The existing `PetAdDetailsPage` notice tests stay and pass unchanged.

`useClosePetAd.test.tsx`:
- posts to `/api/pet-ads/{id}/close` with the id in the path and no body.
- reports closed on 204.
- reports unauthorized on 401, forbidden on 403, gone on 404, 409 and 400.
- reports error on 500 and on a rejected request.
- a second `close` while the first is pending sends no second request.

`ClosePetAdButton.test.tsx`:
- shows a "Close ad" button.
- opens the dialog "Close this ad?" with the text "Closing removes this ad for everyone. This cannot be undone." when clicked.
- moves focus to Cancel when the dialog opens.
- Cancel closes the dialog, sends no request, and returns focus to "Close ad".
- Escape closes the dialog, sends no request, and returns focus to "Close ad".
- confirming sends one close request for the ad id.
- double-clicking the confirm button sends one request (`deferred`).
- shows "Closing..." with `aria-disabled` on the confirm button while the request runs, and Escape does not close the dialog meanwhile.
- calls `onClosed` after 204.
- sends the user to the login page on 401.
- shows "You can only close your own ads." on 403, without the confirm button; OK closes the dialog and does not call `onGone`.
- shows "This ad is already closed or no longer exists." on 404 and on 409; OK closes the dialog and calls `onGone`.
- shows "We could not close this ad. Please try again." with Try again on a 500 and on a network failure, without technical details (the `ProblemDetails` title from the mocked error body is not on screen).
- Try again sends the request again and calls `onClosed` on 204.
- uses the ad title in the accessible name when `adTitle` is given ("Close ad: Friendly Rex").
- offers no reserve option (no control named "Reserve" in the trigger or dialog).

`PetAdDetailsPage.test.tsx` (new tests; existing tests stay):
- shows "Close ad" to the logged-in owner (user id equals `creator.id`).
- recognises the owner when the ids differ only in letter case.
- does not show "Close ad" to another logged-in user.
- does not show "Close ad" when the ad has no creator.
- a visitor is sent to the login page and never sees "Close ad" (existing redirect, asserted for the button).
- shows no reserve option or other status action for the owner.
- after a successful close, opens the home page and shows "Your ad was closed." (home probe route renders the shared `Notice`).
- after a successful close, Back does not return to the closed ad (history entry replaced).
- on 409, shows the message, and after OK shows "We could not find this ad. It may have been removed." (the reload answers 404) with focus on the heading.
- on 401 from the close, opens the login page.

`HomePage.test.tsx`:
- shows "Your ad was closed." when opened with that notice in router state.
- shows no notice text when opened directly.

`MyAdsTab.test.tsx` (new tests; existing tests stay):
- shows a "Close ad" button for each ad, named with the ad's title.
- cancel in the dialog sends no close request and keeps the list as it was.
- after a successful close, shows "Your ad was closed." and requests the list again.
- after a successful close, the closed ad is no longer shown (second `GET` returns the list without it).
- closing the only ad on page 1 shows the empty state "You have no active ads right now." together with the message.
- closing the last ad on page 2 moves to page 1 (`myAdsPage` removed from the URL) and shows that page.
- moves focus to the tab panel after the list reloads.
- on 404, after OK the list is reloaded.
- on 401 from the close, opens the login page.
- changing page clears the "Your ad was closed." message.

## Steps
1. Get approval of this plan, including D1 to D12. Create branch `feature/53-close-pet-ad-frontend` from `main`. Baseline from `PawsPost.Web`: `npm test` (record counts) and `npx tsc -b`.
2. Shared `Dialog` with `Dialog.test.tsx`. Commit: `feat: add an accessible dialog component (#53)`.
3. Move `Notice` to `src/components/Notice.tsx` with `Notice.test.tsx`; `PetAdDetailsPage` imports it; existing notice tests stay green. Commit: `refactor: share the router notice component (#53)`.
4. `useClosePetAd` with tests. Commit: `feat: add the close ad request (#53)`.
5. `ClosePetAdButton` with tests. Commit: `feat: add the close ad button with confirmation (#53)`.
6. Details page: owner check, button, redirect, reload on gone; `HomePage` notice; tests. Commit: `feat: let the owner close an ad from its page (#53)`.
7. `MyAdsTab`: button per card, message, reload, focus; tests. Commit: `feat: let the owner close an ad from My ads (#53)`.
8. Run `npx tsc -b`, `npm run lint`, `npm test`, `npx prettier --check --end-of-line auto src`, `npm run build`. Check manually at 360px (details, tab, dialog), keyboard only (open, Tab trap, Escape, focus return, visible focus) and touch emulation, against the local API with synthetic users and ads. Open the PR with `Closes #53`, listing D1 to D12 and the new texts.

## Risks and open questions
Open questions (none blocking if the user accepts the plan defaults):
- D5 / D8: all dialog and message texts are plan wording, not client wording. The issue's example "This ad is already closed or no longer exists" is used as is.
- D6: focus goes to Cancel on open (safer for a final action); the alternative is the dialog title. Backdrop click does nothing; the alternative is backdrop = Cancel.
- D8: 403/404/409 are shown inside the dialog with a single "OK" button (new word). Alternative: close the dialog and show the message on the page.
- D8: after a close from the details page the history entry is replaced, so Back skips the closed ad. Alternative: a normal push, and Back then shows the existing "We could not find this ad" state.
- D9: moving `Notice` into a shared component touches #20/#23 code. It is needed to show the home message without duplicating logic; please confirm, or say if a copy in `HomePage` is preferred.
- Should the new `Dialog` also be shown on `/styleguide`? Not asked by the issue; not planned.

Dependencies and risks:
- 401 sends the user to `/login`, but the auth context still says logged in (issue #41, open). Same behavior as the other pages today; fixed by #41, not here.
- jsdom and focus: `inert` and focus trapping are implemented by the component, not by the browser's modal mode, so tests prove them directly. If jsdom does not honor `inert` for user-event Tab order, the test asserts the `inert` attribute instead; the manual keyboard check in step 8 covers the real browser.
- `inert` on `body` children assumes the app's root and the portal container are direct children of `body` (true for `main.tsx` and for Testing Library's container). If another portal appears later, it would also be made inert while the dialog is open, which is the intended behavior.
- The owner check is a UI convenience. If the ids ever stop matching, the owner simply does not see the button on the details page; no data leaks, and the server still returns 403 for non-owners.
- Closing an ad does not remove it from anyone's favorites in this work (favorites issues). The closed ad stays visible to its owner only in the future "Closed ads" tab (#55).
- After a close in the tab, the list request runs again, so a slow network shows the Spinner briefly; the success message stays above it.
- Working tree: many untracked files (other issues, meeting notes, `.pr-*-body.md`, `PawsPost/pawspost.db.bak`, `docs/plans/42-...`). None belong to this issue; stage only by explicit path.
