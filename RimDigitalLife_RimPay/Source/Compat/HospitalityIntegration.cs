using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimDigitalLife_RimPay
{
    // ========================================================================
    // RimPay × Hospitality 访客系列联动（纯反射版，零 Hospitality 类型引用）
    //
    // 目标：访客住宿（GuestBed 租金）/购物（BuyItem）/餐饮（售货机）
    // 优先使用 RimPay 数字钱包，不足再用随身白银。
    //
    // Hospitality 付款逻辑全部只认身上的实体白银堆，且"身上没白银"时
    // 直接放弃购买（BuyThing: FirstOrDefault 找不到堆就 return；
    // ClaimBed: RentalFee > 白银就 EndJobWith）。
    // 故采用「预垫付 + 事后对账」模式：
    //   - 消费入口 prefix：把 RimPay 钱包全额垫成白银放入 inventory（记垫付额）
    //   - Hospitality 原逻辑照常花白银付款（白银落地给床位/地上，物理守恒）
    //   - 消费出口 postfix：实付 X = 快照差；钱包扣 min(X, 垫付额)，
    //     未花掉的垫付白银收回销毁 → 净效果 = 钱包优先付款，原白银兜底
    //
    // 重要：patch 方法签名只含 Verse/RimWorld 类型；Hospitality 类型一律 object 接收。
    //
    // 挂点（签名已用反编译核对，1.6/1.7 DLL 完全一致）：
    // - Hospitality.Utilities.ItemUtility.GetMoney(Pawn) → postfix：余额合并（资格判定）
    // - Hospitality.Utilities.FoodUtility.TryPayForFood(Pawn, Building_NutrientPasteDispenser)
    //   → prefix 垫付 / postfix 对账（单方法内完成支付）
    // - Hospitality.JobDriver_BuyItem.BuyThing(Toil) → prefix 垫付 / postfix 对账
    // - Hospitality.JobDriver_ClaimBed.TryMakePreToilReservations(bool) → prefix 差额垫付
    //   （租金 fee 明确，垫付差额即可；付租 TryDrop 落地白银，物理守恒）
    // ========================================================================
    public static class HospitalityIntegration
    {
        private const string HospitalityPackageId = "Orion.Hospitality";

        private static bool initialized = false;

        // 垫付记录：pawn.thingIDNumber → (垫付白银额, 垫付后白银快照)
        private static readonly Dictionary<int, Vector2Int> advances = new Dictionary<int, Vector2Int>();

        // 对账进行中标记（GetMoney postfix 防叠加）
        private static bool inSettle = false;

        private struct Vector2Int
        {
            public int advanced;  // 垫付额（钱包出的）
            public int snapshot;  // 垫付后身上白银总量
            public Vector2Int(int a, int s) { advanced = a; snapshot = s; }
        }

        public static void TryInit(Harmony harmony)
        {
            if (initialized) return;
            try
            {
                if (ModLister.GetActiveModWithIdentifier(HospitalityPackageId, true) == null) return;

                Type itemUtility = CompatPatchHelper.FindType("Hospitality.Utilities.ItemUtility");
                Type foodUtility = CompatPatchHelper.FindType("Hospitality.Utilities.FoodUtility");
                Type driverBuyItem = CompatPatchHelper.FindType("Hospitality.JobDriver_BuyItem");
                Type driverClaimBed = CompatPatchHelper.FindType("Hospitality.JobDriver_ClaimBed");
                if (itemUtility == null || foodUtility == null || driverBuyItem == null || driverClaimBed == null)
                {
                    Verse.Log.Message("[RimPay] Hospitality assembly not loaded yet, guest integration skipped.");
                    return;
                }

                // 1. 资金判定：白银 + RimPay 钱包
                MethodInfo getMoney = itemUtility.GetMethod("GetMoney", BindingFlags.Public | BindingFlags.Static);
                if (getMoney != null)
                {
                    CompatPatchHelper.Patch(harmony, getMoney, null,
                        CompatPatchHelper.GetPostfix(typeof(HospitalityIntegration), nameof(GetMoneyPostfix)));
                }

                // 2. 售货机餐费
                MethodInfo tryPayForFood = foodUtility.GetMethod("TryPayForFood", BindingFlags.Public | BindingFlags.Static);
                if (tryPayForFood != null)
                {
                    CompatPatchHelper.Patch(harmony, tryPayForFood,
                        CompatPatchHelper.GetPostfix(typeof(HospitalityIntegration), nameof(TryPayForFoodPrefix)),
                        CompatPatchHelper.GetPostfix(typeof(HospitalityIntegration), nameof(TryPayForFoodPostfix)));
                }

                // 3. 购物
                MethodInfo buyThing = driverBuyItem.GetMethod("BuyThing", BindingFlags.NonPublic | BindingFlags.Instance);
                if (buyThing != null)
                {
                    CompatPatchHelper.Patch(harmony, buyThing,
                        CompatPatchHelper.GetPostfix(typeof(HospitalityIntegration), nameof(BuyThingPrefix)),
                        CompatPatchHelper.GetPostfix(typeof(HospitalityIntegration), nameof(BuyThingPostfix)));
                }

                // 4. 住宿：预约时按租金差额垫付（fee 由 job.takeExtraIngestibles 承载）
                MethodInfo claimReserve = driverClaimBed.GetMethod("TryMakePreToilReservations", BindingFlags.Public | BindingFlags.Instance);
                if (claimReserve != null)
                {
                    CompatPatchHelper.Patch(harmony, claimReserve,
                        CompatPatchHelper.GetPostfix(typeof(HospitalityIntegration), nameof(ClaimBedReservePrefix)),
                        null);
                }

                // ======== Adamas 子模组（Hospitality: Casino / Spa / Storefront） ========
                // Casino/Spa：InsertCoin(Pawn, Thing) 唯一付费入口（身上没白银时
                // 作者兜底凭空造银进机器）→ prefix 垫付 / postfix 对账；
                // CountSilver(Pawn) 是"赌资<10 就回家"的离场判定 → postfix 合并钱包余额
                InitAdamasVending(harmony, "HospitalityCasino.VendingMachineJobHelper");
                InitAdamasVending(harmony, "HospitalitySpa.VendingMachineJobHelper");

                // Storefront：Toils_Selling.SellThing(Pawn, Pawn, Thing, Building_CashRegister)
                // 私有静态方法，白银不足时作者凭空造银补进收银机 → prefix 垫付 / postfix 对账
                Type toilsSelling = CompatPatchHelper.FindType("Storefront.Selling.Toils_Selling");
                if (toilsSelling != null)
                {
                    MethodInfo sellThing = toilsSelling.GetMethod("SellThing", BindingFlags.NonPublic | BindingFlags.Static);
                    if (sellThing != null)
                    {
                        CompatPatchHelper.Patch(harmony, sellThing,
                            CompatPatchHelper.GetPostfix(typeof(HospitalityIntegration), nameof(AdamasPayPrefix)),
                            CompatPatchHelper.GetPostfix(typeof(HospitalityIntegration), nameof(AdamasPayPostfix)));
                    }
                }

                initialized = true;
                Verse.Log.Message("[RimPay] Hospitality guest integration enabled (lodging/shopping/food prefer digital wallet).");
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[RimPay] Hospitality integration init failed: {ex}");
            }
        }

        // Casino/Spa 售货机（老虎机/温泉售票机）同构 helper 的挂接
        private static void InitAdamasVending(Harmony harmony, string helperTypeName)
        {
            try
            {
                Type helper = CompatPatchHelper.FindType(helperTypeName);
                if (helper == null) return; // 对应子模组未装
                MethodInfo insertCoin = helper.GetMethod("InsertCoin", BindingFlags.Public | BindingFlags.Static);
                MethodInfo countSilver = helper.GetMethod("CountSilver", BindingFlags.Public | BindingFlags.Static);
                if (insertCoin != null)
                {
                    CompatPatchHelper.Patch(harmony, insertCoin,
                        CompatPatchHelper.GetPostfix(typeof(HospitalityIntegration), nameof(AdamasPayPrefix)),
                        CompatPatchHelper.GetPostfix(typeof(HospitalityIntegration), nameof(AdamasPayPostfix)));
                }
                if (countSilver != null)
                {
                    CompatPatchHelper.Patch(harmony, countSilver, null,
                        CompatPatchHelper.GetPostfix(typeof(HospitalityIntegration), nameof(CountSilverPostfix)));
                }
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[RimPay] {helperTypeName} patch failed: {ex.Message}");
            }
        }

        // ======== Adamas 子模组通用垫付/对账（InsertCoin 与 SellThing 共用） ========
        // InsertCoin(Pawn pawn, Thing vendingMachineParent) / SellThing(Pawn salesperson, Pawn customer, ...)
        // 两个方法第一参数或第二参数取 Pawn：InsertCoin 取 0 号参，SellThing 取 1 号参（customer）
        private static void AdamasPayPrefix(object p0, object p1)
        {
            try
            {
                if (!Enabled) return;
                Pawn pawn = p0 as Pawn ?? p1 as Pawn;
                if (pawn == null) return;
                AdvanceWallet(pawn, "RP_Reason_GuestSpending".Translate());
            }
            catch { }
        }

        private static void AdamasPayPostfix(object p0, object p1)
        {
            try
            {
                Pawn pawn = p0 as Pawn ?? p1 as Pawn;
                if (pawn == null) return;
                SettleAdvance(pawn, "RP_Reason_GuestSpending".Translate());
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[RimPay] Adamas spending integration exception: {ex.Message}");
            }
        }

        // CountSilver → + 钱包余额（有钱包的访客不触发"赌资<10 离场"）
        private static void CountSilverPostfix(Pawn pawn, ref int __result)
        {
            try
            {
                if (!Enabled || inSettle) return;
                var comp = PayComp();
                if (comp == null) return;
                __result += comp.GetBalance(pawn);
            }
            catch { }
        }

        // ======== 设置开关 ========
        private static bool Enabled
        {
            get
            {
                var S = RimPayMod.settings;
                return S != null && S.enableHospitalityIntegration;
            }
        }

        private static GameComponent_RimPay PayComp()
        {
            return Current.Game?.GetComponent<GameComponent_RimPay>();
        }

        // ======== 身上白银量 ========
        private static int InventorySilver(Pawn p)
        {
            if (p?.inventory?.innerContainer == null) return 0;
            int total = 0;
            for (int i = 0; i < p.inventory.innerContainer.Count; i++)
            {
                Thing t = p.inventory.innerContainer[i];
                if (t?.def == ThingDefOf.Silver) total += t.stackCount;
            }
            return total;
        }

        // ======== 预垫付：钱包全额 → 白银入 inventory ========
        // 返回 true 表示已垫付（调用方继续原逻辑）
        private static bool AdvanceWallet(Pawn pawn, string reason)
        {
            var comp = PayComp();
            if (comp == null) return false;
            int wallet = comp.GetBalance(pawn);
            if (wallet <= 0) return false;
            if (advances.ContainsKey(pawn.thingIDNumber)) return true; // 已垫付（嵌套消费）

            inSettle = true;
            try
            {
                comp.ModifyBalance(pawn, -wallet, null); // 暂扣，未花部分对账时回补
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = wallet;
                if (!pawn.inventory.innerContainer.TryAdd(silver, true))
                {
                    comp.ModifyBalance(pawn, wallet); // 放不进去，全额退回
                    return false;
                }
                advances[pawn.thingIDNumber] = new Vector2Int(wallet, InventorySilver(pawn));
                return true;
            }
            finally
            {
                inSettle = false;
            }
        }

        // ======== 事后对账：实付优先从钱包出，剩余垫付白银收回 ========
        private static void SettleAdvance(Pawn pawn, string reason)
        {
            var rec = default(Vector2Int);
            if (!advances.TryGetValue(pawn.thingIDNumber, out rec)) return;
            advances.Remove(pawn.thingIDNumber);

            var comp = PayComp();
            if (comp == null) return;

            int now = InventorySilver(pawn);
            int spent = rec.snapshot - now; // 本次消费实付（可能为 0：没买成）
            if (spent < 0) spent = 0;

            int walletPay = Mathf.Min(spent, rec.advanced); // 钱包承担部分
            int leftover = rec.advanced - walletPay;         // 未花垫付，收回

            inSettle = true;
            try
            {
                // 未花部分回补钱包并从身上收回白银
                if (leftover > 0)
                {
                    comp.ModifyBalance(pawn, leftover);
                    RemoveSilver(pawn, leftover);
                }
                // 钱包承担部分记流水（垫付时未记 reason）
                if (walletPay > 0)
                {
                    comp.RecordWalletExpense(pawn, walletPay, reason);
                }
            }
            finally
            {
                inSettle = false;
            }
        }

        // 从身上移除指定量白银（销毁）
        private static void RemoveSilver(Pawn pawn, int amount)
        {
            var container = pawn?.inventory?.innerContainer;
            if (container == null) return;
            int remaining = amount;
            for (int i = container.Count - 1; i >= 0 && remaining > 0; i--)
            {
                Thing t = container[i];
                if (t?.def != ThingDefOf.Silver) continue;
                int take = Mathf.Min(t.stackCount, remaining);
                if (take >= t.stackCount)
                {
                    t.Destroy();
                }
                else
                {
                    t.SplitOff(take).Destroy();
                }
                remaining -= take;
            }
        }

        // ======== 挂点1：GetMoney → 白银 + 钱包（找床/挑商品资格判定） ========
        private static void GetMoneyPostfix(Pawn pawn, ref int __result)
        {
            try
            {
                if (!Enabled || inSettle) return;
                var comp = PayComp();
                if (comp == null) return;
                __result += comp.GetBalance(pawn);
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[RimPay] Hospitality GetMoney integration exception: {ex.Message}");
            }
        }

        // ======== 挂点2：售货机餐费 ========
        private static void TryPayForFoodPrefix(Pawn buyerGuest, Building_NutrientPasteDispenser dispenser, ref bool __result)
        {
            try
            {
                if (!Enabled || buyerGuest == null) return;
                AdvanceWallet(buyerGuest, "RP_Reason_GuestFood".Translate());
            }
            catch { }
        }

        private static void TryPayForFoodPostfix(Pawn buyerGuest, Building_NutrientPasteDispenser dispenser, ref bool __result)
        {
            try
            {
                if (buyerGuest == null) return;
                SettleAdvance(buyerGuest, "RP_Reason_GuestFood".Translate());
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[RimPay] Hospitality food integration exception: {ex.Message}");
            }
        }

        // ======== 挂点3：购物 ========
        private static void BuyThingPrefix(object __instance, object toil)
        {
            try
            {
                if (!Enabled) return;
                Pawn pawn = PawnOf(__instance);
                if (pawn == null) return;
                AdvanceWallet(pawn, "RP_Reason_GuestShopping".Translate());
            }
            catch { }
        }

        private static void BuyThingPostfix(object __instance, object toil)
        {
            try
            {
                Pawn pawn = PawnOf(__instance);
                if (pawn == null) return;
                SettleAdvance(pawn, "RP_Reason_GuestShopping".Translate());
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[RimPay] Hospitality shopping integration exception: {ex.Message}");
            }
        }

        // ======== 挂点4：住宿预垫付（ClaimBed 预约时，租金差额） ========
        // 白银不足租金时差额垫付，让后续 ClaimBed Toil 的
        // 「白银 >= RentalFee」判定与 TryDrop 付款自然通过（白银落地，守恒）。
        private static bool ClaimBedReservePrefix(object __instance, bool errorOnFailed)
        {
            try
            {
                if (!Enabled) return true;
                Pawn pawn = PawnOf(__instance);
                if (pawn == null) return true;

                Job job = pawn.CurJob;
                if (job == null || job.takeExtraIngestibles <= 0) return true;

                int fee = job.takeExtraIngestibles; // RentalFee
                int silver = InventorySilver(pawn);
                if (silver >= fee) return true;

                var comp = PayComp();
                if (comp == null) return true;
                int shortfall = fee - silver;
                int wallet = comp.GetBalance(pawn);
                int fromWallet = Mathf.Min(shortfall, wallet);
                if (fromWallet <= 0) return true; // 钱也不够，走原版逻辑（会失败）

                inSettle = true;
                try
                {
                    comp.ModifyBalance(pawn, -fromWallet, "RP_Reason_GuestLodging".Translate());
                    Thing s = ThingMaker.MakeThing(ThingDefOf.Silver);
                    s.stackCount = fromWallet;
                    pawn.inventory.innerContainer.TryAdd(s, true);
                }
                finally
                {
                    inSettle = false;
                }
                return true;
            }
            catch (Exception ex)
            {
                inSettle = false;
                Verse.Log.Warning($"[RimPay] Hospitality lodging integration exception: {ex.Message}");
                return true;
            }
        }

        // ======== JobDriver → Pawn ========
        private static Pawn PawnOf(object jobDriver)
        {
            JobDriver driver = jobDriver as JobDriver;
            return driver?.pawn;
        }
    }
}
