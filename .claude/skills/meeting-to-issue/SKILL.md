---
name: meeting-to-issue
description: Converts a client meeting notes file into GitHub issue drafts using the issue-writer agent. Use when the user wants to turn meeting notes into issues.
argument-hint: <path to meeting notes file>
disable-model-invocation: true
---

Turn client meeting notes into issue drafts.

The notes file is: $ARGUMENTS

## Steps

1. If no file was given, list the files in `docs/meeting-notes/` and ask which one to use. Stop and wait for the answer.
2. Check that the file exists. If not, say so and stop.
3. Use the `issue-writer` agent on that file.
4. Read the issue files it created in `docs/issues/`.
5. Show the user a short summary: for each issue, the title, the number of acceptance criteria, and the open questions.
6. Ask the user to review the drafts and tell you what to change. Do not publish anything to GitHub.

## Rules

- Do not rewrite the issues yourself. If changes are needed, send them back to the `issue-writer` agent.
- Keep the summary short. The user can open the files for the full text.
