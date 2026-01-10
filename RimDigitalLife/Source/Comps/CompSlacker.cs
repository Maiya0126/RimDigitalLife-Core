using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimDigitalLife
{
    public class CompProperties_Slacker : CompProperties
    {
        public CompProperties_Slacker() { this.compClass = typeof(CompSlacker); }
    }

    public class CompSlacker : ThingComp
    {
        public override void CompTickRare()
        {
            base.CompTickRare();
            Pawn pawn = this.parent.ParentHolder as Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Downed || !pawn.Awake()) return;

            // 1. 检查工作时间
            if (pawn.timetable == null) return;
            var assignment = pawn.timetable.GetAssignment(GenLocalDate.HourOfDay(pawn));
            if (assignment != TimeAssignmentDefOf.Work) return;
            if (pawn.Drafted || pawn.InMentalState) return;

            // 2. 【设置检查】设备类型过滤
            bool isComm = this.parent.def.defName.Contains("Phone") || this.parent.def.defName.Contains("Watch") || this.parent.def.defName.Contains("Pad") || this.parent.def.defName.Contains("Nokirim");
            bool isGame = !isComm; // 剩下的主要是掌机

            if (isComm && !RimDigitalMod.settings.allowCommSlacking) return;
            if (isGame && !RimDigitalMod.settings.allowGameSlacking) return;

            // 3. 【设置检查】概率
            if (Rand.Value > RimDigitalMod.settings.slackChance) return;

            // 4. 扫描目击者
            bool witnessFound = false;
            float radius = RimDigitalMod.settings.detectionRadius;
            IReadOnlyList<Pawn> nearbyPawns = pawn.Map.mapPawns.AllPawnsSpawned;

            foreach (var other in nearbyPawns)
            {
                if (other != pawn && other.IsColonist && !other.Downed && other.Awake())
                {
                    if (other.Position.InHorDistOf(pawn.Position, radius) && GenSight.LineOfSight(other.Position, pawn.Position, pawn.Map))
                    {
                        witnessFound = true;
                        break;
                    }
                }
            }

            // 5. 结算
            if (witnessFound && Rand.Chance(0.5f))
            {
                // === 被抓 ===
                pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDef.Named("RimDigital_CaughtSlacking"));
                if (RimDigitalMod.settings.showMoteText)
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "被抓包!", Color.red);

                if (RimDigitalMod.settings.strictMode)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    // 这不会扣血，只会让他晕几秒，完全符合“严厉模式”的惩罚
                    DamageInfo dinfo = new DamageInfo(DamageDefOf.Stun, 20f);
                    pawn.TakeDamage(dinfo);
                }
            }
            else
            {
                // === 成功 ===
                // 心情数值已经由 RimDigitalMod.ApplyMoodSettings 动态修改了，这里直接加 Memory 即可
                pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDef.Named("RimDigital_SlackingOff"));

                if (RimDigitalMod.settings.showMoteText)
                {
                    string slackText = Rand.Element("刷短视频...", "回个消息...", "看眼股票...", "偷个懒...");
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, slackText, Color.green);
                }
            }
        }
    }
}