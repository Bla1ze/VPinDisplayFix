# VPin Display Fix

A startup utility that automatically finds the 4K display and updates PinUP Popper and Visual Pinball X to use it as the playfield. Solves the common issue where Windows shuffles display IDs after reboots or HDMI reconnections, causing VPX and PinUP Popper to send the playfield to the wrong monitor.

Designed for AtGames Legends 4KP running in OTG mode with a multi-display virtual pinball setup.

## Prerequisites

- **Windows 11**
- **.NET 8 SDK** (for building from source) - https://dotnet.microsoft.com/download/dotnet/8.0
- **PinUP Popper** and/or **Visual Pinball X** installed

## Build

```powershell
cd VPinDisplayFix
dotnet publish -c Release
```

The executable will be at:
```
bin\Release\net8.0-windows\win-x64\publish\VPinDisplayFix.exe
```

Or download a pre-built release from the [Releases](../../releases) page.

## Usage

```powershell
# Preview what would change (recommended first run)
VPinDisplayFix.exe --dry-run

# Auto-fix both PinUP Popper and VPX
VPinDisplayFix.exe

# Only fix PinUP Popper
VPinDisplayFix.exe --pinup-only

# Only fix VPX
VPinDisplayFix.exe --vpx-only

# Custom PinUpPlayer.ini location
VPinDisplayFix.exe --pinup-ini "D:\PinUP\PinUpPlayer.ini"
```

### All Options

| Option | Description |
|---|---|
| `--dry-run` | Show what would change without modifying anything |
| `--pinup-ini <path>` | Path to PinUpPlayer.ini (default: `C:\vpinball\PinUPSystem\PinUpPlayer.ini`) |
| `--pinup-section <name>` | INI section for playfield (default: `INFO3`) |
| `--vpx-only` | Only update VPX registry, skip PinUP Popper |
| `--pinup-only` | Only update PinUP Popper, skip VPX |
| `--width <pixels>` | Target display width (default: 3840) |
| `--height <pixels>` | Target display height (default: 2160) |
| `-h`, `--help` | Show help |

## How It Works

1. **Enumerates all connected displays** using the Win32 `EnumDisplayMonitors` API
2. **Finds the 4K display** (3840x2160, handles both landscape and portrait orientations)
3. **Updates PinUP Popper** — writes the correct virtual desktop coordinates to the `[INFO3]` (Playfield) section of `PinUpPlayer.ini`. A timestamped backup is created before any changes.
4. **Updates Visual Pinball X** — sets the correct display index in the registry at `HKCU\Software\Visual Pinball\VP10\Player\Display`

If everything is already correct, no changes are made.

## Auto-Start at Login

Run this before PinUP Popper launches to ensure the playfield is always on the right display.

1. Right-click `VPinDisplayFix.exe` and select **Create shortcut**
2. Right-click the shortcut, select **Properties**
3. In the **Target** field, confirm the path: `"C:\path\to\VPinDisplayFix.exe"`
4. Move the shortcut to your Startup folder (type `shell:startup` in the Explorer address bar)

The tool runs instantly and exits — it does not stay resident in memory.

## Customization

**Different playfield resolution:** If your playfield is not 3840x2160, use:
```powershell
VPinDisplayFix.exe --width 1920 --height 1080
```

**Different PinUP Popper screen section:** If your playfield is not `[INFO3]`, use:
```powershell
VPinDisplayFix.exe --pinup-section INFO2
```

**Different PinUpPlayer.ini location:**
```powershell
VPinDisplayFix.exe --pinup-ini "D:\Games\PinUPSystem\PinUpPlayer.ini"
```
