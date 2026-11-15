# ReProgman - Remember Progman?

A clone of the Windows 3.1 Program Manager (progman.exe), written for everyone who misses it.

<img src="docs/assets/screenshot-en.png">

The UI follows the display language of the OS — here it is on a Japanese Windows 11:

<img src="docs/assets/screenshot-ja.png">

ReProgman also runs on macOS and GNU/Linux:

<img src="docs/assets/screenshot-ja-macos.png">

## Requirements
- Windows 11 (x64, Arm)
- macOS (Apple Silicon)
- GNU/Linux (x64, Arm64; X11 or Wayland)

## Install & Run
Download the executable from Releases and run it.

The macOS build is not notarized, so its extended attributes have to be removed first:

```
xattr -cr ReProgman.app
```

On Linux, the first launch imports installed applications from XDG
`applications` directories. Refresh from Applications imports later additions.
Entries use the desktop file's `Exec` command and are grouped by its standard
category. Settings stay beside the executable when that directory is writable;
otherwise they are stored in `${XDG_CONFIG_HOME:-~/.config}/ReProgman`.

If an X11 window manager ignores the no-decoration hint, launch with
`--no-wm-decorations` to bypass window-manager control for the main window. The
ReProgman title bar and sizing frame can still move and resize it. Minimize and
maximize controls are disabled, and the window will not appear in the window
manager's task switcher or taskbar. This option is for X11 and XWayland only.

## Build & Debug

Building and running needs the .NET SDK 10.0+.

```csharp
dotnet run --project src/ReProgman
```

Create a self-contained Linux build with the .NET 10 SDK, Clang and the platform's
zlib development package:

```sh
build/publish-linux.sh linux-x64
# or: build/publish-linux.sh linux-arm64
```

The script writes a runnable directory and a `.tar.gz` archive under `artifacts/`.

## License

MIT License
