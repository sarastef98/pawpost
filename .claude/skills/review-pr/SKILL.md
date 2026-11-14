---
name: review-pr
description: Opens a pull request for the current feature branch if needed, then runs the pr-reviewer and code-reviewer agents and offers to post the combined review. Use after /implement-feature.
argument-hint: <optional PR number>
disable-model-invocation: true
---

Open the pull request and review it.

The PR number, if given: $ARGUMENTS

## Steps

1. Run `git branch --show-current`. If it is `main`, stop and tell the user to switch to the feature branch.
2. Take the issue number from the branch name `feature/<number>-<slug>`. Find the issue file in `docs/issues/` and the plan in `docs/plans/<number>-*.md`. If either is missing, stop and say so.
3. Check whether a pull request already exists for this branch (GitHub tools, repository from `git remote get-url origin`). If a PR number was given, use that PR.
4. If no PR exists:
   a. Run `dotnet build`, `dotnet test` and `dotnet format --verify-no-changes`. Remember the real results.
   b. Show the user the commits (`git log main..HEAD --oneline`), the changed files, and the three results.
   c. Ask: "Push the branch and open a pull request?" Wait for a clear yes.
   d. After a yes: `git push -u origin <branch>`, then create the PR against `main`. The title is the issue title. The body follows `templates/pull-request.md`, includes `Closes #<number>`, and ticks a checklist box only if it is true according to the real results from step a.
5. Use the `pr-reviewer` agent with the issue file, the plan file and the branch name.
6. Use the `code-reviewer` agent with the branch name. Run both reviews at the same time if you can.
7. Show the user one combined review: the overall verdict, then Blocking, Should fix and Suggestions, with duplicates merged.
8. Ask: "Post this review as a comment on the PR?" On a clear yes, post it with the GitHub tools.
9. If there are Blocking items, tell the user to send them to the `developer` agent, and then run `/review-pr` again.

## Rules

- Never merge the PR and never approve it. The user decides.
- Never change code in this workflow. Fixes go through the `developer` agent.
- Never push or post anything before a clear yes.
- Never tick a checklist box that was not actually verified.
