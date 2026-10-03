# Changelog / 更新日志

## v1.3.10 (2026-10-03)

### Fixed / 修复

- **修复开启彩虹效果后，电量槽位显示出字面颜色代码的问题**：电量串由 `AdbBattery.FormatBattery` 生成，本身自带 `<color=#RRGGBB>` 渐变着色；而彩虹效果是逐字符包裹颜色标签的，标签本身也被当成字符着色后，TMP 会把 `<`、`c`、`o`、`l`…… 当作可见文字，界面上就出现了类似 `<color=#A3CD20>78%</color>` 的字符。
  Fixed the battery slot showing literal colour codes when the rainbow effect was on: the battery string already carries its own `<color=#RRGGBB>` gradient colour, and the per-character rainbow pass used to wrap the tag characters themselves, so TMP rendered `<`, `c`, `o`, `l` … as visible text (e.g. `<color=#A3CD20>78%</color>`).
- **电量槽位固定使用自带渐变电池色**，不再参与彩虹着色、也不再被槽位颜色覆盖（彩虹开启或关闭表现一致，按电量红→黄→绿）。
  The battery slot now always keeps its built-in gradient colour (red → yellow → green by level); it neither takes part in the rainbow effect nor gets overridden by the slot colour.

### Changed / 变更

- `RainbowText.Apply` 改为**识别富文本标签**：完整标签原样透传，仅对可见字符着色；颜色索引只在可见字符上前进，因此纯文本槽位（时间、时长、FPS 字样）的取色节奏与之前完全一致。
  `RainbowText.Apply` is now rich-text aware: complete tags pass through untouched and only visible characters are coloured. The colour index advances on visible characters only, so plain-text slots colour exactly as before.
- 新增 `ClockConfig.BatterySlot` 常量，明确槽位 4 为固定电量槽（替代原先的硬编码索引）。
  Added the `ClockConfig.BatterySlot` constant to name slot 4 (previously a hard-coded index).

### Added / 新增

- **`scripts/Enable-Wireless-ADB.bat`**：一键为 USB 连接的头显开启无线 ADB（自动读取 Wi-Fi IP、`adb tcpip 5555`、`adb connect` 并验证）。**原样收录，未做改动**；脚本自身查找 adb 的顺序为 PATH → System32 → Android SDK。
  **`scripts/Enable-Wireless-ADB.bat`**: one-click wireless ADB for a USB-connected headset (reads the Wi-Fi IP, runs `adb tcpip 5555`, `adb connect`, then verifies). Included **as-is, unmodified**; the script resolves adb in the order PATH → System32 → Android SDK.
- 中英文 README 的「头显电量（ADB）配置」新增 **adb 安放位置**说明（三级查找顺序与推荐做法）、无线调试脚本用法与注意事项。
  The bilingual README ADB section now documents **where to put adb** (three-level lookup order and the recommended choice), plus usage notes for the wireless ADB script.

## v1.3.9 (2026-09-14)

### Added / 新增

- **局内时钟缩放**（「局内时钟缩放」行，倍率 0.25~3，默认 1）：**只作用于局内**，局外完全不受影响。缩放作用在字号上——槽宽、屏高、间距全部由字号推导，所以等同于整体等比缩放，且 TMP 是 SDF 字体放大也不糊
  "In-song Clock Scale" (multiplier 0.25-3, default 1): **in-song only**, outside is completely unaffected. It scales the font size; since slot widths, screen height and spacing all derive from it, the whole clock scales proportionally and stays crisp (TMP is an SDF font).

### Changed / 变更

- **「局内时钟置底」施加的数值改为 -3.4（上下）/ 2.4（前后）**：原先只覆盖 Y 且硬编码为 -2.7，现在同时覆盖上下与前后两个方向。数值取自你调好的那组，所以置底开着与关着在局内表现一致；真正的用处是**之后在外面随便改「位置 Y/Z」都不会再影响局内那套摆位**
  The in-song bottom-align offsets are now -3.4 (Y) and 2.4 (Z): previously only Y was overridden, hardcoded to -2.7. Both axes are now pinned. The values match the ones you tuned, so the toggle looks identical on/off in song — the real benefit is that you can now change "Position Y/Z" for out-of-song without disturbing the in-song placement.

## v1.3.8 (2026-09-14)

### Added / 新增

- **局内时钟新增「歌曲剩余时长」与「歌曲当前百分比」**：读游戏的 `AudioTimeSyncController.songLength / songTime` 获得实时数据，剩余时长按计时格式显示，百分比为整数（如 `45%`）。这两项只出现在「局内时钟 1/2/3」的下拉里
  The in-song clocks gained "Song Remaining" and "Song Progress", read live from the game's `AudioTimeSyncController.songLength / songTime`. Remaining uses the duration format; progress is an integer percentage (e.g. `45%`). Both appear only in the in-song dropdowns.

### Changed / 变更

- **计时类时钟的时长格式改为自适应**：不足 1 小时只显示 `分:秒`（如 `01:59`），累计满 1 小时才切换成 `时:分:秒`（如 `01:01:59`），小时数不封顶（26 小时显示 `26:00:00`，不按天截断）。适用于「本次启动总时长」「本次游玩时长」「歌曲剩余时长」
  Durations now adapt: under an hour they show `mm:ss` (e.g. `01:59`), and only once a full hour has accumulated do they switch to `hh:mm:ss` (e.g. `01:01:59`). Hours are not capped (26 hours shows `26:00:00`, no day wrap). Applies to game uptime, play time and song remaining.
- 移除「UTC 时间」内容选项（局外与局内的下拉都不再有）
  The "UTC Time" content option is gone from both dropdowns.
- 「显示秒」改名为「当前时间显示秒」——它现在只作用于当前时间；所有计时类时钟恒定显示秒，不受该开关影响
  "Show Seconds" renamed to "Current Time Seconds" — it now applies only to the current time; all duration clocks always show seconds regardless of the switch.

### Fixed / 修复

- 旧配置里残留的已移除取值（`UTC = 5`）会被归一化为「隐藏」，避免下拉选项列表里找不到该值而显示错位（同时运行时也按隐藏处理，下次保存配置时自愈）
  A stale removed value in an existing config (`UTC = 5`) is normalized to "Hidden", so the dropdown no longer shows a mismatched entry; runtime treats it as hidden too and the config self-heals on the next save.

## v1.3.7 (2026-09-14)

### Removed / 移除

- **移除「时区」设置行**：时间始终使用电脑（Windows）的本地时区。
  原先"默认跟随电脑时区"的设计存在实际缺陷——设置页的时区下拉会把当前系统时区**写回配置**
  （实测配置文件里已被写成 `"TimeZoneId": "China Standard Time"`），一旦写回就不再跟随系统，
  表面上"看起来一样"、实际改系统时区不会生效。直接跟随系统反而更符合预期，
  同时省去了启动时枚举上百个系统时区的开销
  The time zone setting row is gone: the clock always uses the PC's (Windows) local time zone.
  The old "empty = follow the PC" design had a real flaw — the settings dropdown wrote the current system
  zone back into the config (the config had been pinned to `"TimeZoneId": "China Standard Time"`), and once
  written it no longer followed the system: it merely *looked* right while ignoring later system changes.
  Following the system directly is both simpler and more correct, and it drops the cost of enumerating
  every system time zone at startup.

### Changed / 变更

- 配置项 `TimeZoneId` 不再使用（旧配置文件里的该字段会被忽略，无需手动清理）
  The `TimeZoneId` config key is no longer used (an existing value in the JSON is simply ignored; no manual cleanup needed).

## v1.3.6 (2026-09-14)

### Changed / 变更

- **去掉"局外时钟设置"/"局内时钟设置"两个分区标题行**，改把前缀写进各行标签本身：
  局外时钟 1/2/3、局内时钟 1/2/3，以及局内时钟置底。不再有标题行，也就不会再有标题造成的空白
  The two section title rows are gone; the prefix now lives in each row's own label:
  "Out-of-song Clock 1/2/3", "In-song Clock 1/2/3" and "In-song Bottom Align". With no title rows there is no title-related blank space left.

### Fixed / 修复

- **分区标题后面的大段空白（v1.3.5 未修好）**：根因是 `label` 元素**自带 LayoutElement**，
  行高回填因此走了"读 `rect.height`"那条分支，而布局未完成时该值是预制件的默认尺寸（接近 100），
  于是被当成内容高度写进 preferredHeight，把那一行撑成近百单位高。现在对超过 30 单位的高度一律
  判定为"读到预制件默认尺寸"、改用常规行高，从机制上堵住这个坑
  The large blank after a section title (not fixed in v1.3.5): the `label` element **ships its own LayoutElement**,
  so the height backfill took the "read `rect.height`" branch — and before layout that value is the prefab default
  (close to 100), which got written into preferredHeight and stretched the row to nearly a hundred units.
  Heights above 30 are now treated as "prefab default" and replaced with the regular row height.

## v1.3.5 (2026-09-14)

### Added / 新增

- **时钟局内置底**（局内时钟设置分区内）：仅局内（歌曲/关卡中）生效，临时把「位置 Y」当作 -2.7 使用，把时钟压到画面下方；不改写用户的配置值，出歌即恢复
  "Bottom Align In Song" (in the In-song Clocks section): applies only during songs, temporarily using -2.7 as the Y offset to push the clock down and out of the way. The configured value is not modified; it returns to normal once the song ends.

### Fixed / 修复

- **设置页局内/局外两段之间出现一大段空白**：分区标题行的行高误用了控件的 `rect.height`（预制件默认尺寸可能很大，实测可撑到近百单位）。改为测量文本的真实首选高度（文本为空或量值不可信时用 7 单位兜底），并写进 LayoutElement 保证布局与滚动高度一致
  Large blank gap between the out-of-song and in-song sections: the section title row's height was taken from the widget's `rect.height` (the prefab's default, which can be close to a hundred units). It now measures the text's real preferred height (falling back to 7 units when empty or implausible) and writes it into a LayoutElement so layout and scroll height agree.

### Changed / 变更

- 分隔符圆点**两侧各增加一个空格**（` · `），圆点左右间隙完全由空格决定，天然等宽；槽位内余量归零，避免额外空隙叠加到圆点一侧
  The dot separator now has **one space on each side** (` · `). Both gaps are governed purely by those spaces, so they are equal by construction; the per-slot padding was reduced to zero so no extra gap piles up on one side.

## v1.3.4 (2026-09-14)

### Added / 新增

- **局内 / 局外两套时钟内容配置**：设置页分为「局外时钟设置」与「局内时钟设置」两段，各含时钟 1/2/3 的内容下拉。局外（菜单/大厅）与局内（歌曲/关卡中）可显示完全不同的内容组合（例如局外"当前时间 + 游玩时长 + 帧率"、局内"当前时间 + 帧率"）。颜色、字号、位置等设置两套共用；时钟 4 仍是固定电量
  Two independent content sets for out-of-song and in-song: the settings page is split into "Out-of-song Clocks" and "In-song Clocks", each with content dropdowns for clocks 1-3. Colour, font size and position are shared; clock 4 remains the fixed battery slot.

### Changed / 变更

- **圆点两侧空隙改为对称**：槽间距固定为 0（原先固定圆点画在后一个槽位内容左侧，导致这段空隙全落在圆点左边，左右不对称）。现在两侧都由字形边距决定，视觉上圆点紧贴两边（`21:02:45·0:01:56·FPS 175`）
  Dot spacing is now symmetric: the inter-slot gap is fixed at 0 (previously the whole gap landed left of the dot because the dot is drawn at the left of the following slot's content). Both sides are now governed by the glyph's side bearing, so the dot sits tight against both neighbours.
- 时钟颜色 / FPS 颜色四行移到最后，并**在彩虹开启时自动隐藏**（彩虹模式下这些单独颜色不生效，显隐会同步重算滚动范围）
  The four colour rows moved to the bottom and are now hidden while the rainbow effect is on (individual colours have no effect in rainbow mode; the scroll range is recomputed on show/hide).
- 设置页顺序：时区 → 语言 → 局外时钟 1/2/3 内容 → 局内时钟 1/2/3 内容 → 彩虹 → 游戏中显示 → 12/24 小时制 → 显示秒 → 字号 → 位置 X/Y/Z → 刷新电量 → 时钟颜色 → FPS 颜色
  Settings order: time zone → language → out-of-song clocks 1-3 → in-song clocks 1-3 → rainbow → show during song → 12/24h → seconds → font size → position X/Y/Z → refresh battery → clock colours → FPS colour.

## v1.3.3 (2026-09-14)

### Changed / 变更

- **槽位空隙进一步收窄到"贴紧"**：由 字号×0.7 改为 字号×0.25（字号 8 时 5.6 → 2.0）。固定圆点画在后一个槽位内容的左侧，因此这段空隙全部落在圆点左边，视觉上圆点紧贴右侧数字（`20:34:50 ·0:01:25`）
  Slot gap tightened from fontSize × 0.7 to fontSize × 0.25 (5.6 → 2.0 at size 8). The dot is drawn at the left of the following slot's content, so the gap sits left of the dot, making the dot hug the number on its right.
- **分隔符固定为圆点**，移除"分隔符"设置项（不再需要选择）
  The separator is now fixed to a dot; the separator setting row is gone.
- 圆点跟随所在槽位的着色规则：彩虹开启时逐字符彩虹，否则用该槽位的颜色（FPS 槽位用 FPS 颜色）
  The dot now follows its slot's colouring: per-character rainbow when rainbow is on, otherwise the slot's colour (the FPS slot uses the FPS colour).
- **「本次游玩时长」改为真实打歌时间**：只在歌曲/关卡内累计，排除菜单挂机与暂停；与「本次启动总时长」（进程启动至今的墙钟时间）不再是同一个值
  "Play Time" now measures actual gameplay: accumulated only inside songs/levels, excluding menu idle time and pauses. It is no longer the same value as "Game Uptime" (wall-clock since process start).
- 两个时长恒定显示秒，移除"不含秒"选项（`d:hh:mm:ss`）
  Both durations always show seconds; the "no seconds" options are removed (`d:hh:mm:ss`).
- 设置页重排：时区 → 语言 → 时钟 1/2/3 内容 → 时钟 1/2/3 颜色 → FPS 颜色 → 彩虹 → 其他
  Settings page reordered: time zone → language → clock 1/2/3 content → clock 1/2/3 colour → FPS colour → rainbow → the rest.

## v1.3.2 (2026-09-12)

### Fixed / 修复

- **时钟之间的空隙过大，且换分隔符也消不掉**：原先四个槽位等宽（都按最宽槽位撑开），短内容（如 `100%`）居中后两侧各留一大块死空白，再加 22 单位的硬编码间距 —— 分隔符只影响约 7 个单位，所以改分隔符看不出效果。现在每个槽位各自按内容紧贴测量，槽位间只留一个随字号缩放的空隙（字号×0.7）。电量槽处的空隙由约 30 收窄到 5.6（-81%），整行宽度收窄 38%~45%
  Gaps between the clocks were too wide and changing the separator could not fix it: all four slots were equal-width (sized to the widest one), so short content like `100%` sat centered with a large dead margin on each side, plus a hardcoded 22-unit spacing — the separator only accounted for ~7 units. Slots are now measured per content and sit flush, with a single font-relative gap (fontSize × 0.7). The gap at the battery slot drops from ~30 to 5.6 (-81%), and the whole row narrows by 38%-45%.
- 分隔符自带两侧空格，把它推离数字（现由槽位空隙统一提供，分隔符本身只留 `/`、`|`、`·`）
  Separators carried their own surrounding spaces, pushing them away from the digits (spacing is now provided solely by the slot gap; the separator itself is just `/`, `|` or `·`).
- 分隔符按槽位索引分配，导致时钟 1 隐藏时排头的可见时钟没有分隔符、后面的却有（改为按第一个可见槽位分配）
  Separators were assigned by slot index, so with clock 1 hidden the leading visible clock had none while the rest did (now assigned relative to the first visible slot).

## v1.3.1 (2026-09-12)

### Fixed / 修复

- 愚人节补丁装得上但摘不掉：`PatchAll` 未指定 Harmony 实例，补丁被归属到自动生成的 ID，`UnpatchSelf()` 摘不掉自己的补丁（改为 `_harmony.PatchAll(...)`）
  April-fools patch could not be removed: the unqualified `PatchAll` registered the patches under an auto-generated Harmony ID, so `UnpatchSelf()` did not own them (now `_harmony.PatchAll(...)`).
- 打开设置页会静默改写时区配置：刷新下拉时用 `Value = x` 触发了 on-change，把「空 = 跟随电脑时区」固化成写死的时区 ID，用户此后改系统时区不再跟随（改为只刷新显示、不写回配置）
  Opening the settings page silently rewrote the time-zone config: refreshing the dropdown via `Value = x` fired on-change and turned "empty = follow the PC" into a pinned time-zone ID, so later system time-zone changes were ignored (now only the display is refreshed).
- FloatingScreen 创建失败时设置页入口一起失灵：`TickSettings()` 位于 `null` 早退之后，永远不执行（已移到早退之前）
  A failed FloatingScreen also broke the settings entry: `TickSettings()` sat after the null early-return and never ran (now runs before it).
- 容器重建后设置菜单重复注册：旧实例上的条目未摘除（现在先 `RemoveSettingsMenu` 再注册）
  Duplicate settings-menu entries after a container rebuild: the stale entry was never removed (now `RemoveSettingsMenu` runs first).
- adb 路径一旦退化为 PATH 查找就被永久缓存，之后放进游戏根目录的 `adb.exe` 在本次会话里永远不会被发现
  Once the adb path fell back to a PATH lookup it was cached forever, so an `adb.exe` dropped into the game root was never picked up during that session.
- 设备长期离线时每 30 秒照旧跑一次 adb（失败无退避）：重试间隔改为递增 30 → 60 → 120 秒，上限 180 秒
  A permanently offline device still spawned adb every 30 s: the retry interval now backs off 30 → 60 → 120 s, capped at 180 s.
- FPS 槽位每 0.25 秒重新枚举一次 XR 子系统取刷新率上限（改为首次读到后缓存）
  The FPS slot re-enumerated XR subsystems every 0.25 s for the refresh-rate cap (now cached after the first successful read).
- 设置页标题硬编码中文，切英文后仍是中文（改为跟随界面语言）
  The settings page title was hardcoded in Chinese and stayed Chinese in English mode (now follows the UI language).
- 清理死代码：`FormatBattery` 未使用的 `charging` 参数、只写不读的 `_charging` 字段
  Dead code removed: the unused `charging` parameter of `FormatBattery` and the write-only `_charging` field.

## v1.3.0 (2026-09-06)

### Added / 新增

- **四槽位横向排列**：时钟改为四个横向槽位（时钟 1 / 时钟 2 / 时钟 3 / 电量），每帧按内容宽度估算槽位宽度并整体居中
  Four horizontal slots (Clock 1 / Clock 2 / Clock 3 / Battery); each slot's width is measured from its content every tick and the row stays centered.
- 时钟 1/2/3 内容可各自配置：隐藏、本次启动总时长、本次游玩时长、帧率、当前时间、UTC 时间、以及「不含秒」的两种时长
  Per-slot content for clocks 1-3: hidden, game uptime, play session, FPS, current time, UTC time, plus no-seconds variants of both durations.
- 槽位 4 固定显示头显电量（用户不可修改），检测不到电量时该槽位自动隐藏且不占宽度
  Slot 4 is locked to headset battery; when the battery is unavailable the slot hides and takes no width.
- 每个时钟可单独设置文字颜色；新增分隔符选项（宽空格 / 斜杠 / 竖线 / 圆点）
  Per-clock text color; new separator option (wide space / slash / pipe / dot).

### Changed / 变更

- 「游戏中显示」开关同时控制电量槽位（原先有独立的电量开关）
  The "Show During Song" toggle now also governs the battery slot (the separate battery toggle is gone).
- 配置项重组：旧的 `ClockType` / `ClockTwoEnabled` / `ClockTwoType` / `ShowBattery` / `InReplay` 由新的槽位配置取代
  Config reorganization: the old `ClockType` / `ClockTwoEnabled` / `ClockTwoType` / `ShowBattery` / `InReplay` keys are superseded by the new per-slot settings.
- 时长格式统一为 `d:hh:mm(:ss)`（天仅在非零时出现，小时可超过 24）
  Durations now use `d:hh:mm(:ss)` (days appear only when non-zero; hours no longer wrap at 24).

### Fixed / 修复

- 暂停（timeScale=0）时菜单长期显示红色 `FPS 0`：采样不可信时回退到刷新率上限
  Red `FPS 0` while paused (timeScale=0): implausible samples now fall back to the display refresh rate.
- 连续运行超过 24 小时时「本次游玩时长」被截断到 24 小时内
  Play-session time wrapped at 24 hours during long sessions.
- 没有在线 ADB 设备时仍盲目执行 adb（无 `-s` 参数会白等 8 秒超时），且误报为「ADB 不可用」而非「未检测到 ADB 设备」
  ADB was still invoked with no online device (8 s timeout with no `-s` target) and reported "ADB unavailable" instead of "No ADB device".
- 设置页第二个入口打开时标签不重新本地化（两个入口共享宿主，语言刷新只跑过一次）
  Labels were not re-localized when the settings page was opened from the second entry point (shared host only localized once).
- `Input.mouseScrollDelta` 在部分 Unity 输入配置下抛异常导致设置页无法滚动
  `Input.mouseScrollDelta` threw under some Unity input configurations, breaking settings scrolling.
- 清理 adb 进程时 `Process.StartTime` 在 try 之外访问，进程已退出时会抛异常并中断清理
  `Process.StartTime` was read outside the try block during adb cleanup and could abort the whole cleanup.
- 电量显示串每次读取都重新拼接（4Hz），改为查询结果变化时才重建
  The battery display string was rebuilt on every 4 Hz read; it is now cached until the result changes.
- 移除不可达代码与失效配置项（主时钟 FPS 死分支、无效的「回放中显示」开关）
  Removed unreachable code and dead config (the FPS dead branch, the no-op "Show During Replay" toggle).

## v1.2.0 (2026-09-06)

### Fixed / 修复

- 游戏退出后 adb 进程残留，Steam 判定游戏仍在运行：退出时先 `adb kill-server`，再按启动时间兜底结束本次会话拉起的 adb 进程
  Leftover adb processes after exit made Steam think the game was still running: `adb kill-server` on exit plus a start-time-filtered fallback kill.

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

## v1.0.0

- 初始版本：时钟、时区、彩虹效果、头显电量、中英双语界面
  Initial release: clock, time zone, rainbow effect, headset battery, bilingual UI.
