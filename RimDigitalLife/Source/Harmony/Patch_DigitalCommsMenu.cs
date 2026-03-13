using HarmonyLib;
using Verse;
using RimWorld;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace RimDigitalLife
{
    [HarmonyPatch]
    public static class Patch_DigitalCommsMenu
    {
        [HarmonyPatch(typeof(FloatMenuMakerMap), "GetOptions")]
        [HarmonyPostfix]
        public static void GetOptionsPostfix(List<Pawn> selectedPawns, Vector3 clickPos, ref List<FloatMenuOption> __result)
        {
            if (selectedPawns == null || selectedPawns.Count == 0)
                return;

            Pawn pawn = selectedPawns[0];
            if (pawn == null || !pawn.IsColonistPlayerControlled)
                return;

            if (pawn.apparel == null)
                return;

            foreach (Apparel apparel in pawn.apparel.WornApparel)
            {
                CompDigitalComms comp = apparel.TryGetComp<CompDigitalComms>();
                if (comp != null)
                {
                    var menuOptions = comp.GetFloatMenuOptionsForPawn(pawn);
                    if (menuOptions != null)
                    {
                        __result.AddRange(menuOptions);
                    }
                }
            }
        }
    }
}