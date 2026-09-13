using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace DisplayManagerClone
{
    public class HotkeyConfig : INotifyPropertyChanged
    {
        private uint _modifiers;
        private uint _key;

        public string ActionId { get; set; }
        public string ActionName { get; set; }

        public uint Modifiers
        {
            get => _modifiers;
            set { _modifiers = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayHotkey)); }
        }

        public uint Key
        {
            get => _key;
            set { _key = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayHotkey)); }
        }

        public string DisplayHotkey
        {
            get
            {
                if (Key == 0) return "None";
                
                string mods = "";
                if ((Modifiers & HotkeyManager.MOD_CTRL) != 0) mods += "Ctrl + ";
                if ((Modifiers & HotkeyManager.MOD_WIN) != 0) mods += "Win + ";
                if ((Modifiers & HotkeyManager.MOD_ALT) != 0) mods += "Alt + ";
                if ((Modifiers & HotkeyManager.MOD_SHIFT) != 0) mods += "Shift + ";

                Key wpfKey = KeyInterop.KeyFromVirtualKey((int)Key);
                string keyStr = wpfKey.ToString();

                // Handle some formatting edge cases
                if (keyStr.StartsWith("D") && keyStr.Length == 2 && char.IsDigit(keyStr[1]))
                    keyStr = keyStr[1].ToString();
                
                return mods + keyStr;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
