# AI Agent Instructions (AGENTS.md)

> **Note to Developers:** This file contains system prompts and workflow rules designed for autonomous AI coding agents (like Google Antigravity, GitHub Copilot Workspace, or Devin) that work on this repository. If you fork this project and use AI tools, they should automatically read this file to adhere to the project's standards.

## General Workflow Rules
## Git Workflows

- **STRICT RULE**: Do NOT automatically push commits to any remote branch (e.g., `dev` or `main`).
- Commit changes locally to batch them up. This saves GitHub Action CI minutes.
- **NEW RULE**: All future work MUST be done on a feature branch branched off `dev` (e.g., `feature/xyz`).
- Do NOT commit directly to `dev`. 
- Use Pull Requests (PRs) to merge feature branches into `dev`. Ensure the `build-and-test` CI status check passes before merging.
- **Quality Assurance**: After completing any new feature or bug fix, ALWAYS review if tests need to be updated (locally and in CI) and revise the README.md if the changes affect user-facing functionality.
