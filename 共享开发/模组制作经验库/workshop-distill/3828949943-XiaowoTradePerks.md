# 工坊 3828949943 架构蒸馏档案 —— 小窝特性扩展 / XIAOWO-TradePerks

> 工坊 id `3828949943`｜manifest ID **`xiaowo_trade_perks`**（Harmony id、`ModData` 键前缀、本地化命名空间全用它，不是标题）｜程序集 `XIAOWOTradePerks.dll`｜`ModVersion 2.0.29`｜`GameVersions 0.46D`｜作者 遗忘的小窝｜8.4 万订阅｜工坊包 manifest 写 `<Capabilities />`，即**零能力**（9 个文件用反射，全部走 `ModReflect.Get/Target` 白名单）
> 反编译源：`缓存文件夹\decomp_ws\3828949943`（205 个 `.cs` / 18,705 行，ILSpy 产物、零注释、变量名由反编译器生成）
> 原版核对：`共享开发\reference\Assembly-CSharp\`（本文凡断言原版行为都以它为准，已逐条对行）
> **为什么值得读**：它和 `3828971731` 是**同一个骨架的两个代际**——那份把 44 条效果压成"一张表 + 一个门"；这一份前 25 条走老路（表 + 每条自己问门），后 20 条直接升级成"**判决位掩码 + 6 个纯函数**"，全库仍然没有一条"效果类"。它还是这批标杆里唯一把**原版天赋池当自家特性商店**用满的样本（注册 / 图标 / 互斥 / 额外槽位 / 开局点数上限全走原版 `ModHelper` + `StartingPerkList`），并且是少见的"零能力 + 用户自备 PNG/JPG 皮肤 + 零 `System.IO` 直调"三件齐活。

---

## 0. 一句话结论

**它不是"一张表 + 一个门"，而是"两张表 + 两种门 + 半个模组不是特性"**：25 条一代特性（`PreparedCatalog` 硬编码）+ 20 条二代特性（`NewPerkCatalog` 数组生成）= 45 条记录，其中 2 条恒真不注册、43 条进原版天赋池。一代特性的效果散在 `TradeEffects` / `TradeEconomyEffects` / `DeterministicSchedule` 里、**每条补丁体自己 `PerkRegistry.IsActive(id)` 问门**；二代特性把 20 条效果全部塞进 `NewPerkRules` 的 6 个纯函数（`Price/PreviewSale/Sell/Buy/Night` 等）+ 一个位掩码开关 `NewPerkGameplay.Flags`，由 `NewPerkGameplay` 这一个门统一结算。**它一次都没碰 `ItemFeature.valueModifier`**（对比 `3828971731` 直接改 `tips` 15→20），价格靠 Postfix `GameItem.GetNegociatedValue` 现算。

---

## 1. 规模与结构

| 目录 | 文件 | 行数 | 说明 |
| --- | ---: | ---: | --- |
| `XIAOWOTradePerks.Official\` | 25 | 1,275 | 领域层：特性表 / 记录 / 注册计划 / 存档迁移 / 故障预算 |
| `XIAOWOTradePerks\` | 178 | 17,411 | 全部业务：补丁 + 效果 + UI + 扩舱 + 皮肤 |
| `Properties\` + `System.Runtime.CompilerServices\` | 2 | 19 | 版本号 + C# 9 `init` 垫片 |
| **合计** | **205** | **18,705** | 另有 `TradePerks.RuntimeTranslations.json`（37 KB，嵌入资源）与 `.csproj` |

- **夹具不用猜**：**101 个 `[HarmonyPatch]` 类**（95 个带显式 `typeof(类型),"方法"`，6 个裸 `[HarmonyPatch]` + `TargetMethods()`）→ **80 个显式（类型,方法）靶点** + `TargetMethods` 动态产出 32 个（`NewPerkTradePatch` 9 / `NativeFreeStorageCategoryPatch` 16 / `BayExpansionWindowVisibilityPatch` 3 / `FinanceReport*` 2+2）+ `NativeSkinWindowPatch` 覆盖 `PixelWindow.Show` 全部返回 `PixelWindow` 的重载 → **约 113 个被挂方法**。
- **补丁方法 119 个**：`Prefix` 38 / `Postfix` 70 / `Finalizer` 11 / **`Transpiler` 0**。`[HarmonyPriority(800)]` ×9（都是"事务开括号"）、`[HarmonyPriority(0)]` ×15（都是"事务闭括号 / 想最后跑"）。`ModReflect.Target` 用 27 次——**它不写裸 `AccessTools.Method` 找靶点，全部过白名单反射**。
- **按目标类型看它动了谁**：`PlayerStore` 14、`GameItem` 10、`StoreClientManager` 8、`RunStats` 4、`NewGamePerksUI` 4、`GameGridInventory` 4、`StoreEvent` 3、`ItemFeature` 3、`AugHelper` 3、`AppraisalServiceHelper` 3。**没有一条 `Transpiler`，热点 `GameItem.IsTag` / `GetCorrectBaseValue` / `SpriteDict.GetSprite` / `StoreUIManager.Update` 全部零命中**。
- 规模分布：中位数 **21 行**、p90 **188 行**、p95 344 行；>500 行只有 4 个：`BayExpansionPerk` 2164 / `ContainerWindowResizer` 2040 / `SkinManager` 1551 / `FunctionSettingsUI` 728。**典型形态是 12~30 行的"一 Patch 一文件"**，不是一特性一文件。
- **文件命名即模块**，无子目录，前缀严格分族：`Native*` 41（原版接缝：存档/时钟/窗口/天赋图标/皮肤）、`New*` 13（二代特性）、`Bay*` 13 / `WineRack*` 2 / `Fridge*` 3 / `Aquaculture*` 4（精工扩舱）、`Cash*` 8（日历收支）、`Inventory*` 7 / `Container*` 2（窗口视口）、`Settings*`+`FunctionSettingsUI`+`Shortcut*`（设置界面）、`Perk*` 3（门 + 存档键）、其余每个特性 1~3 个文件。
- **"一特性一文件"存在，但不是特性本体——是补丁体**。43 条特性里只有 20 条二代特性做到"一条效果一个调用点"，其余是一批 12 行的单补丁文件，效果函数都住在 `TradeEffects` / `TradeEconomyEffects` / `NewPerkGameplay` 里。**值得学的是这个划分**（补丁只做"翻译原版调用到我的函数"，逻辑集中在少数几个命名文件），**不值得学的是量**：≤20 行的文件 **87 个 / 1,189 行**（占文件数 42%、主目录行数 6.8%），`GreyInsurancePatch` 这种 12 行文件唯一的内容就是"Postfix → 调一个函数"。
- **半个模组不是特性**：`Bay*` 2164 + `ContainerWindowResizer` 2040 + `SkinManager` 1551 + `FunctionSettingsUI` 728 + `NativeSkinsUI` 344 + `SettingsWindowResizer` 431 ≈ 7,300 行是"扩舱 / 花盆式窗口缩放 / 自定义皮肤 / 设置面板"，与特性无关。**看到 205 文件别以为 205 个特性**。

---

## 2. 它怎么做这件事

### 2.1 特性怎么定义：两张表，一个记录类，零个特性类

字段只有 7 个（`PerkSpec.cs:8-24`）：`Id / Cost / Tone(Positive|Negative|Neutral) / Icon / ExtraSlots / AlwaysOn / Incompatible`，外加两个派生键：`NameKey => Id+"_name"`、`DescriptionKey => Id+"_description"`（名字描述**不进代码**，走 `ModHelper.GetLocalized("xiaowo_trade_perks", key)`）。

| 代 | 表 | 条数 | 写法 | 代价类型 |
| --- | --- | ---: | --- | --- |
| 一代 | `PreparedCatalog.Perks`（`PreparedCatalog.cs:9-36`） | 25 | 25 个 `new PerkSpec(...)` 字面量硬编码 | +1…+5 共 15 条 / 0 共 3 条 / **−1…−3 共 7 条**（负分挑战：安保眼红、店铺破败、常规信用、拾荒莽夫、猎杀契约、运营附加费、稀薄供给） |
| 二代 | `NewPerkCatalog.Specs()`（`NewPerkCatalog.cs:8-42`） | 20 | 一个 `string[20] Slugs` + 一个 `int[20] costs` + **一个 for 循环**生成，互斥关系用三元表达式算 | −2…+2 |

两表 `Concat` 成 `PreparedCatalog.Perks`（`PreparedCatalog.cs:36`）= **45**；其中 `owner_deal` / `precision_bay_expansion` 标 `alwaysOn: true`（`:23`、`:28`），不注册进天赋池，但 `PerkRegistry` 对它们恒返回 true（见 2.2）。**43 条进池 = manifest 写的"43 项特性"，`ShowReadiness()` 那句"23 个可选特性"= 一代的 23 条**——数字全对得上。

> 二代表的写法值得抄：20 条成本用一行数组、互斥用三元表达式（`:29-40`），加第 21 条只要加一个 slug + 一个成本。

### 2.2 效果怎么生效：两种门，一条现算、一条真改字段

**门只有两个实现，都在 `PerkRegistry.cs:5-16`**：

```
IsActive(id) → 若有存档 → 两条恒真 id 直接 true，其余 ModHelper.IsStartingPerkActive(id)
             → StartingPerk.IsPerkActive(id) 线性扫 PlayerStore.Instance.startingPerks（原版 StartingPerk.cs:227-237）
```

- **一代（25 条）**：没有统一调度器。效果函数被拆进 4 个命名文件，**每个补丁体第一行自己问门**：
  - `TradeEffects`（228 行）= 纯数学：`ApplySaleModifiers`（停电时 ×1.1、奢侈品客 ×0.9）、`InsuredHeat`（heat/2）、`ShortenedCooldown`（×0.75）、`RundownAttractiveness`（−150）、`RenovationCost`（×1.2）……
  - `TradeEconomyEffects`（144 行）= 供应商折扣、声望增益、谈判点数（+10，上限 95）。
  - `DeterministicSchedule`（403 行）= 日历上的现金流事件（见 2.6）。
  - 直接改原生字段的少数几条：`GreyInsurance` 改 `StolenHelper.SetHeat`、`FirstBuyer` 调 `client.OverrideBudget`、`Diplomat` 用 `rep.SetReputation` 补回一半损失。
- **二代（20 条）**：**一个判决位掩码 + 一个纯函数库 + 一个门**。`NewPerkGameplay.Flags`（`:73-87`）循环 20 次问门拼成 `[Flags] NewPerk` 枚举（`NewPerk.cs:6-29`，bit0…bit19）；效果全在 `NewPerkRules` 的 6 个纯函数里用 `Has(flags, NewPerk.X)` 分支；`NewPerkGameplay` 的 14 个入口（`AdjustPrice / Buyer / AppraisalPaid / Injury / Service / Night / Diplomacy / Tooltip / BatchPreview / Begin / End …`）统一 `Safe()` 包住、统一 `Commit()` 结算。
- **它一次都没碰 `ItemFeature.valueModifier`**：全库唯一创建 `ItemFeature` 的地方是 `CashflowAllItemsFeaturePatch.cs:23-31`，走原版工厂 `ItemFeature.CreateFeatureFromNegociationData(val)` + `gameItem.AddItemFeature(...)` —— 抄原版事件的 feature，不手写 modifier。

### 2.3 一条特性从定义到生效：完整调用链

**二代（例：Retail Specialist 卖价 +6%）**

1. 定义 → `NewPerkCatalog.Specs()` 第 15 条 `retail_specialist`（`NewPerkCatalog.cs:41`）→ 进 `PreparedCatalog.Perks`。
2. 注册（只在 `OnEnable` 一次）→ `ModEntry.OnEnable` 组 `RegistrationPlan`（`ModEntry.cs:28-31`）→ `OfficialCatalogRegistrar.RegisterPerks` 逐条 `ModHelper.AddStartingPerk(modId, id, cost, tone, ()=>GetLocalized(name), ()=>GetLocalized(desc), "xiaowo_trade_perks:"+icon, incompatible[], extraSlots, false)`（`OfficialCatalogRegistrar.cs:11-20`）。
3. 原版侧：`ModHelper.AddStartingPerk` 构造 `StartingPerk` 塞 `nameProvider/descriptionProvider` 后调 `StartingPerkList.RegisterModPerk`（`reference\...\ModHelper.cs:407-431`）→ 出现在新游戏天赋界面。
4. 玩家买下 → `NewGameData.startingPerks` → `PlayerStore.InitialSave` 写盘；`NativeInitialSavePatch` Postfix → `NativeRuntime.InitialSaveCompleted()` → `Bind()+Loaded()`（`NativeRuntime.cs:105-113`）——**新档开局没有 `OnGameLoaded*`，必须靠这个补丁补一次绑定**。
5. 读档 → `ModHook.OnGameLoadedEarly` → `NativeRuntime.Bind()` → `Session.Loaded(saveSlotId+":"+runID)` + 跑 `SaveMigrationPlan`（`NativeRuntime.cs:115-138`）。
6. 游戏问价 → `GameItem.GetNegociatedValue()` → `NewPerkQuotePatch`（`[HarmonyPriority(0)]`，排在 gen1 的默认 400 之后）→ `NewPerkGameplay.AdjustPrice(item, value)`（`NewPerkQuotePatch.cs:9-17`）。
7. 三道闸：`_paused`/无存档身份 → 原样返回；买卖方向判定（事务上下文优先，否则 `TradeEconomyEffects.IsPurchasePreview`）；`Ordinary(Client, buying)` 只认普通买卖客，**排除安保 / 通缉 / 事件客 / `xiaowo_` 前缀的自家人**（`NewPerkGameplay.cs:187-208`、`TradeEconomyEffects.cs:118-134`）。
8. 拼掩码 `Flags`（`NewPerkGameplay.cs:73-87`）→ 掩码里没有价格类特性就整段跳过（`:246-249`）→ `NewPerkRules.Price(value, flags, buying, loaded, machinery, unitValue, area, salesCount)` 纯函数算百分比（`NewPerkRules.cs:22-67`）→ Postfix 写回 `__result`。**到这一步为止没写过任何存档、没改过任何物品**。
9. 真成交 → `PlayerStore.SellItem` → `NewPerkTradePatch.Prefix`（800）开事务：记 `playerCash`、记客与账号代次，`Depth++`（`NewPerkTradePatch.cs:24-33`、`NewPerkGameplay.cs:277-320`）。
10. 每件入账 → `RunStats.OnItemSold` Postfix → `Record(item, cost, false)` 快照（`NewPerkSoldPatch.cs:8-13`）。
11. 收尾 → `NewPerkTradePatch.Finalizer`（0）→ `End(t)`：`playerCash` 差值即实际成交额，**`rows.Sum(cost) > 差值` 就整笔放弃**（`:349-352`，防"记录比实付多"）；然后 `Commit()`：先序列化旧账 → `NewPerkRules.Sell()` 改账/加钱 → `Persist()` 写 `new_perks.v1`，抛异常则 `playerCash` 回滚并重存旧 JSON（`NewPerkGameplay.cs:166-185`、`:331-395`）。
12. 提示 → `Notice()` → `Core.Log.Msg`（只进日志，不弹窗；`NewPerkGameplay.cs:416-419`）。

**一代（例：Grey Insurance 旧货保险）**

表项 `PreparedCatalog.cs:13` → 同一个 `RegisterPerks` 注册 → 触发点 `PlayerStore.OnItemBought` Postfix → `TradeEffects.ApplyGreyInsurance(item)`（`GreyInsurancePatch.cs:5-11`）→ 函数内第一行问门 `PerkRegistry.IsActive("xiaowo_trade_grey_insurance")`（`TradeEffects.cs:146`）→ `StolenHelper.SetHeat(item, heat/2)` + 双保险防重（物品 tag `XIAOWO_GREY_INSURANCE_VISIT` + 存档键 `grey_insurance_visit`=周|日|访次|客id，`TradeEffects.cs:168-182`）。**差别就在这一步：一代是"每条自己问门"，加一条效果 = 加一个补丁 + 一个 id 判断。**

### 2.4 原版特性系统长什么样：它用了哪些、扩展了哪些、绕过了哪些

| 原版面 | 它怎么用 |
| --- | --- |
| `ModHelper.AddStartingPerk(...)`（`ModHelper.cs:407-431`） | **主注册口**。一行注册 = id + 成本 + 正/负/中性 + 两个 `Func<string>` 懒取本地化 + `iconKey` + 互斥数组 + 额外槽位。43 条全走它 |
| `ModHelper.RemoveStartingPerks(modId)`（`:433`） | `OnDisable` 卸载清干净（`ModEntry.cs:44`），**不留幽灵天赋** |
| `ModHelper.IsStartingPerkActive(id)`（`:438`） | 唯一判决口，`PerkRegistry` 包一层 |
| `StartingPerkType` 三态 | `Tone` 枚举直接映射：Positive→0 / Negative→1 / Neutral→2（`OfficialCatalogRegistrar.cs:13`）——**用原版的颜色语义表达"加分/扣分挑战"** |
| `StartingPerk.iconKey` + `StartingPerkIconLoader` | 见 2.7，**它踩了一个真坑** |
| `NewGamePerksUI.MaxPerkCount / CanStart / Rebuild / frame` 私有字段 | 自由构筑：3 个 Postfix 把上限改 500（`NativeFreeBuildCountPatch.cs` 等），并在 `frame` 上**手搓两个 Toggle**（`FreeBuildMode.cs:65-155`，250×30 深紫底 + 12×12 青绿勾）——UI 直接复刻原版配色 |
| `StartingPerk.IsShortSleeper()` | 被 `ThirdWatchShortSleeperPatch` 改判（短睡特性互斥） |
| **绕过**：原版 `HandleNewGamePerk` 那一串硬编码 `if (StartingPerk.IsXxx())` | 完全不走。**效果一律用"运行时问门"实现**，所以它能在中途改变、能读档后重新问、能被设置页单独关掉——而"开局发道具"式实现（我们 `ArcPerks` 现在就是）做不到这点 |

### 2.5 存档：三层，零原版结构改动，"save capacity"不是存档配额

| 层 | 键 | 内容 | 位置 |
| --- | --- | --- | --- |
| 天赋选择 | （原生）`PlayerStore.startingPerks` | 玩家在新游戏界面买的 43 条里的哪几条 | **原版存档结构**，模组不写 |
| 二代账本 | `new_perks.v1`（一整段 JSON） | `NewPerkState`：日/周、各类 claimed 标记、`VarietyIds`(≤3)、`Purchases`(supplier→≤3 种货，≤64 供应商)、`Sales/Power/Cleaning/Insurance` 快照 | `ModHelper.SetModData("xiaowo_trade_perks", …)` = 模组私有键 |
| 一次性标量 | `daily_client_generation` / `grey_insurance_visit` / `owner.visitors.completed` / `owner.stock.plan` / `settings.<key>` / `ui.<key>` | 日标记、理赔指纹、访客完成位、当日货单、每存档开关 | 同上，**每功能一键，都是短字符串** |
| 物品级 | `XIAOWO_GRID_*` / `xiaowo_trade_purchase_age_v1` / `XIAOWO_GREY_INSURANCE_VISIT` / `LIQUID_CONTAINER_CAPACITY` / `xiaowo_owner_vendor_stock` | 扩舱后格数、进货日期、理赔指纹、水箱容量、货架库存 | **物品 tag**（跟物品走，跨存档自然正确） |

- **跨存档**：身份 = `saveSlotId + ":" + runID`，配 `LoadSession.Generation` 单调递增（`LoadSession.cs:11-19`）；`NewPerkGameplay.State()` 把 `_identity`/`_generation` 双比对，不一致就**从 `ModData` 重读**（`:119-138`）。`OfficialSaveData` 每次构造记下当时的代次，`Check()` 发现代次变了直接抛 `InvalidOperationException("Stale save adapter")`（`OfficialSaveData.cs:38-44`）——**迟到的异步写永远写不进新档**。
- **归档迁移**：`SaveMigrationPlan` 把 MelonLoader 时代的 6 个 `xiaowo_trade_*` 键一次性搬到无前缀键，带 marker `migration.legacy_1_9_32.schema=1` + 冲突即抛 `MigrationConflictException`（`SaveMigrationPlan.cs:9-21`、`:41-70`）；`NativeSaveValues.Read` 保留 `store.modData[legacy]` 兜底读（`NativeSaveValues.cs:16-29`）。**读老键、写新键**，这是卸载/升级不炸的关键。
- **"save capacity"（简介原话 "Perk effects and save capacity are unchanged"）不是存档配额，而是"已解锁的容器格数"**：扩舱把每格坐标写进物品 tag `XIAOWO_GRID_<zone>_COLUMNS/_ROWS`（`ExpansionLimits.cs:43-69`），上限由 `ExpansionLimitRules.All` 15 条硬编码规格给出（`ExpansionLimitRules.cs:11-28`，如 `fridge_chill` 5×9→9×13、`ras` 12×8→24×16），并在 ModConfig 里逐条暴露成可调项，非法值回落默认（`ExpansionLimits.cs:17-29`）。另有 `LIQUID_CONTAINER_CAPACITY`（12,000,000 起、每级 +6,000,000，`AquacultureExpansionWater.cs:11-17`）与水厂等级。**模组自己的存档占用极小**：一个 JSON + 十几个短键，随机性靠 `runID` 哈希推导（见 2.6），**不存随机结果**。

### 2.6 Patch 靶点表（按域归并，热点逐条核对）

| 域 | 原版方法 | 手法 | 目的 | 风险 |
| --- | --- | --- | --- | --- |
| 价格 | `GameItem.GetNegociatedValue` ×3 | Postfix(400/0) | 一代 ×0.9/×1.1 修饰；二代进价格表 | **全库最热**，每次问价跑 20 次门查询（见 §4 红线 1） |
| 交易 | `PlayerStore.BuyItem / SellItem / BuyItemFromTable / SellItemFromTable / *AllItemFromTable`、`NegociationUIManager.BuyMultipleItem / OnWholesaleClick / SellMultiple`（动态 9 个） | Prefix(800)+Finalizer(0) | 事务开合，靠 `playerCash` 差值算真实成交额 | 只包不替换；`Depth` 计数器防重入 |
| 交易记账 | `RunStats.OnItemBought / OnItemSold / OnServicePaid / OnScavengeInjury` | Postfix | 逐件快照 → 账本 | 4 个都在 `RunStats`，属原版统计口，安全 |
| 声望 | `StoreReputation.ModReputation(double)`（显式 `new Type[]{typeof(double)}`）、`StoreReputation.OnItemTraded` | Prefix 存旧值 + Postfix 判损失 / ThreadStatic 上下文 | 外交官特性：损失补回一半、上限 3.0 | 上下文必须在 `Finalizer` 里还原 |
| 客人生成 | `StoreClientManager.HandleNormalClient`、`TrackClient` ×2、`TryAddClient`、`HandleInspectionClient`、`ComputeInspectionCooldownRange`、`AddAugClient`、`CheckRentDay`；`StoreClientInstance.CreateClientInstance` ×2；`StoreClientList.CreateLandlordClient` | 多为 Prefix(800) 拦截 / Postfix 加预算 | 每日额外预约客、大额客预算、熟客预警、猎杀契约 | 客人链是本作最脆的地方，8 个靶点全在这一处 |
| **热点** | `StoreClientListDict.CreateStoreClient` | Prefix 按 id 拦，`__result = OwnerVisits.Create()` 后返回 false | 塞入自建"业主密约"货商 | 见 §4 红线 3；**只按 identifier 精确匹配，不碰其它客** |
| 特性界面 | `NewGamePerksUI.Open / CanStart / MaxPerkCount / RefreshCounters` | Postfix | 自由构筑 500 点 | 需在 `Open` 后重建 Toggle，注意 Unity 假 null |
| 结算界面 | `RunSummaryUI.BuildOverview` / `AddPerkSlot` | Postfix | 特性列表滚动 + 图标尺寸 | 见 2.7 |
| 服务/经济 | `StoreService.UpdateCost`、`StoreEvent.GetEventPriceEffect / GetDisplayName / IsItemAffectedByThisEvent`、`StoreEventManager.OnDayStart`、`NetworkUpgrade.GetCost`、`ScavHelper.Get*WoundChance` | Postfix 改返回值 / Prefix 加事件 | 运营附加费、现金流事件、夜间拾荒风险 | 改返回值型，低风险 |
| 形状/扩舱 | `GameItem.ValidateShapeState` ×4、`CanTarget / MayTarget / Target`、`MaterialDirectory.CanIngotTarget / OnIngotTarget`、`MachineFridge.ApplyStorageSize`、`MachineRAS.ApplyTankSize`、`AnimalCage.GetCageInventory`、`HusbandryHelper.GetAnimalOvercrowdingLevel`、`StorageExpansionHelper.Expand / RestoreTabs` | Postfix 修复 / Prefix 拦锭子 | 精工扩舱（把金属锭拖到自己设备上升级格数） | `ValidateShapeState` ×4 是**读档校验兜底**，改错会掉货 |
| 容器 | `GameGridInventory.Validate / ValidateBackground / GetLocalPosition / HighlightSlot`、`TreeNodeRender.OnScroll`、`PixelWindow.Show/Hide` | Postfix | 视口滚动 + 窗口缩放 | 与其它改容器窗口的模组**正面冲突** |
| 存档 | `PlayerStore.WriteSlotFile`、`InitialSave`、`StartNewGame` | Prefix+Finalizer / Postfix | 写盘前把算出来的服务费**还原成原生形状**再存（`OperatingSurcharge.cs:76-94`）、新游戏后补绑定、新档前清状态 | 只包不改结构 |
| 其它 | `AppraisalServiceHelper.GiveAppraisalFee / SetString`、`BargainUIManager.GetBargainSuccessChance`、`EscapeUIManager.OpenEscapeMenu`、`KeyBindHelper.get_SuppressHotkeys`、`MainMenuUIController.OnAnyButtonClick`、`ModConfigWindow.Open`、`StartOfDayUIManager.InitPanel`、`AdvCalendarUIManager.OnStatusButtonClicked`、`AugHelper.*` ×3、`EndOfDayUIManager.OnEndDayButtonClick`、`StolenHelper.InitStolenItem`、`ContainerHelper` 13 个 lambda 靶点 | 混合 | 鉴定费、砍价、快捷键、菜单加按钮、日历收支、猎杀契约 | `ContainerHelper+<>c` 一类**编译期生成的 lambda 名**是硬编码坑（§4 红线 4） |

**热点核对结论**：`GameItem.IsTag` 未挂、`GetCorrectBaseValue` 未挂、`SpriteDict.GetSprite` 未挂、`StoreUIManager.Update` **未挂**（`StoreUIManager` 只被读 `Instance / IsAnyTyping / isShot`）。替代方案：自建 `NativeRuntimeBehaviour : MonoBehaviour` 挂 `LateUpdate`，`CursorTick()` 每帧、`Tick()` **0.5 秒节流**（`NativeRuntimeBehaviour.cs:17-26`）——**用一个 DontDestroyOnLoad 的空 GameObject 换掉一个热点 Update 补丁**，这条比我们的做法更省兼容预算。

### 2.7 UI：特性列表 / 图标 / 滚动（2.0.29 修的那两个 bug）

- **列表不自己画**：43 条直接进原版天赋界面，靠 `AddStartingPerk` 的 `name`/`description` 闭包 + `GetLocalized` 懒取文本。它不复制原版 UI。
- **图标三条路，它走了两条半**：`iconKey = "xiaowo_trade_perks:" + p.Icon`（走原版 `CustomUIManager.GetSprite` 的 `modid:name` 解析，见 `StartingPerkIconLoader.cs:12-31` 的优先级 1），同时 `NativePerkIcons.Publish()` 往 `StartingPerkIconLoader.perkIcons[id]` 里再塞一份（优先级 2）。**为什么要塞两份**：`StartingPerkIconLoader.Start()` 里有一句 `perkIcons.Clear()`（`StartingPerkIconLoader.cs:37`），场景重载后字典被清空 → 它用 `AdvCalendarUIManager.OnStatusButtonClicked` 的 Prefix 每次重进日历就 `Publish()` 一遍（`NativeCalendarPerkIconsPatch.cs:5-11`）。**这是本档案里最典型的一个"补丁补丁"**。
- **`RunSummaryUI.AddPerkSlot` Postfix = 2.0.29 的"图标缺失/过大"修复**（`RunSummaryPerkIconPatch.cs:42-65`）：取刚生成那一行最后一格里的 `icon` Image → 换成自家 Sprite → 按 `min(64/w, 64/h)` 重算 `sizeDelta`、`preserveAspect=true`、`type=Simple`、`raycastTarget=false`。**原因是原版槽位给的是固定尺寸，模组图不是 1:1 就会被拉伸或溢出。**
- **`RunSummaryUI.BuildOverview` Postfix = "长特性列表在分区内滚动"**（`RunSummaryPerkListPatch.cs:59-141`）：不重画列表，而是**找到原版 `perk_slot` 的父容器，给它套一层 `ScrollRect` + `RectMask2D` 视口 + 底部 8px 横向滚动条**，`movementType=Clamped`、`scrollSensitivity=36`，`LayoutElement` 高度锁 138。防重入靠名字判断 `parent.name == "TradePerks.SummaryViewport"`（`:64`）——**Postfix 里建 UI 必须自带幂等标记**。
- 设置面板走原生 `CustomUIManager.Instance.CreateWindow("xiaowo_trade_perks.features", …, "meta"/"menu", optionUIPrefab)` + `CustomUIBuilder` 分页（`FunctionSettingsUI.cs:138`），并 `[HarmonyPatch(typeof(ModConfigWindow), "Open")]` 在主菜单/暂停菜单的原版配置窗上加按钮——**不另造窗口系统**。

---

## 3. 关键代码摘录

**① 注册的唯一出口：表里有什么就注册什么**（`XIAOWOTradePerks.Official\OfficialCatalogRegistrar.cs:11-20`）

```csharp
foreach (PerkSpec p in plan.Perks)
{
    StartingPerkType val = (StartingPerkType)((p.Tone != PerkTone.Positive)
        ? ((p.Tone == PerkTone.Negative) ? 1 : 2) : 0);
    string[] array = new string[p.Incompatible.Count];
    for (int i = 0; i < array.Length; i++) array[i] = p.Incompatible[i];
    ModHelper.AddStartingPerk("xiaowo_trade_perks", p.Id, p.Cost, val,
        () => ModHelper.GetLocalized("xiaowo_trade_perks", p.NameKey, Array.Empty<object>()),
        () => ModHelper.GetLocalized("xiaowo_trade_perks", p.DescriptionKey, Array.Empty<object>()),
        "xiaowo_trade_perks:" + p.Icon, array, p.ExtraSlots, false);
}
```

**② 门 + 掩码：20 次字符串扫描换来一个位集**（`XIAOWOTradePerks\NewPerkGameplay.cs:73-87`，`PerkRegistry.cs:5-16`）

```csharp
internal static NewPerk Flags {
    get {
        NewPerk newPerk = NewPerk.None;
        for (int i = 0; i < 20; i++)
            if (PerkRegistry.IsActive(NewPerkCatalog.Id(i)))
                newPerk = (NewPerk)((int)newPerk | (1 << i));
        return newPerk;
    }
}
// PerkRegistry: 两条恒真 id 直接 true，其余 ModHelper.IsStartingPerkActive(id)
```

**③ 20 条效果挤在一个纯函数里**（`XIAOWOTradePerks.Official\NewPerkRules.cs:40-66`，节选）

```csharp
else {
    if (Has(flags, NewPerk.Retail)) num += 6;
    if (Has(flags, NewPerk.Selective) && salesCount >= 5) num -= 10;
    if (!loadedContainer) {
        if (Has(flags, NewPerk.Machinery)) num += (machinery ? 10 : (-5));
        else if (Has(flags, NewPerk.ValueRetail))
            num += ((unitValue <= 50) ? 10 : ((unitValue >= 200) ? (-5) : 0));
        else if (Has(flags, NewPerk.LargeGoods))
            num += ((area >= 6) ? 10 : ((area <= 2) ? (-5) : 0));
    }
}
return Math.Max(1L, (long)Math.Min(2147483647m,
    Math.Round((decimal)value * (decimal)num / 100m, MidpointRounding.AwayFromZero)));
```

**④ 事务开合 + 差值对账 + 回滚**（`NewPerkTradePatch.cs:24-47`、`NewPerkGameplay.cs:166-185`、`:342-352`，合并节选）

```csharp
[HarmonyPriority(800)] private static void Prefix(MethodBase __originalMethod, out Trade? __state) {
    Trade trade = null;
    NewPerkGameplay.Safe(() => trade = NewPerkGameplay.Begin(
        __originalMethod.Name.StartsWith("Buy", StringComparison.Ordinal)
        || __originalMethod.Name == "OnWholesaleClick",
        __originalMethod.Name.Contains("All")));
    __state = trade;
}
[HarmonyPriority(0)] private static Exception? Finalizer(Trade? __state, Exception? __exception) {
    try { NewPerkGameplay.End(__state); } catch (Exception ex) { Core.Log?.Error("…" + ex); }
    return __exception;
}
// Commit：旧账先序列化，改账→加钱→Persist，抛异常则回滚 playerCash 并重存旧 JSON
string text = JsonConvert.SerializeObject(_state);
int playerCash = NativeGame.Store.playerCash;
try { mutate(_state); long n = amount(); NativeGame.Store.playerCash = checked((int)(playerCash + n)); Persist(); }
catch { NativeGame.Store.playerCash = playerCash; _state = JsonConvert.DeserializeObject<NewPerkState>(text); throw; }
// End：真实成交额 < 记录成本之和 → 整笔放弃
long num = t.Buying ? (t.Cash - Store.playerCash) : (Store.playerCash - t.Cash);
if (recorded <= 0 || num < recorded) return;
```

**⑤ 一条效果改原版字段：先问门，再双指纹防重**（`XIAOWOTradePerks\TradeEffects.cs:144-183`，节选）

```csharp
if (item == null || !PerkRegistry.IsActive("xiaowo_trade_grey_insurance")
    || !StolenHelper.IsStolenItem(item)) return;
TagState tagReadonly = item.GetTagReadonly("STOLEN_VALUE_INT");
if (tagReadonly == null) return;
string text = InsuranceVisitKey(CurrentWeek, CurrentDay, val2.identifier,
    StoreClientManager.GetCustomerTotalVisitedCount(val2.identifier));
if (!InsuranceVisitAllowed(NativeSaveValues.Read("grey_insurance_visit"), CurrentWeek, text)) return;
if (item.state.GetTag("XIAOWO_GREY_INSURANCE_VISIT")?.GetString() != text) {
    NativeSaveValues.Write("grey_insurance_visit", text);            // 存档侧指纹
    int num = tagReadonly.GetInt(), num2 = InsuredHeat(num);
    if (num2 != num) StolenHelper.SetHeat(item, num2);
    item.state.EnableTag("XIAOWO_GREY_INSURANCE_VISIT");
    item.state.GetTag("XIAOWO_GREY_INSURANCE_VISIT").SetString(text); // 物品侧指纹
    item.SyncModifiedState();
}
```

**⑥ 把原版列表塞进滚动视口**（`RunSummaryPerkListPatch.cs:62-107`，节选）

```csharp
RectTransform val2 = (RectTransform)val.parent.parent;          // 原版 perk_slot 的父容器
if (val2.GetComponent<HorizontalLayoutGroup>() != null
    && ((Transform)val2).parent.name != "TradePerks.SummaryViewport") {   // 幂等
    GameObject val3 = new GameObject("TradePerks.SummaryPerks",
        typeof(RectTransform), typeof(LayoutElement), typeof(ScrollRect));
    var component2 = val3.GetComponent<LayoutElement>();
    component2.preferredWidth = 1200f; component2.preferredHeight = 138f;
    GameObject val4 = new GameObject("TradePerks.SummaryViewport",
        typeof(RectTransform), typeof(RectMask2D));
    ((Transform)val2).SetParent(val4.transform, false);
    val2.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = FitMode.PreferredSize;
    var component3 = val3.GetComponent<ScrollRect>();
    component3.viewport = val5; component3.content = val2;
    component3.horizontal = true; component3.vertical = false;
    component3.movementType = MovementType.Clamped; component3.scrollSensitivity = 36f;
}
```

---

## 4. 踩坑与红线

1. **门是 O(n) 线性扫描，而它每次问价都要扫 20 遍**。`ModHelper.IsStartingPerkActive` → `StartingPerk.IsPerkActive` 对 `PlayerStore.Instance.startingPerks` 逐条 `string.Equals`（原版 `StartingPerk.cs:227-237`），而 `Flags` 一访问就是 20 次查询（`NewPerkGameplay.cs:73-87`），`Flags` 又出现在 `AdjustPrice` / `Tooltip` / `BatchPreview` / `Buyer` / `Night` 每一次调用里。**自由构筑开到 500 点 = 500 条天赋时，单次问价 10,000 次字符串比较。** 抄这个架构必须加缓存（按 `Session.Generation` 失效）。
2. **`[ThreadStatic]` 上下文必须配 `Finalizer` 还原，不能只写 Postfix**。`Trade.Current`、`ReputationTarget`、`_playerSellingDepth`、`_playerBuyingDepth` 四处都是这个结构（`PlayerSaleContextPatch.cs:9-20`、`NewPerkRepContextPatch.cs:9-19`）；漏还原 = 余下整局的价格/声望全算错。
3. **`StoreClientListDict.CreateStoreClient` 是本作最危险的热点之一，它只按 identifier 精确匹配后 `return false`**（`NativeOwnerFactoryPatch.cs:8-16`）。**红线**：任何"我不知道这个 id 就返回 null/空客"的写法都会把原版所有客人打空——必须 `if (identifier != 我的id) return true;`。它给自建客复用了原版模板 `StoreClientUniqueList.LandlordWholesale()`（`OwnerVisits.cs:67`）并 `ModHelper.SetClientSource(val, "xiaowo_trade_perks")` 打来源，卸载后原版能识别"这是模组客"。**更优解是往 `StoreClientListDict.storeClientDict` 注册工厂、让原生自动打来源**（见 §5 第 6 条）——它之所以没这么写，是因为它的货商要"每次现场构造"。
4. **`ContainerHelper+<>c__DisplayClass9_0` 这类名字是把编译器生成名写死进代码**（`NativeFreeStorageCategoryPatch.cs:19-33`，13 个此类靶点 + 3 个 `+<>c`）。原版一改 lambda 布局就全废；它用 `ModReflect.Target` + 显式 `Type[]{typeof(GameItem), typeof(GameInventory)}` 签名把"找不到"变成**加载期异常**而不是静默不生效——这是它唯一可辩护的地方，但仍是我们不推荐的做法（我们应优先找 `ContainerHelper.CanContainerStoredItem` 这类真方法做单点拦截）。
5. **二次注入进原版静态字典 = 会被 `Clear()` 吃掉**。`StartingPerkIconLoader.Start()` 里 `perkIcons.Clear()`（`StartingPerkIconLoader.cs:37`），所以必须在别的时机反复 `Publish()`。**正解是只依赖 `iconKey="modid:name"` + `CustomUIManager` 注册**（优先级 1 那条路本就够），塞字典是补丁的补丁。
6. **`Safe()`/`_paused` 是全库唯一的错误策略，且它是"停一个功能，不是停全部"**：`NewPerkGameplay` 一旦异常就 `_paused=true`、整段结算停到下次成功读档，并只报一次错（`:145-164`）；`NativeRuntime` 用 `LoadFaultBudget` 按功能名计失败次数，**同一功能连错 3 次就本加载周期内停跑**（`LoadFaultBudget.cs:25-32`、`:48-74`，每次成功 `Run` 就清零）。这套"分功能熔断 + 成功即清零"比全局 try/catch 强得多，代价是它把 `catch` 写进了内部逻辑（违反我们"对内暴露"的规范）——**我们只借熔断器，不借它允许内部吞异常**。
7. **写盘前必须把派生值还原成原生形状**：`OperatingSurcharge.BeginSave()` 在 `WriteSlotFile` 前缀里把服务费**写回原生字段**，`EndSave()` 再按当前状态刷新（`OperatingSurcharge.cs:76-94`）。不还原 → 存进去的是"模组算式的结果"，卸载后数值错乱。
8. **它在 2.0.29 承认自己"这次没验"**：简介明写"Hardware dragging, natural overnight, long-term full loads and every MOD combination were not re-tested"。**它自己都不信自己的 205 文件全绿**——这正好说明这类"补丁满天飞"的扩展，验证成本随靶点数线性增长。

---

## 5. 对我们的可用点

### 对《深空电玩》（`deepspace_arcade` / `DeepSpaceArcade.dll`）

1. **`src/ArcPerks.cs` 立刻换成 `ModHelper.AddStartingPerk`**。我们现在是手工 `new StartingPerk{…}` + `StartingPerkList.RegisterModPerk`（`ArcPerks.cs:24-56`），缺三样它有的：`Func<string>` 懒本地化（换语言不用重建）、`incompatiblePerks` 互斥（正/负特性互斥不用自己判）、`extraPerkSlots`。**改动只有一处注册调用**，收益是"名字/描述跟语言切换走"。
2. **把"开局发道具"改成"运行时问门"**。现状是 `ModHook.OnNewGameStarted` 一次性发（`ArcPerks.cs:62`），所以老档永远拿不到、热重载就丢。抄 `PerkRegistry.IsActive(id)` 那 8 行 + 在真正用道具的地方问一次，**就能让"天赋是读档后仍然成立的条件"而不是"开局那一瞬的赠品"**；顺手把"二手游戏商"改成"刻录机折扣/批发价"这类可随时求值的形状。
3. **客户立绘/图标遇到"列表里图标被拉花"的同类 bug，直接抄 `RunSummaryPerkIconPatch` 的 64px 归一 + `preserveAspect`**（`RunSummaryPerkIconPatch.cs:47-65`）。我们 8 张 `dnpc_*` 立绘走的是另一条路（`SpriteDict.GetSprite` 自愈），但**天赋/成就/统计汇总这类固定尺寸槽位**需要的是这个写法，不是换贴图。
4. **自建 `NativeRuntimeBehaviour` 节流 tick，别再挂 `StoreUIManager.Update`**。它把每帧要做的事拆成 `CursorTick()`（每帧）和 `Tick()`（0.5 s，`NativeRuntimeBehaviour.cs:17-26`）。我们 `ArcExpressions` 的 1.8 s 遣散协程、报纸配图首帧拉伸都吃过 Update 顺序的亏——**这条能直接省掉一个热点补丁**。
5. **零能力读用户自备图片的完整写法**（`SkinManager.cs:313/402/511/543` + `NativeSkinImageHeader.cs:6-19`）：`ModHelper.GetModDataFolder / ListModDataFiles / ReadModDataFileBytes / WriteModDataFileText / ModDataFileExists` + 路径白名单（`Skins/` 前缀、禁 `\`、禁 `:`、禁 `..`）+ **手工解析 PNG/JPEG 头拿宽高，超 4096px / 16 MB 直接拒**，再交 `Texture2D.LoadImage`。我们若要开"玩家自备封面/贴纸"，这是唯一不破零能力的路。

### 对《深空当铺：可能是群友》（`freya_cornell` / `DeepSpacePawn.dll`）

6. **`CreateStoreClient` 这个口我们已经走对了，但可以再收两点**。我们现在是往 `StoreClientListDict.storeClientDict` 注册工厂委托、再调 `CreateStoreClient(id)`（`src/FreyaClients.cs:9-13` + 5 处调用）——**这条路比它更优**：原生 `CreateStoreClient` 命中字典后会自动 `ModSourceHelper.MarkFromFactory(client, factory)` 打来源（`reference\...\StoreClientListDict.cs:1120-1123`），而它是 Prefix 拦在字典查找**之前**，所以必须自己补一句 `ModHelper.SetClientSource(val, "xiaowo_trade_perks")`（`OwnerVisits.cs:75`）。**要继续抄的两点**：① 自建客的底座直接用原版模板（`StoreClientUniqueList.LandlordWholesale()`，`reference\...\StoreClientUniqueList.cs:2643`），批发折扣 `SetWholesale(15)`、`CanReturn=false`、`dismissable`、`mainDialogue` 一次配齐（`OwnerVisits.cs:67-76`），比手搓 `StoreClient` 字段少踩一堆坑；② 货单**不要塞进模板**，模板只负责"这个人是谁"，货在 `mainDialogue.SetEndAction(SpawnStock)` 里现算（同 `:74`）。
7. **"当日货单确定性 + 只存一个 marker"**：`Seed = hash(runID + day)` → 货单可复现；每日只写 `owner.stock.plan = "day|id|id|…"` 一行（`OwnerVisits.cs:79-118`），货池过滤用 `DirectoryMaster.GetFactory<GameItem>(id)?.Method.DeclaringType?.Assembly != typeof(GameItem).Assembly` **只放原版物品，天然避开跨模组货**。我们 `FreyaStock` / `Shuangluoluo.StockSellerItems` / `Siren.StockWaterMerchantPack` 现在是每次现摇、概率写死在代码里——**换成这套，就等于"同一天反复读档不会刷出不同货"，且省掉一整份存档状态**。
8. **`owner.visitors.completed` 那套"完成位掩码 + 按日解码"**（`OwnerDealRules.DecodeVisitState`，`OwnerVisits.cs:29-39`）：一个 int 存"今天商人和房东各来过没有"，比我们现在的多键/多 bool 省。可直接用于"本周名片/电话/展柜三件事是否已发生"。
9. **用户自备 PNG 皮肤的沙箱读写**（同第 5 条）——我们已经有 `art/` 与 `Textures/` 资产管理，这条是把"官方贴图"扩展到"玩家换装"的现成模板，且零能力、设置存在 `ModData` 外（不污染存档）。

---

## 6. 与同批 `3828971731` 的对照（附录）

| 维度 | `3828971731` 势力声望 | 本作 `3828949943` 小窝特性 |
| --- | --- | --- |
| 效果表达 | 44 条一张表，**一个门** `PerkActive(id)` | **两张表 + 两种门**；二代升级成位掩码 + 6 个纯函数 |
| 效果形态 | 改 `ItemFeature.valueModifier`（tips 15→20） | **零 `valueModifier`**：Postfix 问价现算 / 直改 `playerCash`、`heat`、`rep`、`client.budget` |
| Transpiler | 1 个（带降级路径） | **0 个** |
| 存档 | `progress_v1` 大对象 + 写盘投影回原版 ±100 | 一个 JSON 账本 + 十几个短键；随机靠 `runID` 哈希，**不存随机结果** |
| 天赋系统 | 用原版声望阈值解锁 | **把原版天赋池当商店**（注册/图标/互斥/额外槽/500 点自由构筑） |
| 边界 | 178 文件全是声望线 | 205 文件里约 7,300 行与特性无关（扩舱/皮肤/设置） |

**一句话回答"它是不是也是表+门"**：**一半是**——一代 25 条是"表 + 每条自己问门"，二代 20 条才是"一张表 + 一个门 + 一个位掩码"，而且它比声望那份**多做**了"把原版天赋池吃干"（注册/图标/互斥/点数上限/结算界面），**少做**了"改原版数值字段"和"任何 Transpiler"。
