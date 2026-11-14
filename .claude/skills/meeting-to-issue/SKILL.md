---
name: meeting-to-issue
description: Converts a client meeting notes file into GitHub issues using the issue-writer agent. Use when the user wants to turn meeting notes into issues.
argument-hint: <path to meeting notes file>
disable-model-invocation: true
---

Turn client meeting notes into GitHub issues.

The notes file is: $ARGUMENTS

## Steps

1. If no file was given, list the files in `docs/meeting-notes/` and ask which one to use. Stop and wait for the answer.
2. Check that the file exists. If not, say so and stop.
3. Use the `issue-writer` agent on that file.
4. Read the issue files it created in `docs/issues/`.
5. Show the user a short summary: for each issue, the title, the number of acceptance criteria, and the open questions.
6. Ask: "Create these issues on GitHub?" Wait for a clear yes. If the user wants changes, send them back to the `issue-writer` agent and repeat from step 4.
7. After a yes, find the repository from `git remote get-url origin`. For each issue, create it on GitHub with the GitHub tools: the title is the first heading, the body is the rest of the file, and the label is `feature` if it exists.
8. Add the issue number and URL to the top of each local issue file, then show the user the list of created issues with their links.

## Rules

- Never create anything on GitHub before the user says yes.
- Do not rewrite the issues yourself. Changes go through the `issue-writer` agent.
- Never create duplicates: if a local issue file already has an issue URL at the top, skip it and say so.
- Keep the summary short. The user can open the files for the full text.
