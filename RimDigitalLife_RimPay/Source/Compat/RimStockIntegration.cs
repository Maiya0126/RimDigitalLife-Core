using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimDigitalLife_RimPay
{
    // ========================================================================
    // RimPay × RimStock 地球股市联动（纯反射版，零 RimStocks 类型引用）
    //
    // 命名说明：RimStock 的券商市场称为「地球股市」，与 RimPay 自带的
    // 「环世界证券市场」（原生生成的派系股票）是两个独立市场，互不相通。
    //
    // 目标：RimStock 券商账户（纯虚拟数字）可直接使用 RimPay 数字国库资金——
    //   - 入金额度判定：可入 = 地图储存白银 + 数字国库余额
    //   - 入金：地图白银优先（原逻辑销毁），不足部分从数字国库扣（虚拟对虚拟，零损耗）
    //   - 出金：原逻辑空投白银不动；玩家可把白银再存进国库（国库终端现有功能）
    //
    // 重要：patch 方法签名只含 Verse 类型；RimStocks 类型一律 object 接收。
    //
    // 挂点（签名已用反编译核对，1.6/1.7 DLL）：
    // - RimStocks.Game.SilverTransfer.AvailableStoredSilver() → postfix：
    //     可入金额 = 地图白银 + 数字国库（Dialog_Transfer 的最大额度显示与判定）
    // - RimStocks.Game.SilverTransfer.TryDeposit(StocksGameComponent, int) → prefix：
    //     地图白银不足时，先从数字国库扣差额并直接调用原方法剩余部分；
    //     实现：prefix 计算地图白银可覆盖部分，差额走国库，注入 amount 只留白银部分
    // ========================================================================
    public static class RimStockIntegration
    {
        private const string RimStockPackageId = "Agenl.RimStocks";

        private static bool initialized = false;

        // TryDeposit prefix→postfix 传递：本次由国库代付的额度
        private static int treasuryPart = 0;

        // AvailablePostfix 重入保护：TryDepositPrefix 反射调用 AvailableStoredSilver
        // 时会再次触发本类 postfix，此时须返回纯地图白银（未叠加国库的原始值）
        private static bool inDepositFlow = false;

        public static void TryInit(Harmony harmony)
        {
            if (initialized) return;
            try
            {
                if (ModLister.GetActiveModWithIdentifier(RimStockPackageId, true) == null) return;

                Type transferType = CompatPatchHelper.FindType("RimStocks.Game.SilverTransfer");
                if (transferType == null)
                {
                    Verse.Log.Message("[RimPay] RimStock assembly not loaded yet, stock market integration skipped.");
                    return;
                }

                MethodInfo avail = transferType.GetMethod("AvailableStoredSilver", BindingFlags.Public | BindingFlags.Static);
                MethodInfo tryDeposit = transferType.GetMethod("TryDeposit", BindingFlags.Public | BindingFlags.Static);

                if (avail != null)
                {
                    CompatPatchHelper.Patch(harmony, avail, null,
                        CompatPatchHelper.GetPostfix(typeof(RimStockIntegration), nameof(AvailablePostfix)));
                }
                if (tryDeposit != null)
                {
                    CompatPatchHelper.Patch(harmony, tryDeposit,
                        CompatPatchHelper.GetPostfix(typeof(RimStockIntegration), nameof(TryDepositPrefix)),
                        CompatPatchHelper.GetPostfix(typeof(RimStockIntegration), nameof(TryDepositPostfix)));
                }

                initialized = true;
                Verse.Log.Message("[RimPay] RimStock earth stock market integration enabled (broker deposits can use digital treasury).");
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[RimPay] RimStock integration init failed: {ex}");
            }
        }

        // ======== 设置开关 ========
        private static bool Enabled
        {
            get
            {
                var S = RimPayMod.settings;
                return S != null && S.enableRimStockIntegration;
            }
        }

        private static GameComponent_RimPay PayComp()
        {
            return Current.Game?.GetComponent<GameComponent_RimPay>();
        }

        // ======== 挂点1：可入金额 = 地图白银 + 数字国库 ========
        private static void AvailablePostfix(ref int __result)
        {
            try
            {
                if (!Enabled || inDepositFlow) return; // 防自嵌套：prefix 内部调用时返回原始值
                var comp = PayComp();
                if (comp == null) return;
                long total = (long)__result + comp.CloudTreasuryBalance;
                __result = (int)Math.Min(total, int.MaxValue);
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[RimPay] RimStock AvailablePostfix exception: {ex.Message}");
            }
        }

        // ======== 挂点2：入金 prefix —— 地图白银优先，差额国库补 ========
        // 注入 amount：只留"地图白银能覆盖的部分"给原方法销毁；
        // 差额在 prefix 里直接从国库扣，原方法随后 Broker.Deposit(amount) 是全量……
        // 注意：原方法 Deposit 的是完整 amount（销毁白银后把全额记入券商账户），
        // 因此正确做法是：amount 缩减为白银部分，国库差额部分在 prefix 里
        // 直接调用 Broker.Deposit 补记账——但 Broker 是实例成员（c.Broker），
        // prefix 拿不到返回值包装。改为 postfix 方案：原方法全程用白银部分跑完
        // （白银不足时原方法会 return InsufficientFunds）……
        // ——最终采用：prefix 注入 amount = 白银可覆盖部分（若白银 >= 全额则不动），
        // 国库差额在 postfix 里通过反射调用 c.Broker.Deposit(treasuryPart) 补记账。
        private static bool TryDepositPrefix(object c, ref int amount, ref object __result)
        {
            try
            {
                treasuryPart = 0;
                if (!Enabled || amount <= 0) return true;
                var comp = PayComp();
                if (comp == null) return true;

                // 反射取纯地图白银（inDepositFlow 抑制本类 AvailablePostfix 叠加国库）
                Type transferType = CompatPatchHelper.FindType("RimStocks.Game.SilverTransfer");
                MethodInfo avail = transferType?.GetMethod("AvailableStoredSilver", BindingFlags.Public | BindingFlags.Static);
                if (avail == null) return true;
                inDepositFlow = true;
                int storedSilver;
                try
                {
                    storedSilver = (int)avail.Invoke(null, null);
                }
                finally
                {
                    inDepositFlow = false;
                }

                if (storedSilver >= amount) return true; // 白银足够，走原逻辑

                treasuryPart = amount - storedSilver; // 国库承担部分
                amount = storedSilver;                 // 原方法只处理白银部分
                if (amount <= 0)
                {
                    // 白银为零：全额国库出，原方法无白银可销毁。
                    // 原方法对 amount<=0 会返回 AmountTooSmall，跳过它，
                    // 全量记账放 postfix（treasuryPart 已记录）
                    __result = Enum.Parse(CompatPatchHelper.FindType("RimStocks.Core.Broker.RejectReason"), "None");
                    return false;
                }
                return true; // 白银部分交给原方法（销毁 + Deposit 白银额）
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[RimPay] RimStock TryDeposit prefix exception: {ex.Message}");
                return true;
            }
        }

        // postfix：国库差额补记账（反射 c.Broker.Deposit(treasuryPart)）+ 国库扣款
        // 仅当原方法（或 prefix 跳过分支）成功时执行
        private static void TryDepositPostfix(object c, int amount, ref object __result)
        {
            try
            {
                if (treasuryPart <= 0) return;
                int part = treasuryPart;
                treasuryPart = 0;
                if (!Enabled) return;

                // 原方法返回 RejectReason；None = 成功
                string reasonName = __result?.ToString();
                if (reasonName != "None") return; // 白银部分失败，国库不动

                var comp = PayComp();
                if (comp == null) return;

                // 国库扣款（虚拟对虚拟）
                if (!comp.TryPayFromTreasury(part, "RP_Reason_EarthStockDeposit".Translate()))
                {
                    // 国库余额在 prefix 后发生了变化（理论罕见）：本次放弃补记账
                    Verse.Log.Warning($"[RimPay] RimStock deposit: digital treasury balance insufficient (need {part}), treasury part not deposited.");
                    return;
                }

                // 券商账户补记账：c.Broker.Deposit(part)
                try
                {
                    object broker = c?.GetType().GetProperty("Broker")?.GetValue(c, null)
                                    ?? c?.GetType().GetField("Broker")?.GetValue(c);
                    if (broker == null)
                    {
                        // 属性名可能不同：反射找 Broker 成员
                        foreach (var pi in c.GetType().GetProperties())
                        {
                            if (pi.Name == "Broker") { broker = pi.GetValue(c, null); break; }
                        }
                    }
                    MethodInfo deposit = broker?.GetType().GetMethod("Deposit", new[] { typeof(long) });
                    deposit?.Invoke(broker, new object[] { (long)part });
                }
                catch (Exception ex)
                {
                    Verse.Log.Warning($"[RimPay] RimStock broker deposit bookkeeping exception: {ex.Message}");
                }

                Messages.Message("RP_Int_EarthStockDepositPaid".Translate(part), MessageTypeDefOf.NeutralEvent, false);
            }
            catch (Exception ex)
            {
                treasuryPart = 0;
                Verse.Log.Warning($"[RimPay] RimStock TryDeposit postfix exception: {ex.Message}");
            }
        }
    }
}
