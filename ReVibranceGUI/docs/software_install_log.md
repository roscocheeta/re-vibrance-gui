# Software Installation Log

This document tracks all software, dependencies, and tools installed as part of the `ReVibranceGUI` modernization project. Use this list if you need to uninstall components to clean up your system.

## Global Software
*   **.NET 8.0 Desktop SDK**: Required to build and run the modern WPF application. (Failed via `winget`, user needs to install manually).

## Project Dependencies (NuGet Packages)
*   **NvAPIWrapper.Net (v0.8.1.101)**: A .NET wrapper for NVIDIA's NVAPI, used to replace the custom C++ `vibranceDLL.dll`. This is restored automatically by the `.NET` SDK when building the project and does not install globally on the system (cached in `%USERPROFILE%\.nuget\packages`).
