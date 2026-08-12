using System;
using System.Collections.Generic;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using HMUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

namespace RainbowClock
{
#pragma warning disable 0649 // BSML 通过反射绑定这些字段
    /// <summary>
    /// 设置页 BSML 绑定宿主。所有属性/动作被 Views/ClockSettings.bsml 引用。
    /// 语言切换时原地刷新所有标签文本。
    /// </summary>
    public class ClockSettingsHost
    {
        private readonly List<(TextMeshProUGUI label, string enKey)> _labels = new List<(TextMeshProUGUI, string)>();
        private bool _localized;
        private bool _timeZoneCustomized;
        private VerticalLayoutGroup _lastRowsLayout;
        private BatteryError _lastBatteryErrorShown = BatteryError.None;
        private string _lastButtonSerial = "\0";

        // 多入口页面（Mods 列表 + 主菜单按钮）各自独立初始化滚动
        private RectTransform _lastScrollClip;
        private readonly HashSet<RectTransform> _pendingScrolls = new HashSet<RectTransform>();
        private readonly HashSet<RectTransform> _scrollDone = new HashSet<RectTransform>();

        private ClockConfig Config => Plugin.Config;

        // ==================== BSML 值绑定 ====================

        [UIValue("ClockTypeValue")]
        public int ClockTypeValue
        {
            get => Config.ClockType;
            set => Config.ClockType = value;
        }

        [UIValue("LanguageValue")]
        public int LanguageValue
        {
            get => Config.Language;
            set => Config.Language = value;
        }

        [UIValue("TimeZoneValue")]
        public string TimeZoneValue
        {
            get
            {
                string id = Config.TimeZoneId;
                return string.IsNullOrEmpty(id) ? TimeZoneInfo.Local.Id : id;
            }
            set => Config.TimeZoneId = value;
        }

        [UIValue("InSongValue")]
        public bool InSongValue
        {
            get => Config.InSong;
            set => Config.InSong = value;
        }

        [UIValue("InReplayValue")]
        public bool InReplayValue
        {
            get => Config.InReplay;
            set => Config.InReplay = value;
        }

        [UIValue("TwelveValue")]
        public bool TwelveValue
        {
            get => Config.TwelveToggle;
            set => Config.TwelveToggle = value;
        }

        [UIValue("SecondsValue")]
        public bool SecondsValue
        {
            get => Config.SecToggle;
            set => Config.SecToggle = value;
        }

        [UIValue("BatteryValue")]
        public bool BatteryValue
        {
            get => Config.ShowBattery;
            set => Config.ShowBattery = value;
        }

        [UIValue("RainbowValue")]
        public bool RainbowValue
        {
            get => Config.RainbowClock;
            set => Config.RainbowClock = value;
        }

        [UIValue("ClockTwoValue")]
        public bool ClockTwoValue
        {
            get => Config.ClockTwoEnabled;
            set => Config.ClockTwoEnabled = value;
        }

        [UIValue("ClockTwoTypeValue")]
        public int ClockTwoTypeValue
        {
            get => Config.ClockTwoType;
            set => Config.ClockTwoType = value;
        }

        [UIValue("PosXValue")]
        public float PosXValue
        {
            get => Config.ClockX;
            set => Config.ClockX = value;
        }

        [UIValue("PosYValue")]
        public float PosYValue
        {
            get => Config.ClockY;
            set => Config.ClockY = value;
        }

        [UIValue("PosZValue")]
        public float PosZValue
        {
            get => Config.ClockZ;
            set => Config.ClockZ = value;
        }

        [UIValue("FontSizeValue")]
        public float FontSizeValue
        {
            get => Config.FontSize;
            set => Config.FontSize = value;
        }

        [UIValue("ClockColorValue")]
        public Color ClockColorValue
        {
            get => Config.GetColor();
            set => Config.SetColor(value);
        }

        [UIValue("FpsColorValue")]
        public Color FpsColorValue
        {
            get => Config.GetFpsColor();
            set => Config.SetFpsColor(value);
        }

        // ==================== 下拉选项与格式化 ====================

        [UIValue("ClockTypeOptions")]
        public int[] ClockTypeOptions => new[] { 0, 1, 5 };

        [UIValue("ClockTwoTypeOptions")]
        public int[] ClockTwoTypeOptions => new[] { 4, 0, 1, 5 };

        [UIValue("LanguageOptions")]
        public int[] LanguageOptions => new[] { 0, 1, 2 };

        private static readonly string[] TimeZoneIds = BuildTimeZoneIds();

        [UIValue("TimeZoneOptions")]
        public string[] TimeZoneOptions => TimeZoneIds;

        private static string[] BuildTimeZoneIds()
        {
            try
            {
                var zones = TimeZoneInfo.GetSystemTimeZones();
                var ids = new string[zones.Count];
                for (int i = 0; i < zones.Count; i++)
                {
                    ids[i] = zones[i].Id;
                }
                return ids;
            }
            catch
            {
                return new[] { TimeZoneInfo.Local.Id };
            }
        }

        [UIAction("ClockTypeFormatter")]
        public string ClockTypeFormatter(object value) => Loc.GetClockTypeName(Convert.ToInt32(value));

        [UIAction("LanguageFormatter")]
        public string LanguageFormatter(object value) => Loc.GetLanguageName(Convert.ToInt32(value));

        [UIAction("TimeZoneFormatter")]
        public string TimeZoneFormatter(object value)
        {
            string id = Convert.ToString(value);
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id).DisplayName;
            }
            catch
            {
                return id;
            }
        }

        // ==================== 组件引用（解析后填充） ====================

        [UIComponent("ClockType")]
        internal DropDownListSetting ClockTypeDropdown;

        [UIComponent("Lang")]
        internal DropDownListSetting LanguageDropdown;

        [UIComponent("TimeZone")]
        internal DropDownListSetting TimeZoneDropdown;

        [UIComponent("TogInSong")]
        internal ToggleSetting ShowInSongToggle;

        [UIComponent("TogInReplay")]
        internal ToggleSetting ShowInReplayToggle;

        [UIComponent("TogTwelve")]
        internal ToggleSetting TwelveToggle;

        [UIComponent("TogSeconds")]
        internal ToggleSetting SecondsToggle;

        [UIComponent("TogBattery")]
        internal ToggleSetting BatteryToggle;

        [UIComponent("TogRainbow")]
        internal ToggleSetting RainbowToggle;

        [UIComponent("TogClockTwo")]
        internal ToggleSetting ClockTwoToggle;

        [UIComponent("ClockTwoType")]
        internal DropDownListSetting ClockTwoTypeDropdown;

        [UIComponent("FontSize")]
        internal IncrementSetting FontSizeSetting;

        [UIComponent("PosX")]
        internal IncrementSetting PosXSetting;

        [UIComponent("PosY")]
        internal IncrementSetting PosYSetting;

        [UIComponent("PosZ")]
        internal IncrementSetting PosZSetting;

        [UIComponent("ColorRow")]
        internal ColorSetting ClockColorRow;

        [UIComponent("FpsColorRow")]
        internal ColorSetting FpsColorRow;

        [UIComponent("SettingsRows")]
        internal VerticalLayoutGroup SettingsRowsLayout;

        [UIComponent("ScrollClip")]
        internal RectTransform ScrollClip;

        [UIComponent("BtnRefreshBattery")]
        internal Button RefreshBatteryButton;

        // ==================== 动作 ====================

        [UIAction("OnClockTypeChanged")]
        public void OnClockTypeChanged(int value)
        {
            // 值已通过 ClockTypeValue 写回配置
        }

        [UIAction("OnLangChanged")]
        public void OnLangChanged(int value)
        {
            Loc.SetMode((LangMode)value);
            RefreshLanguage();
            Plugin.UpdateMenuButtonHint();
        }

        [UIAction("RefreshBattery")]
        public void RefreshBattery()
        {
            AdbBattery.RefreshNow();
        }

        // ==================== 由主协程驱动 ====================

        /// <summary>每 0.25s 调用：按页面独立初始化滚动；当前页面做本地化等初始化。</summary>
        public void Tick()
        {
            // 1) 滚动初始化：按 ScrollClip 实例独立处理。
            // 设置页有两个入口（Mods 列表 + 主菜单按钮），各自解析一次并覆盖宿主字段；
            // 未激活页面的布局高度为 0 会持续重试，绝不能因另一个页面初始化成功而中断。
            if (ScrollClip != null)
            {
                if (!ReferenceEquals(ScrollClip, _lastScrollClip))
                {
                    if (_lastScrollClip != null && !_scrollDone.Contains(_lastScrollClip))
                    {
                        _pendingScrolls.Add(_lastScrollClip);
                    }
                    _lastScrollClip = ScrollClip;
                    if (!_scrollDone.Contains(ScrollClip))
                    {
                        _pendingScrolls.Add(ScrollClip);
                    }
                }
            }
            foreach (RectTransform clip in _pendingScrolls.ToList())
            {
                if (TrySetupScroll(clip))
                {
                    _scrollDone.Add(clip);
                    _pendingScrolls.Remove(clip);
                }
            }

            // 2) 当前页面的本地化/时区定制/ADB 状态（字段绑定的是最近解析的页面）
            bool parsed = ClockTypeDropdown != null && SettingsRowsLayout != null;
            if (parsed)
            {
                if (SettingsRowsLayout != _lastRowsLayout)
                {
                    _lastRowsLayout = SettingsRowsLayout;
                    _localized = false;
                    _timeZoneCustomized = false;
                }
                if (!_localized)
                {
                    RefreshLanguage();
                }
                if (!_timeZoneCustomized && TimeZoneDropdown != null)
                {
                    _timeZoneCustomized = true;
                    CustomizeTimeZoneDropdown();
                }
                // ADB 状态或目标设备变化时刷新按钮文字（连接状态/错误提示）
                if (AdbBattery.LastErrorType != _lastBatteryErrorShown
                    || AdbBattery.TargetSerial != _lastButtonSerial)
                {
                    _lastBatteryErrorShown = AdbBattery.LastErrorType;
                    _lastButtonSerial = AdbBattery.TargetSerial;
                    RefreshBatteryButtonText();
                }
            }
        }

        /// <summary>
        /// 独立初始化一个设置页面的滚动：行高回填、内容锚定、RectMask2D、SettingsScroller。
        /// 不依赖宿主字段（多入口页面共享宿主），布局未完成时返回 false 由调用方下轮重试。
        /// </summary>
        private bool TrySetupScroll(RectTransform clip)
        {
            try
            {
                // clip 铺满父级（VC）
                clip.anchorMin = Vector2.zero;
                clip.anchorMax = Vector2.one;
                clip.pivot = new Vector2(0.5f, 0.5f);
                clip.anchoredPosition = Vector2.zero;
                clip.sizeDelta = Vector2.zero;

                if (clip.GetComponent<RectMask2D>() == null)
                {
                    clip.gameObject.AddComponent<RectMask2D>();
                }

                // 防御：禁用可能存在的布局组（<bg> 本身没有，双保险）
                var layoutGroup = clip.GetComponent<VerticalLayoutGroup>();
                if (layoutGroup != null)
                {
                    layoutGroup.enabled = false;
                }
                var hlayoutGroup = clip.GetComponent<HorizontalOrVerticalLayoutGroup>();
                if (hlayoutGroup != null && hlayoutGroup != layoutGroup)
                {
                    hlayoutGroup.enabled = false;
                }

                // 内容容器 = clip 的第一个子物体（SettingsRows）
                var page = clip.GetChild(0) as RectTransform;
                var pageLayout = page != null ? page.GetComponent<VerticalLayoutGroup>() : null;
                if (page == null || pageLayout == null)
                {
                    return false;
                }

                // 行高回填（模板 LayoutElement 高度为 0）
                float total = 0f;
                int count = 0;
                foreach (Transform child in page)
                {
                    var layoutElement = child.GetComponent<LayoutElement>();
                    if (layoutElement == null)
                    {
                        continue;
                    }
                    float h = ((RectTransform)child).rect.height;
                    if (h <= 0.01f)
                    {
                        h = 10f;
                    }
                    else if (h < 9f)
                    {
                        h = 9f;
                    }
                    layoutElement.preferredHeight = h;
                    total += h;
                    count++;
                }
                if (count > 1)
                {
                    total += pageLayout.spacing * (count - 1);
                }

                // 内容锚定到顶部 + 固定高度
                page.anchorMin = new Vector2(0f, 1f);
                page.anchorMax = new Vector2(1f, 1f);
                page.pivot = new Vector2(0.5f, 1f);
                page.anchoredPosition = Vector2.zero;
                page.sizeDelta = new Vector2(0f, total);

                // 布局未完成时下轮重试
                float clipHeight = clip.rect.height;
                if (clipHeight <= 1f)
                {
                    Plugin.Log?.Info($"[RainbowClock] scroll setup retry: clipHeight={clipHeight:F1}");
                    return false;
                }

                var scroller = clip.GetComponent<SettingsScroller>();
                if (scroller == null)
                {
                    scroller = clip.gameObject.AddComponent<SettingsScroller>();
                }
                scroller.Setup(page, total - clipHeight);
                Plugin.Log?.Info($"[RainbowClock] scroll ready: content={total:F1} clip={clipHeight:F1} scrollable={total - clipHeight:F1}");
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log?.Error("[RainbowClock] TrySetupScroll: " + e);
                return false;
            }
        }

        /// <summary>
        /// 时区下拉定制：限制列表高度（内部滚动，避免遮挡下方设置项）并加宽列表与按钮。
        /// </summary>
        private void CustomizeTimeZoneDropdown()
        {
            try
            {
                if (TimeZoneDropdown?.Dropdown == null)
                {
                    return;
                }
                // 时区名较长，按钮加宽
                var dropdownTransform = TimeZoneDropdown.Dropdown.transform as RectTransform;
                if (dropdownTransform != null)
                {
                    Vector2 size = dropdownTransform.sizeDelta;
                    size.x = 55f;
                    dropdownTransform.sizeDelta = size;
                }

                var tableViews = TimeZoneDropdown.Dropdown.GetComponentsInChildren<TableView>(true);
                foreach (var tableView in tableViews)
                {
                    var tableRect = tableView.transform as RectTransform;
                    if (tableRect == null)
                    {
                        continue;
                    }
                    Vector2 tableSize = tableRect.sizeDelta;
                    tableSize.x = 110f;
                    tableSize.y = 45f;
                    tableRect.sizeDelta = tableSize;
                }
            }
            catch (Exception e)
            {
                Plugin.Log?.Error("[RainbowClock] CustomizeTimeZoneDropdown: " + e);
            }
        }

        private void RefreshLanguage()
        {
            _labels.Clear();

            CollectLabels();
            foreach (var (label, enKey) in _labels)
            {
                if (label != null)
                {
                    label.text = Loc.T(enKey);
                }
            }

            // 下拉选项重渲染
            if (ClockTypeDropdown != null)
            {
                ClockTypeDropdown.UpdateChoices();
                ClockTypeDropdown.Value = Config.ClockType;
            }
            if (ClockTwoTypeDropdown != null)
            {
                ClockTwoTypeDropdown.UpdateChoices();
                ClockTwoTypeDropdown.Value = Config.ClockTwoType;
            }
            if (LanguageDropdown != null)
            {
                LanguageDropdown.UpdateChoices();
                // 注意：不能在此重设 Value —— BSML 的 on-change 先于 apply-on-change 触发，
                // 语言切换时 Config 还是旧值，重设会把下拉弹回旧选项
            }
            if (TimeZoneDropdown != null)
            {
                TimeZoneDropdown.UpdateChoices();
                TimeZoneDropdown.Value = TimeZoneValue;
            }

            RefreshBatteryButtonText();
            _localized = true;
        }

        private void CollectLabels()
        {
            void Add(TextMeshProUGUI tmp, string enKey)
            {
                if (tmp != null)
                {
                    _labels.Add((tmp, enKey));
                }
            }

            Add(ShowInSongToggle?.TextMesh, "show_song");
            Add(ShowInReplayToggle?.TextMesh, "show_replay");
            Add(TwelveToggle?.TextMesh, "twelve");
            Add(SecondsToggle?.TextMesh, "seconds");
            Add(BatteryToggle?.TextMesh, "battery");
            Add(RainbowToggle?.TextMesh, "rainbow");
            Add(ClockTwoToggle?.TextMesh, "clock_two");
            Add(FontSizeSetting != null ? FindNameLabel(FontSizeSetting) : null, "font_size");
            Add(PosXSetting != null ? FindNameLabel(PosXSetting) : null, "pos_x");
            Add(PosYSetting != null ? FindNameLabel(PosYSetting) : null, "pos_y");
            Add(PosZSetting != null ? FindNameLabel(PosZSetting) : null, "pos_z");

            // 下拉行标签在组件父级 "Label"
            Add(FindLabel(ClockTypeDropdown), "clock_type");
            Add(FindLabel(TimeZoneDropdown), "time_zone");
            Add(FindLabel(LanguageDropdown), "language");
            Add(FindLabel(ClockTwoTypeDropdown), "clock_two_type");

            // 颜色行标签在组件自身 "NameText"
            if (ClockColorRow != null)
            {
                Add(ClockColorRow.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>(), "clock_color");
            }
            if (FpsColorRow != null)
            {
                Add(FpsColorRow.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>(), "fps_color");
            }
        }

        private static TextMeshProUGUI FindLabel(Component setting)
        {
            if (setting == null)
            {
                return null;
            }
            Transform label = setting.transform.parent?.Find("Label");
            return label != null ? label.GetComponent<TextMeshProUGUI>() : null;
        }

        /// <summary>increment/颜色行的标签在组件自身 "NameText"（注意 IncDecSetting.TextMesh 是数值显示，不是标签）</summary>
        private static TextMeshProUGUI FindNameLabel(Component setting)
        {
            if (setting == null)
            {
                return null;
            }
            return setting.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        }

        private void RefreshBatteryButtonText()
        {
            if (RefreshBatteryButton == null)
            {
                return;
            }
            string battText = Loc.T("btn_refresh_battery");

            // 连接状态放入括号：有线只显示"有线"，无线显示"无线 + IP 最后三位"
            string serial = AdbBattery.TargetSerial;
            if (!string.IsNullOrEmpty(serial))
            {
                string suffix;
                if (serial.Contains(":"))
                {
                    string ip = serial.Split(':')[0];
                    int dot = ip.LastIndexOf('.');
                    string last = dot >= 0 && dot < ip.Length - 1 ? ip.Substring(dot + 1) : ip;
                    suffix = Loc.T("conn_wireless") + " " + last;
                }
                else
                {
                    suffix = Loc.T("conn_wired");
                }
                battText += " (" + suffix + ")";
            }
            else if (AdbBattery.LastErrorType != BatteryError.None)
            {
                string err = AdbBattery.LastErrorType == BatteryError.NoDevice
                    ? Loc.T("batt_no_device")
                    : Loc.T("batt_not_available");
                battText += " (" + err + ")";
            }
            else
            {
                battText += " (" + Loc.T("conn_not_connected") + ")";
            }

            BeatSaberUI.SetButtonText(RefreshBatteryButton, battText);
        }
    }
#pragma warning restore 0649
}
