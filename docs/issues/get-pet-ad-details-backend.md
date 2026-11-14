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
- [ ] `GET /api/pet-ads/{id}` returns 200 with a new details response DTO for an `Active` ad. It has the same fields as the existing `PetAdResponse` except `OwnerId`, and it includes the title (the title exists since #21). `PetAdResponse` keeps its current shape.
- [ ] The response never contains the owner's email, phone or owner id.
- [ ] An unknown id returns 404 with `ProblemDetails`.
- [ ] An ad that is not `Active` returns 404 with `ProblemDetails` to everyone, including the owner.
- [ ] A request without a login returns 401 with `ProblemDetails`. Login required is a default chosen by the product owner and is NOT yet confirmed by the client (see open questions).
- [ ] An id that is not a Guid returns 400 with `ValidationProblemDetails` on `id`. An anonymous request gets 401 first, also for a non-Guid or unknown id.
- [ ] The read is `AsNoTracking()`, lives in the service, and the controller only calls it.
- [ ] The 201 response of `POST /api/pet-ads` gets a `Location` header pointing to this endpoint. It uses a named route, shared by both actions. The 201 status and body stay as today.
- [ ] Tests cover: success, unknown id (404), non-active ad (404), not logged in (401), non-Guid id (400), 401 before 400 and 404, the `Location` header, and that no owner contact data or owner id is in the response.

## Out of scope
- The ad list endpoint (#2).
- Photos.
- Showing contact details or messaging the owner.
- Editing or closing ads.
- Owner views of their own non-`Active` ads (they come with the edit and close features).
- Regenerating the frontend `schema.d.ts`. This is a separate step after merge.

## Open questions
- Does the client accept that the details API requires login? The notes say the details page is for logged-in users, but the API rule is not confirmed by the client. Recommended default: yes, require login (401), so the rule is enforced by the server and not only by the screen. If the client says no: add `[AllowAnonymous]` to the action and replace the 401 tests with "works without a login" tests (not logged in returns 200, tampered cookie returns 200).

## Source
Clicking a card on the home page opens a details page with all the ad's information. Whether the details API should require login was not decided by the client.
