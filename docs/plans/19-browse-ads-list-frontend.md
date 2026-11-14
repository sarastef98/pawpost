# Plan: Show the list of active ads on the home page (#19)

Issue: https://github.com/sarastef98/pawpost/issues/19

Status: Approved
Layer: frontend

Depends on: #2 (list API, merged), #21 (ad title, merged), #18 (merged), #11 (layout, routing, shared components, merged), #25 (API types regenerated, merged). Not blocking, but related: the details page `/pet-ads/{id}` comes in #20 and does not exist yet, and a not-found page is needed later as a separate issue (see Decisions 4).

## Goal
A visitor, logged in or not, opens `/` and sees the newest active ads as cards (title, Adoption or Sale badge, price in euros for Sale only, location). They can filter by type and species, and page through the results 20 at a time with Previous and Next. Loading, empty and error states are friendly. The filters and page live in the URL, so refresh and the Back button keep the state.

## Approach
- Use only what is installed: React 19, `react-router-dom` (`useSearchParams`), the typed client in `src/api/client.ts`, Tailwind design tokens, shared components, and the built-in `Intl.NumberFormat`. **No new npm package.**
- Page state is the URL (`?type=Sale&species=Dog&page=2`). A small pure module reads and validates the query string (invalid values are ignored) and builds it back. The URL is the single source of truth, so there is no second copy of the page or filters in component state.
- A hook `usePetAdList(query)` calls `client.GET('/api/pet-ads', { params: { query: { page, pageSize: 20, type?, species? } } })`. It keeps one result object tagged with the request key (the query plus a retry counter). "Loading" is derived: the stored result's key differs from the current key. This means (a) the old list is never shown for a new query (no flash of stale content), and (b) a slow older response is ignored: the effect has a `cancelled` flag set in its cleanup (same pattern as `AuthContext`), so only the response of the latest key can be stored. Retry bumps the counter.
- Response values are converted defensively: the generator types numbers as `number | string`, so `totalCount`, `page`, `pageSize` and `price` go through `Number(...)` and a finite/integer check. A response that fails the check is treated as an error (friendly message), not rendered.
- The page composes: filter form, results summary, list of cards, pager. `HomePage` in `features/home` stays the route component and renders the new list feature below its existing heading and sentence.
- The type dropdown needs a labelled select. There is no shared `Select` yet, so one is added to `src/components/` first (design rule: no one-off look), modelled on `TextField` (visible label above, helper/error text support, `min-h-11`, tokens only). Approved (Decisions 9).
- Cards: `Card` wrapped by a router `Link` to `/pet-ads/{id}`. The whole card is the link (one tab stop per ad). Badge variant from `type` (`Sale` -> `sale`, anything else -> `adoption`).
- Price: one fixed currency constant (euro) and one formatter in a single module, `src/features/petAds/formatPrice.ts` (see Decisions 3). Cards only call `formatPrice`; no component writes a currency symbol itself.

## API endpoints
Existing endpoint from `PawsPost.Web/src/api/schema.d.ts` (checked against `docs/plans/2-browse-pet-ads.md` and `PawsPost/Features/PetAds/ListPetAdsRequest.cs`). Nothing in the API is changed. No endpoint is missing, so there is no blocking dependency.

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| GET | /api/pet-ads | public | query: `page` (default 1, min 1), `pageSize` (default 20, 1 to 100), `species` (optional, max 50 chars), `type` (optional, `Adoption` or `Sale`) | `PetAdListResponse`: `items` (`PetAdListItemResponse`: `id`, `title`, `type`, `species`, `price`, `location`, `createdAt`), `totalCount`, `page`, `pageSize` | 200, 400 (`ValidationProblemDetails`) |

Contract notes that shape the UI:
- Only `Active` ads, newest first. The UI keeps the order as returned and does not sort.
- `price` is `null` for Adoption. `location` can be `null`. `createdAt` is not shown on the card (so it is not parsed).
- The API has **no currency field**. Prices are plain numbers; the euro shown by the UI is a fixed frontend choice (Decisions 3).
- Empty `species` or `type` means no filter, so the UI omits them from the query when empty.
- `species` matches case-insensitively for ASCII letters only, and as an exact match (not "contains"). The UI does not try to fix this.
- A page beyond the last returns 200 with empty `items` and the real `totalCount`.
- Generated types: `page`, `pageSize`, `totalCount` and `price` are typed `number | string`, so the UI converts them with `Number` and validates. In `PetAdListItemResponse`, `type` is typed as the union `"Adoption" | "Sale"` (the `PetAdType` schema). The code still guards at runtime for any other value: such an item gets the Adoption variant and no price (Decisions 8).
- The UI always sends `pageSize=20` explicitly, so it does not depend on the server default.
- The response has no owner or contact fields, so nothing private can be shown.

## Screens and components
- Route: `/` (existing), inside `Layout`. No new route. Query string: `type` (`Adoption` | `Sale`), `species` (text), `page` (positive integer).
- Page content, top to bottom:
  - Existing `h1` "Welcome to PawsPost" and the sentence "Find a new friend or help a pet find a loving home." are **kept unchanged**. Under them an `h2` "Latest ads" starts the list section.
  - Filter form (`<form aria-label="Filter ads">`, `noValidate`): `Select` "Type" (All types, Adoption, Sale), `TextField` "Species" (`maxLength` 50, helper text "For example Dog or Cat"), primary `Button` "Apply filters". Stacked on 360px, a row on wider screens. Filters apply on the Apply button (one request per apply, not per keystroke) and Apply sets `page` back to 1. The form is keyed by the current URL filters, so Back or Clear filters resets the inputs to match the URL.
  - Results summary: a paragraph with `aria-live="polite"` that shows for example "57 ads found" / "Page 2 of 3" once results are loaded (the container stays mounted so the update is announced).
  - Cards: a list (`ul`/`li`) in one column at 360px, 2 columns from the small breakpoint, 3 from the large one, `max-w-content` from the layout. Card content: title (`h3`, the link text), `Badge`, price for Sale only (for example `€150.50`), location (omitted when `null`; no "null" text). Long titles wrap (`break-words`).
  - Pager: `Button` (secondary) "Previous" and "Next" with "Page X of Y" between. Previous `disabled` when `page <= 1`; Next `disabled` when `page >= totalPages` (`totalPages = ceil(totalCount / pageSize)`). Pager is shown whenever there is at least one ad (with one page, both buttons are disabled).
- Components (new):
  - `src/components/Select.tsx`: shared labelled select.
  - `src/features/petAds/PetAdCard.tsx`: one card (link, title, badge, price, location).
  - `src/features/petAds/PetAdFilters.tsx`: the filter form.
  - `src/features/petAds/PetAdList.tsx`: the list section: summary, states, cards, pager (uses the hook).
  - `src/features/petAds/usePetAdList.ts`: fetching, race handling, retry.
  - `src/features/petAds/petAdListQuery.ts`: parse and build the URL query, `totalPages`, response conversion.
  - `src/features/petAds/formatPrice.ts`: the fixed currency constants and the price display.
- States:
  - Loading: shared `Spinner` (role `status`, "Loading"). While loading, neither the old cards nor the old pager are shown. The filter form stays visible and usable.
  - Empty: `EmptyState` with a friendly message. No filters active: "There are no ads yet. Please check back soon." With filters active: "No ads match your filters." plus a secondary `Button` "Clear filters", which removes `type`, `species` and `page` from the URL.
  - Page beyond the last (`items` empty, `totalCount > 0`, so `page > totalPages`): the UI **replaces** the URL with the last page (history replace, so Back does not loop). If `totalCount` is 0 and `page > 1`, it replaces the URL with page 1, which shows the empty state.
  - Error: shared `ErrorMessage` (role `alert`): "We could not load the ads. Please try again." with a **Try again** button that refetches with the same query (retry counter). No status code, no `title` or `detail` from the server. Network failure, 400 and any non-OK status all use this message (a 400 should not happen because the URL is validated first).
- Forms and validation (client side is convenience, the server decides):
  - `species`: trimmed; empty means no filter; the field has `maxLength` 50, and a longer value coming from the URL is ignored.
  - `type`: only the exact values `Adoption` and `Sale` are accepted from the URL; anything else (other casing, numbers, unknown names) is ignored and treated as "All types". The select cannot produce other values.
  - `page`: must match `^[1-9][0-9]*$` and be a safe integer within int32 (at most 2147483647); otherwise treated as 1.
  - Ignored URL values are not rewritten immediately. The page works with the defaults, and the URL is cleaned the next time the user changes something.
- Price display (`formatPrice.ts`):
  - Shown only when `type === 'Sale'` and the price converts to a finite number >= 0. Adoption never shows a price, even if one is sent. A `null`, missing or invalid price shows nothing (no "null", no "NaN", no empty gap).
  - Format: euro symbol before the amount, always two decimals, thousands separators, for example `€150.50`, `€150.00`, `€1,200.00`. Built with `Intl.NumberFormat('en-IE', { style: 'currency', currency: 'EUR' })` using a **fixed locale** (`en-IE`), not the browser's locale, so every visitor and every test sees the same text. The formatter is created once in `formatPrice.ts`, together with the two constants `PRICE_CURRENCY = 'EUR'` and `PRICE_LOCALE = 'en-IE'`. `docs/design.md` has no price format rule and the existing code has no number formatting (checked: no `Intl` or `toLocale` use), so this is the first and only place.
- Accessibility:
  - Every filter has a visible label (`Select`, `TextField`). Real `button` elements. Cards are real links with the global `:focus-visible` outline.
  - Live region: the results summary (`aria-live="polite"`) announces the count and page. The Spinner is already `role="status"`; the error is `role="alert"`.
  - Focus after actions: after Previous or Next, once the new results have loaded, focus moves to the `h2` "Latest ads" (`tabIndex={-1}`) and the window scrolls to it, so a keyboard user does not lose their place when the pager is replaced by the spinner. After "Clear filters" (the button disappears), focus moves to the Type select. After Apply, focus stays on the Apply button (it stays mounted). On first page load nothing is focused.
  - Text contrast comes from the tokens.
- Layout: 360px first, one column, full-width controls, tokens only (no hex values, no arbitrary pixel values). Wider screens add columns and put filters in a row.

## Data changes
none

## Files to create or change
Paths relative to `PawsPost.Web/`.
- `src/components/Select.tsx` and `src/components/Select.test.tsx`: shared labelled select.
- `src/features/petAds/petAdListQuery.ts` (+ test): parse and validate URL values, build the query string, `totalPages`, convert the response (`Number`, validation).
- `src/features/petAds/formatPrice.ts` (+ test): the single place for the currency constants and price text.
- `src/features/petAds/usePetAdList.ts` (+ tests through `PetAdList`): fetch with race protection and retry.
- `src/features/petAds/PetAdCard.tsx` (+ test).
- `src/features/petAds/PetAdFilters.tsx` (+ test).
- `src/features/petAds/PetAdList.tsx` (+ test): states, pager, focus handling.
- `src/features/home/HomePage.tsx`: keep the `h1` and sentence, add the `PetAdList` section below.
- `src/features/home/HomePage.test.tsx`: the two existing tests render `HomePage` with no router and no mocked client. The new content needs a router (`useSearchParams`, `Link`) and a mocked client, so the **setup** is changed (`MemoryRouter` and `vi.mock('../../api/client')` returning an empty list). The two assertions stay unchanged.
- `src/App.test.tsx`: its `vi.mock` makes every `GET` return 401. Home would then show the error state; the existing assertions (banner, main, contentinfo, the h1) would still pass, but the mock should answer `/api/pet-ads` with an empty list so the tests stay meaningful. Only the mock changes, assertions unchanged.
- `src/test/apiResponses.ts`: only if a shared helper for building a list response is useful (for example `petAdListResponse(items, totalCount)`); optional, small.
- Not changed: `src/api/schema.d.ts` (never edited), `src/api/client.ts`, `src/App.tsx` (no new route), `main.tsx`, `package.json`, the backend, `docs/design.md`.

## Tests to write
Vitest, Testing Library and `user-event`, per `.claude/rules/frontend-testing.md`: find by role, label or text, `findBy` for async, mock only the API client at the boundary (`src/api/client.ts`), one behavior per test, no snapshots, each test sets its own mock responses. Render inside `MemoryRouter` with an `initialEntries` URL; assert the URL change through a small location display in the test or by the next `client.GET` call.

List and cards
- shows one card for each ad returned, in the order returned
- shows the title, the Adoption badge and the location on an Adoption card
- shows the Sale badge and the price on a Sale card
- shows the Sale price with the euro symbol, for example `€150.50`
- shows the price with two decimals and thousands separators (`1200` becomes `€1,200.00`)
- shows a Sale price sent as a string correctly (`"1200"` becomes `€1,200.00`)
- shows no price on an Adoption card, even when the response contains a price
- shows no price for a Sale ad whose price is null
- shows no price for a Sale ad whose price is not a valid number
- shows an ad with an unexpected type with the Adoption badge and no price
- shows a card without a location, with no "null" text and no empty gap
- links each card to `/pet-ads/{id}`
- never shows an owner email or phone (cards only show the fields above)

Requests and filters
- asks the API for page 1 with page size 20 and no filters on first load
- sends the type to the API when the visitor picks Sale and applies
- sends the species to the API when the visitor types Dog and applies (and trims spaces)
- sends type and species together
- leaves out an empty species and "All types" from the request
- goes back to page 1 when filters are applied on page 3
- has a labelled Type select and a labelled Species field limited to 50 characters

Paging
- disables Previous on the first page
- enables Next on the first page when there are more ads
- disables Next on the last page and enables Previous
- asks for the next page when Next is pressed and for the previous page on Previous
- shows "Page 2 of 3" computed from totalCount and pageSize (totalCount given as a string)
- moves focus to the "Latest ads" heading after the new page has loaded
- replaces the URL with the last page when the page is beyond the last (empty items, real totalCount)
- shows the empty state on page 1 when totalCount is 0 and the page is above 1

States
- shows a spinner while loading and no cards
- does not show the old cards while the next page or filter is loading
- shows the empty state with no Clear filters button when there are no ads and no filters
- shows the empty state with a Clear filters button when filters match nothing
- clears the filters and shows the list again when Clear filters is pressed, and moves focus to the Type select
- shows a friendly error with no technical details when the API fails (500)
- shows the same friendly error when the network is down (client throws)
- asks the API again and shows the ads when Try again is pressed
- ignores a slow response of an older request that arrives after a newer one (use `deferred()` from `src/test/apiResponses.ts`)
- ignores a response that arrives after the page is left (unmounted)
- shows the error state when the response has an invalid totalCount (not a number)
- announces the number of ads in the polite live region

URL state
- restores type, species and page from the URL on load and sends them to the API
- fills the filter fields from the URL
- updates the URL when filters are applied and when Next is pressed
- ignores an invalid type in the URL (`type=5`, `type=Cat`, `type=sale`) and loads all types
- ignores an invalid page in the URL (`page=0`, `page=-2`, `page=abc`, `page=1.5`) and loads page 1
- ignores a species in the URL longer than 50 characters

Other
- `petAdListQuery`, `formatPrice` (euro symbol, two decimals, fixed locale, invalid input gives nothing) and `Select`: small unit tests for the pure rules above
- HomePage: still shows exactly one level-one heading and the welcome sentence (existing tests, setup changed only)
- App: still shows the home page inside the layout, and the existing route tests pass (existing tests, mock changed only)

Authorization: not applicable, the endpoint is public and there is no data-changing action. There is no login redirect.

## Steps
Each step is one commit. After each step `npm run build`, `npm run lint`, `npm test` and `npm run format:check` must be green.
1. Branch `feature/19-browse-ads-list-frontend` from `main` (pipeline). Plan approved first.
2. Add the shared `Select` component with tests. (`feat: add select component (#19)`)
3. Add `petAdListQuery.ts` and `formatPrice.ts` with unit tests (URL parsing and validation, `totalPages`, response conversion, euro price text). (`feat: add ad list query and price helpers (#19)`)
4. Add `usePetAdList` with the race protection and retry, and `PetAdCard`, with tests (mock client). (`feat: load active ads and show ad cards (#19)`)
5. Add `PetAdList` (summary, loading, empty, error, pager, out-of-range handling, focus) and `PetAdFilters` (filters, Clear filters) with tests. (`feat: add filters, paging and list states (#19)`)
6. Put the list on `HomePage`, adjust the setup of `HomePage.test.tsx` and `App.test.tsx` (router and mocked client), assertions unchanged. (`feat: show the ads list on the home page (#19)`)
7. Check by hand at 360px and desktop widths against the running API: filters, paging, Back and refresh, keyboard only, focus after paging and clearing, tokens only (no hex or arbitrary pixel values). (`fix` only if needed)
8. Review checklist: no `any`, no `dangerouslySetInnerHTML`, no `console` calls, no owner contact data, no new packages, the euro symbol only in `formatPrice.ts`. Run build, lint, tests, format check. Open the PR from the template with `Closes #19`.

## Decisions
Decided by the user on the open questions, accepting the recommended defaults except Decision 3.

1. **Species filter:** a free-text Species field, max 50 characters, applied with the Apply button. **The client has NOT confirmed this and has NOT supplied a species list**, so it may change later (for example to a dropdown if the client gives a list; that is a small local change in `PetAdFilters`). Matching limits to remember: the match is exact (not "contains"), and case-insensitive for ASCII letters only.
2. **Filters and page in the URL:** `?type=Sale&species=Dog&page=2`. `type` must be exactly `Adoption` or `Sale`, `page` a positive integer, `species` at most 50 characters. Anything else is ignored.
3. **Currency:** prices show a euro symbol. This changes my earlier recommendation of no symbol.
   - The API has no currency field. The euro is **one fixed frontend constant** in `formatPrice.ts` (`PRICE_CURRENCY = 'EUR'`, `PRICE_LOCALE = 'en-IE'`), the same for every ad. It is not a per-ad value and not repeated in components.
   - Format: `€150.50` (symbol first, two decimals, thousands separators) through `Intl.NumberFormat` with the fixed locale `en-IE`, so the text does not depend on the visitor's browser language. Price is shown only for Sale ads. A `null` or invalid price shows nothing.
   - The client has **not confirmed** the currency. If the client needs several currencies, the backend needs a currency field on the ad, which is a separate backend issue (and then a follow-up frontend change). Not part of #19.
4. **Link target:** cards link to `/pet-ads/{id}` as the issue says, although #20 does not exist yet. A not-found page IS needed, but it is a **separate issue**, not part of #19. Until #20 and that page exist, a card click leads to the layout with an empty main area (there is no catch-all route today; React Router logs a "No routes matched" warning in development). Tests only check the `href`.
5. **Page beyond the last:** replace the URL with the last page; with `totalCount` 0, go to page 1 and show the empty state.
6. **When filters apply:** on the Apply button, which also resets the page to 1.
7. **Home heading:** keep the `h1` and the welcome sentence from #11, add an `h2` "Latest ads".
8. **Unknown `type` from the API:** the schema types `type` as the union `"Adoption" | "Sale"`; the code guards at runtime for any other value, which shows the Adoption variant and no price.
9. **Shared `Select` component:** approved (design rule: add missing components to the shared components).

## Risks and open questions
Open questions: none. The earlier issue questions about the list API and the title are resolved by merged #2, #21 and #18.

Noted risks (not blocking):
- **Species and currency are not confirmed by the client** (Decisions 1 and 3). If the client answers differently, update this plan and the affected tests; the changes are local to `PetAdFilters` and `formatPrice.ts`.
- **Card links lead to an empty page until #20 and the separate not-found page exist** (Decisions 4). Merge #20 soon after this one.
- **ASCII-only, exact species match:** a visitor typing a non-ASCII species in different casing, or a partial word ("pupp"), will not match. Known backend behavior, not changed here.
- **Very large `page` values** in the URL are accepted up to int32; the server answers with an empty page, which the out-of-range rule then corrects. Page size is fixed at 20 (no selector).
- **Existing tests need setup changes** (`HomePage.test.tsx`, `App.test.tsx`: router and mocked client). Expected, assertions unchanged, not a weakened test.
- **If a third ad type is ever added** to the backend, the Badge, the price rule and the filter need an update.
- **Stop and ask** (do not guess) if: the schema in `src/api/schema.d.ts` no longer matches the list contract above, the API returns a field that looks like owner contact data, or a new package seems needed.

Out of scope (from the issue, not planned): the details page, any login redirect, a not-found page, photos, text search, sort options, favorites, showing `createdAt`, multi-currency prices.
