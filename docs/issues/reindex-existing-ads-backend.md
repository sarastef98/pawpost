Issue: #40
URL: https://github.com/sarastef98/pawpost/issues/40

# Add a re-embedding step for ads without a vector

Layer: backend
Depends on: #35

## Summary
Ads created before #35 have no vector, and an ad whose vector write failed stays without one, so the assistant cannot find them. Add a step that creates vectors for `Active` ads that have none.

## User story
As a logged-in user, I want the assistant to find all active ads, so that older ads and ads with a failed index are not hidden from my search.

## Acceptance criteria
- [ ] A re-embedding step finds `Active` ads that have no stored vector and creates and stores their vectors through `IPetAdSearchIndexer` (and so `IEmbeddingProvider` and `IVectorStore`). Services do not use Gemini types.
- [ ] Ads that are not `Active` are not indexed by this step.
- [ ] Ads that already have a vector are not sent to the provider again.
- [ ] Only public ad fields are sent to the provider, as in #35. No owner email, phone or owner id.
- [ ] Running the step twice gives the same result as running it once (no duplicate vectors).
- [ ] A failure for one ad (provider down, quota) does not stop the step for the other ads, is reported without provider details, and the ad stays without a vector so a later run can retry it. Only the ad id and the exception type are logged.
- [ ] The step reports a short result (for example how many ads were indexed and how many failed), without personal data.
- [ ] Reads that do not change data use `AsNoTracking()`. Any schema change goes through an EF migration.
- [ ] Business rules live in a service. The way the step is started (see open questions) only calls that service.
- [ ] Tests use fake providers and a fake vector store, and never call the real Gemini API. They cover: an `Active` ad without a vector gets one and becomes searchable, an ad with a vector is not embedded again, a non-`Active` ad is skipped, a failure for one ad does not stop the others, and a second run changes nothing.
- [ ] If the step is an endpoint, tests also cover not logged in (401), logged in but not allowed (403), as CLAUDE.md requires for every endpoint.

## Out of scope
- Embedding on edit, close and delete (their own issues must call `IndexAsync` and `RemoveAsync`).
- Indexing ads that are not `Active`.
- Changing the vector store or the AI provider.
- Rate limiting of the assistant endpoint.

## Open questions
- Which mechanism: a command, a startup task or an admin endpoint? Not decided. An admin endpoint needs a decision about who is an admin, and no such role exists in the notes.
- Should the step also re-embed ads whose vector comes from an old embedding model? Not decided. The vector store keeps the model name, so this is possible, but it was not requested.
- How is the Gemini quota respected (batch size, pause between calls, maximum number of ads per run, stop on a quota error)? Not decided.
- If the in-memory vector store were ever used, vectors would be lost on restart. Should the step then run at every start? Not decided. This depends on which vector store is in use.

## Source
Follow-up from the code review of PR #37 (issue #35, `docs/reviews/35-round-1.md` and `35-round-2.md`). Ads created before #35 are not searchable, and an ad whose vector write failed stays unsearchable. The reviewers named a re-index step as the better recovery. The mechanism, the old-model case and the quota handling were not decided. Only `Active` ads are indexed.
