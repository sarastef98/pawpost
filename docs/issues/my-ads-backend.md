Issue: #50
URL: https://github.com/sarastef98/pawpost/issues/50

# Add the owner-only list of my own ads

Layer: backend
Depends on: #44

## Summary
Add an authenticated endpoint that returns the logged-in user's own `Active` ads, paginated, newest first. It is for the "My ads" tab on the owner's profile. The public creators endpoint (`GET /api/creators/{id}`) stays public and unchanged.

## User story
As a logged-in user, I want to list my own ads, so that I can see and manage them.

## Decisions
- Decided by the client: the "My ads" list shows `Active` ads only (no drafts, reservation is not offered, and closed ads go to the Closed tab).

## Acceptance criteria
- [ ] A new endpoint returns the caller's own ads (route chosen in the plan; for example `GET /api/pet-ads/mine`, or an owner-only route under the creator id such as `GET /api/creators/{id}/pet-ads`). It requires authentication and takes the caller from the login cookie, never from the request body.
- [ ] Not logged in returns 401 with `ProblemDetails`.
- [ ] Another user's list is never returned. If the plan uses a route with a user id, a different id than the caller's returns 403 with `ProblemDetails`. If the plan uses a route that only ever returns the caller's own ads, there is no id and this case does not exist.
- [ ] Only `Active` ads are listed (client decision). `Draft`, `Reserved` and `Closed` ads are not (closed ads go to a separate "Closed ads" issue).
- [ ] The list is paginated with `page` (starting at 1) and `pageSize`: default 20, maximum 100, as in the public lists. A `pageSize` above the maximum or a page below 1 returns 400 with `ValidationProblemDetails`.
- [ ] Ads are sorted newest first. The query is `AsNoTracking()` and lives in `PetAdService` (reusing the existing per-owner `Active` query where it fits), behind its interface. The controller only calls the service.
- [ ] A user with no `Active` ads gets 200 with an empty list and `totalCount` 0.
- [ ] The item DTO has public ad fields only, the same as the list items of the public lists. No owner email, phone or `UserName`. The EF entity is never returned.
- [ ] The existing public creators endpoint, its route, its responses and its `Active`-only rule are unchanged.
- [ ] No personal data is logged.
- [ ] Tests cover: success (only the caller's own `Active` ads, newest first, closed and other users' ads absent); not logged in (401); another user's list (403, only if the route has an id); validation (page below 1, `pageSize` above the maximum, default page size); pagination; empty list; no email, phone or `UserName` in the response; the public creators endpoint still returns its public result. Tests use synthetic users and ads only.

## Out of scope
- Screens, tabs and wording (see `my-profile-tabs-frontend.md`).
- Closed ads for the owner (the "Closed ads" tab is a separate issue).
- Favorite ads (favorites feature).
- Closing an ad (see `close-pet-ad-backend.md`).
- Any change to the public creator profile endpoint.
- Regenerating the frontend `schema.d.ts` (separate step after merge).

## Open questions
None.

## Source
Meeting of 16 Nov (`docs/meeting-notes/2026-11-16-my-profile-tabs.md`). The owner's ads appear in a "My ads" tab on their profile. The data is owner-only: 401 when not logged in, 403 for another user's list. The public profile endpoint stays unchanged. The list is paginated and returns public fields only.
