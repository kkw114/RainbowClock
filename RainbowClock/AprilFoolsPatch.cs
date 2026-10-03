using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RainbowClock
{
    /// <summary>
    /// 愚人节彩蛋：4 月 1 日失败结算时随机嘲讽（与 Quest 版一致）。
    /// </summary>
    public static class AprilFoolsPatch
    {
        private static Harmony _harmony;

        public static void Apply()
        {
            try
            {
                if (_harmony != null)
                {
                    return;
                }
                if (!IsAprilFools())
                {
                    return;
                }
                _harmony = new Harmony("com.rainbowclock.aprilfools");
                // 必须用实例调用 PatchAll：无参/无接收者的 PatchAll 会把补丁归属到一个
                // 自动生成的 Harmony ID（由调用方程序集推导），随后 UnpatchSelf() 就摘不掉它们。
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                Plugin.Log?.Info("[RainbowClock] April fools patch installed (id=" + _harmony.Id + ").");
            }
            catch (Exception e)
            {
                Plugin.Log?.Error("[RainbowClock] Failed to install april fools patch: " + e);
            }
        }

        public static void Remove()
        {
            try
            {
                _harmony?.UnpatchSelf();
                _harmony = null;
            }
            catch
            {
                // ignore
            }
        }

        public static bool IsAprilFools()
        {
            DateTime now = DateTime.Now;
            return (now.Month == 3 && now.Day == 31) || (now.Month == 4 && now.Day == 1);
        }
    }

    [HarmonyPatch(typeof(ResultsViewController), "DidActivate")]
    [HarmonyPatch(new Type[] { typeof(bool), typeof(bool), typeof(bool) })]
    internal static class ResultsViewControllerDidActivatePatch
    {
        private static readonly System.Random Rng = new System.Random();

        private static void Postfix(ResultsViewController __instance)
        {
            try
            {
                if (!AprilFoolsPatch.IsAprilFools())
                {
                    return;
                }

                FieldInfo field = AccessTools.Field(typeof(ResultsViewController), "_levelCompletionResults");
                if (field == null)
                {
                    return;
                }
                object results = field.GetValue(__instance);
                if (results == null)
                {
                    return;
                }

                FieldInfo stateField = AccessTools.Field(results.GetType(), "levelEndStateType");
                if (stateField == null)
                {
                    return;
                }
                int state = (int)stateField.GetValue(results);
                if (state != 2) // LevelEndStateType.Failed
                {
                    return;
                }

                string[] texts = Loc.FailTexts();
                ClockController.ShowMessage(texts[Rng.Next(texts.Length)], 8);
            }
            catch (Exception e)
            {
                Plugin.Log?.Error("[RainbowClock] ResultsHook: " + e);
            }
        }
    }
}
