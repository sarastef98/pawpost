---
name: smoke-test
description: Runs the api-smoke-tester agent for the current feature branch and shows the results. Use after /implement-feature to check the feature in the real running app.
argument-hint: <issue number>
disable-model-invocation: true
---

Smoke-test the API for one feature.

The issue number is: $ARGUMENTS

## Steps

1. If no number was given, take it from the current branch name `feature/<number>-<slug>`. If you are on `main` and no number was given, ask which issue and stop.
2. Find the issue file in `docs/issues/` and the plan in `docs/plans/<number>-*.md`. If either is missing, stop and say so.
3. Run `dotnet build`. If it fails, stop and show the error.
4. Use the `api-smoke-tester` agent with the issue file and the plan file.
5. Show the user its table and totals.
6. If there are failures, tell the user which scenarios failed and suggest sending them to the `developer` agent. Do not fix anything yourself.

## Rules

- Do not change code in this workflow.
- Do not push or open pull requests in this workflow.
