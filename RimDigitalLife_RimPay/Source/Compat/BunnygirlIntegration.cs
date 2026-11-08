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
    // RimPay × Bunnygirl Race 联动（纯反射版，零 Bunnygirl 类型引用）
    //
    // 目标：
    // 1. 停掉 CompPocketMoney 每日零花钱（原版会直接销毁地图仓库白银）
    // 2. CompPocketMoney.silver 字段与 RimPay 数字钱包对账同步：
    //    - silver 减少（赌场消费，钱已进 CasinoCashbox 落地）→ 钱包扣等额
    //    - silver 增加（赌赢，TakeColonySilver 拿了地图白银）→ 钱包加等额
    // 3. 访客/商队可玩：silver 字段直接镜像 RimPay 钱包余额
    //    （PocketMoneyInjector 给所有人类种族注入了该组件，访客原本没钱恒为 0）
    //
    // 重要：patch 方法签名只含 Verse 类型；Bunnygirl 类型（CompPocketMoney）
    // 一律 object 接收 + 反射处理，保证 RimPay 程序集类型枚举安全。
    //
    // 挂点（签名已用反编译核对，1.6/1.7 DLL 完全一致）：
    // - BunnygirlRace.CompPocketMoney.CompTick() → postfix：对账同步
    // - BunnygirlRace.CompPocketMoney.TryPayAllowance() → prefix：跳过原方法
    // ========================================================================
    public static class BunnygirlIntegration
    {
        private const string BunnyPackageId = "whoareyou.BunnygirlRace";

        private static bool initialized = false;

        // CompPocketMoney 类型缓存（反射用）
        private static Type compPocketMoneyType = null;
        private static FieldInfo silverField = null;   // public int silver

        // 对账镜像：ThingID → 上次同步时的 silver 值（transient，读档后首拍重建）
        private static readonly Dictionary<string, int> lastSyncedSilver = new Dictionary<string, int>();

        // 同步进行中标记（防递归：镜像写入 silver 触发本类逻辑）
        private static bool syncing = false;

        public static void TryInit(Harmony harmony)
        {
            if (initialized) return;
            try
            {
                if (ModLister.GetActiveModWithIdentifier(BunnyPackageId, true) == null) return;

                compPocketMoneyType = CompatPatchHelper.FindType("BunnygirlRace.CompPocketMoney");
                if (compPocketMoneyType == null)
                {
                    Verse.Log.Message("[RimPay] BunnygirlRace assembly not loaded yet, casino integration skipped.");
                    return;
                }
                silverField = compPocketMoneyType.GetField("silver", BindingFlags.Public | BindingFlags.Instance);
                if (silverField == null)
                {
                    Verse.Log.Warning("[RimPay] BunnygirlRace CompPocketMoney.silver field not found, casino integration skipped.");
                    return;
                }

                MethodInfo compTick = compPocketMoneyType.GetMethod("CompTick", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo payAllowance = compPocketMoneyType.GetMethod("TryPayAllowance", BindingFlags.Public | BindingFlags.Instance);

                if (compTick != null)
                {
                    CompatPatchHelper.Patch(harmony, compTick, null,
                        CompatPatchHelper.GetPostfix(typeof(BunnygirlIntegration), nameof(CompTickPostfix)));
                }
                if (payAllowance != null)
                {
                    CompatPatchHelper.Patch(harmony, payAllowance,
                        CompatPatchHelper.GetPostfix(typeof(BunnygirlIntegration), nameof(TryPayAllowancePrefix)),
                        null);
                }

                initialized = true;
                Verse.Log.Message("[RimPay] BunnygirlRace casino integration enabled (allowance disabled + wallet sync, visitors can play).");
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[RimPay] Bunnygirl integration init failed: {ex}");
            }
        }

        // ======== 设置开关 ========
        private static bool Enabled
        {
            get
            {
                var S = RimPayMod.settings;
                return S != null && S.enableBunnygirlIntegration;
            }
        }

        // ======== 挂点1：每日零花钱 → 跳过（不再销毁仓库白银） ========
        private static bool TryPayAllowancePrefix(object __instance)
        {
            try
            {
                if (!Enabled) return true;
                return false; // 跳过原方法：零花钱改由 RimPay 工资钱包承担
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[RimPay] Bunnygirl allowance intercept exception: {ex.Message}");
                return true;
            }
        }

        // ======== 挂点2：CompTick postfix → 对账同步 ========
        // CompTick 每 2500 tick（原方法内部节流）执行一次，postfix 顺带对账。
        // 同步规则：
        //   - 无镜像记录（首次/读档）→ silver = 钱包余额（访客拿到可玩资金）
        //   - silver 变少 → 钱包扣差额（记「赌场消费」流水）
        //   - silver 变多 → 钱包加差额（记「赌场 winnings」收益）
        //   - 钱包与 silver 任一为 0 时以 silver 为准刷新钱包（单向锚定防漂移）
        private static void CompTickPostfix(object __instance)
        {
            try
            {
                if (!Enabled || syncing) return;

                ThingWithComps parent = __instance as ThingWithComps;
                Pawn pawn = parent as Pawn;
                if (pawn == null || pawn.Dead || !pawn.RaceProps.Humanlike) return;

                var comp = PayComp();
                if (comp == null) return;

                int silver = (int)silverField.GetValue(__instance);
                string key = pawn.ThingID;
                int mirrored;
                bool hasMirror = lastSyncedSilver.TryGetValue(key, out mirrored);

                syncing = true;
                try
                {
                    if (!hasMirror)
                    {
                        // 首次同步：资金池合并（防双重扣款）
                        // silver = max(silver, 钱包余额)：赌资取两者较大值，
                        // 差额补进钱包，使两边账目一致（silver 是赌场唯一认的资产）
                        int wallet = comp.GetBalance(pawn);
                        if (silver < wallet)
                        {
                            // 钱包更富：把差额垫进 silver（净效果：钱包转赌资）
                            int add = wallet - silver;
                            silverField.SetValue(__instance, wallet);
                            // 钱包不动（silver 多出的部分等消费时再从钱包扣）
                        }
                        else if (silver > wallet)
                        {
                            // 残留零花钱更富：转入钱包统一管理（silver 不变，防重复计资）
                            comp.ModifyBalance(pawn, silver - wallet, "RP_Reason_PocketMoneyTransfer".Translate());
                        }
                        lastSyncedSilver[key] = silver;
                        return;
                    }

                    if (silver == mirrored) return; // 无变化

                    if (silver < mirrored)
                    {
                        // 消费了（赌桌/女侍付费，原 mod 已把白银放进 CasinoCashbox 落地）
                        int spent = mirrored - silver;
                        comp.ModifyBalance(pawn, -spent, "RP_Reason_CasinoSpending".Translate());
                    }
                    else
                    {
                        // 增加了（赌赢，原 mod 从地图白银拿钱给 silver）
                        int won = silver - mirrored;
                        comp.ModifyBalance(pawn, won, "RP_Reason_CasinoWinnings".Translate());
                    }
                    // 锚定：silver 保持原值，镜像刷新
                    lastSyncedSilver[key] = silver;
                }
                finally
                {
                    syncing = false;
                }
            }
            catch (Exception ex)
            {
                syncing = false;
                Verse.Log.Warning($"[RimPay] Bunnygirl wallet sync exception: {ex.Message}");
            }
        }

        private static GameComponent_RimPay PayComp()
        {
            return Current.Game?.GetComponent<GameComponent_RimPay>();
        }
    }
}
