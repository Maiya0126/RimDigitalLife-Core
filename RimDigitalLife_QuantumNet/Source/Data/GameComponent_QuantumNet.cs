using System.Collections.Generic;
using RimDigitalLife;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimDigitalLife_QuantumNet
{
    // 流量/算力套餐
    public enum QuantumPlan
    {
        Local,      // 本地流量包（免费，无加成）
        HighSpeed,  // 高速畅享包
        Infinite    // 量子无限包
    }

    // 小人套餐订阅数据（存档安全）
    public class PawnPlanData : IExposable
    {
        public string pawnId = "";
        public QuantumPlan plan = QuantumPlan.Local;
        public int startDay = 0;
        public int startYear = 0;
        public bool autoRenew = true;

        public void ExposeData()
        {
            Scribe_Values.Look(ref pawnId, "pawnId", "");
            string pStr = plan.ToString();
            Scribe_Values.Look(ref pStr, "plan", "Local");
            plan = (QuantumPlan)System.Enum.Parse(typeof(QuantumPlan), pStr, true);
            Scribe_Values.Look(ref startDay, "startDay", 0);
            Scribe_Values.Look(ref startYear, "startYear", 0);
            Scribe_Values.Look(ref autoRenew, "autoRenew", true);
        }
    }

    public static class QuantumPlanExtensions
    {
        public static string GetLabel(this QuantumPlan p)
        {
            switch (p)
            {
                case QuantumPlan.HighSpeed: return "QN_Plan_HighSpeed".Translate();
                case QuantumPlan.Infinite: return "QN_Plan_Infinite".Translate();
                default: return "QN_Plan_Local".Translate();
            }
        }

        // 每周期价格（0 = 免费）
        public static int GetPrice(this QuantumPlan p)
        {
            var S = QuantumNetMod.settings;
            switch (p)
            {
                case QuantumPlan.HighSpeed: return S?.highSpeedPrice ?? 60;
                case QuantumPlan.Infinite: return S?.quantumInfinitePrice ?? 180;
                default: return 0;
            }
        }
    }

    public class GameComponent_QuantumNet : GameComponent
    {
        public static GameComponent_QuantumNet Instance;

        private Dictionary<string, PawnPlanData> pawnPlans = new Dictionary<string, PawnPlanData>();
        private int lastPlanSettleTick = 0;
        private int lastNetworkAITick = 0;
        private int lastReportDay = -1; // 上次发日报的绝对天数（DayOfYear + Year*DaysPerYear）

        public GameComponent_QuantumNet(Game game)
        {
            Instance = this;
            NetworkEventManager.ResetForNewGame(); // 新游戏重置 static 事件状态（载入存档时会被 ExposeData 覆盖）
        }

        // ======== 量子网络覆盖（星链系） ========
        // 简化方案：建造并通电原版通讯台 = 当前地图 100% 网络覆盖
        // 运营商事件：星链风暴期间网络临时中断
        public bool HasNetworkCoverage(Map map)
        {
            var S = QuantumNetMod.settings;
            if (S == null || !S.enableQuantumNet) return false;
            if (NetworkEventManager.NetworkDown) return false; // 星链风暴：网络中断
            if (!S.requireCommsConsole) return true; // 不需通讯台则默认覆盖
            if (map == null) return false;
            return CommsConsoleUtility.PlayerHasPoweredCommsConsole(map);
        }

        public bool HasNetworkCoverage()
        {
            return HasNetworkCoverage(Find.CurrentMap ?? Find.AnyPlayerHomeMap);
        }

        // ======== 套餐订阅存取 ========
        public QuantumPlan GetPawnPlan(Pawn pawn)
        {
            if (pawn == null) return QuantumPlan.Local;
            string key = pawn.ThingID;
            PawnPlanData data;
            if (pawnPlans.TryGetValue(key, out data)) return data.plan;
            return QuantumPlan.Local;
        }

        public PawnPlanData GetPlanData(Pawn pawn)
        {
            if (pawn == null) return null;
            string key = pawn.ThingID;
            PawnPlanData data;
            if (!pawnPlans.TryGetValue(key, out data))
            {
                data = new PawnPlanData { pawnId = key, plan = QuantumPlan.Local };
                pawnPlans[key] = data;
            }
            return data;
        }

        public bool HasPaidPlan(Pawn pawn)
        {
            QuantumPlan p = GetPawnPlan(pawn);
            return p == QuantumPlan.HighSpeed || p == QuantumPlan.Infinite;
        }

        public bool HasInfinitePlan(Pawn pawn)
        {
            return GetPawnPlan(pawn) == QuantumPlan.Infinite;
        }

        // 手动切换套餐（控制台用）：尝试扣费，失败返回 false
        public bool TrySetPlan(Pawn pawn, QuantumPlan plan)
        {
            if (pawn == null) return false;
            if (plan == QuantumPlan.Local)
            {
                PawnPlanData data = GetPlanData(pawn);
                data.plan = QuantumPlan.Local;
                return true;
            }

            if (!HasNetworkCoverage())
            {
                Messages.Message("[QuantumNet] " + "QN_Msg_NoCoverage".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (TryChargePlan(pawn, plan))
            {
                PawnPlanData data = GetPlanData(pawn);
                data.plan = plan;
                data.startDay = GenLocalDate.DayOfYear(Find.CurrentMap ?? Find.AnyPlayerHomeMap);
                data.startYear = GenLocalDate.Year(Find.CurrentMap ?? Find.AnyPlayerHomeMap);
                Messages.Message("[QuantumNet] " + "QN_Msg_Subscribed".Translate(pawn.LabelShort, plan.GetLabel(), plan.GetPrice(), QuantumNetMod.settings?.planPeriodDays ?? 15), MessageTypeDefOf.NeutralEvent, false);
                return true;
            }
            return false;
        }

        // ======== 低频结算 ========
        public override void GameComponentTick()
        {
            base.GameComponentTick();

            int tick = Find.TickManager.TicksGame;

            // Phase 3：RimSeek 智算 Buff（每 500 tick 低频更新，避免每帧开销）
            if (tick % 500 == 0)
            {
                RimSeekBuffManager.Tick();
            }

            // Phase 2：套餐结算（每 1000 tick 检查，每日 00:00 触发）
            if (tick % 1000 == 0)
            {
                CheckAndProcessPlans();
            }

            // Phase 4：直播打赏（每 3000 tick 约 1 游戏小时一次机会）
            if (tick % 3000 == 0)
            {
                StreamTipManager.Tick();
            }

            // 私有网购（每 3000 tick 约 1 游戏小时一次机会）
            if (tick % 3000 == 0)
            {
                PrivateShoppingManager.Tick();
            }

            // 健康提醒（每天 8 点/20 点各一次机会）
            if (tick % 1000 == 0)
            {
                HealthReminderManager.Tick();
            }

            // Phase 4：跨地图通话（每 2 天一次机会）
            if (tick % 120000 == 0)
            {
                CrossMapCallManager.Tick();
            }

            // Phase 5：AI 网络数据每日请求 + 落地（每天 12:00 触发一次）
            if (tick % 1000 == 0)
            {
                NetworkEventManager.Tick();   // 运营商事件（随机时间触发）
                CheckAndProcessNetworkAI();
            }
        }

        private void CheckAndProcessNetworkAI()
        {
            // 每天 12:00 触发一次（与 RimPay 0 点结算错峰，早晨资讯体验更好）
            int currentHour = GenLocalDate.HourOfDay(Find.CurrentMap ?? Find.AnyPlayerHomeMap);
            int currentTick = Find.TickManager.TicksGame;
            if (currentHour == 12 && currentTick - lastNetworkAITick > 30000)
            {
                ProcessNetworkAI();
                lastNetworkAITick = currentTick;
            }
        }

        private void ProcessNetworkAI()
        {
            var S = QuantumNetMod.settings;
            if (S == null || !S.enableQuantumNet) return;

            // 日报频次档位：0=关闭 1=每2天(默认) 2=每天
            int freq = S.reportFrequency;
            if (freq < 0 || freq > 2) freq = 1;
            if (freq == 0) return; // 关闭：不请求 AI、不发日报（省 token）

            // 距上次日报的天数是否达到档位间隔
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            int today = GenLocalDate.DayOfYear(map) + GenLocalDate.Year(map) * GenDate.DaysPerYear;
            int interval = freq == 1 ? 2 : 1;
            if (lastReportDay >= 0 && today - lastReportDay < interval) return;

            // 有网络覆盖且开启 AI 才请求；失败自动回退默认值（QuantumNetAIProvider 内部处理）
            if (S.enableNetworkAI)
            {
                if (HasNetworkCoverage())
                {
                    QuantumNetAIProvider.RequestDailyNetworkIfNeeded();
                }
            }

            // 量子网络日报（用最近一次 AI 成功数据；仅当至少一名无限包用户）
            if (QuantumNetAIProvider.hasAIResult)
            {
                if (HasAnyInfinitePlanUser())
                {
                    SendDailyNetworkLetter();
                    lastReportDay = today;
                }
            }
        }

        private bool HasAnyInfinitePlanUser()
        {
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return false;
            foreach (Pawn pawn in map.mapPawns.FreeColonists)
            {
                if (!pawn.Dead && !pawn.Destroyed && HasInfinitePlan(pawn))
                {
                    return true;
                }
            }
            return false;
        }

        private void SendDailyNetworkLetter()
        {
            try
            {
                string statusText = QuantumNetAIProvider.networkStatus == "congested"
                    ? "QN_Status_Congested".Translate()
                    : QuantumNetAIProvider.networkStatus == "outage" ? "QN_Status_Outage".Translate() : "QN_Status_Normal".Translate();

                string body = "QN_Report_Header".Translate()
                    + "QN_Report_Status".Translate(statusText) + "\n"
                    + "QN_Report_Event".Translate(NetworkEventManager.GetStatusText()) + "\n\n";

                if (!string.IsNullOrEmpty(QuantumNetAIProvider.cloudWeatherHint))
                {
                    body += "QN_Report_Weather".Translate(QuantumNetAIProvider.cloudWeatherHint) + "\n";
                }
                if (!string.IsNullOrEmpty(QuantumNetAIProvider.deepMineHint))
                {
                    string tag = NetworkEventManager.MineScanHighPrecision ? (string)"QN_Report_CalibTag".Translate() : "";
                    body += "QN_Report_Mine".Translate(tag + QuantumNetAIProvider.deepMineHint) + "\n";
                }
                if (!string.IsNullOrEmpty(QuantumNetAIProvider.rimseekTip))
                {
                    body += "QN_Report_Tip".Translate(QuantumNetAIProvider.rimseekTip) + "\n";
                }
                if (!string.IsNullOrEmpty(QuantumNetAIProvider.newsText))
                {
                    body += "\n" + "QN_Report_News".Translate(QuantumNetAIProvider.newsText);
                }

                // RimPay 联动：追加量子网络运营商股票行情
                string quantumStock = QuantumNetRimPayBridge.GetQuantumSectorMovement();
                if (!string.IsNullOrEmpty(quantumStock))
                {
                    body += "\n" + "QN_Report_Stock".Translate(quantumStock);
                }

                Find.LetterStack.ReceiveLetter(
                    "QN_Report_LetterTitle".Translate(),
                    body,
                    LetterDefOf.NeutralEvent,
                    (LookTargets)null);

                // 日报发出后，让一名无限包用户通过 RimTalk 聊起今日资讯
                BroadcastNetworkNewsToRimTalk();
            }
            catch (System.Exception ex)
            {
                Verse.Log.Warning("[QuantumNet] " + "QN_Report_Fail".Translate(ex.Message));
            }
        }

        // ---- AI 网络新闻播报给 RimTalk（随机一名无限包用户聊起今日资讯） ----
        private void BroadcastNetworkNewsToRimTalk()
        {
            var S = QuantumNetMod.settings;
            if (S == null || !S.enableRimTalkNewsBroadcast) return;
            if (!QuantumNetAIProvider.hasAIResult) return;
            if (!RimTalkBridge.IsRimTalkAvailable) return;

            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;

            // 只有无限包用户能收到量子网络日报，由他们聊起话题
            var readers = new System.Collections.Generic.List<Pawn>();
            foreach (Pawn pawn in map.mapPawns.FreeColonists)
            {
                if (!pawn.Dead && !pawn.Destroyed && HasInfinitePlan(pawn))
                {
                    readers.Add(pawn);
                }
            }
            if (readers.Count == 0) return;

            // 从今日 AI 资讯里随机挑一个话题
            var topics = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(QuantumNetAIProvider.newsText)) topics.Add("QN_Topic_News".Translate(QuantumNetAIProvider.newsText));
            if (!string.IsNullOrEmpty(QuantumNetAIProvider.cloudWeatherHint)) topics.Add("QN_Topic_Weather".Translate(QuantumNetAIProvider.cloudWeatherHint));
            if (!string.IsNullOrEmpty(QuantumNetAIProvider.deepMineHint)) topics.Add("QN_Topic_Mine".Translate(QuantumNetAIProvider.deepMineHint));
            if (!string.IsNullOrEmpty(QuantumNetAIProvider.rimseekTip)) topics.Add("QN_Topic_Tip".Translate(QuantumNetAIProvider.rimseekTip));
            string stockBrief = QuantumNetRimPayBridge.GetMarketBrief();
            if (!string.IsNullOrEmpty(stockBrief)) topics.Add("QN_Topic_Stock".Translate(stockBrief));
            if (topics.Count == 0) return;

            Pawn speaker = readers[Rand.Range(0, readers.Count)];
            string topic = topics[Rand.Range(0, topics.Count)];
            string msg = "QN_Topic_Chat".Translate(topic);
            RimTalkBridge.TriggerDialogue(speaker, speaker, "QN_Report_LetterTitle".Translate(), msg, false, AssistantPersona.Rimi);
            Verse.Log.Message("[QuantumNet] " + "QN_Topic_Done".Translate(speaker.LabelShort));
        }

        private void CheckAndProcessPlans()
        {
            // 每天 00:00 结算一次
            int currentHour = GenLocalDate.HourOfDay(Find.CurrentMap ?? Find.AnyPlayerHomeMap);
            int currentTick = Find.TickManager.TicksGame;
            if (currentHour == 0 && currentTick - lastPlanSettleTick > 30000)
            {
                ProcessPlanSettlement();
                lastPlanSettleTick = currentTick;
            }
        }

        private void ProcessPlanSettlement()
        {
            var S = QuantumNetMod.settings;
            if (S == null || !S.enableQuantumNet) return;

            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;

            // 无网络覆盖：不结算、不选购（离线状态）
            if (!HasNetworkCoverage(map)) return;

            int currentDay = GenLocalDate.DayOfYear(map);
            int currentYear = GenLocalDate.Year(map);

            foreach (Pawn pawn in map.mapPawns.FreeColonists)
            {
                if (pawn.Dead || pawn.Destroyed) continue;
                EnsurePlan(pawn, currentDay, currentYear);
            }
        }

        // 确保小人套餐有效：无付费套餐→自动选购；到期→续费或降级
        private void EnsurePlan(Pawn pawn, int day, int year)
        {
            PawnPlanData data = GetPlanData(pawn);

            if (data.plan == QuantumPlan.Local)
            {
                // 自动选购
                if (QuantumNetMod.settings?.enableAutoPlan ?? true)
                {
                    QuantumPlan chosen = AutoChoosePlan(pawn);
                    if (chosen != QuantumPlan.Local)
                    {
                        if (TryChargePlan(pawn, chosen))
                        {
                            data.plan = chosen;
                            data.startDay = day;
                            data.startYear = year;
                            Verse.Log.Message("[QuantumNet] " + "QN_Log_AutoPlan".Translate(pawn.LabelShort, chosen.GetLabel(), chosen.GetPrice()));
                        }
                    }
                }
                return;
            }

            // 已订阅付费套餐 → 到期检查
            int period = QuantumNetMod.settings?.planPeriodDays ?? 15;
            if (PlanExpired(data, day, year, period))
            {
                if (data.autoRenew)
                {
                    if (TryChargePlan(pawn, data.plan))
                    {
                        data.startDay = day;
                        data.startYear = year;
                        Verse.Log.Message("[QuantumNet] " + "QN_Log_Renewed".Translate(pawn.LabelShort, data.plan.GetLabel()));
                    }
                    else
                    {
                        // 续费失败（钱包不足/未联网）→ 降级本地包
                        QuantumPlan old = data.plan;
                        data.plan = QuantumPlan.Local;
                        data.startDay = day;
                        data.startYear = year;
                        Messages.Message("[QuantumNet] " + "QN_Msg_RenewFail".Translate(pawn.LabelShort, old.GetLabel()), MessageTypeDefOf.NeutralEvent, false);
                    }
                }
                else
                {
                    QuantumPlan old = data.plan;
                    data.plan = QuantumPlan.Local;
                    data.startDay = day;
                    data.startYear = year;
                    Verse.Log.Message("[QuantumNet] " + "QN_Log_Expired".Translate(pawn.LabelShort, old.GetLabel()));
                }
            }
        }

        private bool PlanExpired(PawnPlanData data, int day, int year, int period)
        {
            int elapsed = (year - data.startYear) * GenDate.DaysPerYear + (day - data.startDay);
            return elapsed >= period;
        }

        // ======== 自动选购规则 ========
        private QuantumPlan AutoChoosePlan(Pawn pawn)
        {
            var S = QuantumNetMod.settings;
            if (S == null || !S.enableAutoPlan) return QuantumPlan.Local;

            // 报销政策：玩家买单 → 直接量子无限包（不要求余额）
            if (S.planSubsidy) return QuantumPlan.Infinite;

            // 网瘾少年（性格占位，后续接 Core 性格系统）优先花钱
            bool internetAddict = HasInternetAddictTrait(pawn);

            int balance = QuantumNetRimPayBridge.GetBalance(pawn);
            int infinitePrice = S.quantumInfinitePrice;
            int highSpeedPrice = S.highSpeedPrice;

            if (internetAddict)
            {
                if (balance >= infinitePrice) return QuantumPlan.Infinite;
                if (balance >= highSpeedPrice) return QuantumPlan.HighSpeed;
                return QuantumPlan.Local;
            }

            if (balance >= infinitePrice) return QuantumPlan.Infinite;
            if (balance >= highSpeedPrice) return QuantumPlan.HighSpeed;
            return QuantumPlan.Local;
        }

        // 网瘾少年判定：生物年龄 ≤ 20 的少年/青年，或科技狂热者（Transhumanist），优先花钱上网
        private bool HasInternetAddictTrait(Pawn pawn)
        {
            if (pawn == null) return false;
            if (pawn.ageTracker != null && pawn.ageTracker.AgeBiologicalYears <= 20) return true;
            TraitDef transhumanist = DefDatabase<TraitDef>.GetNamedSilentFail("Transhumanist");
            if (transhumanist != null && pawn.story?.traits?.HasTrait(transhumanist) == true) return true;
            return false;
        }

        // ======== 扣费（RimPay 钱包可选） ========
        // 月租从 RimPay 钱包自动扣（外部流出，不进国库）；无 RimPay 时降级为不扣费（本地包）
        private bool TryChargePlan(Pawn pawn, QuantumPlan plan)
        {
            int price = plan.GetPrice();
            if (price <= 0) return true; // 本地包免费

            // 套餐半价促销事件
            if (NetworkEventManager.PlanHalfPrice)
            {
                price = Mathf.Max(1, Mathf.CeilToInt(price / 2f));
            }

            var S = QuantumNetMod.settings;
            bool walletPay = S?.planWalletPay ?? true;

            if (walletPay && QuantumNetRimPayBridge.IsRimPayAvailable)
            {
                int balance = QuantumNetRimPayBridge.GetBalance(pawn);
                if (balance >= price)
                {
                    QuantumNetRimPayBridge.TryModifyBalance(pawn, -price, "QN_Plan_Reason".Translate());
                    return true;
                }
                return false; // 余额不足
            }

            // 无 RimPay / 未开启钱包扣费：付费套餐不可用（降级本地包）
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pawnPlans, "pawnPlans", LookMode.Value, LookMode.Deep);
            Scribe_Values.Look(ref lastPlanSettleTick, "lastPlanSettleTick", 0);
            Scribe_Values.Look(ref lastNetworkAITick, "lastNetworkAITick", 0);
            Scribe_Values.Look(ref lastReportDay, "lastReportDay", -1);
            NetworkEventManager.ExposeData(); // 运营商事件状态存档

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (pawnPlans == null) pawnPlans = new Dictionary<string, PawnPlanData>();
            }
        }
    }
}
