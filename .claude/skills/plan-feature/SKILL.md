---
name: plan-feature
description: Creates a development plan for an issue using the planner agent and asks the user to approve it. Use before implementing any feature.
argument-hint: <issue number or path to an issue file>
disable-model-invocation: true
---

Create and approve a development plan.

The issue is: $ARGUMENTS

## Steps

1. If nothing was given, list the files in `docs/issues/` and ask which issue to plan. Stop and wait for the answer.
2. Find the issue:
   - If it is a file path, use that file.
   - If it is a number, look for a local file in `docs/issues/` that mentions that number at the top. If there is none, fetch the issue from GitHub with the GitHub tools (repository from `git remote get-url origin`) and save it as `docs/issues/<number>-<slug>.md`, with the number and URL at the top.
   - If you cannot find it, say so and stop.
3. Use the `planner` agent on that issue file.
4. Read the plan it wrote in `docs/plans/`.
5. Show the user a short summary: the goal, the endpoints (method, route, auth), the data changes, how many tests are planned, and every open question or dependency.
6. Ask: "Approve this plan?" Wait for a clear answer.
   - If the user wants changes, send them to the `planner` agent and repeat from step 4.
   - If the user says yes, change the line `Status: Draft` in the plan file to `Status: Approved` and tell the user the plan path.

## Rules

- Never write application code in this workflow.
- Never mark a plan Approved without a clear yes.
- Do not rewrite the plan yourself. Changes go through the `planner` agent.
- If the plan has open questions that block implementation, or depends on something that does not exist yet, do not ask for approval. List the questions and the missing dependency, and ask the user how to proceed.
