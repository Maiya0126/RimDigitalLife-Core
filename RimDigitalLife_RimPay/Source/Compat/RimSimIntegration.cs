using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimDigitalLife_RimPay
{
    // ========================================================================
    // RimPay × RimSimManagementFramework 商店联动（纯反射版，零 RSMF 类型引用）
    //
    // 重要：RimWorld 加载 mod 程序集时会 Assembly.GetTypes() 枚举全部类型，
    // 任何"继承 RSMF 类型"的类都会在类型枚举阶段触发 TypeLoadException，
    // 导致整个 RimPay 程序集加载失败（Gizmo/Harmony 全灭）。
    // 因此本文件绝不引用/继承任何 RSMF 类型，改为 Harmony 手动 patch
    // RSMF 的 static API 方法（AccessTools 字符串定位，未装 RSMF 时自动跳过）。
    //
    // 挂点（签名已用 ReflectionOnly 反射核对）：
    // - SimShopCheckoutApi.ModifyPaidSilver(ShopCheckoutContext, int) → postfix 折扣
    // - SimShopFinanceApi.CommitCheckout(Pawn customer, Building_CashRegister register, int paidSilver)
    //   → postfix 店铺收入入国库
    // ========================================================================
    public static class RimSimIntegration
    {
        private const string RimSimPackageId = "chezhou.Framework.RimSimManagementFramework";

        private static bool initialized = false;
        private static bool warnedNoSilver = false;

        // RimPayHarmony 静态构造时调用；本方法签名不含任何 RSMF 类型，缺 DLL 时 JIT 安全
        // 实现说明：走纯 System.Reflection 定位类型/方法，再经 HarmonyMethod 包装 patch——
        // 避免 net472 编译期绑定 Harmony 3.0（1.7）的 .NET 10 签名（CS1705），1.6/1.7 双版本共用。
        public static void TryInit(Harmony harmony)
        {
            if (initialized) return;
            try
            {
                if (ModLister.GetActiveModWithIdentifier(RimSimPackageId, true) == null) return;

                // 字符串定位 RSMF 类型（未加载时返回 null，自动跳过，绝不炸主程序集）
                Type modifyType = FindType("SimManagementLib.Api.SimShopCheckoutApi");
                Type financeType = FindType("SimManagementLib.Api.SimShopFinanceApi");
                if (modifyType == null || financeType == null)
                {
                    Verse.Log.Message("[RimPay] RimSimManagementFramework assembly not loaded yet, store integration skipped.");
                    return;
                }

                MethodInfo modify = modifyType.GetMethod("ModifyPaidSilver", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                MethodInfo commit = financeType.GetMethod("CommitCheckout", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (modify != null)
                {
                    PatchViaReflection(harmony, modify, nameof(ModifyPaidSilverPostfix));
                }
                if (commit != null)
                {
                    PatchViaReflection(harmony, commit, nameof(CommitCheckoutPostfix));
                }

                initialized = true;
                Verse.Log.Message("[RimPay] RimSimManagementFramework store integration enabled (checkout discount + income to treasury).");
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[RimPay] RimSim integration init failed: {ex.Message}");
            }
        }

        // 纯反射调用 harmony.Patch(original, null, postfix, null, null)。
        // 原因：Harmony 3.0（1.7）引用 System.Runtime 10，net472 编译期绑定其成员签名会 CS1705；
        // 反射在运行时解析签名，1.6(Harmony 2.4)/1.7(Harmony 3.0) 双版本共用同一份程序集。
        // 两代 Harmony 的 Patch(MethodBase, HarmonyMethod x4) 与 HarmonyMethod(MethodInfo) 签名一致，反射兼容。
        private static void PatchViaReflection(Harmony harmony, MethodBase original, string postfixName)
        {
            MethodInfo postfix = typeof(RimSimIntegration).GetMethod(postfixName, BindingFlags.NonPublic | BindingFlags.Static);
            if (postfix == null) return;
            Type hmType = typeof(Harmony).Assembly.GetType("HarmonyLib.HarmonyMethod");
            object hm = Activator.CreateInstance(hmType, new object[] { postfix });
            harmony.GetType()
                .GetMethod("Patch", new[] { typeof(MethodBase), hmType, hmType, hmType, hmType })
                .Invoke(harmony, new object[] { original, null, hm, null, null });
        }

        // 跨程序集按全名找类型（等价 AccessTools.TypeByName，无 Harmony 编译期绑定）
        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType(fullName, throwOnError: false);
                if (t != null) return t;
            }
            return null;
        }

        // ======== 折扣：修改顾客实付（数字支付生态促销） ========
        // postfix 签名只含 Verse/RimWorld 类型，RimPay 程序集类型枚举安全
        private static void ModifyPaidSilverPostfix(ref int __result)
        {
            try
            {
                var S = RimPayMod.settings;
                if (S == null || !S.enableRimSimIntegration) return;
                if (__result <= 0) return;
                float disc = GetCurrentDiscount(Find.CurrentMap);
                if (disc <= 0f) return;
                int reduced = Mathf.RoundToInt(__result * (1f - disc));
                __result = Mathf.Max(1, reduced);
            }
            catch { }
        }

        // ======== 入国库：结账提交后按比例转销实付白银 ========
        // 注意：Building_CashRegister 是 RSMF 类型，postfix 参数用其基类 Verse.Building 接收
        private static void CommitCheckoutPostfix(Pawn customer, Building register, int paidSilver)
        {
            try
            {
                var S = RimPayMod.settings;
                if (S == null || !S.enableRimSimIntegration) return;
                var comp = Current.Game?.GetComponent<GameComponent_RimPay>();
                if (comp == null || register == null || register.Map == null || paidSilver <= 0) return;

                int target = Mathf.RoundToInt(paidSilver * Mathf.Clamp01(S.rimSimTreasuryRatio));
                if (target <= 0) return;

                int collected = CollectAndTransferSilver(register.Map, register.Position, 15f, target);
                if (collected > 0)
                {
                    comp.ModifyTreasury(collected, "RP_Reason_StoreIncome".Translate());
                    comp.RecordStoreSale(collected);
                }
                else if (!warnedNoSilver)
                {
                    warnedNoSilver = true;
                    Verse.Log.Message("[RimPay] RimSim integration: no physical silver found near cash register after checkout, nothing sent to treasury this time (no coin minting).");
                }
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[RimPay] RimSim checkout integration exception: {ex.Message}");
            }
        }

        // ======== 折扣档位判定 ========
        // 当前顾客折扣（殖民地数字化档位，取两档最大值）
        public static float GetCurrentDiscount(Map map)
        {
            var S = RimPayMod.settings;
            if (S == null || !S.enableRimSimIntegration) return 0f;
            if (map == null) return 0f;

            float disc = 0f;
            if (ColonyHasDigitalDeviceOwner(map))
                disc = Mathf.Max(disc, Mathf.Clamp01(S.rimSimDeviceDiscount));
            if (ColonyHasQuantumSubscriber(map))
                disc = Mathf.Max(disc, Mathf.Clamp01(S.rimSimQuantumDiscount));
            return disc;
        }

        // 档1：殖民地有殖民者佩戴 RimDigitalLife 数码设备
        private static bool ColonyHasDigitalDeviceOwner(Map map)
        {
            try
            {
                foreach (Pawn p in map.mapPawns.FreeColonists)
                {
                    if (p == null || p.Dead || p.apparel == null) continue;
                    foreach (var app in p.apparel.WornApparel)
                    {
                        if (app.def.apparel?.tags == null) continue;
                        if (app.def.apparel.tags.Contains("DigitalStorage_TerminalAccess") ||
                            app.def.apparel.tags.Contains("RimDigital_WatchDevice") ||
                            app.def.defName.Contains("iShen") ||
                            app.def.defName.Contains("Nokirim"))
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        // 档2：殖民地有殖民者订阅量子网络付费套餐（反射调用 QuantumNet，未安装则不生效）
        private static bool ColonyHasQuantumSubscriber(Map map)
        {
            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "RimDigitalLife_QuantumNet");
                var compType = asm?.GetType("RimDigitalLife_QuantumNet.GameComponent_QuantumNet");
                if (compType == null) return false;
                var comp = Current.Game?.GetComponent(compType);
                if (comp == null) return false;
                var hasPaid = compType.GetMethod("HasPaidPlan");
                if (hasPaid == null) return false;

                foreach (Pawn p in map.mapPawns.FreeColonists)
                {
                    if (p == null || p.Dead) continue;
                    if (hasPaid.Invoke(comp, new object[] { p }) is bool paid && paid) return true;
                }
            }
            catch { }
            return false;
        }

        // ======== 白银收集：只销实体银，销多少入多少 ========
        private static int CollectAndTransferSilver(Map map, IntVec3 center, float radius, int maxAmount)
        {
            int remaining = maxAmount;
            int collected = 0;
            var stacks = map.listerThings.ThingsOfDef(ThingDefOf.Silver)
                .Where(t => t != null && t.Spawned && t.Position.InHorDistOf(center, radius))
                .OrderBy(t => t.Position.DistanceTo(center))
                .ToList();

            foreach (Thing stack in stacks)
            {
                if (remaining <= 0) break;
                int take = Mathf.Min(stack.stackCount, remaining);
                Thing piece = stack.SplitOff(take);
                if (piece != null)
                {
                    piece.Destroy();
                    collected += take;
                    remaining -= take;
                }
            }
            map.resourceCounter.UpdateResourceCounts();
            return collected;
        }
    }
}
