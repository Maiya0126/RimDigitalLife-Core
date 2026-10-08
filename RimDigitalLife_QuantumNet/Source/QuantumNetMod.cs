using System;
using System.Collections.Generic;
using System.Threading;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimDigitalLife_QuantumNet
{
    public class QuantumNetMod : Mod
    {
        public static QuantumNetSettings settings;
        private static Harmony harmony;

        // 模组版本（About.xml / 设置页 / 控制台 同步标注）
        public const string ModVersion = "v0.4.01";

        private string jsonBuffer = "";

        // 设置页分页（顶部 Tab）
        private enum QuantumNetSettingsPage { General, AI }
        private QuantumNetSettingsPage currentPage = QuantumNetSettingsPage.General;
        private Vector2 generalScroll = Vector2.zero;
        private Vector2 aiScroll = Vector2.zero;

        // 测试连接状态
        private bool testInProgress = false;
        private string testResultText = "";
        private QuantumNetProviderConfig testConfigOverride;

        public QuantumNetMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<QuantumNetSettings>();
            jsonBuffer = settings.networkJsonTemplate;

            if (harmony == null)
            {
                harmony = new Harmony("maiya.rimdigitallife.quantumnet");
                harmony.PatchAll();
            }
        }

        public override string SettingsCategory()
        {
            return "Rim Digital Life - Quantum Net Expansion 边缘数码生活：量子网络拓展 (v0.4.01)";
        }

        private int selectedConfigIdx = -1;

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // 顶部 Tab：常规设置 / AI 设置
            Rect tabRect = new Rect(inRect.x, inRect.y, inRect.width, 32f);
            Rect generalTab = new Rect(tabRect.x, tabRect.y, tabRect.width / 2f, tabRect.height);
            Rect aiTab = new Rect(tabRect.x + tabRect.width / 2f, tabRect.y, tabRect.width / 2f, tabRect.height);

            if (currentPage == QuantumNetSettingsPage.General)
                Widgets.DrawHighlight(generalTab);
            else
                Widgets.DrawHighlight(aiTab);

            if (Widgets.ButtonText(generalTab, "常规设置 (General)"))
            {
                currentPage = QuantumNetSettingsPage.General;
            }
            if (Widgets.ButtonText(aiTab, "AI 设置 (AI Network)"))
            {
                currentPage = QuantumNetSettingsPage.AI;
            }

            Rect contentRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 40f);
            if (currentPage == QuantumNetSettingsPage.General)
                DrawGeneralPage(contentRect);
            else
                DrawAIPage(contentRect);
        }

        // ============ 常规设置页（网络覆盖/套餐体系/功能开关） ============
        private void DrawGeneralPage(Rect inRect)
        {
            float viewHeight = 1800f;
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, viewHeight);
            Widgets.BeginScrollView(inRect, ref generalScroll, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            // ============ 量子网络覆盖 ============
            listing.Label("<b>量子网络覆盖 (星链系)</b>");
            listing.GapLine();
            listing.CheckboxLabeled("启用量子网络", ref settings.enableQuantumNet);
            listing.CheckboxLabeled("需要通讯台 (通电通讯台 = 当前地图 100% 覆盖)", ref settings.requireCommsConsole);
            listing.CheckboxLabeled("全图贸易信标 (任意地图只要有一个通电轨道贸易信标，全图物品即可贸易/发射)", ref settings.enableMapWideBeacon);
            listing.Gap(10f);

            // ============ 套餐体系 ============
            listing.Label("<b>流量/算力套餐</b>");
            listing.GapLine();
            listing.Label($"套餐计费周期: {settings.planPeriodDays} 天 (1 象)");
            settings.planPeriodDays = Mathf.RoundToInt(listing.Slider(settings.planPeriodDays, 3f, 60f));
            listing.Label($"高速畅享包: {settings.highSpeedPrice} @银/{settings.planPeriodDays}天");
            settings.highSpeedPrice = Mathf.RoundToInt(listing.Slider(settings.highSpeedPrice, 10f, 500f));
            listing.Label($"量子无限包: {settings.quantumInfinitePrice} @银/{settings.planPeriodDays}天");
            settings.quantumInfinitePrice = Mathf.RoundToInt(listing.Slider(settings.quantumInfinitePrice, 50f, 1000f));
            listing.CheckboxLabeled("套餐自动选购 (按余额/性格/远征需求/报销政策)", ref settings.enableAutoPlan);
            listing.CheckboxLabeled("月租从 RimPay 钱包自动扣费 (无 RimPay 则默认本地包)", ref settings.planWalletPay);
            listing.CheckboxLabeled("报销政策: 由玩家买单，小人可直接订量子无限包 (无需自掏腰包)", ref settings.planSubsidy);
            listing.Gap(10f);

            // ============ 功能开关 (畅享包) ============
            listing.Label("<b>高速畅享包功能</b>");
            listing.GapLine();
            listing.CheckboxLabeled("RimSeek 智算辅助 (全局工作 +10%、科研 +15%)", ref settings.enableRimSeek);
            listing.CheckboxLabeled("RimSeek 健康提醒 (畅享/无限包，每天提醒小人注意健康)", ref settings.enableHealthReminder);
            listing.CheckboxLabeled("看直播打赏 (需 RimTuber 联动)", ref settings.enableLiveTip);
            listing.CheckboxLabeled("黑客破解敌人钱包 (+20% 收益)", ref settings.enableHackWallet);
            listing.CheckboxLabeled("私有网购 (虚拟产品)", ref settings.enablePrivateShopping);
            listing.Gap(10f);

            // ============ 功能开关 (无限包) ============
            listing.Label("<b>量子无限包功能</b>");
            listing.GapLine();
            listing.CheckboxLabeled("RimSeek 深度协作 (+20% 效率且防分心)", ref settings.enableRimSeek);
            listing.CheckboxLabeled("RimTalk 跨地图视频通话 (需 RimTalk 联动)", ref settings.enableRimTalkCall);
            listing.CheckboxLabeled("云端天气/深矿雷达预测 (需要 AI 网络智算)", ref settings.enableCloudPredict);
            listing.Gap(10f);

            listing.End();
            Widgets.EndScrollView();
        }

        // ============ AI 设置页（AI 网络智算/供应商/测试连接/状态面板/JSON模板） ============
        private void DrawAIPage(Rect inRect)
        {
            float viewHeight = 2000f;
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, viewHeight);
            Widgets.BeginScrollView(inRect, ref aiScroll, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            // ============ AI 网络智算（多供应商配置） ============
            listing.Label("<b>AI 网络智算 (QuantumNet AI)</b>");
            listing.GapLine();

            // 推荐使用 Player2 的话术（显著位置）
            Text.Font = GameFont.Tiny;
            listing.Label("<color=#58d3f7><b>🎮 建议注册并使用 Player2 软件，免费体验全部 AI 功能！</b></color>");
            listing.Label("<color=#58d3f7>Player2 为本地免费桌面程序，无需 API Key；若已安装且正在运行，勾选 Player2 配置即可直接使用。</color>");
            listing.Label("<color=#58d3f7>勾选的配置会按列表顺序从上到下依次尝试，失败或额度不足时自动切换下一个。</color>");
            Text.Font = GameFont.Small;
            listing.Gap(4f);

            listing.Label("<i>开启后，每日量子网络数据（天气/深矿/智算建议/新闻）由 AI 生成；全部配置失败时自动回退到默认。</i>");
            listing.CheckboxLabeled("启用 AI 网络智算", ref settings.enableNetworkAI);
            listing.Gap(4f);

            listing.Label($"请求最小间隔: {settings.aiRequestIntervalHours} 小时 (避免刷爆)");
            settings.aiRequestIntervalHours = Mathf.RoundToInt(listing.Slider(settings.aiRequestIntervalHours, 1f, 72f));

            // 日报频次档位（三档按钮组）
            listing.Label("<b>量子网络日报频次</b> (AI 生成：网络状态/天气/深矿/新闻)");
            Rect freqRow = listing.GetRect(30f);
            float fw = freqRow.width / 3f;
            if (Widgets.ButtonText(new Rect(freqRow.x, freqRow.y, fw - 4f, freqRow.height), settings.reportFrequency == 0 ? "■ 关闭" : "关闭"))
            {
                settings.reportFrequency = 0;
            }
            if (Widgets.ButtonText(new Rect(freqRow.x + fw, freqRow.y, fw - 4f, freqRow.height), settings.reportFrequency == 1 ? "■ 每 2 天" : "每 2 天"))
            {
                settings.reportFrequency = 1;
            }
            if (Widgets.ButtonText(new Rect(freqRow.x + fw * 2f, freqRow.y, fw - 4f, freqRow.height), settings.reportFrequency == 2 ? "■ 每天" : "每天"))
            {
                settings.reportFrequency = 2;
            }
            listing.Label("<i>关闭 = 不请求 AI 也不发日报（省 token）；每天 12:00 按频次发送，需订阅量子无限包的殖民者。</i>");
            listing.CheckboxLabeled("AI 网络资讯播报给 RimTalk 小人对话 (需 RimTalk)", ref settings.enableRimTalkNewsBroadcast,
                "日报发出后，随机一名订阅量子无限包的殖民者会通过 RimTalk 聊起今日网络新闻/天气/深矿预测。");
            listing.Gap(4f);

            // 供应商配置列表
            listing.Label("<b>供应商配置 (勾选启用，顺序=尝试顺序):</b>");
            listing.Gap(3f);
            DrawAIConfigList(listing);

            listing.Gap(10f);

            // ============ AI 实时状态面板 ============
            listing.Label("<b>量子网络实时状态 (Live Status)</b>");
            listing.GapLine();
            listing.Label("<i>实时显示 AI 当前生成并生效的网络数据，用于验证 AI 是否真正掌控量子网络。</i>");
            listing.Gap(4f);

            DrawStatusRow(listing, "AI 网络智算开关", settings.enableNetworkAI ? "已开启" : "已关闭");
            DrawStatusRow(listing, "网络状态 (networkStatus)", QuantumNetAIProvider.networkStatus);
            DrawStatusRow(listing, "云端天气 (cloudWeatherHint)", string.IsNullOrEmpty(QuantumNetAIProvider.cloudWeatherHint) ? "(暂无，需 AI 成功返回)" : QuantumNetAIProvider.cloudWeatherHint);
            DrawStatusRow(listing, "深矿雷达 (deepMineHint)", string.IsNullOrEmpty(QuantumNetAIProvider.deepMineHint) ? "(暂无，需 AI 成功返回)" : QuantumNetAIProvider.deepMineHint);
            DrawStatusRow(listing, "智算建议 (rimseekTip)", string.IsNullOrEmpty(QuantumNetAIProvider.rimseekTip) ? "(暂无，需 AI 成功返回)" : QuantumNetAIProvider.rimseekTip);
            DrawStatusRow(listing, "网络新闻 (newsText)", string.IsNullOrEmpty(QuantumNetAIProvider.newsText) ? "(暂无，需 AI 成功返回)" : QuantumNetAIProvider.newsText);
            DrawStatusRow(listing, "最近请求日", $"{QuantumNetAIProvider.LastRequestDay} (今日: {GenLocalDate.DayOfYear(Find.CurrentMap ?? Find.AnyPlayerHomeMap)})");
            DrawStatusRow(listing, "是否有 AI 结果", QuantumNetAIProvider.hasAIResult ? "是" : "否");
            DrawStatusRow(listing, "连续失败次数", QuantumNetAIProvider.ConsecutiveFailures.ToString());
            DrawStatusRow(listing, "冷却中", QuantumNetAIProvider.IsCooldownActive ? "是" : "否");
            DrawStatusRow(listing, "请求进行中", QuantumNetAIProvider.IsRequestInFlight ? "是" : "否");
            if (!string.IsNullOrEmpty(QuantumNetAIProvider.LastHttpErrorDetail))
                DrawStatusRow(listing, "最近错误", QuantumNetAIProvider.LastHttpErrorDetail);

            listing.Gap(4f);
            Rect forceRow = listing.GetRect(30f);
            if (Widgets.ButtonText(forceRow, "立即请求一次 AI 网络数据 (Force Request)"))
            {
                QuantumNetAIProvider.ForceRequestNow();
            }
            listing.Gap(6f);
            listing.Label("<i>点击上方按钮可绕过「每日一次」限制，立即让 AI 生成一组新数据并显示在上面，用于验证连接与 AI 掌控。</i>");
            listing.Gap(10f);

            // ============ AI 网络 JSON 模板 ============
            listing.Label("<b>AI 网络 JSON 模板 (JSON Template)</b>");
            listing.GapLine();
            listing.Label("<i>该 JSON 指示 AI 每日返回的量子网络数据，可自由编辑；改乱后可一键还原默认。</i>");
            listing.Gap(4f);

            Rect jsonRect = listing.GetRect(200f);
            Widgets.DrawBoxSolid(jsonRect, new Color(0.1f, 0.1f, 0.1f, 0.5f));
            jsonBuffer = Widgets.TextArea(jsonRect, jsonBuffer);

            Rect jsonBtnRow = listing.GetRect(30f);
            Rect saveJsonBtn = new Rect(jsonBtnRow.x, jsonBtnRow.y, jsonBtnRow.width / 2f - 4f, jsonBtnRow.height);
            Rect resetJsonBtn = new Rect(jsonBtnRow.x + jsonBtnRow.width / 2f + 4f, jsonBtnRow.y, jsonBtnRow.width / 2f - 4f, jsonBtnRow.height);

            if (Widgets.ButtonText(saveJsonBtn, "保存 JSON 模板 (Save)"))
            {
                settings.networkJsonTemplate = jsonBuffer;
                Messages.Message("[QuantumNet] JSON 模板已保存。", MessageTypeDefOf.NeutralEvent, false);
            }
            if (Widgets.ButtonText(resetJsonBtn, "还原默认 JSON (Reset)"))
            {
                settings.RestoreDefaultJson();
                jsonBuffer = settings.networkJsonTemplate;
                Messages.Message("[QuantumNet] 已还原默认 JSON 模板。", MessageTypeDefOf.NeutralEvent, false);
            }

            listing.Gap(6f);
            listing.Label("<b>当前模组版本: v0.4.01</b>");

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
        private void StartTestConnection(QuantumNetProviderConfig cfg = null)
        {
            if (testInProgress) return;
            testInProgress = true;
            testResultText = "正在测试连接... (Testing...)";
            testConfigOverride = cfg;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    string result = QuantumNetAIProvider.TestConnection(testConfigOverride);
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
                foreach (QuantumNetProvider provider in Enum.GetValues(typeof(QuantumNetProvider)))
                {
                    if (provider == QuantumNetProvider.None) continue;
                    string label = provider.GetLabel();
                    var opt = new FloatMenuOption(label, () =>
                    {
                        var newCfg = new QuantumNetProviderConfig(provider);
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
        private void DrawAIConfigEditor(Listing_Standard listing, QuantumNetProviderConfig cfg)
        {
            listing.GapLine();
            listing.Label("<b>配置详情:</b> " + cfg.DisplayLabel);

            Rect provRow = listing.GetRect(30f);
            Widgets.Label(new Rect(provRow.x, provRow.y, 100f, provRow.height), "供应商:");
            Rect provBtn = new Rect(provRow.x + 105f, provRow.y, provRow.width - 105f, provRow.height);
            if (Widgets.ButtonText(provBtn, cfg.provider.GetLabel()))
            {
                var options = new List<FloatMenuOption>();
                foreach (QuantumNetProvider provider in Enum.GetValues(typeof(QuantumNetProvider)))
                {
                    if (provider == QuantumNetProvider.None) continue;
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

            if (cfg.provider == QuantumNetProvider.Player2)
            {
                listing.Label("Player2 本地服务：无需 API Key，安装并运行 Player2 即可。");
            }
            else if (cfg.provider == QuantumNetProvider.Local)
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
