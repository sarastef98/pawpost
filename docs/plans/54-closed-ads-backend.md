# Plan: Add the owner-only list of my closed ads (#54)

Status: Approved
Layer: backend

Issue: https://github.com/sarastef98/pawpost/issues/54 (local copy: `docs/issues/closed-ads-backend.md`)
Depends on: #50 (`GET /api/pet-ads/mine`, `MyPetAdsRequest`, `GetMyPetAdsAsync` test helper), #52 (`POST /api/pet-ads/{id}/close`, `PetAdService.CloseAsync`, `ClosePetAdAsync` test helper), #44 (`PetAdService.ListActiveByOwnerAsync`, shared `ListItemSelector`, index `IX_PetAds_OwnerId_Status_CreatedAt`), #3 (login cookie, `ClaimsPrincipalExtensions.GetUserId`). All of these are merged and exist in the code, so the issue is not blocked by them.
Source: `docs/meeting-notes/2026-11-16-my-profile-tabs.md`, `docs/meeting-notes/2026-11-16-close-ad.md`.
Follow-up: `closed-ads-tab-frontend.md` (the "Closed ads" tab) uses this endpoint after `schema.d.ts` is regenerated (separate step, out of scope here).
Rules that apply: `CLAUDE.md`, `.claude/rules/creator-profile.md`, `.claude/rules/csharp-conventions.md`, `.claude/rules/testing.md`, `.claude/rules/migrations.md` (only to confirm that no migration is created).

## Goal
A logged-in user can call `GET /api/pet-ads/mine/closed` and get their own `Closed` ads, newest first by `CreatedAt` (then `Id`), paginated with `page` (default 1) and `pageSize` (default 20, maximum 100). The caller is always taken from the login cookie, so the endpoint can never return another user's ads. Not logged in (or an invalid cookie) gives 401 `ProblemDetails`. Bad paging values give 400 `ValidationProblemDetails`. A user without closed ads gets 200 with an empty list and `totalCount` 0. The items are the same public list items as in `GET /api/pet-ads/mine`, `GET /api/pet-ads` and `GET /api/creators/{id}`: no email, phone or `UserName`, no extra date. The endpoint is read-only.

Every other endpoint is unchanged: `GET /api/pet-ads` (with filters), `GET /api/pet-ads/{id}` (still 404 for a closed ad, also for its owner), `GET /api/pet-ads/mine` (still `Active` only), `GET /api/creators/{id}` and the assistant search never show closed ads.

Out of scope (from the issue): screens, tabs and wording; closing and reopening an ad; any change to a public endpoint, including the details endpoint; a `ClosedAt` field or any schema change; favorites; regenerating `schema.d.ts`.

## Approach
1. **Route: `GET /api/pet-ads/mine/closed` (no id in the route).** The issue asks the route to follow the choice made in #50 (`GET /api/pet-ads/mine`). Reasons:
   - Same as #50: the caller is the only possible owner, so another user's list cannot be requested at all. There is no id to compare, so the 403 case does not exist (issue criterion 3).
   - It reads as "my ads, the closed ones" and sits right next to `/mine` in the `PetAds` feature, under `/api/pet-ads` (plural noun, CLAUDE.md).
   - Alternatives considered: `GET /api/pet-ads/mine?status=Closed` would turn `/mine` into a status filter, which invites `Draft`/`Reserved` values and changes the #50 contract; rejected. `GET /api/creators/{id}/closed-ads` would need an id that must always equal the cookie's id, plus a 403 branch; rejected for the same reason #50 rejected it.
   - **Route precedence.** `mine/closed` has two segments. It cannot match `GET {id}` (one segment) or `GET mine` (one segment). The only other two-segment template is `POST {id}/close`, which has a different HTTP verb and a different literal (`close`, not `closed`). Among GET templates, a two-segment path only matches `mine/closed`, so no route conflict exists and no `Order` is needed. Today `GET /api/pet-ads/mine/closed` returns 404 (no matching route); after the change it returns the list. A routing test guards this (see tests).
2. **Authentication.** `PetAdsController` already has a class-level `[Authorize]`. The new action gets no `[AllowAnonymous]`. A missing or invalid cookie gives 401 `ProblemDetails` through the existing `OnRedirectToLogin` handler in `Program.cs`. Authentication runs before model binding and validation, so an anonymous call with bad paging values also gets 401, not 400 (same as `/mine`).
3. **Caller id.** The controller reads the id with `User.GetUserId()` (`PawsPost/Common/ClaimsPrincipalExtensions.cs`), exactly like `ListMineAsync` and `CloseAsync`. The id never comes from the route, query or body. An `ownerId` (or any other) query parameter is not bound and is ignored.
4. **Service: a sibling method, not a status parameter on the existing one.**
   - New public method on `IPetAdService`: `Task<PetAdListResponse> ListClosedByOwnerAsync(Guid ownerId, int page, int pageSize, CancellationToken cancellationToken)`.
   - Inside `PetAdService`, the body of `ListActiveByOwnerAsync` (owner filter, `AsNoTracking()`, count, long-offset guard, `CreatedAt` desc then `Id` desc, `Skip`/`Take`, `ListItemSelector`, `ToListItem`) moves into one **private** helper `ListByOwnerAsync(Guid ownerId, PetAdStatus status, int page, int pageSize, CancellationToken cancellationToken)`. `ListActiveByOwnerAsync` calls it with `PetAdStatus.Active`, `ListClosedByOwnerAsync` with `PetAdStatus.Closed`. The query and the result of `ListActiveByOwnerAsync` stay exactly the same.
   - Why not a public `status` parameter on `ListActiveByOwnerAsync` (or a renamed `ListByOwnerAsync(ownerId, status, ...)` on the interface): `CreatorService` (the **public** profile) and `ListMineAsync` (#50) call `ListActiveByOwnerAsync`. With a public status parameter, the rule "public and my-ads lists show only `Active`, the closed list shows only `Closed`" would move from the service into each caller (a controller or another service would pick the status). CLAUDE.md says business rules live in services, and one wrong argument in `CreatorService` would leak closed ads publicly. Two named methods keep the rule in `PetAdService`, keep the public method's signature and callers untouched (no change in `Features/Creators/`), and make the intent clear at the call site.
   - Why not a full copy of the method: the issue asks to reuse the per-owner query and the shared projection where they fit. The private helper keeps one query and one projection, so all list items keep exactly the same public fields.
   - The new method only reads. It sets no status and has no reopen path.
5. **Request DTO: reuse `MyPetAdsRequest`.** It already lives in `Features/PetAds/`, has `page`/`pageSize` with defaults 1 and 20 (`ListPetAdsRequest.DefaultPageSize`) and `Validate` with max 100 (`ListPetAdsRequest.MaxPageSize`). Both endpoints are "my ads" lists with identical paging, so a second identical class would only duplicate code. No species/type filter (not in the issue). If the team prefers a separate `MyClosedPetAdsRequest` so the two can diverge later, that is a small change (see open points).
6. **Response DTO.** The existing `PetAdListResponse { items, totalCount, page, pageSize }` with `PetAdListItemResponse` items (`id, title, type, species, price, location, createdAt, creator { id, displayName }`). Same as the other lists (issue criterion "same as the list items of the other lists"). No `status` and no `closedAt` field is added (the issue says no other date; status is implied by the endpoint). The EF entity is never returned.
7. **Index.** No new index, no migration. The query filters on `OwnerId` and `Status` and sorts on `CreatedAt` (then `Id`); the composite index `IX_PetAds_OwnerId_Status_CreatedAt` (`AppDbContext`, from #44) covers it, exactly as for `/mine`.
8. **Logging.** The new action and service method log nothing. No email, display name or user id is written to logs.
9. **Unchanged public behavior.** `ListAsync`, `GetByIdAsync`, `ListActiveByOwnerAsync` (used by `/mine` and `CreatorService`) and the assistant search keep their `Status == Active` filters. No code in those paths changes except the internal move of `ListActiveByOwnerAsync`'s body into the private helper, which existing tests (`ListMyPetAdsTests`, `GetCreatorProfileTests`, `ClosePetAdVisibilityTests`) protect.

## API endpoints

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| GET | `/api/pet-ads/mine/closed` (new) | logged in; returns only the caller's own `Closed` ads (caller from the login cookie) | query `page` (int, default 1, min 1), `pageSize` (int, default 20, 1 to 100) as `MyPetAdsRequest` | `PetAdListResponse { items: PetAdListItemResponse[], totalCount, page, pageSize }` | 200 (also with empty `items`), 400 (`ValidationProblemDetails`: `page` below 1, `pageSize` outside 1 to 100, non-numeric values), 401 (`ProblemDetails`, not logged in or invalid cookie) |

No 403: the route has no user id and only ever returns the caller's own ads (issue criterion 3). No 404: an empty result is 200.

Order of checks: 401 (authentication middleware) -> 400 (binding and `Validate` of `MyPetAdsRequest`) -> 200.

Controller attributes on the new action `ListMineClosedAsync`: `[HttpGet("mine/closed")]`, `[ProducesResponseType<PetAdListResponse>(StatusCodes.Status200OK)]`, `[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]`, `[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]`, same style as `ListMineAsync`. A short comment: the owner always comes from the cookie, the service returns only `Closed` ads, the list is read-only.

Unchanged: `GET /api/pet-ads`, `GET /api/pet-ads/mine`, `GET /api/pet-ads/{id}`, `POST /api/pet-ads`, `POST /api/pet-ads/{id}/close`, `GET /api/creators/{id}`, `POST /api/assistant/search`.

## Screens and components
not applicable

## Data changes
- Entities and fields: none. No `ClosedAt` field (client decision). The existing index `IX_PetAds_OwnerId_Status_CreatedAt` serves the query.
- Migration name: none (no schema change). If `dotnet ef migrations` ever reports pending model changes during this work, stop and ask.

## Files to create or change
Create:
- `PawsPost.Tests/Features/PetAds/ListMyClosedPetAdsTests.cs`: the tests below.

Change:
- `PawsPost/Features/PetAds/IPetAdService.cs`: add `ListClosedByOwnerAsync(Guid ownerId, int page, int pageSize, CancellationToken cancellationToken)`.
- `PawsPost/Features/PetAds/PetAdService.cs`: add the private helper `ListByOwnerAsync(ownerId, status, page, pageSize, cancellationToken)` with the existing per-owner query; `ListActiveByOwnerAsync` calls it with `Active` (same behavior), new `ListClosedByOwnerAsync` calls it with `Closed`.
- `PawsPost/Features/PetAds/PetAdsController.cs`: new action `ListMineClosedAsync` (`GET mine/closed`), reads `User.GetUserId()`, calls `ListClosedByOwnerAsync`, returns `Ok`.
- `PawsPost.Tests/Infrastructure/PetAdTestHelpers.cs`: add `GetMyClosedPetAdsAsync(this HttpClient client, string? query = null)` next to `GetMyPetAdsAsync`.

Not changed: `MyPetAdsRequest.cs`, `PetAdListResponse.cs`, `PetAdListItemResponse.cs`, `ListActiveByOwnerAsync`'s signature, `GetByIdAsync`, `ListAsync`, `CloseAsync`, everything in `Features/Creators/` and `Features/Assistant/`, `AppDbContext.cs`, migrations, `Program.cs` (no new service to register), `PawsPost.Web`, existing tests. If the diff touches any of these, stop and check why.

No new NuGet packages.

## Tests to write
All in `ListMyClosedPetAdsTests`. Names follow `Method_Scenario_ExpectedResult`, Arrange / Act / Assert, one reason to fail each. Each test creates its own `PawsPostFactory` (own database, own fakes). Synthetic users only (`owner@x.com` / "Anna K", `other@x.com` / "Boris"), ads built with `BuildPetAd` and fixed `CreatedAt` values, seeded with `SeedAdsAsync` (status `Closed` set directly) or closed through `ClosePetAdAsync` (#52 endpoint). Logged-in clients from `CreateLoggedInClientAsync`, ids from `GetMyIdAsync` / `SeedOwnerAsync`. Small private helpers (`ItemIds`, `ReadJsonAsync`, `AssertUnauthorizedProblemAsync`) as in `ListMyPetAdsTests`.

Success:
- `ListMyClosedPetAds_OwnClosedAds_Returns200WithThoseAds` (ids, titles and `totalCount`)
- `ListMyClosedPetAds_OwnActiveAds_AreNotListed` (one `Active` and one `Closed` own ad; only the closed one)
- `ListMyClosedPetAds_OwnDraftOrReservedAds_AreNotListed` (theory over `Draft`, `Reserved`, seeded directly)
- `ListMyClosedPetAds_OtherUsersClosedAds_AreNotListed`
- `ListMyClosedPetAds_SeveralAds_SortsNewestFirstByCreatedAt` (three closed ads seeded out of order)
- `ListMyClosedPetAds_SameCreatedAt_SortsByIdDescending` (two closed ads with the same `CreatedAt` and fixed ids; tie-breaker)
- `ListMyClosedPetAds_NoClosedAds_ReturnsEmptyListWithTotalCountZero` (user with only an `Active` ad)
- `ListMyClosedPetAds_NoAdsAtAll_ReturnsEmptyListWithTotalCountZero`
- `ListMyClosedPetAds_AdItem_HasSameFieldsAsMyAdsItem` (read the item raw JSON from `/mine` while `Active`, close it via the endpoint, the item from `/mine/closed` has identical raw JSON; `creator` is the caller's id and display name; no extra date or status field)

Closing through #52 and visibility:
- `ListMyClosedPetAds_AdClosedThroughEndpoint_AppearsInClosedList`
- `ListMyClosedPetAds_AdClosedThroughEndpoint_DisappearsFromMyAds` (`/mine` no longer lists it, another `Active` ad still listed)
- `ListMyClosedPetAds_ClosedAd_IsNotInPublicList` (`GET /api/pet-ads`, anonymous, also with `species` and `type` filters matching the ad)
- `ListMyClosedPetAds_ClosedAd_DetailsReturn404ForOwner` (`GET /api/pet-ads/{id}` by the owner)
- `ListMyClosedPetAds_ClosedAd_DetailsReturn404ForOtherUser`
- `ListMyClosedPetAds_ClosedAd_IsNotInCreatorProfile` (`GET /api/creators/{ownerId}`, anonymous and as the owner)
- `ListMyClosedPetAds_ClosedAd_IsNotReturnedByAssistantSearch` (`SeedAndIndexAdsAsync`, close via the endpoint, `PostAssistantSearchAsync` does not return the id)
- Existing `ClosePetAdVisibilityTests`, `ListMyPetAdsTests` (including `ListMyPetAds_OwnNonActiveAds_AreNotListed` with `Closed`), `GetPetAdTests`, `ListPetAdsTests`, `GetCreatorProfileTests` and the assistant tests stay unchanged and must pass. They prove the `ListActiveByOwnerAsync` refactor did not change #50 or the public profile.

Authorization:
- `ListMyClosedPetAds_NotLoggedIn_Returns401ProblemDetails` (status, `application/problem+json`, `status` 401)
- `ListMyClosedPetAds_NotLoggedInWithInvalidPaging_Returns401` (`page=0&pageSize=500`; authentication runs before validation)
- `ListMyClosedPetAds_TamperedCookie_Returns401` (same approach as `ListMyPetAds_TamperedCookie_Returns401`)
- `ListMyClosedPetAds_TwoUsersWithClosedAds_EachSeesOnlyOwnClosedAds`
- `ListMyClosedPetAds_OwnerIdInQuery_IsIgnored` (user B calls `?ownerId=<A's id>` and gets only B's closed ads)
- 403 "not the owner": not applicable, the route has no id (issue criterion 3). The two tests above cover "another user's closed ads are never returned".

Pagination and validation:
- `ListMyClosedPetAds_NoPageSize_UsesDefaultOf20` (25 own closed ads: 20 items, `totalCount` 25, `page` 1, `pageSize` 20)
- `ListMyClosedPetAds_SecondPage_ReturnsRemainingAds` (the 5 oldest, newest first)
- `ListMyClosedPetAds_PageBeyondLastPage_ReturnsEmptyItemsWithTotalCount`
- `ListMyClosedPetAds_PageSizeExactly100_Returns200`
- `ListMyClosedPetAds_PageSizeAbove100_Returns400` (`AssertBadRequestForOnlyAsync(response, "pageSize")`)
- `ListMyClosedPetAds_PageBelow1_Returns400` (`page=0`, only the `page` error)
- `ListMyClosedPetAds_PageSizeBelow1_Returns400`
- `ListMyClosedPetAds_PageNotANumber_Returns400` (`page=abc`)

Privacy and logging:
- `ListMyClosedPetAds_Response_HasNoEmailPhoneOrUserName` (raw JSON contains the display name but not the caller's email, `email`, `phoneNumber`, `userName`)
- `ListMyClosedPetAds_Success_LogsNoPersonalData` (`CapturingLoggerProvider` as in `ListMyPetAds_Success_LogsNoEmailOrDisplayName`; no message contains the email or display name)

Routing:
- `ListMyClosedPetAds_ClosedRoute_ReturnsList` (logged in, `GET /api/pet-ads/mine/closed` gives 200 with an `items` array, not 404 or 405). The existing `ListMyPetAds_MineRoute_IsNotHandledAsAdId` keeps guarding `/mine`.

## Steps
Each step is one commit. Branch `feature/54-closed-ads-backend` from `main`, only after the plan is approved.

1. Extract the private `ListByOwnerAsync(ownerId, status, ...)` helper in `PetAdService` and make `ListActiveByOwnerAsync` call it with `Active`. No behavior change; run the existing `ListMyPetAdsTests`, `GetCreatorProfileTests` and `ClosePetAdVisibilityTests` (green before and after). Commit: `refactor: share the per-owner ad list query (#54)`. This refactor is inside the issue ("reuse the per-owner query").
2. Add `IPetAdService.ListClosedByOwnerAsync` and its implementation, the `ListMineClosedAsync` action (`GET mine/closed`), the `GetMyClosedPetAdsAsync` test helper, and the success, authorization and routing tests. Commit: `feat: add the list of my closed ads (#54)`.
3. Add the pagination, validation, privacy and logging tests. Commit: `test: cover paging, validation and privacy of my closed ads (#54)`.
4. Add the "closing through #52" and visibility tests (closed list, my ads, public list with filters, details 404 for owner and other user, creator profile, assistant search). Commit: `test: prove closed ads stay hidden outside the owner's closed list (#54)`.
5. Run `dotnet build` (no warnings), `dotnet test`, `dotnet format --verify-no-changes`. Check the diff: no migration, no change in `Features/Creators/`, `Features/Assistant/`, `GetByIdAsync`, `ListAsync` or DTOs; no status write or reopen path; no email, phone or `UserName` in responses; no log calls added. Open the PR with `Closes #54`; the description notes that `schema.d.ts` must be regenerated for `closed-ads-tab-frontend.md`.

## Risks and open questions
Open questions:
None that block the work. The client decided sort order, no `ClosedAt`, no migration, unchanged details endpoint, owner-only access with 401, and public fields only. The issue leaves the route to the plan; the choices below are plan defaults.

Not decided by the issue, plan default (no answer needed unless you disagree):
- Route `GET /api/pet-ads/mine/closed` with no user id, so there is no 403 case (same choice as #50). The meeting notes mention "another user's list gives 403", which only applies to a route with an id; the issue explicitly allows the id-less route.
- A sibling service method `ListClosedByOwnerAsync` plus a private shared helper, instead of a public status parameter (reasons in Approach step 4).
- `MyPetAdsRequest` is reused for the paging of both "my" lists. If the team wants a separate request class per endpoint, add `MyClosedPetAdsRequest` with the same checks; nothing else changes.
- Items do not include `status`; every item in this list is `Closed` by definition. The frontend tab issue may want a status badge; it can show it without a field.

Risks:
- **Sort by created date, not by close date.** The client accepted that a recently closed old ad appears lower in the list. If the client later wants "recently closed first", a `ClosedAt` field and a migration are needed (separate issue).
- **Refactor of `ListActiveByOwnerAsync`.** Moving its body into a helper could change the public profile or `/mine` by mistake (for example a wrong status). Step 1 is a separate commit, and the existing tests for `/mine`, the creator profile and closed-ad visibility must stay green unchanged.
- **Route precedence.** `mine/closed` does not overlap with `{id}`, `mine` or `POST {id}/close`. The routing test fails if someone later adds a conflicting template (for example `GET {id}/{something}`).
- **Deleted user with a still-valid cookie.** The query returns 200 with an empty list. No account deletion exists today, so this is accepted (same as #50).
- **Data visible only to the owner.** A closed ad's public fields (title, location, price) are returned to the owner only. They contain no contact data; nothing new is exposed to others.
- **OpenAPI document.** The new endpoint appears in the OpenAPI document. `OpenApiDocumentTests` only looks up specific existing paths, so no existing assertion needs to change.
- **Frontend contract.** `schema.d.ts` regeneration is a separate step after merge (out of scope); the "Closed ads" tab cannot use the endpoint until then.

Stop-and-ask rules:
- If a migration, a change in the creators or assistant feature, a change to the details endpoint, or a new package seems needed, stop and ask.
- If tests still fail after two attempts, stop and report.
