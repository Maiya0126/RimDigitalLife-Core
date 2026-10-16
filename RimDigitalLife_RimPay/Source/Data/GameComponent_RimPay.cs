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
        public int factionLoadID = -1; // 绑定的派系 loadID（>=0 表示派系股票）
        public string category = "Tech"; // 行业分类 (Tech/Entertainment/Food/Agri/Ranch/ECom/Media/Sports/Medical/Arms/Mining/Finance/Quantum/Faction)
        public string desc = ""; // 简要介绍
        public List<string> recentNews = new List<string>(); // 最近动态新闻列表 (如 "第1.15日 [+8.5%] 仿生科技突破...")
        public string lastHeadline = ""; // 最新一条新闻简讯

        public bool IsFactionStock => factionLoadID >= 0;

        public StockData() { }

        public StockData(string code, string name, double basePrice, double volatility, string category = "Tech", int factionLoadID = -1, string desc = "")
        {
            this.code = code;
            this.name = name;
            this.price = basePrice;
            this.volatility = volatility;
            this.changePercent = 0.0;
            this.category = category;
            this.factionLoadID = factionLoadID;
            this.desc = desc;
            this.recentNews = new List<string>();
            this.lastHeadline = "";
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref code, "code", "");
            Scribe_Values.Look(ref name, "name", "");
            Scribe_Values.Look(ref price, "price", 1.0);
            Scribe_Values.Look(ref changePercent, "changePercent", 0.0);
            Scribe_Values.Look(ref volatility, "volatility", 0.1);
            Scribe_Values.Look(ref factionLoadID, "factionLoadID", -1);
            Scribe_Values.Look(ref category, "category", "Tech");
            Scribe_Values.Look(ref desc, "desc", "");
            Scribe_Values.Look(ref lastHeadline, "lastHeadline", "");
            Scribe_Collections.Look(ref recentNews, "recentNews", LookMode.Value);
            Scribe_Collections.Look(ref priceHistory, "priceHistory", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (recentNews == null) recentNews = new List<string>();
            }
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

        // 股市事件驱动系统（动态事件缓存，每日结算后衰减/清空）
        private List<string> recentResearchTags = new List<string>();
        private List<int> recentRaidFactionIDs = new List<int>();
        private Dictionary<int, int> recentBigTradeFactions = new Dictionary<int, int>();

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

            int today = GenLocalDate.DayOfYear(baseMap);
            int thisYear = GenLocalDate.Year(baseMap);

            // 跨天补偿触发：基地日期与上次发薪日不同 → 立即补发（下一个 1000-tick 检查点），
            // 不再依赖 hour==0 的 1 小时窗口——暂停、换图、时差、睡过头全部免疫。
            // 注意：不能用 tick 差值做防重——补发后紧接着的跨天（tick 差 < 30000）会被挡住，
            // 导致下一次发薪延迟约 8 游戏小时；dayChanged 本身已保证"一天只发一次"
            // （发薪日期在 ProcessDailyPayroll 成功末尾才记录，中途异常会自动重试）。
            bool dayChanged = today != lastPaydayDayOfYear || thisYear != lastPaydayYear;
            if (dayChanged)
            {
                ProcessDailyPayroll();
                lastPaydayTick = Find.TickManager.TicksGame; // 仅存档展示用途，不再参与触发判定
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

            // RimTuber 动态墙联动：大事件自动上墙
            TryPostRimTuberFinanceFeed("【宏观快讯】" + macroEventLabel, macroEventDesc, "市场头条");
        }

        private static void TryPostRimTuberFinanceFeed(string title, string body, string tag = "边缘财经")
        {
            if (!IsRimTuberActive()) return;
            try
            {
                var asm = System.AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "RimTuber");
                if (asm == null) return;
                var apiType = asm.GetType("RimTuber.RimTuberAPI");
                if (apiType == null) return;
                var postMethod = apiType.GetMethod("PostFinanceFeed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (postMethod != null)
                {
                    postMethod.Invoke(null, new object[] { title, body, tag });
                }
            }
            catch { }
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
                        string bSign = best.changePercent >= 0 ? "+" : "";
                        sb.AppendLine($"股市行情：{best.name} 领涨 {bSign}{best.changePercent:F1}%，{worst.name} 领跌 {worst.changePercent:F1}%。");
                        if (!string.IsNullOrEmpty(best.lastHeadline))
                        {
                            sb.AppendLine($"  [利好聚焦] {best.name} —— {best.lastHeadline}");
                        }
                        if (!string.IsNullOrEmpty(worst.lastHeadline))
                        {
                            sb.AppendLine($"  [利空预警] {worst.name} —— {worst.lastHeadline}");
                        }
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
            Map dateMap = Find.AnyPlayerHomeMap ?? Find.CurrentMap;
            if (dateMap == null) return;
            int day = GenLocalDate.DayOfYear(dateMap);
            int year = GenLocalDate.Year(dateMap);
            loans.Add(new LoanRecord(faction.Name, faction.loadID, amount, rate, day + period, year, true));
        }

        public void AddBorrow(Faction faction, int amount, double rate)
        {
            var S = RimPayMod.settings;
            int period = (S != null && S.enableLoan && S.loanDefaultPeriod > 0) ? S.loanDefaultPeriod : 15;
            Map dateMap = Find.AnyPlayerHomeMap ?? Find.CurrentMap;
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
            Map dateMap = Find.AnyPlayerHomeMap ?? Find.CurrentMap;
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

        // ======== 股票系统（全行业板块 + 动态派系 + 现实/气候/科研/直播事件驱动） ========

        private struct StockTemplate
        {
            public string code;
            public string name;
            public double basePrice;
            public double volatility;
            public string category;
            public string desc;

            public StockTemplate(string code, string name, double basePrice, double volatility, string category, string desc)
            {
                this.code = code;
                this.name = name;
                this.basePrice = basePrice;
                this.volatility = volatility;
                this.category = category;
                this.desc = desc;
            }
        }

        private static readonly List<StockTemplate> CoreFixedStocks = new List<StockTemplate>
        {
            // --- 消费电子与数码科技 (Tech) ---
            new StockTemplate("$ISHN", "iShen 集团", 12.5, 0.05, "Tech", "星系消费电子巨头，主营智能手机与移动终端"),
            new StockTemplate("$MSFT", "Macrosoft 微硬", 15.0, 0.03, "Tech", "老牌软件与操作系统垄断服务商"),
            new StockTemplate("$NOKI", "Nokirim 诺基林", 18.5, 0.02, "Tech", "工业级通信设备与军工防护外壳制造"),
            new StockTemplate("$DAMI", "DaMi 科技", 4.5, 0.12, "Tech", "平价个人智能出行与电动平衡车研发"),
            new StockTemplate("$BOOS", "BoosDead", 0.8, 0.25, "Tech", "低端山寨数码与劣质电子元件组装厂"),

            // --- 文化娱乐与大众消费 (Entertainment & Food) ---
            new StockTemplate("$RIMT", "Rimtendo 任天缘", 8.2, 0.08, "Entertainment", "风靡边缘世界的掌机与卡带娱乐开发商"),
            new StockTemplate("$RONY", "RONY 缘尼", 7.8, 0.07, "Entertainment", "高端影音视听主机与影像传感器巨头"),
            new StockTemplate("$STAR", "StarveMe 科技", 3.2, 0.18, "Food", "深空应急合成食品与长保压缩营养膏"),

            // --- 现代现实主力行业 (Agri, Ranch, ECom, Media, Sports, Medical, Arms, Mining) ---
            new StockTemplate("$AGRI", "GreenSprout 绿芽农业", 5.2, 0.10, "Agri", "大宗水培农作物与抗旱耐寒农产品种植"),
            new StockTemplate("$MUFF", "MuffaloCorp 耗牛畜牧", 4.8, 0.11, "Ranch", "纯血耗牛繁育与保暖毛皮肉品联合体"),
            new StockTemplate("$ECOM", "OrbitalBay 轨道淘客", 8.8, 0.08, "ECom", "星系轨道电商网购平台与货运直达"),
            new StockTemplate("$TUBR", "TuberNet 块茎传媒", 6.5, 0.15, "Media", "流媒体与顶流自媒体孵化联盟"),
            new StockTemplate("$NEWS", "RimTribune 边缘先驱报", 7.2, 0.06, "Media", "历史悠久的星系通讯社与中立财经新闻网"),
            new StockTemplate("$GLAD", "ApexArena 巅峰竞技", 6.0, 0.12, "Sports", "星际极限滑板竞速赛与角斗联赛娱乐"),
            new StockTemplate("$GLTR", "GlitterPharm 闪耀制药", 16.0, 0.06, "Medical", "闪耀世界高阶仿生体与全能抗生素研发"),
            new StockTemplate("$MEDX", "MedEx 生物科技", 9.5, 0.10, "Medical", "异种基因重构与人造内脏培育工厂"),
            new StockTemplate("$APEX", "ApexArms 顶峰防务", 14.0, 0.09, "Arms", "行星重型近防炮塔与高斯电磁火器制造"),
            new StockTemplate("$PLST", "PlasteelDyn 玻璃钢重工", 11.0, 0.08, "Arms", "超合金复合动力装甲与高密度防护盾研制"),
            new StockTemplate("$URAN", "UranCorp 泛星系铀能", 13.5, 0.12, "Mining", "超临界核能聚变反应堆与深层地质采掘"),

            // --- 投机衍生基金 (Finance) ---
            new StockTemplate("$DSF", "数字白银杠杆基金", 10.0, 0.30, "Finance", "追踪大盘情绪的高杠杆投机衍生基金")
        };

        private static readonly List<StockTemplate> QuantumNetOperatorStocks = new List<StockTemplate>
        {
            new StockTemplate("$SCOM", "星链通信 (StarCom)", 12.0, 0.08, "Quantum", "量子网络低轨星链覆盖通信商"),
            new StockTemplate("$NEBU", "星云算力 (NebulaCloud)", 15.5, 0.07, "Quantum", "超导云算力与 RimSeek 智算调度服务商"),
            new StockTemplate("$CART", "卡带发行商 (CartoBoard)", 7.5, 0.10, "Quantum", "数字娱乐内容发行与虚拟软件载体"),
            new StockTemplate("$BAZR", "赛博百货 (CyberBazaar)", 9.0, 0.09, "Quantum", "全星系虚拟网购与数字商品平台"),
            new StockTemplate("$SCAN", "深矿雷达 (MineScan)", 11.0, 0.08, "Quantum", "轨道重力梯度测绘与深层矿脉遥感云平台")
        };

        public void InitializeStocks()
        {
            if (stocksInitialized && stocks != null && stocks.Count > 0)
            {
                EnsureAllStocksPresent(force: true);
                return;
            }
            stocksInitialized = true;
            if (stocks == null) stocks = new List<StockData>();
            stocks.Clear();
            EnsureAllStocksPresent(force: true);
        }

        // 节流：GetStockPrices 会被证券界面每帧调用，Ensure 全量扫描 O(n²) 且遍历派系，
        // 非强制时每 2500 tick（约 40 秒）才真正扫描一次，避免每帧开销。
        private int lastStockEnsureTick = -999999;

        public void EnsureAllStocksPresent(bool force = false)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (!force && now - lastStockEnsureTick < 2500) return;
            lastStockEnsureTick = now;

            if (stocks == null) stocks = new List<StockData>();

            // 1. 检查核心固定股票
            foreach (var t in CoreFixedStocks)
            {
                double vol = (t.code == "$DSF") ? (RimPayMod.settings?.stocksFundVolatility ?? 0.30f) : t.volatility;
                var existing = stocks.FirstOrDefault(s => s.code == t.code);
                if (existing == null)
                {
                    stocks.Add(CreateStock(t.code, t.name, t.basePrice, vol, t.category, -1, t.desc));
                }
                else
                {
                    if (string.IsNullOrEmpty(existing.category)) existing.category = t.category;
                    if (string.IsNullOrEmpty(existing.desc)) existing.desc = t.desc;
                }
            }

            // 2. 若安装量子网络，检查运营商股票
            if (IsQuantumNetActive())
            {
                foreach (var t in QuantumNetOperatorStocks)
                {
                    var existing = stocks.FirstOrDefault(s => s.code == t.code);
                    if (existing == null)
                    {
                        stocks.Add(CreateStock(t.code, t.name, t.basePrice, t.volatility, t.category, -1, t.desc));
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(existing.category)) existing.category = t.category;
                        if (string.IsNullOrEmpty(existing.desc)) existing.desc = t.desc;
                    }
                }
            }

            // 3. 动态派系股票
            if (Find.FactionManager != null)
            {
                foreach (Faction f in Find.FactionManager.AllFactionsVisible)
                {
                    if (f.IsPlayer || f.temporary) continue;
                    string code = "$" + (f.Name.Length >= 3 ? f.Name.Substring(0, 3).ToUpper() : f.Name.ToUpper());
                    var existing = stocks.FirstOrDefault(s => s.code == code || s.factionLoadID == f.loadID);
                    if (existing == null)
                    {
                        double basePrice = (f.def.techLevel == TechLevel.Animal ? 0.5 : f.def.techLevel == TechLevel.Neolithic ? 1.0 : f.def.techLevel == TechLevel.Medieval ? 2.0 : f.def.techLevel == TechLevel.Industrial ? 4.0 : f.def.techLevel == TechLevel.Spacer ? 8.0 : f.def.techLevel == TechLevel.Ultra ? 12.0 : 16.0);
                        double vol = 0.08 + (double)f.def.techLevel * 0.01;
                        stocks.Add(CreateStock(code, f.Name, basePrice, vol, "Faction", f.loadID, $"{f.Name} (科技级别: {f.def.techLevel.ToString()})"));
                    }
                    else
                    {
                        existing.factionLoadID = f.loadID;
                        existing.category = "Faction";
                    }
                }
            }
        }

        // 创建股票并预生成一段初始历史价格 (蜡烛K线图)
        private static StockData CreateStock(string code, string name, double basePrice, double volatility, string category = "Tech", int factionLoadID = -1, string desc = "")
        {
            StockData s = new StockData(code, name, basePrice, volatility, category, factionLoadID, desc);
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

            EnsureAllStocksPresent();

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

        // ======== 外部事件通知接口 ========
        public void NotifyResearchFinished(ResearchProjectDef proj)
        {
            if (proj == null) return;
            string tag = (proj.defName ?? "").ToLowerInvariant();
            if (!recentResearchTags.Contains(tag)) recentResearchTags.Add(tag);
        }

        public void NotifyRaidStarted(int factionLoadID)
        {
            if (factionLoadID >= 0 && !recentRaidFactionIDs.Contains(factionLoadID))
            {
                recentRaidFactionIDs.Add(factionLoadID);
            }
        }

        public void NotifyBigTrade(int factionLoadID, int silverAmount)
        {
            if (factionLoadID < 0 || silverAmount <= 0) return;
            if (!recentBigTradeFactions.ContainsKey(factionLoadID)) recentBigTradeFactions[factionLoadID] = 0;
            recentBigTradeFactions[factionLoadID] += silverAmount;
        }

        private bool HasRecentResearchMatch(params string[] keywords)
        {
            if (recentResearchTags == null || recentResearchTags.Count == 0) return false;
            foreach (var tag in recentResearchTags)
            {
                foreach (var kw in keywords)
                {
                    if (tag.Contains(kw)) return true;
                }
            }
            return false;
        }

        private static bool IsConditionActive(Map map, string defName)
        {
            if (map?.gameConditionManager == null) return false;
            var def = DefDatabase<GameConditionDef>.GetNamedSilentFail(defName);
            return def != null && map.gameConditionManager.ConditionIsActive(def);
        }

        private static bool IsRimTuberActive()
        {
            return ModLister.GetActiveModWithIdentifier("maiya.rimtuber", true) != null ||
                   ModLister.GetActiveModWithIdentifier("maiya.rimtuber_steam", true) != null;
        }

        private static bool CheckRimTuberLiveStatus(out bool isSelling, out float heat)
        {
            isSelling = false;
            heat = 0f;
            if (!IsRimTuberActive()) return false;
            try
            {
                var asm = System.AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "RimTuber");
                if (asm == null) return false;
                var apiType = asm.GetType("RimTuber.RimTuberAPI");
                if (apiType == null) return false;
                var isLiveMethod = apiType.GetMethod("IsLive", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (isLiveMethod != null && (bool)isLiveMethod.Invoke(null, null))
                {
                    var heatMethod = apiType.GetMethod("GetHeatLevel", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (heatMethod != null) heat = (float)heatMethod.Invoke(null, null);

                    var salesType = asm.GetType("RimTuber.LivestreamSalesManager");
                    if (salesType != null)
                    {
                        var instProp = salesType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        var inst = instProp?.GetValue(null, null);
                        if (inst != null)
                        {
                            var isSellingField = salesType.GetField("isSelling", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (isSellingField != null) isSelling = (bool)isSellingField.GetValue(inst);
                        }
                    }
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static bool IsQuantumNetActive()
        {
            return ModLister.GetActiveModWithIdentifier("maiya.RimDigitalLife.QuantumNet", true) != null ||
                   ModLister.GetActiveModWithIdentifier("maiya.RimDigitalLife.QuantumNet_steam", true) != null ||
                   ModLister.GetActiveModWithIdentifier("chezhou.RimDigitalLife.QuantumNet", true) != null;
        }

        private static bool CheckQuantumNetEvent(string eventId)
        {
            if (!IsQuantumNetActive()) return false;
            try
            {
                var asm = System.AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "RimDigitalLife_QuantumNet");
                if (asm == null) return false;
                var mgrType = asm.GetType("RimDigitalLife_QuantumNet.NetworkEventManager");
                if (mgrType == null) return false;
                var isEventMethod = mgrType.GetMethod("IsEventActive", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (isEventMethod != null)
                {
                    return (bool)isEventMethod.Invoke(null, new object[] { eventId });
                }
            }
            catch { }
            return false;
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
            EnsureAllStocksPresent();

            // 1. AI 市场情绪影响当日涨跌方向（bull 普涨 / bear 普跌 / neutral 随机）
            float toneBias = 0f;
            if (RimPayMod.settings?.enableEconomyAI ?? false)
            {
                if (RimPayAIProvider.marketTone == "bull") toneBias = 0.20f;
                else if (RimPayAIProvider.marketTone == "bear") toneBias = -0.20f;
            }

            // 2. 宏观事件额外影响：崩盘强烈下压且波动加剧，繁荣强烈上推
            float macroBias = 0f;
            float macroVolMult = 1f;
            if (activeMacroEvent == "market_crash") { macroBias = -0.35f; macroVolMult = 1.5f; }
            else if (activeMacroEvent == "tech_boom") { macroBias = 0.28f; macroVolMult = 1.2f; }

            // 3. 探测全球气候与天灾环境
            Map map = Find.AnyPlayerHomeMap ?? Find.CurrentMap;
            bool hasHeatWave = IsConditionActive(map, "HeatWave");
            bool hasColdSnap = IsConditionActive(map, "ColdSnap");
            bool hasSolarFlare = IsConditionActive(map, "SolarFlare");
            bool hasVolcanicWinter = IsConditionActive(map, "VolcanicWinter") || IsConditionActive(map, "ToxicFallout");

            // 4. 探测 RimTuber 直播状态
            bool isTuberLive = CheckRimTuberLiveStatus(out bool isSelling, out float tuberHeat);

            // 5. 遍历计算每只股票的新价格
            foreach (StockData s in stocks)
            {
                float specificBias = 0f;
                float stockVolMult = macroVolMult;
                Faction f = s.IsFactionStock ? Find.FactionManager?.AllFactions?.FirstOrDefault(x => x.loadID == s.factionLoadID) : null;

                // --- 派系股票联动 ---
                if (s.IsFactionStock)
                {
                    if (f != null)
                    {
                        // 外交好感度影响
                        float goodwill = f.PlayerGoodwill;
                        if (goodwill >= 75) specificBias += 0.08f; // 盟友溢价
                        else if (goodwill >= 20) specificBias += 0.04f;
                        else if (goodwill < 0)
                        {
                            specificBias -= 0.06f;
                            stockVolMult *= 1.25f;
                        }

                        // 近期发动袭击：战败重创
                        if (recentRaidFactionIDs.Contains(f.loadID))
                        {
                            specificBias -= 0.12f;
                            stockVolMult *= 1.4f;
                        }

                        // 近期大宗贸易提振
                        if (recentBigTradeFactions.ContainsKey(f.loadID) && recentBigTradeFactions[f.loadID] >= 2000)
                        {
                            specificBias += 0.07f;
                        }
                    }
                }
                else
                {
                    // --- 行业股票现实/事件联动 ---
                    switch (s.category)
                    {
                        case "Agri": // 绿芽农业
                            if (hasHeatWave || hasVolcanicWinter) specificBias -= 0.10f; // 干旱大灾
                            if (HasRecentResearchMatch("hydroponics", "agriculture", "tree", "plant")) specificBias += 0.10f;
                            break;

                        case "Ranch": // 耗牛畜牧
                            if (hasColdSnap) specificBias += 0.08f; // 严寒保暖需求
                            if (hasVolcanicWinter) specificBias -= 0.08f;
                            break;

                        case "Food": // StarveMe 应急合成食品
                            if (hasHeatWave || hasVolcanicWinter) specificBias += 0.12f; // 大饥荒推高应急食品需求！
                            break;

                        case "Medical": // 闪耀制药 / MedEx生物
                            if (HasRecentResearchMatch("medicine", "bionics", "prosthetics", "xenogerm", "gene", "vital")) specificBias += 0.12f;
                            break;

                        case "Arms": // 顶峰军工 / 玻璃钢重工
                            if (recentRaidFactionIDs.Count > 0)
                            {
                                specificBias += 0.10f; // 战事爆发拉动军火采购
                                stockVolMult *= 1.3f;
                            }
                            if (HasRecentResearchMatch("gun", "armor", "mortar", "turret", "precision", "charge", "defense")) specificBias += 0.10f;
                            break;

                        case "Mining": // 泛星系铀能
                            if (hasSolarFlare) specificBias -= 0.08f;
                            if (HasRecentResearchMatch("deepdrill", "geothermal", "nuclear", "energy")) specificBias += 0.11f;
                            break;

                        case "Tech": // iShen, 微硬, 诺基林, 大米等
                            if (hasSolarFlare) specificBias -= 0.09f;
                            if (HasRecentResearchMatch("microelectronics", "fabrication", "shield", "computer", "digital", "battery")) specificBias += 0.09f;
                            if (isSelling && (s.code == "$DAMI" || s.code == "$ISHN")) specificBias += 0.08f; // 带货热销
                            break;

                        case "Entertainment": // 任天缘, 缘尼
                            if (isTuberLive) specificBias += 0.05f;
                            break;

                        case "ECom": // 轨道淘客电商
                            if (isSelling) specificBias += 0.10f; // 直播带货直接利好电商
                            if (CheckQuantumNetEvent("cyber_sale")) specificBias += 0.12f;
                            break;

                        case "Media": // 块茎传媒, 先驱报
                            if (isTuberLive)
                            {
                                specificBias += 0.06f;
                                if (tuberHeat > 0.4f) stockVolMult *= 1.4f; // 弹幕热度高推升传媒投机
                            }
                            break;

                        case "Sports": // 巅峰竞技
                            if (hasHeatWave || hasVolcanicWinter) specificBias -= 0.06f;
                            break;

                        case "Quantum": // 量子网络运营商股票
                            if (CheckQuantumNetEvent("starlink_storm") && s.code == "$SCOM") { specificBias -= 0.15f; stockVolMult *= 1.5f; }
                            if (CheckQuantumNetEvent("cyber_sale") && s.code == "$BAZR") specificBias += 0.12f;
                            if (CheckQuantumNetEvent("minescan_calib") && s.code == "$SCAN") specificBias += 0.10f;
                            if (CheckQuantumNetEvent("network_congested") && s.code == "$NEBU") specificBias -= 0.07f;
                            break;

                        case "Finance": // $DSF 杠杆基金
                            if (isTuberLive && tuberHeat > 0.5f) stockVolMult *= 1.6f; // 散户游资狂热炒作
                            break;
                    }
                }

                // 6. 随机机构做空与游资拉盘事件（极低概率发生微小突发冲击）
                bool wasShock = false;
                if (Rand.Chance(0.04f))
                {
                    wasShock = true;
                    bool isLong = Rand.Bool;
                    specificBias += isLong ? Rand.Range(0.08f, 0.14f) : Rand.Range(-0.14f, -0.08f);
                    stockVolMult *= 1.3f;
                }

                // 综合偏置（折中力度控制在 ±0.25 范围以内）
                float combinedBias = Mathf.Clamp((toneBias + macroBias) * 0.5f + specificBias, -0.25f, 0.25f);
                float open = (float)s.price;
                double change = (Rand.Value * 2.0 - 1.0) * s.volatility * stockVolMult + combinedBias * s.volatility;
                float close = open * (1f + (float)change);
                if (close < 0.1f) close = 0.1f;
                float high = Mathf.Max(open, close) + (float)(Rand.Value * open * s.volatility * 0.3);
                float low = Mathf.Min(open, close) - (float)(Rand.Value * open * s.volatility * 0.3);
                if (low < 0.05f) low = 0.05f;

                s.price = close;
                s.changePercent = change * 100.0;

                // 记录历史价格 K 线
                int historyDays = RimPayMod.settings?.stockHistoryDays > 0 ? RimPayMod.settings.stockHistoryDays : 15;
                s.priceHistory.Add(new StockBar(open, high, low, close));
                while (s.priceHistory.Count > historyDays)
                {
                    s.priceHistory.RemoveAt(0);
                }

                // 7. 动态生成并记录该股票今日现实级财经新闻简讯
                int day = map != null ? GenLocalDate.DayOfYear(map) : 1;
                int year = map != null ? GenLocalDate.Year(map) : 5500;
                string headline = GenerateStockNewsHeadline(s, s.changePercent, map, hasHeatWave, hasColdSnap, hasSolarFlare, hasVolcanicWinter, isSelling, isTuberLive, tuberHeat, f, specificBias, wasShock);
                s.lastHeadline = headline;
                string signTag = s.changePercent >= 0 ? "[涨]" : "[跌]";
                string signNum = s.changePercent >= 0 ? "+" : "";
                if (s.recentNews == null) s.recentNews = new List<string>();
                s.recentNews.Insert(0, $"第{year}.{day}日 {signTag} {signNum}{s.changePercent:F1}% {headline}");
                while (s.recentNews.Count > 5) s.recentNews.RemoveAt(s.recentNews.Count - 1);
            }

            // RimTuber 动态墙联动：若有股票单日剧烈异动（>=18%），发热搜卡片
            var topStock = stocks.OrderByDescending(x => Mathf.Abs((float)x.changePercent)).FirstOrDefault();
            if (topStock != null && Mathf.Abs((float)topStock.changePercent) >= 18f)
            {
                string sign = topStock.changePercent >= 0 ? "+" : "";
                string action = topStock.changePercent >= 0 ? "暴涨飙升" : "遭遇闪崩";
                string newsDetail = !string.IsNullOrEmpty(topStock.lastHeadline) ? topStock.lastHeadline : "受多空资金激烈博弈影响，日内振幅巨大。";
                TryPostRimTuberFinanceFeed($"【大盘异动】{topStock.code} 单日{action} {sign}{topStock.changePercent:F1}%",
                    $"{newsDetail} 最新股价报 {topStock.price:F2} @银。", "行情快报");
            }

            // 更新周期结束：重置近期短期事件缓存，准备下一周期
            recentResearchTags.Clear();
            recentRaidFactionIDs.Clear();
            recentBigTradeFactions.Clear();
        }

        // ======== 现实级生动财经新闻生成器 ========
        private string GenerateStockNewsHeadline(StockData s, double changePct, Map map,
            bool hasHeatWave, bool hasColdSnap, bool hasSolarFlare, bool hasVolcanicWinter,
            bool isSelling, bool isTuberLive, float tuberHeat, Faction f, float specificBias, bool wasShock)
        {
            bool isUp = changePct >= 0;

            // 1. 突发机构做空与游资扫货
            if (wasShock)
            {
                if (isUp)
                    return "神秘离岸财团大举建仓增持，主力资金强力买入拉抬股价。";
                else
                    return "知名星际做空机构发布做空研报，质疑其季度报表存在营收水分。";
            }

            // 2. 派系股票
            if (s.IsFactionStock)
            {
                if (f != null && recentRaidFactionIDs.Contains(f.loadID))
                {
                    return "对殖民地突袭失利惨遭反击，主力精锐受重创引发市场抛售。";
                }
                if (f != null && recentBigTradeFactions.ContainsKey(f.loadID) && recentBigTradeFactions[f.loadID] >= 2000)
                {
                    return "与殖民地完成数千白银大宗物资交割，双边商贸景气度创季度新高。";
                }
                if (f != null)
                {
                    if (f.PlayerGoodwill >= 75)
                        return "与殖民地深化战略同盟，双边边境关税豁免获议会通过。";
                    if (f.PlayerGoodwill < 0)
                        return isUp ? "边境局势缓和预期升温，防务摩擦成本有所控制。" : "地缘外交摩擦升级，边境商道受阻引发商业预期恶化。";
                }
                return isUp ? "领地完成季节性赋税征缴，财政盈余超市场预期。" : "星际通胀与补给消耗高企，派系季度财报不及预期。";
            }

            // 3. 行业股票
            switch (s.category)
            {
                case "Agri":
                    if (hasHeatWave || hasVolcanicWinter)
                        return "极端恶劣天灾侵袭水培温室，主要粮食种植区发布大宗减产预警。";
                    if (HasRecentResearchMatch("hydroponics", "agriculture", "tree", "plant"))
                        return "高抗逆性水培植株育种科研突破，大宗粮食现货量价齐升。";
                    return isUp ? "季节性农产丰产交割，深空储备粮订单超预期。" : "农用营养液原料涨价，水培农业利润空间受挤压。";

                case "Ranch":
                    if (hasColdSnap)
                        return "极寒寒流席卷大陆！高保暖纯血耗牛毛皮制品与肉品需求井喷。";
                    if (hasVolcanicWinter)
                        return "天灾严寒与饲草短缺导致牲畜出栏受限，畜牧供应链承压。";
                    return isUp ? "纯血耗牛优良繁育率大幅提升，优质肉品订单排期饱满。" : "饲料运输周期延宕，牲畜屠宰加工利润略有收窄。";

                case "Food":
                    if (hasHeatWave || hasVolcanicWinter)
                        return "全球干旱饥荒推升避险需求！StarveMe 应急长保合成代餐罐头遭全星系抢购。";
                    return isUp ? "快节奏拓荒代餐包销量走强，应急食品业务营收稳健。" : "普通食材产出充裕，高价压缩食品需求短期承压。";

                case "Medical":
                    if (HasRecentResearchMatch("medicine", "bionics", "prosthetics", "xenogerm", "gene", "vital"))
                        return "高阶仿生神经修复与生化血清科研突破，机构全面上调目标评级。";
                    return isUp ? "全能抗生素与免疫增强试剂订单充裕，生物制药板块量价齐升。" : "新型基因药剂临床审批流程延后，短线遭遇谨慎资金抛盘。";

                case "Arms":
                    if (recentRaidFactionIDs.Count > 0)
                        return "战事爆发推升近防重火器紧急订购潮，军工防务全线走强！";
                    if (HasRecentResearchMatch("gun", "armor", "mortar", "turret", "precision", "charge", "defense"))
                        return "高能电磁武器与复合护甲专利通过军方验证，防务采购订单排满。";
                    return isUp ? "定居点要塞化工程升级，阵地防御装备采购需求旺盛。" : "星系局部停火呼声渐高，消耗性弹药采购指标短期缩减。";

                case "Mining":
                    if (hasSolarFlare)
                        return "地磁剧烈扰动导致深层地质勘探雷达失真，采矿工程进度受阻。";
                    if (HasRecentResearchMatch("deepdrill", "geothermal", "nuclear", "energy"))
                        return "深层地热核裂变能源科研落地，重工业高能采矿全面提速。";
                    return isUp ? "高品位铀矿石现货紧俏，重工业能源需求带动开采估值。" : "大型采掘钻头损耗偏高，开采成本超预期导致短线震荡。";

                case "Tech":
                    if (hasSolarFlare)
                        return "太阳耀斑导致多处数据中心离线，云端通信服务短暂中断引发抛盘。";
                    if (HasRecentResearchMatch("microelectronics", "fabrication", "shield", "computer", "digital", "battery"))
                        return "微电子制造与纳米集成电路研发突破，数码芯片板块全面爆发。";
                    if (s.code == "$BOOS")
                        return isUp ? "廉价拼装电子元件在落后定居点大举铺货，薄利多销提振估值。" : "涉嫌芯片套壳组装与虚假宣传，遭消费者权益保护协会立案调查。";
                    if (s.code == "$DAMI")
                        return isSelling ? "顶流主播直播带货热销！电动平衡车与智能出行系列卖爆断货。" : (isUp ? "平价代步平衡车在各定居点迅速普及，年轻拓荒群体形成抢购。" : "电机驱动控制器供应链延迟，部分型号交付周期拉长。");
                    if (s.code == "$ISHN")
                        return isSelling ? "旗舰智能终端直播带货爆单，星系高端消费数码龙头地位稳固。" : (isUp ? "新一代智能手表与终端发布，生态订阅营收占比创新高。" : "硬件产品迭代放缓引发投资者观望，短线处于估值盘整。");
                    return isUp ? "数码终端生态协同优势显现，软件服务增值业务强劲。" : "核心芯片原料采购成本攀升，硬件毛利率面临阶段性压力。";

                case "Entertainment":
                    if (isTuberLive)
                        return "主播正在火热开播，边缘娱乐互动软件在线活跃度达日内峰值。";
                    return isUp ? "年度现象级游戏掌机大作首发告捷，卡带与数字下载量双双破纪录。" : "缺乏爆款独占游戏大作支撑，硬件销售进入周期性淡季。";

                case "ECom":
                    if (isSelling)
                        return "顶流主播单场带货成交额破纪录，跨星系轨道电商供应链全负荷运转！";
                    if (CheckQuantumNetEvent("cyber_sale"))
                        return "赛博百货数字大促全面引爆，全星系线上零售成交单量破历史极值！";
                    return isUp ? "自营轨道货运穿梭机队完成扩容，次日达履约率提升至新高。" : "远距离星系货运能耗成本上涨，电商物流配送毛利承压。";

                case "Media":
                    if (isTuberLive && tuberHeat > 0.4f)
                        return "直播间弹幕热度爆表全网刷屏，流媒体广告赞助分成创历史新高！";
                    return isUp ? "深度揭秘调查独家报道引发全网热议，媒体受众订阅数爆发增长。" : "边缘虚假传言引发公关信任危机，部分大牌赞助商削减宣发预算。";

                case "Sports":
                    return isUp ? "星际极限滑板竞速锦标赛收官，赛事联名周边与门票收入创佳绩。" : "主要参赛选手训练中遭遇骨折重伤退赛，观赛热度有所回落。";

                case "Quantum":
                    if (CheckQuantumNetEvent("starlink_storm") && s.code == "$SCOM")
                        return "强磁场星链风暴袭来！低轨中继通信链路大面积掉线，通信服务紧急维护。";
                    if (CheckQuantumNetEvent("cyber_sale") && s.code == "$BAZR")
                        return "赛博百货年中庆典开启，虚拟网购与数字资产交易量打破历史峰值。";
                    if (CheckQuantumNetEvent("minescan_calib") && s.code == "$SCAN")
                        return "重力梯度遥感雷达高精度校准完毕，探明超高品位富矿引发勘探狂潮。";
                    if (CheckQuantumNetEvent("network_congested") && s.code == "$NEBU")
                        return "算力并发量达到带宽上限导致网络拥堵，云端任务排队引发客户投诉。";
                    return isUp ? "量子加密信道扩容顺利交付，企业级安全通信协议获普遍采用。" : "微波中继天线阵列例行检修，区域网络吞吐量受到短期限制。";

                case "Finance":
                    return isUp ? "多头游资大举加杠杆抢筹，投机衍生基金日内放量强势拉抬！" : "高杠杆持仓盘遭遇集中爆仓清算，空方借势打压引发踩踏抛盘。";
            }

            return isUp ? "基本面保持健康，多方主力资金有序护盘买入。" : "获利盘选择高位离场套现，短线承压小幅回踩。";
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
                    if (totalProfit >= 100)
                    {
                        TryPostRimTuberFinanceFeed("【殖民地股神】" + pawn.LabelShort + " 爆赚离场",
                            $"{pawn.LabelShort} 今日凭借敏锐嗅觉成功抄底 {target.code} ({target.name})，单笔大赚 {totalProfit} @银！在殖民地引起热议。", "股民高光");
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
                    if (loss >= 100)
                    {
                        TryPostRimTuberFinanceFeed("【天台风很大】" + pawn.LabelShort + " 抄底被套牢",
                            $"{pawn.LabelShort} 高位追涨 {target.code} ({target.name})，单日暴跌割肉，损失 {loss} @银... 今晚只能吃营养膏了。", "理财翻车");
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
            Scribe_Collections.Look(ref recentResearchTags, "recentResearchTags", LookMode.Value);
            Scribe_Collections.Look(ref recentRaidFactionIDs, "recentRaidFactionIDs", LookMode.Value);
            Scribe_Collections.Look(ref recentBigTradeFactions, "recentBigTradeFactions", LookMode.Value, LookMode.Value);
            
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
                if (recentResearchTags == null) recentResearchTags = new List<string>();
                if (recentRaidFactionIDs == null) recentRaidFactionIDs = new List<int>();
                if (recentBigTradeFactions == null) recentBigTradeFactions = new Dictionary<int, int>();

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
