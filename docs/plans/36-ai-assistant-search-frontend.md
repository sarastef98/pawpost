# Plan: Add assistant box for finding ads in plain words (#36)

Status: Approved
Layer: frontend

Issue: https://github.com/sarastef98/pawpost/issues/36 (local copy: `docs/issues/ai-assistant-search-frontend.md`)
Depends on: #35 (assistant endpoint, merged on `main`), #14 (`useAuth`, `/login`, header), #20 (details page `/pet-ads/:id`). All three exist in the code. The only gap: `PawsPost.Web/src/api/schema.d.ts` does not contain the assistant endpoint yet (checked: no `assistant` match in the file), so it must be regenerated first (step 2).
Rules that apply: `CLAUDE.md`, `.claude/rules/frontend.md`, `.claude/rules/design.md`, `.claude/rules/frontend-testing.md`, `docs/design.md`.

## Goal
A logged-in user opens the assistant page, types a question in their own words, presses the submit button, and sees the assistant's short answer as plain text plus the matching ads as cards, each linking to `/pet-ads/:id`. While waiting, a Spinner is shown and the button is disabled. If nothing fits, the answer and a friendly empty state are shown. An empty question is not sent. A too long question is stopped on the client, and a server 400 message is shown next to the field. A logged-out visitor is sent to `/login` without any request. A 401 also goes to `/login`. Other failures (503, 500, network, unreadable answer) show an `ErrorMessage` with Try again.

## Proposals for the user to confirm
The issue leaves these open. The plan needs a working value for each, so each one was a proposal. The user confirmed all of them as written when approving the plan on 2026-11-16. Each one is also listed under "Risks and open questions".

- **F1. Where the assistant appears: its own page at `/assistant`**, with a header link in `AuthNav` for logged-in users only (next to "Post an ad"; nothing while loading or logged out, same as "Post an ad"). Reason: the issue requires the same login rule as the details page, and a box on the public home page would have to be hidden or show a login hint for visitors, which is extra UI the issue does not ask for. Alternatives: a box on the home page for logged-in users, or a page without a header link.
- **F2. Header link text: "Ask the assistant".** Must fit at 360px next to "Post an ad" and "Log out" (the header already wraps with `flex-wrap`). If it does not fit well, a shorter fallback "Assistant" is proposed.
- **F3. One question, one answer (no conversation).** This is the issue's own interim rule. A new question replaces the previous answer and ads. The typed question stays in the field after the answer, so the user can adjust it. Nothing is stored between page visits.
- **F4. Maximum number of ads: decided by the server only.** The backend caps the list with `Assistant:MaxResults` (5 in `appsettings.json`, confirmed in plan #35). The screen shows every ad the server returns and has no own limit, so there is one source of truth. Changing the maximum stays a backend configuration change.
- **F5. Wording** (all short and friendly, per `docs/design.md`):
  - Page heading (`h1`): "Find a pet with the assistant"
  - Intro text: "Describe the pet you are looking for in your own words."
  - Field label: "Your question"
  - Placeholder: "For example: a calm dog that is good with small children" (the example from the meeting notes). The placeholder never replaces the visible label.
  - Helper text: "Up to 500 characters."
  - Submit button: "Ask"; while waiting: "Please wait..." (same busy text as the create form).
  - Empty question message: "Please type a question."
  - Too long message: "Your question can be at most 500 characters."
  - Answer section heading (`h2`): "Answer"
  - Ads section heading (`h2`): "Matching ads"
  - Empty state (no ads): "No ads match your question yet. Try describing the pet in other words." with one action, a link "Browse all ads" to `/`.
  - Error: "The assistant could not answer right now. Please try again." with Try again.
  - 400 without a usable message: "Please check your question and try again."
- **F6. Question field: the existing single-line `TextField`**, so Enter submits the form natively. Alternative: the shared `TextArea` (multi-line, then the button or Tab+Enter submits).
- **F7. Try again re-sends the last submitted question** (not whatever is in the field now), because the error belongs to that question.
- **F8. Cards: reuse `PetAdCard` from `features/petAds`** (title, Badge, price for Sale, location, link to `/pet-ads/:id` with `encodeURIComponent`). This is a cross-feature import; the alternative is a duplicate card in `features/assistant`, which `design.md` discourages ("Do not create a one-off look").
- **F9. Results are announced** to screen readers through one `role="status"` region with "Answer ready. 3 ads found." / "Answer ready. No ads found.", in the same spirit as the "N ads found" live region in `PetAdList`. After the answer arrives, focus stays on the submit button (not moved), so keyboard users keep their place; the live region tells them the result is there.

## Approach
- New feature folder `PawsPost.Web/src/features/assistant/`, mirroring the backend feature name `Features/Assistant` (`.claude/rules/frontend.md`).
- Guard pattern copied from `CreatePetAdPage` / `PetAdDetailsPage`: the outer `AssistantPage` reads `useAuth().status`. `loggedOut` renders `<Navigate to="/login" replace />`, `loading` renders the heading plus `Spinner`, `loggedIn` renders the inner `AssistantSearch`. So nothing can be requested before login status is known. The request is only sent on submit anyway, never on mount.
- Submit flow copied from `CreatePetAdForm`: `noValidate` form, `submitting` ref plus `busy` state as double-submit guard, client check first, then `client.POST('/api/assistant/search', { body: { question } })` through the typed client. 401 sets an `unauthorized` flag that renders `<Navigate to="/login" replace />`.
- Pure logic in small separate files so it can be unit tested, like `petAdFormRules.ts`, `petAdFormErrors.ts` and `petAdDetailsData.ts`:
  - `assistantQuestion.ts`: `QUESTION_MAX_LENGTH = 500` (mirrors `AssistantSearchRequest.QuestionMaxLength`), `validateQuestion(value)` (trim, empty, length after trimming, same rule as the server), the request body type taken from `components['schemas']['AssistantSearchRequest']`.
  - `assistantResult.ts`: `toAssistantResult(value: unknown)` converts the 200 body defensively: `answer` must be a string, `ads` must be an array, each ad goes through the existing `toPetAdDetails` (any bad ad makes the whole result `null`, which shows the error state, same rule as `toListData`). Types are derived from `components['schemas']['AssistantSearchResponse']`, never written by hand.
  - `assistantErrors.ts`: maps a 400 `ValidationProblemDetails` to one field message. Keys are normalised as in `petAdFormErrors.ts` (strip leading `$` and `.`, case-insensitive), so `question`, `Question` and `$.question` all match. Because the form has only one field, messages under any other key (including the empty key) are also shown next to the field. Server text is shown as sent. No usable message gives the F5 fallback.
- State of the inner component as a small union: `idle | loading | success(answer, ads, question) | error(question)`, plus field error and `unauthorized`. A late response after unmount is ignored (cancelled flag in a ref, same idea as `usePetAd`).
- The answer is rendered as a React text node in a `p` with `whitespace-pre-line break-words`. Never `dangerouslySetInnerHTML`, no Markdown rendering (would need a package or HTML).
- The ad list comes only from the response `ads`. The client never adds, reorders or derives ads from the answer text ("No ads are invented on the client").
- Shared components only: `TextField`, `Button`, `Card`, `Spinner`, `EmptyState`, `ErrorMessage`, plus `PetAdCard` (F8). Design tokens and existing token classes only. No new shared component is expected, and no new npm package.

## Regenerating the API types
Checked in the current repo: `schema.d.ts` has no `/api/assistant/search` path and no `AssistantSearchRequest` / `AssistantSearchResponse` schemas. The file is generated, never edited by hand: `npm run gen:api` (script `node scripts/gen-api.ts`, `openapi-typescript`) reads `http://localhost:5280/openapi/v1.json` from the `http` launch profile. Follow the process in `docs/plans/25-regenerate-api-types.md`:
1. Confirm the checkout is current `main` (contains merge 435e1a5 of #35) with no changes to tracked files.
2. Check port 5280 read-only (`Get-NetTCPConnection -LocalPort 5280 -State Listen -ErrorAction SilentlyContinue`). If it is in use, do not kill anything: stop and ask the user.
3. Start the API for this step only: `dotnet run --project PawsPost --launch-profile http`. No Gemini key is needed: the app starts without it (plan #35, P11), and generation does not call the assistant.
4. Fetch `/openapi/v1.json` (read-only) and verify it contains: `post` on `/api/assistant/search`, request body `AssistantSearchRequest` with `question`, a 200 response `AssistantSearchResponse` with `answer` and `ads` as an array of `PetAdDetailsResponse`, and 400, 401 and 503 responses. If anything is missing, the API is stale: stop and ask.
5. From `PawsPost.Web` run `npm run gen:api`. Optionally run it a second time and confirm the diff is identical (stable output). Then stop the API with Ctrl+C in its own terminal and confirm the port is free.
6. Read `git diff -- PawsPost.Web/src/api/schema.d.ts`. Expected additions only: the path `/api/assistant/search` with its `post` operation (and any generated operation/parameter entries that belong to it), the schemas `AssistantSearchRequest` (`question`, probably typed `null | string` like the other nullable required request fields) and `AssistantSearchResponse` (`answer: string`, `ads: PetAdDetailsResponse[]`), and the 200/400/401/503 response entries. Any other change (auth types, pet-ad paths or schemas, `ProblemDetails`, header or formatting drift) is a stop-and-ask, not a fix.
7. Check: `npx tsc -b`, `npm test` (same counts as before), `npm run lint`, `npx prettier --check --end-of-line auto src`, `npm run build`. Do not run Prettier `--write` on `schema.d.ts`.
8. Commit only `schema.d.ts` by explicit path: `chore: regenerate API types for the assistant endpoint (#36)`. Untracked docs files in the working tree (other issues, meeting notes, rules) must not be staged.

If the file is already regenerated when work starts, this step is skipped and noted in the PR.

## API endpoints
Existing endpoints used. The assistant endpoint exists in the merged backend (`PawsPost/Features/Assistant/AssistantController.cs`) and will be in `schema.d.ts` after step 2. No API change.

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| POST | /api/assistant/search | logged in (cookie), any user, no ownership | `AssistantSearchRequest { question: string }` (required, at most 500 characters after trimming) | `AssistantSearchResponse { answer: string, ads: PetAdDetailsResponse[] }` (ads: only `Active`, best match first, at most `MaxResults` = 5, may be empty) | 200, 400 (`ValidationProblemDetails`, key `question`), 401 (`ProblemDetails`), 503 (`ProblemDetails`, "The assistant is temporarily unavailable.") |
| GET | /api/auth/me | used through `useAuth` | none | `UserResponse` | unchanged |

Real backend behavior the screen relies on:
- When nothing fits, the server answers 200 with the fixed answer "Sorry, I could not find any ads that match your question." and `ads: []` (`AssistantService.NoMatchAnswer`). The screen shows that answer plus the empty state. It does not check the answer text.
- 401 comes before 400.
- `PetAdDetailsResponse` has no owner email, phone or owner id. The screen shows only what `PetAdCard` shows.

## Screens and components
- Route: `/assistant`, declared inside the `Layout` route in `App.tsx` (F1).
- Components (one per file):
  - `AssistantPage` (default export, route element): the guard (loading, logged out, logged in).
  - `AssistantSearch`: heading, intro, form, result area, submit flow.
  - `AssistantAnswer` (presentational): the "Answer" heading and the plain-text answer, then either the "Matching ads" list (`ul` of `PetAdCard`, one column at 360px, `sm:grid-cols-2`, same grid as `PetAdList`) or the `EmptyState` with the "Browse all ads" link.
  - Shared, existing: `TextField`, `Button`, `Card`, `Spinner`, `EmptyState`, `ErrorMessage`, `PetAdCard` (F8).
  - `AuthNav`: add the "Ask the assistant" link to `/assistant` for logged-in users (F1, F2).
- States:
  - Auth loading: `h1` plus `Spinner`, no form, no request, no flash of the login page.
  - Logged out: `<Navigate to="/login" replace />`, no request.
  - Idle (before the first question): heading, intro and form only. No empty state yet (nothing was asked).
  - Loading: `Spinner` in the result area, submit button disabled with `aria-busy` and "Please wait...", input `readOnly`, the previous answer is removed (F3).
  - Success with ads: answer, then the ad cards.
  - Empty (200 with `ads: []`): the server's answer, then the `EmptyState` (F5). No list.
  - Error (503, 500, any other non-400 status, network failure, or a body that fails `toAssistantResult`): `ErrorMessage` with the F5 text and Try again (F7). No status code or technical text. After Try again the result area shows the Spinner; once it settles, focus moves to a `tabIndex={-1}` "Answer" heading or the error container (same pattern as `PetAdDetailsPage`).
  - 401 from the API: `<Navigate to="/login" replace />`.
  - Result announcement: one `role="status"` region (F9).
- Forms and validation:
  - One field "Your question" (`TextField`, F6) with visible label, placeholder and helper text (F5), and one submit `Button` ("Ask"). A real `form` with `noValidate`, so Enter submits.
  - Client checks before sending (convenience only, the server decides): trimmed value empty gives "Please type a question." and no request; trimmed length over 500 gives the too-long message and no request. The input does not use the `maxLength` attribute (it would count leading and trailing spaces, which the server ignores, and would silently cut pasted text). The body sends the trimmed question.
  - Server 400: the message from `errors` is shown next to the field (via `errorText`, which sets `aria-invalid` and `aria-describedby`), and focus goes to the field.
  - Double submit guard: `submitting` ref plus `busy`.
  - Mobile first at 360px; long answers, titles and locations wrap (`break-words`). Visible focus from the existing tokens. Keyboard: Tab to the field, Enter or the button submits, Tab reaches each card link and Try again.

## Data changes
none

## Files to create or change
- `PawsPost.Web/src/api/schema.d.ts`: regenerated with `npm run gen:api` only (step 2). Never edited by hand.
- `PawsPost.Web/src/features/assistant/assistantQuestion.ts`: max length constant, `validateQuestion`, body building (trimmed), types from the schema.
- `PawsPost.Web/src/features/assistant/assistantQuestion.test.ts`
- `PawsPost.Web/src/features/assistant/assistantResult.ts`: `toAssistantResult`, reusing `toPetAdDetails` from `features/petAds/petAdDetailsData.ts`.
- `PawsPost.Web/src/features/assistant/assistantResult.test.ts`
- `PawsPost.Web/src/features/assistant/assistantErrors.ts`: 400 mapping and fallback messages.
- `PawsPost.Web/src/features/assistant/assistantErrors.test.ts`
- `PawsPost.Web/src/features/assistant/AssistantAnswer.tsx` and `AssistantAnswer.test.tsx`: answer and list or empty state.
- `PawsPost.Web/src/features/assistant/AssistantSearch.tsx`: form, submit flow, states.
- `PawsPost.Web/src/features/assistant/AssistantPage.tsx`: guard.
- `PawsPost.Web/src/features/assistant/AssistantPage.test.tsx`: page tests (covers `AssistantSearch` through the page).
- `PawsPost.Web/src/App.tsx` and `App.test.tsx`: the `/assistant` route.
- `PawsPost.Web/src/features/auth/AuthNav.tsx` and `AuthNav.test.tsx`: the header link.
- Possibly `PawsPost.Web/src/test/apiResponses.ts`: a helper that builds an assistant response with sample ads, if several test files need it.

Not changed: `client.ts`, `PetAdCard`, `petAdDetailsData.ts` (only imported), any shared component, `LoginPage`, the backend.

## Tests to write
Vitest and Testing Library, in the style of `CreatePetAdPage.test.tsx`: `vi.mock('../../api/client')` with `POST` mocked (the API client is the only mock), an `AuthContext.Provider` value per status, `MemoryRouter` with `/login` and `/pet-ads/:id` probe routes, `user-event`, `findBy` queries, elements found by role, label or visible text. Answers built with `okResult`, `failResult` and `deferred` from `src/test/apiResponses.ts`. Ad data is synthetic.

`AssistantPage.test.tsx` (one behavior per test):
- shows the answer and the matching ads after asking a question.
- sends the trimmed question to `/api/assistant/search` through the client (asserts the call path and body).
- each ad card links to its details page `/pet-ads/<id>`, and clicking one opens the details probe.
- shows a Spinner and disables the Ask button while waiting for the answer (`deferred`).
- sends only one request on a double click or double Enter.
- shows the answer and the "no ads" empty state with a link to all ads when the server returns no ads, and shows no list.
- does not show any ad that is not in the response (for example the answer text mentions a title, but only the response ads become links).
- does not send an empty or whitespace-only question and shows "Please type a question." next to the field.
- does not send a question longer than 500 characters (after trimming) and shows the too-long message; a question of exactly 500 characters with surrounding spaces is sent.
- shows the server's 400 message next to the field and moves focus to it (keys `question`, `Question` and `$.question`).
- shows the fallback message when a 400 has no usable message.
- redirects a logged-out visitor to `/login` and does not call the client.
- shows a Spinner while the login state is loading, shows no form and no login page, and makes no request.
- redirects to `/login` when the API answers 401.
- shows an ErrorMessage with Try again on 503, without technical details (no "503", no server title).
- shows an ErrorMessage on a network failure (client rejects).
- shows an ErrorMessage when the response body has the wrong shape.
- Try again re-sends the last submitted question and shows the answer when it succeeds.
- a new question replaces the previous answer and ads (F3).
- renders an answer containing HTML characters (for example `<b>hi</b>` and `<img src=x onerror=alert(1)>`) as visible text, with no `img` or `b` element created.
- the question field has a visible label "Your question", and the form is reachable and submittable with the keyboard only (Tab, type, Enter).
- announces the result in the status region ("Answer ready. 2 ads found.").

`assistantQuestion.test.ts`: empty, whitespace only, 500 characters accepted, 501 rejected, surrounding spaces not counted, body is trimmed.
`assistantResult.test.ts`: valid body, empty `ads`, missing or non-string `answer` gives null, `ads` not an array gives null, one bad ad gives null, numeric strings for price and age accepted (via `toPetAdDetails`).
`assistantErrors.test.ts`: key normalisation and case, unknown and empty keys go to the field, joined messages, fallback when nothing usable.
`AssistantAnswer.test.tsx`: shows answer text and one card per ad in the given order; shows the empty state and no list when there are no ads.
`AuthNav.test.tsx`: "Ask the assistant" links to `/assistant` when logged in; not shown when logged out or loading.
`App.test.tsx`: `/assistant` renders the assistant page inside the layout.

## Steps
1. Get the proposals F1 to F9 confirmed or changed, and approval of this plan. Create branch `feature/36-ai-assistant-search-frontend` from `main`.
2. Regenerate `schema.d.ts` as described in "Regenerating the API types". Commit: `chore: regenerate API types for the assistant endpoint (#36)`.
3. Add `assistantQuestion.ts` with tests. Commit: `feat: add assistant question rules (#36)`.
4. Add `assistantResult.ts` with tests. Commit: `feat: add assistant response converter (#36)`.
5. Add `assistantErrors.ts` with tests. Commit: `feat: map assistant validation errors (#36)`.
6. Add `AssistantAnswer` with tests. Commit: `feat: show assistant answer and matching ads (#36)`.
7. Add `AssistantSearch` and `AssistantPage` (guard, form, states, redirects, Try again, live region) with page tests. Commit: `feat: add assistant page (#36)`.
8. Add the `/assistant` route and its test. Commit: `feat: add /assistant route (#36)`.
9. Add the header link in `AuthNav` with tests. Commit: `feat: link to the assistant in the header (#36)`.
10. Run `npx tsc -b`, `npm run lint`, `npm test`, `npx prettier --check --end-of-line auto src`, `npm run build`. Check manually at 360px (header with three items), with keyboard only, and with visible focus. A manual check against the real assistant needs a Gemini key in user-secrets and synthetic demo ads only; it is optional and not part of the tests. Open the PR with `Closes #36`, listing which proposals were confirmed.

## Risks and open questions
Open questions from the issue (not decided; the plan uses the proposal until a person answers):
1. Conversation memory: does the assistant remember earlier questions in a session? Proposal F3: no, one question and one answer. Changing it later would change both the backend request shape (#35 P1) and this screen.
2. Maximum number of ads shown. Proposal F4: the server's `Assistant:MaxResults` (5) only, no client limit.
3. Where the assistant appears. Proposal F1: own page `/assistant` with a header link for logged-in users. Header link text F2.
4. Wording of labels, placeholder, empty state, errors. Proposal F5 (all of it needs confirmation).

Additional questions this plan raises (also need a person):
5. Single-line field or multi-line text area (F6).
6. What Try again sends (F7).
7. Reusing `PetAdCard` across features (F8), or a separate assistant card. Note: `PetAdCard` shows no species, breed or age, though the response has them.
8. Focus and announcement after an answer (F9).
9. Return to `/assistant` after login: not done, as on the details page (#20). The redirect carries no return target and login goes to `/`.
10. 503 gets the same generic error as other failures (F5). A separate "assistant is temporarily unavailable" wording is possible if the client wants it.

Dependencies and risks:
- Blocking until step 2: the assistant endpoint is not in `schema.d.ts` yet. Regeneration needs the API running locally from current `main` on port 5280; if the port is held by another process, the developer stops and asks (plan #25 rules), never kills it.
- The answer comes from a language model. It may contain Markdown characters (`*`, `#`), which are shown as plain text by design (the issue requires plain text). It may also mention an ad by title that is not in the list; the list itself cannot be invented because it comes from the response.
- Ads created before #35 are not searchable (backend P7), so in a demo with old data many questions will show the empty state. This is a backend limitation, not a bug in this screen.
- Every question calls the paid provider. There is no rate limiting (not asked for). The double-submit guard and the disabled button prevent accidental repeats only.
- Without a Gemini key the backend answers 503 for every search, so a local manual check shows the error state. Tests do not need a key (client mocked).
- Header space at 360px with three items for logged-in users; checked manually in step 10, fallback label in F2.
- The client length check mirrors the server constant (500). If the backend changes it, the client check must change too; the server message is still shown in that case.
- Stale login state: if the cookie expired, the 401 redirect handles it; the header may show logged-in until the next login, as accepted in #20.
