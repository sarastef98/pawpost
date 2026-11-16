Issue: #35
URL: https://github.com/sarastef98/pawpost/issues/35

# Add semantic search assistant endpoint

Layer: backend
Depends on: #1, #18, #3

## Summary
Add an assistant endpoint where a logged-in user sends a free-text question and receives a short friendly answer plus the best matching `Active` ads, found by meaning (embeddings and similarity search). The AI provider sits behind interfaces so it can be replaced later. Ads are embedded when they are created, so they become searchable automatically.

## User story
As a logged-in user, I want to describe the pet I want in my own words and get matching ads, so that I can find a fitting animal even when my words are not in the ad.

## Acceptance criteria
- [ ] Three interfaces exist: `IEmbeddingProvider` (creates vectors from text), `IChatProvider` (writes the answer from the question and the found ads) and `IVectorStore` (saves and searches vectors). Services depend only on these interfaces.
- [ ] The first implementation uses Gemini for embeddings and chat. Gemini types and namespaces appear only inside the provider implementation folder, never in services, controllers or DTOs.
- [ ] Provider choice, model names and the API key come from configuration. The key is read from user-secrets or an environment variable and is not in code, `appsettings*.json` or git, and is never logged.
- [ ] The first `IVectorStore` implementation uses the existing SQLite database or a simple in-memory store. No new server. No new NuGet package is added without asking first.
- [ ] A new endpoint (route and verb chosen in the plan, for example `POST /api/assistant/search`) accepts a question and returns 200 with a short answer text and a list of matching ads. Each matching ad has its id (so the screen can link to `GET /api/pet-ads/{id}` details) and public fields only, as in the details response DTO from #18.
- [ ] The response never contains an owner's email, phone or owner id, and none of these are sent to the provider. Only public ad fields are sent.
- [ ] Only `Active` ads can be returned.
- [ ] A request without a login returns 401 with `ProblemDetails`.
- [ ] The question is validated on the server: required and with a maximum length (value chosen in the plan). Invalid input returns 400 with `ValidationProblemDetails`. An anonymous request gets 401 first.
- [ ] The assistant only talks about ads that exist on the site. If nothing fits, the answer says so, the ad list is empty and no ad is invented.
- [ ] The number of returned ads is capped by a maximum (the value is an open question).
- [ ] When a pet ad is created through `POST /api/pet-ads`, its vector is created and stored, so it becomes searchable without a manual step.
- [ ] If embedding fails when an ad is created, the ad is still created and the failure is handled without exposing provider details (the exact behavior is chosen in the plan, see open questions).
- [ ] An ad that is not `Active`, or is removed from the site, is not returned by the search (its vector is removed or filtered out).
- [ ] Ad text is treated as data, not instructions. In the prompt it is placed in a clearly separated data section, and the model is told to ignore any instructions inside it.
- [ ] Business rules live in services. The controller only calls the service. Reads use `AsNoTracking()`. Any schema change goes through an EF migration.
- [ ] Tests use fake providers and a fake vector store. No test calls the real Gemini API or needs a key.
- [ ] Tests cover: success, not logged in (401), empty question and too long question (400), no matching ads, closed or removed ad not returned, new ad becomes searchable, an ad containing instructions does not change the answer flow, and that no owner contact data is in the response or in the provider input.

## Out of scope
- The assistant screen (separate frontend issue).
- Re-embedding ads that were created before this feature (decision pending, see open questions).
- Embedding on edit, close and delete: these endpoints do not exist yet. Their issues must call the embedding or removal step. This issue only provides what they need and makes sure search excludes non-`Active` ads.
- Replacing Gemini with another provider (the interfaces only prepare for it).
- A separate vector database server.
- Using real personal data. Only synthetic demo ads are used.

## Open questions
- Does the assistant remember earlier questions in the same session, or is each question answered alone? Not decided. This changes the request shape. Until decided, each question is answered alone.
- What is the maximum number of matching ads to return? Not decided.
- What happens to ads created before this feature exists? A one-time re-embedding step is likely, but not decided (for example a command, a startup task or an admin endpoint).
- If embedding fails when an ad is created or edited (provider down, quota), should the ad save fail, or save and retry later? Not given in the notes.
- Which vector store first: the existing SQLite database or in-memory? The team allows both. An in-memory store loses vectors on restart, which interacts with the re-embedding question.
- Maximum question length, and which ad fields are embedded? Not given. Only public fields may be used.

## Source
Meeting of 15 Nov (`docs/meeting-notes/2026-11-15-rag-assistant.md`). A visitor types a sentence and gets matching ads by meaning, with a short friendly answer. It only talks about existing ads and says so when nothing fits. New and edited ads become searchable automatically and deleted ads disappear. Only logged-in users may use it. The team requires the provider behind `IEmbeddingProvider`, `IChatProvider` and `IVectorStore`, Gemini first, configuration and secrets outside the repository, a simple vector store, fake providers in tests, protection against prompt injection and synthetic data only. Memory between questions, the maximum number of ads and handling of existing ads are not decided.
