Issue: #10
URL: https://github.com/sarastef98/pawpost/issues/10

# Set up styling foundation and shared components

Layer: frontend
Depends on: none

## Summary
Set up Tailwind CSS with design tokens defined in one place, and build the small set of shared components every screen will reuse.

## User story
As a visitor, I want every screen to look and behave consistently, so that the app feels trustworthy and easy to use.

## Acceptance criteria
- [ ] Tailwind CSS is set up in `PawsPost.Web`.
- [ ] Design tokens (colors, font, radius, spacing) are defined in one place only, with values taken from `docs/design.md`.
- [ ] No component hard-codes a color or size; all of them use the tokens.
- [ ] `Button` exists with variants primary, secondary and danger.
- [ ] `TextField` exists with a label, optional helper text and optional error text.
- [ ] `Card` exists.
- [ ] `Badge` exists with variants Adoption and Sale.
- [ ] `Spinner` exists.
- [ ] `EmptyState` exists.
- [ ] `ErrorMessage` exists and shows friendly text without technical details.
- [ ] Every component renders correctly at 360px width.
- [ ] Every control can be reached and used with the keyboard and shows a visible focus outline.
- [ ] `Button` is a real `button` element and `TextField` label is linked to its input.
- [ ] Each component has a test.

## Out of scope
- Dark mode.
- Icon library.
- Animations.
- Layout, routing and the style guide page (see `ui-layout-routing-frontend.md`).

## Open questions
- Tailwind CSS is a new npm package. Needs approval before it is added (project rule: no new package without asking).
- Which props does each component need beyond those listed (for example Button sizes, disabled and loading states)? The notes do not say.
- What text should `EmptyState` and `ErrorMessage` show by default? Wording is not given in the notes.

## Source
The client wants a consistent look defined once in `docs/design.md`, built with Tailwind, with one set of shared components (Button, TextField, Card, Badge, Spinner, EmptyState, ErrorMessage). It must work on a 360px phone first and be keyboard accessible. Dark mode, icons and animations are not wanted now.
