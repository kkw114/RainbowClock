using System;
using System.Text;

// ============================================================
// RainbowClock 纯逻辑回归测试（不依赖 Unity / 游戏程序集）
// 被测算法从以下文件原样复制，任何一处改动都需要同步：
//   RainbowClock/ClockController.cs        GetStopwatchString / StripRichText / GetSeparatorGap
//   RainbowClock/ClockController.cs        GetFpsGradientColor
//   RainbowClock/Localization.cs           GetSeparatorText
//   RainbowClock/RainbowText.cs            Apply
//   RainbowClock/FpsTracker.cs             DisplayFps 阈值
// ============================================================
internal static class Program
{
    private static int _pass;
    private static int _fail;

    private static void Check(string name, object actual, object expected)
    {
        bool ok;
        // float/double 比较带容差：布局计算是浮点累加，末位差异不算失败
        bool actualNumeric = actual is float || actual is double;
        bool expectedNumeric = expected is float || expected is double;
        bool actualBool = actual is bool;
        bool expectedBool = expected is bool;
        if (actualNumeric && expectedNumeric)
        {
            double a = Convert.ToDouble(actual);
            double b = Convert.ToDouble(expected);
            ok = Math.Abs(a - b) <= 1e-4 * Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b)));
        }
        else if (actualBool && expectedBool)
        {
            ok = (bool)actual == (bool)expected;
        }
        else
        {
            ok = Equals(actual?.ToString(), expected?.ToString());
        }
        if (ok) { _pass++; }
        else { _fail++; }
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name,-52} actual={actual}  expected={expected}");
    }

    // ---------- 被测算法（副本） ----------
    // 计时类时长自适应格式：<1h → mm:ss；>=1h → hh:mm:ss（小时不封顶）
    private static string GetStopwatchString(double totalSeconds)
    {
        if (totalSeconds < 0d) { totalSeconds = 0d; }
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

    // 歌曲剩余时长 / 进度（复刻 ClockController 的计算）
    private static double SongRemaining(double songLength, double songTime)
    {
        if (songLength <= 0d) { return 0d; }
        double remaining = songLength - songTime;
        return remaining > 0d ? remaining : 0d;
    }

    private static string SongProgress(double songLength, double songTime)
    {
        if (songLength <= 0d) { return ""; }
        double progress = songTime / songLength;
        if (progress < 0d) { progress = 0d; }
        if (progress > 1d) { progress = 1d; }
        return Math.Round(progress * 100d) + "%";
    }

    private static string StripRichText(string input)
    {
        if (string.IsNullOrEmpty(input) || input.IndexOf('<') < 0) { return input ?? ""; }
        var sb = new StringBuilder(input.Length);
        int i = 0;
        while (i < input.Length)
        {
            if (input[i] == '<')
            {
                int end = input.IndexOf('>', i);
                if (end < 0) { break; }
                i = end + 1;
                continue;
            }
            sb.Append(input[i]);
            i++;
        }
        return sb.ToString();
    }

    private static float GetSeparatorGap(float fontSize, bool spaces)
        => spaces ? 0f : fontSize * 0.9f;

    private static string GetFpsGradientColor(int fps, int target)
    {
        int redThreshold;
        if (target <= 0) { redThreshold = 100; }
        else if (target <= 60) { redThreshold = target - 5; }
        else if (target <= 90) { redThreshold = target - 10; }
        else if (target <= 120) { redThreshold = target - 20; }
        else { redThreshold = target - 30; }

        if (target > 0 && fps >= target) { return "00FF00"; }
        if (fps >= redThreshold) { return "FFFF00"; }
        return "FF0000";
    }

    // 固定分隔符：圆点两侧各带一个空格（复刻 ClockController.SlotSeparator / ColorizeSeparator）
    // hex 参数是"已解析过的 RRGGBB"（ClockConfig.ParseHex 会剥掉 #），不带宽字符
    private const string SlotSeparator = " · ";

    private static string ColorizeSeparator(bool rainbow, string slotHex, string fpsHex)
    {
        if (rainbow)
        {
            _index = 0; // 测试隔离：产品代码里 _index 是连续推进的
            return RainbowApply(SlotSeparator);
        }
        return "<color=#" + (slotHex ?? fpsHex) + ">" + SlotSeparator + "</color>";
    }

    // RainbowText.Apply 副本（含索引推进）
    private static readonly string[] Colors =
    {
        "#ff6060", "#ffa060", "#ffff60", "#a0ff60", "#60ff60", "#60ffa0",
        "#60ffff", "#60a0ff", "#6060ff", "#a060ff", "#ff60ff", "#ff60a0"
    };
    private static int _index = 0;
    private static string RainbowApply(string input)
    {
        var sb = new StringBuilder(input.Length * 24);
        foreach (char c in input)
        {
            sb.Append("<color=").Append(Colors[_index]).Append('>').Append(c).Append("</color>");
            _index = (_index + 1) % Colors.Length;
        }
        int addValue = (Colors.Length - 1) - input.Length;
        if (input.Length < 10)
        {
            _index += addValue;
            if (_index > Colors.Length - 1) { _index -= Colors.Length; }
        }
        return sb.ToString();
    }

    private static int DisplayFps(int currentFps, int target)
        => currentFps >= 5 ? currentFps : (target > 0 ? target : currentFps);

    private static void Main()
    {
        Console.WriteLine("=== 时长格式自适应（<1h → mm:ss；>=1h → hh:mm:ss） ===");
        Check("0s", GetStopwatchString(0), "00:00");
        Check("1分59秒（用户举例）", GetStopwatchString(119), "01:59");
        Check("59s", GetStopwatchString(59), "00:59");
        Check("59分59秒（仍两位数）", GetStopwatchString(3599), "59:59");
        Check("整 1 小时 → 切换三位", GetStopwatchString(3600), "01:00:00");
        Check("1小时1分59秒（用户举例）", GetStopwatchString(3719), "01:01:59");
        Check("2小时0分5秒", GetStopwatchString(7205), "02:00:05");
        Check("26 小时不按天截断", GetStopwatchString(93600), "26:00:00");
        Check("100 小时", GetStopwatchString(360000), "100:00:00");
        Check("负秒兜底", GetStopwatchString(-5), "00:00");
        Check("亚秒截断", GetStopwatchString(0.99), "00:00");

        Console.WriteLine();
        Console.WriteLine("=== 歌曲剩余时长 / 当前百分比 ===");
        Check("剩余 = 总长 - 当前时间", SongRemaining(180, 60), 120d);
        Check("刚进歌剩余=总长", SongRemaining(180, 0), 180d);
        Check("结束瞬间剩余=0", SongRemaining(180, 180), 0d);
        Check("超出不出现负数", SongRemaining(180, 200), 0d);
        Check("音频未加载（长度0）剩余=0", SongRemaining(0, 0), 0d);
        Check("剩余 2分0秒 的显示", GetStopwatchString(SongRemaining(180, 60)), "02:00");
        Check("剩余超过1小时显示三位", GetStopwatchString(SongRemaining(4000, 100)), "01:05:00");

        Check("进度 0%", SongProgress(180, 0), "0%");
        Check("进度 50%", SongProgress(180, 90), "50%");
        Check("进度 33%（四舍五入）", SongProgress(180, 60), "33%");
        Check("进度 100%", SongProgress(180, 180), "100%");
        Check("进度不超 100%", SongProgress(180, 200), "100%");
        Check("进度不为负", SongProgress(180, -10), "0%");
        Check("音频未加载（长度0）返回空串", SongProgress(0, 0), "");

        Console.WriteLine();
        Console.WriteLine("=== 富文本剥离（宽度测量） ===");
        Check("无标签", StripRichText("HH:mm"), "HH:mm");
        Check("rainbow 单字符", StripRichText("<color=#ff6060>A</color>"), "A");
        Check("rainbow 整串", StripRichText("<color=#ff6060>1</color><color=#ffa060>2</color>"), "12");
        Check("带分隔符", StripRichText("  /  <color=#00FF00>60</color>"), "  /  60");
        Check("嵌套标签", StripRichText("<b><color=#fff>x</color></b>"), "x");
        Check("空串", StripRichText(""), "");
        Check("null", StripRichText(null), "");

        Console.WriteLine();
        Console.WriteLine("=== 固定分隔符：圆点 + 两侧各一个空格 ===");
        Check("分隔符为「空格 圆点 空格」", SlotSeparator, " · ");
        Check("左侧有一个空格", SlotSeparator.StartsWith(" "), true);
        Check("右侧有一个空格", SlotSeparator.EndsWith(" "), true);
        int leadSpaces = 0;
        while (leadSpaces < SlotSeparator.Length && SlotSeparator[leadSpaces] == ' ') { leadSpaces++; }
        int trailSpaces = 0;
        while (trailSpaces < SlotSeparator.Length && SlotSeparator[SlotSeparator.Length - 1 - trailSpaces] == ' ') { trailSpaces++; }
        Check("圆点左右空格数相等（对称）", leadSpaces, trailSpaces);
        Check("圆点参与宽度测量", StripRichText(ColorizeSeparator(false, "FF0000", "00FF00")), " · ");
        Check("彩虹模式下圆点与空格逐字符着色", ColorizeSeparator(true, "FF0000", "00FF00").Contains("<color="), true);
        Check("非彩虹模式圆点用槽位色", ColorizeSeparator(false, "FF0000", "00FF00"), "<color=#FF0000> · </color>");
        Check("FPS 槽位的圆点用 FPS 颜色", ColorizeSeparator(false, null, "00FF00"), "<color=#00FF00> · </color>");

        Console.WriteLine();
        Console.WriteLine("=== FPS 梯度着色 ===");
        Check("90 上限 90", GetFpsGradientColor(90, 90), "00FF00");
        Check("89 上限 90", GetFpsGradientColor(89, 90), "FFFF00");
        Check("80 上限 90 (低10红)", GetFpsGradientColor(80, 90), "FFFF00");
        Check("79 上限 90", GetFpsGradientColor(79, 90), "FF0000");
        Check("55 上限 60 (低5红)", GetFpsGradientColor(55, 60), "FFFF00");
        Check("54 上限 60", GetFpsGradientColor(54, 60), "FF0000");
        Check("100 上限 120 (低20红)", GetFpsGradientColor(100, 120), "FFFF00");
        Check("99 上限 120", GetFpsGradientColor(99, 120), "FF0000");
        Check("90 上限 144 (低30红)", GetFpsGradientColor(90, 144), "FF0000");
        Check("115 上限 144", GetFpsGradientColor(115, 144), "FFFF00");
        Check("读不到上限 60", GetFpsGradientColor(60, 0), "FF0000");
        Check("读不到上限 100", GetFpsGradientColor(100, 0), "FFFF00");

        Console.WriteLine();
        Console.WriteLine("=== 暂停时 FPS 兜底 ===");
        Check("暂停采样0 上限90", DisplayFps(0, 90), 90);
        Check("暂停采样0 无上限", DisplayFps(0, 0), 0);
        Check("正常 72 上限90", DisplayFps(72, 90), 72);
        Check("卡顿 3 上限90", DisplayFps(3, 90), 90);

        Console.WriteLine();
        Console.WriteLine("=== 彩虹着色索引（不越界、颜色表完整） ===");
        bool rainbowOk = true;
        for (int len = 1; len <= 40; len++)
        {
            _index = 0;
            string s = RainbowApply(new string('x', len));
            int open = s.Split(new[] { "<color=" }, StringSplitOptions.None).Length - 1;
            int close = s.Split(new[] { "</color>" }, StringSplitOptions.None).Length - 1;
            if (open != len || close != len) { rainbowOk = false; break; }
            if (_index < 0 || _index > Colors.Length - 1) { rainbowOk = false; break; }
        }
        Check("1~40 字符均完整且索引合法", rainbowOk, true);

        Console.WriteLine();
        Console.WriteLine("=== 槽位布局（圆点两侧间隙由分隔符空格决定） ===");
        // 复刻 ApplyLayout：SlotPadX = 0、槽间距 = 0，所以槽位宽度 = 文本宽度（含分隔符两端空格）
        const float ScreenPadX = 8f;
        const float SlotPadX = 0f;
        const float SlotGapFactor = 0f;
        Func<float[], float, float[]> layout = (textW, fontSize) =>
        {
            var vis = new System.Collections.Generic.List<int>();
            for (int i = 0; i < textW.Length; i++) { if (textW[i] > 0f) { vis.Add(i); } }
            var widths = new float[textW.Length];
            float total = ScreenPadX * 2f;
            foreach (int i in vis)
            {
                widths[i] = Math.Max(2f, textW[i]) + SlotPadX * 2f;
                total += widths[i];
            }
            float gap = fontSize * SlotGapFactor;
            total += gap * (vis.Count - 1);

            var centers = new float[textW.Length];
            float cursor = -total * 0.5f + ScreenPadX;
            foreach (int i in vis)
            {
                centers[i] = cursor + widths[i] * 0.5f;
                cursor += widths[i] + gap;
            }
            return new[] { total, gap, centers[0], centers[1], centers[2], centers[3], widths[0], widths[3] };
        };

        float[] t = layout(new[] { 30f, 0f, 0f, 18f }, 8f);
        float totalW = t[0], gapW = t[1];
        Check("槽间距固定为 0", gapW, 0f);
        Check("槽位无额外内余量（间隙全由空格给出）", t[6], 30f);
        Check("总宽 = 留白 + 各槽文本宽", totalW, 16f + 30f + 18f);
        Check("1 号槽（时钟1）中心", t[2], -totalW / 2f + ScreenPadX + 15f);
        Check("隐藏槽位不占宽（宽度 0）", t[7] > 0 && t[3] == 0f && t[4] == 0f, true);

        // 短内容不再被最宽槽位撑开
        float[] t2 = layout(new[] { 60f, 60f, 60f, 18f }, 8f);
        Check("电量槽宽只随自身内容", t2[7], 18f);
        Check("长槽位不受短槽位影响", t2[6], 60f);

        // 三槽（截图场景）：21:24:21 · 0:00:26 · 0:00:26（分隔符已含两侧空格）
        float[] t3 = layout(new[] { 38f, 46f, 46f, 0f }, 8f);
        Check("三槽总宽", t3[0], 16f + 38f + 46f + 46f);

        // 左右留白对称
        float[] t4 = layout(new[] { 30f, 30f, 30f, 18f }, 8f);
        Check("末槽右边缘对称", t4[5] + 9f, t4[0] / 2f - ScreenPadX);

        Console.WriteLine();
        Console.WriteLine("=== 局内置底（临时改 Y/Z 偏移，不动配置） ===");
        // 复刻 ClockController 的偏移决策：置底同时施加 上下(Y) 与 前后(Z)
        const float BottomY = -3.4f;
        const float BottomZ = 2.4f;
        Func<bool, bool, float, float> offsetY = (inSong, bottom, cfgY) =>
            (inSong && bottom) ? BottomY : cfgY;
        Func<bool, bool, float, float> offsetZ = (inSong, bottom, cfgZ) =>
            (inSong && bottom) ? BottomZ : cfgZ;

        Check("局外不受开关影响（Y）", offsetY(false, true, 1.5f), 1.5f);
        Check("局外不受开关影响（Z）", offsetZ(false, true, 9f), 9f);
        Check("局内开启置底 → Y=-3.4", offsetY(true, true, 1.5f), -3.4f);
        Check("局内开启置底 → Z=2.4", offsetZ(true, true, 9f), 2.4f);
        Check("局内未开启 → 用用户 Y", offsetY(true, false, 1.5f), 1.5f);
        Check("局内未开启 → 用用户 Z", offsetZ(true, false, 9f), 9f);
        // 开关只影响本帧偏移，不写回配置
        float configuredY = 3.25f, configuredZ = -1.5f;
        float appliedY = offsetY(true, true, configuredY);
        float appliedZ = offsetZ(true, true, configuredZ);
        Check("置底后用户 Y 未被改写", configuredY, 3.25f);
        Check("置底后用户 Z 未被改写", configuredZ, -1.5f);
        Check("但实际应用的偏移已变为置底值", appliedY == BottomY && appliedZ == BottomZ, true);
        // 用户当前调好的数值正好等于置底值 → 局内开与不开表现一致（"把置底数值改成现在的数值"的直接效果）
        Check("配置=置底值时开关前后一致（Y）", offsetY(true, true, -3.4f), offsetY(true, false, -3.4f));
        Check("配置=置底值时开关前后一致（Z）", offsetZ(true, true, 2.4f), offsetZ(true, false, 2.4f));

        Console.WriteLine();
        Console.WriteLine("=== 局内时钟缩放（只作用于局内） ===");
        // 复刻 ClockController.EffectiveFontSize：缩放作用在字号上，布局全部由字号推导
        const float MinScale = 0.05f;
        Func<float, bool, float, float> effFont = (baseSize, inSong, scale) =>
        {
            float size = baseSize;
            if (inSong) { size *= Math.Max(MinScale, scale); }
            return Math.Max(1f, size);
        };
        Check("局外完全不受缩小影响", effFont(10.5f, false, 0.5f), 10.5f);
        Check("局外完全不受放大影响", effFont(10.5f, false, 2f), 10.5f);
        Check("局内默认 1.0 与局外同尺寸", effFont(10.5f, true, 1f), 10.5f);
        Check("局内缩小 0.5 → 字号减半", effFont(10.5f, true, 0.5f), 5.25f);
        Check("局内放大 1.5", effFont(10.5f, true, 1.5f), 15.75f);
        Check("缩放下限保护（0 不会把时钟缩没）", effFont(10.5f, true, 0f), 1f);
        Check("负数缩放走下限保护", effFont(10.5f, true, -3f), 1f);
        Check("字号下限 1 兜底（0.05 倍也不低于 1）", effFont(1f, true, 0.05f), 1f);
        Check("下限之上按倍率精确缩放", effFont(30f, true, 0.5f), 15f);
        Check("缩放只改字号、不改配置基准", effFont(10.5f, false, 0.5f), 10.5f);

        Console.WriteLine();
        Console.WriteLine("=== 槽位可见性（隐藏槽位不占宽度） ===");
        // 0=隐藏 → 不显示；其余内容 → 显示；电量取不到 → 空串 → 不显示
        Func<int, bool> contentVisible = c => c != 0;
        Func<bool, string, bool> slotVisible = (available, text) => text.Length > 0;
        Check("时钟1 当前时间", contentVisible(4), true);
        Check("时钟2 隐藏", contentVisible(0), false);
        Check("电量 可用", slotVisible(true, "100%"), true);
        Check("电量 取不到", slotVisible(false, ""), false);
        int visibleCount = 0;
        foreach (int c in new[] { 4, 0, 0 }) { if (contentVisible(c)) { visibleCount++; } }
        if (slotVisible(true, "100%")) { visibleCount++; }
        Check("默认配置可见槽位=2（时间+电量）", visibleCount, 2);

        Console.WriteLine();
        Console.WriteLine("=== ADB 重试间隔递增（避免设备离线时每 30s 白跑 adb） ===");
        // 复刻 AdbBattery.RetryIntervalTicks
        Func<int, int> retrySeconds = count =>
        {
            int seconds = 30;
            for (int i = 0; i < count && seconds < 180; i++) { seconds *= 2; }
            return seconds > 180 ? 180 : seconds;
        };
        Check("首次失败 30s", retrySeconds(0), 30);
        Check("第 2 次 60s", retrySeconds(1), 60);
        Check("第 3 次 120s（此后放弃轮询）", retrySeconds(2), 120);
        Check("上限封顶 180s", retrySeconds(9), 180);

        Console.WriteLine();
        Console.WriteLine("=== adb 路径缓存规则（只在找到真实文件时缓存） ===");
        // 复刻 ResolveAdbExecutable 的缓存决策
        Func<string, string, string, string> resolve = (configured, gameLocal, cached) =>
        {
            if (!string.IsNullOrEmpty(configured) && !configured.Equals("adb", StringComparison.OrdinalIgnoreCase))
            {
                return configured; // 显式配置：每次重新读，配置改了立刻生效
            }
            if (cached != null) { return cached; }
            return string.IsNullOrEmpty(gameLocal) ? "adb" : gameLocal;
        };
        Check("显式配置优先", resolve(@"D:\tools\adb.exe", @"C:\game\adb.exe", null), @"D:\tools\adb.exe");
        Check("配置改了立刻生效（不被缓存挡住）", resolve(@"D:\new\adb.exe", null, "adb"), @"D:\new\adb.exe");
        Check("游戏目录内置优先于 PATH", resolve("adb", @"C:\game\adb.exe", null), @"C:\game\adb.exe");
        Check("内置 adb 被缓存", resolve("adb", null, @"C:\game\adb.exe"), @"C:\game\adb.exe");
        Check("找不到时不缓存（退化为 PATH 查找）", resolve("adb", null, null), "adb");

        Console.WriteLine();
        Console.WriteLine("=== 分隔符分配（固定圆点，按第一个可见槽位） ===");
        // 复刻 ClockController.AssignSeparators（只标记是否需要分隔符）
        Func<string[], string[]> assign = displays =>
        {
            var r = new string[displays.Length];
            bool seen = false;
            for (int i = 0; i < displays.Length; i++)
            {
                bool visible = !string.IsNullOrEmpty(displays[i]);
                if (visible)
                {
                    r[i] = seen ? SlotSeparator : "";
                    seen = true;
                }
                else { r[i] = ""; }
            }
            return r;
        };
        string[] a1 = assign(new[] { "12:00", "", "", "100%" });
        Check("排头槽位不加分隔符", a1[0], "");
        Check("其后的可见槽位加圆点", a1[3], " · ");
        Check("隐藏槽位不带分隔符", a1[1], "");
        string[] a2 = assign(new[] { "", "00:05", "", "100%" });
        Check("时钟 1 隐藏时排头（时钟 2）不加", a2[1], "");
        Check("时钟 1 隐藏时电量仍加圆点", a2[3], " · ");
        string[] a3 = assign(new[] { "12:00", "", "", "" });
        Check("只剩一个槽位时无分隔符", a3[0], "");
        string[] a4 = assign(new[] { "", "", "", "100%" });
        Check("只有电量时无分隔符", a4[3], "");

        Console.WriteLine();
        Console.WriteLine("=== 游玩时长累计（只在歌曲内、排除暂停） ===");
        // 复刻 ClockController.AccumulatePlayTime
        Func<double, bool, bool, bool, double, double> accumulate =
            (playSeconds, prevInSong, nowInSong, paused, delta) =>
        {
            if (prevInSong && nowInSong)
            {
                if (delta > 0d && delta < 1.5d)
                {
                    playSeconds += delta * (paused ? 0d : 1d);
                }
            }
            return playSeconds;
        };
        Check("连续两帧在歌内 → 累加", accumulate(0d, true, true, false, 0.25d), 0.25d);
        Check("进入歌曲第一帧不累加（上帧在菜单）", accumulate(0d, false, true, false, 0.25d), 0d);
        Check("回到菜单后不累加", accumulate(10d, true, false, false, 0.25d), 10d);
        Check("菜单期间不累加", accumulate(5d, false, false, false, 0.25d), 5d);
        Check("暂停（timeScale=0）不累加", accumulate(5d, true, true, true, 0.25d), 5d);
        Check("异常大跳变（加载卡顿）不计入", accumulate(5d, true, true, false, 9d), 5d);
        // 累计 60 帧 × 0.25s = 15 秒
        double acc = 0d;
        for (int i = 0; i < 60; i++) { acc = accumulate(acc, true, true, false, 0.25d); }
        Check("60 帧累计 15 秒", acc, 15d);
        // 混合：20 秒歌曲 + 30 秒菜单 + 10 秒歌曲 = 30 秒游玩
        // 同一时间窗的"游戏启动总时长"（墙钟）应为 60.25 秒，两者必须不同
        double mixed = 0d;
        double wall = 0d;
        for (int i = 0; i < 80; i++) { mixed = accumulate(mixed, true, true, false, 0.25d); wall += 0.25d; }
        for (int i = 0; i < 120; i++) { mixed = accumulate(mixed, false, false, false, 0.25d); wall += 0.25d; }
        for (int i = 0; i < 1; i++) { mixed = accumulate(mixed, false, true, false, 0.25d); wall += 0.25d; } // 重新进歌首帧
        for (int i = 0; i < 40; i++) { mixed = accumulate(mixed, true, true, false, 0.25d); wall += 0.25d; }
        Check("20s 歌 + 30s 菜单 + 10s 歌 = 30s 游玩", mixed, 30d);
        Check("同窗口墙钟时长 = 60.25s", wall, 60.25d);
        Check("游玩时长明显小于游戏总时长", mixed < wall, true);

        Console.WriteLine();
        Console.WriteLine("=== 局外 / 局内 内容配置相互独立 ===");
        // 复刻 ClockConfig.GetContent(slot, inSong)
        int[] outside = { 4, 0, 0 };  // 局外：当前时间 / 隐藏 / 隐藏
        int[] inside = { 4, 3, 0 };   // 局内：当前时间 / 帧率 / 隐藏
        Func<int, bool, int> getContent = (slot, inSong) =>
            slot >= 3 ? 0 : (inSong ? inside[slot] : outside[slot]);
        Check("局外 槽位1 = 当前时间", getContent(0, false), 4);
        Check("局外 槽位2 = 隐藏", getContent(1, false), 0);
        Check("局内 槽位1 = 当前时间", getContent(0, true), 4);
        Check("局内 槽位2 = 帧率", getContent(1, true), 3);
        Check("同一槽位局内/局外取值不同", getContent(1, false) != getContent(1, true), true);
        Check("局外改动不影响局内", getContent(0, false) == getContent(0, true), true);
        // 槽位 4 恒为电量：两套配置都拿不到内容（由 AdbBattery 决定）
        Check("槽位4 局外不可配置", getContent(3, false), 0);
        Check("槽位4 局内不可配置", getContent(3, true), 0);
        // 写回互不串台
        Action<int[], int> setOutside = (arr, v) => arr[0] = v;
        setOutside(outside, 1);
        Check("写回局外后局内仍不变", getContent(0, true), 4);
        Check("写回局外后局外已变", getContent(0, false), 1);

        Console.WriteLine();
        Console.WriteLine($"===== PASS={_pass}  FAIL={_fail} =====");
        Environment.Exit(_fail == 0 ? 0 : 1);
    }
}
