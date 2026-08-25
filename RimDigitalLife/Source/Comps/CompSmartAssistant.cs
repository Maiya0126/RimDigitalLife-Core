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
        Rimi,
        Sirim
    }

    public class CompSmartAssistant : ThingComp
    {
        private static Texture2D _iconRimi;
        private static Texture2D _iconSirim;
        private static Texture2D _iconSpeech;

        private static Texture2D IconRimi
        {
            get
            {
                if (_iconRimi == null)
                    _iconRimi = ContentFinder<Texture2D>.Get("UI/Icons/Icon_Rimi");
                return _iconRimi;
            }
        }
        private static Texture2D IconSirim
        {
            get
            {
                if (_iconSirim == null)
                    _iconSirim = ContentFinder<Texture2D>.Get("UI/Icons/Icon_Sirim");
                return _iconSirim;
            }
        }
        private static Texture2D IconSpeech
        {
            get
            {
                if (_iconSpeech == null)
                    _iconSpeech = ContentFinder<Texture2D>.Get("Things/Mote/SpeechSymbols/Speech");
                return _iconSpeech;
            }
        }

        public AssistantPersona currentPersona = AssistantPersona.Rimi;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref currentPersona, "currentPersona", AssistantPersona.Rimi);
        }

        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetWornGizmosExtra())
            {
                yield return g;
            }

            if (!RimDigitalMod.settings.enableAssistantChat) yield break;
            if (!RimTalkBridge.IsRimTalkAvailable) yield break;

            Apparel apparel = this.parent as Apparel;
            Pawn p = apparel?.Wearer;

            if (p != null && p.Faction == Faction.OfPlayer)
            {
                yield return CreatePersonaSwitchGizmo();
                yield return CreateAskGizmo(p);
            }
        }

        private Command_Action CreatePersonaSwitchGizmo()
        {
            string switchLabel;
            string switchDesc;
            Texture2D switchIcon;

            if (currentPersona == AssistantPersona.Rimi)
            {
                switchLabel = "RDL_Current_Rimi".Translate();
                switchDesc = "RDL_SwitchDesc_ToSirim".Translate();
                switchIcon = IconRimi;
            }
            else
            {
                switchLabel = "RDL_Current_Sirim".Translate();
                switchDesc = "RDL_SwitchDesc_ToRimi".Translate();
                switchIcon = IconSirim;
            }

            return new Command_Action
            {
                defaultLabel = switchLabel,
                defaultDesc = switchDesc,
                icon = switchIcon,
                action = SwitchPersona
            };
        }

        private Command_Action CreateAskGizmo(Pawn pawn)
        {
            string askLabel;
            string askDesc;

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

            return new Command_Action
            {
                defaultLabel = askLabel,
                defaultDesc = askDesc,
                icon = IconSpeech,
                action = () => TriggerAssistantChat(pawn, true)
            };
        }

        public void SwitchPersona()
        {
            currentPersona = currentPersona == AssistantPersona.Rimi 
                ? AssistantPersona.Sirim 
                : AssistantPersona.Rimi;
            SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
        }

        public override void CompTickRare()
        {
            base.CompTickRare();

            if (!RimDigitalMod.settings.enableAssistantChat) return;
            if (!RimTalkBridge.IsRimTalkAvailable) return;

            Apparel apparel = this.parent as Apparel;
            Pawn pawn = apparel?.Wearer;

            if (pawn == null || pawn.Map == null || !pawn.Spawned) return;

            float intervalHours = RimDigitalMod.settings.chatIntervalHours;
            if (intervalHours <= 0.1f) return;

            float chance = 1f / (intervalHours * 10f);
            if (Rand.Value > chance) return;

            TriggerAssistantChat(pawn, false);
        }

        private void TriggerAssistantChat(Pawn pawn, bool isDirectQuestion)
        {
            if (!RimDigitalMod.settings.enableAssistantChat) return;
            
            Log.Message($"[RimDigitalLife] TriggerAssistantChat called: pawn={pawn?.LabelShort}, isDirectQuestion={isDirectQuestion}, persona={currentPersona}");
            
            string visualText;
            string systemPrompt;
            string userPrompt;

            if (currentPersona == AssistantPersona.Rimi)
            {
                visualText = isDirectQuestion 
                    ? "RDL_Mote_ConnectingRimi".Translate() 
                    : "RDL_Mote_RimiPush".Translate();
                systemPrompt = RimTalkBridge.GetRimiSystemPrompt(isDirectQuestion);
            }
            else
            {
                visualText = isDirectQuestion 
                    ? "RDL_Mote_ConnectingSirim".Translate() 
                    : "RDL_Mote_SirimRunning".Translate();
                systemPrompt = RimTalkBridge.GetSirimSystemPrompt(isDirectQuestion);
            }

            userPrompt = RimTalkBridge.GetUserPromptForPawn(pawn, isDirectQuestion);

            Pawn recipient = pawn;

            Log.Message($"[RimDigitalLife] Calling TriggerDialogue: integrationMode={RimTalkBridge.IntegrationMode}");
            bool success = RimTalkBridge.TriggerDialogue(pawn, recipient, systemPrompt, userPrompt, isDirectQuestion, currentPersona);
            Log.Message($"[RimDigitalLife] TriggerDialogue result: {success}");

            if (success)
            {
                MoteMaker.ThrowText(pawn.DrawPos + new Vector3(0, 0, 0.5f), pawn.Map, visualText, Color.cyan, 3.0f);
                AddThoughtToPawn(pawn, "RimDigital_ConsultedAI");
            }
        }

        private void AddThoughtToPawn(Pawn pawn, string thoughtDefName)
        {
            ThoughtDef thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail(thoughtDefName);
            if (thoughtDef == null || pawn?.needs?.mood?.thoughts?.memories == null) return;

            Thought_Memory socialMemory = (Thought_Memory)ThoughtMaker.MakeThought(thoughtDef);
            if (socialMemory == null) return;

            pawn.needs.mood.thoughts.memories.TryGainMemory(socialMemory, pawn);
        }

        private Pawn FindNearbyColonist(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null) return null;

            Pawn closestPawn = null;
            float closestDist = 999999f;

            foreach (Pawn otherPawn in pawn.Map.mapPawns.AllPawns)
            {
                if (otherPawn == pawn) continue;

                if (otherPawn.Faction == Faction.OfPlayer &&
                    otherPawn.RaceProps.Humanlike &&
                    !otherPawn.Dead &&
                    otherPawn.Spawned &&
                    otherPawn.Awake())
                {
                    float dist = pawn.Position.DistanceTo(otherPawn.Position);
                    if (dist < closestDist && dist <= 50f)
                    {
                        closestDist = dist;
                        closestPawn = otherPawn;
                    }
                }
            }

            return closestPawn;
        }
    }
}