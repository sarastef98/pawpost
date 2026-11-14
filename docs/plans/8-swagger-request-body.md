# Plan: Swagger UI shows null as the request body for POST /api/pet-ads (#8)

Issue: https://github.com/sarastef98/pawpost/issues/8

Status: Approved

## Goal
In Swagger UI, `POST /api/pet-ads` shows the real request body (`type`, `species`, `breed`, `ageInMonths`, `description`, `price`, `location`) instead of `null`. The API behavior does not change.

## Approach
`CreateAsync` takes `CreatePetAdRequest?`. The `?` is deliberate: when the JSON has a bad value (for example an unknown `type`), the user gets only that error and not an extra "request is required" error. The OpenAPI generator reads the `?` as "the body may be null" and writes the body schema as `oneOf: [ {type: null}, {$ref: CreatePetAdRequest} ]`. Swagger UI builds its example from the first option, so it shows `null`.

We keep the controller as it is and fix the generated document instead:
- Add a small **OpenAPI transformer** (built into `Microsoft.AspNetCore.OpenApi`, already in the project, so no new package). It runs when the document is generated. For every request body whose schema is `oneOf: [null, X]`, it keeps only `X`.
- It also marks the request body as `required: true`. That is accurate, because a missing body is rejected with 400 by the controller.
- The transformer is registered in `Program.cs` where `AddOpenApi()` is called. It applies to every endpoint, so future endpoints with a nullable body get the same fix.
- The exact transformer type (schema vs. operation transformer) is confirmed while implementing. The choice depends on what the installed `Microsoft.AspNetCore.OpenApi` / `Microsoft.OpenApi` versions expose. If neither can do it cleanly, we stop and report instead of working around it.

Option rejected: making the parameter non-nullable. It would bring back the extra "request is required" error on bad JSON and change existing tests.

## API endpoints
No new or changed endpoints. Only the generated OpenAPI description of `POST /api/pet-ads` changes.

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| POST | /api/pet-ads | logged in | CreatePetAdRequest | PetAdResponse | 201, 400, 401 (unchanged) |

## Data changes
- Entities and fields: none
- Migration name: none

## Files to create or change
- `PawsPost/Common/NonNullableRequestBodyTransformer.cs` (new): the OpenAPI transformer.
- `PawsPost/Program.cs`: register the transformer in `AddOpenApi(...)`.
- `PawsPost.Tests/Features/Swagger/OpenApiDocumentTests.cs` (new): tests below.

## Tests to write
This changes no data endpoint, so the usual success / validation / authorization trio is replaced by checks on the generated document (same approach as #6):
- `GetOpenApiDocument_PetAdsPost_BodyReferencesCreatePetAdRequest`: the body schema of `POST /api/pet-ads` is a `$ref` to `CreatePetAdRequest`, with no `null` option.
- `GetOpenApiDocument_PetAdsPost_BodyIsRequired`: `requestBody.required` is `true`.
- `GetOpenApiDocument_CreatePetAdRequest_ListsExpectedFields`: the schema lists `type`, `species`, `ageInMonths`, `description`.
- Existing pet-ad tests stay unchanged and must still pass, which proves the 400 / 401 behavior did not change.

## Steps
1. Create the branch `feature/8-swagger-request-body` from `main`.
2. Add the transformer and register it (`fix: show real request body in Swagger for pet ads (#8)`).
3. Add the tests (`test: cover OpenAPI request body for pet ads (#8)`).
4. Run `dotnet build`, `dotnet test` and `dotnet format --verify-no-changes`, then open the PR with `Closes #8`.
5. Check once by hand that `/swagger` now shows the example body on `POST /api/pet-ads`.

## Risks and open questions
- The OpenAPI document is 3.1, and the `Microsoft.OpenApi` types differ between versions. The implementation may need a small adjustment; see the approach note about stopping if it cannot be done cleanly.
- Swagger UI behavior in the browser cannot be covered by automated tests, so step 5 is a manual check.
- No open questions for the client.
