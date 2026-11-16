---
name: review-pr
description: Opens a pull request for the current feature branch if needed, runs the pr-reviewer and code-reviewer agents, and sends findings to the review-fixer agent, for up to two fix rounds. Use after /implement-feature.
argument-hint: <optional PR number> [--ask]
disable-model-invocation: true
---

Open the pull request, review it, and fix the findings automatically.

The arguments: $ARGUMENTS

If the arguments contain `--ask`, ignore that word when looking for a PR number, and ask the user for a clear yes before steps 4c, 8 and 9d. Without `--ask`, do not ask for confirmation in those steps.

## Steps

1. Run `git branch --show-current`. If it is `main`, stop and tell the user to switch to the feature branch.
2. Take the issue number from the branch name `feature/<number>-<slug>`. Find the issue file in `docs/issues/` and the plan in `docs/plans/<number>-*.md`. If either is missing, stop and say so.
   Read the `Layer:` line of the plan. If it is missing, treat the plan as `backend` and tell the user. The layer decides which checks apply everywhere below, called "the layer checks":
   - backend: `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`.
   - frontend: inside `PawsPost.Web`: `npm run lint`, `npm run build` (it also type-checks), `npm test`, `npm run format:check`.
3. Check whether a pull request already exists for this branch (GitHub tools, repository from `git remote get-url origin`). If a PR number was given, use that PR.
4. If no PR exists:
   a. Run the layer checks. If any of them fails, stop and show the user the failure. Do not open a PR.
   b. Show the user the commits (`git log main..HEAD --oneline`), the changed files, and the results of the layer checks.
   c. Push the branch with `git push -u origin <branch>` and create the PR against `main`. The title is the issue title. The body follows `templates/pull-request.md`, includes `Closes #<number>`, and ticks a checklist box only if it is true according to the real results from step a.
5. Use the `pr-reviewer` agent with the issue file, the plan file and the branch name.
6. Use the `code-reviewer` agent with the branch name. Run both reviews at the same time if you can.
7. Merge the two reviews into one: the overall verdict, then Blocking, Should fix and Suggestions, with duplicates merged. The round number is 1 if `docs/reviews/<number>-round-1.md` does not exist, otherwise one more than the highest existing round. Save the merged review to `docs/reviews/<number>-round-<round>.md` (create the folder if needed) and show it to the user.
8. Post the merged review as a comment on the PR with the GitHub tools.
9. If there are Blocking or Should fix items:
   a. If two fix rounds have already been done, stop. Tell the user the remaining work needs a person.
   b. Use the `review-fixer` agent with the review file path and the plan path, so it knows the layer. Suggestions are not fixed unless the user asks.
   c. Show the user the fixer's reply and the new commits.
   d. If the fixer reported any finding as `not fixed` or `disagree`, or the test command of the layer (`dotnet test` or `npm test`) fails, or a finding needs a client decision, stop and report to the user. Otherwise run `git push` and go back to step 5 for the next round.
10. If there are no Blocking items left, tell the user the PR is ready for them to review and merge.

## Rules

- Never merge the PR and never approve it. The user decides.
- Never push to `main` and never force-push.
- Maximum two fix rounds.
- Never change code in this workflow yourself. Fixes come only from the `review-fixer` agent.
- Never tick a checklist box that was not actually verified.
- Do not commit the files in `docs/reviews/`.
