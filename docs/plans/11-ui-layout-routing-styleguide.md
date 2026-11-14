# Plan: Add page layout, routing and style guide page (#11)

Status: Approved
Layer: frontend

Depends on: #10 (shared components in `PawsPost.Web/src/components/`, merged in PR #12).

## Goal
Every page is wrapped in one shared layout (header with the text logo "PawsPost" on the left and a navigation area on the right, a centered content area up to 1100px, a simple footer). The route `/` shows a home page and `/styleguide` shows a style guide page with every shared component and variant. The Vite starter content in `App.tsx` is gone. The layout works at 360px first, is keyboard accessible, and both routes are tested inside the layout.

## Approach
- Add a `Layout` component (header, `main`, footer) and two pages, `HomePage` and `StyleGuidePage`. `App.tsx` only declares the layout and the two routes.
- Use `react-router-dom` (approved, decision 1): `BrowserRouter` in `main.tsx`, `Routes`, `Route` and `Outlet` in the app.
- Testability: `App` does not create its own browser router, so tests render it inside a `MemoryRouter` with a chosen starting URL. `main.tsx` wraps `App` in `BrowserRouter`.
- The layout uses only design tokens from `src/index.css` (`max-w-content`, `bg-surface`, `border-border`, `text-muted`, spacing utilities). No hex colors, no arbitrary `[...]` values. Mobile first: default classes target 360px (header content may wrap, content has horizontal padding, no fixed widths); wider only by `max-w-content` with `mx-auto`.
- Accessibility: landmarks `header`, `nav` (labelled with `aria-label`), `main`, `footer`; one `h1` per page. The global `:focus-visible` outline from #10 covers links; the logo is a real link (`a` via the router `Link`).
- Style guide page reuses only the shared components, in labelled sections with headings: Button (primary, secondary, danger), TextField (label only, with helper text, with error text), Card, Badge (Adoption, Sale), Spinner, EmptyState (with an action Button), ErrorMessage (with and without retry). Sample text is short and friendly, per `docs/design.md`.
- Nothing on these pages loads data, so data loading, empty and error states do not apply. The style guide shows those states as components.

## API endpoints
None. This plan uses no API endpoints. (Existing in `schema.d.ts`: `/api/pet-ads`, `/api/health`, `/api/auth/*`; none is needed. The old `/api/health` indicator in `App.tsx` is dropped, decision 6.)

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| none | none | none | none | none | none |

## Screens and components
- Routes:
  - `/`: `HomePage` inside the layout.
  - `/styleguide`: `StyleGuidePage` inside the layout, also in production (decision 5).
  - Any other URL: no not-found page for now (decision 8).
- Components:
  - `Layout` (`src/components/Layout.tsx`): shared page frame, so it lives with the shared components. Renders the header, `main` (centered, `max-w-content`, `mx-auto`, side padding, children from `Outlet`) and footer.
  - Header inside `Layout`: logo on the left, the text "PawsPost" as a link to `/` (decision 2). On the right an empty `nav` landmark that stays out of the way (no visible space or links) until later features add links (decision 9). Real navigation links are out of scope.
  - Footer inside `Layout`: one short line, "PawsPost" plus the current year (decision 4).
  - `HomePage` (`src/features/home/HomePage.tsx`): one `h1` and one short welcome sentence (decision 3). Exact wording is written by the developer in friendly, short language per `docs/design.md`.
  - `StyleGuidePage` (`src/features/styleguide/StyleGuidePage.tsx`): all components and variants as described in Approach.
- States: loading, empty, error. Not applicable to the two pages themselves (no data). The style guide displays Spinner, EmptyState and ErrorMessage as samples.
- Forms and validation: none. The TextField samples are static and show the given helper and error text only; they do no validation.
- Responsive check: 360px first (no horizontal scroll, header does not overflow), then desktop where content stops growing at 1100px and stays centered.

## Data changes
none

## Files to create or change
- `PawsPost.Web/package.json` and `package-lock.json`: add `react-router-dom` (approved).
- `PawsPost.Web/src/main.tsx`: wrap `App` in `BrowserRouter`.
- `PawsPost.Web/src/App.tsx`: replace the whole Vite starter (counter, logos, doc links, spacer sections, the `./App.css` import, the hero/react/vite asset imports and the `/api/health` check) with `Routes` using `Layout`, `/` and `/styleguide`.
- `PawsPost.Web/src/App.css`: delete, together with its import in `App.tsx` (decision 7). It is still the Vite template (`#center`, `.hero`, `.counter`, `.ticks`, `#next-steps`, `#docs`, `#social`, `#spacer`); `index.css` from #10 already holds the global styles, so nothing is moved.
- `PawsPost.Web/src/App.test.tsx`: rewrite with route-in-layout tests (see below).
- `PawsPost.Web/src/components/Layout.tsx` and `Layout.test.tsx`
- `PawsPost.Web/src/features/home/HomePage.tsx` and `HomePage.test.tsx`
- `PawsPost.Web/src/features/styleguide/StyleGuidePage.tsx` and `StyleGuidePage.test.tsx`
- `PawsPost.Web/src/components/designTokens.test.ts`: not edited. Its glob covers `src/components/*.tsx`, so `Layout.tsx` is checked automatically. It is not extended to `src/features/` (decision 10).
- Not touched: `src/index.css`, existing shared components, `src/api/*`, `index.html`, and the unused starter assets `src/assets/hero.png`, `react.svg`, `vite.svg` and `public/icons.svg` (decision 7).

## How the plan handles App.css, App.tsx and App.test.tsx
- `App.css` is still the Vite starter stylesheet. Step 4 removes its import and deletes the file in the same commit that replaces the starter markup.
- `App.test.tsx` exists and tests the starter: the "Get started" heading and the "API: ok/unreachable" text from a mocked `client.GET('/api/health')`. These behaviors are removed on purpose, so the user explicitly approved replacing these old tests (decision 6). They are replaced by the new route tests below, not skipped or weakened. The `./api/client` mock is no longer needed in this file because `App` no longer calls the API.
- All other tests must keep passing: the #10 component tests and `designTokens.test.ts`.

## Tests to write
Vitest, Testing Library, `user-event`, queries by role, label or text, `MemoryRouter` for routes. No API mocking is needed. Test names are sentences (decision 11), for example `shows the home page inside the layout at the home route`.
- `App.test.tsx`:
  - shows the home heading inside the layout at `/` (banner, main and contentinfo landmarks present together with the page heading);
  - shows the style guide heading inside the layout at `/styleguide` (same landmarks present);
  - clicking the "PawsPost" logo link in the header goes from the style guide back to the home page.
- `Layout.test.tsx`: renders a header with the logo link pointing to `/`, a navigation landmark, a main area with its content, and a footer containing "PawsPost" and the year; the logo link can be reached with Tab and receives focus. The 1100px and centered rules are checked manually at desktop width, not by class names (frontend testing rule).
- `HomePage.test.tsx`: shows one level-one heading and the welcome sentence.
- `StyleGuidePage.test.tsx`: shows a heading for each component group; shows primary, secondary and danger Buttons; shows a TextField with label only, one with helper text and one with error text (error marked `aria-invalid`); shows the Card; shows the "Adoption" and "Sale" badges; shows a loading status ("Loading"); shows the EmptyState message and its action; shows ErrorMessage with an alert role, with and without a retry button; sample buttons are reachable by keyboard.
- Guard check: `designTokens.test.ts` covers `Layout.tsx`. A manual grep of `src/features/home` and `src/features/styleguide` for hex colors and `[...]` classes is done before the PR.
- Manual: 360px width (no horizontal scroll) and desktop (content centered, at most 1100px), keyboard-only walkthrough on both routes (Tab order, visible outline on every link and control), contrast of new text and background pairs at least 4.5:1 (footer text, muted text on background). Record results in the PR.
- Commands before the PR: `npm run lint`, `npm run format:check`, `npm run build`, `npm test`.

## Steps
1. Add `react-router-dom`, wrap `App` in `BrowserRouter` in `main.tsx`, and confirm `npm run build` and `npm test` still pass.
2. Add `Layout` (header with text logo link and empty nav landmark, centered `main`, footer with "PawsPost" and the year) plus `Layout.test.tsx`.
3. Add a minimal `HomePage` (heading and one welcome sentence) plus `HomePage.test.tsx`.
4. Replace the starter in `App.tsx` with the layout and the `/` and `/styleguide` routes (a minimal `StyleGuidePage` heading is enough here), delete `App.css` and its import, and rewrite `App.test.tsx` with the route-in-layout tests.
5. Fill `StyleGuidePage` with every component and variant, plus its test.
6. Final checks: guard check, lint, format check, build, tests, 360px and desktop check, keyboard walkthrough, contrast check. Record the manual checks in the PR.

## Decisions
Answered by the user:
1. Approved: new npm package `react-router-dom`.
2. The logo is the text "PawsPost", linking to `/`.
3. Home page: a heading and one short welcome sentence.
4. Footer: a short line such as "PawsPost" plus the year.
5. `/styleguide` is available in production too. No development-only gating.
6. The old `App.test.tsx` tests may be replaced with route-in-layout tests, and the health indicator is dropped. The user explicitly approved removing the old tests.
7. Delete `App.css` and its import. Leave the unused starter assets alone.
8. No not-found page for now.
9. The nav area is an empty `nav` landmark that stays out of the way until later features add links.
10. No skip-to-content link, and the hard-coded value guard is not extended to `src/features/`.
11. New tests use sentence names.

## Risks and open questions
- No open questions remain from the issue.
- Contrast: new text and background pairs (footer text, muted text on background, Sale badge on primary-soft, white on primary shown on the style guide) must meet 4.5:1. If a pair fails, stop and report instead of changing design values (same as #10).
- Deployment: with `BrowserRouter`, a direct visit to `/styleguide` in production needs the host to fall back to `index.html`. Vite dev and preview do this by default. Hosting is not decided in the issue; noted for the user.
- Because `/styleguide` ships in production (decision 5), it is publicly reachable. It shows only sample text and no data, so there is no security concern, but it is a visible page for real users.
- Footer year: the current year comes from the browser date. Tests should not hard-code a year, only check that a four-digit year is shown.
- Dependency: #10 is merged (PR #12), so the shared components are on `main`.
