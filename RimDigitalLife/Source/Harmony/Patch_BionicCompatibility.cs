using HarmonyLib;
using RimWorld;
using Verse;

namespace RimDigitalLife
{
    [HarmonyPatch(typeof(ApparelUtility), "HasPartsToWear")]
    public static class Patch_HasPartsToWear_DigitalDevices
    {
        public static void Postfix(Pawn p, ThingDef apparel, ref bool __result)
        {
            if (__result) return;

            if (apparel.apparel == null) return;
            if (apparel.apparel.tags == null) return;

            bool isDigitalDevice = false;
            for (int i = 0; i < apparel.apparel.tags.Count; i++)
            {
                string tag = apparel.apparel.tags[i];
                if (tag == "RimPhone_Communication" ||
                    tag == "DigitalStorage_TerminalAccess" ||
                    tag == "RimDigital_MobilityItem" ||
                    tag == "RimDigital_WatchDevice")
                {
                    isDigitalDevice = true;
                    break;
                }
            }

            if (!isDigitalDevice) return;

            if (p.RaceProps.Humanlike)
            {
                __result = true;
            }
        }
    }
}
