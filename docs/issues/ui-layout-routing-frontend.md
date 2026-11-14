Issue: #11
URL: https://github.com/sarastef98/pawpost/issues/11

# Add page layout, routing and style guide page

Layer: frontend
Depends on: #10

## Summary
Add a shared page layout (header, centered content, footer), page routing with a home page and a `/styleguide` page that shows every shared component and variant.

## User story
As a team member, I want one page that shows every component and variant inside a consistent layout, so that we can review the design together.

## Acceptance criteria
- [ ] A page layout wraps all pages with a header, a content area and a footer.
- [ ] The header has the logo on the left and a place for navigation links on the right.
- [ ] The content area is centered and at most 1100px wide.
- [ ] The footer is simple.
- [ ] The route `/` shows a home page.
- [ ] The route `/styleguide` shows a style guide page.
- [ ] The style guide shows every shared component and every variant on one page: Button (primary, secondary, danger), TextField (label, helper text, error text), Card, Badge (Adoption, Sale), Spinner, EmptyState and ErrorMessage.
- [ ] The layout works at 360px width first and looks good on desktop.
- [ ] Header links, and all other controls, work with the keyboard and show a visible focus outline.
- [ ] The Vite starter content in `App.tsx` is replaced by the layout and routes.
- [ ] Tests cover both routes rendering inside the layout.

## Out of scope
- Dark mode, icon library, animations.
- Real navigation links (they come with later features).
- Real home page content.

## Open questions
- Which routing library? A router is a new npm package and needs approval before it is added.
- What should the logo be (image or text)? The notes do not say.
- What should the home page show for now? The notes only say "a home page".
- What should the footer contain?
- Should `/styleguide` be available in production or only in development?

## Source
The client wants a page layout with a header (logo left, navigation links right), a centered content area up to 1100px, and a simple footer. Routing starts with a home page and a `/styleguide` page that shows every component and variant for design review. It must work on a 360px phone first and be keyboard accessible.
