using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Collections.Generic;

namespace DisplayManagerClone
{
    public partial class MainWindow : Window
    {
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
                        Action? action = GetActionFromId(config.ActionId);
                        if (action != null && _hotkeyManager != null)
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
                    if (_hotkeyConfigs != null)
                    {
                        if (_hotkeyConfigs != null)
                {
                    _hotkeyConfigManager.SaveHotkeys(new System.Collections.Generic.List<HotkeyConfig>(_hotkeyConfigs));
                }
                    }
                }
            }
        }

        private Action? GetActionFromId(string id)
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

        private string? GetConflictWarning(uint modifiers, uint key)
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
                    string? warning = GetConflictWarning(_currentCapturedModifiers, _currentCapturedKey);
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
                    
                    if (_hotkeyConfigs != null)
                    {
                        if (_hotkeyConfigs != null)
                {
                    _hotkeyConfigManager.SaveHotkeys(new System.Collections.Generic.List<HotkeyConfig>(_hotkeyConfigs));
                }
                    }
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
                
                if (_hotkeyConfigs != null)
                {
                    _hotkeyConfigManager.SaveHotkeys(new System.Collections.Generic.List<HotkeyConfig>(_hotkeyConfigs));
                }
                LoadAndRegisterHotkeys(); // Re-register all
                
                TxtHotkeyInput.Text = "Click here and press a key combo...";
                _currentCapturedModifiers = 0;
                _currentCapturedKey = 0;
            }
        }

    }
}
