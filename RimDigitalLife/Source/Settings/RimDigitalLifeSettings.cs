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

        // === 移动设备摔倒概率 ===
        public float skateboardFallChance = 0.10f;    // 滑板摔倒概率 (10%)
        public float hoverboardFallChance = 0.05f;    // 平衡车摔倒概率 (5%)

        // === 移动设备移速加成 ===
        public float skateboardSpeedBonus = 16f;      // 滑板移速加成 (+16)
        public float hoverboardSpeedBonus = 8f;       // 平衡车移速加成 (+8)

        // === AI助手设置 ===
        public bool enableAssistantChat = true;        // 启用Rimi/Sirim对话功能
        public float chatIntervalHours = 12f;          // AI助手自动触发间隔 (小时)

        // === 现实时间健康提醒 ===
        public bool enableEyeReminder = true;          // 启用用眼休息提醒
        public bool enableWaterReminder = true;        // 启用喝水提醒
        public bool enableLateNightReminder = true;    // 启用深夜休息提醒
        
        public float eyeReminderMinutes = 45f;         // 用眼提醒间隔 (分钟)
        public float waterReminderMinutes = 90f;       // 喝水提醒间隔 (分钟)
        public int lateNightStartHour = 23;            // 深夜开始时间 (23点)
        public int lateNightEndHour = 6;               // 深夜结束时间 (6点)
        public float lateNightReminderIntervalMinutes = 60f; // 深夜提醒间隔 (分钟)
        
        public bool reminderUseDialog = true;          // 使用对话框提醒 (否则用消息条)
        public bool reminderPauseGame = false;         // 提醒时暂停游戏

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

            Scribe_Values.Look(ref skateboardFallChance, "skateboardFallChance", 0.10f);
            Scribe_Values.Look(ref hoverboardFallChance, "hoverboardFallChance", 0.05f);

            Scribe_Values.Look(ref skateboardSpeedBonus, "skateboardSpeedBonus", 16f);
            Scribe_Values.Look(ref hoverboardSpeedBonus, "hoverboardSpeedBonus", 8f);

            Scribe_Values.Look(ref enableAssistantChat, "enableAssistantChat", true);
            Scribe_Values.Look(ref chatIntervalHours, "chatIntervalHours", 12f);

            Scribe_Values.Look(ref enableEyeReminder, "enableEyeReminder", true);
            Scribe_Values.Look(ref enableWaterReminder, "enableWaterReminder", true);
            Scribe_Values.Look(ref enableLateNightReminder, "enableLateNightReminder", true);
            Scribe_Values.Look(ref eyeReminderMinutes, "eyeReminderMinutes", 45f);
            Scribe_Values.Look(ref waterReminderMinutes, "waterReminderMinutes", 90f);
            Scribe_Values.Look(ref lateNightStartHour, "lateNightStartHour", 23);
            Scribe_Values.Look(ref lateNightEndHour, "lateNightEndHour", 6);
            Scribe_Values.Look(ref lateNightReminderIntervalMinutes, "lateNightReminderIntervalMinutes", 60f);
            Scribe_Values.Look(ref reminderUseDialog, "reminderUseDialog", true);
            Scribe_Values.Look(ref reminderPauseGame, "reminderPauseGame", false);

            base.ExposeData();
        }
    }
}