using RimWorld;
using RimWorld.Planet;
using Verse.AI;
using Verse.Sound;
using UnityEngine;
using Verse;

namespace RimDigitalLife
{
    // ============================================================
    // PlayFeedback：观看主机时的摇摆动画 + 手柄按键音效驱动
    // 移植自 Game Night（获授权），适配观看式玩法（JobDriver_WatchBuilding）
    // ============================================================
    public static class PlayFeedback
    {
        private static AnimationDef swayDef;

        private static AnimationDef SwayDef
        {
            get
            {
                if (swayDef == null)
                {
                    swayDef = DefDatabase<AnimationDef>.GetNamedSilentFail("RDL_PlaySway");
                }
                return swayDef;
            }
        }

        private static SoundDef buttonsSound;

        private static SoundDef ButtonsSound
        {
            get
            {
                if (buttonsSound == null)
                {
                    buttonsSound = DefDatabase<SoundDef>.GetNamedSilentFail("RDL_ControllerButtons");
                }
                return buttonsSound;
            }
        }

        // 小人是否正处于“玩主机”状态（观看式：CurJob 目标 = 主机）
        public static bool IsPlaying(Pawn pawn)
        {
            if (pawn?.jobs?.curDriver is JobDriver_WatchBuilding
                && pawn.CurJob != null
                && pawn.CurJob.targetA.Thing is Building b
                && b.def.GetModExtension<ConsoleControllerExtension>() != null
                && pawn.Spawned && !pawn.Drafted && !pawn.Downed
                && (pawn.pather?.Moving ?? false) == false)
            {
                return true;
            }
            return false;
        }

        public static bool IsAnimated(Pawn pawn)
        {
            if (!IsPlaying(pawn)) return false;
            var renderer = pawn.Drawer?.renderer;
            if (renderer == null || SwayDef == null) return false;
            return renderer.CurAnimation == SwayDef;
        }

        private static float Phase(Pawn pawn)
        {
            return (Find.TickManager.TicksGame + pawn.thingIDNumber * 37) * 0.03631899f;
        }

        private static float Fade(Pawn pawn)
        {
            // 渐入：用 job 持续 tick 数近似（startTick 起 30 tick 渐入）
            Job job = pawn.jobs?.curJob;
            if (job == null || Find.TickManager == null) return 1f;
            int elapsed = Find.TickManager.TicksGame - job.startTick;
            return Mathf.Clamp01(elapsed / 30f);
        }

        public static float Angle(Pawn pawn)
        {
            return (Mathf.Sin(Phase(pawn)) * 1.7f + Mathf.Sin(Phase(pawn) * 1.73f) * 0.45f) * Fade(pawn);
        }

        public static float RenderAltitude(Pawn pawn)
        {
            return pawn.def.Altitude;
        }

        public static Vector3 Offset(Pawn pawn)
        {
            float num = Phase(pawn);
            float num2 = Fade(pawn);
            return new Vector3(
                Mathf.Sin(num) * 0.025f * num2,
                RenderAltitude(pawn) - pawn.DrawPos.y,
                Mathf.Sin(num * 1.41f) * 0.012f * num2);
        }

        public static Vector3 TransformHeldPosition(Pawn pawn, Vector3 position)
        {
            if (!IsAnimated(pawn))
            {
                return position;
            }
            return pawn.DrawPos + Offset(pawn) + Quaternion.AngleAxis(Angle(pawn), Vector3.up) * (position - pawn.DrawPos);
        }

        private static int nextButtonTick;
        private static float lastButtonRealtime = -100f;

        // 每帧驱动：设定摇摆动画 + 随机播放按键音
        public static void Tick(Pawn pawn)
        {
            if (!IsPlaying(pawn))
            {
                Stop(pawn);
                return;
            }
            var renderer = pawn.Drawer?.renderer;
            if (renderer != null && SwayDef != null && renderer.CurAnimation == null)
            {
                renderer.SetAnimation(SwayDef);
            }
            int ticksGame = Find.TickManager.TicksGame;
            if (nextButtonTick == 0 || ticksGame >= nextButtonTick)
            {
                bool hasPrev = nextButtonTick != 0;
                nextButtonTick = ticksGame + 38 + (int)((uint)(pawn.thingIDNumber * 397 + ticksGame * 17) % 53u);
                if (hasPrev && !Find.TickManager.Paused
                    && pawn.Map == Find.CurrentMap
                    && WorldRendererUtility.DrawingMap
                    && Time.realtimeSinceStartup - lastButtonRealtime >= 0.65f
                    && ButtonsSound != null)
                {
                    lastButtonRealtime = Time.realtimeSinceStartup;
                    ButtonsSound.PlayOneShot(SoundInfo.InMap(pawn));
                }
            }
        }

        public static void Stop(Pawn pawn)
        {
            var renderer = pawn?.Drawer?.renderer;
            if (renderer != null && SwayDef != null && renderer.CurAnimation == SwayDef)
            {
                renderer.SetAnimation(null);
            }
        }
    }

    // ============================================================
    // 动画 Worker（原生 keyframe 动画系统的驱动钩子）
    // 移植自 Game Night 的 AnimationWorker_GameNight
    // ============================================================
    public class AnimationWorker_RDLPlay : BaseAnimationWorker
    {
        public override bool Enabled(AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            return PlayFeedback.IsPlaying(parms.pawn);
        }

        public override Vector3 OffsetAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            return PlayFeedback.Offset(parms.pawn);
        }

        public override float AngleAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            return PlayFeedback.Angle(parms.pawn);
        }

        public override Vector3 ScaleAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            return Vector3.one;
        }

        public override GraphicStateDef GraphicStateAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            return null;
        }

        public override void PostDraw(AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms, Matrix4x4 matrix)
        {
        }
    }
}
