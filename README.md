# ReVibranceGUI

ReVibranceGUI is a lightweight, modern Windows desktop utility that automates NVIDIA and AMD GPU vibrance (digital saturation) settings based on the currently active application.

This project is a complete C# WPF / .NET 8 architectural rewrite of the original [vibranceGUI](https://github.com/juvander/vibranceGUI) tool, replacing legacy Windows Forms components with modern APIs, robust background polling, and intelligent game-detection heuristics.

## ✨ Features

- **Per-Process Customization:** Configure specific vibrance levels (50% - 100%) for individual games.
- **Smart Game Detection:** Automatically scans Steam, Epic Games, GOG, and Blizzard Battle.net manifests to locate your game executables effortlessly.
- **Dynamic Icons:** Extracts and mounts native `.exe` icons into your UI's Game Cards for a rich visual experience.
- **Multi-GPU Support:** Supports both NVIDIA (via NVAPI) and AMD (via ADL) graphics cards natively.
- **Hardware Integration:** The UI directly queries and visualizes your detected graphics adapter state.
- **Non-Intrusive:** Minimizes silently to the Windows System Tray and runs securely on Windows Startup without UAC prompts.

## 🚀 Architecture Improvements (vs Original)
1. **Removed `SetWinEventHook`:** Replaced the legacy global Windows hook with a lightweight 500ms `Task.Delay` background poller using `GetForegroundWindow`. This improves stability and reduces the risk of being flagged by aggressive kernel-level anti-cheat mechanisms that monitor global hooks.
2. **Modern State Persistence:** Decoupled the old configuration files into a clean `%AppData%\ReVibranceGUI\settings.json` serialized state model.
3. **Upgraded UI/UX:** Migrated from `WinForms` to a fully responsive, dark-mode-ready WPF layout.

## 🛠️ Development & Building

### Requirements
- **OS:** Windows 10/11
- **Framework:** .NET 8 SDK (`net8.0-windows`)

### Compiling
To build the project locally, run:
```bash
dotnet build ReVibranceGUI.sln --configuration Release
```
The executable will be located in `ReVibranceGUI/bin/Release/net8.0-windows/`.

### Testing
We use `xUnit` for regression testing on the internal settings engines and heuristic scanners.
```bash
dotnet test ReVibranceGUI.sln
```

## 📜 License
This project is a complete rewrite of the original `vibranceGUI` by juvander and SteffenCarlsen. Please note that the original upstream repositories do not contain an explicit open-source license. This rewrite is provided for educational and personal use.
