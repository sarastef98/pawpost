Issue: #52
URL: https://github.com/sarastef98/pawpost/issues/52

# Add the close pet ad endpoint

Layer: backend
Depends on: #3, #35

## Summary
Add an endpoint that lets the owner of an `Active` ad close it. Closing is a soft close (status `Closed`) and is also the way to delete an ad. A closed ad cannot be reopened, disappears from every public list, and its vector is removed from the assistant search.

Decisions (client): there is no reserve option; nothing in this work sets `Reserved`. Closing and deleting are the same action. Closing an ad that is not `Active` is rejected with 409 (left to the team by the client).

Decisions (team): the close response is 204 with no body. Ownership is checked before status. If removing the vector fails, the close still succeeds.

Related: the owner's own ads list is a separate issue (`my-ads-backend.md`). It does not change this endpoint.

## User story
As the owner of an ad, I want to close it, so that it is no longer shown and nobody contacts me about it.

## Acceptance criteria
- [ ] A new endpoint closes one ad by id (route and verb chosen in the plan, under `/api/pet-ads`). It requires authentication and takes the caller from the login cookie, never from the request body.
- [ ] Not logged in returns 401 with `ProblemDetails`. Logged in but not the owner of the ad returns 403 with `ProblemDetails`. An unknown ad id returns 404 with `ProblemDetails`. A malformed id returns 400 with `ValidationProblemDetails`.
- [ ] Closing an `Active` ad as its owner sets the status to `Closed` and returns 204 with no body. The ad is not deleted from the database.
- [ ] The ownership check comes before the status check. A non-owner gets 403 even when the ad is already `Closed`.
- [ ] Closing an ad whose status is not `Active` (for example one that is already `Closed`), as its owner, returns 409 with `ProblemDetails` and a clear message. The ad is not changed.
- [ ] Two close requests for the same ad that arrive at the same time end with one success (204) and one 409, and the ad is `Closed` once.
- [ ] A `Closed` ad cannot be reopened. This issue adds no endpoint that changes a `Closed` ad back, and nothing in this issue sets `Reserved`.
- [ ] A `Closed` ad is no longer returned by `GET /api/pet-ads`, `GET /api/pet-ads/{id}` (404, as for any non-`Active` ad) and the public creator profile endpoint. These already filter on `Active`; tests prove it for a closed ad.
- [ ] After the status is saved, the ad's vector is removed from the assistant search with `IPetAdSearchIndexer.RemoveAsync` (see #35), so the assistant no longer returns it.
- [ ] If removing the vector fails, the close still succeeds (204) and the status stays `Closed`. The failure is logged with the ad id only, no personal data. Search already excludes non-`Active` ads (#35), so a leftover vector is not returned.
- [ ] The ownership check and the status rule live in `PetAdService`, behind `IPetAdService`. The controller only calls the service and maps the result to a status code.
- [ ] The update is a normal tracked EF write; no raw SQL. No new index is needed unless the plan shows otherwise.
- [ ] No personal data is logged. Only the ad id may be logged.
- [ ] Tests cover: close success (204 with no body, status becomes `Closed`, ad still in the database); not logged in (401); logged in but not the owner (403); non-owner closing an already `Closed` ad (403, not 409); unknown ad (404); malformed id (400); owner closing an already `Closed` ad (409, ad unchanged); closed ad missing from the list, details (404) and creator profile; the vector removal is called (fake indexer, no real AI provider); a failing indexer still gives 204, the ad is `Closed`, and the log has no personal data. Tests use synthetic users and ads only.

## Out of scope
- Screens, buttons, the confirmation dialog and wording (see `close-pet-ad-frontend.md`).
- Removing the closed ad from users' favorites lists. This belongs to the favorites feature (notes `2026-11-16-favorite-ads`).
- The owner's own ads list (see `my-ads-backend.md`) and the owner's "Closed ads" list (separate issue).
- A reserve option or any way to set `Reserved`.
- Reopening a closed ad.
- A separate delete endpoint or hard delete.
- Editing an ad.
- Regenerating the frontend `schema.d.ts` (separate step after merge).

## Open questions
None.

## Source
Meeting of 16 Nov (`docs/meeting-notes/2026-11-16-close-ad.md`), with later team decisions. The creator of an ad can only close it; there is no reserve button. Only the owner can close, only `Active` ads can be closed, closing is final and is also how an ad is deleted. Closing a non-`Active` ad gives 409. A closed ad leaves the favorites lists, which is handled by the favorites feature.
