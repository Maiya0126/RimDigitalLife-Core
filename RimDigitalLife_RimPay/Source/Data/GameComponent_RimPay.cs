using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using RimDigitalLife;

namespace RimDigitalLife_RimPay
{
    // 单条交易流水记录
    public class TransactionRecord : IExposable
    {
        public int day = 0;
        public int year = 0;
        public int amount = 0;
        public string reason = "";
        public bool isIncome = false;

        public TransactionRecord()
        {
        }

        public TransactionRecord(int day, int year, int amount, string reason, bool isIncome)
        {
            this.day = day;
            this.year = year;
            this.amount = amount;
            this.reason = reason;
            this.isIncome = isIncome;
        }

        public string GetSummary()
        {
            return $"第{year}.{day}日  {(isIncome ? "+" : "")}{amount} @银  {reason}";
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref day, "day", 0);
            Scribe_Values.Look(ref year, "year", 0);
            Scribe_Values.Look(ref amount, "amount", 0);
            Scribe_Values.Look(ref reason, "reason", "");
            Scribe_Values.Look(ref isIncome, "isIncome", false);
        }
    }

    // 贷款记录
    public class LoanRecord : IExposable
    {
        public string factionName = "";
        public int factionKey = -1;
        public int amount = 0;
        public double rate = 0.08;
        public int dueDay = 0;
        public int dueYear = 0;
        public bool isFromPlayer = true; // true=放贷, false=借款

        public LoanRecord() { }

        public LoanRecord(string factionName, int factionKey, int amount, double rate, int dueDay, int dueYear, bool isFromPlayer)
        {
            this.factionName = factionName;
            this.factionKey = factionKey;
            this.amount = amount;
            this.rate = rate;
            this.dueDay = dueDay;
            this.dueYear = dueYear;
            this.isFromPlayer = isFromPlayer;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref factionName, "factionName", "");
            Scribe_Values.Look(ref factionKey, "factionKey", -1);
            Scribe_Values.Look(ref amount, "amount", 0);
            Scribe_Values.Look(ref rate, "rate", 0.08);
            Scribe_Values.Look(ref dueDay, "dueDay", 0);
            Scribe_Values.Look(ref dueYear, "dueYear", 0);
            Scribe_Values.Look(ref isFromPlayer, "isFromPlayer", true);
        }
    }

    // 股票单日K线数据 (真实股票K线看板数据)
    public class StockBar : IExposable
    {
        public float open = 0f;
        public float high = 0f;
        public float low = 0f;
        public float close = 0f;

        public StockBar() { }

        public StockBar(float open, float high, float low, float close)
        {
            this.open = open;
            this.high = high;
            this.low = low;
            this.close = close;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref open, "open", 0f);
            Scribe_Values.Look(ref high, "high", 0f);
            Scribe_Values.Look(ref low, "low", 0f);
            Scribe_Values.Look(ref close, "close", 0f);
        }
    }

    // 股票数据
    public class StockData : IExposable
    {
        public string code = "";
        public string name = "";
        public double price = 1.0;
        public double changePercent = 0.0;
        public double volatility = 0.1; // 波动率
        public List<StockBar> priceHistory = new List<StockBar>(); // 近30天价格K线历史

        public StockData() { }

        public StockData(string code, string name, double basePrice, double volatility)
        {
            this.code = code;
            this.name = name;
            this.price = basePrice;
            this.volatility = volatility;
            this.changePercent = 0.0;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref code, "code", "");
            Scribe_Values.Look(ref name, "name", "");
            Scribe_Values.Look(ref price, "price", 1.0);
            Scribe_Values.Look(ref changePercent, "changePercent", 0.0);
            Scribe_Values.Look(ref volatility, "volatility", 0.1);
            Scribe_Collections.Look(ref priceHistory, "priceHistory", LookMode.Deep);
        }
    }

    public class GameComponent_RimPay : GameComponent
    {
        // 殖民地数字国库余额
        private int cloudTreasuryBalance = 0;
        
        // 记录小人个人账户余额的字典
        // 使用小人的 ThingID 字符串作为键，确保存档安全
        private Dictionary<string, int> personalWallets = new Dictionary<string, int>();

        // 每位小人最近 15 条交易流水
        private Dictionary<string, List<TransactionRecord>> transactionLogs = new Dictionary<string, List<TransactionRecord>>();
        private const int MaxTransactionPerPawn = 15;

        // 自定义类型覆盖
        private Dictionary<string, string> pawnCustomTypes = new Dictionary<string, string>();

        // 借贷记录
        private List<LoanRecord> loans = new List<LoanRecord>();

        // 股票数据
        private List<StockData> stocks = new List<StockData>();
        private bool stocksInitialized = false;

        // 国库持仓: code → 股数
        private Dictionary<string, int> treasuryStockHoldings = new Dictionary<string, int>();

        // 国库持仓成本: code → 总成本 (@银)，用于计算盈亏
        private Dictionary<string, double> treasuryStockCost = new Dictionary<string, double>();

        // 上次更新股票价格的 tick
        private int lastStockUpdateTick = 0;

        // 上次发薪水的 Tick
        private int lastPaydayTick = 0;

        // 上次发薪的游戏日期（基地 tile 经度基准，用于跨天补偿触发）
        private int lastPaydayDayOfYear = -1;
        private int lastPaydayYear = -1;

        // 宏观事件（多日持续，存读档）
        private string activeMacroEvent = "";
        private int macroEventDaysLeft = 0;
        private string macroEventLabel = "";
        private string macroEventDesc = "";

        // RimSim 商店联动统计（入国库的店铺收入）
        private int storeSalesToday = 0;
        private int storeSalesAccumulated = 0;
        private int storeSalesDayStamp = -1;

        public GameComponent_RimPay(Game game)
        {
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            // 每 1000 tick (大约 16 秒) 检查一次发薪
            if (Find.TickManager.TicksGame % 1000 == 0)
            {
                CheckAndProcessPayroll();
            }
        }

        private void CheckAndProcessPayroll()
        {
            // 日期基准统一用基地地图的 tile（经度）——
            // GenLocalDate 的小时/日期按各地图经度计算，RPG 任务地图与基地经度不同，
            // 用 CurrentMap 会导致"0 点窗口"与基地时间错位（跨图跨午夜 → 当天集体不发薪）。
            Map baseMap = Find.AnyPlayerHomeMap ?? Find.CurrentMap;
            if (baseMap == null) return;

            int currentTick = Find.TickManager.TicksGame;
            int today = GenLocalDate.DayOfYear(baseMap);
            int thisYear = GenLocalDate.Year(baseMap);

            // 跨天补偿触发：基地日期与上次发薪日不同 → 立即补发（下一个 1000-tick 检查点），
            // 不再依赖 hour==0 的 1 小时窗口——暂停、换图、时差、睡过头全部免疫。
            // 00:00 后日期自然翻转即触发，30000 tick 防重保留，防止同一天内重复发薪。
            bool dayChanged = today != lastPaydayDayOfYear || thisYear != lastPaydayYear;
            if (dayChanged && currentTick - lastPaydayTick > 30000)
            {
                ProcessDailyPayroll();
                lastPaydayTick = currentTick;
            }
        }

        private void ProcessDailyPayroll()
        {
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;

            // 0. AI 经济：若开启，请求今日 AI 倍率（失败自动回退 1.0）
            float aiSalaryMult = 1f;
            float aiRentMult = 1f;
            float aiInterestDelta = 0f;
            if (RimPayMod.settings?.enableEconomyAI ?? false)
            {
                RimPayAIProvider.RequestDailyEconomyIfNeeded();
                aiSalaryMult = RimPayAIProvider.salaryMultiplier;
                aiRentMult = RimPayAIProvider.rentMultiplier;
                aiInterestDelta = RimPayAIProvider.interestRateDelta;
            }

            int totalPaid = 0;
            int totalRent = 0;
            int totalInterest = 0;
            int totalColonists = 0;

            // 范围：全体玩家殖民者（所有地图 + 车队 + 运输舱 + 奴隶），
            // 排除冷冻舱睡眠（Suspended）与任务借用小人（quest lodger）——
            // RPG 远征/任务地图上的小人同样领工资、按基地房间收租。
            List<Pawn> payrollPawns = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists;

            foreach (Pawn pawn in payrollPawns)
            {
                if (pawn == null || pawn.Dead || pawn.Suspended || pawn.IsQuestLodger()) continue;
                totalColonists++;

                // 1. 发放工资
                int dailySalary = Mathf.RoundToInt(CalculateDailySalary(pawn) * aiSalaryMult);
                if (dailySalary > 0)
                {
                    if (cloudTreasuryBalance >= dailySalary)
                    {
                        ModifyTreasury(-dailySalary, "发薪");
                        ModifyBalance(pawn, dailySalary, "每日工资");
                        totalPaid += dailySalary;
                    }
                    else
                    {
                        // 欠薪
                        pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed("RimDigital_UnpaidWage"));
                    }
                }

                // 2. 收取当日房租 (CalculateRent 内部已应用设备折扣)
                int rent = Mathf.RoundToInt(CalculateRent(pawn) * aiRentMult);

                int currentBalance = GetBalance(pawn);
                if (currentBalance >= rent)
                {
                    ModifyBalance(pawn, -rent, "住宿房租");
                    ModifyTreasury(rent, "房租");
                    totalRent += rent;
                }
                else
                {
                    // 钱不够付房租，强制清空，但不扣负数
                    int actualPaid = currentBalance;
                    ModifyBalance(pawn, -actualPaid, "住宿房租(欠费)");
                    ModifyTreasury(actualPaid, "房租(欠费)");
                    totalRent += actualPaid;
                }

                // 3. 星际基金“余额宝”利息发放 (由系统凭空产生，不扣国库)
                var S_interest = RimPayMod.settings;
                if (S_interest != null && S_interest.enableInterest)
                {
                    int newBalance = GetBalance(pawn);
                    if (newBalance > S_interest.interestMinBalance) // 达到门槛才计息
                    {
                        float effRate = S_interest.interestRate + aiInterestDelta / 100f; // 基础利率 + AI 浮动增量
                        int interest = (int)(newBalance * effRate);
                        if (interest > 0)
                        {
                            ModifyBalance(pawn, interest, "基金利息");
                            totalInterest += interest;
                            
                            // 定期给小人一点好心情，概率从设置读取
                            if (Rand.Chance(S_interest.interestMoodChance)) 
                            {
                                pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed("RimDigital_InterestYield"));
                            }
                        }
                    }
                }
            }

            if (totalPaid > 0 || totalRent > 0)
            {
                Messages.Message($"【RimPay结算中心】已完成本日薪资与房租结算 (共 {totalColonists} 人)。\n发放薪水: {totalPaid} @银 | 回收房租: {totalRent} @银 | 系统发放利息: {totalInterest} @银。", MessageTypeDefOf.NeutralEvent, false);
            }

            // 4. 处理贷款利息
            ProcessLoanInterest();

            // 4.5 宏观事件推进：接收/派生新事件，或推进剩余天数
            ProcessMacroEvent();

            // 5. 更新股票价格 + 小人自动炒股
            UpdateStockPrices();
            ProcessPawnAutoTrading();

            // 6. 每日财经日报信封 (默认开启，可在 RimPay 设置中关闭)
            if (RimPayMod.settings?.showDailyEconomyLetter ?? false)
            {
                SendDailyEconomyLetter(totalPaid, totalRent);
            }

            // 7. AI 财经事件 → RimTalk 播报 (让小人聊天提到今天的财经大事)
            BroadcastEconomyAiToRimTalk();

            // 8. 记录本次发薪的基地日期（跨天补偿触发的基准）
            lastPaydayDayOfYear = GenLocalDate.DayOfYear(map);
            lastPaydayYear = GenLocalDate.Year(map);
        }

        // ---- AI 财经事件播报给 RimTalk（随机一名殖民者触发一段对话） ----
        private void BroadcastEconomyAiToRimTalk()
        {
            var S = RimPayMod.settings;
            if (S == null || !S.enableEconomyAI || !S.enableAiRimTalkBroadcast) return;
            if (!RimPayAIProvider.hasAIResult) return;
            string evt = RimPayAIProvider.eventText;
            if (string.IsNullOrEmpty(evt)) return;
            if (!RimTalkBridge.IsRimTalkAvailable) return;

            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            var colonists = map.mapPawns.FreeColonists.Where(p => !p.Dead).ToList();
            if (colonists.Count == 0) return;

            Pawn speaker = colonists[Rand.Range(0, colonists.Count)];
            string msg = $"看了今天的边缘财经消息：{evt}，大家怎么看？";
            RimTalkBridge.TriggerDialogue(speaker, speaker, "AI财经头条", msg, false, AssistantPersona.Rimi);
        }

        // ---- 宏观事件推进：每日调用一次 ----
        // 优先采用 AI 返回的 macroEvent；若 AI 未指定，则按市场情绪以一定概率派生一个随机宏观事件。
        private void ProcessMacroEvent()
        {
            var S = RimPayMod.settings;
            bool aiOn = S != null && S.enableEconomyAI;

            // 如果旧事件还在，先推进天数
            if (!string.IsNullOrEmpty(activeMacroEvent))
            {
                macroEventDaysLeft--;
                if (macroEventDaysLeft <= 0)
                {
                    Verse.Log.Message($"[RimPay AI] 宏观事件【{activeMacroEvent}】已结束。");
                    activeMacroEvent = "";
                    macroEventLabel = "";
                    macroEventDesc = "";
                }
            }

            // AI 已关闭：不接收也不派生新事件
            if (!aiOn || !RimPayAIProvider.hasAIResult) return;

            // AI 显式指定了宏观事件
            string aiMacro = RimPayAIProvider.macroEvent;
            if (!string.IsNullOrEmpty(aiMacro) && aiMacro != "none" && aiMacro != activeMacroEvent)
            {
                StartMacroEvent(aiMacro);
                return;
            }

            // AI 未指定 → 按市场情绪随机派生（有事件期间不再叠加新事件）
            if (!string.IsNullOrEmpty(activeMacroEvent)) return;

            string tone = RimPayAIProvider.marketTone;
            float chance = tone == "bull" || tone == "bear" ? 0.30f : 0.12f;
            if (Rand.Chance(chance))
            {
                string[] pool;
                if (tone == "bull") pool = new[] { "tech_boom", "gold_rush" };
                else if (tone == "bear") pool = new[] { "market_crash", "shortage" };
                else pool = new[] { "tech_boom", "gold_rush", "market_crash", "shortage" };
                StartMacroEvent(pool[Rand.Range(0, pool.Length)]);
            }
        }

        private void StartMacroEvent(string key)
        {
            activeMacroEvent = key;
            macroEventDaysLeft = 3;
            macroEventLabel = GetMacroEventLabel(key);
            macroEventDesc = GetMacroEventDesc(key);
            Verse.Log.Message($"[RimPay AI] 触发宏观事件【{macroEventLabel}】，持续 {macroEventDaysLeft} 天。");

            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            Find.LetterStack.ReceiveLetter("边缘财经快讯：" + macroEventLabel,
                macroEventDesc + "\n\n（该宏观事件将持续 " + macroEventDaysLeft + " 天，期间影响股市/交易/搜刮等经济环节。）",
                LetterDefOf.PositiveEvent, (LookTargets)null);
        }

        private static string GetMacroEventLabel(string key)
        {
            switch (key)
            {
                case "market_crash": return "市场崩盘";
                case "gold_rush": return "淘金热";
                case "shortage": return "物资短缺";
                case "tech_boom": return "科技繁荣";
                default: return "未知事件";
            }
        }

        private static string GetMacroEventDesc(string key)
        {
            switch (key)
            {
                case "market_crash": return "边缘股市遭遇恐慌性抛售！未来数日股价大幅下挫、波动剧烈，商人也趁机压价。";
                case "gold_rush": return "淘金热席卷边缘世界！敌人身上携带的数字白银大幅增加，击杀者分成也更丰厚。";
                case "shortage": return "供应链紧张导致物价飞涨！餐费与生活成本上升，部分商品买卖价格明显波动。";
                case "tech_boom": return "科技股暴涨！股市普涨、交易价格水涨船高，商人愿意出更高价收购你的货物。";
                default: return "";
            }
        }

        // 状态面板/其他模块读取当前宏观事件
        public string ActiveMacroEvent => activeMacroEvent;
        public string MacroEventLabel => macroEventLabel;
        public int MacroEventDaysLeft => macroEventDaysLeft;

        // ======== RimSim 商店联动统计 ========
        public void RecordStoreSale(int amount)
        {
            if (amount <= 0) return;
            Map map = Find.AnyPlayerHomeMap;
            if (map != null)
            {
                int day = GenLocalDate.DayOfYear(map);
                if (storeSalesDayStamp != day)
                {
                    storeSalesDayStamp = day;
                    storeSalesToday = 0;
                }
            }
            storeSalesToday += amount;
            storeSalesAccumulated += amount;
        }

        public int StoreSalesToday => storeSalesToday;
        public int StoreSalesAccumulated => storeSalesAccumulated;

        // ---- 发送每日财经日报信封 ----
        private void SendDailyEconomyLetter(int totalPaid, int totalRent)
        {
            try
            {
                Map map = Find.AnyPlayerHomeMap;
                if (map == null) return;

                int day = GenLocalDate.DayOfYear(map);
                int year = GenLocalDate.Year(map);

                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine($"—— 边缘财经日报 · 第{year}.{day}日 ——");
                sb.AppendLine();

                // 国库概览
                int physical = map.resourceCounter.GetCount(ThingDefOf.Silver);
                sb.AppendLine($"国库收支：实体白银 {physical} @银，云端国库 {cloudTreasuryBalance} @银。");

                // 股市今日概览 (找涨幅与跌幅最大的股票)
                if (stocks.Count > 0)
                {
                    StockData best = null, worst = null;
                    foreach (StockData s in stocks)
                    {
                        if (best == null || s.changePercent > best.changePercent) best = s;
                        if (worst == null || s.changePercent < worst.changePercent) worst = s;
                    }
                    if (best != null && worst != null)
                    {
                        sb.AppendLine($"股市行情：{best.name} 领涨 {best.changePercent:F1}%，{worst.name} 领跌 {worst.changePercent:F1}%。");
                    }
                }

                // 借贷概览
                int playerLent = 0, playerBorrowed = 0;
                foreach (LoanRecord lr in loans)
                {
                    if (lr.amount <= 0) continue;
                    if (lr.isFromPlayer) playerLent += lr.amount;
                    else playerBorrowed += lr.amount;
                }
                if (playerLent > 0 || playerBorrowed > 0)
                {
                    sb.AppendLine($"借贷市场：在途放贷 {playerLent} @银，在途借款 {playerBorrowed} @银。");
                }
                else
                {
                    sb.AppendLine("借贷市场：今日无在途贷款与借款。");
                }

                // 工资与纳税概览
                if (totalPaid > 0)
                {
                    sb.AppendLine($"薪酬发放：今日共发放工资 {totalPaid} @银，回收房租 {totalRent} @银。");
                }

                // 昨日全天账单（从国库流水账汇总：餐费/搜刮/贸易/存取款/利息等）
                string yesterdayBill = SummarizeTreasuryDay(day - 1, year);
                if (!string.IsNullOrEmpty(yesterdayBill))
                {
                    sb.AppendLine(yesterdayBill);
                }

                // AI 财经头条 (AI 开启且成功时追加)
                if ((RimPayMod.settings?.enableEconomyAI ?? false) && RimPayAIProvider.hasAIResult && !string.IsNullOrEmpty(RimPayAIProvider.eventText))
                {
                    sb.AppendLine($"AI 财经头条：{RimPayAIProvider.eventText}");
                }

                sb.AppendLine();
                sb.AppendLine("— RimPay 数字经济拓展 · 边缘财经 —");

                Find.LetterStack.ReceiveLetter(
                    "边缘财经日报",
                    sb.ToString(),
                    LetterDefOf.NeutralEvent,
                    (LookTargets)null);
            }
            catch (System.Exception ex)
            {
                if (RimPayMod.settings?.enableEconomyAI ?? false)
                {
                    Verse.Log.Message($"[RimPay] 每日财经日报生成失败: {ex.Message}");
                }
            }
        }

        // 从国库流水账汇总指定日期的收支分类（供日报"昨日账单"使用）。
        // 无任何流水时返回 null（日报中省略该行）。
        private string SummarizeTreasuryDay(int day, int year)
        {
            if (day < 1) return null;

            int income = 0, expense = 0;
            Dictionary<string, int> incomeByReason = new Dictionary<string, int>();
            Dictionary<string, int> expenseByReason = new Dictionary<string, int>();

            foreach (TreasuryLogEntry e in treasuryLogs)
            {
                if (e.day != day || e.year != year) continue;
                if (e.amount > 0)
                {
                    income += e.amount;
                    if (!incomeByReason.ContainsKey(e.reason)) incomeByReason[e.reason] = 0;
                    incomeByReason[e.reason] += e.amount;
                }
                else if (e.amount < 0)
                {
                    expense += -e.amount;
                    if (!expenseByReason.ContainsKey(e.reason)) expenseByReason[e.reason] = 0;
                    expenseByReason[e.reason] += -e.amount;
                }
            }

            if (income == 0 && expense == 0) return null;

            string incomeStr = income > 0 ? string.Join("、", incomeByReason.Select(kv => $"{kv.Key}+{kv.Value}")) : "无";
            string expenseStr = expense > 0 ? string.Join("、", expenseByReason.Select(kv => $"{kv.Key}-{kv.Value}")) : "无";
            return $"昨日账单：收入 {income} @银 ({incomeStr}) ｜ 支出 {expense} @银 ({expenseStr})。";
        }

        private int CalculateRent(Pawn pawn)
        {
            var S = RimPayMod.settings;
            if (S == null || !S.enableRent) return 0;

            // 佩戴数码设备享受房租折扣
            float discount = HasDigitalDevice(pawn) ? S.deviceRentDiscount : 1f;

            int rent;
            if (pawn.ownership != null && pawn.ownership.OwnedRoom != null)
            {
                Room room = pawn.ownership.OwnedRoom;
                float impressiveness = room.GetStat(RoomStatDefOf.Impressiveness);
                
                if (impressiveness > 120) rent = S.rentLuxury; // 极其奢华
                else if (impressiveness > 80) rent = S.rentHigh;
                else if (impressiveness > 50) rent = S.rentMid;
                else if (impressiveness > 20) rent = S.rentLow;
                else rent = S.rentMin; // 简陋房间
            }
            else
            {
                rent = 1; // 睡地板或无专属房间
            }

            return Mathf.Max(0, Mathf.RoundToInt(rent * discount));
        }

        private bool HasDigitalDevice(Pawn pawn)
        {
            if (pawn.apparel == null) return false;
            foreach (var app in pawn.apparel.WornApparel)
            {
                if (app.def.apparel != null && app.def.apparel.tags != null)
                {
                    if (app.def.apparel.tags.Contains("DigitalStorage_TerminalAccess") || 
                        app.def.apparel.tags.Contains("RimDigital_WatchDevice") ||
                        app.def.defName.Contains("iShen") || 
                        app.def.defName.Contains("Nokirim"))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private int CalculateDailySalary(Pawn pawn)
        {
            var S = RimPayMod.settings;
            if (S == null || !S.enableSalary) return 0;

            // 第一层：基础零花钱/保底
            int salary = S.baseSalary;

            // 第二层：儿童/婴儿只拿零花钱
            if (pawn.DevelopmentalStage == DevelopmentalStage.Baby || pawn.DevelopmentalStage == DevelopmentalStage.Child)
            {
                return S.childSalary;
            }

            // 奴隶政策
            if (pawn.IsSlaveOfColony)
            {
                return S.slaveSalary; // 安抚津贴
            }

            // 第三层：技能绩效 (挑最高的技能加成)
            int bestSkill = 0;
            if (pawn.skills != null)
            {
                foreach (var skill in pawn.skills.skills)
                {
                    if (skill.Level > bestSkill) bestSkill = skill.Level;
                }
            }
            
            if (bestSkill >= 15) salary += S.expertBonus;      // 顶级专家
            else if (bestSkill >= 10) salary += S.skilledBonus; // 熟练工
            else if (bestSkill >= 6) salary += S.basicBonus;  // 普通工人

            return salary;
        }

        public int CloudTreasuryBalance
        {
            get { return cloudTreasuryBalance; }
        }

        // 国家资产（建筑）：殖民地玩家建筑（含地板）的账面财富值，不计入袭击威胁点
        public float GetNationalBuildingWealth(Map map)
        {
            if (map == null || map.wealthWatcher == null) return 0f;
            return map.wealthWatcher.WealthBuildings;
        }

        // 托管资产（随身装备）：小人/动物身上的武器、衣物、背包物品账面值（不含本体与机械体）
        public float GetEscrowedPawnEquipmentWealth(Map map)
        {
            if (map == null) return 0f;
            float total = 0f;
            List<Pawn> pawns = map.mapPawns.PawnsInFaction(Faction.OfPlayer);
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == null || p.Destroyed) continue;
                if (p.IsQuestLodger()) continue;
                if (p.IsColonyMech) continue;
                total += WealthWatcher.GetEquipmentApparelAndInventoryWealth(p);
            }
            return total;
        }

        // 修改数字国库余额
        public void ModifyTreasury(int amount)
        {
            ModifyTreasury(amount, null);
        }

        // 修改数字国库余额（带流水原因，供“昨日国库收支”统计）
        public void ModifyTreasury(int amount, string reason)
        {
            cloudTreasuryBalance += amount;
            if (cloudTreasuryBalance < 0) cloudTreasuryBalance = 0;

            if (!string.IsNullOrEmpty(reason) && amount != 0)
            {
                RecordTreasuryLog(amount, reason);
            }
        }

        // 从数字国库支付一笔费用（供其他模组/RimTuber 调用）。
        // 余额足够则扣款成功返回 true；不足则返回 false，不动国库。
        public bool TryPayFromTreasury(int amount, string reason = "")
        {
            if (amount <= 0) return true;
            if (cloudTreasuryBalance < amount) return false;
            ModifyTreasury(-amount, string.IsNullOrEmpty(reason) ? "模组扣款" : reason);
            return true;
        }

        // 完整的"兜底支付"接口（供其他模组集成 RimPay 使用）：
        // 先扣除地图物理白银，不足部分自动从数字国库补齐。
        // 总价（物理银+国库）足够则支付成功并返回实际从国库扣的数额；否则返回 -1 且分文不动。
        // 这是 RimPay 作为数字经济基础设施提供给模组的标准支付入口。
        public int TryPayTotal(int amount, Map map, string reason = "")
        {
            if (amount <= 0) return 0;

            int physical = map != null
                ? map.resourceCounter.GetCount(ThingDefOf.Silver)
                : 0;
            if (physical + cloudTreasuryBalance < amount) return -1;

            int physicalToUse = Mathf.Min(physical, amount);
            int treasuryToUse = amount - physicalToUse;

            if (physicalToUse > 0 && map != null)
            {
                int remaining = physicalToUse;
                var stacks = map.listerThings.ThingsOfDef(ThingDefOf.Silver).ToList();
                foreach (Thing stack in stacks)
                {
                    if (remaining <= 0) break;
                    int take = Mathf.Min(stack.stackCount, remaining);
                    Thing piece = stack.SplitOff(take);
                    if (piece != null) { piece.Destroy(); remaining -= take; }
                }
                map.resourceCounter.UpdateResourceCounts();
            }

            if (treasuryToUse > 0)
            {
                ModifyTreasury(-treasuryToUse, string.IsNullOrEmpty(reason) ? "模组扣款" : reason);
            }

            return treasuryToUse;
        }

        // ======== 国库流水账（昨日收支统计的真实数据源） ========
        public class TreasuryLogEntry : IExposable
        {
            public int day = 0;
            public int year = 0;
            public int amount = 0;
            public string reason = "";

            public TreasuryLogEntry() { }

            public TreasuryLogEntry(int day, int year, int amount, string reason)
            {
                this.day = day;
                this.year = year;
                this.amount = amount;
                this.reason = reason;
            }

            public void ExposeData()
            {
                Scribe_Values.Look(ref day, "day", 0);
                Scribe_Values.Look(ref year, "year", 0);
                Scribe_Values.Look(ref amount, "amount", 0);
                Scribe_Values.Look(ref reason, "reason", "");
            }
        }

        private List<TreasuryLogEntry> treasuryLogs = new List<TreasuryLogEntry>();

        private void RecordTreasuryLog(int amount, string reason)
        {
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            int day = GenLocalDate.DayOfYear(map);
            int year = GenLocalDate.Year(map);
            treasuryLogs.Add(new TreasuryLogEntry(day, year, amount, reason));

            // 只保留最近约 90 条，避免无限膨胀
            while (treasuryLogs.Count > 90)
            {
                treasuryLogs.RemoveAt(0);
            }
        }

        // 获取小人个人余额
        public int GetBalance(Pawn pawn)
        {
            if (pawn == null) return 0;
            string key = pawn.ThingID;
            if (personalWallets.ContainsKey(key))
            {
                return personalWallets[key];
            }
            return 0;
        }

        // 修改小人个人余额（不记流水 - 低层调用）
        public void ModifyBalance(Pawn pawn, int amount)
        {
            ModifyBalance(pawn, amount, null);
        }

        // 修改小人个人余额（带流水记录）
        public void ModifyBalance(Pawn pawn, int amount, string reason)
        {
            if (pawn == null) return;
            string key = pawn.ThingID;
            
            if (!personalWallets.ContainsKey(key))
            {
                personalWallets[key] = 0;
            }
            
            personalWallets[key] += amount;
            if (personalWallets[key] < 0) personalWallets[key] = 0;

            // 记录流水
            if (!string.IsNullOrEmpty(reason))
            {
                RecordTransaction(pawn, amount, reason);
            }
        }

        // 查询某人最近的交易流水
        public List<TransactionRecord> GetTransactions(Pawn pawn)
        {
            if (pawn == null) return new List<TransactionRecord>();
            string key = pawn.ThingID;
            if (transactionLogs.ContainsKey(key))
            {
                return transactionLogs[key];
            }
            return new List<TransactionRecord>();
        }

        // 记录一条交易（有界存储，最多 MaxTransactionPerPawn 条）
        private void RecordTransaction(Pawn pawn, int amount, string reason)
        {
            if (pawn == null) return;
            string key = pawn.ThingID;

            if (!transactionLogs.ContainsKey(key))
            {
                transactionLogs[key] = new List<TransactionRecord>();
            }

            var list = transactionLogs[key];
            // 日期统一按基地 tile 经度（任务地图与基地经度不同，各自"同一天"会错位，
            // 统一基准后个人钱包流水与日报/国库流水账的日期判定完全一致）
            Map txMap = Find.AnyPlayerHomeMap ?? pawn.MapHeld ?? Find.CurrentMap;
            if (txMap == null) return; // 无任何地图时无法记日期，跳过流水
            int currentDay = GenLocalDate.DayOfYear(txMap);
            int currentYear = GenLocalDate.Year(txMap);

            list.Insert(0, new TransactionRecord(currentDay, currentYear, amount, reason, amount > 0));

            // 最多保留 15 条
            while (list.Count > MaxTransactionPerPawn)
            {
                list.RemoveAt(list.Count - 1);
            }
        }

        // ======== 自定义类型 ========
        public void SetPawnCustomType(Pawn pawn, string type)
        {
            if (pawn == null) return;
            string key = pawn.ThingID;
            if (type == null) pawnCustomTypes.Remove(key);
            else pawnCustomTypes[key] = type;
        }

        public string GetPawnCustomType(Pawn pawn)
        {
            if (pawn == null) return null;
            string key = pawn.ThingID;
            if (pawnCustomTypes.ContainsKey(key)) return pawnCustomTypes[key];
            return null;
        }

        // ======== 借贷系统 ========
        public int GetNetFactionDebt(int factionLoadID)
        {
            int net = 0;
            foreach (LoanRecord lr in loans)
            {
                if (lr.factionKey == factionLoadID && lr.amount > 0)
                {
                    if (lr.isFromPlayer) net += lr.amount; // 玩家放出的债 (正账)
                    else net -= lr.amount; // 玩家欠的债 (负账)
                }
            }
            return net;
        }

        public void AddLoan(Faction faction, int amount, double rate)
        {
            var S = RimPayMod.settings;
            int period = (S != null && S.enableLoan && S.loanDefaultPeriod > 0) ? S.loanDefaultPeriod : 15;
            Map dateMap = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            if (dateMap == null) return;
            int day = GenLocalDate.DayOfYear(dateMap);
            int year = GenLocalDate.Year(dateMap);
            loans.Add(new LoanRecord(faction.Name, faction.loadID, amount, rate, day + period, year, true));
        }

        public void AddBorrow(Faction faction, int amount, double rate)
        {
            var S = RimPayMod.settings;
            int period = (S != null && S.enableLoan && S.loanDefaultPeriod > 0) ? S.loanDefaultPeriod : 15;
            Map dateMap = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            if (dateMap == null) return;
            int day = GenLocalDate.DayOfYear(dateMap);
            int year = GenLocalDate.Year(dateMap);
            loans.Add(new LoanRecord(faction.Name, faction.loadID, amount, rate, day + period, year, false));
        }

        public List<LoanRecord> GetActiveLoans()
        {
            return loans.Where(l => l.amount > 0).ToList();
        }

        public void ProcessLoanInterest()
        {
            List<LoanRecord> toRemove = new List<LoanRecord>();
            Map dateMap = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            if (dateMap == null) return;
            int currentDay = GenLocalDate.DayOfYear(dateMap);
            int currentYear = GenLocalDate.Year(dateMap);

            foreach (LoanRecord lr in loans)
            {
                if (lr.amount <= 0) continue;
                if (currentYear > lr.dueYear || (currentYear == lr.dueYear && currentDay >= lr.dueDay))
                {
                    int interest = (int)(lr.amount * lr.rate);
                    if (lr.isFromPlayer)
                    {
                        // 放贷 → 收到利息
                        ModifyTreasury(interest, "放贷利息");
                        Messages.Message($"贷款利息到账: 从 {lr.factionName} 收到 {interest} @银 利息。", MessageTypeDefOf.PositiveEvent, false);
                        lr.dueDay = currentDay + 15;
                    }
                    else
                    {
                        // 借款 → 支付利息
                        if (cloudTreasuryBalance >= interest)
                        {
                            ModifyTreasury(-interest, "借款利息");
                            Messages.Message($"贷款利息支出: 向 {lr.factionName} 支付 {interest} @银 利息。", MessageTypeDefOf.NegativeEvent, false);
                            lr.dueDay = currentDay + 15;
                        }
                        else
                        {
                            // 国库不足 → 标记坏账
                            Messages.Message($"警告: 无法支付 {lr.factionName} 的利息 {interest} @银，关系恶化！", MessageTypeDefOf.ThreatBig, false);
                            // 寻找派系并降低好感
                            Faction faction = Find.FactionManager.AllFactions.FirstOrDefault(f => f.loadID == lr.factionKey);
                            if (faction != null) faction.TryAffectGoodwillWith(Faction.OfPlayer, -15);
                            toRemove.Add(lr);
                        }
                    }
                }
            }
            foreach (LoanRecord lr in toRemove) loans.Remove(lr);
        }

        // ======== 股票系统 ========
        public void InitializeStocks()
        {
            if (stocks != null && stocks.Count > 0)
            {
                stocksInitialized = true;
                return;
            }
            stocksInitialized = true;
            if (stocks == null) stocks = new List<StockData>();
            stocks.Clear();

            // 固定公司股票
            stocks.Add(CreateStock("$ISHN", "iShen 集团", 12.5, 0.05));
            stocks.Add(CreateStock("$RIMT", "Rimtendo 任天缘", 8.2, 0.08));
            stocks.Add(CreateStock("$RONY", "RONY 缘尼", 7.8, 0.07));
            stocks.Add(CreateStock("$MSFT", "Macrosoft 微硬", 15.0, 0.03));
            stocks.Add(CreateStock("$NOKI", "Nokirim 诺基林", 18.5, 0.02));
            stocks.Add(CreateStock("$DAMI", "DaMi 科技", 4.5, 0.12));
            stocks.Add(CreateStock("$BOOS", "BoosDead", 0.8, 0.25));
            stocks.Add(CreateStock("$STAR", "StarveMe 科技", 3.2, 0.18));
            stocks.Add(CreateStock("$DSF", "数字白银杠杆基金", 10.0, RimPayMod.settings?.stocksFundVolatility ?? 0.30f));

            // 动态派系股票
            foreach (Faction f in Find.FactionManager.AllFactionsVisible)
            {
                if (f.IsPlayer || f.temporary) continue;
                string code = "$" + (f.Name.Length >= 3 ? f.Name.Substring(0, 3).ToUpper() : f.Name.ToUpper());
                double basePrice = (f.def.techLevel == TechLevel.Animal ? 0.5 : f.def.techLevel == TechLevel.Neolithic ? 1.0 : f.def.techLevel == TechLevel.Medieval ? 2.0 : f.def.techLevel == TechLevel.Industrial ? 4.0 : f.def.techLevel == TechLevel.Spacer ? 8.0 : f.def.techLevel == TechLevel.Ultra ? 12.0 : 16.0);
                double vol = 0.08 + (double)f.def.techLevel * 0.01;
                stocks.Add(CreateStock(code, f.Name, basePrice, vol));
            }
        }

        // 创建股票并预生成一段初始历史价格 (蜡烛K线图)
        private static StockData CreateStock(string code, string name, double basePrice, double volatility)
        {
            StockData s = new StockData(code, name, basePrice, volatility);
            float prevClose = (float)basePrice;
            int genDays = RimPayMod.settings?.stockHistoryDays > 0 ? RimPayMod.settings.stockHistoryDays : 15;
            for (int i = 0; i < genDays; i++)
            {
                float open = prevClose;
                float change = (float)((Rand.Value * 2.0 - 1.0) * volatility * 0.5);
                float close = open * (1f + change);
                if (close < 0.1f) close = 0.1f;
                float high = Mathf.Max(open, close) + (float)(Rand.Value * open * volatility * 0.3);
                float low = Mathf.Min(open, close) - (float)(Rand.Value * open * volatility * 0.3);
                if (low < 0.05f) low = 0.05f;

                s.priceHistory.Add(new StockBar(open, high, low, close));
                prevClose = close;
            }
            s.price = s.priceHistory[s.priceHistory.Count - 1].close;
            return s;
        }

        public List<StockData> GetStockPrices()
        {
            if (stocks == null || stocks.Count == 0)
            {
                stocksInitialized = false;
                InitializeStocks();
            }

            // 额外保险 (自愈机制)：如果任何股票的价格历史损坏、为空或不足 15 天
            // 立刻在内存中为其虚拟补充生成完整的 15 天 K线，确保任何新老存档都能立刻看到完美的大盘
            foreach (StockData s in stocks)
            {
                if (s.priceHistory == null || s.priceHistory.Count < 15)
                {
                    RegenerateHistoryFor(s);
                }
            }

            return stocks;
        }

        private void RegenerateHistoryFor(StockData s)
        {
            if (s.priceHistory == null)
            {
                s.priceHistory = new List<StockBar>();
            }
            s.priceHistory.Clear();

            float prevClose = (float)s.price;
            // 倒推生成历史 (确保最后一天价格刚好等于当前真实价格)
            int regenDays = RimPayMod.settings?.stockHistoryDays > 0 ? RimPayMod.settings.stockHistoryDays : 15;
            for (int i = 0; i < regenDays; i++)
            {
                float open = prevClose;
                float change = (float)((Rand.Value * 2.0 - 1.0) * s.volatility * 0.5);
                float close = open * (1f + change);
                if (close < 0.1f) close = 0.1f;
                float high = Mathf.Max(open, close) + (float)(Rand.Value * open * s.volatility * 0.3);
                float low = Mathf.Min(open, close) - (float)(Rand.Value * open * s.volatility * 0.3);
                if (low < 0.05f) low = 0.05f;

                s.priceHistory.Add(new StockBar(open, high, low, close));
                prevClose = close;
            }
            s.price = s.priceHistory[s.priceHistory.Count - 1].close;
        }

        public void UpdateStockPrices()
        {
            int currentTick = Find.TickManager.TicksGame;
            int interval = RimPayMod.settings?.stockPriceUpdateTicks > 0 ? RimPayMod.settings.stockPriceUpdateTicks : 60000;
            if (currentTick - lastStockUpdateTick < interval) return;
            lastStockUpdateTick = currentTick;

            // 与股价更新联动：股价刷新时同时检查是否需要重新请求 AI（由刷新间隔控制频率）
            if (RimPayMod.settings?.enableEconomyAI ?? false)
            {
                RimPayAIProvider.RequestDailyEconomyIfNeeded();
            }

            if (!stocksInitialized) InitializeStocks();

            // AI 市场情绪影响当日涨跌方向（bull 普涨 / bear 普跌 / neutral 随机）
            float toneBias = 0f;
            if (RimPayMod.settings?.enableEconomyAI ?? false)
            {
                if (RimPayAIProvider.marketTone == "bull") toneBias = 0.25f;
                else if (RimPayAIProvider.marketTone == "bear") toneBias = -0.25f;
            }

            // 宏观事件额外影响：崩盘强烈下压且波动加剧，繁荣强烈上推
            float macroBias = 0f;
            float macroVolMult = 1f;
            if (activeMacroEvent == "market_crash") { macroBias = -0.45f; macroVolMult = 1.6f; }
            else if (activeMacroEvent == "tech_boom") { macroBias = 0.35f; macroVolMult = 1.2f; }

            foreach (StockData s in stocks)
            {
                float open = (float)s.price;
                double change = (Rand.Value * 2.0 - 1.0) * s.volatility * macroVolMult + (toneBias + macroBias) * s.volatility;
                float close = open * (1f + (float)change);
                if (close < 0.1f) close = 0.1f;
                float high = Mathf.Max(open, close) + (float)(Rand.Value * open * s.volatility * 0.3);
                float low = Mathf.Min(open, close) - (float)(Rand.Value * open * s.volatility * 0.3);
                if (low < 0.05f) low = 0.05f;

                s.price = close;
                s.changePercent = change * 100.0;

                // 记录历史价格K线（保留天数从设置读取）
                int historyDays = RimPayMod.settings?.stockHistoryDays > 0 ? RimPayMod.settings.stockHistoryDays : 15;
                s.priceHistory.Add(new StockBar(open, high, low, close));
                while (s.priceHistory.Count > historyDays)
                {
                    s.priceHistory.RemoveAt(0);
                }
            }
        }

        // 国库持仓
        public int GetStockHeld(string code)
        {
            return treasuryStockHoldings.ContainsKey(code) ? treasuryStockHoldings[code] : 0;
        }

        public void BuyStock(string code, int shares, double price)
        {
            if (!treasuryStockHoldings.ContainsKey(code)) treasuryStockHoldings[code] = 0;
            treasuryStockHoldings[code] += shares;
            if (!treasuryStockCost.ContainsKey(code)) treasuryStockCost[code] = 0.0;
            treasuryStockCost[code] += shares * price;
        }

        public void SellStock(string code, int shares, double price)
        {
            if (!treasuryStockHoldings.ContainsKey(code)) treasuryStockHoldings[code] = 0;
            int heldBefore = treasuryStockHoldings[code];
            treasuryStockHoldings[code] -= shares;
            if (treasuryStockHoldings[code] < 0) treasuryStockHoldings[code] = 0;

            // 按比例冲减成本
            if (!treasuryStockCost.ContainsKey(code)) treasuryStockCost[code] = 0.0;
            if (heldBefore > 0)
            {
                double ratio = (double)Mathf.Min(shares, heldBefore) / heldBefore;
                treasuryStockCost[code] -= treasuryStockCost[code] * ratio;
                if (treasuryStockCost[code] < 0) treasuryStockCost[code] = 0.0;
            }
        }

        // 国库某支股票的持仓盈亏（正=盈利，负=亏损）
        public int GetStockProfit(string code)
        {
            int held = GetStockHeld(code);
            if (held <= 0) return 0;
            StockData s = stocks?.FirstOrDefault(x => x.code == code);
            if (s == null) return 0;
            double marketValue = held * s.price;
            double cost = treasuryStockCost.ContainsKey(code) ? treasuryStockCost[code] : 0.0;
            return Mathf.RoundToInt((float)(marketValue - cost));
        }

        // 小人自动炒股
        public void ProcessPawnAutoTrading()
        {
            if (!stocksInitialized) InitializeStocks();
            UpdateStockPrices();

            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;

            foreach (Pawn pawn in map.mapPawns.FreeColonists)
            {
                if (pawn.Dead) continue;
                var S_trade = RimPayMod.settings;
                if (S_trade == null || !S_trade.enablePawnTrading) return;

                int balance = GetBalance(pawn);
                if (balance < S_trade.pawnTradeMinBalance) continue;
                if (pawn.needs?.mood?.CurLevelPercentage < S_trade.pawnTradeMoodRequirement) continue;

                // 性格影响
                bool isGreedy = pawn.story?.traits?.HasTrait(TraitDefOf.Greedy) ?? false;
                bool isRisky = pawn.story?.traits?.HasTrait(TraitDefOf.Bloodlust) ?? false;
                bool isCareful = !isGreedy && !isRisky;
                int skill = pawn.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0;

                // 选择一个高波动或低波动股票
                List<StockData> candidates = new List<StockData>();
                foreach (StockData s in stocks)
                {
                    if (isGreedy || isRisky) { if (s.volatility > 0.1) candidates.Add(s); }
                    else if (isCareful) { if (s.volatility < 0.08) candidates.Add(s); }
                    else candidates.Add(s);
                }
                if (candidates.Count == 0) candidates = stocks;

                StockData target = candidates[Rand.Range(0, candidates.Count)];
                int investAmt = Mathf.Min((int)(balance * S_trade.pawnTradeInvestRatio), S_trade.pawnTradeInvestCap);
                if (investAmt < 10) continue;

                int shares = Mathf.Max(1, (int)(investAmt / target.price));
                double cost = shares * target.price;
                int intCost = Mathf.RoundToInt((float)cost);

                if (intCost <= 0) continue;

                // 技能命中率
                float hitChance = S_trade.pawnTradeBaseHitChance + skill * S_trade.pawnTradeHitPerSkill;
                bool positive = Rand.Value < hitChance;
                double change = (positive ? 1.0 : -1.0) * target.volatility * 0.5f;
                double profitPerShare = target.price * change;
                int totalProfit = Mathf.RoundToInt((float)(shares * profitPerShare));

                if (totalProfit > 0)
                {
                    // 赚钱
                    ModifyBalance(pawn, totalProfit, "炒股收益");
                    pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed("RimDigital_Profit"));
                    if (totalProfit > S_trade.pawnTradeRimTalkThreshold && RimTalkBridge.IsRimTalkAvailable)
                    {
                        string msg = $"嗨！我买的 {target.code} ({target.name}) 今天涨了，赚了 {totalProfit} @银！运气不错！";
                        RimTalkBridge.TriggerDialogue(pawn, pawn, "炒股收益", msg, false, AssistantPersona.Rimi);
                    }
                }
                else if (totalProfit < 0)
                {
                    // 亏钱
                    int loss = -totalProfit;
                    ModifyBalance(pawn, -loss, "炒股亏损");
                    pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed("RimDigital_Loss"));
                    if (loss > S_trade.pawnTradeRimTalkThreshold && RimTalkBridge.IsRimTalkAvailable)
                    {
                        string msg = $"唉...买的 {target.code} ({target.name}) 跌了，亏了 {loss} @银...再也不信了！";
                        RimTalkBridge.TriggerDialogue(pawn, pawn, "炒股亏损", msg, false, AssistantPersona.Sirim);
                    }
                }
            }
        }

        // ======== 每日国库收支汇总 ========
        public string GetDailyTreasurySummary(int day, int year)
        {
            // 从国库流水账汇总（真实记录国库每一笔收支）
            int income = 0, expense = 0;
            Dictionary<string, int> incomeByReason = new Dictionary<string, int>();
            Dictionary<string, int> expenseByReason = new Dictionary<string, int>();

            foreach (TreasuryLogEntry e in treasuryLogs)
            {
                if (e.day != day || e.year != year) continue;
                if (e.amount > 0)
                {
                    income += e.amount;
                    if (!incomeByReason.ContainsKey(e.reason)) incomeByReason[e.reason] = 0;
                    incomeByReason[e.reason] += e.amount;
                }
                else
                {
                    expense += -e.amount;
                    if (!expenseByReason.ContainsKey(e.reason)) expenseByReason[e.reason] = 0;
                    expenseByReason[e.reason] += -e.amount;
                }
            }

            string incomeStr = income > 0 ? string.Join(" | ", incomeByReason.Select(kv => $"{kv.Key}+{kv.Value}")) : "无";
            string expenseStr = expense > 0 ? string.Join(" | ", expenseByReason.Select(kv => $"{kv.Key}-{kv.Value}")) : "无";
            return $"昨日国库收支: 收入 {income} @银 ({incomeStr}) | 支出 {expense} @银 ({expenseStr})";
        }

        // 存档与读档
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref cloudTreasuryBalance, "cloudTreasuryBalance", 0);
            Scribe_Values.Look(ref lastPaydayTick, "lastPaydayTick", 0);
            Scribe_Values.Look(ref lastPaydayDayOfYear, "lastPaydayDayOfYear", -1);
            Scribe_Values.Look(ref lastPaydayYear, "lastPaydayYear", -1);
            Scribe_Values.Look(ref stocksInitialized, "stocksInitialized", false);
            Scribe_Values.Look(ref lastStockUpdateTick, "lastStockUpdateTick", 0);
            Scribe_Collections.Look(ref personalWallets, "personalWallets", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref transactionLogs, "transactionLogs", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref pawnCustomTypes, "pawnCustomTypes", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref loans, "loans", LookMode.Deep);
            Scribe_Collections.Look(ref stocks, "stocks", LookMode.Deep);
            Scribe_Collections.Look(ref treasuryStockHoldings, "treasuryStockHoldings", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref treasuryStockCost, "treasuryStockCost", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref treasuryLogs, "treasuryLogs", LookMode.Deep);
            Scribe_Values.Look(ref activeMacroEvent, "activeMacroEvent", "");
            Scribe_Values.Look(ref macroEventDaysLeft, "macroEventDaysLeft", 0);
            Scribe_Values.Look(ref macroEventLabel, "macroEventLabel", "");
            Scribe_Values.Look(ref macroEventDesc, "macroEventDesc", "");
            Scribe_Values.Look(ref storeSalesToday, "storeSalesToday", 0);
            Scribe_Values.Look(ref storeSalesAccumulated, "storeSalesAccumulated", 0);
            Scribe_Values.Look(ref storeSalesDayStamp, "storeSalesDayStamp", -1);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (personalWallets == null) personalWallets = new Dictionary<string, int>();
                if (transactionLogs == null) transactionLogs = new Dictionary<string, List<TransactionRecord>>();
                if (pawnCustomTypes == null) pawnCustomTypes = new Dictionary<string, string>();
                if (loans == null) loans = new List<LoanRecord>();
                if (stocks == null || stocks.Count == 0)
                {
                    stocks = new List<StockData>();
                    stocksInitialized = false;
                    InitializeStocks();
                }
                if (treasuryStockHoldings == null) treasuryStockHoldings = new Dictionary<string, int>();
                if (treasuryStockCost == null) treasuryStockCost = new Dictionary<string, double>();
                if (treasuryLogs == null) treasuryLogs = new List<TreasuryLogEntry>();

                // 旧档迁移：有持仓但无成本记录（成本追踪上线前的购买），
                // 按当前股价回填成本，使盈亏从 0 起步、之后随价格真实浮动。
                foreach (var kvp in treasuryStockHoldings)
                {
                    if (kvp.Value <= 0 || treasuryStockCost.ContainsKey(kvp.Key)) continue;
                    StockData sd = stocks?.FirstOrDefault(x => x.code == kvp.Key);
                    if (sd != null)
                    {
                        treasuryStockCost[kvp.Key] = kvp.Value * sd.price;
                        Verse.Log.Message($"[RimPay] 迁移持仓成本: {kvp.Key} x{kvp.Value} @ {sd.price:F2}（旧档回填）");
                    }
                }
            }
        }
    }
}
