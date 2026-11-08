using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;

namespace RimDigitalLife_RimPay
{
    // 在通讯台上添加一个按钮，用于打开“数字国库管理终端”
    // Building_CommsConsole 继承 Building -> Thing.GetGizmos
    [HarmonyPatch(typeof(Thing), "GetGizmos")]
    public static class Patch_CommsConsole_GetGizmos
    {
        [HarmonyPostfix]
        public static void Postfix(Thing __instance, ref IEnumerable<Gizmo> __result)
        {
            if (!(__instance is Building_CommsConsole console))
            {
                return;
            }

            var gizmos = new List<Gizmo>(__result);
            
            Command_Action treasuryAction = new Command_Action
            {
                action = delegate
                {
                    Find.WindowStack.Add(new Window_TreasuryTerminal(console.Map));
                },
                defaultLabel = "RP_Gizmo_Treasury_Label".Translate(),
                defaultDesc = "RP_Gizmo_Treasury_Desc".Translate(),
                icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Icons/RimPay_Treasury", false) ?? ContentFinder<UnityEngine.Texture2D>.Get("UI/Icons/RimPay_Icon", false) ?? BaseContent.BadTex,
                hotKey = KeyBindingDefOf.Misc1
            };
            
            gizmos.Add(treasuryAction);
            __result = gizmos;
        }
    }
}