# 小窝后厨经营 / XIAOWO-KitchenBusiness｜工坊 3830523891

> 反编译源码 `缓存文件夹\decomp_ws\3830523891\`（**180 个 .cs / 25,209 行**；主体 178 个在 `XIAOWOKitchenBusiness\`，另加 `Properties\AssemblyInfo.cs` 与 `System.Runtime.CompilerServices\IsExternalInit.cs`）。
> 包内 manifest 实测：`<ID>xiaowo_kitchen_business</ID>`、`<ModVersion>2.1.6</ModVersion>`、`<GameVersions><Version>0.46D</Version>`、**`<Capabilities />` 自闭零能力**，工坊包内只有 `XIAOWOKitchenBusiness.dll` + `manifest.xml` + `preview.png`。
> **为什么值得读**：这是工坊里唯一一份把"加吃喝"做成**完整经营循环**的零能力样本 —— 渐进解锁、第二套好感、批量分帧、过夜结算、可拖拽终端 UI 全压在一个 DLL 里，而且每一条都能单独拆出来复用。

## 0. 两条路先选：3 分钟选型对照

| 维度 | 本模组（后厨经营） | 兄弟档案 [3830798369-VanillaFoodExpansion.md](3830798369-VanillaFoodExpansion.md) |
| --- | --- | --- |
| 规模 | 25,209 行 / 180 文件 / 51 物品 / 54 配方 / 6 个自定义顾客 | 9,201 行 / 38 文件 / 111 物品 / 2 个 NPC 商人 |
| 做什么 | **新增一套玩法**：天数门→设备→备餐→过夜结算→订货→行情→餐饮订单 | **扩原版内容**：往原版食物池里灌物品，再挂两个卖货 NPC |
| 改动面 | 26 个 Harmony 补丁 + 6 个 ModHook，碰 `GetValue`/`SaveGame`/`EscapeUIManager`/`StoreClientManager` | 26 处手动 `Harmony.Patch`，主要碰 `StoreClientManager`/`Dialogue`/`GameItem.name` |
| 存档 | 自己一段 JSON 塞 `PlayerStore.ModData()["XIAOWO.KitchenBusiness.v1"]`，坏档就**拒绝存档** | 每个 NPC 一条竖线分隔字符串塞 `store.modData` |

一句话判据：**能靠"多几件物品 + 一两个 NPC"解决的，别写后厨；需要"每天一个决定"的，别指望加物品解决。**

## 1. 规模与结构

| 文件 | 行 | 角色 |
| --- | ---: | --- |
| `KitchenTerminal.cs` | 3,782 | 平板终端：6 页 + 缩略态 + 拖拽 + 原生遮挡排序（见 §2.5） |
| `KitchenFactory.cs` | 1,482 | 物品注册 / 容器与机器面板 / 雪碧图 |
| `KitchenCookbookUi.cs` | 1,115 | 多页食谱书（借原版 `MultiPageUIManager`） |
| `KitchenSprites.cs` | 1,041 | 87 个美术资源解码（全部 `EmbeddedResource`） |
| `Cooking.cs` | 1,020 | 单份/批量共用的烹饪结算（`CraftBatchItem` 同一条路径） |
| `KitchenRelations.cs` | 977 | 四线好感 + HUD |
| `KitchenVisitors.cs` | 932 | 6 个自定义 `StoreClient` 工厂 |
| `Beverages.cs` | 828 | 饮品机，与烹饪同构 |
| `KitchenProgression.cs` | 803 | **小炉 / 门控 / 过夜结算**（本文主角） |
| 其余 171 个 | ≤ 558 | 单职责小文件（`KitchenXXXRules.cs` 纯函数 + `KitchenXXXPatch.cs` 补丁） |

数据规模：`KitchenCatalog.cs` 51 条 `KitchenItemSpec` 定义；配方 16 家常 + 18 鱼 + 6 蔬 + 14 饮品。全部美术走 `XIAOWOKitchenBusiness.csproj:103-189` 的 87 条 `EmbeddedResource` —— **这就是 `<Capabilities/>` 能空着的原因：不读外部文件就不需要 `files` 能力**。

## 2. 它怎么做这件事

### 2.1 「从免费小炉起步」：天数门 + 条件门 + 付费提前，三合一全是纯函数

判定点不是事件、也不是 `GameManager.Day`，而是**每次 getter 现算**，唯一数据源是原版静态计数器 `StoreStation.GetDayCounter()`：

```csharp
// KitchenProgression.cs:54
internal static int Day => KitchenEconomyRules.UnlockDay(State.FirstDay, StoreStation.GetDayCounter(), State.Legacy);
```

`StoreStation.GetDayCounter()` 是**存档天数**（不是现实时钟）；`State.FirstDay` 是首次接触后厨那天，存进 `KitchenGrowth.FirstDay`（`KitchenGrowth.cs:11`，默认 `-1`）。于是"第 N 天"= `max(1, max(今天, 今天-FirstDay+1))`（`KitchenEconomyRules.cs:21-24`）——**既认日历天数也认营业天数，取大值**，玩家中途才装模组也不会被瞬间解锁一切。

| 阶段 | 天数门 | 条件门 | 判定处 |
| --- | --- | --- | --- |
| 库存与账本 | `day >= 3` | 无 | `KitchenEconomyRules.cs:59-68`（page 4） |
| 设备（单灶/饮品机/双灶） | `day >= 10 / 12 / 15` | 好感 LV1/2/2 | `KitchenRelations.cs:390-395` |
| 批量制作 | `(day >= 10 \| PaidBatch) & professional` | 专业设备 | 同上（page 0） |
| 食材订货 | `(day >= 12 \| PaidOrders) & professional` | 专业设备 | 同上（page 1） |
| 食材行情 | `(day >= 15) & professional` | 专业设备、**不可购买提前** | 同上（page 2） |
| 餐饮订单 | `day >= 18 && Sold >= 30 && professional` | 累计售出 30 份 | `KitchenProgression.cs:94-108` |
| 珍贵原料 | `day >= 18` + 好感 LV3 | — | `KitchenRelations.cs:396-411` |

三个关键设计：
1. **`professional` 把"简陋小炉"挡在门外**：`PlayerOwnsItem("stove_single") || PlayerOwnsItem("stove_double") || PlayerOwnsItem("beverage_machine")`，小炉 (`starter_stove`) 故意不计入（`KitchenProgression.cs:70-80`）——"免费起步"和"专业线"是两套设备，天数是硬闸，设备是软闸。
2. **`Legacy` 老存档直通**：导入旧档时 `State.Legacy = true` → `UnlockDay` 直接返回 ≥18，全部天数门一次放行（`KitchenProgression.cs:215-219`）。给老玩家补票，而不是让他们重玩。
3. **付费提前只开两档**：`EarlyUnlockPrice(page)` 只有 `page 0 → 120`、`page 1 → 180`，行情档 `page 2` 返回 0 → `CanPurchaseUnlock` 直接 false（`KitchenEconomyRules.cs:35-52`）。**"最贵的那档不给买"是刻意的**：行情是信息优势，买了等于跳过整条经营曲线。

### 2.2 关系成长：完全独立的"第二套声望"，存自己的键

四条线 `supplier / chef / drinks / plates`（`KitchenRelations.cs:19-29` 的 `Contacts` 常量表），**与原版 faction reputation 零交集**：

- 数据结构：`KitchenBusinessProgress.Relations` → `KitchenRelationsProgress.Contacts: Dictionary<string, KitchenContact>`；`KitchenContact` 只有 5 个字段 `Points / Day / Earned / SpendRemainder / LastSource`（`KitchenContact.cs:5-13`）。
- 落盘：`KitchenProgress.Terminal.Business` → 整段 JSON 进 `PlayerStore.ModData()["XIAOWO.KitchenBusiness.v1"]`（`KitchenState.cs:134`），**不碰原版存档结构**。
- 每日上限 16 / 三级 24-72-144：`KitchenRelationsRules.cs:7-31`；`Award()` 是唯一入口，一个表达式同时夹"总量不超 144"和"今日不超 16"（`:38-41`）。
- 每日重置靠 `KitchenContact.Day` 与当前天比对，**天变了就把 `Earned` 清零**（`KitchenRelations.cs:266-271`）——没有定时器、没有跨日事件。
- 获得方式全部免费挂到既有行为上：采购 `每 20 元 = 1 点`（余数存 `SpendRemainder` 跨次累积，`:298-302`）、成品销售 `每份 2 点`、委托交付 `每份 3 点`（`:313-319`）、整摞餐盘每摞额外 +2（`KitchenRelationsRules.cs:33-36`）。
- 升级只弹 `StoreUIManager.Instance.Notify(...)`（`:280-287`），不改任何原版数值。

### 2.3 批量制作：单份共用结算 + 每帧 2 件 + 3 ms 秒表预算

`Cooking.CraftBatchItem` 与手动烹饪走**同一条** `StartCore`，区别只有一个 `immediate` 参数（`Cooking.cs:153-156, 199-239`）：`immediate:true` 时跳过 `CraftSession` 快照与原料预留（`KitchenImmediateInputs` 只校验水源还在原位），完成后立刻 `Complete(...)` 并集齐产物。**"批量"不是另一套产线，而是把同一份结算循环 N 次。**

调度在 `KitchenBulkCraft.Tick()`，三道闸：

```csharp
// KitchenBulkCraft.cs:186-216（已删 //IL_xxxx 噪声）
internal static void Tick()
{
    Job job = _job;
    if (job == null || Time.frameCount < job.Frame) return;   // ① 每份至少隔一帧
    if (!KitchenTerminal.Ready) { Reset(); return; }
    long timestamp = Stopwatch.GetTimestamp();
    try
    {
        SuppressPresentation = true;                          // ② 抑制 UI 逐件刷新
        for (int i = 0; i < 2; i++)                           // ③ 一帧最多 2 件
        {
            if (_job != job) break;
            using (KitchenStock.ReadScope()) TickItem(job);
            if ((double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / Stopwatch.Frequency >= 3.0) break;
        }
    }
    finally
    {
        SuppressPresentation = false;
        if (_job != job || Time.unscaledTime >= _displayAt)    // ④ 显示节流 0.25s
        { _displayAt = Time.unscaledTime + 0.25f; /* 只在最后刷新一次机器面板 */ }
    }
}
```

配套四个细节：`job.Frame = Time.frameCount + 1` 在**每次提交后**重设（`:173, 302`），所以一件一件排队；`Committed()` 由烹饪完成回调推回来数（`:165-176`）；`KitchenStock.ReadScope()` 让整批共享一次全店扫描（见 §2.5）；异常路径先 `KitchenEnergyCost.Refund` + `Cancel` 再 `Finish`，电量不白烧（`:331-347`）。

### 2.4 餐盘 / 待售区 / 翌日结算：挂 `ModHook.OnGoingSleepEarly`，不与夜间清洁抢对象

三处硬事实：

1. **挂点**：`Core.cs:40` → `ModHook.OnGoingSleepEarly += KitchenProgression.SettleNight;`（原生钩子，不是 Harmony）。注意是 **Early**，即玩家按下睡觉、原版夜间流程尚未执行时。
2. **幂等**：`if (... || State.SettledDay == StoreStation.GetDayCounter()) return;`（`KitchenProgression.cs:541`），同一天调多少次都只结一次，`SettledDay` 随存档。
3. **不改动原版夜间逻辑**：只遍历玩家自己的 `xiaowo_kitchen_starter_stove`，从它的 `Slot(stove, 1)`（待售区 `GameGridInventory`）里按 `XIAOWO_KITCHEN_NIGHT_READY` 标记取成品，需求上限由 `NightDemand(Sold, Pool, StarterDemand)` 定；卖出走 `CraftLifecycle.DetachForCleanup(item, "starter-overnight-sale", consumed: true)`（**原版销毁通道**）而不是 `Destroy`，钱走 `PlayerStore.Instance.ModCash(+n, true)`。夜间清洁服务扫的是店内地面/台面物品，本模组只动**机器自身子容器**里的东西，两者对象不重叠，因此不需要互斥（原版清洁的具体实现不在本模组源码里，此处为推测：靠"不碰对方对象"而非"抢锁"来避让）。

待售区容量：`Capacity => State.Pool ? 6 : 4`（`KitchenProgression.cs:58-68`），扩容条件是 `Sold >= 12 && cash >= 60`（`:390-402`）——**用"已售份数"当经验值**，又一处免费的行为统计。

### 2.5 平板 / 终端 UI：自绘 UI 的规范样本（拖拽 / 存位置 / 遵原生遮挡）

四层结构，全部在 `KitchenTerminal.cs`：
- `_root` + `Canvas(renderMode=ScreenSpaceOverlay, sortingOrder=1)` + `CanvasScaler(960×540, matchWidthOrHeight=1)` + `GraphicRaycaster`（`:710-720`）；
- `_pinnedRoot`：缩略平板（94×54），`_pinnedCanvas.renderMode = 0`、`overrideSorting = false`，**不抢原生排序**（`:721-724`）；
- 展开态 `Tablet`（800×462）自带 `StoreUI` 做缩放和 `UIDragHandler` 做拖拽：`ignoreClamp=false`、`id="xiaowo_kitchen_terminal"`、`moveFrontObjects` 空表（`:789-801`）——直接复用原版窗口拖拽手感；
- 每帧 `Anchor()` 决定缩略态挂在哪（详见 §3.5 摘录）。

位置持久化不写存档，写**模组配置项**：`ModHelper.GetConfig("xiaowo_kitchen_business").Define("compact_position", "", "Interface", ...)`，值为归一化 `x,y ∈ [0,1]`（`KitchenTerminal.cs:296-318, 462-477`）；解析失败/清空即回默认位置。拖拽结束才存一次（`KitchenCompactDrag.cs:62-66`），拖动中只移动不落盘。

另一个必须抄的细节：缩略态拖拽前先查 `!UIDragHandler.isDraggingAnyUI`，避免和原生窗口拖拽抢事件（`KitchenCompactDrag.cs:30`）；`OnDisable` 里 `FinishIfDragging()` 补存一次，避免拖到一半被关掉丢位置（`:68-79`）。

## 3. 关键代码摘录

**(1) 门控的单一真值源**（`KitchenEconomyRules.cs:54-71`）—— 所有页面/入口都问它，没人自己写 `if (day > x)`：

```csharp
internal static bool Unlocked(int page, int day, bool professional, bool legacy, bool batch = false, bool orders = false)
{
    bool flag = legacy;
    if (!flag)
    {
        flag = page switch
        {
            5 => true,
            4 => day >= 3,
            0 => ((day >= 10) | batch) & professional,
            1 => ((day >= 12) | orders) & professional,
            2 => (day >= 15) & professional,
            3 => true,
            _ => false,
        };
    }
    return flag;
}
```

**(2) 付费提前：先查三件事，再进不可重入段**（`KitchenProgression.cs:143-168`）：

```csharp
internal static bool PayUnlock(int page)
{
    if (!KitchenTerminal.CanPresent || _unlockBusy || !CanPayUnlock(page)) return false;
    _unlockBusy = true;
    try
    {
        int amount = KitchenEconomyRules.EarlyUnlockPrice(page);
        if (!KitchenUnlockPurchase.TryBuy(State, page, Day, Professional,
                PlayerStore.Instance.GetCash(), (int num) => PlayerStore.Instance.ModCash(-num, true)))
            return false;
        KitchenLedger.Purchase(amount);   // 顺手记账，进当日流水
        Status = KitchenLocalization.Select("提前开通成功，随存档保存；高级内容仍需满足好感与阶段要求。", "Early access unlocked and saved with this run; advanced content still needs favor and progression.");
        return true;
    }
    finally { _unlockBusy = false; }
}
```

**(3) 每日上限的纯函数**（`KitchenRelationsRules.cs:38-41`）—— 一条表达式夹两个上限，调用方无需判断：

```csharp
internal static int Award(int points, int earnedToday, int requested)
{
    return Math.Min(Math.Max(0, 144 - points),
           Math.Min(Math.Max(0, 16 - earnedToday), Math.Max(0, requested)));
}
```

**(4) 过夜结算的取货与销毁**（`KitchenProgression.cs:539-556` + `:573-600`）：

```csharp
internal static void SettleNight()
{
    if (!KitchenTerminal.Ready || State.FirstDay < 0 || State.SettledDay == StoreStation.GetDayCounter()) return;
    GameItem[] array = (from i in PlayerStore.Instance.FindAllItem(true)
        where ((i != null) ? i.identifier : null) == "xiaowo_kitchen_starter_stove"
              && i.parentInventory != null && KitchenInventoryOwnership.Owned(i)
        orderby i.uniqueId select i).ToArray();
    if (array.Length == 0) return;
    int dayCounter = StoreStation.GetDayCounter();
    int num = KitchenEconomyConsistencyRules.NightDemand(State.Sold, State.Pool, KitchenEconomy.Settings.StarterDemand);
    // ...逐炉遍历 Slot(item, 1).childItems
    //   IsStarterFood && Owned && GetInt(v,"XIAOWO_KITCHEN_NIGHT_READY",-1) >= 0 && hashSet.Add(RuntimeKey())
    //   整份卖完 → CraftLifecycle.DetachForCleanup(v, "starter-overnight-sale", consumed: true)
    //   部分卖   → v.unitCount -= n
    //   PlayerStore.Instance.ModCash(val2.GetBaseValue() * n, true) + KitchenLedger.Sale(...)
    State.LastSold = num2; State.LastIncome = num3; State.SettledDay = dayCounter;
    Core.Log?.Msg("Kitchen overnight sales: day=" + dayCounter + " quantity=" + num2 + " income=" + num3);
}
```

**(5) 读缓存只在 scope 内存在**（`KitchenStock.cs:31-44`）—— 预览按需重算的全部秘密：

```csharp
internal static IDisposable ReadScope() { _readDepth++; return new ReadLease(); }

internal static void Invalidate()
{
    _storeItems = null; _ownedItems = null;
    Availability.Clear(); Relevance.Clear(); CandidateReads.Clear();
}

// 所有读口都长这样（:46-67）：_readDepth > 0 才写缓存，== 0 就每次真扫
internal static List<GameItem> StoreItems()
{
    if (_readDepth > 0 && _storeItems != null) return _storeItems;
    /* ... PlayerStore.Instance.FindAllItem(true) ... */
    if (_readDepth > 0) _storeItems = list;
    return list;
}
```

**(6) 行情：确定性随机游走 + 按天追赶，不跑后台模拟**（`KitchenMarketRules.cs:115-128`）：

```csharp
for (long num2 = (long)value.Day + 1L; num2 <= today; num2++)   // 从上次算到的那天补到今天
{
    uint num3 = Draw(run, id + ":move", (int)num2);              // FNV+移位，seed = runID:品种:天
    int num4 = (int)(200 + num3 % 401);
    if ((num3 & 0x80000000u) == 0) num4 = -num4;
    value.Index = Math.Clamp((int)Math.Round((double)(value.Index * (10000L + num4)) / 10000.0), 5000, 20000);
    value.Day = (int)num2;
}
// 再叠最近 5 天内的供需冲击（Shock，:33-66），最后夹到 50~200 显示
```

**(7) 缩略平板插进原生层级的精确位置**（`KitchenTerminal.cs:1037-1064`）：

```csharp
StoreShutterButton instance = StoreShutterButton.Instance;
Transform val3 = instance?.blockerGameObject?.transform;
while ((Object)val3 != null && val3.parent != _backCanvas.transform) val3 = val3.parent;
int num = 0;
if ((Object)val3 != null && val3.parent == _backCanvas.transform)
    num = val3.GetSiblingIndex() + ((_pinnedRoot.transform.GetSiblingIndex() > val3.GetSiblingIndex()) ? 1 : 0);
if (_pinnedRoot.transform.GetSiblingIndex() != num) _pinnedRoot.transform.SetSiblingIndex(num);
((Transform)_compact).localScale = Vector3.one * (_windowCanvas.scaleFactor / _backCanvas.scaleFactor);
Vector3 val4 = main.WorldToScreenPoint(_frame.position + new Vector3(4.1f, 2.55f, 0f));
PositionCompact(new Vector2(val4.x, val4.y));
```

配合 `MatchNativeCanvas(_windowCanvas, StoreUIManager.Instance?.primaryCanvas)`（`:1078-1099`，对齐 `renderMode / worldCamera / planeDistance / sortingLayerID`）与 `_backCanvas = RenderHandler.current?.windowUICanvas`（`:1012`）——**卷帘门拉起时平板跟着露出来，被原生窗口盖住时就真被盖住**，一条 `sortingOrder` 都没硬写。

## 4. 踩坑与红线

1. **存不了档就不让存**：`KitchenState.StageForNativeSave` 在"键存在但内容是空串/null"时直接 `throw`（`KitchenState.cs:40-48`，异常文本 "refusing to overwrite it"），`KitchenSavePatch` 捕获后 `return false` → **整个原版 `SaveGame` 被跳过**，同时 `KitchenSaveFeedback` 弹红条（`KitchenSavePatch.cs:19-33`）。设计意图清晰（宁可这次不存，也不写坏档），代价是**任何一次序列化 bug 都会变成"玩家存不了档"**。抄这套必须同时抄 `KitchenSaveExitGuard` / `KitchenSaveReturnGuard` 的透明降级路径（`EscapeUIManager.OnExit` / `OnMainMenu` 前缀再试一次，`KitchenSaveReturnGuard.cs:12-27`），否则玩家连退到主菜单都会被拦。
2. **定价补丁是全仓库最大冲突面**：`GetValue` / `GetCurrentValue` / `GetBaseValue` / `GetNegociatedValue` 四个取值器全打一遍（`KitchenBeverageGetValuePatch.cs` 甚至 Prefix+Postfix 各一个），`HeadChefStablePricePatch` 显式 `[HarmonyPriority(0)]` 说明作者知道自己在链尾。**任何别的定价模组（讨价还价、鉴价、行情类）都会与它互相打架**，且 `KitchenBeveragePricing.Ensure` 在 `GetValue` 的 Prefix 里做数据自愈——读值都成了副作用。
3. **`ReadScope` 忘了包就是 O(n²)**：`KitchenStock` 的缓存只在 `_readDepth > 0` 时生效（`KitchenStock.cs:46-67`），退出 scope 立刻 `Invalidate`。写新界面时一次循环里反复 `Available(id)` 而不开 scope，就会退化成每次全店 `FindAllItem`。
4. **跨日瞬间的 transient 会归零**：`KitchenState.Get` 里若 `_state.Day != GetDayCounter()`，会**整体重建 `KitchenProgress`**（只保留 `Phone` / `Terminal`，`KitchenState.cs:78-87`）。所以 `SoldStock` / `SupplierStockIds` / `BuyerSpent` 这些"当天字段"在天刚变时读到的都是默认值——不要在 `OnGoingSleepEarly` 里读它们做结算。
5. **反射 Unity Localization 内部字段且静默吞异常**：`KitchenLocalization.cs:24-28` 抓 `LocalizationSettings.s_Instance` 与 `m_SelectedLocaleAsync`（`NonPublic`），`CurrentLocaleCode` 外面套 `try { } catch { }`（`:36-58`）。引擎/包升级后只会静默退化回英文，不会报错。追加语言也只支持 `ru` / `ja` 两种（`KitchenRuntimeTranslation.cs:24-34` 返回 0/1），加第三种语言要改代码 + 重编资源。
6. **自定义顾客的"只追加"断言**：`KitchenVisitorSchedulerPatch.AppendUnique` 在 `clientStack.Add` 后校验 `Count == count+1 && clientStack[count].identifier == id`，否则 `throw new InvalidOperationException(...)`（`:61-64`）；异常被上层 `catch` 记日志。别的模组若在同一 `OnGenerateCustomerVeryLate` 里替换队列而非追加，就会在此处炸掉（且只在日志里可见）。
7. **物品 id 半开放**：自家物品一律 `xiaowo_kitchen_` 前缀，但把原版 `small_morsel` / `cup_noodle` / `large_bottled_water` 当原料写死（`KitchenProgression.cs:29-37`），电话名片干脆克隆 `card_pharma` 再改 identifier（`KitchenBusinessCard.cs:33-41`）。原版改 id 或改名片形状 = 直接断。
8. **87 个美术进 DLL**：换来零 `files` 能力，代价是改一张图要重编译整个 2 MB+ 的 DLL，且工坊包里没有任何可热更素材。想热更就得回到 `files` 能力 + 文件夹方案。

## 5. 对我们的可用点

1. **→《深空电玩》货源资格**（现为纯天数门，`PROGRESS.md` 记"第 13 天 thatgame 未提前解锁"）：把 `if (day >= 13)` 升级成 `KitchenEconomyRules.Unlocked` 同款**单一纯函数**（§3 摘录 1），加"专业设备"条件门与 `PaidXxx` 付费提前位（`KitchenGrowth.cs:13-15` 两个 bool 就够，随存档），并保留后厨那条"最贵档不给买"的红线。
2. **→《深空电玩》刻录机批量/多步动画**：搬 `KitchenBulkCraft.Tick` 的四件套 —— `job.Frame = Time.frameCount + 1`（每步隔帧）、一帧 ≤2 步、3 ms 秒表预算、`SuppressPresentation` + 0.25 s 显示节流。现项目里"逐件实交/刻录"若有逐帧刷 UI，这是现成的卡帧解法。
3. **→《深空当铺：可能是群友》`DspVisitorPhones`（名片 + 电话约次日 + 7 天 CD）**：把冷却算进原生字段而不是自绘：`StorePhoneClient { cooldownDuration = 4, currentCooldown = LastInviteDay + 4 - today, phoneState = cooldown>0 ? 5 : 4 }`（`NpcPhone.cs:143-197` 同款）；再学 `KitchenPhone.EnsureContact` 直接往 `PlayerStore.PhoneClientDict[号码]` 塞条目（`KitchenPhone.cs:104-130`），原生电话簿就会自己显示"几天后可约"。
4. **→ 两项目共用的"第二套声望"**：《深空电玩》的"声望 -4"是原版 faction，《群友》的角色好感若也要"独立于原版"，直接抄 `KitchenContact`（5 字段）+ `Award()` 纯函数 + `Day != today → Earned = 0` 惰性日切（§3 摘录 3 与 `KitchenRelations.cs:266-271`）：**不订阅事件、不写原版结构、一个表达式管住双上限**。
5. **→《深空电玩》浮窗/面板遮挡**（整备套件、打印面板、报纸相框都踩过"被原版窗口盖住/盖不住原版"）：抄 `KitchenTerminal.Anchor()` 的插位算法（§3 摘录 7）+ `MatchNativeCanvas`，位置存 `ModHelper.GetConfig().Define("...position", "0.82,0.9")` 归一化坐标而不是写存档，拖动中不落盘、`OnDisable` 补存一次。
