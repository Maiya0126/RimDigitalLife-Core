using System.Collections.Generic;
using System.Linq;
using RimDigitalLife;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimDigitalLife_QuantumNet
{
    // ============================================================
    // RimTalk 跨地图通话（需 RimTalk + 量子无限包）
    // 触发方：远征队/异地小人（每 2 天随机一个无限包订阅者"发起通话"）
    // 呈现方：基地小人「接到来电」并回应（RimTalk 只处理地图上的小人，
    //        远征队小人不在其扫描范围，故由基地小人承载对话气泡）
    // Prompt 注入地理与情感上下文：距离主基地 X 公里、环境、来电人
    // 展示形式：📡 [远征队·小明 ➔ 基地·小红]
    // 注意：本工程基于 1.6.4871 API（PlanetTile / Find.WorldObjects.Caravans）
    // ============================================================
    public static class CrossMapCallManager
    {
        private static int lastCallTick = 0;

        // 由 GameComponent 低频驱动（每 2 天）
        public static void Tick()
        {
            var S = QuantumNetMod.settings;
            if (S == null || !S.enableRimTalkCall) return;
            var comp = GameComponent_QuantumNet.Instance;
            if (comp == null) return;

            Map home = Find.AnyPlayerHomeMap;
            if (home == null) return;
            if (!RimTalkBridge.IsRimTalkAvailable) return;
            if (!comp.HasNetworkCoverage(home)) return;
            if (NetworkEventManager.NetworkDown) return; // 星链风暴期间通话中断

            int tick = Find.TickManager.TicksGame;
            if (tick - lastCallTick < 120000) return; // 每 2 天
            lastCallTick = tick;

            List<Pawn> travelers = GetCaravanColonists();
            if (travelers.Count == 0) return;

            // 仅量子无限包订阅者可使用跨地图通话
            List<Pawn> eligible = travelers.Where(p => comp.HasInfinitePlan(p)).ToList();
            if (eligible.Count == 0) return;

            Pawn caller = eligible.RandomElement();
            Pawn recipient = PickBaseColonist(home, caller);
            TriggerCall(caller, recipient, home);
        }

        private static List<Pawn> GetCaravanColonists()
        {
            var result = new List<Pawn>();
            foreach (Caravan c in Find.WorldObjects.Caravans)
            {
                if (c == null || c.PawnsListForReading == null) continue;
                foreach (Pawn p in c.PawnsListForReading)
                {
                    if (p != null && p.IsFreeColonist && !p.Dead)
                    {
                        result.Add(p);
                    }
                }
            }
            return result;
        }

        private static Pawn PickBaseColonist(Map home, Pawn exclude)
        {
            List<Pawn> colonists = home.mapPawns.FreeColonists
                .Where(p => !p.Dead && p != exclude)
                .ToList();
            if (colonists.Count == 0) return exclude;
            return colonists.RandomElement();
        }

        // 基地小人「接到来电」：RimTalk 生成基地小人的回应气泡
        private static void TriggerCall(Pawn caller, Pawn recipient, Map home)
        {
            string biomeName = "荒野";
            string distanceText = "？";
            bool found = false;

            foreach (Caravan c in Find.WorldObjects.Caravans)
            {
                if (c != null && c.PawnsListForReading != null && c.PawnsListForReading.Contains(caller))
                {
                    PlanetTile callerTile = c.Tile;
                    PlanetTile homeTile = home.Tile;
                    if (callerTile.Valid && homeTile.Valid)
                    {
                        int dist = (int)Find.WorldGrid.ApproxDistanceInTiles(callerTile, homeTile);
                        distanceText = dist.ToString();
                        var wt = Find.WorldGrid[callerTile];
                        if (wt != null && wt.PrimaryBiome != null)
                        {
                            biomeName = wt.PrimaryBiome.label;
                        }
                    }
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                biomeName = "未知区域";
                distanceText = "远方的";
            }

            // 基地小人视角：接到来电并回应（RimTalk 必定处理地图上的小人）
            string userPrompt = $"你的好友{caller.LabelShort}正跟随远征队，在距离主基地 {distanceText} 公里的「{biomeName}」中。"
                + $"刚才{caller.LabelShort}给你打来了一个跨地图视频电话，信号有些延迟，{caller.LabelShort}说非常想念你和大家，聊了远征队的近况。"
                + $"请以{recipient.LabelShort}的口吻，说出你在通话中对{caller.LabelShort}说的话（关心、叮嘱、分享基地近况）。";

            bool ok = RimTalkBridge.TriggerDialogue(recipient, recipient, "跨地图通话", userPrompt, false, AssistantPersona.Rimi);

            Verse.Log.Message($"[量子网络] 📡 [远征队·{caller.LabelShort} ➔ 基地·{recipient.LabelShort}]: 跨地图通话已接通 {(ok ? "成功" : "队列失败")}");
        }

        // 开发者模式测试入口（绕过冷却/套餐/覆盖检查，直接走完整通话链路）
        public static void DebugTriggerCall(Pawn caller, Pawn recipient, Map home)
        {
            TriggerCall(caller, recipient, home);
        }
    }
}
