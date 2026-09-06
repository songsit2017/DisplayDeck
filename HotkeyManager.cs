using System;
using System.Windows.Interop;

namespace DisplayManagerClone
{
    public class HotkeyManager : IDisposable
    {
        private HwndSource _source;
        private int _hotkeyId = 9000;
        private Action _onHotkeyPressed;

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CTRL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint VK_RIGHT = 0x27;
        public const uint VK_X = 0x58;

        public HotkeyManager(IntPtr hwnd, Action onHotkeyPressed)
        {
            _onHotkeyPressed = onHotkeyPressed;
            _source = HwndSource.FromHwnd(hwnd);
            _source.AddHook(HwndHook);

            // Register Ctrl + Win + X
            bool success = NativeMethods.RegisterHotKey(hwnd, _hotkeyId, MOD_CTRL | MOD_WIN, VK_X);
            if (!success)
            {
                System.Windows.MessageBox.Show("ไม่สามารถตั้งค่าคีย์ลัด Ctrl + Win + X ได้ (อาจมีโปรแกรมอื่นใช้งานอยู่)", "Warning", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == _hotkeyId)
            {
                _onHotkeyPressed?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_source != null)
            {
                NativeMethods.UnregisterHotKey(_source.Handle, _hotkeyId);
                _source.RemoveHook(HwndHook);
                _source = null;
            }
        }
    }
}
