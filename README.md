[English](README.md) | [中文](README.zh-CN.md)

# RainbowClock

A bilingual (Chinese/English) clock mod for Beat Saber 1.40.8 (PC).

## Features

- Clock display: current time / session time, with 12/24-hour and seconds toggles
- Clock 2: an extra clock between the main clock and battery (UTC time / current time / session time)
- FPS display (main clock & clock 2): digits gradient-colored by refresh-rate cap, custom FPS label color
- Time zone: any system time zone, defaults to the PC's
- Rainbow effect: per-character colors
- Clock color, font size, position (custom X/Y/Z offset)
- Show-during-song toggle; respects the "No Text and HUDs" player setting
- Headset battery (ADB), gradient-colored by level; hidden when unavailable
- Automatic ADB device selection: wired USB first, then last successful device, then wireless VR headsets (skips phones)
- Bilingual UI (follows the game language automatically, manual switch available)

## Installation

Requires **BSIPA**, **BeatSaberMarkupLanguage** and **SiraUtil**.

1. Download the latest `RainbowClock_vX.X.X.zip` from the Releases page
2. Extract `RainbowClock.dll` into `Beat Saber/Plugins/`

## Headset Battery (ADB) Configuration

The mod queries the headset battery via adb: `adb shell cmd battery get level/status` (falls back to `dumpsys battery`).

**Works out of the box**: as long as `adb` is in PATH and the headset is connected via USB/Wi-Fi adb (e.g. `adb connect <IP>:5555`), the battery will show automatically.

For customization, edit `Beat Saber/UserData/彩虹时钟.json`:

```json
"AdbPath": "adb",                  // Path to the adb executable; defaults to PATH lookup
"AdbSerial": "",                   // Target serial when multiple devices exist (see `adb devices`); leave empty for auto
"BatteryRefreshSeconds": 60        // Auto-refresh interval in seconds (min 10); the settings button refreshes instantly
```

## Settings

- Main menu → left **MODS** list → 彩虹时钟
- Or Main menu → Options → Mods → 彩虹时钟

## Build

```powershell
dotnet build -c Release
```

References the game directory (default `E:\SteamLibrary\steamapps\common\Beat Saber`); override with `-p:GameDir=...`.

## Credits

- Feature design referenced from [ClockMod (Quest)](https://github.com/EnderdracheLP/ClockMod)
- PC implementation inspired by [SimpleClock](https://github.com/MadSquids/SimpleClock)
- FPS feature referenced from [FPS-Counter](https://github.com/Loloppe/FPS-Counter)

## License

MIT License — see [LICENSE](LICENSE).
