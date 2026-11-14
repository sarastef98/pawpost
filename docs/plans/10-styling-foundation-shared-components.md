# Plan: Set up styling foundation and shared components (#10)

Status: Approved
Layer: frontend

## Goal
`PawsPost.Web` has Tailwind CSS with all design tokens from `docs/design.md` defined in one place, and a small set of shared components (Button, TextField, Card, Badge, Spinner, EmptyState, ErrorMessage). Each component uses only tokens, works at 360px, is keyboard accessible with a visible focus outline, and has a test.

## Approach
- Use Tailwind CSS v4 with its Vite plugin. In v4 tokens are declared in CSS with an `@theme` block, so `src/index.css` gets one `@theme` block that is the only place colors, font, radius, spacing and font sizes are written. Tailwind turns each token into utility classes (for example `bg-primary`, `text-muted`, `rounded-card`). Components use only those utilities, never raw values like `#C2410C` or `text-[17px]`.
- Shared components live in `src/components/` (they are not tied to one feature, unlike `src/features/<feature>/`). One component per file, PascalCase, each with a `.test.tsx` next to it.
- Mobile first: default classes target 360px, no fixed widths, wider layouts are not needed in these components.
- Accessibility: real `button` element, `label` linked to input with `htmlFor`/`useId`, helper and error text linked with `aria-describedby`, `aria-invalid` on error, a shared focus-visible outline from the `primary` token, `role="status"` on Spinner, `role="alert"` on ErrorMessage.
- No component hard-codes a color or size. A simple check (a test or grep step) confirms no hex colors or arbitrary `[...]` values appear in `src/components`.

## API endpoints
None. This plan uses no API endpoints; the components are presentational only.

| Method | Route | Auth | Request | Response | Status codes |
|---|---|---|---|---|---|
| none | none | none | none | none | none |

## Screens and components
- Route: none (routing and the style guide page belong to `ui-layout-routing-frontend.md`, issue #11).
- Components (all in `src/components/`). Props are only what the issue lists plus standard `disabled` on Button. No Button sizes and no loading state (decision 2):
  - `Button`: real `button`; `variant`: `primary` | `secondary` (outlined) | `danger`; passes through standard button props (`type`, `onClick`, `disabled`, children). Default `type="button"`.
  - `TextField`: `label` (required), `helperText?`, `errorText?`, plus standard input props. Label above the input, helper or error text below; the error replaces the helper text and uses the `danger` color.
  - `Card`: surface container (white, 8px radius, soft shadow, border token) with children.
  - `Badge`: `variant`: `adoption` (success color, label "Adoption") | `sale` (primary-soft, label "Sale"); fully rounded.
  - `Spinner`: accessible loading indicator with a visually hidden text label ("Loading").
  - `EmptyState`: `message` prop (no default wording) and an optional single action (children slot for one Button).
  - `ErrorMessage`: `message` prop (no default wording), optional `onRetry` that shows a retry Button. Never renders error objects, status codes or stack traces.
- States: loading is the Spinner, empty is EmptyState, error is ErrorMessage. These are the building blocks; no data screen is built here.
- Forms and validation: TextField only displays `errorText` it is given. It does no validation itself.

Design tokens to define in `@theme` (values from `docs/design.md`):
- Colors: primary #C2410C, primary-hover #9A3412, primary-soft #FFEDD5, background #FFFBF5, surface #FFFFFF, text #292524, muted #57534E, border #E7E5E4, success #15803D, danger #B91C1C.
- Font: Inter first, then the system sans-serif stack. Inter is not loaded by this plan (decision 4). Sizes 14, 16, 20, 24, 32px. Headings semibold, body regular.
- Radius: 8px for cards and inputs, fully rounded for badges.
- Spacing: 4px base scale. Max content width 1100px as a token.
- Shadow: one soft shadow.

## Data changes
none

## Files to create or change
- `PawsPost.Web/package.json` and `package-lock.json`: add `tailwindcss` and `@tailwindcss/vite` (approved).
- `PawsPost.Web/vite.config.ts`: register the Tailwind Vite plugin.
- `PawsPost.Web/src/index.css`: add `@import "tailwindcss"`, the single `@theme` token block, base body styles (background, text color, font) and the shared focus-visible outline. Remove the Vite template variables and the dark-mode block that conflict with the design (dark mode is out of scope).
- `PawsPost.Web/src/components/Button.tsx` and `Button.test.tsx`
- `PawsPost.Web/src/components/TextField.tsx` and `TextField.test.tsx`
- `PawsPost.Web/src/components/Card.tsx` and `Card.test.tsx`
- `PawsPost.Web/src/components/Badge.tsx` and `Badge.test.tsx`
- `PawsPost.Web/src/components/Spinner.tsx` and `Spinner.test.tsx`
- `PawsPost.Web/src/components/EmptyState.tsx` and `EmptyState.test.tsx`
- `PawsPost.Web/src/components/ErrorMessage.tsx` and `ErrorMessage.test.tsx`
- `PawsPost.Web/src/App.css` and `src/App.tsx`: not changed unless the index.css cleanup breaks `App.test.tsx`. A rough-looking demo page is accepted until issue #11.

## Tests to write
Vitest, Testing Library, `user-event`, queries by role, label or text. No API mocking is needed. Test names are sentences (per `.claude/rules/frontend-testing.md`).
- Button: renders as a `button` role with its name; calls `onClick` when clicked; does not call it when disabled; can be activated with the keyboard (Tab to focus, Enter/Space); each variant renders (checked by role and name, not class names).
- TextField: label is linked so `getByLabelText` finds the input; typing updates the value; shows helper text; shows error text and marks the input `aria-invalid`; helper and error are linked with `aria-describedby`; can be focused by keyboard.
- Card: renders its children.
- Badge: shows the "Adoption" and "Sale" variants with their text.
- Spinner: exposes a status role with the "Loading" text.
- EmptyState: shows the message it is given and the action when given.
- ErrorMessage: shows the message it is given with an alert role; shows a retry button only when `onRetry` is given and calls it on click; does not show technical details (for example it never renders a raw error object or status code).
- Guard check: a step that greps `src/components` for hex colors and arbitrary-value classes (`[#`, `px]`), run manually or as a small test, to cover "no hard-coded colors or sizes".
- Existing `App.test.tsx` must still pass unchanged.
- Manual check at 360px width and keyboard-only walkthrough (Tab order, visible outline), noted in the PR. Contrast of text pairs is checked against the 4.5:1 rule.

## Steps
1. Add `tailwindcss` and `@tailwindcss/vite` and register the plugin in `vite.config.ts`. Confirm `npm run build` and `npm test` still pass.
2. Replace the template styles in `index.css` with the Tailwind import, the single `@theme` token block, base styles and the focus outline. Check `App.test.tsx` still passes.
3. Add `Button` with variants, plus its test.
4. Add `TextField` with label, helper and error text, plus its test.
5. Add `Card` and `Badge`, plus tests.
6. Add `Spinner`, `EmptyState` and `ErrorMessage`, plus tests.
7. Run the hard-coded value check, the 4.5:1 contrast check for each text and background pair (including primary-soft badge text and white on primary), `npm run lint`, `npm run format:check`, `npm run build` and `npm test`. Do the 360px and keyboard check and record it in the PR. If any contrast pair fails, stop and report to the user instead of changing design values.

## Decisions
Answered by the user:
1. Approved: new npm packages `tailwindcss` and `@tailwindcss/vite`.
2. No extra props: only what the issue lists plus standard `disabled`. No Button sizes, no loading state.
3. EmptyState and ErrorMessage take the message as a prop, with no default wording.
4. Font: system fallback stack with Inter first. No font package (no `@fontsource/inter`) and no Google Fonts link for now. `index.html` is not changed.
5. A temporarily rough demo `App.tsx` is acceptable until issue #11.
6. Badge labels are "Adoption" and "Sale".
7. Contrast: keep the 4.5:1 check in step 7. If a pair fails, stop and report to the user.
8. New tests use sentence names per `.claude/rules/frontend-testing.md`.

## Risks and open questions
- No open questions remain from the issue.
- Contrast risk: the Sale badge (primary text on primary-soft) and white text on primary may fail 4.5:1. Per decision 7, implementation stops and reports if so.
- Existing template styles: `src/index.css` and `src/App.css` are Vite starter styles. Removing the old variables from `index.css` may break `App.css` visuals; accepted per decision 5, but `App.test.tsx` must stay green.
- The style guide page that shows all components belongs to issue #11, so this plan verifies components through tests and manual checks only.
- Dependency: none on other issues. Issue #11 depends on this one.
