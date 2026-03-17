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
        }

        public override string SettingsCategory()
        {
            return "RimDigital Life：Core 边缘数码生活:核心版";
        }

        private Vector2 scrollPosition = Vector2.zero;

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, 900f);
            
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            listing.Label("<b>摸鱼机制设置 (Slacker Settings)</b>");
            listing.GapLine();

            listing.Label($"摸鱼触发概率: {settings.slackChance * 100:F1}% (默认: 2.0%)");
            settings.slackChance = listing.Slider(settings.slackChance, 0f, 0.10f);

            listing.Label($"抓包视线半径: {settings.detectionRadius:F0} 格");
            settings.detectionRadius = listing.Slider(settings.detectionRadius, 1f, 20f);
            listing.Gap();

            listing.Label($"摸鱼心情加成: +{settings.moodBonusValue:F0}");
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
            listing.Label($"滑板摔倒概率: {settings.skateboardFallChance * 100:F0}% (默认: 10%)");
            settings.skateboardFallChance = listing.Slider(settings.skateboardFallChance, 0f, 1f);
            listing.Label($"平衡车摔倒概率: {settings.hoverboardFallChance * 100:F0}% (默认: 5%)");
            settings.hoverboardFallChance = listing.Slider(settings.hoverboardFallChance, 0f, 1f);
            listing.Gap();

            listing.Label("<b>移速加成 (Speed Bonus)</b>");
            float oldSkateSpeed = settings.skateboardSpeedBonus;
            listing.Label($"滑板移速加成: +{settings.skateboardSpeedBonus:F0} (默认: +16)");
            settings.skateboardSpeedBonus = listing.Slider(settings.skateboardSpeedBonus, 0f, 50f);

            float oldHoverSpeed = settings.hoverboardSpeedBonus;
            listing.Label($"平衡车移速加成: +{settings.hoverboardSpeedBonus:F0} (默认: +8)");
            settings.hoverboardSpeedBonus = listing.Slider(settings.hoverboardSpeedBonus, 0f, 50f);

            if (oldSkateSpeed != settings.skateboardSpeedBonus || oldHoverSpeed != settings.hoverboardSpeedBonus)
            {
                ApplySpeedSettings();
            }

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
        }
    }
}