using ReVibranceGUI.Helpers;
using ReVibranceGUI.Models;

namespace ReVibranceGUI.Services
{
    /// <summary>
    /// Single place that turns a picker selection into a <see cref="GameProfile"/> (B5).
    /// Previously this logic was copy-pasted across three MainWindow handlers.
    /// </summary>
    public static class GameProfileFactory
    {
        public const int DefaultVibrance = 100;

        /// <summary>
        /// Extracts the exe from a display label like "[Steam] Wardogs (wardogs.exe)".
        /// Returns the label unchanged if no trailing "(...)" is present.
        /// </summary>
        public static string ExtractExeName(string displayLabel)
        {
            int start = displayLabel.LastIndexOf('(');
            int end = displayLabel.LastIndexOf(')');
            return start != -1 && end > start
                ? displayLabel.Substring(start + 1, end - start - 1).Trim()
                : displayLabel.Trim();
        }

        public static string FormatScanLabel(string? platform, string name, string exeName)
        {
            string plat = string.IsNullOrEmpty(platform) ? "PC" : platform;
            return $"[{plat}] {name} ({exeName})";
        }

        /// <summary>Creates a profile. Icon extraction is skipped when <paramref name="loadIcon"/> is false (tests).</summary>
        public static GameProfile Create(string displayName, string exeName, string? exePath, bool loadIcon = true)
        {
            return new GameProfile
            {
                DisplayName = displayName,
                ExeName = exeName,
                ExePath = exePath ?? string.Empty,
                VibranceLevel = DefaultVibrance,
                IconImage = loadIcon ? IconHelper.GetIcon(exePath) : null
            };
        }

        /// <summary>True if a profile for the same executable already exists (case-insensitive).</summary>
        public static bool ContainsExe(IEnumerable<GameProfile> profiles, string exeName)
        {
            string normalized = VibranceAutomator.NormalizeProcessName(exeName);
            return profiles.Any(p => VibranceAutomator.NormalizeProcessName(p.ExeName) == normalized);
        }
    }
}
