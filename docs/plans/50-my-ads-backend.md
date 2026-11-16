# Plan: Add the owner-only list of my own ads (#50)

Status: Approved
Layer: backend

Issue: https://github.com/sarastef98/pawpost/issues/50 (local copy: `docs/issues/my-ads-backend.md`)
Depends on: #44 (`PetAdService.ListActiveByOwnerAsync`, `PetAdListItemResponse` with `creator`, `GET /api/creators/{id}`, index `IX_PetAds_OwnerId_Status_CreatedAt`), #3 (accounts, login cookie, `ClaimsPrincipalExtensions.GetUserId`). All of these already exist in the code, so the issue is not blocked by them.
Follow-up: `my-profile-tabs-frontend.md` (the "My ads" tab) uses this endpoint after `schema.d.ts` is regenerated (separate step, out of scope here).
Rules that apply: `CLAUDE.md`, `.claude/rules/creator-profile.md`, `.claude/rules/csharp-conventions.md`, `.claude/rules/testing.md`.

## Goal
A logged-in user can call `GET /api/pet-ads/mine` and get their own `Active` ads, newest first, paginated with `page` (default 1) and `pageSize` (default 20, maximum 100). The caller is always taken from the login cookie, so the endpoint can never return another user's ads. Not logged in gives 401 `ProblemDetails`. Bad paging values give 400 `ValidationProblemDetails`. A user without `Active` ads gets 200 with an empty list and `totalCount` 0. The items are the same public list items as in `GET /api/pet-ads` and `GET /api/creators/{id}`: no email, phone or `UserName`. The public creators endpoint is not touched.

Out of scope (from the issue): screens and tabs, the owner's "Closed ads" list, favorites, closing an ad (#52), any change to `GET /api/creators/{id}`, regenerating `schema.d.ts`.

## Approach
1. **Route: `GET /api/pet-ads/mine` (no id in the route).** The issue lets the plan choose between a "mine" route and an owner-only route with a user id. This plan picks `/mine` because:
   - The caller is the only possible owner, so "another user's list" cannot be requested at all. There is no id to compare and no 403 branch to get wrong. The issue says that in this case the 403 criterion does not exist.
   - With `/api/creators/{id}/pet-ads` the id would be redundant (it must always equal the cookie's id) and would add an extra check and an extra failure mode.
   - It sits in the `PetAds` feature, next to the other ad lists, and under `/api/pet-ads` as CLAUDE.md asks (plural nouns).
   - Routing: in ASP.NET Core a literal segment (`mine`) has higher precedence than a parameter segment (`{id}`), so `GET /api/pet-ads/mine` reaches the new action, not `GetByIdAsync`. Today `/api/pet-ads/mine` returns 400 (not a Guid) for a logged-in user; after this change it returns the list. No existing test uses `mine` as an id (`GetPetAd_IdNotAGuid_*` uses `abc`, `123`, `not-a-guid`), so nothing breaks.
2. **Authentication.** `PetAdsController` already has a class-level `[Authorize]`. The new action gets no `[AllowAnonymous]`, so login is required. A missing or invalid cookie gives 401 `ProblemDetails` through the existing `OnRedirectToLogin` handler in `Program.cs`. Authentication runs before model validation, so an anonymous call with bad paging values also gets 401, not 400 (same as the details endpoint today).
3. **Caller id.** The controller reads the id with `User.GetUserId()` (`PawsPost/Common/ClaimsPrincipalExtensions.cs`), the same way `CreateAsync` and `AuthController.MeAsync` do. The id never comes from the route, query or body. An `ownerId` (or any other) query parameter is not bound and is ignored.
4. **Reuse of the existing service method.** The controller calls the existing `IPetAdService.ListActiveByOwnerAsync(User.GetUserId(), request.Page, request.PageSize, cancellationToken)` and returns `Ok(result)`. That method already does everything the issue asks: `AsNoTracking()`, `OwnerId == ownerId && Status == Active`, `CreatedAt` desc then `Id` desc, `Skip`/`Take` with the long-offset guard, and the shared `ListItemSelector` projection that reads only the creator's id and display name. No new service method and no change to `PetAdService` or `IPetAdService`. The "Active only" rule stays in the service (client decision), the controller holds no rule.
5. **Request DTO.** New `MyPetAdsRequest` in `Features/PetAds/` with `page` and `pageSize` from the query and an `IValidatableObject.Validate` with the same checks and messages as `CreatorAdsRequest` and `ListPetAdsRequest`, reusing `ListPetAdsRequest.DefaultPageSize` (20) and `ListPetAdsRequest.MaxPageSize` (100). Why a new class and not `CreatorAdsRequest`: that class belongs to the `Creators` feature, and `Creators` already depends on `PetAds`; using it from `PetAdsController` would make the two features depend on each other. Extracting a shared paging base class would touch `CreatorAdsRequest` and `ListPetAdsRequest`, which is a refactor the issue does not ask for. No species/type filter (not in the issue).
6. **Response DTO.** The existing `PetAdListResponse { items, totalCount, page, pageSize }` with `PetAdListItemResponse` items (`id, title, type, species, price, location, createdAt, creator { id, displayName }`). These are exactly "the same as the list items of the public lists" (issue). No new response type, so the frontend can reuse its list item rendering.
7. **Index.** No new index and no migration. The query filters on `OwnerId` and `Status` and sorts on `CreatedAt`; the composite index `IX_PetAds_OwnerId_Status_CreatedAt` added in #44 already covers this.
8. **Logging.** The action and service log nothing. No email, display name or user id is written to logs.

## API endpoints

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| GET | `/api/pet-ads/mine` (new) | logged in; returns only the caller's own ads (caller from the login cookie) | query `page` (int, default 1, min 1), `pageSize` (int, default 20, 1 to 100) as `MyPetAdsRequest` | `PetAdListResponse { items: PetAdListItemResponse[], totalCount, page, pageSize }` | 200 (also with empty `items`), 400 (`ValidationProblemDetails`: `page` below 1, `pageSize` outside 1 to 100, non-numeric values), 401 (`ProblemDetails`, not logged in or invalid cookie) |

No 403: the route has no user id and only ever returns the caller's own ads (issue criterion 3). No 404: an empty result is 200.

Unchanged: `GET /api/pet-ads`, `GET /api/pet-ads/{id}`, `POST /api/pet-ads`, `GET /api/creators/{id}`.

Controller attributes on the new action: `[HttpGet("mine")]`, `[ProducesResponseType<PetAdListResponse>(200)]`, `[ProducesResponseType<ValidationProblemDetails>(400)]`, `[ProducesResponseType<ProblemDetails>(401)]`, same style as the existing actions.

## Screens and components
not applicable

## Data changes
- Entities and fields: none. The existing index `IX_PetAds_OwnerId_Status_CreatedAt` (from #44) serves the query.
- Migration name: none (no schema change).

## Files to create or change
Create:
- `PawsPost/Features/PetAds/MyPetAdsRequest.cs`: query `page`, `pageSize`, defaults and `Validate` as in `CreatorAdsRequest`, reusing the constants of `ListPetAdsRequest`.
- `PawsPost.Tests/Features/PetAds/ListMyPetAdsTests.cs`: the tests below.

Change:
- `PawsPost/Features/PetAds/PetAdsController.cs`: new action `ListMineAsync` (`GET mine`), reads `User.GetUserId()`, calls `ListActiveByOwnerAsync`, returns `Ok`. A short comment that the owner always comes from the cookie and that the service returns only `Active` ads.
- `PawsPost.Tests/Infrastructure/PetAdTestHelpers.cs`: add `GetMyPetAdsAsync(this HttpClient client, string? query = null)` next to `GetPetAdsAsync` and `GetCreatorAsync`.

Not changed: `PetAdService.cs`, `IPetAdService.cs`, `PetAdListResponse.cs`, `PetAdListItemResponse.cs`, everything in `Features/Creators/`, `AppDbContext.cs`, migrations, `Program.cs` (no new service to register), `PawsPost.Web`. If the diff touches any of these, stop and check why.

No new NuGet packages.

## Tests to write
All in `ListMyPetAdsTests` unless noted. Names follow `Method_Scenario_ExpectedResult`, Arrange / Act / Assert, one reason to fail each. Each test creates its own `PawsPostFactory` (own database). Synthetic users only (`owner@x.com`, `other@x.com`), ads seeded with `BuildPetAd` / `SeedAdsAsync` and fixed `CreatedAt` values. Logged-in clients from `CreateLoggedInClientAsync`, owner id from `GetMyIdAsync`.

Success:
- `ListMyPetAds_OwnActiveAds_Returns200WithThoseAds` (checks ids, titles and `totalCount`)
- `ListMyPetAds_OtherUsersActiveAds_AreNotListed`
- `ListMyPetAds_OwnNonActiveAds_AreNotListed` (theory over `Draft`, `Reserved`, `Closed`)
- `ListMyPetAds_SeveralAds_SortsNewestFirst`
- `ListMyPetAds_AdItem_HasSameFieldsAsListItem` (same property names as an item of `GET /api/pet-ads`, including `creator` with the caller's id and display name)
- `ListMyPetAds_NoActiveAds_ReturnsEmptyListWithTotalCountZero` (user with only a `Closed` ad, and user with no ads at all)

Pagination and validation:
- `ListMyPetAds_NoPageSize_UsesDefaultOf20` (25 own ads seeded: 20 items, `totalCount` 25, `pageSize` 20, `page` 1)
- `ListMyPetAds_SecondPage_ReturnsRemainingAds`
- `ListMyPetAds_PageBeyondLastPage_ReturnsEmptyItemsWithTotalCount`
- `ListMyPetAds_PageSizeExactly100_Returns200`
- `ListMyPetAds_PageSizeAbove100_Returns400` (via `AssertBadRequestForOnlyAsync(response, "pageSize")`)
- `ListMyPetAds_PageBelow1_Returns400` (`page=0`, only the `page` error)
- `ListMyPetAds_PageSizeBelow1_Returns400`
- `ListMyPetAds_PageNotANumber_Returns400` (`page=abc`)

Authorization:
- `ListMyPetAds_NotLoggedIn_Returns401ProblemDetails` (status and `application/problem+json`)
- `ListMyPetAds_NotLoggedInWithInvalidPage_Returns401` (auth runs before validation)
- `ListMyPetAds_TamperedCookie_Returns401` (same approach as `GetPetAd_TamperedCookie_Returns401`)
- `ListMyPetAds_TwoUsersWithAds_EachSeesOnlyOwnAds` (another user cannot get someone else's list: user B logged in sees only B's ads, never A's)
- `ListMyPetAds_OwnerIdInQuery_IsIgnored` (user B calls `?ownerId=<A's id>` and still gets only B's ads; proves the caller comes from the cookie only)
- 403 "not the owner" case: not applicable, the route has no id (issue criterion 3). The two tests above cover "another user's list is never returned".

Privacy:
- `ListMyPetAds_Response_HasNoEmailPhoneOrUserName` (raw JSON does not contain the caller's email, `email`, `phoneNumber`, `userName`)
- `ListMyPetAds_Success_LogsNoEmailOrDisplayName` (capturing logger as in `LoggingAndPublicAccessTests`)

Routing and the public endpoints stay unchanged:
- `ListMyPetAds_MineRoute_IsNotHandledAsAdId` (logged in, `GET /api/pet-ads/mine` returns 200 with a list body, not 400 for the `id` field)
- Existing `GetCreatorProfileTests` (public access, `Active` only, owner logged in still sees only `Active`, no email/phone) and `GetPetAdTests` / `ListPetAdsTests` stay as they are and must pass. No file in `Features/Creators/` changes, so the public creators endpoint, its route, responses and `Active`-only rule are unchanged; the existing tests prove it.

## Steps
Each step is one commit. Branch `feature/50-my-ads-backend` from `main`, only after the plan is approved.

1. Add `MyPetAdsRequest`, the `ListMineAsync` action in `PetAdsController` calling `ListActiveByOwnerAsync` with `User.GetUserId()`, the `GetMyPetAdsAsync` test helper, and the success and authorization tests. Commit: `feat: add the list of my own active ads (#50)`.
2. Add the pagination, validation, privacy and routing tests. Commit: `test: cover paging, validation and privacy of my ads (#50)`.
3. Run `dotnet build` (no warnings), `dotnet test`, `dotnet format --verify-no-changes`. Check the diff: no changes in `Features/Creators/`, `PetAdService`, DTOs or migrations; no email, phone or `UserName` in responses; no log calls added. Open the PR with `Closes #50`; the PR description notes that `schema.d.ts` must be regenerated for the frontend tab issue.

## Risks and open questions
Open questions:
None. The issue leaves the route to the plan; the choice below is a plan default.

Not decided by the issue, plan default (no answer needed unless you disagree):
- Route `GET /api/pet-ads/mine` with no user id, so there is no 403 case. The meeting notes mention "another user's list gives 403", which only applies to a route with an id; the issue explicitly allows the id-less route. If the team prefers `GET /api/creators/{id}/pet-ads` with a 403 check, the plan must change (new action in `CreatorsController`, ownership check in a service, 403 tests).
- Response is the existing `PetAdListResponse`; each item's `creator` is the caller themself (id and display name). That is public data and the same shape as the other lists.
- A new `MyPetAdsRequest` duplicates the two paging checks of `CreatorAdsRequest` instead of sharing a base class, to avoid a refactor outside the issue.

Risks:
- **Route precedence.** The new literal route `mine` must win over `{id}`. ASP.NET Core does this by default, and the test `ListMyPetAds_MineRoute_IsNotHandledAsAdId` guards it. If a route constraint or order is changed later, the test fails instead of silently returning 400.
- **Deleted user with a still-valid cookie.** `GetUserId()` returns the id from the cookie; if the user no longer exists the query simply returns 200 with an empty list. No account deletion exists today, so this is accepted.
- **Future "Closed ads" tab.** That separate issue will need the owner's closed ads. This plan does not add a status parameter; that issue decides its own route (for example a sibling route), so nothing here needs to change.
- **Frontend contract.** The new endpoint appears in the OpenAPI document; `schema.d.ts` regeneration is a separate step after merge (out of scope), and the frontend tab cannot use the endpoint until then.

Stop-and-ask rules:
- If any change to `PetAdService`, the creators feature, the database or a new package seems needed, stop and ask.
- If tests still fail after two attempts, stop and report.
