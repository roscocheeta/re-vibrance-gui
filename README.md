# ReVibranceGUI

ReVibranceGUI is a lightweight, modern Windows desktop utility that automates NVIDIA, AMD, and Intel GPU vibrance (digital saturation) settings based on the currently active application.

This project is a complete C# WPF / .NET 8 architectural rewrite of the original [vibranceGUI](https://github.com/juvander/vibranceGUI) tool, replacing legacy Windows Forms components with modern APIs, robust background polling, and intelligent game-detection heuristics.

> [!WARNING]
> **Experimental Hardware Support:** While ReVibranceGUI natively supports NVIDIA, AMD, and Intel GPUs, it has currently only been actively tested on NVIDIA hardware. AMD and Intel integrations are experimental and may not work exactly as intended. Bug reports are welcome!

## ✨ Features

- **Per-Process Customization:** Configure specific vibrance levels (50% - 100%) for individual games.
- **Smart Game Detection:** Automatically scans Steam, Epic Games, GOG, and Blizzard Battle.net manifests to locate your game executables effortlessly.
- **Dynamic Icons:** Extracts and mounts native `.exe` icons into your UI's Game Cards for a rich visual experience.
- **Multi-GPU Support:** Supports NVIDIA (via NVAPI), AMD (via ADL), and Intel (via IGCL) graphics cards natively.
- **Hardware Integration:** The UI directly queries and visualizes your detected graphics adapter state.
- **Non-Intrusive:** Minimizes silently to the Windows System Tray and runs securely on Windows Startup without UAC prompts.
- **Silent Boot:** Launch completely invisible in the background on Windows startup when configured to minimize to tray, bypassing the main window entirely.
- **Auto-Start Monitoring:** Choose whether to automatically begin monitoring games when ReVibranceGUI launches.
- **System Tray Controls:** Right-click the system tray icon to pause, resume, or exit the application.
- **Global Pause Hotkey:** Easily pause and resume vibrance adjustments using a custom global keyboard shortcut.
- **Auto-Update Notifications:** Automatically notifies you when a new version of ReVibranceGUI is available.

## 🚀 Architecture Improvements (vs Original)
1. **Removed `SetWinEventHook`:** Replaced the legacy global Windows hook with a lightweight 500ms `Task.Delay` background poller using `GetForegroundWindow`. This improves stability and reduces the risk of being flagged by aggressive kernel-level anti-cheat mechanisms that monitor global hooks.
2. **Modern State Persistence:** Decoupled the old configuration files into a clean `%AppData%\ReVibranceGUI\settings.json` serialized state model.
3. **Upgraded UI/UX:** Migrated from `WinForms` to a fully responsive WPF layout featuring Auto, Light, and Dark themes.
4. **Unified Math Engine:** NVIDIA, AMD, and Intel APIs scale vibrance differently. The core `VibranceMath` engine normalizes them all into a unified, predictable `0%` to `100%` scale for the UI.
5. **Multi-Monitor Awareness:** Allows you to target vibrance changes strictly to your *Primary* monitor, or broadcast them across *All* connected monitors.

## 🚀 Usage Requirements

To run ReVibranceGUI, you will need:
- **OS:** Windows 10 or Windows 11
- **Runtime:** [.NET 8.0 Desktop Runtime (v8.0.x)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) 

Download the latest `ReVibranceGUI.exe` from the [Releases](https://github.com/roscocheeta/re-vibrance-gui/releases) page and run it.

## 🛠️ Development & Building

### Build Requirements
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
