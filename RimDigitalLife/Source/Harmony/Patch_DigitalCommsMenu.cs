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

            // Check if pawn already has any digital comms device
            bool hasCommsDevice = false;
            foreach (Apparel apparel in pawn.apparel.WornApparel)
            {
                if (apparel.TryGetComp<CompDigitalComms>() != null)
                {
                    hasCommsDevice = true;
                    break;
                }
            }

            // Only add one "Use Comms" option if they have any comms device
            if (hasCommsDevice)
            {
                CompDigitalComms dummyComp = null;
                foreach (Apparel apparel in pawn.apparel.WornApparel)
                {
                    dummyComp = apparel.TryGetComp<CompDigitalComms>();
                    if (dummyComp != null)
                        break;
                }

                if (dummyComp != null)
                {
                    var menuOptions = dummyComp.GetFloatMenuOptionsForPawn(pawn);
                    if (menuOptions != null)
                    {
                        __result.AddRange(menuOptions);
                    }
                }
            }
        }
    }
}