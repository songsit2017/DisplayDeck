# DisplayDeck

WPF / .NET 8 Multi-Monitor & Window Management Utility (inspired by DisplayFusion).

## Features
- **Window Management & Functions:** Hotkeys for moving windows across monitors, splitting to sides, snapping to corners, centering, span across all displays, minimize, maximize, and always-on-top.
- **Window Rules:** Automatically detect and assign newly opened applications to designated monitors (filtering out child popups and tooltips).
- **Desktop Wallpaper Management:** Per-monitor wallpaper, span across all displays with bezel compensation, solid background colors, and folder-based slideshow with shuffle.
- **Monitor Control Center:** Change display resolutions, refresh rates, monitor positioning, primary monitor designation, and orientation (Landscape / Portrait).
- **Monitor Profiles:** Save and restore multi-monitor layout profiles via CCD API with Watchdog auto-recovery on display disconnect/reconnect.
- **TitleBar Overlay Buttons:** Floating overlay next to the window titlebar for instant 1-click monitor moves and pinning always-on-top.

## Build and Run
Requires **.NET 8 SDK** on Windows 10/11:
```powershell
# Build solution
dotnet build DisplayManagerClone.sln

# Run Unit Tests
dotnet test DisplayManagerClone.sln

# Run Application
dotnet run --project DisplayManagerClone.csproj
```

## Installer and updates

To use DisplayDeck's multi-monitor picker from a supported window's Maximize button, enable **Snap on Maximize hover** in DisplayDeck. In Windows **Settings → System → Multitasking → Snap windows**, uncheck only **Show snap layouts when I hover over a window's maximize button** to prevent both menus from appearing. Apps with custom caption buttons may require the existing TitleBar Buttons picker.

Download `DisplayDeck-win-Setup.exe` from [GitHub Releases](https://github.com/songsit2017/DisplayDeck/releases). The installer includes the complete Windows x64 app and its .NET runtime. Installed copies check for updates on startup and every six hours, download them in the background, and apply them when you select **Exit** from the tray menu.

To package a release on Windows, run `./Build-Installer.ps1 -Version 1.0.1` with an unused version. Upload the setup executable, full `.nupkg` and `releases.win.json` together to the corresponding GitHub release. Publish stable releases only when they are ready for installed copies to pick them up.
