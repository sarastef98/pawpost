---
paths:
  - "PawsPost.Web/**/*.test.ts"
  - "PawsPost.Web/**/*.test.tsx"
---

# Frontend testing rules

- Test what the user sees and does. Find elements by role, label or visible text, not by class names or test ids.
- Use `user-event` for typing and clicking. Wait for async changes with `findBy` queries, never with fixed delays.
- Mock only the API client, at the boundary. Never mock the component under test or React itself.
- One behavior per test. Names read like a sentence: `shows an error message when the API is unreachable`.
- No snapshot tests for logic. A snapshot proves nothing about behavior.
- Never delete, skip or weaken a test to make a build pass.
