[English](README.md) | [中文](README.zh-CN.md)

# 彩虹时钟

面向 Beat Saber 1.40.8（PC）的中英双语时钟模组。

## 功能

四个横向槽位，从左到右：**时钟 1 · 时钟 2 · 时钟 3 · 电量**

- 时钟 1~3 可自由配置，每个时钟可单独选择内容与文字颜色；
  **局外**可选：隐藏 / 本次启动总时长 / 本次游玩时长 / 帧率 / 当前时间；
  **局内**另有：歌曲剩余时长 / 歌曲当前百分比
- **局外与局内的时钟内容分开配置**：「局外时钟 1/2/3」与「局内时钟 1/2/3」六行分别设置两套内容（例如局外"时间 + 游玩时长 + 帧率"、局内"时间 + 剩余时长 + 百分比"）。颜色、字号、位置等设置两套共用
- **计时类时钟的时长自适应显示**：不足 1 小时显示 `分:秒`（如 `01:59`），满 1 小时切换为 `时:分:秒`（如 `01:01:59`）。当前时间不受此影响，由「当前时间显示秒」单独控制
- **局内时钟置底**：仅局内生效，把「位置 Y（上下）/ Z（前后）」临时换成内置的置底数值（-3.4 / 2.4），不改写你的配置；出歌即恢复。好处是外面随便改「位置 Y/Z」都不会影响局内那套摆位
- **局内时钟缩放**：仅局内生效的倍率（0.25~3，默认 1），局外不受影响。作用在字号上，整体等比缩放且不会糊
- **本次游玩时长**只在歌曲/关卡内累计，排除菜单挂机与暂停，与「本次启动总时长」是两个不同的值；两者恒定显示秒（`d:hh:mm:ss`）
- 每个槽位按自身内容紧贴排布，隐藏或内容较短的槽位不会留下空白
- 槽位 4 固定为头显电量（ADB），按电量梯度着色；取不到电量时该槽位自动隐藏
- 时钟颜色、字号、位置（X/Y/Z 自定义偏移）；槽位之间以 ` · `（圆点 + 两侧各一空格）分隔，圆点跟随所在槽位的颜色（彩虹开启时逐字符彩虹）
- 彩虹效果开启时自动隐藏颜色设置行（彩虹模式下单独的颜色不生效）
- FPS 数字按帧率上限梯度着色，「FPS」字样可单独设色
- 彩虹效果：逐字符彩色显示
- 时间始终使用电脑（Windows）的本地时区，没有时区设置项
- 游戏中显示开关，尊重「无文本和 HUD」玩家设置
- ADB 设备自动选择：有线优先 → 记忆上次成功设备 → 无线 VR 头显（跳过手机）
- 中英双语界面（自动跟随游戏语言，可手动切换）

## 安装

需要 **BSIPA**、**BeatSaberMarkupLanguage**、**SiraUtil**。

1. 下载最新 Release 中的 `RainbowClock_vX.X.X.zip`
2. 解压 `RainbowClock.dll` 放入 `Beat Saber/Plugins/`

## 头显电量（ADB）配置

模组通过 adb 查询头显电量：`adb shell cmd battery get level/status`（失败自动降级 `dumpsys battery`）。
没有在线设备时不再执行 adb，电量槽位自动隐藏。

### adb 放在哪里

模组按以下顺序查找 `adb.exe`，**任一位置有即可用**（都不需要时槽位自动隐藏，不影响其它功能）：

| 优先级 | 位置 | 说明 |
|---|---|---|
| 1 | 配置项 `AdbPath` | 显式指定，最高优先级 |
| 2 | **游戏根目录**（`Plugins` 的上一级） | **推荐**：把 `adb.exe` 与 `AdbWinApi.dll`、`AdbWinUsbApi.dll` 一起放进去即可 |
| 3 | 系统 `PATH` | 如 `C:\Windows\System32\adb.exe` 或 Android platform-tools |

**推荐放在游戏根目录**：好处是不依赖系统环境，换电脑时跟着游戏目录一起走；日志里会打印实际用的是哪一个：

```
[RainbowClock] using bundled adb: E:\...\Beat Saber\adb.exe
```

若三处都没有，日志会出现 `adb start failed`，电量槽位一直隐藏。

### 开启无线调试

配套脚本 [`scripts/Enable-Wireless-ADB.bat`](scripts/Enable-Wireless-ADB.bat)：插上 USB 线后双击运行，它会自动切到 TCP 模式并建立无线连接，之后即可拔线。

脚本自己也会找 adb，顺序为 **游戏目录 → PATH → System32 → Android SDK**（与模组一致，因此游戏目录那一份 adb 可以两用）。

启用成功后，模组即可读到电量。注意：

- 脚本必须**先用 USB 线跑一次**（它从 `adb devices` 里挑选 USB 设备来切换）
- `adb tcpip 5555` **不持久**，头显重启后需重新插线跑一次脚本
- 顺序建议：**先跑脚本连上头显，再启动游戏**

### 配置项

编辑 `Beat Saber/UserData/彩虹时钟.json`：

```json
"AdbPath": "adb",                  // adb 可执行文件路径，默认自动查找（配置 > 游戏目录 > PATH）
"AdbSerial": "",                   // 多设备时指定目标序列号（adb devices 查看），留空自动
"KillAdbOnExit": true,             // 退出游戏时结束本模组拉起的 adb 进程（默认开）
"BatteryRefreshSeconds": 30        // 自动刷新间隔（秒，下限 10），设置页按钮可手动立即刷新
```

设备自动选择优先级：有线 USB → 上次成功的设备 → 无线 VR 头显（跳过手机）。

电量查询失败的降级策略：进程启动 10 秒后首次查询，失败后按递增间隔重试，**连续失败 3 次即停止轮询并结束 adb 进程**（避免残留的 adb 导致 Steam 无法识别游戏退出）；设置页「刷新电量」可随时重新启用轮询。


## 设置入口

- 主菜单左侧 **MODS** 列表 → 彩虹时钟
- 或 主菜单 → 选项 → Mods → 彩虹时钟

设置页内容顺序：语言 → 局外时钟 1/2/3 → 局内时钟 1/2/3 → 局内时钟置底 → 彩虹效果 → 游戏中显示 → 12/24 小时制 → 显示秒 → 字号 → 位置 X/Y/Z → 刷新电量 → 时钟 1/2/3 颜色 → FPS 颜色。

（颜色四行在彩虹效果开启时会自动隐藏。）

## 构建

```powershell
dotnet build -c Release
```

项目引用游戏目录（默认 `E:\SteamLibrary\steamapps\common\Beat Saber`），可通过 `-p:GameDir=...` 覆盖。

若 NuGet restore 报 `Value cannot be null. (Parameter 'path1')`，是环境缺少 `ProgramFiles(x86)` 变量：
先把它设为 `C:\Program Files (x86)` 再构建；已还原过的项目也可直接 `--no-restore` 构建。

## 致谢

- 功能参考 [ClockMod (Quest)](https://github.com/EnderdracheLP/ClockMod)
- 参考 [SimpleClock](https://github.com/MadSquids/SimpleClock) 的 PC 实现
- FPS 功能参考 [FPS-Counter](https://github.com/Loloppe/FPS-Counter)

## 许可证

MIT License — 详见 [LICENSE](LICENSE)。
