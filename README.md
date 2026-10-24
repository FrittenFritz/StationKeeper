<div align="center">

<img src="docs/banner.svg" alt="Station Keeper" width="100%">

<br>

![Platform](https://img.shields.io/badge/PLATFORM-WINDOWS-00D8FF?style=for-the-badge&labelColor=0A0F1A)
![.NET](https://img.shields.io/badge/.NET_FRAMEWORK-4.x-00D8FF?style=for-the-badge&labelColor=0A0F1A)
![License](https://img.shields.io/badge/LICENSE-MIT-00D8FF?style=for-the-badge&labelColor=0A0F1A)
![Version](https://img.shields.io/badge/VERSION-1.0.0-00D8FF?style=for-the-badge&labelColor=0A0F1A)

</div>

<img src="docs/divider.svg" width="100%">

## ◢ OVERVIEW

**Station Keeper** is a tiny Windows tool that keeps you from being kicked for inactivity in **Star Citizen**. While the game window is in focus, it nudges the mouse by a few pixels and back. That's all it does: it sends no key presses and no clicks.

<div align="center">
  <img src="docs/screenshot.png" alt="Station Keeper screenshot" width="380">
</div>

<img src="docs/divider.svg" width="100%">

## ◢ FEATURES

| | Feature | Details |
|---|---|---|
| ⏻ | **ACTIVE / INACTIVE toggle** | Large HUD toggle. The app always starts **inactive**. |
| ◎ | **Foreground-only** | A nudge is only sent when the foreground window belongs to the `StarCitizen` process. |
| ↔ | **Minimal input** | Moves the mouse 3 px and back via Win32 `SendInput`. **No keys, no clicks.** |
| ⏱ | **Random interval** | 180–300 s by default, with a live countdown. |
| ▣ | **Always on top** | Optional checkbox, saved across restarts. |
| ◈ | **Status panel** | Shows the countdown, the target-window state and the last event. |
| ⚙ | **Zero dependencies** | Built with the .NET Framework compiler that ships with Windows. No installer, autostart, registry entries or admin rights. |

<img src="docs/divider.svg" width="100%">

## ◢ BUILD

Requires Windows with .NET Framework 4.x, which is preinstalled on Windows 10/11.

```bat
build.cmd
```

The output is written to `bin\StationKeeper.exe`.

<img src="docs/divider.svg" width="100%">

## ◢ USAGE

1. Run `StationKeeper.exe`.
2. Click the toggle to switch to **ACTIVE**.
3. Switch to Star Citizen. Station Keeper nudges the mouse only while the game is in focus.
4. To quit, click **✕** in the top-right corner. You can drag the window anywhere.

```bat
StationKeeper.exe --min 120 --max 240
```

| Argument | Default | Description |
|---|---|---|
| `--min <seconds>` | `180` | Minimum interval between nudges |
| `--max <seconds>` | `300` | Maximum interval between nudges |

<img src="docs/divider.svg" width="100%">

## ◢ CONFIGURATION

Settings are stored in:

```
%APPDATA%\StationKeeper\settings.ini
```

```ini
[StationKeeper]
AlwaysOnTop=0
```

> [!NOTE]
> - **Always on top** cannot overlay the game in exclusive fullscreen. Use the *Borderless* window mode.
> - If Star Citizen runs as administrator, Station Keeper must run as administrator too. Otherwise Windows blocks the input.
> - The executable is unsigned, so Windows SmartScreen or Defender may warn on first launch.

<img src="docs/divider.svg" width="100%">

## ◢ DISCLAIMER

> [!WARNING]
> **The Roberts Space Industries Terms of Service prohibit the use of macro and automation software.** Using this tool may violate those terms and can result in action against your account. **Use it entirely at your own risk.** The authors accept no liability.

This is an unofficial fan project. It is not affiliated with, endorsed by or sponsored by Cloud Imperium Games or Roberts Space Industries. Star Citizen®, Roberts Space Industries® and Cloud Imperium® are trademarks of Cloud Imperium Rights LLC. No game assets are used.

<img src="docs/divider.svg" width="100%">

<div align="center">

**[MIT License](LICENSE)** · Made by **FrittenFritz**

<sub>STATION KEEPER // v1.0.0</sub>

</div>
