Issue: #18
URL: https://github.com/sarastef98/pawpost/issues/18

# Add endpoint to get one pet ad

Layer: backend
Depends on: #1

## Summary
Add `GET /api/pet-ads/{id}` that returns all public information of one ad. Today the API only has `POST /api/pet-ads`, so the details page has nothing to read.

## User story
As a user, I want to open one ad and see all its information, so that I can decide whether to adopt or buy the animal.

## Acceptance criteria
- [ ] `GET /api/pet-ads/{id}` returns 200 with a response DTO for an `Active` ad. It has the same fields as the existing `PetAdResponse` (plus the title, if the client wants one, see open questions).
- [ ] The response never contains the owner's email or phone.
- [ ] An unknown id returns 404 with `ProblemDetails`.
- [ ] An ad that is not `Active` returns 404 to everyone (assumption, see open questions).
- [ ] A request without a login returns 401 with `ProblemDetails` (assumption, see open questions).
- [ ] The read is `AsNoTracking()`, lives in the service, and the controller only calls it.
- [ ] The 201 response of `POST /api/pet-ads` gets a `Location` header pointing to this endpoint (the controller has a comment saying it is missing for lack of this endpoint).
- [ ] Tests cover: success, unknown id (404), non-active ad (404), not logged in (401), and that no owner contact data is in the response.

## Out of scope
- The ad list endpoint (#2).
- Photos.
- Showing contact details or messaging the owner.
- Editing or closing ads.

## Open questions
- Should this API itself require login? The notes say the details page is for logged-in users but the API question is not decided. Recommended default: yes, require login (401), so the rule is enforced by the server and not only by the screen. If the client says no, drop the 401 criterion and use `[AllowAnonymous]`.
- Can the owner open their own `Draft`, `Reserved` or `Closed` ads here? Recommended default: no, only `Active` ads for now (404 otherwise); owner views come with the edit/close features.
- Should the ad have a title? The notes say each card shows "the ad's title", but neither `CreatePetAdRequest` nor `PetAdResponse` nor the `PetAd` entity has one. Adding it is a schema change (migration) and a change to #1's create contract. Needs a client decision (see also #2).
- Is `OwnerId` allowed in the public response? It is in `PetAdResponse` today. It is not contact data, but confirm.

## Source
Clicking a card on the home page opens a details page with all the ad's information. Whether the details API should require login was not decided.
