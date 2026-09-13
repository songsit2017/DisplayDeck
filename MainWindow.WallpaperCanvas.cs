using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Collections.Generic;

namespace DisplayManagerClone
{
    public partial class MainWindow : Window
    {
        private void ChkManageWallpaper_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (WallpaperCanvas != null)
            {
                bool isManaged = ChkManageWallpaper.IsChecked == true;
                WallpaperCanvas.IsEnabled = isManaged;
                if (FindName("RbSameImage") is RadioButton rbSame) rbSame.IsEnabled = isManaged;
                if (FindName("RbDifferentImage") is RadioButton rbDiff) rbDiff.IsEnabled = isManaged;
                if (FindName("RbSpan") is RadioButton rbSpan) rbSpan.IsEnabled = isManaged;
                if (FindName("TxtWallpaperSource") is TextBox txtBox) txtBox.IsEnabled = isManaged;
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

    }
}
