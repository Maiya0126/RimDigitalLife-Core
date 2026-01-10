using HarmonyLib;
using Verse;
using RimWorld;

namespace RimDigitalLife
{
    [HarmonyPatch(typeof(PlayLogEntry_Interaction), "ToGameStringFromPOV")]
    public static class Patch_LogDisplay
    {
        public static void Postfix(ref string __result, InteractionDef ___intDef)
        {
            if (___intDef == null) return;
            if (___intDef.defName == "RimDigital_PhoneCall")
            {
                __result = "<color=#00FF00>[R-Chat]</color> " + __result;
            }
        }
    }
}