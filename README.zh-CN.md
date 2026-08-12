[English](README.md) | [中文](README.zh-CN.md)

# 彩虹时钟

面向 Beat Saber 1.40.8（PC）的中英双语时钟模组。

## 功能

- 时钟显示：当前时间 / 本次游玩时长，12/24 小时制、显示秒可切换
- 时钟二：在主时钟与电量之间追加显示（UTC 时间 / 当前时间 / 本次游玩）
- FPS 显示（时钟一/时钟二）：数字按帧率上限梯度着色，FPS 字样可自定义颜色
- 时区设置：支持任意系统时区，默认跟随电脑
- 彩虹效果：逐字符彩色显示
- 时钟颜色、字号、位置（X/Y/Z 自定义偏移）
- 游戏中显示开关，尊重「无文本和 HUD」玩家设置
- 头显电量（ADB）查询，按电量梯度着色，获取不到自动隐藏
- ADB 设备自动选择：有线优先 → 记忆上次成功设备 → 无线 VR 头显（跳过手机）
- 中英双语界面（自动跟随游戏语言，可手动切换）

## 安装

需要 **BSIPA**、**BeatSaberMarkupLanguage**、**SiraUtil**。

1. 下载最新 Release 中的 `RainbowClock_vX.X.X.zip`
2. 解压 `RainbowClock.dll` 放入 `Beat Saber/Plugins/`

## 头显电量（ADB）配置

模组通过 adb 查询头显电量：`adb shell cmd battery get level/status`（失败自动降级 `dumpsys battery`）。

**默认即可用**（无需任何配置）：只要 `adb` 在 PATH 中、且头显已通过 USB/Wi-Fi adb 连接（如 `adb connect <IP>:5555`），电量会自动显示。

如需自定义，编辑 `Beat Saber/UserData/彩虹时钟.json`：

```json
"AdbPath": "adb",                  // adb 可执行文件路径，默认从 PATH 查找
"AdbSerial": "",                   // 多设备时指定目标序列号（adb devices 查看），留空自动
"BatteryRefreshSeconds": 60        // 自动刷新间隔（秒，下限 10），设置页按钮可手动立即刷新
```

## 设置入口

- 主菜单左侧 **MODS** 列表 → 彩虹时钟
- 或 主菜单 → 选项 → Mods → 彩虹时钟

## 构建

```powershell
dotnet build -c Release
```

项目引用游戏目录（默认 `E:\SteamLibrary\steamapps\common\Beat Saber`），可通过 `-p:GameDir=...` 覆盖。

## 致谢

- 功能参考 [ClockMod (Quest)](https://github.com/EnderdracheLP/ClockMod)
- 参考 [SimpleClock](https://github.com/MadSquids/SimpleClock) 的 PC 实现

## 许可证

MIT License — 详见 [LICENSE](LICENSE)。
