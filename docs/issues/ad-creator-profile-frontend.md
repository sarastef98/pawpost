Issue: #45
URL: https://github.com/sarastef98/pawpost/issues/45

# Show the ad creator and add the creator profile page

Layer: frontend
Depends on: #44, #19, #20, #14

## Summary
Show the creator's public name on the ad cards and on the ad details page, link it from the details page to a new creator profile page that lists the creator's ads, and add a display name field to the register form. The API is defined in `ad-creator-profile-backend.md` and is not redefined here.

Decisions (client): the profile page URL uses the creator's user id (a Guid). The profile page is public, like browsing the list. Cards show the creator's name as plain text; only the ad details page links to the profile.

## User story
As a visitor, I want to see who created an ad and open their profile, so that I can see all the ads from the same person.

## Acceptance criteria
- [ ] The register form (#14) has a new required field for the public display name, with a label. It is sent through the typed client as defined in the backend issue.
- [ ] Under the display name field the register form shows the name rules as a short hint (3 to 30 characters; letters, digits, spaces, hyphen and underscore).
- [ ] The register page shows the server's errors for that field next to it, in friendly wording. A taken name (409) shows a clear message (for example "This name is already taken. Please choose another."). No technical details.
- [ ] Each ad card on the home page (#19) shows the creator's display name as plain text, not a link (the card is already a link to the details page).
- [ ] The ad details page (#20) shows the creator's display name and a link to the creator's profile page. This is the only place that links to the profile. The login rules of the details page stay as they are.
- [ ] A new public profile page (route using the creator id, for example `/creators/:id`) shows inside the shared layout the creator's display name and the creator's ads as Cards, in the order the API returns them (newest first). The cards look like the home page cards and link to `/pet-ads/:id`.
- [ ] The profile page shows only the display name and the ads. No email, phone or other data.
- [ ] The profile list uses Previous and Next paging, with the page size and limits from the backend issue (default 20, maximum 100). Previous is disabled on the first page and Next on the last.
- [ ] While loading, a Spinner is shown.
- [ ] A creator without ads shows an EmptyState with a friendly message.
- [ ] An unknown creator (404) shows a friendly "creator not found" EmptyState with a link back to the home page.
- [ ] If the profile cannot be loaded, an ErrorMessage with a Try again button is shown, without technical details.
- [ ] The profile page needs no login: it is shown and loaded for visitors who are not logged in, with no redirect to `/login`.
- [ ] A back link leads to the home page.
- [ ] Wording (labels, hints, the name-taken message, empty states) is friendly and plain; the exact texts are chosen in the plan.
- [ ] Uses shared components and design tokens only, works at 360px first, is keyboard accessible with visible focus.
- [ ] Tests cover: creator shown as plain text on a card (no link), creator shown on the details page with a link to the profile, profile renders name and ads, profile works without login, paging, empty profile, 404, loading state, error with Try again, and the register form with the new field, the rules hint and the "name taken" message.

## Out of scope
- The API, uniqueness rules, default names and access rules (backend issue).
- Editing the display name (separate feature).
- Any other profile content (bio, photo, contact details, ratings, favorites).
- Showing non-`Active` ads of the creator.
- Messaging the creator.
- Showing the creator in the response of creating an ad or in the current user (`/api/auth/me`) data.

## Open questions
None.

## Source
Meeting of 15 Nov (`docs/meeting-notes/2026-11-15-ad-creator-profile.md`). Ads show who created them. The details page shows the creator and links to the creator's profile page, which lists all of the creator's `Active` ads. The creator is shown on the browse cards too. The public name is chosen by a new required field on the register form. It depends on the backend issue.
