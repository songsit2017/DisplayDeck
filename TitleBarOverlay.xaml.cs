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
                .Select(handle => (Handle: handle, Info: new NativeMethods.MONITORINFO()))
                .Where(m => NativeMethods.GetMonitorInfo(m.Handle, m.Info) && m.Info.rcWork.Width > 0 && m.Info.rcWork.Height > 0)
                .ToArray();
            if (!NativeMethods.IsWindowVisible(target) || monitors.Length == 0) return;

            bool dark = Equals(Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1), 0);
            bool highContrast = SystemParameters.HighContrast;
            static Brush Shade(byte value) => new SolidColorBrush(Color.FromRgb(value, value, value));
            Brush panel = highContrast ? SystemColors.WindowBrush : Shade(dark ? (byte)32 : (byte)248);
            Brush zoneBackground = highContrast ? SystemColors.ControlBrush : Shade(dark ? (byte)54 : (byte)234);
            Brush foreground = highContrast ? SystemColors.ControlTextBrush : dark ? Brushes.WhiteSmoke : Brushes.Black;
            Brush outline = highContrast ? SystemColors.WindowTextBrush : Shade(dark ? (byte)115 : (byte)148);
            Brush selected = highContrast ? SystemColors.HighlightBrush : dark ? Brushes.DeepSkyBlue : Brushes.RoyalBlue;
            var zoneStyle = (Style)Resources["SnapZoneButtonStyle"];

            int left = monitors.Min(m => m.Info.rcMonitor.left);
            int top = monitors.Min(m => m.Info.rcMonitor.top);
            int width = monitors.Max(m => m.Info.rcMonitor.right) - left;
            int height = monitors.Max(m => m.Info.rcMonitor.bottom) - top;
            double scale = Math.Min(420.0 / width, 300.0 / height);
            var map = new Canvas { Width = width * scale, Height = height * scale };
            var currentMonitor = NativeMethods.MonitorFromWindow(target, NativeMethods.MONITOR_DEFAULTTONEAREST);
            foreach (var monitor in monitors)
            {
                var bounds = monitor.Info.rcMonitor;
                var zones = new UniformGrid { Columns = 2, Rows = 2 };
                foreach (var zone in new[] { SnapZone.Left, SnapZone.Right, SnapZone.Full })
                {
                    var button = new Button
                    {
                        Content = zone.ToString(), Margin = new Thickness(1), Style = zoneStyle,
                        Background = zoneBackground, Foreground = foreground, BorderBrush = outline
                    };
                    button.Click += (_, _) =>
                    {
                        _snapPicker.IsOpen = false;
                        if (!WindowManager.TrySnapWindow(target, monitor.Handle, zone))
                            MessageBox.Show("The window could not be snapped to that display.", "Snap across monitors");
                    };
                    zones.Children.Add(button);
                }
                var card = new Border
                {
                    Width = bounds.Width * scale,
                    Height = bounds.Height * scale,
                    BorderThickness = new Thickness(2),
                    BorderBrush = monitor.Handle == currentMonitor ? selected : outline,
                    CornerRadius = new CornerRadius(5),
                    Child = zones
                };
                Canvas.SetLeft(card, (bounds.left - left) * scale);
                Canvas.SetTop(card, (bounds.top - top) * scale);
                map.Children.Add(card);
            }

            _snapPicker.Child = new Border
            {
                Background = panel, BorderBrush = outline, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8), Padding = new Thickness(8), Child = map
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
