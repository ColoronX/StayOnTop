# StayOnTop — Always on Top for Windows 10

A portable, dependency-free (beyond the Windows .NET Framework) tray utility to toggle the **active window** on top with **Alt+Q**.

## Features

- Global `Alt+Q` shortcut to pin/unpin the active window.
- Tray icon changes color when any window pinned by StayOnTop remains pinned.
- Right-click tray icon: see tracked pinned count, **Unpin all**, or **Exit**.
- Double-click tray icon for help.
- No console window, no installer, no background network access, and no persistent configuration.
- Exiting unpins windows pinned by StayOnTop (unless they have been changed independently).

## Build on Windows 10 (no Visual Studio required)

1. Extract this folder to any location.
2. Double-click `Build.bat`.
3. Launch `bin\StayOnTop.exe`.
4. Focus any normal desktop application and press **Alt+Q**.

The build uses the C# compiler bundled with the **.NET Framework 4.x** installation at `%WINDIR%\Microsoft.NET\Framework[64]\v4.0.30319\csc.exe`, which is normally available on Windows 10. If missing, enable/install .NET Framework 4.x.

## Usage and limitations

- Click an app's window and press `Alt+Q` to toggle topmost status.
- The pin effect is standard Win32 `HWND_TOPMOST` (no border overlay).
- If the shortcut is already registered by another program, StayOnTop displays an error; change the source constants to choose another combination.
- Some Windows system surfaces, exclusive fullscreen windows, and windows owned by elevated processes may not respond as expected. Run the utility elevated only when needed.
- **Unpin all** only applies to windows tracked as pinned by this utility.
- StayOnTop does not automatically start with Windows. You can add a shortcut to `shell:startup` if desired.
- StayOnTop is Windows-only. The binary must be built on Windows; the `.exe` is **not** included in the source archive.

## Files

- `StayOnTop.cs` — complete C# source code
- `Build.bat` — one-click Windows build script
