# ReVibranceGUI Enhancements & Roadmap

This document tracks planned features, UI improvements, and technical enhancements to be implemented in future iterations of ReVibranceGUI.

## UI/UX Revamp
- [x] **Aesthetic Overhaul:** Redesign the main interface to be simple, minimal, and aesthetically pleasing. Move away from standard WPF controls to a modern look (e.g., dark mode by default, rounded corners, custom sliders).
- [x] **System Tray Integration:** Implement the ability to minimize the application to the Windows System Tray (notification area) so it is no longer visible on the main taskbar. Include a UI setting checkbox for "Minimize to system tray when monitoring".
- [x] **Run on Startup:** Add a UI checkbox and the associated Windows Registry/Startup Folder logic to allow the application to automatically launch silently when the user logs in.

## Game & Process Management
- [x] **Multiple Executables Support:** Allow the user to build a list of multiple target `.exe` files to monitor simultaneously, each potentially with their own vibrance profiles, rather than a single target process.
- [x] **Running Process Picker:** Add a button that opens a dialog showing all currently running processes on the system, allowing the user to select the target game `.exe` directly.
- [x] **Manual File Browser:** Add a "Browse..." button that opens a standard Windows File Explorer dialog so the user can manually navigate to and select a game's `.exe` file.
- [x] **Improved Game Scanner:** Enhance the automatic game scanner to more accurately parse manifests and identify the correct primary executable, avoiding `unknown.exe` fallbacks.
- [x] **Per-Process Vibrance Configurations:** Expand the data model to allow users to configure distinct vibrance levels for individual games (e.g., set Battlefield to 100%, but Counter-Strike to 80%). The automator will need to track which specific process is in focus and apply its mapped vibrance value rather than a global "In-Game" setting.
- [x] **Per-Process Resolution Switching:** Automatically switch the display resolution (and refresh rate) when a specific game is launched (e.g., automatically swap to 1280x960 144hz for CS2), and revert to desktop native resolution when the game is minimized or closed.
- [x] **Target Display Selection:** Add a setting to allow the vibrance (and resolution) changes to affect only the Primary Monitor, or let the user choose exactly which displays are affected, leaving other monitors untouched.
- [x] **Process Icons in List:** Extract and display the actual `.exe` or `.ico` icon for each game next to its name in the Monitored Games list, providing a much richer, more visual experience compared to raw text.
  - *Note: This will likely require increasing the default dimensions of the main GUI window to comfortably accommodate larger "Game Cards" (with icons and per-game sliders) in the monitored games area.*
- [x] **Active Process Indicator:** Add a minimal visual indicator directly on the game's profile card in the UI to signify when that specific `.exe` is actively "Detected running". This will provide clear feedback on which profile the automator has currently hooked into, independent of the global monitoring status.

## Quality of Life & Automation
- [x] **System Tray Context Menu:** Implement a right-click context menu for the Windows system tray icon to provide quick access to core application controls without opening the main window, including:
  - **Stop / Start Monitoring:** A toggle to quickly pause or resume the background game-detection loop.
  - **Exit App:** A direct option to completely close ReVibranceGUI.
- [x] **Global Hotkey for Pausing:** Implement a global keyboard hook to allow users to bind a custom keyboard shortcut to toggle the "Pause/Resume" state of the vibrance monitoring loop. (Modeled after similar capabilities in SteffenCarlsen's fork of the original VibranceGUI).
- [x] **Automatic Version Checker:** Query the GitHub API on application startup to check for new releases. Display a non-intrusive notification (e.g., a subtle "Update Available!" link in the footer) that takes the user to the GitHub Releases page.
- [x] **Auto-Start Monitoring on Startup:** Add a setting to automatically begin the monitoring loop when the application launches. This setting should logically depend on the "Run on Startup" setting being enabled. The UI/UX needs to reflect this dependency (e.g., indenting the checkbox or disabling it when "Run on Startup" is unchecked) to align with best practices.
