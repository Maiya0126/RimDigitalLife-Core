using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimDigitalLife_RimPay
{
    // ========================================================================
    // 财富托管 (Wealth Escrow)：建筑与随身装备可托管，不计入袭击威胁点。
    // 数字国库中的白银已物理隐藏（存入即销毁），无需在此处理。
    // 两个独立开关：hideBuildingWealth（建筑/地板）、hidePawnEquipmentWealth（随身装备）。
    // 小人/动物/机械体本体价值始终保留（实力挂钩）。
    // ========================================================================
    [HarmonyPatch(typeof(Map), "get_PlayerWealthForStoryteller")]
    public static class Patch_Map_PlayerWealthForStoryteller
    {
        [HarmonyPostfix]
        public static void Postfix(Map __instance, ref float __result)
        {
            if (__instance == null || __instance.wealthWatcher == null) return;
            if (!__instance.TreatAsPlayerHomeForThreatPoints) return;
            if (Find.Storyteller?.difficulty?.fixedWealthMode ?? false) return;

            var S = RimPayMod.settings;
            if (S == null) return;

            // 1. 建筑托管：建筑（含地板）财富在威胁点中权重 0.5，全部托管后扣除
            if (S.hideBuildingWealth)
            {
                __result -= __instance.wealthWatcher.WealthBuildings * 0.5f;
            }

            // 2. 随身装备托管：小人/动物身上的武器、衣物、背包物品（不含本体、不含机械体）
            if (S.hidePawnEquipmentWealth)
            {
                float equipmentWealth = 0f;
                List<Pawn> pawns = __instance.mapPawns.PawnsInFaction(Faction.OfPlayer);
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn p = pawns[i];
                    if (p == null || p.Destroyed) continue;
                    if (p.IsQuestLodger()) continue;
                    if (p.IsColonyMech) continue; // 机械体本体+内置武器是战斗编制，不托管
                    equipmentWealth += WealthWatcher.GetEquipmentApparelAndInventoryWealth(p);
                }
                __result -= equipmentWealth;
            }

            if (__result < 0f) __result = 0f;
        }
    }
}
