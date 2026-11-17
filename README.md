# DisplayManagerClone

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
