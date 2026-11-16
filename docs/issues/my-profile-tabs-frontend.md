Issue: #51
URL: https://github.com/sarastef98/pawpost/issues/51

# Add profile tabs with My ads and a My profile header button

Layer: frontend
Depends on: #50, #45

## Summary
Show tabs on the creator profile page (`/creators/:id`, #45) when the logged-in user opens their own profile. The first tab is "My ads", which uses the new owner-only list. Add a "My profile" button in the header for logged-in users. The API is defined in `my-ads-backend.md` and is not redefined here.

## User story
As a logged-in user, I want a profile page with a "My ads" tab and a header button to reach it, so that I can find and manage my own ads quickly.

## Decisions
- Decided by the client: the "My ads" tab shows `Active` ads only.
- Decided by the client: the wording is "My ads" for the tab, "My profile" for the header button and "Close ad" for the close action.
- Decided by the client: the "My profile" button links directly to `/creators/<own id>`. There is no separate `/profile` route.

## Acceptance criteria
- [ ] The profile page (`CreatorProfilePage`) shows tabs only when the logged-in user's id equals the profile id. The check is only for showing the tabs; the server decides what the endpoint returns.
- [ ] A visitor, or a logged-in user looking at another person's profile, sees the page exactly as today (#45): name and `Active` ads, no tabs, no extra requests to the owner-only endpoint. The public profile still loads without login and without a redirect.
- [ ] The first (and for now only) tab is labeled "My ads". It lists the user's own `Active` ads with the owner-only endpoint from `my-ads-backend.md`, as Cards like the home page cards linking to `/pet-ads/:id`, newest first, with Previous and Next paging (limits from the backend issue).
- [ ] The tab has a Spinner while loading, an EmptyState with a friendly message when there are no ads, and an ErrorMessage with Try again when loading fails. A 401 sends the user to the login page.
- [ ] The tabs are accessible: `tablist`, `tab` and `tabpanel` roles, `aria-selected`, tabs linked to their panels. Left and Right arrow keys move between tabs, Home and End go to the first and last, Tab moves focus into the panel, and the focus is visible. Tabs are real focusable elements and work by touch at 360px.
- [ ] The tab list is built so more tabs can be added later without changing how the tabs work.
- [ ] The "My ads" tab leaves room for the close action on each ad, whose label is "Close ad"; the action itself is added by `close-pet-ad-frontend.md`.
- [ ] The header (`AuthNav`) shows a button labeled "My profile" for logged-in users, next to the login/logout buttons, and it links directly to `/creators/<own id>` (no `/profile` route). It is not shown to visitors, and not while the login state is loading.
- [ ] The creator name link on the ad details page already goes to `/creators/<id>`; it is not changed, and when the creator is the logged-in user the tabs show there.
- [ ] Uses shared components and design tokens only, works at 360px first, is keyboard accessible with visible focus.
- [ ] Tests cover: tabs shown on own profile, not shown for another user's profile or for a visitor (and no call to the owner-only endpoint then); "My ads" list, paging, loading, empty, error and 401; keyboard behavior (arrows, Home, End) and the ARIA roles; "My profile" button shown only when logged in and linking to `/creators/<own id>`; the public profile still works without login.

## Out of scope
- The API and access checks (`my-ads-backend.md`).
- The close action and its confirmation (`close-pet-ad-frontend.md`).
- The "My favorite ads" tab (favorites feature) and the "Closed ads" tab (separate issue).
- A separate `/profile` route or redirect.
- Editing the profile or display name, and any other profile content.
- Regenerating the frontend `schema.d.ts` (separate step after the backend merge).

## Open questions
None.

## Source
Meeting of 16 Nov (`docs/meeting-notes/2026-11-16-my-profile-tabs.md`). The owner's ads are a tab on their profile page, not a separate page. The tabs show only when the logged-in user opens their own profile; other people see the unchanged public page. First tab "My ads"; "My favorite ads" and "Closed ads" come later. A "My profile" button is added in the header next to login and logout. It depends on the backend issue and on #45.
