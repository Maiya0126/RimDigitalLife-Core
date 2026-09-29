# RimDigitalLife: RimPay 数字经济拓展 — 交接说明 (Handoff)

> 本文件用于开启「RimPay 拓展模组专属对话」时，把当前完整状态交接过去。
> 相关源代码位置：`D:\Visual Studio Code ALL\RimDigitalLife-Core\RimDigitalLife_RimPay\`

---

## 一、本轮已完成并编译通过 (v0.7.01)

### 0. 本轮新增（v0.7.01）
- **RimSim 商店联动 (RimSimIntegration.cs，新 Compat 文件)**：对接 RimSim Management Framework（`chezhou.Framework.RimSimManagementFramework`，Workshop 3736621496）官方 API（`SimManagementLib.Api`）：
  - **结账折扣**：`ShopCheckoutWorker.ModifyPaidSilver`——殖民地数字化档位决定顾客实付折扣：档1=有殖民者佩戴 RimDigitalLife 数码设备（默认 -5%）、档2=有殖民者订阅量子网络付费套餐（反射查 QuantumNet `GameComponent_QuantumNet.HasPaidPlan`，默认 -10%，取两档最大）；两档滑块均可调。
  - **店铺收入入国库**：`AfterCheckoutPaid`——按可调比例（默认 100%）把实付白银转销入数字国库（`ModifyTreasury(…, "店铺收入入账")` + `RecordStoreSale` 统计），**只销收银机附近(15格)实体银堆、销多少入多少，不凭空造币**；找不到银打一次性日志提示。
  - **管理台页签**：`SimShopUiApi.RegisterRuntimePage("RimPay_ShopAccount", "RimPay 账户", Both)`——显示今日/累计入账、国库余额、当前折扣档位。
  - **休眠机制**：未安装/未启用 RSMF 时零开销休眠（`ModLister.GetActiveModWithIdentifier` 检查）；RSMF 类型全部隔离在 RimSimIntegration.cs，缺 DLL 不影响主模组加载；QuantumNet 判定走反射（未装则档2不生效）。
  - **编译要点（重要踩坑，两轮修正）**：
    1. SimManagementLib.dll 目标框架为 **net481**（高于本项目 net472），MSBuild RAR 会 MSB3274 静默丢弃编译引用——第一版用编译期引用 + 继承 `ShopCheckoutWorker` 注册 Worker，编译通过但**游戏运行时炸**：RimWorld 加载 mod 程序集时 `Assembly.GetTypes()` 枚举类型，"继承 RSMF 类型"的类在**类型枚举阶段**触发 TypeLoadException → 整个 RimPay 程序集加载失败 → **所有 Gizmo/Harmony 全灭**（通讯台数字国库 gizmo 消失的根因）。类型隔离只能防方法 JIT，防不了 GetTypes()。
    2. **最终方案：纯反射零引用**——程序集里不出现任何 RSMF 类型，改为 Harmony 手动 patch RSMF 的 static API（`AccessTools.TypeByName`/`AccessTools.Method` 字符串定位）：patch `SimShopCheckoutApi.ModifyPaidSilver`（postfix 改 `__result` 折扣）+ `SimShopFinanceApi.CommitCheckout`（postfix，参数 `Building register` 用基类接 RSMF 的 `Building_CashRegister`）。csproj 已移除 SimManagementLib 引用；postfix 方法签名只含 Verse/RimWorld 类型；RSMF 未激活时 TypeByName 返回 null 自动跳过。
    3. 其他：`ShopUiPageScope` 在 `SimManagementLib.SimDef` 命名空间；激活检查用 `ModLister.GetActiveModWithIdentifier`（没有 HasActiveModWithIdentifier）。RSMF API 签名已用 ReflectionOnly 反射逐成员核对（Worker 六个 virtual：CanPawnEnterCheckout/BeforeCheckoutCommit/BuildCheckoutLines/ModifyPaidSilver/AfterCheckoutPaid/OnCheckoutFailed；Context 字段：customer/shop/register/billLines/paidSilver 等）。**教训：跨 mod 编译期引用第三方 mod 的 DLL 时，若目标是"可选依赖"，绝不能在程序集中出现任何继承/字段/签名级别的第三方类型——GetTypes() 阶段就会炸；只能字符串反射 + Harmony 手动 patch。**
  - 管理台页签功能已砍（RegisterRuntimePage 需要继承 RSMF 的 ShopUiPageWorker，同会炸）；RimPay 账户信息在数字国库页查看。
  - 设置：`enableRimSimIntegration`（默认开）/`rimSimTreasuryRatio`(1)/`rimSimDeviceDiscount`(0.05)/`rimSimQuantumDiscount`(0.10)，常规页新增「RimSim 商店联动」小节，viewHeight 3200→3400；csproj 引用 SimManagementLib（Private=False）。
- **AI 结果归档**：`RimPayAIProvider` 新增 `EconomySnapshot` 归档（最近 10 次成功结果：日期/各倍率/marketTone/宏观事件/头条），AI 设置页状态面板下方新增「AI 结果归档」区块（Tiny 字号逐条展示）。仅内存保存，读档后从新积累。AI 页 viewHeight 2600→3000。
- **版本号升级 v0.7.01**：`RimPayMod.cs` 设置标题与 `About.xml` 同步。
- **About.xml 描述按现状重写**：补齐 AI 经济中枢（多供应商/failover/测试连接/状态面板/宏观事件/刷新间隔/归档）、证券交易+盈亏、借贷中心、财富托管、边缘财经日报+流水账、RimTalk/RimTuber 联动、支付 API 等全部新功能；删除已不存在的"就医/娱乐扣费""花呗催收"描述。
- **托管资产后缀动态化**：国库页"托管资产(建筑/随身装备)"后缀随设置开关实时变化（勾选="不计入袭击威胁"，取消="计入袭击威胁"）。
- **空引用全面加固**（主菜单红字卡死根因修复后的复查）：`RimPayAIProvider` 请求发起、`CheckAndProcessPayroll`、`RecordTransaction`、`AddLoan/AddBorrow/ProcessLoanInterest` 共 5 处 `GenLocalDate/Find.CurrentMap` 空引用防护。
- **修复股票/流水/借贷存档丢失**：`StockData`/`TransactionRecord`/`LoanRecord` 补上 `IExposable` 接口（此前写了 ExposeData 但漏挂接口，Deep 存档静默失败——盈亏=市值、涨跌全 0% 的根因）；`PostLoadInit` 加旧档持仓成本回填迁移；删除从未使用的 `pawnStockHoldings` 死数据。
- **修复 Mod 设置经济参数不持久化**：`RimPaySettings.ExposeData` 此前只存了 AI 相关字段，约 48 个经济参数（薪资/房租/餐费/利息/借贷/股市/炒股/搜刮/钱包流水全部滑块与开关）从未写入，导致玩家调整后**重启游戏全部回默认**。已机械性补齐全部 `Scribe_Values.Look`（key 与字段名一致，默认值与字段声明一致，旧玩家配置无损兼容）。
- **医疗就医扣费 (RimPay 医保)**：新补丁 `Patch_MedicalFee.cs`（postfix `TendUtility.DoTend`），医生治疗完成时收取诊疗费——病人钱包优先支付，不足部分数字国库"医保报销"（不会因重伤破产），诊疗收入回流国库；病人头顶青色气泡提示费用与报销额；写入国库流水账（"诊疗收入"/"医保报销"），自动进日报"昨日账单"。设置：`enableMedicalFee`（默认开）+ `medicalBaseFee`（默认 10，滑块 0-100），常规页新增「医疗就医」小节。仅收玩家殖民者/奴隶的治疗（囚犯/敌人/动物不收）；手术/义体暂不收。csproj 已注册；About.xml 补医保描述；常规页 viewHeight 3000→3200。

### 0. 设置页分页 + AI 测试与验证工具（本轮新增）
- **设置页拆分为两个 Tab**：`RimPayMod.cs` 的 `DoSettingsWindowContents` 顶部加「常规设置 (General)」「AI 设置 (AI Economy)」两个按钮切换；常规页放薪资/房租/餐费/利息/借贷/股市/炒股/搜刮/钱包，AI 页放 AI 开关/供应商列表/状态面板/日报/JSON 模板。
- **「测试连接」按钮**：`DrawAIConfigEditor` 内新增，后台线程调用新增的 `RimPayAIProvider.TestConnection(cfg)`，实时显示 `[OK]`/`[FAIL]` 结果（含 HTTP 状态码与供应商专属提示，如 Player2 未启动、本地 Ollama 未监听 11434）。照搬 RimTuber 的 `AIProviderManager.TestConnection`。
- **「AI 经济实时状态面板」**：AI 设置页新增，实时显示 `salaryMultiplier / rentMultiplier / interestRateDelta / marketTone / eventText` 及请求日/连续失败/冷却/进行中/最近错误，用于验证 AI 是否真正掌控经济。
- **「立即请求一次」按钮**：新增 `RimPayAIProvider.ForceRequestNow()`，绕过每日一次/最小间隔/冷却限制，强制立刻让 AI 生成一组新数值并显示在状态面板。
- **AI marketTone 接入股市**：`UpdateStockPrices` 新增 toneBias，bull 普涨 / bear 普跌 / neutral 随机，让 AI 的行情判断真实影响证券 K 线。
- **设置页高度加大 + 默认常规页 + 标题带版本**：常规页 viewHeight 3000、AI 页 2600；默认显示常规设置；设置标题改为 `... (v0.6.05)`，删除页内底部重复版本标签。
- **数字国库界面设置入口**：`Window_TreasuryTerminal` 右上角新增「⚙ 设置 (Settings)」按钮，直接打开 RimPay Mod 设置页（`Dialog_ModSettings` + `LoadedModManager.GetMod<RimPayMod>()`），三个 Tab 均可见。
- **AI 财经播报桥接 RimTalk**：`GameComponent_RimPay.BroadcastEconomyAiToRimTalk()` 每日结算后随机一名殖民者触发 RimTalk 对话提到 `eventText`；Core `RimTalkBridge.TriggerDialogueViaQueue` 改为携带 userPrompt（此前被静默丢弃，`CompSmartAssistant`/炒股播报也因此内容更丰富）。开关 `enableAiRimTalkBroadcast`（默认 true）。**注意：Core 的 RimTalkBridge.cs 有同步改动，需与 Core 一起编译部署。**
- **$DSF 波动率接线**：`CreateStock("$DSF", ...)` 改用 `settings.stocksFundVolatility`（此前硬编码 0.30，滑块是摆设）。
- **借贷默认金额接线**：`Window_LoanAdjustment` 默认金额读 `settings.loanDefaultAmount`；设置页借贷区域新增滑块。
- **AI 掌控范围扩展**：AI 新增可返回 `loanRateMultiplier`（借贷利率倍率）、`mealFeeMultiplier`（餐费倍率）、`lootShareDelta`（搜刮分成增量），分别应用于借贷中心利率、进食扣费、敌人搜刮分成；JSON 模板与 AI 状态面板已同步。
- **AI 刷新频率可调**：原"每日一次"改为"间隔驱动"，新增设置 `aiRefreshIntervalHours`（默认 12 小时），且每次股价更新时顺带检查刷新，`marketTone` 可在一天内多次变化。旧字段 `aiRequestIntervalHours` 已废弃。
- **AI 影响交易环节**：`Patch_TradeSession` 新增 `Tradeable.GetPriceFor` 后置补丁，按 `tradePriceMultiplier`（AI 返回或由 marketTone 派生：bull=1.1 / bear=0.9）浮动买卖价格——牛市溢价、熊市压价。
- **AI 宏观事件**：AI 可返回 `macroEvent`（market_crash/gold_rush/shortage/tech_boom）；AI 未指定时按市场情绪以一定概率派生（牛 30%/熊 30%/中性 12%）。事件持续 3 天并弹「边缘财经快讯」信封，效果：崩盘→股市强下压+波动加大；繁荣→股市强上推；淘金热→敌人搜刮翻倍；短缺→餐费+50%。状态与 `GameComponent_RimPay` 存读档，状态面板显示"宏观事件"行。
- **国库真实流水账（修复"昨日国库收支"为 0）**：根因是旧 `GetDailyTreasurySummary` 从"个人钱包流水"反推国库收支，但餐费/存取款/股票/借贷/搜刮/贸易等大量国库收支根本不在个人流水里，且发薪/房租方向反了。新增 `TreasuryLogEntry` 流水账 + `ModifyTreasury(amount, reason)` 重载，所有国库收支调用点（发薪/房租/餐费/搜刮/存取款/股票买卖/借贷/利息/奖金/没收/贸易）均带原因记账；`GetDailyTreasurySummary` 改为从流水账汇总收入/支出。注意：新流水账自部署后开始积累，需再跨过一天才能看到"昨日"数据。
- **证券盈亏列**：国库持仓新增成本追踪（`treasuryStockCost`），`BuyStock/SellStock` 带价格参数维护成本；证券交易列表新增"盈亏"列（绿+红-），K 线详情窗口标题行也显示盈亏。
- **财富托管 (Wealth Escrow) 取代「国家资产(建筑)」**：改名并扩展为两开关（默认均开，设置页常规页「财富托管」小节）：
  - `hideBuildingWealth`：建筑（含地板）财富不计入袭击威胁点（`WealthBuildings×0.5`）。
  - `hidePawnEquipmentWealth`：小人/动物随身武器、衣物、驮载物品不计入威胁（`WealthWatcher.GetEquipmentApparelAndInventoryWealth`，遍历非机械体玩家派系 pawn）。
  - 机械体本体+内置武器整体不藏（战斗力挂钩）；小人/动物本体价值始终保留。
  - 数字国库白银已物理隐藏（存入即销毁），无需托管。
  - 国库资产条改两行：第一行 物理白银/数字国库/流动资产合计；第二行 托管资产(建筑)/托管资产(随身装备)（等高、双色：建筑紫/装备青）。
  - About.xml 已去掉"黄金"、补充托管措辞。
- **修复股票/流水/借贷存档丢失（盈亏=市值、涨跌全0% 的根因）**：`StockData`、`TransactionRecord`、`LoanRecord` 三个类都写了 `ExposeData()` 方法但**漏实现 `IExposable` 接口**，导致 `Scribe_Collections.Look(LookMode.Deep)` 静默存档失败（日志刷 "Cannot use LookDeep..."）。后果：每次读档 stocks 全部重置（涨跌归0%、股价回基准）、钱包流水清空、贷款记录清空、持仓成本丢失（盈亏=市值）。已补上接口声明；并在 `PostLoadInit` 加旧档迁移：有持仓但无成本记录的，按当前股价回填成本（盈亏从 0 起步，日志打 "[RimPay] 迁移持仓成本"）。
- **RimPay 支付 API + RimTuber 订阅扣款桥接**：`GameComponent_RimPay` 新增两个公共支付接口，供其他模组集成 RimPay（RimPay 作为数字经济基础设施，原版及走 Tradeable 交易的模组已被 `Patch_Tradeable` 自动无缝接管，无需集成）：
  - `TryPayFromTreasury(int amount, string reason)` → `bool`：仅从数字国库扣款，余额不足返回 false 不动国库。
  - `TryPayTotal(int amount, Map map, string reason)` → `int`：完整兜底支付，先扣地图物理银、不足自动从国库补齐，返回实际从国库扣的数额，-1 表示总资产不足（分文未动）。
  - RimTuber 的 `Compat/RimPayBridge.cs` 已反射封装这两个方法；`Window_SalesManager` 订阅扣款（月费500/年费5000）调用之。其他模组作者可仿照 RimTuber 的 RimPayBridge 反射集成，或参考 `TryPayTotal`。
- 版本号同步：`RimPayMod.cs` 与 `About.xml` 标注 v0.6.05。

### 1. 钱包"无流水"Bug 修复
- 根因：钱包收支列表在 `Widgets.BeginScrollView` 里又嵌了 `Listing_Standard`（它内部会 `BeginGroup`），导致流水标签画到视口外被裁剪隐蔽。
- 修复：改为纯 Rect 手绘行（与证券/借贷列表相同手法）。数据一直正常写入 `transactionLogs`，只是渲染被吞。
- 文件：`Source\UI\Window_PawnWallet.cs`

### 2. 全部经济逻辑接入 ModSettings（不再是摆设）
薪资、房租、餐费、余额宝利息、借贷利率/周期/上限/逾期惩罚、股市更新间隔/波动率/K线天数、小人炒股门槛/投入比例/RimTalk阈值/命中率、敌人搜刮分成、钱包流水条数 —— 全部读 `RimPayMod.settings`。
文件：`Source\Data\GameComponent_RimPay.cs`、`Source\Comps\Patch_DailyExpenses.cs`、`Source\Comps\Patch_PawnLoot.cs`

### 3. RimPay 专属设置入口（新增）
`RimPayMod.cs`（Mod 类，`SettingsCategory` = RimPay 拓展）+ `RimPaySettings.cs`（ModSettings）。此前 RimPay 一直共用 Core 的设置页，现在有了自己的设置页。

### 4. 每日财经日报信封
每日 00:00 结算后按开关发送 `边缘财经日报` 信封（国库/股市行情/借贷/薪酬概览），AI 开启成功时追加"AI 财经头条"。
- 开关：`showDailyEconomyLetter`（默认 true）
- 文件：`GameComponent_RimPay.cs` 的 `SendDailyEconomyLetter()`

### 5. 多供应商 AI 配置列表（本次重点）
参考 RIMTuber 的多配置架构，实现"勾选启用 + 顺序 failover"：
- 新建 `RimPayProviderConfig.cs`：单个配置（供应商/端点/Key/模型/启用开关）
- `RimPaySettings` 新增 `apiConfigs` 列表 + `GetEnabledConfigs()`（按列表顺序返回勾选启用项，旧单配置字段迁移进列表首位）
- 供应商：Google / OpenAI / DeepSeek / Grok / GLM / OpenRouter / SiliconFlow / **Player2（本地免费，复用 RimTuber Client ID）** / **Local（本地 Ollama/LM Studio）** / Custom
- `RimPayAIProvider.cs`：异步 UnityWebRequest，按列表顺序逐一尝试，某供应商失败/无额度/超时自动切下一个；全部失败→白字日志+冷却+回退本地随机倍率(1x)
- 设置 UI：勾选开关、↑↓ 调整顺序、✕ 删除、＋ 添加、点行选中编辑详情（供应商/端点/Key/模型）
- 设置页顶部保留 Player2 显著推荐话术

---

## 二、需要进游戏感受/验证的点

1. **钱包流水**：进游戏读档 → 选中小人 → 数字钱包 → 应能看到「每日工资/住宿房租/基金利息」流水（需先跨过一个 00:00 或先手动发奖金产生一条记录）。
2. **设置页分 Tab**：Mod 设置 → RimPay 页。顶部应有「常规设置 / AI 设置」两个按钮，点击切换；两页各自独立滚动，互不干扰。常规页检查各滑块可拖动且数值即时显示；AI 页检查供应商列表可勾选/排序/删除/添加；AI JSON 模板为「深色底编辑框 + 保存/还原」双按钮样式（同 RimTalk/RimTuber 提示词编辑器，点击保存才生效）。
3. **测试连接按钮**：AI 设置页 → 选中一个配置 → 底部点「测试连接」。填有效 Key 显示绿色 `[OK]` 及 AI 回复；填错 Key/端点显示红色 `[FAIL]` 及具体原因（如 HTTP 401 / 404 / Player2 未启动 / Ollama 未监听）。
4. **AI 状态面板 + 立即请求**：AI 页勾选「启用 AI 控制经济」→ 点「立即请求一次 AI 经济数据」→ 状态面板应显示新的倍率/利率增量/marketTone/财经头条，且日志出现"供应商[1/n] 成功"。再点一次可看到数值变化（AI 每次生成不同）。
5. **多供应商 failover**：配置 2 个启用项，把第一个填成无效端点、第二个填 Player2/有效端点；点「立即请求」，日志应有"供应商[1/2]失败→尝试下一个→供应商[2/2]成功"。
6. **marketTone 影响股市**：AI 返回 `bull` 时，当日股市应普遍上涨；`bear` 时普遍下跌。可多次「立即请求」对比证券交易界面 K 线方向。
7. **每日财经日报信封**：跨过 00:00 后，应收到「边缘财经日报」信封；AI 开启且成功时信内追加"AI 财经头条"。
8. **借贷/股票数值联动**：把借贷利率滑块从 8% 拖到 15%，打开借贷中心应看到利率变化；把 $DSF 波动率改大，证券交易 K 线应更剧烈（本轮修复后真正生效）。
9. **RimTalk 财经播报**：开启 AI + `enableAiRimTalkBroadcast`，每日结算后随机一名小人会触发 RimTalk 对话，内容提及当天 eventText（需已装 RimTalk；注意 Core 的 RimTalkBridge.cs 需同步部署）。
10. **借贷默认金额**：把设置里"借贷默认金额"滑块改掉，重新打开放贷/借款窗口，默认金额应随之变化。
11. **扩展 AI 字段**：AI 开启后天天看状态面板的 loanRateMultiplier/mealFeeMultiplier/lootShareDelta 是否有非 1 / 非 0 值；对应借贷利率、每顿餐费、搜刮分成是否浮动。
12. **AI 刷新频率**：把"AI 刷新间隔"设为 2 小时，多次点击「立即请求」或跨过股价更新，日志应有多次"经济变量已刷新"且 marketTone 可能变化。
13. **交易价格联动**：AI 为 bull 时，商人收购价应偏高；bear 时偏低。打开交易界面对比价格变化。
14. **宏观事件**：AI 开启后跨数日，可能收到「边缘财经快讯」信封（崩盘/淘金热/短缺/繁荣），状态面板"宏观事件"行出现事件及剩余天数；对应效果（崩盘股市大跌、淘金热搜刮翻倍、短缺餐费上涨）应可观察。
15. **财富托管**：打开数字国库，资产条两行——第一行 物理白银/数字国库/流动资产合计；第二行 托管资产(建筑)/托管资产(随身装备)。常规页「财富托管」两开关默认开：关闭「建筑财富托管」后袭击威胁点恢复计入建筑；关闭「随身装备托管」后恢复计入小人/动物随身装备。`历史`窗口财富保持真实数值不变。机械体与小/动物本体价值始终计入威胁。

---

## 三、剩余/下一轮可做

- **接口权限审计**：确认 `RimPayAIProvider` 的 Player2 请求头与本地 Local 端点在真实环境中正确（测试连接已带对应提示，仍建议真实环境核一次）。
- **发布前**：预览图（`About\Preview.png` 缺失，Core 有可参考）、工坊描述已按现状重写（v0.7.01）。
- **网购功能**：已确认归属量子网络拓展（QuantumNet，`enablePrivateShopping` 开关占位已存在），RimPay 仅提供支付通道（已就绪），去 QuantumNet 对话实现。

---

## 四、重要参考（照搬对象）

- RIMTuber 多配置与 failover：`D:\Visual Studio Code ALL\RimTuber\RimTuber\Source\Core\RimTuberSettings.cs`、`Source\Content\AIProviderManager.cs`
- RimPay 引用 Core 复用（RimTalkBridge 等）：csproj 已 `<Reference Include="RimDigitalLife">`