# Client meeting, 14 Nov

- The app needs a consistent look, defined once in docs/design.md. All colors, fonts and spacing come from there.
- Use Tailwind CSS. The design tokens (colors, font, radius, spacing) are defined in one place only, and no screen hard-codes a color or size.
- Build a small set of shared components: Button (primary, secondary, danger), TextField (label, helper text, error text), Card, Badge (Adoption and Sale variants), Spinner, EmptyState and ErrorMessage.
- Build a page layout with a header (logo on the left, a place for navigation links on the right), a centered content area up to 1100px wide, and a simple footer.
- Add page routing with a home page and a /styleguide page. The style guide shows every component and variant on one page, so the team can review the design.
- Everything must work on a phone (360px wide) first, and look good on desktop.
- All controls work with the keyboard and show a visible focus outline.
- Not now: dark mode, icon library, animations.
