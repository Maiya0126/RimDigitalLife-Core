# RimDigitalLife: Quantum Net 量子网络拓展 — 交接说明 (Handoff)

> 本文件用于开启「Quantum Net 拓展模组专属对话」时，把完整状态与规划交接过去。
> 开发目录：`D:\Visual Studio Code ALL\RimDigitalLife-Core\RimDigitalLife_QuantumNet\`（目前仅有空骨架，尚无代码）
> 成品目录（游戏内）：`D:\games\steamapps\common\Rimworld\Mods\MaiyaMod02C Rim Digital Life - Quantum Net Expansion 量子网络拓展`（尚未部署）

---

## 一、模组定位

**Rim Digital Life - Quantum Net Expansion 边缘数码生活：量子网络拓展**
- 前缀：`maiya.RimDigitalLife.QuantumNet`（仿 RimPay 的 `maiya.RimDigitalLife.RimPay`）
- 前置：必须依赖 Core（`maiya.RimDigitalLife.Core`）+ Harmony
- 定位：**网络与 AI 算力主题扩展**，与 RimPay（经济主题）并列的第二拓展包

---

## 二、已确定的功能规划（来自历次讨论定稿）

### 1. 量子网络覆盖（星链系）
- **简化方案**：建造并通电原版通讯台（Comms Console）= 当前地图 100% 网络覆盖（不新建基站）
- 依赖 Core 的通讯台/终端相关代码

### 2. 三大流量/算力套餐（按 1 象 = 15 天计费）
- **本地流量包（免费）**：打字/看时间/基础问答，无工作 Buff；仅 Rimi/Sirim 离线简易响应
- **高速畅享包（60 币/15天）**：RimSeek 智算辅助（全局工作 +10%、科研 +15%）、健康提醒、看直播打赏、黑客破解敌人钱包（+20% 收益）、私有网购
- **量子无限包（180 币/15天）**：RimSeek 深度协作（+20% 效率且防分心）、RimTalk 跨地图视频通话、云端天气/深矿雷达预测
- 套餐自动选购：按小人钱包余额、性格（网瘾少年优先花钱）、远征需求、玩家报销政策

### 3. RimSeek 智算办公（代替物理家具）
- 明确定调：**不做人体工学椅/升降桌等家具**（与玩家已有家具模组冲突、且无贴图负担）
- 改为：小人携带任意数码设备 + 已订阅套餐 → 代码直接给"AI 智算辅助"效率 Buff

### 4. RimTuber 直播打赏（联动 RimTuber 模组）
- 殖民者看**玩家自己的直播**时，用 RimPay 钱包打赏 1~50 币
- 打赏资金**直接进数字国库**（玩家=主播，不再"同聚落创收"）
- 小人获得「支持主播 +6」心情
- **需要 RimPay 扩展**作为可选联动（无 RimPay 时打赏逻辑禁用，仅保留看直播心情）

### 5. RimTalk 跨地图通话（联动 RimTalk）
- 远征队/异地小人 + 量子无限包 → 触发跨地图对话
- Prompt 注入地理与情感上下文：距离主基地 X 公里、荒野/遗迹环境、思念对象
- 展示形式：`📡 [远征队·小明 ➔ 基地·小红]: "..."`

### 6. RimPay 钱包联动消费
- 流量套餐月租从小人 RimPay 钱包自动扣（外部流出，不进国库）
- 无钱包/未开启 RimPay 时降级为"不扣费、套餐默认本地包"

---

## 三、关键架构参考（照搬对象，务必先读）

1. **多供应商 AI 配置 + failover**（与 RimPay 完全同款）：
   - `RimDigitalLife_RimPay\Source\Settings\RimPayProviderRegistry.cs`
   - `RimPayProviderConfig.cs`（列表配置：启用开关/供应商/端点/Key/模型/顺序 failover）
   - `RimPayAIProvider.cs`（异步 UnityWebRequest + 白字日志 + 冷却回退）
2. **设置页模式**：`RimPayMod.cs`（ModSettings + 分段 Listing + 配置列表 + JSON 模板 + 还原默认按钮）
3. **跨模组桥**：Core 的 `RimDigitalLife\Source\Comps\RimTalkBridge.cs`（TriggerDialogue 等，RimPay 已在直接用）
4. **RimTuber 原始实现**：`D:\Visual Studio Code ALL\RimTuber\RimTuber\Source\`（直播弹幕、打赏、观众池）

---

## 四、开发规范与注意事项（沿用本项目多年约定）

1. **C# 语言版本**：仅 C# 7.3（无 `?=`、无递归 switch、无 C#8 递归模式），csproj 不设 LangVersion
2. **csproj 手工加文件**：旧式项目，每个新 .cs / .png / .xml 必须显式加 `<Compile>` / `<Content>`
3. **依赖引用**：`<Reference Include="RimDigitalLife">`（Core），如需 RimPay 联动则引用 RimPay 并在 About 声明可选依赖（`<loadAfter>` 而非强制 `<modDependencies>`）
4. **命名**：命名空间 `RimDigitalLife_QuantumNet`；AssemblyName 同名；成品目录 `MaiyaMod02C Rim Digital Life - Quantum Net Expansion 量子网络拓展`
5. **版本号**：起始 v0.1.01；数字国库/设置页底部与 About.xml 同步标注
6. **中文文案**：全中文界面；Debug 按钮格式「中文 (English)」；单位沿用「@银」（若与 RimPay 联动）
7. **图标规范**：64×64 PNG，图案外围黑色描边（不要整图黑框），不写字；参考 RimPay_Icon / RimPay_Treasury 的生成方式
8. **性能**：无每帧 ContentFinder；套餐/扣费低频 tick；AI 请求每日一次后台线程
9. **时间规则**：每次回复末尾必须显示需求时间/结束时间/合计用时（Get-Date，禁止猜测）
10. **只负责本模组**：新对话内只改 QuantumNet，不碰 Core / RimPay / 言出多彩 / RimTuber（只读参考）

---

## 五、推荐开发顺序（Roadmap）

```
Phase 1: 工程骨架 + About.csproj + 设置页（套餐开关/网络开关/套餐价格）
Phase 2: 通讯台网络覆盖判定 + 套餐自动选购/扣费（RimPay 钱包可选）
Phase 3: RimSeek 智算 Buff（打工/科研效率） + 停机降级逻辑
Phase 4: RimTuber 直播打赏联动（可选 RimPay）+ RimTalk 跨地图通话 Prompt 注入
Phase 5: 云端天气/深矿雷达预测 + 发布前检查（About、预览图、版本号）
```

---

## 六、新对话开场参考提示词

> "接下来我们只负责开发 RimDigitalLife 量子网络拓展模组（QuantumNet），
> 先读 D:\Visual Studio Code ALL\RimDigitalLife-Core\开发记录\QuantumNet拓展交接说明.md，
> 工程位于 D:\Visual Studio Code ALL\RimDigitalLife-Core\RimDigitalLife_QuantumNet\。
> 每次回复末尾按规范显示需求/结束/合计用时。"

---

## 七、补充定稿决策（2026-09-03 追加，与上文本对话确认）

1. **虚拟网购归属**：零食/玩具/游戏卡带/衣服/化妆品/虚拟主题等虚拟产品网购，**放在本拓展（QuantumNet）**。
   - 解锁条件为订阅「高速畅享包」，属于量子网络套餐服务体系
   - 职责划分：QuantumNet 提供服务（网购/直播/智算），RimPay 提供资金流转（钱包扣费）
   - 扣费通过 Core 桥（仿 RimTalkBridge）调用 RimPay 钱包，资金进国库；无 RimPay 时网购禁用

2. **AI 设置**：**必须做**，与 RimPay 完全同款但**独立实现一份**（不引用 RimPay 的 AI 类，RimPay 仅可选依赖）：
   - `QuantumNetProviderRegistry` / `QuantumNetProviderConfig` / `QuantumNetAIProvider`
   - 命名空间 `RimDigitalLife_QuantumNet`，异步 UnityWebRequest + 白字日志 + 冷却回退

3. **虚拟供应商**：定义 **3~5 个虚拟供应商**（星链提供商/云算力商/数字内容商等），仅作剧情与服务提供方设定（套餐、新闻、AI 对话中提及）。
   - **不加入 RimPay 股票市场**
   - **不保留 Harmony 注入 RimPay 股票作为可选方案**（明确否决）

---

## 八、Phase 1 完成记录（2026-09-03）

工程骨架 + About + csproj + 设置页（常规/AI）+ AI 供应商配置框架已建成，**编译通过（Release，无警告）**。

### 已创建文件
```
RimDigitalLife_QuantumNet\
├─ About\About.xml                          (packageId=maiya.RimDigitalLife.QuantumNet, v0.1.01, loadAfter 含 RimPay)
├─ RimDigitalLife_QuantumNet.csproj         (命名空间/AssemblyName 同名, PostBuildEvent → MaiyaMod02C)
├─ Properties\AssemblyInfo.cs
└─ Source\
   ├─ QuantumNetMod.cs                      (设置页：常规 Tab + AI Tab, 供应商列表/测试连接/JSON模板/状态面板)
   ├─ Settings\QuantumNetSettings.cs        (网络/套餐/功能开关 + AI 配置 + JSON 模板 + 迁移)
   ├─ Settings\QuantumNetProviderRegistry.cs(枚举 + 供应商定义, 与 RimPay 同款但独立)
   ├─ Settings\QuantumNetProviderConfig.cs  (单个配置 IExposable)
   └─ AI\QuantumNetAIProvider.cs            (异步 UnityWebRequest + failover + 冷却回退, 解析量子网络 JSON)
```

### 设置页字段（已定稿）
- **常规**：网络覆盖（总开关/需通讯台）、套餐周期(15天)、高速畅享包(60)、量子无限包(180)、自动选购、钱包扣费、畅享/无限包各功能开关
- **AI**：AI 网络智算开关、请求间隔、供应商配置列表（勾选/排序/删除/添加/测试连接）、实时状态面板、JSON 模板（networkStatus/cloudWeatherHint/deepMineHint/rimseekTip/newsText）

### 待办（下一 Phase）
- Phase 2：通讯台网络覆盖判定（Patch_CommsConsole）+ 套餐自动选购/扣费（RimPay 桥可选）
- 后续：RimSeek Buff、RimTuber 打赏、RimTalk 通话、云端天气/深矿预测、虚拟供应商剧情落地

---

## 九、Phase 2 完成记录（2026-09-03）

通讯台网络覆盖判定 + 套餐自动选购/扣费（RimPay 钱包可选）已建成，**编译部署成功**。

### 新增文件
```
Source\
├─ Data\GameComponent_QuantumNet.cs      (枚举 QuantumPlan + PawnPlanData + 核心结算 GameComponent)
├─ Bridges\QuantumNetRimPayBridge.cs     (反射桥：GetBalance/ModifyBalance, RimPay 可选)
├─ Comps\Patch_CommsConsole.cs           (通讯台 gizmo「量子网络」按钮 → 控制台)
└─ UI\Window_QuantumNetConsole.cs        (覆盖状态 + 殖民者套餐管理/手动切换)
```

### 实现要点
- **覆盖判定**：`CommsConsoleUtility.PlayerHasPoweredCommsConsole(map)`（原版，当前地图通电通讯台 = 100% 覆盖）；受 `enableQuantumNet` / `requireCommsConsole` 开关控制
- **套餐结算**：每天 00:00 低频结算（`GameComponentTick` + 每日一次）；无付费套餐 → 自动选购；到期 → 自动续费或降级本地包
- **自动选购**：报销政策(planSubsidy 开启→直接无限包) > 余额门槛（≥无限价→无限，≥畅享价→畅享）；网瘾少年判定占位 `HasInternetAddictTrait`（Phase 3 接性格）
- **扣费**：`planWalletPay` + RimPay 桥 → 钱包扣费（reason="量子网络套餐"，外部流出不进国库）；无 RimPay → 付费套餐不可用、降级本地包
- **部署修复**：PostBuildEvent 中文/全角冒号路径在 cmd 中字节错位 → 改为 `deploy.ps1`（PowerShell UTF-16 处理中文路径，cmd 只传 ASCII 参数），已验证部署成功

### 待办（下一 Phase）
- Phase 3：RimSeek 智算 Buff（打工/科研效率，畅享+10%/15%，无限+20% 防分心）+ 停机降级 + 网瘾少年性格
- 后续：RimTuber 打赏、RimTalk 通话、云端天气/深矿预测、虚拟供应商剧情

---

## 十、Phase 3 + Phase 4 完成记录（2026-09-03，合并完成）

RimSeek 智算 Buff + RimTuber 直播打赏 + RimTalk 跨地图通话 已建成，**编译部署成功**。

### 新增文件
```
Defs\
├─ HediffDefs\Hediffs_QuantumNet.xml       (QuantumNet_RimSeek_HighSpeed +10%工作/+15%科研；Infinite +20%工作/+20%科研)
└─ ThoughtDefs\Thoughts_QuantumNet.xml     (QuantumNet_SupportStreamer 支持主播 +6 心情)
Source\
├─ RimSeekBuffManager.cs                   (Phase 3：按 设备+套餐+覆盖 动态加/移 hediff，停机降级)
├─ Harmony\Patch_AntiDistraction.cs        (无限包防分心：抵消 Core 的 -5% 分心惩罚，依赖 loadAfter 顺序)
├─ Bridges\QuantumNetRimTuberBridge.cs     (反射桥：RimTuberAPI.IsLive 判断玩家是否在直播)
├─ StreamTipManager.cs                     (Phase 4：直播打赏 1~50 币 → 钱包扣费 → 国库入账 → +6 心情)
└─ CrossMapCallManager.cs                  (Phase 4：跨地图通话，注入地理/情感上下文，📡 展示)
```

### 实现要点
- **RimSeek Buff**：携带数码设备（`CompTerminalLink`）+ 付费套餐 + 网络覆盖 → 加 hediff；无覆盖/无套餐/无设备 → 移除（停机降级）；每 500 tick 低频更新
- **防分心**：`Patch_QuantumNet_AntiDistraction` patch `StatWorker.GetValue`，无限包用户带设备在线时 `/0.95f` 抵消 Core 的 -5%（QuantumNet loadAfter Core 保证执行顺序）
- **网瘾少年**：`HasInternetAddictTrait` = 生物年龄 ≤ 20，或 Transhumanist trait
- **直播打赏**：RimTuber 直播中 + 覆盖 + 付费套餐 + 直播屏幕 30 格内 → 打赏 1~50 币（钱包扣 → `ModifyTreasury` 进国库），获「支持主播 +6」；无 RimPay 仅保留心情
- **跨地图通话**：`Find.WorldObjects.Caravans` 找远征殖民者 + 无限包 → 每 2 天触发 `RimTalkBridge.TriggerDialogue`，Prompt 注入距离公里数/biome/思念对象；Log 展示 `📡 [远征队·X ➔ 基地·Y]`

### 重要：本机游戏版本 API（1.6.4871，与 MCP 源码一致）
- `Caravan` 在 `RimWorld.Planet` 命名空间；`Find.WorldObjects.Caravans`（非 Find.WorldCaravanManager）
- `Caravan.Tile` / `Map.Tile` 均为 `RimWorld.Planet.PlanetTile`（有 `.Valid` 属性）
- `Find.WorldGrid.ApproxDistanceInTiles(PlanetTile, PlanetTile)` → float
- biome 用 `Find.WorldGrid[PlanetTile].PrimaryBiome.label`（Tile 无 .biome）
- 若日后更换游戏版本，需重新核对上述 API

### 待办（下一 Phase）
- Phase 5：云端天气/深矿雷达预测（AI 数据落地到 GameComponent）、虚拟供应商剧情（3~5 家）、发布前检查（About 版本/预览图/图标/语言）
- 说明：云端天气/深矿预测已具备 AI 数据生成框架（QuantumNetAIProvider + 设置页），Phase 5 只需把 networkStatus/cloudWeatherHint/deepMineHint 落到游戏内展示（如 Mote/窗口/新闻）

---

## 十一、全图贸易信标（方案 A）完成记录（2026-09-03）

**功能定位**：任何地图（主基地/远征营地/临时据点）只要存在**任意一个通电的轨道贸易信标（OrbitalTradeBeacon）**，全图物品即可用于轨道贸易与吊舱发射。不依赖通讯台/量子网络覆盖。用于替代 Map Wide Orbital Trade Beacon 模组（1839069104，作者 Supes）。

### 新增文件
```
Source\
├─ MapWideBeaconUtility.cs                 (HasMapWideBeacon / AllLaunchableThingsForTrade 全图枚举 / FindThingOfDef)
└─ Harmony\Patch_TradeUtility.cs           (Prefix 补丁两处：TradeUtility.AllLaunchableThingsForTrade + LaunchThingsOfType)
```

### 关键改动
- **量子网络覆盖与全图信标解耦**：信标全图效果独立判定，不要求通讯台存在 → 野外营地也能全图贸易
- **PatchAll 修复**：此前 QuantumNetMod 未调用 `harmony.PatchAll()`，导致通讯台「量子网络」gizmo 补丁一直未生效——本次一并修复（`new Harmony("maiya.rimdigitallife.quantumnet")` + PatchAll）
- **设置项**：`enableMapWideBeacon`（默认 true）；设置页「常规」新增开关说明

### 覆盖的贸易链路（与 MapWideTradeBeacon 一致）
- 物品可见：`TradeUtility.AllLaunchableThingsForTrade` → 全图枚举（含书柜 HeldBooks / 服装架 HeldItems / 普通物品）
- 吊舱发射：`TradeUtility.LaunchThingsOfType` → 全图按 def 找物品转移

### 已知限制
- 仅 patch 了这两个主入口；地图内有通电信标时完全绕过信标范围判定（Prefix 返回 false）
- 若玩家同时启用 MapWideTradeBeacon 模组可能冲突，建议二选一（本功能可完全替代）
- 小人/囚犯/动物售卖（AllSellableColonyPawns）不受信标范围限制，无需处理

---

## 十二、Phase 5 完成记录（2026-09-03）— 全部 Roadmap 完成，版本升至 v0.2.01

云端天气/深矿预测 + 虚拟供应商剧情 + 发布前检查 完成，**编译部署成功**。

### 新增/修改
```
Source\
├─ QuantumNetProviders.cs                   (5 家虚拟供应商：星链通信/星云算力/游戏卡带/赛博百货/深矿雷达)
├─ Data\GameComponent_QuantumNet.cs         (新增 ProcessNetworkAI / SendDailyNetworkLetter / HasAnyInfinitePlanUser)
└─ AI\QuantumNetAIProvider.cs               (成功返回后 InjectProviderNews 注入供应商剧情)
Textures\UI\Icons\QuantumNet_Icon.png       (64×64 卫星+信号波纹，黑色描边，System.Drawing 生成)
About\About.xml                             (v0.2.01，功能描述补齐全图信标/云端预测/虚拟供应商)
QuantumNetMod.cs                            (SettingsCategory → v0.2.01；AI Tab 新增日报开关)
```

### 实现要点
- **AI 每日请求**：GameComponent 每天 00:00 触发 `QuantumNetAIProvider.RequestDailyNetworkIfNeeded()`（需网络覆盖 + 开启 AI；失败自动回退默认值 + 冷却）
- **云端天气/深矿预测落地**：有 AI 结果且至少一名无限包用户时，发送「量子网络日报」信封（网络状态/云端天气/深矿雷达/智算建议/新闻）；设置项 `showDailyNetworkLetter`（默认开）
- **虚拟供应商**：5 家注册在 `QuantumNetProviders`，AI 成功返回后 `InjectProviderNews()` 随机提及一家，进入当日新闻/日报
- **发布检查**：图标已生成并接入通讯台按钮；版本 v0.2.01；设置页标题同步

### 最终目录结构（v0.2.01）
```
RimDigitalLife_QuantumNet\
├─ About\About.xml / RimDigitalLife_QuantumNet.csproj / deploy.ps1 / Properties\AssemblyInfo.cs
├─ Defs\HediffDefs\Hediffs_QuantumNet.xml
├─ Defs\ThoughtDefs\Thoughts_QuantumNet.xml
├─ Textures\UI\Icons\QuantumNet_Icon.png
└─ Source\
   ├─ QuantumNetMod.cs / QuantumNetProviders.cs / RimSeekBuffManager.cs / StreamTipManager.cs / CrossMapCallManager.cs / MapWideBeaconUtility.cs
   ├─ Data\GameComponent_QuantumNet.cs
   ├─ Settings\QuantumNetSettings.cs / QuantumNetProviderRegistry.cs / QuantumNetProviderConfig.cs
   ├─ Bridges\QuantumNetRimPayBridge.cs / QuantumNetRimTuberBridge.cs
   ├─ Comps\Patch_CommsConsole.cs
   ├─ Harmony\Patch_AntiDistraction.cs / Patch_TradeUtility.cs
   ├─ UI\Window_QuantumNetConsole.cs
   └─ AI\QuantumNetAIProvider.cs
```

### 后续可扩展（非 Roadmap）
- RimSeek 智算建议 `rimseekTip` 落地为工作气泡/提示（目前仅日报展示）
- RimTalk 播报量子网络新闻（仿 RimPay 的 AI 财经播报）
- 私有网购虚拟商品目录落地（设置里已有 enablePrivateShopping 开关）
- 健康提醒（设置已有 enableHealthReminder 开关）
- 黑客破解敌人钱包（设置已有 enableHackWallet 开关，需 patch 敌人击杀/钱包）

---

## 十三、私有网购（方案 A 心情版）完成记录（2026-09-12）

小人用自己的 RimPay 数字钱包网购虚拟产品，**编译部署成功**。

### 新增/修改
```
Source\PrivateShoppingManager.cs            (网购触发/扣费/心情)
Defs\ThoughtDefs\Thoughts_QuantumNet.xml    (QuantumNet_OnlineShopping 收到网购包裹 +5 心情)
Source\Data\GameComponent_QuantumNet.cs     (GameComponentTick 挂载驱动)
```

### 触发条件（全部满足才有概率触发）
- 设置开关 `enablePrivateShopping` 开启
- 当前地图有量子网络覆盖（通电通讯台）
- RimPay 已安装（需钱包扣费）
- 小人订阅付费套餐（畅享/无限包功能）
- 小人携带数码设备（CompTerminalLink）
- 小人处于闲暇（娱乐/躺下/游荡）
- 每 2 游戏小时一次机会，35% 概率，每次仅一人

### 效果
- 从小人自己钱包扣 5~30 币（reason="私有网购·商品名"，外部流出不进国库）
- 钱不够则不买
- 获得「收到网购包裹 +5」心情（1.5 天）
- 消息提示：`[网购] 小明 用数字钱包在赛博百货购买了「游戏卡带」，花费 12 @银，包裹已送达！`
- 商品池：零食大礼包/限量版玩具/游戏卡带/新款衣服/化妆品/虚拟主题皮肤（对应"赛博百货"供应商设定）

---

## 十四、运营商事件系统 + 跨地图通话修复 + 日报改 12 点（2026-09-19）

**编译部署成功。**

### 1. 通讯运营商事件系统（新增 NetworkEventManager.cs）
- **触发机制**：事件结束后随机 2~5 天再触发下一个（`nextEventTick` 随机间隔），触发时刻天然随机分布，不固定 0 点；事件持续 1~3 天；触发时消息 + 信封通知；状态随存档保存
- **6 种事件与效果**：
  | 事件 | 运营商 | 效果 | 持续 |
  |---|---|---|---|
  | 星链风暴 | 星链通信 | `HasNetworkCoverage` 临时返回 false（RimSeek 停机/套餐不结算/通话网购中断） | 1~2 天 |
  | 网络拥堵 | 星云算力 | 所有付费用户 RimSeek 降级为拥堵版 hediff（+5%工作/+7%科研） | 1~2 天 |
  | 赛博百货大促 | 赛博百货 | 网购半价 + 消息标注（大促半价） | 2~3 天 |
  | 卡带新品发售 | CartoBoard | 网购触发概率 0.35→0.7 | 1~2 天 |
  | 深矿雷达校准 | MineScan | 日报深矿预测加【校准·高精度】标注 | 1~2 天 |
  | 套餐半价促销 | 联合 | 新订/续费半价 | 1~2 天 |
- 新增 hediff `QuantumNet_RimSeek_Congested`（拥堵版低强度加成）
- 控制台窗口显示当前事件状态（⚡ 黄字行）

### 2. 跨地图通话修复（基地侧接到来电模式）
- **问题**：反编译核实 RimTalk（2026-09-16 版）只处理地图上的小人（字符串池有 get_IsPlayerHome/GetNearByTalkablePawns，无任何 Caravan 引用）→ 原实现把对话请求塞给远征队小人会被忽略，对话不出现
- **修复**：触发方仍是远征队无限包小人（每 2 天随机发起），但**对话由基地小人承载**——prompt 改为基地小人视角"接到来电并回应"，RimTalk 必定处理；Log 仍显示 `📡 [远征队·X ➔ 基地·Y]`
- 星链风暴期间通话中断（NetworkDown 检查）

### 3. 日报改 12 点
- AI 请求 + 量子网络日报从每天 00:00 改为 **12:00**（与 RimPay 0 点结算/财经日报错峰，避免消息轰炸）
- 日报新增「运营商事件」状态行
- 用独立的 `lastNetworkAITick` 防重（不再复用套餐结算 tick）

### 4. 开发者模式事件触发按钮（QuantumNetDebugActions.cs）
- 开发模式 → 调试动作 → 「量子网络」分类，8 个按钮：
- 触发星链风暴 / 触发网络拥堵 / 触发赛博百货大促 / 触发卡带新品发售 / 触发深矿雷达校准 / 触发套餐半价促销 / 结束当前事件 / 事件状态
- `DebugForceEvent(id)`：无视随机间隔强制触发（已有活动事件先结束），触发后照常排下一个随机事件

### 5. 日报频次档位（参考 RimTuber 但按本模组实际调整）
- **三档**（QuantumNet 日报是"当日预测"时效性强，5 天档无意义；模板化内容每天发偏多）：
  - **关闭**：不请求 AI、不发日报（省 token，AI 数据仅设置页状态面板可见）
  - **每 2 天**（默认）：与运营商事件周期（2~5 天）匹配度最好
  - **每天**：重度玩家
- 设置页 AI Tab 三档按钮组（■ 高亮当前档）；旧 `showDailyNetworkLetter` 字段保留仅存档兼容
- 实现：`lastReportDay`（绝对天数）+ 档位间隔判断；12:00 检查；发日报同时发起下一次 AI 请求（日报用最近一次成功数据）

---

## 十五、v0.3.01：Debug 测试按钮 + 控制台白字 + 版本号规范（2026-09-19）

**编译部署成功，版本升至 v0.3.01。**

### 改动
1. **跨地图通话测试按钮**：开发模式 → 调试动作 → 「量子网络」→「测试跨地图通话 (Test Cross-Map Call)」；自动挑远征队小人（优先无限包）+ 基地随机接话人，绕过冷却/套餐/覆盖直接走完整链路；无远征队/RimTalk 时给出提示
2. **控制台事件状态改白字**：原黄色（易与游戏警告黄字混淆）改为正常白字（保留 ⚡ 前缀）
3. **版本号规范（新约定）**：此前无系统管理（v0.2.01 后一直没动）。规则：**功能新增/系统级改动升次段号（v0.3.01），小修/文案升末段号（v0.3.02）**；About.xml 与 SettingsCategory 必须同步

### 待用户决策
- **无 RimPay 时私有网购**：当前禁用（符合定稿）。可选方案：改用地图实体白银扣款（货到付款式）——待用户确认是否要做

---

## 十六、v0.3.02：RimTalk 播报量子网络新闻（2026-09-21）

**编译部署成功。**

### 实现
- **位置**：`GameComponent_QuantumNet.BroadcastNetworkNewsToRimTalk()`，在 `SendDailyNetworkLetter` 发出日报后调用
- **机制**（仿 RimPay 的 AI 财经播报）：随机一名**量子无限包订阅者**（只有他们能收到日报）通过 Core `RimTalkBridge.TriggerDialogue` 聊起今日资讯
- **话题**：从当日 AI 数据随机挑一条（网络新闻/云端天气/深矿雷达/智算建议），话术："刚看了中午的量子网络日报。{话题}，你们觉得呢？"
- **开关**：设置项 `enableRimTalkNewsBroadcast`（默认开），设置页 AI Tab；需 RimTalk 已安装
- **版本**：v0.3.01 → v0.3.02（功能新增升次段号）