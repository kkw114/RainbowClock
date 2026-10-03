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
    /// 注意：宿主实例被两个入口（Mods 列表 + 主菜单按钮）共享，
    /// 因此所有"当前页面"相关状态都必须按组件实例检测并重建，不能只初始化一次。
    /// </summary>
    public class ClockSettingsHost
    {
        private readonly List<(TextMeshProUGUI label, string enKey)> _labels = new List<(TextMeshProUGUI, string)>();
        private bool _localized;
        private ColorSetting[] _lastColorRows;
        private BatteryError _lastBatteryErrorShown = BatteryError.None;
        private string _lastButtonSerial = "\0";

        // 多入口页面（Mods 列表 + 主菜单按钮）各自独立初始化滚动
        private RectTransform _lastScrollClip;
        private readonly HashSet<RectTransform> _pendingScrolls = new HashSet<RectTransform>();
        private readonly HashSet<RectTransform> _scrollDone = new HashSet<RectTransform>();
        /// <summary>滚动初始化重试上限（0.25s 调用一次，40 次约 10 秒），达到后放弃，避免无限重试刷日志。</summary>
        private const int MaxScrollAttempts = 40;
        /// <summary>常规设置行的行高（也是量不到高度时的兜底值）。</summary>
        private const float FallbackRowHeight = 10f;
        /// <summary>
        /// 行高上限：超过此值视为"读到的是预制件默认尺寸"而不是真实内容高度，改用兜底值。
        /// 正常设置行约 9~15 单位，预制件默认尺寸接近 100。
        /// </summary>
        private const float MaxRowHeight = 30f;
        private readonly Dictionary<RectTransform, int> _scrollAttempts = new Dictionary<RectTransform, int>();

        /// <summary>上一次应用到 UI 的彩虹开关值（null 表示还没应用过）。</summary>
        private bool? _rainbowStateApplied;

        private ClockConfig Config => Plugin.Config;

        // ==================== BSML 值绑定 ====================

        [UIValue("Clock1ContentValue")]
        public int Clock1ContentValue
        {
            get => NormalizeContent(Config.Clock1Content, false);
            set => Config.Clock1Content = value;
        }

        [UIValue("Clock2ContentValue")]
        public int Clock2ContentValue
        {
            get => NormalizeContent(Config.Clock2Content, false);
            set => Config.Clock2Content = value;
        }

        [UIValue("Clock3ContentValue")]
        public int Clock3ContentValue
        {
            get => NormalizeContent(Config.Clock3Content, false);
            set => Config.Clock3Content = value;
        }

        // ===== 局内（歌曲/关卡中）· 只有内容可配置，颜色等设置与局外共用 =====

        [UIValue("InGameClock1ContentValue")]
        public int InGameClock1ContentValue
        {
            get => NormalizeContent(Config.InGameClock1Content, true);
            set => Config.InGameClock1Content = value;
        }

        [UIValue("InGameClock2ContentValue")]
        public int InGameClock2ContentValue
        {
            get => NormalizeContent(Config.InGameClock2Content, true);
            set => Config.InGameClock2Content = value;
        }

        [UIValue("InGameClock3ContentValue")]
        public int InGameClock3ContentValue
        {
            get => NormalizeContent(Config.InGameClock3Content, true);
            set => Config.InGameClock3Content = value;
        }

        /// <summary>局内置底：仅局内生效，临时把位置 Y（上下）/ Z（前后）换成内置的置底数值。</summary>
        [UIValue("InGameBottomAlignValue")]
        public bool InGameBottomAlignValue
        {
            get => Config.InGameBottomAlign;
            set => Config.InGameBottomAlign = value;
        }

        /// <summary>局内时钟缩放倍率：仅局内生效，作用在字号上（整体等比缩放）。</summary>
        [UIValue("InGameScaleValue")]
        public float InGameScaleValue
        {
            get => Config.InGameScale;
            set => Config.InGameScale = value;
        }

        [UIValue("LanguageValue")]
        public int LanguageValue
        {
            get => Config.Language;
            set => Config.Language = value;
        }

        [UIValue("InSongValue")]
        public bool InSongValue
        {
            get => Config.InSong;
            set => Config.InSong = value;
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

        [UIValue("RainbowValue")]
        public bool RainbowValue
        {
            get => Config.RainbowClock;
            set => Config.RainbowClock = value;
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

        // 颜色行直接把 Unity Color 传给 color-setting，避免宿主持有另一份颜色状态导致两者不同步
        [UIValue("Clock1ColorValue")]
        public Color Clock1ColorValue
        {
            get => ParseColor(Config.Clock1Color);
            set => Config.SetSlotColor(0, value);
        }

        [UIValue("Clock2ColorValue")]
        public Color Clock2ColorValue
        {
            get => ParseColor(Config.Clock2Color);
            set => Config.SetSlotColor(1, value);
        }

        [UIValue("Clock3ColorValue")]
        public Color Clock3ColorValue
        {
            get => ParseColor(Config.Clock3Color);
            set => Config.SetSlotColor(2, value);
        }

        [UIValue("FpsColorValue")]
        public Color FpsColorValue
        {
            get => Config.GetFpsColor();
            set => Config.SetFpsColor(value);
        }

        private static Color ParseColor(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.white;
        }

        // ==================== 下拉选项与格式化 ====================

        /// <summary>局外槽位内容选项：0=隐藏、1=本次启动总时长、2=本次游玩时长、3=帧率、4=当前时间。</summary>
        [UIValue("ContentOptions")]
        public int[] ContentOptions => new[]
        {
            (int)ClockContent.Hidden,
            (int)ClockContent.GameTotal,
            (int)ClockContent.PlaySession,
            (int)ClockContent.Fps,
            (int)ClockContent.CurrentTime
        };

        /// <summary>
        /// 局内槽位内容选项：在局外基础上追加"歌曲剩余时长""歌曲当前百分比"。
        /// 新选项一律追加在末尾，避免已有配置里的数值含义发生漂移。
        /// </summary>
        [UIValue("InGameContentOptions")]
        public int[] InGameContentOptions => new[]
        {
            (int)ClockContent.Hidden,
            (int)ClockContent.GameTotal,
            (int)ClockContent.PlaySession,
            (int)ClockContent.Fps,
            (int)ClockContent.CurrentTime,
            (int)ClockContent.SongRemaining,
            (int)ClockContent.SongProgress
        };

        /// <summary>该内容值在当前分区是否可选（歌曲相关只在局内有意义）。</summary>
        private static bool IsValidContent(int value, bool inGame)
        {
            switch ((ClockContent)value)
            {
                case ClockContent.Hidden:
                case ClockContent.GameTotal:
                case ClockContent.PlaySession:
                case ClockContent.Fps:
                case ClockContent.CurrentTime:
                    return true;
                case ClockContent.SongRemaining:
                case ClockContent.SongProgress:
                    return inGame;
                default:
                    // 已移除的取值（如旧的 UTC=5）落到这里
                    return false;
            }
        }

        /// <summary>
        /// 把无效的内容值归一化为"隐藏"。
        /// 旧配置里可能残留已移除的取值（如 UTC=5），而下拉选项列表已不含它——
        /// 直接把原值交给下拉会显示错位，归一化后 UI 与运行时行为一致（都是隐藏），
        /// 并在下次保存配置时自愈。
        /// </summary>
        private static int NormalizeContent(int value, bool inGame)
        {
            return IsValidContent(value, inGame) ? value : (int)ClockContent.Hidden;
        }

        [UIValue("LanguageOptions")]
        public int[] LanguageOptions => new[] { 0, 1, 2 };

        [UIAction("ContentFormatter")]
        public string ContentFormatter(object value) => Loc.GetContentName(Convert.ToInt32(value));

        [UIAction("LanguageFormatter")]
        public string LanguageFormatter(object value) => Loc.GetLanguageName(Convert.ToInt32(value));

        // ==================== 组件引用（解析后填充） ====================

        [UIComponent("Clock1Content")]
        internal DropDownListSetting Clock1ContentDropdown;

        [UIComponent("Clock2Content")]
        internal DropDownListSetting Clock2ContentDropdown;

        [UIComponent("Clock3Content")]
        internal DropDownListSetting Clock3ContentDropdown;

        [UIComponent("InGameClock1Content")]
        internal DropDownListSetting InGameClock1ContentDropdown;

        [UIComponent("InGameClock2Content")]
        internal DropDownListSetting InGameClock2ContentDropdown;

        [UIComponent("InGameClock3Content")]
        internal DropDownListSetting InGameClock3ContentDropdown;

        [UIComponent("Lang")]
        internal DropDownListSetting LanguageDropdown;

        [UIComponent("TogInSong")]
        internal ToggleSetting ShowInSongToggle;

        [UIComponent("TogInGameBottom")]
        internal ToggleSetting InGameBottomToggle;

        [UIComponent("InGameScale")]
        internal IncrementSetting InGameScaleSetting;

        [UIComponent("TogTwelve")]
        internal ToggleSetting TwelveToggle;

        [UIComponent("TogSeconds")]
        internal ToggleSetting SecondsToggle;

        [UIComponent("TogRainbow")]
        internal ToggleSetting RainbowToggle;

        [UIComponent("FontSize")]
        internal IncrementSetting FontSizeSetting;

        [UIComponent("PosX")]
        internal IncrementSetting PosXSetting;

        [UIComponent("PosY")]
        internal IncrementSetting PosYSetting;

        [UIComponent("PosZ")]
        internal IncrementSetting PosZSetting;

        [UIComponent("Clock1ColorRow")]
        internal ColorSetting Clock1ColorRow;

        [UIComponent("Clock2ColorRow")]
        internal ColorSetting Clock2ColorRow;

        [UIComponent("Clock3ColorRow")]
        internal ColorSetting Clock3ColorRow;

        [UIComponent("FpsColorRow")]
        internal ColorSetting FpsColorRow;

        [UIComponent("SettingsRows")]
        internal VerticalLayoutGroup SettingsRowsLayout;

        [UIComponent("ScrollClip")]
        internal RectTransform ScrollClip;

        [UIComponent("BtnRefreshBattery")]
        internal Button RefreshBatteryButton;

        // ==================== 动作 ====================

        [UIAction("OnClock1ContentChanged")]
        public void OnClock1ContentChanged(int value)
        {
            OnContentChanged();
        }

        [UIAction("OnClock2ContentChanged")]
        public void OnClock2ContentChanged(int value)
        {
            OnContentChanged();
        }

        [UIAction("OnClock3ContentChanged")]
        public void OnClock3ContentChanged(int value)
        {
            OnContentChanged();
        }

        // ===== 局内内容变化（只有内容可配置） =====

        [UIAction("OnInGameClock1ContentChanged")]
        public void OnInGameClock1ContentChanged(int value)
        {
            OnContentChanged();
        }

        [UIAction("OnInGameClock2ContentChanged")]
        public void OnInGameClock2ContentChanged(int value)
        {
            OnContentChanged();
        }

        [UIAction("OnInGameClock3ContentChanged")]
        public void OnInGameClock3ContentChanged(int value)
        {
            OnContentChanged();
        }

        [UIAction("OnClock1ColorChanged")]
        public void OnClock1ColorChanged(Color value)
        {
            Config.SetSlotColor(0, value);
        }

        [UIAction("OnClock2ColorChanged")]
        public void OnClock2ColorChanged(Color value)
        {
            Config.SetSlotColor(1, value);
        }

        [UIAction("OnClock3ColorChanged")]
        public void OnClock3ColorChanged(Color value)
        {
            Config.SetSlotColor(2, value);
        }

        [UIAction("OnLangChanged")]
        public void OnLangChanged(int value)
        {
            Loc.SetMode((LangMode)value);
            RefreshLanguage();
            Plugin.UpdateMenuButtonHint();
        }

        /// <summary>彩虹开关变化：颜色配置在彩虹模式下无效，立即按开关显隐这几行。</summary>
        [UIAction("OnRainbowChanged")]
        public void OnRainbowChanged(bool value)
        {
            ApplyColorRowVisibility();
        }

        [UIAction("RefreshBattery")]
        public void RefreshBattery()
        {
            AdbBattery.RefreshNow(true); // 手动刷新：重置重试计数并恢复自动轮询
        }

        /// <summary>槽位内容变化：配置已由 value 绑定写回，这里只做下拉与状态刷新。</summary>
        private void OnContentChanged()
        {
            // 内容变化会改变槽位文字宽度与可见槽位数量，控制器每 0.25s 自动重排，无需额外处理
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
                // 布局已被销毁（退出游戏/切换页面）时直接丢弃，避免对已销毁对象反复操作抛异常
                if (clip == null)
                {
                    _pendingScrolls.Remove(clip);
                    _scrollAttempts.Remove(clip);
                    continue;
                }
                if (TrySetupScroll(clip))
                {
                    _scrollDone.Add(clip);
                    _pendingScrolls.Remove(clip);
                    _scrollAttempts.Remove(clip);
                }
                else
                {
                    int attempts = _scrollAttempts.TryGetValue(clip, out int a) ? a : 0;
                    attempts++;
                    if (attempts >= MaxScrollAttempts)
                    {
                        // 页面未激活/布局高度为 0，重试足够次数后放弃（下次解析会重新尝试）
                        _pendingScrolls.Remove(clip);
                        _scrollAttempts.Remove(clip);
                        Plugin.Log?.Warn("[RainbowClock] giving up scroll setup for an inactive/hidden settings page after too many retries.");
                    }
                    else
                    {
                        _scrollAttempts[clip] = attempts;
                    }
                }
            }

            // 2) 当前页面的本地化/时区定制/ADB 状态（字段绑定的是最近解析的页面）
            if (SettingsRowsLayout != null && Clock1ContentDropdown != null)
            {
                // 两个入口各自解析一次：颜色行数组实例变化即代表换了一个页面，
                // 必须重新本地化（否则第二个入口打开时标签还是英文/旧语言）
                var colorRows = new[] { Clock1ColorRow, Clock2ColorRow, Clock3ColorRow, FpsColorRow };
                bool newPage = _lastColorRows == null || !SameRows(_lastColorRows, colorRows);
                if (newPage)
                {
                    _lastColorRows = colorRows;
                    _localized = false;
                }
                if (!_localized)
                {
                    RefreshLanguage();
                }
                // ADB 状态或目标设备变化时刷新按钮文字（连接状态/错误提示）
                if (AdbBattery.LastErrorType != _lastBatteryErrorShown
                    || AdbBattery.TargetSerial != _lastButtonSerial)
                {
                    _lastBatteryErrorShown = AdbBattery.LastErrorType;
                    _lastButtonSerial = AdbBattery.TargetSerial;
                    RefreshBatteryButtonText();
                }

                // 彩虹模式下行显隐需要跟随（开启彩虹时颜色配置无效，隐藏这几行）
                if (newPage || _rainbowStateApplied != Plugin.Config.RainbowClock)
                {
                    ApplyColorRowVisibility();
                }
            }
        }

        /// <summary>
        /// 按彩虹开关显隐"时钟 1/2/3 颜色 + FPS 颜色"这四行。
        /// 彩虹开启时这些单独颜色不生效，隐藏掉避免误导；关闭时才显示。
        /// 行显隐会改变滚动内容总高，所以隐藏后必须重算滚动范围。
        /// </summary>
        private void ApplyColorRowVisibility()
        {
            bool showColors = !Plugin.Config.RainbowClock;
            _rainbowStateApplied = Plugin.Config.RainbowClock;

            var rows = new[] { Clock1ColorRow, Clock2ColorRow, Clock3ColorRow, FpsColorRow };
            foreach (ColorSetting row in rows)
            {
                if (row == null)
                {
                    continue;
                }
                GameObject root = FindRowRoot(row);
                if (root != null && root.activeSelf != showColors)
                {
                    root.SetActive(showColors);
                }
            }

            // 行数变了 → 重新回填行高并更新滚动范围
            RefreshScrollContent();
        }

        /// <summary>
        /// 找到设置行的根物体（SettingsRows 的直接子物体）。
        /// BSML 组件通常就在行根上，但不能假定，所以向上找到"父级是带垂直布局的容器"那一层。
        /// </summary>
        private static GameObject FindRowRoot(Component setting)
        {
            Transform t = setting.transform;
            while (t.parent != null && t.parent.GetComponent<VerticalLayoutGroup>() == null)
            {
                t = t.parent;
            }
            return t.gameObject;
        }

        /// <summary>
        /// 计算设置行的行高，并把结果写回 LayoutElement（让 VerticalLayoutGroup 的排布与
        /// 手工算出的滚动内容高度用同一个值）。
        ///
        /// 关键防御：**不能信任预制件的 rect.height**。
        /// 布局未完成时它等于预制件的默认尺寸，可能接近 100——曾被当成内容高度写进 preferredHeight，
        /// 结果那一行被撑成近百单位高，在它后面留下一大段空白（"两段设置之间空隙过大"就是这个原因）。
        /// 所以超过 MaxRowHeight 一律视为不可信，改用常规行高。
        /// </summary>
        private static float ComputeRowHeight(Transform child)
        {
            var layoutElement = child.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                // 没有 LayoutElement 的行：补一个兜底高度，避免塌成 0 高看不见
                child.gameObject.AddComponent<LayoutElement>().preferredHeight = FallbackRowHeight;
                return FallbackRowHeight;
            }

            float h = ((RectTransform)child).rect.height;
            if (h <= 0.01f || h > MaxRowHeight)
            {
                h = FallbackRowHeight; // 量不到，或量到的是预制件默认尺寸 → 用常规行高
            }
            else if (h < 9f)
            {
                h = 9f;
            }
            layoutElement.preferredHeight = h;
            return h;
        }

        /// <summary>行显隐/行高变化后重算滚动内容高度（按当前处于激活状态的行）。</summary>
        private void RefreshScrollContent()
        {
            RectTransform clip = ScrollClip;
            if (clip == null)
            {
                return;
            }
            try
            {
                var page = clip.GetChild(0) as RectTransform;
                var pageLayout = page != null ? page.GetComponent<VerticalLayoutGroup>() : null;
                if (page == null || pageLayout == null)
                {
                    return;
                }

                float total = 0f;
                int count = 0;
                foreach (Transform child in page)
                {
                    if (!child.gameObject.activeSelf)
                    {
                        continue; // 隐藏的行不占高度
                    }
                    total += ComputeRowHeight(child);
                    count++;
                }
                if (count > 1)
                {
                    total += pageLayout.spacing * (count - 1);
                }

                page.sizeDelta = new Vector2(0f, total);

                var scroller = clip.GetComponent<SettingsScroller>();
                if (scroller != null)
                {
                    scroller.UpdateScrollable(total - clip.rect.height);
                }
            }
            catch (Exception e)
            {
                Plugin.Log?.Warn("[RainbowClock] RefreshScrollContent: " + e.Message);
            }
        }

        private static bool SameRows(ColorSetting[] a, ColorSetting[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (!ReferenceEquals(a[i], b[i]))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 独立初始化一个设置页面的滚动：行高回填、内容锚定、RectMask2D、SettingsScroller。
        /// 不依赖宿主字段（多入口页面共享宿主），布局未完成时返回 false 由调用方下轮重试。
        /// </summary>
        private bool TrySetupScroll(RectTransform clip)
        {
            if (clip == null)
            {
                return true; // 已销毁：按“完成”处理，让调用方从待办列表中移除
            }
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
                    if (!child.gameObject.activeSelf)
                    {
                        continue; // 隐藏的行（如彩虹模式下隐藏的颜色行）不占高度
                    }
                    total += ComputeRowHeight(child);
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

                // 布局未完成时下轮重试（重试次数由调用方限制，不再每帧刷日志）
                float clipHeight = clip.rect.height;
                if (clipHeight <= 1f)
                {
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

        private void RefreshLanguage()
        {
            _labels.Clear();

            CollectLabels();
            foreach (var entry in _labels)
            {
                if (entry.label != null)
                {
                    entry.label.text = Loc.T(entry.enKey);
                }
            }

            // 下拉选项重渲染（value 与配置一致，重设是幂等的，不会把选项弹回旧值）
            UpdateDropdown(Clock1ContentDropdown, Clock1ContentValue);
            UpdateDropdown(Clock2ContentDropdown, Clock2ContentValue);
            UpdateDropdown(Clock3ContentDropdown, Clock3ContentValue);
            UpdateDropdown(InGameClock1ContentDropdown, InGameClock1ContentValue);
            UpdateDropdown(InGameClock2ContentDropdown, InGameClock2ContentValue);
            UpdateDropdown(InGameClock3ContentDropdown, InGameClock3ContentValue);
            if (LanguageDropdown != null)
            {
                LanguageDropdown.UpdateChoices();
                // 注意：不能在此重设 Value —— BSML 的 on-change 先于 apply-on-change 触发，
                // 语言切换时 Config 还是旧值，重设会把下拉弹回旧选项
            }

            RefreshBatteryButtonText();
            _localized = true;
        }

        private static void UpdateDropdown(DropDownListSetting dropdown, int value)
        {
            if (dropdown == null)
            {
                return;
            }
            dropdown.UpdateChoices();
            dropdown.Value = value;
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
            Add(InGameBottomToggle?.TextMesh, "ingame_bottom");
            Add(InGameScaleSetting != null ? FindNameLabel(InGameScaleSetting) : null, "ingame_scale");
            Add(TwelveToggle?.TextMesh, "twelve");
            Add(SecondsToggle?.TextMesh, "seconds");
            Add(RainbowToggle?.TextMesh, "rainbow");
            Add(FontSizeSetting != null ? FindNameLabel(FontSizeSetting) : null, "font_size");
            Add(PosXSetting != null ? FindNameLabel(PosXSetting) : null, "pos_x");
            Add(PosYSetting != null ? FindNameLabel(PosYSetting) : null, "pos_y");
            Add(PosZSetting != null ? FindNameLabel(PosZSetting) : null, "pos_z");

            // 下拉行标签在组件父级 "Label"（用"局外/局内"前缀区分两套内容，不设分区标题行）
            Add(FindLabel(Clock1ContentDropdown), "outside_slot1");
            Add(FindLabel(Clock2ContentDropdown), "outside_slot2");
            Add(FindLabel(Clock3ContentDropdown), "outside_slot3");
            Add(FindLabel(InGameClock1ContentDropdown), "ingame_slot1");
            Add(FindLabel(InGameClock2ContentDropdown), "ingame_slot2");
            Add(FindLabel(InGameClock3ContentDropdown), "ingame_slot3");
            Add(FindLabel(LanguageDropdown), "language");

            // 颜色行标签在组件自身 "NameText"
            if (Clock1ColorRow != null)
            {
                Add(Clock1ColorRow.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>(), "slot1_color");
            }
            if (Clock2ColorRow != null)
            {
                Add(Clock2ColorRow.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>(), "slot2_color");
            }
            if (Clock3ColorRow != null)
            {
                Add(Clock3ColorRow.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>(), "slot3_color");
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

            // 按钮提示也需跟随语言刷新（说明槽位 4 固定为电量且取不到时自动隐藏）
            SetButtonHint(RefreshBatteryButton, Loc.T("battery_fixed"));
        }

        /// <summary>刷新按钮的 HoverHint 文本（说明槽位 4 固定为电量、取不到时自动隐藏）。</summary>
        private static void SetButtonHint(Component button, string text)
        {
            try
            {
                var hint = button.GetComponent<HMUI.HoverHint>();
                if (hint != null)
                {
                    hint.text = text;
                }
            }
            catch (Exception e)
            {
                Plugin.Log?.Warn("[RainbowClock] set button hint failed: " + e.Message);
            }
        }
    }
#pragma warning restore 0649
}
