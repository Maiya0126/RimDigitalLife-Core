using System;
using HarmonyLib;
using Verse;

namespace RimDigitalLife
{
    [HarmonyPatch(typeof(UIRoot), "UIRootUpdate")]
    public static class Patch_UIRootUpdate_RealTimeReminder
    {
        private static bool errorLogged = false;

        public static void Postfix()
        {
            try
            {
                RealTimeReminder.Update();
            }
            catch (Exception ex)
            {
                // 只记录一次，绝不每帧刷屏
                if (!errorLogged)
                {
                    errorLogged = true;
                    Log.Error($"[RimDigitalLife] RealTimeReminder 更新失败，已暂停健康提醒直至重启: {ex}");
                }
            }
        }
    }
}