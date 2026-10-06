# VibranceGUI Modernization Plan

## Current State & Issues
The original [vibranceGUI](https://github.com/juv/vibranceGUI) repository is an old .NET Framework 4.0 Windows Forms application. 
On Windows 11, users frequently report crashes, auto-start conflicts with `explorer.exe`, and "run only once" hanging instances. 

### Why it crashes on Windows 11:
1. **Custom C++ DLL for NVAPI:** The app relies on a compiled 32-bit `vibranceDLL.dll` to interface with NVIDIA's driver API (NVAPI). This DLL is outdated and can clash with modern NVIDIA drivers and Windows 11's Desktop Window Manager (DWM).
2. **Old .NET Framework:** It targets .NET Framework 4.0, which lacks modern High-DPI support, causing UI scaling issues on modern monitors, and can sometimes exhibit compatibility quirks on Windows 11.
3. **Low-level Window Hooking:** The app uses `WinEventHook` to detect when a game window is in focus. In Windows 11, aggressive background hooks can sometimes cause hanging or be blocked by security/anti-cheat measures.

## Proposed Architecture for `re-vibrance-gui`

To uplift the project, we should rebuild the core functionality using a modern C# framework. 

### 1. Framework: .NET 8 (or 9) WPF
* **Why:** WPF (Windows Presentation Foundation) is fully supported in .NET 8+, offers excellent High-DPI scaling out of the box, and provides a much cleaner UI separation (XAML) than WinForms. It is lightweight enough for a system-tray utility.
* **Alternative:** .NET 8 WinForms. Easier to direct-port the existing UI, but WPF is generally preferred for a "refreshed" look and feel.

### 2. Replacing the NVIDIA C++ DLL
* **Solution:** We can eliminate the fragile `vibranceDLL.dll` completely by using **[NvAPIWrapper](https://github.com/falahati/NvAPIWrapper)**. 
* **Details:** NvAPIWrapper is an open-source, actively maintained .NET wrapper for NVAPI. It exposes Display Control (including Digital Vibrance/Saturation) natively in C#, removing the need to maintain C++ code.

### 3. AMD Support (ADL)
* The original project uses manual P/Invoke calls (`AMD/vendor/adl32` and `adl64`). We can migrate these directly into the new .NET 8 project. Since .NET 8 handles 32-bit/64-bit interop much better (e.g., using `Environment.Is64BitProcess`), we can streamline the AMD logic without external dependencies.

### 4. Background Monitoring (Window Focus)
* Instead of aggressive global `SetWinEventHook`, we can use modern `UIAutomation` or a safer polling mechanism combined with `GetForegroundWindow` to detect when target applications (like CS2, Valorant) are active, applying the vibrance settings gracefully.

## Migration Steps

1. **Initialize a new .NET 8 WPF App** in the `re-vibrance-gui` directory.
2. **Add Dependencies:** Install `NvAPIWrapper` via NuGet for NVIDIA support.
3. **Port Logic:** 
   - Re-implement the `IVibranceProxy` interface.
   - Create `ModernNvidiaVibranceProxy` using `NvAPIWrapper`.
   - Copy and update the `AmdDynamicVibranceProxy` and ADL P/Invokes.
4. **Rebuild UI:** Recreate the simple main window and system tray icon using WPF (`NotifyIcon` equivalents in WPF).
5. **Port Settings:** Migrate the settings serializer to use modern `System.Text.Json` instead of the old XML/Registry approach.

---
Would you like me to go ahead and initialize the new .NET 8 WPF project and start porting the NVIDIA logic using NvAPIWrapper?
