using System.Collections.Generic;
using RimWorld;
using Verse;
using HarmonyLib;

namespace RimDigitalLife_RimPay
{
    // 选中小人时，在底部控制栏增加一个统一的钱包按钮
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Patch_Pawn_GetGizmos
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            // 只有玩家殖民地的活人才能使用 RimPay
            if (!__instance.IsColonistPlayerControlled || __instance.Dead) return;

            GameComponent_RimPay comp = Current.Game.GetComponent<GameComponent_RimPay>();
            if (comp == null) return;

            var gizmos = new List<Gizmo>(__result);

            int wallet = comp.GetBalance(__instance);

            // 统一数字钱包按钮：显示余额，点击打开钱包窗口
            Command_Action walletAction = new Command_Action
            {
                action = delegate
                {
                    Find.WindowStack.Add(new Window_PawnWallet(__instance));
                },
                defaultLabel = $"数字钱包: {wallet}",
                defaultDesc = $"打开 {__instance.LabelShort} 的 RimPay 数字钱包。\n查看收支流水、发放奖金或没收资金。",
                icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Icons/RimPay_Icon", false) ?? BaseContent.BadTex,
                hotKey = KeyBindingDefOf.Misc2
            };
            gizmos.Add(walletAction);

            __result = gizmos;
        }
    }
}