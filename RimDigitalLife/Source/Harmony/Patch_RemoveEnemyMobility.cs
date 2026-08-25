using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Reflection;

namespace RimDigitalLife
{
    [HarmonyPatch]
    public static class Patch_RemoveEnemyMobility
    {
        private static readonly string[] MobilityItemTags = new string[]
        {
            "RimDigital_MobilityItem"
        };

        [HarmonyPatch(typeof(Pawn), "SpawnSetup")]
        [HarmonyPostfix]
        public static void RemoveMobilityItemsFromEnemy(Pawn __instance, bool respawningAfterLoad)
        {
            TryRemoveMobilityItems(__instance);
        }

        [HarmonyPatch(typeof(Pawn), "PostMapInit")]
        [HarmonyPostfix]
        public static void RemoveMobilityItemsOnMapInit(Pawn __instance)
        {
            TryRemoveMobilityItems(__instance);
        }

        [HarmonyPatch(typeof(Pawn), "SetFaction", new System.Type[] { typeof(Faction), typeof(Pawn) })]
        [HarmonyPostfix]
        public static void RemoveMobilityItemsOnFactionSet(Pawn __instance, Faction newFaction)
        {
            TryRemoveMobilityItems(__instance);
        }

        private static void TryRemoveMobilityItems(Pawn pawn)
        {
            if (pawn == null)
                return;

            if (pawn.Faction == null)
                return;

            if (pawn.Faction.IsPlayer)
                return;

            if (!pawn.RaceProps.Humanlike)
                return;

            if (pawn.apparel == null)
                return;

            List<Apparel> toRemove = new List<Apparel>();

            foreach (Apparel apparel in pawn.apparel.WornApparel)
            {
                if (apparel != null && apparel.def != null && apparel.def.apparel != null)
                {
                    foreach (string tag in MobilityItemTags)
                    {
                        if (apparel.def.apparel.tags.Contains(tag))
                        {
                            toRemove.Add(apparel);
                            break;
                        }
                    }
                }
            }

            foreach (Apparel apparel in toRemove)
            {
                pawn.apparel.Remove(apparel);
                if (!apparel.Destroyed)
                {
                    apparel.Destroy();
                }
            }
        }
    }
}