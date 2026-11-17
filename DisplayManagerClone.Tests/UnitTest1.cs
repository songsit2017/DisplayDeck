using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
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

        [Theory]
        [InlineData("CON", "profile")]
        [InlineData("PRN", "profile")]
        [InlineData("AUX", "profile")]
        [InlineData("NUL", "profile")]
        [InlineData("COM1", "profile")]
        [InlineData("LPT1", "profile")]
        [InlineData("CON.txt", "profile")]
        [InlineData("AUX.json", "profile")]
        [InlineData("PRN.backup", "profile")]
        [InlineData("lpt1.log", "profile")]
        [InlineData("COM3.data", "profile")]
        public void MonitorProfileManager_GetSafeProfileFileName_SanitizesReservedNamesAndExtensions(string inputName, string expectedPrefix)
        {
            string fileName = MonitorProfileManager.GetSafeProfileFileName(inputName);

            Assert.StartsWith($"{expectedPrefix}_", fileName, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(".json", fileName);
            Assert.DoesNotContain("CON.", fileName, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("AUX.", fileName, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PRN.", fileName, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void MonitorProfileManager_GetSafeProfileFileName_DistinctForDifferentNames()
        {
            string file1 = MonitorProfileManager.GetSafeProfileFileName("Work Layout");
            string file2 = MonitorProfileManager.GetSafeProfileFileName("Work/Layout");

            Assert.NotEqual(file1, file2);
            Assert.EndsWith(".json", file1);
            Assert.EndsWith(".json", file2);
        }

        [Fact]
        public void MonitorProfileData_SerializationRoundtrip_WithRealStructData_PreservesAllFields()
        {
            var path = new CCDNativeMethods.DISPLAYCONFIG_PATH_INFO
            {
                flags = 1,
                sourceInfo = new CCDNativeMethods.DISPLAYCONFIG_PATH_SOURCE_INFO
                {
                    id = 101,
                    modeInfoIdx = 0,
                    statusFlags = 2,
                    adapterId = new CCDNativeMethods.LUID { LowPart = 12345, HighPart = 67890 }
                },
                targetInfo = new CCDNativeMethods.DISPLAYCONFIG_PATH_TARGET_INFO
                {
                    id = 202,
                    modeInfoIdx = 1,
                    outputTechnology = 10,
                    rotation = 1,
                    scaling = 2,
                    refreshRate = new CCDNativeMethods.DISPLAYCONFIG_RATIONAL { Numerator = 60000, Denominator = 1000 },
                    scanLineOrdering = 0,
                    targetAvailable = 1,
                    statusFlags = 4,
                    adapterId = new CCDNativeMethods.LUID { LowPart = 12345, HighPart = 67890 }
                }
            };

            var mode = new CCDNativeMethods.DISPLAYCONFIG_MODE_INFO
            {
                infoType = 1, // source mode
                id = 101,
                adapterId = new CCDNativeMethods.LUID { LowPart = 12345, HighPart = 67890 }
            };
            mode.modeInfo.sourceMode = new CCDNativeMethods.DISPLAYCONFIG_SOURCE_MODE
            {
                width = 2560,
                height = 1440,
                pixelFormat = 32,
                position = new CCDNativeMethods.POINTL { x = 1920, y = 0 }
            };

            var profile = new MonitorProfileData
            {
                ProfileName = "Multi-Monitor-Workstation",
                Paths = new[] { path },
                Modes = new[] { mode }
            };

            var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
            string json = JsonSerializer.Serialize(profile, options);
            var deserialized = JsonSerializer.Deserialize<MonitorProfileData>(json, options);

            Assert.NotNull(deserialized);
            Assert.Equal("Multi-Monitor-Workstation", deserialized.ProfileName);
            Assert.Single(deserialized.Paths);
            Assert.Single(deserialized.Modes);

            var dPath = deserialized.Paths[0];
            Assert.Equal(1u, dPath.flags);
            Assert.Equal(101u, dPath.sourceInfo.id);
            Assert.Equal(12345u, dPath.sourceInfo.adapterId.LowPart);
            Assert.Equal(67890, dPath.sourceInfo.adapterId.HighPart);
            Assert.Equal(202u, dPath.targetInfo.id);
            Assert.Equal(10u, dPath.targetInfo.outputTechnology);
            Assert.Equal(60000u, dPath.targetInfo.refreshRate.Numerator);
            Assert.Equal(1000u, dPath.targetInfo.refreshRate.Denominator);

            var dMode = deserialized.Modes[0];
            Assert.Equal(1u, dMode.infoType);
            Assert.Equal(101u, dMode.id);
            Assert.Equal(2560u, dMode.modeInfo.sourceMode.width);
            Assert.Equal(1440u, dMode.modeInfo.sourceMode.height);
            Assert.Equal(1920, dMode.modeInfo.sourceMode.position.x);
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

        [Theory]
        [InlineData(true, 0, 0, 0, 800, 600, true)]       // Normal valid top-level window
        [InlineData(false, 0, 0, 0, 800, 600, false)]     // Invisible window
        [InlineData(true, 0x1234, 0, 0, 800, 600, false)] // Owned window (dialog, popup)
        [InlineData(true, 0, NativeMethods.WS_CHILD, 0, 800, 600, false)] // Child window
        [InlineData(true, 0, 0, NativeMethods.WS_EX_TOOLWINDOW, 800, 600, false)] // Tool window
        [InlineData(true, 0, 0, 0, 50, 50, false)]        // Zero-size / tiny window (< 100x100)
        [InlineData(true, 0, 0, 0, 99, 600, false)]       // Width < 100
        [InlineData(true, 0, 0, 0, 600, 99, false)]       // Height < 100
        public void WindowRule_IsValidWindowCriteria_FiltersCorrectly(
            bool isVisible,
            long ownerHandle,
            int style,
            int exStyle,
            int width,
            int height,
            bool expectedValid)
        {
            bool actual = WindowRuleManager.IsValidWindowCriteria(
                isVisible,
                (IntPtr)ownerHandle,
                style,
                exStyle,
                width,
                height);

            Assert.Equal(expectedValid, actual);
        }

        [Fact]
        public void TitleBarOverlay_AttachTo_TracksTargetHwndExplicitly()
        {
            var thread = new Thread(() =>
            {
                var overlay = new TitleBarOverlay();
                Assert.Equal(IntPtr.Zero, overlay.TargetHwnd);

                IntPtr testHwnd = (IntPtr)0xABCD;
                overlay.AttachTo(testHwnd, 100, 200);

                Assert.Equal(testHwnd, overlay.TargetHwnd);
                overlay.Close();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [Fact]
        public void MonitorConfigManager_ApplySettings_StagingFailureRollsBackStagedMonitors()
        {
            var manager = new MonitorConfigManager();

            var monitor1 = new MonitorConfig
            {
                DeviceName = @"\\.\DISPLAY1",
                DisplayName = "Monitor 1",
                IsPrimary = true,
                CurrentResolution = new Resolution { Width = 1920, Height = 1080, Frequency = 60 }
            };
            var monitor2 = new MonitorConfig
            {
                DeviceName = @"\\.\DISPLAY2",
                DisplayName = "Monitor 2",
                IsPrimary = false,
                CurrentResolution = new Resolution { Width = 2560, Height = 1440, Frequency = 144 }
            };

            var stagedDevices = new List<string?>();
            var rollbackedDevices = new List<string?>();

            // Mock EnumDisplaySettings to succeed and return dummy DEVMODEs
            manager.EnumDisplaySettingsFunc = (string? dev, int modeNum, ref NativeMethods.DEVMODE devMode) =>
            {
                devMode.dmSize = (short)Marshal.SizeOf(typeof(NativeMethods.DEVMODE));
                devMode.dmPelsWidth = 1920;
                devMode.dmPelsHeight = 1080;
                return true;
            };

            // Mock ChangeDisplaySettings:
            // CDS_TEST passes for both
            // Staging passes for Monitor 1, fails for Monitor 2
            manager.ChangeDisplaySettingsFunc = (string? dev, ref NativeMethods.DEVMODE devMode, IntPtr hwnd, uint flags, IntPtr lParam) =>
            {
                if ((flags & NativeMethods.CDS_TEST) != 0)
                {
                    return NativeMethods.DISP_CHANGE_SUCCESSFUL;
                }

                if ((flags & (NativeMethods.CDS_UPDATEREGISTRY | NativeMethods.CDS_NORESET)) != 0)
                {
                    if (dev == @"\\.\DISPLAY1")
                    {
                        stagedDevices.Add(dev);
                        // If it's a rollback (same device staged again after failure)
                        if (stagedDevices.Count > 1)
                        {
                            rollbackedDevices.Add(dev);
                        }
                        return NativeMethods.DISP_CHANGE_SUCCESSFUL;
                    }
                    if (dev == @"\\.\DISPLAY2")
                    {
                        // Simulate failure during staging of 2nd monitor
                        return -1; // DISP_CHANGE_FAILED
                    }
                }
                return NativeMethods.DISP_CHANGE_SUCCESSFUL;
            };

            var ex = Assert.Throws<Exception>(() => manager.ApplySettings(new List<MonitorConfig> { monitor1, monitor2 }));
            Assert.Contains("rolled back", ex.Message, StringComparison.OrdinalIgnoreCase);

            // Verify Monitor 1 was staged and then rolled back
            Assert.Contains(@"\\.\DISPLAY1", stagedDevices);
            Assert.Contains(@"\\.\DISPLAY1", rollbackedDevices);
        }

        [Fact]
        public void MonitorConfigManager_ApplySettings_TestValidationAbortPreventsStaging()
        {
            var manager = new MonitorConfigManager();

            var monitor1 = new MonitorConfig
            {
                DeviceName = @"\\.\DISPLAY1",
                DisplayName = "Monitor 1",
                IsPrimary = true,
                CurrentResolution = new Resolution { Width = 1920, Height = 1080, Frequency = 60 }
            };

            bool stagingCalled = false;

            manager.EnumDisplaySettingsFunc = (string? dev, int modeNum, ref NativeMethods.DEVMODE devMode) => true;

            // CDS_TEST fails immediately
            manager.ChangeDisplaySettingsFunc = (string? dev, ref NativeMethods.DEVMODE devMode, IntPtr hwnd, uint flags, IntPtr lParam) =>
            {
                if ((flags & NativeMethods.CDS_TEST) != 0)
                {
                    return -2; // DISP_CHANGE_BADMODE
                }
                if ((flags & NativeMethods.CDS_UPDATEREGISTRY) != 0)
                {
                    stagingCalled = true;
                }
                return NativeMethods.DISP_CHANGE_SUCCESSFUL;
            };

            var ex = Assert.Throws<Exception>(() => manager.ApplySettings(new List<MonitorConfig> { monitor1 }));
            Assert.Contains("Validation failed", ex.Message);
            Assert.False(stagingCalled, "Staging should never be called if validation fails.");
        }

        [Fact]
        public void MonitorConfigManager_ApplySettings_FinalCommitFailureRollsBackAllMonitors()
        {
            var manager = new MonitorConfigManager();

            var monitor1 = new MonitorConfig
            {
                DeviceName = @"\\.\DISPLAY1",
                DisplayName = "Monitor 1",
                IsPrimary = true,
                CurrentResolution = new Resolution { Width = 1920, Height = 1080, Frequency = 60 }
            };

            int registryWrites = 0;
            manager.EnumDisplaySettingsFunc = (string? dev, int modeNum, ref NativeMethods.DEVMODE devMode) => true;

            manager.ChangeDisplaySettingsFunc = (string? dev, ref NativeMethods.DEVMODE devMode, IntPtr hwnd, uint flags, IntPtr lParam) =>
            {
                if ((flags & NativeMethods.CDS_TEST) != 0) return NativeMethods.DISP_CHANGE_SUCCESSFUL;
                if ((flags & NativeMethods.CDS_UPDATEREGISTRY) != 0)
                {
                    registryWrites++;
                    return NativeMethods.DISP_CHANGE_SUCCESSFUL;
                }
                return NativeMethods.DISP_CHANGE_SUCCESSFUL;
            };

            // Final commit fails
            manager.ChangeDisplaySettingsCommitFunc = (string? dev, IntPtr ptr, IntPtr hwnd, uint flags, IntPtr lParam) => -1;

            var ex = Assert.Throws<Exception>(() => manager.ApplySettings(new List<MonitorConfig> { monitor1 }));
            Assert.Contains("Original settings have been restored", ex.Message);
            // 1 staging write + 1 rollback write = 2 registry writes
            Assert.Equal(2, registryWrites);
        }
    }
}
