# AI Agent Instructions (AGENTS.md)

> **Note to Developers:** This file contains system prompts and workflow rules designed for autonomous AI coding agents (such as Google Antigravity, GitHub Copilot Workspace, or Devin) that work on this repository. If you fork this project and use AI tools, they should automatically read this file to adhere to the project's standards.

---

## 🌲 Git Branching & Workflow Rules

- **Base Branch:** All new work MUST be done on a branch off `dev`. Standard prefixes:
  - `feature/` for new capabilities or enhancements.
  - `fix/` for bug fixes or patch regressions.
  - `chore/` for dependency updates or build tooling.
  - `docs/` for documentation changes.
- **Direct Pushes Forbidden:** NEVER commit or push directly to `dev` or `master`.
- **Local Commit Batching:** Batch commits locally before pushing feature branches to save GitHub Actions CI runner minutes.
- **Pull Request Protocol:** Always submit Pull Requests against `dev`. All automated CI checks (`build-and-test`, `gitleaks`, formatting, dependency audits) must pass green before merging.
- **Production Releases:** Releases are merged from `dev` to `master` and tagged using Semantic Versioning (`git tag -a vX.Y.Z`). Tagging triggers the automated release workflow.

---

## 🔒 Pre-PR Quality & Security Gates

Before submitting any Pull Request, every AI agent or developer MUST verify the following locally:

1. **100% Test Pass Rate:**
   ```bash
   dotnet test ReVibranceGUI.sln --configuration Release --verbosity normal
   ```
2. **Zero Analyzer Warnings:**
   - The build enforces `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
   - Code must produce 0 warnings from `WpfAnalyzers` (WPF rules) and `SecurityCodeScan` (SCS rules).
3. **Zero Vulnerable Dependencies:**
   ```bash
   dotnet list package --vulnerable --include-transitive
   ```
4. **Code Formatting Clean:**
   ```bash
   dotnet format ReVibranceGUI.sln --verify-no-changes
   ```
5. **Documentation Integrity:**
   - If user-facing functionality or requirements change, always update `README.md` and `TESTING.md`. Preserve existing unrelated comments and docstrings.

---

## 🛡️ Secure Coding Standards

- **Safe Process Execution:** Never pass unsanitized input to `Process.Start`. Always validate URI schemes (`http://` or `https://` only) before shell navigation.
- **Unquoted Paths (CWE-428):** Executable paths used in registry run keys or shell commands must always be explicitly wrapped in quotes.
- **Atomic File Writes:** State files (like `settings.json`) must be saved atomically via temporary files before replacing to avoid state corruption.
