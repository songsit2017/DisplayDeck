using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DisplayManagerClone
{
    public class HotkeyConfigManager
    {
        private readonly string _configFilePath;

        public HotkeyConfigManager()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "DisplayManagerClone");
            Directory.CreateDirectory(folder);
            _configFilePath = Path.Combine(folder, "hotkeys.json");
        }

        public List<HotkeyConfig> LoadHotkeys()
        {
            if (File.Exists(_configFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_configFilePath);
                    var list = JsonSerializer.Deserialize<List<HotkeyConfig>>(json);
                    if (list != null) return list;
                }
                catch (Exception)
                {
                    // If error loading, fallback to default
                }
            }
            return GetDefaultHotkeys();
        }

        public void SaveHotkeys(List<HotkeyConfig> hotkeys)
        {
            try
            {
                string json = JsonSerializer.Serialize(hotkeys, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving hotkeys: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private List<HotkeyConfig> GetDefaultHotkeys()
        {
            return new List<HotkeyConfig>
            {
                new HotkeyConfig { ActionId = "NextMonitor", ActionName = "Move Window to Next Monitor", Modifiers = 0, Key = 0 },
                new HotkeyConfig { ActionId = "PrevMonitor", ActionName = "Move Window to Previous Monitor", Modifiers = 0, Key = 0 },
                new HotkeyConfig { ActionId = "PrimaryMonitor", ActionName = "Move Window to Primary Monitor", Modifiers = 0, Key = 0 },
                new HotkeyConfig { ActionId = "CenterMonitor", ActionName = "Move Window to Center of Monitor", Modifiers = 0, Key = 0 },
                new HotkeyConfig { ActionId = "Center75", ActionName = "Move Window to Center of Monitor and Size to 75%", Modifiers = 0, Key = 0 },
                new HotkeyConfig { ActionId = "CornerTL", ActionName = "Move Window to Top-Left Corner and Size 50%", Modifiers = 0, Key = 0 },
                new HotkeyConfig { ActionId = "CornerTR", ActionName = "Move Window to Top-Right Corner and Size 50%", Modifiers = 0, Key = 0 },
                new HotkeyConfig { ActionId = "CornerBL", ActionName = "Move Window to Bottom-Left Corner and Size 50%", Modifiers = 0, Key = 0 },
                new HotkeyConfig { ActionId = "CornerBR", ActionName = "Move Window to Bottom-Right Corner and Size 50%", Modifiers = 0, Key = 0 },
                new HotkeyConfig { ActionId = "SideLeft", ActionName = "Size and Move Window to Left Side of Monitor", Modifiers = 0, Key = 0 },
                new HotkeyConfig { ActionId = "SideRight", ActionName = "Size and Move Window to Right Side of Monitor", Modifiers = 0, Key = 0 },
                new HotkeyConfig { ActionId = "SideTop", ActionName = "Size and Move Window to Top Side of Monitor", Modifiers = 0, Key = 0 },
                new HotkeyConfig { ActionId = "SideBottom", ActionName = "Size and Move Window to Bottom Side of Monitor", Modifiers = 0, Key = 0 },
                new HotkeyConfig { ActionId = "SpanAll", ActionName = "Span Window Across All Monitors", Modifiers = 0, Key = 0 },
                new HotkeyConfig { ActionId = "Maximize", ActionName = "Maximize Window", Modifiers = 0, Key = 0 },
                new HotkeyConfig { ActionId = "Minimize", ActionName = "Minimize Window", Modifiers = 0, Key = 0 },
                new HotkeyConfig { ActionId = "Restore", ActionName = "Restore Window", Modifiers = 0, Key = 0 },
                new HotkeyConfig { ActionId = "AlwaysOnTop", ActionName = "Toggle Window Always on Top", Modifiers = 0, Key = 0 }
            };
        }
    }
}
