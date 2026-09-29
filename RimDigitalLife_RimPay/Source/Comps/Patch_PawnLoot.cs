using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using HarmonyLib;

namespace RimDigitalLife_RimPay
{
    [StaticConstructorOnStartup]
    public static class LootHarmony
    {
        static LootHarmony()
        {
            var harmony = new Harmony("maiya.RimDigitalLife.RimPay.Loot");
            harmony.PatchAll();
        }
    }

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
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
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
                comp.ModifyBalance(instigator, halfToKiller, "掠夺敌人钱包");
            }
            comp.ModifyTreasury(halfToTreasury, "搜刮上缴");

            string who = isColonistKill ? pawn.LabelShort : "一名敌人";
            string msg = "从 " + who + " 身上搜刮到 " + lootValue + " @银 的数字白银";
            if (isColonistKill)
            {
                msg += " (" + halfToKiller + " 归 " + instigator.LabelShort + "，" + halfToTreasury + " 入数字国库)";
            }
            else
            {
                msg += "，已全额缴入数字国库。";
            }
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