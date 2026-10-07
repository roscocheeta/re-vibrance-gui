using System.Diagnostics;
using System.Runtime.InteropServices;
using ReVibranceGUI.Models;

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

        private readonly Action<GameProfile?> _applyProfileAction;
        private readonly Func<string?> _getForegroundProcessName;
        private readonly TimeSpan _pollInterval;

        private readonly object _sync = new();

        private volatile IReadOnlyDictionary<string, GameProfile> _targetProcesses;
        private string? _currentlyAppliedProcess;

        private CancellationTokenSource? _cts;
        private Task? _monitorTask;

        public VibranceAutomator(IEnumerable<GameProfile> targetProcesses, Action<GameProfile?> applyProfileAction)
            : this(targetProcesses, applyProfileAction, GetForegroundProcessName, TimeSpan.FromMilliseconds(500))
        {
        }

        internal VibranceAutomator(
            IEnumerable<GameProfile> targetProcesses,
            Action<GameProfile?> applyProfileAction,
            Func<string?> getForegroundProcessName,
            TimeSpan pollInterval)
        {
            _targetProcesses = BuildMap(targetProcesses);
            _applyProfileAction = applyProfileAction;
            _getForegroundProcessName = getForegroundProcessName;
            _pollInterval = pollInterval;
        }

        public bool IsRunning => _monitorTask is { IsCompleted: false };

        public static string NormalizeProcessName(string exeName)
        {
            string p = exeName.Trim().ToLowerInvariant();
            return p.EndsWith(".exe", StringComparison.Ordinal) ? p[..^4] : p;
        }

        internal static IReadOnlyDictionary<string, GameProfile> BuildMap(IEnumerable<GameProfile> profiles)
        {
            var map = new Dictionary<string, GameProfile>();
            foreach (var profile in profiles)
            {
                if (string.IsNullOrWhiteSpace(profile.ExeName)) continue;
                map.TryAdd(NormalizeProcessName(profile.ExeName), profile);
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
                    SafeApply(null);
                    _currentlyAppliedProcess = null;
                }
            }
        }

        public void UpdateProfiles(IEnumerable<GameProfile> targetProcesses)
        {
            _targetProcesses = BuildMap(targetProcesses);
            lock (_sync)
            {
                if (_currentlyAppliedProcess == null) return;

                if (_targetProcesses.TryGetValue(_currentlyAppliedProcess, out var profile))
                {
                    SafeApply(profile);
                }
                else
                {
                    SafeApply(null);
                    _currentlyAppliedProcess = null;
                }
            }
        }

        internal void Tick()
        {
            string? processName = _getForegroundProcessName();
            if (processName == null) return;

            var targets = _targetProcesses;
            lock (_sync)
            {
                if (targets.TryGetValue(processName, out var profile))
                {
                    if (_currentlyAppliedProcess != processName)
                    {
                        SafeApply(profile);
                        _currentlyAppliedProcess = processName;
                    }
                }
                else if (_currentlyAppliedProcess != null)
                {
                    SafeApply(null);
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
            }
        }

        private void SafeApply(GameProfile? profile)
        {
            try
            {
                _applyProfileAction(profile);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to apply profile", ex);
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
