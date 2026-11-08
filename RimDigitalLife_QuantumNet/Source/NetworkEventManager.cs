using RimWorld;
using UnityEngine;
using Verse;

namespace RimDigitalLife_QuantumNet
{
    // ============================================================
    // 通讯运营商事件系统
    // 触发机制：事件结束后随机 2~5 天再触发下一个（触发时刻天然随机，不固定 0 点）
    // 事件持续 1~3 天，触发时消息 + 信封通知，事件期间产生实际效果
    // ============================================================
    public static class NetworkEventManager
    {
        // 事件 ID
        public const string StarlinkStorm = "starlink_storm";   // 星链风暴：网络中断
        public const string NetworkCongested = "network_congested"; // 网络拥堵：RimSeek 效率减半
        public const string CyberSale = "cyber_sale";           // 赛博百货大促：网购半价+心情
        public const string CartoRelease = "carto_release";     // 卡带新品发售：网购概率翻倍
        public const string MineScanCalib = "minescan_calib";   // 深矿雷达校准：日报高精度标注
        public const string PlanPromo = "plan_promo";           // 套餐半价促销

        // 当前活动事件（存档）
        public static string activeEventId = "";
        public static string activeEventLabel = "";
        public static int eventEndTick = 0;
        // 下次事件触发 tick（存档）
        public static int nextEventTick = -1;

        private static bool initialized = false;

        public static bool HasActiveEvent
        {
            get { return !string.IsNullOrEmpty(activeEventId) && Find.TickManager.TicksGame < eventEndTick; }
        }

        public static bool IsEventActive(string eventId)
        {
            return HasActiveEvent && activeEventId == eventId;
        }

        // 由 GameComponent 低频驱动（每 1000 tick）
        public static void Tick()
        {
            int tick = Find.TickManager.TicksGame;

            if (!initialized)
            {
                initialized = true;
                if (nextEventTick < 0)
                {
                    nextEventTick = tick + Rand.Range(300000, 600000); // 5~10 天后首个事件
                }
            }

            // 事件到期结束
            if (!string.IsNullOrEmpty(activeEventId) && tick >= eventEndTick)
            {
                Verse.Log.Message("[QuantumNet] " + "QN_Event_Ended".Translate(activeEventLabel));
                activeEventId = "";
                activeEventLabel = "";
            }

            // 到点触发新事件
            if (tick >= nextEventTick)
            {
                nextEventTick = tick + Rand.Range(300000, 600000); // 5~10 天后下一个
                if (!HasActiveEvent)
                {
                    TriggerRandomEvent(tick);
                }
            }
        }

        private static void TriggerRandomEventById(string eventId, int tick)
        {
            int durationTicks;
            string label;
            switch (eventId)
            {
                case StarlinkStorm:
                    durationTicks = Rand.Range(60000, 120000); // 1~2 天
                    label = "QN_Event_Storm_Label".Translate();
                    break;
                case NetworkCongested:
                    durationTicks = Rand.Range(60000, 120000); // 1~2 天
                    label = "QN_Event_Congest_Label".Translate();
                    break;
                case CyberSale:
                    durationTicks = Rand.Range(120000, 180000); // 2~3 天
                    label = "QN_Event_Sale_Label".Translate();
                    break;
                case CartoRelease:
                    durationTicks = Rand.Range(60000, 120000); // 1~2 天
                    label = "QN_Event_Carto_Label".Translate();
                    break;
                case MineScanCalib:
                    durationTicks = Rand.Range(60000, 120000); // 1~2 天
                    label = "QN_Event_Calib_Label".Translate();
                    break;
                default: // PlanPromo
                    durationTicks = Rand.Range(60000, 120000); // 1~2 天
                    label = "QN_Event_Promo_Label".Translate();
                    break;
            }
            string desc = EventDesc(eventId);

            activeEventId = eventId;
            activeEventLabel = label;
            eventEndTick = tick + durationTicks;

            Verse.Log.Message("[QuantumNet] " + "QN_Event_Triggered".Translate(label, (durationTicks / 60000f).ToString("F1")));
            Messages.Message("QN_Event_Msg".Translate(label), MessageTypeDefOf.NeutralEvent, false);
            Find.LetterStack.ReceiveLetter(
                "QN_Event_LetterTitle".Translate(label),
                desc,
                LetterDefOf.NeutralEvent,
                (LookTargets)null);
        }

        private static string EventDesc(string eventId)
        {
            switch (eventId)
            {
                case StarlinkStorm: return "QN_Event_Storm_Desc".Translate();
                case NetworkCongested: return "QN_Event_Congest_Desc".Translate();
                case CyberSale: return "QN_Event_Sale_Desc".Translate();
                case CartoRelease: return "QN_Event_Carto_Desc".Translate();
                case MineScanCalib: return "QN_Event_Calib_Desc".Translate();
                default: return "QN_Event_Promo_Desc".Translate();
            }
        }

        // 事件效果查询接口
        public static bool NetworkDown      { get { return IsEventActive(StarlinkStorm); } }
        public static bool RimSeekCongested { get { return IsEventActive(NetworkCongested); } }
        public static bool ShoppingHalfPrice { get { return IsEventActive(CyberSale); } }
        public static bool ShoppingBoost    { get { return IsEventActive(CartoRelease); } }
        public static bool MineScanHighPrecision { get { return IsEventActive(MineScanCalib); } }
        public static bool PlanHalfPrice    { get { return IsEventActive(PlanPromo); } }

        public static void ExposeData()
        {
            Scribe_Values.Look(ref activeEventId, "qnet_activeEventId", "");
            Scribe_Values.Look(ref activeEventLabel, "qnet_activeEventLabel", "");
            Scribe_Values.Look(ref eventEndTick, "qnet_eventEndTick", 0);
            Scribe_Values.Look(ref nextEventTick, "qnet_nextEventTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                initialized = false; // 重置，让 Tick 重新初始化
            }
        }

        // 事件状态文本（控制台/日报用）
        public static string GetStatusText()
        {
            if (!HasActiveEvent) return "QN_None".Translate();
            int daysLeft = Mathf.Max(0, (int)((eventEndTick - Find.TickManager.TicksGame) / 60000f) + 1);
            return "QN_Event_DaysLeft".Translate(activeEventLabel, daysLeft);
        }

        // ======== 开发者模式（DebugAction 调用） ========
        public static string ActiveEventId { get { return activeEventId; } }
        public static int NextEventTickDebug { get { return nextEventTick; } }

        // 新游戏重置（GameComponent 构造时调用；载入旧存档时会被 ExposeData 覆盖，不受影响）
        public static void ResetForNewGame()
        {
            activeEventId = "";
            activeEventLabel = "";
            eventEndTick = 0;
            nextEventTick = -1;
            initialized = false;
        }

        // 强制触发指定事件（无视随机间隔；若已有活动事件则先结束）
        public static void DebugForceEvent(string eventId)
        {
            int tick = Find.TickManager.TicksGame;
            if (HasActiveEvent)
            {
                activeEventId = "";
                activeEventLabel = "";
            }
            nextEventTick = tick + Rand.Range(300000, 600000); // 触发后照常排下一个随机事件
            TriggerRandomEventById(eventId, tick);
        }

        // 立即结束当前事件
        public static void DebugEndEvent()
        {
            activeEventId = "";
            activeEventLabel = "";
            eventEndTick = 0;
        }

        // 把 TriggerRandomEvent 拆为按 ID 触发（随机触发 = 随机选 ID）
        private static void TriggerRandomEvent(int tick)
        {
            string[] pool = new string[]
            {
                StarlinkStorm, NetworkCongested, CyberSale, CartoRelease, MineScanCalib, PlanPromo
            };
            TriggerRandomEventById(pool[Rand.Range(0, pool.Length)], tick);
        }
    }
}
