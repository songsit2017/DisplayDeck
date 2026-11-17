using System;
using System.IO;
using System.Text.Json;

namespace DisplayManagerClone
{
    public class MonitorProfileData
    {
        public string ProfileName { get; set; } = string.Empty;
        public CCDNativeMethods.DISPLAYCONFIG_PATH_INFO[] Paths { get; set; } = Array.Empty<CCDNativeMethods.DISPLAYCONFIG_PATH_INFO>();
        public CCDNativeMethods.DISPLAYCONFIG_MODE_INFO[] Modes { get; set; } = Array.Empty<CCDNativeMethods.DISPLAYCONFIG_MODE_INFO>();
    }

    public class MonitorProfileManager
    {
        private readonly string ProfilesDirectory;
        private readonly string _lastActiveFile;

        public string? LastActiveProfile
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

        private static readonly HashSet<string> ReservedWindowsNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        public System.Collections.Generic.List<string> GetSavedProfiles()
        {
            var profiles = new System.Collections.Generic.List<string>();
            if (Directory.Exists(ProfilesDirectory))
            {
                var files = Directory.GetFiles(ProfilesDirectory, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("ProfileName", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString()))
                        {
                            profiles.Add(nameProp.GetString()!);
                            continue;
                        }
                    }
                    catch { }

                    // Fallback to legacy file name without extension
                    profiles.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
            return profiles.Distinct().ToList();
        }

        public static string GetSafeProfileFileName(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
                throw new ArgumentException("Profile name cannot be empty.");

            string trimmed = profileName.Trim();

            // Hash to guarantee distinct, clash-free, and collision-free file paths for any unicode/special string
            using var sha = System.Security.Cryptography.SHA256.Create();
            byte[] hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(trimmed.ToLowerInvariant()));
            string hashPrefix = Convert.ToHexString(hash).Substring(0, 16);

            // Sanitize readable portion
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = new string(trimmed.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());

            // Check the stem (part before the first dot) against Windows reserved device names.
            // Windows treats "CON", "CON.txt", "CON.anything" etc. as reserved.
            string stem = sanitized.Contains('.') ? sanitized.Substring(0, sanitized.IndexOf('.')) : sanitized;
            if (ReservedWindowsNames.Contains(stem) || sanitized.Length > 30)
            {
                sanitized = "profile";
            }

            return $"{sanitized}_{hashPrefix}.json";
        }

        private string GetProfilePath(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
                throw new ArgumentException("Profile name cannot be empty.");

            string newFormatPath = Path.Combine(ProfilesDirectory, GetSafeProfileFileName(profileName));
            if (File.Exists(newFormatPath)) return newFormatPath;

            // Check if profile exists by reading ProfileName inside JSON files
            if (Directory.Exists(ProfilesDirectory))
            {
                foreach (var file in Directory.GetFiles(ProfilesDirectory, "*.json"))
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("ProfileName", out var nameProp))
                        {
                            if (string.Equals(nameProp.GetString(), profileName.Trim(), StringComparison.OrdinalIgnoreCase))
                            {
                                return file;
                            }
                        }
                    }
                    catch { }
                }

                // Check legacy file name match
                string legacySafe = string.Join("_", profileName.Split(Path.GetInvalidFileNameChars()));
                string legacyPath = Path.Combine(ProfilesDirectory, $"{legacySafe}.json");
                if (File.Exists(legacyPath)) return legacyPath;
            }

            return newFormatPath;
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
                ProfileName = profileName.Trim(),
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
