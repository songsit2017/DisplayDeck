using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DisplayManagerClone
{
    public class MonitorInfo
    {
        public uint Index { get; set; }
        public string DevicePath { get; set; }
        public string DisplayName { get; set; }
        public string CurrentWallpaper { get; set; }
    }

    public class WallpaperManager
    {
        private IDesktopWallpaper _desktopWallpaper;

        public WallpaperManager()
        {
            _desktopWallpaper = (IDesktopWallpaper)new DesktopWallpaperClass();
        }

        public List<MonitorInfo> GetMonitors()
        {
            var monitors = new List<MonitorInfo>();
            uint count = _desktopWallpaper.GetMonitorDevicePathCount();

            for (uint i = 0; i < count; i++)
            {
                string devicePath = _desktopWallpaper.GetMonitorDevicePathAt(i);
                string wallpaper = _desktopWallpaper.GetWallpaper(devicePath);

                monitors.Add(new MonitorInfo
                {
                    Index = i,
                    DevicePath = devicePath,
                    DisplayName = $"Monitor {i + 1}",
                    CurrentWallpaper = wallpaper
                });
            }

            return monitors;
        }

        public string GetWallpaperForIndex(int index)
        {
            try
            {
                string devicePath = _desktopWallpaper.GetMonitorDevicePathAt((uint)index);
                return _desktopWallpaper.GetWallpaper(devicePath);
            }
            catch
            {
                return null;
            }
        }

        public void SetWallpaper(string monitorDevicePath, string imagePath)
        {
            try
            {
                // We don't set position here anymore, we let the UI decide
                _desktopWallpaper.SetWallpaper(monitorDevicePath, imagePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set wallpaper: {ex.Message}");
                throw;
            }
        }

        public void SetPosition(DesktopWallpaperPosition position)
        {
            try
            {
                _desktopWallpaper.SetPosition(position);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set position: {ex.Message}");
            }
        }
    }
}
