using Verse;
using RimWorld;

namespace RimDigitalLife
{
    public class RimDigitalLifeSettings : ModSettings
    {
        // === 默认值 ===
        public float slackChance = 0.02f;        // 摸鱼概率 (2%)
        public float detectionRadius = 8f;       // 抓包半径 (8格)
        public float moodBonusValue = 5f;        // 摸鱼心情加成 (+5)

        public bool showMoteText = true;         // 显示气泡
        public bool strictMode = false;          // 严厉模式 (打断工作)

        public bool allowCommSlacking = true;    // 允许玩手机/手表摸鱼
        public bool allowGameSlacking = true;    // 允许玩掌机摸鱼
        public bool workSpeedPenalty = false;    // 分心惩罚 (全局工作速度 -5%)

        // === 【新增】智能助手设置 ===
        // 设为 static 以便在组件中随时访问
        public static float chatIntervalHours = 12f;

        // === 保存与读取 ===
        public override void ExposeData()
        {
            Scribe_Values.Look(ref slackChance, "slackChance", 0.02f);
            Scribe_Values.Look(ref detectionRadius, "detectionRadius", 8f);
            Scribe_Values.Look(ref moodBonusValue, "moodBonusValue", 5f);

            Scribe_Values.Look(ref showMoteText, "showMoteText", true);
            Scribe_Values.Look(ref strictMode, "strictMode", false);

            Scribe_Values.Look(ref allowCommSlacking, "allowCommSlacking", true);
            Scribe_Values.Look(ref allowGameSlacking, "allowGameSlacking", true);
            Scribe_Values.Look(ref workSpeedPenalty, "workSpeedPenalty", false);

            // 【新增】保存智能助手频率
            Scribe_Values.Look(ref chatIntervalHours, "chatIntervalHours", 12f);

            base.ExposeData();
        }
    }
}