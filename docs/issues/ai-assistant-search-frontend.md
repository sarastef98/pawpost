Issue: #36
URL: https://github.com/sarastef98/pawpost/issues/36

# Add assistant box for finding ads in plain words

Layer: frontend
Depends on: #35, #14, #20

## Summary
Add a simple assistant screen where a logged-in user types a question, sees the assistant's short answer and the matching ads, each linking to its details page. The API is defined in `ai-assistant-search-backend.md`.

## User story
As a logged-in user, I want to describe the pet I want in my own words and see the ads that fit, so that I can find an animal without knowing the exact words used in ads.

## Acceptance criteria
- [ ] A page or box (route and navigation placement chosen in the plan) shows a question field and a submit button.
- [ ] Submitting sends the question through the typed client to the assistant endpoint defined in the backend issue.
- [ ] The answer text and the matching ads are shown. Each ad links to its details page `/pet-ads/:id`.
- [ ] While waiting, a loading state is shown and the submit button is disabled.
- [ ] If nothing fits, the answer is shown and a friendly empty state is shown instead of a list. No ads are invented on the client.
- [ ] An empty question is not sent, and a message is shown. The maximum length from the backend is respected, with a validation message from the server shown when it answers 400.
- [ ] A visitor who is not logged in (via `useAuth`) is redirected to `/login` and nothing is requested. While login status is unknown, a Spinner is shown. A 401 answer also redirects to `/login`.
- [ ] Other errors show an ErrorMessage with a Try again button, without technical details.
- [ ] Uses shared components and design tokens only, works at 360px first, is keyboard accessible with visible focus.
- [ ] Answer text is rendered as plain text, never as HTML.
- [ ] Tests cover: answer and ads shown, links to details, loading state, no results, empty question, redirect when logged out, error with Try again.

## Out of scope
- The API, ranking, prompts and provider details (backend issue).
- Conversation history, unless the client decides it (see open questions).
- Contact details, messaging, photos.

## Open questions
- Does the assistant remember earlier questions in the same session? Not decided. This decides whether the screen shows a conversation or a single answer. Until decided, one question and one answer are shown.
- What is the maximum number of ads shown? Not decided (also an open question in the backend issue).
- Where does the assistant appear: its own page, a link in the header, or a box on the home page? Not given. Wording of labels, placeholder and empty state is also not given.

## Source
Meeting of 15 Nov (`docs/meeting-notes/2026-11-15-rag-assistant.md`). The frontend is a simple assistant box with the question, the answer and the matching ads, each linking to its details page. Only logged-in users may use it, as on the ad details page. It depends on the backend issue. Memory between questions and the maximum number of ads are not decided.
