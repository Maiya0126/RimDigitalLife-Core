using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimDigitalLife_RimPay
{
    public class Window_TreasuryTerminal : Window
    {
        private int depositAmount = 1000;
        private int withdrawAmount = 1000;
        private string depositBuffer = "1000";
        private string withdrawBuffer = "1000";
        private Vector2 rosterScroll = Vector2.zero;
        private Vector2 loanScroll = Vector2.zero;
        private Vector2 stockScroll = Vector2.zero;
        private Map map;
        private int currentTab = 0;
        private string selectedStockCode = "$ISHN";
        private List<TabRecord> tabs = new List<TabRecord>();

        public Window_TreasuryTerminal(Map map)
        {
            this.map = map;
            this.doCloseX = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(960f, 700f);

        public override void DoWindowContents(Rect inRect)
        {
            GameComponent_RimPay comp = Current.Game.GetComponent<GameComponent_RimPay>();
            if (comp == null) { Widgets.Label(inRect, "RP_W_Treasury_CompLoadFail".Translate()); return; }

            // 右上角：Mod 设置入口（三个 Tab 均可见）
            Rect settingsBtn = new Rect(inRect.xMax - 126f, inRect.y + 4f, 118f, 28f);
            if (Widgets.ButtonText(settingsBtn, "RP_W_Settings".Translate()))
            {
                Find.WindowStack.Add(new Dialog_ModSettings(LoadedModManager.GetMod<RimPayMod>()));
            }

            // ===== 顶部原版 Tab 标签页 =====
            tabs.Clear();
            tabs.Add(new TabRecord("RP_W_Treasury_Tab".Translate(), delegate { currentTab = 0; }, currentTab == 0));
            tabs.Add(new TabRecord("RP_W_Loan_Tab".Translate(), delegate { currentTab = 1; }, currentTab == 1));
            tabs.Add(new TabRecord("RP_W_Stock_Tab".Translate(), delegate { currentTab = 2; }, currentTab == 2));

            Rect contentRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 40f);

            // 绘制原版黄色带有微凹角的标签
            TabDrawer.DrawTabs(contentRect, tabs);

            if (currentTab == 0)
                DrawTreasuryTab(contentRect, comp);
            else if (currentTab == 1)
                DrawLoanTab(contentRect, comp);
            else if (currentTab == 2)
                DrawStockTab(contentRect, comp);
        }

        // ================================================================
        // 标签1：数字国库
        // ================================================================
        private void DrawTreasuryTab(Rect rect, GameComponent_RimPay comp)
        {
            Listing_Standard outer = new Listing_Standard();
            outer.Begin(rect);

            Text.Font = GameFont.Medium;
            outer.Label("RP_W_Treasury_Title".Translate());
            Text.Font = GameFont.Small;
            outer.GapLine();

            int physicalSilver = map.resourceCounter.GetCount(ThingDefOf.Silver);
            int treasury = comp.CloudTreasuryBalance;
            float escrowedBuilding = comp.GetNationalBuildingWealth(map);
            float escrowedEquipment = comp.GetEscrowedPawnEquipmentWealth(map);
            int total = physicalSilver + treasury;

            // 第一行：流动资产三件套
            Rect assetBar = outer.GetRect(44f);
            float colW = (assetBar.width - 20f) / 3f;
            DrawAssetBox(assetBar.x, assetBar.y, colW, assetBar.height,
                "RP_W_Treasury_Asilver".Translate(), physicalSilver.ToString("N0"), "RP_W_Treasury_CountedWealth".Translate(), new Color(0.15f, 0.4f, 0.8f), 0.5f);
            DrawAssetBox(assetBar.x + colW + 10f, assetBar.y, colW, assetBar.height,
                "RP_W_Treasury_Tab".Translate(), treasury.ToString("N0"), "RP_W_Treasury_HiddenPhysically".Translate(), new Color(0.2f, 0.7f, 0.2f), 0.5f);
            DrawAssetBox(assetBar.x + (colW + 10f) * 2f, assetBar.y, colW, assetBar.height,
                "RP_W_Treasury_TotalLiquid".Translate(), total.ToString("N0"), "", new Color(0.7f, 0.5f, 0.1f), 0.5f);

            // 第二行：托管资产（后缀随设置开关动态变化），与第一行等高
            outer.Gap(6f);
            Rect escrowBar = outer.GetRect(44f);
            float escrowW = (escrowBar.width - 10f) / 2f;
            string buildingSuffix = (RimPayMod.settings?.hideBuildingWealth ?? true) ? "RP_W_Treasury_NotCountedRaid".Translate() : "RP_W_Treasury_CountedRaid".Translate();
            string equipSuffix = (RimPayMod.settings?.hidePawnEquipmentWealth ?? true) ? "RP_W_Treasury_NotCountedRaid".Translate() : "RP_W_Treasury_CountedRaid".Translate();
            DrawAssetBox(escrowBar.x, escrowBar.y, escrowW, escrowBar.height,
                "RP_W_Treasury_EscrowBuilding".Translate(), escrowedBuilding.ToString("N0"), buildingSuffix, new Color(0.6f, 0.4f, 0.85f), 0.5f);
            DrawAssetBox(escrowBar.x + escrowW + 10f, escrowBar.y, escrowW, escrowBar.height,
                "RP_W_Treasury_EscrowEquip".Translate(), escrowedEquipment.ToString("N0"), equipSuffix, new Color(0.1f, 0.65f, 0.75f), 0.5f);

            outer.Gap(6f);

            Rect bodyRect = outer.GetRect(365f);
            float leftW = bodyRect.width * 0.42f;
            float rightW = bodyRect.width - leftW - 16f;

            Rect leftRect = new Rect(bodyRect.x, bodyRect.y, leftW, bodyRect.height);
            DrawDepositWithdraw(leftRect, comp);

            Rect divider = new Rect(leftRect.xMax + 6f, bodyRect.y, 4f, bodyRect.height);
            Widgets.DrawBoxSolid(divider, new Color(0.25f, 0.25f, 0.25f));

            Rect rightRect = new Rect(divider.xMax + 6f, bodyRect.y, rightW, bodyRect.height);
            DrawColonistRoster(rightRect, comp);

            outer.Gap(8f);

            // 全区收支汇总
            int totalPayroll = 0, totalBalance = 0;
            foreach (Pawn p in map.mapPawns.FreeColonists.Where(p => !p.Dead))
            {
                totalPayroll += GetSalary(comp, p);
                totalBalance += comp.GetBalance(p);
            }

            Rect summaryRect = outer.GetRect(48f);
            Widgets.DrawBoxSolid(summaryRect, new Color(0.15f, 0.15f, 0.2f, 0.5f));
            Listing_Standard summary = new Listing_Standard();
            summary.Begin(summaryRect);
            Text.Font = GameFont.Tiny;
            summary.Label("RP_W_Treasury_SummaryLine".Translate(totalPayroll, totalBalance, treasury, totalBalance + treasury));
            Text.Font = GameFont.Small;
            summary.End();

            // 昨日国库收支（青色）
            outer.Gap(4f);
            Rect yesterdayRect = outer.GetRect(22f);
            string yesterdayStr = comp.GetDailyTreasurySummary(GenLocalDate.DayOfYear(map) - 1, GenLocalDate.Year(map));
            GUI.color = Color.cyan;
            Text.Font = GameFont.Tiny;
            Widgets.Label(yesterdayRect, yesterdayStr);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            // 底部提示
            outer.Gap(4f);
            Text.Font = GameFont.Tiny;
            outer.Label("RP_W_Treasury_Footnote".Translate());
            Text.Font = GameFont.Small;

            outer.End();
        }

        // ================================================================
        // 标签2：借贷中心（整合派系颜色、借贷滑动框、总账正负值、拉伸名册宽度）
        // ================================================================
        private void DrawLoanTab(Rect rect, GameComponent_RimPay comp)
        {
            Listing_Standard outer = new Listing_Standard();
            outer.Begin(rect);

            Text.Font = GameFont.Medium;
            outer.Label("RP_W_Loan_Title".Translate());
            Text.Font = GameFont.Small;
            outer.GapLine();

            outer.Label("RP_W_Treasury_Balance".Translate(comp.CloudTreasuryBalance));
            outer.Gap(6f);

            outer.Label("RP_W_Loan_ListHeader".Translate());
            outer.Gap(4f);

            // 1. 绘制表头 (在外部，保持固定不滚动)
            Rect headerRow = outer.GetRect(22f);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(headerRow.x + 4f, headerRow.y, 220f, headerRow.height), "RP_W_Loan_ColFaction".Translate());
            Widgets.Label(new Rect(headerRow.x + 230f, headerRow.y, 100f, headerRow.height), "RP_W_Loan_ColGoodwill".Translate());
            Widgets.Label(new Rect(headerRow.x + 340f, headerRow.y, 100f, headerRow.height), "RP_W_Loan_ColRate".Translate());
            Widgets.Label(new Rect(headerRow.x + 450f, headerRow.y, 120f, headerRow.height), "RP_W_Loan_ColLedger".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // 2. 绘制派系列表
            var factions = Find.FactionManager.AllFactionsVisible
                .Where(f => !f.IsPlayer && !f.temporary && !f.HostileTo(Faction.OfPlayer))
                .ToList();

            Rect factionListRect = outer.GetRect(260f);
            float viewH = factions.Count * 32f + 10f;
            Rect viewRect = new Rect(factionListRect.x, factionListRect.y, factionListRect.width - 16f, Mathf.Max(viewH, factionListRect.height));
            Widgets.BeginScrollView(factionListRect, ref loanScroll, viewRect);

            if (factions.Count == 0)
            {
                Widgets.Label(new Rect(viewRect.x + 4f, viewRect.y, viewRect.width, 24f), "RP_W_Loan_NoFactions".Translate());
            }
            else
            {
                float rowY = viewRect.y;
                foreach (Faction f in factions)
                {
                    var S_loan = RimPayMod.settings;
                    float goodwill = f.PlayerGoodwill;
                    float rate = S_loan?.loanRateBase ?? 0.08f;
                    if (goodwill > (S_loan?.loanFriendlyThreshold ?? 75f)) rate = S_loan?.loanRateFriendly ?? 0.06f;
                    else if (goodwill < (S_loan?.loanColdThreshold ?? 25f)) rate = S_loan?.loanRateCold ?? 0.10f;

                    // AI 经济：若开启，借贷利率随当日 AI 倍率浮动（上下限保护）
                    if (RimPayMod.settings?.enableEconomyAI ?? false)
                    {
                        rate = Mathf.Clamp(rate * RimPayAIProvider.loanRateMultiplier, 0.02f, 0.5f);
                    }

                    string rateStr = (rate * 100).ToString("F0");
                    
                    // 好感度状态变色
                    string goodwillStr = goodwill.ToString("F0");
                    Color fColor = Color.white;
                    string relationLabel = "RP_W_Loan_Neutral".Translate();
                    if (goodwill >= 75) { fColor = Color.green; relationLabel = "RP_W_Loan_Ally".Translate(); }
                    else if (goodwill < 0) { fColor = Color.red; relationLabel = "RP_W_Loan_Cold".Translate(); }

                    // 当前借贷总账
                    int netDebt = comp.GetNetFactionDebt(f.loadID);
                    string debtStr = netDebt == 0 ? (string)"RP_W_Loan_NoDebt".Translate() : (netDebt > 0 ? $"+{netDebt}" : $"{netDebt}");
                    Color debtColor = netDebt == 0 ? Color.white : (netDebt > 0 ? Color.green : Color.red);

                    Rect row = new Rect(viewRect.x, rowY, viewRect.width, 30f);
                    Widgets.DrawBoxSolid(row, new Color(0.15f, 0.15f, 0.15f, 0.3f));

                    Text.Anchor = TextAnchor.MiddleLeft;
                    Text.Font = GameFont.Tiny;

                    // 第一列：派系名称 (220f)
                    Widgets.Label(new Rect(row.x + 4f, row.y, 220f, row.height), f.Name);

                    // 第二列：好感 (100f)
                    GUI.color = fColor;
                    Widgets.Label(new Rect(row.x + 230f, row.y, 100f, row.height), "RP_W_Loan_Relation".Translate(relationLabel, goodwillStr));
                    GUI.color = Color.white;

                    // 第三列：利率 (100f)
                    Widgets.Label(new Rect(row.x + 340f, row.y, 100f, row.height), "RP_W_Loan_RateValue".Translate(rateStr));

                    // 第四列：当前总账 (120f)
                    GUI.color = debtColor;
                    Widgets.Label(new Rect(row.x + 450f, row.y, 120f, row.height), debtStr);
                    GUI.color = Color.white;

                    // 操作按钮区 (放贷与借款)
                    Rect loanBtn = new Rect(row.x + row.width - 130f, row.y + 2f, 60f, row.height - 4f);
                    if (Widgets.ButtonText(loanBtn, "RP_W_Loan_Lend".Translate()))
                    {
                        Find.WindowStack.Add(new Window_LoanAdjustment(f, true, comp.CloudTreasuryBalance, delegate(int chosenAmount)
                        {
                            comp.AddLoan(f, chosenAmount, rate);
                            comp.ModifyTreasury(-chosenAmount, "RP_Reason_LendOut");
                            Messages.Message("RP_W_Loan_LendDone".Translate(f.Name, chosenAmount, rateStr), MessageTypeDefOf.PositiveEvent, false);
                        }));
                    }

                    Rect borrowBtn = new Rect(loanBtn.xMax + 5f, row.y + 2f, 60f, row.height - 4f);
                    if (Widgets.ButtonText(borrowBtn, "RP_W_Loan_Borrow".Translate()))
                    {
                        Find.WindowStack.Add(new Window_LoanAdjustment(f, false, S_loan?.loanMaxBorrow ?? 5000, delegate(int chosenAmount)
                        {
                            comp.AddBorrow(f, chosenAmount, S_loan?.borrowRateBase ?? 0.12f); // 借款利率从设置读取
                            comp.ModifyTreasury(chosenAmount, "RP_Reason_BorrowIn");
                            Messages.Message("RP_W_Loan_BorrowDone".Translate(f.Name, chosenAmount, ((S_loan?.borrowRateBase ?? 0.12f) * 100).ToString("F0")), MessageTypeDefOf.PositiveEvent, false);
                        }));
                    }

                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperLeft;
                    rowY += 32f;
                }
            }

            Widgets.EndScrollView();

            // 3. 借贷总账记录
            outer.Gap(8f);
            outer.Label("RP_W_Loan_ActiveRecords".Translate());
            List<LoanRecord> activeLoans = comp.GetActiveLoans();
            if (activeLoans.Count == 0)
            {
                outer.Label("RP_W_Loan_NoRecords".Translate());
            }
            else
            {
                foreach (LoanRecord lr in activeLoans)
                {
                    Rect row = outer.GetRect(28f);
                    Widgets.DrawBoxSolid(row, new Color(0.15f, 0.15f, 0.15f, 0.2f));

                    string actionStr = lr.isFromPlayer ? "RP_Reason_LendTo".Translate() : "RP_Reason_BorrowFrom".Translate();
                    string text = "RP_W_Loan_RecordLine".Translate(actionStr, lr.factionName, (lr.isFromPlayer ? "#7fd97f" : "#ff7f7f"), lr.amount, lr.rate * 100, lr.dueDay);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(new Rect(row.x + 4f, row.y, row.width - 120f, row.height), text);
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperLeft;

                    Rect actionBtn = new Rect(row.x + row.width - 110f, row.y + 1f, 100f, row.height - 2f);
                    string btnLabel = lr.isFromPlayer ? "RP_W_Loan_Reclaim".Translate() : "RP_W_Loan_Repay".Translate();
                    if (Widgets.ButtonText(actionBtn, btnLabel))
                    {
                        if (lr.isFromPlayer)
                        {
                            // 收回放贷
                            comp.ModifyTreasury(lr.amount, "RP_Reason_ReclaimLoan");
                            Messages.Message("RP_W_Loan_ReclaimDone".Translate(lr.factionName, lr.amount), MessageTypeDefOf.PositiveEvent, false);
                            lr.amount = 0; // 标记清除
                        }
                        else
                        {
                            // 还清借款
                            if (comp.CloudTreasuryBalance >= lr.amount)
                            {
                                comp.ModifyTreasury(-lr.amount, "RP_Reason_RepayLoan");
                                Messages.Message("RP_W_Loan_RepayDone".Translate(lr.factionName, lr.amount), MessageTypeDefOf.PositiveEvent, false);
                                lr.amount = 0; // 标记清除
                            }
                            else
                            {
                                Messages.Message("RP_W_Loan_RepayNoFunds".Translate(), MessageTypeDefOf.RejectInput, false);
                            }
                        }
                    }
                }
            }

            outer.End();
        }

        // ================================================================
        // 标签3：证券交易（名册拉宽、红绿大盘配色、独立K线详情大弹窗、填数字输入框）
        // ================================================================
        private void DrawStockTab(Rect rect, GameComponent_RimPay comp)
        {
            Listing_Standard outer = new Listing_Standard();
            outer.Begin(rect);

            Text.Font = GameFont.Medium;
            outer.Label("RP_W_Stock_Title".Translate());
            Text.Font = GameFont.Small;
            outer.GapLine();

            outer.Label("RP_W_Stock_BalanceHint".Translate(comp.CloudTreasuryBalance));
            outer.Gap(6f);

            // 1. 绘制表头 (在外部，保持固定不滚动)
            Rect headerRow = outer.GetRect(22f);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(headerRow.x + 4f, headerRow.y, 60f, headerRow.height), "RP_W_Stock_ColCode".Translate());
            Widgets.Label(new Rect(headerRow.x + 68f, headerRow.y, 180f, headerRow.height), "RP_W_Stock_ColName".Translate());
            Widgets.Label(new Rect(headerRow.x + 252f, headerRow.y, 80f, headerRow.height), "RP_W_Stock_ColPrice".Translate());
            Widgets.Label(new Rect(headerRow.x + 336f, headerRow.y, 80f, headerRow.height), "RP_W_Stock_ColChange".Translate());
            Widgets.Label(new Rect(headerRow.x + 420f, headerRow.y, 70f, headerRow.height), "RP_W_Stock_ColHeld".Translate());
            Widgets.Label(new Rect(headerRow.x + 494f, headerRow.y, 90f, headerRow.height), "RP_W_Stock_ColValue".Translate());
            Widgets.Label(new Rect(headerRow.x + 588f, headerRow.y, 90f, headerRow.height), "RP_W_Stock_ColProfit".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // 2. 绘制股票列表 (直接计算Rect，彻底量化行坐标，完美解决不显示Bug)
            List<StockData> stocks = comp.GetStockPrices();
            Rect stockListRect = outer.GetRect(rect.height - 110f);
            float viewH = stocks.Count * 36f + 10f;
            Rect viewRect = new Rect(stockListRect.x, stockListRect.y, stockListRect.width - 16f, Mathf.Max(viewH, stockListRect.height));
            Widgets.BeginScrollView(stockListRect, ref stockScroll, viewRect);

            if (stocks.Count == 0)
            {
                Widgets.Label(new Rect(viewRect.x + 4f, viewRect.y, viewRect.width, 24f), "RP_W_Stock_NoData".Translate());
            }
            else
            {
                float rowY = viewRect.y;
                foreach (StockData s in stocks)
                {
                    Rect row = new Rect(viewRect.x, rowY, viewRect.width, 32f);
                    bool isSelected = (s.code == selectedStockCode);
                    Widgets.DrawBoxSolid(row, isSelected ? new Color(0.25f, 0.3f, 0.35f, 0.5f) : new Color(0.15f, 0.15f, 0.15f, 0.3f));

                    string changeStr = s.changePercent >= 0 ? $"+{s.changePercent:F1}%" : $"{s.changePercent:F1}%";
                    Color changeColor = s.changePercent >= 0 ? new Color(0.3f, 0.9f, 0.3f) : new Color(0.9f, 0.3f, 0.3f);

                    int held = comp.GetStockHeld(s.code);
                    int marketValue = Mathf.RoundToInt((float)(held * s.price));
                    int profit = comp.GetStockProfit(s.code);

                    // 悬浮提示：显示板块、背景、最新异动成因与持仓分析
                    if (Mouse.IsOver(row))
                    {
                        var sbTip = new System.Text.StringBuilder();
                        sbTip.AppendLine($"<b>{s.code} · {s.name.Translate()}</b>");
                        sbTip.AppendLine("RP_W_Stock_TipSectors".Translate(GetCategoryLabel(s.category).Translate(), (s.volatility * 100).ToString("F0")));
                        if (!string.IsNullOrEmpty(s.desc))
                        {
                            sbTip.AppendLine($"<i>{s.desc.Translate()}</i>");
                        }
                        if (s.IsFactionStock)
                        {
                            Faction f = Find.FactionManager?.AllFactions?.FirstOrDefault(x => x.loadID == s.factionLoadID);
                            if (f != null)
                            {
                                sbTip.AppendLine("RP_W_Stock_TipFaction".Translate(f.Name, f.PlayerGoodwill.ToString("+0;-0;0"), f.PlayerRelationKind.GetLabel()));
                            }
                        }
                        sbTip.AppendLine();
                        sbTip.AppendLine("RP_W_Stock_TipPrice".Translate(s.price.ToString("F2"), changeStr));
                        if (held > 0)
                        {
                            sbTip.AppendLine("RP_W_Stock_TipHeld".Translate(held, marketValue, (profit >= 0 ? "+" : "") + profit));
                        }
                        if (!string.IsNullOrEmpty(s.lastHeadline))
                        {
                            sbTip.AppendLine();
                            sbTip.AppendLine("RP_W_Stock_TipHeadline".Translate(s.lastHeadline.Translate()));
                        }
                        else
                        {
                            sbTip.AppendLine();
                            sbTip.AppendLine("RP_W_Stock_TipNoEvent".Translate());
                        }
                        TooltipHandler.TipRegion(row, sbTip.ToString().TrimEnd());
                    }

                    Text.Anchor = TextAnchor.MiddleLeft;
                    Text.Font = GameFont.Tiny;

                    // 1. 代码
                    Widgets.Label(new Rect(row.x + 4f, row.y, 60f, row.height), s.code);
                    // 2. 名称 (180f)
                    Widgets.Label(new Rect(row.x + 68f, row.y, 180f, row.height), s.name.Translate());

                    // 3. 当前价 & 今日涨跌 (红绿大盘变色)
                    GUI.color = changeColor;
                    Widgets.Label(new Rect(row.x + 252f, row.y, 80f, row.height), s.price.ToString("F2"));
                    Widgets.Label(new Rect(row.x + 336f, row.y, 80f, row.height), changeStr);
                    GUI.color = Color.white;

                    // 4. 持仓
                    Widgets.Label(new Rect(row.x + 420f, row.y, 70f, row.height), held > 0 ? held.ToString() : "-");
                    // 5. 当前市值
                    Widgets.Label(new Rect(row.x + 494f, row.y, 90f, row.height), held > 0 ? $"{marketValue}" : "-");

                    // 6. 盈亏 (绿+红-)
                    if (held > 0 && profit != 0)
                    {
                        GUI.color = profit >= 0 ? new Color(0.3f, 0.9f, 0.3f) : new Color(0.9f, 0.3f, 0.3f);
                        Widgets.Label(new Rect(row.x + 588f, row.y, 90f, row.height), profit >= 0 ? $"+{profit}" : $"{profit}");
                        GUI.color = Color.white;
                    }
                    else
                    {
                        Widgets.Label(new Rect(row.x + 588f, row.y, 90f, row.height), "-");
                    }

                    // 操作按钮：进入K线详情大弹窗进行买卖交易
                    Rect tradeBtn = new Rect(row.x + row.width - 100f, row.y + 2f, 90f, row.height - 4f);
                    if (Widgets.ButtonText(tradeBtn, "RP_W_Stock_Detail".Translate()))
                    {
                        Find.WindowStack.Add(new Window_StockDetail(s, comp));
                    }

                    // 整行点击事件 (双击或单击均触发弹窗)
                    if (Widgets.ButtonInvisible(new Rect(row.x, row.y, row.width - 105f, row.height)))
                    {
                        selectedStockCode = s.code;
                        Find.WindowStack.Add(new Window_StockDetail(s, comp));
                    }

                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperLeft;
                    rowY += 36f;
                }
            }

            Widgets.EndScrollView();
            outer.End();
        }

        // ---- 资产格子 ----
        private void DrawAssetBox(float x, float y, float w, float h, string label, string value, string suffix, Color bg, float alpha = 0.25f)
        {
            Rect box = new Rect(x, y, w, h);
            Color drawColor = bg;
            drawColor.a = alpha;
            Widgets.DrawBoxSolid(box, drawColor);
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect labelRect = new Rect(box.x + 8f, box.y, box.width - 16f, box.height * 0.4f);
            Widgets.Label(labelRect, label);
            Text.Font = GameFont.Medium;
            Rect valRect = new Rect(box.x + 8f, labelRect.yMax, box.width - 16f, box.height * 0.6f);
            Widgets.Label(valRect, value);
            Text.Font = GameFont.Small;
            if (!string.IsNullOrEmpty(suffix))
            {
                Text.Anchor = TextAnchor.MiddleRight;
                Rect suffixRect = new Rect(box.x + 8f, box.y, box.width - 12f, box.height);
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(suffixRect, suffix);
                GUI.color = Color.white;
            }
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawDepositWithdraw(Rect rect, GameComponent_RimPay comp)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(rect);
            int physicalSilver = map.resourceCounter.GetCount(ThingDefOf.Silver);
            Text.Font = GameFont.Medium;
            list.Label("RP_W_Treasury_DepositWithdraw".Translate());
            Text.Font = GameFont.Small;
            list.GapLine();
            list.Label("RP_W_Treasury_DepositLabel".Translate());
            Rect depRow = list.GetRect(30f);
            Rect depInput = new Rect(depRow.x, depRow.y, depRow.width - 125f, depRow.height);
            Widgets.TextFieldNumeric(depInput, ref depositAmount, ref depositBuffer, 0, physicalSilver);
            Rect depAllBtn = new Rect(depInput.xMax + 5f, depRow.y, 55f, depRow.height);
            if (Widgets.ButtonText(depAllBtn, "RP_W_Treasury_All".Translate()))
            {
                depositAmount = physicalSilver; depositBuffer = depositAmount.ToString();
            }
            Rect depBtn = new Rect(depAllBtn.xMax + 5f, depRow.y, 60f, depRow.height);
            if (ColoredButton(depBtn, "RP_W_Treasury_DepositBtn".Translate(), new Color(0.15f, 0.55f, 0.2f)))
            {
                if (depositAmount > 0 && depositAmount <= physicalSilver)
                {
                    int removed = RemovePhysicalSilver(map, depositAmount);
                    if (removed > 0) { comp.ModifyTreasury(removed, "RP_Reason_SilverDeposit"); Messages.Message("RP_W_Treasury_DepositDone".Translate(removed), MessageTypeDefOf.PositiveEvent); depositAmount = 0; depositBuffer = "0"; }
                }
            }
            list.Gap(10f);
            list.Label("RP_W_Treasury_WithdrawLabel".Translate());
            Rect withRow = list.GetRect(30f);
            Rect withInput = new Rect(withRow.x, withRow.y, withRow.width - 125f, withRow.height);
            Widgets.TextFieldNumeric(withInput, ref withdrawAmount, ref withdrawBuffer, 0, comp.CloudTreasuryBalance);
            Rect withAllBtn = new Rect(withInput.xMax + 5f, withRow.y, 55f, withRow.height);
            if (Widgets.ButtonText(withAllBtn, "RP_W_Treasury_All".Translate()))
            {
                withdrawAmount = comp.CloudTreasuryBalance; withdrawBuffer = withdrawAmount.ToString();
            }
            Rect withBtn = new Rect(withAllBtn.xMax + 5f, withRow.y, 60f, withRow.height);
            if (ColoredButton(withBtn, "RP_W_Treasury_WithdrawBtn".Translate(), new Color(0.55f, 0.15f, 0.15f)))
            {
                if (withdrawAmount > 0 && withdrawAmount <= comp.CloudTreasuryBalance)
                {
                    comp.ModifyTreasury(-withdrawAmount, "RP_Reason_SilverWithdraw");
                    Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                    silver.stackCount = withdrawAmount;
                    IntVec3 pos = GetCommsConsolePos(map) ?? DropCellFinder.TradeDropSpot(map);
                    GenPlace.TryPlaceThing(silver, pos, map, ThingPlaceMode.Near);
                    Messages.Message("RP_W_Treasury_WithdrawDone".Translate(withdrawAmount), MessageTypeDefOf.PositiveEvent);
                    withdrawAmount = 0; withdrawBuffer = "0";
                }
            }
            list.Gap(10f);
            list.Label("RP_W_Treasury_DepositAvail".Translate(physicalSilver, comp.CloudTreasuryBalance));
            list.End();
        }

        private void DrawColonistRoster(Rect rect, GameComponent_RimPay comp)
        {
            List<Pawn> colonists = map.mapPawns.FreeColonists.Where(p => !p.Dead).OrderByDescending(p => comp.GetBalance(p)).ToList();
            int totalDailyPayroll = 0;
            foreach (Pawn p in colonists) totalDailyPayroll += GetSalary(comp, p);
            float supportDays = totalDailyPayroll > 0 ? (float)comp.CloudTreasuryBalance / totalDailyPayroll : 0f;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f), "RP_W_Treasury_RosterTitle".Translate());
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x, rect.y + 24f, rect.width, 24f), "RP_W_Treasury_RosterSummary".Translate(totalDailyPayroll, supportDays.ToString("F1")));

            float headerY = rect.y + 50f;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x + 4f, headerY, 100f, 20f), "RP_W_Treasury_ColName".Translate());
            Widgets.Label(new Rect(rect.x + 100f, headerY, 70f, 20f), "RP_W_Treasury_ColBalance".Translate());
            Widgets.Label(new Rect(rect.x + 170f, headerY, 70f, 20f), "RP_W_Treasury_ColSalary".Translate());
            Widgets.Label(new Rect(rect.x + 240f, headerY, rect.width - 244f, 20f), "RP_W_Treasury_ColType".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            float scrollY = headerY + 22f;
            float scrollH = rect.y + rect.height - scrollY - 2f;
            float contentH = colonists.Count * 26f + 10f;
            Rect scrollRect = new Rect(rect.x, scrollY, rect.width, scrollH);
            Rect viewRect = new Rect(rect.x, scrollY, rect.width - 16f, Mathf.Max(contentH, scrollH));
            Widgets.BeginScrollView(scrollRect, ref rosterScroll, viewRect);
            Text.Font = GameFont.Tiny;

            if (colonists.Count == 0)
            {
                Widgets.Label(new Rect(rect.x + 4f, scrollY, rect.width - 8f, 20f), "RP_W_Treasury_NoColonists".Translate());
            }
            else
            {
                float rowY = viewRect.y;
                foreach (Pawn p in colonists)
                {
                    Rect row = new Rect(viewRect.x, rowY, viewRect.width, 24f);
                    Widgets.DrawBoxSolid(row, new Color(0.15f, 0.15f, 0.15f, 0.3f));
                    int bal = comp.GetBalance(p);
                    int salary = GetSalary(comp, p);
                    string tier = GetSalaryTier(p, comp, out Color tierColor);

                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(new Rect(row.x + 4f, row.y, 96f, row.height), p.LabelShort);
                    Widgets.Label(new Rect(row.x + 100f, row.y, 70f, row.height), bal.ToString());
                    Widgets.Label(new Rect(row.x + 170f, row.y, 70f, row.height), salary.ToString());

                // 类型列：可点击
                Rect typeRect = new Rect(row.x + 240f, row.y, row.width - 244f, row.height);
                GUI.color = tierColor;
                Widgets.Label(typeRect, tier.Translate());
                GUI.color = Color.white;

                if (Widgets.ButtonInvisible(typeRect))
                {
                    List<FloatMenuOption> opts = new List<FloatMenuOption>();
                    string[] types = { "Auto", "Child", "Slave", "Novice", "Basic", "Skilled", "Expert" };
                    foreach (string t in types)
                    {
                        opts.Add(new FloatMenuOption(("RP_W_Treasury_Tier_" + t).Translate(), delegate
                        {
                            comp.SetPawnCustomType(p, t == "Auto" ? null : t);
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
                    Text.Anchor = TextAnchor.UpperLeft;
                    rowY += 26f;
                }
            }
            Text.Font = GameFont.Small;
            Widgets.EndScrollView();
        }

        private string GetSalaryTier(Pawn pawn, GameComponent_RimPay comp, out Color color)
        {
            string custom = comp.GetPawnCustomType(pawn);
            if (custom != null)
            {
                if (custom == "Child") color = new Color(0.6f, 0.6f, 0.15f);
                else if (custom == "Slave") color = new Color(0.8f, 0.4f, 0.0f);
                else if (custom == "Expert") color = Color.yellow;
                else if (custom == "Skilled") color = new Color(0.3f, 0.6f, 1f);
                else if (custom == "Basic") color = Color.white;
                else if (custom == "Novice") color = new Color(0.5f, 0.5f, 0.5f);
                else color = Color.white;
                return "RP_W_Treasury_Tier_" + custom;
            }

            if (pawn.DevelopmentalStage == DevelopmentalStage.Baby || pawn.DevelopmentalStage == DevelopmentalStage.Child)
            { color = new Color(0.6f, 0.6f, 0.15f); return "RP_W_Treasury_Tier_Child"; }
            if (pawn.IsSlaveOfColony)
            { color = new Color(0.8f, 0.4f, 0.0f); return "RP_W_Treasury_Tier_Slave"; }
            int bestSkill = 0;
            if (pawn.skills != null)
                foreach (var s in pawn.skills.skills) if (s.Level > bestSkill) bestSkill = s.Level;
            if (bestSkill >= 15) { color = Color.yellow; return "RP_W_Treasury_Tier_Expert"; }
            if (bestSkill >= 10) { color = new Color(0.3f, 0.6f, 1f); return "RP_W_Treasury_Tier_Skilled"; }
            if (bestSkill >= 6) { color = Color.white; return "RP_W_Treasury_Tier_Basic"; }
            color = new Color(0.5f, 0.5f, 0.5f); return "RP_W_Treasury_Tier_Novice";
        }

        private int GetSalary(GameComponent_RimPay comp, Pawn pawn)
        {
            string custom = comp.GetPawnCustomType(pawn);
            if (custom == "Expert") return 50;
            if (custom == "Skilled") return 30;
            if (custom == "Basic") return 20;
            if (custom == "Novice") return 10;
            if (custom == "Child") return 5;
            if (custom == "Slave") return 2;

            int salary = 10;
            if (pawn.DevelopmentalStage == DevelopmentalStage.Baby || pawn.DevelopmentalStage == DevelopmentalStage.Child) return 5;
            if (pawn.IsSlaveOfColony) return 2;
            int bestSkill = 0;
            if (pawn.skills != null)
                foreach (var s in pawn.skills.skills) if (s.Level > bestSkill) bestSkill = s.Level;
            if (bestSkill >= 15) salary += 40;
            else if (bestSkill >= 10) salary += 20;
            else if (bestSkill >= 6) salary += 10;
            return salary;
        }

        private static string GetCategoryLabel(string cat)
        {
            switch (cat)
            {
                case "Tech": return "RP_W_Stock_Cat_Tech";
                case "Entertainment": return "RP_W_Stock_Cat_Entertainment";
                case "Food": return "RP_W_Stock_Cat_Food";
                case "Agri": return "RP_W_Stock_Cat_Agri";
                case "Ranch": return "RP_W_Stock_Cat_Ranch";
                case "ECom": return "RP_W_Stock_Cat_ECom";
                case "Media": return "RP_W_Stock_Cat_Media";
                case "Sports": return "RP_W_Stock_Cat_Sports";
                case "Medical": return "RP_W_Stock_Cat_Medical";
                case "Arms": return "RP_W_Stock_Cat_Arms";
                case "Mining": return "RP_W_Stock_Cat_Mining";
                case "Finance": return "RP_W_Stock_Cat_Finance";
                case "Quantum": return "RP_W_Stock_Cat_Quantum";
                case "Faction": return "RP_W_Stock_Cat_Faction";
                default: return "RP_W_Stock_Cat_Misc";
            }
        }

        private static bool ColoredButton(Rect rect, string label, Color bgColor)
        {
            bool hovered = Mouse.IsOver(rect);
            Color baseColor = hovered ? (bgColor * 1.2f) : bgColor;
            baseColor.a = 1f;
            Widgets.DrawRectFast(rect, baseColor);
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.white;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            if (hovered && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            { Event.current.Use(); return true; }
            return false;
        }

        private static int RemovePhysicalSilver(Map map, int totalToRemove)
        {
            int remaining = totalToRemove;
            foreach (Thing silver in map.listerThings.ThingsOfDef(ThingDefOf.Silver).ToList())
            {
                if (remaining <= 0) break;
                if (!silver.Spawned) continue;
                int take = Mathf.Min(silver.stackCount, remaining);
                Thing taken = silver.SplitOff(take);
                if (taken != null) { taken.Destroy(); remaining -= take; }
            }
            map.resourceCounter.UpdateResourceCounts();
            return totalToRemove - remaining;
        }

        private static IntVec3? GetCommsConsolePos(Map map)
        {
            foreach (Building b in map.listerBuildings.allBuildingsColonist)
                if (b is Building_CommsConsole) return b.Position;
            return null;
        }
    }
}
