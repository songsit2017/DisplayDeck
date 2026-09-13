using System;
using System.Runtime.InteropServices;

namespace DisplayManagerClone
{
    [ComImport]
    [Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
    internal class DesktopWallpaperClass
    {
    }

    [ComImport]
    [Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorID, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
        
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorID);
        
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetMonitorDevicePathAt(uint monitorIndex);
        
        uint GetMonitorDevicePathCount();
        [PreserveSig]
        int GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorID, [Out] out NativeMethods.RECT displayRect);

        [PreserveSig]
        int SetBackgroundColor([In] uint color);

        [PreserveSig]
        int GetBackgroundColor([Out] out uint color);     
        void SetPosition(DesktopWallpaperPosition position);
        
        DesktopWallpaperPosition GetPosition();
        
        void SetSlideshow(IntPtr items);
        
        IntPtr GetSlideshow();
        
        void SetSlideshowOptions(DesktopSlideshowOptions options, uint slideshowTick);
        
        void GetSlideshowOptions(out DesktopSlideshowOptions options, out uint slideshowTick);
        
        void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string monitorID, DesktopSlideshowDirection direction);
        
        DesktopSlideshowDirection GetStatus();
        
        void Enable(bool enable);
    }

    public enum DesktopWallpaperPosition
    {
        Center = 0,
        Tile = 1,
        Stretch = 2,
        Fit = 3,
        Fill = 4,
        Span = 5
    }

    internal enum DesktopSlideshowOptions
    {
        ShuffleImages = 0x01
    }

    internal enum DesktopSlideshowDirection
    {
        Forward = 0,
        Backward = 1
    }
}
