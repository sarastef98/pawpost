# Plan: Add a friendly not-found page (#27)

Status: Approved
Layer: frontend

## Goal
Any URL that no route matches shows a friendly "Page not found" page inside the shared `Layout` (header and footer stay visible), with a link back to the home page `/`. The known routes keep working unchanged.

## Approach
Add one small feature component, `NotFoundPage`, and register it as a catch-all route (`path="*"`) inside the existing `<Route element={<Layout />}>` in `App.tsx`. React Router v7 ranks `*` lowest, so it only matches when nothing else does. That covers `/nope` and `/pet-ads/abc/extra` without special-casing URLs.

The page is built from the shared `EmptyState` (message plus children slot) and a router `Link` to `/`. The page owns the single `h1`. A link is the correct element for navigation, and the shared `Button` renders a `button`, so the link reuses the primary-button design tokens (`bg-primary text-surface hover:bg-primary-hover rounded-card min-h-11 px-4 py-2 font-semibold`) and no new shared component is added. Visible keyboard focus already comes from the global `:focus-visible` rule in `src/index.css`, so no extra focus styles are needed.

Document title: nothing in the app sets a title today (`index.html` has the placeholder `pawspost-web`, and no page sets `document.title`). React 19 can render a `<title>` element from a component and moves it into `<head>`, so the page renders `<title>Page not found</title>`. No new package and no new shared hook. See open questions about the title being left behind or not on later pages.

Independence from #20 (PR #29): this plan only adds a `path="*"` route and touches no `/pet-ads/:id` code. Whether or not #20 is merged, the catch-all works. Implementation must be based on `main`. If #29 merges first, `App.tsx` will have an extra route line, and the catch-all is simply added after the existing routes, so a trivial merge at most.

## API endpoints
None. This page uses no API calls. It must not call the API client.

## Screens and components
- Route: `path="*"` as the last child of the `Layout` route in `PawsPost.Web/src/App.tsx`. Element: `<NotFoundPage />`.
- Components:
  - `NotFoundPage` (new, `src/features/notFound/NotFoundPage.tsx`): a wrapper with the `h1` "Page not found" (same heading style as `HomePage`, `text-2xl`), a `<title>Page not found</title>`, and `EmptyState` with the message "We could not find that page. Let's get you back to the ads." and, as its child, a `Link to="/"` labelled "Back to home" styled with design tokens only.
  - Reused: `Layout`, `EmptyState`.
  - Layout at 360px first: single column, centered text from `EmptyState`, link at least 44px tall (`min-h-11`), no fixed widths.
- States: loading and error are not applicable (static page, no data). The page is itself the "nothing here" state. No technical details shown: no status code, no path, no error text.
- Forms and validation: none.

## Data changes
none

## Files to create or change
- `PawsPost.Web/src/features/notFound/NotFoundPage.tsx`: new page component (one component per file, feature folder per frontend rules).
- `PawsPost.Web/src/features/notFound/NotFoundPage.test.tsx`: tests for the page alone.
- `PawsPost.Web/src/App.tsx`: import `NotFoundPage` and add `<Route path="*" element={<NotFoundPage />} />` after the existing routes inside the `Layout` route. No other change.
- `PawsPost.Web/src/App.test.tsx`: add routing tests (see below). Existing tests stay untouched.

## Tests to write
Page, in `NotFoundPage.test.tsx` (render inside `MemoryRouter`; no API mock needed):
- shows exactly one level 1 heading named "Page not found".
- shows the friendly message and no status code, path or error text (for example `queryByText(/404/)` is absent).
- has a link named "Back to home" with `href` `/`.
- sets the document title to "Page not found".
- the link is reachable by keyboard: after `user.tab()` the link has focus.

Routing, in `App.test.tsx` (reuse the existing `renderAt` helper and API mock):
- an unknown URL `/nope` shows the not-found heading inside the layout (banner, main, contentinfo all present, heading is within main).
- a deeper unknown URL `/pet-ads/abc/extra` also shows the not-found page.
- clicking "Back to home" from `/nope` shows the home page heading "Welcome to PawsPost" (user-event).
- known routes are not affected: `/` shows the home heading and `/styleguide` shows "Style guide" with no "Page not found" heading. The existing `/login` and `/register` tests already cover those routes and must continue to pass unchanged. Add a `queryByRole('heading', { name: 'Page not found' })` absent check for `/login` and `/register` in new, separate tests, not by editing old ones.

Tests must be independent of each other and mock only `./api/client`, as the file already does.

## Steps
1. Create `NotFoundPage.tsx` with the heading, title, `EmptyState` message and the home `Link` styled with design tokens. Commit: `feat: add not-found page component (#27)`.
2. Add `NotFoundPage.test.tsx` for the page alone. Commit: `test: cover not-found page (#27)`.
3. Add the catch-all `path="*"` route in `App.tsx`. Commit: `feat: show not-found page for unknown routes (#27)`.
4. Add the routing tests in `App.test.tsx` (unknown URLs inside the layout, link back home, known routes unaffected). Commit: `test: cover not-found route in App (#27)`.
5. Run `npm run build`, the frontend tests, and the lint command in `PawsPost.Web`, and check at 360px width in the browser (keyboard tab to the link, visible focus). Fix anything found. Commit only if changes are needed.

## Risks and open questions
Not blocked. No dependency on missing code or on #20/PR #29. The issue says `Depends on: #11`, which is already merged (Layout and routing exist).

Open questions (the first three come from the issue, with its recommended defaults kept):
1. Wording. Default: heading "Page not found", message "We could not find that page. Let's get you back to the ads.", link "Back to home".
2. Should the style guide page show the not-found state? Default: no (the issue lists it as out of scope).
3. Link or Button? Default: router `Link` to `/` styled with the primary-button tokens, no new shared component. Styling classes are duplicated from `Button` in one place. If the developer prefers, extracting a shared link-button is a separate change and not planned here.
4. Document title (new). No page in the app sets a title today, so only this page will set "Page not found". React 19 removes its `<title>` when the page unmounts, and the `index.html` title `pawspost-web` returns on other pages. Default: set the title only on this page, as the acceptance criteria require, and leave a shared title mechanism for a separate issue. Risk: if hoisting `<title>` does not update `document.title` under jsdom in tests, fall back to a `useEffect` that sets `document.title` and restores the previous value on cleanup (still no new package). Decide at step 1.
5. Link to `/pet-ads/{id}` from cards before #20 merges lands on this page with the text "Page not found". That is accepted by the product owner in the issue. Once #20 is merged the route exists. No work here.
