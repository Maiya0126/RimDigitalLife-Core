using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimDigitalLife_QuantumNet
{
    // ============================================================
    // RimTuber 直播打赏联动（Phase 4，需 RimTuber + RimPay 可选）
    // 殖民者看玩家（主播）自己的直播时，用 RimPay 钱包打赏 1~50 币
    // 打赏资金直接进数字国库（玩家=主播）；小人获得「支持主播 +6」心情
    // 无 RimPay 时打赏逻辑禁用，仅保留看直播心情
    // ============================================================
    public static class StreamTipManager
    {
        private static int lastTipTick = 0;

        // 由 GameComponent 低频驱动（每 3000 tick）
        public static void Tick()
        {
            var S = QuantumNetMod.settings;
            if (S == null || !S.enableLiveTip) return;
            var comp = GameComponent_QuantumNet.Instance;
            if (comp == null) return;

            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            if (!comp.HasNetworkCoverage(map)) return;
            if (!QuantumNetRimTuberBridge.IsLive()) return; // 玩家当前没在直播

            // 冷却：约每 1 游戏小时尝试一次机会
            int tick = Find.TickManager.TicksGame;
            if (tick - lastTipTick < 60000) return;
            lastTipTick = tick;

            ThingDef displayDef = DefDatabase<ThingDef>.GetNamedSilentFail("RimTuber_LivestreamDisplay");
            if (displayDef == null) return;

            List<Pawn> watchers = new List<Pawn>();
            foreach (Pawn pawn in map.mapPawns.FreeColonists)
            {
                if (pawn.Dead || pawn.Destroyed) continue;
                if (!comp.HasPaidPlan(pawn)) continue; // 看直播打赏为畅享/无限包功能
                if (NearLivestreamDisplay(pawn, map, displayDef))
                {
                    watchers.Add(pawn);
                }
            }
            if (watchers.Count == 0) return;

            TryTip(watchers.RandomElement());
        }

        private static bool NearLivestreamDisplay(Pawn pawn, Map map, ThingDef displayDef)
        {
            if (pawn.Map != map || !pawn.Spawned) return false;
            foreach (Thing t in map.listerThings.ThingsOfDef(displayDef))
            {
                if (t.Spawned && pawn.Position.InHorDistOf(t.Position, 30f))
                {
                    return true;
                }
            }
            return false;
        }

        private static void TryTip(Pawn pawn)
        {
            bool hasRimPay = QuantumNetRimPayBridge.IsRimPayAvailable;

            if (hasRimPay)
            {
                int amount = Rand.RangeInclusive(1, 50);
                int balance = QuantumNetRimPayBridge.GetBalance(pawn);
                if (balance <= 0)
                {
                    Messages.Message($"[直播] {pawn.LabelShort} 正在看玩家直播，可惜钱包空空，无法打赏。", MessageTypeDefOf.NeutralEvent, false);
                    return;
                }
                if (amount > balance) amount = balance;

                QuantumNetRimPayBridge.TryModifyBalance(pawn, -amount, "看直播打赏");
                QuantumNetRimPayBridge.TryModifyTreasury(amount); // 直接进数字国库（玩家=主播）

                Messages.Message($"[直播] {pawn.LabelShort} 正在观看玩家直播，打赏了 {amount} @银 支持主播！", MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                Messages.Message($"[直播] {pawn.LabelShort} 正在观看玩家直播，为主播加油！", MessageTypeDefOf.PositiveEvent, false);
            }

            // 心情：支持主播 +6（无 RimPay 也保留看直播心情）
            ThoughtDef support = DefDatabase<ThoughtDef>.GetNamedSilentFail("QuantumNet_SupportStreamer");
            if (support != null)
            {
                pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(support);
            }
        }
    }
}
