using System;
using System.IO;
using System.Text.Json;
using Xunit;
using DisplayManagerClone;

namespace DisplayManagerClone.Tests
{
    public class ModelAndLogicTests
    {
        [Fact]
        public void Resolution_EqualityAndToString_WorksCorrectly()
        {
            var res1 = new Resolution { Width = 1920, Height = 1080, Frequency = 60 };
            var res2 = new Resolution { Width = 1920, Height = 1080, Frequency = 60 };
            var res3 = new Resolution { Width = 2560, Height = 1440, Frequency = 144 };

            Assert.Equal(res1, res2);
            Assert.NotEqual(res1, res3);
            Assert.Equal("1920 x 1080 @ 60Hz", res1.ToString());
            Assert.Equal("2560 x 1440 @ 144Hz", res3.ToString());
        }

        [Theory]
        [InlineData(0, "None")]
        [InlineData((uint)13, "Return")]
        public void HotkeyConfig_DisplayString_FormatsCorrectly(uint key, string expectedKeyPart)
        {
            var config = new HotkeyConfig
            {
                ActionId = "Test",
                ActionName = "Test Action",
                Modifiers = 0,
                Key = key
            };

            Assert.Contains(expectedKeyPart, config.DisplayHotkey);
        }

        [Fact]
        public void MonitorProfileManager_GetSafeProfileFileName_AvoidsCollisionsAndReservedNames()
        {
            string fileName1 = MonitorProfileManager.GetSafeProfileFileName("Work Layout");
            string fileName2 = MonitorProfileManager.GetSafeProfileFileName("Work/Layout");
            string fileName3 = MonitorProfileManager.GetSafeProfileFileName("CON");
            string fileName4 = MonitorProfileManager.GetSafeProfileFileName("PRN");

            Assert.False(fileName1.StartsWith("CON."));
            Assert.False(fileName3.StartsWith("CON."));
            Assert.False(fileName4.StartsWith("PRN."));

            // Distinct hashes even with sanitized characters
            Assert.NotEqual(fileName1, fileName2);
            Assert.EndsWith(".json", fileName1);
        }

        [Fact]
        public void MonitorProfileData_SerializationRoundtrip_PreservesData()
        {
            var profile = new MonitorProfileData
            {
                ProfileName = "Multi-Monitor-Workstation",
                Paths = new CCDNativeMethods.DISPLAYCONFIG_PATH_INFO[0],
                Modes = new CCDNativeMethods.DISPLAYCONFIG_MODE_INFO[0]
            };

            string json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            var deserialized = JsonSerializer.Deserialize<MonitorProfileData>(json);

            Assert.NotNull(deserialized);
            Assert.Equal("Multi-Monitor-Workstation", deserialized.ProfileName);
            Assert.Empty(deserialized.Paths);
            Assert.Empty(deserialized.Modes);
        }

        [Fact]
        public void WindowRule_Serialization_PreservesRules()
        {
            var rule = new WindowRule
            {
                ProcessName = "chrome",
                TargetMonitorDeviceName = @"\\.\DISPLAY2",
                TargetMonitorDisplayName = "Display 2 (Dell)"
            };

            string json = JsonSerializer.Serialize(rule);
            var deserialized = JsonSerializer.Deserialize<WindowRule>(json);

            Assert.NotNull(deserialized);
            Assert.Equal("chrome", deserialized.ProcessName);
            Assert.Equal(@"\\.\DISPLAY2", deserialized.TargetMonitorDeviceName);
            Assert.Equal("Display 2 (Dell)", deserialized.TargetMonitorDisplayName);
        }
    }
}
