using HarmonyLib;
using RimWorld;
using Verse;

namespace RimDigitalLife_RimPay
{
    // ========================================================================
    // 监听科研项目完成：科研突破利好对应科技/医药/军工/农业板块股票
    // ========================================================================
    [HarmonyPatch(typeof(ResearchManager), "FinishProject")]
    public static class Patch_ResearchManager_FinishProject
    {
        [HarmonyPostfix]
        public static void Postfix(ResearchProjectDef proj)
        {
            if (proj == null) return;
            GameComponent_RimPay comp = Current.Game?.GetComponent<GameComponent_RimPay>();
            comp?.NotifyResearchFinished(proj);
        }
    }
}
