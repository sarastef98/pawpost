---
paths:
  - "PawsPost.Web/src/**/*.tsx"
  - "PawsPost.Web/src/**/*.css"
---

# Design rules

- Follow `docs/design.md`. It is the source of truth for colors, type, spacing and tone.
- Never hard-code colors, font sizes or spacing in a component. Use the design tokens defined once in the styling foundation.
- Reuse the shared components (buttons, inputs, cards, badges). If one is missing, add it to the shared components, then use it. Do not create a one-off look in a single screen.
- Mobile first: every screen must work at 360px width before it is made wider.
- Every data screen has a designed loading, empty and error state.
- Text on screen is friendly and short. Errors never show technical details.
