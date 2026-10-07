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
                Messages.Message("[RimPay AI] 请先在 AI 设置页勾选「启用 AI 控制经济」后再试。", MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (S.GetEnabledConfigs().Count == 0)
            {
                Messages.Message("[RimPay AI] 未勾选任何供应商配置，无法请求。请先在下方勾选一个配置。", MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (requestInFlight)
            {
                Messages.Message("[RimPay AI] 已有请求进行中，请稍候...", MessageTypeDefOf.RejectInput, false);
                return;
            }

            lastRequestDay = -1;
            lastRequestTime = -99999f;
            failureCooldownEnd = 0f;
            consecutiveFailures = 0;
            lastNotifiedFailures = 0;

            RequestDailyEconomyIfNeeded();
            Messages.Message("[RimPay AI] 已发起强制请求，结果见上方状态面板与日志。", MessageTypeDefOf.NeutralEvent, false);
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
                            Verse.Log.Message($"[RimPay AI] 供应商[{i+1}/{enabled.Count}] {cfg.DisplayLabel} 成功");
                            break;
                        }
                        else
                        {
                            lastError = lastHttpErrorDetail;
                            Verse.Log.Message($"[RimPay AI] 供应商[{i+1}/{enabled.Count}] {cfg.DisplayLabel} 失败 ({lastError})，尝试下一个");
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
                                dayLabel = snapMap != null ? $"第{GenLocalDate.Year(snapMap)}.{GenLocalDate.DayOfYear(snapMap)}日" : "未知日期",
                                salaryMult = salaryMultiplier,
                                rentMult = rentMultiplier,
                                loanMult = loanRateMultiplier,
                                mealMult = mealFeeMultiplier,
                                interestDelta = interestRateDelta,
                                tradeMult = tradePriceMultiplier,
                                lootDelta = lootShareDelta,
                                tone = marketTone,
                                macro = string.IsNullOrEmpty(macroEvent) ? "无" : macroEvent,
                                evt = eventText
                            };
                            historyArchive.Insert(0, snap);
                            while (historyArchive.Count > MaxArchiveCount) historyArchive.RemoveAt(historyArchive.Count - 1);
                        }
                        catch { /* 归档失败不影响主流程 */ }

                        if (!string.IsNullOrEmpty(eventText))
                        {
                            Verse.Log.Message($"[RimPay AI] 经济变量已刷新 | marketTone={marketTone} | 宏观事件={macroEvent} | 事件: {eventText}");
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

                    bool isCN = LanguageDatabase.activeLanguage?.FriendlyNameEnglish?.Contains("Chinese") ?? false;
                    string failMsg = isCN
                        ? $"[RimPay AI] 全部供应商失败 #{consecutiveFailures} | 最后错误: {lastError}"
                        : $"[RimPay AI] All configs failed #{consecutiveFailures} | last error: {lastError}";
                    Verse.Log.Message(failMsg);

                    if (consecutiveFailures != lastNotifiedFailures)
                    {
                        lastNotifiedFailures = consecutiveFailures;
                        float remaining = Mathf.Max(0f, failureCooldownEnd - Time.time);
                        string notify = isCN
                            ? (consecutiveFailures >= 3
                                ? $"[RimPay AI] 连续失败{consecutiveFailures}次，进入冷却({remaining:F0}s)，经济回退本地随机"
                                : $"[RimPay AI] 请求失败({consecutiveFailures}次)，经济回退本地随机")
                            : (consecutiveFailures >= 3
                                ? $"[RimPay AI] Failed {consecutiveFailures}x, cooldown {remaining:F0}s, economy fallback to local random"
                                : $"[RimPay AI] Request failed ({consecutiveFailures}), economy fallback to local random");
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
                    Verse.Log.Message($"[RimPay AI] 请求异常: {ex.Message}\n{ex.StackTrace}");
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
            if (cfg == null) return "[FAIL] 配置为空，请先添加并选中一个供应商配置";

            string endpoint = cfg.GetEffectiveEndpoint();
            if (string.IsNullOrEmpty(endpoint))
                return "[FAIL] 端点 URL 为空 - 请填写端点 URL（或选择供应商自动填充）";

            string apiKey = cfg.GetEffectiveKey();
            if (cfg.provider.GetRequiresApiKey() && string.IsNullOrEmpty(apiKey))
                return "[FAIL] API Key 为空 - 该供应商需要 API Key";

            string model = cfg.GetEffectiveModel();
            if (string.IsNullOrEmpty(model))
                return "[FAIL] 模型名称为空 - 请填写模型名称（例如 deepseek-chat / gpt-4o-mini）";

            try
            {
                string sys = "You are a test assistant. Reply with exactly: OK";
                string user = "Test connection. Reply OK.";
                string jsonBody = BuildRequestBody(model, sys, user);
                if (string.IsNullOrEmpty(jsonBody)) return "[FAIL] 无法构造请求体";

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
                            return "[FAIL] 连接超时 (20s) - 可能是网络问题或端点 URL 错误";
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
                                detail = " — Player2 桌面程序未启动或未就绪，请确认 Player2 正在运行后再试";
                            else if (cfg.provider == RimPayProvider.Local)
                                detail = " — 本地模型服务未启动（Ollama / LM Studio 未运行或未监听 11434 端口）";
                            else
                                detail = " — 无法连接服务器，请检查网络与端点 URL";
                        }
                        else if (code == 400) detail = " — 请求格式错误：很可能是模型名称不对，请核对模型名";
                        else if (code == 401) detail = " — 认证失败：API Key 无效或已过期";
                        else if (code == 403) detail = " — 访问被拒绝：API Key 无权限或账户受限";
                        else if (code == 404) detail = " — 端点不存在：端点 URL 可能写错";
                        else if (code == 429) detail = " — 请求过频被限流，请稍后再试";
                        else if (code >= 500) detail = " — 服务器错误：API 供应商暂时故障，稍后再试";

                        string errorHint = "";
                        if (!string.IsNullOrEmpty(errorBody) && errorBody.Length < 200)
                            errorHint = $"\n服务器返回: {errorBody}";

                        return $"[FAIL] HTTP {code}{detail}{errorHint}";
                    }

                    string response = ParseResponse(request.downloadHandler.text);
                    if (!string.IsNullOrEmpty(response))
                        return $"[OK] 连接成功! 模型: {model} | AI 回复: {response}";

                    string rawPreview = request.downloadHandler.text;
                    if (rawPreview.Length > 300) rawPreview = rawPreview.Substring(0, 300) + "...";
                    return $"[OK] 已连接，但响应解析失败 (聊天功能可能仍可用)\n原始响应: {rawPreview}";
                }
            }
            catch (Exception ex)
            {
                string exType = ex.GetType().Name;
                if (exType.Contains("Timeout") || exType.Contains("Socket"))
                    return $"[FAIL] 网络错误: {ex.Message} - 请检查网络连接";
                return $"[FAIL] 异常 ({exType}): {ex.Message}";
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
                Verse.Log.Message($"[RimPay AI] JSON 解析失败: {ex.Message}");
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