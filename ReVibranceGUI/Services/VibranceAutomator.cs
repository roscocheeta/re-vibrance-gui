using ReVibranceGUI.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ReVibranceGUI.Services
{
    public class VibranceAutomator
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private CancellationTokenSource _cts;
        private Task _monitorTask;
        
        // Maps lowercase process name (no .exe) to its custom vibrance level
        private Dictionary<string, int> _targetProcesses;
        private int _windowsVibranceLevel;
        private Action<int> _setVibranceAction;
        
        private string _currentlyAppliedProcess = null;

        public VibranceAutomator(List<GameProfile> targetProcesses, int windowsVibrance, Action<int> setVibranceAction)
        {
            _targetProcesses = new Dictionary<string, int>();
            foreach(var proc in targetProcesses)
            {
                string p = proc.ExeName.ToLowerInvariant();
                if (p.EndsWith(".exe")) p = p.Substring(0, p.Length - 4);
                
                if (!_targetProcesses.ContainsKey(p))
                {
                    _targetProcesses.Add(p, proc.VibranceLevel);
                }
            }
            
            _windowsVibranceLevel = windowsVibrance;
            _setVibranceAction = setVibranceAction;
        }

        public void Start()
        {
            if (_monitorTask != null && !_monitorTask.IsCompleted)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _monitorTask = Task.Run(() => MonitorLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _monitorTask?.Wait(1000); // Give it a second to exit cleanly
            
            if (_currentlyAppliedProcess != null)
            {
                _setVibranceAction(_windowsVibranceLevel);
                _currentlyAppliedProcess = null;
            }
        }

        public void UpdateLevels(int windowsVibrance)
        {
            _windowsVibranceLevel = windowsVibrance;
            
            // If we are currently on the desktop (no game applied), update immediately
            if (_currentlyAppliedProcess == null)
            {
                _setVibranceAction(_windowsVibranceLevel);
            }
        }
        
        public void UpdateProfiles(List<GameProfile> targetProcesses)
        {
            var newDict = new Dictionary<string, int>();
            foreach(var proc in targetProcesses)
            {
                string p = proc.ExeName.ToLowerInvariant();
                if (p.EndsWith(".exe")) p = p.Substring(0, p.Length - 4);
                
                if (!newDict.ContainsKey(p))
                {
                    newDict.Add(p, proc.VibranceLevel);
                }
            }
            
            _targetProcesses = newDict;
            
            // If the currently applied process changed its vibrance level, apply it
            if (_currentlyAppliedProcess != null && _targetProcesses.ContainsKey(_currentlyAppliedProcess))
            {
                _setVibranceAction(_targetProcesses[_currentlyAppliedProcess]);
            }
        }

        private async Task MonitorLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    IntPtr hwnd = GetForegroundWindow();
                    if (hwnd != IntPtr.Zero)
                    {
                        uint processId;
                        GetWindowThreadProcessId(hwnd, out processId);

                        if (processId != 0)
                        {
                            var process = Process.GetProcessById((int)processId);
                            string processName = process.ProcessName.ToLowerInvariant();

                            if (_targetProcesses.ContainsKey(processName))
                            {
                                // We are focused on a target game
                                if (_currentlyAppliedProcess != processName)
                                {
                                    _setVibranceAction(_targetProcesses[processName]);
                                    _currentlyAppliedProcess = processName;
                                }
                            }
                            else
                            {
                                // We are focused on something else (desktop/browser)
                                if (_currentlyAppliedProcess != null)
                                {
                                    _setVibranceAction(_windowsVibranceLevel);
                                    _currentlyAppliedProcess = null;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Ignore process read errors
                }

                await Task.Delay(500, token);
            }
        }
    }
}
