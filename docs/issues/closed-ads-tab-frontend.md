Issue: #55
URL: https://github.com/sarastef98/pawpost/issues/55

# Add the Closed ads tab to the owner's profile

Layer: frontend
Depends on: #54, #51

## Summary
Add a "Closed ads" tab to the profile page (`/creators/:id`), shown only when the logged-in user opens their own profile, next to the "My ads" tab built in #51. It lists the user's own closed ads with the owner-only endpoint. The API is defined in `closed-ads-backend.md` and is not redefined here.

## User story
As a logged-in user, I want a "Closed ads" tab on my profile, so that I can see the ads I closed.

## Decisions
- Decided by the client: the wording is "Closed ads".
- Decided by the client: closed ads are visible to their owner only. A visitor, or a user looking at someone else's profile, never sees them.
- Decided by the client: closing is final, so closed ads have no "Close ad" action and no reopen action.
- Decided by the client: the cards in the Closed ads tab are plain cards, not links. The owner cannot open a closed ad's details page, and the details endpoint is not changed.
- Decided by the client: the list is sorted newest first by the created date. No date other than what the normal cards show is displayed.

## Acceptance criteria
- [ ] The profile page (`CreatorProfilePage`) adds a second tab labeled "Closed ads" to the tabs from #51, only when the logged-in user's id equals the profile id. The check is only for showing the tab; the server decides what the endpoint returns.
- [ ] A visitor, or a logged-in user looking at another person's profile, sees the page exactly as today: no tabs, and no requests to the closed ads endpoint. The public profile still loads without login and without a redirect.
- [ ] The tab lists the user's own closed ads with the endpoint from `closed-ads-backend.md`, newest first by created date (the order comes from the API), with Previous and Next paging (limits from the backend issue). It shows the same card content as the "My ads" tab (name, type, species, price, location as the cards already show). The list is requested only when the tab is opened.
- [ ] The cards in the Closed ads tab are plain cards, not links: no link to `/pet-ads/:id`, not focusable as a link, no click navigation. The owner cannot open a closed ad's details page.
- [ ] Closed ads do not show the "Close ad" action, and there is no reopen, reserve or edit action. Closing is final.
- [ ] No date other than what the normal cards already show is displayed. No closed date is shown.
- [ ] The tab has a Spinner while loading, an EmptyState with a friendly message when there are no closed ads, and an ErrorMessage with Try again when loading fails. A 401 sends the user to the login page.
- [ ] Closing an ad from the "My ads" tab (#53) makes it appear in the "Closed ads" tab: the Closed list is refreshed the next time the tab is opened (no stale list).
- [ ] The tab follows the tab accessibility of #51: `tab` and `tabpanel` roles, `aria-selected`, linked to its panel, arrow keys, Home and End, visible focus, and it works by touch at 360px. The tab list needs no change in how tabs work; it only gets one more tab.
- [ ] The cards are marked in a way that closed ads are recognisable (for example a "Closed" label from shared components), if the card does not already make it clear. Exact wording and look are chosen in the plan.
- [ ] Uses shared components and design tokens only, works at 360px first, is keyboard accessible with visible focus.
- [ ] Tests cover: the tab shown on own profile, not shown for another user's profile or for a visitor (and no call to the closed ads endpoint then); the list, paging, loading, empty, error and 401; closed cards are not links and have no "Close ad" action; the list is not requested before the tab is opened; the list is refreshed after an ad was closed; keyboard behavior (arrows, Home, End) and ARIA roles for the two tabs; the public profile still works without login.

## Out of scope
- The API and access checks (`closed-ads-backend.md`).
- The "My ads" tab and the tab component itself (`my-profile-tabs-frontend.md`) and the close action (`close-pet-ad-frontend.md`).
- The "My favorite ads" tab (favorites feature).
- Reopening a closed ad, deleting it for good, editing it.
- Opening a closed ad's details page, or any change to the details endpoint.
- Showing a closed date.
- Regenerating the frontend `schema.d.ts` (separate step after the backend merge).

## Open questions
None.

## Source
Meeting of 16 Nov (`docs/meeting-notes/2026-11-16-my-profile-tabs.md` and `2026-11-16-close-ad.md`). The profile page has tabs for the owner: "My ads" first, "Closed ads" later as a separate issue. The owner sees their own closed ads in that tab; everyone else never sees closed ads. Closing is final, so there is no reopen. It depends on the closed ads backend issue and on the profile tabs issue (#51). The client later accepted two recommendations: sort by created date with no other date shown, and plain (non-link) closed cards with the details endpoint unchanged.
