# 彩虹时钟 RainbowClock

面向 Beat Saber 1.40.8（PC）的中英双语时钟模组，由 Quest 版 [ClockMod](https://github.com/EnderdracheLP/ClockMod) 移植。

## 功能

- 时钟显示：当前时间 / 本次游玩时长，12/24 小时制、显示秒可切换
- 时钟二：在主时钟与电量之间追加显示（UTC 时间 / 当前时间 / 本次游玩）
- 时区设置：支持任意系统时区，默认跟随电脑
- 彩虹效果：逐字符彩色显示
- 时钟颜色、字号、位置（X/Y/Z 自定义偏移）
- 游戏中显示开关，尊重"无文本和 HUD"玩家设置
- 头显电量（ADB）：通过 `adb shell cmd battery get level/status` 查询（自动降级 `dumpsys battery`），充电/满电青色显示，获取不到自动隐藏
- 中英双语界面（自动跟随游戏语言，可手动切换）
- 新年祝福、愚人节彩蛋

## 安装

需要 **BSIPA**、**BeatSaberMarkupLanguage**、**SiraUtil**。

1. 下载最新 Release 中的 `RainbowClock_vX.X.X.zip`
2. 解压 `RainbowClock.dll` 放入 `Beat Saber/Plugins/`
3. （可选）配置 ADB 电量：确保 `adb` 可用，或在模组设置里填写 adb 路径

## 设置入口

- 主菜单左侧 **MODS** 列表 → 彩虹时钟
- 或 主菜单 → 选项 → Mods → 彩虹时钟

## 构建

```powershell
dotnet build -c Release
```

项目引用游戏目录（`E:\SteamLibrary\steamapps\common\Beat Saber`），可通过 `-p:GameDir=...` 覆盖。

## 致谢

- 功能移植自 [ClockMod (Quest)](https://github.com/EnderdracheLP/ClockMod)
- 参考 [SimpleClock](https://github.com/MadSquids/SimpleClock) 的 PC 实现
