#pragma warning disable CS8600, CS8604, CS8603, CS8601
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ReVibranceGUI.Scanners
{
    public class SteamScanner : IGameScanner
    {
        // Simple mapping for common games to their known exe names since Steam manifests don't provide the exe
        private readonly Dictionary<string, string> _knownExecutables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Counter-Strike 2", "cs2.exe" },
            { "Counter-Strike: Global Offensive", "csgo.exe" },
            { "Apex Legends", "r5apex.exe" },
            { "Dota 2", "dota2.exe" },
            { "Team Fortress 2", "hl2.exe" },
            { "PUBG: BATTLEGROUNDS", "TslGame.exe" },
            { "Rust", "RustClient.exe" },
            { "Tom Clancy's Rainbow Six Siege", "RainbowSix.exe" },
            { "Grand Theft Auto V", "GTA5.exe" }
        };

        public IEnumerable<GameInstall> Scan()
        {
            var games = new List<GameInstall>();
            string steamPath = GetSteamPath();
            
            if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath))
            {
                return games;
            }

            var libraryFolders = GetLibraryFolders(steamPath);
            foreach (var libFolder in libraryFolders)
            {
                var steamapps = Path.Combine(libFolder, "steamapps");
                if (!Directory.Exists(steamapps)) continue;

                var manifests = Directory.GetFiles(steamapps, "appmanifest_*.acf");
                foreach (var manifest in manifests)
                {
                    var game = ParseManifest(manifest);
                    if (game != null)
                    {
                        // Fallback: If we don't know the exact exe, we just provide the directory name.
                        // Ideally we'd search the directory for an exe, but there can be multiple (crash handlers, etc).
                        if (string.IsNullOrEmpty(game.ExeName))
                        {
                            string fullPath = FindPrimaryExecutable(game.InstallPath);
                            if (!string.IsNullOrEmpty(fullPath))
                            {
                                game.ExePath = fullPath;
                                game.ExeName = Path.GetFileName(fullPath);
                            }
                            else
                            {
                                game.ExeName = "unknown.exe";
                            }
                        }
                        else
                        {
                            game.ExePath = Path.Combine(game.InstallPath, game.ExeName);
                        }
                        
                        games.Add(game);
                    }
                }
            }

            return games;
        }

        private string GetSteamPath()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (key != null)
                    {
                        return key.GetValue("SteamPath")?.ToString()?.Replace('/', '\\');
                    }
                }
            }
            catch
            {
                // Ignore registry errors
            }
            return null;
        }

        private List<string> GetLibraryFolders(string steamPath)
        {
            var folders = new List<string> { steamPath }; // Main install dir is always a library
            string vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            
            if (File.Exists(vdfPath))
            {
                try
                {
                    string content = File.ReadAllText(vdfPath);
                    // Match "path"		"D:\\SteamLibrary"
                    var matches = Regex.Matches(content, "\"path\"\\s+\"([^\"]+)\"");
                    foreach (Match match in matches)
                    {
                        if (match.Success && match.Groups.Count > 1)
                        {
                            string path = match.Groups[1].Value.Replace("\\\\", "\\");
                            if (!string.Equals(path, steamPath, StringComparison.OrdinalIgnoreCase))
                            {
                                folders.Add(path);
                            }
                        }
                    }
                }
                catch { /* Ignore parsing errors */ }
            }

            return folders;
        }

        private GameInstall ParseManifest(string manifestPath)
        {
            try
            {
                string content = File.ReadAllText(manifestPath);
                
                string name = Regex.Match(content, "\"name\"\\s+\"([^\"]+)\"").Groups[1].Value;
                string installDir = Regex.Match(content, "\"installdir\"\\s+\"([^\"]+)\"").Groups[1].Value;

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(installDir))
                {
                    string steamappsPath = Path.GetDirectoryName(manifestPath);
                    string fullInstallPath = Path.Combine(steamappsPath, "common", installDir);

                    string exeName = "";
                    if (_knownExecutables.TryGetValue(name, out string knownExe))
                    {
                        exeName = knownExe;
                    }

                    return new GameInstall
                    {
                        Platform = "Steam",
                        Name = name,
                        InstallPath = fullInstallPath,
                        ExeName = exeName
                    };
                }
            }
            catch { /* Ignore manifest read errors */ }

            return null;
        }

        private string FindPrimaryExecutable(string installPath)
        {
            if (!Directory.Exists(installPath)) return null;

            try
            {
                // Search recursively for exes, as many games store exes in subfolders (e.g. Binaries/Win64)
                var exes = Directory.GetFiles(installPath, "*.exe", SearchOption.AllDirectories);
                if (exes.Length > 0)
                {
                    string bestMatch = null;
                    long largestSize = 0;

                    foreach(var exe in exes)
                    {
                        string name = Path.GetFileName(exe).ToLower();
                        
                        if (name.Contains("crash") || name.Contains("dxsetup") || 
                            name.Contains("vcredist") || name.Contains("unins") || 
                            name.Contains("launcher") || name.Contains("battleye") ||
                            name.Contains("eadesktop") || name.Contains("anticheat") ||
                            name.Contains("installer") || name.Contains("setup") || 
                            name.Contains("tool") || name.Contains("overlay"))
                        {
                            continue;
                        }

                        // Heuristic: The main game exe is usually the largest one
                        try 
                        {
                            long size = new FileInfo(exe).Length;
                            if (size > largestSize)
                            {
                                largestSize = size;
                                bestMatch = exe;
                            }
                        }
                        catch { }
                    }
                    
                    return bestMatch ?? exes[0];
                }
            }
            catch { }
            return null;
        }
    }
}
