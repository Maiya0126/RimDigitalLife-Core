using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Reflection;

namespace RimDigitalLife
{
    [HarmonyPatch]
    public static class Patch_PawnGenerator
    {
        private static readonly string[] MobilityItemTags = new string[]
        {
            "RimDigital_MobilityItem"
        };

        [HarmonyPatch]
        [HarmonyTargetMethods]
        public static IEnumerable<MethodInfo> TargetMethods()
        {
            var method1 = typeof(PawnGenerator).GetMethod("GeneratePawn", new System.Type[] { typeof(PawnKindDef), typeof(Faction) });
            if (method1 != null) yield return method1;

            var method2 = typeof(PawnGenerator).GetMethod("GeneratePawn", new System.Type[] { typeof(PawnGenerationRequest) });
            if (method2 != null) yield return method2;
        }

        [HarmonyPostfix]
        public static void PreventMobilityItemsOnEnemy(Pawn __result)
        {
            TryRemoveMobilityItems(__result);
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