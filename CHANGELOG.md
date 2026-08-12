# Changelog / 更新日志

## v1.1.0 (2026-08-12)

### Added / 新增

- **FPS 显示**：时钟一、时钟二均可选择显示 FPS；数字按帧率上限梯度着色（≥上限绿色，低于上限一定帧数红色，中间黄色——上限≤60 低 5 帧红 / 60-90 低 10 帧红 / 90-120 低 20 帧红 / >120 低 30 帧红）；「FPS」字样可自定义颜色（彩虹开启时逐字符彩虹，数字不受彩虹/自定义色影响）
  FPS display for both main clock and clock 2; digits gradient-colored by the refresh-rate cap (green ≥ cap, red below cap by 5/10/20/30 for caps ≤60/60-90/90-120/>120, yellow in between); the "FPS" label has its own color (rainbow when enabled); digits are never affected by rainbow/custom colors.
- 电量梯度改为每 20% 均匀分布（红→橙红→橙→黄→黄绿→绿）
  Battery gradient now evenly spaced every 20% (red → orange-red → orange → yellow → yellow-green → green).
- ADB 设备自动选择：有线 USB 优先 → 记忆上次成功设备 → 无线 VR 头显（自动识别 Quest/Pico/Vive/Index，跳过手机）→ 无线其他
  Automatic ADB device selection: wired USB first → last successful device → wireless VR headsets (auto-detects Quest/Pico/Vive/Index, skips phones) → other wireless devices.
- 连接状态显示在「刷新电量」按钮括号内（有线/无线；无线显示 IP 后三位）
  Connection status shown in the refresh button's parentheses (wired/wireless; wireless shows the last 3 digits of the IP).

### Changed / 变更

- 电量默认刷新间隔 60s → 30s
  Default battery refresh interval changed from 60s to 30s.

### Fixed / 修复

- 设置页双入口（Mods 列表 + 主菜单按钮）滚动初始化互相覆盖，导致设置入口无法滚动
  Settings page scrolling broken via the Mods list entry (two entries shared init state).
- 设置页内容被布局系统反复拽回（初始页/滚动页交叉闪烁）、页面空白
  Settings content dragged back by the layout system (flicker between initial/scroll positions), blank page.
- `adb devices -l` 解析失败（Windows 输出用空格对齐而非 tab）
  `adb devices -l` parsing failed (Windows output is space-aligned, not tab-separated).
