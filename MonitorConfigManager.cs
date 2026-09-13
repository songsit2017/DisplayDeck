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
            int displayIndex = 1;
            NativeMethods.DISPLAY_DEVICE d = new NativeMethods.DISPLAY_DEVICE();
            d.cb = Marshal.SizeOf(d);

            while (NativeMethods.EnumDisplayDevices(null, id, ref d, 0))
            {
                if ((d.StateFlags & 1) != 0) // DISPLAY_DEVICE_ATTACHED_TO_DESKTOP
                {
                    string adapterName = d.DeviceName;
                    string displayName = d.DeviceString; // Fallback to GPU name

                    NativeMethods.DISPLAY_DEVICE monitorDevice = new NativeMethods.DISPLAY_DEVICE();
                    monitorDevice.cb = Marshal.SizeOf(monitorDevice);
                    
                    if (NativeMethods.EnumDisplayDevices(adapterName, 0, ref monitorDevice, 0))
                    {
                        if (!string.IsNullOrEmpty(monitorDevice.DeviceString))
                        {
                            displayName = monitorDevice.DeviceString;
                        }
                    }

                    displayName = $"Display {displayIndex} ({displayName})";

                    var config = new MonitorConfig
                    {
                        DeviceName = adapterName,
                        DisplayName = displayName,
                        IsPrimary = (d.StateFlags & 4) != 0 // DISPLAY_DEVICE_PRIMARY_DEVICE
                    };

                    displayIndex++;

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
            var failedMonitors = new System.Collections.Generic.List<string>();

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
                    devMode.dmFields = NativeMethods.DM_POSITION | NativeMethods.DM_PELSWIDTH | NativeMethods.DM_PELSHEIGHT | NativeMethods.DM_DISPLAYFREQUENCY;

                    uint flags = NativeMethods.CDS_UPDATEREGISTRY | NativeMethods.CDS_NORESET;
                    if (monitor.IsPrimary)
                    {
                        flags |= NativeMethods.CDS_SET_PRIMARY;
                    }

                    int result = NativeMethods.ChangeDisplaySettingsEx(monitor.DeviceName, ref devMode, IntPtr.Zero, flags, IntPtr.Zero);
                    if (result != NativeMethods.DISP_CHANGE_SUCCESSFUL)
                    {
                        failedMonitors.Add($"{monitor.DisplayName} (error code: {result})");
                    }
                }
            }

            // Apply all changes
            NativeMethods.ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);

            if (failedMonitors.Count > 0)
            {
                throw new Exception($"Failed to apply settings for:\n{string.Join("\n", failedMonitors)}");
            }
        }
    }
}
