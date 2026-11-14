# Plan: Add endpoint to get one pet ad (#18)

Status: Approved
Layer: backend

Issue: https://github.com/sarastef98/pawpost/issues/18 (local copy: `docs/issues/get-pet-ad-details-backend.md`)
Depends on: #1 (merged: `POST /api/pet-ads`, `PetAd` entity), #21 (merged: `PetAd.Title` exists), #2 (merged: `GET /api/pet-ads` list, `[AllowAnonymous]` on that action only)
Blocks: #20 (frontend details page) and #19, which need the regenerated `schema.d.ts` (see Decision 9).

## Goal
`GET /api/pet-ads/{id}` returns 200 with all public information of one `Active` ad for a logged-in user. An unknown id or an ad that is not `Active` gives 404 `ProblemDetails`. A request without a valid login gives 401 `ProblemDetails`. The 201 response of `POST /api/pet-ads` gets a `Location` header that points to this endpoint. No owner email, phone or owner id is returned by the details endpoint.

Out of scope (from the issue): the list endpoint (#2), photos, contact details and messaging, editing or closing ads, owner views of non-`Active` ads.

## Decisions
The user answered "go with your suggestions" for every question. Each recommended default is accepted exactly as proposed. The plan stays `Status: Draft` until a person approves it.

1. The details endpoint requires login. The class-level `[Authorize]` applies, there is no `[AllowAnonymous]` on this action. Anonymous requests and tampered cookies get 401 `ProblemDetails`. Note: the client has NOT decided this. The user chose it as a default so the rule is enforced by the server and not only by the screen (#20 redirects to `/login`). It can change later together with the client. If it changes: add `[AllowAnonymous]` to the action and replace the 401 tests with "not logged in returns 200" and "tampered cookie returns 200" tests (same pattern as the list tests). #20 would then not need its 401 handling.
2. The owner sees nothing extra. The service filters `Status == Active` for everyone, so `Draft`, `Reserved` and `Closed` ads are 404 for everyone, including the owner. 404 (not 403) so the API does not confirm that a hidden ad exists. Owner views come with the edit and close features.
3. Option B: `OwnerId` is NOT in the details response. A new record `PetAdDetailsResponse(Id, Title, Type, Species, Breed, AgeInMonths, Description, Price, Location, Status, CreatedAt)` is used, with the same field order and attributes as `PetAdResponse` minus `OwnerId`. `PetAdResponse` (returned by the create endpoint) keeps its current shape, including `OwnerId`. Reasons: consistent with the list, which leaves it out on purpose; it is not contact data but would let any logged-in user link ads of the same seller; the details page in #20 does not need it. The cost is one extra record.
4. Follows from 3: the endpoint returns `PetAdDetailsResponse`. The create endpoint keeps returning `PetAdResponse`. Its `Location` header points at the details endpoint, which returns a slightly smaller body. Noted in the PR.
5. Plain `Guid id` in the route (`[HttpGet("{id}")]`) with NO route constraint. `PetAd.Id` is a `Guid`. A value that is not a Guid (`abc`, `123`) fails model binding and `[ApiController]` returns 400 `ValidationProblemDetails` on `id`, the same shape as the list's bad parameters. Authentication runs first, so an anonymous request with a bad id is still 401. The all-zero Guid is a valid Guid and gives 404. Reason for no constraint: an `{id:guid}` constraint would give a plain empty-body 404 (no status-code-pages middleware here) and a logged-out request with a bad id would get 404 instead of 401. This behavior must be verified by a test before relying on it.
6. `Location` header on the 201 of `POST /api/pet-ads` uses a named route and `CreatedAtRoute`: `[HttpGet("{id}", Name = ...)]` on the details action, with the route name in one private constant used by both actions. Do not use `CreatedAtAction(nameof(GetByIdAsync), ...)`: MVC strips the `Async` suffix from action names by default (`SuppressAsyncSuffixInActionNames`), so that call fails at runtime with "No route matches the supplied values". The 201 status and body stay as today, and the comment "No Location header yet" is removed. 401 responses keep having no `Location`: the existing `Assert.Null(response.Headers.Location)` checks (`CreatePetAdTests` around line 690 and the auth tests) stay unchanged and must still pass. No existing test asserts a Location on the 201.
7. A logged-out request for an unknown id gets 401 first. Authorization runs before the action, so nothing about the ad's existence leaks. Covered by a test.
8. No caching. The read uses `AsNoTracking()` and projects straight into the DTO in the query (`Select`), so only the needed columns are read and the `Owner` row is never loaded. No `Cache-Control` or response caching is added: the response is behind a login and the issue does not ask for caching.
9. The OpenAPI document gets a new `get` operation on `/api/pet-ads/{id}`. Regenerating the frontend `PawsPost.Web/src/api/schema.d.ts` is a separate step after this PR is merged (same approach as #21 and #2). #19 and #20 stay blocked until then. Not part of this plan.

Also decided by the plan, not open:
- `CreatedAt` is marked as UTC with `DateTime.SpecifyKind(..., Utc)` after reading, because SQLite returns `Kind = Unspecified` (same fix as in the list).
- "Not the owner" does not apply to this read endpoint in the usual sense: there is no owner-only data and nothing is changed. The equivalent rule: any logged-in user, owner or not, can read an `Active` ad, and nobody can read a non-`Active` one. Tests cover a non-owner reading an `Active` ad (200) and the owner reading their own `Closed` ad (404).
- No new NuGet package. No migration expected: no schema change, and the primary key on `Id` serves the lookup.

## Approach
Same flow as the existing code: `Controller -> Service -> AppDbContext`, DTOs only.

- Controller: add one `[HttpGet("{id}", Name = ...)]` action `GetByIdAsync(Guid id, CancellationToken cancellationToken)` to `PetAdsController`. No `[AllowAnonymous]`, so the class-level `[Authorize]` protects it (decision 1). It calls `petAdService.GetByIdAsync(id, cancellationToken)`. If the result is `null` it returns `NotFound()` (with `[ApiController]` this gives a `ProblemDetails` body, `application/problem+json`), otherwise `Ok(ad)`. No logic in the controller. `ProducesResponseType` for 200 (`PetAdDetailsResponse`), 400 (`ValidationProblemDetails`), 401 and 404 (`ProblemDetails`).
- Service (`PetAdService.GetByIdAsync`), business rule here: `dbContext.PetAds.AsNoTracking().Where(petAd => petAd.Id == id && petAd.Status == PetAdStatus.Active).Select(...).SingleOrDefaultAsync(cancellationToken)`. `null` if none. Map with `SpecifyKind(Utc)` on `CreatedAt`. An ad that does not exist and an ad that is not `Active` look the same to the caller (decision 2).
- Create action: replace `StatusCode(201, ad)` with `CreatedAtRoute` (decision 6). Body and status unchanged.
- DTO: `PetAdDetailsResponse` as in decision 3. Comment on top: never add owner email, phone or owner id.
- Errors stay `ProblemDetails`: 400 from model binding, 401 from the cookie handler (already writes `ProblemDetails`), 404 from `NotFound()`.

## API endpoints

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| GET | /api/pet-ads/{id} | logged in (class-level `[Authorize]`, any user, owner or not) | route `id` (Guid) | `PetAdDetailsResponse` | 200, 400 (id not a Guid), 401, 404 (unknown id or not `Active`) |
| POST | /api/pet-ads | logged in (owner = caller) | `CreatePetAdRequest` | `PetAdResponse` (unchanged shape) | 201 now with `Location: /api/pet-ads/{id}`, 400, 401 (otherwise unchanged) |
| GET | /api/pet-ads | public | (unchanged) | (unchanged) | (unchanged) |

Rules for GET one:
- Only `Active` ads. Unknown and non-`Active` both give 404.
- 401 comes before 400 and 404.
- Never returns owner email, phone or `ownerId`.
- `price` is `null` for `Adoption`. `type` and `status` are strings, `createdAt` is UTC with `Z`.

## Screens and components
not applicable

## Data changes
- Entities and fields: none. No entity change, no new index.
- Migration name: none. No migration is expected. If EF reports a pending model change, stop and ask.

## Files to create or change
- `PawsPost/Features/PetAds/PetAdDetailsResponse.cs` (new): details record.
- `PawsPost/Features/PetAds/IPetAdService.cs`: add `Task<PetAdDetailsResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)`.
- `PawsPost/Features/PetAds/PetAdService.cs`: implement `GetByIdAsync`.
- `PawsPost/Features/PetAds/PetAdsController.cs`: add the `GetByIdAsync` action with the named route, change the create action to `CreatedAtRoute`, remove the "No Location header yet" comment. Do not touch the class-level `[Authorize]` or the list action.
- `PawsPost/Features/PetAds/PetAdResponse.cs`: no change.
- `PawsPost/Program.cs`, `Common/*`, `Data/*`: no change expected.
- `PawsPost.Tests/Infrastructure/PetAdTestHelpers.cs`: add `GetPetAdAsync(this HttpClient client, string id)` for `GET /api/pet-ads/{id}`. Reuse the existing `SeedOwnerAsync`, `BuildPetAd`, `SeedAdsAsync`, `CreateLoggedInClientAsync`, `AssertBadRequestForOnlyAsync`.
- `PawsPost.Tests/Features/PetAds/GetPetAdTests.cs` (new): tests below.
- `PawsPost.Tests/Features/PetAds/CreatePetAdTests.cs`: add the Location test. Existing tests unchanged.
- `PawsPost.Tests/Features/Swagger/OpenApiDocumentTests.cs`: add OpenAPI tests.
- No new NuGet packages.

## Tests to write
In `GetPetAdTests` unless noted. Names are `Method_Scenario_ExpectedResult`. Each test has its own `PawsPostFactory`, fixed `CreatedAt` seeds, no `DateTime.Now` in assertions, and asserts the content, not only the status. Seeding goes through `SeedAdsAsync` because the API cannot create non-`Active` ads.

Success:
- `GetPetAd_ActiveSaleAd_Returns200WithAllFields`: `id`, `title`, `type` ("Sale"), `species`, `breed`, `ageInMonths`, `description`, `price`, `location`, `status` ("Active"), `createdAt` match the seed.
- `GetPetAd_ActiveAdoptionAd_ReturnsNullPrice`.
- `GetPetAd_AdWithoutBreedAndLocation_ReturnsNullForThem`.
- `GetPetAd_Success_JsonUsesStringEnumsAndUtcCreatedAt`: `type` and `status` are strings, `createdAt` ends with `Z` and reads back as `DateTimeKind.Utc`.
- `GetPetAd_AdCreatedThroughPost_CanBeReadBackWithTheSameFields`: create with `POST`, read with `GET`, fields equal (except `ownerId`, which only the create body has).

Not found and status:
- `GetPetAd_UnknownId_Returns404ProblemDetails`: random Guid, `application/problem+json`, status 404.
- `GetPetAd_EmptyGuid_Returns404`.
- `GetPetAd_DraftReservedOrClosedAd_Returns404` (theory over the three statuses, `ProblemDetails` asserted).
- `GetPetAd_OtherAdsExist_ReturnsOnlyTheRequestedAd`: two active ads seeded, one read, that one returned.

Validation of the id (decision 5):
- `GetPetAd_IdNotAGuid_Returns400WithOnlyThatField` (theory: `abc`, `123`, `not-a-guid`), uses `AssertBadRequestForOnlyAsync(response, "id")`. If the real behavior differs from the plan, stop and report instead of changing the decision.

Authorization:
- `GetPetAd_NotLoggedIn_Returns401ProblemDetails`: existing active ad, no cookie, `application/problem+json`, no `Location`.
- `GetPetAd_NotLoggedInAndUnknownId_Returns401` (decision 7).
- `GetPetAd_NotLoggedInAndIdNotAGuid_Returns401` (401 before 400).
- `GetPetAd_TamperedCookie_Returns401`: cookie `pawspost.auth=tampered-value-that-is-not-valid`, same style as the existing tests.
- `GetPetAd_LoggedInUserWhoIsNotTheOwner_Returns200`: a second registered user reads another user's `Active` ad.
- `GetPetAd_OwnerReadsOwnActiveAd_Returns200`.
- `GetPetAd_OwnerReadsOwnClosedAd_Returns404` (decision 2).
- `GetPetAd_AnonymousClientAfterPublicList_StillGets401`: the same anonymous client gets 200 on `GET /api/pet-ads` and 401 on `GET /api/pet-ads/{id}`. Guards against the list's `[AllowAnonymous]` spreading.
- If decision 1 changes with the client, these 401 tests are replaced as described there.

No contact or owner data:
- `GetPetAd_Success_ResponseHasNoOwnerEmailPhoneOrOwnerId`: the response text does not contain the owner's email address, `email`, `phone` (case-insensitive) or `ownerId`.
- `GetPetAd_Success_ContainsOnlyTheDetailFields`: the set of property names is exactly the details fields.

Location header (in `CreatePetAdTests`):
- `CreatePetAd_Success_ReturnsLocationHeaderOfTheDetailsEndpoint`: `Location` is not null, its path equals `/api/pet-ads/{createdId}`, and a `GET` on that Location with the same logged-in client returns 200 with the same id. Status 201 and body assertions stay (body still has `ownerId`).
- Existing 401 tests with `Assert.Null(response.Headers.Location)` stay unchanged.

OpenAPI (in `OpenApiDocumentTests`, same helpers):
- `GetOpenApiDocument_PetAdsIdGet_HasRequiredIdPathParameter`: `get` on `/api/pet-ads/{id}` has parameter `id`, `in: path`, required, `format: uuid`.
- `GetOpenApiDocument_PetAdsIdGet_ResponseReferencesPetAdDetailsResponse`: the 200 response is a `$ref` to `PetAdDetailsResponse`, and 400, 401 and 404 responses are listed.
- `GetOpenApiDocument_PetAdDetailsResponseSchema_HasExpectedFieldsAndNoOwnerId`: properties are the detail fields, no `ownerId`, non-nullable fields are required.
- Existing OpenAPI tests stay unchanged and must still pass, including the `PetAdResponse` schema.

Not tested: that EF can query SQLite, ASP.NET's cookie handling itself.

## Steps
1. Create the branch `feature/18-get-pet-ad-details` from `main`.
2. Add `PetAdDetailsResponse`, the `IPetAdService.GetByIdAsync` member and the `PetAdService` implementation. Commit: `feat: add pet ad details service (#18)`.
3. Add the `GetByIdAsync` action with the named route to `PetAdsController` (protected, no `[AllowAnonymous]`). Confirm by hand that the class still has `[Authorize]`. Commit: `feat: add get pet ad endpoint (#18)`.
4. Change the create action to `CreatedAtRoute`, remove the old comment. Run the existing tests to confirm nothing else breaks. Commit: `feat: add Location header to pet ad creation (#18)`.
5. Add the helper and the success, not found and non-`Active` tests. Commit: `test: cover getting one pet ad (#18)`.
6. Add the id validation, authorization and no-contact-data tests. Commit: `test: cover details validation and access (#18)`.
7. Add the Location test and the OpenAPI tests. Commit: `test: cover Location header and details endpoint in OpenAPI (#18)`.
8. Run `dotnet build` (no warnings), `dotnet test`, `dotnet format --verify-no-changes`. Open the PR with `Closes #18`. In the PR description note: (a) the decisions, in particular that login is required by default and the client has not decided it, (b) the create 201 body keeps `ownerId` while the details body does not, (c) `schema.d.ts` must be regenerated in a separate frontend step and #19 and #20 wait for it.
9. After the merge (separate step, not part of this PR): regenerate `PawsPost.Web/src/api/schema.d.ts`, so #19 and #20 can start.

## Risks and open questions
Open questions: none. All were answered, see "Decisions". One decision (1, login required) is a default the client has not confirmed, so it may change later.

Stop-and-ask rules that stay in force:
- If the invalid id does not give the 400 described in decision 5, or the `Location` header cannot be generated with the named route, stop and report.
- If EF asks for a migration, stop and ask. No schema change is part of this issue.
- If a fix would need a new NuGet package, stop and ask.

Risks:
- Async suffix: `CreatedAtAction(nameof(GetByIdAsync))` throws at runtime (decision 6). Mitigated by the named route and the Location test that also follows the link.
- Wrong `[AllowAnonymous]` scope: the list keeps it on its action only, the new action has none. Mitigated by the 401 tests and the list-versus-details test.
- If the client later wants public details, the 401 tests must be replaced (decision 1) and the 401 handling in #20 becomes unused.
- Option B leaves two similar DTOs. Accepted to keep the create contract (#1) unchanged.
- Dependency check: user accounts and login (#3), `PetAd` with `Title` (#21) and the list (#2) already exist in the code, so the backend is not blocked. The frontend (#19, #20) is blocked until `schema.d.ts` is regenerated.
