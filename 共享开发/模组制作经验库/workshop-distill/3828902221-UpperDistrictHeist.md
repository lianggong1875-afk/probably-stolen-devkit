# 工坊 3828902221 架构蒸馏档案 —— `upperdistrict`《上层区拾荒其实是偷窃》v2.01.84

> **工坊 id**：`3828902221`｜作者「我是卡奴m卡拉比丘的杯子」｜`ModVersion 2.01.84`｜`GameVersions 0.46D`｜`<Capabilities />` 为空｜`<Prerequisites>` 硬依赖 `xiaowo_night_auction` / `brewing_expansion` / `tobacco_expansion`（以上均实读 `D:\steam\...\4348910\3828902221\manifest.xml`）｜装配 `UpperDistrict.dll`｜约 2.3 万订阅。
> **反编译目录**：`缓存文件夹\decomp_ws\3828902221\`（97 个 `.cs` / **12,185 行**；`UpperDistrict.Native\` 54 文件（贴 Unity），`UpperDistrict.Native.Core\` 42 文件**无一行 `using UnityEngine`**；另 26 张美术（18 jpg + 8 png）、`Locale.en/zh.csv`、`Assets.csv`）。
> **为什么值得读**：它是全库目前**唯一"四级分级地点 + 入场费/等级/冷却/声望四维门槛 + 多层概率产出 + 每日警戒预算"全部做成纯函数 Core** 的样本，而且**接入方式选得对**——用官方 `ModHelper.AddAfterhourLocation` 注册面 + 自建全屏舞台，**不反射伪造 `MapUIManager.Current`、不替换 `Travel`、不管外出次数**（对比 GoFishing 档③为自建地点付的 28,502 行，见 `3829110153-GoFishing.md` §3.1）。想学"打烊后的新去处到底挂哪一层"，它的答案比 GoFishing 更便宜、比 Sakurazaki 更规整。

---

## 2. 规模与结构

### 2.1 目录 → 行数

| 目录 | 文件 | 行数 | 占比 | 职责 |
| --- | ---: | ---: | ---: | --- |
| `UpperDistrict.Native\` | 54 | 9,481 | 77.8% | 入口 `ModEntry`（**6,217 行**）、28 个补丁壳、GUI 拼装、三个外部模组绑定器 |
| `UpperDistrict.Native.Core\` | 42 | 2,694 | 22.1% | **纯 C# 规则层**：地点表、准入、声望、警戒、概率池、彩票、工厂、存档编解码 |
| `Properties\AssemblyInfo.cs` | 1 | 10 | 0.1% | 程序集信息 |

`ModEntry.cs` 一个文件吃掉全库 **51.0%**，是典型"单入口神类 + 壳补丁"形状；但与本库其它巨型模组不同，它把**所有可测规则都外置到了 Core**——`Core` 42 个文件里 `UnityEngine` 出现 **0 次**，可以直接搬进离线测试工程跑。

### 2.2 扩展点用量（源码实测，非透视报告口径）

| 扩展点 | 实测 | 备注 |
| --- | ---: | --- |
| `[HarmonyPatch(typeof(X),"M")]` | **28 个壳文件** | 透视报告只列了 10 处：它的正则漏掉 `new Type[]{...}` 显式重载表形式（如 `BoxContentsAuctionOutputHeat.cs:5-9`）。**别信透视报告的补丁计数** |
| `ModHook` 订阅 | 12 种 | `OnGameLoadedInit/Late`、`OnModItemDirectoryInit`、`OnModAssetsLoaded`、`OnLootTablesLoaded`、`OnNewGameStarted`、`OnGenerateCustomerVeryLate`、`OnPlaceInventorInventoryItemLate`、`OnLeavingStoreEarly`、`OnReturningStoreLate`、`OnWakingUpLate`、`OnGoingSleepEarly` |
| `ModHelper` 调用 | 22 种 | 核心是 `AddAfterhourLocation` / `RemoveAfterhourLocations` / `ReturnFromAfterhour` / `GetCurrentAfterhourLocationId` / `MoveItemToAfterhours` / `AddCredit` / `Get/SetModData` |
| `ModReflect`（经 `PublicInterop`） | 6 种 | 只用于：跨模组（拍卖行/酿酒/烟草）绑定 + 取消原版地图行程（`NativeMapCancellation.cs:37-57`） |
| 自建 UI | `CustomUIManager` + `NativeGui` | 全部窗口**自绘**在 `CustomUIWindow` 上，屏占比用归一化坐标硬编码 |

---

## 3. 它怎么做这件事

### 3.1 四级地点怎么表达：**一张 6 行表 + 一个枚举**，没有"每级一个类"

`DistrictId` 是 6 值枚举（`Core\DistrictId.cs:3-11`：`Upper/Security/Wine/Vault/Orchard/Tobacco`），每级的全部差异收在一个 `readonly` 字段类里，实例化成**静态数组**：

`Core\DistrictRules.cs:7-15`（下文 §4.1 全文引用）：`new DistrictDefinition(id, key, level, fee, step, cooldown, faction, reputationCost, investigation, experience)`，6 条数据把"等级/首次费/每次递增/冷却天数/声望阵营/声望预算/入场加调查/入场加经验"一次说完。**没有 `UpperDistrict` 类、没有 `VaultDistrict` 子类**，全部行为由 `DistrictRules.Get(id)` 取数据 + 分支函数算出来。

`DistrictDefinition` 只带两个派生函数（`Core\DistrictDefinition.cs:41-61`）：

- `AdmissionFee(priorVisits) = BaseFee + FeeStep * priorVisits`
- `EntryReputationCost(priorVisits)`：非金库直接返回常量；金库 `min(280, 100 + 20*priorVisits)`（**首次 100、每次 +20、封顶 280 就写在这一行**）

### 3.2 数值逐条对账（简介表格 vs 源码）

| 地点 | 等级 | 入场费 | 声望预算 | 调查 | 冷却 | 入场经验 | 声望阵营 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 上层区 `Upper` | 0 | 300 | 5 | +2 | 0 | 5 | `FACTION_UPPER_LEVEL` |
| 治安部 `Security` | 1 | 700 | 10 | +5 | 0 | 10 | `FACTION_SECURITY` |
| 酒窖 `Wine` | 2 | 1500，每次 +100 | 20 | +2 | 2 天 | 5 | `BE_FACTION_OUTER_RING` |
| 金库 `Vault` | 3 | 8000，每次 +2000 | 首次 100、每次 +20、≤280 | +2 | 7 天 | 15 | `FACTION_SECURITY` |
| 果园 `Orchard` | 0 | 0 | 0 | 0 | 0 | 5 | 无（不扣声望） |
| 烟店 `Tobacco` | 2 | 2000 | 15 | +2 | 4 天 | 5 | `FACTION_BLACK_MARKET` |

数据源：`Core\DistrictRules.cs:9-14`；阵营名映射 `ModEntry.cs:5153-5169`。**简介里那张表全部对得上**，但有三处必须改正口径：

1. **"声望额度"不是"可用的声望上限"，是"这一趟最多会被扣掉多少声望"**（`ReputationCost` 字段）。实际扣法见 §3.6：入场只扣 `ceil(预算/5)`（=20%），离场按警戒占比补扣剩余 80%。所以"上层区 5"读作"最多掉 5 点上层区声望"，不是"进一次要 5 点声望"。
2. **盗贼等级是 5 级（0–4），"四级"说的是地点等级**：`ThiefLevel()` 阈值 `20 / 80 / 140 / 260`（`Core\DistrictRules.cs:26-45`）；地点只要求 `0/1/2/3`（`RequiredLevel`），所以**金库是第 4 级地点，不是第 4 级盗贼**。
3. **果园与烟店是简介表格漏掉的两级**：果园 0 元打工——`OrchardRules` 把 `DistrictState.Alert` **复用作体力**（`AlertLimit = 1000`、每次摘果 `+200`、每次工资 200、声望 +2、种子数 `1 + 声望/50`，`Core\OrchardRules.cs:7-49`）；烟店 2000 元 4 天冷却。

> 简介里另有一句"工业区首次 5000 元永久解锁四厂"，对得上：`FactoryRules.UnlockFee = 5000`、`Unlock()` 检查 `cash < 5000` 才抛（`Core\FactoryRules.cs:7`、`:28-33`）。

### 3.3 入场条件：一个纯函数 + 一个 12 值失败枚举

判定只有一处，签名是**数据进、判决出**：

```csharp
AdmissionQuote Quote(RunState run, DistrictId id, VisitContext context)   // Core\ExpeditionRules.cs:7
```

`VisitContext` 只装 6 个外部事实：`Day / Night / RemainingOutings / Cash / LootReady / UnsettledReputation`（`Core\VisitContext.cs:7-19`），由 `ModEntry.Context(id)` 从原版 `PlayerStore` 抄出来（`ModEntry.cs:5704-5718`）。判定顺序写在 `CommonFailure`（`Core\ExpeditionRules.cs:30-57`），失败即 `return`，**第一个失败原因就是 UI 给的理由**：

| 序 | 判据 | 失败枚举 | 玩家看到 |
| ---: | --- | --- | --- |
| 1 | `!context.Night`（`storeState == 0` 才算夜里） | `Daytime` | `error_daytime` |
| 2 | `RemainingOutings == 0` | `NoOutings` | `error_no_outings` |
| 3 | `ThiefLevel(run.Experience) < RequiredLevel` | `Level` | `error_level` |
| 4 | `AvailableDay > Day` | `Cooldown` | `error_cooldown` |
| 5 | 上一趟声望没结清（`ReputationPending`） | `UnsettledReputation` | `error_unsettled_reputation` |
| 6 | `!LootReady`（产出源模组没装/没读好/原版箱不齐） | `MissingLoot` | `error_missing_loot` |
| 7 | `Cash < Fee` | `Funds` | `error_funds` |
| 8 | 果园当天已打过卡 | `DailyWork` | `error_daily_work` |

"需解锁外出、打烊且有次数；重伤、广场恐惧或严打不能进入"这四条**不在 `Quote` 里**，而在它的上游 `OutingBlockReason()`（`ModEntry.cs:4428-4460`，全文见 §4.3）：`HealthData.IsSeriouslyWounded()` / `StartingPerkContent.IsAgoraphobiaResting()` / `StoreStation.Instance.storeEventManager.IsEventActive("crackdown")` / `!PlayerStore.Instance.isAfterhoursIntroduced` / `storeState != 0` / `visitLeftTonight <= 0`。**分层很清楚：能不能出门 = 原版规则；能不能进这一级 = 本模组规则。**

### 3.4 入场/退场挂在哪（本档案最关键的一问）

**结论：挂 `ModHelper.AddAfterhourLocation`（官方 11 参注册面）+ 自建全屏舞台；出行与返店次数 100% 交给原版，一行都没自己算。**

`RegisterLocation()`（`ModEntry.cs:5775-5788`，§4.2 全文）把 6 个地点注册成 `DelegateAfterhourLocation`，参数顺序对齐原版签名 `AddAfterhourLocation(modId, id, title, onArrive, onLeave, buildPanel, imageKey, backgroundKey, ambienceId, isDiscovered, isAvailable)`（`共享开发/reference/Assembly-CSharp/ModHelper.cs:577`）：

| 槽位 | 本模组填什么 | 作用 |
| --- | --- | --- |
| `imageKey` / `backgroundKey` | `"upperdistrict:" + scene`（`upper_scene`…`tobacco_scene`） | **必须带 `modId:` 前缀**，否则 `CustomUIManager.GetSprite` 不查模组贴图 |
| `isDiscovered` | `() => false` | 恒 false：不在原版"已发现"列表里露出，只走自建站台图 |
| `isAvailable` | `loaded && ExpeditionRules.Quote(...).Allowed` | **把 §3.3 的纯函数直接当门禁**，原版 `MapUIManager.Travel` 里会先判它 |
| `onArrive` / `onLeave` | `Arrive(id)` / `LeaveDistrict(id)` | 收钱、加调查、开警戒、开搜索窗 / 结声望、关窗 |
| `buildPanel` | `BuildPanel(id, panel)` | 原版 340×340 小面板兜底（`ModEntry.cs:5833-5842`） |

**没有 Unity 场景，也没有 `AfterhourLocation` 子类。**"场景"是 `PrepareStages()`（`ModEntry.cs:6158-6202`）用模组贴图现场 `Sprite.Create` 出来的一个 `SpriteRenderer`（`sortingOrder = 20`，16:9 居中裁切），塞进 `location.stageObject`；搜索界面是 `CreateSearchView()`（`ModEntry.cs:5002-5042`）建的全屏 `CustomUIWindow`，塞进 `location.panelObject`。两个字段都是 `DelegateAfterhourLocation` 的公开可写字段——**它靠"委托 + 手塞两个字段"绕过了继承**。

完整链路（每步都有出处）：

1. **墙面入口**：`CreateWallTerminal()`（`ModEntry.cs:6115-6156`）把一张 `wall_terminal` 贴图做成 `Image + Button`，`SetParent` 到**原版 UI 画布** `RenderHandler.current.windowUICanvasObject`（`NativeTerminalClick.cs:11-21`），`SetAsFirstSibling()` + 按画布尺寸算锚点（`NativeTerminalClick.cs:87-97`，宽 = `min(canvasW*0.074, canvasH*0.09*aspect)`、位置 `(w*0.748, h*0.95-h)`）。**这是"往原版墙面上贴一个可点贴图"的零场景做法**，比 `Instantiate` 原版物件便宜。
2. **仪表盘**：点击 → `ShowDashboard()`（`:5994-6005`）→ `BuildClassicDashboard(level, blockReason)`：显示盗贼等级/经验、`Deposit` 按钮、`Depart` 按钮。
3. **出发**：`Depart()`（`:6007-6049`）**不自己开地图**，而是 `ResponseList.CreateAfterhoursResponse()` 取原版"出门"响应，再**给每个选项套一层行动前校验**：`NavigationIsCurrent(selectionContext) && !OutingBlockReason()` 通过才调原版 `nativeChoice()`；随后 `StartCoroutine(WaitForTransit(...))` 等过场结束。
4. **过场/地图**：`WaitForTransit`（`:4716-4746`）最多等 5 秒，若 `TransitUI.IsOpen` 就切到**自建站台图** `ShowStationChart()`（`:4748-4791`，`station_map` 大图 + 6 个 `ChartButton` + 彩票店/搬家公司的 `CommercePin`）；点"×"回原版地图（`ShowNativeTransit`，`:4793-4814`，直接 `TransitUI.Open(instance.transitPrefab)`）。
5. **进入地点**：`ChartButton → ShowAdmission(id)`（`:4848-4857`）先给一张入场单（费用/等级/调查/冷却四行文案，`:552`），确认后 `BeginLocationTravel(locations[id])` → **`MapUIManager.Travel(location)`**（`:4920-4956`）→ 原版黑屏过场 → 原版的 `location.Arrive()` → 我们的 `Arrive(id)`。
6. **退场**：搜索界面"返回"按钮 → `ReturnFromOwnedLocation(locations[id])` → **`ModHelper.ReturnFromAfterhour(true)`**（`:4993-5000`）；原版 `MapUIManager.ReturnNow` 会调 `location.Leave()` → `LeaveDistrict(id)`（`:4299-4314`）结清声望。
7. **"返店扣一次外出"**：**本模组没有一行代码扣 `visitLeftTonight`**。真正扣次数的是原版 `EmporiumEntry.OnArriveStore(fromMap: false)`（`共享开发/reference/Assembly-CSharp/EmporiumEntry.cs:905-908`），由 `MapUIManager.OnReturning()` 调用（`MapUIManager.cs:281-285`）。而**取消"已准备的出发"走 `CloseUI()` → `OnArriveStore(fromMap: true)`（`MapUIManager.cs:109-121`），这条路径故意不扣次数**——`NativeMapCancellation` 的降级分支正是靠 `fromMap: true` 做到"取消不收费"（`NativeMapCancellation.cs:66-72`）。这是它能零成本复用整套作息/次数系统的根本原因。

配套的两个"原版没给 API"的补丁：

- **取消已准备的出发**：`NativeMapCancellation.Cancel()`（`NativeMapCancellation.cs:37-57`）反射 `MapUIManager.CloseUI(bool)`；拿不到方法就降级：`TransitUI.Close()` + 关 `mapPanel` + `EmporiumEntry.OnArriveStore(true)` + 反射 `RestoreStoreUIAfterReturn()`（`:59-73`）。
- **"ESC 暂停而不是关窗"**：所有自建窗口一律 `SetCloseOnEscape(false)`（`ModEntry.cs:5119`、`:5835`），每个入口先判 `Interactable.isPaused` 才动作（`:5887`、`:6009`、`:4757`）——ESC 交回原版暂停语义，窗口不自己吞。

### 3.5 产出池：四层概率怎么才不乱

核心是**"整数票 + 累积 ends + 二分"**，一层都不含糊：

- `LootProbabilityEntry(itemId, tickets)`：票数 `<= 0` 直接抛（`Core\LootProbabilityEntry.cs:11-19`）。
- `LootProbabilityPool`：构造时校验**条目非空、无重复 id、票数总和必须恰好 1,000,000**（`Core\LootProbabilityPool.cs:15-33`），随后把票数累加成 `ends[]`；`Pick(roll)` 在 `ends` 上二分（`:35-56`）。**"必须凑满 100 万"这条断言是整张表不出漂移的护栏**——改表时少写一个数就启动即抛，不会静默改变概率。
- 目录 `LootProbabilityCatalog` 用字典装 20+ 张池（`Core\LootProbabilityCatalog.cs:11-33`），键名分三档：`"upper"`/`"security"`/`"wine"`/`"vault"`（地点池）、`"vault.Medical"`…（**箱内再抽的子池**）、`"factory.chemical.production"`…（工厂池）。

多层概率的三处实例，全部"可数、可对账"：

| 池 | 结构 | 数出来的结果 |
| --- | --- | --- |
| `upper`（上层区） | 单层 30 条目，票数按价值阶梯递减：`bandage 86331` → … → `system_uncapped_neural_core 1273` | **30 种，最贵 0.127%**（`LootProbabilityCatalog.cs:13`） |
| `security`（治安部） | 23 条普通货 + **5 条整箱**各 `10000` 票 | 5 条箱 = **5%**，五类各 **1%**；抽到箱后再走 `CreateSecurityCrate()` 拿原版预置箱（`ModEntry.cs:4488-4510`） |
| `vault`（金库） | 53 条专属散件（合计 550,000 票）+ 4 条 `xiaowo_auction_container / X` 各 `25000` | 散件 **90%**、四类高级箱各 **2.5%**；箱内再抽 `vault.Medical` 等 11–12 条子池（`:19-23`） |

抽签入口只有两个函数：`LootProbabilityPool.Pick(Random.Range(0, 1_000_000))`；`RollDistrictItem`（`ModEntry.cs:4535-4557`）负责"治安部命中整箱 → 换成原版箱 → 把箱内每一件都打上 `StolenHelper.InitStolenItem(item, 100)`"（`MarkSecurityContents`，`:4559-4569`）。**整箱也要算赃物，否则玩家把箱子当白货卖——这是它想清楚了的细节。**

`LootReady` 是这套表的**准入联锁**：`DistrictLootReady` 要求该池**每一条** id 都 `DirectoryMaster.Has<GameItem>`，治安部还要 5 个原版预置箱都在 `PreBuiltItemHelper.preBuiltItems` 里（`ModEntry.cs:4522-4532`、`:4483-4486`）。缺一样就 `MissingLoot` 拒进——**宁可不让进，也不给空箱**。

### 3.6 警戒（alert）

- **上限**：进场时若 `AlertDay != Day` 就重置 `Alert = 0`，并 `AlertLimit = (id == Wine) ? 40 : Random.Range(30, 61)`（`ModEntry.cs:5811-5817`）。**酒窖固定 40，其余每天 30–60 随机**，一天只摇一次。
- **有效上限** = 基础 + 手套 `+15` + 盗贼等级 ≥3 `+10`（+ 金库维护事件 `+15`）：`EffectiveAlertLimit`（`:5171-5174`）→ `ExpeditionRules.AlertLimitBonus`（`Core\ExpeditionRules.cs:100-112`）+ `DistrictEventRules.AlertLimitBonus`（`Core\DistrictEventRules.cs:40-47`）。
- **怎么涨**：每次搜索 `Random.Range(2, 6)`，再叠加 `AlertDelta`（撬棍 `+1`、手套 `-1`、等级 4 `-1`，地板 0；`Core\ExpeditionRules.cs:114-126`），再叠事件（上层宴会 `-1`、酒窖盘点 `+2`；`Core\DistrictEventRules.cs:49-64`），最后 `Alert = min(上限, Alert + 增量)`（`ModEntry.cs:5973-5974`）。
- **涨满怎么样**：**只是不能再搜**。`Search` 开头 `if (Alert >= 上限) return`（`:5928`），按钮 `interactable = admitted && Alert < 上限`（`:5075`）。**不强制离场、不额外罚款**；真正的惩罚在离场结声望时按 `Alert/上限` 比例放大（§3.7）。

### 3.7 声望"额度"到底是什么

它不是声望门槛，是**一趟行程的声望扣款额度**，分两段扣（`Core\TheftReputationRules.cs:16-37`）：

```
入场扣  EntryCost(budget)    = ceil(budget / 5)                              // 恰好 20%
总扣    TotalCost(budget,a,L) = ceil(budget * (L + 4*min(a,L)) / (5*L))       // a=0 → 20%，a=L → 100%
离场扣  DepartureCost         = TotalCost - EntryCost                          // 剩余 80% 按警戒占比
```

所以"上层区 5、治安部 10、酒窖 20、金库 100→280"= **这一趟最多掉多少声望**。实现上有四个值得抄的动作：

1. **`Begin()` 先落账再扣钱**：进场就把 `ReputationPending/ReputationBudget/ReputationAlertLimit` 写进 `DistrictState`（`TheftReputationRules.cs:39-51`），`ReputationExitCost = -1` 表示"离场金额还没冻结"。
2. **离场金额冻结**：`FreezeDeparture()` 只在第一次算，之后写死（`:65-76`）——**防止玩家在"已经决定离场"之后换装改警戒上限来砍价**。
3. **存档前快照上限**：`SnapshotTheftAlertLimit()`（`ModEntry.cs:5224-5230`）在 `PlayerStore.SaveGame` 前缀里把当前 `EffectiveAlertLimit` 写进状态，原因同上（手套/等级是活的）。
4. **结不清就拒绝再进**：`ReputationPending` 为真时 `Quote` 返回 `UnsettledReputation`（`Core\ExpeditionRules.cs:48-51`），读档校验 `TheftReputationRules.Validate` 一旦不一致就抛 `Invalid theft reputation ledger; retain original data`（`:85-110`）——**宁可报错也不擅自"修好"玩家的账**。

实际写盘走 `StoreReputation.ModReputationRaw(text, -cost, false)`（`ModEntry.cs:5200`），写失败时比对"数学期望值 vs 实际值"，只在**两者一致**时吞掉（`:5198-5208`）——这是全库少见的"带对账的 catch"。

### 3.8 经验与等级成长

- **"投入 500 元加 5 经验"对得上，但不是唯一来源**：`Deposit()` 里 `HasCredit(500)` → `AddCredit(-500)` → `Run.Experience += 5`（`ModEntry.cs:6051-6060`）。**可无限重复点**，经验无衰减、无上限（只有 `Run.Experience <= 2147483642` 的 int 溢出保护）。
- `Core\EconomyRules.cs:25-32` 的 `ExperienceFromDeposit(long cash)` **是死代码**：全库仅定义处出现，`Deposit()` 是内联写的。想按金额阶梯给经验的人别被这个函数骗了。
- **进场也给经验**（按地点：5/10/5/15），且**每个地点每天一次**：`RecordSuccessfulEntry` 用 `LastExperienceDay == day` 判重（`Core\ExpeditionRules.cs:59-79`）。所以"刷经验"两条腿：点存款（无限）＋ 跑图（每日限次）。
- 等级阈值 `20/80/140/260` → 0–4 级；开局特性「职业小偷」直接把经验托到 140（`ModEntry.cs:5647`，即第 3 级）。

### 3.9 Patch 靶点表（28 处，按子系统归并）

| 原版靶点 | 处数 | 干了什么 |
| --- | ---: | --- |
| `GameItem.GetBaseValue` / `GetNegociatedValue` / `ValidateShapeState` / `EnableTag` / `MayHaveValidInventorySlot` | 9 | 果园溢价水果定价、搬家公司报价、形状守卫、**拍卖行箱内货清"热度"**、封装机优先塞格 |
| `GameGridInventory.*` / `SlotMarker.*` / `GraphUtils.*` | 7 | 封装/投放的"先占位后校验"守卫（`Placement*Guard.cs` 8 个壳） |
| `PlayerStore.GetNextClient` / `SaveGame` / `OnItemBought`（搬家） | 4 | 委托队列插单、**存档前刷本模组快照**、搬家购买 |
| `LockHelper` / `StolenHelper` / `GameSlotInventory` / `GameItemElement` | 4 | 箱内货热度初始化/传播/清除、高清命中区 |
| `StoreClient`（搬家验收 ×2） | 2 | 拒收模式 |
| `StartingPerkIconLoader.Start` | 1 | 注册"职业小偷"图标 |
| `SlotMarker`/`GameItemElement.Destroy` 等其余守卫 | 1 | 销毁守卫 |

---

## 4. 关键代码摘录

### 4.1 四级地点 = 6 行数据表（`UpperDistrict.Native.Core\DistrictRules.cs:7-15`）

```csharp
private static readonly DistrictDefinition[] Definitions = new DistrictDefinition[6]
{
    new DistrictDefinition(DistrictId.Upper,    "upper",    0,  300,    0, 0, Faction.Upper,       5, 2, 5),
    new DistrictDefinition(DistrictId.Security, "security", 1,  700,    0, 0, Faction.Security,   10, 5, 10),
    new DistrictDefinition(DistrictId.Wine,     "wine",     2, 1500,  100, 2, Faction.Outer,      20, 2, 5),
    new DistrictDefinition(DistrictId.Vault,    "vault",    3, 8000, 2000, 7, Faction.Security,  100, 2, 15),
    new DistrictDefinition(DistrictId.Orchard,  "orchard",  0,    0,    0, 0, Faction.None,        0, 0, 5),
    new DistrictDefinition(DistrictId.Tobacco,  "tobacco",  2, 2000,    0, 4, Faction.BlackMarket, 15, 2, 5)
};
```

### 4.2 注册 + 到达（`UpperDistrict.Native\ModEntry.cs:5775-5788`、`5790-5829`）

```csharp
private void RegisterLocation(DistrictId id, string scene)
{
    DistrictDefinition definition = DistrictRules.Get(id);
    locations[id] = ModHelper.AddAfterhourLocation("upperdistrict", "upperdistrict." + definition.Key,
        () => (!loaded) ? Text("district_" + definition.Key)
                        : Text("location_card", Text("district_" + definition.Key),
                               definition.AdmissionFee(Run.For(id).Visits), definition.RequiredLevel),
        () => { Arrive(id); },              // onArrive
        () => { LeaveDistrict(id); },       // onLeave
        panel => { BuildPanel(id, panel); },// buildPanel（原版小面板）
        "upperdistrict:" + scene, "upperdistrict:" + scene, null,
        () => false,                        // isDiscovered：恒 false，只走自建站台图
        () => loaded && ExpeditionRules.Quote(Run, id, Context(id)).Allowed);   // isAvailable = 纯函数
}

private void Arrive(DistrictId id)
{
    admitted = false;
    active = id;
    AdmissionQuote admissionQuote = ExpeditionRules.Quote(Run, id, Context(id));
    if (!admissionQuote.Allowed) { CreateSearchView(id); return; }   // 拒进也要给界面，只显示"已拒绝"
    ModHelper.AddCredit(-admissionQuote.Fee, false);
    if (admissionQuote.Investigation > 0)                            // 入场即加原版调查度
        PlayerStore.Instance.secData.evidenceLevel =
            Math.Min(100, PlayerStore.Instance.secData.evidenceLevel + admissionQuote.Investigation);
    ExpeditionRules.RecordSuccessfulEntry(Run, id, Day);             // 计次 + 冷却 + 当天首次经验
    DistrictState districtState = Run.For(id);
    if (districtState.AlertDay != Day)                               // 每天只重置/摇一次警戒上限
    {
        districtState.AlertDay = Day;
        districtState.Alert = 0;
        districtState.AlertLimit = ((id == DistrictId.Wine) ? 40 : Random.Range(30, 61));
    }
    if (TheftReputationRules.Applies(id))
    {
        TheftReputationRules.Begin(Run, id, admissionQuote.ReputationCost, EffectiveAlertLimit(id));
        ApplyTheftReputation(id, TheftReputationRules.EntryCost(admissionQuote.ReputationCost), "entry");
    }
    admitted = true;
    saves.Save();
    CreateSearchView(id);
}
```

### 4.3 夜间外出的六条否决（`UpperDistrict.Native\ModEntry.cs:4428-4460`）

```csharp
private string OutingBlockReason()
{
    if (!loaded || !PlayerStore.HasInstance || (object)EmporiumEntry.Instance == null
        || (object)MapUIManager.Instance == null || StoreStation.Instance == null
        || StoreStation.Instance.storeEventManager == null)
        return "error_outing_unready";
    if (HealthData.IsSeriouslyWounded())                                  return "error_outing_wounded";
    if (StartingPerkContent.IsAgoraphobiaResting())                       return "error_outing_rest";
    if (StoreStation.Instance.storeEventManager.IsEventActive("crackdown")) return "error_outing_crackdown";
    if (!PlayerStore.Instance.isAfterhoursIntroduced)                     return "error_outing_locked";
    if ((int)PlayerStore.Instance.storeState != 0)                        return "error_daytime";
    if (PlayerStore.Instance.visitLeftTonight <= 0)                       return "error_no_outings";
    return null;
}
```

### 4.4 一次搜索的完整事务（`UpperDistrict.Native\ModEntry.cs:5923-5935`、`5968-5979`）

```csharp
DistrictState districtState = Run.For(id);
bool carriedGloves  = Carried("upperdistrict.gloves");
bool carriedCrowbar = Carried("upperdistrict.crowbar");
int thiefLevel = DistrictRules.ThiefLevel(Run.Experience);
int limit = EffectiveAlertLimit(id);
if (districtState.Alert >= limit) return;                  // 警戒满：静默不动，按钮本来就灰
int rolls = 1 + ExpeditionRules.SearchItemBonus(id, carriedCrowbar);   // 撬棍多摸一件
List<GameItem> list = new List<GameItem>();
try
{
    for (int i = 0; i < rolls; i++)
    {
        GameItem val = id switch
        {
            DistrictId.Vault   => vault.Roll(m => log.Log(m)),
            DistrictId.Wine    => brewing.RollWine(),
            DistrictId.Tobacco => tobacco.Roll(m => log.Log(m)),
            _                  => RollDistrictItem(id)     // upper / security 走整数票池
        };
        if (val == null || val.identifier == "unknown") { val?.Destroy(); continue; }
        list.Add(val);
        ModHelper.SetItemOwnerShip(val, true);
        StolenHelper.InitStolenItem(val, 100);              // 百分百赃物
        if (id == DistrictId.Security) MarkSecurityContents(val);
        ModHelper.MoveItemToAfterhours(val);
    }
    if (!list.Any(item => item.parentInventory != null)) { SetSearchNotice(Text("ground_full")); return; }
    int delta = DistrictEventRules.AlertIncrease(id,
        ExpeditionRules.AlertDelta(id, Random.Range(2, 6), carriedGloves, carriedCrowbar, thiefLevel),
        ActiveDistrictEvents());
    districtState.Alert = Math.Min(limit, districtState.Alert + delta);
    TheftReputationRules.RememberLimit(districtState, limit);
    saves.Save();
}
finally
{
    foreach (GameItem item in list) if (item.parentInventory == null) item.Destroy();   // 没塞进包的一律销毁
}
```

**这段是全档案最该抄的 30 行**：`try/finally` 把"生成成功、投放失败"的物品全部销毁；投放成功与否**用 `parentInventory` 引用比对**而不是返回值；警戒在任何一环失败时都不涨。

---

## 5. 踩坑与红线

1. **别信透视报告的补丁计数**。`inspect_mod.py` 正则漏掉 `[HarmonyPatch(typeof(X),"M",new Type[]{...})]` 形式，本包实际 28 个补丁壳只报了 10 个。**审计时用 `grep -rn "HarmonyPatch(typeof" ` 复核。**
2. **`isDiscovered => false` 是对的，但代价是"原版地图永远看不到你"**。它必须自建站台图（`station_map` + 6 个归一化坐标按钮）才能让玩家点到地点；坐标是硬编码比例（`ChartButton(..., 0.24f, 0.74f)`），**图一换就要重排**。
3. **`AddAfterhourLocation` 的 `imageKey` 必须写 `"<modId>:<key>"`**，且 Sprite 要在 `OnModAssetsLoaded` 里手工 `Sprite.Create` 并塞进 `mod.PaperSprites`（本包 `PrepareStages`，`ModEntry.cs:6176-6199`）；只放 `Textures/` 不会自动进 `PaperSprites`。
4. **取消"已准备的出发"没有公开 API**。作者用反射 `MapUIManager.CloseUI(bool)`，拿不到就手工降级重放 **`OnArriveStore(true)`**——注意必须是 `true`：`fromMap: false` 会扣掉一次外出（`EmporiumEntry.cs:905-908`），"取消"就变成了"白跑一趟"。**这一段反射是 `PublicInterop`（`ModReflect`）包的，不是裸 `System.Reflection`**，所以仍是零能力；但 `FindMethod` 失败必须走降级分支，否则玩家会被卡在黑屏过场里。
5. **声望必须"先落账、再扣钱、能冻结、能校验"**。漏掉 `FreezeDeparture` 就能被"离场前换手套"砍价；漏掉 `SnapshotTheftAlertLimit` 就能被"存档再读档"改基数；漏掉 `Validate` 就会把坏账静默修成好账。三者缺一都是经济漏洞。
6. **`EconomyRules.ExperienceFromDeposit` 是死代码**，真正加经验的是 `Deposit()` 的内联 `+= 5`。照它改"按金额给经验"不会生效。
7. **`ReputationPending` 没有超时**。玩家在酒窖里拔电源/回滚存档，下次进来会一直撞 `UnsettledReputation`，只能靠再次进场→离场结清。这是**设计选择**（防逃避扣款），但对"卡死"类 bug 报告要能解释。
8. **`Carried()` 只认 `EmporiumEntry.Instance` 的三个小时口袋槽**（`afterhourPocketSlotInvBackpack/Left/Right`，`ModEntry.cs:5844-5868`），还要求 `ModHelper.IsOutsideStore()`；在店里判断"带没带手套"永远返回 false——**装备加成只在场内生效**。
9. **硬依赖三家模组的名号写死在代码里**：`xiaowo_night_auction`（金库）、`brewing_expansion`（酒窖/果园）、`tobacco_expansion`（烟店）。缺任一 → 对应池 `LootReady = false` → 该地点永久 `MissingLoot` 拒进；失败文案分 `missing / disabled / load` 三种（`VaultLootSource.cs:66-80`），**不是一句"加载失败"糊过去**。
10. **单文件 6,217 行**。它把 28 个补丁拆成壳文件是对的，但入口类仍然吃掉一半行数；照抄结构时至少把"注册/Dashboard/站台图/搜索窗/工厂/彩票/搬家"按功能切文件。

---

## 6. 对我们的可用点

### 6.1 →《深空电玩》（`deepspace_arcade`）

1. **概率表就用 `LootProbabilityPool` 那套整数票**。电玩现在有"货源资格与发行门槛""盗版查获""概率维修 +5% 试次"等多处概率口径，散在各处。把 `LevelListLoot`/`LootTable`（原版 `RNGNeeds.ProbabilityList<string>`）换成"1,000,000 票 + 累积 ends + 二分"，并**在构造时断言总和恰为百万、id 不重复**（`LootProbabilityPool.cs:17-32`），改表时错一位就启动报错，比事后查"某个盘怎么突然不出了"便宜得多。
2. **`OutingBlockReason` 直接搬去做电玩的夜间摊位门禁**。电玩的"付费情报/周选/PGA 拍卖"目前没有统一的"今晚能不能开张"判定；这 6 条（重伤/广场恐惧/严打/未解锁外出/白天/次数用尽）就是原版语义的标准答案，且它是**返回文案键**而不是 bool，UI 直接显示不用二次翻译。
3. **影子账本 + 回读校验**搬去给 `ArcMarket` 的 ES3 写盘剥离收口：`NativeRunStore.Save()` 的写法是"序列化 → `SetModData` → **立刻 `GetModData` 读回比对，不等就抛**"，`Commit` 失败把旧串写回（`NativeRunStore.cs:90-127`），再挂 `PlayerStore.SaveGame` 前缀刷一次（`NativeRunBeforeSlotSave.cs:5-11`）。电玩现在靠"读档 `ResyncStock` 补打标签"，加上这层能提前一步发现"写盘被吞"。
4. **把规则切成 0 Unity 依赖的 Core**。本包 42 个 Core 文件没有一行 `using UnityEngine`，所以 `Quote/EntryCost/TotalCost/Pick` 全部能离线跑。电玩的定价、排期、维修概率同样可以搬进 `tests/integrity` 直接断言，而不是靠真机截图倒推。

### 6.2 →《深空当铺：可能是群友》（`freya_cornell` / `DeepSpacePawn.dll`）

5. **声望三段式扣费正好是"伞萝萝/塞壬谈判翻脸"的量化模型**：入场先扣 20%，剩下的 80% 按"这一趟闹得多大"（对应我们的警戒概念）在离场时结算，中途金额冻结。三个纯函数 `EntryCost/TotalCost/DepartureCost`（`TheftReputationRules.cs:16-37`）可以原样抄，配套 `ReputationPending` + 读档 `Validate` 挡住"拔电源逃罚"。
6. **警戒条（Alert/AlertLimit + 每日重置 + 装备/等级/事件三层加成）可以直接做我们的"查获风险"**：`EffectiveAlertLimit` 的写法（基础值存状态、加成都现算、**存档前快照**）比"每次现算不落盘"稳，尤其我们自己已经有 `DspState` 这类 modData 账本（多角色共用一份，见 `深空当铺：可能是群友/PROGRESS.md` §1）。

### 6.3 反向：这两条**不要**抄

- **别用 `isDiscovered => false` + 自建站台图**，除非你确定不需要原版站台卡片。我们两个模组都已有成熟的 `PaperUI` 入口，用原版地图（`isDiscovered` 留空 + `isAvailable` 给门禁）更省。
- **别把 6,217 行单文件当模板**。可抄的是"规则进 Core、补丁进壳文件"，不是它的入口类体积。
