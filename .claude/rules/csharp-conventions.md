---
paths:
  - "**/*.cs"
---

# C# conventions

- Nullable reference types stay enabled. Fix warnings, don't suppress them.
- Controllers stay thin: they receive the request, call a service, return the result. Business logic lives in services.
- Never return EF Core entities from an API. Use separate request and response DTOs.
- Use async all the way down. Async methods end in `Async` and accept a `CancellationToken`.
- Use constructor injection. Services have an interface (`IPetAdService`).
- Naming: `PascalCase` for public members, `_camelCase` for private fields, `I` prefix for interfaces.
- Validate input and return `ProblemDetails` for errors, with proper status codes (400, 404, 409).
- No secrets or connection strings in code. Use configuration.
- Code is organized by feature under `PawsPost/Features/<Feature>/`: controller, service, interface, DTOs and validator together.
