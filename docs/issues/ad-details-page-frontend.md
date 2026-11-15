Issue: #20
URL: https://github.com/sarastef98/pawpost/issues/20

# Add ad details page for logged-in users

Layer: frontend
Depends on: #18, #19, #11, #14

## Summary
Add the page `/pet-ads/:id` that shows all information of one ad. Visitors who are not logged in are redirected to the login page. The API is defined in #18.

## User story
As a logged-in user, I want to open an ad and see everything about it, so that I can decide whether to adopt or buy.

## Acceptance criteria
- [ ] The route `/pet-ads/:id` shows the details page inside the shared layout.
- [ ] The page reads the ad through the typed client from `GET /api/pet-ads/{id}`.
- [ ] It shows all fields the API returns: title (if it exists), Badge (Adoption or Sale), price for Sale only, species, breed, age, description and location.
- [ ] A visitor who is not logged in (via `useAuth`) is redirected to `/login` and the ad is not requested.
- [ ] While the app is still finding out if the user is logged in, a Spinner is shown (no flash of the login page).
- [ ] If the API answers 401, the user is also redirected to `/login`.
- [ ] An unknown ad (404) shows a friendly "ad not found" EmptyState with a link back to the home page.
- [ ] Other load errors show an ErrorMessage with a Try again button, without technical details.
- [ ] A back link leads to the home page list.
- [ ] Uses shared components and design tokens only, works at 360px first, is keyboard accessible with visible focus.
- [ ] Tests cover: ad shown, redirect when logged out, loading state, 404, error with Try again.

## Out of scope
- Photos, contact details, messaging, editing or closing.
- Sending the visitor back to the ad after login (not decided, see open questions).

## Open questions
- Is the visitor sent back to the ad after logging in? Not decided. Recommended default: yes, as it is friendlier. It needs a redirect-target mechanism (for example a `returnTo` value passed to `/login` in the router state), which does not exist today: the login page from #14 has no such feature and its post-login destination is itself an open question there. Until decided, login goes where #14 sends it.
- Does the details API require login? Not decided (see #18). Recommended default: yes. This page works either way, but the 401 handling above is only needed if it does.
- A visitor who opens the ad link and is redirected sees no explanation. Should the login page say "Please log in to see this ad"? Wording not given by the client.

## Source
Clicking a card opens a details page with all the ad's information. Only logged-in users may see it; others are redirected to the login page. Whether the details API needs login and whether the visitor returns to the ad after login are not decided.
