using System;
using UnityEngine;

namespace RainbowClock
{
    /// <summary>
    /// 全部可配置项。由 IPA.Config.Stores 自动生成保存逻辑。
    /// </summary>
    [Serializable]
    public class ClockConfig
    {
        /// <summary>0=当前时间 1=本次游玩</summary>
        public virtual int ClockType { get; set; } = 0;

        /// <summary>游戏中显示时钟</summary>
        public virtual bool InSong { get; set; } = true;
        /// <summary>回放中显示时钟（未检测到回放模组时无效果）</summary>
        public virtual bool InReplay { get; set; } = true;

        /// <summary>false=24小时制 true=12小时制</summary>
        public virtual bool TwelveToggle { get; set; } = false;
        public virtual bool SecToggle { get; set; } = false;

        /// <summary>彩虹效果</summary>
        public virtual bool RainbowClock { get; set; } = false;

        /// <summary>显示 ADB 头显电量</summary>
        public virtual bool ShowBattery { get; set; } = true;

        /// <summary>时钟二（显示在主时钟与电量之间）</summary>
        public virtual bool ClockTwoEnabled { get; set; } = false;

        /// <summary>时钟二内容：0=当前时间 1=本次游玩 4=UTC时间</summary>
        public virtual int ClockTwoType { get; set; } = 4;

        public virtual float FontSize { get; set; } = 8f;

        /// <summary>自定义位置偏移（X 左右 / Y 上下 / Z 远近），叠加在默认位置上</summary>
        public virtual float ClockX { get; set; } = 0f;
        public virtual float ClockY { get; set; } = 0f;
        public virtual float ClockZ { get; set; } = 0f;

        public virtual string ClockColor { get; set; } = "#FFFFFF";

        /// <summary>语言模式：0=自动 1=English 2=中文</summary>
        public virtual int Language { get; set; } = 0;

        /// <summary>时区 ID（TimeZoneInfo.Id），空=跟随电脑时区</summary>
        public virtual string TimeZoneId { get; set; } = "";

        /// <summary>adb 可执行文件路径，默认 adb（PATH 中查找）</summary>
        public virtual string AdbPath { get; set; } = "adb";

        /// <summary>多设备时指定序列号，留空自动</summary>
        public virtual string AdbSerial { get; set; } = "";

        /// <summary>电量自动刷新间隔（秒）</summary>
        public virtual int BatteryRefreshSeconds { get; set; } = 30;

        public Color GetColor()
        {
            if (ColorUtility.TryParseHtmlString(ClockColor, out Color color))
            {
                return color;
            }
            return Color.white;
        }

        public void SetColor(Color color)
        {
            ClockColor = "#" + ColorUtility.ToHtmlStringRGB(color);
        }
    }
}
