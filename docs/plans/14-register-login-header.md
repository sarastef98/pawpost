# Plan: Add register page, login page and log in/out header links (#14)

Issue: https://github.com/sarastef98/pawpost/issues/14

Status: Approved
Layer: frontend

Depends on: #3 (auth API, merged, contract below), #11 (layout, routing, shared components, merged).

## Goal
A visitor can open `/register` and create an account, open `/login` and log in, and the page header shows Log in and Register links to logged-out visitors and a Log out button to logged-in users. Wrong credentials, an already registered email, server field errors and network failures all show friendly messages with no technical details. The password is never logged, stored or shown.

## Approach
- Use only what is already installed: React 19, `react-router-dom` (already installed, `BrowserRouter` in `main.tsx`, `Routes` in `App.tsx`), the typed `openapi-fetch` client in `src/api/client.ts`, Tailwind design tokens and the shared components (Button, TextField, Card, ErrorMessage, Spinner). **No new npm package is needed or proposed.**
- Auth state: a small React context in one auth module, `src/features/auth/AuthContext.tsx` (provider plus a `useAuth()` hook). It holds `status` (`loading` | `loggedOut` | `loggedIn`) and the user (`id`, `email`, from `UserResponse`) in memory only. It never holds the password and never touches the cookie (the cookie is HttpOnly, the browser sends it by itself on same-origin relative URLs).
- How the app knows who is logged in (Decision 1): the provider calls `GET /api/auth/me` once on start. 200 means logged in, 401 means logged out (this 401 is normal, not an error). After a successful login the provider sets the user from the login response (200 returns `UserResponse`), so no second request is needed. After logout it sets logged out. Register does not change auth state. A network failure on the start check is treated as logged out with no error shown (Decision 10).
- The provider wraps `<App />` in `main.tsx`, inside `BrowserRouter`. Tests wrap in the provider or mock the client.
- Header: `Layout.tsx` currently has an empty `<nav aria-label="Main" />` landmark. It will render a new `AuthNav` component inside it: a loading placeholder while `loading`, Log in and Register links when logged out, a Log out `Button` when logged in (and nothing else, Decision 8). The nav landmark stays, so the existing Layout test that looks for a navigation role keeps working.
- Pages live in `src/features/auth/` (mirrors the backend `Features/Auth`): `RegisterPage.tsx`, `LoginPage.tsx`. Both render inside the existing `<Layout />` route group in `App.tsx`.
- Error handling helper `src/features/auth/authErrors.ts`: turns an `openapi-fetch` result (or a thrown network error) into a view model: field errors map and one general message. Field error keys from ASP.NET are usually `Email` and `Password` (any casing); match case-insensitively to the form fields; errors for unknown keys or an empty key go to the general message. No status codes or `title`/`detail` from the server are ever shown directly as technical text. For 400, the server's field messages are shown as sent (Decision 4).
- Forms: controlled inputs, `noValidate` on the form so our own friendly messages are shown instead of browser bubbles (Decision 18); client checks only that email and password are not empty (convenience, the server decides). `type="email"`, `autoComplete="email"`, `type="password"`, `autoComplete="current-password"` on login and `new-password` on register. Submit uses `Button type="submit"`, `disabled` plus text "Please wait..." (and `aria-busy`) while the request runs, and the handler ignores a second submit while busy.
- Errors shown: field errors through `TextField errorText`; general messages (401, 409, network) through `ErrorMessage` (role `alert`) above the form inside the `Card`. Focus moves to the first field with an error, or the alert, after a failed submit (accessibility polish; decide in implementation, covered by a test only if done).

## API endpoints
Existing endpoints from `PawsPost.Web/src/api/schema.d.ts` (checked against `PawsPost/Features/Auth/AuthController.cs`). Nothing is changed in the API.

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| POST | /api/auth/register | public | `RegisterRequest` (`email`, `password`) | `UserResponse` (`id`, `email`); does NOT log the user in | 201, 400 (`ValidationProblemDetails`, `errors` map of field to string[]), 409 (`ProblemDetails`, email already registered) |
| POST | /api/auth/login | public | `LoginRequest` (`email`, `password`) | `UserResponse` + HttpOnly `Set-Cookie` | 200, 400 (`ValidationProblemDetails`, missing fields), 401 (`ProblemDetails`, same for wrong password, unknown email, malformed email and lockout) |
| GET | /api/auth/me | logged in | none | `UserResponse` | 200, 401 (`ProblemDetails`) |
| POST | /api/auth/logout | logged in | none | none | 204, 401 (`ProblemDetails`, not logged in) |

Contract notes that shape the UI:
- The cookie is HttpOnly, Secure, SameSite=Lax, set by the server. The frontend never reads or writes it. Requests use relative URLs (same origin, via the Vite proxy in development), so the browser sends it automatically; no `credentials` option change should be needed (verify in step 2).
- Login 401 gives no hint whether email or password was wrong, and lockout looks the same. The UI message must not distinguish them either (Decision 7).
- Register 400 can contain password-rule errors (Identity: length, upper, lower, digit, symbol) and email format errors. Error text is server-written English and is shown as sent (Decision 4).
- A 401 from logout means "already logged out"; treat it as logged out, no error.
- No endpoint is missing for this issue. No blocking dependency.

## Screens and components
- Routes (all inside the shared `Layout`):
  - `/register` shows `RegisterPage`
  - `/login` shows `LoginPage`
  - A logged-in user who opens either page sees it as normal, no redirect (Decision 11).
- Components (new, in `src/features/auth/`):
  - `AuthContext.tsx`: provider and `useAuth()` (`status`, `user`, plus `login(email, password)`, `register(email, password)`, `logout()` or equivalents that wrap the typed client; the pages call these, not the client directly, so the password is only passed through, never stored).
  - `AuthNav.tsx`: header content for the three states.
  - `RegisterPage.tsx`, `LoginPage.tsx`: heading level 1 ("Create your account" / "Log in"), a `Card` containing the form, a link to the other page ("Already have an account? Log in" / "New here? Create an account").
  - `authErrors.ts`: result-to-message mapping.
- Components (changed): `Layout.tsx` (fills the nav), `App.tsx` (two routes), `main.tsx` (provider).
- Shared components used: `Button` (primary submit; Log out as secondary, since design allows one primary per screen), `TextField`, `Card`, `ErrorMessage`, `Spinner`. Tokens only. Nav links reuse the logo `Link` token styling from `Layout` (Decision 19); if a shared link component turns out to be really needed, add it to `src/components/` first as its own small step (design rule), not a one-off.
- States:
  - Header loading: while status is `loading` the nav shows a fixed-size placeholder so the links do not flash and the layout does not jump (the shared `Spinner` is also acceptable; an implementation detail, either gives the designed loading state).
  - Form submitting: submit button disabled with "Please wait...", and inputs disabled too so the values cannot change mid-request.
  - Empty: not applicable (forms, no list data). Stated deliberately.
  - Error: `ErrorMessage` for 401, 409, network or unexpected status; field errors under the matching fields for 400. No retry button on these (the form itself is the retry). Header: no error state for the start check (Decision 10); a logout failure shows the message from Decision 9.
- Forms and validation:
  - Login: email (required), password (required). Client message "Please enter your email." / "Please enter your password." Server 400 field errors are shown next to the matching fields.
  - Register: email (required), password (required). No repeat password (Decision 6) and no password rules text (Decision 5).
  - Client-side checks stop at "not empty". No client copy of the password rules, to avoid drifting from the server.
  - Layout at 360px first: single column, full-width fields and button inside the `Card`, centered with a max width token on wider screens (use existing `max-w-*` tokens; no one-off sizes).
  - Accessibility: every input has a label (via `TextField`), real buttons, visible focus, errors tied with `aria-describedby` (done by `TextField`), alert role for general errors, Log out is a `button` and the auth links are real links.
- Password handling: kept only in the input's React state, passed straight to the API call, cleared from state after success; never put in the URL, `localStorage`, `sessionStorage`, logs or error text. No `console.*` calls in this feature. After a failed login keep the email and clear the password.

## Data changes
none

## Files to create or change
Paths are relative to `PawsPost.Web/`.
- `src/features/auth/AuthContext.tsx`: provider and `useAuth()`; the only place that calls `me`, `login`, `register`, `logout` and holds the user.
- `src/features/auth/authErrors.ts`: maps results to field errors and a general friendly message.
- `src/features/auth/AuthNav.tsx`: header navigation content for loading, logged out, logged in.
- `src/features/auth/RegisterPage.tsx`: register form page.
- `src/features/auth/LoginPage.tsx`: login form page.
- `src/features/auth/*.test.tsx`: tests next to each file (see Tests).
- `src/components/Layout.tsx`: render `AuthNav` inside the existing `<nav aria-label="Main">`.
- `src/components/Layout.test.tsx`: wrap in the provider or mock the client (the existing tests render `Layout` without any auth state); keep all existing assertions.
- `src/App.tsx`: add routes `/register` and `/login` inside the `Layout` route group.
- `src/App.test.tsx`: existing tests render `App` with no provider and no mocked client; update to mock the client boundary (`/me` returns 401) so they keep passing unchanged in meaning. Add route tests for the new pages (can live here or in the page tests).
- `src/main.tsx`: wrap `<App />` with `AuthProvider` inside `BrowserRouter`.
- `src/api/schema.d.ts`: NOT edited. If it is out of date with the API, regenerate with `npm run gen:api` only; the auth endpoints are already present.
- No change to `package.json`, to the API project, or to `docs/design.md`.

## Tests to write
Vitest, Testing Library and `user-event` (already installed), per `.claude/rules/frontend-testing.md`: find by role, label or text, `findBy` for async, mock only the API client at the boundary (`src/api/client.ts`), one behavior per test, sentence-style names, no snapshots. Each test sets up its own mock responses, so there is no order dependence.

Register page
- shows the register page with a labelled email field and a labelled password field inside the layout at `/register`
- calls the register endpoint with the typed email and password and, after a 201, goes to `/login` showing "Your account is ready. Please log in."
- shows "Please enter..." messages and does not call the API when the fields are empty
- shows the server's field errors, as sent, next to the matching fields on a 400
- shows "This email is already registered. Try logging in instead." (with a link to `/login`) on a 409 and no status code or technical text
- shows "Something went wrong. Please check your connection and try again." when the network is down
- disables the submit button and shows the loading text while the request is running, and sends only one request when pressed twice
- the form works with the keyboard only (tab to fields, Enter submits)
- has no repeat password field and no password rules text

Login page
- shows the login page with a labelled email field and a labelled password field inside the layout at `/login`
- calls the login endpoint and treats the user as logged in after a 200 (header shows Log out), then goes to `/`
- shows "Wrong email or password. Please try again." on a 401, and the text does not contain a status code or say which one was wrong
- shows the same message for any 401 (lockout looks identical)
- shows server field errors on a 400
- shows the generic friendly error when the network is down
- disables the submit button while submitting and sends only one request on a double press
- keeps the email and clears the password after a failed login
- the password never appears in rendered error text
- shows the "Your account is ready. Please log in." message when arriving from register

Header (AuthNav / Layout)
- shows no Log in, Register or Log out while the start check is still running (and shows the loading state)
- shows Log in (href `/login`) and Register (href `/register`) links when `/me` returns 401
- shows a Log out button and no Log in or Register links, and no email or greeting, when `/me` returns 200
- shows the logged-out links, with no error, when the start check fails with a network error
- calls the logout endpoint when Log out is pressed, goes to `/`, and shows Log in and Register again
- shows the logged-out links when logout returns 401
- keeps the user logged in and shows "We could not log you out. Please try again." when logout fails by network
- the Log out button and links are reachable with the Tab key
- does not redirect a logged-in user who opens `/login` or `/register`

Auth context
- calls `/me` only once on start
- sets the user after a login and clears it after a logout
- (security) no automated test for "no `console` calls and no storage calls"; done as a review item in step 8.

Existing tests: all Layout, App, HomePage, StyleGuidePage and component tests must keep passing without being changed in meaning (only their setup is adjusted for the provider or mock).

Authorization: backend not changed. The authorization behavior in the UI is the header state; pages that require login and redirects to login are out of scope (issue).

## Steps
Each step is one commit; after each step `npm run build`, `npm run lint`, `npm test` and `npm run format:check` must be green.
1. The branch `feature/14-register-login-header` is created by the pipeline from `main`; this step needs no answers and nothing to wait for. (`chore`, only if a commit is needed)
2. Add `AuthContext.tsx` with `useAuth()`, the `/me` start check and `login`, `register`, `logout` wrappers over the typed client, plus `authErrors.ts`, with unit tests (mock client): states, `/me` 200 and 401, network failure, error mapping including case-insensitive field keys. Confirm cookies are sent without extra client options and that the Secure cookie is kept on `http://localhost` (manual check against the running API, see risks). (`feat: add auth state and error mapping (#14)`)
3. Wrap the app in `AuthProvider` (`main.tsx`) and adjust `App.test.tsx` and `Layout.test.tsx` setup so existing tests still pass. (`test: adapt tests for auth provider (#14)`)
4. Add `AuthNav` and render it in the Layout nav with header tests (loading, logged out, logged in, Log out, logout failure). (`feat: show log in, register and log out in the header (#14)`)
5. Add `LoginPage`, route `/login`, with tests (success, 401, 400, network, double submit, keyboard). (`feat: add login page (#14)`)
6. Add `RegisterPage`, route `/register`, with tests (success redirect and message, 400, 409, network, double submit, keyboard). (`feat: add register page (#14)`)
7. Check at 360px and desktop widths by hand, check tokens only (no hex values, no arbitrary pixel values), contrast, focus outline. Manual end-to-end against the running API: register, login, refresh (still logged in), logout; also try the Secure cookie on `localhost` in the browser used. (`fix` only if needed)
8. Review checklist: no `console` calls, no password in storage, URL or error text, no `any`, no `dangerouslySetInnerHTML`. Run build, lint, tests, format check. Open the PR from the template with `Closes #14`. (`docs` or none)

## Decisions
Decided by the user with "go with your suggestions", accepting every recommended default. Decisions 3 to 8 are items the issue marks as the client's to decide; the user decided them on the client's behalf, so they can still be changed later with the client (the changes are small and local to the auth feature).

1. **How the header knows who is logged in:** call `GET /api/auth/me` once at app start; set the user from the login response after login; clear it after logout. Register does not change auth state.
2. **Where to go after login and after logout:** the home page `/` for both (`useNavigate`).
3. **After a successful register (client item):** go to `/login` with the message "Your account is ready. Please log in." (passed with router state, nothing stored). No auto-login.
4. **Message wording (client item):**
   - Wrong credentials (401): "Wrong email or password. Please try again."
   - Email already registered (409): "This email is already registered. Try logging in instead." (with a link to `/login`)
   - Other failures, network down or unexpected status: "Something went wrong. Please check your connection and try again."
   - Empty fields (client check): "Please enter your email." / "Please enter your password."
   - Logout failure: "We could not log you out. Please try again."
   - 400 field errors: the server's text is shown as sent (these are Identity's English messages; no client-side translation table, so it cannot drift from the server).
5. **Password rules up front (client item):** not shown; only the server's errors are shown.
6. **Repeat password field (client item):** none. Email and password only.
7. **Lockout message (client item):** the same message as a wrong password (the API returns the same 401). Accepted trade-off: a locked-out user with the right password sees "Wrong email or password" for about 5 minutes with no explanation.
8. **Logged-in header content (client item):** only a Log out button. No email, no greeting.
9. **Logout network failure:** keep the user logged in and show "We could not log you out. Please try again." (small `ErrorMessage`/alert near the nav). A 401 from logout counts as already logged out, no error.
10. **Start-up `/me` network failure:** treat as logged out, show the Log in and Register links with no error, so the header never stays stuck on loading.
11. **Logged-in user opening `/login` or `/register`:** the page is shown as normal, no redirect (redirects are out of scope).
12. **Session expiry while on the site:** not handled now. The header may show Log out until the next reload. To be handled when pages that need login exist (a global 401 handler would be a new issue).
13. (Kept as a risk, see below: Secure cookie on localhost.)
14. (Kept as a risk, see below: same-site hosting.)
15. **Token and personal data rule:** there is no token in the frontend (HttpOnly cookie). The single auth module is `src/features/auth/AuthContext.tsx`, which holds only the user's id and email, in memory only. Confirmed fine.
16. **Duplicate registration reveals that an email exists:** accepted (409 is required by the issue and #3). No UI workaround.
17. **Existing `Layout` and `App` tests need setup changes** (provider or mocked client) in step 3. Expected, not a weakened test; assertions stay the same.
18. **`noValidate` on the forms** so our messages and the server's decide, not browser bubbles.
19. **Nav links** reuse the logo link's token styling from `Layout`. A shared link component is added first only if it proves really needed.

## Risks and open questions
Open questions: none. All earlier open questions are answered in "Decisions". If the client later changes decisions 3 to 8, update this plan and the affected tests.

Noted risks (not blocking):
- **Secure cookie on local development.** The #3 plan sets `Secure` on the auth cookie, but the Vite dev server uses plain `http://localhost`. Chrome and Firefox accept Secure cookies on `localhost`; Safari may not. If the cookie is not kept, login would appear to succeed and `/me` would return 401 after a refresh. Check in step 2 and step 7. If it fails, it is a backend or proxy configuration change (a new issue), not part of #14.
- **Same-site hosting.** The #3 plan chose `SameSite=Lax` and no CORS, assuming the frontend is served same-site (relative `/api` URLs through the Vite proxy). This plan relies on that. If production serves the frontend from a different site, login will not work and needs a backend decision. No action in #14.
- **Server error text is not always friendly** (Identity English defaults, shown as sent per Decision 4). If the client wants friendlier text, it is a backend wording change or an agreed mapping, in a new issue.
- **Email in memory:** `AuthContext` holds the user's email in memory while logged in; it is never stored or logged.
- **Logout only clears the cookie** server-side (known limitation from the #3 plan); nothing to do in the frontend.

Out of scope (from the issue, not planned): remember me, login with Google, email verification, password reset, any API change, pages that require login and redirects to login, profile page, account deletion, display name, phone number.
