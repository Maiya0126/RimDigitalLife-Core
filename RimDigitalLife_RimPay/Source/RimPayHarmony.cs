using System.Collections.Generic;
using RimWorld;
using Verse;
using HarmonyLib;

namespace RimDigitalLife_RimPay
{
    [StaticConstructorOnStartup]
    public static class RimPayHarmony
    {
        static RimPayHarmony()
        {
            var harmony = new Harmony("maiya.RimDigitalLife.RimPay");
            harmony.PatchAll();
            Log.Message("[RimDigitalLife_RimPay] Harmony patches applied.");

            // RimSim 商店联动（内部先检查 RimSim 是否激活，未激活自动休眠）
            try
            {
                RimSimIntegration.TryInit(harmony);
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[RimDigitalLife_RimPay] RimSim 联动初始化异常: {ex.Message}");
            }
        }
    }
}