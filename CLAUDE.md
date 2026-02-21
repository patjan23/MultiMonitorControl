# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

All commands run from `MultiMonitorControl/` (the project directory):

```bash
dotnet restore                  # Restore NuGet packages
dotnet build                    # Build (Debug by default)
dotnet build -c Release         # Build Release
dotnet run                      # Run the application
```

To publish a self-contained executable:
```bash
dotnet publish -c Release --self-contained true --runtime win-x64 --output publish
```

`build.bat` (in the repo root) automates clean → restore → Debug build → Release build → publish.

There are no tests in this project.

## Architecture

Single-project WPF desktop application targeting `.NET 6.0-windows`. The solution is `MultiMonitorControl.sln`; the project is `MultiMonitorControl/MultiMonitorControl.csproj`.

### Key Files

| File | Purpose |
|------|---------|
| `MainWindow.xaml.cs` | All application logic (~734 lines): Windows API P/Invoke declarations, monitor detection, hardware control, UI event handlers, profile management |
| `MainWindow.xaml` | Dark-themed UI (~383 lines): sliders for brightness/contrast/RGB, monitor ComboBox, profile buttons, status bar |
| `Models/MonitorInfo.cs` | Logical monitor metadata (handle, name, bounds, primary flag, DDC/CI support) |
| `Models/MonitorProfile.cs` | Serializable profile data (brightness, contrast, RGB gains, monitor name, timestamp) |
| `App.xaml` / `App.xaml.cs` | Minimal WPF app entry point |

### Hardware Integration

Monitor settings are controlled via the **DDC/CI protocol** through P/Invoke into two Windows DLLs:

- **`user32.dll`** — `EnumDisplayMonitors`, `GetMonitorInfo` (logical monitor enumeration)
- **`dxva2.dll`** — `GetPhysicalMonitorsFromHMONITOR`, `SetMonitorBrightness`, `SetMonitorContrast`, `SetMonitorRedGreenOrBlueGain`, and their `Get*` counterparts

All hardware calls are wrapped in try-catch; failures surface as status bar messages rather than exceptions. Monitor handles (`PHYSICAL_MONITOR` structs) are cleaned up in `OnClosed()`.

### Profile & Persistence

- **Quick profiles** (Gaming, Office, Movie, Photo) are hardcoded presets applied directly in `MainWindow.xaml.cs`.
- **Custom profiles** are JSON files the user saves/opens via `SaveFileDialog`/`OpenFileDialog`.
- **Auto-save** uses a 2-second debounce `DispatcherTimer` that writes last-used settings to `%AppData%\MultiMonitorControl\LastUsed.json` (loaded on startup to restore state).

### Threading

- Monitor detection runs `async`/`await` to avoid blocking the UI thread.
- All hardware API calls happen on the UI thread (WPF single-threaded model).
- Two `DispatcherTimer` instances: auto-save (2 s debounce) and status message clear (5 s).

### Platform Constraint

This application is **Windows-only** (`net6.0-windows`, WPF, P/Invoke into `dxva2.dll`/`user32.dll`). It requires monitors that support the DDC/CI protocol; monitors without it are detected and flagged as having limited support.
