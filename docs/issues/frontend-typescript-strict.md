Issue: #16
URL: https://github.com/sarastef98/pawpost/issues/16

# Enable TypeScript strict mode in the frontend

Layer: frontend
Depends on: none

Note: best done after #14 (PR #15) merges, so the auth code is fixed once. This is not a hard dependency.

## Summary
`.claude/rules/frontend.md` requires "TypeScript strict. No `any`. Fix type errors, don't silence them.", but `PawsPost.Web/tsconfig.app.json` does not set `"strict": true`. Turn it on and fix the type errors it reveals.

## User story
As a developer, I want the compiler to run in strict mode, so that null and implicit-any mistakes are caught at build time and not in the browser.

## Acceptance criteria
- [ ] `PawsPost.Web/tsconfig.app.json` sets `"strict": true`.
- [ ] `npx tsc -b` passes with no errors.
- [ ] No `any` is added (explicit or implicit).
- [ ] No `@ts-ignore`, `@ts-expect-error`, `@ts-nocheck`, or non-null assertion (`!`) is added to silence errors. Errors are fixed by correct types or real null handling.
- [ ] Existing tests still pass.
- [ ] `npm run build` and lint still pass.
- [ ] No user-visible behavior change.

## Out of scope
- Any feature change.
- Refactors beyond what is needed to fix type errors.
- Extra compiler flags beyond `strict` (for example `noUncheckedIndexedAccess`).

## Open questions
- Should `"strict": true` also be set in `PawsPost.Web/tsconfig.node.json` (covers `vite.config.ts` and `scripts`)? Recommendation: yes if it needs no or trivial fixes, otherwise leave it for a follow-up.
- Should `noUncheckedIndexedAccess` be added? Recommendation: no, keep to `strict` only.

## Source
Not a client request, and no client requirement is involved. This is a developer-side technical chore found in code review of PR #15 (issue #14).

Current state, confirmed from the config files: `tsconfig.app.json` only sets `noUnusedLocals`, `noUnusedParameters`, `erasableSyntaxOnly` and `noFallthroughCasesInSwitch`. `tsconfig.node.json` has the same four flags, and `tsconfig.json` only holds project references. This comes from the original scaffold commit c259cec. Without `strict`, `strictNullChecks` and `noImplicitAny` are off, so the compiler does not catch null mistakes, for example `User | null` in the auth code from #14, or `data | undefined` results from the typed API client.
