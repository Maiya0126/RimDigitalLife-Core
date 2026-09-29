using System.Collections.Generic;
using RimDigitalLife;
using RimWorld;
using Verse;

namespace RimDigitalLife_QuantumNet
{
    // ============================================================
    // RimSeek 智算办公 Buff 管理（Phase 3 + 运营商事件）
    // 小人携带任意数码设备 + 已订阅付费套餐 + 网络覆盖 → 获得效率加成
    // - 高速畅享包：全局工作 +10%、科研 +15%
    // - 量子无限包：全局工作 +20%、科研 +20%（防分心由 Patch_QuantumNet_AntiDistraction 抵消 Core -5%）
    // 运营商事件：
    // - 网络拥堵事件：所有付费用户降级为「拥堵版」低强度加成（+5%/+7%）
    // - 星链风暴（网络中断）/无覆盖：Buff 失效（停机降级）
    // ============================================================
    public static class RimSeekBuffManager
    {
        public const string HighSpeedHediff = "QuantumNet_RimSeek_HighSpeed";
        public const string InfiniteHediff = "QuantumNet_RimSeek_Infinite";
        public const string CongestedHediff = "QuantumNet_RimSeek_Congested";

        // 由 GameComponent 低频驱动（每 500 tick）
        public static void Tick()
        {
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            var comp = GameComponent_QuantumNet.Instance;
            if (comp == null) return;
            var S = QuantumNetMod.settings;
            if (S == null || !S.enableRimSeek) return;

            bool coverage = comp.HasNetworkCoverage(map);
            bool congested = NetworkEventManager.RimSeekCongested;

            foreach (Pawn pawn in map.mapPawns.FreeColonists)
            {
                if (pawn.Dead || pawn.Destroyed) continue;
                UpdateHediff(pawn, coverage, congested);
            }
        }

        private static void UpdateHediff(Pawn pawn, bool coverage, bool congested)
        {
            bool hasDevice = HasDigitalDevice(pawn);
            QuantumPlan plan = GameComponent_QuantumNet.Instance != null
                ? GameComponent_QuantumNet.Instance.GetPawnPlan(pawn)
                : QuantumPlan.Local;

            HediffDef hs = DefDatabase<HediffDef>.GetNamedSilentFail(HighSpeedHediff);
            HediffDef inf = DefDatabase<HediffDef>.GetNamedSilentFail(InfiniteHediff);
            HediffDef con = DefDatabase<HediffDef>.GetNamedSilentFail(CongestedHediff);
            if (hs == null || inf == null || con == null) return;

            // 停机降级：无覆盖/无设备/非付费套餐 → 移除全部 Buff
            bool active = hasDevice && coverage && plan != QuantumPlan.Local;
            if (!active)
            {
                RemoveIfPresent(pawn, hs);
                RemoveIfPresent(pawn, inf);
                RemoveIfPresent(pawn, con);
                return;
            }

            if (congested)
            {
                // 拥堵：所有付费用户统一降级为拥堵版
                RemoveIfPresent(pawn, hs);
                RemoveIfPresent(pawn, inf);
                AddIfAbsent(pawn, con);
            }
            else if (plan == QuantumPlan.Infinite)
            {
                RemoveIfPresent(pawn, hs);
                RemoveIfPresent(pawn, con);
                AddIfAbsent(pawn, inf);
            }
            else // HighSpeed
            {
                RemoveIfPresent(pawn, inf);
                RemoveIfPresent(pawn, con);
                AddIfAbsent(pawn, hs);
            }
        }

        // 判断小人是否携带数码设备（手机/手表/掌机等，Core 用 CompTerminalLink 标记）
        public static bool HasDigitalDevice(Pawn pawn)
        {
            if (pawn == null || pawn.apparel == null) return false;
            foreach (var app in pawn.apparel.WornApparel)
            {
                if (app.TryGetComp<CompTerminalLink>() != null)
                {
                    return true;
                }
            }
            return false;
        }

        private static void AddIfAbsent(Pawn pawn, HediffDef def)
        {
            if (pawn.health == null) return;
            if (pawn.health.hediffSet.GetFirstHediffOfDef(def) == null)
            {
                pawn.health.AddHediff(def);
            }
        }

        private static void RemoveIfPresent(Pawn pawn, HediffDef def)
        {
            if (pawn.health == null) return;
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }
    }
}
