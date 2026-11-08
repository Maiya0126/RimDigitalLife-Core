using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimDigitalLife_RimPay
{
    // ========================================================================
    // RimPay × RimMarket 边缘市场联动（纯反射版，零 RimMarket 类型引用）
    //
    // 目标：小人在边缘市场（RimMarket，forti.rimmarket）消费时，
    // 优先使用 RimPay 数字钱包余额，不足部分再用随身实体白银补足；
    // 实体白银转移走原逻辑，钱包扣款侧由本补丁处理，双方账目对得上。
    //
    // 覆盖对象：任何派系小人（殖民者/访客/NPC 商队皆可）——只要其 RimPay
    // 钱包有余额即生效；钱包余额为 0 的小人行为与原版完全一致。
    // （注：当前 RimPay 仅给殖民者发工资，其他派系小人余额通常为 0，
    //   机制已就绪，后续若给 NPC 发钱包即可无缝生效。）
    //
    // 重要：RimWorld 加载 mod 程序集时会 Assembly.GetTypes() 枚举全部类型，
    // 任何"引用 RimMarket 类型"的签名都会在类型枚举阶段触发 TypeLoadException，
    // 导致整个 RimPay 程序集加载失败。因此本文件所有 patch 方法签名只含
    // Verse/RimWorld 基类型，RimMarket 类型一律 object 接收。
    //
    // 挂点（签名已用反编译核对，1.6/1.7 DLL 完全一致）：
    // - RimMarket.Market.MarketUtility.SilverOf(Pawn) → postfix：
    //     可用余额 = 物理白银 + 数字钱包（购物资格判定 + 挑商品预算 + 买几件）
    // - RimMarket.Market.MarketUtility.TransferSilver(Pawn, ThingOwner, int) → prefix+postfix：
    //     钱包优先付款（GuestBuy 买 NPC 摊主商品路径）
    // - RimMarket.Market.MarketUtility.TakePayment(List<Pawn>, int, bool) → prefix+postfix：
    //     钱包优先付款（ColonyStalls.Sell 自家摊位 / InnUtility.Charge 客栈路径）
    // ========================================================================
    public static class RimMarketIntegration
    {
        private const string RimMarketPackageId = "forti.rimmarket";

        private static bool initialized = false;

        // 重入保护：补丁扣款期间，SilverOf postfix 不再叠加钱包余额读取干扰
        private static bool inWalletPayment = false;

        // prefix→postfix 传递：本单已用钱包支付的额度（游戏单线程，无并发）
        private static int pendingTransferWallet = 0;
        private static int pendingTakeWallet = 0;

        public static void TryInit(Harmony harmony)
        {
            if (initialized) return;
            try
            {
                if (ModLister.GetActiveModWithIdentifier(RimMarketPackageId, true) == null) return;

                Type utilityType = CompatPatchHelper.FindType("RimMarket.Market.MarketUtility");
                if (utilityType == null)
                {
                    Verse.Log.Message("[RimPay] RimMarket assembly not loaded yet, edge market integration skipped.");
                    return;
                }

                MethodInfo silverOf = utilityType.GetMethod("SilverOf", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                MethodInfo transferSilver = utilityType.GetMethod("TransferSilver", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                MethodInfo takePayment = utilityType.GetMethod("TakePayment", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                if (silverOf != null)
                {
                    CompatPatchHelper.Patch(harmony, silverOf, null,
                        CompatPatchHelper.GetPostfix(typeof(RimMarketIntegration), nameof(SilverOfPostfix)));
                }
                if (transferSilver != null)
                {
                    CompatPatchHelper.Patch(harmony, transferSilver,
                        CompatPatchHelper.GetPostfix(typeof(RimMarketIntegration), nameof(TransferSilverPrefix)),
                        CompatPatchHelper.GetPostfix(typeof(RimMarketIntegration), nameof(TransferSilverPostfix)));
                }
                if (takePayment != null)
                {
                    CompatPatchHelper.Patch(harmony, takePayment,
                        CompatPatchHelper.GetPostfix(typeof(RimMarketIntegration), nameof(TakePaymentPrefix)),
                        CompatPatchHelper.GetPostfix(typeof(RimMarketIntegration), nameof(TakePaymentPostfix)));
                }

                initialized = true;
                Verse.Log.Message("[RimPay] RimMarket edge market integration enabled (digital wallet first, physical silver as fallback).");
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[RimPay] RimMarket integration init failed: {ex}");
            }
        }

        // ======== 钱包可用性 ========
        // 任何派系小人只要有 RimPay 钱包余额即可用；扣款动作进行中防重入
        private static bool WalletUsable(Pawn p)
        {
            var S = RimPayMod.settings;
            if (S == null || !S.enableRimMarketIntegration) return false;
            if (inWalletPayment) return false;
            if (p == null || p.Dead || !p.RaceProps.Humanlike) return false;
            return true;
        }

        private static GameComponent_RimPay PayComp()
        {
            return Current.Game?.GetComponent<GameComponent_RimPay>();
        }

        // ======== 挂点1：可用余额 = 物理白银 + 数字钱包 ========
        // 资格判定（IsCustomer 的 SilverOf>=5）、GuestBuy/Sell 挑商品与买几件都读它
        private static void SilverOfPostfix(Pawn p, ref int __result)
        {
            try
            {
                if (!WalletUsable(p)) return;
                var comp = PayComp();
                if (comp == null) return;
                __result += comp.GetBalance(p);
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[RimPay] RimMarket SilverOf integration exception: {ex.Message}");
            }
        }

        // ======== 挂点2：GuestBuy 付款（买 NPC 摊主商品） ========
        // prefix：钱包优先——先从数字钱包扣 min(amount, 余额)，扣到的部分直接给
        // 收款方（驮兽/商人库存）生成等量白银（物理守恒）；注入参数 amount 减去
        // 已付部分，剩余交给原逻辑从随身白银转移。
        private static bool TransferSilverPrefix(Pawn from, ThingOwner to, ref int amount, ref int __result)
        {
            try
            {
                pendingTransferWallet = 0; // 防上次异常残留
                if (inWalletPayment || amount <= 0) return true;
                if (!WalletUsable(from) || to == null) return true;

                var comp = PayComp();
                if (comp == null) return true;
                int balance = comp.GetBalance(from);
                int walletPay = Mathf.Min(amount, balance);
                if (walletPay <= 0) return true;

                // 向收款方生成等量物理白银；容器收不下（罕见）则整体退回原逻辑。
                // TryAdd 失败时 Thing 未入容器，直接放弃钱包路径即可，无需回滚扣款（扣款在 TryAdd 成功之后才执行）
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = walletPay;
                if (!to.TryAdd(silver, true))
                {
                    return true;
                }

                inWalletPayment = true;
                try
                {
                    comp.ModifyBalance(from, -walletPay, "RP_Reason_EdgeMarketSpending".Translate());
                }
                finally
                {
                    inWalletPayment = false;
                }
                amount -= walletPay;       // 剩余部分原逻辑继续收实体白银
                if (amount <= 0)
                {
                    // 钱包全额付清，跳过原方法：__result 已是最终值，
                    // pendingTransferWallet 必须清零，否则 postfix 会再加一次（双倍）
                    pendingTransferWallet = 0;
                    __result = walletPay;
                    return false;
                }
                pendingTransferWallet = walletPay; // 原方法返回实体转移量并覆盖 __result，postfix 再补回钱包部分
                return true;
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[RimPay] RimMarket TransferSilver prefix exception: {ex.Message}");
                return true;
            }
        }

        // postfix：原方法返回的是"实体白银转移量"（会覆盖 __result），
        // 把 prefix 的钱包已付额加回来 = 本单总支付
        private static void TransferSilverPostfix(Pawn from, ThingOwner to, int amount, ref int __result)
        {
            try
            {
                if (pendingTransferWallet > 0)
                {
                    __result += pendingTransferWallet;
                    pendingTransferWallet = 0;
                }
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[RimPay] RimMarket TransferSilver postfix exception: {ex.Message}");
            }
        }

        // ======== 挂点3：ColonyStalls.Sell / InnUtility.Charge 付款（TakePayment 路径） ========
        // prefix：钱包优先——付款人列表中第一个有余额的小人先数字扣款，
        // 注入参数 amount 减去已付部分，剩余交给原逻辑收实体白银。
        private static bool TakePaymentPrefix(object pawns, ref int amount, bool takeGoods)
        {
            try
            {
                pendingTakeWallet = 0; // 防上次异常残留
                if (inWalletPayment || amount <= 0) return true;
                var payers = pawns as List<Pawn>;
                if (payers == null || payers.Count == 0) return true;

                var comp = PayComp();
                if (comp == null) return true;

                foreach (Pawn p in payers)
                {
                    if (amount <= 0) break;
                    if (!WalletUsable(p)) continue;
                    int balance = comp.GetBalance(p);
                    int walletPay = Mathf.Min(amount, balance);
                    if (walletPay <= 0) continue;

                    inWalletPayment = true;
                    try
                    {
                        comp.ModifyBalance(p, -walletPay, "RP_Reason_EdgeMarketSpending".Translate());
                    }
                    finally
                    {
                        inWalletPayment = false;
                    }
                    pendingTakeWallet += walletPay;
                    amount -= walletPay; // 原逻辑只收剩余差额
                }
                return true; // 原逻辑继续收剩余实体白银（可能为 0，返回空清单也正确）
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[RimPay] RimMarket TakePayment prefix exception: {ex.Message}");
                return true;
            }
        }

        // postfix：把钱包支付额以白银 Thing 追加进返回清单，
        // 调用方（TakeAndPlacePayment/Charge）会把清单物品放置到收银格/地面（物理守恒）
        private static void TakePaymentPostfix(object pawns, int amount, bool takeGoods, object __result)
        {
            try
            {
                if (pendingTakeWallet <= 0) return;
                int walletPart = pendingTakeWallet;
                pendingTakeWallet = 0;

                var gotThings = __result as List<Thing>;
                if (gotThings == null) return;
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = walletPart;
                gotThings.Add(silver);
            }
            catch (Exception ex)
            {
                pendingTakeWallet = 0;
                Verse.Log.Warning($"[RimPay] RimMarket TakePayment postfix exception: {ex.Message}");
            }
        }
    }
}
