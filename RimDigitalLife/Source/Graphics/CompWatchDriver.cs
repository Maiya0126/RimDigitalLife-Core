using Verse;

namespace RimDigitalLife
{
    // ============================================================
    // CompWatchDriver：主机建筑上驱动 PlayFeedback（动画/音效）的 ThingComp
    // 每帧扫描本建筑附近的观看小人并驱动反馈——Game Night 把这段放在
    // JobDriver_Play 的 PreTick；我们的观看式玩法下 JobDriver 是原生的，
    // 所以在建筑侧每帧驱动（数量=正在玩的人数，开销可忽略）。
    // ============================================================
    public class CompWatchDriver : ThingComp
    {
        private CompConsoleControllers Controllers => parent.GetComp<CompConsoleControllers>();

        public override void CompTick()
        {
            base.CompTick();
            TickAllWatchers();
        }

        // 驱动全部观看者（每帧由 CompTick 调用）
        public void TickAllWatchers()
        {
            var controllers = Controllers;
            if (controllers == null) return;
            for (int i = 0; i < 2; i++)
            {
                Pawn p = controllers.PlayerAt(i);
                if (p != null)
                {
                    PlayFeedback.Tick(p);
                }
            }
        }
    }
}
