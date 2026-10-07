using ReVibranceGUI.AMD;
using ReVibranceGUI.Helpers;
using ReVibranceGUI.Models;
using ReVibranceGUI.Nvidia;
using ReVibranceGUI.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ReVibranceGUI
{
    public partial class MainWindow : Window
    {
        private static readonly SolidColorBrush NvidiaGreen = Freeze(System.Windows.Media.Color.FromRgb(0x76, 0xB9, 0x00));
        private static readonly SolidColorBrush AmdRed = Freeze(System.Windows.Media.Color.FromRgb(0xED, 0x1C, 0x24));
        private static readonly SolidColorBrush MixedBlue = Freeze(System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD7));
        private static readonly SolidColorBrush ActiveGreen = Freeze(System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50));
        private static readonly SolidColorBrush IdleGrey = Freeze(System.Windows.Media.Color.FromRgb(0x99, 0x99, 0x99));
        private static readonly SolidColorBrush StopButtonGrey = Freeze(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33));
        private static readonly SolidColorBrush StartButtonRed = Freeze(System.Windows.Media.Color.FromRgb(0xFF, 0x4B, 0x4B));

        private readonly ModernNvidiaVibranceProxy _nvidiaProxy;
        private readonly ModernAmdVibranceProxy _amdProxy;
        private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
        private readonly DispatcherTimer _saveDebounce;
        private VibranceAutomator? _automator;
        private bool _isInitializing = true;

        public ObservableCollection<GameProfile> TargetProcesses { get; } = new();

        private bool IsAutomatorRunning => _automator != null;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            // R4: save shortly after any change instead of only on close.
            _saveDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
            _saveDebounce.Tick += (_, _) => { _saveDebounce.Stop(); SaveSettings(); };

            _notifyIcon = CreateTrayIcon();

            StartupManager.RepairIfStale();
            RunOnStartupCheckBox.IsChecked = StartupManager.IsEnabled();

            _nvidiaProxy = new ModernNvidiaVibranceProxy();
            _amdProxy = new ModernAmdVibranceProxy();
            WindowsVibranceSlider.Value = InitializeHardwareFooter();

            LoadSettings();

            TargetProcesses.CollectionChanged += (_, _) => ScheduleSave();
            MinimizeToTrayCheckBox.Checked += (_, _) => ScheduleSave();
            MinimizeToTrayCheckBox.Unchecked += (_, _) => ScheduleSave();

            _isInitializing = false;
        }

        // ───────────────────────────── Setup ─────────────────────────────

        private System.Windows.Forms.NotifyIcon CreateTrayIcon()
        {
            var icon = new System.Windows.Forms.NotifyIcon
            {
                Text = "ReVibranceGUI",
                Visible = true
            };

            // C3: Assembly.Location is the .dll on .NET 8; use the real exe path.
            string? exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                icon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
            }

            icon.DoubleClick += (_, _) =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            };
            return icon;
        }

        /// <summary>Populates the footer and returns the current desktop vibrance level.</summary>
        private int InitializeHardwareFooter()
        {
            bool nv = _nvidiaProxy.IsInitialized;
            bool amd = _amdProxy.IsInitialized;

            if (nv && amd)
            {
                HardwareText.Text = $"{_nvidiaProxy.GetGpuNames()} & {_amdProxy.GetGpuNames()}";
                HardwareIcon.Fill = MixedBlue;
                return _nvidiaProxy.GetCurrentVibranceLevel();
            }
            if (nv)
            {
                HardwareText.Text = _nvidiaProxy.GetGpuNames();
                HardwareIcon.Fill = NvidiaGreen;
                return _nvidiaProxy.GetCurrentVibranceLevel();
            }
            if (amd)
            {
                HardwareText.Text = _amdProxy.GetGpuNames();
                HardwareIcon.Fill = AmdRed;
                return _amdProxy.GetCurrentVibranceLevel();
            }

            HardwareText.Text = "No Supported GPU Detected";
            ToggleAutomationButton.IsEnabled = false;
            Logger.Warn("No supported GPU detected.");
            return VibranceMath.UiMin;
        }

        // ──────────────────────────── Settings ───────────────────────────

        private void LoadSettings()
        {
            var settings = SettingsManager.Load();
            MinimizeToTrayCheckBox.IsChecked = settings.MinimizeToTray;

            foreach (var profile in settings.GameProfiles)
            {
                profile.IconImage = IconHelper.GetIcon(profile.ExePath);
                TargetProcesses.Add(profile);
            }

            switch (settings.Theme)
            {
                case "Light": ThemeLightBtn.IsChecked = true; break;
                case "Dark": ThemeDarkBtn.IsChecked = true; break;
                default: ThemeAutoBtn.IsChecked = true; break;
            }
        }

        private void ScheduleSave()
        {
            if (_isInitializing) return;
            _saveDebounce.Stop();
            _saveDebounce.Start();
        }

        private void SaveSettings()
        {
            SettingsManager.Save(new AppSettings
            {
                MinimizeToTray = MinimizeToTrayCheckBox.IsChecked == true,
                Theme = ThemeLightBtn.IsChecked == true ? "Light" : ThemeDarkBtn.IsChecked == true ? "Dark" : "Auto",
                GameProfiles = TargetProcesses.ToList()
            });
        }

        // ─────────────────────────── Lifecycle ───────────────────────────

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Minimized && MinimizeToTrayCheckBox.IsChecked == true)
            {
                Hide();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _saveDebounce.Stop();
            SaveSettings();

            _automator?.Stop();
            _automator = null;

            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            base.OnClosed(e);
        }

        // ──────────────────────────── Vibrance ───────────────────────────

        private void ApplyProfile(GameProfile? profile)
        {
            Dispatcher.InvokeAsync(() =>
            {
                int vibranceLevel = profile?.VibranceLevel ?? (int)WindowsVibranceSlider.Value;
                string targetDisplay = profile?.TargetDisplay ?? "All";

                if (_nvidiaProxy.IsInitialized) _nvidiaProxy.SetVibranceLevel(vibranceLevel, targetDisplay);
                if (_amdProxy.IsInitialized) _amdProxy.SetVibranceLevel(vibranceLevel, targetDisplay);

                if (profile != null && profile.ChangeResolution && profile.ResolutionWidth > 0 && profile.ResolutionHeight > 0)
                {
                    string? devName = targetDisplay == "Primary" ? DisplayManager.GetDisplays().FirstOrDefault(d => d.IsPrimary)?.DeviceName : null;
                    DisplayManager.SetResolution(devName, profile.ResolutionWidth, profile.ResolutionHeight, profile.RefreshRate);
                }
                else
                {
                    string? devName = targetDisplay == "Primary" ? DisplayManager.GetDisplays().FirstOrDefault(d => d.IsPrimary)?.DeviceName : null;
                    DisplayManager.RestoreResolution(devName);
                }
            });
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing || WindowsVibranceValue == null) return;

            int level = (int)WindowsVibranceSlider.Value;
            WindowsVibranceValue.Text = $"{level}%";

            if (_automator != null)
            {
                // Live preview is now driven by tick or apply
                ApplyProfile(null);
            }
            else
            {
                ApplyProfile(null); // Live desktop preview while not monitoring.
            }
        }

        private void GameSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            _automator?.UpdateProfiles(TargetProcesses.ToList());
            ScheduleSave();
        }

        private void Setting_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _automator?.UpdateProfiles(TargetProcesses.ToList());
            ScheduleSave();
        }

        private void ToggleAutomationButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsAutomatorRunning)
            {
                if (TargetProcesses.Count == 0) return;

                _automator = new VibranceAutomator(TargetProcesses.ToList(), ApplyProfile);
                _automator.Start();
                Logger.Info($"Monitoring started for {TargetProcesses.Count} game(s).");

                ToggleAutomationButton.Content = "STOP MONITORING";
                ToggleAutomationButton.Background = StopButtonGrey;
                MonitoringDot.Fill = ActiveGreen;
                MonitoringText.Text = "Active";
                AddGameButton.IsEnabled = false;
                ProcessListBox.IsEnabled = false;
            }
            else
            {
                _automator?.Stop();
                _automator = null;
                Logger.Info("Monitoring stopped.");

                ToggleAutomationButton.Content = "START MONITORING";
                ToggleAutomationButton.Background = StartButtonRed;
                MonitoringDot.Fill = IdleGrey;
                MonitoringText.Text = "Idle";
                AddGameButton.IsEnabled = true;
                ProcessListBox.IsEnabled = true;
            }
        }

        // ─────────────────────────── Game list ───────────────────────────

        private void AddProfile(GameProfile profile)
        {
            if (GameProfileFactory.ContainsExe(TargetProcesses, profile.ExeName)) return;
            TargetProcesses.Add(profile);
            _automator?.UpdateProfiles(TargetProcesses.ToList());
        }

        private void RemoveProcess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button { DataContext: GameProfile profile })
            {
                TargetProcesses.Remove(profile);
                _automator?.UpdateProfiles(TargetProcesses.ToList());
            }
        }

        private void AddGameButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button { ContextMenu: not null } btn)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private async void ScanGamesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            List<Scanners.GameInstall> games;
            try
            {
                games = await Task.Run(() =>
                {
                    var results = new List<Scanners.GameInstall>();
                    Scanners.IGameScanner[] scanners =
                    {
                        new Scanners.SteamScanner(),
                        new Scanners.EpicScanner(),
                        new Scanners.GOGScanner(),
                        new Scanners.BlizzardScanner()
                    };
                    foreach (var scanner in scanners)
                    {
                        try { results.AddRange(scanner.Scan()); }
                        catch (Exception ex) { Logger.Warn($"{scanner.GetType().Name} failed", ex); }
                    }
                    return results;
                });
            }
            catch (Exception ex)
            {
                Logger.Error("Game scan failed", ex);
                return;
            }

            var labelled = games
                .Where(g => g.ExeName != "unknown.exe")
                .Select(g => (Label: GameProfileFactory.FormatScanLabel(g.Platform, g.Name, g.ExeName), Game: g))
                .ToList();

            var window = new SelectionWindow("Scan Results", "Select a game to monitor:", labelled.Select(x => x.Label).ToList())
            {
                Owner = this
            };
            if (window.ShowDialog() != true || window.SelectedItem is not string selected) return;

            var match = labelled.FirstOrDefault(x => x.Label == selected).Game;
            string? exePath = null;
            if (match != null)
            {
                exePath = !string.IsNullOrEmpty(match.ExePath)
                    ? match.ExePath
                    : !string.IsNullOrEmpty(match.InstallPath) ? System.IO.Path.Combine(match.InstallPath, match.ExeName) : null;
            }

            AddProfile(GameProfileFactory.Create(selected, GameProfileFactory.ExtractExeName(selected), exePath));
        }

        private void RunningProcessesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var procMap = new Dictionary<string, string?>();

            foreach (var p in Process.GetProcesses())
            {
                using (p) // R1: dispose every Process handle.
                {
                    try
                    {
                        if (string.IsNullOrEmpty(p.MainWindowTitle)) continue;

                        string label = $"{p.MainWindowTitle} ({p.ProcessName}.exe)";
                        string? path = null;
                        try { path = p.MainModule?.FileName; } catch { /* Access denied for elevated/protected processes. */ }
                        procMap.TryAdd(label, path);
                    }
                    catch
                    {
                        // Process exited while enumerating.
                    }
                }
            }

            var sorted = procMap.Keys.OrderBy(k => k, StringComparer.CurrentCultureIgnoreCase).ToList();
            var window = new SelectionWindow("Running Processes", "Select a running process:", sorted) { Owner = this };
            if (window.ShowDialog() != true || window.SelectedItem is not string selected) return;

            procMap.TryGetValue(selected, out string? exePath);
            AddProfile(GameProfileFactory.Create(selected, GameProfileFactory.ExtractExeName(selected), exePath));
        }

        private void BrowseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".exe",
                Filter = "Executables (.exe)|*.exe"
            };
            if (dialog.ShowDialog() != true) return;

            string exeName = System.IO.Path.GetFileName(dialog.FileName);
            AddProfile(GameProfileFactory.Create($"[Manual] {exeName}", exeName, dialog.FileName));
        }

        // ──────────────────────────── Options ────────────────────────────

        private void RunOnStartupCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            try
            {
                StartupManager.Enable();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to enable run on startup", ex);
                System.Windows.MessageBox.Show($"Failed to set startup registry key: {ex.Message}");
            }
        }

        private void RunOnStartupCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            try
            {
                StartupManager.Disable();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to disable run on startup", ex);
                System.Windows.MessageBox.Show($"Failed to remove startup registry key: {ex.Message}");
            }
        }

        private void ThemeRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton { IsChecked: true, Content: string theme })
            {
                ApplyTheme(theme);
                ScheduleSave();
            }
        }

        private static void ApplyTheme(string theme)
        {
            bool isDark = true;
            if (theme == "Auto")
            {
                try
                {
                    using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                    if (key?.GetValue("AppsUseLightTheme") is int useLight)
                    {
                        isDark = useLight == 0;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn("Could not read Windows theme preference", ex);
                }
            }
            else
            {
                isDark = theme == "Dark";
            }

            if (System.Windows.Application.Current is App app)
            {
                app.Resources.MergedDictionaries.Clear();
                string themeUri = isDark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(themeUri, UriKind.Relative) });
            }
        }

        private static SolidColorBrush Freeze(System.Windows.Media.Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
