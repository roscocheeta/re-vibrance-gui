# Automated Game Detection Research

To make ReVibranceGUI smarter, we can build a scanner that automatically detects installed games from major launchers. Instead of the user typing `cs2.exe`, they could select "Counter-Strike 2" from a populated list.

Here is a technical breakdown of how we can retrieve installed games programmatically in C# for the most common platforms.

---

## 1. Steam (Valve)
Steam is the easiest and most robust to parse, as it relies on plain text Key-Value (VDF/ACF) files.

**How to detect:**
1. **Find Steam:** Check the Registry at `HKEY_CURRENT_USER\Software\Valve\Steam` for the `SteamPath`.
2. **Find Libraries:** Read `[SteamPath]\steamapps\libraryfolders.vdf`. This file lists all alternate drives/folders where the user has installed Steam libraries.
3. **Parse Manifests:** For each library folder, scan the `steamapps\` directory for files matching `appmanifest_*.acf`.
4. **Extract Data:** Parse the `.acf` file (it's similar to JSON) to extract:
   - `name`: "Counter-Strike 2"
   - `installdir`: The folder name (e.g., "Counter-Strike Global Offensive")
   - The executable is usually within this folder, though Steam doesn't list the exact `.exe` name in the manifest. We would map known Steam AppIDs to their `.exe` names (e.g., AppID 730 -> `cs2.exe`).

## 2. Epic Games Store
Epic Games uses JSON manifests stored in a hidden system directory.

**How to detect:**
1. **Find Manifests:** Check registry `HKCU\Software\Epic Games\EpicGamesLauncher` -> `AppDataPath`. If not found, default to `C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests`.
2. **Parse Manifests:** Iterate over all `.item` files in this folder.
3. **Extract Data:** Read the JSON to find:
   - `DisplayName`: "Valorant"
   - `InstallLocation`: The physical path (e.g., `C:\Program Files\Epic Games\Valorant`)
   - `Executable`: Often provided in the JSON, allowing us to know exactly what process to watch.

## 3. EA App (Formerly Origin)
EA stores installation paths directly in the Windows Registry.

**How to detect:**
1. **Registry Scan:** Iterate through the subkeys of `HKEY_LOCAL_MACHINE\SOFTWARE\EA Games`.
2. **Extract Data:** Each subkey (e.g., `Battlefield 2042`) represents a game. Look for the `Install Dir` string value inside the key.
3. **Finding the EXE:** Similar to Steam, we would scan the `Install Dir` for `.exe` files, or maintain a lookup dictionary for popular EA games.

## 4. Battle.net (Blizzard)
Blizzard uses a mix of Registry keys and internal databases.

**How to detect:**
1. **Registry Method:** Check `HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Blizzard Entertainment\[GameName]`.
   - Read the `InstallPath` value.
2. **Finding the EXE:** Append the known executable name (e.g., `_retail_\Wow.exe` for World of Warcraft) to the install path.

---

## Proposed Architecture for ReVibranceGUI

We can implement this cleanly using the Strategy Pattern in C#.

```csharp
public class GameInstall
{
    public string Platform { get; set; } // "Steam", "Epic", etc.
    public string Name { get; set; } // "Counter-Strike 2"
    public string ExeName { get; set; } // "cs2.exe"
    public string InstallPath { get; set; }
}

public interface IGameScanner
{
    IEnumerable<GameInstall> Scan();
}
```

We would then create `SteamScanner`, `EpicScanner`, `EAScanner`, and `BlizzardScanner` implementing this interface. 

When the user opens the "Add Game" menu, we run all scanners asynchronously, aggregate the results into a single list, and let the user pick. Once picked, we pass the `ExeName` directly into the `VibranceAutomator` we built earlier!

Would you like me to go ahead and start writing the **Steam** and **Epic Games** scanners into our new project?
