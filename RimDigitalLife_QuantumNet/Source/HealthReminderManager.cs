using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimDigitalLife_QuantumNet
{
    // ============================================================
    // RimSeek 健康提醒（畅享/无限包功能，本地实现不依赖 AI 请求）
    // 订阅付费套餐 + 携带数码设备 + 网络覆盖的殖民者，
    // 每天 8 点与 20 点各有一次机会（50%）收到 RimSeek 健康提醒：
    // 获得「RimSeek 健康提醒」心情（被关心的感觉）；疲劳时附加休息建议。
    // ============================================================
    public static class HealthReminderManager
    {
        public const string ThoughtDefName = "QuantumNet_HealthReminder";

        private static int lastRemindTick = 0;

        private static readonly string[] Tips = new string[]
        {
            "久坐伤身，记得起来活动活动筋骨！",
            "长时间盯着屏幕对眼睛不好，看看远处的风景吧。",
            "该补充水分了，来杯水休息一下。",
            "伸展一下身体，健康的工作节奏更重要。",
            "别忘了几小时后起来走动走动，活动手脚。"
        };

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

            // 每天 8 点与 20 点各一次机会
            int hour = GenLocalDate.HourOfDay(map);
            if (hour != 8 && hour != 20) return;

            int tick = Find.TickManager.TicksGame;
            if (tick - lastRemindTick < 30000) return; // 半天防重
            lastRemindTick = tick;

            if (!Rand.Chance(0.5f)) return;

            // 筛选符合条件的小人（畅享/无限包 + 设备 + 在线）
            List<Pawn> candidates = new List<Pawn>();
            foreach (Pawn pawn in map.mapPawns.FreeColonists)
            {
                if (pawn.Dead || pawn.Destroyed || !pawn.Spawned) continue;
                if (!comp.HasPaidPlan(pawn)) continue;
                if (!RimSeekBuffManager.HasDigitalDevice(pawn)) continue;
                candidates.Add(pawn);
            }
            if (candidates.Count == 0) return;

            Pawn target = candidates.RandomElement();
            Remind(target);
        }

        private static void Remind(Pawn pawn)
        {
            string tip = Tips[Rand.Range(0, Tips.Length)];

            // 疲劳时附加休息建议
            float rest = pawn.needs?.rest?.CurLevelPercentage ?? 1f;
            if (rest < 0.35f)
            {
                tip += "你已经很疲惫了，早点休息吧！";
            }

            ThoughtDef thought = DefDatabase<ThoughtDef>.GetNamedSilentFail(ThoughtDefName);
            if (thought != null)
            {
                pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
            }

            Messages.Message($"[健康提醒] RimSeek 提醒 {pawn.LabelShort}：{tip}", MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
