using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace ReVibranceGUI.Scanners
{
    public class BlizzardScanner : IGameScanner
    {
        // Blizzard games map (Product Name -> Executable)
        private readonly Dictionary<string, string> _knownExecutables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "World of Warcraft", "Wow.exe" },
            { "World of Warcraft Classic", "WowClassic.exe" },
            { "Overwatch", "Overwatch.exe" },
            { "Diablo III", "Diablo III.exe" },
            { "Diablo IV", "Diablo IV.exe" },
            { "Diablo II: Resurrected", "D2R.exe" },
            { "Hearthstone", "Hearthstone.exe" },
            { "StarCraft II", "SC2_x64.exe" },
            { "StarCraft", "StarCraft.exe" },
            { "Heroes of the Storm", "Heroes of the Storm_x64.exe" },
            { "Call of Duty: Modern Warfare", "ModernWarfare.exe" },
            { "Call of Duty: Black Ops Cold War", "BlackOpsColdWar.exe" },
            { "Call of Duty: Vanguard", "Vanguard.exe" },
            { "Call of Duty: Modern Warfare II", "cod.exe" },
            { "Call of Duty: Modern Warfare III", "cod.exe" },
            { "Call of Duty: Warzone", "cod.exe" } // Usually shares cod.exe
        };

        public IEnumerable<GameInstall> Scan()
        {
            var games = new List<GameInstall>();

            try
            {
                // Blizzard registers games in Uninstall key
                string uninstallPath = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(uninstallPath))
                {
                    if (key != null)
                    {
                        foreach (string subkeyName in key.GetSubKeyNames())
                        {
                            using (RegistryKey subkey = key.OpenSubKey(subkeyName))
                            {
                                if (subkey != null)
                                {
                                    string publisher = subkey.GetValue("Publisher")?.ToString();
                                    string displayName = subkey.GetValue("DisplayName")?.ToString();
                                    string installLoc = subkey.GetValue("InstallLocation")?.ToString();

                                    if (publisher != null && (publisher.Contains("Blizzard Entertainment") || publisher.Contains("Activision")))
                                    {
                                        // Filter out Battle.net app itself
                                        if (displayName == "Battle.net" || string.IsNullOrEmpty(installLoc))
                                            continue;

                                        string exeName = "unknown.exe";
                                        
                                        // Use lookup table
                                        if (_knownExecutables.TryGetValue(displayName, out string knownExe))
                                        {
                                            exeName = knownExe;
                                        }
                                        else
                                        {
                                            // Fallback heuristic if unknown
                                            exeName = FindExecutable(installLoc) ?? "unknown.exe";
                                        }

                                        games.Add(new GameInstall
                                        {
                                            Platform = "Battle.net",
                                            Name = displayName,
                                            InstallPath = installLoc,
                                            ExeName = exeName
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { /* Ignore registry permission errors */ }

            return games;
        }

        private string FindExecutable(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    var files = Directory.GetFiles(path, "*.exe");
                    long largest = 0;
                    string best = null;
                    foreach(var f in files)
                    {
                        string name = Path.GetFileName(f).ToLower();
                        if (name.Contains("launcher") || name.Contains("crash") || name.Contains("error")) continue;
                        
                        long size = new FileInfo(f).Length;
                        if (size > largest)
                        {
                            largest = size;
                            best = Path.GetFileName(f);
                        }
                    }
                    return best;
                }
            }
            catch { }
            return null;
        }
    }
}
