using HarmonyLib;
using RimWorld;
using Verse;

namespace RimDigitalLife
{
    [HarmonyPatch(typeof(StatWorker), "GetValue", typeof(StatRequest), typeof(bool))]
    public static class Patch_WorkSpeedPenalty
    {
        // 【关键修改】注意参数里加了 "StatDef ___stat" (三个下划线)
        // 并且删掉了 "__instance"，因为直接用注入的变量更安全
        public static void Postfix(StatRequest req, ref float __result, StatDef ___stat)
        {
            // 1. 检查设置开关
            if (!RimDigitalMod.settings.workSpeedPenalty) return;

            // 2. 检查是否是“全局工作速度” (使用注入的变量)
            if (___stat != StatDefOf.WorkSpeedGlobal) return;

            // 3. 检查对象是否是小人
            if (!req.HasThing || !(req.Thing is Pawn pawn)) return;

            // 4. 检查是否携带了数码设备
            if (pawn.apparel != null)
            {
                foreach (var app in pawn.apparel.WornApparel)
                {
                    if (app.TryGetComp<CompTerminalLink>() != null)
                    {
                        // 惩罚 -5%
                        __result *= 0.95f;
                        return;
                    }
                }
            }
        }
    }
}