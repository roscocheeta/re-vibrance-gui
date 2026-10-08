using System.IO;
using System.Text.Json;
using ReVibranceGUI.Models;

namespace ReVibranceGUI.Services
{
    public class AppSettings
    {
        public bool MinimizeToTray { get; set; } = false;
        public string Theme { get; set; } = "Auto";
        public bool EnablePauseHotkey { get; set; } = false;
        public string PauseHotkey { get; set; } = string.Empty;
        public bool AutoStartMonitoring { get; set; } = false;
        public List<GameProfile> GameProfiles { get; set; } = new();
        // Note: the old global "IngameVibrance" field was removed (R5). System.Text.Json
        // ignores unknown properties by default, so older settings files still load.
    }

    public static class SettingsManager
    {
        private static readonly string[] ValidThemes = { "Auto", "Light", "Dark" };
        private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

        public static string DefaultPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ReVibranceGUI", "settings.json");

        public static AppSettings Load() => Load(DefaultPath);

        public static AppSettings Load(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    return Sanitize(settings);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load settings from {path}; using defaults", ex);
            }
            return new AppSettings();
        }

        public static void Save(AppSettings settings) => Save(settings, DefaultPath);

        /// <summary>
        /// Atomic save (R4): write to a temp file, then replace, so a crash mid-write
        /// can't leave a truncated/corrupt settings.json.
        /// </summary>
        public static void Save(AppSettings settings, string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                string tempPath = path + ".tmp";
                File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, WriteOptions));
                File.Move(tempPath, path, overwrite: true);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save settings to {path}", ex);
            }
        }

        /// <summary>
        /// Validates data from the user-writable settings file (S6): clamps levels,
        /// drops entries without an exe name, de-duplicates, normalizes the theme.
        /// </summary>
        internal static AppSettings Sanitize(AppSettings settings)
        {
            if (!ValidThemes.Contains(settings.Theme)) settings.Theme = "Auto";

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            settings.GameProfiles = (settings.GameProfiles ?? new())
                .Where(p => p != null && !string.IsNullOrWhiteSpace(p.ExeName))
                .Where(p => seen.Add(p.ExeName))
                .Select(p =>
                {
                    p.VibranceLevel = VibranceMath.ClampUi(p.VibranceLevel);
                    p.DisplayName ??= p.ExeName;
                    p.ExePath ??= string.Empty;
                    
                    // Cap string lengths
                    if (p.DisplayName.Length > 255) p.DisplayName = p.DisplayName.Substring(0, 255);
                    if (p.ExeName.Length > 255) p.ExeName = p.ExeName.Substring(0, 255);
                    if (p.ExePath.Length > 500) p.ExePath = p.ExePath.Substring(0, 500);

                    // Reject UNC paths to prevent NTLM credential leaks
                    if (p.ExePath.StartsWith(@"\\") || p.ExePath.StartsWith("//"))
                    {
                        p.ExePath = string.Empty;
                    }

                    // Validate TargetResolution format (e.g. 1920x1080 or 1920x1080@144Hz)
                    if (!string.IsNullOrWhiteSpace(p.TargetResolution))
                    {
                        if (p.TargetResolution.Length > 50 || !System.Text.RegularExpressions.Regex.IsMatch(p.TargetResolution, @"^\d{3,5}x\d{3,5}(@\d{1,3}Hz)?$"))
                        {
                            p.TargetResolution = string.Empty;
                        }
                    }

                    return p;
                })
                .ToList();

            return settings;
        }
    }
}
