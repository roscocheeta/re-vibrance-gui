using System.Diagnostics;
using System.IO;

namespace ReVibranceGUI.Services
{
    /// <summary>
    /// Minimal thread-safe file logger. Writes to %AppData%\ReVibranceGUI\logs\app.log,
    /// rolling to app.log.1 once the file exceeds 1 MB. Never throws.
    /// </summary>
    public static class Logger
    {
        private const long MaxBytes = 1024 * 1024;
        private static readonly object Sync = new();
        private static string _logFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ReVibranceGUI", "logs", "app.log");

        /// <summary>Overrides the log location (used by tests).</summary>
        internal static void SetLogFile(string path) => _logFile = path;

        public static void Info(string message) => Write("INFO", message, null);
        public static void Warn(string message, Exception? ex = null) => Write("WARN", message, ex);
        public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

        private static void Write(string level, string message, Exception? ex)
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
            if (ex != null) line += $" | {ex.GetType().Name}: {ex.Message}";
            Debug.WriteLine(line);

            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_logFile)!);
                    var info = new FileInfo(_logFile);
                    if (info.Exists && info.Length > MaxBytes)
                    {
                        File.Move(_logFile, _logFile + ".1", overwrite: true);
                    }
                    File.AppendAllText(_logFile, line + Environment.NewLine);
                }
            }
            catch
            {
                // Logging must never crash the app.
            }
        }
    }
}
