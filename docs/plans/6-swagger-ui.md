# Plan: Add Swagger UI to test the API in the browser (#6)

Issue: https://github.com/sarastef98/pawpost/issues/6

Status: Approved

## Goal
In Development, opening `/swagger` shows a Swagger UI page listing every endpoint (accounts and pet ads). The developer can call them from the page, including endpoints that need a login. In any other environment the page does not exist.

## Approach
The app already generates an OpenAPI document (`AddOpenApi()` and `MapOpenApi()`, served at `/openapi/v1.json`). That document is only a machine-readable description of the API. Swagger UI is the web page that reads it and lets you click "Try it out". So we only add the page. We do not add a second document generator.

- New NuGet package: **`Swashbuckle.AspNetCore.SwaggerUI`**. It contains only the UI files and the `UseSwaggerUI` middleware. It does not generate the document itself, so there is no conflict with `Microsoft.AspNetCore.OpenApi`. This package needs the developer's approval (rule: no new package without asking). Swagger UI was chosen over Scalar. The exact version is the latest stable one that supports .NET 10, confirmed when it is added.
- `Program.cs`: inside the existing `if (app.Environment.IsDevelopment())` block, next to `MapOpenApi()`, add `app.UseSwaggerUI(...)` pointing at `/openapi/v1.json`, with the route prefix `swagger`. Being in that block is what keeps it out of Production.
- Login from the page: the cookie is set by `POST /api/auth/login` and the browser sends it automatically on later calls from the same page (same origin). No Swagger security scheme is needed for that. The cookie is `Secure`, so the page must be opened over `https://localhost:7058/swagger` (the `https` launch profile). Over plain `http` the browser will not store the cookie.
- No endpoint, DTO or authentication behavior changes.

## API endpoints
No new or changed API endpoints. One development-only page is added:

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| GET | /swagger | public, Development only | none | HTML page | 200 in Development, 404 otherwise |

## Data changes
- Entities and fields: none
- Migration name: none

## Files to create or change
- `PawsPost/PawsPost.csproj`: add the `Swashbuckle.AspNetCore.SwaggerUI` package reference.
- `PawsPost/Program.cs`: add `UseSwaggerUI` inside the existing Development-only block.
- `PawsPost.Tests/Features/Swagger/SwaggerUiTests.cs`: new tests (see below).

## Tests to write
This adds no data-changing endpoint, so the usual success / validation / authorization trio is replaced by environment checks:
- `GetSwaggerUi_InDevelopment_ReturnsHtmlPage`: 200 and an HTML content type.
- `GetSwaggerUi_InProduction_ReturnsNotFound`: the factory is started with the `Production` environment and `/swagger/index.html` answers 404.
- `GetOpenApiDocument_InDevelopment_ListsAuthPaths`: the document at `/openapi/v1.json` contains `/api/auth/register` and `/api/auth/login`, which proves the page has something to show. (Pet ad endpoints live on `feature/1-create-pet-ad`, not yet in `main`, so they are not asserted here. They appear on the page automatically once #1 is merged.)

## Steps
1. After the plan is approved, add the package reference (`chore: add Swashbuckle SwaggerUI package (#6)`).
2. Enable Swagger UI in Development in `Program.cs` (`feat: add Swagger UI in Development (#6)`).
3. Add the tests (`test: cover Swagger UI availability (#6)`).
4. Run `dotnet build`, `dotnet test` and `dotnet format --verify-no-changes`, then open the PR with `Closes #6`.

## Risks and open questions
- **Approval needed:** the new package `Swashbuckle.AspNetCore.SwaggerUI`.
- Swashbuckle 10 and `Microsoft.AspNetCore.OpenApi` 10 should work side by side because only the UI package is used. If the build shows a version conflict (for example with `Microsoft.OpenApi`), we stop and report it instead of working around it.
- The `Secure` cookie means login from the page only works over `https`. This is expected and is not changed here.
- Check manually, once, that "Try it out" on `POST /api/pet-ads` works after logging in. Automated tests cannot cover the browser cookie behavior.
