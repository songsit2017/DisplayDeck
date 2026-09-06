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
            TrayIcon.Icon = System.Drawing.Icon.FromHandle(bitmap.GetHicon());
            
            this.StateChanged += MainWindow_StateChanged;
            this.Closing += MainWindow_Closing;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            _hotkeyManager = new HotkeyManager(hwnd, WindowManager.MoveActiveWindowToNextMonitor);
            
            _watchdogTimer = new DispatcherTimer();
            _watchdogTimer.Interval = TimeSpan.FromSeconds(2);
            _watchdogTimer.Tick += WatchdogTimer_Tick;
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;

            // Show window on startup for better visibility
            this.WindowState = WindowState.Normal;
            
            LoadMonitors();
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
            TrayIcon?.Dispose();
            Application.Current.Shutdown();
        }

        private void LoadMonitors()
        {
            if (_wallpaperManager == null) _wallpaperManager = new WallpaperManager();
            
            // Load Configuration Monitors for both tabs
            _configMonitors = _configManager.GetMonitors();
            CmbConfigMonitors.ItemsSource = _configMonitors;
            if (_configMonitors.Count > 0)
            {
                CmbConfigMonitors.SelectedIndex = 0;
            }
            DrawMonitorCanvas();
            DrawWallpaperCanvas();
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

        private void DrawWallpaperCanvas()
        {
            WallpaperCanvas.Children.Clear();
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

            // Canvas size for Wallpaper tab
            double availableWidth = 700;
            double availableHeight = 250;

            // Calculate scale
            double scaleX = (availableWidth - 40) / totalWidth;
            double scaleY = (availableHeight - 40) / totalHeight;
            double scale = Math.Min(scaleX, scaleY);
            if (scale > 0.15) scale = 0.15;

            // Center offset
            double offsetX = (availableWidth - (totalWidth * scale)) / 2 - (minX * scale);
            double offsetY = (availableHeight - (totalHeight * scale)) / 2 - (minY * scale);

            int index = 1;
            foreach (var m in _configMonitors)
            {
                System.Windows.Media.Brush backgroundBrush = m == _selectedWallpaperMonitor ? _primaryColor : _secondaryColor;
                
                string currentWallpaperPath = null;
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

                if (!string.IsNullOrEmpty(currentWallpaperPath) && System.IO.File.Exists(currentWallpaperPath))
                {
                    try
                    {
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(currentWallpaperPath, UriKind.Absolute);
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.DecodePixelWidth = 200; // Optimization
                        bitmap.EndInit();

                        backgroundBrush = new System.Windows.Media.ImageBrush(bitmap)
                        {
                            Stretch = System.Windows.Media.Stretch.UniformToFill,
                            Opacity = m == _selectedWallpaperMonitor ? 1.0 : 0.6
                        };
                    }
                    catch { }
                }

                Border border = new Border
                {
                    Width = m.CurrentResolution.Width * scale,
                    Height = m.CurrentResolution.Height * scale,
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

                Canvas.SetLeft(border, (m.X * scale) + offsetX);
                Canvas.SetTop(border, (m.Y * scale) + offsetY);
                WallpaperCanvas.Children.Add(border);
                
                // If it's the selected one, update the UI labels
                if (m == _selectedWallpaperMonitor)
                {
                    TxtWallpaperSource.Text = currentWallpaperPath ?? "Solid Color";
                }

                index++;
            }
        }

        private void WallpaperCanvas_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement fe && fe.Parent is Border border && border.Tag is MonitorConfig config)
            {
                _selectedWallpaperMonitor = config;
                DrawWallpaperCanvas();
            }
            else if (e.OriginalSource is Border b && b.Tag is MonitorConfig c)
            {
                _selectedWallpaperMonitor = c;
                DrawWallpaperCanvas();
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
                    _wallpaperManager.SetPosition(DesktopWallpaperPosition.Span);
                    // Just apply the first monitor's wallpaper to all, or let Windows handle span
                    if (_monitorWallpapers.Count > 0)
                    {
                        var firstWallpaper = _monitorWallpapers.Values.GetEnumerator();
                        if (firstWallpaper.MoveNext() && !string.IsNullOrEmpty(firstWallpaper.Current))
                        {
                            var hwMonitors = _wallpaperManager.GetMonitors();
                            if (hwMonitors.Count > 0)
                            {
                                // In span mode, setting it on the primary or all monitors usually works
                                foreach (var hw in hwMonitors)
                                {
                                    _wallpaperManager.SetWallpaper(hw.DevicePath, firstWallpaper.Current);
                                }
                            }
                        }
                    }
                }
                else if (RbSameImage.IsChecked == true)
                {
                    _wallpaperManager.SetPosition(DesktopWallpaperPosition.Fill);
                    if (_monitorWallpapers.Count > 0)
                    {
                        // Get the wallpaper of the currently selected monitor, or the first one available
                        string pathToApply = null;
                        if (_selectedWallpaperMonitor != null && _monitorWallpapers.ContainsKey(_selectedWallpaperMonitor.DeviceName))
                        {
                            pathToApply = _monitorWallpapers[_selectedWallpaperMonitor.DeviceName];
                        }
                        else
                        {
                            var firstWallpaper = _monitorWallpapers.Values.GetEnumerator();
                            if (firstWallpaper.MoveNext()) pathToApply = firstWallpaper.Current;
                        }

                        if (!string.IsNullOrEmpty(pathToApply))
                        {
                            foreach (var hw in _wallpaperManager.GetMonitors())
                            {
                                _wallpaperManager.SetWallpaper(hw.DevicePath, pathToApply);
                            }
                        }
                    }
                }
                else // Different image per monitor
                {
                    _wallpaperManager.SetPosition(DesktopWallpaperPosition.Fill);
                    int index = 0;
                    var hwMonitors = _wallpaperManager.GetMonitors();
                    foreach (var m in _configMonitors)
                    {
                        if (_monitorWallpapers.TryGetValue(m.DeviceName, out string path) && !string.IsNullOrEmpty(path))
                        {
                            if (index < hwMonitors.Count)
                            {
                                _wallpaperManager.SetWallpaper(hwMonitors[index].DevicePath, path);
                            }
                        }
                        index++;
                    }
                }
                
                MessageBox.Show("Wallpapers applied successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying wallpaper: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveLayout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _profileManager.SaveCurrentProfile();
                MessageBox.Show("Display layout saved successfully! You can now restore it anytime.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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
                _isRestoring = true;
                _profileManager.RestoreProfile();
                MessageBox.Show("Display layout restored successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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
                _isRestoring = true;
                _profileManager.RestoreProfile();
                TrayIcon?.ShowNotification("Watchdog Activated", "Display change detected. Layout auto-restored!", H.NotifyIcon.Core.NotificationIcon.Info);
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
    }
}