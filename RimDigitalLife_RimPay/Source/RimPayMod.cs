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
            return "Rim Digital Life - RimPay Expansion (v1.1.0)";
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

            if (Widgets.ButtonText(generalTab, "RP_Set_TabGeneral".Translate()))
            {
                currentPage = RimPaySettingsPage.General;
            }
            if (Widgets.ButtonText(aiTab, "RP_Set_TabAI".Translate()))
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
            listing.Label("<b>" + "RP_Set_Salary_Title".Translate() + "</b>");
            listing.GapLine();
            listing.CheckboxLabeled("RP_Set_Salary_Enable".Translate(), ref settings.enableSalary);
            listing.Label("RP_Set_Salary_Base".Translate(settings.baseSalary));
            settings.baseSalary = Mathf.RoundToInt(listing.Slider(settings.baseSalary, 0f, 100f));
            listing.Label("RP_Set_Salary_Child".Translate(settings.childSalary));
            settings.childSalary = Mathf.RoundToInt(listing.Slider(settings.childSalary, 0f, 50f));
            listing.Label("RP_Set_Salary_Slave".Translate(settings.slaveSalary));
            settings.slaveSalary = Mathf.RoundToInt(listing.Slider(settings.slaveSalary, 0f, 50f));
            listing.Label("RP_Set_Salary_Expert".Translate(settings.expertBonus));
            settings.expertBonus = Mathf.RoundToInt(listing.Slider(settings.expertBonus, 0f, 100f));
            listing.Label("RP_Set_Salary_Skilled".Translate(settings.skilledBonus));
            settings.skilledBonus = Mathf.RoundToInt(listing.Slider(settings.skilledBonus, 0f, 60f));
            listing.Label("RP_Set_Salary_Basic".Translate(settings.basicBonus));
            settings.basicBonus = Mathf.RoundToInt(listing.Slider(settings.basicBonus, 0f, 40f));
            listing.Gap(10f);

            // ============ 房租 ============
            listing.Label("<b>" + "RP_Set_Rent_Title".Translate() + "</b>");
            listing.GapLine();
            listing.CheckboxLabeled("RP_Set_Rent_Enable".Translate(), ref settings.enableRent);
            listing.Label("RP_Set_Rent_Luxury".Translate(settings.rentLuxury));
            settings.rentLuxury = Mathf.RoundToInt(listing.Slider(settings.rentLuxury, 0f, 50f));
            listing.Label("RP_Set_Rent_High".Translate(settings.rentHigh));
            settings.rentHigh = Mathf.RoundToInt(listing.Slider(settings.rentHigh, 0f, 40f));
            listing.Label("RP_Set_Rent_Mid".Translate(settings.rentMid));
            settings.rentMid = Mathf.RoundToInt(listing.Slider(settings.rentMid, 0f, 30f));
            listing.Label("RP_Set_Rent_Low".Translate(settings.rentLow));
            settings.rentLow = Mathf.RoundToInt(listing.Slider(settings.rentLow, 0f, 20f));
            listing.Label("RP_Set_Rent_Min".Translate(settings.rentMin));
            settings.rentMin = Mathf.RoundToInt(listing.Slider(settings.rentMin, 0f, 10f));
            listing.Label("RP_Set_Rent_DeviceDiscount".Translate(Mathf.RoundToInt((1f - settings.deviceRentDiscount) * 100f)));
            settings.deviceRentDiscount = 1f - listing.Slider(1f - settings.deviceRentDiscount, 0f, 0.5f);
            listing.Gap(10f);

            // ============ 餐费 ============
            listing.Label("<b>" + "RP_Set_Meal_Title".Translate() + "</b>");
            listing.GapLine();
            listing.CheckboxLabeled("RP_Set_Meal_Enable".Translate(), ref settings.enableMealFee);
            listing.Label("RP_Set_Meal_Lavish".Translate(settings.mealLavish));
            settings.mealLavish = Mathf.RoundToInt(listing.Slider(settings.mealLavish, 0f, 30f));
            listing.Label("RP_Set_Meal_Fine".Translate(settings.mealFine));
            settings.mealFine = Mathf.RoundToInt(listing.Slider(settings.mealFine, 0f, 25f));
            listing.Label("RP_Set_Meal_Simple".Translate(settings.mealSimple));
            settings.mealSimple = Mathf.RoundToInt(listing.Slider(settings.mealSimple, 0f, 20f));
            listing.Label("RP_Set_Meal_Awful".Translate(settings.mealAwful));
            settings.mealAwful = Mathf.RoundToInt(listing.Slider(settings.mealAwful, 0f, 15f));
            listing.Label("RP_Set_Meal_Raw".Translate(settings.mealRaw));
            settings.mealRaw = Mathf.RoundToInt(listing.Slider(settings.mealRaw, 0f, 10f));
            listing.Gap(10f);

            // ============ 医疗就医 (RimPay 医保) ============
            listing.Label("<b>" + "RP_Set_Medical_Title".Translate() + "</b>");
            listing.GapLine();
            listing.CheckboxLabeled("RP_Set_Medical_Enable".Translate(), ref settings.enableMedicalFee,
                "RP_Set_Medical_Desc".Translate());
            listing.Label("RP_Set_Medical_Fee".Translate(settings.medicalBaseFee));
            settings.medicalBaseFee = Mathf.RoundToInt(listing.Slider(settings.medicalBaseFee, 0f, 100f));
            listing.Gap(10f);

            // ============ 余额宝利息 ============
            listing.Label("<b>" + "RP_Set_Interest_Title".Translate() + "</b>");
            listing.GapLine();
            listing.CheckboxLabeled("RP_Set_Interest_Enable".Translate(), ref settings.enableInterest);
            listing.Label("RP_Set_Interest_MinBalance".Translate(settings.interestMinBalance));
            settings.interestMinBalance = Mathf.RoundToInt(listing.Slider(settings.interestMinBalance, 0f, 500f));
            listing.Label("RP_Set_Interest_Rate".Translate((settings.interestRate * 100f).ToString("F2")));
            settings.interestRate = listing.Slider(settings.interestRate, 0f, 0.02f);
            listing.Label("RP_Set_Interest_Mood".Translate((settings.interestMoodChance * 100f).ToString("F0")));
            settings.interestMoodChance = listing.Slider(settings.interestMoodChance, 0f, 1f);
            listing.Gap(10f);

            // ============ 借贷 ============
            listing.Label("<b>" + "RP_Set_Loan_Title".Translate() + "</b>");
            listing.GapLine();
            listing.CheckboxLabeled("RP_Set_Loan_Enable".Translate(), ref settings.enableLoan);
            listing.Label("RP_Set_Loan_RateBase".Translate((settings.loanRateBase * 100f).ToString("F0")));
            settings.loanRateBase = listing.Slider(settings.loanRateBase, 0f, 0.2f);
            listing.Label("RP_Set_Loan_RateFriendly".Translate((settings.loanRateFriendly * 100f).ToString("F0")));
            settings.loanRateFriendly = listing.Slider(settings.loanRateFriendly, 0f, 0.2f);
            listing.Label("RP_Set_Loan_RateCold".Translate((settings.loanRateCold * 100f).ToString("F0")));
            settings.loanRateCold = listing.Slider(settings.loanRateCold, 0f, 0.2f);
            listing.Label("RP_Set_Loan_BorrowBase".Translate((settings.borrowRateBase * 100f).ToString("F0")));
            settings.borrowRateBase = listing.Slider(settings.borrowRateBase, 0f, 0.25f);
            listing.Label("RP_Set_Loan_Period".Translate(settings.loanDefaultPeriod));
            settings.loanDefaultPeriod = Mathf.RoundToInt(listing.Slider(settings.loanDefaultPeriod, 1f, 60f));
            listing.Label("RP_Set_Loan_DefaultAmount".Translate(settings.loanDefaultAmount));
            settings.loanDefaultAmount = Mathf.RoundToInt(listing.Slider(settings.loanDefaultAmount, 100f, 5000f));
            listing.Label("RP_Set_Loan_BadDebt".Translate(settings.loanBadDebtGoodwill));
            settings.loanBadDebtGoodwill = Mathf.RoundToInt(listing.Slider(settings.loanBadDebtGoodwill, -50f, 0f));
            listing.Gap(10f);

            // ============ 股市 ============
            listing.Label("<b>" + "RP_Set_Stock_Title".Translate() + "</b>");
            listing.GapLine();
            listing.CheckboxLabeled("RP_Set_Stock_Enable".Translate(), ref settings.enableStocks);
            listing.Label("RP_Set_Stock_Interval".Translate((settings.stockPriceUpdateTicks / 60000f).ToString("F1")));
            settings.stockPriceUpdateTicks = Mathf.RoundToInt(listing.Slider(settings.stockPriceUpdateTicks / 60000f, 0.5f, 3f) * 60000f);
            listing.Label("RP_Set_Stock_FundVol".Translate((settings.stocksFundVolatility * 100f).ToString("F0")));
            settings.stocksFundVolatility = listing.Slider(settings.stocksFundVolatility, 0.05f, 0.6f);
            listing.Label("RP_Set_Stock_HistoryDays".Translate(settings.stockHistoryDays));
            settings.stockHistoryDays = Mathf.RoundToInt(listing.Slider(settings.stockHistoryDays, 5f, 30f));
            listing.Gap(10f);

            // ============ 小人炒股 ============
            listing.Label("<b>" + "RP_Set_PawnTrade_Title".Translate() + "</b>");
            listing.GapLine();
            listing.CheckboxLabeled("RP_Set_PawnTrade_Enable".Translate(), ref settings.enablePawnTrading);
            listing.Label("RP_Set_PawnTrade_MinBalance".Translate(settings.pawnTradeMinBalance));
            settings.pawnTradeMinBalance = Mathf.RoundToInt(listing.Slider(settings.pawnTradeMinBalance, 0f, 1000f));
            listing.Label("RP_Set_PawnTrade_Ratio".Translate((settings.pawnTradeInvestRatio * 100f).ToString("F0")));
            settings.pawnTradeInvestRatio = listing.Slider(settings.pawnTradeInvestRatio, 0f, 0.5f);
            listing.Label("RP_Set_PawnTrade_Cap".Translate(settings.pawnTradeInvestCap));
            settings.pawnTradeInvestCap = Mathf.RoundToInt(listing.Slider(settings.pawnTradeInvestCap, 50f, 500f));
            listing.Label("RP_Set_PawnTrade_RimTalk".Translate(settings.pawnTradeRimTalkThreshold));
            settings.pawnTradeRimTalkThreshold = Mathf.RoundToInt(listing.Slider(settings.pawnTradeRimTalkThreshold, 10f, 100f));
            listing.Label("RP_Set_PawnTrade_HitChance".Translate((settings.pawnTradeBaseHitChance * 100f).ToString("F0")));
            settings.pawnTradeBaseHitChance = listing.Slider(settings.pawnTradeBaseHitChance, 0.1f, 0.9f);
            listing.Label("RP_Set_PawnTrade_HitPerSkill".Translate((settings.pawnTradeHitPerSkill * 100f).ToString("F0")));
            settings.pawnTradeHitPerSkill = listing.Slider(settings.pawnTradeHitPerSkill, 0f, 0.1f);
            listing.Gap(10f);

            // ============ 搜刮 ============
            listing.Label("<b>" + "RP_Set_Loot_Title".Translate() + "</b>");
            listing.GapLine();
            listing.CheckboxLabeled("RP_Set_Loot_Enable".Translate(), ref settings.enablePawnLoot);
            listing.Label("RP_Set_Loot_KillerShare".Translate((settings.lootKillerShare * 100f).ToString("F0")));
            settings.lootKillerShare = listing.Slider(settings.lootKillerShare, 0f, 1f);
            listing.Gap(10f);

            // ============ 钱包流水 ============
            listing.Label("<b>" + "RP_Set_WalletFlow_Title".Translate() + "</b>");
            listing.GapLine();
            listing.Label("RP_Set_WalletFlow_Max".Translate(settings.maxTransactionPerPawn));
            settings.maxTransactionPerPawn = Mathf.RoundToInt(listing.Slider(settings.maxTransactionPerPawn, 5f, 50f));
            listing.Gap(10f);

            // ============ 财富托管 (Wealth Escrow) ============
            listing.Label("<b>" + "RP_Set_Escrow_Title".Translate() + "</b>");
            listing.GapLine();
            listing.Label("<i>" + "RP_Set_Escrow_Desc".Translate() + "</i>");
            listing.CheckboxLabeled("RP_Set_Escrow_Building".Translate(), ref settings.hideBuildingWealth,
                "RP_Set_Escrow_BuildingDesc".Translate());
            listing.CheckboxLabeled("RP_Set_Escrow_Equipment".Translate(), ref settings.hidePawnEquipmentWealth,
                "RP_Set_Escrow_EquipmentDesc".Translate());
            listing.Gap(10f);

            // ============ RimSimManagementFramework边缘模拟经营框架联动 ============
            listing.Label("<b>" + "RP_Set_RimSim_Title".Translate() + "</b>");
            listing.GapLine();
            listing.Label("<i>" + "RP_Set_RimSim_Desc".Translate() + "</i>");
            listing.CheckboxLabeled("RP_Set_RimSim_Enable".Translate(), ref settings.enableRimSimIntegration,
                "RP_Set_RimSim_EnableDesc".Translate());
            listing.Label("RP_Set_RimSim_TreasuryRatio".Translate((settings.rimSimTreasuryRatio * 100f).ToString("F0")));
            settings.rimSimTreasuryRatio = listing.Slider(settings.rimSimTreasuryRatio, 0f, 1f);
            listing.Label("RP_Set_RimSim_DeviceDiscount".Translate((settings.rimSimDeviceDiscount * 100f).ToString("F0")));
            settings.rimSimDeviceDiscount = listing.Slider(settings.rimSimDeviceDiscount, 0f, 0.3f);
            listing.Label("RP_Set_RimSim_QuantumDiscount".Translate((settings.rimSimQuantumDiscount * 100f).ToString("F0")));
            settings.rimSimQuantumDiscount = listing.Slider(settings.rimSimQuantumDiscount, 0f, 0.3f);
            listing.Gap(10f);

            // ============ RimMarket 边缘市场联动 ============
            listing.Label("<b>" + "RP_Set_RimMarket_Title".Translate() + "</b>");
            listing.GapLine();
            listing.Label("<i>" + "RP_Set_RimMarket_Desc".Translate() + "</i>");
            listing.CheckboxLabeled("RP_Set_RimMarket_Enable".Translate(), ref settings.enableRimMarketIntegration,
                "RP_Set_RimMarket_EnableDesc".Translate());
            listing.Gap(10f);

            // ============ NPC 数字钱包 ============
            listing.Label("<b>" + "RP_Set_NpcWallet_Title".Translate() + "</b>");
            listing.GapLine();
            listing.Label("<i>" + "RP_Set_NpcWallet_Desc".Translate() + "</i>");
            listing.CheckboxLabeled("RP_Set_NpcWallet_Enable".Translate(), ref settings.enableNpcWallets,
                "RP_Set_NpcWallet_EnableDesc".Translate());
            listing.Label("RP_Set_NpcWallet_Multiplier".Translate(settings.npcWalletMultiplier.ToString("F1")));
            settings.npcWalletMultiplier = listing.Slider(settings.npcWalletMultiplier, 0.1f, 3f);
            listing.Gap(10f);

            // ============ Bunnygirl Race 赌场联动 ============
            listing.Label("<b>" + "RP_Set_Bunny_Title".Translate() + "</b>");
            listing.GapLine();
            listing.Label("<i>" + "RP_Set_Bunny_Desc".Translate() + "</i>");
            listing.CheckboxLabeled("RP_Set_Bunny_Enable".Translate(), ref settings.enableBunnygirlIntegration,
                "RP_Set_Bunny_EnableDesc".Translate());
            listing.Gap(10f);

            // ============ Hospitality 访客联动 ============
            listing.Label("<b>" + "RP_Set_Hospitality_Title".Translate() + "</b>");
            listing.GapLine();
            listing.Label("<i>" + "RP_Set_Hospitality_Desc".Translate() + "</i>");
            listing.CheckboxLabeled("RP_Set_Hospitality_Enable".Translate(), ref settings.enableHospitalityIntegration,
                "RP_Set_Hospitality_EnableDesc".Translate());
            listing.Gap(10f);

            // ============ RimStock 边缘股市联动 ============
            listing.Label("<b>" + "RP_Set_RimStock_Title".Translate() + "</b>");
            listing.GapLine();
            listing.Label("<i>" + "RP_Set_RimStock_Desc".Translate() + "</i>");
            listing.CheckboxLabeled("RP_Set_RimStock_Enable".Translate(), ref settings.enableRimStockIntegration,
                "RP_Set_RimStock_EnableDesc".Translate());
            listing.Gap(10f);

            listing.Gap(6f);
            listing.Label("<b>" + "RP_Set_Version".Translate("v1.1.0") + "</b>");

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
            listing.Label("<b>" + "RP_AI_Title".Translate() + "</b>");
            listing.GapLine();

            // 推荐使用 Player2 的话术（显著位置）
            Text.Font = GameFont.Tiny;
            listing.Label("<color=#58d3f7><b>" + "RP_AI_P2Promo1".Translate() + "</b></color>");
            listing.Label("<color=#58d3f7>" + "RP_AI_P2Promo2".Translate() + "</color>");
            listing.Label("<color=#58d3f7>" + "RP_AI_P2Promo3".Translate() + "</color>");
            Text.Font = GameFont.Small;
            listing.Gap(4f);

            listing.Label("<i>" + "RP_AI_EnableDesc".Translate() + "</i>");
            listing.CheckboxLabeled("RP_AI_Enable".Translate(), ref settings.enableEconomyAI);
            listing.Gap(4f);

            listing.Label("RP_AI_Interval".Translate(settings.aiRefreshIntervalHours));
            settings.aiRefreshIntervalHours = Mathf.RoundToInt(listing.Slider(settings.aiRefreshIntervalHours, 2f, 72f));
            listing.Gap(4f);

            // 供应商配置列表
            listing.Label("<b>" + "RP_AI_Providers".Translate() + "</b>");
            listing.Gap(3f);
            DrawAIConfigList(listing);

            listing.Gap(10f);

            // ============ AI 实时状态面板（验证 AI 是否掌控经济） ============
            listing.Label("<b>" + "RP_AI_LiveTitle".Translate() + "</b>");
            listing.GapLine();
            listing.Label("<i>" + "RP_AI_LiveDesc".Translate() + "</i>");
            listing.Gap(4f);

            DrawStatusRow(listing, "RP_AI_S_Toggle".Translate(), settings.enableEconomyAI ? "RP_Common_On".Translate() : "RP_Common_Off".Translate());
            DrawStatusRow(listing, "RP_AI_S_SalaryMult".Translate(), "RP_AI_S_SalaryMultVal".Translate(RimPayAIProvider.salaryMultiplier.ToString("F2")));
            DrawStatusRow(listing, "RP_AI_S_RentMult".Translate(), "RP_AI_S_RentMultVal".Translate(RimPayAIProvider.rentMultiplier.ToString("F2")));
            DrawStatusRow(listing, "RP_AI_S_InterestDelta".Translate(), "RP_AI_S_InterestDeltaVal".Translate(RimPayAIProvider.interestRateDelta.ToString("+0.00;-0.00;0.00")));
            DrawStatusRow(listing, "RP_AI_S_LoanMult".Translate(), "RP_AI_S_LoanMultVal".Translate(RimPayAIProvider.loanRateMultiplier.ToString("F2")));
            DrawStatusRow(listing, "RP_AI_S_MealMult".Translate(), "RP_AI_S_MealMultVal".Translate(RimPayAIProvider.mealFeeMultiplier.ToString("F2")));
            DrawStatusRow(listing, "RP_AI_S_LootDelta".Translate(), "RP_AI_S_LootDeltaVal".Translate(RimPayAIProvider.lootShareDelta.ToString("+0.00;-0.00;0.00")));
            DrawStatusRow(listing, "RP_AI_S_TradeMult".Translate(), "RP_AI_S_TradeMultVal".Translate(RimPayAIProvider.tradePriceMultiplier.ToString("F2")));
            string toneText = RimPayAIProvider.marketTone == "bull" ? (string)"RP_Tone_Bull".Translate() :
                              RimPayAIProvider.marketTone == "bear" ? (string)"RP_Tone_Bear".Translate() :
                              (string)"RP_Tone_Neutral".Translate();
            DrawStatusRow(listing, "RP_AI_S_Tone".Translate(), toneText);
            GameComponent_RimPay compStatus = Current.Game?.GetComponent<GameComponent_RimPay>();
            if (compStatus != null)
            {
                string macroText = string.IsNullOrEmpty(compStatus.ActiveMacroEvent)
                    ? (string)"RP_Common_None".Translate()
                    : (string)"RP_AI_S_MacroVal".Translate(compStatus.MacroEventLabel, compStatus.MacroEventDaysLeft);
                DrawStatusRow(listing, "RP_AI_S_Macro".Translate(), macroText);
            }
            DrawStatusRow(listing, "RP_AI_S_Event".Translate(), string.IsNullOrEmpty(RimPayAIProvider.eventText) ? (string)"RP_Common_PendingAI".Translate() : RimPayAIProvider.eventText);
            Map statusMap = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            string todayStr = statusMap != null ? GenLocalDate.DayOfYear(statusMap).ToString() : "-";
            DrawStatusRow(listing, "RP_AI_S_LastDay".Translate(), "RP_AI_S_LastDayFmt".Translate(RimPayAIProvider.LastRequestDay, todayStr));
            DrawStatusRow(listing, "RP_AI_S_HasResult".Translate(), RimPayAIProvider.hasAIResult ? "RP_Common_Yes".Translate() : "RP_Common_No".Translate());
            DrawStatusRow(listing, "RP_AI_S_Fails".Translate(), RimPayAIProvider.ConsecutiveFailures.ToString());
            DrawStatusRow(listing, "RP_Common_Cooldown".Translate(), RimPayAIProvider.IsCooldownActive ? "RP_Common_Yes".Translate() : "RP_Common_No".Translate());
            DrawStatusRow(listing, "RP_AI_S_InFlight".Translate(), RimPayAIProvider.IsRequestInFlight ? "RP_Common_Yes".Translate() : "RP_Common_No".Translate());
            if (!string.IsNullOrEmpty(RimPayAIProvider.LastHttpErrorDetail))
                DrawStatusRow(listing, "RP_AI_S_LastError".Translate(), RimPayAIProvider.LastHttpErrorDetail);

            listing.Gap(4f);
            Rect forceRow = listing.GetRect(30f);
            if (Widgets.ButtonText(forceRow, "RP_AI_ForceBtn".Translate()))
            {
                RimPayAIProvider.ForceRequestNow();
            }
            listing.Gap(6f);
            listing.Label("<i>" + "RP_AI_ForceDesc".Translate() + "</i>");
            listing.Gap(10f);

            // ============ AI 结果归档（最近 10 次） ============
            listing.Label("<b>" + "RP_AI_ArchiveTitle".Translate() + "</b>");
            listing.GapLine();
            var archive = RimPayAIProvider.HistoryArchive;
            if (archive == null || archive.Count == 0)
            {
                listing.Label("<i>" + "RP_AI_ArchiveEmpty".Translate() + "</i>");
            }
            else
            {
                Text.Font = GameFont.Tiny;
                foreach (var snap in archive)
                {
                    string line = "RP_AI_ArchiveLine".Translate(
                        snap.dayLabel,
                        snap.salaryMult.ToString("F2"),
                        snap.rentMult.ToString("F2"),
                        snap.loanMult.ToString("F2"),
                        snap.mealMult.ToString("F2"),
                        snap.interestDelta.ToString("+0.00;-0.00;0.00"),
                        snap.tradeMult.ToString("F2"),
                        snap.lootDelta.ToString("+0.00;-0.00;0.00"),
                        snap.tone,
                        snap.macro);
                    if (!string.IsNullOrEmpty(snap.evt)) line += "\n  " + "RP_AI_ArchiveHeadline".Translate(snap.evt);
                    listing.Label(line);
                    listing.Gap(2f);
                }
                Text.Font = GameFont.Small;
            }
            listing.Gap(10f);

            // ============ 每日财经日报信封 ============
            listing.Label("<b>" + "RP_AI_LetterTitle".Translate() + "</b>");
            listing.GapLine();
            listing.CheckboxLabeled("RP_AI_DailyLetter".Translate(), ref settings.showDailyEconomyLetter,
                "RP_AI_DailyLetterDesc".Translate());
            listing.CheckboxLabeled("RP_AI_RimTalkCast".Translate(), ref settings.enableAiRimTalkBroadcast,
                "RP_AI_RimTalkCastDesc".Translate());
            listing.Gap(10f);

            // ============ AI 经济 JSON 模板（提示词编辑器样式，同 RimTalk/RimTuber） ============
            listing.Label("<b>" + "RP_AI_JsonTitle".Translate() + "</b>");
            listing.GapLine();
            listing.Label("<i>" + "RP_AI_JsonDesc".Translate() + "</i>");
            listing.Gap(4f);

            Rect jsonRect = listing.GetRect(240f);
            Widgets.DrawBoxSolid(jsonRect, new Color(0.1f, 0.1f, 0.1f, 0.5f));
            jsonBuffer = Widgets.TextArea(jsonRect, jsonBuffer);

            Rect jsonBtnRow = listing.GetRect(30f);
            Rect saveJsonBtn = new Rect(jsonBtnRow.x, jsonBtnRow.y, jsonBtnRow.width / 2f - 4f, jsonBtnRow.height);
            Rect resetJsonBtn = new Rect(jsonBtnRow.x + jsonBtnRow.width / 2f + 4f, jsonBtnRow.y, jsonBtnRow.width / 2f - 4f, jsonBtnRow.height);

            if (Widgets.ButtonText(saveJsonBtn, "RP_AI_JsonSave".Translate()))
            {
                settings.economyJsonTemplate = jsonBuffer;
                Messages.Message("[RimPay] " + "RP_AI_JsonSaved".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }
            if (Widgets.ButtonText(resetJsonBtn, "RP_AI_JsonReset".Translate()))
            {
                settings.RestoreDefaultJson();
                jsonBuffer = settings.economyJsonTemplate;
                Messages.Message("[RimPay] " + "RP_AI_JsonResetDone".Translate(), MessageTypeDefOf.NeutralEvent, false);
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
            testResultText = "RP_AI_TestingConn".Translate();
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
            if (Widgets.ButtonText(addBtn, "RP_AI_AddConfig".Translate()))
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
            listing.Label("<b>" + "RP_AI_CfgDetail".Translate() + "</b> " + cfg.DisplayLabel);

            Rect provRow = listing.GetRect(30f);
            Widgets.Label(new Rect(provRow.x, provRow.y, 100f, provRow.height), "RP_AI_CfgProvider".Translate());
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
                listing.Label("RP_AI_CfgP2Hint".Translate());
            }
            else if (cfg.provider == RimPayProvider.Local)
            {
                listing.Label("RP_AI_CfgLocalHint".Translate());
            }

            if (cfg.provider.GetRequiresApiKey())
            {
                listing.Label("API Key:");
                cfg.apiKey = listing.TextEntry(cfg.apiKey ?? "");
            }

            listing.Label("RP_AI_CfgEndpoint".Translate());
            cfg.endpointUrl = listing.TextEntry(cfg.endpointUrl ?? "");

            listing.Label("RP_AI_CfgModel".Translate());
            cfg.model = listing.TextEntry(cfg.model ?? "");

            listing.CheckboxLabeled("RP_AI_CfgEnable".Translate(), ref cfg.enabled);

            // ============ 测试连接按钮 ============
            listing.Gap(4f);
            Rect testBtnRect = listing.GetRect(30f);
            if (Widgets.ButtonText(testBtnRect, testInProgress ? (string)"RP_AI_TestingBtn".Translate() : (string)"RP_AI_CfgTest".Translate()))
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
