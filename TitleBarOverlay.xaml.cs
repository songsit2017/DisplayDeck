using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DisplayManagerClone
{
    public partial class TitleBarOverlay : Window
    {
        private IntPtr _targetHwnd = IntPtr.Zero;
        public IntPtr TargetHwnd => _targetHwnd;
        private readonly Popup _snapPicker = new() { StaysOpen = false, AllowsTransparency = true };
        public bool IsSnapPickerOpen => _snapPicker.IsOpen;
        private readonly DispatcherTimer _hoverTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
        private IntPtr _hoverTarget;
        private long _hoverSince;
        private long _lastNearPicker;
        private bool _openedByHover;

        public TitleBarOverlay()
        {
            InitializeComponent();
            _snapPicker.PlacementTarget = BtnSnap;
            IsVisibleChanged += (_, _) => { if (!IsVisible && !_openedByHover) _snapPicker.IsOpen = false; };
            _hoverTimer.Tick += CheckMaximizeHover;
            Closed += (_, _) => { _hoverTimer.Stop(); _snapPicker.IsOpen = false; };
        }

        public void SetMaximizeHoverEnabled(bool enabled)
        {
            if (enabled) _hoverTimer.Start();
            else
            {
                _hoverTimer.Stop();
                _hoverTarget = IntPtr.Zero;
                if (_openedByHover) _snapPicker.IsOpen = false;
            }
        }

        private static bool IsOverMaximize(IntPtr target)
        {
            if (target == IntPtr.Zero || !NativeMethods.GetCursorPos(out var point)) return false;
            var underCursor = NativeMethods.WindowFromPoint(point);
            const uint rootWindow = 2;
            if (underCursor != target && NativeMethods.GetAncestor(underCursor, rootWindow) != target) return false;

            const uint hitTestMessage = 0x0084;
            const uint abortIfHung = 0x0002;
            const int maximizeButton = 9;
            int position = (point.x & 0xffff) | ((point.y & 0xffff) << 16);
            return NativeMethods.SendMessageTimeout(target, hitTestMessage, IntPtr.Zero,
                new IntPtr(position), abortIfHung, 30, out var hit) != IntPtr.Zero && hit.ToInt64() == maximizeButton;
        }

        private void CheckMaximizeHover(object? sender, EventArgs e)
        {
            var target = NativeMethods.GetForegroundWindow();
            NativeMethods.GetWindowThreadProcessId(target, out uint pid);
            long now = Environment.TickCount64;
            bool overMaximize = pid != 0 && pid != (uint)Environment.ProcessId && IsOverMaximize(target);
            const int leftMouseButton = 1;
            bool mouseDown = NativeMethods.GetAsyncKeyState(leftMouseButton) < 0;

            if (_snapPicker.IsOpen)
            {
                if (!_openedByHover) return;
                bool overPicker = _snapPicker.Child?.IsMouseOver == true;
                if (overMaximize || overPicker) _lastNearPicker = now;
                if ((mouseDown && !overPicker) || (!overPicker && target != _hoverTarget) || now - _lastNearPicker > 450)
                {
                    _snapPicker.IsOpen = false;
                    _hoverTarget = IntPtr.Zero;
                }
                return;
            }

            if (!overMaximize || mouseDown) { _hoverTarget = IntPtr.Zero; return; }
            if (_hoverTarget != target) { _hoverTarget = target; _hoverSince = now; return; }
            if (now - _hoverSince < 450) return;
            _lastNearPicker = now;
            ShowSnapPicker(target, fromHover: true);
            _hoverSince = now;
        }

        private void BtnSnap_Click(object sender, RoutedEventArgs e) => ShowSnapPicker(_targetHwnd, fromHover: false);

        private void ShowSnapPicker(IntPtr target, bool fromHover)
        {
            var monitors = WindowManager.GetAllMonitorHandles()
                .Select(handle => (Handle: handle, Info: new NativeMethods.MONITORINFOEX()))
                .Where(m => NativeMethods.GetMonitorInfo(m.Handle, m.Info) && m.Info.rcWork.Width > 0 && m.Info.rcWork.Height > 0)
                .ToArray();
            if (!NativeMethods.IsWindowVisible(target) || monitors.Length == 0) return;

            bool dark = Equals(Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1), 0);
            bool highContrast = SystemParameters.HighContrast;
            static Brush Shade(byte value, byte opacity = 255) => new SolidColorBrush(Color.FromArgb(opacity, value, value, value));
            Brush panel = highContrast ? SystemColors.WindowBrush : Shade(dark ? (byte)25 : (byte)248, 182);
            Brush zoneBackground = highContrast ? SystemColors.ControlBrush : Shade(dark ? (byte)78 : (byte)222, 116);
            Brush foreground = highContrast ? SystemColors.ControlTextBrush : dark ? Brushes.WhiteSmoke : Brushes.Black;
            Brush outline = highContrast ? SystemColors.WindowTextBrush : Shade(dark ? (byte)115 : (byte)148);
            Brush selected = highContrast ? SystemColors.HighlightBrush : dark ? Brushes.DeepSkyBlue : Brushes.RoyalBlue;
            var zoneStyle = (Style)Resources["SnapZoneButtonStyle"];

            int left = monitors.Min(m => m.Info.rcMonitor.left);
            int top = monitors.Min(m => m.Info.rcMonitor.top);
            int width = monitors.Max(m => m.Info.rcMonitor.right) - left;
            int height = monitors.Max(m => m.Info.rcMonitor.bottom) - top;
            double scale = Math.Min(520.0 / width, 380.0 / height);
            double scaleX = Math.Max(scale, monitors.Max(m => 168.0 / m.Info.rcMonitor.Width));
            double scaleY = scale;
            var map = new Canvas { Width = width * scaleX };
            var cards = new List<(Border Card, NativeMethods.RECT Bounds)>();
            var currentMonitor = NativeMethods.MonitorFromWindow(target, NativeMethods.MONITOR_DEFAULTTONEAREST);
            foreach (var monitor in monitors)
            {
                var bounds = monitor.Info.rcMonitor;
                var zones = new Grid();
                zones.ColumnDefinitions.Add(new ColumnDefinition());
                zones.ColumnDefinitions.Add(new ColumnDefinition());
                zones.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 22 });

                const string displayPrefix = @"\\.\DISPLAY";
                string device = monitor.Info.szDevice;
                string label = device.StartsWith(displayPrefix, StringComparison.OrdinalIgnoreCase)
                    ? $"Display {device[displayPrefix.Length..]}"
                    : device;
                var header = new TextBlock
                {
                    Text = label, Foreground = foreground, FontWeight = FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(5, 2, 5, 2)
                };
                Grid.SetColumnSpan(header, 2);
                zones.Children.Add(header);
                var choices = new List<(SnapZone Zone, string Caption)>
                {
                    (SnapZone.Left, "Left"), (SnapZone.Right, "Right"),
                    (SnapZone.Top, "Top"), (SnapZone.Bottom, "Bottom"),
                    (SnapZone.TopLeft, "Top left"), (SnapZone.TopRight, "Top right"),
                    (SnapZone.BottomLeft, "Bottom left"), (SnapZone.BottomRight, "Bottom right")
                };
                var work = monitor.Info.rcWork;
                if (work.Width >= 2400 && work.Width >= work.Height * 1.6)
                {
                    choices.AddRange(new[]
                    {
                        (SnapZone.FirstThird, "Left ⅓"), (SnapZone.MiddleThird, "Center ⅓"),
                        (SnapZone.LastThird, "Right ⅓"), (SnapZone.FirstTwoThirds, "Left ⅔"),
                        (SnapZone.LastTwoThirds, "Right ⅔")
                    });
                }
                if (work.Width >= 3200 && work.Width >= work.Height * 2.0)
                {
                    choices.AddRange(new[]
                    {
                        (SnapZone.FirstQuarter, "Column 1 of 4"), (SnapZone.SecondQuarter, "Column 2 of 4"),
                        (SnapZone.ThirdQuarter, "Column 3 of 4"), (SnapZone.LastQuarter, "Column 4 of 4")
                    });
                }
                int fullRow = 1 + (choices.Count + 1) / 2;
                const double minimumRowHeight = 26;
                for (int row = 1; row <= fullRow; row++)
                    zones.RowDefinitions.Add(new RowDefinition { MinHeight = minimumRowHeight });
                scaleY = Math.Max(scaleY, (30 + fullRow * minimumRowHeight) / bounds.Height);
                choices.Add((SnapZone.Full, "Full"));
                for (int index = 0; index < choices.Count; index++)
                {
                    var (zone, caption) = choices[index];
                    var button = new Button
                    {
                        Content = caption, Margin = new Thickness(1), Style = zoneStyle,
                        ToolTip = $"{label} · {caption}",
                        Background = zoneBackground, Foreground = foreground, BorderBrush = outline
                    };
                    button.Click += (_, _) =>
                    {
                        _snapPicker.IsOpen = false;
                        if (!WindowManager.TrySnapWindow(target, monitor.Handle, zone))
                            MessageBox.Show("The window could not be snapped to that display.", "Snap across monitors");
                    };
                    Grid.SetRow(button, zone == SnapZone.Full ? fullRow : 1 + index / 2);
                    Grid.SetColumn(button, zone == SnapZone.Full ? 0 : index % 2);
                    if (zone == SnapZone.Full || (index == choices.Count - 2 && index % 2 == 0))
                        Grid.SetColumnSpan(button, 2);
                    zones.Children.Add(button);
                }
                var card = new Border
                {
                    Width = bounds.Width * scaleX,
                    BorderThickness = new Thickness(2),
                    BorderBrush = monitor.Handle == currentMonitor ? selected : outline,
                    CornerRadius = new CornerRadius(5),
                    Child = zones
                };
                cards.Add((card, bounds));
            }

            map.Height = height * scaleY;
            foreach (var (card, bounds) in cards)
            {
                card.Height = bounds.Height * scaleY;
                Canvas.SetLeft(card, (bounds.left - left) * scaleX);
                Canvas.SetTop(card, (bounds.top - top) * scaleY);
                map.Children.Add(card);
            }

            var viewport = new ScrollViewer
            {
                MaxWidth = 540, MaxHeight = 440, Content = map,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            _snapPicker.Child = new Border
            {
                Background = panel, BorderBrush = outline, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8), Padding = new Thickness(8), Child = viewport
            };
            _openedByHover = fromHover;
            _snapPicker.StaysOpen = fromHover;
            _snapPicker.PlacementTarget = fromHover ? null : BtnSnap;
            _snapPicker.Placement = fromHover ? PlacementMode.MousePoint : PlacementMode.Bottom;
            _snapPicker.VerticalOffset = fromHover ? 8 : 0;
            _snapPicker.IsOpen = true;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Make window click-through non-activating so clicking our buttons doesn't steal focus from target
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);
        }

        public void AttachTo(IntPtr targetHwnd, double left, double top)
        {
            _targetHwnd = targetHwnd;
            this.Left = left;
            this.Top = top;

            if (!this.IsVisible)
            {
                this.Show();
            }
        }

        private void BtnPrevMonitor_Click(object sender, RoutedEventArgs e)
        {
            if (_targetHwnd != IntPtr.Zero && NativeMethods.IsWindowVisible(_targetHwnd))
            {
                WindowManager.MoveWindowToPreviousMonitor(_targetHwnd);
            }
        }

        private void BtnNextMonitor_Click(object sender, RoutedEventArgs e)
        {
            if (_targetHwnd != IntPtr.Zero && NativeMethods.IsWindowVisible(_targetHwnd))
            {
                WindowManager.MoveWindowToNextMonitor(_targetHwnd);
            }
        }

        private void BtnPinTop_Click(object sender, RoutedEventArgs e)
        {
            if (_targetHwnd != IntPtr.Zero && NativeMethods.IsWindowVisible(_targetHwnd))
            {
                WindowManager.ToggleWindowAlwaysOnTop(_targetHwnd);
            }
        }
    }
}
