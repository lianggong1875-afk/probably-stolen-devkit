# 空间站事件拓展 / Station Events｜工坊 3833966506

> 蒸馏自反编译源码 `缓存文件夹\decomp_ws\3833966506`（**38 个 .cs / 约 8100 行**，`StationEvents\EventContent.cs` 单文件 3692 行占 45%）。
> 元数据：`AssemblyTitle("Station Events")`、`AssemblyFileVersion("1.4.0.0")`、模组 id 写死 `local.station-events`、`<Capabilities/>` 零能力（csproj 只引 `Assembly-CSharp / 0Harmony / UnityEngine.* / Unity.TextMeshPro / Unity.Localization / Unity.ResourceManager`）。
> 一句话价值：**"用原版事件系统承载一整套自定义内容"的工业化样本**——70 条事件写成一张 C# 数据表，靠 `StoreEventActionDict` 四个动作 + `ActivateEvent` 落地，靠 `ES3Type.WriteProperties` 投影做到"运行时有、存档里没有"。
> 与 [11-事件系统接入规范](../11-事件系统接入规范.md) 的关系：11 号是"怎么接"的规范，本文是"它到底怎么写的"的源码级证据，并**修正 11 号里两处按通用建议写的描述**（见 §1.4、§7 尾注）。**本文行号均来自该反编译目录，未读到的内容标「（推测）」。**

## 0 速览：文件分工与硬数字

| 层 | 文件（行数） | 职责 |
| --- | --- | --- |
| 入口 | `StationEventsMod.cs`(142) | `IMod`；4 个配置项；`PatchAll` + 5 个 `ModHook` |
| 内容 | `EventContent.cs`(3692) | 70 条 `EventSpec` + 全部 `StoreEvent` / `StoreClient` 工厂 |
| 调度 | `EventRuntime.cs`(805)、`IndependentEvents.cs`(164)、`EventState.cs`(421)、`DensityRules.cs`(76)、`EventDiscovery.cs`(55)、`MarketOverlap.cs`(62)、`EventPoolRules.cs`(39)、`EventUnlockRules.cs`(24) | 注册、每日抽、冷却、密度、去重、状态编码 |
| 存档 | `NativePersistence.cs`(240)、`NativeSnapshot.cs`(11)、`SavedEvent.cs`(27) | ES3 投影 + 快照录入/还原 |
| 展示 | `CalendarScroll.cs`(559)、`NewsScroll.cs`(490)、`NativeExtras.cs`(344) | 日历格滚动、报纸滚动、情报/纸条 |
| 访客 / 本地化 | `VisitorQueuePatch.cs`(126)、`TranslationCatalog.cs`(390) | 与原版客人交错入队；9 语言 × 290 条硬编码 |

**数量实测**（逐项数过，不是估计）：`EventSpec` **70 条**（`EventContent.cs` 里 `Key = "` 出现 70 次，`ComposeSpecs()` 把 9 个分组表拼成 `Specs`）；Patch **14 个类**（透视报告写"5 个特性式 Patch"，实测 10 个用 `[HarmonyPrefix/Postfix/Transpiler/Finalizer]`、4 个用方法名约定 `Prefix`/`Postfix`）；`ModHelper` 确实只有那 6 个方法（`SetClientSource` 用了 10 次）、`ModReflect.StaticMethod` 只有 1 处（`IsEventRetired`）。

## 1 它怎么"定义一个新事件"

### 1.1 数据结构：`EventSpec`（纯数据）

`StationEvents/EventSpec.cs:5-36`：

```csharp
internal sealed class EventSpec
{
	internal string Key;
	internal string Zh;
	internal string En;
	internal string NewsZh;
	internal string NewsEn;
	internal string EndZh;
	internal string EndEn;
	internal int MinimumDay;      // 最早第几天能进池
	internal int Duration;        // 持续天数
	internal int Cooldown;        // 结束后冷却天数
	internal int Weight;          // 抽取权重（原版叫 odd）
	internal string[] PriceTags;  // 影响哪些 GameItemType 的价格
	internal string[] BuyingTags = Array.Empty<string>();     // 访客要买的品类
	internal string[] BuyingItemIds = Array.Empty<string>();  // 访客要买的具体 id
	internal int[] PriceChanges;  // 与 PriceTags 一一对应的百分比
}
```

**没有**"效果脚本 / 前置条件委托 / 可见性开关"字段——这些下沉到 `EventContent` 的工厂里按 `Key` 分派（70 条事件先进 9 个 `IsXxx(key)` 分类函数，再进各自的 `CreateXxxCustomer`）。即：**数据结构只覆盖"能被数据描述的部分"，行为一律是代码**。

### 1.2 一条事件的真实写法（`EventContent.cs:49-65` 摘，新闻正文用 … 省略）

```csharp
new EventSpec
{
	Key = "cafeteria",
	Zh = "公共食堂暂停营业",
	En = "Canteen Closure",
	MinimumDay = 40,
	BuyingTags = new string[1] { "FOOD" },
	Duration = 2,
	Cooldown = 14,
	Weight = 9,
	PriceTags = new string[1] { "FOOD" },
	PriceChanges = new int[1] { 40 },
	NewsZh = "下层区一家公共食堂因设备故障暂停营业。……",
	NewsEn = "A public canteen in the Lower Levels has closed …",
	EndZh = "公共食堂已恢复营业，……",
	EndEn = "The public canteen has reopened, …"
},
```

加一条事件 = 表里加一个匿名初始化块 +（需要特殊顾客/货物时）在对应 `CreateXxxCustomer` 里加一个 `case`。**全在 C# 里，没有任何 JSON**：反编译目录除 `.cs` 只有 `StationEvents.csproj`，无 `Config/`、无 `Localization/`、无 `.json`。

### 1.3 从 spec 到原版 `StoreEvent`：`EventContent.CreateEvent`

`EventContent.cs:1374-1412`（把 `val2` 中间变量与空行略去，语义未改）：

```csharp
internal static StoreEvent CreateEvent(string key)
{
	EventSpec eventSpec = Find(key);
	if (eventSpec == null) return null;
	string text = "local.station-events:" + eventSpec.Key;
	StoreEvent val = new StoreEvent();
	val.identifier = text;
	val.newsName = Text(eventSpec.Zh, eventSpec.En);
	val.displayName = Text(eventSpec.Zh, eventSpec.En);
	val.newsDescription = Text(eventSpec.NewsZh, eventSpec.NewsEn);
	val.duration = eventSpec.Duration;
	val.cooldown = eventSpec.Cooldown;
	val.eventType = (EventType)0;      // NORMALE
	val.eventArea = (EventArea)2;      // ALL：上下层通吃
	val.importance = 5;
	val.isDurationVisible = true;
	val.onActiveActionId           = text + ":start";   // ↓ 四个动作 id 必须手填；原版
	val.onUpdateActionId           = text + ":update";  //   InitNormalEvent 只会填
	val.onRemovedActionId          = text + ":end";     //   identifier+"addClientAction"
	val.addClientFromEventActionId = text + ":clients";
	for (int i = 0; i < eventSpec.PriceTags.Length; i++)          // ↓ 价格效果就是这一句
	{
		NegociationData val3 = new NegociationData(eventSpec.PriceTags[i], eventSpec.PriceChanges[i], val.newsName, val.displayName);
		val3.identifier = text + ":price:" + eventSpec.PriceTags[i];   // 原版构造给的是 Guid
		val.negociationDatas.Add(val3);
	}
	ApplyStage(val);       // contract 的两阶段换档，见 §2
	return val;
}
```

三个要点：

1. **四个 action id 手填**。原版 `StoreEvent.InitNormalEvent()`（`StoreEvent.cs:271-273`）只填 `identifier + "addClientAction"`；这里全改 `:start/:update/:end/:clients`，字典是纯字符串键，与原版动作并存不冲突。
2. **价格效果就是 `negociationDatas`**。`NegociationData(itemType, value, publicDisplay, reason)`，`value` 是百分比整数；原版 `GameItem.GetCurrentValue()`（`reference/Assembly-CSharp/GameItem.cs:1771-1791`）遍历 `GetActiveEvents()`，把命中 tag 的 `value` 累加进 `marketMult`。**所以改价格根本不用碰价格代码**。
3. **`NegociationData.identifier` 被改成 `<事件id>:price:<tag>`**（原版给 `Guid`）。换来两件事：`CleanFeatures("local.station-events:"+key+":price:")` 能按前缀清残留（`EventRuntime.cs:763-776`），换档时也能精确定位。

### 1.4 事件怎么进原版池：**它根本没进蓝图表**

`EventRuntime.Register()`（`EventRuntime.cs:121-155`）：

```csharp
internal static void Register()
{
	foreach (EventSpec eventSpec in EventContent.Specs)
	{
		EventSpec s = eventSpec;
		StoreEventBlueprint item = new StoreEventBlueprint((Func<StoreEvent>)(() => CreateEvent(s.Key)), s.Weight, "local.station-events:" + s.Key);
		blueprints.Add(item);                       // ← 加进自己的私有 List，不进任何原版静态表
		AddAction(s.Key, "start",   () => { Started(s.Key); });
		AddAction(s.Key, "update",  () => { Updated(s.Key); });
		AddAction(s.Key, "end",     () => { Ended(s.Key); });
		AddAction(s.Key, "clients", () => { QueueClients(s.Key); });
	}
}
private static void AddAction(string key, string suffix, Action action)
{
	string key2 = "local.station-events:" + key + ":" + suffix;
	actions.Add(key2, action);
	StoreEventActionDict.actions[key2] = action;    // ← 全项目唯一写进原版的地方
}
```

`blueprints` 是 `EventRuntime` 自己的 `static readonly List<StoreEventBlueprint>`（`:38`）。全仓 grep `normalEventBlueprints` 只有两处：`EventRuntime.cs:783`（`Disable()` 里 `Remove`，注册时没加过，等于空转）、`IndependentEvents.cs:146`（合并池模式下**只读**它来借原版候选）。**它一个静态蓝图表都没写。**

真正的"入池"是自建抽取后直接激活（`IndependentEvents.cs:119-141`）：`RNG.Roll(DrawChance) → RNG.GetOdd(weights) → storeEventFunc() → manager.ActivateEvent(val, false, false)`（走原版激活，连带 `IsKnown`、`OnEventActive`、`activeEvents`、冷却）。完整四道闸门见 §7-B。

| 入池路线 | 怎么用 | 取舍 |
| --- | --- | --- |
| 塞 `normalEventBlueprints` | 原版 `HandleNewEventNormale` 自己抽 | 交给原版，**无法控密度/间隔/上限**；且 `GetEventCounter()>=3` 就不开抽 |
| 塞 `cosmeticEventBlueprints` | 原版 `HandleNewEventComestic` 抽（`while activeEvents.Count < 2`） | 只能当气氛新闻，被 2 条上限卡死；`ApplyCooldown` 跳过 COSMETIC 故无冷却 |
| **自持蓝图 + 自己抽 + `ActivateEvent`**（本品） | 完全自定义闸门 | 自由度最高；代价是自己处理计数屏蔽、存档投影、展示接管（本文后半全是这个代价） |

它甚至**反向借原版候选**：`include_vanilla_events` 打开时 `AddNativeCandidates()` 把 `normalEventBlueprints` 里未冷却、未 `IsEventRetired` 的条目复制进自己候选，共用一套密度（`IndependentEvents.cs:144-154`），并用 `manager.IsEventCompatiblePreCheck(val)` 兜互斥。

### 1.5 前置条件 / 可见性

- **天数**：`EventUnlockRules.MinimumDay(spec)`（开 `all_events_from_start` 就全体返回 0）+ 全局排程起点 `SchedulingStartDay`（默认 40 天）。
- **内容**：`EventContent.Available(spec)` 转派 9 个 `XxxAvailable(key)`。硬门槛直接读原版公开状态：`StoreReputation.IsPerkUnlocked("SEC_REQUISITION")`、`PlayerStore.Instance.isStolenItemIntroduced`、`ModHelper.IsNetworkUpgradeUnlocked("RETIRED_FARMER")`、`NetworkUpgrade.IsWinemakerUnlocked()`、展示柜有 `FOOD` 且总价 ≥350（`HasPrivateChefShowcase()`，`:2609-2618`）。
- **可见性**：只用了 `isDurationVisible = true`；结束新闻 `:ended` 用 `eventType = COSMETIC` + `importance = 4`，靠 `ApplyCooldown` 对 COSMETIC 直接 return 来避免占冷却（`StoreEventManager.cs:243-253`）。
- **反向闸门**：不该出的伪装成"在冷却"，见 §7-B。

## 2 效果怎么落地（全表，最有价值的一张）

原生 `StoreEvent` 只有两把"真·世界效果"的钥匙：`negociationDatas`（价格）与 `addClientFromEventActionId`（来客）。**其余效果全靠"造一个合适的 `StoreClient`"**。

| 效果 | 原版 API / 字段 | 源码位置 | 说明 |
| --- | --- | --- | --- |
| **改某类货价格 ±%** | `StoreEvent.negociationDatas` + `NegociationData(itemType, value, publicDisplay, reason)` | 写入 `EventContent.cs:1404-1409`；消费 `GameItem.cs:1771-1791`、`CommissaryData.cs:177-183`、`CalendarEventElement.cs:33` | tag 命中即累加 `marketMult`，多事件叠加；**零 Patch** |
| **阶段换档（同一事件两种效果）** | 运行时重建 `negociationDatas` | `EventContent.ApplyStage` `:1763-1791` | `contract` 前段"加急采购"+40，第 2 天起变"工程取消清仓"−40，靠 `IsClearance(e)=totalDuration-duration>=1` 判阶段 |
| **顾客上门买货** | `new StoreClient()` + `clientIntent=(ClientIntent)1` + `clientBuyingTagList` / `clientBuyingIdList` | `:1474-1513` | 必须 `AddBasicDialog()` 再拼 `mainDialogue`；`placedWrongItemWhenSellingToDialogue` / `placeRightItemWhenSellingToDialogue` 给对/错提示 |
| **顾客带货来卖（清仓）** | `clientIntent=(ClientIntent)2` + `PrepareWholesaleVisit()` | `:1494-1500`、`:1568-1576` | 靠 `mainDialogue.nextDialogue.SetEndAction(...)` 触发上架；失败时 `WholesalePlacementPatch` 在 `NegociationUIManager.BuildSelling` 上加"没摆上/重试"按钮 |
| **往柜台/展示台上架货** | `PlayerStore.TryAddDirectSellingItemToTable(id\|GameItem, bool isOwend=false, bool isStolen=false, bool ignorePerk=false, int forcedHeat=0)` | 调用点 `:926/933/1289/1730/1907/1926/2320/3248/3259/3636/3647` | 5 参语义：归属 / **isStolen**（成色与查处风险）/ 忽略特权 / 强制热度。`stolen=true` 就是"贼货上门" |
| **顾客预算** | `StoreClient.SetBudget(int)` / `GetBudget()` | 包装器 `SetPurchaseBudget()` `:1665-1669`（基础值 **±10% 抖动**）；预算表 `BaseBudget()` `:1671-1683`、`ExpandedBudget()` `:1866-1876` | 预算 = 基础值 + `min(day,100) × 系数`，随天数线性膨胀 |
| **顾客身份/派系** | `StoreClientFactionSetup.InitBlackMarket / InitSecurity / InitUpperLevelCitizen …` | `:2659 / 2677 / 2770`；本模组的 `InitSecurityLogistics()`（`:2768-2775`）在 `InitSecurity` 之上补 `isSecurity=false` + `bubbleColor="sec"` + `tradeReputationMultiplier=2.0` | 手写阵营字符串会让 `CompleteClientCreation` 挂错识破档位，故全用官方 setup 再叠字段 |
| **议价/识破强度** | `client.tradeReputationMultiplier`、`acceptedChemicalProductGrade`、`CanExposeChem`、`acceptRetailMarkup`、`clientBlackTagList` | `:2661-2664 / 2773 / 2222` | 四个字段就能做出"难缠的老江湖" |
| **禁收违禁品** | `client.noAcceptingContraband = true`（20+ 处） | 注意 `CompleteClientCreation(false)` **之后**又写一次（`:1514`、`:1862`），该函数内部会重算 |
| **声誉** | **只读**：`StoreReputation.IsPerkUnlocked(perk)`、`GetRevReputation()` | `:824 / 2145-2161 / 2569-2595 / 2871` | 做门槛与数量（革命声望/20 → 多给货），**从不写** |
| **钱 / 现金** | 无直接调用 | — | 玩家赚钱只有一条路：顾客用预算买货 |
| **报纸新闻** | `newsName` / `newsDescription` + 结束态 `:ended` 事件 | `EventRuntime.Ended()` `:447-460` | "结束也走事件系统"：造 `duration=1, cooldown=0` 的 COSMETIC，正文用 `EndZh/EndEn` |
| **预告纸条** | `StoreEvent.CreateAndAddSlip(int timer)` / `QueueFuturEvent(ev, day, IsKnown)` | `NativeExtras.QueueForecast` `:186-197` | `IsKnown=true` 才是"玩家已知道的预告" |
| **情报（顾客嘴里的风声）** | `StoreClientListInformation.informationList / informationListProba / informationEventIds` 三张平行表 | `NativeExtras.cs:31-42` | 见 §4.3 |
| **来客（每日上限）** | `StoreClientManager.AddClient(client)` + 手工重排 `clientStack` | `EventRuntime.QueueVisitors` `:488-646` | 见 §5 |

**"它完全不碰"的清单**（同样有价值）：不改声誉、不给钱、不改难度、不写 `storeRequests`（只留 `LegacyRequestCleanup` 清旧版遗留）、不碰 `cosmeticEventBlueprints`、不动关系网科技树（只读 `IsPerkUnlocked`）。

## 3 存档处理：ES3 投影 + 自己的快照（对应经验库 14 号范式）

### 3.1 与 ES3 相关的只有三处

| 位置 | 作用 |
| --- | --- |
| `SerializationProjectionPatch.cs`：`[HarmonyPatch(typeof(ES3Type), "WriteProperties")]` + `Prefix(ref object __0)` | 序列化任意对象前，若它就是我们的 `StoreEventManager` → 换成剔除版克隆 |
| `SavePreparationPatch.cs`：`PlayerStore.SaveGame()` 的 `Prefix`（可返回 false 跳过存档）+ `HarmonyFinalizer` | 存档前建快照与投影，结束后一定清引用 |
| `NativePersistence.cs` | `Capture` / `FilterForWrite` / `Restore` 三件套 |

```csharp
// NativePersistence.cs:73-100（原文核心；foreach 体压缩为单行）
internal static object FilterForWrite(object obj)
{
	StoreEventManager val = (StoreEventManager)((obj is StoreEventManager) ? obj : null);
	if (val == null) return obj;
	ValidateFutureLists(val);
	if (CloneMethod == null) throw new MissingMethodException("System.Object", "MemberwiseClone");
	StoreEventManager val2 = (StoreEventManager)CloneMethod.Invoke(val, null);   // 浅克隆
	List<StoreEvent> list = new List<StoreEvent>();
	foreach (StoreEvent item in Active(val))
		if (item == null || !Own(item.identifier)) list.Add(item);              // 只留非我方
	ActiveField.SetValue(val2, list);                                           // activeEvents 是 private
	CopyUnownedFuture(val, out val2.futurEvents, out val2.futurEventsDay);
	val2.eventTracker = UnownedCooldowns(val.eventTracker);
	return val2;
}
```

`ActiveField = typeof(StoreEventManager).GetField("activeEvents", BindingFlags.Instance | BindingFlags.NonPublic)`（`:12`）——原版这个列表是 **private**（`reference/Assembly-CSharp/StoreEventManager.cs:17`），而 `futurEvents` / `futurEventsDay` / `eventTracker` 都是 public，所以只有一个字段需要反射。

### 3.2 为什么非做投影不可（三条能从代码证实的理由）

1. **`StoreEvent` 每个字段都是 `[ES3Serializable]`**（`reference/Assembly-CSharp/StoreEvent.cs:22-75`）——"塞进 `activeEvents` 就必然进存档"，没有选择余地；想不存只能在写的时候摘掉。
2. **本模组的正本是"spec 表 + 自己的快照"，不是原版对象**。读档走 `Restore`：按 `SavedEvent.Key` 用 `EventRuntime.CreateEvent(key)` **重新造对象**，再覆盖 `duration/totalDuration/startDay/currentDay/IsKnown`（`NativePersistence.cs:138-161`）。改数值、改文案、换语言都能立刻生效；反之信任存档里的旧对象，文案会冻结在存档那天（`事件与内容扩展四例.md` §3.4 记录过这个坑，本品用"压根不存"绕过）。
3. **卸载安全**。原版读档不问"这个 id 是谁的"：`activeEvents` 里留着 `local.station-events:xxx` 就是幽灵事件，`eventTracker` 里留着冷却键就永远冷却。投影后原版存档里**一个我方 id 都没有**，卸模组即干净。

> （推测）第四条：`Restore` 对每个 entry 校验 `val.identifier == text` 否则 `throw`，更像在防"自己改前缀/改 key"这类迭代事故，而不是防玩家。

### 3.3 完整链路

```text
存档：PlayerStore.SaveGame() Prefix → PrepareSave()（EventRuntime.cs:376-403）
  FinishSave() → EnsureState() → NativePersistence.Capture(mgr)      // 抄我方条目成 List<SavedEvent>
  ManagerProjection = FilterForWrite(mgr)                           // 先预演克隆，异常能早发现
  state.Snapshot = snapshot; state.SnapshotDay = Day; WriteState()   // → ModHelper.SetModData
  PreparedManager = mgr                                              // 打开投影开关
  ES3 写档 → ES3Type.WriteProperties Prefix → FilterWrite(ref obj)
    ReferenceEquals(obj, PreparedManager) 才换成剔除克隆 → 存档里没有我方 id
  HarmonyFinalizer → FinishSave()                                    // 两个引用都置 null
  任一步抛异常 → PrepareSave 返回 false → Prefix 返回 false → 本次存档整个不写

读档：BeforeLoad / AfterLoad（EventRuntime.cs:295-342）
  OnGameLoadedInit  → BeforeLoad: contextEpoch++ / loading=true / RemoveModData(两份旧键) / 清 UI 缓存
  OnGameLoadedEarly → AfterLoad: EnsureState() ← GetModData("local.station-events","native-events-v1")
      TryRead 失败 → faulted=true + LogError 一句，拓展整体停摆（原版数据不动）
      ReconcileDay(Day)       天数回放 / 拒绝更早的档
      ApplyCurrentPolicy(Day) spec 迁移：按当前 Duration 重算 totalDuration/duration
      RemoveRetiredEvents()   17 个已废弃 key 硬编码清理
      Restore(mgr, Snapshot.Entries, Snapshot.Cooldowns) → RemoveQueued(null) → WriteState()
  OnGameLoadedLate  → NativeExtras.Loaded(): 扫背包里我方纸条，重挂 onCycleEndSlotItemFunc
```

`ReconcileDay`（`EventState.cs:233-284`）是最老练的一段：`day < SnapshotDay`（读到更早的档）→ `return false` 让拓展停摆；否则按天数差**回放**：`FutureDays -= num`、`Duration -= num`、`CurrentDay += num`、冷却同样递减，过期的丢弃。

### 3.4 状态编码：不是 JSON，是紧凑管道符 + Base64

`EventState.Encode`（`EventState.cs:64-89`）的报文（`\n` 分行、`|` 分列、字符串一律 `B()` → Base64，`~` 代表 null）：

```text
station-events-v1                    ← 版本头，TryRead 硬校验
D|SnapshotDay                        R|ScheduledDay|VisitorDay|VisitorsServed
V|PolicyVersion                      F|DensityPulse|PulseUntilDay          M|MixedLastStartDay
E|key|FutureDays|Duration|TotalDuration|StartDay|CurrentDay|Known|NewsName|NewsDescription|DisplayName
C|<我方 id>|冷却剩余                    P|key|StartDay|Visits|Purchases|Sales|VisitDay0|VisitDay1|Role|Occurrences
```

`TryRead` 的 4 条硬约束值得照抄：**总长 ≤256KB、行数 ≤256、每行段数精确匹配、每段数值有 min/max 区间**（`I()` 越界即 `throw` → 外层唯一的 `catch(Exception) → return false`）。这是本仓允许的"边界捕获"范式：语义是"读坏了整份作废"，不是吞异常。key 额外过 `ValidKey`（只允许 `a-z : -`，≤96 字符）。

### 3.5 与 14 号 ES3 无污染范式的对照

| 项 | 14 号建议 | 本品实现 |
| --- | --- | --- |
| 投影点 | `ES3Type.WriteProperties` Prefix 改参数 | ✅ 一致，且用 `ReferenceEquals(obj, PreparedManager)` 精确锁定，不误伤其它对象 |
| 克隆方式 | 反射 `MemberwiseClone` 造剔除副本 | ✅ 一致 |
| 读档重建 | `OnGameLoadedEarly` 从 ModData 读回、工厂重建 | ✅ 一致，另加三层校验（identifier 一致、`FutureDays >= -1`、`key\|days` 去重） |
| 失败姿态 | 失败就自杀降级 | ✅ `faulted` 一旦置真，`Live`/`CanStart`/`Schedule` 全部短路；`PrepareSave` 失败连存档都不写 |
| 14 号没有的一手 | — | **天数回放 `ReconcileDay`**（跨天读档必需）与 **`PolicyVersion` 迁移**（旧档按当前 spec 自动升级） |

## 4 日历 / 报纸 / 情报 / 纸条

### 4.1 日历：`CalendarDayElement.SetElement`（Prefix 加座位 + Postfix 包滚动）

原版日历格**固定 5 行**（`CalendarDayElement.Init()` 里 `for (i=0;i<5;i++)` 各 `Instantiate` 一个 prefab，`reference/Assembly-CSharp/CalendarDayElement.cs:36-46`；本模组写成常量 `CalendarScroll.NativeRows = 5`，`CalendarScroll.cs:113`），超出的活动日塞不下。补丁做两件事：

```csharp
// CalendarDayScrollPatch.cs:6-17 —— 注意：没有 [HarmonyPrefix] 特性，靠方法名约定；原为带大括号的两行
[HarmonyPatch(typeof(CalendarDayElement), "SetElement")]
internal static class CalendarDayScrollPatch
{
	private static void Prefix(CalendarDayElement __instance, List<StoreEvent> __1, CalendarDayElement __2)
		=> CalendarScroll.BeforeSetElement(__instance, __1, __2);
	private static void Postfix(CalendarDayElement __instance) => CalendarScroll.AfterSetElement(__instance);
}
```

- **Prefix**（`CalendarScroll.cs:126-165`）：`RequiredCapacity(events.Count, previous.eventElements.Count)` 不够就用原版 `eventElementPrefab` `Instantiate` 补座位（拿不到 `CalendarEventElement` 就 `Destroy` 并 `throw`），追加进 `day.eventElements`——**原版随后自己按索引填内容**，补丁只负责加壳。
- **Postfix**（`:167-199`）：找出**最后一个仍激活的行下标** `num`；`num < 5` 就 `Unwrap` 还原，否则 `GetState → Wrap → Reflow → ResetPosition`。
- **Wrap**（`:229-284`）：把 `ScrollRect + RectMask2D + Viewport + Content + 自绘 Scrollbar` 的壳**插在原父节点、原 sibling 位置**，用 `RectState` 快照把原 RectTransform 的 anchor/pivot/size/position/scale/rotation 全套搬过去，再把 `layoutGroup` 挂到新 Content 下——**视觉零位移**，只是变可滚动。
- 生命周期用 `CalendarScrollLayout` 这个小 MonoBehaviour（`OnEnable/OnDisable/OnDestroy` → `Opened/Closed/Destroyed`）回收状态字典，避免 UI 重建后残留 `CalendarDayElement` 键。
- 顺带：`CalendarEventElement` 会读 `storeEvent.negociationDatas` 显示价格影响（`CalendarEventElement.cs:33`），**§1.3 写进去的价格数据在日历上自动可见**，不用自己画。

### 4.2 报纸：`SelectLayout` 接管 + 复用原版 `PopulateUI`

原版只有 4 套版式，`PopulateUI` 的上限是 `Mathf.Min(activeEvents.Count, activeLayoutElements.Count)`（`reference/Assembly-CSharp/NewsUIManager.cs:110-130`）。作者的发现是：**只要把 `activeLayout` / `activeLayoutElements` 换成自己的（反射设 private 字段），原版 `PopulateUI` 就会把 N 条全填进去**——不必重写填表逻辑。

```csharp
// NewsScroll.cs:55-94（压缩：省略部分换行）
internal static bool TrySelect(NewsUIManager manager, List<StoreEvent> events)
{
	if ((Object)(object)manager == (Object)null || events == null) return false;
	if (events.Count <= 4) { if ((Object)(object)owner == (Object)(object)manager) Hide(); return false; }
	if (ActiveLayout == null || ActiveElements == null)
		throw new MissingFieldException("The native newspaper layout fields changed.");
	if ((Object)(object)owner != (Object)(object)manager || (Object)(object)root == (Object)null) Create(manager);
	EnsureRows(events.Count);
	manager.layout1.SetActive(false);  manager.layout2.SetActive(false);
	manager.layout3.SetActive(false);  manager.layout4.SetActive(false);
	foreach (NewsUIElement element in elements) { element.ClearElement(); ((Component)element).gameObject.SetActive(false); }
	root.SetActive(true);
	ActiveLayout.SetValue(manager, root);        // private field：activeLayout
	ActiveElements.SetValue(manager, elements);  // private field：activeLayoutElements
	scroll.StopMovement(); resetTop = true; dirty = true;
	return true;                                 // Prefix 返回 false → 原版 SelectLayout 不执行
}
```

`NewsLayoutPatch` = `[HarmonyPrefix] bool Prefix` 返回 `!TrySelect(...)`；`NewsPopulatedPatch` = `PopulateUI` 的 Postfix → `AfterPopulate` 标脏并立即 `Reflow()`（`:96-103`）。行高用 `TMP.GetPreferredValues(text, width-16, ∞).y` 现算（`:406-407`），`NewsScrollLayout.LateUpdate → Tick()` 处理宽高变化。**代价**：`RectMask2D`/`ScrollRect`/`Scrollbar` 全手搓（`:279-336`）。

### 4.3 情报（顾客口述风声）与预告纸条

登记三张平行表，`proba = 75`（`NativeExtras.cs:31-42`）：`informationList.Add(client => SetInformation(client, spec))` / `informationListProba.Add(75)` / `informationEventIds.Add("local.station-events:" + spec.Key)`。

`SetInformation`（`:119-158`）把 `acceptDealDialogue` 拼成"我听到了关于《事件名》的安排" → `SlipBody + GetEventPriceEffect()` → 结束动作 `QueueForecast(key, delay)`；后者先 `CreateAndAddSlip(delay)`（塞一张原版纸条）再 `QueueFuturEvent(val, delay, true)`。**不可用时回落到原版情报**（`SetVanillaFallback` 挑第一条非我方、不在冷却的，`:160-184`），所以不会出现"顾客欲言又止"。

纸条是**原版 `slip` 物品 + 4 个私有 tag**（`local.station-events:slip-key/-due/-duration/-cycle-day`，`:209-228`），靠 `GameItemFunc.onCycleEndSlotItemFunc` 每次翻格重算 `SLIP_DATA`（剩余天数）与 `SLIP_BODY`；正文调原版 **private** `StoreEvent.BuildSlipBody(int)`（`:20`、`:318-325`），所以报文格式与原版预告完全一致；剩余 `<0` 就 `item.Destroy()`（`:283-288`）。

### 4.4 卷帘门

**没有接入**。全仓 grep 无 `Shutter` / 卷帘 相关类型或 Patch。它在卷帘门生态里只是"另一个往 `activeEvents` 里放事件的人"——卷帘门读 `GetActiveEvents()` 会看到它的 70 条事件（含无价格效果的 COSMETIC 结束新闻），这正是 `深空电玩` 那个 `hide_unrelated_events` 开关存在的原因。

## 5 顾客相关：`SetClientSource` + `OnGenerateCustomerVeryLate`

### 5.1 双标记：`sourceModId` + `eventSourceId`

10 处 `ModHelper.SetClientSource(client, "local.station-events")`，紧随其后必设 `client.eventSourceId = "local.station-events:" + key`。归属判定因此是**双条件**（`EventRuntime.cs:648-655`）：`client.sourceModId == "local.station-events" && Own(client.identifier) && Own(client.eventSourceId)`。`sourceModId` 声明来源（也是别人识别"这不是原版客人"的唯一线索），`eventSourceId` 才是"哪个事件引来的"，用于过期清理与"一事件只来一次"。

### 5.2 `OnGenerateCustomerVeryLate` 只负责**派系**来客

```csharp
// EventRuntime.cs:479-486
internal static void QueueFactionVisitors(StoreClientManager manager)
{
	List<StoreClient> ordinaryArrivals = VisitorQueuePatch.TakeOrdinaryArrivals(manager);
	if (Live && manager != null && object.ReferenceEquals(PlayerStore.Instance.storeClientManager, manager) && EnsureState()
		&& NativePersistence.Active(Manager).Exists((StoreEvent s) => s != null && s.duration > 0 && s.startDay >= 0
			&& NativePersistence.Own(s.identifier)
			&& EventContent.IsFactionEvent(s.identifier.Substring("local.station-events:".Length))))
		QueueVisitors(manager, afterVanilla: true, ordinaryArrivals);
}
```

普通来客走**另一条路**：事件激活/推进时由 `:clients` 动作（`QueueClients`，`:462-477`）当场排队。派系来客必须等原版客人全生成完，所以挂 `VeryLate`。

**每日本模组来客硬上限 2 名**（`:559-566`）：`capacity = max(0, 2 - state.VisitorsServed - 已在场的未计数客人)`；`EnsureVisitorDay(day)` 每天重置计数并扫 `VisitDays` 重算。排序键 `(剩余天数, 出现次数, 起始天, key)`——**先照顾快结束的事件**。

### 5.3 交错而不是追加

`VisitorQueuePatch` 挂 `HandleNormalClient`：`Prefix` 只 `out int __state = clientStack.Count`；`Postfix` 取区间 `[__state, Count)` 的纯原版客人（`attractedItemId == null && sourceModId 空 && eventSourceId 空`），把队列里已有的我方客人抽成 `Queue`，重建时**每个原版客人后紧跟一个我方客人**，剩余追加末尾（`VisitorQueuePatch.cs:21-74`）。派系来客再单独 `PlaceFactionVisitor` 插到自己人后面（不希望被原版客人夹住）。`TakeOrdinaryArrivals` 用过即清，避免跨帧串档。

### 5.4 防重复：`VisitDays[2]` + `Occurrences`

`EventProgress`（`EventProgress.cs:3-20`）为每条事件存 `StartDay/Visits/Purchases/Sales/VisitDays[2]/SelectedRole/Occurrences`。`SelectedRole` 是**进档落盘的随机角色选择**（`RNG.GetRandomInt(0,1)`，`EventRuntime.cs:519-529`），所以读档后角色不变，否则同一事件每次读档换个人设。`HasRoleArrived` 保证同一 `StartDay` 的同一角色只来一次；`RecordVisit` 三重校验（`Day == day`、`Progress.StartDay == occurrence`、事件仍活跃且 `IsClearance` 一致）才计数。

## 6 配置与本地化

### 6.1 四个配置项（`GetConfig` 全部）

| key | 类型 / 默认 | 作用 | 落地 |
| --- | --- | --- | --- |
| `event_density` | `string`，默认 `Choices[2]`（"暗流涌动"） | 五档：最短间隔 5/4/3/2/1 天 + 活跃门槛 1/1/2/2/2 | `DensityRules.Level`（下标非法回 2） |
| `event_rhythm` | `bool`，`true` | 平静/平稳/活跃三阶段脉冲 | `DensityRules.PeriodsEnabled` |
| `all_events_from_start` | `bool`，`false` | 关闭时事件按 `MinimumDay`（40~60 天）分批进池 | `EventUnlockRules.AllFromStart` |
| `include_vanilla_events` | `bool`，`false` | 与原版普通事件合并成一个加权池 | `EventPoolRules.IncludeVanilla`（setter 里 `EventRuntime.PoolModeChanged()` 重置排程日） |

四项都 `.Define(key, default, section, Func<string> label, choices?, Func<string> desc, false)`——**label/desc 传委托**，切语言不用重建配置项（11 号 §8-③ 收过，这里确认为真）。`OnChanged` 立刻生效，且**都不取消已有事件/预告**（描述里明说）。

### 6.2 文案组织：zh/en 内联 +「英文原文当键」的 9 语言表

`EventContent.Text(zh, en)`（`:3652-3691`）判定顺序：`zh == en` → 返 en；`LocalizationSettings` 空/未就绪 → 返 en；`localeCode == "zh"` → 返 zh；其余 → `TranslationCatalog.Get(code, en)`，查不到返 en。

`TranslationCatalog`（390 行）结构：`English[290]`（**英文原文数组，同时充当索引键**）+ `Values[9][290]`（`fr/de/it/ja/ko/pl/ptBR/ru/es`），`Indices = Dictionary<英文, index>`，`Get` 用 `Array.IndexOf(Languages, code)` 定位语言（`:381-389`）；`LocaleCode()` 自己处理 `zh_Hans` / `chinese` / `pt-BR` / `"English (United States)"` 这类脏值。

**评价**：加一条事件 = 中文 + 英文各写一遍即可上线，9 语可后补；代价是**靠英文原文做键**，改英文文案则旧翻译静默失效（fallback 到英文，不报错）。比"按数组下标对齐"（11 号描述的旧做法）更抗插入，更怕改词。

### 6.3 下拉选项的 bilingual 拆分

`Choices` 里写的是 `"暗流涌动 | Undercurrents"` 这种合并串，配置系统本身不懂双语；`DensityLocalePatch` 在 `CustomUIBuilder.AddDropdown(IList<string>, int, Action<int>, string)` 的 Prefix 里用 `ReferenceEquals(__0, StationEventsMod.DensityChoices)` 认领自己的列表，再按 `" | "` 拆开分别 `Text()`（`DensityLocalePatch.cs:14-31`）。**ReferenceEquals 认领**很省，也不污染别的模组的 dropdown。

## 7 可复用模式（4 个）

### 7-A 事件定义表 + 工厂 + 四动作注册（最小闭环）

`EventSpec`（§1.1）+ `CreateEvent`（§1.3）+ `Register`（§1.4）。三条要点：① `identifier` 带命名空间前缀，**全部判定收敛到一个 `Own(id)`**（`NativePersistence.cs:16-19`），清理/存档/归属都调它；② 四个 `on*ActionId` 指向 `StoreEventActionDict.actions` 的键，**动作函数只做记账不直接放效果**（`Started` 只写 state，效果由事件自身的 `negociationDatas` 承担）；③ 工厂必须能"按 key 从零重建"，因为读档、纸条重算、结束新闻都靠它（`CreateEvent(key + ":ended")` 也走同一入口，`EventRuntime.cs:157-184`）。

### 7-B 每日抽取的四道闸门（含"反向闸门"）

```csharp
// IndependentEvents.cs:52-61 + :87-95（原文，断行有调整）
bool includeVanilla = EventPoolRules.IncludeVanilla;
if ((includeVanilla && (!EventRuntime.CanCombine(manager) || (!EventPoolRules.NativeNormalTimeAvailable() && !EventUnlockRules.AllFromStart)))
	|| !EventRuntime.TryBeginDailyDraw(manager)) return;       // 闸门三：一天只抽一次
int densityPulse = EventRuntime.GetDensityPulse(manager);
if (densityPulse <= 0) return;                                 // 脉冲处于静默期
// …统计当前活跃数 num…
if (num >= DensityRules.Cap()) return;                         // 闸门二：名额（1 或 2）
int num3 = (includeVanilla ? EventRuntime.GetLastCombinedStartDay(manager) : EventRuntime.GetLastActivatedDay(manager));
if (num3 >= 0 && (long)StoreStation.Instance.dayCounter - (long)num3 < DensityRules.MinimumInterval()) return;   // 闸门一：间隔
// …组候选 → RNG.Roll(DrawChance(num, pulse)) → RNG.GetOdd(weights) → ActivateEvent
```

**反向闸门**（`EventRuntime.cs:249-259`）——把"不该出"伪装成"在冷却"：

```csharp
internal static void Eligibility(string id, ref bool result)
{
	if (StationEventsMod.Active && NativePersistence.Own(id))
	{
		EventSpec eventSpec = EventContent.Find(id.Substring("local.station-events:".Length));
		if (eventSpec != null && (!Live || !EnsureState() || faulted || Day < EventUnlockRules.MinimumDay(eventSpec) || !EventContent.Available(eventSpec)))
			result = true;      // 对原版来说"这条在冷却" == 屏蔽
	}
}
```

妙处：`CanStart()` 自己也调 `Manager.IsEventOnCooldown(text)`（`:194`），**同一个 Postfix 顺带把自家闸门也关上了**，不必写两遍条件。

### 7-C 保存投影（三件套，见 §3.3 全链路）

```csharp
// EventRuntime.cs:411-417
internal static void FilterWrite(ref object obj)
{
	if (StationEventsMod.Active && PreparedManager != null && object.ReferenceEquals(obj, PreparedManager))
		obj = NativePersistence.FilterForWrite(obj);   // 换成剔除我方事件的 MemberwiseClone
}
```

```text
Capture(mgr): activeEvents 里 Own 的 → Save(-1)；futurEvents[i] 里 Own 的 → Save(futurEventsDay[i])；
              eventTracker 里 Own 的 → Cooldowns
Restore(mgr, entries, cooldowns): 先保留非我方的现存对象（保序）→ 再按 entries 逐个
              CreateEvent(key) + 覆盖 duration/totalDuration/startDay/currentDay/IsKnown；
              FutureDays < 0 进 activeEvents，否则进 futurEvents + futurEventsDay；
              最后 ActiveField.SetValue / futurEvents / futurEventsDay / eventTracker 一并写回
```

### 7-D 展示层接管（"复用原版填表，只换容器"）

`NewsScroll.TrySelect`（§4.2）：**不重写 `PopulateUI`，只把它要用的两个 private 字段换成自己的**。日历同理：**不重写 `SetElement`，只在它执行前多给几个座位**（§4.1）。两条都比"整段替换原版方法"稳，是本工作区"扩展优先、替换最后"的正面样本；唯一要补的是生命周期——`NewsScroll.Clear()` / `CalendarScroll.Clear()` 在 `OnDisable`、`BeforeLoad`、`NewGame` 三处都调。

> **对 11 号文档的两处修正**：①§1.1/§3 写"往 `threatEventBlueprints` 加蓝图就有资格被原版抽中"——这是原版**允许**的做法，但本品 1.4.0 **一个静态蓝图表都没写**，走私有列表 + 自建抽取；②§1.6 列的 `isHiddenFromNewsPaper` 等可见性成员在本品里没出现，它只用了 `isDurationVisible` 与 `eventType = COSMETIC`。照 11 号写没问题，但别以为本品是那么实现的。

## 8 坑与兼容

1. **Transpiler 是最大冲突面**。`NativeNormalCountPatch` 把 `HandleNewEventNormale` 里 **3 处** `GetEventCounter()` 换成自己的减法版，`NativeCosmeticCountPatch` 把 `HandleNewEventComestic` 里 **1 处** `List<StoreEvent>.Count`（`activeEvents.Count < 2`）换成排除我方的计数，且都 `if (count != 3) / != 1) throw`。为什么必须换：原版 `GetEventCounter() >= 3` 直接不开抽（`StoreEventManager.cs:359`）、`activeEvents.Count < 2` 是装饰新闻的循环条件（`:390`），我方事件会把这两个"世界拥挤度"顶满，**导致原版自己的事件不再生成**。
2. **两个 transpiler 补丁 = 双改冲突点**：第二个模组若也 transpile 同一方法，Harmony 依次应用时后者匹配不到目标（已被前者改写）→ 抛 `InvalidOperationException`。**本品没有任何 `[HarmonyPriority]` / `HarmonyBefore/After`**，先后看加载顺序。我们要抄的是"断言替换点数量"这个习惯，不是这两个补丁本身。
3. **谁抢 `GetActiveEvents`**：本品不 Patch 它，但 70 条事件会出现在返回值里。所以 `ArcMarket.cs` 的重排 Postfix 会连它一起排（无害），而**任何按条数/位置假设的代码都会翻车**（一天可能 5+ 条）。抄它这点：拿单条用 `GetActiveEvent(id)`、批量状态用反射读原 `activeEvents` 列表，别依赖 `GetActiveEvents()` 的顺序。
4. **报纸接管与 Better Newspapers 正面冲突**：本品 Prefix 掉 `SelectLayout(List<StoreEvent>)` 并自建容器，而《更好的报纸》是在 `PopulateUI` Postfix 里遍历 `layout1Elements..layout4Elements` 摆图（见 [16 号文档](../16-报纸事件配图与BetterNewspapers规范.md) §2）。当天事件 >4 时原版四个 layout 被 `SetActive(false)`，配图逻辑找不到容器 → **配图静默失效**。要共存，图必须挂在"当前 activeLayout 下的 activeLayoutElements"，不能写死 4 个 layout。
5. **失败姿态过于刚硬**：`PrepareSave()` 里任何异常（含 `ValidateFutureLists` 发现 `futurEvents` 与 `futurEventsDay` 长度不一致）→ 返回 false → **整次存档被跳过**，玩家只看到"没存上"。取舍明确（宁可丢一次存档也不写坏档），但排障必须看日志。
6. **`ItemSpawner.Spawn(item)` 上架贼货**（`EventContent.cs:2320`）：与本工作区"判存在不用 Spawn、发放用 `DirectoryMaster.Item`"的规范相悖。这里只用于立刻塞进柜台展示（不落玩家背包），勉强可接受，换 `DirectoryMaster.Item(id, false)` 更规范。
7. **`Eligibility` 不校验 manager 身份**（`:249`）：只要 id 是自己的就改 `__result`。别的入口（`CanStart`/`Schedule`/`CanCombine`）都做了 `ReferenceEquals`，唯独这个漏了，靠"原版只有一个 `StoreEventManager`"兜着。
8. **两个 Harmony id**：`new Harmony("local.station-events")` 打补丁，`OnDisable` 里还要 `new Harmony("mod.local.station-events").UnpatchAll("mod.local.station-events")`（`StationEventsMod.cs:134-137`）。代码没解释（推测：曾与 ModLoader 的 `"mod." + id` 实例混用，留了双保险）。抄的时候用一个 id 就好。
9. **硬编码名单**：`EventState.RetiredKeys` 17 个已废弃 key（`EventState.cs:10-14`）、`Choices` 5 档中英串、`TranslationCatalog` 290 条——删事件时必须同步改 `RetiredKeys`，否则旧档里的孤儿事件会一直占着 `Progress`。
10. **合并池会改原版出现率**：`include_vanilla_events` 打开后 `EventDiscovery.AdjustWeights` 那套"已见过减半、把减掉的权重按比例分给没见过的"**同时作用在原版候选上**（`EventDiscovery.cs:10-57`），原版事件相对权重被改写。默认关闭是对的。另有 `MarketOverlap` 的 −90 阈值（`MarketOverlap.cs:27`）：已有事件把某 tag 压到 `≤ -90` 时，再抽到同类降价事件权重减半——防价格踩踏，可复用。
11. **无 `try/catch` 滥用**：全仓只有 4 处 `catch`（`EventState.TryRead`、`PrepareSave`、`Enable` 失败回滚、日历 `Destroy` 分支），**没有空 catch**，且都会 `LogError` / `throw`。这是评审时的正面参照。

## 9 对我们的可用点

### 9.1 给 `深空电玩`（deepspace_arcade）

- **价格效果零补丁**：任何"某类货涨价/跌价 N 天"只需造一个带 `negociationDatas` 的 `StoreEvent` + `ActivateEvent`，不碰 `GameItem.GetCurrentValue`，也就不会和 `ArcMarket` 现有价格补丁打架。
- **赛事日程用"预告纸条"而不是自己排期**：`CreateAndAddSlip(timer)` + `QueueFuturEvent(ev, timer, IsKnown:true)`，纸条由原版 `BuildSlipBody` 渲染、倒计时自动走（`SLIP_DATA` + `onCycleEndSlotItemFunc`），比"报纸提前预告"更贴《深空当铺》的物理质感。
- **报纸 >4 条的场景要提前决策**：若我们也在 `PopulateUI` 上挂东西（16 号文档的配图），必须处理"layout1~4 被别人关掉"这一分支——建议我们不做 SelectLayout 接管，把配图改成跟着 `activeLayout` 的 `activeLayoutElements` 走。
- **密度系统可直接借表**：`DensityRules` 的 15 格概率表（5 档 × 3 脉冲）+ `ChoosePulse` 剔掉上一次权重，做活动出现频率够用，全整数无浮点。
- **反面教材**：不要为了"原版计数不占坑"去 transpile `GetEventCounter`；我们的事件量级（每周 1~2 条）不需要，硬上只白送冲突面。

### 9.2 给 `深空当铺：可能是群友`（freya_cornell / DeepSpacePawn）

- **"自定义顾客携带事件"的合规范本**：`SetClientSource(client, modId)` + `client.eventSourceId = modId + ":" + key` 双标记（§5.1），加 `OnArrival / onItemBought / onItemSold / onItemAddedToWeighted` 四个回调（都在 `CompleteClientCreation(false)` **之前** `Delegate.Combine`）。
- **每日访客配额闸门**（§5.2）：`capacity = max(0, 2 - Visited - 在场的未计数者)`，我们的群友来客同样需要"一天最多几个、先照顾快结束的线"这套账，照抄 `EnsureVisitorDay` + `VisitDays[]` 的结构。
- **交错入队**（§5.3）是"我们的客人别永远排最后"的唯一低成本解法；11 号文档定过型，这里是原始出处。
- **`StoreClientFactionSetup.InitBlackMarket / InitSecurity`** 等官方身份帮助类优于手写 `clientFaction` 字符串（03 号文档 §94 有同一教训，这里是工坊实证）。

### 9.3 通用（两款都用）

1. **ES3 无污染投影这份实现完成度最高**：`SaveGame` Prefix 建投影 → `ES3Type.WriteProperties` Prefix 换对象 → `Finalizer` 清引用 → 读档工厂重建 + 天数回放 + 版本迁移。比 14 号骨架多出**天数回放**与**失败即整体停摆**两块，建议回补进 14 号。
2. **紧凑状态编码**（版本头 + 段落前缀 + Base64 字符串 + 区间校验 + 行数/长度上限）比自写 JSON 更抗坏档，也不需要序列化器；别忘 `hashSet` 去重那几行（防手改存档重复行）。
3. **配置 label/desc 用 `Func<string>`**；下拉选项用 `ReferenceEquals` 认领自己的列表（§6.3），比加前缀字符串干净。
4. **最值得抄的 3 个点**：① `negociationDatas` 即价格效果——不碰价格代码就能改全品类行情；② 保存时用 `MemberwiseClone` 把自定义事件从原版管理器的存档投影里摘掉、读档再用工厂重建；③ 展示层只换容器不换逻辑——报纸换 `activeLayout/activeLayoutElements`、日历加座位再包 `ScrollRect`。

### 9.4 待验证

- `ES3Type.WriteProperties` 在 ES3 里通常是泛型基类的虚方法，本品 `[HarmonyPatch(typeof(ES3Type), "WriteProperties")]` **不指定参数类型**也命中（说明该类型只有一个同名方法）。我们照抄前应在当前游戏版本实测一次，避免 `AmbiguousMatchException`。
- 报纸接管后 `NewsUIManager.allElements` 与 `rows` 的对应（`NewsScroll.cs:249-253` 把自己 new 的 element 塞进 `owner.allElements`）在**反复开关报纸 UI** 时是否重复累积——`Create` 前置判 `owner != manager || root == null` 看起来安全，未真机验证。
- `Live` 把 `EmporiumEntry.Instance != null` 作为必要条件（`EventRuntime.cs:46-56`），意味着**不在店里时整套拓展不工作**。这是刻意还是保守写法未验证（推测：避开 UI 未初始化时的空引用）。
