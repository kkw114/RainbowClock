using System;
using System.Collections.Generic;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using HMUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        private bool _rowsFixed;
        private VerticalLayoutGroup _lastRowsLayout;
        private BatteryError _lastBatteryErrorShown = BatteryError.None;

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

        // ==================== 下拉选项与格式化 ====================

        [UIValue("ClockTypeOptions")]
        public int[] ClockTypeOptions => new[] { 0, 1 };

        [UIValue("ClockTwoTypeOptions")]
        public int[] ClockTwoTypeOptions => new[] { 4, 0, 1 };

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

        /// <summary>每 0.25s 调用：解析完成后做本地化初始化、行高设置与手动滚动初始化。</summary>
        public void Tick()
        {
            bool parsed = ClockTypeDropdown != null && SettingsRowsLayout != null;
            if (parsed)
            {
                // 设置页有两个入口（Mods 列表 + 主菜单按钮），各自解析一次；
                // 检测到新的解析结果时重新执行初始化
                if (SettingsRowsLayout != _lastRowsLayout)
                {
                    _lastRowsLayout = SettingsRowsLayout;
                    _localized = false;
                    _rowsFixed = false;
                }
                if (!_localized)
                {
                    RefreshLanguage();
                }
                if (!_rowsFixed)
                {
                    FixRowHeights();
                }
                // ADB 状态变化时刷新按钮文字（未连接/不可用提示）
                if (AdbBattery.LastErrorType != _lastBatteryErrorShown)
                {
                    _lastBatteryErrorShown = AdbBattery.LastErrorType;
                    RefreshBatteryButtonText();
                }
            }
        }

        /// <summary>
        /// 为每个设置行设置 LayoutElement 高度（模板默认为 0），并把内容容器锚定到顶部、设固定高度，
        /// 最后在裁剪容器上初始化手动摇杆滚动。
        /// </summary>
        private void FixRowHeights()
        {
            if (SettingsRowsLayout == null)
            {
                return;
            }
            try
            {
                float total = 0f;
                int count = 0;
                foreach (Transform child in SettingsRowsLayout.transform)
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
                    total += SettingsRowsLayout.spacing * (count - 1);
                }

                // 内容容器：顶部锚定 + 固定高度
                var contentRect = SettingsRowsLayout.transform as RectTransform;
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = new Vector2(1f, 1f);
                contentRect.pivot = new Vector2(0.5f, 1f);
                contentRect.anchoredPosition = Vector2.zero;
                contentRect.sizeDelta = new Vector2(0f, total);

                SetupManualScroll(total);
                CustomizeTimeZoneDropdown();
            }
            catch (Exception e)
            {
                Plugin.Log?.Error("[RainbowClock] FixRowHeights: " + e);
            }
        }

        /// <summary>在裁剪容器上挂 SettingsScroller，启用摇杆滚动（行级可见性管理代替 RectMask2D 裁剪）。</summary>
        private void SetupManualScroll(float contentHeight)
        {
            try
            {
                if (ScrollClip == null)
                {
                    return;
                }
                // 不用 RectMask2D：其裁剪边缘在阻尼滚动时会产生闪烁；
                // 由 SettingsScroller 做行级可见性（完整进入裁剪区才显示）

                float clipHeight = ScrollClip.rect.height;
                if (clipHeight <= 1f)
                {
                    // 布局尚未完成，下一轮重试
                    _rowsFixed = false;
                    return;
                }

                var scroller = ScrollClip.GetComponent<SettingsScroller>();
                if (scroller == null)
                {
                    scroller = ScrollClip.gameObject.AddComponent<SettingsScroller>();
                }
                scroller.Setup(SettingsRowsLayout.transform as RectTransform, ScrollClip, contentHeight - clipHeight, clipHeight);
                _rowsFixed = true;
                Plugin.Log?.Info($"[RainbowClock] manual scroll ready: content={contentHeight:F1} clip={clipHeight:F1} scrollable={contentHeight - clipHeight:F1}");
            }
            catch (Exception e)
            {
                Plugin.Log?.Error("[RainbowClock] SetupManualScroll: " + e);
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
            switch (AdbBattery.LastErrorType)
            {
                case BatteryError.NoDevice:
                    battText += " (" + Loc.T("batt_no_device") + ")";
                    break;
                case BatteryError.Unavailable:
                    battText += " (" + Loc.T("batt_not_available") + ")";
                    break;
            }
            BeatSaberUI.SetButtonText(RefreshBatteryButton, battText);
        }
    }
#pragma warning restore 0649
}
