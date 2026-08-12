using System;
using System.Collections.Generic;

namespace RainbowClock
{
    /// <summary>语言模式。0=自动(跟随游戏) 1=English 2=中文</summary>
    public enum LangMode
    {
        Auto = 0,
        English = 1,
        Chinese = 2
    }

    /// <summary>
    /// 中英双语文本。所有界面文案通过 <see cref="T"/> 获取。
    /// </summary>
    public static class Loc
    {
        private static readonly Dictionary<string, string> En = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> Zh = new Dictionary<string, string>();

        public static LangMode Mode { get; private set; } = LangMode.Auto;
        public static bool IsChinese => Mode switch
        {
            LangMode.Chinese => true,
            LangMode.English => false,
            _ => GameIsChinese()
        };
        static Loc()
        {
            // ============ 设置页标签 ============
            En["clock_type"] = "Clock Type";              Zh["clock_type"] = "时钟类型";
            En["time_zone"] = "Time Zone";                Zh["time_zone"] = "时区";
            En["language"] = "Language";                  Zh["language"] = "语言";
            En["show_song"] = "Show During Song";         Zh["show_song"] = "游戏中显示";
            En["show_replay"] = "Show During Replay";     Zh["show_replay"] = "回放中显示";
            En["twelve"] = "12/24 Hour Toggle";           Zh["twelve"] = "12/24 小时制";
            En["seconds"] = "Show Seconds";               Zh["seconds"] = "显示秒";
            En["battery"] = "Show Headset Battery (ADB)"; Zh["battery"] = "显示头显电量";
            En["rainbow"] = "Rainbowify it";              Zh["rainbow"] = "彩虹效果";
            En["font_size"] = "Font Size";                Zh["font_size"] = "字号";
            En["clock_color"] = "Clock Color";            Zh["clock_color"] = "时钟颜色";
            En["fps_color"] = "FPS Color";                Zh["fps_color"] = "FPS 颜色";
            En["clock_two"] = "Clock 2";                  Zh["clock_two"] = "时钟二";
            En["clock_two_type"] = "Clock 2 Content";     Zh["clock_two_type"] = "时钟二内容";
            En["pos_x"] = "Position X (Left/Right)";      Zh["pos_x"] = "位置 X（左/右）";
            En["pos_y"] = "Position Y (Up/Down)";         Zh["pos_y"] = "位置 Y（上/下）";
            En["pos_z"] = "Position Z (Forward/Back)";    Zh["pos_z"] = "位置 Z（前/后）";

            // ============ 时钟类型选项 ============
            En["type_current"] = "Current Time";          Zh["type_current"] = "当前时间";
            En["type_session"] = "Session Time";          Zh["type_session"] = "本次游玩";
            En["type_utc"] = "UTC Time";                  Zh["type_utc"] = "UTC 时间";
            En["type_fps"] = "FPS";                       Zh["type_fps"] = "FPS（帧率）";

            // ============ 语言选项 ============
            En["lang_auto"] = "Auto (Follow Game)";       Zh["lang_auto"] = "自动 (跟随游戏)";
            En["lang_en"] = "English";                    Zh["lang_en"] = "English";
            En["lang_zh"] = "中文";                        Zh["lang_zh"] = "中文";

            // ============ 按钮 ============
            En["btn_refresh_battery"] = "Refresh Battery (ADB)"; Zh["btn_refresh_battery"] = "刷新电量 (ADB)";

            // ============ 其他 ============
            En["new_year"] = "Happy New Year!";           Zh["new_year"] = "新年快乐！";
            En["batt_not_available"] = "ADB unavailable"; Zh["batt_not_available"] = "ADB 不可用";
            En["batt_no_device"] = "No ADB device";       Zh["batt_no_device"] = "未检测到 ADB 设备";
            En["conn_wired"] = "USB";                     Zh["conn_wired"] = "有线";
            En["conn_wireless"] = "WiFi";                 Zh["conn_wireless"] = "无线";
            En["conn_not_connected"] = "Not connected";   Zh["conn_not_connected"] = "未连接";

            // 愚人节失败嘲讽（4 月 1 日）
            En["fail1"] = "Get Better";                   Zh["fail1"] = "菜就多练";
            En["fail2"] = "fail";                         Zh["fail2"] = "失败了";
            En["fail3"] = "lol";                          Zh["fail3"] = "哈哈";
            En["fail4"] = "learn2play";                   Zh["fail4"] = "手残";
            En["fail5"] = "no skills";                    Zh["fail5"] = "没技术";
            En["fail6"] = "get skills";                   Zh["fail6"] = "练练吧";
            En["fail7"] = "loser";                        Zh["fail7"] = "菜鸟";
            En["fail8"] = "hit bloq";                     Zh["fail8"] = "打方块呀";
            En["fail9"] = "no comment";                   Zh["fail9"] = "无语";
            En["fail10"] = "you failed";                  Zh["fail10"] = "你失败了";
            En["fail11"] = "can you even play";           Zh["fail11"] = "你会玩吗";
        }

        /// <summary>设置语言模式并返回是否中文。</summary>
        public static void SetMode(LangMode mode)
        {
            Mode = mode;
        }

        /// <summary>
        /// 取当前语言文本。mode=Auto 时跟随游戏语言（Polyglot），失败回退英文。
        /// </summary>
        public static string T(string key)
        {
            Dictionary<string, string> table = Mode switch
            {
                LangMode.Chinese => Zh,
                LangMode.English => En,
                _ => GameIsChinese() ? Zh : En
            };

            if (table.TryGetValue(key, out string value))
            {
                return value;
            }
            return En.TryGetValue(key, out string fallback) ? fallback : key;
        }

        private static readonly object LangCacheLock = new object();
        private static bool _langCached;
        private static bool _langCachedValue;
        private static long _langCacheUntil;

        /// <summary>游戏语言检测（带 5 秒缓存，避免高频访问 Polyglot）。</summary>
        public static bool GameIsChinese()
        {
            lock (LangCacheLock)
            {
                long now = DateTime.UtcNow.Ticks;
                if (_langCached && now < _langCacheUntil)
                {
                    return _langCachedValue;
                }
                _langCachedValue = QueryGameIsChinese();
                _langCached = true;
                _langCacheUntil = now + 5_000_000_000L;
                return _langCachedValue;
            }
        }

        private static bool QueryGameIsChinese()
        {
            try
            {
                BGLib.Polyglot.Language lang = BGLib.Polyglot.Localization.Instance.SelectedLanguage;
                return lang == BGLib.Polyglot.Language.Simplified_Chinese
                    || lang == BGLib.Polyglot.Language.Traditional_Chinese;
            }
            catch
            {
                return false;
            }
        }

        public static string GetClockTypeName(int type)
        {
            return type switch
            {
                1 => T("type_session"),
                4 => T("type_utc"),
                5 => T("type_fps"),
                _ => T("type_current")
            };
        }

        public static string GetLanguageName(int mode)
        {
            return mode switch
            {
                1 => T("lang_en"),
                2 => T("lang_zh"),
                _ => T("lang_auto")
            };
        }

        public static string[] FailTexts()
        {
            return new[]
            {
                T("fail1"), T("fail2"), T("fail3"), T("fail4"), T("fail5"),
                T("fail6"), T("fail7"), T("fail8"), T("fail9"), T("fail10"), T("fail11")
            };
        }
    }
}
