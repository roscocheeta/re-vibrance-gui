using ReVibranceGUI.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ReVibranceGUI.Services
{
    /// <summary>
    /// Polls the foreground window every 500 ms and applies the matching per-game vibrance,
    /// reverting to the Windows (desktop) level when no monitored game is focused.
    /// </summary>
    public sealed class VibranceAutomator
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private readonly Action<int> _setVibranceAction;
        private readonly Func<string?> _getForegroundProcessName;
        private readonly TimeSpan _pollInterval;

        // R2: the vibrance action is invoked from both the UI thread (UpdateLevels/UpdateProfiles/Stop)
        // and the background loop. This lock serializes driver calls and state transitions.
        private readonly object _sync = new();

        // R2: replaced atomically (immutable snapshot), so readers never see a half-built dictionary.
        private volatile IReadOnlyDictionary<string, int> _targetProcesses;
        private volatile int _windowsVibranceLevel;
        private string? _currentlyAppliedProcess;

        private CancellationTokenSource? _cts;
        private Task? _monitorTask;

        public VibranceAutomator(IEnumerable<GameProfile> targetProcesses, int windowsVibrance, Action<int> setVibranceAction)
            : this(targetProcesses, windowsVibrance, setVibranceAction, GetForegroundProcessName, TimeSpan.FromMilliseconds(500))
        {
        }

        /// <summary>Test seam: inject the foreground-process lookup and poll interval.</summary>
        internal VibranceAutomator(
            IEnumerable<GameProfile> targetProcesses,
            int windowsVibrance,
            Action<int> setVibranceAction,
            Func<string?> getForegroundProcessName,
            TimeSpan pollInterval)
        {
            _targetProcesses = BuildMap(targetProcesses);
            _windowsVibranceLevel = windowsVibrance;
            _setVibranceAction = setVibranceAction;
            _getForegroundProcessName = getForegroundProcessName;
            _pollInterval = pollInterval;
        }

        public bool IsRunning => _monitorTask is { IsCompleted: false };

        /// <summary>"Game.EXE" / "game.exe" / "game" → "game" (matches Process.ProcessName).</summary>
        public static string NormalizeProcessName(string exeName)
        {
            string p = exeName.Trim().ToLowerInvariant();
            return p.EndsWith(".exe", StringComparison.Ordinal) ? p[..^4] : p;
        }

        internal static IReadOnlyDictionary<string, int> BuildMap(IEnumerable<GameProfile> profiles)
        {
            var map = new Dictionary<string, int>();
            foreach (var profile in profiles)
            {
                if (string.IsNullOrWhiteSpace(profile.ExeName)) continue;
                map.TryAdd(NormalizeProcessName(profile.ExeName), profile.VibranceLevel);
            }
            return map;
        }

        public void Start()
        {
            if (IsRunning) return;
            _cts = new CancellationTokenSource();
            _monitorTask = Task.Run(() => MonitorLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            try
            {
                // C4: MonitorLoop now swallows its own cancellation, but guard anyway so
                // a cancelled/faulted task can never throw onto the UI thread.
                _monitorTask?.Wait(TimeSpan.FromSeconds(1));
            }
            catch (AggregateException ex)
            {
                Logger.Warn("Monitor task ended with an error during stop", ex.InnerException);
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                _monitorTask = null;
            }

            lock (_sync)
            {
                if (_currentlyAppliedProcess != null)
                {
                    SafeApply(_windowsVibranceLevel);
                    _currentlyAppliedProcess = null;
                }
            }
        }

        public void UpdateLevels(int windowsVibrance)
        {
            _windowsVibranceLevel = windowsVibrance;
            lock (_sync)
            {
                if (_currentlyAppliedProcess == null)
                {
                    SafeApply(windowsVibrance);
                }
            }
        }

        public void UpdateProfiles(IEnumerable<GameProfile> targetProcesses)
        {
            _targetProcesses = BuildMap(targetProcesses);
            lock (_sync)
            {
                if (_currentlyAppliedProcess == null) return;

                if (_targetProcesses.TryGetValue(_currentlyAppliedProcess, out int level))
                {
                    SafeApply(level);
                }
                else
                {
                    // The focused game was removed from the list: revert to desktop level.
                    SafeApply(_windowsVibranceLevel);
                    _currentlyAppliedProcess = null;
                }
            }
        }

        /// <summary>One polling iteration. Internal so tests can drive it deterministically.</summary>
        internal void Tick()
        {
            string? processName = _getForegroundProcessName();
            if (processName == null) return;

            var targets = _targetProcesses;
            lock (_sync)
            {
                if (targets.TryGetValue(processName, out int level))
                {
                    if (_currentlyAppliedProcess != processName)
                    {
                        SafeApply(level);
                        _currentlyAppliedProcess = processName;
                    }
                }
                else if (_currentlyAppliedProcess != null)
                {
                    SafeApply(_windowsVibranceLevel);
                    _currentlyAppliedProcess = null;
                }
            }
        }

        private async Task MonitorLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        Tick();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Monitor tick failed", ex);
                    }

                    await Task.Delay(_pollInterval, token);
                }
            }
            catch (OperationCanceledException)
            {
                // C4: normal shutdown path; previously this escaped and made Stop() throw.
            }
        }

        private void SafeApply(int level)
        {
            try
            {
                _setVibranceAction(level);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to apply vibrance level {level}", ex);
            }
        }

        private static string? GetForegroundProcessName()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;

            GetWindowThreadProcessId(hwnd, out uint processId);
            if (processId == 0) return null;

            try
            {
                // R1: Process wraps an OS handle; dispose it (this runs twice a second).
                using var process = Process.GetProcessById((int)processId);
                return process.ProcessName.ToLowerInvariant();
            }
            catch (ArgumentException)
            {
                return null; // Process exited between the two calls.
            }
        }
    }
}
