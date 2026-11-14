Issue: #2 https://github.com/sarastef98/pawpost/issues/2

# Browse pet ads without an account

Layer: backend
Depends on: #21

## Summary
Add `GET /api/pet-ads`, the list API for the home page. Anyone, including visitors without an account, can read a page of `Active` ads, newest first, and filter by species and type. The cards (#19) show the title, so this depends on the title issue (#21).

## User story
As a visitor, I want to browse ads without creating an account, so that I can find an animal to adopt or buy.

## Acceptance criteria
- [ ] `GET /api/pet-ads` can be called without authentication.
- [ ] Only `Active` ads are returned. `Draft`, `Reserved` and `Closed` ads are never listed.
- [ ] Ads are sorted newest first by created date. The query is `AsNoTracking()` and lives in the service. An index on status and created date is added by a migration, as the database rules require (single-column indexes exist today).
- [ ] The list is paged with `page` (starting at 1) and `pageSize`. The default page size is 20. The maximum page size is 100 (proposed value, see open questions).
- [ ] A `pageSize` above the maximum, or a page number below 1, is rejected with 400 and `ProblemDetails`.
- [ ] Optional filter `species`: matched case-insensitively, exact match on the trimmed value. Species stays free text. Maximum length 50, longer is 400.
- [ ] Optional filter `type`: `Adoption` or `Sale`. Any other value, including a number, is rejected with 400 and `ProblemDetails`.
- [ ] Both filters can be combined.
- [ ] The response has `items`, `totalCount`, `page` and `pageSize` (proposed names). `totalCount` is the number of ads that match the filters, so the frontend can compute the pages. An empty result is 200 with empty `items` and `totalCount` 0.
- [ ] Each item has `id`, `title`, `type`, `price` (only for `Sale`, absent for `Adoption`), `location`, plus `species` and `createdAt`. No other fields unless the card needs them.
- [ ] Responses use DTOs and never expose the owner's email or phone.
- [ ] Tests cover: success, filter by species (case-insensitive), filter by type, both filters together, invalid type (400), paging (second page, `totalCount`), newest first, only `Active` ads listed, no owner contact data in the response, page size above the cap (400), invalid page (400), and that no login is needed.

## Out of scope
- Messaging between buyer and owner ("maybe later" in the notes).
- Creating, editing and closing ads.
- The single ad endpoint (#18).
- Photos, search by text, sorting options, saving favorites (not now, per the notes).
- Showing non-`Active` ads, including to their owner.
- The home page screen itself (#19). Filters and page live in the URL there, which is frontend only.

## Open questions
- Which species should the filter offer? The backend does not need a list (species stays free text), but the frontend does. The client must supply it.
- What is the maximum page size? The notes only say 20 per page. Recommended default: 100.
- Should `OwnerId` stay in the public list items? It is in `PetAdResponse` today and is not contact data. Recommended default: leave it out of the list items, since the cards do not need it. Confirm.
- Are filters by price or location needed? The notes only mention species and type, so they are not planned.
- How can a buyer contact an owner while contact details are private and messaging is only "maybe later"?

## Source
Meeting of 14 Nov: the home page shows the list of active pet ads as cards, newest first, with no login. Visitors filter by species and type. The list is paged, 20 ads per page. Earlier, the client said anyone should be able to browse ads without an account.
