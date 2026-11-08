using System.Linq;
using LudeonTK;
using RimDigitalLife;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimDigitalLife_QuantumNet
{
    // 运营商事件开发者模式触发按钮（开发模式 → 调试动作 → 量子网络）
    [StaticConstructorOnStartup]
    public static class QuantumNetDebugActions
    {
        [DebugAction("量子网络", "触发星链风暴 (Starlink Storm)")]
        public static void DebugStarlinkStorm()
        {
            NetworkEventManager.DebugForceEvent(NetworkEventManager.StarlinkStorm);
        }

        [DebugAction("量子网络", "触发网络拥堵 (Network Congested)")]
        public static void DebugNetworkCongested()
        {
            NetworkEventManager.DebugForceEvent(NetworkEventManager.NetworkCongested);
        }

        [DebugAction("量子网络", "触发赛博百货大促 (Cyber Sale)")]
        public static void DebugCyberSale()
        {
            NetworkEventManager.DebugForceEvent(NetworkEventManager.CyberSale);
        }

        [DebugAction("量子网络", "触发卡带新品发售 (Carto Release)")]
        public static void DebugCartoRelease()
        {
            NetworkEventManager.DebugForceEvent(NetworkEventManager.CartoRelease);
        }

        [DebugAction("量子网络", "触发深矿雷达校准 (MineScan Calib)")]
        public static void DebugMineScanCalib()
        {
            NetworkEventManager.DebugForceEvent(NetworkEventManager.MineScanCalib);
        }

        [DebugAction("量子网络", "触发套餐半价促销 (Plan Promo)")]
        public static void DebugPlanPromo()
        {
            NetworkEventManager.DebugForceEvent(NetworkEventManager.PlanPromo);
        }

        [DebugAction("量子网络", "结束当前事件 (End Current Event)")]
        public static void DebugEndCurrentEvent()
        {
            if (!NetworkEventManager.HasActiveEvent)
            {
                Messages.Message("[QuantumNet] " + "QN_Dbg_NoEvent".Translate(), MessageTypeDefOf.NeutralEvent, false);
                return;
            }
            NetworkEventManager.DebugEndEvent();
            Messages.Message("[QuantumNet] " + "QN_Dbg_Ended".Translate(), MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction("量子网络", "事件状态 (Event Status)")]
        public static void DebugEventStatus()
        {
            string status = NetworkEventManager.HasActiveEvent
                ? $"{NetworkEventManager.GetStatusText()} (ID: {NetworkEventManager.ActiveEventId})"
                : "QN_Dbg_NoActive".Translate();
            Messages.Message("[QuantumNet] " + "QN_Dbg_Status".Translate(status), MessageTypeDefOf.NeutralEvent, false);
            Verse.Log.Message($"[QuantumNet] status: {status} | next event tick: {NetworkEventManager.NextEventTickDebug}");
        }

        [DebugAction("量子网络", "测试跨地图通话 (Test Cross-Map Call)")]
        public static void DebugTestCrossMapCall()
        {
            var comp = GameComponent_QuantumNet.Instance;
            if (comp == null) return;
            Map home = Find.AnyPlayerHomeMap;
            if (home == null)
            {
                Messages.Message("[QuantumNet] " + "QN_Dbg_NoHomeMap".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (!RimTalkBridge.IsRimTalkAvailable)
            {
                Messages.Message("[QuantumNet] " + "QN_Dbg_NoRimTalk".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            // 从远征队里挑一个测试者（优先无限包用户，没有则任意殖民者）
            Pawn caller = null;
            var travelers = new System.Collections.Generic.List<Pawn>();
            foreach (Caravan c in Find.WorldObjects.Caravans)
            {
                if (c == null || c.PawnsListForReading == null) continue;
                foreach (Pawn p in c.PawnsListForReading)
                {
                    if (p != null && p.IsFreeColonist && !p.Dead) travelers.Add(p);
                }
            }
            if (travelers.Count == 0)
            {
                Messages.Message("[QuantumNet] " + "QN_Dbg_NoCaravan".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            caller = travelers.FirstOrDefault(p => comp.HasInfinitePlan(p)) ?? travelers[0];

            // 基地接话人（排除 caller）
            var bases = home.mapPawns.FreeColonists.Where(p => !p.Dead && p != caller).ToList();
            Pawn recipient = bases.Any() ? bases.RandomElement() : caller;

            CrossMapCallManager.DebugTriggerCall(caller, recipient, home);
            Messages.Message("[QuantumNet] " + "QN_Dbg_TestCall".Translate(caller.LabelShort, recipient.LabelShort), MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
