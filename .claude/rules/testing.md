---
paths:
  - "PawsPost.Tests/**/*.cs"
---

# Testing rules

Details on top of the testing section in CLAUDE.md.

- Each test class gets its own isolated database. Tests never share data and never depend on order.
- Shared helpers (creating a user, an authenticated client, test data builders) live in one helper folder in the test project. Do not copy them into test classes.
- Assert the behavior, not only the status code: check the important fields of the response, and for writes check the stored data.
- Make tests deterministic: no `Thread.Sleep`, no random data without a fixed seed, no `DateTime.Now` in assertions. Use fixed values.
- One reason to fail per test. If a test name needs "and", split it.
- Test names read like a sentence: `CreatePetAd_PriceMissingOnSale_ReturnsBadRequest`.
- Never delete, skip or weaken a test to make a build pass. A failing test means the code or the test is wrong, so find out which.
- Do not test the framework (for example that EF can save an entity). Test our rules.
