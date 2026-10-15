using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimDigitalLife_RimPay
{
    // ========================================================================
    // RimPay 股市对外公开只读 API（供 RimTuber、QuantumNet 等跨模组联动调用）
    // 完全解耦，支持直接引用或反射调用，无异常安全保障
    // ========================================================================
    public static class RimPayStockAPI
    {
        public static bool IsAvailable()
        {
            return Current.Game?.GetComponent<GameComponent_RimPay>() != null;
        }

        // 大盘市场基调：bull (牛市), bear (熊市), neutral (中性)
        public static string GetMarketTone()
        {
            return RimPayAIProvider.marketTone ?? "neutral";
        }

        // 当前宏观事件标签（如 "市场崩盘"、"科技繁荣"），无则返回空
        public static string GetActiveMacroEvent()
        {
            var comp = Current.Game?.GetComponent<GameComponent_RimPay>();
            return comp?.MacroEventLabel ?? "";
        }

        // 获取领涨股票摘要（如 "$GLTR (闪耀制药) +14.2%"）
        public static string GetTopGainerSummary()
        {
            var comp = Current.Game?.GetComponent<GameComponent_RimPay>();
            var stocks = comp?.GetStockPrices();
            if (stocks == null || stocks.Count == 0) return "";
            var top = stocks.OrderByDescending(s => s.changePercent).FirstOrDefault();
            if (top == null || top.changePercent <= 0) return "";
            return $"{top.code} ({top.name}) +{top.changePercent:F1}%";
        }

        // 获取领跌股票摘要（如 "$SCOM (星链通信) -15.0%"）
        public static string GetTopLoserSummary()
        {
            var comp = Current.Game?.GetComponent<GameComponent_RimPay>();
            var stocks = comp?.GetStockPrices();
            if (stocks == null || stocks.Count == 0) return "";
            var worst = stocks.OrderBy(s => s.changePercent).FirstOrDefault();
            if (worst == null || worst.changePercent >= 0) return "";
            return $"{worst.code} ({worst.name}) {worst.changePercent:F1}%";
        }

        // 今日大盘一句话速报（供弹幕、量子网络日报引用）
        public static string GetMarketBriefSummary()
        {
            var comp = Current.Game?.GetComponent<GameComponent_RimPay>();
            if (comp == null) return "";
            var stocks = comp.GetStockPrices();
            if (stocks == null || stocks.Count == 0) return "";

            string gainer = GetTopGainerSummary();
            string loser = GetTopLoserSummary();
            string tone = GetMarketTone();
            string toneDesc = tone == "bull" ? "大盘普涨" : tone == "bear" ? "大盘承压" : "大盘震荡";

            if (!string.IsNullOrEmpty(gainer) && !string.IsNullOrEmpty(loser))
                return $"{toneDesc}：领涨 {gainer}，领跌 {loser}";
            if (!string.IsNullOrEmpty(gainer))
                return $"{toneDesc}：领涨 {gainer}";
            if (!string.IsNullOrEmpty(loser))
                return $"{toneDesc}：领跌 {loser}";
            return toneDesc;
        }

        // 获取特定股票的当前价格与涨幅
        public static bool TryGetStockInfo(string code, out string name, out double price, out double changePercent)
        {
            name = "";
            price = 0;
            changePercent = 0;
            var comp = Current.Game?.GetComponent<GameComponent_RimPay>();
            var s = comp?.GetStockPrices()?.FirstOrDefault(x => x.code == code);
            if (s == null) return false;
            name = s.name;
            price = s.price;
            changePercent = s.changePercent;
            return true;
        }

        // 获取特定行业的领头股票涨幅（如 "Tech", "Medical", "Arms", "Quantum" 等）
        public static string GetCategoryMovement(string category)
        {
            var comp = Current.Game?.GetComponent<GameComponent_RimPay>();
            var stocks = comp?.GetStockPrices()?.Where(s => s.category == category).ToList();
            if (stocks == null || stocks.Count == 0) return "";
            var best = stocks.OrderByDescending(s => s.changePercent).FirstOrDefault();
            if (best == null) return "";
            string sign = best.changePercent >= 0 ? "+" : "";
            return $"{best.name} {sign}{best.changePercent:F1}%";
        }
    }
}
