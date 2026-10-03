using System;
using System.Linq;
using System.Reflection;

// ============================================================
// 加载编译好的 RainbowClock.dll，用反射核对它在运行时依赖的
// 游戏 API 是否真实存在（编译器只看签名，运行时看解析）。
// 不实例化 Unity 对象，因此可在游戏外运行。
// ============================================================
internal static class Program
{
    private static Assembly _mod;

    private static Type Find(string fullName)
    {
        Type t = _mod.GetType(fullName, false, false);
        if (t != null)
        {
            return t;
        }
        foreach (Assembly a in _mod.GetReferencedAssemblies().Select(TryLoad))
        {
            if (a == null)
            {
                continue;
            }
            t = a.GetType(fullName, false, false);
            if (t != null)
            {
                return t;
            }
        }
        return null;
    }

    private static Assembly TryLoad(AssemblyName name)
    {
        string[] dirs =
        {
            @"E:\SteamLibrary\steamapps\common\Beat Saber\Beat Saber_Data\Managed",
            @"E:\SteamLibrary\steamapps\common\Beat Saber\Plugins",
            @"E:\Download\Beat Saber 1.40.8 MOD Only Steam 20260117 - 副本\Plugins",
            // 0Harmony 不在 Plugins 而在整合包的 Libs 目录下
            @"E:\Download\Beat Saber 1.40.8 MOD Only Steam 20260117 - 副本\Libs"
        };
        foreach (string dir in dirs)
        {
            string path = System.IO.Path.Combine(dir, name.Name + ".dll");
            try { if (System.IO.File.Exists(path)) { return Assembly.LoadFrom(path); } }
            catch { }
        }
        return null;
    }

    private static void Check(string label, bool ok, string detail = "")
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {label}{(detail.Length > 0 ? "   " + detail : "")}");
    }

    private static void Main()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => TryLoad(new AssemblyName(e.Name));
        string modPath = @"E:\AIchat\Clock\Clock1.40.8\RainbowClock\bin\Release\RainbowClock.dll";
        _mod = Assembly.LoadFrom(modPath);
        Console.WriteLine($"mod: {_mod.GetName().Name} {_mod.GetName().Version}");
        Console.WriteLine();

        // --- ClockController 依赖的游戏 API ---
        Type audio = Find("AudioTimeSyncController");
        Check("AudioTimeSyncController 存在", audio != null, audio?.FullName);
        if (audio != null)
        {
            // 歌曲剩余时长 / 进度的数据来源
            Check("  .songTime", audio.GetProperty("songTime") != null);
            Check("  .songLength", audio.GetProperty("songLength") != null);
            Check("  .songEndTime", audio.GetProperty("songEndTime") != null);
            Check("  .isAudioLoaded", audio.GetProperty("isAudioLoaded") != null);
        }

        Type pdm = Find("PlayerDataModel");
        Check("PlayerDataModel 存在", pdm != null, pdm?.FullName);
        if (pdm != null)
        {
            PropertyInfo pd = pdm.GetProperty("playerData");
            Check("  .playerData", pd != null, pd?.PropertyType.Name);
            PropertyInfo pss = pd?.PropertyType.GetProperty("playerSpecificSettings");
            Check("  .playerSpecificSettings", pss != null, pss?.PropertyType.Name);
            PropertyInfo nth = pss?.PropertyType.GetProperty("noTextsAndHuds");
            Check("  .noTextsAndHuds", nth != null, nth?.PropertyType.Name);
        }

        Type lobby = Find("LobbySetupViewController");
        Check("LobbySetupViewController 存在", lobby != null, lobby?.FullName);

        // --- 愚人节补丁用字符串查找的字段 ---
        Type rvc = Find("ResultsViewController");
        Check("ResultsViewController 存在", rvc != null, rvc?.FullName);
        if (rvc != null)
        {
            FieldInfo lcr = rvc.GetField("_levelCompletionResults", BindingFlags.NonPublic | BindingFlags.Instance);
            Check("  _levelCompletionResults 字段名有效", lcr != null, lcr?.FieldType.Name);
            MethodInfo did = rvc.GetMethod("DidActivate", BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(bool), typeof(bool), typeof(bool) }, null);
            Check("  DidActivate(bool,bool,bool) 签名有效", did != null);
            if (lcr != null)
            {
                FieldInfo state = lcr.FieldType.GetField("levelEndStateType",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                Check("  levelEndStateType 字段名有效", state != null, state?.FieldType.Name);
                if (state != null && state.FieldType.IsEnum)
                {
                    string[] names = Enum.GetNames(state.FieldType);
                    Console.WriteLine($"        {state.FieldType.Name} 取值: {string.Join(", ", names)}");
                    Check("  Failed == 2（补丁按 2 判断）", names.Length > 2 && names[2] == "Failed", $"index2={names.ElementAtOrDefault(2)}");
                }
            }
        }

        // --- AdbBattery / 设置页依赖的 BSML API ---
        Type ddl = Find("BeatSaberMarkupLanguage.Components.Settings.DropDownListSetting");
        Check("DropDownListSetting 存在", ddl != null);
        if (ddl != null)
        {
            Check("  .UpdateChoices()", ddl.GetMethod("UpdateChoices") != null);
            Check("  .Value 可写", ddl.GetProperty("Value")?.CanWrite == true);
            Check("  .ApplyValue()（用于不触发 on-change 刷新）", ddl.GetMethod("ApplyValue") != null);
        }

        Type fs = Find("BeatSaberMarkupLanguage.FloatingScreen.FloatingScreen");
        if (fs != null)
        {
            MethodInfo create = fs.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "CreateFloatingScreen" && m.GetParameters().Length == 6);
            Check("FloatingScreen.CreateFloatingScreen(6 参数)", create != null);
            if (create != null)
            {
                ParameterInfo last = create.GetParameters()[5];
                Check("  第 6 参数名为 hasBackground（mod 传 false）", last.Name == "hasBackground", last.Name);
            }
        }

        Type hint = Find("HMUI.HoverHint");
        Check("HMUI.HoverHint.text 可写", hint?.GetProperty("text")?.CanWrite == true);

        Type bsmSettings = Find("BeatSaberMarkupLanguage.Settings.BSMLSettings");
        Check("BSMLSettings.RemoveSettingsMenu(Object)",
            bsmSettings?.GetMethod("RemoveSettingsMenu", new[] { typeof(object) }) != null);

        // --- Harmony：_harmony.PatchAll(asm) 是否会把补丁归属到该实例 ID ---
        Type harmony = Find("HarmonyLib.Harmony");
        Check("HarmonyLib.Harmony 存在", harmony != null);
        if (harmony != null)
        {
            Check("  .PatchAll(Assembly) 存在", harmony.GetMethod("PatchAll", new[] { typeof(Assembly) }) != null);
            Check("  .UnpatchSelf() 存在", harmony.GetMethod("UnpatchSelf") != null);
            Check("  .Id 可读（确认实例身份）", harmony.GetProperty("Id")?.CanRead == true);
            Check("  new Harmony(string) 存在", harmony.GetConstructor(new[] { typeof(string) }) != null);
        }

        // --- 本地化表：代码里用到的 key 必须都在表里 ---
        Type loc = _mod.GetType("RainbowClock.Loc");
        Type configType = _mod.GetType("RainbowClock.ClockConfig");
        Check("RainbowClock.Loc 存在", loc != null);
        Check("RainbowClock.ClockConfig 存在", configType != null);
    }
}
