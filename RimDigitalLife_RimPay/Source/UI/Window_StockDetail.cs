using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimDigitalLife_RimPay
{
    public class Window_StockDetail : Window
    {
        private StockData stock;
        private int tradeAmount = 10;
        private string tradeBuffer = "10";
        private GameComponent_RimPay comp;

        public Window_StockDetail(StockData stock, GameComponent_RimPay comp)
        {
            this.stock = stock;
            this.comp = comp;
            this.doCloseX = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(660f, 650f);

        public override void DoWindowContents(Rect inRect)
        {
            if (stock == null || comp == null) return;

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            // 标题及基础信息
            Text.Font = GameFont.Medium;
            string changeStr = stock.changePercent >= 0 ? $"+{stock.changePercent:F1}%" : $"{stock.changePercent:F1}%";
            Color changeColor = stock.changePercent >= 0 ? Color.green : Color.red;
            listing.Label("RP_W_Stock_DetailTitle".Translate(stock.code, stock.name.Translate()));
            Text.Font = GameFont.Small;

            int heldStock = comp.GetStockHeld(stock.code);
            int stockProfit = comp.GetStockProfit(stock.code);
            string profitStr = stockProfit >= 0 ? $"+{stockProfit}" : $"{stockProfit}";
            Color profitColor = stockProfit >= 0 ? Color.green : Color.red;
            listing.Label("RP_W_Stock_DetailInfo".Translate(stock.price.ToString("F2"), changeStr.Colorize(changeColor), heldStock, profitStr.Colorize(profitColor)));

            if (!string.IsNullOrEmpty(stock.desc))
            {
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.75f, 0.75f, 0.75f);
                string factionExtra = "";
                if (stock.IsFactionStock)
                {
                    Faction f = Find.FactionManager?.AllFactions?.FirstOrDefault(x => x.loadID == stock.factionLoadID);
                    if (f != null)
                    {
                        factionExtra = "RP_W_Stock_DetailFaction".Translate(f.PlayerGoodwill.ToString("+0;-0;0"), f.PlayerRelationKind.GetLabel());
                    }
                }
                listing.Label($"{stock.desc.Translate()}{factionExtra}");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
            listing.GapLine();

            // K线蜡烛图 (占大约半个窗口)
            Rect chartRect = listing.GetRect(240f);
            DrawStockChart(chartRect);

            listing.Gap(12f);

            // 交易操作区
            listing.Label("RP_W_Stock_TradeTitle".Translate(comp.CloudTreasuryBalance));
            Rect tradeRow = listing.GetRect(30f);
            Rect inputRect = new Rect(tradeRow.x, tradeRow.y, 140f, tradeRow.height);
            Widgets.TextFieldNumeric(inputRect, ref tradeAmount, ref tradeBuffer, 1, 10000);

            // 快捷数额按钮
            Rect q1 = new Rect(inputRect.xMax + 10f, tradeRow.y, 45f, tradeRow.height);
            if (Widgets.ButtonText(q1, "10")) { tradeAmount = 10; tradeBuffer = "10"; }
            Rect q2 = new Rect(q1.xMax + 5f, tradeRow.y, 45f, tradeRow.height);
            if (Widgets.ButtonText(q2, "50")) { tradeAmount = 50; tradeBuffer = "50"; }
            Rect q3 = new Rect(q2.xMax + 5f, tradeRow.y, 50f, tradeRow.height);
            if (Widgets.ButtonText(q3, "100")) { tradeAmount = 100; tradeBuffer = "100"; }

            // 买入按钮 (绿)
            Rect buyBtn = new Rect(q3.xMax + 20f, tradeRow.y, 75f, tradeRow.height);
            int totalBuyCost = Mathf.RoundToInt((float)(tradeAmount * stock.price));
            if (ColoredButton(buyBtn, "RP_W_Stock_Buy".Translate(), new Color(0.15f, 0.55f, 0.2f)))
            {
                if (comp.CloudTreasuryBalance >= totalBuyCost)
                {
                    comp.BuyStock(stock.code, tradeAmount, stock.price);
                    comp.ModifyTreasury(-totalBuyCost, "RP_Reason_StockBuy");
                    Messages.Message("RP_W_Stock_BuyDone".Translate(stock.price.ToString("F2"), tradeAmount, stock.code, totalBuyCost), MessageTypeDefOf.PositiveEvent, false);
                }
                else
                {
                    Messages.Message("RP_W_Stock_BuyNoFunds".Translate(), MessageTypeDefOf.RejectInput, false);
                }
            }

            // 卖出按钮 (红)
            Rect sellBtn = new Rect(buyBtn.xMax + 8f, tradeRow.y, 75f, tradeRow.height);
            int held = comp.GetStockHeld(stock.code);
            int totalSellRevenue = Mathf.RoundToInt((float)(tradeAmount * stock.price));
            if (ColoredButton(sellBtn, "RP_W_Stock_Sell".Translate(), new Color(0.55f, 0.15f, 0.15f)))
            {
                if (held >= tradeAmount)
                {
                    comp.SellStock(stock.code, tradeAmount, stock.price);
                    comp.ModifyTreasury(totalSellRevenue, "RP_Reason_StockSell");
                    Messages.Message("RP_W_Stock_SellDone".Translate(stock.price.ToString("F2"), tradeAmount, stock.code, totalSellRevenue), MessageTypeDefOf.PositiveEvent, false);
                }
                else
                {
                    Messages.Message("RP_W_Stock_SellNoHeld".Translate(), MessageTypeDefOf.RejectInput, false);
                }
            }

            // 近期市场动态与异动成因流
            listing.Gap(10f);
            listing.Label("RP_W_Stock_NewsTitle".Translate());
            if (stock.recentNews != null && stock.recentNews.Count > 0)
            {
                Text.Font = GameFont.Tiny;
                foreach (string news in stock.recentNews)
                {
                    Color nColor = news.Contains("[涨]") ? new Color(0.35f, 0.9f, 0.45f) :
                                   news.Contains("[跌]") ? new Color(1f, 0.4f, 0.4f) : new Color(0.85f, 0.85f, 0.85f);
                    listing.Label(news.Colorize(nColor));
                }
                Text.Font = GameFont.Small;
            }
            else if (!string.IsNullOrEmpty(stock.lastHeadline))
            {
                Text.Font = GameFont.Tiny;
                listing.Label(stock.lastHeadline.Translate());
                Text.Font = GameFont.Small;
            }
            else
            {
                Text.Font = GameFont.Tiny;
                GUI.color = Color.gray;
                listing.Label("RP_W_Stock_NoNews".Translate());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

            // 返回按钮
            listing.Gap(12f);
            Rect returnBtn = listing.GetRect(32f);
            Rect retPos = new Rect(returnBtn.xMax - 120f, returnBtn.y, 120f, returnBtn.height);
            if (Widgets.ButtonText(retPos, "RP_W_Stock_Back".Translate()))
            {
                Close();
            }

            listing.End();
        }

        private void DrawStockChart(Rect rect)
        {
            Rect chartArea = rect;
            Widgets.DrawBoxSolid(chartArea, new Color(0.08f, 0.08f, 0.12f, 0.7f));

            if (stock.priceHistory.Count < 2)
            {
                Widgets.Label(chartArea, "RP_W_Stock_NoHistory".Translate());
                return;
            }

            float minPrice = float.MaxValue, maxPrice = float.MinValue;
            foreach (StockBar bar in stock.priceHistory)
            {
                if (bar.low < minPrice) minPrice = bar.low;
                if (bar.high > maxPrice) maxPrice = bar.high;
            }
            if (maxPrice - minPrice < 0.0001f) maxPrice = minPrice + 1f;

            float pad = 12f;
            float chartW = chartArea.width - pad * 2f;
            float chartH = chartArea.height - pad * 2f;

            // 绘制网格线
            GUI.color = new Color(0.2f, 0.2f, 0.25f, 0.4f);
            for (int i = 0; i <= 3; i++)
            {
                float y = chartArea.y + pad + chartH * i / 3f;
                Widgets.DrawLineHorizontal(y, chartArea.x + pad, chartArea.x + pad + chartW);
            }
            GUI.color = Color.white;

            // K线蜡烛
            int count = stock.priceHistory.Count;
            float stepX = chartW / (count - 1);
            float bodyW = Mathf.Max(4f, stepX * 0.65f);

            for (int i = 0; i < count; i++)
            {
                StockBar bar = stock.priceHistory[i];

                float x = chartArea.x + pad + stepX * i;
                float yOpen = chartArea.y + pad + chartH * (1f - (bar.open - minPrice) / (maxPrice - minPrice));
                float yClose = chartArea.y + pad + chartH * (1f - (bar.close - minPrice) / (maxPrice - minPrice));
                float yHigh = chartArea.y + pad + chartH * (1f - (bar.high - minPrice) / (maxPrice - minPrice));
                float yLow = chartArea.y + pad + chartH * (1f - (bar.low - minPrice) / (maxPrice - minPrice));

                bool isBull = bar.close >= bar.open;
                Color candleColor = isBull ? new Color(0.15f, 0.75f, 0.25f) : new Color(0.8f, 0.2f, 0.2f);

                Widgets.DrawLine(new Vector2(x, yHigh), new Vector2(x, yLow), candleColor, 1.2f);

                float topY = Mathf.Min(yOpen, yClose);
                float bottomY = Mathf.Max(yOpen, yClose);
                float bodyH = Mathf.Max(1.5f, bottomY - topY);

                Rect bodyRect = new Rect(x - bodyW / 2f, topY, bodyW, bodyH);
                Widgets.DrawBoxSolid(bodyRect, candleColor);
            }

            // 价格标注
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(chartArea.x + 6f, chartArea.y + 4f, 120f, 14f), "RP_W_Stock_ChartHigh".Translate(maxPrice.ToString("F2")));
            Widgets.Label(new Rect(chartArea.x + 6f, chartArea.y + chartArea.height - 18f, 120f, 14f), "RP_W_Stock_ChartLow".Translate(minPrice.ToString("F2")));
            GUI.color = Color.white;
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
            {
                Event.current.Use();
                return true;
            }
            return false;
        }
    }
}
