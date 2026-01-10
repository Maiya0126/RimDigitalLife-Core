using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimDigitalLife
{
    public class CompProperties_SmartAssistant : CompProperties
    {
        public CompProperties_SmartAssistant()
        {
            this.compClass = typeof(CompSmartAssistant);
        }
    }

    public enum AssistantPersona
    {
        Rimi,   // 感性
        Sirim   // 理性
    }

    public class CompSmartAssistant : ThingComp
    {
        public AssistantPersona currentPersona = AssistantPersona.Rimi;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref currentPersona, "currentPersona", AssistantPersona.Rimi);
        }

        // 绘制底部按钮 (Gizmos)
        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetWornGizmosExtra())
            {
                yield return g;
            }

            // 如果没装 RimTalk，不显示功能
            if (ModLister.GetActiveModWithIdentifier("cj.rimtalk") == null) yield break;

            Apparel apparel = this.parent as Apparel;
            Pawn p = apparel?.Wearer;

            if (p != null && p.Faction == Faction.OfPlayer)
            {
                // ==========================================
                // 按钮 1: 切换人格 (支持翻译)
                // ==========================================

                string switchLabel = "";
                string switchDesc = "";
                Texture2D switchIcon = null;

                if (currentPersona == AssistantPersona.Rimi)
                {
                    // "RDL_Current_Rimi" -> "当前: Rimi (感性)"
                    switchLabel = "RDL_Current_Rimi".Translate();
                    switchDesc = "RDL_SwitchDesc_ToSirim".Translate();
                    switchIcon = ContentFinder<Texture2D>.Get("UI/Icons/Icon_Rimi");
                }
                else
                {
                    // "RDL_Current_Sirim" -> "当前: Sirim (理性)"
                    switchLabel = "RDL_Current_Sirim".Translate();
                    switchDesc = "RDL_SwitchDesc_ToRimi".Translate();
                    switchIcon = ContentFinder<Texture2D>.Get("UI/Icons/Icon_Sirim");
                }

                yield return new Command_Action
                {
                    defaultLabel = switchLabel,
                    defaultDesc = switchDesc,
                    icon = switchIcon,
                    action = delegate ()
                    {
                        SwitchPersona();
                    }
                };

                // ==========================================
                // 按钮 2: 主动询问 (支持翻译)
                // ==========================================

                string askLabel = "";
                string askDesc = "";

                if (currentPersona == AssistantPersona.Rimi)
                {
                    askLabel = "RDL_AskLabel_Rimi".Translate();
                    askDesc = "RDL_AskDesc_Rimi".Translate();
                }
                else
                {
                    askLabel = "RDL_AskLabel_Sirim".Translate();
                    askDesc = "RDL_AskDesc_Sirim".Translate();
                }

                yield return new Command_Action
                {
                    defaultLabel = askLabel,
                    defaultDesc = askDesc,
                    icon = ContentFinder<Texture2D>.Get("Things/Mote/SpeechSymbols/Speech"),
                    action = delegate ()
                    {
                        TriggerAssistantChat(p, true);
                    }
                };
            }
        }

        public void SwitchPersona()
        {
            if (currentPersona == AssistantPersona.Rimi)
                currentPersona = AssistantPersona.Sirim;
            else
                currentPersona = AssistantPersona.Rimi;

            SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
        }

        // 自动触发逻辑 (闲聊)
        public override void CompTickRare()
        {
            base.CompTickRare();

            if (ModLister.GetActiveModWithIdentifier("cj.rimtalk") == null) return;

            Apparel apparel = this.parent as Apparel;
            Pawn pawn = apparel?.Wearer;

            if (pawn == null || pawn.Map == null || !pawn.Spawned) return;

            // =============================================
            // 读取设置里的频率来计算触发概率
            // =============================================
            float intervalHours = RimDigitalLifeSettings.chatIntervalHours;

            // 如果设置 <= 0.1，视为关闭
            if (intervalHours <= 0.1f) return;

            // 计算概率: CompTickRare 每250tick运行一次 (1小时=2500tick=10次检查)
            // 公式: 1 / (小时数 * 10)
            float chance = 1f / (intervalHours * 10f);

            if (Rand.Value > chance) return;

            // 触发闲聊 (isDirectQuestion = false)
            TriggerAssistantChat(pawn, false);
        }

        // 核心逻辑：触发对话
        private void TriggerAssistantChat(Pawn pawn, bool isDirectQuestion)
        {
            string defName = "";
            string visualText = "";

            // 根据人格和是否提问，选择 Def 和 飘字Key
            if (currentPersona == AssistantPersona.Rimi)
            {
                if (isDirectQuestion)
                {
                    defName = "SmartAssistant_Ask_Rimi";
                    visualText = "RDL_Mote_ConnectingRimi".Translate(); // "(正在连接 Rimi...)"
                }
                else
                {
                    defName = "SmartAssistant_Rimi";
                    visualText = "RDL_Mote_RimiPush".Translate(); // "(收到 Rimi 的推送)"
                }
            }
            else // Sirim
            {
                if (isDirectQuestion)
                {
                    defName = "SmartAssistant_Ask_Sirim";
                    visualText = "RDL_Mote_ConnectingSirim".Translate();
                }
                else
                {
                    defName = "SmartAssistant_Sirim";
                    visualText = "RDL_Mote_SirimRunning".Translate();
                }
            }

            // 获取 XML 定义
            InteractionDef intDef = DefDatabase<InteractionDef>.GetNamedSilentFail(defName);
            if (intDef == null) return;

            // 1. 视觉效果：飘字 (青色)
            MoteMaker.ThrowText(pawn.DrawPos + new Vector3(0, 0, 0.5f), pawn.Map, visualText, Color.cyan, 3.0f);

            // 2. 发送给 RimTalk
            // Recipient 填 pawn 自己，防止报错
            PlayLogEntry_Interaction entry = new PlayLogEntry_Interaction(intDef, pawn, pawn, null);
            Find.PlayLog.Add(entry);
        }
    }
}