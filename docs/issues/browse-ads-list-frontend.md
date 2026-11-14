Issue: #19
URL: https://github.com/sarastef98/pawpost/issues/19

# Show the list of active ads on the home page

Layer: frontend
Depends on: #2, #11

## Summary
Replace the home page content (`src/features/home/HomePage.tsx`) with a list of active ads as cards, with species and type filters and Previous/Next paging. The list API is defined in #2 and is not redefined here.

## User story
As a visitor, I want to see and filter the newest ads without an account, so that I can find an animal to adopt or buy.

## Acceptance criteria
- [ ] The home page loads the list through the typed client and shows each ad as a Card, in the order the API returns (newest first).
- [ ] Each card shows the title, a Badge (Adoption or Sale), the price for Sale ads only, and the location.
- [ ] Each card is a link to `/pet-ads/{id}` (the page itself comes in the details-page issue).
- [ ] A visitor can filter by species and by type (Adoption or Sale). Changing a filter goes back to page 1.
- [ ] The list shows 20 ads per page with Previous and Next buttons. Previous is disabled on the first page and Next on the last.
- [ ] While loading, a Spinner is shown.
- [ ] With no ads, or no ads matching the filters, an EmptyState with a friendly message is shown. With filters active it has a "Clear filters" button.
- [ ] If the list cannot be loaded, an ErrorMessage with a Try again button is shown, without technical details.
- [ ] No login is needed. Owner contact data is never shown.
- [ ] Uses shared components and design tokens only, works at 360px first, is keyboard accessible with visible focus, and every filter has a label.
- [ ] Tests cover: cards render, Sale price only for Sale, filtering, paging, empty state, clear filters, error with Try again.

## Out of scope
- The details page (separate issue) and any login redirect.
- Photos, search by text, sorting options, saving favorites.

## Open questions
- #2 does not cover what this screen needs. Update #2 (or create a new backend issue) to add: filters by species and type, newest first, default page size 20, and a response that includes the title and the total count or "has next page". Until then this issue is blocked.
- The title does not exist in `CreatePetAdRequest`, `PetAdResponse` or the `PetAd` entity. Which text should a card show: a new required title field, or a fallback such as breed plus species? Recommended default: add a title field in the backend (see the details backend issue, #18).
- Which species values does the filter offer? The backend stores species as free text (max 50 characters). Recommended default: a dropdown with a fixed list from the client (for example Dog, Cat, Other) plus "All", and the backend matches species case-insensitively. The client must give the list.
- Should filters and page be kept in the URL (for example `?type=Sale&page=2`)? Recommended default: yes, so back/refresh keep the state.
- Is the price shown with a currency? The notes and API give none.

## Source
The home page shows active pet ads as cards, newest first, with title, Adoption/Sale badge, price for Sale and location. Visitors filter by species and type, 20 ads per page with Previous/Next. Empty and error states are friendly, with a way to clear filters and a Try again button.
