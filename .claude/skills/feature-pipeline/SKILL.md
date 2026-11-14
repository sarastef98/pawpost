---
name: feature-pipeline
description: Runs one feature through the whole pipeline - issue, plan, implementation, tests, pull request and reviews - and resumes where it left off. Use to take a feature from meeting notes or an issue number to a reviewed PR.
argument-hint: <meeting notes file or issue number>
disable-model-invocation: true
---

Take one feature from start to a reviewed pull request.

The input is: $ARGUMENTS

This workflow does not repeat the steps of the other skills. For each stage, read that skill's `SKILL.md` and follow its steps exactly. Where that file says `$ARGUMENTS`, use the value you worked out for that stage and ignore the placeholder.

## Steps

1. If nothing was given, list the files in `docs/meeting-notes/` and the files in `docs/issues/` and ask which one to start from. Stop and wait.
2. Work out where to start:
   - A file in `docs/meeting-notes/` means stage 1.
   - An issue number or an issue file means the issue already exists, so start at stage 2.
3. Before each stage check whether it is already done and skip it if so:
   - Stage 1 is done when the issue file in `docs/issues/` has an issue number and URL at the top.
   - Stage 2 is done when `docs/plans/<number>-*.md` exists and says `Status: Approved`.
   - Stage 3 is done when the branch `feature/<number>-<slug>` exists and has commits beyond `main`.
   - Stage 4 is done when the PR exists and the last review has no Blocking or Should fix items.
4. Stage 1, issues: follow `.claude/skills/meeting-to-issue/SKILL.md`. If several issues were created, show them and ask which one to take through the pipeline now. Suggest an order if one issue depends on another (for example accounts before creating ads).
5. Stage 2, plan: follow `.claude/skills/plan-feature/SKILL.md` for that issue. This stage needs the user's approval. If the plan is not approved, stop and report why.
6. Stage 3, code and tests: follow `.claude/skills/implement-feature/SKILL.md` with the approved plan.
7. Stage 4, PR and reviews: follow `.claude/skills/review-pr/SKILL.md`. It runs automatically.
8. Finish with a short report: the issue link, the PR link, the result of the last review, and what is left for the user (reading the PR and merging it).

## Rules

- Stop and report, do not guess, whenever a stage stops. Say which stage stopped, why, and that the user can run `/feature-pipeline <issue number>` again to resume.
- Never skip the user's yes before creating issues, and never skip plan approval.
- Never merge the PR and never approve it.
- Do not write code, tests or plans yourself. They come from the agents.
- Take one feature through at a time.
