# Meeting notes: Smart assistant with semantic search

Date: 2026-11-15
Topic: Let visitors describe the pet they want in their own words and get matching ads.

## What the client wants

- A visitor can type a sentence like "a calm dog that is good with small children" and see the ads that fit best, even if those exact words are not in the ad.
- The assistant answers in a short friendly text and lists the matching ads with links to their details pages.
- It only talks about ads that exist on the site. If nothing fits, it says so and does not invent ads.
- New and edited ads become searchable automatically. Deleted ads disappear from the results.
- Only logged-in users can use the assistant (same rule as the ad details page).

## Technical constraints (from the team)

- The search finds ads by meaning (semantic search), using embeddings stored in a vector store.
- Backend work: embed each ad when it is created or edited, store the vector, and search by similarity.
- The AI provider must sit behind interfaces so it can be replaced later without changing the rest of the code:
  - `IEmbeddingProvider` creates vectors from text.
  - `IChatProvider` writes the answer from the question and the found ads.
  - `IVectorStore` saves and searches vectors.
- The first implementation uses Gemini for embeddings and chat. No Gemini types may appear outside the provider implementation folder.
- Provider choice, model names and the API key come from configuration. The key goes in user secrets or an environment variable, never in the repository.
- The first vector store implementation uses the existing SQLite database or a simple in-memory store. This is a demo, so no new server is needed.
- Tests use fake providers. No test may call the real Gemini API or need a key.
- Ad text sent to the AI is treated as data. Instructions inside an ad must not change the assistant's behavior (prompt injection).
- Use only synthetic demo ads. No real personal data is sent to the provider.

## Layers

- Backend: yes (embedding on ad create/edit/delete, search endpoint, provider interfaces).
- Frontend: yes (a simple assistant box with the question, the answer and the matching ads).
- Both layers should be separate issues. The frontend depends on the backend.

## Not decided yet

- Whether the assistant remembers earlier questions in the same session, or answers each question alone.
- How many matching ads to show at most.
- What to do with ads that were created before this feature exists (a one-time re-embedding step is likely).
