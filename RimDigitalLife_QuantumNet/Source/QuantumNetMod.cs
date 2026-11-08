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
        public const string ModVersion = "v1.0.5";

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
            return "Rim Digital Life - Quantum Net Expansion (" + ModVersion + ")";
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

            if (Widgets.ButtonText(generalTab, "QN_Tab_General".Translate()))
            {
                currentPage = QuantumNetSettingsPage.General;
            }
            if (Widgets.ButtonText(aiTab, "QN_Tab_AI".Translate()))
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
            listing.Label("<b>" + "QN_Set_CoverageTitle".Translate() + "</b>");
            listing.GapLine();
            listing.CheckboxLabeled("QN_Set_EnableNet".Translate(), ref settings.enableQuantumNet);
            listing.CheckboxLabeled("QN_Set_NeedConsole".Translate(), ref settings.requireCommsConsole);
            listing.CheckboxLabeled("QN_Set_MapBeacon".Translate(), ref settings.enableMapWideBeacon);
            listing.Gap(10f);

            // ============ 套餐体系 ============
            listing.Label("<b>" + "QN_Set_PlansTitle".Translate() + "</b>");
            listing.GapLine();
            listing.Label("QN_Set_Period".Translate(settings.planPeriodDays));
            settings.planPeriodDays = Mathf.RoundToInt(listing.Slider(settings.planPeriodDays, 3f, 60f));
            listing.Label("QN_Set_HighPrice".Translate(settings.highSpeedPrice, settings.planPeriodDays));
            settings.highSpeedPrice = Mathf.RoundToInt(listing.Slider(settings.highSpeedPrice, 10f, 500f));
            listing.Label("QN_Set_InfPrice".Translate(settings.quantumInfinitePrice, settings.planPeriodDays));
            settings.quantumInfinitePrice = Mathf.RoundToInt(listing.Slider(settings.quantumInfinitePrice, 50f, 1000f));
            listing.CheckboxLabeled("QN_Set_AutoPlan".Translate(), ref settings.enableAutoPlan);
            listing.CheckboxLabeled("QN_Set_WalletPay".Translate(), ref settings.planWalletPay);
            listing.CheckboxLabeled("QN_Set_Subsidy".Translate(), ref settings.planSubsidy);
            listing.Gap(10f);

            // ============ 功能开关 (畅享包) ============
            listing.Label("<b>" + "QN_Set_HighTitle".Translate() + "</b>");
            listing.GapLine();
            listing.CheckboxLabeled("QN_Set_RimSeek".Translate(), ref settings.enableRimSeek);
            listing.CheckboxLabeled("QN_Set_Health".Translate(), ref settings.enableHealthReminder);
            listing.CheckboxLabeled("QN_Set_LiveTip".Translate(), ref settings.enableLiveTip);
            listing.CheckboxLabeled("QN_Set_Hack".Translate(), ref settings.enableHackWallet);
            listing.CheckboxLabeled("QN_Set_Shop".Translate(), ref settings.enablePrivateShopping);
            listing.Gap(10f);

            // ============ 功能开关 (无限包) ============
            listing.Label("<b>" + "QN_Set_InfTitle".Translate() + "</b>");
            listing.GapLine();
            listing.CheckboxLabeled("QN_Set_DeepCoop".Translate(), ref settings.enableRimSeek);
            listing.CheckboxLabeled("QN_Set_CrossMap".Translate(), ref settings.enableRimTalkCall);
            listing.CheckboxLabeled("QN_Set_CloudPredict".Translate(), ref settings.enableCloudPredict);
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
            listing.Label("<b>" + "QN_AI_Title".Translate() + "</b>");
            listing.GapLine();

            // 推荐使用 Player2 的话术（显著位置）
            Text.Font = GameFont.Tiny;
            listing.Label("<color=#58d3f7><b>" + "QN_AI_P2Promo1".Translate() + "</b></color>");
            listing.Label("<color=#58d3f7>" + "QN_AI_P2Promo2".Translate() + "</color>");
            listing.Label("<color=#58d3f7>" + "QN_AI_P2Promo3".Translate() + "</color>");
            Text.Font = GameFont.Small;
            listing.Gap(4f);

            listing.Label("<i>" + "QN_AI_EnableDesc".Translate() + "</i>");
            listing.CheckboxLabeled("QN_AI_Enable".Translate(), ref settings.enableNetworkAI);
            listing.Gap(4f);

            listing.Label("QN_AI_Interval".Translate(settings.aiRequestIntervalHours));
            settings.aiRequestIntervalHours = Mathf.RoundToInt(listing.Slider(settings.aiRequestIntervalHours, 1f, 72f));

            // 日报频次档位（三档按钮组）
            listing.Label("<b>" + "QN_AI_ReportFreq".Translate() + "</b>");
            Rect freqRow = listing.GetRect(30f);
            float fw = freqRow.width / 3f;
            if (Widgets.ButtonText(new Rect(freqRow.x, freqRow.y, fw - 4f, freqRow.height), settings.reportFrequency == 0 ? "QN_AI_FreqOffSel".Translate() : "QN_AI_FreqOff".Translate()))
            {
                settings.reportFrequency = 0;
            }
            if (Widgets.ButtonText(new Rect(freqRow.x + fw, freqRow.y, fw - 4f, freqRow.height), settings.reportFrequency == 1 ? "QN_AI_Freq2Sel".Translate() : "QN_AI_Freq2".Translate()))
            {
                settings.reportFrequency = 1;
            }
            if (Widgets.ButtonText(new Rect(freqRow.x + fw * 2f, freqRow.y, fw - 4f, freqRow.height), settings.reportFrequency == 2 ? "QN_AI_Freq1Sel".Translate() : "QN_AI_Freq1".Translate()))
            {
                settings.reportFrequency = 2;
            }
            listing.Label("<i>" + "QN_AI_FreqDesc".Translate() + "</i>");
            listing.CheckboxLabeled("QN_AI_RimTalkCast".Translate(), ref settings.enableRimTalkNewsBroadcast,
                "QN_AI_RimTalkCastDesc".Translate());
            listing.Gap(4f);

            // 供应商配置列表
            listing.Label("<b>" + "QN_AI_Providers".Translate() + "</b>");
            listing.Gap(3f);
            DrawAIConfigList(listing);

            listing.Gap(10f);

            // ============ AI 实时状态面板 ============
            listing.Label("<b>" + "QN_AI_LiveTitle".Translate() + "</b>");
            listing.GapLine();
            listing.Label("<i>" + "QN_AI_LiveDesc".Translate() + "</i>");
            listing.Gap(4f);

            DrawStatusRow(listing, "QN_AI_S_Toggle".Translate(), settings.enableNetworkAI ? "QN_Yes".Translate() : "QN_No".Translate());
            DrawStatusRow(listing, "QN_AI_S_Status".Translate(), QuantumNetAIProvider.networkStatus);
            DrawStatusRow(listing, "QN_AI_S_Weather".Translate(), string.IsNullOrEmpty(QuantumNetAIProvider.cloudWeatherHint) ? (string)"QN_AI_S_None".Translate() : QuantumNetAIProvider.cloudWeatherHint);
            DrawStatusRow(listing, "QN_AI_S_Mine".Translate(), string.IsNullOrEmpty(QuantumNetAIProvider.deepMineHint) ? (string)"QN_AI_S_None".Translate() : QuantumNetAIProvider.deepMineHint);
            DrawStatusRow(listing, "QN_AI_S_Tip".Translate(), string.IsNullOrEmpty(QuantumNetAIProvider.rimseekTip) ? (string)"QN_AI_S_None".Translate() : QuantumNetAIProvider.rimseekTip);
            DrawStatusRow(listing, "QN_AI_S_News".Translate(), string.IsNullOrEmpty(QuantumNetAIProvider.newsText) ? (string)"QN_AI_S_None".Translate() : QuantumNetAIProvider.newsText);
            DrawStatusRow(listing, "QN_AI_S_LastDay".Translate(), "QN_AI_S_LastDayFmt".Translate(QuantumNetAIProvider.LastRequestDay, GenLocalDate.DayOfYear(Find.CurrentMap ?? Find.AnyPlayerHomeMap)));
            DrawStatusRow(listing, "QN_AI_S_HasResult".Translate(), QuantumNetAIProvider.hasAIResult ? "QN_Yes".Translate() : "QN_No".Translate());
            DrawStatusRow(listing, "QN_AI_S_Fails".Translate(), QuantumNetAIProvider.ConsecutiveFailures.ToString());
            DrawStatusRow(listing, "QN_AI_S_Cooldown".Translate(), QuantumNetAIProvider.IsCooldownActive ? "QN_Yes".Translate() : "QN_No".Translate());
            DrawStatusRow(listing, "QN_AI_S_InFlight".Translate(), QuantumNetAIProvider.IsRequestInFlight ? "QN_Yes".Translate() : "QN_No".Translate());
            if (!string.IsNullOrEmpty(QuantumNetAIProvider.LastHttpErrorDetail))
                DrawStatusRow(listing, "QN_AI_S_LastError".Translate(), QuantumNetAIProvider.LastHttpErrorDetail);

            listing.Gap(4f);
            Rect forceRow = listing.GetRect(30f);
            if (Widgets.ButtonText(forceRow, "QN_AI_ForceBtn".Translate()))
            {
                QuantumNetAIProvider.ForceRequestNow();
            }
            listing.Gap(6f);
            listing.Label("<i>" + "QN_AI_ForceDesc".Translate() + "</i>");
            listing.Gap(10f);

            // ============ AI 网络 JSON 模板 ============
            listing.Label("<b>" + "QN_AI_JsonTitle".Translate() + "</b>");
            listing.GapLine();
            listing.Label("<i>" + "QN_AI_JsonDesc".Translate() + "</i>");
            listing.Gap(4f);

            Rect jsonRect = listing.GetRect(200f);
            Widgets.DrawBoxSolid(jsonRect, new Color(0.1f, 0.1f, 0.1f, 0.5f));
            jsonBuffer = Widgets.TextArea(jsonRect, jsonBuffer);

            Rect jsonBtnRow = listing.GetRect(30f);
            Rect saveJsonBtn = new Rect(jsonBtnRow.x, jsonBtnRow.y, jsonBtnRow.width / 2f - 4f, jsonBtnRow.height);
            Rect resetJsonBtn = new Rect(jsonBtnRow.x + jsonBtnRow.width / 2f + 4f, jsonBtnRow.y, jsonBtnRow.width / 2f - 4f, jsonBtnRow.height);

            if (Widgets.ButtonText(saveJsonBtn, "QN_AI_JsonSave".Translate()))
            {
                settings.networkJsonTemplate = jsonBuffer;
                Messages.Message("[QuantumNet] " + "QN_AI_JsonSaved".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }
            if (Widgets.ButtonText(resetJsonBtn, "QN_AI_JsonReset".Translate()))
            {
                settings.RestoreDefaultJson();
                jsonBuffer = settings.networkJsonTemplate;
                Messages.Message("[QuantumNet] " + "QN_AI_JsonResetDone".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }

            listing.Gap(6f);
            listing.Label("<b>" + "QN_Set_Version".Translate(ModVersion) + "</b>");

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
            testResultText = "QN_Testing".Translate();
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
            if (Widgets.ButtonText(addBtn, "QN_AI_AddConfig".Translate()))
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
            listing.Label("<b>" + "QN_AI_CfgDetail".Translate() + "</b> " + cfg.DisplayLabel);

            Rect provRow = listing.GetRect(30f);
            Widgets.Label(new Rect(provRow.x, provRow.y, 100f, provRow.height), "QN_AI_CfgProvider".Translate());
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
                listing.Label("QN_AI_CfgP2Hint".Translate());
            }
            else if (cfg.provider == QuantumNetProvider.Local)
            {
                listing.Label("QN_AI_CfgLocalHint".Translate());
            }

            if (cfg.provider.GetRequiresApiKey())
            {
                listing.Label("API Key:");
                cfg.apiKey = listing.TextEntry(cfg.apiKey ?? "");
            }

            listing.Label("QN_AI_CfgEndpoint".Translate());
            cfg.endpointUrl = listing.TextEntry(cfg.endpointUrl ?? "");

            listing.Label("QN_AI_CfgModel".Translate());
            cfg.model = listing.TextEntry(cfg.model ?? "");

            listing.CheckboxLabeled("QN_AI_CfgEnable".Translate(), ref cfg.enabled);

            // ============ 测试连接按钮 ============
            listing.Gap(4f);
            Rect testBtnRect = listing.GetRect(30f);
            if (Widgets.ButtonText(testBtnRect, testInProgress ? "QN_Testing".Translate() : "QN_AI_CfgTest".Translate()))
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
