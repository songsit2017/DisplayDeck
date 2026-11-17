using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DisplayManagerClone
{
    public class WindowManager
    {
        public static List<IntPtr> GetAllMonitorHandles()
        {
            var monitors = new List<IntPtr>();
            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData)
                {
                    monitors.Add(hMonitor);
                    return true;
                }, IntPtr.Zero);
            return monitors;
        }

        public static IntPtr FindMonitorByDeviceName(string targetMonitorDeviceName)
        {
            if (string.IsNullOrEmpty(targetMonitorDeviceName)) return IntPtr.Zero;
            IntPtr found = IntPtr.Zero;
            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData)
                {
                    var info = new NativeMethods.MONITORINFOEX();
                    if (NativeMethods.GetMonitorInfo(hMonitor, info))
                    {
                        if (string.Equals(info.szDevice, targetMonitorDeviceName, StringComparison.OrdinalIgnoreCase))
                        {
                            found = hMonitor;
                            return false;
                        }
                    }
                    return true;
                }, IntPtr.Zero);
            return found;
        }

        public static void MoveActiveWindowToNextMonitor()
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            MoveWindowToNextMonitor(hwnd);
        }

        public static void MoveWindowToNextMonitor(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;

            IntPtr currentMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);

            var monitors = GetAllMonitorHandles();

            if (monitors.Count <= 1) return;

            int currentIndex = monitors.IndexOf(currentMonitor);
            if (currentIndex == -1) currentIndex = 0;

            int nextIndex = (currentIndex + 1) % monitors.Count;
            IntPtr nextMonitor = monitors[nextIndex];

            MoveWindowToMonitor(hwnd, currentMonitor, nextMonitor);
        }

        public static void MoveWindowToSpecificMonitor(IntPtr hwnd, string targetMonitorDeviceName)
        {
            if (hwnd == IntPtr.Zero || string.IsNullOrEmpty(targetMonitorDeviceName)) return;

            IntPtr currentMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            IntPtr nextMonitor = FindMonitorByDeviceName(targetMonitorDeviceName);

            if (nextMonitor != IntPtr.Zero && currentMonitor != nextMonitor)
            {
                MoveWindowToMonitor(hwnd, currentMonitor, nextMonitor);
            }
        }

        public static void MoveWindowToMonitor(IntPtr hwnd, IntPtr currentMonitor, IntPtr nextMonitor)
        {
            var currentInfo = new NativeMethods.MONITORINFO();
            NativeMethods.GetMonitorInfo(currentMonitor, currentInfo);

            var nextInfo = new NativeMethods.MONITORINFO();
            NativeMethods.GetMonitorInfo(nextMonitor, nextInfo);

            NativeMethods.WINDOWPLACEMENT placement = new NativeMethods.WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(typeof(NativeMethods.WINDOWPLACEMENT));
            NativeMethods.GetWindowPlacement(hwnd, ref placement);

            bool isMaximized = placement.showCmd == NativeMethods.SW_SHOWMAXIMIZED;

            if (isMaximized)
            {
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
            }

            NativeMethods.GetWindowRect(hwnd, out NativeMethods.RECT windowRect);

            // Calculate percentages based on the current monitor's work area
            double leftPercent = (double)(windowRect.left - currentInfo.rcWork.left) / currentInfo.rcWork.Width;
            double topPercent = (double)(windowRect.top - currentInfo.rcWork.top) / currentInfo.rcWork.Height;
            double widthPercent = (double)windowRect.Width / currentInfo.rcWork.Width;
            double heightPercent = (double)windowRect.Height / currentInfo.rcWork.Height;

            // Apply percentages to the next monitor's work area
            int newX = nextInfo.rcWork.left + (int)Math.Round(nextInfo.rcWork.Width * leftPercent);
            int newY = nextInfo.rcWork.top + (int)Math.Round(nextInfo.rcWork.Height * topPercent);
            int newWidth = (int)Math.Round(nextInfo.rcWork.Width * widthPercent);
            int newHeight = (int)Math.Round(nextInfo.rcWork.Height * heightPercent);

            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, newX, newY, newWidth, newHeight, NativeMethods.SWP_NOZORDER);

            if (isMaximized)
            {
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWMAXIMIZED);
            }
        }

        public static void MoveActiveWindowToPreviousMonitor()
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            MoveWindowToPreviousMonitor(hwnd);
        }

        public static void MoveWindowToPreviousMonitor(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;

            IntPtr currentMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);

            var monitors = GetAllMonitorHandles();

            if (monitors.Count <= 1) return;

            int currentIndex = monitors.IndexOf(currentMonitor);
            if (currentIndex == -1) currentIndex = 0;

            int prevIndex = (currentIndex - 1 + monitors.Count) % monitors.Count;
            IntPtr prevMonitor = monitors[prevIndex];

            MoveWindowToMonitor(hwnd, currentMonitor, prevMonitor);
        }

        public static void MinimizeWindow()
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd != IntPtr.Zero)
            {
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MINIMIZE);
            }
        }

        public static void MaximizeWindow()
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd != IntPtr.Zero)
            {
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWMAXIMIZED);
            }
        }

        public static void RestoreWindow()
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd != IntPtr.Zero)
            {
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
            }
        }

        public static void MoveWindowToCenterOfMonitor()
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;

            IntPtr currentMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var info = new NativeMethods.MONITORINFO();
            NativeMethods.GetMonitorInfo(currentMonitor, info);

            NativeMethods.GetWindowRect(hwnd, out NativeMethods.RECT windowRect);
            int width = windowRect.Width;
            int height = windowRect.Height;

            int newX = info.rcWork.left + (info.rcWork.Width - width) / 2;
            int newY = info.rcWork.top + (info.rcWork.Height - height) / 2;

            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, newX, newY, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER);
        }

        public static void MoveWindowToCenterOfMonitorAndSizeTo75Percent()
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;

            IntPtr currentMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var info = new NativeMethods.MONITORINFO();
            NativeMethods.GetMonitorInfo(currentMonitor, info);

            // Check if maximized and restore it first
            NativeMethods.WINDOWPLACEMENT placement = new NativeMethods.WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(typeof(NativeMethods.WINDOWPLACEMENT));
            NativeMethods.GetWindowPlacement(hwnd, ref placement);
            if (placement.showCmd == NativeMethods.SW_SHOWMAXIMIZED)
            {
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
            }

            int width = (int)(info.rcWork.Width * 0.75);
            int height = (int)(info.rcWork.Height * 0.75);

            int newX = info.rcWork.left + (info.rcWork.Width - width) / 2;
            int newY = info.rcWork.top + (info.rcWork.Height - height) / 2;

            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, newX, newY, width, height, NativeMethods.SWP_NOZORDER);
        }

        public static void ToggleWindowAlwaysOnTop()
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            ToggleWindowAlwaysOnTop(hwnd);
        }

        public static void ToggleWindowAlwaysOnTop(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;

            int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            bool isTopmost = (exStyle & NativeMethods.WS_EX_TOPMOST) == NativeMethods.WS_EX_TOPMOST;

            if (isTopmost)
            {
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0, NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE);
            }
            else
            {
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE);
            }
        }

        public static void MoveWindowToCornerTL() => MoveWindowToCorner(0);
        public static void MoveWindowToCornerTR() => MoveWindowToCorner(1);
        public static void MoveWindowToCornerBL() => MoveWindowToCorner(2);
        public static void MoveWindowToCornerBR() => MoveWindowToCorner(3);

        public static void SizeWindowToSideLeft() => SizeWindowToSide(0);
        public static void SizeWindowToSideRight() => SizeWindowToSide(1);
        public static void SizeWindowToSideTop() => SizeWindowToSide(2);
        public static void SizeWindowToSideBottom() => SizeWindowToSide(3);

        private static void MoveWindowToCorner(int corner) // 0: TL, 1: TR, 2: BL, 3: BR
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;
            
            RestoreIfMaximized(hwnd);

            IntPtr currentMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var info = new NativeMethods.MONITORINFO();
            NativeMethods.GetMonitorInfo(currentMonitor, info);

            int width = info.rcWork.Width / 2;
            int height = info.rcWork.Height / 2;
            int x = info.rcWork.left;
            int y = info.rcWork.top;

            if (corner == 1 || corner == 3) x += width; // Right
            if (corner == 2 || corner == 3) y += height; // Bottom

            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, NativeMethods.SWP_NOZORDER);
        }

        private static void SizeWindowToSide(int side) // 0: Left, 1: Right, 2: Top, 3: Bottom
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;

            RestoreIfMaximized(hwnd);

            IntPtr currentMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var info = new NativeMethods.MONITORINFO();
            NativeMethods.GetMonitorInfo(currentMonitor, info);

            int width = info.rcWork.Width;
            int height = info.rcWork.Height;
            int x = info.rcWork.left;
            int y = info.rcWork.top;

            if (side == 0 || side == 1) width /= 2; // Left or Right
            if (side == 2 || side == 3) height /= 2; // Top or Bottom

            if (side == 1) x += width; // Right
            if (side == 3) y += height; // Bottom

            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, NativeMethods.SWP_NOZORDER);
        }

        public static IntPtr GetPrimaryMonitorHandle()
        {
            IntPtr primaryMonitor = IntPtr.Zero;
            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData)
                {
                    var info = new NativeMethods.MONITORINFO();
                    if (NativeMethods.GetMonitorInfo(hMonitor, info))
                    {
                        if ((info.dwFlags & 1) != 0) // MONITORINFOF_PRIMARY
                        {
                            primaryMonitor = hMonitor;
                            return false;
                        }
                    }
                    return true;
                }, IntPtr.Zero);
            return primaryMonitor;
        }

        public static void MoveWindowToPrimaryMonitor()
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;

            IntPtr currentMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            IntPtr primaryMonitor = GetPrimaryMonitorHandle();

            if (primaryMonitor != IntPtr.Zero && currentMonitor != primaryMonitor)
            {
                MoveWindowToMonitor(hwnd, currentMonitor, primaryMonitor);
            }
        }

        public static void SpanWindowAcrossAllMonitors()
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;

            RestoreIfMaximized(hwnd);

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData)
                {
                    var info = new NativeMethods.MONITORINFO();
                    if (NativeMethods.GetMonitorInfo(hMonitor, info))
                    {
                        if (info.rcWork.left < minX) minX = info.rcWork.left;
                        if (info.rcWork.top < minY) minY = info.rcWork.top;
                        if (info.rcWork.right > maxX) maxX = info.rcWork.right;
                        if (info.rcWork.bottom > maxY) maxY = info.rcWork.bottom;
                    }
                    return true;
                }, IntPtr.Zero);

            if (minX != int.MaxValue)
            {
                int width = maxX - minX;
                int height = maxY - minY;
                NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, minX, minY, width, height, NativeMethods.SWP_NOZORDER);
            }
        }

        private static void RestoreIfMaximized(IntPtr hwnd)
        {
            NativeMethods.WINDOWPLACEMENT placement = new NativeMethods.WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(typeof(NativeMethods.WINDOWPLACEMENT));
            NativeMethods.GetWindowPlacement(hwnd, ref placement);
            if (placement.showCmd == NativeMethods.SW_SHOWMAXIMIZED)
            {
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
            }
        }
    }
}
