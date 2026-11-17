using System;
using System.Windows;
using System.Windows.Interop;

namespace DisplayManagerClone
{
    public partial class TitleBarOverlay : Window
    {
        private IntPtr _targetHwnd = IntPtr.Zero;

        public TitleBarOverlay()
        {
            InitializeComponent();
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
                WindowManager.MoveActiveWindowToPreviousMonitor();
            }
        }

        private void BtnNextMonitor_Click(object sender, RoutedEventArgs e)
        {
            if (_targetHwnd != IntPtr.Zero && NativeMethods.IsWindowVisible(_targetHwnd))
            {
                WindowManager.MoveActiveWindowToNextMonitor();
            }
        }

        private void BtnPinTop_Click(object sender, RoutedEventArgs e)
        {
            if (_targetHwnd != IntPtr.Zero && NativeMethods.IsWindowVisible(_targetHwnd))
            {
                WindowManager.ToggleWindowAlwaysOnTop();
            }
        }
    }
}
