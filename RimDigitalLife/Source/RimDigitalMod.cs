using UnityEngine;
using Verse;
using RimWorld;

namespace RimDigitalLife
{
    public class RimDigitalMod : Mod
    {
        public static RimDigitalLifeSettings settings;

        public RimDigitalMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<RimDigitalLifeSettings>();
        }

        public override string SettingsCategory()
        {
            return "RimDigital Life：Core 边缘数码生活:核心版";
        }

        // 绘制设置菜单
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            // === 标题 ===
            listing.Label("<b>摸鱼机制设置 (Slacker Settings)</b>");
            listing.GapLine();

            // === 1. 频率与概率 ===
            listing.Label($"摸鱼触发概率: {settings.slackChance * 100:F1}% (默认: 2.0%)");
            settings.slackChance = listing.Slider(settings.slackChance, 0f, 0.10f);

            listing.Label($"抓包视线半径: {settings.detectionRadius:F0} 格");
            settings.detectionRadius = listing.Slider(settings.detectionRadius, 1f, 20f);
            listing.Gap();

            // === 2. 收益倍率 (实时修改 Def) ===
            listing.Label($"摸鱼心情加成: +{settings.moodBonusValue:F0}");
            float oldMood = settings.moodBonusValue;
            settings.moodBonusValue = listing.Slider(settings.moodBonusValue, 1f, 20f);

            // 如果玩家拖动了滑块，实时应用到游戏数据中
            if (oldMood != settings.moodBonusValue)
            {
                ApplyMoodSettings();
            }
            listing.Gap();

            // === 3. 设备筛选 ===
            listing.Label("<b>允许摸鱼的设备类型:</b>");
            listing.CheckboxLabeled("允许玩手机/手表 (通讯设备)", ref settings.allowCommSlacking);
            listing.CheckboxLabeled("允许玩掌机 (娱乐设备)", ref settings.allowGameSlacking);
            listing.Gap();

            // === 4. 惩罚机制 ===
            listing.Label("<b>惩罚与难度:</b>");
            listing.CheckboxLabeled("显示头顶气泡 (Show Bubbles)", ref settings.showMoteText);
            listing.CheckboxLabeled("严厉模式 (Strict Mode)", ref settings.strictMode, "被抓包时强制打断工作并击晕 2 秒。");
            listing.CheckboxLabeled("分心惩罚 (Distracted)", ref settings.workSpeedPenalty, "只要身上带着数码设备，全局工作速度降低 5%。");

            listing.End();
            base.DoSettingsWindowContents(inRect);
        }

        // 关闭设置窗口时也会保存
        public override void WriteSettings()
        {
            base.WriteSettings();
            ApplyMoodSettings();
        }

        // 将设置里的数值写入 XML 定义 (运行时修改)
        public static void ApplyMoodSettings()
        {
            var def = DefDatabase<ThoughtDef>.GetNamed("RimDigital_SlackingOff", false);
            if (def != null && def.stages.Count > 0)
            {
                def.stages[0].baseMoodEffect = settings.moodBonusValue;
            }
        }
    }
}