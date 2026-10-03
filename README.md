[English](README.md) | [中文](README.zh-CN.md)

# RainbowClock

A bilingual (Chinese/English) clock mod for Beat Saber 1.40.8 (PC).

## Features

Four horizontal slots, left to right: **Clock 1 · Clock 2 · Clock 3 · Battery**

- Slots 1-3 are freely configurable, each with its own content and text color.
  **Out of song**: hidden / game uptime / play time / FPS / current time.
  **In song** also offers: song remaining / song progress
- **Out-of-song and in-song clocks are configured separately**: "Out-of-song Clock 1/2/3" and "In-song Clock 1/2/3" rows select the content for each clock outside vs. during a song. Colors, font size and position are shared
- **Durations adapt to their length**: under an hour they show `mm:ss` (e.g. `01:59`), and switch to `hh:mm:ss` once a full hour has accumulated (e.g. `01:01:59`). The current time is unaffected and is controlled separately by "Current Time Seconds"
- **In-song Bottom Align**: in song only, temporarily replaces "Position Y / Z" with the built-in bottom values (-3.4 / 2.4) without touching your config; it restores once the song ends. The benefit is that you can change "Position Y/Z" for out-of-song without disturbing the in-song placement
- **In-song Clock Scale**: an in-song-only multiplier (0.25-3, default 1); outside is unaffected. Applied to the font size, so the whole clock scales proportionally and stays crisp
- **Game uptime** is time since the game process started (includes menu idle); **play time** counts only time spent inside songs, excluding menus and pauses
- Slots are sized to their own content and pack tight against each other; a hidden or short slot leaves no dead space
- Slot 4 is locked to headset battery (ADB), gradient-colored by level; the slot hides itself when no reading is available
- Clock color, font size, position (custom X/Y/Z offset); a fixed ` · ` (dot with one space each side) separates the slots and follows each slot's color (rainbow when the rainbow effect is on)
- Color rows are hidden while the rainbow effect is on, since individual colors do not apply in rainbow mode
- FPS digits are gradient-colored by refresh-rate cap; the "FPS" label has its own color
- FPS digits are gradient-colored by refresh-rate cap; the "FPS" label has its own color
- Rainbow effect: per-character colors
- Time is always taken from the PC's (Windows) local time zone; there is no time zone setting
- Show-during-song toggle; respects the "No Text and HUDs" player setting
- Automatic ADB device selection: wired USB first, then last successful device, then wireless VR headsets (skips phones)
- Bilingual UI (follows the game language automatically, manual switch available)

## Installation

Requires **BSIPA**, **BeatSaberMarkupLanguage** and **SiraUtil**.

1. Download the latest `RainbowClock_vX.X.X.zip` from the Releases page
2. Extract `RainbowClock.dll` into `Beat Saber/Plugins/`

## Headset Battery (ADB) Configuration

The mod queries the headset battery via adb: `adb shell cmd battery get level/status` (falls back to `dumpsys battery`).
If no device is online, the battery slot hides and nothing is queried.

### Where to put adb

The mod looks for `adb.exe` in this order — **any one of them is enough** (if none is found the slot simply stays hidden and nothing else is affected):

| Priority | Location | Notes |
|---|---|---|
| 1 | `AdbPath` config value | Explicit path, highest priority |
| 2 | **Game root folder** (the parent of `Plugins`) | **Recommended**: drop `adb.exe` plus `AdbWinApi.dll` and `AdbWinUsbApi.dll` there |
| 3 | System `PATH` | e.g. `C:\Windows\System32\adb.exe` or Android platform-tools |

**Putting it in the game root is recommended**: it keeps the mod independent of the system environment and travels with the game folder. The log prints which one is in use:

```
[RainbowClock] using bundled adb: E:\...\Beat Saber\adb.exe
```

If none of the three exists, the log shows `adb start failed` and the battery slot stays hidden.

### Enabling wireless ADB

Bundled helper: [`scripts/Enable-Wireless-ADB.bat`](scripts/Enable-Wireless-ADB.bat). With the headset plugged in over USB, run it — it switches adbd to TCP mode and establishes the wireless connection, after which you can unplug the cable.

The script resolves adb itself in the order **game folder → PATH → System32 → Android SDK** (same as the mod, so a single copy in the game folder serves both).

Once connected, the mod can read the battery. Keep in mind:

- The script must be run **once over USB** (it picks the USB device out of `adb devices` to switch it)
- `adb tcpip 5555` is **not persistent** — run the script again over USB after a headset reboot
- Suggested order: **run the script first, then start the game**

### Options

Edit `Beat Saber/UserData/彩虹时钟.json`:

```json
"AdbPath": "adb",                  // Path to the adb executable; auto-resolved by default (config > game folder > PATH)
"AdbSerial": "",                   // Target serial when multiple devices exist (see `adb devices`); empty for auto
"KillAdbOnExit": true,             // Kill adb processes started by this mod when the game exits (on by default)
"BatteryRefreshSeconds": 30        // Auto-refresh interval in seconds (min 10); the settings button refreshes instantly
```

Automatic device selection: wired USB → last successful device → wireless VR headset (phones are skipped).

Failure fallback: the first query happens 10 s after process start; failures are retried at increasing intervals, and after **3 consecutive failures polling stops and adb is terminated** (so a leftover adb can't keep Steam from noticing the game exited). The "Refresh Battery" button in the settings re-enables polling at any time.


## Settings

- Main menu → left **MODS** list → 彩虹时钟
- Or Main menu → Options → Mods → 彩虹时钟

## Build

```powershell
dotnet build -c Release
```

References the game directory (default `E:\SteamLibrary\steamapps\common\Beat Saber`); override with `-p:GameDir=...`.

If NuGet restore fails with `Value cannot be null. (Parameter 'path1')`, the environment is missing the
`ProgramFiles(x86)` variable — set it (e.g. `C:\Program Files (x86)`) before building, or use `--no-restore`
on an already-restored tree.

## Credits

- Feature design referenced from [ClockMod (Quest)](https://github.com/EnderdracheLP/ClockMod)
- PC implementation inspired by [SimpleClock](https://github.com/MadSquids/SimpleClock)
- FPS feature referenced from [FPS-Counter](https://github.com/Loloppe/FPS-Counter)

## License

MIT License — see [LICENSE](LICENSE).
