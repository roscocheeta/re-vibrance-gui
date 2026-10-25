# Contributing to ReVibranceGUI

Thank you for your interest in contributing to ReVibranceGUI! This document outlines our code standards, security architecture, and contribution workflow.

---

## 🛠️ Development Setup

- **Operating System:** Windows 10 or Windows 11
- **SDK:** [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (`net8.0-windows`)
- **IDE:** Visual Studio 2022 (with .NET desktop development workload) or Visual Studio Code

### Clone & Build
```bash
git clone https://github.com/roscocheeta/re-vibrance-gui.git
cd re-vibrance-gui
git checkout dev
dotnet restore ReVibranceGUI.sln
dotnet build ReVibranceGUI.sln
```

---

## 🌿 Branching Policy

We follow a strict GitFlow branching model:
- **`dev`** is our active development branch. All feature branches must branch off and target `dev`.
- **`master`** is reserved for tagged production releases.
- Use descriptive branch names:
  - `feature/<name>`
  - `fix/<name>`
  - `chore/<name>`
  - `docs/<name>`

---

## 🔒 Security & Code Standards

ReVibranceGUI enforces strict compile-time Roslyn analyzers and security audits:

1. **Zero Warnings Policy:** `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` is enabled across the solution.
2. **WpfAnalyzers:** Ensures all XAML bindings, dependency properties, and data contexts are valid and leak-free.
3. **SecurityCodeScan:** Protects against insecure process launching, path traversals, and improper input handling.
4. **URL Whitelisting:** Any hyperlink or shell navigation via `Process.Start` must enforce `Uri.UriSchemeHttps` or `Uri.UriSchemeHttp`.
5. **Path Quoting:** All registry entries and process invocations must quote paths containing spaces (CWE-428).

---

## 🧪 Pre-Submission Checklist

Before creating a Pull Request, verify that all checks pass locally:

```bash
# 1. Run unit & contract tests
dotnet test ReVibranceGUI.sln --configuration Release

# 2. Check for vulnerable dependencies
dotnet list package --vulnerable --include-transitive

# 3. Verify code formatting
dotnet format ReVibranceGUI.sln --verify-no-changes
```

Once all checks pass, push your branch and open a Pull Request targeting `dev`. CI will run automated builds, tests, Gitleaks secret scans, and CodeQL security analysis.
