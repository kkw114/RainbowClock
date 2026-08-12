using System;
using System.Collections;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.FloatingScreen;
using TMPro;
using UnityEngine;
using Zenject;

namespace RainbowClock
{
    /// <summary>
    /// 主时钟控制器：FloatingScreen + TMP 文本 + 0.25s 协程。
    /// 每个场景（菜单/玩家/教程）由 Zenject 各创建一个实例；
    /// 秒表/会话时长/临时消息等状态为静态，跨场景保留。
    /// </summary>
    public class ClockController : IInitializable, IDisposable
    {
        private static readonly Vector3 MenuPosTop = new Vector3(0f, 2.75f, 4f);
        private static readonly Vector3 MenuRotTop = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 SongPosTop = new Vector3(0f, 2.75f, 4.5f);
        private static readonly Vector3 SongRotTop = new Vector3(-10f, 0f, 0f);
        private static readonly Vector3 LobbyPosTop = new Vector3(0f, 1.75f, 2.5f);
        private static readonly Vector3 LobbyRotTop = new Vector3(0f, 0f, 0f);

        // ===== 跨场景状态 =====
        private static DateTime _sessionStart = DateTime.UtcNow;
        private static bool _stateLoaded;
        private static string _message = "";
        private static int _messageCountdown;

        private FloatingScreen _screen;
        private TextMeshProUGUI _clockText;
        private Coroutine _coroutine;
        private string _lastSceneName = "";
        private AudioTimeSyncController _audioSyncCache;
        private PlayerDataModel _playerDataCache;
        private LobbySetupViewController _lobbyCache;
        private string _lastRenderedText = "";

        public void Initialize()
        {
            MakeClock();
            LoadStateOnce();
            AdbBattery.RefreshNow();
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
                _clockText = null;
            }
        }

        private void MakeClock()
        {
            try
            {
                _screen = FloatingScreen.CreateFloatingScreen(
                    new Vector2(30f, 15f),
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

                _clockText = BeatSaberUI.CreateText(
                    _screen.gameObject.GetComponent<RectTransform>(),
                    "",
                    new Vector2(0, 0));

                if (_clockText == null)
                {
                    LogError("Failed to create clock text.");
                    return;
                }

                _clockText.alignment = TextAlignmentOptions.Center;
                _clockText.enableWordWrapping = false;
                _clockText.overflowMode = TextOverflowModes.Overflow;
                _clockText.richText = true;
                _clockText.fontSize = Plugin.Config.FontSize;
                _clockText.color = Plugin.Config.GetColor();
            }
            catch (Exception e)
            {
                LogError($"Exception while creating clock: {e}");
            }
        }

        private void LoadStateOnce()
        {
            if (_stateLoaded)
            {
                return;
            }
            _stateLoaded = true;
            _sessionStart = DateTime.UtcNow;
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
            if (Plugin.Config.ShowBattery)
            {
                AdbBattery.Tick();
            }

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

            // 新年祝福
            DateTime nowTime = DateTime.Now;
            if (_messageCountdown <= 0 && nowTime.Month == 1 && nowTime.Day == 1
                && nowTime.Hour == 0 && nowTime.Minute == 0 && nowTime.Second <= 10)
            {
                ShowMessage(Loc.T("new_year"), 10);
            }

            // 是否显示
            bool show = true;
            if (inSong && (!Plugin.Config.InSong || noTextAndHud))
            {
                show = false;
            }

            if (_screen == null || _clockText == null)
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
            pos += new Vector3(Plugin.Config.ClockX, Plugin.Config.ClockY, Plugin.Config.ClockZ);
            _screen.transform.position = pos;
            _screen.transform.eulerAngles = rot;

            // 文本（主时钟 - 时钟二 - 头显电量）
            string text;
            if (_messageCountdown > 0)
            {
                text = _message;
                _messageCountdown--;
            }
            else
            {
                // 主时钟段（FPS 段内部自着色，其余段按彩虹处理）
                text = BuildMainSegment();

                if (Plugin.Config.ClockTwoEnabled)
                {
                    string two = BuildClockTwoSegment();
                    if (!string.IsNullOrEmpty(two))
                    {
                        text += " - " + two;
                    }
                }

                if (Plugin.Config.ShowBattery && AdbBattery.Available)
                {
                    string batt = AdbBattery.CurrentString;
                    if (!string.IsNullOrEmpty(batt))
                    {
                        text += " - " + batt;
                    }
                }
            }

            // 按内容动态调整容器尺寸，防止显秒/秒表/彩虹文本被截断
            UpdateScreenSize(text);

            // 文本变化时才赋值（避免 TMP 无谓重建）
            _clockText.fontSize = Plugin.Config.FontSize;
            _clockText.color = Plugin.Config.GetColor();
            if (_lastRenderedText != text)
            {
                _lastRenderedText = text;
                _clockText.text = text;
            }

            Plugin.TickSettings();
        }

        /// <summary>根据文本长度估算所需容器大小并调整 FloatingScreen 尺寸。</summary>
        private void UpdateScreenSize(string text)
        {
            int visibleLen = text.Length;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '<')
                {
                    int end = text.IndexOf('>', i);
                    if (end < 0)
                    {
                        break;
                    }
                    visibleLen -= end - i + 1;
                    i = end;
                }
            }

            float fontSize = Mathf.Max(1f, Plugin.Config.FontSize);
            float width = Mathf.Max(30f, visibleLen * fontSize * 0.58f + 12f);
            float height = Mathf.Max(15f, fontSize * 2.2f);

            Vector2 size = new Vector2(width, height);
            if (_screen.ScreenSize != size)
            {
                _screen.ScreenSize = size;
            }
        }

        /// <summary>主时钟段：FPS 模式返回自着色文本，其他类型按彩虹处理。</summary>
        private string BuildMainSegment()
        {
            if (Plugin.Config.ClockType == 5)
            {
                return GetFpsDisplayText();
            }
            string text = BuildText();
            if (Plugin.Config.RainbowClock)
            {
                text = RainbowText.Apply(text);
            }
            return text;
        }

        /// <summary>时钟二段：FPS 模式返回自着色文本，其他类型按彩虹处理。</summary>
        private string BuildClockTwoSegment()
        {
            if (Plugin.Config.ClockTwoType == 5)
            {
                return GetFpsDisplayText();
            }
            string text = BuildClockTwoText();
            if (Plugin.Config.RainbowClock)
            {
                text = RainbowText.Apply(text);
            }
            return text;
        }

        /// <summary>
        /// FPS 显示：数字按上限梯度着色（不受彩虹/自定义色影响）；
        /// "FPS" 字样：彩虹开启时逐字符彩虹，关闭时使用自定义 FPS 颜色。
        /// </summary>
        private static string GetFpsDisplayText()
        {
            int fps = FpsTracker.CurrentFps;
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

            if (fps >= target)
            {
                return "00FF00";
            }
            if (fps >= redThreshold)
            {
                return "FFFF00";
            }
            return "FF0000";
        }

        private string BuildText()
        {
            if (Plugin.Config.ClockType == 1)
            {
                return GetStopwatchString((DateTime.UtcNow - _sessionStart).TotalSeconds);
            }
            if (Plugin.Config.ClockType == 5)
            {
                return "FPS " + FpsTracker.CurrentFps;
            }
            return GetTimeString();
        }

        private string GetTimeString()
        {
            return GetNowInConfigZone().ToString(GetTimeFormat());
        }

        /// <summary>按配置时区取当前本地时间（空=跟随电脑时区）。</summary>
        public static DateTime GetNowInConfigZone()
        {
            try
            {
                string id = Plugin.Config.TimeZoneId;
                if (string.IsNullOrEmpty(id))
                {
                    return TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.Local);
                }
                TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById(id);
                return TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
            }
            catch
            {
                return TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.Local);
            }
        }

        /// <summary>按 12/24 与显秒配置返回格式化串。</summary>
        private string GetTimeFormat()
        {
            bool twelve = Plugin.Config.TwelveToggle;
            bool sec = Plugin.Config.SecToggle;
            if (twelve && sec)
            {
                return "h:mm:ss tt";
            }
            if (twelve)
            {
                return "h:mm tt";
            }
            if (sec)
            {
                return "HH:mm:ss";
            }
            return "HH:mm";
        }

        /// <summary>时钟二内容：0=当前时间 1=本次游玩 4=UTC时间 5=FPS。</summary>
        private string BuildClockTwoText()
        {
            switch (Plugin.Config.ClockTwoType)
            {
                case 0:
                    return GetNowInConfigZone().ToString(GetTimeFormat());
                case 1:
                    return GetStopwatchString((DateTime.UtcNow - _sessionStart).TotalSeconds);
                case 5:
                    return "FPS " + FpsTracker.CurrentFps;
                default:
                    return DateTime.UtcNow.ToString(GetTimeFormat());
            }
        }

        /// <summary>与 Quest 版一致的秒表格式：d:h:m:s，秒可隐藏。</summary>
        public static string GetStopwatchString(double totalSeconds)
        {
            int seconds = (int)totalSeconds % 60;
            int minutes = (int)(totalSeconds / 60) % 60;
            int hours = (int)(totalSeconds / 3600) % 24;
            int days = (int)(totalSeconds / 86400);
            bool showSeconds = Plugin.Config.SecToggle;

            string s = "";
            if (days > 0)
            {
                s += days + ":";
            }
            if (hours > 0 || s.Length > 0 || !showSeconds)
            {
                s += (s.Length > 0 ? hours.ToString("00") : hours.ToString()) + ":";
            }
            s += s.Length > 0 ? minutes.ToString("00") : minutes.ToString();
            if (showSeconds)
            {
                s += ":" + seconds.ToString("00");
            }
            return s;
        }

        // ==================== 供设置页/愚人节调用 ====================

        public static void ShowMessage(string message, int durationSeconds)
        {
            _message = message;
            _messageCountdown = durationSeconds * 4; // 0.25s 一帧
        }

        public static double SessionSeconds => (DateTime.UtcNow - _sessionStart).TotalSeconds;
    }
}
