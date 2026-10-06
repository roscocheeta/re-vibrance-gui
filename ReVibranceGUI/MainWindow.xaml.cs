using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ReVibranceGUI.Nvidia;
using ReVibranceGUI.AMD;

namespace ReVibranceGUI
{
    public partial class MainWindow : Window
    {
        private ModernNvidiaVibranceProxy _nvidiaProxy;
        private ModernAmdVibranceProxy _amdProxy;
        private VibranceAutomator _automator;
        private bool _isAutomatorRunning = false;
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private bool _isInitializing = true;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            
            // Setup System Tray Icon
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
            _notifyIcon.Text = "ReVibranceGUI";
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += (s, args) =>
            {
                this.Show();
                this.WindowState = WindowState.Normal;
            };

            // Check Startup Registry Key
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            if (key?.GetValue("ReVibranceGUI") != null)
            {
                RunOnStartupCheckBox.IsChecked = true;
            }

            _nvidiaProxy = new ModernNvidiaVibranceProxy();
            _amdProxy = new ModernAmdVibranceProxy();

            int initialVibrance = 0;
            var bc = new BrushConverter();
            
            if (_nvidiaProxy.IsInitialized && _amdProxy.IsInitialized)
            {
                HardwareText.Text = $"{_nvidiaProxy.GetGpuNames()} & {_amdProxy.GetGpuNames()}";
                HardwareIcon.Fill = (System.Windows.Media.Brush)bc.ConvertFrom("#0078D7"); // Windows Blue for Mixed
                initialVibrance = _nvidiaProxy.GetCurrentVibranceLevel(); // Default to Nvidia's level
            }
            else if (_nvidiaProxy.IsInitialized)
            {
                HardwareText.Text = _nvidiaProxy.GetGpuNames();
                HardwareIcon.Fill = (System.Windows.Media.Brush)bc.ConvertFrom("#76B900");
                initialVibrance = _nvidiaProxy.GetCurrentVibranceLevel();
            }
            else if (_amdProxy.IsInitialized)
            {
                HardwareText.Text = _amdProxy.GetGpuNames();
                HardwareIcon.Fill = (System.Windows.Media.Brush)bc.ConvertFrom("#ED1C24");
                initialVibrance = _amdProxy.GetCurrentVibranceLevel();
            }
            else
            {
                HardwareText.Text = "No Supported GPU Detected";
                ToggleAutomationButton.IsEnabled = false;
            }

            WindowsVibranceSlider.Value = initialVibrance;
            
            // Load settings
            var settings = SettingsManager.Load();
            MinimizeToTrayCheckBox.IsChecked = settings.MinimizeToTray;
            
            foreach(var p in settings.GameProfiles)
            {
                if (!string.IsNullOrEmpty(p.ExePath))
                {
                    p.IconImage = IconHelper.GetIcon(p.ExePath);
                }
                TargetProcesses.Add(p);
            }
            
            if (settings.Theme == "Light") ThemeLightBtn.IsChecked = true;
            else if (settings.Theme == "Dark") ThemeDarkBtn.IsChecked = true;
            else ThemeAutoBtn.IsChecked = true;
            
            _isInitializing = false;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            
            if (this.WindowState == WindowState.Minimized && MinimizeToTrayCheckBox.IsChecked == true)
            {
                this.Hide();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            var settings = new AppSettings();
            settings.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked == true;
            settings.Theme = ThemeLightBtn.IsChecked == true ? "Light" : (ThemeDarkBtn.IsChecked == true ? "Dark" : "Auto");
            foreach(var p in TargetProcesses)
            {
                settings.GameProfiles.Add(p);
            }
            SettingsManager.Save(settings);

            _notifyIcon?.Dispose();
            _automator?.Stop();
            base.OnClosed(e);
        }

        private void SetVibranceLevel(int level)
        {
            if (_nvidiaProxy.IsInitialized) _nvidiaProxy.SetVibranceLevel(level);
            if (_amdProxy.IsInitialized) _amdProxy.SetVibranceLevel(level);
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;

            if (WindowsVibranceValue != null && WindowsVibranceSlider != null)
            {
                WindowsVibranceValue.Text = WindowsVibranceSlider.Value.ToString("0") + "%";

                if (_isAutomatorRunning && _automator != null)
                {
                    _automator.UpdateLevels((int)WindowsVibranceSlider.Value);
                }
                else
                {
                    if (sender == WindowsVibranceSlider)
                    {
                        SetVibranceLevel((int)WindowsVibranceSlider.Value);
                    }
                }
            }
        }
        
        private void GameSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            if (_isAutomatorRunning && _automator != null)
            {
                _automator.UpdateProfiles(TargetProcesses.ToList());
            }
        }

        public System.Collections.ObjectModel.ObservableCollection<GameProfile> TargetProcesses { get; set; } = new System.Collections.ObjectModel.ObservableCollection<GameProfile>();

        private void ToggleAutomationButton_Click(object sender, RoutedEventArgs e)
        {
            var bc = new BrushConverter();
            if (!_isAutomatorRunning)
            {
                if (TargetProcesses.Count == 0) return;

                var processes = new System.Collections.Generic.List<GameProfile>(TargetProcesses);

                _automator = new VibranceAutomator(
                    processes, 
                    (int)WindowsVibranceSlider.Value, 
                    SetVibranceLevel);
                
                _automator.Start();
                _isAutomatorRunning = true;
                
                ToggleAutomationButton.Content = "STOP MONITORING";
                ToggleAutomationButton.Background = (System.Windows.Media.Brush)bc.ConvertFrom("#333333");
                
                MonitoringDot.Fill = (System.Windows.Media.Brush)bc.ConvertFrom("#4CAF50");
                MonitoringText.Text = "Active";
                
                // Disable UI elements
                AddGameButton.IsEnabled = false;
                ProcessListBox.IsEnabled = false;
            }
            else
            {
                _automator?.Stop();
                _automator = null;
                _isAutomatorRunning = false;
                
                ToggleAutomationButton.Content = "START MONITORING";
                ToggleAutomationButton.Background = (System.Windows.Media.Brush)bc.ConvertFrom("#FF4B4B");
                
                MonitoringDot.Fill = (System.Windows.Media.Brush)bc.ConvertFrom("#999999");
                MonitoringText.Text = "Idle";
                
                // Enable UI elements
                AddGameButton.IsEnabled = true;
                ProcessListBox.IsEnabled = true;
            }
        }

        private void RemoveProcess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is GameProfile profile)
            {
                TargetProcesses.Remove(profile);
                if (_isAutomatorRunning && _automator != null)
                {
                    _automator.UpdateProfiles(TargetProcesses.ToList());
                }
            }
        }

        private void AddGameButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private async void ScanGamesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var games = new System.Collections.Generic.List<Scanners.GameInstall>();
            await System.Threading.Tasks.Task.Run(() => 
            {
                var steam = new Scanners.SteamScanner();
                var epic = new Scanners.EpicScanner();
                var gog = new Scanners.GOGScanner();
                var blizzard = new Scanners.BlizzardScanner();
                
                games.AddRange(steam.Scan());
                games.AddRange(epic.Scan());
                games.AddRange(gog.Scan());
                games.AddRange(blizzard.Scan());
            });
            
            var gameNames = new System.Collections.Generic.List<string>();
            foreach (var g in games)
            {
                if (g.ExeName != "unknown.exe")
                {
                    string plat = string.IsNullOrEmpty(g.Platform) ? "PC" : g.Platform;
                    gameNames.Add($"[{plat}] {g.Name} ({g.ExeName})");
                }
            }

            var window = new SelectionWindow("Scan Results", "Select a game to monitor:", gameNames);
            window.Owner = this;
            if (window.ShowDialog() == true)
            {
                string selectedItem = window.SelectedItem;
                if (!TargetProcesses.Any(p => p.DisplayName == selectedItem))
                {
                    string exe = selectedItem;
                    int start = selectedItem.LastIndexOf('(');
                    int end = selectedItem.LastIndexOf(')');
                    if (start != -1 && end != -1 && end > start)
                    {
                        exe = selectedItem.Substring(start + 1, end - start - 1);
                    }
                    
                    var gameMatch = games.FirstOrDefault(g => $"[{g.Platform}] {g.Name} ({g.ExeName})" == selectedItem || $"[PC] {g.Name} ({g.ExeName})" == selectedItem);
                    string exePath = "";
                    if (gameMatch != null)
                    {
                        if (!string.IsNullOrEmpty(gameMatch.ExePath))
                        {
                            exePath = gameMatch.ExePath;
                        }
                        else if (!string.IsNullOrEmpty(gameMatch.InstallPath))
                        {
                            exePath = System.IO.Path.Combine(gameMatch.InstallPath, gameMatch.ExeName);
                        }
                    }
                    
                    var profile = new GameProfile
                    {
                        DisplayName = selectedItem,
                        ExeName = exe,
                        ExePath = exePath,
                        VibranceLevel = 100,
                        IconImage = IconHelper.GetIcon(exePath)
                    };
                    TargetProcesses.Add(profile);
                    
                    if (_isAutomatorRunning && _automator != null)
                        _automator.UpdateProfiles(TargetProcesses.ToList());
                }
            }
        }

        private void RunningProcessesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var processNames = new System.Collections.Generic.HashSet<string>();
            var procMap = new System.Collections.Generic.Dictionary<string, string>();
            
            foreach (var p in System.Diagnostics.Process.GetProcesses())
            {
                try
                {
                    if (!string.IsNullOrEmpty(p.MainWindowTitle))
                    {
                        string display = $"{p.MainWindowTitle} ({p.ProcessName}.exe)";
                        processNames.Add(display);
                        try { procMap[display] = p.MainModule.FileName; } catch { }
                    }
                }
                catch { }
            }

            var sortedList = new System.Collections.Generic.List<string>(processNames);
            sortedList.Sort();

            var window = new SelectionWindow("Running Processes", "Select a running process:", sortedList);
            window.Owner = this;
            if (window.ShowDialog() == true)
            {
                string selectedItem = window.SelectedItem;
                if (!TargetProcesses.Any(p => p.DisplayName == selectedItem))
                {
                    string exe = selectedItem;
                    int start = selectedItem.LastIndexOf('(');
                    int end = selectedItem.LastIndexOf(')');
                    if (start != -1 && end != -1 && end > start)
                    {
                        exe = selectedItem.Substring(start + 1, end - start - 1);
                    }
                    
                    procMap.TryGetValue(selectedItem, out string exePath);
                    
                    var profile = new GameProfile
                    {
                        DisplayName = selectedItem,
                        ExeName = exe,
                        ExePath = exePath,
                        VibranceLevel = 100,
                        IconImage = IconHelper.GetIcon(exePath)
                    };
                    TargetProcesses.Add(profile);
                    
                    if (_isAutomatorRunning && _automator != null)
                        _automator.UpdateProfiles(TargetProcesses.ToList());
                }
            }
        }

        private void BrowseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.DefaultExt = ".exe";
            dialog.Filter = "Executables (.exe)|*.exe";

            if (dialog.ShowDialog() == true)
            {
                string exeName = System.IO.Path.GetFileName(dialog.FileName);
                string exePath = dialog.FileName;
                string displayName = $"[Manual] {exeName}";
                
                if (!TargetProcesses.Any(p => p.ExeName == exeName))
                {
                    var profile = new GameProfile
                    {
                        DisplayName = displayName,
                        ExeName = exeName,
                        ExePath = exePath,
                        VibranceLevel = 100,
                        IconImage = IconHelper.GetIcon(exePath)
                    };
                    TargetProcesses.Add(profile);
                    
                    if (_isAutomatorRunning && _automator != null)
                        _automator.UpdateProfiles(TargetProcesses.ToList());
                }
            }
        }

        private void RunOnStartupCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                key?.SetValue("ReVibranceGUI", System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to set startup registry key: {ex.Message}");
            }
        }

        private void RunOnStartupCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                key?.DeleteValue("ReVibranceGUI", false);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to remove startup registry key: {ex.Message}");
            }
        }

        private void ThemeRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton rb && rb.IsChecked == true)
            {
                string theme = rb.Content.ToString();
                ApplyTheme(theme);
            }
        }

        private void ApplyTheme(string theme)
        {
            bool isDark = true;
            if (theme == "Auto")
            {
                try
                {
                    using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    {
                        if (key != null && key.GetValue("AppsUseLightTheme") is int useLight)
                        {
                            isDark = useLight == 0;
                        }
                    }
                }
                catch { }
            }
            else
            {
                isDark = theme == "Dark";
            }

            var app = (App)System.Windows.Application.Current;
            if (app != null)
            {
                app.Resources.MergedDictionaries.Clear();
                string themeUri = isDark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(themeUri, UriKind.Relative) });
            }
        }
    }
}
