using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using HarmonyLib;

namespace RimDigitalLife_RimPay
{
    // ========================================================================
    // 注意：本文件的 [HarmonyPatch] 类（Kill / MakeDowned）由 RimPayHarmony 的
    // harmony.PatchAll() 统一注册（PatchAll 扫描全程序集）。
    // 此处曾有一个独立的 LootHarmony 静态构造器再 PatchAll 一次——两个不同
    // Harmony id 对同一方法重复注册，导致全程序集 postfix 执行两次
    // （玩家反馈"餐费每次消费扣费 2 次"的根因，日志同毫秒成对扣费）。
    // 已删除该重复初始化，勿再加回。
    // ========================================================================

    [HarmonyPatch(typeof(Pawn), "Kill")]
    public static class Patch_Pawn_Kill
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, DamageInfo? dinfo)
        {
            PawnLootHelper.ProcessLoot(__instance, dinfo);
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
    public static class Patch_Pawn_MakeDowned
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn_HealthTracker __instance, DamageInfo? dinfo)
        {
            Pawn pawn = (Pawn)typeof(Pawn_HealthTracker).GetField("pawn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(__instance);
            if (pawn != null)
            {
                PawnLootHelper.ProcessLoot(pawn, dinfo);
            }
        }
    }

    public static class PawnLootHelper
    {
        private static HashSet<int> lootedPawnIDs = new HashSet<int>();

        public static void ProcessLoot(Pawn pawn, DamageInfo? dinfo)
        {
            if (pawn == null || pawn.RaceProps == null || !pawn.RaceProps.Humanlike) return;
            if (pawn.Faction == Faction.OfPlayer) return;
            if (pawn.HostFaction == Faction.OfPlayer) return;
            if (pawn.IsPrisonerOfColony) return;

            int id = pawn.thingIDNumber;
            if (lootedPawnIDs.Contains(id)) return;
            lootedPawnIDs.Add(id);

            int lootValue = CalculateLootValue(pawn);

            GameComponent_RimPay comp = Current.Game.GetComponent<GameComponent_RimPay>();
            if (comp == null) return;

            // 宏观事件：淘金热 → 敌人随身数字白银大幅增加
            if (comp.ActiveMacroEvent == "gold_rush")
            {
                lootValue = Mathf.Max(1, (int)(lootValue * 2.0f));
            }
            if (lootValue <= 0) return;

            Pawn instigator = dinfo?.Instigator as Pawn;
            bool isColonistKill = instigator != null && instigator.IsColonistPlayerControlled && !instigator.Dead;

            var S_loot = RimPayMod.settings;
            if (S_loot == null || !S_loot.enablePawnLoot) return;

            float killerShare = S_loot.lootKillerShare;
            // AI 经济：若开启，AI 可微调击杀者分成（增量）
            if (RimPayMod.settings?.enableEconomyAI ?? false)
            {
                killerShare = Mathf.Clamp01(killerShare + RimPayAIProvider.lootShareDelta);
            }
            int halfToKiller = isColonistKill ? (int)(lootValue * killerShare) : 0;
            int halfToTreasury = lootValue - halfToKiller;

            if (halfToKiller > 0 && isColonistKill)
            {
                comp.ModifyBalance(instigator, halfToKiller, "RP_Reason_LootWallet");
            }
            comp.ModifyTreasury(halfToTreasury, "RP_Reason_LootTax");

            string who = isColonistKill ? pawn.LabelShort : (string)"RP_Loot_UnknownEnemy".Translate();
            string msg = isColonistKill
                ? "RP_Loot_MsgKiller".Translate(who, lootValue, halfToKiller, instigator.LabelShort, halfToTreasury)
                : "RP_Loot_MsgTreasury".Translate(who, lootValue);
            Messages.Message(msg, MessageTypeDefOf.NeutralEvent, false);
        }

        private static int CalculateLootValue(Pawn pawn)
        {
            float baseValue = 0f;

            if (pawn.Faction != null)
            {
                TechLevel tl = pawn.Faction.def.techLevel;
                if (tl == TechLevel.Animal) baseValue = 10;
                else if (tl == TechLevel.Neolithic) baseValue = 30;
                else if (tl == TechLevel.Medieval) baseValue = 60;
                else if (tl == TechLevel.Industrial) baseValue = 120;
                else if (tl == TechLevel.Spacer) baseValue = 250;
                else if (tl == TechLevel.Ultra) baseValue = 400;
                else if (tl == TechLevel.Archotech) baseValue = 600;
                else baseValue = 50;
            }
            else
            {
                baseValue = 25;
            }

            float equipmentValue = 0f;
            if (pawn.equipment != null)
            {
                foreach (var thingWithComps in pawn.equipment.AllEquipmentListForReading)
                {
                    equipmentValue += thingWithComps.MarketValue * 0.1f;
                }
            }
            if (pawn.apparel != null)
            {
                foreach (var app in pawn.apparel.WornApparel)
                {
                    equipmentValue += app.MarketValue * 0.05f;
                }
            }

            float skillValue = 0f;
            if (pawn.skills != null)
            {
                foreach (var skill in pawn.skills.skills)
                {
                    skillValue += skill.Level * 0.5f;
                }
            }

            int total = Mathf.Max(1, Mathf.RoundToInt(baseValue + equipmentValue + skillValue));
            return total;
        }
    }
}