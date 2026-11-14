# Workflow

One command takes a feature from client notes to a reviewed pull request:

    /feature-pipeline <meeting notes file or issue number>

## Stages

| Stage | Skill | Agent | Output |
|---|---|---|---|
| 1. Issue | `/meeting-to-issue` | `issue-writer` | GitHub issue and `docs/issues/*.md` |
| 2. Plan | `/plan-feature` | `planner` | `docs/plans/<issue>-<slug>.md` |
| 3. Code and tests | `/implement-feature` | `developer`, `test-writer` | commits on `feature/<issue>-<slug>` |
| 4. PR and reviews | `/review-pr` | `pr-reviewer`, `code-reviewer`, `review-fixer` | pull request with review comments and fixes |

## Where a person decides

- Creating the issues on GitHub (one yes).
- Approving the plan. A plan with open client questions is never approved.
- Merging the pull request.

Everything else is automatic: opening the PR, posting reviews, up to two fix rounds, pushing fixes to the feature branch.

## Safety

- Hooks block pushes to `main`, force-pushes, commits on `main`, edits to secrets and to applied migrations.
- Agents that review cannot edit files. Agents that write code cannot push.
- The pipeline stops and reports instead of guessing.

## Building blocks

- Agents: `.claude/agents/`
- Skills (workflows): `.claude/skills/`
- Rules: `CLAUDE.md` and `.claude/rules/`
- Hooks and permissions: `.claude/hooks/`, `.claude/settings.json`
- GitHub connection (MCP): `.mcp.json`
- Templates: `templates/`
