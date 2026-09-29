using HarmonyLib;
using RimWorld;
using Verse;

namespace RimDigitalLife_QuantumNet
{
    // 量子无限包「防分心」：抵消 Core 的分心惩罚（携带数码设备时全局工作速度 -5%）
    // 依赖加载顺序：QuantumNet loadAfter Core → 本 Postfix 在 Core 的 Patch_WorkSpeedPenalty 之后执行
    [HarmonyPatch(typeof(StatWorker), "GetValue", typeof(StatRequest), typeof(bool))]
    public static class Patch_QuantumNet_AntiDistraction
    {
        [HarmonyPostfix]
        public static void Postfix(StatRequest req, ref float __result, StatDef ___stat)
        {
            if (___stat != StatDefOf.WorkSpeedGlobal) return;
            if (!req.HasThing || !(req.Thing is Pawn pawn)) return;

            var S = QuantumNetMod.settings;
            if (S == null || !S.enableRimSeek) return;
            var comp = GameComponent_QuantumNet.Instance;
            if (comp == null) return;

            // 仅量子无限包用户且携带设备且在线时抵消 Core 的 -5%
            if (comp.HasInfinitePlan(pawn)
                && RimSeekBuffManager.HasDigitalDevice(pawn)
                && comp.HasNetworkCoverage(pawn.MapHeld))
            {
                __result /= 0.95f;
            }
        }
    }
}
