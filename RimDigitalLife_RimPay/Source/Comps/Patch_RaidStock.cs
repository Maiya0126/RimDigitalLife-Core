using HarmonyLib;
using RimWorld;
using Verse;

namespace RimDigitalLife_RimPay
{
    // ========================================================================
    // 监听敌对派系袭击发起：军工股 ApexArms 走强，战乱派系股票承压
    // ========================================================================
    [HarmonyPatch(typeof(IncidentWorker_Raid), "TryGenerateRaidInfo")]
    public static class Patch_IncidentWorker_Raid_StockImpact
    {
        [HarmonyPostfix]
        public static void Postfix(IncidentParms parms, bool __result)
        {
            if (__result && parms?.faction != null && parms.faction.HostileTo(Faction.OfPlayer))
            {
                GameComponent_RimPay comp = Current.Game?.GetComponent<GameComponent_RimPay>();
                comp?.NotifyRaidStarted(parms.faction.loadID);
            }
        }
    }
}
