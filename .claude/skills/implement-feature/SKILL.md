---
name: implement-feature
description: Implements an approved plan on a new feature branch using the developer agent. Use after /plan-feature has an approved plan.
argument-hint: <issue number or path to a plan file>
disable-model-invocation: true
---

Implement an approved plan.

The plan is: $ARGUMENTS

## Steps

1. If nothing was given, list the files in `docs/plans/` with their Status line and ask which plan to implement. Stop and wait.
2. Find the plan: a file path is used as is. A number means the file `docs/plans/<number>-*.md`. If none exists, tell the user to run `/plan-feature` first and stop.
3. Read the plan. If it does not say `Status: Approved`, stop and tell the user to approve it first.
4. Work out the branch name `feature/<issue-number>-<slug>` from the plan file name. If the file name has no issue number, use `feature/<slug>` and tell the user.
5. Run `git status --short`. If there are uncommitted changes outside `docs/`, stop and ask the user what to do. Uncommitted files in `docs/` (the issue and the plan) are fine, they come along to the new branch.
6. Create the branch from `main` with `git switch main` then `git switch -c <branch>`. If the branch already exists, stop and ask.
7. Commit the issue and plan files first: `docs: add issue and plan (#<number>)`.
8. Use the `developer` agent with the plan path.
9. Show the user: the commits (`git log main..HEAD --oneline`), the changed files (`git diff main --stat`), and anything the developer skipped or is blocked on.
10. Tell the user to review the changes, and that tests and the pull request come next. Do not push.

## Rules

- Never implement a plan that is not Approved.
- Never push and never open a PR in this workflow.
- Do not write code yourself. All code comes from the `developer` agent.
