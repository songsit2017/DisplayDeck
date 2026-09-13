using System;
using System.Collections.Generic;
using System.Windows.Interop;

namespace DisplayManagerClone
{
    public class HotkeyManager : IDisposable
    {
        private HwndSource _source;
        private int _currentHotkeyId = 9000;
        private Dictionary<int, Action> _hotkeyActions = new Dictionary<int, Action>();

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CTRL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint VK_RIGHT = 0x27;
        public const uint VK_LEFT = 0x25;
        public const uint VK_UP = 0x26;
        public const uint VK_DOWN = 0x28;
        public const uint VK_X = 0x58;
        public const uint VK_M = 0x4D;
        public const uint VK_R = 0x52;
        public const uint VK_C = 0x43;
        public const uint VK_T = 0x54;

        public HotkeyManager(IntPtr hwnd)
        {
            _source = HwndSource.FromHwnd(hwnd);
            _source.AddHook(HwndHook);
        }

        public bool RegisterHotkey(uint modifiers, uint key, Action action)
        {
            int id = ++_currentHotkeyId;
            bool success = NativeMethods.RegisterHotKey(_source.Handle, id, modifiers, key);
            if (success)
            {
                _hotkeyActions[id] = action;
            }
            return success;
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (_hotkeyActions.TryGetValue(id, out Action action))
                {
                    action?.Invoke();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_source != null)
            {
                foreach (var id in _hotkeyActions.Keys)
                {
                    NativeMethods.UnregisterHotKey(_source.Handle, id);
                }
                _source.RemoveHook(HwndHook);
                _source = null;
            }
        }
    }
}
