using Microsoft.Win32;

namespace ReVibranceGUI.Services
{
    /// <summary>
    /// Manages the HKCU Run key entry for "Run on Startup".
    /// </summary>
    public static class StartupManager
    {
        private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "ReVibranceGUI";

        /// <summary>
        /// Builds the Run-key command. Fixes C3 (.NET 8 Assembly.Location returns the .dll,
        /// which Windows cannot launch) and S1 (unquoted paths with spaces, CWE-428).
        /// </summary>
        public static string BuildCommand(string exePath) => $"\"{exePath}\"";

        private static string? CurrentExePath => Environment.ProcessPath;

        public static bool IsEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) != null;
        }

        public static void Enable()
        {
            string? exe = CurrentExePath;
            if (string.IsNullOrEmpty(exe)) throw new InvalidOperationException("Unable to determine executable path.");

            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            key.SetValue(ValueName, BuildCommand(exe));
            Logger.Info($"Startup entry set: {BuildCommand(exe)}");
        }

        public static void Disable()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
            Logger.Info("Startup entry removed.");
        }

        /// <summary>
        /// If an entry exists but points somewhere stale (e.g. the old broken .dll path,
        /// or the app was moved), rewrite it to the current exe.
        /// </summary>
        public static void RepairIfStale()
        {
            try
            {
                string? exe = CurrentExePath;
                if (string.IsNullOrEmpty(exe)) return;

                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                if (key?.GetValue(ValueName) is string existing && existing != BuildCommand(exe))
                {
                    key.SetValue(ValueName, BuildCommand(exe));
                    Logger.Info($"Repaired stale startup entry '{existing}' -> '{BuildCommand(exe)}'");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Could not repair startup entry", ex);
            }
        }
    }
}
