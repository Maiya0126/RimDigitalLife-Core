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
                defaultLabel = "数字国库",
                defaultDesc = "打开 RimPay 数字国库与殖民地财报总览。存取白银、查看全区资产与殖民者钱包。",
                icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Icons/RimPay_Treasury", false) ?? ContentFinder<UnityEngine.Texture2D>.Get("UI/Icons/RimPay_Icon", false) ?? BaseContent.BadTex,
                hotKey = KeyBindingDefOf.Misc1
            };
            
            gizmos.Add(treasuryAction);
            __result = gizmos;
        }
    }
}