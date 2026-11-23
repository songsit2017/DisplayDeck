using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;

namespace DisplayManagerClone
{
    public partial class TitleBarOverlay : Window
    {
        private IntPtr _targetHwnd = IntPtr.Zero;
        public IntPtr TargetHwnd => _targetHwnd;
        private readonly Popup _snapPicker = new() { StaysOpen = false, AllowsTransparency = true };
        public bool IsSnapPickerOpen => _snapPicker.IsOpen;

        public TitleBarOverlay()
        {
            InitializeComponent();
            _snapPicker.PlacementTarget = BtnSnap;
            IsVisibleChanged += (_, _) => { if (!IsVisible) _snapPicker.IsOpen = false; };
        }

        private void BtnSnap_Click(object sender, RoutedEventArgs e)
        {
            var target = _targetHwnd;
            var monitors = WindowManager.GetAllMonitorHandles()
                .Select(handle => (Handle: handle, Info: new NativeMethods.MONITORINFO()))
                .Where(m => NativeMethods.GetMonitorInfo(m.Handle, m.Info) && m.Info.rcWork.Width > 0 && m.Info.rcWork.Height > 0)
                .ToArray();
            if (!NativeMethods.IsWindowVisible(target) || monitors.Length == 0) return;

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
                    var button = new Button { Content = zone.ToString(), Margin = new Thickness(1) };
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
                    BorderBrush = monitor.Handle == currentMonitor ? Brushes.DeepSkyBlue : Brushes.Gray,
                    Child = zones
                };
                Canvas.SetLeft(card, (bounds.left - left) * scale);
                Canvas.SetTop(card, (bounds.top - top) * scale);
                map.Children.Add(card);
            }

            _snapPicker.Child = new Border { Background = Brushes.WhiteSmoke, Padding = new Thickness(8), Child = map };
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
