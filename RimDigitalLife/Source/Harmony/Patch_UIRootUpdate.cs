using HarmonyLib;
using Verse;

namespace RimDigitalLife
{
    [HarmonyPatch(typeof(UIRoot), "UIRootUpdate")]
    public static class Patch_UIRootUpdate_RealTimeReminder
    {
        public static void Postfix()
        {
            RealTimeReminder.Update();
        }
    }
}
