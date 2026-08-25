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
            return "RimDigital Life：Core 边缘数码生活:核心版";
        }

        private Vector2 scrollPosition = Vector2.zero;

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, 1200f);
            
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            listing.Label("<b>摸鱼机制设置 (Slacker Settings)</b>");
            listing.GapLine();

            listing.Label(string.Format("摸鱼触发概率: {0:F1}% (默认: 2.0%)", settings.slackChance * 100));
            settings.slackChance = listing.Slider(settings.slackChance, 0f, 0.10f);

            listing.Label(string.Format("抓包视线半径: {0:F0} 格", settings.detectionRadius));
            settings.detectionRadius = listing.Slider(settings.detectionRadius, 1f, 20f);
            listing.Gap();

            listing.Label(string.Format("摸鱼心情加成: +{0:F0}", settings.moodBonusValue));
            float oldMood = settings.moodBonusValue;
            settings.moodBonusValue = listing.Slider(settings.moodBonusValue, 1f, 20f);

            if (oldMood != settings.moodBonusValue)
            {
                ApplyMoodSettings();
            }
            listing.Gap();

            listing.Label("<b>允许摸鱼的设备类型:</b>");
            listing.CheckboxLabeled("允许玩手机/手表 (通讯设备)", ref settings.allowCommSlacking);
            listing.CheckboxLabeled("允许玩掌机 (娱乐设备)", ref settings.allowGameSlacking);
            listing.Gap();

            listing.Label("<b>惩罚与难度:</b>");
            listing.CheckboxLabeled("显示头顶气泡 (Show Bubbles)", ref settings.showMoteText);
            listing.CheckboxLabeled("严厉模式 (Strict Mode)", ref settings.strictMode, "被抓包时强制打断工作并击晕 2 秒。");
            listing.CheckboxLabeled("分心惩罚 (Distracted)", ref settings.workSpeedPenalty, "只要身上带着数码设备，全局工作速度降低 5%。");
            listing.GapLine();

            listing.Label("<b>移动设备设置 (Mobility Settings)</b>");
            listing.Gap();
            listing.Label(string.Format("滑板摔倒概率: {0:F0}% (默认: 10%)", settings.skateboardFallChance * 100));
            settings.skateboardFallChance = listing.Slider(settings.skateboardFallChance, 0f, 1f);
            listing.Label(string.Format("平衡车摔倒概率: {0:F0}% (默认: 5%)", settings.hoverboardFallChance * 100));
            settings.hoverboardFallChance = listing.Slider(settings.hoverboardFallChance, 0f, 1f);
            listing.Gap();

            listing.Label("<b>移速加成 (Speed Bonus)</b>");
            float oldSkateSpeed = settings.skateboardSpeedBonus;
            listing.Label(string.Format("滑板移速加成: +{0:F0} (默认: +16)", settings.skateboardSpeedBonus));
            settings.skateboardSpeedBonus = listing.Slider(settings.skateboardSpeedBonus, 0f, 50f);

            float oldHoverSpeed = settings.hoverboardSpeedBonus;
            listing.Label(string.Format("平衡车移速加成: +{0:F0} (默认: +8)", settings.hoverboardSpeedBonus));
            settings.hoverboardSpeedBonus = listing.Slider(settings.hoverboardSpeedBonus, 0f, 50f);

            if (oldSkateSpeed != settings.skateboardSpeedBonus || oldHoverSpeed != settings.hoverboardSpeedBonus)
            {
                ApplySpeedSettings();
            }

            listing.GapLine();
            listing.Label("<b>AI助手设置 (AI Assistant)</b>");
            listing.Gap();
            
            listing.CheckboxLabeled("启用 Rimi/Sirim 对话功能", ref settings.enableAssistantChat, 
                "关闭后，小人将不会自动或手动触发 Rimi/Sirim 对话。");
            
            if (settings.enableAssistantChat)
            {
                listing.Label(string.Format("自动对话间隔: {0:F1} 小时 (默认: 12)", settings.chatIntervalHours));
                settings.chatIntervalHours = listing.Slider(settings.chatIntervalHours, 1f, 24f);
            }

            listing.GapLine();
            listing.Label("<b>健康提醒设置 (Health Reminders)</b>");
            listing.Label("<i>提醒屏幕前的玩家注意休息和健康</i>");
            listing.Gap();
            
            listing.CheckboxLabeled("启用用眼休息提醒", ref settings.enableEyeReminder, 
                "定时提醒眼睛需要休息，看看远处。");
            if (settings.enableEyeReminder)
            {
                listing.Label(string.Format("  提醒间隔: {0:F0} 分钟 (默认: 45)", settings.eyeReminderMinutes));
                settings.eyeReminderMinutes = listing.Slider(settings.eyeReminderMinutes, 15f, 120f);
            }
            
            listing.CheckboxLabeled("启用喝水提醒", ref settings.enableWaterReminder, 
                "定时提醒喝一杯水。");
            if (settings.enableWaterReminder)
            {
                listing.Label(string.Format("  提醒间隔: {0:F0} 分钟 (默认: 90)", settings.waterReminderMinutes));
                settings.waterReminderMinutes = listing.Slider(settings.waterReminderMinutes, 30f, 180f);
            }
            
            listing.CheckboxLabeled("启用深夜休息提醒", ref settings.enableLateNightReminder, 
                "深夜时段提醒玩家注意休息。");
            if (settings.enableLateNightReminder)
            {
                listing.Label(string.Format("  深夜时段: {0}:00 - {1}:00", settings.lateNightStartHour, settings.lateNightEndHour));
                listing.Label("  调整开始时间:");
                settings.lateNightStartHour = (int)listing.Slider(settings.lateNightStartHour, 20f, 24f);
                listing.Label("  调整结束时间:");
                settings.lateNightEndHour = (int)listing.Slider(settings.lateNightEndHour, 0f, 8f);
                listing.Label(string.Format("  提醒间隔: {0:F0} 分钟 (默认: 60)", settings.lateNightReminderIntervalMinutes));
                settings.lateNightReminderIntervalMinutes = listing.Slider(settings.lateNightReminderIntervalMinutes, 30f, 180f);
            }
            
            listing.Gap();
            listing.Label("<b>提醒方式:</b>");
            listing.CheckboxLabeled("使用对话框提醒", ref settings.reminderUseDialog, 
                "使用弹窗对话框显示提醒（更醒目）。取消则使用右侧消息条。");
            listing.CheckboxLabeled("提醒时暂停游戏", ref settings.reminderPauseGame, 
                "弹出提醒时自动暂停游戏。");
            listing.Label($"<i>本次游戏已运行: {RealTimeReminder.GetPlayTimeFormatted()}</i>");

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