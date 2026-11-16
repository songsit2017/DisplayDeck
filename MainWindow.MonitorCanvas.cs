using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

namespace DisplayManagerClone
{
    public partial class MainWindow : Window
    {
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

        private void SnapToNearestEdge(MonitorConfig draggedConfig)
        {
            if (draggedConfig == null) return;
            const int SNAP_THRESHOLD = 50; // physical pixels

            foreach (var other in _configMonitors)
            {
                if (other == draggedConfig) continue;

                int dragL = draggedConfig.X;
                int dragR = draggedConfig.X + draggedConfig.CurrentResolution.Width;
                int dragT = draggedConfig.Y;
                int dragB = draggedConfig.Y + draggedConfig.CurrentResolution.Height;

                int otherL = other.X;
                int otherR = other.X + other.CurrentResolution.Width;
                int otherT = other.Y;
                int otherB = other.Y + other.CurrentResolution.Height;

                // Snap right of dragged -> left of other
                if (Math.Abs(dragR - otherL) < SNAP_THRESHOLD)
                    draggedConfig.X = otherL - draggedConfig.CurrentResolution.Width;

                // Snap left of dragged -> right of other
                if (Math.Abs(dragL - otherR) < SNAP_THRESHOLD)
                    draggedConfig.X = otherR;

                // Snap bottom of dragged -> top of other
                if (Math.Abs(dragB - otherT) < SNAP_THRESHOLD)
                    draggedConfig.Y = otherT - draggedConfig.CurrentResolution.Height;

                // Snap top of dragged -> bottom of other
                if (Math.Abs(dragT - otherB) < SNAP_THRESHOLD)
                    draggedConfig.Y = otherB;

                // Snap top edge alignment (horizontal rows)
                if (Math.Abs(dragT - otherT) < SNAP_THRESHOLD)
                    draggedConfig.Y = otherT;

                // Snap bottom edge alignment
                if (Math.Abs(dragB - otherB) < SNAP_THRESHOLD)
                    draggedConfig.Y = otherB - draggedConfig.CurrentResolution.Height;

                // Snap left edge alignment (vertical columns)
                if (Math.Abs(dragL - otherL) < SNAP_THRESHOLD)
                    draggedConfig.X = otherL;

                // Snap right edge alignment
                if (Math.Abs(dragR - otherR) < SNAP_THRESHOLD)
                    draggedConfig.X = otherR - draggedConfig.CurrentResolution.Width;
            }
        }

        private void MonitorCanvas_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_draggingBorder != null)
            {
                var draggedConfig = _draggingBorder.Tag as MonitorConfig;
                _draggingBorder.ReleaseMouseCapture();
                _draggingBorder = null;

                if (draggedConfig != null)
                {
                    SnapToNearestEdge(draggedConfig);

                    _isUpdatingPos = true;
                    TxtPosX.Text = draggedConfig.X.ToString();
                    TxtPosY.Text = draggedConfig.Y.ToString();
                    _isUpdatingPos = false;
                }

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

    }
}
