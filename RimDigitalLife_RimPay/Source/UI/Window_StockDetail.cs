using System;
using System.Collections.Generic;
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

        public override Vector2 InitialSize => new Vector2(650f, 540f);

        public override void DoWindowContents(Rect inRect)
        {
            if (stock == null || comp == null) return;

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            // 标题及基础信息
            Text.Font = GameFont.Medium;
            string changeStr = stock.changePercent >= 0 ? $"+{stock.changePercent:F1}%" : $"{stock.changePercent:F1}%";
            Color changeColor = stock.changePercent >= 0 ? Color.green : Color.red;
            listing.Label($"{stock.code}  {stock.name}");
            Text.Font = GameFont.Small;

            int heldStock = comp.GetStockHeld(stock.code);
            int stockProfit = comp.GetStockProfit(stock.code);
            string profitStr = stockProfit >= 0 ? $"+{stockProfit}" : $"{stockProfit}";
            Color profitColor = stockProfit >= 0 ? Color.green : Color.red;
            listing.Label($"当前价格: {stock.price:F2} @银  |  今日涨跌: {changeStr.Colorize(changeColor)}  |  国库持仓: {heldStock} 股  |  盈亏: {profitStr.Colorize(profitColor)} @银");
            listing.GapLine();

            // K线蜡烛图 (占大约半个窗口)
            Rect chartRect = listing.GetRect(240f);
            DrawStockChart(chartRect);

            listing.Gap(12f);

            // 交易操作区
            listing.Label($"<b>交易操作:</b> (国库可用余额: {comp.CloudTreasuryBalance} @银)");
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
            if (ColoredButton(buyBtn, "买入", new Color(0.15f, 0.55f, 0.2f)))
            {
                if (comp.CloudTreasuryBalance >= totalBuyCost)
                {
                    comp.BuyStock(stock.code, tradeAmount, stock.price);
                    comp.ModifyTreasury(-totalBuyCost, "股票买入");
                    Messages.Message($"【RimPay证券】成功以每股 {stock.price:F2} @银 的价格购买了 {tradeAmount} 股 {stock.code}，共消耗 {totalBuyCost} @银。", MessageTypeDefOf.PositiveEvent, false);
                }
                else
                {
                    Messages.Message("数字国库资金不足，无法购买该数量的股票！", MessageTypeDefOf.RejectInput, false);
                }
            }

            // 卖出按钮 (红)
            Rect sellBtn = new Rect(buyBtn.xMax + 8f, tradeRow.y, 75f, tradeRow.height);
            int held = comp.GetStockHeld(stock.code);
            int totalSellRevenue = Mathf.RoundToInt((float)(tradeAmount * stock.price));
            if (ColoredButton(sellBtn, "卖出", new Color(0.55f, 0.15f, 0.15f)))
            {
                if (held >= tradeAmount)
                {
                    comp.SellStock(stock.code, tradeAmount, stock.price);
                    comp.ModifyTreasury(totalSellRevenue, "股票卖出");
                    Messages.Message($"【RimPay证券】成功以每股 {stock.price:F2} @银 的价格卖出了 {tradeAmount} 股 {stock.code}，获得 {totalSellRevenue} @银。", MessageTypeDefOf.PositiveEvent, false);
                }
                else
                {
                    Messages.Message("持仓数量不足，无法卖出该数额！", MessageTypeDefOf.RejectInput, false);
                }
            }

            // 返回按钮
            listing.Gap(15f);
            Rect returnBtn = listing.GetRect(32f);
            Rect retPos = new Rect(returnBtn.xMax - 120f, returnBtn.y, 120f, returnBtn.height);
            if (Widgets.ButtonText(retPos, "返回"))
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
                Widgets.Label(chartArea, "暂无足够历史数据。");
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
            Widgets.Label(new Rect(chartArea.x + 6f, chartArea.y + 4f, 120f, 14f), $"最高: {maxPrice:F2}");
            Widgets.Label(new Rect(chartArea.x + 6f, chartArea.y + chartArea.height - 18f, 120f, 14f), $"最低: {minPrice:F2}");
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
