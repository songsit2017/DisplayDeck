using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DisplayManagerClone
{
    public class WindowRule
    {
        public string ProcessName { get; set; } = string.Empty;
        public string TargetMonitorDeviceName { get; set; } = string.Empty;
        public string TargetMonitorDisplayName { get; set; } = string.Empty;
    }

    public class WindowRuleManager : IDisposable
    {
        private readonly string _rulesFilePath;
        public List<WindowRule> Rules { get; private set; } = new List<WindowRule>();

        private IntPtr _hook;
        private NativeMethods.WinEventDelegate _hookDelegate;

        public WindowRuleManager()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "DisplayManagerClone");
            Directory.CreateDirectory(folder);
            _rulesFilePath = Path.Combine(folder, "window_rules.json");

            LoadRules();
            _hookDelegate = new NativeMethods.WinEventDelegate(WinEventProc);
            _hook = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_OBJECT_SHOW,
                NativeMethods.EVENT_OBJECT_SHOW,
                IntPtr.Zero,
                _hookDelegate,
                0,
                0,
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);
        }

        private void LoadRules()
        {
            if (File.Exists(_rulesFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_rulesFilePath);
                    Rules = JsonSerializer.Deserialize<List<WindowRule>>(json) ?? new List<WindowRule>();
                }
                catch { }
            }
        }

        public void SaveRules()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(Rules, options);
            File.WriteAllText(_rulesFilePath, json);
        }

        public static bool IsValidWindowCriteria(bool isVisible, IntPtr owner, int style, int exStyle, int width, int height)
        {
            if (!isVisible) return false;

            // Must be a root / top-level window (not owned by another window)
            if (owner != IntPtr.Zero) return false;

            // Must not be a child control
            if ((style & NativeMethods.WS_CHILD) != 0) return false;

            // Tool windows (tooltips, floating toolbars, palettes) should be ignored
            if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0) return false;

            // Check dimensions to ignore zero-size invisible helper windows
            if (width < 100 || height < 100) return false;

            return true;
        }

        private bool IsApplicationMainWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;
            if (!NativeMethods.IsWindowVisible(hwnd)) return false;

            IntPtr owner = NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER);
            int style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_STYLE);
            int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            int width = 0, height = 0;
            if (NativeMethods.GetWindowRect(hwnd, out NativeMethods.RECT rect))
            {
                width = rect.Width;
                height = rect.Height;
            }

            return IsValidWindowCriteria(true, owner, style, exStyle, width, height);
        }

        private async void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            try
            {
                if (idObject != NativeMethods.OBJID_WINDOW || hwnd == IntPtr.Zero) return;

                // Quick pre-filter: must be a visible, non-child, non-owned window
                if (!NativeMethods.IsWindowVisible(hwnd)) return;
                IntPtr owner = NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER);
                if (owner != IntPtr.Zero) return;
                int style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_STYLE);
                if ((style & NativeMethods.WS_CHILD) != 0) return;

                string? processName = GetProcessName(hwnd);
                if (string.IsNullOrEmpty(processName)) return;

                var rule = Rules.FirstOrDefault(r => string.Equals(r.ProcessName, processName, StringComparison.OrdinalIgnoreCase));
                if (rule != null)
                {
                    // Delay to let the window fully initialize (size, style) before final check
                    await Task.Delay(150);
                    if (IsApplicationMainWindow(hwnd))
                    {
                        WindowManager.MoveWindowToSpecificMonitor(hwnd, rule.TargetMonitorDeviceName);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WindowRuleManager hook error: {ex.Message}");
            }
        }

        private string? GetProcessName(IntPtr hwnd)
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return null;

            IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProcess == IntPtr.Zero) return null;

            try
            {
                var sb = new StringBuilder(1024);
                uint size = (uint)sb.Capacity;
                if (NativeMethods.QueryFullProcessImageName(hProcess, 0, sb, ref size))
                {
                    string exePath = sb.ToString();
                    return Path.GetFileNameWithoutExtension(exePath);
                }
                return null;
            }
            finally
            {
                NativeMethods.CloseHandle(hProcess);
            }
        }

        public void Dispose()
        {
            if (_hook != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_hook);
                _hook = IntPtr.Zero;
            }
        }
    }
}
