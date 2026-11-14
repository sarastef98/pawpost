Issue: #14
URL: https://github.com/sarastef98/pawpost/issues/14

# Add register page, login page and log in/out header links

Layer: frontend
Depends on: #3, #11

## Summary
Add a register page and a login page that use the existing auth API (`POST /api/auth/register`, `POST /api/auth/login`), and make the page header show Log in and Register links to logged-out visitors and a Log out button to logged-in users. A wrong email or password shows a friendly message.

## User story
As a visitor, I want to create an account and log in from the website, so that I can use PawsPost as myself.

## Acceptance criteria
- [ ] The route `/register` shows a register page inside the shared layout, with an email field and a password field, each with a label.
- [ ] Submitting the register form calls `POST /api/auth/register` through the typed client (`src/api/client.ts`). The API contract is defined in #3 and is not changed here.
- [ ] The route `/login` shows a login page inside the shared layout, with an email field and a password field, each with a label.
- [ ] Submitting the login form calls `POST /api/auth/login` through the typed client.
- [ ] When the login succeeds, the user is treated as logged in.
- [ ] When the login fails with 401 (wrong email or password), the page shows a short, friendly message such as "Wrong email or password. Please try again." No status codes, no technical details, and the message does not say which of the two was wrong.
- [ ] When register fails with 409 (email already registered), the page shows a friendly message and no technical details.
- [ ] When register or login fails with 400, the field errors from the server's `ValidationProblemDetails` are shown next to the matching fields in friendly wording.
- [ ] The forms validate required fields on the client for convenience. The server still decides.
- [ ] While a form is being submitted, the submit button shows a loading state and cannot be pressed twice.
- [ ] If the request fails for another reason (for example the network is down), the page shows a friendly error message with no technical details.
- [ ] For logged-out visitors the header navigation shows a Log in link (to `/login`) and a Register link (to `/register`).
- [ ] For logged-in users the header navigation shows a Log out button instead of those links.
- [ ] Pressing Log out calls `POST /api/auth/logout`, and the header then shows Log in and Register again.
- [ ] While the app is still finding out whether the visitor is logged in, the header does not flash the wrong links (a designed loading state).
- [ ] The password is never logged, never stored by the frontend, and never shown in an error message.
- [ ] The pages use the shared components (Button, TextField, Card, ErrorMessage, Spinner) and design tokens only. No one-off colors or sizes.
- [ ] The pages work at 360px width first and look good on desktop.
- [ ] Every input has a label, buttons are real `button` elements, and everything works with the keyboard and shows a visible focus outline.
- [ ] Tests cover: both pages render inside the layout; successful register and login; the friendly message on 401; the 409 message; 400 field errors; the header for logged-out and logged-in users; and Log out.

## Out of scope
- Remember me (client has not decided).
- Login with Google (client has not decided).
- Email verification and password reset (not requested; also out of scope in #3).
- Any change to the API (the existing endpoints from #3 are used as they are).
- Pages that require login, such as creating an ad, and redirecting to the login page from them.
- Profile page, account deletion, display name or phone number.

## Open questions
- How does the header know who is logged in? The notes do not say. The existing API has `GET /api/auth/me` (200 with the user, 401 when not logged in) and the cookie is HttpOnly, so the frontend cannot read it. Is it acceptable to call `/api/auth/me` when the app starts and after login, register and logout? Confirm with the developer. If something else is needed, that is a backend change and a new issue.
- Where does the user go after a successful login, and after logging out (home page?). The notes do not say.
- The API does not log the user in after register (assumption in the #3 plan). After a successful register, should the user be sent to the login page with a "Your account is ready, please log in" message, or should the frontend log them in automatically right after (a second request)? The client must decide.
- What exact wording should the messages use (wrong email or password, email already registered, generic error)? Proposed wording above is a suggestion; the client has not approved it.
- Password rules are not confirmed by the client (the #3 plan uses at least 8 characters with upper case, lower case, a digit and a symbol, pending client confirmation). Should the register page tell the user the rules before they submit, or only show the server's errors after?
- Should the register form have a "repeat password" field? The notes only mention email and password.
- After many failed logins the API locks the account and returns the same 401 as a wrong password (assumption in the #3 plan). Is the same friendly message fine for that case?
- Should the logged-in header show the user's email or a greeting? The notes only ask for a Log out button, so nothing else is planned.

## Source
Client meeting of 14 Nov (`docs/meeting-notes/2026-11-14-login-ui.md`). A visitor can create an account on a register page with email and password, and log in on a login page. When logged in, the header shows a Log out button; logged-out visitors see Log in and Register links. A wrong email or password shows a clear, friendly message, not technical details. Remember me and login with Google are not decided yet.
