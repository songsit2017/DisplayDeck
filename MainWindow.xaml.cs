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

            // Show window on startup for better visibility
            this.WindowState = WindowState.Normal;
            
            LoadMonitors();
        }

        private HotkeyConfigManager _hotkeyConfigManager = new HotkeyConfigManager();
        private System.Collections.ObjectModel.ObservableCollection<HotkeyConfig> _hotkeyConfigs;
        private uint _currentCapturedModifiers;
        private uint _currentCapturedKey;

        private void LoadAndRegisterHotkeys(bool showErrors = false)
        {
            var list = _hotkeyConfigManager.LoadHotkeys();
            _hotkeyConfigs = new System.Collections.ObjectModel.ObservableCollection<HotkeyConfig>(list);
            GridHotkeys.ItemsSource = _hotkeyConfigs;

            // Clear previous registrations
            if (_hotkeyManager != null)
            {
                _hotkeyManager.Dispose();
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                _hotkeyManager = new HotkeyManager(hwnd);

                string failedRegistrations = "";

                foreach (var config in _hotkeyConfigs)
                {
                    if (config.Key != 0)
                    {
                        Action action = GetActionFromId(config.ActionId);
                        if (action != null)
                        {
                            bool success = _hotkeyManager.RegisterHotkey(config.Modifiers, config.Key, action);
                            if (!success)
                            {
                                failedRegistrations += $"- {config.ActionName} ({config.DisplayHotkey})\n";
                                // Revert it so it shows 'None' if failed
                                config.Modifiers = 0;
                                config.Key = 0;
                            }
                        }
                    }
                }

                if (showErrors && !string.IsNullOrEmpty(failedRegistrations))
                {
                    MessageBox.Show($"The following hotkeys could not be registered because they are already in use by Windows or another application:\n\n{failedRegistrations}", "Hotkey Registration Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    
                    // Save the reverted configs
                    _hotkeyConfigManager.SaveHotkeys(new System.Collections.Generic.List<HotkeyConfig>(_hotkeyConfigs));
                }
            }
        }

        private Action GetActionFromId(string id)
        {
            switch (id)
            {
                case "NextMonitor": return WindowManager.MoveActiveWindowToNextMonitor;
                case "PrevMonitor": return WindowManager.MoveActiveWindowToPreviousMonitor;
                case "PrimaryMonitor": return WindowManager.MoveWindowToPrimaryMonitor;
                case "CenterMonitor": return WindowManager.MoveWindowToCenterOfMonitor;
                case "Center75": return WindowManager.MoveWindowToCenterOfMonitorAndSizeTo75Percent;
                case "CornerTL": return WindowManager.MoveWindowToCornerTL;
                case "CornerTR": return WindowManager.MoveWindowToCornerTR;
                case "CornerBL": return WindowManager.MoveWindowToCornerBL;
                case "CornerBR": return WindowManager.MoveWindowToCornerBR;
                case "SideLeft": return WindowManager.SizeWindowToSideLeft;
                case "SideRight": return WindowManager.SizeWindowToSideRight;
                case "SideTop": return WindowManager.SizeWindowToSideTop;
                case "SideBottom": return WindowManager.SizeWindowToSideBottom;
                case "SpanAll": return WindowManager.SpanWindowAcrossAllMonitors;
                case "Maximize": return WindowManager.MaximizeWindow;
                case "Minimize": return WindowManager.MinimizeWindow;
                case "Restore": return WindowManager.RestoreWindow;
                case "AlwaysOnTop": return WindowManager.ToggleWindowAlwaysOnTop;
            }
            return null;
        }

        private void TxtHotkeyInput_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;

            var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
            
            // Ignore modifier keys alone
            if (key == System.Windows.Input.Key.LeftCtrl || key == System.Windows.Input.Key.RightCtrl ||
                key == System.Windows.Input.Key.LeftAlt || key == System.Windows.Input.Key.RightAlt ||
                key == System.Windows.Input.Key.LeftShift || key == System.Windows.Input.Key.RightShift ||
                key == System.Windows.Input.Key.LWin || key == System.Windows.Input.Key.RWin ||
                key == System.Windows.Input.Key.Clear || key == System.Windows.Input.Key.OemClear || key == System.Windows.Input.Key.Apps)
            {
                return;
            }

            uint modifiers = 0;
            if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control)) modifiers |= HotkeyManager.MOD_CTRL;
            if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt)) modifiers |= HotkeyManager.MOD_ALT;
            if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift)) modifiers |= HotkeyManager.MOD_SHIFT;
            if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Windows)) modifiers |= HotkeyManager.MOD_WIN;

            uint virtualKey = (uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(key);

            _currentCapturedModifiers = modifiers;
            _currentCapturedKey = virtualKey;

            // Update text
            string modsStr = "";
            if ((modifiers & HotkeyManager.MOD_CTRL) != 0) modsStr += "Ctrl + ";
            if ((modifiers & HotkeyManager.MOD_WIN) != 0) modsStr += "Win + ";
            if ((modifiers & HotkeyManager.MOD_ALT) != 0) modsStr += "Alt + ";
            if ((modifiers & HotkeyManager.MOD_SHIFT) != 0) modsStr += "Shift + ";

            string keyStr = key.ToString();
            if (keyStr.StartsWith("D") && keyStr.Length == 2 && char.IsDigit(keyStr[1]))
                keyStr = keyStr[1].ToString();

            TxtHotkeyInput.Text = modsStr + keyStr;
        }

        private string GetConflictWarning(uint modifiers, uint key)
        {
            var k = System.Windows.Input.KeyInterop.KeyFromVirtualKey((int)key);
            
            if (modifiers == HotkeyManager.MOD_WIN)
            {
                switch (k)
                {
                    case System.Windows.Input.Key.E: return "Win + E is used by Windows to open File Explorer.";
                    case System.Windows.Input.Key.R: return "Win + R is used by Windows to open the Run dialog.";
                    case System.Windows.Input.Key.D: return "Win + D is used by Windows to show the desktop.";
                    case System.Windows.Input.Key.M: return "Win + M is used by Windows to minimize all windows.";
                    case System.Windows.Input.Key.L: return "Win + L is used by Windows to lock the PC.";
                    case System.Windows.Input.Key.P: return "Win + P is used by Windows to project the screen.";
                    case System.Windows.Input.Key.Tab: return "Win + Tab is used by Windows for Task View.";
                    case System.Windows.Input.Key.Left:
                    case System.Windows.Input.Key.Right:
                    case System.Windows.Input.Key.Up:
                    case System.Windows.Input.Key.Down: return "Win + Arrow keys are used by Windows for Snap Assist.";
                }
            }

            if (modifiers == HotkeyManager.MOD_CTRL)
            {
                switch (k)
                {
                    case System.Windows.Input.Key.C: return "Ctrl + C is widely used for Copy.";
                    case System.Windows.Input.Key.V: return "Ctrl + V is widely used for Paste.";
                    case System.Windows.Input.Key.X: return "Ctrl + X is widely used for Cut.";
                    case System.Windows.Input.Key.Z: return "Ctrl + Z is widely used for Undo.";
                    case System.Windows.Input.Key.Y: return "Ctrl + Y is widely used for Redo.";
                    case System.Windows.Input.Key.A: return "Ctrl + A is widely used for Select All.";
                    case System.Windows.Input.Key.S: return "Ctrl + S is widely used for Save.";
                    case System.Windows.Input.Key.P: return "Ctrl + P is widely used for Print.";
                    case System.Windows.Input.Key.F: return "Ctrl + F is widely used for Find.";
                }
            }

            if (modifiers == HotkeyManager.MOD_ALT)
            {
                if (k == System.Windows.Input.Key.F4) return "Alt + F4 is used by Windows to close the active application.";
                if (k == System.Windows.Input.Key.Tab) return "Alt + Tab is used by Windows to switch applications.";
            }

            return null;
        }

        private void BtnAssignHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (GridHotkeys.SelectedItem is HotkeyConfig config)
            {
                if (_currentCapturedKey != 0)
                {
                    string warning = GetConflictWarning(_currentCapturedModifiers, _currentCapturedKey);
                    if (!string.IsNullOrEmpty(warning))
                    {
                        var result = MessageBox.Show(warning + "\n\nDo you still want to assign this hotkey?", "Potential Conflict", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (result == MessageBoxResult.No)
                        {
                            TxtHotkeyInput.Text = "Click here and press a key combo...";
                            _currentCapturedModifiers = 0;
                            _currentCapturedKey = 0;
                            return;
                        }
                    }

                    config.Modifiers = _currentCapturedModifiers;
                    config.Key = _currentCapturedKey;
                    
                    _hotkeyConfigManager.SaveHotkeys(new System.Collections.Generic.List<HotkeyConfig>(_hotkeyConfigs));
                    LoadAndRegisterHotkeys(true); // Re-register all, show errors if any
                    
                    TxtHotkeyInput.Text = "Click here and press a key combo...";
                    _currentCapturedModifiers = 0;
                    _currentCapturedKey = 0;
                }
                else
                {
                    MessageBox.Show("Please press a key combination in the textbox first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                MessageBox.Show("Please select an action from the list first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnClearHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (GridHotkeys.SelectedItem is HotkeyConfig config)
            {
                config.Modifiers = 0;
                config.Key = 0;
                
                _hotkeyConfigManager.SaveHotkeys(new System.Collections.Generic.List<HotkeyConfig>(_hotkeyConfigs));
                LoadAndRegisterHotkeys(); // Re-register all
                
                TxtHotkeyInput.Text = "Click here and press a key combo...";
                _currentCapturedModifiers = 0;
                _currentCapturedKey = 0;
            }
        }

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

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
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

        private void DrawMonitorCanvas()
        {
            MonitorCanvas.Children.Clear();
            if (_configMonitors.Count == 0) return;

            // Calculate bounding box of all monitors
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            foreach (var m in _configMonitors)
            {
                if (m.X < minX) minX = m.X;
                if (m.Y < minY) minY = m.Y;
                if (m.X + m.CurrentResolution.Width > maxX) maxX = m.X + m.CurrentResolution.Width;
                if (m.Y + m.CurrentResolution.Height > maxY) maxY = m.Y + m.CurrentResolution.Height;
            }

            double totalWidth = maxX - minX;
            double totalHeight = maxY - minY;

            // Canvas size (assuming around 700x300 available)
            double availableWidth = 700;
            double availableHeight = 250;

            // Calculate scale
            double scaleX = (availableWidth - 40) / totalWidth; // 40px padding
            double scaleY = (availableHeight - 40) / totalHeight;
            _canvasScale = Math.Min(scaleX, scaleY);
            if (_canvasScale > 0.15) _canvasScale = 0.15; // Max scale

            // Center offset
            _canvasOffsetX = (availableWidth - (totalWidth * _canvasScale)) / 2 - (minX * _canvasScale);
            _canvasOffsetY = (availableHeight - (totalHeight * _canvasScale)) / 2 - (minY * _canvasScale);

            var selectedConfig = CmbConfigMonitors.SelectedItem as MonitorConfig;
            int index = 1;

            foreach (var m in _configMonitors)
            {
                System.Windows.Media.Brush backgroundBrush = m == selectedConfig ? _primaryColor : _secondaryColor;
                
                // Try to load actual desktop wallpaper
                string wallpaperPath = _wallpaperManager?.GetWallpaperForIndex(index - 1);
                if (!string.IsNullOrEmpty(wallpaperPath) && System.IO.File.Exists(wallpaperPath))
                {
                    try
                    {
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(wallpaperPath, UriKind.Absolute);
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.DecodePixelWidth = 400; // Optimization
                        bitmap.EndInit();

                        backgroundBrush = new System.Windows.Media.ImageBrush(bitmap)
                        {
                            Stretch = System.Windows.Media.Stretch.UniformToFill,
                            Opacity = 0.7 // Darken slightly so white text is readable
                        };
                    }
                    catch
                    {
                        // Fallback to solid color if image loading fails
                    }
                }

                Border border = new Border
                {
                    Width = m.CurrentResolution.Width * _canvasScale,
                    Height = m.CurrentResolution.Height * _canvasScale,
                    BorderThickness = new Thickness(m == selectedConfig ? 4 : 2),
                    CornerRadius = new CornerRadius(0),
                    Background = backgroundBrush,
                    BorderBrush = m == selectedConfig ? System.Windows.Media.Brushes.DeepSkyBlue : System.Windows.Media.Brushes.Black,
                    Tag = m
                };

                // Create inner content with Number and Name
                Grid contentGrid = new Grid();
                contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                TextBlock tbNumber = new TextBlock
                {
                    Text = index.ToString(),
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 40,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 5, ShadowDepth = 2 }
                };
                Grid.SetRow(tbNumber, 0);

                TextBlock tbName = new TextBlock
                {
                    Text = m.DisplayName,
                    Foreground = System.Windows.Media.Brushes.LightGray,
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0,0,0,5),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                };
                Grid.SetRow(tbName, 1);

                contentGrid.Children.Add(tbNumber);
                contentGrid.Children.Add(tbName);
                border.Child = contentGrid;

                Canvas.SetLeft(border, (m.X * _canvasScale) + _canvasOffsetX);
                Canvas.SetTop(border, (m.Y * _canvasScale) + _canvasOffsetY);

                MonitorCanvas.Children.Add(border);
                index++;
            }
        }

        private void CmbConfigMonitors_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CmbConfigMonitors.SelectedItem is MonitorConfig config)
            {
                CmbResolutions.ItemsSource = config.SupportedResolutions;
                CmbResolutions.SelectedItem = config.CurrentResolution;
                ChkPrimary.IsChecked = config.IsPrimary;
                
                _isUpdatingPos = true;
                TxtPosX.Text = config.X.ToString();
                TxtPosY.Text = config.Y.ToString();
                _isUpdatingPos = false;
                
                DrawMonitorCanvas();
            }
        }

        private void CmbResolutions_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CmbConfigMonitors.SelectedItem is MonitorConfig config && CmbResolutions.SelectedItem is Resolution res)
            {
                config.CurrentResolution = res;
                DrawMonitorCanvas();
            }
        }

        private void ChkPrimary_Checked(object sender, RoutedEventArgs e)
        {
            if (CmbConfigMonitors.SelectedItem is MonitorConfig config)
            {
                config.IsPrimary = ChkPrimary.IsChecked == true;
                if (config.IsPrimary)
                {
                    // If setting as primary, position must be 0,0
                    config.X = 0;
                    config.Y = 0;
                    _isUpdatingPos = true;
                    TxtPosX.Text = "0";
                    TxtPosY.Text = "0";
                    _isUpdatingPos = false;
                }
                DrawMonitorCanvas();
            }
        }

        private void MonitorCanvas_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement fe && fe.Parent is Border border && border.Tag is MonitorConfig config)
            {
                CmbConfigMonitors.SelectedItem = config;
                _draggingBorder = border;
                _dragStartPoint = e.GetPosition(MonitorCanvas);
                border.CaptureMouse();
                DrawMonitorCanvas(); // Update selection color
            }
            else if (e.OriginalSource is Border b && b.Tag is MonitorConfig c)
            {
                CmbConfigMonitors.SelectedItem = c;
                _draggingBorder = b;
                _dragStartPoint = e.GetPosition(MonitorCanvas);
                b.CaptureMouse();
                DrawMonitorCanvas();
            }
        }

        private void MonitorCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_draggingBorder != null && _draggingBorder.Tag is MonitorConfig config)
            {
                Point currentPoint = e.GetPosition(MonitorCanvas);
                double offsetX = currentPoint.X - _dragStartPoint.X;
                double offsetY = currentPoint.Y - _dragStartPoint.Y;

                double currentLeft = Canvas.GetLeft(_draggingBorder);
                double currentTop = Canvas.GetTop(_draggingBorder);

                Canvas.SetLeft(_draggingBorder, currentLeft + offsetX);
                Canvas.SetTop(_draggingBorder, currentTop + offsetY);

                _dragStartPoint = currentPoint;

                // Reverse calc to real X, Y
                config.X = (int)((Canvas.GetLeft(_draggingBorder) - _canvasOffsetX) / _canvasScale);
                config.Y = (int)((Canvas.GetTop(_draggingBorder) - _canvasOffsetY) / _canvasScale);

                // Update textboxes without triggering redraw loop
                _isUpdatingPos = true;
                TxtPosX.Text = config.X.ToString();
                TxtPosY.Text = config.Y.ToString();
                _isUpdatingPos = false;
            }
        }

        private void MonitorCanvas_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_draggingBorder != null)
            {
                _draggingBorder.ReleaseMouseCapture();
                _draggingBorder = null;
                // Optional: Snap to edges logic could go here
                DrawMonitorCanvas(); // redraw to snap and clean up
            }
        }

        private bool _isUpdatingPos = false;

        private void TxtPos_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isUpdatingPos) return;
            if (CmbConfigMonitors.SelectedItem is MonitorConfig config)
            {
                if (int.TryParse(TxtPosX.Text, out int x)) config.X = x;
                if (int.TryParse(TxtPosY.Text, out int y)) config.Y = y;
                DrawMonitorCanvas();
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

        private void DrawWallpaperCanvas()
        {
            if (WallpaperCanvas == null) return;
            WallpaperCanvas.Children.Clear();
            if (_configMonitors.Count == 0 || WallpaperCanvas.ActualWidth == 0) return;

            int bezelX = 0, bezelY = 0;
            if (TxtBezelX != null) int.TryParse(TxtBezelX.Text, out bezelX);
            if (TxtBezelY != null) int.TryParse(TxtBezelY.Text, out bezelY);

            // Sort monitors to apply bezel iteratively
            var sortedMonitorsX = new System.Collections.Generic.List<MonitorConfig>(_configMonitors);
            sortedMonitorsX.Sort((a, b) => a.X.CompareTo(b.X));
            
            var sortedMonitorsY = new System.Collections.Generic.List<MonitorConfig>(_configMonitors);
            sortedMonitorsY.Sort((a, b) => a.Y.CompareTo(b.Y));

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            var monitorRects = new System.Collections.Generic.Dictionary<string, System.Windows.Rect>();

            foreach (var m in _configMonitors)
            {
                int rankX = sortedMonitorsX.IndexOf(m);
                int rankY = sortedMonitorsY.IndexOf(m);
                
                int virtX = m.X + (rankX * bezelX);
                int virtY = m.Y + (rankY * bezelY);

                if (virtX < minX) minX = virtX;
                if (virtY < minY) minY = virtY;
                if (virtX + m.CurrentResolution.Width > maxX) maxX = virtX + m.CurrentResolution.Width;
                if (virtY + m.CurrentResolution.Height > maxY) maxY = virtY + m.CurrentResolution.Height;

                monitorRects[m.DeviceName] = new System.Windows.Rect(virtX, virtY, m.CurrentResolution.Width, m.CurrentResolution.Height);
            }

            double totalWidth = maxX - minX;
            double totalHeight = maxY - minY;

            // Canvas size for Wallpaper tab
            double availableWidth = WallpaperCanvas.ActualWidth;
            double availableHeight = WallpaperCanvas.ActualHeight;

            // Calculate scale
            double scaleX = (availableWidth - 20) / totalWidth;
            double scaleY = (availableHeight - 20) / totalHeight;
            double scale = Math.Min(scaleX, scaleY);

            // Center offset
            double offsetX = (availableWidth - (totalWidth * scale)) / 2 - (minX * scale);
            double offsetY = (availableHeight - (totalHeight * scale)) / 2 - (minY * scale);

            // Determine global wallpaper path for Span or Same modes
            string globalWallpaperPath = null;
            if ((RbSpan.IsChecked == true || RbSameImage.IsChecked == true) && _monitorWallpapers.Count > 0)
            {
                if (_selectedWallpaperMonitor != null && _monitorWallpapers.ContainsKey(_selectedWallpaperMonitor.DeviceName))
                {
                    globalWallpaperPath = _monitorWallpapers[_selectedWallpaperMonitor.DeviceName];
                }
                else
                {
                    var firstWallpaper = _monitorWallpapers.Values.GetEnumerator();
                    if (firstWallpaper.MoveNext()) globalWallpaperPath = firstWallpaper.Current;
                }
            }

            int index = 1;
            foreach (var m in _configMonitors)
            {
                System.Windows.Media.Brush backgroundBrush = m == _selectedWallpaperMonitor ? _primaryColor : _secondaryColor;
                
                string currentWallpaperPath = globalWallpaperPath;
                if (RbDifferentImage.IsChecked == true)
                {
                    if (_monitorWallpapers.ContainsKey(m.DeviceName))
                    {
                        currentWallpaperPath = _monitorWallpapers[m.DeviceName];
                    }
                    else
                    {
                        currentWallpaperPath = _wallpaperManager?.GetWallpaperForIndex(index - 1);
                        if (!string.IsNullOrEmpty(currentWallpaperPath))
                            _monitorWallpapers[m.DeviceName] = currentWallpaperPath;
                    }
                }

                var rect = monitorRects[m.DeviceName];
                
                if (!string.IsNullOrEmpty(currentWallpaperPath) && System.IO.File.Exists(currentWallpaperPath))
                {
                    try
                    {
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(currentWallpaperPath, UriKind.Absolute);
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        // Remove DecodePixelWidth so it doesn't get pixelated when spanning
                        bitmap.EndInit();

                        var imageBrush = new System.Windows.Media.ImageBrush(bitmap)
                        {
                            Opacity = m == _selectedWallpaperMonitor ? 1.0 : 0.6
                        };

                        if (RbSpan.IsChecked == true)
                        {
                            imageBrush.Stretch = System.Windows.Media.Stretch.Fill;
                            imageBrush.ViewboxUnits = System.Windows.Media.BrushMappingMode.RelativeToBoundingBox;
                            imageBrush.Viewbox = new System.Windows.Rect(
                                (double)(rect.X - minX) / totalWidth,
                                (double)(rect.Y - minY) / totalHeight,
                                (double)rect.Width / totalWidth,
                                (double)rect.Height / totalHeight
                            );
                        }
                        else
                        {
                            imageBrush.Stretch = System.Windows.Media.Stretch.UniformToFill;
                        }

                        backgroundBrush = imageBrush;
                    }
                    catch { }
                }

                Border border = new Border
                {
                    Width = rect.Width * scale,
                    Height = rect.Height * scale,
                    BorderThickness = new Thickness(m == _selectedWallpaperMonitor ? 3 : 1),
                    CornerRadius = new CornerRadius(0),
                    Background = backgroundBrush,
                    BorderBrush = m == _selectedWallpaperMonitor ? System.Windows.Media.Brushes.DeepSkyBlue : System.Windows.Media.Brushes.DimGray,
                    Tag = m
                };

                TextBlock tbNumber = new TextBlock
                {
                    Text = index.ToString(),
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 40,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 5, ShadowDepth = 2 }
                };
                border.Child = tbNumber;

                Canvas.SetLeft(border, (rect.X * scale) + offsetX);
                Canvas.SetTop(border, (rect.Y * scale) + offsetY);
                WallpaperCanvas.Children.Add(border);
                
                // If it's the selected one, update the UI labels
                if (m == _selectedWallpaperMonitor)
                {
                    TxtWallpaperSource.Text = currentWallpaperPath ?? "Solid Color";
                }

                index++;
            }
        }

        private bool _isDraggingMonitor = false;
        private Border _draggedBorder = null;

        private void WallpaperCanvas_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Border clickedBorder = null;
            if (e.OriginalSource is FrameworkElement fe && fe.Parent is Border border && border.Tag is MonitorConfig config)
            {
                clickedBorder = border;
            }
            else if (e.OriginalSource is Border b && b.Tag is MonitorConfig c)
            {
                clickedBorder = b;
            }

            if (clickedBorder != null)
            {
                _selectedWallpaperMonitor = clickedBorder.Tag as MonitorConfig;
                
                // Update selection visuals without redrawing
                foreach (UIElement child in WallpaperCanvas.Children)
                {
                    if (child is Border cb && cb.Tag is MonitorConfig mc)
                    {
                        cb.BorderThickness = new Thickness(mc == _selectedWallpaperMonitor ? 3 : 1);
                        cb.BorderBrush = mc == _selectedWallpaperMonitor ? System.Windows.Media.Brushes.DeepSkyBlue : System.Windows.Media.Brushes.DimGray;
                        if (cb.Background is System.Windows.Media.ImageBrush ib)
                        {
                            ib.Opacity = mc == _selectedWallpaperMonitor ? 1.0 : 0.6;
                        }
                    }
                }

                if (_monitorWallpapers.ContainsKey(_selectedWallpaperMonitor.DeviceName))
                    TxtWallpaperSource.Text = _monitorWallpapers[_selectedWallpaperMonitor.DeviceName];
                else
                    TxtWallpaperSource.Text = "Solid Color";

                if (RbSpan.IsChecked == true)
                {
                    _isDraggingMonitor = true;
                    _draggedBorder = clickedBorder;
                    _dragStartPoint = e.GetPosition(WallpaperCanvas);
                    clickedBorder.CaptureMouse();
                }
            }
        }

        private void WallpaperCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDraggingMonitor && _draggedBorder != null)
            {
                System.Windows.Point currentPoint = e.GetPosition(WallpaperCanvas);
                double diffX = currentPoint.X - _dragStartPoint.X;
                double diffY = currentPoint.Y - _dragStartPoint.Y;

                double newLeft = Canvas.GetLeft(_draggedBorder) + diffX;
                double newTop = Canvas.GetTop(_draggedBorder) + diffY;

                Canvas.SetLeft(_draggedBorder, newLeft);
                Canvas.SetTop(_draggedBorder, newTop);

                _dragStartPoint = currentPoint;

                // Live update the span preview background
                UpdateSpanPreviewMapping();
            }
        }

        private void WallpaperCanvas_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (RbSpan.IsChecked != true) return;

            Border targetBorder = null;
            if (e.OriginalSource is FrameworkElement fe && fe.Parent is Border border && border.Tag is MonitorConfig)
            {
                targetBorder = border;
            }
            else if (e.OriginalSource is Border b && b.Tag is MonitorConfig)
            {
                targetBorder = b;
            }

            if (targetBorder == null)
            {
                // Fallback to selected monitor
                foreach (UIElement child in WallpaperCanvas.Children)
                {
                    if (child is Border cb && cb.Tag == _selectedWallpaperMonitor)
                    {
                        targetBorder = cb;
                        break;
                    }
                }
            }

            if (targetBorder != null)
            {
                double scaleFactor = e.Delta > 0 ? 1.05 : 0.95; // 5% scale per tick
                
                // Keep aspect ratio intact
                double newWidth = targetBorder.Width * scaleFactor;
                double newHeight = targetBorder.Height * scaleFactor;
                
                // Prevent scaling too small
                if (newWidth > 20 && newHeight > 20)
                {
                    targetBorder.Width = newWidth;
                    targetBorder.Height = newHeight;
                    UpdateSpanPreviewMapping();
                }
                
                e.Handled = true;
            }
        }

        private void WallpaperCanvas_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isDraggingMonitor && _draggedBorder != null)
            {
                _isDraggingMonitor = false;
                _draggedBorder.ReleaseMouseCapture();
                _draggedBorder = null;
            }
        }

        private void UpdateSpanPreviewMapping()
        {
            if (RbSpan.IsChecked != true || WallpaperCanvas.Children.Count == 0) return;

            // Find bounding box of all borders in canvas
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            var borders = new System.Collections.Generic.List<Border>();

            foreach (UIElement child in WallpaperCanvas.Children)
            {
                if (child is Border b && b.Tag is MonitorConfig)
                {
                    borders.Add(b);
                    double left = Canvas.GetLeft(b);
                    double top = Canvas.GetTop(b);
                    if (left < minX) minX = left;
                    if (top < minY) minY = top;
                    if (left + b.Width > maxX) maxX = left + b.Width;
                    if (top + b.Height > maxY) maxY = top + b.Height;
                }
            }

            double totalWidth = maxX - minX;
            double totalHeight = maxY - minY;
            if (totalWidth <= 0 || totalHeight <= 0) return;

            foreach (var b in borders)
            {
                if (b.Background is System.Windows.Media.ImageBrush ib)
                {
                    double left = Canvas.GetLeft(b);
                    double top = Canvas.GetTop(b);
                    
                    ib.Viewbox = new System.Windows.Rect(
                        (left - minX) / totalWidth,
                        (top - minY) / totalHeight,
                        b.Width / totalWidth,
                        b.Height / totalHeight
                    );
                }
            }
        }

        private void ChangeWallpaperSource_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedWallpaperMonitor == null)
            {
                if (_configMonitors.Count > 0)
                    _selectedWallpaperMonitor = _configMonitors[0];
                else
                    return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "Image Files (*.bmp;*.jpg;*.jpeg;*.png)|*.BMP;*.JPG;*.JPEG;*.PNG|All files (*.*)|*.*";
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() == true)
            {
                string path = dialog.FileName;
                _monitorWallpapers[_selectedWallpaperMonitor.DeviceName] = path;
                TxtWallpaperSource.Text = path;
                DrawWallpaperCanvas();
            }
        }

        private void ApplyWallpaper_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (RbSpan.IsChecked == true)
                {
                    _wallpaperManager.SetPosition(DesktopWallpaperPosition.Fill);
                    if (_monitorWallpapers.Count > 0)
                    {
                        string pathToApply = null;
                        if (_selectedWallpaperMonitor != null && _monitorWallpapers.ContainsKey(_selectedWallpaperMonitor.DeviceName))
                            pathToApply = _monitorWallpapers[_selectedWallpaperMonitor.DeviceName];
                        else
                        {
                            var firstWallpaper = _monitorWallpapers.Values.GetEnumerator();
                            if (firstWallpaper.MoveNext()) pathToApply = firstWallpaper.Current;
                        }

                        if (!string.IsNullOrEmpty(pathToApply))
                        {
                            ApplyCustomSpanWallpaper(pathToApply);
                        }
                    }
                }
                else if (RbSameImage.IsChecked == true)
                {
                    _wallpaperManager.SetPosition(DesktopWallpaperPosition.Fill);
                    if (_monitorWallpapers.Count > 0)
                    {
                        string pathToApply = null;
                        if (_selectedWallpaperMonitor != null && _monitorWallpapers.ContainsKey(_selectedWallpaperMonitor.DeviceName))
                            pathToApply = _monitorWallpapers[_selectedWallpaperMonitor.DeviceName];
                        else
                        {
                            var firstWallpaper = _monitorWallpapers.Values.GetEnumerator();
                            if (firstWallpaper.MoveNext()) pathToApply = firstWallpaper.Current;
                        }

                        if (!string.IsNullOrEmpty(pathToApply))
                        {
                            _wallpaperManager.SetWallpaper(null, pathToApply);
                        }
                    }
                }
                else // Different image per monitor
                {
                    _wallpaperManager.SetPosition(DesktopWallpaperPosition.Fill);
                    var hwMonitors = _wallpaperManager.GetMonitors();
                    foreach (var m in _configMonitors)
                    {
                        if (_monitorWallpapers.TryGetValue(m.DeviceName, out string path) && !string.IsNullOrEmpty(path))
                        {
                            var matchedHw = hwMonitors.Find(hw => hw.Rect.left == m.X && hw.Rect.top == m.Y);
                            if (matchedHw == null)
                            {
                                int fallbackIndex = _configMonitors.IndexOf(m);
                                if (fallbackIndex < hwMonitors.Count) matchedHw = hwMonitors[fallbackIndex];
                            }
                            
                            if (matchedHw != null)
                            {
                                _wallpaperManager.SetWallpaper(matchedHw.DevicePath, path);
                            }
                        }
                    }
                }
                
                MessageBox.Show("Wallpapers applied successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying wallpaper: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyCustomSpanWallpaper(string imagePath)
        {
            if (_configMonitors.Count == 0 || !System.IO.File.Exists(imagePath) || WallpaperCanvas.Children.Count == 0) return;

            // Extract the virtual layout directly from the Canvas UI so user's manual dragging/offsets are preserved perfectly!
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            var borders = new System.Collections.Generic.List<Border>();

            foreach (UIElement child in WallpaperCanvas.Children)
            {
                if (child is Border b && b.Tag is MonitorConfig)
                {
                    borders.Add(b);
                    double left = Canvas.GetLeft(b);
                    double top = Canvas.GetTop(b);
                    if (left < minX) minX = left;
                    if (top < minY) minY = top;
                    if (left + b.Width > maxX) maxX = left + b.Width;
                    if (top + b.Height > maxY) maxY = top + b.Height;
                }
            }

            double totalWidth = maxX - minX;
            double totalHeight = maxY - minY;
            if (totalWidth <= 0 || totalHeight <= 0) return;

            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage(new Uri(imagePath, UriKind.Absolute));
                
                double scaleX = (double)totalWidth / bitmap.PixelWidth;
                double scaleY = (double)totalHeight / bitmap.PixelHeight;
                double scale = Math.Max(scaleX, scaleY);

                int scaledWidth = (int)(bitmap.PixelWidth * scale);
                int scaledHeight = (int)(bitmap.PixelHeight * scale);
                
                double offsetX = (scaledWidth - totalWidth) / 2;
                double offsetY = (scaledHeight - totalHeight) / 2;

                var hwMonitors = _wallpaperManager.GetMonitors();

                foreach (var b in borders)
                {
                    MonitorConfig m = b.Tag as MonitorConfig;
                    var matchedHw = hwMonitors.Find(hw => hw.Rect.left == m.X && hw.Rect.top == m.Y);
                    
                    if (matchedHw == null)
                    {
                        int fallbackIndex = _configMonitors.IndexOf(m);
                        if (fallbackIndex < hwMonitors.Count) matchedHw = hwMonitors[fallbackIndex];
                    }

                    if (matchedHw == null) continue;

                    double left = Canvas.GetLeft(b);
                    double top = Canvas.GetTop(b);

                    int cropX = (int)((left - minX + offsetX) / scale);
                    int cropY = (int)((top - minY + offsetY) / scale);
                    int cropW = (int)(b.Width / scale);
                    int cropH = (int)(b.Height / scale);

                    // Clamp to image bounds
                    cropX = Math.Max(0, Math.Min(cropX, bitmap.PixelWidth - 1));
                    cropY = Math.Max(0, Math.Min(cropY, bitmap.PixelHeight - 1));
                    cropW = Math.Max(1, Math.Min(cropW, bitmap.PixelWidth - cropX));
                    cropH = Math.Max(1, Math.Min(cropH, bitmap.PixelHeight - cropY));

                    var croppedBitmap = new System.Windows.Media.Imaging.CroppedBitmap(bitmap, new System.Windows.Int32Rect(cropX, cropY, cropW, cropH));
                    
                    string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"DisplayManager_Span_{m.DeviceName.Replace("\\", "").Replace(".", "")}.jpg");
                    
                    using (var fs = new System.IO.FileStream(tempPath, System.IO.FileMode.Create))
                    {
                        var encoder = new System.Windows.Media.Imaging.JpegBitmapEncoder();
                        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(croppedBitmap));
                        encoder.QualityLevel = 100;
                        encoder.Save(fs);
                    }

                    _wallpaperManager.SetWallpaper(matchedHw.DevicePath, tempPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to slice span image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                var win = new IdentifyWindow(index.ToString(), m.X, m.Y, m.CurrentResolution.Width, m.CurrentResolution.Height);
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
