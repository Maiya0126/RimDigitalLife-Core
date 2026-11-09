using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse.AI;
using Verse;

namespace RimDigitalLife
{
    // ============================================================
    // ConsoleControllerExtension：手柄休息槽位坐标（四向）
    // 数据源自 Game Night（获授权），在 Def modExtensions 里配置
    // ============================================================
    public class ConsoleControllerExtension : DefModExtension
    {
        // 与 Game Night 同构：每个朝向一个 Vector3 列表（2 个手柄槽位）
        public List<Vector3> south = new List<Vector3>();
        public List<Vector3> east = new List<Vector3>();
        public List<Vector3> north = new List<Vector3>();
        public List<Vector3> west = new List<Vector3>();

        public List<Vector3> RestSlots(Rot4 rot)
        {
            if (rot == Rot4.North) return north;
            if (rot == Rot4.East) return east;
            if (rot == Rot4.South) return south;
            return west;
        }
    }

    // ============================================================
    // CompConsoleControllers：手柄渲染 Comp
    // 移植自 Game Night（获授权）：观看小人未到位时手柄放在主机槽位，
    // 到位后手柄移到小人手上并叠加肤色手掌。
    // 适配我们模组的观看式玩法（JobDriver_WatchBuilding）。
    // ============================================================
    public class CompConsoleControllers : ThingComp
    {
        private const float HeldSize = 0.46f;

        private string controllerPathRoot = "";   // 如 Things/Building/Controllers/RimStation
        private Graphic controller;
        private Graphic hands;

        public CompProperties_ConsoleControllers Props => (CompProperties_ConsoleControllers)props;

        private Building Parent => (Building)parent;

        // 观看该建筑的小人（最多 2 个手柄位）：观看式玩法下小人站在 watchRect，
        // 这里取互动格/前排两个观看位。简单可靠的判定：建筑周围的坐下小人中被保留者。
        public int HeldCount => (PlayerAt(0) != null ? 1 : 0) + (PlayerAt(1) != null ? 1 : 0);

        private List<Pawn> cachedWatchers = new List<Pawn>();
        private int cacheTick = -999;

        private void RefreshWatchers()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now == cacheTick) return;
            cacheTick = now;
            cachedWatchers.Clear();
            Map map = parent.Map;
            if (map == null) return;
            // 找正在"观看"该建筑的小人：CurJob 目标是本建筑且在娱乐 toil
            foreach (Pawn p in map.mapPawns.FreeColonistsAndPrisoners)
            {
                if (p == null || p.Dead || !p.Spawned) continue;
                var job = p.CurJob;
                if (job == null) continue;
                if (job.targetA.Thing != parent) continue;
                if (!(p.jobs?.curDriver is JobDriver_WatchBuilding)) continue;
                if (p.Drafted || p.Downed || p.pather?.Moving != false) continue;
                cachedWatchers.Add(p);
                if (cachedWatchers.Count >= 2) break;
            }
        }

        public Pawn PlayerAt(int index)
        {
            RefreshWatchers();
            return index < cachedWatchers.Count ? cachedWatchers[index] : null;
        }

        // 手柄在手上的位置（跟随小人朝向；同 Game Night）
        public static Vector3 HeldPosition(Pawn pawn)
        {
            Vector3 drawPos = pawn.DrawPos;
            if (pawn.Rotation == Rot4.North)
            {
                drawPos.z += 0.46f;
                drawPos.y -= 0.05f;
            }
            else
            {
                drawPos += pawn.Rotation.FacingCell.ToVector3() * (pawn.Rotation.IsHorizontal ? 0.32f : 0.2f);
                if (pawn.Rotation.IsHorizontal)
                {
                    drawPos.z -= 0.26f;
                }
                drawPos.y = AltitudeLayer.PawnUnused.AltitudeFor() + 0.002f;
            }
            return PlayFeedback.TransformHeldPosition(pawn, drawPos);
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (!parent.Spawned) return;

            string platform = Props?.platform;
            if (string.IsNullOrEmpty(platform)) platform = parent.def.defName;

            if (controller == null || controllerPathRoot != platform)
            {
                controllerPathRoot = platform;
                controller = GraphicDatabase.Get<Graphic_Single>(
                    "Things/Building/Controllers/" + platform,
                    ShaderDatabase.Cutout, new Vector2(HeldSize, HeldSize), Color.white);
                if (controller == null || controller == BaseContent.BadTex)
                {
                    Log.Warning($"[RimDigitalLife] 手柄贴图加载失败: Things/Building/Controllers/{platform}（检查玩家文件完整性）");
                }
                hands = GraphicDatabase.Get<Graphic_Single>(
                    "Things/Building/Controllers/Hands",
                    ShaderDatabase.Cutout, new Vector2(HeldSize, HeldSize), Color.white);
                if (hands == null || hands == BaseContent.BadTex)
                {
                    Log.Warning("[RimDigitalLife] 手掌蒙皮加载失败: Things/Building/Controllers/Hands（检查玩家文件完整性）");
                }
            }

            List<Vector3> slots = parent.def.GetModExtension<ConsoleControllerExtension>()?.RestSlots(parent.Rotation);
            if (slots == null || slots.Count < 2) return;

            for (int i = 0; i < 2; i++)
            {
                Pawn pawn = PlayerAt(i);
                if (pawn == null)
                {
                    // 手柄闲置在主机前
                    Vector3 loc = parent.DrawPos + slots[i];
                    loc.y = AltitudeLayer.BuildingOnTop.AltitudeFor() + 0.003f;
                    controller.Draw(loc, parent.Rotation.Opposite, parent);
                }
                else
                {
                    // 手柄在小人手上 + 肤色手掌覆盖
                    Vector3 vector = HeldPosition(pawn);
                    float extraRotation = (PlayFeedback.IsAnimated(pawn) ? PlayFeedback.Angle(pawn) : 0f);
                    controller.Draw(vector, pawn.Rotation, parent, extraRotation);
                    Color color = pawn.story?.SkinColor ?? Color.white;
                    hands.Draw(vector + new Vector3(0f, 0.0005f, 0f), pawn.Rotation, parent, extraRotation);
                }
            }
        }
    }

    public class CompProperties_ConsoleControllers : CompProperties
    {
        // 手柄贴图名（Things/Building/Controllers/ 下的文件名）
        public string platform = "";

        public CompProperties_ConsoleControllers()
        {
            compClass = typeof(CompConsoleControllers);
        }

        public CompProperties_ConsoleControllers(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
