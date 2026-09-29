using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using HarmonyLib;

namespace RimDigitalLife_RimPay
{
    // ========================================================================
    // 补丁 1：拦截进食行为 (Eat Food)
    // ========================================================================
    [HarmonyPatch(typeof(Toils_Ingest), "FinalizeIngest")]
    public static class Patch_Toils_Ingest_FinalizeIngest
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn ingester, TargetIndex ingestibleInd)
        {
            if (ingester == null || !ingester.IsColonistPlayerControlled) return;

            GameComponent_RimPay comp = Current.Game.GetComponent<GameComponent_RimPay>();
            if (comp == null) return;

            Thing food = ingester.CurJob?.GetTarget(ingestibleInd).Thing;
            if (food == null || food.def == null || food.def.ingestible == null) return;

            var S = RimPayMod.settings;
            if (S == null || !S.enableMealFee) return;

            // 根据食物的营养度或价值计算餐饮费
            int cost = CalculateFoodCost(food, S);
            if (cost <= 0) return;

            // AI 经济：若开启，餐费随当日 AI 餐费倍率浮动（失败自动回退 1.0）
            if (RimPayMod.settings?.enableEconomyAI ?? false)
            {
                cost = (int)(cost * RimPayAIProvider.mealFeeMultiplier);
            }

            // 宏观事件：物资短缺 → 餐费额外上涨 50%
            if (comp.ActiveMacroEvent == "shortage")
            {
                cost = Mathf.Max(1, (int)(cost * 1.5f));
            }

            // 折扣计算（是否佩戴数码设备）
            bool hasDevice = HasDigitalDevice(ingester);
            if (hasDevice)
            {
                cost = (int)(cost * S.deviceRentDiscount); // 佩戴设备享受折扣
            }

            int balance = comp.GetBalance(ingester);
            if (balance >= cost)
            {
                // 扣费并回流国库
                comp.ModifyBalance(ingester, -cost, "用餐费用");
                comp.ModifyTreasury(cost, "餐费");
            }
            else
            {
                // 钱包余额不足以支付餐费，吃上"救济粮"
                comp.ModifyBalance(ingester, -balance, "救济粮");
                comp.ModifyTreasury(balance, "餐费(救济)");
            }
        }

        private static int CalculateFoodCost(Thing food, RimPaySettings S)
        {
            // 定价从设置读取，玩家可自行调整
            if (food.def.ingestible.preferability >= FoodPreferability.MealLavish) return S.mealLavish;
            if (food.def.ingestible.preferability >= FoodPreferability.MealFine) return S.mealFine;
            if (food.def.ingestible.preferability >= FoodPreferability.MealSimple) return S.mealSimple;
            if (food.def.ingestible.preferability >= FoodPreferability.MealAwful) return S.mealAwful; // 如营养膏
            return S.mealRaw; // 生食/浆果等
        }

        private static bool HasDigitalDevice(Pawn pawn)
        {
            if (pawn.apparel == null) return false;
            foreach (var app in pawn.apparel.WornApparel)
            {
                if (app.def.apparel != null && app.def.apparel.tags != null)
                {
                    if (app.def.apparel.tags.Contains("DigitalStorage_TerminalAccess") || 
                        app.def.apparel.tags.Contains("RimDigital_WatchDevice") ||
                        app.def.defName.Contains("iShen") || 
                        app.def.defName.Contains("Nokirim"))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

    // ========================================================================
    // 补丁 2：拦截睡觉/起床行为 (Sleep/Wake up) - 收取房租
    // ========================================================================
    // 我们可以在小人完成睡觉工作 (JobDefOf.LayDown) 或睡醒时触发
    // 由于 LayDown 比较复杂，也可以在每天发工资的时候一并收取当天的房租
}
