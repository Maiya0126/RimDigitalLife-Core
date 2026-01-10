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
            // 你也可以把这个放到 Keyed 里，或者保持硬编码
            return "RimDigital Life：Core";
        }

        // 绘制设置菜单
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            // ==========================================
            // 第一部分：摸鱼机制设置 (保留逻辑，使用翻译)
            // ==========================================
            listing.Label("RDL_Settings_SlackerTitle".Translate());
            listing.GapLine();

            // 1. 频率
            listing.Label("RDL_Settings_Chance".Translate((settings.slackChance * 100).ToString("F1") + "%"));
            settings.slackChance = listing.Slider(settings.slackChance, 0f, 0.10f);

            // 2. 半径
            listing.Label("RDL_Settings_Radius".Translate(settings.detectionRadius.ToString("F0")));
            settings.detectionRadius = listing.Slider(settings.detectionRadius, 1f, 20f);
            listing.Gap();

            // 3. 收益 (实时修改 Def)
            listing.Label("RDL_Settings_MoodBonus".Translate(settings.moodBonusValue.ToString("F0")));
            float oldMood = settings.moodBonusValue;
            settings.moodBonusValue = listing.Slider(settings.moodBonusValue, 1f, 20f);

            // 实时应用
            if (oldMood != settings.moodBonusValue)
            {
                ApplyMoodSettings();
            }
            listing.Gap();

            // 4. 设备筛选
            listing.Label("RDL_Settings_AllowedDevices".Translate());
            listing.CheckboxLabeled("RDL_Settings_AllowComm".Translate(), ref settings.allowCommSlacking);
            listing.CheckboxLabeled("RDL_Settings_AllowGame".Translate(), ref settings.allowGameSlacking);
            listing.Gap();

            // 5. 惩罚机制
            listing.Label("RDL_Settings_PenaltyTitle".Translate());
            listing.CheckboxLabeled("RDL_Settings_ShowBubbles".Translate(), ref settings.showMoteText);

            // 带 Tooltip 的 Checkbox
            listing.CheckboxLabeled("RDL_Settings_StrictMode".Translate(), ref settings.strictMode, "RDL_Settings_StrictDesc".Translate());
            listing.CheckboxLabeled("RDL_Settings_Penalty".Translate(), ref settings.workSpeedPenalty, "RDL_Settings_PenaltyDesc".Translate());

            // ==========================================
            // 第二部分：智能助手设置 (新增)
            // ==========================================
            listing.GapLine();
            listing.Label("RDL_Settings_AssistantTitle".Translate());
            listing.Gap();

            string label = "";

            // 确保 Settings 类里有 public static float chatIntervalHours
            if (RimDigitalLifeSettings.chatIntervalHours <= 0.1f)
            {
                label = "RDL_Settings_ChatOff".Translate();
            }
            else
            {
                label = "RDL_Settings_ChatInterval".Translate(RimDigitalLifeSettings.chatIntervalHours.ToString("F1"));
            }

            listing.Label(label);

            // 滑块：0到48小时
            RimDigitalLifeSettings.chatIntervalHours = listing.Slider(RimDigitalLifeSettings.chatIntervalHours, 0f, 48f);

            // 动态提示文本
            if (RimDigitalLifeSettings.chatIntervalHours > 0.1f)
            {
                if (RimDigitalLifeSettings.chatIntervalHours < 4f)
                    listing.Label("RDL_Settings_TipHigh".Translate());
                else if (RimDigitalLifeSettings.chatIntervalHours > 24f)
                    listing.Label("RDL_Settings_TipLow".Translate());
            }
            else
            {
                listing.Label("RDL_Settings_TipNone".Translate());
            }

            // ==========================================
            // 结束绘制
            // ==========================================
            listing.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            ApplyMoodSettings();
        }

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