using System;

// 对比各版本的行宽与槽位空隙，用于调参时快速复核（字号 8，字符宽度按 TMP 估算）。
internal static class Program
{
    private static float Ch(char c)
    {
        if (char.IsDigit(c)) return 4.45f;
        if (c == ':') return 2.4f;
        if (c == '%') return 5.0f;
        if (c == ' ') return 2.2f;
        if (c == '|' || c == '/') return 2.6f;
        if (c == '·') return 2.4f;
        return 4.4f;
    }

    private static float Width(string s)
    {
        float w = 0f;
        foreach (char c in s) w += Ch(c);
        return w;
    }

    private const float ScreenPadX = 8f;
    private const float SlotPadX = 1.5f;
    private const float FontSize = 8f;
    /// <summary>当前参数：槽位空隙 = 字号 × 该系数。</summary>
    private const float GapFactor = 0.25f;
    private const string Separator = "·";

    /// <summary>最早版本：等宽槽位 + 硬编码 22 间距 + 分隔符自带两侧空格。</summary>
    private static float EqualWidth(string[] texts)
    {
        float slot = 22f;
        int n = 0;
        foreach (string t in texts)
        {
            if (t.Length == 0) continue;
            slot = Math.Max(slot, Width("  " + Separator + "  " + t) * 1.04f + 2f);
            n++;
        }
        float gap = 22f + FontSize * 0.9f;
        return ScreenPadX * 2f + slot * n + gap * (n - 1);
    }

    /// <summary>按内容测量 + 指定空隙系数（用于对比 0.7 / 0.25）。</summary>
    private static float PerContent(string[] texts, float gapFactor)
    {
        float gap = FontSize * gapFactor;
        float total = ScreenPadX * 2f;
        int n = 0;
        bool seen = false;
        foreach (string t in texts)
        {
            if (t.Length == 0) continue;
            total += Math.Max(2f, Width((seen ? Separator : "") + t)) + SlotPadX * 2f;
            seen = true;
            n++;
        }
        return total + gap * (n - 1);
    }

    private static void Report(string label, string[] texts)
    {
        float eq = EqualWidth(texts);
        float g07 = PerContent(texts, 0.7f);
        float now = PerContent(texts, GapFactor);
        Console.WriteLine($"{label,-28} 等宽旧版={eq,7:F1}  空隙0.7={g07,7:F1}  当前={now,7:F1}");
    }

    private static void Main()
    {
        Console.WriteLine("=== 整行宽度（字号 8，仅圆点分隔符） ===");
        Report("时间+电量（默认）", new[] { "12:34", "", "", "100%" });
        Report("时间+秒+电量", new[] { "12:34:56", "", "", "100%" });
        Report("三个时钟", new[] { "20:34:50", "0:01:25", "FPS 175", "" });
        Report("三个时钟+电量", new[] { "20:34:50", "0:01:25", "FPS 175", "100%" });

        Console.WriteLine();
        Console.WriteLine("=== 槽位间可见空隙（数字 → 圆点 → 数字） ===");
        Console.WriteLine($"  等宽旧版:  约 30.0（等宽死空白约 8.0×2 + 22 硬编码间距）");
        Console.WriteLine($"  上一版:    {FontSize * 0.7f:F1}（字号×0.7）");
        Console.WriteLine($"  当前:      {FontSize * GapFactor:F1}（字号×0.25，贴紧）");
        Console.WriteLine($"  相比等宽旧版收窄 {100f * (1f - (FontSize * GapFactor) / 30f):F1}%");
        Console.WriteLine();
        Console.WriteLine("  圆点画在后一个槽位内容的左侧，这段空隙全部落在圆点左边，");
        Console.WriteLine("  效果为「20:34:50 ·0:01:25」—— 圆点紧贴右侧数字。");
        Console.WriteLine("  调参旋钮：ClockController.SlotGapFactor（当前 0.25）。");
    }
}
