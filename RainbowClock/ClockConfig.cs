using System;
using UnityEngine;

namespace RainbowClock
{
    /// <summary>时钟槽位（横向从左到右）。槽位 4 固定为头显电量，不可配置。</summary>
    public enum ClockSlot
    {
        Clock1 = 0,
        Clock2 = 1,
        Clock3 = 2,
        /// <summary>固定电量槽位，内容由 AdbBattery 决定。</summary>
        Battery = 3
    }

    /// <summary>槽位可显示的内容。0 = 隐藏。</summary>
    public enum ClockContent
    {
        Hidden = 0,
        /// <summary>本次游戏启动总时长（进程启动 → 现在，含菜单挂机）</summary>
        GameTotal = 1,
        /// <summary>本次游玩时长（真实打歌时间，仅在歌曲/关卡内累计）</summary>
        PlaySession = 2,
        Fps = 3,
        CurrentTime = 4,
        // 5 = 原「UTC 时间」，已移除。刻意不复用该值：旧配置里的 5 会落到 default 分支而隐藏槽位，
        // 若拿它给新选项，旧配置的含义会悄悄漂移。
        /// <summary>歌曲剩余时长（仅局内可用）</summary>
        SongRemaining = 6,
        /// <summary>歌曲当前百分比（仅局内可用）</summary>
        SongProgress = 7
    }

    /// <summary>
    /// 全部可配置项。由 IPA.Config.Stores 自动生成保存逻辑。
    /// </summary>
    [Serializable]
    public class ClockConfig
    {
        public const int SlotCount = 4;

        /// <summary>固定用于头显电量的槽位索引（槽位 4）：内容不可配置，配色固定走电量渐变。</summary>
        public const int BatterySlot = 3;

        // ==================== 各槽位内容 ====================
        // 注意：槽位 4 固定为头显电量，没有对应配置项。
        // 局外（菜单/大厅）与局内（歌曲/关卡中）各有一套内容配置，颜色等其它设置共用。

        /// <summary>局外 · 槽位 1 内容，默认当前时间。</summary>
        public virtual int Clock1Content { get; set; } = (int)ClockContent.CurrentTime;
        /// <summary>局外 · 槽位 2 内容，默认隐藏。</summary>
        public virtual int Clock2Content { get; set; } = (int)ClockContent.Hidden;
        /// <summary>局外 · 槽位 3 内容，默认隐藏。</summary>
        public virtual int Clock3Content { get; set; } = (int)ClockContent.Hidden;

        /// <summary>局内 · 槽位 1 内容（默认与局外一致）。</summary>
        public virtual int InGameClock1Content { get; set; } = (int)ClockContent.CurrentTime;
        /// <summary>局内 · 槽位 2 内容（默认与局外一致）。</summary>
        public virtual int InGameClock2Content { get; set; } = (int)ClockContent.Hidden;
        /// <summary>局内 · 槽位 3 内容（默认与局外一致）。</summary>
        public virtual int InGameClock3Content { get; set; } = (int)ClockContent.Hidden;

        /// <summary>槽位之间是否显示内容名称前缀，默认关闭（只显示数据）。</summary>
        public virtual bool ShowLabels { get; set; } = false;

        /// <summary>
        /// 局内置底：仅局内（歌曲/关卡中）生效，临时把"位置 Y（上下）/ Z（前后）"换成内置的置底数值
        /// （不修改用户的配置值），把时钟压到画面下方，避免挡住方块。
        /// </summary>
        public virtual bool InGameBottomAlign { get; set; } = false;

        /// <summary>
        /// 局内时钟缩放倍率：**仅局内生效**，作用在"字号"上（布局、槽宽、屏高全部由字号推导，
        /// 所以等同于整体等比缩放，且 TMP 是 SDF 字体不会糊）。1 = 与局外同尺寸。
        /// </summary>
        public virtual float InGameScale { get; set; } = 1f;

        /// <summary>各槽位文字颜色（仅对非 FPS 内容生效；FPS 数字始终按帧率梯度着色）。</summary>
        public virtual string Clock1Color { get; set; } = "#FFFFFF";
        public virtual string Clock2Color { get; set; } = "#FFFFFF";
        public virtual string Clock3Color { get; set; } = "#FFFFFF";

        /// <summary>FPS 字样的颜色（彩虹关闭且非彩虹内容时生效）。</summary>
        public virtual string FpsColor { get; set; } = "#FFFFFF";

        // ==================== 显示条件 ====================

        /// <summary>游戏中显示时钟</summary>
        public virtual bool InSong { get; set; } = true;

        /// <summary>彩虹效果（逐字符着色）</summary>
        public virtual bool RainbowClock { get; set; } = false;

        // ==================== 格式 ====================

        /// <summary>false=24小时制 true=12小时制</summary>
        public virtual bool TwelveToggle { get; set; } = false;
        public virtual bool SecToggle { get; set; } = false;

        // ==================== 外观 ====================

        public virtual float FontSize { get; set; } = 8f;

        /// <summary>自定义位置偏移（X 左右 / Y 上下 / Z 远近），叠加在默认位置上</summary>
        public virtual float ClockX { get; set; } = 0f;
        public virtual float ClockY { get; set; } = 0f;
        public virtual float ClockZ { get; set; } = 0f;

        // ==================== 语言 ====================

        /// <summary>语言模式：0=自动 1=English 2=中文</summary>
        public virtual int Language { get; set; } = 0;

        // ==================== ADB 电量 ====================

        /// <summary>adb 可执行文件路径，默认 adb（PATH 中查找）</summary>
        public virtual string AdbPath { get; set; } = "adb";

        /// <summary>多设备时指定序列号，留空自动</summary>
        public virtual string AdbSerial { get; set; } = "";

        /// <summary>上次查询成功的设备（自动记忆，多设备时优先使用）</summary>
        public virtual string LastAdbSerial { get; set; } = "";

        /// <summary>电量自动刷新间隔（秒）</summary>
        public virtual int BatteryRefreshSeconds { get; set; } = 30;

        /// <summary>退出游戏时自动结束由本模组拉起的 adb 进程（防止 adb 残留导致 Steam 认为游戏未退出）</summary>
        public virtual bool KillAdbOnExit { get; set; } = true;

        // ==================== 辅助 ====================

        /// <summary>读取槽位内容配置（越界回退为隐藏）。<paramref name="inSong"/> = 是否在歌曲/关卡中。</summary>
        public int GetContent(int slot, bool inSong)
        {
            switch (slot)
            {
                case 0: return inSong ? InGameClock1Content : Clock1Content;
                case 1: return inSong ? InGameClock2Content : Clock2Content;
                case 2: return inSong ? InGameClock3Content : Clock3Content;
                default: return (int)ClockContent.Hidden;
            }
        }

        public void SetContent(int slot, bool inSong, int value)
        {
            switch (slot)
            {
                case 0:
                    if (inSong) { InGameClock1Content = value; } else { Clock1Content = value; }
                    break;
                case 1:
                    if (inSong) { InGameClock2Content = value; } else { Clock2Content = value; }
                    break;
                case 2:
                    if (inSong) { InGameClock3Content = value; } else { Clock3Content = value; }
                    break;
            }
        }

        public string GetSlotColorHex(int slot)
        {
            switch (slot)
            {
                case 0: return Clock1Color;
                case 1: return Clock2Color;
                case 2: return Clock3Color;
                default: return "#FFFFFF";
            }
        }

        public void SetSlotColor(int slot, Color color)
        {
            string hex = "#" + ColorUtility.ToHtmlStringRGB(color);
            switch (slot)
            {
                case 0: Clock1Color = hex; break;
                case 1: Clock2Color = hex; break;
                case 2: Clock3Color = hex; break;
            }
        }

        /// <summary>把配置的十六进制色解析为 #RRGGBB（非法值回退白色）。</summary>
        public static string ParseHex(string value)
        {
            if (!string.IsNullOrEmpty(value)
                && ColorUtility.TryParseHtmlString(value, out Color color))
            {
                return ColorUtility.ToHtmlStringRGB(color);
            }
            return "FFFFFF";
        }

        public Color GetFpsColor()
        {
            if (ColorUtility.TryParseHtmlString(FpsColor, out Color color))
            {
                return color;
            }
            return Color.white;
        }

        public string GetFpsColorHex()
        {
            return ColorUtility.ToHtmlStringRGB(GetFpsColor());
        }

        public void SetFpsColor(Color color)
        {
            FpsColor = "#" + ColorUtility.ToHtmlStringRGB(color);
        }
    }
}
