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
        private readonly string ProfilesDirectory;
        private readonly string _lastActiveFile;

        public string LastActiveProfile
        {
            get
            {
                try
                {
                    if (File.Exists(_lastActiveFile))
                    {
                        string profile = File.ReadAllText(_lastActiveFile).Trim();
                        return string.IsNullOrEmpty(profile) ? null : profile;
                    }
                }
                catch { }
                return null;
            }
            set
            {
                try
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        if (File.Exists(_lastActiveFile)) File.Delete(_lastActiveFile);
                    }
                    else
                    {
                        File.WriteAllText(_lastActiveFile, value.Trim());
                    }
                }
                catch { }
            }
        }

        public MonitorProfileManager()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string baseFolder = Path.Combine(appData, "DisplayManagerClone");
            ProfilesDirectory = Path.Combine(baseFolder, "Profiles");
            _lastActiveFile = Path.Combine(baseFolder, "last_active_profile.txt");
            if (!Directory.Exists(ProfilesDirectory))
            {
                Directory.CreateDirectory(ProfilesDirectory);
            }
        }

        public System.Collections.Generic.List<string> GetSavedProfiles()
        {
            var profiles = new System.Collections.Generic.List<string>();
            if (Directory.Exists(ProfilesDirectory))
            {
                var files = Directory.GetFiles(ProfilesDirectory, "*.json");
                foreach (var file in files)
                {
                    profiles.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
            return profiles;
        }

        private string GetProfilePath(string profileName)
        {
            // Sanitize file name
            string safeName = string.Join("_", profileName.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(ProfilesDirectory, $"{safeName}.json");
        }

        public void SaveCurrentProfile(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
                throw new ArgumentException("Profile name cannot be empty.");

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
            File.WriteAllText(GetProfilePath(profileName), json);
            LastActiveProfile = profileName;
        }

        public void RestoreProfile(string profileName)
        {
            string path = GetProfilePath(profileName);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Profile '{profileName}' not found.");

            string json = File.ReadAllText(path);
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
                
            LastActiveProfile = profileName;
        }
        
        public void DeleteProfile(string profileName)
        {
            string path = GetProfilePath(profileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
