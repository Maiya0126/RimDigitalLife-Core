using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;
using System.Linq;

namespace RimDigitalLife
{
    public class RimDigitalMod : Mod
    {
        public static RimDigitalLifeSettings settings;
        private static Harmony harmony;

        // 模组版本（与 About.xml modVersion 同步维护）
        public const string ModVersion = "v1.2.3";

        public RimDigitalMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<RimDigitalLifeSettings>();
            
            if (harmony == null)
            {
                harmony = new Harmony("com.rimdigitallife.mod");
                harmony.PatchAll();
            }

            ApplySpeedSettings();
            RealTimeReminder.Initialize();
        }

        public override string SettingsCategory()
        {
            return "Rim Digital Life - Core (v1.2.3)";
        }

        private Vector2 scrollPosition = Vector2.zero;

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, 1900f);
            
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            listing.Label("<b>" + "RDL_Settings_SlackerTitle".Translate() + "</b>");
            listing.GapLine();

            listing.Label("RDL_Settings_Chance".Translate(settings.slackChance * 100));
            settings.slackChance = listing.Slider(settings.slackChance, 0f, 0.10f);

            listing.Label("RDL_Settings_Radius".Translate(settings.detectionRadius));
            settings.detectionRadius = listing.Slider(settings.detectionRadius, 1f, 20f);
            listing.Gap();

            listing.Label("RDL_Settings_MoodBonus".Translate(settings.moodBonusValue));
            float oldMood = settings.moodBonusValue;
            settings.moodBonusValue = listing.Slider(settings.moodBonusValue, 1f, 20f);

            if (oldMood != settings.moodBonusValue)
            {
                ApplyMoodSettings();
            }
            listing.Gap();

            listing.Label("<b>" + "RDL_Settings_AllowedDevices".Translate() + "</b>");
            listing.CheckboxLabeled("RDL_Settings_AllowComm".Translate(), ref settings.allowCommSlacking);
            listing.CheckboxLabeled("RDL_Settings_AllowGame".Translate(), ref settings.allowGameSlacking);
            listing.Gap();

            listing.Label("<b>" + "RDL_Settings_PenaltyTitle".Translate() + "</b>");
            listing.CheckboxLabeled("RDL_Settings_ShowBubbles".Translate(), ref settings.showMoteText);
            listing.CheckboxLabeled("RDL_Settings_StrictMode".Translate(), ref settings.strictMode, "RDL_Settings_StrictDesc".Translate());
            listing.CheckboxLabeled("RDL_Settings_Penalty".Translate(), ref settings.workSpeedPenalty, "RDL_Settings_PenaltyDesc".Translate());
            listing.GapLine();

            listing.Label("<b>" + "RDL_Settings_MobilityTitle".Translate() + "</b>");
            listing.Gap();
            listing.Label("RDL_Settings_SkateFall".Translate(settings.skateboardFallChance * 100));
            settings.skateboardFallChance = listing.Slider(settings.skateboardFallChance, 0f, 1f);
            listing.Label("RDL_Settings_HoverFall".Translate(settings.hoverboardFallChance * 100));
            settings.hoverboardFallChance = listing.Slider(settings.hoverboardFallChance, 0f, 1f);
            listing.Gap();

            listing.Label("<b>" + "RDL_Settings_SpeedTitle".Translate() + "</b>");
            float oldSkateSpeed = settings.skateboardSpeedBonus;
            listing.Label("RDL_Settings_SkateSpeed".Translate(settings.skateboardSpeedBonus));
            settings.skateboardSpeedBonus = listing.Slider(settings.skateboardSpeedBonus, 0f, 50f);

            float oldHoverSpeed = settings.hoverboardSpeedBonus;
            listing.Label("RDL_Settings_HoverSpeed".Translate(settings.hoverboardSpeedBonus));
            settings.hoverboardSpeedBonus = listing.Slider(settings.hoverboardSpeedBonus, 0f, 50f);

            if (oldSkateSpeed != settings.skateboardSpeedBonus || oldHoverSpeed != settings.hoverboardSpeedBonus)
            {
                ApplySpeedSettings();
            }

            listing.GapLine();
            listing.Label("<b>" + "RDL_Settings_AssistantTitle".Translate() + "</b>");
            listing.Gap();

            listing.CheckboxLabeled("RDL_Settings_EnableAssistant".Translate(), ref settings.enableAssistantChat,
                "RDL_Settings_EnableAssistantDesc".Translate());

            if (settings.enableAssistantChat)
            {
                listing.Label("RDL_Settings_ChatInterval".Translate(settings.chatIntervalHours));
                settings.chatIntervalHours = listing.Slider(settings.chatIntervalHours, 1f, 24f);
            }

            listing.GapLine();
            listing.Label("<b>" + "RDL_Settings_ReminderTitle".Translate() + "</b>");
            listing.Label("<i>" + "RDL_Settings_ReminderSub".Translate() + "</i>");
            listing.Gap();

            listing.CheckboxLabeled("RDL_Settings_EyeEnable".Translate(), ref settings.enableEyeReminder,
                "RDL_Settings_EyeEnableDesc".Translate());
            if (settings.enableEyeReminder)
            {
                listing.Label("  " + "RDL_Settings_Interval".Translate(settings.eyeReminderMinutes));
                settings.eyeReminderMinutes = listing.Slider(settings.eyeReminderMinutes, 15f, 120f);
            }

            listing.CheckboxLabeled("RDL_Settings_WaterEnable".Translate(), ref settings.enableWaterReminder,
                "RDL_Settings_WaterEnableDesc".Translate());
            if (settings.enableWaterReminder)
            {
                listing.Label("  " + "RDL_Settings_Interval".Translate(settings.waterReminderMinutes));
                settings.waterReminderMinutes = listing.Slider(settings.waterReminderMinutes, 30f, 180f);
            }

            listing.CheckboxLabeled("RDL_Settings_LateNightEnable".Translate(), ref settings.enableLateNightReminder,
                "RDL_Settings_LateNightEnableDesc".Translate());
            if (settings.enableLateNightReminder)
            {
                listing.Label("  " + "RDL_Settings_LateNightRange".Translate(settings.lateNightStartHour, settings.lateNightEndHour));
                listing.Label("  " + "RDL_Settings_StartTime".Translate());
                settings.lateNightStartHour = (int)listing.Slider(settings.lateNightStartHour, 20f, 24f);
                listing.Label("  " + "RDL_Settings_EndTime".Translate());
                settings.lateNightEndHour = (int)listing.Slider(settings.lateNightEndHour, 0f, 8f);
                listing.Label("  " + "RDL_Settings_Interval".Translate(settings.lateNightReminderIntervalMinutes));
                settings.lateNightReminderIntervalMinutes = listing.Slider(settings.lateNightReminderIntervalMinutes, 30f, 180f);
            }

            listing.Gap();
            listing.Label("<b>" + "RDL_Settings_ReminderStyle".Translate() + "</b>");
            listing.CheckboxLabeled("RDL_Settings_UseDialog".Translate(), ref settings.reminderUseDialog,
                "RDL_Settings_UseDialogDesc".Translate());
            listing.CheckboxLabeled("RDL_Settings_PauseGame".Translate(), ref settings.reminderPauseGame,
                "RDL_Settings_PauseGameDesc".Translate());
            listing.Label("<i>" + "RDL_Settings_PlayTime".Translate(RealTimeReminder.GetPlayTimeFormatted()) + "</i>");

            // 测试提示功能按钮（立即弹出一个提醒窗口预览效果，不用等间隔）
            listing.Gap(6f);
            if (listing.ButtonText("RDL_Settings_TestReminder".Translate()))
            {
                Find.WindowStack.Add(new ReminderDialog(
                    "RDL_Reminder_Water_Title".Translate(),
                    "RDL_Reminder_Water_Message".Translate(),
                    settings.reminderPauseGame));
            }
            listing.Gap(4f);

            listing.Gap(6f);
            listing.Label("<b>" + "RDL_Settings_Version".Translate(ModVersion) + "</b>");
            listing.End();
            Widgets.EndScrollView();
            base.DoSettingsWindowContents(inRect);
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            ApplyMoodSettings();
            ApplySpeedSettings();
        }

        public static void ApplyMoodSettings()
        {
            var def = DefDatabase<ThoughtDef>.GetNamed("RimDigital_SlackingOff", false);
            if (def != null && def.stages.Count > 0)
            {
                def.stages[0].baseMoodEffect = settings.moodBonusValue;
            }
        }

        public static void ApplySpeedSettings()
        {
            var skateboardDef = DefDatabase<ThingDef>.GetNamed("Apparel_BoosDeadSkateboard", false);
            if (skateboardDef != null && skateboardDef.equippedStatOffsets != null)
            {
                var moveSpeedStat = skateboardDef.equippedStatOffsets.FirstOrDefault(x => x.stat == StatDefOf.MoveSpeed);
                if (moveSpeedStat != null)
                {
                    moveSpeedStat.value = settings.skateboardSpeedBonus;
                }
                else
                {
                    skateboardDef.equippedStatOffsets.Add(new StatModifier { stat = StatDefOf.MoveSpeed, value = settings.skateboardSpeedBonus });
                }
            }

            var hoverboardDef = DefDatabase<ThingDef>.GetNamed("Apparel_DaMiHoverboard", false);
            if (hoverboardDef != null && hoverboardDef.equippedStatOffsets != null)
            {
                var moveSpeedStat = hoverboardDef.equippedStatOffsets.FirstOrDefault(x => x.stat == StatDefOf.MoveSpeed);
                if (moveSpeedStat != null)
                {
                    moveSpeedStat.value = settings.hoverboardSpeedBonus;
                }
                else
                {
                    hoverboardDef.equippedStatOffsets.Add(new StatModifier { stat = StatDefOf.MoveSpeed, value = settings.hoverboardSpeedBonus });
                }
            }

            UpdateHediffSpeed("RimDigital_Hediff_Skateboard", settings.skateboardSpeedBonus);
            UpdateHediffSpeed("RimDigital_Hediff_Hoverboard", settings.hoverboardSpeedBonus);
        }

        private static void UpdateHediffSpeed(string hediffDefName, float speedValue)
        {
            var hediffDef = DefDatabase<HediffDef>.GetNamed(hediffDefName, false);
            if (hediffDef == null || hediffDef.stages == null || hediffDef.stages.Count == 0) return;

            var stage = hediffDef.stages[0];
            if (stage.statOffsets == null) return;

            var moveSpeedStat = stage.statOffsets.FirstOrDefault(x => x.stat == StatDefOf.MoveSpeed);
            if (moveSpeedStat != null)
            {
                moveSpeedStat.value = speedValue;
            }
        }
    }
}