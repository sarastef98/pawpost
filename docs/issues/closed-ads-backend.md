Issue: #54
URL: https://github.com/sarastef98/pawpost/issues/54

# Add the owner-only list of my closed ads

Layer: backend
Depends on: #52, #50

## Summary
Add an authenticated endpoint that returns the logged-in user's own `Closed` ads, paginated, newest first. It is for the "Closed ads" tab on the owner's profile. It follows the same pattern as the owner-only "My ads" list (#50). Closed ads stay hidden from everyone else: every public endpoint is unchanged.

## User story
As a logged-in user, I want to list my own closed ads, so that I can still see the ads I closed.

## Decisions
- Decided by the client: a closed ad is shown to its owner only, in a separate "Closed ads" tab. Everyone else never sees closed ads.
- Decided by the client: closing is final. There is no reopen, so this list is read-only.
- Decided by the client: the list is sorted newest first by the created date (`CreatedAt`, then `Id` as tie-breaker). No `ClosedAt` field is added and no migration is needed for this. No date other than what the normal cards show is displayed.
- Decided by the client: the owner cannot open a closed ad's details page. The details endpoint `GET /api/pet-ads/{id}` is not changed and still returns 404 for non-Active ads.

## Acceptance criteria
- [ ] A new endpoint returns the caller's own `Closed` ads. The route is chosen in the plan and follows the route chosen for #50 (for example `GET /api/pet-ads/mine/closed`). It requires authentication and takes the caller from the login cookie, never from the request body.
- [ ] Not logged in returns 401 with `ProblemDetails`.
- [ ] Another user's closed ads are never returned. If the plan uses a route with a user id, a different id than the caller's returns 403 with `ProblemDetails`. If the route only ever returns the caller's own ads, there is no id and this case does not exist (same choice as #50).
- [ ] Only `Closed` ads of the caller are listed. `Active`, `Draft` and `Reserved` ads, and ads of other users, are not.
- [ ] The list is paginated with `page` (starting at 1) and `pageSize`: default 20, maximum 100, as in the other lists. A `pageSize` above the maximum or a page below 1 returns 400 with `ValidationProblemDetails`.
- [ ] Ads are sorted newest first by `CreatedAt`, then `Id` as tie-breaker, as the other lists. No `ClosedAt` field is added and there is no migration for this issue. The query is `AsNoTracking()`, lives in `PetAdService` behind `IPetAdService` (reusing the per-owner query and the shared list projection where they fit), and the controller only calls the service. The existing index on owner, status and created date covers the sort.
- [ ] A user with no closed ads gets 200 with an empty list and `totalCount` 0.
- [ ] The item DTO has public ad fields only, the same as the list items of the other lists. No owner email, phone or `UserName`, and no date other than what the normal list items already have. The EF entity is never returned.
- [ ] The endpoint is read-only. It adds no way to reopen a closed ad or to change its status.
- [ ] The details endpoint stays unchanged: `GET /api/pet-ads/{id}` still returns 404 for a closed ad (and any non-Active ad), for the owner too. No owner-only access to a closed ad's details is added.
- [ ] Other public endpoints are unchanged: a closed ad is still absent from `GET /api/pet-ads` (including filters), from the public creator profile endpoint (`GET /api/creators/{id}`), from the owner's "My ads" list (#50), and from the assistant search.
- [ ] No personal data is logged.
- [ ] Tests cover: success (only the caller's own `Closed` ads, newest first by created date; `Active` ads and other users' closed ads absent); not logged in (401); another user's list (403, only if the route has an id); validation (page below 1, `pageSize` above the maximum, default page size); pagination; empty list; no email, phone or `UserName` in the response; a closed ad is absent from the public list, the public creator profile and the "My ads" list; the details endpoint still returns 404 for a closed ad, including for its owner. Closed ads in tests are created by closing through the service or the endpoint from #52, or set directly in the test database. Tests use synthetic users and ads only.

## Out of scope
- Screens, tabs and wording (see `closed-ads-tab-frontend.md`).
- Closing an ad (see `close-pet-ad-backend.md`) and reopening a closed ad.
- The owner's "My ads" list (see `my-ads-backend.md`).
- Any change to a public endpoint, including the details endpoint.
- A `ClosedAt` field or any schema change.
- Favorite ads (favorites feature).
- Regenerating the frontend `schema.d.ts` (separate step after merge).

## Open questions
None.

## Source
Meeting of 16 Nov (`docs/meeting-notes/2026-11-16-my-profile-tabs.md` and `2026-11-16-close-ad.md`). A closed ad is shown to its owner only, in a separate "Closed ads" tab on their profile. Everyone else never sees closed ads. Closing is final, a soft close (status `Closed`), with no reopen. The data is owner-only: 401 when not logged in. The list is paginated and returns public fields only. The client later accepted two recommendations: sort by created date (no `ClosedAt`, no migration), and no owner access to a closed ad's details (details endpoint unchanged).
