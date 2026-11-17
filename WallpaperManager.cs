using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DisplayManagerClone
{
    public class MonitorInfo
    {
        public uint Index { get; set; }
        public string DevicePath { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string CurrentWallpaper { get; set; } = string.Empty;
        public NativeMethods.RECT Rect { get; set; }
    }

    public class WallpaperManager : IDisposable
    {
        private IDesktopWallpaper? _desktopWallpaper;

        public WallpaperManager()
        {
            _desktopWallpaper = (IDesktopWallpaper)new DesktopWallpaperClass();
        }

        public List<MonitorInfo> GetMonitors()
        {
            var monitors = new List<MonitorInfo>();
            if (_desktopWallpaper == null) return monitors;
            uint count = _desktopWallpaper.GetMonitorDevicePathCount();

            for (uint i = 0; i < count; i++)
            {
                string devicePath = _desktopWallpaper.GetMonitorDevicePathAt(i);
                string wallpaper = _desktopWallpaper.GetWallpaper(devicePath);
                
                NativeMethods.RECT rect = new NativeMethods.RECT();
                _desktopWallpaper.GetMonitorRECT(devicePath, out rect);

                monitors.Add(new MonitorInfo
                {
                    Index = i,
                    DevicePath = devicePath,
                    DisplayName = $"Monitor {i + 1}",
                    CurrentWallpaper = wallpaper,
                    Rect = rect
                });
            }

            return monitors;
        }

        public string? GetWallpaperForIndex(int index)
        {
            try
            {
                if (_desktopWallpaper == null) return null;
                string devicePath = _desktopWallpaper.GetMonitorDevicePathAt((uint)index);
                return _desktopWallpaper.GetWallpaper(devicePath);
            }
            catch
            {
                return null;
            }
        }

        public void SetWallpaper(string? monitorDevicePath, string imagePath)
        {
            try
            {
                _desktopWallpaper?.SetWallpaper(monitorDevicePath, imagePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set wallpaper: {ex.Message}");
                throw;
            }
        }

        public void SetBackgroundColor(uint color)
        {
            _desktopWallpaper?.SetBackgroundColor(color);
        }

        public void SetPosition(DesktopWallpaperPosition position)
        {
            _desktopWallpaper?.SetPosition(position);
        }

        public void Dispose()
        {
            if (_desktopWallpaper != null)
            {
                Marshal.ReleaseComObject(_desktopWallpaper);
                _desktopWallpaper = null;
            }
        }
    }
}
