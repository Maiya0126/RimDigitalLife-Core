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
            if (comp == null) { Widgets.Label(inRect, "组件加载失败，请重启游戏。"); return; }

            // 右上角：Mod 设置入口（三个 Tab 均可见）
            Rect settingsBtn = new Rect(inRect.xMax - 126f, inRect.y + 4f, 118f, 28f);
            if (Widgets.ButtonText(settingsBtn, "⚙ 设置 (Settings)"))
            {
                Find.WindowStack.Add(new Dialog_ModSettings(LoadedModManager.GetMod<RimPayMod>()));
            }

            // ===== 顶部原版 Tab 标签页 =====
            tabs.Clear();
            tabs.Add(new TabRecord("数字国库", delegate { currentTab = 0; }, currentTab == 0));
            tabs.Add(new TabRecord("借贷中心", delegate { currentTab = 1; }, currentTab == 1));
            tabs.Add(new TabRecord("证券交易", delegate { currentTab = 2; }, currentTab == 2));

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
            outer.Label("RimPay 数字国库 — 殖民地财报总览");
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
                "物理白银", physicalSilver.ToString("N0"), "计入殖民地财富", new Color(0.15f, 0.4f, 0.8f), 0.5f);
            DrawAssetBox(assetBar.x + colW + 10f, assetBar.y, colW, assetBar.height,
                "数字国库", treasury.ToString("N0"), "已物理隐藏", new Color(0.2f, 0.7f, 0.2f), 0.5f);
            DrawAssetBox(assetBar.x + (colW + 10f) * 2f, assetBar.y, colW, assetBar.height,
                "流动资产合计", total.ToString("N0"), "", new Color(0.7f, 0.5f, 0.1f), 0.5f);

            // 第二行：托管资产（后缀随设置开关动态变化），与第一行等高
            outer.Gap(6f);
            Rect escrowBar = outer.GetRect(44f);
            float escrowW = (escrowBar.width - 10f) / 2f;
            string buildingSuffix = (RimPayMod.settings?.hideBuildingWealth ?? true) ? "不计入袭击威胁" : "计入袭击威胁";
            string equipSuffix = (RimPayMod.settings?.hidePawnEquipmentWealth ?? true) ? "不计入袭击威胁" : "计入袭击威胁";
            DrawAssetBox(escrowBar.x, escrowBar.y, escrowW, escrowBar.height,
                "托管资产(建筑)", escrowedBuilding.ToString("N0"), buildingSuffix, new Color(0.6f, 0.4f, 0.85f), 0.5f);
            DrawAssetBox(escrowBar.x + escrowW + 10f, escrowBar.y, escrowW, escrowBar.height,
                "托管资产(随身装备)", escrowedEquipment.ToString("N0"), equipSuffix, new Color(0.1f, 0.65f, 0.75f), 0.5f);

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
            summary.Label($"<b>全区收支汇总</b>  每日薪资: {totalPayroll}  |  殖民者钱包合计: {totalBalance}  |  数字国库: {treasury}  |  总数字资产: {totalBalance + treasury}  (单位: @银)");
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
            outer.Label("<i>单位: @银 (1 数字白银 = 1 物理白银)  |  物理白银计入殖民地财富计算，数字国库不计入  |  可以将白银存入数字国库压低殖民地财富值，减轻袭击力度    [v0.7.02]</i>");
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
            outer.Label("RimPay 借贷中心");
            Text.Font = GameFont.Small;
            outer.GapLine();

            outer.Label($"<b>数字国库余额: {comp.CloudTreasuryBalance} @银</b>");
            outer.Gap(6f);

            outer.Label("放贷与借款列表 (利率基于派系好感度浮动):");
            outer.Gap(4f);

            // 1. 绘制表头 (在外部，保持固定不滚动)
            Rect headerRow = outer.GetRect(22f);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(headerRow.x + 4f, headerRow.y, 220f, headerRow.height), "派系名称");
            Widgets.Label(new Rect(headerRow.x + 230f, headerRow.y, 100f, headerRow.height), "好感状态");
            Widgets.Label(new Rect(headerRow.x + 340f, headerRow.y, 100f, headerRow.height), "借贷利率/季");
            Widgets.Label(new Rect(headerRow.x + 450f, headerRow.y, 120f, headerRow.height), "当前总账");
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
                Widgets.Label(new Rect(viewRect.x + 4f, viewRect.y, viewRect.width, 24f), "暂无可以借贷的派系。");
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
                    string relationLabel = "中立";
                    if (goodwill >= 75) { fColor = Color.green; relationLabel = "盟友"; }
                    else if (goodwill < 0) { fColor = Color.red; relationLabel = "冷漠"; }

                    // 当前借贷总账
                    int netDebt = comp.GetNetFactionDebt(f.loadID);
                    string debtStr = netDebt == 0 ? "无欠款" : (netDebt > 0 ? $"+{netDebt}" : $"{netDebt}");
                    Color debtColor = netDebt == 0 ? Color.white : (netDebt > 0 ? Color.green : Color.red);

                    Rect row = new Rect(viewRect.x, rowY, viewRect.width, 30f);
                    Widgets.DrawBoxSolid(row, new Color(0.15f, 0.15f, 0.15f, 0.3f));

                    Text.Anchor = TextAnchor.MiddleLeft;
                    Text.Font = GameFont.Tiny;

                    // 第一列：派系名称 (220f)
                    Widgets.Label(new Rect(row.x + 4f, row.y, 220f, row.height), f.Name);

                    // 第二列：好感 (100f)
                    GUI.color = fColor;
                    Widgets.Label(new Rect(row.x + 230f, row.y, 100f, row.height), $"{relationLabel} ({goodwillStr})");
                    GUI.color = Color.white;

                    // 第三列：利率 (100f)
                    Widgets.Label(new Rect(row.x + 340f, row.y, 100f, row.height), $"{rateStr}%");

                    // 第四列：当前总账 (120f)
                    GUI.color = debtColor;
                    Widgets.Label(new Rect(row.x + 450f, row.y, 120f, row.height), debtStr);
                    GUI.color = Color.white;

                    // 操作按钮区 (放贷与借款)
                    Rect loanBtn = new Rect(row.x + row.width - 130f, row.y + 2f, 60f, row.height - 4f);
                    if (Widgets.ButtonText(loanBtn, "放贷"))
                    {
                        Find.WindowStack.Add(new Window_LoanAdjustment(f, true, comp.CloudTreasuryBalance, delegate(int chosenAmount)
                        {
                            comp.AddLoan(f, chosenAmount, rate);
                            comp.ModifyTreasury(-chosenAmount, "对外放贷");
                            Messages.Message($"成功向 {f.Name} 放贷了 {chosenAmount} @银，利率 {rateStr}% / 季度。", MessageTypeDefOf.PositiveEvent, false);
                        }));
                    }

                    Rect borrowBtn = new Rect(loanBtn.xMax + 5f, row.y + 2f, 60f, row.height - 4f);
                    if (Widgets.ButtonText(borrowBtn, "借款"))
                    {
                        Find.WindowStack.Add(new Window_LoanAdjustment(f, false, S_loan?.loanMaxBorrow ?? 5000, delegate(int chosenAmount)
                        {
                            comp.AddBorrow(f, chosenAmount, S_loan?.borrowRateBase ?? 0.12f); // 借款利率从设置读取
                            comp.ModifyTreasury(chosenAmount, "对外借款");
                            Messages.Message($"成功从 {f.Name} 借款了 {chosenAmount} @银，利率 {(S_loan?.borrowRateBase ?? 0.12f) * 100:F0}% / 季度。", MessageTypeDefOf.PositiveEvent, false);
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
            outer.Label("<b>当前有效的借贷记录:</b>");
            List<LoanRecord> activeLoans = comp.GetActiveLoans();
            if (activeLoans.Count == 0)
            {
                outer.Label("  目前无任何未结算的贷款或借款。");
            }
            else
            {
                foreach (LoanRecord lr in activeLoans)
                {
                    Rect row = outer.GetRect(28f);
                    Widgets.DrawBoxSolid(row, new Color(0.15f, 0.15f, 0.15f, 0.2f));

                    string actionStr = lr.isFromPlayer ? "放贷给" : "借款自";
                    string text = $"  {actionStr} {lr.factionName} : <color={(lr.isFromPlayer ? "#7fd97f" : "#ff7f7f")}>{lr.amount}</color> @银  (利率: {lr.rate * 100:F0}%/季 | 到期: 第{lr.dueDay}日)";
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(new Rect(row.x + 4f, row.y, row.width - 120f, row.height), text);
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperLeft;

                    Rect actionBtn = new Rect(row.x + row.width - 110f, row.y + 1f, 100f, row.height - 2f);
                    string btnLabel = lr.isFromPlayer ? "收回本金" : "还清借款";
                    if (Widgets.ButtonText(actionBtn, btnLabel))
                    {
                        if (lr.isFromPlayer)
                        {
                            // 收回放贷
                            comp.ModifyTreasury(lr.amount, "收回放贷");
                            Messages.Message($"【RimPay借贷】已成功收回向 {lr.factionName} 的放贷本金 {lr.amount} @银，本金已存入数字国库。", MessageTypeDefOf.PositiveEvent, false);
                            lr.amount = 0; // 标记清除
                        }
                        else
                        {
                            // 还清借款
                            if (comp.CloudTreasuryBalance >= lr.amount)
                            {
                                comp.ModifyTreasury(-lr.amount, "还清借款");
                                Messages.Message($"【RimPay借贷】已成功还清向 {lr.factionName} 的借款本金 {lr.amount} @银。", MessageTypeDefOf.PositiveEvent, false);
                                lr.amount = 0; // 标记清除
                            }
                            else
                            {
                                Messages.Message("数字国库余额不足以还清借款！", MessageTypeDefOf.RejectInput, false);
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
            outer.Label("RimPay 证券交易中心");
            Text.Font = GameFont.Small;
            outer.GapLine();

            outer.Label($"<b>数字国库余额: {comp.CloudTreasuryBalance} @银</b>  |  <i>双击或点击任意股票查看15天K线蜡烛图及批量交易</i>");
            outer.Gap(6f);

            // 1. 绘制表头 (在外部，保持固定不滚动)
            Rect headerRow = outer.GetRect(22f);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(headerRow.x + 4f, headerRow.y, 60f, headerRow.height), "代码");
            Widgets.Label(new Rect(headerRow.x + 68f, headerRow.y, 180f, headerRow.height), "名称");
            Widgets.Label(new Rect(headerRow.x + 252f, headerRow.y, 80f, headerRow.height), "当前价");
            Widgets.Label(new Rect(headerRow.x + 336f, headerRow.y, 80f, headerRow.height), "今日涨跌");
            Widgets.Label(new Rect(headerRow.x + 420f, headerRow.y, 70f, headerRow.height), "持仓数");
            Widgets.Label(new Rect(headerRow.x + 494f, headerRow.y, 90f, headerRow.height), "当前市值");
            Widgets.Label(new Rect(headerRow.x + 588f, headerRow.y, 90f, headerRow.height), "盈亏");
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
                Widgets.Label(new Rect(viewRect.x + 4f, viewRect.y, viewRect.width, 24f), "当前无股票数据可用。");
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

                    Text.Anchor = TextAnchor.MiddleLeft;
                    Text.Font = GameFont.Tiny;

                    // 1. 代码
                    Widgets.Label(new Rect(row.x + 4f, row.y, 60f, row.height), s.code);
                    // 2. 名称 (180f)
                    Widgets.Label(new Rect(row.x + 68f, row.y, 180f, row.height), s.name);

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
                    int profit = comp.GetStockProfit(s.code);
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
                    if (Widgets.ButtonText(tradeBtn, "详情K线"))
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
            list.Label("存取白银");
            Text.Font = GameFont.Small;
            list.GapLine();
            list.Label("存入 (物理白银 → 数字国库):");
            Rect depRow = list.GetRect(30f);
            Rect depInput = new Rect(depRow.x, depRow.y, depRow.width - 125f, depRow.height);
            Widgets.TextFieldNumeric(depInput, ref depositAmount, ref depositBuffer, 0, physicalSilver);
            Rect depAllBtn = new Rect(depInput.xMax + 5f, depRow.y, 55f, depRow.height);
            if (Widgets.ButtonText(depAllBtn, "全部"))
            {
                depositAmount = physicalSilver; depositBuffer = depositAmount.ToString();
            }
            Rect depBtn = new Rect(depAllBtn.xMax + 5f, depRow.y, 60f, depRow.height);
            if (ColoredButton(depBtn, "存入", new Color(0.15f, 0.55f, 0.2f)))
            {
                if (depositAmount > 0 && depositAmount <= physicalSilver)
                {
                    int removed = RemovePhysicalSilver(map, depositAmount);
                    if (removed > 0) { comp.ModifyTreasury(removed, "白银存入"); Messages.Message($"成功存入 {removed} @银 至数字国库。", MessageTypeDefOf.PositiveEvent); depositAmount = 0; depositBuffer = "0"; }
                }
            }
            list.Gap(10f);
            list.Label("提取 (数字国库 → 物理白银):");
            Rect withRow = list.GetRect(30f);
            Rect withInput = new Rect(withRow.x, withRow.y, withRow.width - 125f, withRow.height);
            Widgets.TextFieldNumeric(withInput, ref withdrawAmount, ref withdrawBuffer, 0, comp.CloudTreasuryBalance);
            Rect withAllBtn = new Rect(withInput.xMax + 5f, withRow.y, 55f, withRow.height);
            if (Widgets.ButtonText(withAllBtn, "全部"))
            {
                withdrawAmount = comp.CloudTreasuryBalance; withdrawBuffer = withdrawAmount.ToString();
            }
            Rect withBtn = new Rect(withAllBtn.xMax + 5f, withRow.y, 60f, withRow.height);
            if (ColoredButton(withBtn, "提取", new Color(0.55f, 0.15f, 0.15f)))
            {
                if (withdrawAmount > 0 && withdrawAmount <= comp.CloudTreasuryBalance)
                {
                    comp.ModifyTreasury(-withdrawAmount, "白银提取");
                    Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                    silver.stackCount = withdrawAmount;
                    IntVec3 pos = GetCommsConsolePos(map) ?? DropCellFinder.TradeDropSpot(map);
                    GenPlace.TryPlaceThing(silver, pos, map, ThingPlaceMode.Near);
                    Messages.Message($"成功从数字国库提取 {withdrawAmount} @银。", MessageTypeDefOf.PositiveEvent);
                    withdrawAmount = 0; withdrawBuffer = "0";
                }
            }
            list.Gap(10f);
            list.Label($"<i>可存入: {physicalSilver} @银  |  数字国库: {comp.CloudTreasuryBalance} @银</i>");
            list.End();
        }

        private void DrawColonistRoster(Rect rect, GameComponent_RimPay comp)
        {
            List<Pawn> colonists = map.mapPawns.FreeColonists.Where(p => !p.Dead).OrderByDescending(p => comp.GetBalance(p)).ToList();
            int totalDailyPayroll = 0;
            foreach (Pawn p in colonists) totalDailyPayroll += GetSalary(comp, p);
            float supportDays = totalDailyPayroll > 0 ? (float)comp.CloudTreasuryBalance / totalDailyPayroll : 0f;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f), "殖民者财政名册");
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x, rect.y + 24f, rect.width, 24f), $"每日薪资合计: {totalDailyPayroll}  |  数字国库可支撑: {supportDays:F1} 天  (单位: @银)");

            float headerY = rect.y + 50f;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x + 4f, headerY, 100f, 20f), "姓名");
            Widgets.Label(new Rect(rect.x + 100f, headerY, 70f, 20f), "余额 @银");
            Widgets.Label(new Rect(rect.x + 170f, headerY, 70f, 20f), "日薪 @银");
            Widgets.Label(new Rect(rect.x + 240f, headerY, rect.width - 244f, 20f), "类型");
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
                Widgets.Label(new Rect(rect.x + 4f, scrollY, rect.width - 8f, 20f), "暂无殖民者。");
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
                    Widgets.Label(typeRect, tier);
                    GUI.color = Color.white;

                    if (Widgets.ButtonInvisible(typeRect))
                    {
                        List<FloatMenuOption> opts = new List<FloatMenuOption>();
                        string[] types = { "自动", "儿童", "奴隶", "初级", "基础", "熟练", "专家" };
                        foreach (string t in types)
                        {
                            opts.Add(new FloatMenuOption(t, delegate
                            {
                                comp.SetPawnCustomType(p, t == "自动" ? null : t);
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
                if (custom == "儿童") color = new Color(0.6f, 0.6f, 0.15f);
                else if (custom == "奴隶") color = new Color(0.8f, 0.4f, 0.0f);
                else if (custom == "专家") color = Color.yellow;
                else if (custom == "熟练") color = new Color(0.3f, 0.6f, 1f);
                else if (custom == "基础") color = Color.white;
                else if (custom == "初级") color = new Color(0.5f, 0.5f, 0.5f);
                else color = Color.white;
                return custom;
            }

            if (pawn.DevelopmentalStage == DevelopmentalStage.Baby || pawn.DevelopmentalStage == DevelopmentalStage.Child)
            { color = new Color(0.6f, 0.6f, 0.15f); return "儿童"; }
            if (pawn.IsSlaveOfColony)
            { color = new Color(0.8f, 0.4f, 0.0f); return "奴隶"; }
            int bestSkill = 0;
            if (pawn.skills != null)
                foreach (var s in pawn.skills.skills) if (s.Level > bestSkill) bestSkill = s.Level;
            if (bestSkill >= 15) { color = Color.yellow; return "专家"; }
            if (bestSkill >= 10) { color = new Color(0.3f, 0.6f, 1f); return "熟练"; }
            if (bestSkill >= 6) { color = Color.white; return "基础"; }
            color = new Color(0.5f, 0.5f, 0.5f); return "初级";
        }

        private int GetSalary(GameComponent_RimPay comp, Pawn pawn)
        {
            string custom = comp.GetPawnCustomType(pawn);
            if (custom == "专家") return 50;
            if (custom == "熟练") return 30;
            if (custom == "基础") return 20;
            if (custom == "初级") return 10;
            if (custom == "儿童") return 5;
            if (custom == "奴隶") return 2;

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