# Testing & Verification Guide

ReVibranceGUI uses `xUnit` for its automated testing framework, paired with compile-time Roslyn analyzers and OS integration contract tests.

As a best practice, our test methods are named descriptively to act as self-documenting code. Browse the `ReVibranceGUI.Tests` project to inspect the exact test scenarios.

---

## 🧪 Test Architecture & Strategy

### 1. In-Memory Native Seams (Fast Unit Tests)
Instead of spinning up external drivers or relying on physical GPU hardware, the test suite leverages the `IVibranceProxy` interface abstraction.
- All unit tests run against clean in-memory fakes.
- The entire test suite executes in under **1 second** on local developer machines and CI runners with **0 external dependencies**.

### 2. OS Integration Contract Tests (`OsIntegrationContractTests.cs`)
To prevent silent integration failures at the Windows OS boundary, dedicated contract tests verify:
- **Windows Registry Transactions:** Verifies that user-mode HKCU registry operations succeed, read back correctly, and clean up safely.
- **Headless Runner Guards:** Guarantees that `DisplayManager` enumerations and current resolution queries fail gracefully without throwing unhandled exceptions in Session 0 headless CI environments.
- **Atomic File Persistence:** Tests that `SettingsManager` recovers gracefully from malformed/corrupted `settings.json` files and persists valid settings atomically via temporary swap files.
- **Process Path Normalization:** Tests that process names are cleanly extracted and normalized regardless of path slashes, case variations, or trailing extensions.

### 3. WPF MTA & Thread Safety
Running WPF tests across multi-threaded MTA xUnit runners can cause race conditions in Windows message pumps. To ensure 100% test reliability:
- `xunit.runner.json` enforces `"parallelizeTestCollections": false`.
- `ReVibranceGUI.Tests/AssemblyInfo.cs` configures `[assembly: CollectionBehavior(DisableTestParallelization = true)]`.

---

## 🔒 Compile-Time Security & Analyzers

Every build runs automated Roslyn analyzers to prevent security and binding regressions before code is ever committed:
- **WpfAnalyzers (`WPFxxxx`):** Catches XAML binding typos, invalid DataContext types, and UI memory leaks at compile time.
- **SecurityCodeScan (`SCSxxxx`):** Flags unsafe `Process.Start` calls, path traversal vulnerabilities, and insecure cryptographic usage.
- **.NET 8 `<NuGetAudit>`:** Evaluates direct and transitive dependencies against the GitHub Advisory Database during `dotnet build`.

---

## 🚀 Running Verification Commands

### 1. Execute Unit & Contract Tests
```bash
dotnet test ReVibranceGUI.sln --configuration Release --verbosity normal
```

### 2. Audit Dependencies for Vulnerabilities
```bash
dotnet list package --vulnerable --include-transitive
```

### 3. Verify Code Formatting
```bash
dotnet format ReVibranceGUI.sln --verify-no-changes
```
