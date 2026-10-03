using System;
using System.Linq;
using System.Reflection;

// ============================================================
// 反射探针：打印游戏/BSML 程序集里的真实 API 成员，
// 用来确认代码里用到的成员（属性/字段/方法）确实存在、类型一致。
// 只做反射，不实例化任何 Unity 对象，因此可以在游戏外运行。
// ============================================================
internal static class Program
{
    private static readonly System.Collections.Generic.HashSet<string> Attempted =
        new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static readonly System.Collections.Generic.HashSet<string> Resolving =
        new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 从游戏目录解析依赖程序集（反射探针在游戏外运行，需要手动挂解析器）。
    /// 只对"正在加载中"的同名程序集做防重入（从 Resolving 移除后允许重试），
    /// 失败不永久拉黑，否则后续请求会一直拿到 null。
    /// </summary>
    private static void InstallResolver()
    {
        string[] dirs =
        {
            @"E:\SteamLibrary\steamapps\common\Beat Saber\Beat Saber_Data\Managed",
            @"E:\SteamLibrary\steamapps\common\Beat Saber\Plugins",
            @"E:\Download\Beat Saber 1.40.8 MOD Only Steam 20260117 - 副本\Plugins",
            @"E:\Download\Beat Saber 1.40.8 MOD Only Steam 20260117 - 副本\Libs"
        };
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            string simple = new AssemblyName(args.Name).Name;
            if (!Resolving.Add(simple))
            {
                return null; // 正在加载同名程序集 → 拒绝，避免递归
            }
            try
            {
                foreach (string dir in dirs)
                {
                    string path = System.IO.Path.Combine(dir, simple + ".dll");
                    try
                    {
                        if (System.IO.File.Exists(path))
                        {
                            return Assembly.LoadFrom(path);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"  [resolve] {simple} 加载失败: {e.GetType().Name}");
                    }
                }
                return null;
            }
            finally
            {
                Resolving.Remove(simple);
            }
        };
    }

    private static void Dump(Type type)
    {
        if (type == null)
        {
            Console.WriteLine("  <type not found>");
            return;
        }
        Console.WriteLine($"=== {type.FullName} ===");
        Console.WriteLine("--- properties ---");
        foreach (PropertyInfo p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            Console.WriteLine($"  {p.PropertyType.Name,-28} {p.Name}{(p.CanWrite ? "  { set; }" : "")}");
        }
        Console.WriteLine("--- fields ---");
        foreach (FieldInfo f in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            Console.WriteLine($"  {f.FieldType.Name,-28} {f.Name}");
        }
        Console.WriteLine("--- declared methods ---");
        foreach (MethodInfo m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                     .OrderBy(m => m.Name))
        {
            Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name + " " + x.Name))})");
        }
        Console.WriteLine();
    }

    private static void Main()
    {
        InstallResolver();
        try
        {
            Run();
        }
        catch (Exception e)
        {
            Console.WriteLine("PROBE FAILED: " + e.GetType().FullName + ": " + e.Message);
            if (e is ReflectionTypeLoadException rtle && rtle.LoaderExceptions != null)
            {
                foreach (Exception inner in rtle.LoaderExceptions)
                {
                    Console.WriteLine("  -> " + (inner == null ? "<null>" : inner.Message));
                }
            }
        }
    }

    /// <summary>
    /// 所有引用 BSML/游戏类型的代码都在这个方法里：JIT 编译 Main 时不会解析这些程序集，
    /// 因此 InstallResolver 能先装好解析器再触发加载。
    /// </summary>
    private static void Run()
    {
        // ---- 歌曲实时时长/进度的可用 API（用于"歌曲剩余时长""歌曲当前百分比"） ----
        Console.WriteLine("=== AudioTimeSyncController 成员 ===");
        Dump(typeof(AudioTimeSyncController));
        Console.WriteLine("=== IAudioTimeSource 成员（AudioTimeSyncController 实现的接口） ===");
        Type ats = typeof(AudioTimeSyncController).GetInterfaces()
            .FirstOrDefault(i => i.Name.Contains("AudioTimeSource"));
        Dump(ats);

        // 确认是否存在 songLength / songEndTime / songTime
        Console.WriteLine("=== 关键成员逐个确认 ===");
        foreach (string name in new[] { "songTime", "songEndTime", "songLength", "timeScale", "isAudioPaused" })
        {
            PropertyInfo p = typeof(AudioTimeSyncController).GetProperty(name);
            FieldInfo f = typeof(AudioTimeSyncController).GetField(name);
            Type memberType = p != null ? p.PropertyType : (f != null ? f.FieldType : null);
            Console.WriteLine($"  {name,-16} {(memberType != null ? "存在 " + memberType.Name : "不存在")}");
        }
        Console.WriteLine();

        // ---- 决定性验证：直接读取 BSMLParser 的标签注册表，确认 "text" 已注册 ----
        Console.WriteLine("=== BSML 标签注册表（决定 BSML 文件里能用哪些标签） ===");
        try
        {
            // 触发静态构造，注册表才被填充
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
                typeof(BeatSaberMarkupLanguage.BSMLParser).TypeHandle);
        }
        catch (Exception e)
        {
            Console.WriteLine($"  BSMLParser 静态构造失败（游戏外常见，可忽略）: {e.GetType().Name}: {e.Message}");
        }

        bool dumpedAny = false;
        foreach (FieldInfo f in typeof(BeatSaberMarkupLanguage.BSMLParser)
                     .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (!typeof(System.Collections.IDictionary).IsAssignableFrom(f.FieldType))
            {
                continue;
            }
            try
            {
                var dict = f.GetValue(null) as System.Collections.IDictionary;
                if (dict == null || dict.Count == 0)
                {
                    continue;
                }
                var keys = new System.Collections.Generic.List<string>();
                foreach (object k in dict.Keys) { keys.Add(Convert.ToString(k)); }
                keys.Sort(StringComparer.Ordinal);
                Console.WriteLine($"  字段 {f.Name}（{keys.Count} 项）:");
                Console.WriteLine("    " + string.Join(", ", keys));
                dumpedAny = true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"  读取字段 {f.Name} 失败: {e.GetType().Name}");
            }
        }
        if (!dumpedAny)
        {
            Console.WriteLine("  (未能读到注册表字段——静态构造可能在游戏外未完成)");
        }
        Console.WriteLine();

        // 本次 BSML 里用到的标签是否都已注册
        string[] usedTags = { "bg", "vertical", "dropdown-list-setting", "toggle-setting",
                              "increment-setting", "color-setting", "button", "text" };
        Console.WriteLine("  本模组用到的标签: " + string.Join(", ", usedTags));
        Console.WriteLine();

        // FloatingScreen 的静态工厂方法签名（mod 里调用的是 6 参数版本）
        Console.WriteLine("=== FloatingScreen static factories ===");
        foreach (MethodInfo m in typeof(BeatSaberMarkupLanguage.FloatingScreen.FloatingScreen)
                     .GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name + " " + x.Name))})");
        }
        Console.WriteLine();

        // Harmony 的 PatchAll / UnpatchSelf 重载
        Type harmony = Type.GetType("HarmonyLib.Harmony, 0Harmony", false)
            ?? typeof(BeatSaberMarkupLanguage.BSMLParser).Assembly.GetType("HarmonyLib.Harmony", false, false);
        if (harmony == null)
        {
            try { harmony = Assembly.LoadFrom(@"E:\Download\Beat Saber 1.40.8 MOD Only Steam 20260117 - 副本\Libs\0Harmony.dll").GetType("HarmonyLib.Harmony"); }
            catch { }
        }
        if (harmony != null)
        {
            Console.WriteLine("=== HarmonyLib.Harmony methods ===");
            foreach (MethodInfo m in harmony.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                         .Where(m => m.Name.Contains("Patch") || m.Name.Contains("Unpatch")).OrderBy(m => m.Name))
            {
                Console.WriteLine($"  {(m.IsStatic ? "static " : "")}{m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name + " " + x.Name))})");
            }
            Console.WriteLine("=== HarmonyLib.Harmony properties / ctors ===");
            foreach (PropertyInfo p in harmony.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                Console.WriteLine($"  {p.PropertyType.Name,-24} {p.Name}  get={p.CanRead} set={p.CanWrite}");
            }
            foreach (ConstructorInfo c in harmony.GetConstructors())
            {
                Console.WriteLine($"  ctor({string.Join(", ", c.GetParameters().Select(x => x.ParameterType.Name + " " + x.Name))})");
            }
            Console.WriteLine();
        }

        // ---- 游戏侧 API 假设核对 ----
        Console.WriteLine("=== ResultsViewController 字段（愚人节补丁用反射取） ===");
        foreach (FieldInfo f in typeof(ResultsViewController).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
        {
            Console.WriteLine($"  {f.FieldType.Name} {f.Name}");
        }
        Console.WriteLine();

        Console.WriteLine("=== PlayerDataModel.playerData 链路 ===");
        Type pdm = typeof(ResultsViewController).Assembly.GetType("PlayerDataModel", false, false);
        Console.WriteLine($"  PlayerDataModel type: {pdm?.FullName ?? "<missing>"}");
        PropertyInfo pd = pdm?.GetProperty("playerData", BindingFlags.Public | BindingFlags.Instance);
        Console.WriteLine($"  PlayerDataModel.playerData : {pd?.PropertyType.FullName ?? "<missing>"}");
        if (pd != null)
        {
            PropertyInfo pss = pd.PropertyType.GetProperty("playerSpecificSettings", BindingFlags.Public | BindingFlags.Instance);
            Console.WriteLine($"  .playerSpecificSettings    : {pss?.PropertyType.FullName ?? "<missing>"}");
            if (pss != null)
            {
                PropertyInfo nth = pss.PropertyType.GetProperty("noTextsAndHuds", BindingFlags.Public | BindingFlags.Instance);
                Console.WriteLine($"  .noTextsAndHuds             : {nth?.PropertyType.Name ?? "<missing>"}");
            }
        }
        Console.WriteLine();

        Dump(typeof(BeatSaberMarkupLanguage.Components.Settings.DropDownListSetting));        Dump(typeof(BeatSaberMarkupLanguage.Components.Settings.IncDecSetting));
        Dump(typeof(BeatSaberMarkupLanguage.Components.Settings.ToggleSetting));
        Dump(typeof(BeatSaberMarkupLanguage.Components.Settings.ColorSetting));
        Dump(typeof(HMUI.HoverHint));
        Dump(typeof(BeatSaberMarkupLanguage.Settings.BSMLSettings));
        Dump(typeof(BeatSaberMarkupLanguage.FloatingScreen.FloatingScreen));

        // Utilities 用的是 mod 里同样的 using（BeatSaberMarkupLanguage.Util）
        Type util = typeof(BeatSaberMarkupLanguage.BSMLParser).Assembly
            .GetType("BeatSaberMarkupLanguage.Util.Utilities", false, false);
        Dump(util);

        // BeatSaberUI 的静态方法表
        Type ui = typeof(BeatSaberMarkupLanguage.BSMLParser).Assembly
            .GetType("BeatSaberMarkupLanguage.BeatSaberUI", false, false);
        if (ui != null)
        {
            Console.WriteLine($"=== {ui.FullName} === (static)");
            foreach (MethodInfo m in ui.GetMethods(BindingFlags.Public | BindingFlags.Static).OrderBy(m => m.Name))
            {
                Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name + " " + x.Name))})");
            }
        }
        else
        {
            Console.WriteLine("BeatSaberUI: <not found>");
        }
    }
}
