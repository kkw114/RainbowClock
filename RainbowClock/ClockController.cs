using System;
using System.Collections;
using System.Collections.Generic;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.FloatingScreen;
using TMPro;
using UnityEngine;
using Zenject;

namespace RainbowClock
{
    /// <summary>
    /// 主时钟控制器：FloatingScreen + 四个横向槽位（时钟 1 / 时钟 2 / 时钟 3 / 电量）。
    /// 槽位 1~3 内容可配置，槽位 4 固定显示头显电量（取不到时自动隐藏该槽位）。
    /// 每个场景（菜单/玩家/教程）由 Zenject 各创建一个实例；
    /// 进程启动时间等状态为静态，跨场景保留。
    /// </summary>
    public class ClockController : IInitializable, IDisposable
    {
        private static readonly Vector3 MenuPosTop = new Vector3(0f, 2.75f, 4f);
        private static readonly Vector3 MenuRotTop = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 SongPosTop = new Vector3(0f, 2.75f, 4.5f);
        private static readonly Vector3 SongRotTop = new Vector3(-10f, 0f, 0f);
        private static readonly Vector3 LobbyPosTop = new Vector3(0f, 1.75f, 2.5f);
        private static readonly Vector3 LobbyRotTop = new Vector3(0f, 0f, 0f);

        // ===== 布局常量（屏幕本地单位，与字号同量纲）=====
        /// <summary>屏幕左右各留白。</summary>
        private const float ScreenPadX = 8f;
        /// <summary>槽位内文字两侧各留的余量。取 0：圆点两侧的间隙完全由分隔符自带的空格提供，保证左右对称。</summary>
        private const float SlotPadX = 0f;
        /// <summary>
        /// 槽位之间额外的空隙 = 字号 × 该系数。
        /// 固定为 0：这部分空隙只会落在圆点左侧，加大会破坏左右对称。
        /// </summary>
        private const float SlotGapFactor = 0f;
        /// <summary>屏幕高度 = 字号 × 该系数。</summary>
        private const float HeightFactor = 2.4f;
        /// <summary>
        /// 槽位之间的分隔符：圆点，**两侧各带一个空格**。
        /// 空格宽度由字体给出，天然左右等宽，所以圆点两边的间隙看起来一样（各约一个空格）。
        /// </summary>
        public const string SlotSeparator = " · ";
        /// <summary>
        /// 局内"置底"时施加的偏移（仅局内且开关开启时临时生效，不改写用户配置）。
        /// 上下 = Y，前后 = Z。这两个数值取自用户调好的局内落点，
        /// 好处是之后在外面随便改"位置 Y/Z"，都不会影响局内那套摆位。
        /// </summary>
        private const float InGameBottomY = -3.4f;
        private const float InGameBottomZ = 2.4f;
        /// <summary>局内缩放倍率的下限（防止配置成 0 或负数把时钟缩没）。</summary>
        private const float MinScale = 0.05f;

        // ===== 跨场景状态 =====
        /// <summary>本次游戏启动时刻（进程启动 → 现在 = 总时长）。</summary>
        private static readonly DateTime GameStartUtc = GetGameStartUtc();
        /// <summary>累计真实游玩时长（秒）：只在歌曲/关卡内累加，不含菜单挂机。</summary>
        private static double _playSeconds;
        /// <summary>上一次累计用的时刻（Time.realtimeSinceStartup）与当时的在歌状态。</summary>
        private static float _lastPlaySampleTime;
        private static bool _lastSampleInSong;
        private static bool _messageActive;
        private static string _message = "";
        private static int _messageCountdown;

        private FloatingScreen _screen;
        private readonly List<ClockSegment> _segments = new List<ClockSegment>();
        /// <summary>本帧各槽位生效的内容类型（局外/局内两套配置），供着色与布局读取。</summary>
        private readonly int[] _slotContents = new int[ClockConfig.SlotCount];
        private Coroutine _coroutine;
        private string _lastSceneName = "";
        private AudioTimeSyncController _audioSyncCache;
        private PlayerDataModel _playerDataCache;
        private LobbySetupViewController _lobbyCache;
        private string _lastRenderedText = "";
        /// <summary>下一帧强制重写 TMP 文本。</summary>
        private bool _forceRender;

        /// <summary>一个横向槽位：文字组件 + 已着色的内容 + 已着色的分隔符前缀。</summary>
        private class ClockSegment
        {
            public TextMeshProUGUI Text;
            public string Display;
            /// <summary>已着色的分隔符前缀（空 = 该槽位是排头，不带分隔符）。</summary>
            public string Separator;
        }

        public void Initialize()
        {
            MakeClock();
            // 电量查询由 AdbBattery.Tick 在进程启动 10 秒后自动发起，此处不再立即查询
            _coroutine = CoroutineRunner.Instance.StartCoroutine(UpdateLoop());
        }

        public void Dispose()
        {
            if (_coroutine != null)
            {
                CoroutineRunner.StopRoutine(_coroutine);
                _coroutine = null;
            }
            if (_screen != null)
            {
                UnityEngine.Object.Destroy(_screen.gameObject);
                _screen = null;
                _segments.Clear();
            }
        }

        // ==================== 创建 ====================

        private void MakeClock()
        {
            try
            {
                _screen = FloatingScreen.CreateFloatingScreen(
                    new Vector2(80f, 18f),
                    false,
                    Vector3.zero,
                    Quaternion.identity,
                    0f,
                    false);

                if (_screen == null)
                {
                    LogError("Failed to create floating screen.");
                    return;
                }

                var root = _screen.gameObject.GetComponent<RectTransform>();
                for (int i = 0; i < ClockConfig.SlotCount; i++)
                {
                    // 每个槽位一个独立文本，横向手动排布（不用 HorizontalLayoutGroup：
                    // 文字宽度随内容变化，手动定位可以保证每个槽位内始终居中）
                    TextMeshProUGUI text = BeatSaberUI.CreateText(root, "", Vector2.zero);
                    if (text == null)
                    {
                        LogError($"Failed to create slot {i + 1} text.");
                        continue;
                    }
                    text.alignment = TextAlignmentOptions.Center;
                    text.enableWordWrapping = false;
                    text.overflowMode = TextOverflowModes.Overflow;
                    text.richText = true;
                    text.color = Color.white;
                    _segments.Add(new ClockSegment { Text = text, Display = "" });
                }
            }
            catch (Exception e)
            {
                LogError($"Exception while creating clock: {e}");
            }
        }

        private static void LogError(string msg)
        {
            Plugin.Log?.Error("[RainbowClock] " + msg);
            Debug.LogError("[RainbowClock] " + msg);
        }

        // ==================== 主循环 ====================

        private IEnumerator UpdateLoop()
        {
            var wait = new WaitForSeconds(0.25f);
            while (true)
            {
                Tick();
                yield return wait;
            }
            // ReSharper disable once IteratorNeverReturns
        }

        private void Tick()
        {
            // 自动刷新电量
            AdbBattery.Tick();

            // 场景内对象缓存：场景变化才重新查找（FindObjectOfType 开销大，4Hz 下必须缓存）
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName != _lastSceneName)
            {
                _lastSceneName = sceneName;
                _audioSyncCache = null;
                _playerDataCache = null;
                _lobbyCache = null;
            }
            if (_audioSyncCache == null)
            {
                _audioSyncCache = UnityEngine.Object.FindObjectOfType<AudioTimeSyncController>();
            }
            if (_playerDataCache == null)
            {
                _playerDataCache = UnityEngine.Object.FindObjectOfType<PlayerDataModel>();
            }
            if (_lobbyCache == null)
            {
                _lobbyCache = UnityEngine.Object.FindObjectOfType<LobbySetupViewController>();
            }

            bool inSong = _audioSyncCache != null;
            bool noTextAndHud = _playerDataCache?.playerData?.playerSpecificSettings?.noTextsAndHuds ?? false;
            bool inLobby = _lobbyCache != null;

            // 累计真实游玩时长（必须在任何提前 return 之前调用，否则暂停显示时会漏计）
            AccumulatePlayTime(inSong);

            // 选取本帧生效的内容配置：局内（歌曲/关卡）用局内那套，其余用局外那套。
            // 同样要在提前 return 之前算好，避免"游戏中显示"关闭时配置不刷新。
            for (int i = 0; i < _slotContents.Length; i++)
            {
                _slotContents[i] = Plugin.Config.GetContent(i, inSong);
            }

            // 新年祝福
            DateTime nowTime = DateTime.Now;
            if (!_messageActive && nowTime.Month == 1 && nowTime.Day == 1
                && nowTime.Hour == 0 && nowTime.Minute == 0 && nowTime.Second <= 10)
            {
                ShowMessage(Loc.T("new_year"), 10);
            }

            // 是否显示
            bool show = !(inSong && (!Plugin.Config.InSong || noTextAndHud));

            // 设置页注册/刷新先跑：即使 FloatingScreen 创建失败（或未成功取到文本组件），
            // 也不能让设置入口一起失灵
            Plugin.TickSettings();

            if (_screen == null || _segments.Count == 0)
            {
                return;
            }

            _screen.gameObject.SetActive(show);
            if (!show)
            {
                return;
            }

            // 位置/旋转（默认位置 + 用户自定义偏移）
            Vector3 pos, rot;
            if (inSong)
            {
                pos = SongPosTop;
                rot = SongRotTop;
            }
            else if (inLobby)
            {
                pos = LobbyPosTop;
                rot = LobbyRotTop;
            }
            else
            {
                pos = MenuPosTop;
                rot = MenuRotTop;
            }
            // 位置偏移：局内可临时改为"置底"那套数值（上下 Y + 前后 Z，不改动用户配置，只在本帧生效）
            float offsetY = Plugin.Config.ClockY;
            float offsetZ = Plugin.Config.ClockZ;
            if (inSong && Plugin.Config.InGameBottomAlign)
            {
                offsetY = InGameBottomY;
                offsetZ = InGameBottomZ;
            }
            pos += new Vector3(Plugin.Config.ClockX, offsetY, offsetZ);
            _screen.transform.position = pos;
            _screen.transform.eulerAngles = rot;

            // 各槽位内容。分隔符固定为圆点，按"第一个可见槽位之前不加、其后各槽位都加"分配：
            // 按索引分配的话，时钟 1 隐藏时排头的可见槽位没有分隔符、后面的都有，左右不对称。
            bool messageShown = _messageActive && _messageCountdown > 0;
            string[] slotDisplays;
            if (messageShown)
            {
                _messageCountdown--;
                if (_messageCountdown <= 0)
                {
                    _messageActive = false;
                }
                slotDisplays = new[] { _message, "", "", "" };
            }
            else
            {
                _messageActive = false;
                slotDisplays = BuildSlotDisplays();
            }

            AssignSeparators(slotDisplays);
            SetSlotDisplays(slotDisplays, !messageShown);

            // 按内容动态调整容器尺寸与槽位位置（局内会套用局内缩放倍率）
            ApplyLayout(inSong);

            // 比较"最终要写入的富文本"：彩虹开启时每次生成的标签都不同，必须每帧写入才能看到颜色滚动
            string rendered = Compose();
            if (_forceRender || _lastRenderedText != rendered)
            {
                _forceRender = false;
                _lastRenderedText = rendered;
                for (int i = 0; i < _segments.Count; i++)
                {
                    _segments[i].Text.text = (_segments[i].Separator ?? "") + (_segments[i].Display ?? "");
                }
            }
        }

        /// <summary>
        /// 按"第一个可见槽位之前不加分隔符、其后各槽位都加"分配分隔符。
        /// 依赖 slotDisplays 判定可见性，所以必须在内容确定后调用。
        /// 只标记"是否需要分隔符"，实际着色文本由 <see cref="SetSlotDisplays"/> 生成
        /// （圆点要跟随时钟颜色 / 彩虹，必须和内容一起着色）。
        /// </summary>
        private void AssignSeparators(string[] slotDisplays)
        {
            bool seenVisible = false;
            for (int i = 0; i < _segments.Count; i++)
            {
                bool visible = i < slotDisplays.Length
                    && !string.IsNullOrEmpty(slotDisplays[i]);
                if (visible)
                {
                    _segments[i].Separator = seenVisible ? SlotSeparator : "";
                    seenVisible = true;
                }
                else
                {
                    _segments[i].Separator = "";
                }
            }
        }

        /// <summary>写入各槽位文本；<paramref name="colorize"/> = false 时用于临时消息（不套颜色标签）。</summary>
        private void SetSlotDisplays(string[] displays, bool colorize)
        {
            for (int slot = 0; slot < _segments.Count; slot++)
            {
                string display = slot < displays.Length ? displays[slot] : "";
                if (colorize && display.Length > 0)
                {
                    int content = _slotContents[slot];
                    // 分隔符与内容分开着色后拼在一起：圆点跟着该槽位的颜色规则走
                    string separator = _segments[slot].Separator.Length > 0
                        ? ColorizeSeparator(slot, content)
                        : "";
                    _segments[slot].Separator = separator;
                    display = Colorize(slot, content, display);
                }
                _segments[slot].Display = display;
            }
        }

        /// <summary>拼接最终写入的富文本，用于判断是否需要更新 TMP。</summary>
        private string Compose()
        {
            string result = "";
            for (int i = 0; i < _segments.Count; i++)
            {
                string display = _segments[i].Display ?? "";
                if (display.Length == 0)
                {
                    continue;
                }
                if (result.Length > 0)
                {
                    result += "\u0001";
                }
                result += (_segments[i].Separator ?? "") + display;
            }
            return result;
        }

        /// <summary>
        /// 计算四个槽位要显示的数据。槽位 4 固定电量：取不到（无 adb / 无设备 / 解析失败）时返回空串，槽位自动隐藏。
        /// </summary>
        private string[] BuildSlotDisplays()
        {
            var displays = new string[ClockConfig.SlotCount];

            for (int slot = 0; slot < 3; slot++)
            {
                displays[slot] = FormatContent((ClockContent)_slotContents[slot]);
            }

            // 电量槽：固定头显电量，用户不可配置
            displays[ClockConfig.BatterySlot] = AdbBattery.Available ? AdbBattery.CurrentString : "";

            return displays;
        }

        /// <summary>按内容类型格式化槽位数据。</summary>
        private string FormatContent(ClockContent content)
        {
            switch (content)
            {
                case ClockContent.CurrentTime:
                    // 当前时间是唯一由"显示秒"开关控制的时钟
                    return GetTimeString();
                case ClockContent.GameTotal:
                    return GetStopwatchString(GameTotalSeconds);
                case ClockContent.PlaySession:
                    return GetStopwatchString(PlaySessionSeconds);
                case ClockContent.SongRemaining:
                    return GetStopwatchString(GetSongRemainingSeconds());
                case ClockContent.SongProgress:
                    return GetSongProgressString();
                case ClockContent.Fps:
                    return GetFpsDisplayText();
                default:
                    return "";
            }
        }

        // ==================== 歌曲实时时长 ====================

        /// <summary>
        /// 歌曲结束时间（秒）；不在歌里或音频未就绪时为 0。
        /// 优先用 songEndTime（歌曲实际结束点），它无效时回退 songLength（音频总长）。
        /// </summary>
        private float GetSongEndSeconds()
        {
            AudioTimeSyncController sync = _audioSyncCache;
            if (sync == null || !sync.isAudioLoaded)
            {
                return 0f;
            }
            float end = sync.songEndTime;
            if (end <= 0f)
            {
                end = sync.songLength;
            }
            return end > 0f ? end : 0f;
        }

        /// <summary>歌曲剩余时长（秒）；读不到时返回 0。</summary>
        private double GetSongRemainingSeconds()
        {
            float end = GetSongEndSeconds();
            if (end <= 0f)
            {
                return 0d;
            }
            AudioTimeSyncController sync = _audioSyncCache;
            float time = sync != null ? sync.songTime : 0f;
            float remaining = end - time;
            return remaining > 0f ? remaining : 0f;
        }

        /// <summary>歌曲当前百分比（如 "45%"）；读不到结束时间时返回空串（槽位自动隐藏）。</summary>
        private string GetSongProgressString()
        {
            float end = GetSongEndSeconds();
            if (end <= 0f)
            {
                return "";
            }
            AudioTimeSyncController sync = _audioSyncCache;
            float time = sync != null ? sync.songTime : 0f;
            float progress = Mathf.Clamp01(time / end);
            return Mathf.RoundToInt(progress * 100f) + "%";
        }

        /// <summary>
        /// 槽位着色：
        /// 电量槽固定走自带渐变电池色（不参与彩虹，也不套槽位色）；
        /// FPS 内容自带梯度着色（不受彩虹/自定义色影响，只有「FPS」字样用槽位色）；
        /// 其余内容彩虹开启时逐字符彩虹，关闭时使用该槽位的自定义颜色。
        /// </summary>
        private string Colorize(int slot, int contentValue, string display)
        {
            // 电量串由 AdbBattery.FormatBattery 生成，已自带 <color=#RRGGBB> 渐变着色。
            // 这里必须原样透传：若再走 RainbowText.Apply，标签会被当成可见字符显示出来
            // （界面上出现字面的 <color=#A3CD20>78%</color>）；若套槽位色又会覆盖渐进色。
            if (slot == ClockConfig.BatterySlot)
            {
                return display;
            }
            if (contentValue == (int)ClockContent.Fps)
            {
                return display;
            }
            if (Plugin.Config.RainbowClock)
            {
                return RainbowText.Apply(display);
            }
            return "<color=#" + ClockConfig.ParseHex(Plugin.Config.GetSlotColorHex(slot)) + ">" + display + "</color>";
        }

        /// <summary>
        /// 分隔符（圆点）着色：与所在槽位保持一致的规则 ——
        /// 彩虹开启时圆点也走彩虹逐字符变色；否则用该槽位的颜色
        /// （FPS 槽位没有"槽位色"，用 FPS 颜色，因为它旁边的数字也是那张色板）。
        /// </summary>
        private string ColorizeSeparator(int slot, int contentValue)
        {
            if (Plugin.Config.RainbowClock)
            {
                return RainbowText.Apply(SlotSeparator);
            }
            string hex = contentValue == (int)ClockContent.Fps
                ? Plugin.Config.GetFpsColorHex()
                : ClockConfig.ParseHex(Plugin.Config.GetSlotColorHex(slot));
            return "<color=#" + hex + ">" + SlotSeparator + "</color>";
        }

        /// <summary>FPS 显示：数字按上限梯度着色，「FPS」字样用 FPS 颜色（彩虹开启时逐字符彩虹）。</summary>
        private string GetFpsDisplayText()
        {
            int fps = FpsTracker.DisplayFps;
            int target = FpsTracker.GetTargetFps();
            string digitColor = GetFpsGradientColor(fps, target);

            string prefix = "FPS";
            if (Plugin.Config.RainbowClock)
            {
                prefix = RainbowText.Apply(prefix);
            }
            else
            {
                prefix = "<color=#" + Plugin.Config.GetFpsColorHex() + ">" + prefix + "</color>";
            }

            return prefix + " <color=#" + digitColor + ">" + fps + "</color>";
        }

        /// <summary>
        /// FPS 数字梯度色：≥上限绿色；低于上限一定帧数红色；中间黄色。
        /// 规则：上限≤60 → 低于 5 帧红；60&lt;上限≤90 → 低于 10 帧红；
        /// 90&lt;上限≤120 → 低于 20 帧红；上限&gt;120 → 低于 30 帧红。
        /// </summary>
        private static string GetFpsGradientColor(int fps, int target)
        {
            int redThreshold;
            if (target <= 0)
            {
                redThreshold = 100;
            }
            else if (target <= 60)
            {
                redThreshold = target - 5;
            }
            else if (target <= 90)
            {
                redThreshold = target - 10;
            }
            else if (target <= 120)
            {
                redThreshold = target - 20;
            }
            else
            {
                redThreshold = target - 30;
            }

            if (target > 0 && fps >= target)
            {
                return "00FF00";
            }
            if (fps >= redThreshold)
            {
                return "FFFF00";
            }
            return "FF0000";
        }

        // ==================== 时间与时长 ====================

        private string GetTimeString()
        {
            // 始终使用电脑（Windows）的本地时间与时区，不再提供时区选择：
            // 原先"可配置时区"实际上会被设置页的下拉写回配置而钉死，
            // 反而不如直接跟随系统（改系统时区立即生效）。
            return FormatTime(DateTime.Now, Plugin.Config.TwelveToggle, Plugin.Config.SecToggle);
        }

        /// <summary>按 12/24 与显秒配置格式化时间。</summary>
        internal static string FormatTime(DateTime time, bool twelve, bool sec)
        {
            if (twelve)
            {
                return time.ToString(sec ? "h:mm:ss tt" : "h:mm tt");
            }
            return time.ToString(sec ? "HH:mm:ss" : "HH:mm");
        }

        /// <summary>
        /// 累计"真实游玩时长"：只在歌曲/关卡内累加，并排除暂停（timeScale=0）。
        /// 与 <see cref="GameTotalSeconds"/> 的区别：后者是进程启动至今的墙钟时间（含菜单挂机）。
        /// </summary>
        private void AccumulatePlayTime(bool inSong)
        {
            float now = Time.realtimeSinceStartup;
            // 只在"上一次采样也在歌里 且 这一次也在歌里"时累加，
            // 这样进入歌曲的第一帧不会把之前菜单里的时间算进来
            if (_lastSampleInSong && inSong && _lastPlaySampleTime > 0f)
            {
                double delta = now - _lastPlaySampleTime;
                // 正常 Tick 间隔 0.25s；异常大的跳变（加载卡顿/休眠）不计入
                if (delta > 0d && delta < 1.5d)
                {
                    _playSeconds += delta * Mathf.Clamp01(Time.timeScale);
                }
            }
            _lastPlaySampleTime = now;
            _lastSampleInSong = inSong;
        }

        /// <summary>本次游玩时长（秒）：真实打歌时间，仅在歌曲/关卡内累计，不含菜单挂机。</summary>
        public static double PlaySessionSeconds => _playSeconds;

        /// <summary>本次游戏启动总时长（秒）：进程启动至今的墙钟时间。</summary>
        public static double GameTotalSeconds => (DateTime.UtcNow - GameStartUtc).TotalSeconds;

        /// <summary>游戏进程启动时刻（UTC）；读不到时退化为首次访问时刻。</summary>
        private static DateTime GetGameStartUtc()
        {
            try
            {
                return System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
            }
            catch
            {
                return DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 计时类时长格式，随累计时长自适应：
        /// - 不足 1 小时：只显示 分:秒（如 01:59）
        /// - 达到 1 小时：显示 时:分:秒（如 01:01:59）；小时数不封顶（26 小时就是 26:00:00，不按天截断）
        /// 所有计时类时钟都用这个格式，不受"显示秒"开关影响（该开关只作用于当前时间）。
        /// </summary>
        public static string GetStopwatchString(double totalSeconds)
        {
            if (totalSeconds < 0d)
            {
                totalSeconds = 0d;
            }
            long total = (long)totalSeconds;
            long seconds = total % 60;
            long minutes = total / 60 % 60;
            long hours = total / 3600;

            string minuteSecond = minutes.ToString("00") + ":" + seconds.ToString("00");
            if (hours <= 0)
            {
                return minuteSecond;
            }
            return hours.ToString("00") + ":" + minuteSecond;
        }

        // ==================== 布局 ====================

        /// <summary>
        /// 本帧实际使用的字号 = 配置字号 × 局内缩放倍率（仅局内生效）。
        /// 布局的槽宽、屏高、间距全部由字号推导，所以缩放字号等同于整体等比缩放，
        /// 且 TMP 是 SDF 字体，放大也不会糊。
        /// </summary>
        private static float EffectiveFontSize(bool inSong)
        {
            float size = Plugin.Config.FontSize;
            if (inSong)
            {
                size *= Mathf.Max(MinScale, Plugin.Config.InGameScale);
            }
            return Mathf.Max(1f, size);
        }

        /// <summary>按各槽位文字宽度重算屏幕尺寸与槽位位置（宽度变化时才写入）。</summary>
        private void ApplyLayout(bool inSong)
        {
            if (_screen == null || _segments.Count == 0)
            {
                return;
            }

            float fontSize = EffectiveFontSize(inSong);
            float height = Mathf.Max(15f, fontSize * HeightFactor);

            // 统计可见槽位（隐藏的槽位不占宽度）
            var visible = new List<int>();
            for (int i = 0; i < _segments.Count; i++)
            {
                string display = _segments[i].Display ?? "";
                if (display.Length > 0)
                {
                    visible.Add(i);
                }
            }
            if (visible.Count == 0)
            {
                return;
            }

            // 每个槽位各自按内容测量宽度（不再等宽）：
            // 等宽会把所有槽位撑到最宽槽位的尺寸，短内容（如 "100%"）居中后
            // 两侧各留一大块空白，看起来就是时钟之间"空隙过大"，而且换分隔符也消不掉。
            var widths = new float[_segments.Count];
            foreach (int index in visible)
            {
                var text = _segments[index].Text;
                if (text == null)
                {
                    continue;
                }
                text.fontSize = fontSize;
                string full = (_segments[index].Separator ?? "") + (_segments[index].Display ?? "");
                // 注意：必须按去掉富文本标签后的可见文本测量。
                // TMP 的 GetPreferredValues 会把 <color=#RRGGBB> 这类标签当成可见字符计入宽度，
                // 彩虹开启时每个字符多出约 20 个字符的宽度，槽位会被撑大十几倍。
                Vector2 preferred = text.GetPreferredValues(StripRichText(full), 0f, 0f);
                widths[index] = Mathf.Max(2f, preferred.x) + SlotPadX * 2f;
            }

            // 槽位之间只留一个紧凑空隙（随字号缩放）。
            // 符号分隔符已作为槽位内容的一部分（画在数字左侧），不再额外加宽：
            // 分隔符自身就把相邻两个数字分开了，再加一笔只会把空隙拉大。
            float gap = fontSize * SlotGapFactor;

            float total = ScreenPadX * 2f + gap * (visible.Count - 1);
            foreach (int index in visible)
            {
                total += widths[index];
            }

            var size = new Vector2(Mathf.Max(30f, total), height);
            if (_screen.ScreenSize != size)
            {
                _screen.ScreenSize = size;
            }

            // 槽位定位：从左留白起依次紧排，每个槽位用自己的宽度
            float cursor = -size.x * 0.5f + ScreenPadX;
            for (int i = 0; i < _segments.Count; i++)
            {
                var text = _segments[i].Text;
                if (text == null)
                {
                    continue;
                }
                bool isVisible = visible.Contains(i);
                var rect = text.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);

                if (isVisible)
                {
                    float w = widths[i];
                    rect.sizeDelta = new Vector2(w, size.y);
                    // 文字在该槽位内居中
                    rect.anchoredPosition = new Vector2(cursor + w * 0.5f, 0f);
                    cursor += w + gap;
                }
                if (text.gameObject.activeSelf != isVisible)
                {
                    text.gameObject.SetActive(isVisible);
                }
            }
        }

        /// <summary>去掉 &lt;...&gt; 富文本标签，得到实际可见文本（用于测量宽度）。</summary>
        private static string StripRichText(string input)
        {
            if (string.IsNullOrEmpty(input) || input.IndexOf('<') < 0)
            {
                return input ?? "";
            }
            var sb = new System.Text.StringBuilder(input.Length);
            int i = 0;
            while (i < input.Length)
            {
                if (input[i] == '<')
                {
                    int end = input.IndexOf('>', i);
                    if (end < 0)
                    {
                        break;
                    }
                    i = end + 1;
                    continue;
                }
                sb.Append(input[i]);
                i++;
            }
            return sb.ToString();
        }

        // ==================== 供设置页/愚人节调用 ====================

        /// <summary>在时钟位置临时显示一条消息（占满槽位 1，其余槽位隐藏）。</summary>
        public static void ShowMessage(string message, int durationSeconds)
        {
            _message = message;
            _messageCountdown = Mathf.Max(1, durationSeconds * 4); // 0.25s 一帧
            _messageActive = true;
        }
    }
}
