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

## Improving the UI experience

To update or improve the look and feel of the UI, use the `ui-ux-pro-max` skill. Install it once (this is a developer tool, not a project dependency):

```bash
npx skills add https://github.com/nextlevelbuilder/ui-ux-pro-max-skill --skill ui-ux-pro-max
```

Its suggestions must still follow the rules above: `docs/design.md` and the shared design tokens win over any style the skill proposes. Review the skill's source before installing it.
