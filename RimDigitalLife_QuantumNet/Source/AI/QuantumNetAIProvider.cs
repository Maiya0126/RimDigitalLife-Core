using System;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;
using RimWorld;
using UnityEngine;
using Verse;
using UnityEngine.Networking;

namespace RimDigitalLife_QuantumNet
{
    // ============================================================
    // QuantumNet AI 请求执行体 (多供应商配置 + 顺序 failover)
    // 与 RimPayAIProvider 完全同款架构，独立实现：
    // - 多个供应商配置（勾选启用），按列表顺序从上到下尝试
    // - 当前配置失败/无额度 → 自动切换下一个
    // - 全部失败 → 白字日志 + 冷却 + 回退默认值
    // ============================================================
    public static class QuantumNetAIProvider
    {
        // 量子网络每日 AI 结果（供主线程读取）
        public static string networkStatus = "normal";   // normal|congested|outage
        public static string cloudWeatherHint = "";      // 云端天气预测
        public static string deepMineHint = "";          // 深矿雷达预测
        public static string rimseekTip = "";            // RimSeek 智算建议
        public static string newsText = "";              // 量子网络新闻
        public static bool hasAIResult = false;
        public static int lastRequestDay = -1;

        private static bool requestInFlight = false;
        private static int consecutiveFailures = 0;
        private static float failureCooldownEnd = 0f;
        private static float lastRequestTime = 0f;
        private static string lastHttpErrorDetail = "";
        private static int lastNotifiedFailures = 0;

        public static bool IsInCooldown => QuantumNetMod.settings?.enableNetworkAI == true
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
            var S = QuantumNetMod.settings;
            if (S == null || !S.enableNetworkAI)
            {
                Messages.Message("[QuantumNet AI] " + "QN_AI_NeedEnable".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (S.GetEnabledConfigs().Count == 0)
            {
                Messages.Message("[QuantumNet AI] " + "QN_AI_NoConfig".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (requestInFlight)
            {
                Messages.Message("[QuantumNet AI] " + "QN_AI_InFlight".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            lastRequestDay = -1;
            lastRequestTime = -99999f;
            failureCooldownEnd = 0f;
            consecutiveFailures = 0;
            lastNotifiedFailures = 0;

            RequestDailyNetworkIfNeeded();
            Messages.Message("[QuantumNet AI] " + "QN_AI_Forced".Translate(), MessageTypeDefOf.NeutralEvent, false);
        }

        // 每日调用：若开启 AI 且到达间隔，异步尝试所有已启用的供应商配置
        public static void RequestDailyNetworkIfNeeded()
        {
            var S = QuantumNetMod.settings;
            if (S == null || !S.enableNetworkAI) return;
            if (S.GetEnabledConfigs().Count == 0) return;

            // 同一天只请求一次
            int day = GenLocalDate.DayOfYear(Find.CurrentMap ?? Find.AnyPlayerHomeMap);
            if (lastRequestDay == day) return;
            if (requestInFlight) return;
            if (IsInCooldown) return;

            // 间隔控制（小时 → 秒）
            float minInterval = Mathf.Max(2f, (S.aiRequestIntervalHours > 0 ? S.aiRequestIntervalHours : 24) * 3600f);
            if (Time.time - lastRequestTime < minInterval) return;

            lastRequestDay = day;
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
                        if (!string.IsNullOrEmpty(rawResponse) && ParseNetworkData(rawResponse))
                        {
                            success = true;
                            lastHttpErrorDetail = "";
                            Verse.Log.Message($"[QuantumNet AI] provider[{i + 1}/{enabled.Count}] {cfg.DisplayLabel} OK");
                            break;
                        }
                        else
                        {
                            lastError = lastHttpErrorDetail;
                            Verse.Log.Message($"[QuantumNet AI] provider[{i + 1}/{enabled.Count}] {cfg.DisplayLabel} failed ({lastError}), trying next");
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
                        InjectProviderNews();
                        if (!string.IsNullOrEmpty(newsText))
                        {
                            Verse.Log.Message($"[QuantumNet AI] network data generated | status={networkStatus} | news: {newsText}");
                        }
                        return;
                    }

                    // 全部失败：白字日志 + 冷却 + 回退默认值
                    consecutiveFailures++;
                    requestInFlight = false;
                    lastRequestTime = Time.time;
                    hasAIResult = false;
                    networkStatus = "normal";
                    cloudWeatherHint = "";
                    deepMineHint = "";
                    rimseekTip = "";
                    newsText = "";

                    if (consecutiveFailures >= 3)
                    {
                        float cd = Mathf.Min(15f * consecutiveFailures, 120f);
                        failureCooldownEnd = Time.time + cd;
                    }

                    bool isCN = LanguageDatabase.activeLanguage?.FriendlyNameEnglish?.Contains("Chinese") ?? false;
                    string failMsg = isCN
                        ? $"[QuantumNet AI] 全部供应商失败 #{consecutiveFailures} | 最后错误: {lastError}"
                        : $"[QuantumNet AI] All configs failed #{consecutiveFailures} | last error: {lastError}";
                    Verse.Log.Message(failMsg);

                    if (consecutiveFailures != lastNotifiedFailures)
                    {
                        lastNotifiedFailures = consecutiveFailures;
                        float remaining = Mathf.Max(0f, failureCooldownEnd - Time.time);
                        string notify = isCN
                            ? (consecutiveFailures >= 3
                                ? $"[QuantumNet AI] 连续失败{consecutiveFailures}次，进入冷却({remaining:F0}s)，网络数据回退默认"
                                : $"[QuantumNet AI] 请求失败({consecutiveFailures}次)，网络数据回退默认")
                            : (consecutiveFailures >= 3
                                ? $"[QuantumNet AI] Failed {consecutiveFailures}x, cooldown {remaining:F0}s, network data fallback"
                                : $"[QuantumNet AI] Request failed ({consecutiveFailures}), network data fallback");
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
                    Verse.Log.Message($"[QuantumNet AI] exception: {ex.Message}\n{ex.StackTrace}");
                }
            });
        }

        // ========================= 单次请求 =========================
        private static string SendRequest(QuantumNetProviderConfig cfg, string endpoint, string model, string apiKey)
        {
            bool needKey = cfg.provider.GetRequiresApiKey();
            if (needKey && string.IsNullOrEmpty(apiKey)) return null;

            string sysPrompt = "You are the Quantum Net AI for a RimWorld colony. "
                + "Output ONLY a compact JSON object with this exact structure: "
                + "{\"networkStatus\": \"normal|congested|outage\", "
                + "\"cloudWeatherHint\": \"one short weather forecast sentence (<=40 chars)\", "
                + "\"deepMineHint\": \"one short deep mine radar prediction sentence (<=40 chars)\", "
                + "\"rimseekTip\": \"one short AI work suggestion sentence (<=40 chars)\", "
                + "\"newsText\": \"one short sentence (<=60 chars) describing today's quantum network event\"}. "
                + "Do not add any other text, markdown, or explanation.";

            string template = QuantumNetMod.settings?.networkJsonTemplate ?? "";
            string userPrompt = string.IsNullOrEmpty(template)
                ? "Generate today's quantum network data as JSON."
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
                    if (cfg.provider == QuantumNetProvider.Player2)
                        request.SetRequestHeader("player2-game-key", QuantumNetProviderRegistry.Player2GameClientId);

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
        // 同步阻塞式（必须在后台线程调用，由 QuantumNetMod.StartTestConnection 驱动）
        public static string TestConnection(QuantumNetProviderConfig cfg)
        {
            if (cfg == null) return "[FAIL] " + "QN_AI_E_ConfigEmpty".Translate();

            string endpoint = cfg.GetEffectiveEndpoint();
            if (string.IsNullOrEmpty(endpoint))
                return "[FAIL] " + "QN_AI_E_EndpointEmpty".Translate();

            string apiKey = cfg.GetEffectiveKey();
            if (cfg.provider.GetRequiresApiKey() && string.IsNullOrEmpty(apiKey))
                return "[FAIL] " + "QN_AI_E_KeyEmpty".Translate();

            string model = cfg.GetEffectiveModel();
            if (string.IsNullOrEmpty(model))
                return "[FAIL] " + "QN_AI_E_ModelEmpty".Translate();

            try
            {
                string sys = "You are a test assistant. Reply with exactly: OK";
                string user = "Test connection. Reply OK.";
                string jsonBody = BuildRequestBody(model, sys, user);
                if (string.IsNullOrEmpty(jsonBody)) return "[FAIL] " + "QN_AI_E_NoBody".Translate();

                using (var request = new UnityWebRequest(endpoint, "POST"))
                {
                    byte[] body = Encoding.UTF8.GetBytes(jsonBody);
                    request.uploadHandler = new UploadHandlerRaw(body);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.timeout = 15;
                    if (!string.IsNullOrEmpty(apiKey))
                        request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                    if (cfg.provider == QuantumNetProvider.Player2)
                        request.SetRequestHeader("player2-game-key", QuantumNetProviderRegistry.Player2GameClientId);

                    var op = request.SendWebRequest();
                    float startWait = Time.realtimeSinceStartup;
                    while (!op.isDone)
                    {
                        Thread.Sleep(50);
                        if (Time.realtimeSinceStartup - startWait > 20f)
                        {
                            request.Abort();
                            return "[FAIL] " + "QN_AI_E_Timeout".Translate();
                        }
                    }

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        long code = request.responseCode;
                        string errorBody = request.downloadHandler?.text ?? "";
                        string detail = "";
                        if (code == 0)
                        {
                            if (cfg.provider == QuantumNetProvider.Player2)
                                detail = " — " + "QN_AI_E_Player2".Translate();
                            else if (cfg.provider == QuantumNetProvider.Local)
                                detail = " — " + "QN_AI_E_LocalModel".Translate();
                            else
                                detail = " — " + "QN_AI_E_NoConn".Translate();
                        }
                        else if (code == 400) detail = " — " + "QN_AI_E_BadRequest".Translate();
                        else if (code == 401) detail = " — " + "QN_AI_E_Auth".Translate();
                        else if (code == 403) detail = " — " + "QN_AI_E_Forbidden".Translate();
                        else if (code == 404) detail = " — " + "QN_AI_E_NotFound".Translate();
                        else if (code == 429) detail = " — " + "QN_AI_E_RateLimit".Translate();
                        else if (code >= 500) detail = " — " + "QN_AI_E_Server".Translate();

                        string errorHint = "";
                        if (!string.IsNullOrEmpty(errorBody) && errorBody.Length < 200)
                            errorHint = "\n" + "QN_AI_E_Body".Translate(errorBody);

                        return $"[FAIL] HTTP {code}{detail}{errorHint}";
                    }

                    string response = ParseResponse(request.downloadHandler.text);
                    if (!string.IsNullOrEmpty(response))
                        return "[OK] " + "QN_AI_T_Connected".Translate(model, response);

                    string rawPreview = request.downloadHandler.text;
                    if (rawPreview.Length > 300) rawPreview = rawPreview.Substring(0, 300) + "...";
                    return "[OK] " + "QN_AI_T_ParseFail".Translate(rawPreview);
                }
            }
            catch (Exception ex)
            {
                string exType = ex.GetType().Name;
                if (exType.Contains("Timeout") || exType.Contains("Socket"))
                    return "[FAIL] " + "QN_AI_E_Network".Translate(ex.Message);
                return "[FAIL] " + "QN_AI_E_Exception".Translate(exType, ex.Message);
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

        // 解析量子网络 JSON（宽松解析）
        private static bool ParseNetworkData(string raw)
        {
            try
            {
                string status = GetJsonString(raw, "networkStatus", "normal");
                string weather = GetJsonString(raw, "cloudWeatherHint", "");
                string mine = GetJsonString(raw, "deepMineHint", "");
                string tip = GetJsonString(raw, "rimseekTip", "");
                string news = GetJsonString(raw, "newsText", "");

                if (status != "normal" && status != "congested" && status != "outage")
                    status = "normal";

                networkStatus = status;
                cloudWeatherHint = weather;
                deepMineHint = mine;
                rimseekTip = tip;
                newsText = news;
                return true;
            }
            catch (Exception ex)
            {
                Verse.Log.Message($"[QuantumNet AI] JSON parse failed: {ex.Message}");
                return false;
            }
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

        // 把虚拟供应商剧情注入到当日新闻（随机提及一家服务商）
        private static void InjectProviderNews()
        {
            var provider = QuantumNetProviders.RandomProvider();
            if (provider == null) return;
            string prefix = provider.name.Translate() + ": " + provider.role.Translate();
            if (string.IsNullOrEmpty(newsText))
            {
                newsText = prefix + " " + "QN_AI_NewsSuffix".Translate();
            }
            else
            {
                newsText = newsText + " " + "QN_AI_RelatedPrefix".Translate(prefix);
            }
        }
    }
}
