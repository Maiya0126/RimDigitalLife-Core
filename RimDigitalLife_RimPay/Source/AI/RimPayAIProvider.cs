using System;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RimWorld;
using UnityEngine;
using Verse;
using UnityEngine.Networking;

namespace RimDigitalLife_RimPay
{
    // ============================================================
    // RimPay AI 请求执行体 (多供应商配置 + 顺序 failover)
    // 参考 RimTuber 的 AIProviderManager 架构：
    // - 多个供应商配置（勾选启用），按列表顺序从上到下尝试
    // - 当前配置失败/无额度 → 自动切换下一个
    // - 全部失败 → 白字日志 + 冷却 + 回退本地随机
    // ============================================================
    public static class RimPayAIProvider
    {
        // 每日经济倍率结果（供主线程读取）
        public static float salaryMultiplier = 1f;
        public static float rentMultiplier = 1f;
        public static float interestRateDelta = 0f;   // 利率增量（百分比点）
        public static float loanRateMultiplier = 1f;  // 借贷利率倍率
        public static float mealFeeMultiplier = 1f;   // 餐费倍率
        public static float lootShareDelta = 0f;      // 搜刮分成增量（如 -0.15 ~ 0.15）
        public static string marketTone = "neutral";
        public static float tradePriceMultiplier = 1f; // 交易价格倍率（由 marketTone 派生，牛市溢价/熊市压价）
        public static string macroEvent = "";          // 宏观事件 key（market_crash/gold_rush/shortage/tech_boom/""=无）
        public static string eventText = "";
        public static bool hasAIResult = false;
        public static int lastRequestDay = -1;

        // AI 结果归档（最近 N 次成功记录，供状态面板回看 AI 掌控痕迹；仅内存，读档后从新积累）
        public class EconomySnapshot
        {
            public string dayLabel = "";
            public float salaryMult, rentMult, loanMult, mealMult, interestDelta, tradeMult, lootDelta;
            public string tone = "", macro = "", evt = "";
        }
        private static readonly List<EconomySnapshot> historyArchive = new List<EconomySnapshot>();
        private const int MaxArchiveCount = 10;
        public static IReadOnlyList<EconomySnapshot> HistoryArchive => historyArchive;

        private static bool requestInFlight = false;
        private static int consecutiveFailures = 0;
        private static float failureCooldownEnd = 0f;
        private static float lastRequestTime = 0f;
        private static string lastHttpErrorDetail = "";
        private static int lastNotifiedFailures = 0;

        public static bool IsInCooldown => RimPayMod.settings?.enableEconomyAI == true
            && consecutiveFailures >= 3 && Time.time < failureCooldownEnd;

        // 供设置页 AI 状态面板展示的只读状态
        public static int ConsecutiveFailures => consecutiveFailures;
        public static bool IsCooldownActive => consecutiveFailures >= 3 && Time.time < failureCooldownEnd;
        public static bool IsRequestInFlight => requestInFlight;
        public static string LastHttpErrorDetail => lastHttpErrorDetail;
        public static int LastRequestDay => lastRequestDay;

        // 设置页「立即请求」按钮：绕过当日一次 / 最小间隔 / 冷却限制，强制立刻请求（异步）
        public static void ForceRequestNow()
        {
            var S = RimPayMod.settings;
            if (S == null || !S.enableEconomyAI)
            {
                Messages.Message("RP_AI_ErrNotEnabled".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (S.GetEnabledConfigs().Count == 0)
            {
                Messages.Message("RP_AI_ErrNoConfig".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (requestInFlight)
            {
                Messages.Message("RP_AI_ErrInFlight".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            lastRequestDay = -1;
            lastRequestTime = -99999f;
            failureCooldownEnd = 0f;
            consecutiveFailures = 0;
            lastNotifiedFailures = 0;

            RequestDailyEconomyIfNeeded();
            Messages.Message("RP_AI_ForceRequested".Translate(), MessageTypeDefOf.NeutralEvent, false);
        }

        // 每日结算调用：若开启 AI 且到达间隔，异步尝试所有已启用的供应商配置
        public static void RequestDailyEconomyIfNeeded()
        {
            var S = RimPayMod.settings;
            if (S == null || !S.enableEconomyAI) return;
            if (S.GetEnabledConfigs().Count == 0) return;

            // 间隔驱动：达到最小刷新间隔即可重新请求（不再限每日一次，由刷新间隔控制频率）
            if (requestInFlight) return;
            if (IsInCooldown) return;

            // 间隔控制（小时 → 秒）
            float minInterval = Mathf.Max(2f, (S.aiRefreshIntervalHours > 0 ? S.aiRefreshIntervalHours : 12) * 3600f);
            if (Time.time - lastRequestTime < minInterval) return;

            // 主菜单/无地图时（如从设置页"立即请求"）DayOfYear 不可用，记 -1 占位，请求照常发起
            // 日期基准统一基地 tile（与发薪/日报一致，避免任务地图经度导致"请求日"显示错位）
            Map requestMap = Find.AnyPlayerHomeMap ?? Find.CurrentMap;
            lastRequestDay = requestMap != null ? GenLocalDate.DayOfYear(requestMap) : -1;
            requestInFlight = true;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var enabled = S.GetEnabledConfigs();
                    bool success = false;
                    string lastError = "";

                    // 按列表顺序从上到下尝试，失败自动切下一个
                    for (int i = 0; i < enabled.Count; i++)
                    {
                        var cfg = enabled[i];
                        string endpoint = cfg.GetEffectiveEndpoint();
                        string model = cfg.GetEffectiveModel();
                        string key = cfg.GetEffectiveKey();
                        if (string.IsNullOrEmpty(endpoint)) continue;

                        string rawResponse = SendRequest(cfg, endpoint, model, key);
                        if (!string.IsNullOrEmpty(rawResponse) && ParseDailyEconomy(rawResponse))
                        {
                            success = true;
                            lastHttpErrorDetail = "";
                            Verse.Log.Message($"[RimPay AI] Provider[{i+1}/{enabled.Count}] {cfg.DisplayLabel} succeeded");
                            break;
                        }
                        else
                        {
                            lastError = lastHttpErrorDetail;
                            Verse.Log.Message($"[RimPay AI] Provider[{i+1}/{enabled.Count}] {cfg.DisplayLabel} failed ({lastError}), trying next");
                        }
                    }

                    if (success)
                    {
                        hasAIResult = true;
                        consecutiveFailures = 0;
                        failureCooldownEnd = 0f;
                        lastNotifiedFailures = 0;
                        requestInFlight = false;
                        lastRequestTime = Time.time;

                        // 归档本次结果（最新在前，最多保留 10 条）
                        try
                        {
                            Map snapMap = Find.AnyPlayerHomeMap ?? Find.CurrentMap;
                            var snap = new EconomySnapshot
                            {
                                dayLabel = snapMap != null ? "RP_AI_SnapshotDay".Translate(GenLocalDate.Year(snapMap), GenLocalDate.DayOfYear(snapMap)) : "RP_AI_SnapshotUnknownDay".Translate(),
                                salaryMult = salaryMultiplier,
                                rentMult = rentMultiplier,
                                loanMult = loanRateMultiplier,
                                mealMult = mealFeeMultiplier,
                                interestDelta = interestRateDelta,
                                tradeMult = tradePriceMultiplier,
                                lootDelta = lootShareDelta,
                                tone = marketTone,
                                macro = string.IsNullOrEmpty(macroEvent) ? (string)"RP_AI_MacroNone".Translate() : macroEvent,
                                evt = eventText
                            };
                            historyArchive.Insert(0, snap);
                            while (historyArchive.Count > MaxArchiveCount) historyArchive.RemoveAt(historyArchive.Count - 1);
                        }
                        catch { /* 归档失败不影响主流程 */ }

                        if (!string.IsNullOrEmpty(eventText))
                        {
                            Verse.Log.Message($"[RimPay AI] Economy refreshed | marketTone={marketTone} | macroEvent={macroEvent} | event: {eventText}");
                        }
                        return;
                    }

                    // 全部失败：白字日志 + 冷却 + 回退默认倍率（1x）
                    consecutiveFailures++;
                    requestInFlight = false;
                    lastRequestTime = Time.time;
                    hasAIResult = false;
                    salaryMultiplier = 1f;
                    rentMultiplier = 1f;
                    interestRateDelta = 0f;
                    loanRateMultiplier = 1f;
                    mealFeeMultiplier = 1f;
                    lootShareDelta = 0f;
                    marketTone = "neutral";
                    tradePriceMultiplier = 1f;
                    macroEvent = "";
                    eventText = "";

                    if (consecutiveFailures >= 3)
                    {
                        float cd = Mathf.Min(15f * consecutiveFailures, 120f);
                        failureCooldownEnd = Time.time + cd;
                    }

                    string failMsg = $"[RimPay AI] All configs failed #{consecutiveFailures} | last error: {lastError}";
                    Verse.Log.Message(failMsg);

                    if (consecutiveFailures != lastNotifiedFailures)
                    {
                        lastNotifiedFailures = consecutiveFailures;
                        float remaining = Mathf.Max(0f, failureCooldownEnd - Time.time);
                        string notify = consecutiveFailures >= 3
                            ? (string)"RP_AI_FailCooldown".Translate(consecutiveFailures, Mathf.RoundToInt(remaining))
                            : (string)"RP_AI_Fail".Translate(consecutiveFailures);
                        Messages.Message(notify,
                            consecutiveFailures >= 3 ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.NeutralEvent,
                            false);
                    }
                }
                catch (Exception ex)
                {
                    requestInFlight = false;
                    lastRequestTime = Time.time;
                    consecutiveFailures++;
                    Verse.Log.Message($"[RimPay AI] Request exception: {ex.Message}\n{ex.StackTrace}");
                }
            });
        }

        // ========================= 单次请求 =========================
        private static string SendRequest(RimPayProviderConfig cfg, string endpoint, string model, string apiKey)
        {
            bool needKey = cfg.provider.GetRequiresApiKey();
            if (needKey && string.IsNullOrEmpty(apiKey)) return null;

            string sysPrompt = "You are the RimPay central bank AI for a RimWorld colony economy. "
                + "Output ONLY a compact JSON object with this exact structure: "
                + "{\"salaryMultiplier\": 0.8~1.3, \"rentMultiplier\": 0.8~1.2, "
                + "\"interestRateDelta\": -1.0~1.0, \"marketTone\": \"bull|bear|neutral\", "
                + "\"eventText\": \"one short sentence (<=60 chars) describing today's economy event\"}. "
                + "Do not add any other text, markdown, or explanation.";

            string template = RimPayMod.settings?.economyJsonTemplate ?? "";
            string userPrompt = string.IsNullOrEmpty(template)
                ? "Generate today's colony economy variables as JSON."
                : "Here is the template to follow:\n" + template;

            string jsonBody = BuildRequestBody(model, sysPrompt, userPrompt);
            if (string.IsNullOrEmpty(jsonBody)) return null;

            try
            {
                using (var request = new UnityWebRequest(endpoint, "POST"))
                {
                    byte[] body = Encoding.UTF8.GetBytes(jsonBody);
                    request.uploadHandler = new UploadHandlerRaw(body);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.timeout = 15;
                    if (!string.IsNullOrEmpty(apiKey))
                        request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                    if (cfg.provider == RimPayProvider.Player2)
                        request.SetRequestHeader("player2-game-key", RimPayProviderRegistry.Player2GameClientId);

                    var op = request.SendWebRequest();
                    float startWait = Time.realtimeSinceStartup;
                    while (!op.isDone)
                    {
                        Thread.Sleep(50);
                        if (Time.realtimeSinceStartup - startWait > 20f)
                        {
                            request.Abort();
                            lastHttpErrorDetail = "timeout (20s)";
                            return null;
                        }
                    }

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        long code = request.responseCode;
                        string detail;
                        if (code == 401) detail = "API key invalid";
                        else if (code == 404) detail = "endpoint or model not found";
                        else if (code == 429) detail = "rate limited";
                        else if (code == 402) detail = "insufficient quota";
                        else detail = $"HTTP {code}";
                        lastHttpErrorDetail = detail;
                        return null;
                    }

                    lastHttpErrorDetail = "";
                    return ParseResponse(request.downloadHandler.text);
                }
            }
            catch (Exception ex)
            {
                lastHttpErrorDetail = ex.Message;
                return null;
            }
        }

        // ========================= 设置页「测试连接」 =========================
        // 同步阻塞式（必须在后台线程调用，由 RimPayMod.StartTestConnection 驱动），
        // 返回人类可读结果字符串，供设置页直接显示。
        public static string TestConnection(RimPayProviderConfig cfg)
        {
            if (cfg == null) return "[FAIL] " + "RP_T_NoConfig".Translate();

            string endpoint = cfg.GetEffectiveEndpoint();
            if (string.IsNullOrEmpty(endpoint))
                return "[FAIL] " + "RP_T_NoEndpoint".Translate();

            string apiKey = cfg.GetEffectiveKey();
            if (cfg.provider.GetRequiresApiKey() && string.IsNullOrEmpty(apiKey))
                return "[FAIL] " + "RP_T_NoKey".Translate();

            string model = cfg.GetEffectiveModel();
            if (string.IsNullOrEmpty(model))
                return "[FAIL] " + "RP_T_NoModel".Translate();

            try
            {
                string sys = "You are a test assistant. Reply with exactly: OK";
                string user = "Test connection. Reply OK.";
                string jsonBody = BuildRequestBody(model, sys, user);
                if (string.IsNullOrEmpty(jsonBody)) return "[FAIL] " + "RP_T_NoBody".Translate();

                using (var request = new UnityWebRequest(endpoint, "POST"))
                {
                    byte[] body = Encoding.UTF8.GetBytes(jsonBody);
                    request.uploadHandler = new UploadHandlerRaw(body);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.timeout = 15;
                    if (!string.IsNullOrEmpty(apiKey))
                        request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                    if (cfg.provider == RimPayProvider.Player2)
                        request.SetRequestHeader("player2-game-key", RimPayProviderRegistry.Player2GameClientId);

                    var op = request.SendWebRequest();
                    float startWait = Time.realtimeSinceStartup;
                    while (!op.isDone)
                    {
                        Thread.Sleep(50);
                        if (Time.realtimeSinceStartup - startWait > 20f)
                        {
                            request.Abort();
                            return "[FAIL] " + "RP_T_Timeout".Translate();
                        }
                    }

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        long code = request.responseCode;
                        string errorBody = request.downloadHandler?.text ?? "";
                        string detail = "";
                        if (code == 0)
                        {
                            if (cfg.provider == RimPayProvider.Player2)
                                detail = " — " + "RP_T_ErrPlayer2".Translate();
                            else if (cfg.provider == RimPayProvider.Local)
                                detail = " — " + "RP_T_ErrLocal".Translate();
                            else
                                detail = " — " + "RP_T_ErrNoConn".Translate();
                        }
                        else if (code == 400) detail = " — " + "RP_T_Err400".Translate();
                        else if (code == 401) detail = " — " + "RP_T_Err401".Translate();
                        else if (code == 403) detail = " — " + "RP_T_Err403".Translate();
                        else if (code == 404) detail = " — " + "RP_T_Err404".Translate();
                        else if (code == 429) detail = " — " + "RP_T_Err429".Translate();
                        else if (code >= 500) detail = " — " + "RP_T_Err5xx".Translate();

                        string errorHint = "";
                        if (!string.IsNullOrEmpty(errorBody) && errorBody.Length < 200)
                            errorHint = "\n" + "RP_T_ServerResponse".Translate(errorBody);

                        return $"[FAIL] HTTP {code}{detail}{errorHint}";
                    }

                    string response = ParseResponse(request.downloadHandler.text);
                    if (!string.IsNullOrEmpty(response))
                        return "[OK] " + "RP_T_Ok".Translate(model, response);

                    string rawPreview = request.downloadHandler.text;
                    if (rawPreview.Length > 300) rawPreview = rawPreview.Substring(0, 300) + "...";
                    return "[OK] " + "RP_T_ParseFail".Translate(rawPreview);
                }
            }
            catch (Exception ex)
            {
                string exType = ex.GetType().Name;
                if (exType.Contains("Timeout") || exType.Contains("Socket"))
                    return "[FAIL] " + "RP_T_NetworkError".Translate(ex.Message);
                return "[FAIL] " + "RP_T_Exception".Translate(exType, ex.Message);
            }
        }

        private static string BuildRequestBody(string model, string sys, string user)
        {
            string escSys = EscapeJson(sys);
            string escUser = EscapeJson(user);
            string escModel = EscapeJson(model);
            return $"{{\"model\":\"{escModel}\",\"messages\":[{{\"role\":\"system\",\"content\":\"{escSys}\"}},{{\"role\":\"user\",\"content\":\"{escUser}\"}}],\"max_tokens\":300,\"temperature\":0.8}}";
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "").Replace("\t", "\\t");
        }

        // 提取 assistant 的纯文本内容
        private static string ParseResponse(string responseText)
        {
            if (string.IsNullOrEmpty(responseText)) return null;
            try
            {
                var matches = Regex.Matches(responseText, "\"(content|text)\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
                string best = null;
                foreach (Match m in matches)
                {
                    string val = m.Groups[2].Value;
                    if (string.IsNullOrEmpty(val)) continue;
                    best = val;
                }
                if (best != null)
                {
                    return best.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\t", "\t");
                }
            }
            catch { }
            return null;
        }

        // 解析每日经济 JSON（宽松解析）
        private static bool ParseDailyEconomy(string raw)
        {
            try
            {
                float fSalary = GetJsonFloat(raw, "salaryMultiplier", 1f);
                float fRent = GetJsonFloat(raw, "rentMultiplier", 1f);
                float fInterest = GetJsonFloat(raw, "interestRateDelta", 0f);
                float fLoan = GetJsonFloat(raw, "loanRateMultiplier", 1f);
                float fMeal = GetJsonFloat(raw, "mealFeeMultiplier", 1f);
                float fLoot = GetJsonFloat(raw, "lootShareDelta", 0f);
                float fTrade = GetJsonFloat(raw, "tradePriceMultiplier", 0f); // 0=未指定，用 marketTone 派生
                string tone = GetJsonString(raw, "marketTone", "neutral");
                string macro = GetJsonString(raw, "macroEvent", "");
                string evt = GetJsonString(raw, "eventText", "");

                salaryMultiplier = Mathf.Clamp(fSalary, 0.5f, 2f);
                rentMultiplier = Mathf.Clamp(fRent, 0.5f, 2f);
                interestRateDelta = Mathf.Clamp(fInterest, -2f, 2f);
                loanRateMultiplier = Mathf.Clamp(fLoan, 0.8f, 1.5f);
                mealFeeMultiplier = Mathf.Clamp(fMeal, 0.8f, 1.5f);
                lootShareDelta = Mathf.Clamp(fLoot, -0.15f, 0.15f);
                marketTone = tone;
                macroEvent = macro;
                eventText = evt;

                // 交易价格倍率：AI 显式给出则用，否则按市场情绪派生
                tradePriceMultiplier = fTrade > 0f ? Mathf.Clamp(fTrade, 0.8f, 1.2f) : GetTradeMultiplierFromTone(tone);
                return true;
            }
            catch (Exception ex)
            {
                Verse.Log.Message($"[RimPay AI] JSON parse failed: {ex.Message}");
                return false;
            }
        }

        private static float GetTradeMultiplierFromTone(string tone)
        {
            if (tone == "bull") return 1.1f;
            if (tone == "bear") return 0.9f;
            return 1f;
        }

        private static float GetJsonFloat(string json, string key, float fallback)
        {
            var m = Regex.Match(json, $"\"{key}\"\\s*:\\s*([-0-9.]+)");
            return m.Success && float.TryParse(m.Groups[1].Value, out float v) ? v : fallback;
        }

        private static string GetJsonString(string json, string key, string fallback)
        {
            var m = Regex.Match(json, $"\"{key}\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
            if (m.Success)
                return m.Groups[1].Value.Replace("\\n", "\n").Replace("\\\"", "\"");
            return fallback;
        }
    }
}