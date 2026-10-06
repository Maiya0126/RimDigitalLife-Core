using System;
using System.Collections.Generic;
using System.Threading;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimDigitalLife_RimPay
{
    public class RimPayMod : Mod
    {
        public static RimPaySettings settings;
        private string jsonBuffer = "";

        // 设置页分页（顶部 Tab）
        private enum RimPaySettingsPage { General, AI }
        private RimPaySettingsPage currentPage = RimPaySettingsPage.General;
        private Vector2 generalScroll = Vector2.zero;
        private Vector2 aiScroll = Vector2.zero;

        // 测试连接状态
        private bool testInProgress = false;
        private string testResultText = "";
        private RimPayProviderConfig testConfigOverride;

        public RimPayMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<RimPaySettings>();
            jsonBuffer = settings.economyJsonTemplate;
        }

        public override string SettingsCategory()
        {
            return "Rim Digital Life - RimPay 边缘数码生活：数字经济拓展 (v0.7.01)";
        }

        private int selectedConfigIdx = -1;

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // 顶部 Tab：常规设置 / AI 设置
            Rect tabRect = new Rect(inRect.x, inRect.y, inRect.width, 32f);
            Rect generalTab = new Rect(tabRect.x, tabRect.y, tabRect.width / 2f, tabRect.height);
            Rect aiTab = new Rect(tabRect.x + tabRect.width / 2f, tabRect.y, tabRect.width / 2f, tabRect.height);

            if (currentPage == RimPaySettingsPage.General)
                Widgets.DrawHighlight(generalTab);
            else
                Widgets.DrawHighlight(aiTab);

            if (Widgets.ButtonText(generalTab, "常规设置 (General)"))
            {
                currentPage = RimPaySettingsPage.General;
            }
            if (Widgets.ButtonText(aiTab, "AI 设置 (AI Economy)"))
            {
                currentPage = RimPaySettingsPage.AI;
            }

            Rect contentRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 40f);
            if (currentPage == RimPaySettingsPage.General)
                DrawGeneralPage(contentRect);
            else
                DrawAIPage(contentRect);
        }

        // ============ 常规设置页（薪资/房租/餐费/利息/借贷/股市/炒股/搜刮/钱包） ============
        private void DrawGeneralPage(Rect inRect)
        {
            float viewHeight = 3400f;
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, viewHeight);
            Widgets.BeginScrollView(inRect, ref generalScroll, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            // ============ 薪资 ============
            listing.Label("<b>薪资发放</b>");
            listing.GapLine();
            listing.CheckboxLabeled("启用工资发放", ref settings.enableSalary);
            listing.Label($"基础工资: {settings.baseSalary} @银/日");
            settings.baseSalary = Mathf.RoundToInt(listing.Slider(settings.baseSalary, 0f, 100f));
            listing.Label($"儿童零花钱: {settings.childSalary} @银/日");
            settings.childSalary = Mathf.RoundToInt(listing.Slider(settings.childSalary, 0f, 50f));
            listing.Label($"奴隶安抚金: {settings.slaveSalary} @银/日");
            settings.slaveSalary = Mathf.RoundToInt(listing.Slider(settings.slaveSalary, 0f, 50f));
            listing.Label($"专家加成: +{settings.expertBonus} @银/日");
            settings.expertBonus = Mathf.RoundToInt(listing.Slider(settings.expertBonus, 0f, 100f));
            listing.Label($"熟练工加成: +{settings.skilledBonus} @银/日");
            settings.skilledBonus = Mathf.RoundToInt(listing.Slider(settings.skilledBonus, 0f, 60f));
            listing.Label($"基础工人加成: +{settings.basicBonus} @银/日");
            settings.basicBonus = Mathf.RoundToInt(listing.Slider(settings.basicBonus, 0f, 40f));
            listing.Gap(10f);

            // ============ 房租 ============
            listing.Label("<b>住宿房租</b>");
            listing.GapLine();
            listing.CheckboxLabeled("启用房租收取", ref settings.enableRent);
            listing.Label($"奢华房间 (印象>120): {settings.rentLuxury} @银/日");
            settings.rentLuxury = Mathf.RoundToInt(listing.Slider(settings.rentLuxury, 0f, 50f));
            listing.Label($"高级房间 (印象>80): {settings.rentHigh} @银/日");
            settings.rentHigh = Mathf.RoundToInt(listing.Slider(settings.rentHigh, 0f, 40f));
            listing.Label($"舒适房间 (印象>50): {settings.rentMid} @银/日");
            settings.rentMid = Mathf.RoundToInt(listing.Slider(settings.rentMid, 0f, 30f));
            listing.Label($"普通房间 (印象>20): {settings.rentLow} @银/日");
            settings.rentLow = Mathf.RoundToInt(listing.Slider(settings.rentLow, 0f, 20f));
            listing.Label($"简陋房间: {settings.rentMin} @银/日");
            settings.rentMin = Mathf.RoundToInt(listing.Slider(settings.rentMin, 0f, 10f));
            listing.Label($"佩戴数码设备折扣: {Mathf.RoundToInt((1f - settings.deviceRentDiscount) * 100f)}%");
            settings.deviceRentDiscount = 1f - listing.Slider(1f - settings.deviceRentDiscount, 0f, 0.5f);
            listing.Gap(10f);

            // ============ 餐费 ============
            listing.Label("<b>用餐扣费</b>");
            listing.GapLine();
            listing.CheckboxLabeled("启用餐费扣除", ref settings.enableMealFee);
            listing.Label($"奢华餐: {settings.mealLavish} @银/顿");
            settings.mealLavish = Mathf.RoundToInt(listing.Slider(settings.mealLavish, 0f, 30f));
            listing.Label($"精致餐: {settings.mealFine} @银/顿");
            settings.mealFine = Mathf.RoundToInt(listing.Slider(settings.mealFine, 0f, 25f));
            listing.Label($"简单餐: {settings.mealSimple} @银/顿");
            settings.mealSimple = Mathf.RoundToInt(listing.Slider(settings.mealSimple, 0f, 20f));
            listing.Label($"难吃餐 (营养膏等): {settings.mealAwful} @银/顿");
            settings.mealAwful = Mathf.RoundToInt(listing.Slider(settings.mealAwful, 0f, 15f));
            listing.Label($"生食/浆果: {settings.mealRaw} @银/顿");
            settings.mealRaw = Mathf.RoundToInt(listing.Slider(settings.mealRaw, 0f, 10f));
            listing.Gap(10f);

            // ============ 医疗就医 (RimPay 医保) ============
            listing.Label("<b>医疗就医 (RimPay 医保)</b>");
            listing.GapLine();
            listing.CheckboxLabeled("启用诊疗扣费", ref settings.enableMedicalFee,
                "医生治疗时收取诊疗费：病人钱包优先支付，不足部分由数字国库医保报销。诊疗收入回流国库。");
            listing.Label($"单次诊疗费: {settings.medicalBaseFee} @银");
            settings.medicalBaseFee = Mathf.RoundToInt(listing.Slider(settings.medicalBaseFee, 0f, 100f));
            listing.Gap(10f);

            // ============ 余额宝利息 ============
            listing.Label("<b>余额宝利息</b>");
            listing.GapLine();
            listing.CheckboxLabeled("启用存款利息", ref settings.enableInterest);
            listing.Label($"计息门槛 (余额超过才计息): {settings.interestMinBalance} @银");
            settings.interestMinBalance = Mathf.RoundToInt(listing.Slider(settings.interestMinBalance, 0f, 500f));
            listing.Label($"每日利率: {settings.interestRate * 100f:F2}%");
            settings.interestRate = listing.Slider(settings.interestRate, 0f, 0.02f);
            listing.Label($"利息好心情概率: {settings.interestMoodChance * 100f:F0}%");
            settings.interestMoodChance = listing.Slider(settings.interestMoodChance, 0f, 1f);
            listing.Gap(10f);

            // ============ 借贷 ============
            listing.Label("<b>借贷中心</b>");
            listing.GapLine();
            listing.CheckboxLabeled("启用借贷中心", ref settings.enableLoan);
            listing.Label($"基准放贷利率: {settings.loanRateBase * 100f:F0}%/季");
            settings.loanRateBase = listing.Slider(settings.loanRateBase, 0f, 0.2f);
            listing.Label($"友好派系利率: {settings.loanRateFriendly * 100f:F0}%/季");
            settings.loanRateFriendly = listing.Slider(settings.loanRateFriendly, 0f, 0.2f);
            listing.Label($"冷漠派系利率: {settings.loanRateCold * 100f:F0}%/季");
            settings.loanRateCold = listing.Slider(settings.loanRateCold, 0f, 0.2f);
            listing.Label($"基准借款利率: {settings.borrowRateBase * 100f:F0}%/季");
            settings.borrowRateBase = listing.Slider(settings.borrowRateBase, 0f, 0.25f);
            listing.Label($"借贷周期: {settings.loanDefaultPeriod} 天");
            settings.loanDefaultPeriod = Mathf.RoundToInt(listing.Slider(settings.loanDefaultPeriod, 1f, 60f));
            listing.Label($"借贷默认金额: {settings.loanDefaultAmount} @银");
            settings.loanDefaultAmount = Mathf.RoundToInt(listing.Slider(settings.loanDefaultAmount, 100f, 5000f));
            listing.Label($"逾期坏账好感惩罚: {settings.loanBadDebtGoodwill}");
            settings.loanBadDebtGoodwill = Mathf.RoundToInt(listing.Slider(settings.loanBadDebtGoodwill, -50f, 0f));
            listing.Gap(10f);

            // ============ 股市 ============
            listing.Label("<b>证券交易</b>");
            listing.GapLine();
            listing.CheckboxLabeled("启用证券交易", ref settings.enableStocks);
            listing.Label($"股价更新间隔: {settings.stockPriceUpdateTicks / 60000f:F1} 天");
            settings.stockPriceUpdateTicks = Mathf.RoundToInt(listing.Slider(settings.stockPriceUpdateTicks / 60000f, 0.5f, 3f) * 60000f);
            listing.Label($"$DSF 杠杆基金波动率: {settings.stocksFundVolatility * 100f:F0}%");
            settings.stocksFundVolatility = listing.Slider(settings.stocksFundVolatility, 0.05f, 0.6f);
            listing.Label($"K线保留天数: {settings.stockHistoryDays} 天");
            settings.stockHistoryDays = Mathf.RoundToInt(listing.Slider(settings.stockHistoryDays, 5f, 30f));
            listing.Gap(10f);

            // ============ 小人炒股 ============
            listing.Label("<b>小人自动炒股</b>");
            listing.GapLine();
            listing.CheckboxLabeled("启用小人自动炒股", ref settings.enablePawnTrading);
            listing.Label($"炒股最低余额门槛: {settings.pawnTradeMinBalance} @银");
            settings.pawnTradeMinBalance = Mathf.RoundToInt(listing.Slider(settings.pawnTradeMinBalance, 0f, 1000f));
            listing.Label($"投入余额比例: {settings.pawnTradeInvestRatio * 100f:F0}%");
            settings.pawnTradeInvestRatio = listing.Slider(settings.pawnTradeInvestRatio, 0f, 0.5f);
            listing.Label($"单次投入上限: {settings.pawnTradeInvestCap} @银");
            settings.pawnTradeInvestCap = Mathf.RoundToInt(listing.Slider(settings.pawnTradeInvestCap, 50f, 500f));
            listing.Label($"盈亏触发 RimTalk 阈值: {settings.pawnTradeRimTalkThreshold} @银");
            settings.pawnTradeRimTalkThreshold = Mathf.RoundToInt(listing.Slider(settings.pawnTradeRimTalkThreshold, 10f, 100f));
            listing.Label($"基础命中率: {settings.pawnTradeBaseHitChance * 100f:F0}%");
            settings.pawnTradeBaseHitChance = listing.Slider(settings.pawnTradeBaseHitChance, 0.1f, 0.9f);
            listing.Label($"每点智力命中加成: {settings.pawnTradeHitPerSkill * 100f:F0}%");
            settings.pawnTradeHitPerSkill = listing.Slider(settings.pawnTradeHitPerSkill, 0f, 0.1f);
            listing.Gap(10f);

            // ============ 搜刮 ============
            listing.Label("<b>敌人搜刮</b>");
            listing.GapLine();
            listing.CheckboxLabeled("启用敌人 RimPay 搜刮", ref settings.enablePawnLoot);
            listing.Label($"击杀者分成: {settings.lootKillerShare * 100f:F0}% (其余入国库)");
            settings.lootKillerShare = listing.Slider(settings.lootKillerShare, 0f, 1f);
            listing.Gap(10f);

            // ============ 钱包流水 ============
            listing.Label("<b>钱包流水</b>");
            listing.GapLine();
            listing.Label($"每人保留流水条数: {settings.maxTransactionPerPawn} 条");
            settings.maxTransactionPerPawn = Mathf.RoundToInt(listing.Slider(settings.maxTransactionPerPawn, 5f, 50f));
            listing.Gap(10f);

            // ============ 财富托管 (Wealth Escrow) ============
            listing.Label("<b>财富托管 (Wealth Escrow)</b>");
            listing.GapLine();
            listing.Label("<i>RimPay 提供数字资产托管：被托管的资产不参与袭击威胁点计算，殖民地真实财富不变。数字国库中的白银已物理隐藏，无需托管。</i>");
            listing.CheckboxLabeled("建筑财富托管", ref settings.hideBuildingWealth,
                "殖民地建筑（含地板）财富托管为国家储备，不再计入袭击威胁点。");
            listing.CheckboxLabeled("随身装备托管", ref settings.hidePawnEquipmentWealth,
                "小人/动物随身携带的武器、衣物、驮载物品不计入袭击威胁点；不影响小人本体与机械体价值。");
            listing.Gap(10f);

            // ============ RimSimManagementFramework边缘模拟经营框架联动 ============
            listing.Label("<b>RimSimManagementFramework边缘模拟经营框架联动 (Shop Integration)</b>");
            listing.GapLine();
            listing.Label("<i>需安装 RimSimManagementFramework边缘模拟经营框架。未安装时以下功能自动休眠（与模组加载顺序无关）。</i>");
            listing.CheckboxLabeled("启用边缘模拟经营框架商店联动", ref settings.enableRimSimIntegration,
                "结账折扣 + 店铺收入自动入数字国库。需安装 RimSimManagementFramework边缘模拟经营框架。");
            listing.Label($"店铺收入入国库比例: {settings.rimSimTreasuryRatio * 100f:F0}%");
            settings.rimSimTreasuryRatio = listing.Slider(settings.rimSimTreasuryRatio, 0f, 1f);
            listing.Label($"数码生态折扣 (佩戴 RimDigitalLife 数码设备): 顾客实付 -{settings.rimSimDeviceDiscount * 100f:F0}%");
            settings.rimSimDeviceDiscount = listing.Slider(settings.rimSimDeviceDiscount, 0f, 0.3f);
            listing.Label($"量子网络档折扣 (有殖民者订阅量子网络): 顾客实付 -{settings.rimSimQuantumDiscount * 100f:F0}%");
            settings.rimSimQuantumDiscount = listing.Slider(settings.rimSimQuantumDiscount, 0f, 0.3f);
            listing.Gap(10f);

            listing.Gap(6f);
            listing.Label("<b>当前模组版本: v0.7.01</b>");

            listing.End();
            Widgets.EndScrollView();
        }

        // ============ AI 设置页（AI 控制/供应商/测试连接/状态面板/日报/JSON模板） ============
        private void DrawAIPage(Rect inRect)
        {
            float viewHeight = 3000f;
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, viewHeight);
            Widgets.BeginScrollView(inRect, ref aiScroll, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            // ============ AI 经济智能（多供应商配置） ============
            listing.Label("<b>AI 经济控制 (RimPay Economy AI)</b>");
            listing.GapLine();

            // 推荐使用 Player2 的话术（显著位置）
            Text.Font = GameFont.Tiny;
            listing.Label("<color=#58d3f7><b>🎮 建议注册并使用 Player2 软件，免费体验全部 AI 功能！</b></color>");
            listing.Label("<color=#58d3f7>Player2 为本地免费桌面程序，无需 API Key；若已安装且正在运行，勾选 Player2 配置即可直接使用。</color>");
            listing.Label("<color=#58d3f7>勾选的配置会按列表顺序从上到下依次尝试，失败或额度不足时自动切换下一个。</color>");
            Text.Font = GameFont.Small;
            listing.Gap(4f);

            listing.Label("<i>开启后，每日经济变量由 AI 随机控制；全部配置失败时自动回退到内置随机。</i>");
            listing.CheckboxLabeled("启用 AI 控制经济", ref settings.enableEconomyAI);
            listing.Gap(4f);

            listing.Label($"AI 刷新间隔: {settings.aiRefreshIntervalHours} 小时 (越短越活跃，注意 API 用量)");
            settings.aiRefreshIntervalHours = Mathf.RoundToInt(listing.Slider(settings.aiRefreshIntervalHours, 2f, 72f));
            listing.Gap(4f);

            // 供应商配置列表
            listing.Label("<b>供应商配置 (勾选启用，顺序=尝试顺序):</b>");
            listing.Gap(3f);
            DrawAIConfigList(listing);

            listing.Gap(10f);

            // ============ AI 实时状态面板（验证 AI 是否掌控经济） ============
            listing.Label("<b>AI 经济实时状态 (Live Status)</b>");
            listing.GapLine();
            listing.Label("<i>实时显示 AI 当前生成并生效的经济数值，用于验证 AI 是否真正掌控经济。</i>");
            listing.Gap(4f);

            DrawStatusRow(listing, "AI 控制开关", settings.enableEconomyAI ? "已开启" : "已关闭");
            DrawStatusRow(listing, "工资倍率 (salaryMultiplier)", $"{RimPayAIProvider.salaryMultiplier:F2} × 每日工资");
            DrawStatusRow(listing, "房租倍率 (rentMultiplier)", $"{RimPayAIProvider.rentMultiplier:F2} × 每日房租");
            DrawStatusRow(listing, "余额宝利率增量 (interestRateDelta)", $"{RimPayAIProvider.interestRateDelta:+0.00;-0.00;0.00}% 加到基础利率");
            DrawStatusRow(listing, "借贷利率倍率 (loanRateMultiplier)", $"{RimPayAIProvider.loanRateMultiplier:F2} × 借贷/放贷利率");
            DrawStatusRow(listing, "餐费倍率 (mealFeeMultiplier)", $"{RimPayAIProvider.mealFeeMultiplier:F2} × 每顿餐费");
            DrawStatusRow(listing, "搜刮分成增量 (lootShareDelta)", $"{RimPayAIProvider.lootShareDelta:+0.00;-0.00;0.00} 加进击杀者分成");
            DrawStatusRow(listing, "交易价格倍率 (tradePriceMultiplier)", $"{RimPayAIProvider.tradePriceMultiplier:F2} × 买卖价格");
            string toneText = RimPayAIProvider.marketTone == "bull" ? "bull (牛市：股市普涨)" :
                              RimPayAIProvider.marketTone == "bear" ? "bear (熊市：股市普跌)" :
                              "neutral (中性：股市随机)";
            DrawStatusRow(listing, "市场情绪 (marketTone)", toneText);
            GameComponent_RimPay compStatus = Current.Game?.GetComponent<GameComponent_RimPay>();
            if (compStatus != null)
            {
                string macroText = string.IsNullOrEmpty(compStatus.ActiveMacroEvent)
                    ? "(无)"
                    : $"{compStatus.MacroEventLabel} (剩余 {compStatus.MacroEventDaysLeft} 天)";
                DrawStatusRow(listing, "宏观事件", macroText);
            }
            DrawStatusRow(listing, "AI 财经头条 (eventText)", string.IsNullOrEmpty(RimPayAIProvider.eventText) ? "(暂无，需 AI 成功返回)" : RimPayAIProvider.eventText);
            Map statusMap = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            string todayStr = statusMap != null ? GenLocalDate.DayOfYear(statusMap).ToString() : "-";
            DrawStatusRow(listing, "最近请求日", $"{RimPayAIProvider.LastRequestDay} (今日: {todayStr})");
            DrawStatusRow(listing, "是否有 AI 结果", RimPayAIProvider.hasAIResult ? "是" : "否");
            DrawStatusRow(listing, "连续失败次数", RimPayAIProvider.ConsecutiveFailures.ToString());
            DrawStatusRow(listing, "冷却中", RimPayAIProvider.IsCooldownActive ? "是" : "否");
            DrawStatusRow(listing, "请求进行中", RimPayAIProvider.IsRequestInFlight ? "是" : "否");
            if (!string.IsNullOrEmpty(RimPayAIProvider.LastHttpErrorDetail))
                DrawStatusRow(listing, "最近错误", RimPayAIProvider.LastHttpErrorDetail);

            listing.Gap(4f);
            Rect forceRow = listing.GetRect(30f);
            if (Widgets.ButtonText(forceRow, "立即请求一次 AI 经济数据 (Force Request)"))
            {
                RimPayAIProvider.ForceRequestNow();
            }
            listing.Gap(6f);
            listing.Label("<i>点击上方按钮可绕过「每日一次」限制，立即让 AI 生成一组新数值并显示在上面，用于验证连接与 AI 掌控。</i>");
            listing.Gap(10f);

            // ============ AI 结果归档（最近 10 次） ============
            listing.Label("<b>AI 结果归档 (最近 10 次)</b>");
            listing.GapLine();
            var archive = RimPayAIProvider.HistoryArchive;
            if (archive == null || archive.Count == 0)
            {
                listing.Label("<i>(暂无归档，AI 成功返回后自动记录)</i>");
            }
            else
            {
                Text.Font = GameFont.Tiny;
                foreach (var snap in archive)
                {
                    string line = $"{snap.dayLabel} | 薪×{snap.salaryMult:F2} 租×{snap.rentMult:F2} 贷×{snap.loanMult:F2} 餐×{snap.mealMult:F2} 利{snap.interestDelta:+0.00;-0.00;0.00} 价×{snap.tradeMult:F2} 搜{snap.lootDelta:+0.00;-0.00;0.00} | {snap.tone} | {snap.macro}";
                    if (!string.IsNullOrEmpty(snap.evt)) line += $"\n  头条: {snap.evt}";
                    listing.Label(line);
                    listing.Gap(2f);
                }
                Text.Font = GameFont.Small;
            }
            listing.Gap(10f);

            // ============ 每日财经日报信封 ============
            listing.Label("<b>每日财经日报 (信封提醒)</b>");
            listing.GapLine();
            listing.CheckboxLabeled("开启每日财经日报信封", ref settings.showDailyEconomyLetter,
                "若关闭，则不发送信封，仅保留结算消息。");
            listing.CheckboxLabeled("AI 财经播报给 RimTalk 小人对话", ref settings.enableAiRimTalkBroadcast,
                "每日 AI 生成财经事件后，让一名小人通过 RimTalk 聊起这个话题。需已安装并启用 RimTalk。");
            listing.Gap(10f);

            // ============ AI 经济 JSON 模板（提示词编辑器样式，同 RimTalk/RimTuber） ============
            listing.Label("<b>AI 经济 JSON 模板 (JSON Template)</b>");
            listing.GapLine();
            listing.Label("<i>该 JSON 指示 AI 每日返回的经济变量，可自由编辑；改乱后可一键还原默认。</i>");
            listing.Gap(4f);

            Rect jsonRect = listing.GetRect(240f);
            Widgets.DrawBoxSolid(jsonRect, new Color(0.1f, 0.1f, 0.1f, 0.5f));
            jsonBuffer = Widgets.TextArea(jsonRect, jsonBuffer);

            Rect jsonBtnRow = listing.GetRect(30f);
            Rect saveJsonBtn = new Rect(jsonBtnRow.x, jsonBtnRow.y, jsonBtnRow.width / 2f - 4f, jsonBtnRow.height);
            Rect resetJsonBtn = new Rect(jsonBtnRow.x + jsonBtnRow.width / 2f + 4f, jsonBtnRow.y, jsonBtnRow.width / 2f - 4f, jsonBtnRow.height);

            if (Widgets.ButtonText(saveJsonBtn, "保存 JSON 模板 (Save)"))
            {
                settings.economyJsonTemplate = jsonBuffer;
                Messages.Message("[RimPay] JSON 模板已保存。", MessageTypeDefOf.NeutralEvent, false);
            }
            if (Widgets.ButtonText(resetJsonBtn, "还原默认 JSON (Reset)"))
            {
                settings.RestoreDefaultJson();
                jsonBuffer = settings.economyJsonTemplate;
                Messages.Message("[RimPay] 已还原默认 JSON 模板。", MessageTypeDefOf.NeutralEvent, false);
            }

            listing.End();
            Widgets.EndScrollView();
        }

        // 状态面板的一行（键值对）
        private void DrawStatusRow(Listing_Standard listing, string key, string value)
        {
            Rect row = listing.GetRect(20f);
            Widgets.Label(new Rect(row.x, row.y, row.width * 0.42f, row.height), key);
            Widgets.Label(new Rect(row.x + row.width * 0.42f, row.y, row.width * 0.58f, row.height), value);
        }

        // 测试连接：在后台线程跑，避免卡设置界面
        private void StartTestConnection(RimPayProviderConfig cfg = null)
        {
            if (testInProgress) return;
            testInProgress = true;
            testResultText = "正在测试连接... (Testing...)";
            testConfigOverride = cfg;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    string result = RimPayAIProvider.TestConnection(testConfigOverride);
                    testResultText = result;
                }
                catch (Exception ex)
                {
                    testResultText = "[FAIL] " + ex.Message;
                }
                finally
                {
                    testInProgress = false;
                }
            });
        }

        // ============ 供应商配置列表（勾选/排序/删除/添加 + 选中编辑） ============
        private void DrawAIConfigList(Listing_Standard listing)
        {
            var configs = settings.apiConfigs;

            for (int i = 0; i < configs.Count; i++)
            {
                var cfg = configs[i];
                Rect row = listing.GetRect(28f);
                bool isSelected = (selectedConfigIdx == i);

                Color bgColor = isSelected ? new Color(0.3f, 0.3f, 0.5f, 0.3f) : new Color(0.2f, 0.2f, 0.2f, 0.1f);
                Widgets.DrawBoxSolid(row, bgColor);

                float x = row.x;
                float y = row.y;
                float h = row.height;

                Rect checkRect = new Rect(x, y + 2f, 24f, h - 4f);
                bool enabled = cfg.enabled;
                Widgets.Checkbox(checkRect.x, checkRect.y, ref enabled, 20f);
                cfg.enabled = enabled;
                x += 28f;

                string displayLabel = cfg.DisplayLabel;
                if (!cfg.enabled) displayLabel = "[OFF] " + displayLabel;
                Rect labelRect = new Rect(x, y, row.width - 28f - 30f - 30f - 30f - 28f, h);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, displayLabel);
                Text.Anchor = TextAnchor.UpperLeft;
                x = labelRect.xMax + 4f;

                Rect upRect = new Rect(x, y + 2f, 28f, h - 4f);
                if (i > 0 && Widgets.ButtonText(upRect, "^"))
                {
                    var tmp = configs[i - 1];
                    configs[i - 1] = configs[i];
                    configs[i] = tmp;
                    if (selectedConfigIdx == i) selectedConfigIdx = i - 1;
                    else if (selectedConfigIdx == i - 1) selectedConfigIdx = i;
                }
                x += 30f;

                Rect downRect = new Rect(x, y + 2f, 28f, h - 4f);
                if (i < configs.Count - 1 && Widgets.ButtonText(downRect, "v"))
                {
                    var tmp = configs[i + 1];
                    configs[i + 1] = configs[i];
                    configs[i] = tmp;
                    if (selectedConfigIdx == i) selectedConfigIdx = i + 1;
                    else if (selectedConfigIdx == i + 1) selectedConfigIdx = i;
                }
                x += 30f;

                Rect delRect = new Rect(x, y + 2f, 28f, h - 4f);
                if (Widgets.ButtonText(delRect, "X"))
                {
                    configs.RemoveAt(i);
                    if (selectedConfigIdx >= configs.Count) selectedConfigIdx = -1;
                    if (selectedConfigIdx == i) selectedConfigIdx = -1;
                    break;
                }

                if (Widgets.ButtonInvisible(row))
                {
                    selectedConfigIdx = isSelected ? -1 : i;
                }
            }

            Rect addRow = listing.GetRect(30f);
            Rect addBtn = new Rect(addRow.x, addRow.y, addRow.width, addRow.height);
            if (Widgets.ButtonText(addBtn, "添加供应商配置 (+)"))
            {
                var options = new List<FloatMenuOption>();
                foreach (RimPayProvider provider in System.Enum.GetValues(typeof(RimPayProvider)))
                {
                    if (provider == RimPayProvider.None) continue;
                    string label = provider.GetLabel();
                    var opt = new FloatMenuOption(label, () =>
                    {
                        var newCfg = new RimPayProviderConfig(provider);
                        configs.Add(newCfg);
                        selectedConfigIdx = configs.Count - 1;
                    });
                    options.Add(opt);
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            if (selectedConfigIdx >= 0 && selectedConfigIdx < configs.Count)
            {
                DrawAIConfigEditor(listing, configs[selectedConfigIdx]);
            }
        }

        // ============ 选中配置的详情编辑器 ============
        private void DrawAIConfigEditor(Listing_Standard listing, RimPayProviderConfig cfg)
        {
            listing.GapLine();
            listing.Label("<b>配置详情:</b> " + cfg.DisplayLabel);

            Rect provRow = listing.GetRect(30f);
            Widgets.Label(new Rect(provRow.x, provRow.y, 100f, provRow.height), "供应商:");
            Rect provBtn = new Rect(provRow.x + 105f, provRow.y, provRow.width - 105f, provRow.height);
            if (Widgets.ButtonText(provBtn, cfg.provider.GetLabel()))
            {
                var options = new List<FloatMenuOption>();
                foreach (RimPayProvider provider in System.Enum.GetValues(typeof(RimPayProvider)))
                {
                    if (provider == RimPayProvider.None) continue;
                    string label = provider.GetLabel();
                    var opt = new FloatMenuOption(label, () =>
                    {
                        cfg.provider = provider;
                        if (string.IsNullOrEmpty(cfg.endpointUrl) || cfg.endpointUrl == cfg.provider.GetEndpointUrl())
                            cfg.endpointUrl = provider.GetEndpointUrl() ?? "";
                        if (string.IsNullOrEmpty(cfg.model) || cfg.model == cfg.provider.GetDefaultModel())
                            cfg.model = provider.GetDefaultModel() ?? "";
                    });
                    options.Add(opt);
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            if (cfg.provider == RimPayProvider.Player2)
            {
                listing.Label("Player2 本地服务：无需 API Key，安装并运行 Player2 即可。");
            }
            else if (cfg.provider == RimPayProvider.Local)
            {
                listing.Label("本地模型：未安装 Ollama 前会自动失败并切换下一个配置。");
            }

            if (cfg.provider.GetRequiresApiKey())
            {
                listing.Label("API Key:");
                cfg.apiKey = listing.TextEntry(cfg.apiKey ?? "");
            }

            listing.Label("端点 URL (留空则用供应商默认):");
            cfg.endpointUrl = listing.TextEntry(cfg.endpointUrl ?? "");

            listing.Label("模型名称 (留空则用供应商默认):");
            cfg.model = listing.TextEntry(cfg.model ?? "");

            listing.CheckboxLabeled("启用此配置", ref cfg.enabled);

            // ============ 测试连接按钮 ============
            listing.Gap(4f);
            Rect testBtnRect = listing.GetRect(30f);
            if (Widgets.ButtonText(testBtnRect, testInProgress ? "正在测试中... (Testing...)" : "测试连接 (Test Connection)"))
            {
                StartTestConnection(cfg);
            }

            if (!string.IsNullOrEmpty(testResultText) && testConfigOverride == cfg)
            {
                Color prev = GUI.color;
                bool success = testResultText.StartsWith("[OK]");
                GUI.color = success ? new Color(0.3f, 0.9f, 0.3f) : new Color(1f, 0.3f, 0.3f);
                Text.Font = GameFont.Tiny;
                foreach (string line in testResultText.Split('\n'))
                {
                    listing.Label(line);
                }
                Text.Font = GameFont.Small;
                GUI.color = prev;
            }
            listing.Gap(4f);
        }
    }
}