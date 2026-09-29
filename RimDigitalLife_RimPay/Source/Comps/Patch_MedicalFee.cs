using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimDigitalLife_RimPay
{
    // ========================================================================
    // RimPay 医保：医生治疗 (tend) 完成时收取诊疗费。
    // 病人钱包优先支付，不足部分由数字国库"医保报销"（病人不会因重伤破产）。
    // 诊疗收入回流数字国库，并记入国库流水账（自动出现在日报"昨日账单"）。
    // ========================================================================
    [HarmonyPatch(typeof(TendUtility), "DoTend")]
    public static class Patch_TendUtility_DoTend
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn doctor, Pawn patient, Medicine medicine)
        {
            var S = RimPayMod.settings;
            if (S == null || !S.enableMedicalFee) return;
            if (patient == null || patient.Dead) return;
            // 只对玩家殖民者与奴隶收费（囚犯/敌人和动物无钱包，不收）
            if (!(patient.IsColonistPlayerControlled || patient.IsSlaveOfColony)) return;

            GameComponent_RimPay comp = Current.Game.GetComponent<GameComponent_RimPay>();
            if (comp == null) return;

            int cost = Mathf.Max(0, S.medicalBaseFee);
            if (cost <= 0) return;

            // 钱包优先，不足部分国库医保报销
            int balance = comp.GetBalance(patient);
            int paidByWallet = Mathf.Min(balance, cost);
            int coveredByTreasury = cost - paidByWallet;

            if (paidByWallet > 0)
            {
                comp.ModifyBalance(patient, -paidByWallet, "诊疗费用");
                comp.ModifyTreasury(paidByWallet, "诊疗收入");
            }
            if (coveredByTreasury > 0)
            {
                comp.ModifyTreasury(-coveredByTreasury, "医保报销");
            }

            // 病人头顶气泡提示（直观感受经济流动）
            if (patient.Spawned && patient.Map != null)
            {
                string msg = coveredByTreasury > 0
                    ? $"诊疗费 {cost} @银（医保报销 {coveredByTreasury}）"
                    : $"诊疗费 {cost} @银";
                MoteMaker.ThrowText(patient.DrawPos + new Vector3(0f, 0f, 0.5f), patient.Map, msg, new Color(0.4f, 0.9f, 1f));
            }
        }
    }
}
