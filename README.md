# SCGuard

Tray app for Windows. Turns off Mullvad when Star Citizen (or the RSI Launcher) is open, turns it back on when you close them.

EAC hates VPNs and I got sick of alt-tabbing to toggle Mullvad every time. So this exists now.

**There is no .exe download.** You build it yourself, takes like 30 seconds. See [Building](#building) below.

## How it works

Checks every 2 seconds if `StarCitizen.exe` or `RSI Launcher.exe` is running. If either one is open, it runs `mullvad disconnect`. When both are closed, it runs `mullvad connect`. Thats the whole app.

VPN stays off the entire time the launcher is open, not just in-game. EAC phones home during login so yeah, has to be this way.

VRChat, Apex, other EAC games don't trigger it. Only SC.

## Tray icons

- **Green (VP)** - Mullvad on, you're good
- **Amber (SC)** - SC or launcher detected, Mullvad off
- **Blue (MO)** - You manually forced it off

Right-click for manual override, left-click for status.

## Building

Need .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0

Then just double-click `BUILD.bat`. Or if you want to do it yourself:

```
dotnet publish SCGuard.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Exe ends up in `bin/Release/net8.0-windows/win-x64/publish/`

## Autostart

`Win+R` > `shell:startup` > drop `SCGuard.exe` in there.

## Needs

- Windows 10/11
- Mullvad installed (the CLI is in PATH by default)
- .NET 8 SDK to build (not needed to run)

## Good to know

This fully disconnects Mullvad. Your whole connection is naked while SC or the launcher is open. If thats a problem for you, look into TunnlTo for split tunneling instead.

If SCGuard crashes or you close it, it reconnects Mullvad first so you don't end up exposed without realizing.

## Credits

Made by **ALEKSIS-SENPAI** (always down to play SC)

Made on a whim at like 2am. It works, don't ask me how.

Feel free to fork and edit. No copyright.
