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

        public override bool Equals(object? obj)
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
        public string DeviceName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Orientation { get; set; } // 0=0 deg, 1=90 deg, 2=180 deg, 3=270 deg
        public Resolution CurrentResolution { get; set; } = new Resolution();
        public List<Resolution> SupportedResolutions { get; set; } = new List<Resolution>();
    }

    public delegate int ChangeDisplaySettingsHandler(string? deviceName, ref NativeMethods.DEVMODE devMode, IntPtr hwnd, uint flags, IntPtr lParam);
    public delegate int ChangeDisplaySettingsCommitHandler(string? deviceName, IntPtr devMode, IntPtr hwnd, uint flags, IntPtr lParam);
    public delegate bool EnumDisplaySettingsHandler(string? deviceName, int modeNum, ref NativeMethods.DEVMODE devMode);

    public class MonitorConfigManager
    {
        public ChangeDisplaySettingsHandler ChangeDisplaySettingsFunc { get; set; } = NativeMethods.ChangeDisplaySettingsEx;
        public ChangeDisplaySettingsCommitHandler ChangeDisplaySettingsCommitFunc { get; set; } = (dev, ptr, hwnd, flags, param) => NativeMethods.ChangeDisplaySettingsEx(dev, ptr, hwnd, flags, param);
        public EnumDisplaySettingsHandler EnumDisplaySettingsFunc { get; set; } = NativeMethods.EnumDisplaySettings;
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
                        config.Orientation = devMode.dmDisplayOrientation;
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
            var devModes = new Dictionary<MonitorConfig, NativeMethods.DEVMODE>();
            var originalDevModes = new Dictionary<MonitorConfig, NativeMethods.DEVMODE>();
            var testFailures = new List<string>();

            // Phase 1: Capture originals + Prepare and Test settings for each monitor (CDS_TEST)
            foreach (var monitor in monitors)
            {
                // Save the original DEVMODE for rollback
                NativeMethods.DEVMODE originalDevMode = new NativeMethods.DEVMODE();
                originalDevMode.dmSize = (short)Marshal.SizeOf(originalDevMode);
                if (EnumDisplaySettingsFunc(monitor.DeviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref originalDevMode))
                {
                    originalDevModes[monitor] = originalDevMode;
                }

                NativeMethods.DEVMODE devMode = new NativeMethods.DEVMODE();
                devMode.dmSize = (short)Marshal.SizeOf(devMode);

                if (!EnumDisplaySettingsFunc(monitor.DeviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode))
                {
                    testFailures.Add($"{monitor.DisplayName}: Failed to query current display mode.");
                    continue;
                }

                devMode.dmPositionX = monitor.X;
                devMode.dmPositionY = monitor.Y;
                devMode.dmDisplayOrientation = monitor.Orientation;
                devMode.dmPelsWidth = monitor.CurrentResolution.Width;
                devMode.dmPelsHeight = monitor.CurrentResolution.Height;
                devMode.dmDisplayFrequency = monitor.CurrentResolution.Frequency;
                devMode.dmFields = NativeMethods.DM_POSITION | NativeMethods.DM_DISPLAYORIENTATION | NativeMethods.DM_PELSWIDTH | NativeMethods.DM_PELSHEIGHT | NativeMethods.DM_DISPLAYFREQUENCY;

                uint testFlags = NativeMethods.CDS_TEST;
                if (monitor.IsPrimary)
                {
                    testFlags |= NativeMethods.CDS_SET_PRIMARY;
                }

                int testResult = ChangeDisplaySettingsFunc(monitor.DeviceName, ref devMode, IntPtr.Zero, testFlags, IntPtr.Zero);
                if (testResult != NativeMethods.DISP_CHANGE_SUCCESSFUL)
                {
                    testFailures.Add($"{monitor.DisplayName}: Resolution/mode not supported (test error: {testResult})");
                }
                else
                {
                    devModes[monitor] = devMode;
                }
            }

            // If any monitor failed validation, abort completely without applying partial changes
            if (testFailures.Count > 0)
            {
                throw new Exception($"Validation failed for one or more monitors. No changes were applied:\n{string.Join("\n", testFailures)}");
            }

            // Phase 2: Stage settings in registry (CDS_UPDATEREGISTRY | CDS_NORESET)
            var stagedMonitors = new List<MonitorConfig>();
            string? stageError = null;
            foreach (var kvp in devModes)
            {
                var monitor = kvp.Key;
                var devMode = kvp.Value;

                uint flags = NativeMethods.CDS_UPDATEREGISTRY | NativeMethods.CDS_NORESET;
                if (monitor.IsPrimary)
                {
                    flags |= NativeMethods.CDS_SET_PRIMARY;
                }

                int stageResult = ChangeDisplaySettingsFunc(monitor.DeviceName, ref devMode, IntPtr.Zero, flags, IntPtr.Zero);
                if (stageResult != NativeMethods.DISP_CHANGE_SUCCESSFUL)
                {
                    stageError = $"{monitor.DisplayName} (staging error: {stageResult})";
                    break; // Stop staging, need rollback
                }
                stagedMonitors.Add(monitor);
            }

            if (stageError != null)
            {
                // Rollback: re-stage original DEVMODE for every monitor that was successfully staged
                foreach (var monitor in stagedMonitors)
                {
                    if (originalDevModes.TryGetValue(monitor, out var origDev))
                    {
                        var rollbackDev = origDev; // copy the struct
                        rollbackDev.dmFields = NativeMethods.DM_POSITION | NativeMethods.DM_DISPLAYORIENTATION | NativeMethods.DM_PELSWIDTH | NativeMethods.DM_PELSHEIGHT | NativeMethods.DM_DISPLAYFREQUENCY;
                        uint rollbackFlags = NativeMethods.CDS_UPDATEREGISTRY | NativeMethods.CDS_NORESET;
                        ChangeDisplaySettingsFunc(monitor.DeviceName, ref rollbackDev, IntPtr.Zero, rollbackFlags, IntPtr.Zero);
                    }
                }
                throw new Exception($"Failed to stage settings for {stageError}. All staged changes have been rolled back.");
            }

            // Phase 3: Final Commit
            int finalResult = ChangeDisplaySettingsCommitFunc(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
            if (finalResult != NativeMethods.DISP_CHANGE_SUCCESSFUL)
            {
                // Final commit failed — rollback all monitors to originals
                foreach (var kvp in originalDevModes)
                {
                    var monitor = kvp.Key;
                    var origDev = kvp.Value;
                    var rollbackDev = origDev;
                    rollbackDev.dmFields = NativeMethods.DM_POSITION | NativeMethods.DM_DISPLAYORIENTATION | NativeMethods.DM_PELSWIDTH | NativeMethods.DM_PELSHEIGHT | NativeMethods.DM_DISPLAYFREQUENCY;
                    uint rollbackFlags = NativeMethods.CDS_UPDATEREGISTRY | NativeMethods.CDS_NORESET;
                    ChangeDisplaySettingsFunc(monitor.DeviceName, ref rollbackDev, IntPtr.Zero, rollbackFlags, IntPtr.Zero);
                }
                // Re-commit the rolled-back originals
                ChangeDisplaySettingsCommitFunc(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
                throw new Exception($"Final display settings commit failed with code {finalResult}. Original settings have been restored.");
            }
        }
    }
}
