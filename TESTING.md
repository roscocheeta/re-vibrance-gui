# Testing Strategy

ReVibranceGUI uses `xUnit` for its automated testing framework. 

As a best practice, our test methods are named descriptively to act as self-documenting code. You can browse the `ReVibranceGUI.Tests` project to see exactly what scenarios are verified.

## What is Covered

We focus our automated testing on the core, non-UI business logic to ensure stability across driver and framework updates:

1. **Vibrance Math Engines:** 
   - We verify that the normalization algorithms correctly convert NVIDIA, AMD, and Intel proprietary saturation ranges into our unified `0%` to `100%` scale.
2. **Game Scanners & Heuristics:**
   - We test the parsers for Steam (`.acf`), Epic Games (`.item`), and other manifests to ensure they correctly extract game titles and executable paths.
3. **State Management & Persistence:**
   - We verify that the `SettingsManager` correctly serializes and deserializes the JSON configuration without data loss.
4. **Startup Management:**
   - We ensure the registry modification wrappers correctly enable or disable the run-on-startup behavior.

## What is NOT Covered

1. **WPF UI Integration:** 
   - We do not currently use UI automation frameworks to test window rendering or button clicks due to the complexity and fragility of such tests. UI verification is handled manually.
2. **Native GPU API Calls:**
   - We mock the actual `NvAPIWrapper` and ADL P/Invoke calls in our tests so that they can be run on any machine, even if it does not have the corresponding GPU hardware installed. We do not run integration tests directly against the GPU driver in CI.

## How to Run Tests

To run the test suite locally from the command line:

```bash
dotnet test ReVibranceGUI.sln
```

Alternatively, you can run them directly within Visual Studio using the built-in Test Explorer.
