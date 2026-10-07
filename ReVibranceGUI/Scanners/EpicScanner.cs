#pragma warning disable CS8600, CS8604, CS8603, CS8601
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace ReVibranceGUI.Scanners
{
    public class EpicScanner : IGameScanner
    {
        public IEnumerable<GameInstall> Scan()
        {
            var games = new List<GameInstall>();
            string manifestPath = GetManifestPath();

            if (string.IsNullOrEmpty(manifestPath) || !Directory.Exists(manifestPath))
            {
                return games;
            }

            var itemFiles = Directory.GetFiles(manifestPath, "*.item");
            foreach (var file in itemFiles)
            {
                var game = ParseItemFile(file);
                if (game != null)
                {
                    games.Add(game);
                }
            }

            return games;
        }

        private string GetManifestPath()
        {
            try
            {
                // Try 64-bit hive first
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Epic Games\EpicGamesLauncher"))
                {
                    if (key?.GetValue("AppDataPath") is string appDataPath)
                    {
                        return Path.Combine(appDataPath, "Manifests");
                    }
                }

                // Try Current User hive
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Epic Games\EpicGamesLauncher"))
                {
                    if (key?.GetValue("AppDataPath") is string appDataPath)
                    {
                        return Path.Combine(appDataPath, "Data", "Manifests");
                    }
                }
            }
            catch { /* Ignore registry errors */ }

            // Fallback
            return @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests";
        }

        private GameInstall ParseItemFile(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;

                    // Epic items are somewhat messy. We need to check if it's an actual game and not a redistributable/DLC.
                    if (root.TryGetProperty("bIsApplication", out var isApp) && isApp.GetBoolean() == true)
                    {
                        string displayName = root.TryGetProperty("DisplayName", out var nameProp) ? nameProp.GetString() : "Unknown Epic Game";
                        string installLoc = root.TryGetProperty("InstallLocation", out var locProp) ? locProp.GetString() : "";
                        string exeName = root.TryGetProperty("LaunchExecutable", out var exeProp) ? exeProp.GetString() : "";

                        if (!string.IsNullOrEmpty(installLoc))
                        {
                            // LaunchExecutable is sometimes a relative path like "ShooterGame/Binaries/Win64/VALORANT-Win64-Shipping.exe"
                            // We just want the executable name.
                            if (!string.IsNullOrEmpty(exeName))
                            {
                                exeName = Path.GetFileName(exeName);
                            }

                            return new GameInstall
                            {
                                Platform = "Epic",
                                Name = displayName,
                                InstallPath = installLoc,
                                ExeName = exeName
                            };
                        }
                    }
                }
            }
            catch { /* Ignore parse errors */ }

            return null;
        }
    }
}
