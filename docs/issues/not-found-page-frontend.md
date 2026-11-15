Issue: #27
URL: https://github.com/sarastef98/pawpost/issues/27

# Add a friendly not-found page

Layer: frontend
Depends on: #11

## Summary
Show a friendly not-found page inside the shared layout for any URL that no route matches. Today `PawsPost.Web/src/App.tsx` has routes for `/`, `/styleguide`, `/login` and `/register` inside `Layout`, but no catch-all route, so an unknown URL shows the layout with an empty main area.

## User story
As a visitor, I want to see a clear, friendly page when I open an address that does not exist, so that I know I am in the wrong place and can get back to the ads.

## Acceptance criteria
- [ ] An unmatched path (for example `/nope` and `/pet-ads/abc/extra`) shows the not-found page inside the shared `Layout` (header and footer still visible).
- [ ] The page is shown through a catch-all route (`path="*"`), not by special-casing individual URLs.
- [ ] The page has exactly one `h1` that makes clear it is a not-found page, and the document title says so too.
- [ ] It shows a short, friendly message with no technical details (no status codes, paths or error text).
- [ ] It has a link or Button back to the home page `/`. Navigating with it shows the home page.
- [ ] The page is keyboard accessible with visible focus on the way back to home.
- [ ] It works at 360px width first.
- [ ] It uses shared components (for example `EmptyState`, and `Button` or a router `Link` styled with design tokens) and design tokens only, with no hard-coded colors, sizes or spacing.
- [ ] The known routes `/`, `/styleguide`, `/login` and `/register` still work and are unchanged.
- [ ] Tests cover: an unknown URL shows the page inside the layout, the link back to home works, and the known routes are not affected.

## Out of scope
- The "ad not found" state for a 404 from the API on the ad details page. That is a separate in-page state in #20, not this page.
- HTTP status codes. This is a single page app served statically, so a real 404 status needs hosting configuration and is not part of this issue.
- Redirects (for example sending unknown URLs to the home page).
- Showing the not-found state on the style guide page (see open questions).

## Open questions
- Exact wording of the heading and message. Not given by anyone. Recommended default: heading "Page not found", message "We could not find that page. Let's get you back to the ads.", and a link "Back to home".
- Should the style guide page show the not-found state? Recommended default: no, keep it simple.
- The shared `Button` renders a `button` element, not a link. Recommended default: use a router `Link` to `/` styled with the same design tokens (a real link is correct for navigation), and do not add a new shared component unless the developer sees a real need.

## Source
Comes from the product owner's decision while planning #19, not from client meeting notes. The product owner said a not-found page is needed. Until it exists, an unknown URL, and card links to `/pet-ads/{id}` before the details page (#20) exists, lead to an empty main area, and the product owner accepted that for now. The plan for #11 had decided "no not-found page for now". This issue is independent of #19 and #20 and can be done at any time. No client requirement is invented here. The tone follows `docs/design.md` (friendly, short, no technical details).
