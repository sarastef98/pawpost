# Plan: Enable TypeScript strict mode in the frontend (#16)

Status: Approved
Layer: frontend

## Goal
The TypeScript compiler runs with `"strict": true` for the frontend app code and for the Node-side config and scripts. `npx tsc -b`, `npm run build`, lint and all existing tests pass. Nothing changes for the user.

## Approach
Add `"strict": true` to `PawsPost.Web/tsconfig.app.json` and `PawsPost.Web/tsconfig.node.json`. Measurements show the code already compiles cleanly under strict, so no source fix is expected. If a fix does turn up (for example after `main` moves), it uses correct types or real null handling. No `any`, no `@ts-ignore`, `@ts-expect-error`, `@ts-nocheck`, no new non-null assertion (`!`), no cast used only to hide an error. Only `strict` is added: no other flags and no new npm packages.

`strict` turns on `strictNullChecks`, `noImplicitAny`, `strictFunctionTypes`, `strictBindCallApply`, `strictPropertyInitialization`, `noImplicitThis`, `useUnknownInCatchVariables` and `alwaysStrict`.

## Decisions
Decided by the user ("go with your suggestions"):
1. Set `"strict": true` in `tsconfig.node.json` too (covers `vite.config.ts` and `scripts`), as well as in `tsconfig.app.json`. It has 0 errors, so it is a one-line change.
2. Do NOT add `noUncheckedIndexedAccess` or any other compiler flag. Only `strict`.
3. Leave the existing `document.getElementById('root')!` in `src/main.tsx` untouched. It is not a type error under strict and was not added by this issue. Replacing it with a real check would be a separate issue.

Recorded as moot because of the measurements:
- Size of the job unknown: answered, 0 errors in both configs, so there is no splitting question.
- Test files compile under strict (they are inside `tsconfig.app.json`). The `client.GET as unknown as Mock` casts compile and are not `any`. They are left as they are.
- Generated `src/api/schema.d.ts`: 0 errors, and `skipLibCheck: true` skips `.d.ts` files anyway. It is never edited (rules forbid it). If a later regeneration causes an error, fix the code that uses it or raise a separate issue. Do not hide it with a tsconfig `exclude`.
- Ordering after #14 (PR #15): satisfied, it is merged (b413ad4). No blocker.

## Size of the job: measured
Measured read-only from `PawsPost.Web` on `main` at b413ad4 (after #14 merged), with `--noEmit`. No tracked file was changed.

| Config | Command | Files checked | Errors |
|---|---|---|---|
| tsconfig.app.json | `npx tsc -p tsconfig.app.json --noEmit --strict` | 39 under `src` | 0 |
| tsconfig.node.json | `npx tsc -p tsconfig.node.json --noEmit --strict` | 3 | 0 |

- `--showConfig` confirmed `strict: true` was active (with `skipLibCheck: true` and `noEmit: true`).
- Sanity probe: a temporary file with `const x: string = null` and an untyped parameter gave 4 errors under the same command, so the check really catches strict errors. The probe file was removed.
- Result by kind: 0 implicit any, 0 possibly null or undefined, 0 other. No file needs a fix. Test files and `src/api/schema.d.ts` also give no errors.
- This is a snapshot of `main` at b413ad4. If `main` moves before the branch is made, re-run both commands to confirm.

So the job is only the config change plus proving nothing changed.

## API endpoints
No new endpoints and none changed. The frontend already uses these existing endpoints (types come from `PawsPost.Web/src/api/schema.d.ts`, which is not edited):

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| GET | /api/auth/me | logged in | none | UserResponse | 200, 401 |
| POST | /api/auth/login | public | email, password | UserResponse | 200, 400, 401 |
| POST | /api/auth/register | public | email, password | none used | 2xx, 400, 409 |
| POST | /api/auth/logout | logged in | none | none | 2xx, 401 |

(Taken from how `AuthContext.tsx` calls them.)

## Screens and components
Not a screen change. No route, component, state or form behavior changes.

- Route: none changed
- Components: none added or changed
- States: unchanged
- Forms and validation: unchanged

## Data changes
none

## Files to create or change
- `PawsPost.Web/tsconfig.app.json`: add `"strict": true` in the Linting section. Nothing else.
- `PawsPost.Web/tsconfig.node.json`: add `"strict": true` in the Linting section. Nothing else.
- No source, test or script file changes are expected (0 errors measured).
- Not changed: `PawsPost.Web/src/api/schema.d.ts` (generated, never edited), `tsconfig.json`, `package.json`, `src/main.tsx`.

## Tests to write
No new tests. This is a behavior-preserving config change with no code change, and the existing 18 test files already cover the behavior. New tests would only be needed if a fix changed behavior, which is out of scope.

What proves nothing changed:
- Run `npm test` on `main` before the change and record the pass count. After the change `npm test` must give the same count, all green, with no test edited.
- `npx tsc -b` passes with no errors (this builds both configs).
- `npm run build`, `npm run lint` and `npm run format:check` pass.
- Diff scan: the full diff should be only the two `"strict": true` lines. Check `git diff main -- PawsPost.Web` for added `any`, `@ts-ignore`, `@ts-expect-error`, `@ts-nocheck`, ` as ` and `!`, and find none.

## Steps
1. Baseline: on `main` run `npm test` and record the pass count. Create branch `feature/16-typescript-strict`. If `main` has moved since b413ad4, re-run the two `tsc --strict` measurements first.
2. Add `"strict": true` to `tsconfig.app.json` and `tsconfig.node.json`. Commit: `chore: enable TypeScript strict mode (#16)`.
3. Prove nothing changed: `npx tsc -b`, `npm run build`, `npm run lint`, `npm run format:check`, `npm test` (same pass count as baseline), and the diff scan for added `any`, `@ts-`, `as` and `!`.
4. If step 3 unexpectedly shows an error (for example because `main` moved), fix it with correct types or real null handling in a separate commit, or stop and report. Never silence it.

## Risks and open questions
Open questions: none. All earlier questions are answered in "Decisions" or recorded there as moot.

Risks:
- The measurement is a snapshot of `main` at b413ad4. If `main` moves before the branch is made, re-run the measurement (step 1).
- Future code added on other branches may fail under strict after this merges. That is the intended effect, and those authors fix it with correct types.
