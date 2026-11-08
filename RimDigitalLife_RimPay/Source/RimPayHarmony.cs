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
                Log.Warning($"[RimPay] RimSim integration init failed: {ex.Message}");
            }

            // RimMarket 边缘市场联动（内部先检查 RimMarket 是否激活，未激活自动休眠）
            try
            {
                RimMarketIntegration.TryInit(harmony);
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[RimPay] RimMarket integration init failed: {ex.Message}");
            }

            // Bunnygirl Race 赌场联动（未安装自动休眠）
            try
            {
                BunnygirlIntegration.TryInit(harmony);
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[RimPay] Bunnygirl integration init failed: {ex.Message}");
            }

            // Hospitality 访客联动（未安装自动休眠）
            try
            {
                HospitalityIntegration.TryInit(harmony);
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[RimPay] Hospitality integration init failed: {ex.Message}");
            }

            // RimStock 边缘股市联动（未安装自动休眠）
            try
            {
                RimStockIntegration.TryInit(harmony);
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[RimPay] RimStock integration init failed: {ex.Message}");
            }
        }
    }
}