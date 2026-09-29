using Verse;

namespace RimDigitalLife_QuantumNet
{
    public class QuantumNetSettings : ModSettings
    {
        // ======== 量子网络覆盖 (Network) ========
        public bool enableQuantumNet = true;           // 量子网络总开关
        public bool requireCommsConsole = true;        // 需通电通讯台 = 当前地图 100% 覆盖
        public bool enableMapWideBeacon = true;        // 全图贸易信标：任意地图只要有一个通电信标即全图贸易 (替代 MapWideTradeBeacon)

        // ======== 套餐体系 (Plans) ========
        public int planPeriodDays = 15;                // 套餐计费周期 (1 象)
        public int highSpeedPrice = 60;                // 高速畅享包价格
        public int quantumInfinitePrice = 180;         // 量子无限包价格
        public bool enableAutoPlan = true;             // 套餐自动选购
        public bool planWalletPay = true;              // 月租从 RimPay 钱包自动扣费 (可选联动)
        public bool planSubsidy = false;               // 报销政策：玩家买单 → 小人可直接订量子无限包 (无需自掏腰包)

        // ======== 功能开关 (各套餐解锁项) ========
        public bool enableRimSeek = true;              // RimSeek 智算办公 (畅享/无限)
        public bool enableHealthReminder = true;       // 健康提醒 (畅享)
        public bool enableLiveTip = true;              // 看直播打赏 (畅享, 需 RimTuber)
        public bool enableHackWallet = true;           // 黑客破解敌人钱包 (畅享)
        public bool enablePrivateShopping = true;      // 私有网购 (畅享)
        public bool enableRimTalkCall = true;          // RimTalk 跨地图视频通话 (无限)
        public bool enableCloudPredict = true;         // 云端天气/深矿雷达预测 (无限)

        // ======== AI 网络智算 (Network AI) ========
        public bool enableNetworkAI = false;           // 是否启用 AI 网络智算
        public System.Collections.Generic.List<QuantumNetProviderConfig> apiConfigs = new System.Collections.Generic.List<QuantumNetProviderConfig>();

        // 旧版单配置字段 (仅迁移用)
        public QuantumNetProvider apiProvider = QuantumNetProvider.SiliconFlow;
        public string apiEndpointUrl = "";
        public string apiKey = "";
        public string apiModel = "";

        public int aiRequestIntervalHours = 24;        // 最少请求间隔 (小时)
        public bool showDailyNetworkLetter = true;     // (旧字段，已被日报频次档位取代，仅存档兼容)
        // 日报频次档位：0=关闭 1=每2天(默认) 2=每天
        public int reportFrequency = 1;
        public bool enableRimTalkNewsBroadcast = true; // AI 新闻播报给 RimTalk 小人对话 (需 RimTalk)
        public string networkJsonTemplate = DefaultNetworkJson;

        public const string DefaultNetworkJson =
            "{\n" +
            "  \"networkStatus\": \"normal|congested|outage\",\n" +
            "  \"cloudWeatherHint\": \"今日天气预测一句话，不超过40字\",\n" +
            "  \"deepMineHint\": \"深矿雷达预测一句话，不超过40字\",\n" +
            "  \"rimseekTip\": \"AI 智算工作建议一句话，不超过40字\",\n" +
            "  \"newsText\": \"今日量子网络大事件一句话，不超过60字\"\n" +
            "}";

        public override void ExposeData()
        {
            // ======== 网络覆盖 ========
            Scribe_Values.Look(ref enableQuantumNet, "enableQuantumNet", true);
            Scribe_Values.Look(ref requireCommsConsole, "requireCommsConsole", true);
            Scribe_Values.Look(ref enableMapWideBeacon, "enableMapWideBeacon", true);

            // ======== 套餐体系 ========
            Scribe_Values.Look(ref planPeriodDays, "planPeriodDays", 15);
            Scribe_Values.Look(ref highSpeedPrice, "highSpeedPrice", 60);
            Scribe_Values.Look(ref quantumInfinitePrice, "quantumInfinitePrice", 180);
            Scribe_Values.Look(ref enableAutoPlan, "enableAutoPlan", true);
            Scribe_Values.Look(ref planWalletPay, "planWalletPay", true);
            Scribe_Values.Look(ref planSubsidy, "planSubsidy", false);

            // ======== 功能开关 ========
            Scribe_Values.Look(ref enableRimSeek, "enableRimSeek", true);
            Scribe_Values.Look(ref enableHealthReminder, "enableHealthReminder", true);
            Scribe_Values.Look(ref enableLiveTip, "enableLiveTip", true);
            Scribe_Values.Look(ref enableHackWallet, "enableHackWallet", true);
            Scribe_Values.Look(ref enablePrivateShopping, "enablePrivateShopping", true);
            Scribe_Values.Look(ref enableRimTalkCall, "enableRimTalkCall", true);
            Scribe_Values.Look(ref enableCloudPredict, "enableCloudPredict", true);

            // ======== AI 网络智算 ========
            Scribe_Values.Look(ref enableNetworkAI, "enableNetworkAI", false);
            string providerStr = apiProvider.ToString();
            Scribe_Values.Look(ref providerStr, "apiProvider", "SiliconFlow");
            apiProvider = QuantumNetProviderRegistry.FromString(providerStr);
            Scribe_Values.Look(ref apiEndpointUrl, "apiEndpointUrl", "");
            Scribe_Values.Look(ref apiKey, "apiKey", "");
            Scribe_Values.Look(ref apiModel, "apiModel", "");
            Scribe_Values.Look(ref aiRequestIntervalHours, "aiRequestIntervalHours", 24);
            Scribe_Values.Look(ref showDailyNetworkLetter, "showDailyNetworkLetter", true);
            Scribe_Values.Look(ref reportFrequency, "reportFrequency", 1);
            Scribe_Values.Look(ref enableRimTalkNewsBroadcast, "enableRimTalkNewsBroadcast", true);
            Scribe_Values.Look(ref networkJsonTemplate, "networkJsonTemplate", DefaultNetworkJson);
            Scribe_Collections.Look(ref apiConfigs, "apiConfigs", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (string.IsNullOrEmpty(networkJsonTemplate))
                {
                    networkJsonTemplate = DefaultNetworkJson;
                }
                if (apiConfigs == null)
                {
                    apiConfigs = new System.Collections.Generic.List<QuantumNetProviderConfig>();
                }

                // 迁移：旧版单配置 → 列表首位
                bool hasLegacy = !string.IsNullOrEmpty(apiEndpointUrl)
                    || !string.IsNullOrEmpty(apiKey)
                    || !string.IsNullOrEmpty(apiModel);
                if (apiConfigs.Count == 0 && (hasLegacy || apiProvider != QuantumNetProvider.None))
                {
                    var legacy = new QuantumNetProviderConfig(
                        apiProvider,
                        apiProvider.GetLabel(),
                        apiEndpointUrl,
                        apiKey,
                        apiModel,
                        true);
                    apiConfigs.Add(legacy);
                }
            }

            base.ExposeData();
        }

        // 返回按列表顺序排列的「已启用」配置
        public System.Collections.Generic.List<QuantumNetProviderConfig> GetEnabledConfigs()
        {
            var result = new System.Collections.Generic.List<QuantumNetProviderConfig>();
            if (apiConfigs == null)
            {
                apiConfigs = new System.Collections.Generic.List<QuantumNetProviderConfig>();
            }
            foreach (var cfg in apiConfigs)
            {
                if (cfg != null && cfg.enabled)
                {
                    result.Add(cfg);
                }
            }
            return result;
        }

        public void RestoreDefaultJson()
        {
            networkJsonTemplate = DefaultNetworkJson;
        }
    }
}
