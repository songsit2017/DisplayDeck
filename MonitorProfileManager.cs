using System;
using System.IO;
using System.Text.Json;

namespace DisplayManagerClone
{
    public class MonitorProfileData
    {
        public CCDNativeMethods.DISPLAYCONFIG_PATH_INFO[] Paths { get; set; }
        public CCDNativeMethods.DISPLAYCONFIG_MODE_INFO[] Modes { get; set; }
    }

    public class MonitorProfileManager
    {
        private const string ProfileFileName = "monitor_profile.json";

        public void SaveCurrentProfile()
        {
            int status = CCDNativeMethods.GetDisplayConfigBufferSizes(
                CCDNativeMethods.QDC_ALL_PATHS,
                out uint pathCount,
                out uint modeCount);

            if (status != 0)
                throw new Exception($"GetDisplayConfigBufferSizes failed with status {status}");

            var paths = new CCDNativeMethods.DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new CCDNativeMethods.DISPLAYCONFIG_MODE_INFO[modeCount];

            status = CCDNativeMethods.QueryDisplayConfig(
                CCDNativeMethods.QDC_ALL_PATHS,
                ref pathCount,
                paths,
                ref modeCount,
                modes,
                IntPtr.Zero);

            if (status != 0)
                throw new Exception($"QueryDisplayConfig failed with status {status}");

            // Truncate arrays if API returned fewer elements than allocated
            if (pathCount < paths.Length)
            {
                Array.Resize(ref paths, (int)pathCount);
            }
            if (modeCount < modes.Length)
            {
                Array.Resize(ref modes, (int)modeCount);
            }

            var profile = new MonitorProfileData
            {
                Paths = paths,
                Modes = modes
            };

            var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
            string json = JsonSerializer.Serialize(profile, options);
            File.WriteAllText(ProfileFileName, json);
        }

        public void RestoreProfile()
        {
            if (!File.Exists(ProfileFileName))
                throw new FileNotFoundException("No saved profile found. Please save a profile first.");

            string json = File.ReadAllText(ProfileFileName);
            var options = new JsonSerializerOptions { IncludeFields = true };
            var profile = JsonSerializer.Deserialize<MonitorProfileData>(json, options);

            if (profile == null || profile.Paths == null || profile.Modes == null)
                throw new Exception("Invalid profile data.");

            uint flags = CCDNativeMethods.SDC_APPLY | CCDNativeMethods.SDC_USE_SUPPLIED_DISPLAY_CONFIG | CCDNativeMethods.SDC_ALLOW_CHANGES;

            int status = CCDNativeMethods.SetDisplayConfig(
                (uint)profile.Paths.Length,
                profile.Paths,
                (uint)profile.Modes.Length,
                profile.Modes,
                flags);

            if (status != 0)
                throw new Exception($"SetDisplayConfig failed with status {status}. The physical connections might have changed in a way that makes this topology invalid.");
        }
    }
}
