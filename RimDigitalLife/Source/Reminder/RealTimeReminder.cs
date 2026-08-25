using System;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimDigitalLife
{
    public static class RealTimeReminder
    {
        private static float accumulatedSeconds = 0f;
        private static DateTime lastCheckTime;
        private static bool initialized = false;
        
        private static float lastEyeReminderSeconds = 0f;
        private static float lastWaterReminderSeconds = 0f;
        private static float lastLateNightReminderSeconds = -1f;
        
        private const float MinCheckInterval = 4f;
        private static float timeSinceLastCheck = 0f;

        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            
            accumulatedSeconds = 0f;
            lastCheckTime = DateTime.Now;
            lastEyeReminderSeconds = 0f;
            lastWaterReminderSeconds = 0f;
            lastLateNightReminderSeconds = -1f;
            timeSinceLastCheck = 0f;
            
            Log.Message("[RimDigitalLife] RealTimeReminder initialized");
        }

        public static void Update()
        {
            if (!initialized) Initialize();
            if (RimDigitalMod.settings == null) return;
            
            if (!RimDigitalMod.settings.enableEyeReminder && 
                !RimDigitalMod.settings.enableWaterReminder &&
                !RimDigitalMod.settings.enableLateNightReminder)
            {
                return;
            }

            timeSinceLastCheck += Time.deltaTime;
            if (timeSinceLastCheck < MinCheckInterval) return;
            
            timeSinceLastCheck = 0f;
            
            DateTime now = DateTime.Now;
            float elapsedSeconds = (float)(now - lastCheckTime).TotalSeconds;
            lastCheckTime = now;
            
            if (elapsedSeconds > 300f) elapsedSeconds = 300f;
            
            accumulatedSeconds += elapsedSeconds;
            
            CheckEyeReminder();
            CheckWaterReminder();
            CheckLateNightReminder();
        }

        private static void CheckEyeReminder()
        {
            if (!RimDigitalMod.settings.enableEyeReminder) return;
            
            float intervalSeconds = RimDigitalMod.settings.eyeReminderMinutes * 60f;
            if (intervalSeconds <= 0f) return;
            
            if (accumulatedSeconds - lastEyeReminderSeconds >= intervalSeconds)
            {
                lastEyeReminderSeconds = accumulatedSeconds;
                ShowReminder(
                    "RDL_Reminder_Eye_Title".Translate(),
                    "RDL_Reminder_Eye_Message".Translate(),
                    RimDigitalMod.settings.reminderPauseGame
                );
            }
        }

        private static void CheckWaterReminder()
        {
            if (!RimDigitalMod.settings.enableWaterReminder) return;
            
            float intervalSeconds = RimDigitalMod.settings.waterReminderMinutes * 60f;
            if (intervalSeconds <= 0f) return;
            
            if (accumulatedSeconds - lastWaterReminderSeconds >= intervalSeconds)
            {
                lastWaterReminderSeconds = accumulatedSeconds;
                ShowReminder(
                    "RDL_Reminder_Water_Title".Translate(),
                    "RDL_Reminder_Water_Message".Translate(),
                    RimDigitalMod.settings.reminderPauseGame
                );
            }
        }

        private static void CheckLateNightReminder()
        {
            if (!RimDigitalMod.settings.enableLateNightReminder) return;
            
            DateTime now = DateTime.Now;
            int startHour = RimDigitalMod.settings.lateNightStartHour;
            int endHour = RimDigitalMod.settings.lateNightEndHour;
            int currentHour = now.Hour;
            
            bool isLateNight = false;
            if (startHour > endHour)
            {
                isLateNight = currentHour >= startHour || currentHour < endHour;
            }
            else
            {
                isLateNight = currentHour >= startHour && currentHour < endHour;
            }
            
            if (!isLateNight)
            {
                lastLateNightReminderSeconds = -1f;
                return;
            }
            
            if (lastLateNightReminderSeconds < 0f)
            {
                lastLateNightReminderSeconds = accumulatedSeconds;
                string timeStr = now.ToString("HH:mm");
                ShowReminder(
                    "RDL_Reminder_LateNight_Title".Translate(),
                    "RDL_Reminder_LateNight_Message".Translate(timeStr),
                    RimDigitalMod.settings.reminderPauseGame
                );
                return;
            }
            
            float intervalSeconds = RimDigitalMod.settings.lateNightReminderIntervalMinutes * 60f;
            if (intervalSeconds <= 0f) return;
            
            if (accumulatedSeconds - lastLateNightReminderSeconds >= intervalSeconds)
            {
                lastLateNightReminderSeconds = accumulatedSeconds;
                string timeStr = now.ToString("HH:mm");
                ShowReminder(
                    "RDL_Reminder_LateNight_Title".Translate(),
                    "RDL_Reminder_LateNight_Message".Translate(timeStr),
                    RimDigitalMod.settings.reminderPauseGame
                );
            }
        }

        private static void ShowReminder(string title, string message, bool pauseGame)
        {
            if (RimDigitalMod.settings.reminderUseDialog)
            {
                Find.WindowStack.Add(new ReminderDialog(title, message, pauseGame));
            }
            else
            {
                Messages.Message($"[{title}] {message}", MessageTypeDefOf.SilentInput);
            }
        }

        public static void ResetTimers()
        {
            lastEyeReminderSeconds = accumulatedSeconds;
            lastWaterReminderSeconds = accumulatedSeconds;
            lastLateNightReminderSeconds = -1f;
        }

        public static string GetPlayTimeFormatted()
        {
            int totalMinutes = (int)(accumulatedSeconds / 60f);
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;
            return $"{hours}小时{minutes}分钟";
        }
    }
}
