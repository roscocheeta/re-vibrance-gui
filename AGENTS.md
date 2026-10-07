# General Workflow Rules

## Git Workflows

- **STRICT RULE**: Do NOT automatically push commits to any remote branch (e.g., `dev` or `main`).
- Commit changes locally to batch them up. This saves GitHub Action CI minutes.
- Only push to a remote repository when the user EXPLICITLY requests or authorizes a push.
