using Verse;

namespace RimDigitalLife_RimPay
{
    public class RimPaySettings : ModSettings
    {
        // ======== API 经济智能 (AI Economy) ========
        public bool enableEconomyAI = false;            // 是否启用 AI 控制经济变量

        // 多供应商配置列表（照 RimTuber：勾选用哪个，按列表顺序从上到下尝试，失败自动切下一个）
        public System.Collections.Generic.List<RimPayProviderConfig> apiConfigs = new System.Collections.Generic.List<RimPayProviderConfig>();

        // 旧版单配置字段（仅用于迁移：若 apiConfigs 为空且旧字段已被填写，则迁移到列表）
        public RimPayProvider apiProvider = RimPayProvider.Player2;
        public string apiEndpointUrl = "";
        public string apiKey = "";
        public string apiModel = "";

        public int aiRefreshIntervalHours = 12;         // AI 刷新间隔 (小时)，控制重新请求频率，默认 12
        public bool showDailyEconomyLetter = true;      // 每日财经日报信封 (默认开启)
        public bool enableAiRimTalkBroadcast = true;    // AI 财经事件播报给 RimTalk 小人对话 (默认开启)

        // ======== 薪资 (Salary) ========
        public bool enableSalary = true;                // 是否启用工资发放
        public int baseSalary = 10;                     // 基础工资
        public int childSalary = 5;                     // 儿童零花钱
        public int slaveSalary = 2;                     // 奴隶安抚金
        public int expertBonus = 40;                    // 专家等级加成
        public int skilledBonus = 20;                   // 熟练工加成
        public int basicBonus = 10;                     // 基础工人加成

        // ======== 房租 (Rent) ========
        public bool enableRent = true;                  // 是否收取房租
        public int rentLuxury = 20;                     // 印象>120
        public int rentHigh = 15;                       // 印象>80
        public int rentMid = 10;                        // 印象>50
        public int rentLow = 5;                         // 印象>20
        public int rentMin = 2;                         // 最低房租
        public float deviceRentDiscount = 0.85f;        // 佩戴设备房租折扣 (1 = 无折扣，0.85 = 85折)

        // ======== 餐费 (Meal Fee) ========
        public bool enableMealFee = true;               // 是否收取餐费
        public int mealLavish = 15;                     // 奢华食物
        public int mealFine = 10;                       // 精致食物
        public int mealSimple = 8;                      // 简单食物
        public int mealAwful = 3;                       // 难吃食物 (营养膏等)
        public int mealRaw = 1;                         // 生食/浆果等

        // ======== 医疗就医 (RimPay 医保) ========
        public bool enableMedicalFee = true;            // 启用诊疗扣费
        public int medicalBaseFee = 10;                 // 单次诊疗费 (@银)

        // ======== 余额宝利息 (Interest) ========
        public bool enableInterest = true;              // 是否发放存款利息
        public int interestMinBalance = 100;            // 达到该余额才计息
        public float interestRate = 0.005f;             // 每日利率 (0.005 = 0.5%)
        public float interestMoodChance = 0.2f;         // 获得利息心情的概率

        // ======== 借贷 (Loan) ========
        public bool enableLoan = true;                  // 是否启用借贷中心
        public float loanRateBase = 0.08f;              // 基准放贷利率
        public float loanRateFriendly = 0.06f;          // 友好派系利率
        public float loanRateCold = 0.10f;              // 冷漠派系利率
        public float borrowRateBase = 0.12f;            // 基准借款利率
        public float loanFriendlyThreshold = 75f;       // 友好好感阈值
        public float loanColdThreshold = 25f;           // 冷漠好感阈值
        public int loanDefaultPeriod = 15;              // 借贷周期 (游戏天数)
        public int loanDefaultAmount = 500;             // 借贷默认金额
        public int loanMaxBorrow = 5000;                // 最大可借金额
        public int loanBadDebtGoodwill = -15;           // 逾期坏账好感惩罚

        // ======== 股市 (Stock) ========
        public bool enableStocks = true;                // 是否启用证券交易
        public int stockPriceUpdateTicks = 60000;       // 股价更新间隔 (tick，1天=60000)
        public float stocksFundVolatility = 0.30f;      // $DSF 杠杆基金波动率
        public double stocksMinPrice = 0.1;             // 股价下限
        public int stockHistoryDays = 15;               // K线保留天数

        // ======== 小人炒股 (Pawn Trading) ========
        public bool enablePawnTrading = true;           // 是否启用小人自动炒股
        public int pawnTradeMinBalance = 200;           // 炒股最低余额门槛
        public float pawnTradeMoodRequirement = 0.3f;   // 炒股所需心情下限
        public float pawnTradeInvestRatio = 0.2f;       // 投入余额比例 (0.2 = 20%)
        public int pawnTradeInvestCap = 200;            // 单次投入上限
        public int pawnTradeRimTalkThreshold = 50;      // 盈亏多少触发 RimTalk
        public float pawnTradeBaseHitChance = 0.5f;     // 基础命中率
        public float pawnTradeHitPerSkill = 0.03f;      // 每点智力增加的命中率

        // ======== 搜刮 (Loot) ========
        public bool enablePawnLoot = true;              // 是否启用敌人搜刮
        public float lootKillerShare = 0.5f;            // 击杀者分成 (0.5 = 50%)

        // ======== 钱包流水 (Wallet) ========
        public int maxTransactionPerPawn = 15;          // 每人保留的最大流水条数

        // ======== 财富托管 (Wealth Escrow) ========
        public bool hideBuildingWealth = true;          // 建筑财富托管：建筑不计入袭击威胁点 (默认开)
        public bool hidePawnEquipmentWealth = true;     // 随身装备托管：小人/动物随身武器衣物不计入威胁 (默认开)

        // ======== RimSim 商店联动 (Shop Integration) ========
        public bool enableRimSimIntegration = true;     // RimSim 商店联动总开关 (未安装 RimSim 时自动休眠)
        public float rimSimTreasuryRatio = 1f;          // 店铺收入入国库比例 (0~1，默认 100%)
        public float rimSimDeviceDiscount = 0.05f;      // 数码设备档折扣：殖民地有佩戴数码设备的殖民者时顾客实付 -5%
        public float rimSimQuantumDiscount = 0.10f;     // 量子网络档折扣：有订阅量子网络套餐的殖民者时 -10% (覆盖上档)

        // AI 返回的 JSON 模板 (玩家可查阅/修改，可一键还原默认)
        public string economyJsonTemplate = DefaultEconomyJson;

// 还原默认用的常量
        public const string DefaultEconomyJson =
            "{\n" +
            "  \"salaryMultiplier\": 0.8~1.3,\n" +
            "  \"rentMultiplier\": 0.8~1.2,\n" +
            "  \"interestRateDelta\": -0.5~0.5,\n" +
            "  \"loanRateMultiplier\": 0.8~1.5,\n" +
            "  \"mealFeeMultiplier\": 0.8~1.5,\n" +
            "  \"lootShareDelta\": -0.15~0.15,\n" +
            "  \"tradePriceMultiplier\": 0.8~1.2,\n" +
            "  \"macroEvent\": \"none|market_crash|gold_rush|shortage|tech_boom\",\n" +
            "  \"marketTone\": \"bull|bear|neutral\",\n" +
            "  \"eventText\": \"一句话描述今日边缘财经大事件，不超过60字\"\n" +
            "}";

        public override void ExposeData()
        {
            Scribe_Values.Look(ref enableEconomyAI, "enableEconomyAI", false);
            string providerStr = apiProvider.ToString();
            Scribe_Values.Look(ref providerStr, "apiProvider", "Player2");
            apiProvider = RimPayProviderRegistry.FromString(providerStr);
            Scribe_Values.Look(ref apiEndpointUrl, "apiEndpointUrl", "");
            Scribe_Values.Look(ref apiKey, "apiKey", "");
            Scribe_Values.Look(ref apiModel, "apiModel", "");
            Scribe_Values.Look(ref aiRefreshIntervalHours, "aiRefreshIntervalHours", 12);
            Scribe_Values.Look(ref showDailyEconomyLetter, "showDailyEconomyLetter", true);
            Scribe_Values.Look(ref enableAiRimTalkBroadcast, "enableAiRimTalkBroadcast", true);
            Scribe_Values.Look(ref hideBuildingWealth, "hideBuildingWealth", true);
            Scribe_Values.Look(ref hidePawnEquipmentWealth, "hidePawnEquipmentWealth", true);
            // RimSim 商店联动
            Scribe_Values.Look(ref enableRimSimIntegration, "enableRimSimIntegration", true);
            Scribe_Values.Look(ref rimSimTreasuryRatio, "rimSimTreasuryRatio", 1f);
            Scribe_Values.Look(ref rimSimDeviceDiscount, "rimSimDeviceDiscount", 0.05f);
            Scribe_Values.Look(ref rimSimQuantumDiscount, "rimSimQuantumDiscount", 0.10f);

            // ======== 经济参数（此前遗漏，导致重启后滑块全部回默认） ========
            // 薪资
            Scribe_Values.Look(ref enableSalary, "enableSalary", true);
            Scribe_Values.Look(ref baseSalary, "baseSalary", 10);
            Scribe_Values.Look(ref childSalary, "childSalary", 5);
            Scribe_Values.Look(ref slaveSalary, "slaveSalary", 2);
            Scribe_Values.Look(ref expertBonus, "expertBonus", 40);
            Scribe_Values.Look(ref skilledBonus, "skilledBonus", 20);
            Scribe_Values.Look(ref basicBonus, "basicBonus", 10);
            // 房租
            Scribe_Values.Look(ref enableRent, "enableRent", true);
            Scribe_Values.Look(ref rentLuxury, "rentLuxury", 20);
            Scribe_Values.Look(ref rentHigh, "rentHigh", 15);
            Scribe_Values.Look(ref rentMid, "rentMid", 10);
            Scribe_Values.Look(ref rentLow, "rentLow", 5);
            Scribe_Values.Look(ref rentMin, "rentMin", 2);
            Scribe_Values.Look(ref deviceRentDiscount, "deviceRentDiscount", 0.85f);
            // 餐费
            Scribe_Values.Look(ref enableMealFee, "enableMealFee", true);
            Scribe_Values.Look(ref mealLavish, "mealLavish", 15);
            Scribe_Values.Look(ref mealFine, "mealFine", 10);
            Scribe_Values.Look(ref mealSimple, "mealSimple", 8);
            Scribe_Values.Look(ref mealAwful, "mealAwful", 3);
            Scribe_Values.Look(ref mealRaw, "mealRaw", 1);
            // 医疗就医
            Scribe_Values.Look(ref enableMedicalFee, "enableMedicalFee", true);
            Scribe_Values.Look(ref medicalBaseFee, "medicalBaseFee", 10);
            // 余额宝利息
            Scribe_Values.Look(ref enableInterest, "enableInterest", true);
            Scribe_Values.Look(ref interestMinBalance, "interestMinBalance", 100);
            Scribe_Values.Look(ref interestRate, "interestRate", 0.005f);
            Scribe_Values.Look(ref interestMoodChance, "interestMoodChance", 0.2f);
            // 借贷
            Scribe_Values.Look(ref enableLoan, "enableLoan", true);
            Scribe_Values.Look(ref loanRateBase, "loanRateBase", 0.08f);
            Scribe_Values.Look(ref loanRateFriendly, "loanRateFriendly", 0.06f);
            Scribe_Values.Look(ref loanRateCold, "loanRateCold", 0.10f);
            Scribe_Values.Look(ref borrowRateBase, "borrowRateBase", 0.12f);
            Scribe_Values.Look(ref loanFriendlyThreshold, "loanFriendlyThreshold", 75f);
            Scribe_Values.Look(ref loanColdThreshold, "loanColdThreshold", 25f);
            Scribe_Values.Look(ref loanDefaultPeriod, "loanDefaultPeriod", 15);
            Scribe_Values.Look(ref loanDefaultAmount, "loanDefaultAmount", 500);
            Scribe_Values.Look(ref loanMaxBorrow, "loanMaxBorrow", 5000);
            Scribe_Values.Look(ref loanBadDebtGoodwill, "loanBadDebtGoodwill", -15);
            // 股市
            Scribe_Values.Look(ref enableStocks, "enableStocks", true);
            Scribe_Values.Look(ref stockPriceUpdateTicks, "stockPriceUpdateTicks", 60000);
            Scribe_Values.Look(ref stocksFundVolatility, "stocksFundVolatility", 0.30f);
            Scribe_Values.Look(ref stocksMinPrice, "stocksMinPrice", 0.1);
            Scribe_Values.Look(ref stockHistoryDays, "stockHistoryDays", 15);
            // 小人炒股
            Scribe_Values.Look(ref enablePawnTrading, "enablePawnTrading", true);
            Scribe_Values.Look(ref pawnTradeMinBalance, "pawnTradeMinBalance", 200);
            Scribe_Values.Look(ref pawnTradeMoodRequirement, "pawnTradeMoodRequirement", 0.3f);
            Scribe_Values.Look(ref pawnTradeInvestRatio, "pawnTradeInvestRatio", 0.2f);
            Scribe_Values.Look(ref pawnTradeInvestCap, "pawnTradeInvestCap", 200);
            Scribe_Values.Look(ref pawnTradeRimTalkThreshold, "pawnTradeRimTalkThreshold", 50);
            Scribe_Values.Look(ref pawnTradeBaseHitChance, "pawnTradeBaseHitChance", 0.5f);
            Scribe_Values.Look(ref pawnTradeHitPerSkill, "pawnTradeHitPerSkill", 0.03f);
            // 搜刮
            Scribe_Values.Look(ref enablePawnLoot, "enablePawnLoot", true);
            Scribe_Values.Look(ref lootKillerShare, "lootKillerShare", 0.5f);
            // 钱包流水
            Scribe_Values.Look(ref maxTransactionPerPawn, "maxTransactionPerPawn", 15);

            Scribe_Values.Look(ref economyJsonTemplate, "economyJsonTemplate", DefaultEconomyJson);
            Scribe_Collections.Look(ref apiConfigs, "apiConfigs", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (string.IsNullOrEmpty(economyJsonTemplate))
                {
                    economyJsonTemplate = DefaultEconomyJson;
                }
                if (apiConfigs == null)
                {
                    apiConfigs = new System.Collections.Generic.List<RimPayProviderConfig>();
                }

                // 迁移：旧版单配置（旧字段已填写）→ 列表首位
                bool hasLegacy = !string.IsNullOrEmpty(apiEndpointUrl)
                    || !string.IsNullOrEmpty(apiKey)
                    || !string.IsNullOrEmpty(apiModel);
                if (apiConfigs.Count == 0 && (hasLegacy || apiProvider != RimPayProvider.None))
                {
                    var legacy = new RimPayProviderConfig(
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

        // 返回按列表顺序排列的“已启用（勾选）”配置
        public System.Collections.Generic.List<RimPayProviderConfig> GetEnabledConfigs()
        {
            var result = new System.Collections.Generic.List<RimPayProviderConfig>();
            if (apiConfigs == null)
            {
                apiConfigs = new System.Collections.Generic.List<RimPayProviderConfig>();
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
            economyJsonTemplate = DefaultEconomyJson;
        }
    }
}