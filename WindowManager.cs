using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DisplayManagerClone
{
    public class WindowManager
    {
        public static void MoveActiveWindowToNextMonitor()
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;

            IntPtr currentMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);

            var monitors = new List<IntPtr>();
            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData)
                {
                    monitors.Add(hMonitor);
                    return true;
                }, IntPtr.Zero);

            if (monitors.Count <= 1) return;

            int currentIndex = monitors.IndexOf(currentMonitor);
            if (currentIndex == -1) currentIndex = 0;

            int nextIndex = (currentIndex + 1) % monitors.Count;
            IntPtr nextMonitor = monitors[nextIndex];

            MoveWindowToMonitor(hwnd, currentMonitor, nextMonitor);
        }

        private static void MoveWindowToMonitor(IntPtr hwnd, IntPtr currentMonitor, IntPtr nextMonitor)
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
    }
}
