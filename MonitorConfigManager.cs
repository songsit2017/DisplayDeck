using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace DisplayManagerClone
{
    public class Resolution
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int Frequency { get; set; }

        public override string ToString()
        {
            return $"{Width} x {Height} @ {Frequency}Hz";
        }

        public override bool Equals(object obj)
        {
            if (obj is Resolution r)
                return Width == r.Width && Height == r.Height && Frequency == r.Frequency;
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Width, Height, Frequency);
        }
    }

    public class MonitorConfig
    {
        public string DeviceName { get; set; }
        public string DisplayName { get; set; }
        public bool IsPrimary { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public Resolution CurrentResolution { get; set; }
        public List<Resolution> SupportedResolutions { get; set; } = new List<Resolution>();
    }

    public class MonitorConfigManager
    {
        public List<MonitorConfig> GetMonitors()
        {
            var monitors = new List<MonitorConfig>();
            uint id = 0;
            NativeMethods.DISPLAY_DEVICE d = new NativeMethods.DISPLAY_DEVICE();
            d.cb = Marshal.SizeOf(d);

            while (NativeMethods.EnumDisplayDevices(null, id, ref d, 0))
            {
                if ((d.StateFlags & 1) != 0) // DISPLAY_DEVICE_ATTACHED_TO_DESKTOP
                {
                    var config = new MonitorConfig
                    {
                        DeviceName = d.DeviceName,
                        DisplayName = d.DeviceString,
                        IsPrimary = (d.StateFlags & 4) != 0 // DISPLAY_DEVICE_PRIMARY_DEVICE
                    };

                    NativeMethods.DEVMODE devMode = new NativeMethods.DEVMODE();
                    devMode.dmSize = (short)Marshal.SizeOf(devMode);

                    if (NativeMethods.EnumDisplaySettings(d.DeviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode))
                    {
                        config.X = devMode.dmPositionX;
                        config.Y = devMode.dmPositionY;
                        config.CurrentResolution = new Resolution
                        {
                            Width = devMode.dmPelsWidth,
                            Height = devMode.dmPelsHeight,
                            Frequency = devMode.dmDisplayFrequency
                        };
                    }

                    // Get supported resolutions
                    int modeNum = 0;
                    var resolutions = new HashSet<Resolution>();
                    while (NativeMethods.EnumDisplaySettings(d.DeviceName, modeNum, ref devMode))
                    {
                        resolutions.Add(new Resolution
                        {
                            Width = devMode.dmPelsWidth,
                            Height = devMode.dmPelsHeight,
                            Frequency = devMode.dmDisplayFrequency
                        });
                        modeNum++;
                    }
                    config.SupportedResolutions = resolutions.OrderByDescending(r => r.Width).ThenByDescending(r => r.Height).ThenByDescending(r => r.Frequency).ToList();
                    
                    monitors.Add(config);
                }
                id++;
            }
            return monitors;
        }

        public void ApplySettings(List<MonitorConfig> monitors)
        {
            foreach (var monitor in monitors)
            {
                NativeMethods.DEVMODE devMode = new NativeMethods.DEVMODE();
                devMode.dmSize = (short)Marshal.SizeOf(devMode);
                
                if (NativeMethods.EnumDisplaySettings(monitor.DeviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode))
                {
                    devMode.dmPositionX = monitor.X;
                    devMode.dmPositionY = monitor.Y;
                    devMode.dmPelsWidth = monitor.CurrentResolution.Width;
                    devMode.dmPelsHeight = monitor.CurrentResolution.Height;
                    devMode.dmDisplayFrequency = monitor.CurrentResolution.Frequency;
                    devMode.dmFields = NativeMethods.DM_POSITION | NativeMethods.DM_PELSWIDTH | NativeMethods.DM_PELSHEIGHT;

                    uint flags = NativeMethods.CDS_UPDATEREGISTRY | NativeMethods.CDS_NORESET;
                    if (monitor.IsPrimary)
                    {
                        flags |= NativeMethods.CDS_SET_PRIMARY;
                    }

                    NativeMethods.ChangeDisplaySettingsEx(monitor.DeviceName, ref devMode, IntPtr.Zero, flags, IntPtr.Zero);
                }
            }

            // Apply all changes
            NativeMethods.ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
        }
    }
}
