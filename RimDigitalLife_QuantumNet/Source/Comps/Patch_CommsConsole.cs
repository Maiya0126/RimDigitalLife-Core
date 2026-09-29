using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;

namespace RimDigitalLife_QuantumNet
{
    // 在通讯台上添加「量子网络」按钮，打开量子网络控制台
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

            Command_Action netAction = new Command_Action
            {
                action = delegate
                {
                    Find.WindowStack.Add(new Window_QuantumNetConsole(console.Map));
                },
                defaultLabel = "量子网络",
                defaultDesc = "打开量子网络控制台：查看网络覆盖与殖民者流量/算力套餐。",
                icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Icons/QuantumNet_Icon", false) ?? BaseContent.BadTex,
                hotKey = KeyBindingDefOf.Misc2
            };

            gizmos.Add(netAction);
            __result = gizmos;
        }
    }
}
