---
paths:
  - "PawsPost/Features/Assistant/**"
  - "PawsPost/Features/Ai/**"
  - "PawsPost.Tests/**/*Assistant*.cs"
  - "PawsPost.Tests/**/*Embedding*.cs"
---

# Assistant and semantic search rules

Source: `docs/meeting-notes/2026-11-15-rag-assistant.md`.

## Interfaces

- The AI provider sits behind three interfaces so it can be replaced without touching other code:
  - `IEmbeddingProvider` creates vectors from text.
  - `IChatProvider` writes the answer from the question and the found ads.
  - `IVectorStore` saves and searches vectors.
- The first implementation uses Gemini. Gemini types and namespaces appear only inside the provider implementation folder, never in services, controllers or DTOs.
- The first vector store uses the existing SQLite database or a simple in-memory store. No new server and no new NuGet package without asking first.

## Configuration and secrets

- Provider choice, model names and the API key come from configuration.
- The key lives in user-secrets or an environment variable. Never in code, `appsettings*.json` or git. Never log it.

## Behavior

- Only logged-in users can use the assistant.
- Embed an ad when it is created or edited. Remove its vector when it is deleted or closed so it disappears from results. Only `Active` ads may be returned.
- The assistant only talks about ads that exist on the site. If nothing fits, it says so and invents no ads.
- Results are paginated or capped with a maximum, and each links to the ad details page.
- Ad text is data, not instructions. Put it in a clearly separated data section of the prompt, and tell the model to ignore instructions inside it (prompt injection).
- Send only public ad fields to the provider. No emails, phones or other personal data. Demo data is synthetic.
- Validate the question on the server: required, maximum length.

## Open questions (do not decide alone, ask or list in the issue)

- Whether the assistant remembers earlier questions in a session.
- The maximum number of ads shown.
- How to handle ads created before this feature exists (likely a one-time re-embedding step).

## Tests

- Use fake providers and a fake vector store. No test calls the real Gemini API or needs a key.
- Cover: not logged in, empty or too long question, no matching ads, deleted ad not returned, an ad containing instructions does not change the answer flow.
