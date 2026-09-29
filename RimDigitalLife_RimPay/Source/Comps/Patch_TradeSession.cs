using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimDigitalLife_RimPay
{
    // ========================================================================
    // 补丁 1：让交易系统认为玩家的总资金 = 物理白银 + 云端国库
    // ========================================================================
    [HarmonyPatch(typeof(Tradeable), "CountHeldBy")]
    public static class Patch_Tradeable_CountHeldBy
    {
        [HarmonyPostfix]
        public static void Postfix(Tradeable __instance, Transactor trans, ref int __result)
        {
            // 只有当查询玩家（Colony）的资金，并且当前物品是货币（白银）时才介入
            if (trans == Transactor.Colony && __instance.IsCurrency && __instance.ThingDef == ThingDefOf.Silver)
            {
                GameComponent_RimPay comp = Current.Game.GetComponent<GameComponent_RimPay>();
                if (comp != null)
                {
                    // 让系统认为玩家拥有的总资金加上了数字国库里的余额
                    __result += comp.CloudTreasuryBalance;
                }
            }
        }
    }

    // ========================================================================
    // 补丁 2：接管交易结算逻辑，优先扣除物理白银，不足部分从云端国库扣除
    // ========================================================================
    [HarmonyPatch(typeof(Tradeable), "ResolveTrade")]
    public static class Patch_Tradeable_ResolveTrade
    {        [HarmonyPrefix]
        public static bool Prefix(Tradeable __instance)
        {
            // 如果不是玩家付款，或者物品不是白银，走原版逻辑
            if (__instance.ActionToDo != TradeAction.PlayerSells || !__instance.IsCurrency || __instance.ThingDef != ThingDefOf.Silver)
            {
                return true;
            }

            // 获取需要支付给商人的白银总数
            int totalSilverToPay = __instance.CountToTransferToDestination;
            if (totalSilverToPay <= 0) return true;

            // 统计玩家现有的物理白银堆
            int physicalSilverCount = 0;
            foreach (Thing t in __instance.thingsColony)
            {
                physicalSilverCount += t.stackCount;
            }

            // 【情况 A】物理白银足够支付，直接走原版逻辑
            if (physicalSilverCount >= totalSilverToPay)
            {
                return true;
            }

            // 【情况 B】物理白银不足！需要动用数字国库
            int silverDeficit = totalSilverToPay - physicalSilverCount;
            GameComponent_RimPay comp = Current.Game.GetComponent<GameComponent_RimPay>();
            
            // 如果连国库也不够（理论上不可能，因为 CountHeldBy 已经挡住了），安全回退
            if (comp == null || comp.CloudTreasuryBalance < silverDeficit)
            {
                Log.Error("[RimDigitalLife_RimPay] Fatal Error: Cloud Treasury balance insufficient during ResolveTrade fallback.");
                return true;
            }

            // 1. 先把手里所有的物理白银（thingsColony）全部交给商人
            if (physicalSilverCount > 0)
            {
                TransferableUtility.TransferNoSplit(__instance.thingsColony, physicalSilverCount, delegate(Thing thing, int countToTransfer)
                {
                    TradeSession.trader.GiveSoldThingToTrader(thing, countToTransfer, TradeSession.playerNegotiator);
                });
            }

            // 2. 从数字国库扣除欠缺的部分
            comp.ModifyTreasury(-silverDeficit, "贸易扣款");
            Messages.Message($"本次交易从数字国库中无缝扣除了 {silverDeficit} 数字白银。", MessageTypeDefOf.NeutralEvent, false);

            // 3. 为商人凭空生成这部分白银，否则商人卖了东西却没拿到钱（保证经济平衡）
            Thing generatedSilver = ThingMaker.MakeThing(ThingDefOf.Silver);
            generatedSilver.stackCount = silverDeficit;
            TradeSession.trader.GiveSoldThingToTrader(generatedSilver, silverDeficit, TradeSession.playerNegotiator);

            // 拦截完成，跳过原版必将报错的 ResolveTrade
            return false;
        }
    }

    // ========================================================================
    // 补丁 3：AI 市场情绪影响买卖价格（牛市溢价 / 熊市压价）
    // ========================================================================
    [HarmonyPatch(typeof(Tradeable), "GetPriceFor")]
    public static class Patch_Tradeable_GetPriceFor
    {
        [HarmonyPostfix]
        public static void Postfix(TradeAction action, ref float __result)
        {
            if (!(RimPayMod.settings?.enableEconomyAI ?? false)) return;
            if (!RimPayAIProvider.hasAIResult) return;
            float mult = RimPayAIProvider.tradePriceMultiplier;
            if (mult == 1f) return;
            __result *= mult;
            if (__result < 0f) __result = 0f;
        }
    }
}
