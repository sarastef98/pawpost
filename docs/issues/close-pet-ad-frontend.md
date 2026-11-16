Issue: #53
URL: https://github.com/sarastef98/pawpost/issues/53

# Add the close ad action for the owner

Layer: frontend
Depends on: #52, #51, #20, #14

## Summary
Let the owner of an ad close it, after a confirmation, from the ad details page and from the ads in the "My ads" tab of their own profile. The API is defined in `close-pet-ad-backend.md` and is not redefined here. There is no reserve button. The "My ads" tab is built by `my-profile-tabs-frontend.md`, so the part for the tab can only be done after that issue.

Decisions (client): closing is final and is also how an ad is deleted. Button wording is "Close ad". The confirmation says the ad is removed for everyone.

## User story
As the owner of an ad, I want to close it from its page or from my ads list after confirming, so that it is removed and I do not close it by accident.

## Acceptance criteria
- [ ] The ad details page (#20) shows one "Close ad" action, only to the logged-in owner of the ad. Other users and visitors do not see it. The owner is recognised by comparing the creator id in the details response with the current user id from the logged-in state (#14); the check is only for showing the button, the server decides.
- [ ] Each ad in the "My ads" tab (from `my-profile-tabs-frontend.md`) shows the same "Close ad" action. This part can only be done after the tab exists.
- [ ] There is no reserve option or any other status action.
- [ ] Choosing the action opens a confirmation first (a shared dialog component; if it does not exist it is added to the shared components). It says in plain words that closing removes the ad for everyone and cannot be undone. The user can confirm or cancel. Cancel closes the dialog and changes nothing.
- [ ] The dialog is keyboard accessible: focus moves into it, Escape cancels, focus returns to the action when it is cancelled, buttons are real `button` elements.
- [ ] Confirming calls the close endpoint through the typed client. While the request runs, the confirm button is disabled and shows progress, so it cannot be sent twice.
- [ ] After a successful close from the details page, the user goes to the home page and sees a short friendly message that the ad was closed. The closed ad is not shown afterwards.
- [ ] After a successful close from the "My ads" tab, the user stays on the tab, the list refreshes, the closed ad is gone from it, and a short friendly message says the ad was closed. If the page becomes empty, the tab shows its empty state or goes to the last page.
- [ ] 401 (session ended) sends the user to the login page. 403, 404 and 409 each show a friendly message in the dialog or on the page (for example "This ad is already closed or no longer exists"), without technical details. Other errors show an ErrorMessage with a Try again option.
- [ ] Wording (dialog title and text, success and error messages) is friendly and plain; the exact texts are chosen in the plan. The button says "Close ad".
- [ ] Uses shared components and design tokens only, works at 360px first, is keyboard accessible with visible focus.
- [ ] Tests cover: action shown to the owner on the details page, hidden for another user and for a visitor, no reserve option; action on the ads in the "My ads" tab; cancel changes nothing and makes no request; confirm calls the API and shows the success result; redirect to home from the details page; list refresh in the tab; double-click sends one request; 401, 403/404, 409 and generic error handling; dialog keyboard behavior (Escape, focus).

## Out of scope
- The API, rules and access checks (backend issue).
- Building the tabs, the "My ads" list and the "My profile" header button (`my-profile-tabs-frontend.md`).
- The "Closed ads" tab (separate issue).
- Removing the ad from favorites lists (favorites issues).
- Reopening a closed ad, a reserve option, or a separate delete feature.
- Editing an ad.
- Regenerating the frontend `schema.d.ts` (separate step after the backend merge).

## Open questions
None.

## Source
Meeting of 16 Nov (`docs/meeting-notes/2026-11-16-close-ad.md` and `2026-11-16-my-profile-tabs.md`). The creator has a single "Close ad" action, no reserve button, on the ad details page and on the ads in the "My ads" tab of their profile. Closing asks for confirmation first, which says the ad is removed for everyone. Closing is final and is also how an ad is deleted. It depends on the backend issue and on the my profile tabs issue.
