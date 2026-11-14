Issue: #25
URL: https://github.com/sarastef98/pawpost/issues/25

# Regenerate the frontend API types from the current backend

Layer: frontend
Depends on: #18

## Summary
`PawsPost.Web/src/api/schema.d.ts` is out of date: the backend API changed after it was last generated. Regenerate it with `npm run gen:api` so the list and details screens can use the real API types.

#18 must be merged first (PR #24, which closes #18, is reviewed and waiting for the product owner to merge). #21 (required `title`) and #2 (`GET /api/pet-ads` list) are already merged. This issue blocks #19 (home page list) and #20 (details page).

## User story
As a frontend developer, I want `schema.d.ts` to match the backend's OpenAPI document, so that the typed client knows every endpoint and the compiler catches API mismatches.

## Acceptance criteria
- [ ] The API is run from the merged `main` (after PR #24 is merged), and `npm run gen:api` is run from `PawsPost.Web`. `schema.d.ts` is not edited by hand (see `.claude/rules/frontend.md`).
- [ ] The diff of `schema.d.ts` contains only what the generator produces. No manual changes.
- [ ] `schema.d.ts` contains `GET /api/pet-ads` with query parameters `page`, `pageSize`, `species` and `type`, and the response schema `PetAdListResponse` (`items`, `totalCount`, `page`, `pageSize`) whose items are `PetAdListItemResponse`.
- [ ] `schema.d.ts` contains `GET /api/pet-ads/{id}` returning `PetAdDetailsResponse` (which has no `ownerId`).
- [ ] `title` is present and required in `CreatePetAdRequest` and `PetAdResponse`.
- [ ] `npx tsc -b` passes (TypeScript strict is on).
- [ ] `npm test` passes.
- [ ] `npm run lint` passes.
- [ ] `npm run format:check` passes. On Windows, line endings can make it fail locally, so check with `npx prettier --check src --end-of-line auto`.
- [ ] `npm run build` passes.
- [ ] If any frontend code stops compiling because of the new required `title`, it is fixed with the smallest possible change. Today nothing outside `schema.d.ts` calls `/api/pet-ads` (only `src/api/client.ts` imports `paths` and `src/features/auth/useAuth.ts` uses `UserResponse`), so no such fix is expected.
- [ ] No new feature, screen or component is added.

## Out of scope
- Building the list screen (#19) or the details screen (#20).
- Any backend change.
- Changing the generator scripts.
- Declaring the Location header of the create endpoint in the OpenAPI document (it is set at runtime but not in the generated types; the frontend can read it from the response if it ever needs it)

## Open questions
- `npm run gen:api` needs the API running. `scripts/gen-api.ts` downloads `/openapi/v1.json` from the API and writes `src/api/schema.d.ts` with `openapi-typescript`. It does not start the API. The URL is read by `scripts/api-url.ts` from the first `applicationUrl` of the `http` profile in `PawsPost/Properties/launchSettings.json`, so there is no URL to pass in. If the API is not running, the script prints "Is the API running?" and fails. Who starts the API, and from which checkout? Decided default: the developer doing this issue starts it from an up-to-date `main` with `dotnet run --project ../PawsPost --launch-profile http`, runs `npm run gen:api` in another terminal, then stops the API. Start the API only for this step and stop it afterwards, because a running API locks the backend build output.

## Source
Not from client meeting notes. This is a developer-side technical chore that follows from backend changes (#21, #2 and #18) that are merged or about to merge. No client requirement is added.
