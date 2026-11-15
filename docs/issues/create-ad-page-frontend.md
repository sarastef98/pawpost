Issue: #31
URL: https://github.com/sarastef98/pawpost/issues/31

# Add a page to create a pet ad

Layer: frontend
Depends on: #1, #21, #20, #14, #11, #32

## Summary
Add a page where a logged-in user creates a pet ad with a form, and a "Post an ad" button in the header that links to it. The page uses the existing `POST /api/pet-ads` (#1, with the required title from #21). The species list is enforced by the backend issue #32; this issue does not change the API.

## User story
As a logged-in user, I want to fill in a form to post an ad for an animal, so that I can give it away for adoption or sell it.

## Acceptance criteria
- [ ] The route `/pet-ads/new` shows the create-ad page inside the shared layout. It is not captured by `/pet-ads/:id`.
- [ ] The header shows a clear "Post an ad" button that links to this page.
- [ ] A visitor who is not logged in (via `useAuth`) and opens the page is redirected to `/login`.
- [ ] While the app is still finding out whether the user is logged in, a Spinner is shown (no flash of the form or of the login page).
- [ ] The form has these fields, each with a label: title, type (Adoption or Sale), species, breed (marked optional), age, description, location (marked optional).
- [ ] Species is a select with the fixed list Dog, Cat, Bird, Rabbit, Other. The values sent are the ones the backend accepts, as defined in #32 and the regenerated `src/api/schema.d.ts`.
- [ ] Age is entered as a number plus radio buttons to choose Months or Years. The value is converted to months and sent as `ageInMonths`.
- [ ] The price field is shown only when the type is Sale, and it is required then. When the type is Adoption no price field is shown and no price is sent.
- [ ] Each field shows a clear message next to it when its value is wrong, using the rules the server already enforces: required fields (title, type, species, age, description), maximum lengths, age at most 360 months, and a price above 0 for Sale. The exact limits come from the API contract in #1, #21 and the species issue and are not redefined here.
- [ ] The client-side checks are for convenience only. The server still decides.
- [ ] The Submit button is disabled while the ad is being saved, so a double click cannot post the ad twice.
- [ ] When the server rejects the form with 400 (`ValidationProblemDetails`), its messages are shown next to the matching fields.
- [ ] When saving fails for any other reason (for example the network is down), a clear error without technical details is shown and everything the user typed is kept.
- [ ] When the API answers 401, the user is redirected to `/login`.
- [ ] After the ad is created (201), the user is taken to the new ad's details page (`/pet-ads/:id`, #20) and sees a short success message.
- [ ] The form does not offer photos, a draft option, or a status field. The ad is `Active` right away, which the server already does.
- [ ] The request is sent through the typed client (`src/api/client.ts`) and its types.
- [ ] Uses shared components (Button, TextField, Select, ErrorMessage, Spinner) and design tokens only, works at 360px first, is keyboard accessible with visible focus, every input has a label (including the Months/Years radio group).
- [ ] The wording of the button, the success message and the error messages follows `docs/plans/31-create-ad-page-frontend.md`, which is accepted.
- [ ] Tests cover: redirect when logged out, loading state, Sale shows and requires the price, Adoption hides it, species select options, age conversion for Months and Years, field validation messages, successful create navigates to the details page with the success message, server 400 field errors, other failure keeps the typed values, and the submit button is disabled while saving.

## Out of scope
- Photos.
- Saving a draft.
- Editing or closing an ad.
- A preview before posting.
- Any change to the API. The species list is a separate backend issue (#32).

## Open questions
- Age in years: are decimals allowed (for example 1.5 years), and how is the value rounded when converted to months? The maximum is 360 months (30 years); the server accepts only whole months from 0 to 360.
- Should the "Post an ad" button also show for logged-out visitors (and lead them to login), or only for logged-in users? The notes say the page is "linked from the header" for logged-in users and that logged-out visitors are redirected. This is not clear for the button.
- After login, is the visitor sent back to the create-ad page? Not in the notes. See the same open question in #20 and #14.
- Is a default type preselected in the type field? (The type is a select unless the client says otherwise.)
- Is the price shown in a specific currency? Not decided (open question in #1).
- If the user switches from Sale to Adoption, is the typed price cleared? The notes do not say.
- Should the frontend duplicate the server limits before submit, or only show the server's messages after submit? The notes ask for a message next to each wrong field; whether this happens before submit is not stated.

## Source
Client meeting of 15 Nov (`docs/meeting-notes/2026-11-15-create-ad-ui.md`). Logged-in users get a page to create a pet ad, linked from the header with a "Post an ad" button. Logged-out visitors are redirected to login. The form has title, type, species, optional breed, age, description and optional location. Price shows only for Sale and is required then. Field errors show next to the fields, Submit is disabled while saving, server errors are mapped to fields, other errors keep the typed data. After creating, the user sees the ad's details page and a success message. Photos, drafts, editing/closing and preview are not now.

Developer decisions after review: species is a fixed list (Dog, Cat, Bird, Rabbit, Other) and needs a new backend issue; age is a number plus Months/Years radio buttons converted to `ageInMonths`; the route is `/pet-ads/new`; the wording in the plan is accepted.
