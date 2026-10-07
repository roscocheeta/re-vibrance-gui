#pragma warning disable CS8600, CS8604, CS8603, CS8601
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace ReVibranceGUI.Scanners
{
    public class GOGScanner : IGameScanner
    {
        public IEnumerable<GameInstall> Scan()
        {
            var games = new List<GameInstall>();
            
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games"))
                {
                    if (key != null)
                    {
                        foreach (string subkeyName in key.GetSubKeyNames())
                        {
                            using (RegistryKey gameKey = key.OpenSubKey(subkeyName))
                            {
                                if (gameKey != null)
                                {
                                    string gameName = gameKey.GetValue("GAMENAME")?.ToString();
                                    string path = gameKey.GetValue("PATH")?.ToString();
                                    string exeFile = gameKey.GetValue("EXEFILE")?.ToString();

                                    if (!string.IsNullOrEmpty(gameName) && !string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(exeFile))
                                    {
                                        string exeName = Path.GetFileName(exeFile);
                                        games.Add(new GameInstall
                                        {
                                            Platform = "GOG",
                                            Name = gameName,
                                            InstallPath = path,
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
    }
}
