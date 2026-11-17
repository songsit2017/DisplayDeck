using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using H.NotifyIcon;

namespace DisplayManagerClone
{
    public partial class MainWindow : Window
    {
        private HotkeyManager _hotkeyManager;
        private WallpaperManager _wallpaperManager;
        private MonitorProfileManager _profileManager = new MonitorProfileManager();
        private MonitorConfigManager _configManager = new MonitorConfigManager();
        private WindowRuleManager _windowRuleManager;
        private System.Collections.Generic.List<MonitorConfig> _configMonitors = new System.Collections.Generic.List<MonitorConfig>();
        private DispatcherTimer _watchdogTimer;
        private bool _isRestoring = false;
        private bool _isAutoRecoveryEnabled = false;

        // Visual Canvas fields
        private double _canvasScale = 0.1;
        private double _canvasOffsetX = 0;
        private double _canvasOffsetY = 0;
        private Border _draggingBorder = null;
        private Point _dragStartPoint;
        private System.Windows.Media.Brush _primaryColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215));
        private System.Windows.Media.Brush _secondaryColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 100, 100));
        private System.Windows.Media.Brush _selectedColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 140, 0));

        public MainWindow()
        {
            InitializeComponent();
            
            // Create a simple icon in memory to ensure TrayIcon is visible
            var bitmap = new System.Drawing.Bitmap(32, 32);
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            {
                g.Clear(System.Drawing.Color.DarkBlue);
                g.DrawString("DM", new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold), System.Drawing.Brushes.White, new System.Drawing.PointF(0, 4));
            }
            IntPtr hIcon = bitmap.GetHicon();
            TrayIcon.Icon = System.Drawing.Icon.FromHandle(hIcon);
            NativeMethods.DestroyIcon(hIcon);
            bitmap.Dispose();
            
            this.StateChanged += MainWindow_StateChanged;
            this.Closing += MainWindow_Closing;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            _hotkeyManager = new HotkeyManager(hwnd);
            
            LoadAndRegisterHotkeys();

            _windowRuleManager = new WindowRuleManager();
            RefreshRulesList();
            
            _watchdogTimer = new DispatcherTimer();
            _watchdogTimer.Interval = TimeSpan.FromSeconds(2);
            _watchdogTimer.Tick += WatchdogTimer_Tick;
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;

            // Show window on startup for better visibility unless launched with --minimized
            bool startMinimized = Environment.GetCommandLineArgs().Any(arg => string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase));
            if (startMinimized)
            {
                this.WindowState = WindowState.Minimized;
                this.Hide();
            }
            else
            {
                this.WindowState = WindowState.Normal;
            }
            
            LoadStartupState();
            InitTitleBarButtons();
            LoadMonitors();
        }

        private const string STARTUP_REG_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "DisplayManagerClone";

        private void LoadStartupState()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(STARTUP_REG_KEY, false);
                ChkStartWithWindows.IsChecked = key?.GetValue(APP_NAME) != null;
            }
            catch { }
        }

        private void ChkStartWithWindows_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(STARTUP_REG_KEY, true);
                if (key != null)
                {
                    if (ChkStartWithWindows.IsChecked == true)
                    {
                        string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            key.SetValue(APP_NAME, $"\"{exePath}\" --minimized");
                        }
                    }
                    else
                    {
                        key.DeleteValue(APP_NAME, throwOnMissingValue: false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update startup registry setting: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private HotkeyConfigManager _hotkeyConfigManager = new HotkeyConfigManager();
        private System.Collections.ObjectModel.ObservableCollection<HotkeyConfig> _hotkeyConfigs;
        private uint _currentCapturedModifiers;
        private uint _currentCapturedKey;

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                this.Hide();
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            this.WindowState = WindowState.Minimized;
            this.Hide();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            LoadMonitors();
        }

        private TitleBarOverlay _titleBarOverlay;
        private IntPtr _foregroundHook = IntPtr.Zero;
        private NativeMethods.WinEventDelegate _foregroundHookDelegate;
        private DispatcherTimer _titleBarFollowTimer;
        private IntPtr _lastForegroundHwnd = IntPtr.Zero;
        private bool _isTitleBarButtonsEnabled = false;

        private void InitTitleBarButtons()
        {
            _titleBarOverlay = new TitleBarOverlay();

            _titleBarFollowTimer = new DispatcherTimer();
            _titleBarFollowTimer.Interval = TimeSpan.FromMilliseconds(100);
            _titleBarFollowTimer.Tick += (s, e) => UpdateTitleBarOverlayPosition();

            _foregroundHookDelegate = new NativeMethods.WinEventDelegate(ForegroundEventProc);
            _foregroundHook = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _foregroundHookDelegate,
                0,
                0,
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);
        }

        private void ChkTitleBarButtons_CheckedChanged(object sender, RoutedEventArgs e)
        {
            _isTitleBarButtonsEnabled = ChkTitleBarButtons.IsChecked == true;
            if (_isTitleBarButtonsEnabled)
            {
                _titleBarFollowTimer?.Start();
                UpdateTitleBarOverlayPosition();
            }
            else
            {
                _titleBarFollowTimer?.Stop();
                _titleBarOverlay?.Hide();
            }
        }

        private void ForegroundEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (!_isTitleBarButtonsEnabled) return;
            Dispatcher.InvokeAsync(UpdateTitleBarOverlayPosition);
        }

        private void UpdateTitleBarOverlayPosition()
        {
            if (!_isTitleBarButtonsEnabled || _titleBarOverlay == null) return;

            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg == IntPtr.Zero || !NativeMethods.IsWindowVisible(fg))
            {
                _titleBarOverlay.Hide();
                return;
            }

            // Don't show on our own windows
            IntPtr ourHwnd = new WindowInteropHelper(this).Handle;
            IntPtr overlayHwnd = new WindowInteropHelper(_titleBarOverlay).Handle;
            if (fg == ourHwnd || fg == overlayHwnd)
            {
                return;
            }

            NativeMethods.GetWindowRect(fg, out NativeMethods.RECT rect);
            if (rect.Width <= 100 || rect.Height <= 50)
            {
                _titleBarOverlay.Hide();
                return;
            }

            // Determine DPI of monitor containing the window
            double dpiScale = 1.0;
            IntPtr hMon = NativeMethods.MonitorFromWindow(fg, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (hMon != IntPtr.Zero)
            {
                if (NativeMethods.GetDpiForMonitor(hMon, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0)
                {
                    dpiScale = dpiX / 96.0;
                }
            }
            if (dpiScale <= 0) dpiScale = 1.0;

            // Position: Top-right area next to minimize/maximize/close buttons
            // Typically close/min/max buttons occupy about 140px on the right
            double leftDip = (rect.right - 250) / dpiScale;
            double topDip = (rect.top + 4) / dpiScale;

            _lastForegroundHwnd = fg;
            _titleBarOverlay.AttachTo(fg, leftDip, topDip);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            if (_foregroundHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_foregroundHook);
                _foregroundHook = IntPtr.Zero;
            }
            _titleBarFollowTimer?.Stop();
            _titleBarOverlay?.Close();
            _hotkeyManager?.Dispose();
            _windowRuleManager?.Dispose();
            TrayIcon?.Dispose();
            Application.Current.Shutdown();
        }

        private void LoadMonitors()
        {
            if (_wallpaperManager == null) _wallpaperManager = new WallpaperManager();
            
            // Load Configuration Monitors for both tabs
            _configMonitors = _configManager.GetMonitors();
            CmbConfigMonitors.ItemsSource = _configMonitors;
            CmbRuleMonitors.ItemsSource = _configMonitors; // Also set for Window Rules tab
            
            if (_configMonitors.Count > 0)
            {
                CmbConfigMonitors.SelectedIndex = 0;
                CmbRuleMonitors.SelectedIndex = 0;
            }
            DrawMonitorCanvas();
            DrawWallpaperCanvas();
            RefreshProfilesList();
        }

        private void RefreshProfilesList()
        {
            var profiles = _profileManager.GetSavedProfiles();
            CmbProfiles.ItemsSource = profiles;
            if (profiles.Count > 0 && CmbProfiles.SelectedItem == null)
            {
                CmbProfiles.SelectedIndex = 0;
            }
        }

        private void ApplyConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isRestoring = true;
                _configManager.ApplySettings(_configMonitors);
                MessageBox.Show("Display settings applied successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadMonitors(); // Reload
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DispatcherTimer delayTimer = new DispatcherTimer();
                delayTimer.Interval = TimeSpan.FromSeconds(2);
                delayTimer.Tick += (s, args) => 
                {
                    delayTimer.Stop();
                    _isRestoring = false;
                };
                delayTimer.Start();
            }
        }

        private MonitorConfig _selectedWallpaperMonitor;
        private System.Collections.Generic.Dictionary<string, string> _monitorWallpapers = new System.Collections.Generic.Dictionary<string, string>();

        private void WallpaperCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
            {
                DrawWallpaperCanvas();
            }
        }

        private void BackgroundMode_Checked(object sender, RoutedEventArgs e)
        {
            if (WallpaperCanvas != null)
            {
                DrawWallpaperCanvas();
            }
        }

        private void TxtBezel_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (WallpaperCanvas != null)
            {
                DrawWallpaperCanvas();
            }
        }

        private void SaveLayout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string profileName = TxtProfileName.Text.Trim();
                if (string.IsNullOrEmpty(profileName))
                {
                    MessageBox.Show("Please enter a name for the new profile.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _profileManager.SaveCurrentProfile(profileName);
                MessageBox.Show($"Display layout '{profileName}' saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                TxtProfileName.Clear();
                RefreshProfilesList();
                CmbProfiles.SelectedItem = profileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save layout: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RestoreLayout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CmbProfiles.SelectedItem == null)
                {
                    MessageBox.Show("Please select a profile to load.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string profileName = CmbProfiles.SelectedItem.ToString();
                _isRestoring = true;
                _profileManager.RestoreProfile(profileName);
                MessageBox.Show($"Display layout '{profileName}' restored successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restore layout: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Reset flag after a small delay
                DispatcherTimer delayTimer = new DispatcherTimer();
                delayTimer.Interval = TimeSpan.FromSeconds(2);
                delayTimer.Tick += (s, args) => 
                {
                    delayTimer.Stop();
                    _isRestoring = false;
                };
                delayTimer.Start();
            }
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CmbProfiles.SelectedItem == null)
                {
                    MessageBox.Show("Please select a profile to delete.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string profileName = CmbProfiles.SelectedItem.ToString();
                var result = MessageBox.Show($"Are you sure you want to delete profile '{profileName}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    _profileManager.DeleteProfile(profileName);
                    RefreshProfilesList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChkAutoRecovery_CheckedChanged(object sender, RoutedEventArgs e)
        {
            _isAutoRecoveryEnabled = ChkAutoRecovery.IsChecked == true;
        }

        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            if (!_isAutoRecoveryEnabled) return;
            if (_isRestoring) return;
            
            // Restart timer to debounce multiple rapid events
            _watchdogTimer.Stop();
            _watchdogTimer.Start();
        }

        private void WatchdogTimer_Tick(object sender, EventArgs e)
        {
            _watchdogTimer.Stop();
            if (!_isAutoRecoveryEnabled) return;

            try
            {
                string profileToRestore = _profileManager.LastActiveProfile;
                if (string.IsNullOrEmpty(profileToRestore) && CmbProfiles.SelectedItem != null)
                {
                    profileToRestore = CmbProfiles.SelectedItem.ToString();
                }

                if (!string.IsNullOrEmpty(profileToRestore))
                {
                    _isRestoring = true;
                    _profileManager.RestoreProfile(profileToRestore);
                    TrayIcon?.ShowNotification("Watchdog Activated", $"Display change detected. Profile '{profileToRestore}' auto-restored!", H.NotifyIcon.Core.NotificationIcon.Info);
                }
            }
            catch
            {
                // Suppress errors during background recovery (e.g., if profile doesn't exist)
            }
            finally
            {
                // Re-enable listening after a short delay
                DispatcherTimer delayTimer = new DispatcherTimer();
                delayTimer.Interval = TimeSpan.FromSeconds(2);
                delayTimer.Tick += (s, args) => 
                {
                    delayTimer.Stop();
                    _isRestoring = false;
                };
                delayTimer.Start();
            }
        }

        private void Identify_Click(object sender, RoutedEventArgs e)
        {
            int index = 1;
            foreach (var m in _configMonitors)
            {
                double dpiScaleX = 1.0;
                double dpiScaleY = 1.0;

                try
                {
                    IntPtr hMon = WindowManager.FindMonitorByDeviceName(m.DeviceName);
                    if (hMon != IntPtr.Zero)
                    {
                        if (NativeMethods.GetDpiForMonitor(hMon, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0)
                        {
                            dpiScaleX = dpiX / 96.0;
                            dpiScaleY = dpiY / 96.0;
                        }
                    }
                }
                catch { }

                var win = new IdentifyWindow(index.ToString(), m.X, m.Y, m.CurrentResolution.Width, m.CurrentResolution.Height, dpiScaleX, dpiScaleY);
                win.Show();
                index++;
            }
        }

        private void BrowseRuleExe_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "Executable Files (*.exe)|*.exe|All files (*.*)|*.*";
            dialog.Title = "Select Application Executable";
            dialog.RestoreDirectory = true;
            
            if (dialog.ShowDialog() == true)
            {
                TxtRuleProcess.Text = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }

        private void AddRule_Click(object sender, RoutedEventArgs e)
        {
            string processName = TxtRuleProcess.Text.Trim();
            var selectedMonitor = CmbRuleMonitors.SelectedItem as MonitorConfig;

            if (string.IsNullOrEmpty(processName) || selectedMonitor == null)
            {
                MessageBox.Show("Please enter a process name and select a target monitor.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var rule = new WindowRule
            {
                ProcessName = processName,
                TargetMonitorDeviceName = selectedMonitor.DeviceName,
                TargetMonitorDisplayName = selectedMonitor.DisplayName
            };

            // Remove existing rule for same process
            _windowRuleManager.Rules.RemoveAll(r => string.Equals(r.ProcessName, processName, StringComparison.OrdinalIgnoreCase));
            
            _windowRuleManager.Rules.Add(rule);
            _windowRuleManager.SaveRules();

            TxtRuleProcess.Clear();
            RefreshRulesList();
        }

        private void RemoveRule_Click(object sender, RoutedEventArgs e)
        {
            if (LstRules.SelectedItem is WindowRule rule)
            {
                _windowRuleManager.Rules.Remove(rule);
                _windowRuleManager.SaveRules();
                RefreshRulesList();
            }
        }

        private void RefreshRulesList()
        {
            LstRules.ItemsSource = null;
            LstRules.ItemsSource = _windowRuleManager.Rules;
        }
    }
}
