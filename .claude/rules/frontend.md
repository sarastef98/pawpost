---
paths:
  - "PawsPost.Web/src/**/*.ts"
  - "PawsPost.Web/src/**/*.tsx"
---

# Frontend rules

- TypeScript strict. No `any`. Fix type errors, don't silence them.
- Function components and hooks. One component per file, `PascalCase` file and component names.
- Organize by feature: `src/features/<feature>/` holds that feature's components, hooks and tests, mirroring the backend feature names.
- Call the API only through the typed client in `src/api/client.ts`. Never write API types by hand. Never edit `src/api/schema.d.ts`. Regenerate it with `npm run gen:api`.
- Every screen that loads data handles three states: loading, empty and error.
- Forms validate on the client for convenience, but the server decides. Show the server's `ProblemDetails` errors to the user.
- Never use `dangerouslySetInnerHTML` with data from the API.
- No secrets in frontend code. Everything in `VITE_` variables is public.
- Never log tokens or personal data. Keep the login token in one auth module, nowhere else.
- Accessibility: every input has a label, buttons are real `button` elements, images have alt text.
