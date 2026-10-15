using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimDigitalLife_QuantumNet
{
    // ============================================================
    // RimSeek 健康提醒（畅享/无限包功能，本地实现不依赖 AI 请求）
    // 方案 A：状况驱动——小人出现健康/需求问题时触发提醒（非定时问候）
    // 检测状况：流血 / 有伤需治疗 / 明显疼痛 / 严重疲劳 / 严重饥饿
    // 每个小人每类状况 1 天冷却，避免刷屏
    // 效果：消息提示 + 「RimSeek 健康提醒」轻心情（被 AI 关心）
    // ============================================================
    public static class HealthReminderManager
    {
        public const string ThoughtDefName = "QuantumNet_HealthReminder";

        private const int CooldownTicks = 60000; // 每类状况提醒冷却 1 天

        // 小人 → 状况ID → 上次提醒 tick（内存即可，重档后重新提醒一次无妨）
        private static readonly Dictionary<string, Dictionary<string, int>> lastRemindTicks = new Dictionary<string, Dictionary<string, int>>();

        // 由 GameComponent 低频驱动（每 1000 tick）
        public static void Tick()
        {
            var S = QuantumNetMod.settings;
            if (S == null || !S.enableHealthReminder) return;
            var comp = GameComponent_QuantumNet.Instance;
            if (comp == null) return;

            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            if (!comp.HasNetworkCoverage(map)) return;

            int tick = Find.TickManager.TicksGame;

            foreach (Pawn pawn in map.mapPawns.FreeColonists)
            {
                if (pawn.Dead || pawn.Destroyed || !pawn.Spawned) continue;
                if (!comp.HasPaidPlan(pawn)) continue;
                if (!RimSeekBuffManager.HasDigitalDevice(pawn)) continue;

                CheckAndRemind(pawn, tick);
            }
        }

        private static void CheckAndRemind(Pawn pawn, int tick)
        {
            // 状况检测（优先级从高到低，一次最多提醒一条最重要的）
            string conditionId = null;
            string tip = null;

            if (pawn.health?.hediffSet == null) return;

            if (pawn.health.hediffSet.BleedRateTotal > 0f)
            {
                conditionId = "bleeding";
                tip = "检测到你在持续流血！RimSeek 强烈建议尽快就医或自行包扎止血。";
            }
            else if (pawn.health.hediffSet.HasTendableHediff())
            {
                conditionId = "tendable";
                tip = "你的身体有需要治疗的伤口或疾病，记得让医生处理一下。";
            }
            else if (pawn.health.hediffSet.PainTotal > 0.4f)
            {
                conditionId = "pain";
                tip = "你正承受明显的疼痛，止痛或接受治疗会让你好受很多。";
            }
            else if (pawn.needs?.rest != null && pawn.needs.rest.CurLevelPercentage < 0.2f)
            {
                conditionId = "tired";
                tip = "你快撑不住了！RimSeek 建议马上去床上好好睡一觉。";
            }
            else if (pawn.needs?.food != null && pawn.needs.food.CurLevelPercentage < 0.15f)
            {
                conditionId = "hungry";
                tip = "你快饿坏了，记得吃点东西补充营养。";
            }

            if (conditionId == null) return;

            // 冷却检查（每小人每类状况 1 天）
            if (!CooldownPassed(pawn, conditionId, tick)) return;
            MarkReminded(pawn, conditionId, tick);

            ThoughtDef thought = DefDatabase<ThoughtDef>.GetNamedSilentFail(ThoughtDefName);
            if (thought != null)
            {
                pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
            }

            Messages.Message($"[健康提醒] RimSeek 提醒 {pawn.LabelShort}：{tip}", MessageTypeDefOf.NeutralEvent, false);
            if (Prefs.DevMode)
            {
                Verse.Log.Message($"[QuantumNet 健康提醒] {pawn.LabelShort} | 状况: {conditionId}");
            }
        }

        private static bool CooldownPassed(Pawn pawn, string conditionId, int tick)
        {
            Dictionary<string, int> conds;
            if (!lastRemindTicks.TryGetValue(pawn.ThingID, out conds)) return true;
            int last;
            if (!conds.TryGetValue(conditionId, out last)) return true;
            return tick - last >= CooldownTicks;
        }

        private static void MarkReminded(Pawn pawn, string conditionId, int tick)
        {
            Dictionary<string, int> conds;
            if (!lastRemindTicks.TryGetValue(pawn.ThingID, out conds))
            {
                conds = new Dictionary<string, int>();
                lastRemindTicks[pawn.ThingID] = conds;
            }
            conds[conditionId] = tick;
        }
    }
}
