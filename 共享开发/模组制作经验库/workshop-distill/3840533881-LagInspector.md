# Lag Inspector（卡顿纠察）蒸馏档案

- 工坊 id：`3840533881`　命名空间：`LagInspector`　程序集：`LagInspector.dll`（`net40`，仅引用 `Assembly-CSharp` / `0Harmony` / UnityEngine 三件）
- 反编译源码：`缓存文件夹\decomp_ws\3840533881`（55 个 `.cs`，约 3.9k 行；目录内只有 `.csproj`，**manifest 未随包提供**，能力声明无法核对）
- 实质：**一个装在 38 个 Harmony 补丁上的"运行时性能采样器"**——18 个补丁给 17 类玩家操作计时，20 个补丁给"常见子方法"做耗时探针；归因完全靠 `Harmony.GetPatchInfo(method).Owners` 反查"这个被计时的方法上挂了谁的补丁"
- 本文结论全部来自上面这份源码；凡属"代码里看不到、只能推断"的，标**（推测）**

---

## 0. 一句话本质 + 文件地图

**它不问"谁慢"，它问"这个慢方法身上压着谁的补丁"。**

计时本体只是一对 `Stopwatch.GetTimestamp()`（`Begin`/`End`），没有任何 IL 改写、没有 `Finalizer` 级别的拦截器、也不 hook Unity 帧循环。真正的"智商"在 `SourceResolver`：

```text
被计时方法 ──Harmony.GetPatchInfo(method).Owners──> patch.owner（Harmony ID 字符串）
                                                     │  剥 "mod." 前缀
                                                     ▼
                                    ModLoader.Mods[i].Manifest.ID  /  .HarmonyInstance.Id
                                                     │  命中 → 显示 Manifest.Name
                                                     │  未命中 → 原样显示 owner 字符串
```

| 文件 | 行数 | 作用 |
| --- | --- | --- |
| `OperationProfiler.cs` | 701 | **核心**：帧栈、阈值/冷却、探针栈、细分残差、历史环形表、派发 |
| `SourceResolver.cs` | 329 | **归因核心**：`GetPatchInfo` → owner → modId → 显示名，四级证据合流 |
| `Config.cs` | 323 | 13 个 `ConfigEntry`、范围钳制、旧值迁移（`弹窗 + 日志` → 双语选项） |
| `RelatedMethods.cs` | 187 | 17 类操作各自的"常见子方法"白名单（`AccessTools.Method` 静态表 + 缓存） |
| `SlowEvent.cs` / `TimeSlice.cs` / `SourceTag.cs` / `NotifyMode.cs` | 156/16/12/9 | 事件模型、时间片、来源标签、提示模式枚举 |
| `NotificationUI.cs` / `HistoryUI.cs` / `LogUI.cs` | 144/109/203 | 三个 `CustomUIBuilder` 窗口：弹窗 / 记录 / 日志文件 |
| `DragClassifier.cs` | 203 | `EndDrag` 的搬运语义分类（仓库/前柜台/后柜台/展示柜/垃圾桶/同容器换格…） |
| `L10n.cs` / `Ops.cs` / `PatchUtil.cs` / `ModEntry.cs` | 80/63/39/61 | 双语、17 个操作常量与英文标签、参数取 id、`IMod` 入口 |
| 18 个 `*Patch.cs` / 20 个 `Probe_*.cs` | 28–94 / 28–33 | 计时入口（17 类操作，`SaveGame` 与 `InitialSave` 共用一名）/ 细分探针（结构完全同构，只换 `[HarmonyPatch]` 目标） |

---

## 1. 计时骨架

### 1.1 三件套，不是成对 Stopwatch

每个计时入口都是**同一个模子**（`BuyItemPatch.cs:10-39`）：`Prefix` 抢时间戳、`Postfix` 收口、`Finalizer` 兜异常：

| 成员 | 优先级 | 干什么 |
| --- | --- | --- |
| `Prefix(object[] __args, out long __state)` | `[HarmonyPriority(800)]` | 从 `__args` 抠 `detail`，`__state = OperationProfiler.Begin(op, detail)` |
| `Postfix(long __state, MethodBase __originalMethod)` | `[HarmonyPriority(0)]` | `OperationProfiler.End(__state, __originalMethod)` |
| `Finalizer(long __state, MethodBase __originalMethod, Exception __exception)` | `[HarmonyPriority(800)]` | 同样 `End(...)`，再把 `__exception` 原样返回 |

三个要点（都是代码事实，抄的时候别漏）：

1. **`End` 是幂等的**：`Take(id)` 把帧从栈里摘掉后 `frame.Finished = true`；`End` 开头 `if (frame == null || frame.Finished) return;`（`OperationProfiler.cs:158-163`）。所以"Postfix 收一次 + Finalizer 再收一次"不会重复记账——这是它敢写双出口的唯一原因。
2. **`__state` 为 0 表示"这次不计"**：`Begin` 在 `Config.Enabled == false` 时返回 `0L`；`End(0)` 立刻返回。`EndDragPatch` 还额外用 `__state = 0L` 主动跳过（`EndDragPatch.cs:66-70`，第一个 `bool` 参数为 true 时不计时，**（推测）**是"内部再入/取消"标记）。
3. **异常也一定收口**：`Finalizer` 在所有 postfix 之后跑，且返回值原样透传——它不吞异常，只借道收时间。这跟工作区"边界捕获必须留痕"的规则方向相反：这里**故意不捕获**，因为它是旁观者。

### 1.2 帧栈：嵌套靠"按 id 摘帧"，不靠父子指针

`[ThreadStatic] private static List<Frame> threadFrames`（`OperationProfiler.cs:60-99`）。`Frame` 自带 `Depth = frames.Count`（进栈前的层数）、`Id`（`Interlocked.Increment`）、`Start`、`Slices`、`ChildFrameMs`、`OwnProbeMs`。

- `Begin`：`frames.Add(frame)`，返回 `Id`。
- `End`：`Take(id)` 从**栈顶往下**找同 id 的帧并 `RemoveAt`；然后 `Peek()` 取剩下的栈顶当**父帧**，`NoteChild(parent, child, elapsedMs, originalMethod)`。
- 因此 `批量购买` 里的 N 次 `购买物品` 天然形成 N 个子帧，全部回灌给父帧（`ChildFrameMs`、`SlowestChildMs`、`Slices`）。

### 1.3 "不重复累计"落在四个地方

| 层 | 规则 | 位置 |
| --- | --- | --- |
| 弹窗 | 默认只报最外层：`if (depth > 0 && !Config.NotifyNested) return false;`（`NotifyNested` 默认 `false`） | `OperationProfiler.cs:611-620` |
| 历史表 | **子操作仍单独入表**（带上 `Depth`，记录窗口用 `·` 标记嵌套）；只被阈值门挡，不被深度门挡 | `356-360`、`SlowEvent.ToShortLine` |
| 细分 | 父帧只算**自己那份残差**，子帧的 `Slices` 用 `MergeSlices` 合并上来；同 key 只累加一次 | `395-426`、`572-609` |
| 探针 | 每层探针先减掉自己的子探针：`ChildMs += 儿子 elapsed`，只把**净自耗时**记成 slice | `263-302` |

### 1.4 阈值、冷却、强制记录

- 阈值：`Config.ThresholdMs` = `Clamp(值, 50, 10000)`，默认 **500**（`Config.cs:57`）。`End` 里 `if (num < threshold && !forced) return;`——**低于阈值的事件根本不会走归因**，所以昂贵的 `StackTrace` 抓取天然只发生在慢操作上。
- 冷却：`Dictionary<string,long> LastNotify` 以**操作名**为键（`购买物品` 与 `批量购买` 各冷却各的），用 `Stopwatch` 时间戳算差；`CooldownSeconds` 默认 **15**、`0` 表示每次都提示（`611-632`）。注意：深度门在冷却记账**之前**直接 return，所以嵌套子操作既不会弹窗也不会污染冷却表。
- 强制记录：`Config.RecordAllTransfers`（默认 false）打开后，`物品转移` 无论多快都入表，`Forced = (num < threshold)`，且 `Forced` 事件**永不弹窗**，日志行尾加 `未超阈值=是`。
- 原版过滤：`if (list.Count != 0 || Config.WatchVanilla || forced)`——`watch_vanilla`（默认 true）关掉后，"一条模组证据都查不到"的慢操作不记录。
- 历史容量：`history_size` 默认 60（钳 10–500），`TrimHistory` 再钳到 ≤1000，`RemoveAt(0)` 丢最老。**只存内存，不落盘、不跨会话**。
- 主线程卡死留痕：`Config.StallCrumb`（默认 true）时，每次 `Begin` 调 `StallWatchdog.Crumb("卡顿纠察: 操作名 detail")`（`127-141`，整体 try/catch）。真正卡 5 秒时，游戏自己的 GameLog 里会留下"当时正在做什么"。

---

## 2. "疑似来源模组"归因算法（本文重点）

### 2.1 数据源：只用 `Harmony.GetPatchInfo(method)`，**没有** `GetAllPatchedMethods`

全目录 grep：`GetAllPatchedMethods` **零命中**。唯一的 Harmony 反查点是 `SourceResolver.DirectInternal`（`SourceResolver.cs:40-73`）：

- `Harmony.GetPatchInfo(method)` → `Patches patchInfo` → 遍历 `patchInfo.Owners`（**每个** owner 字符串只出现一次，它是该被补丁方法上所有 prefix/postfix/transpiler/finalizer 的 owner 并集，**不含"谁打了几个补丁""哪个补丁慢"的信息**）。
- 每个 owner 都是一条 `SourceTag { ModId, ModName, Reason = "补丁 类型.方法名" }`。
- 不自己维护注册表、不读写 `HarmonyInstance` 的补丁表、不缓存 `MethodInfo` 到 owner 的反向索引——**每次现查，查完缓存**。

### 2.2 owner → modId → 显示名（三层映射，逐层退化）

```text
patch.owner ──① 剥 "mod." 前缀──> 裸 id ──② IsSelf? 丢弃
                                          └──> ③ NameOf(id)
                                                    ├─ ModLoader.Mods[i].Manifest.ID == id (忽略大小写) → Manifest.Name
                                                    ├─ ModLoader.Mods[i].HarmonyInstance.Id == id (Ordinal) → Manifest.Name
                                                    └─ 都没命中 → 直接返回 id 本身
```

- ①`AddOwner`（`166-180`）：`if (text.StartsWith("mod.", OrdinalIgnoreCase)) text = text.Substring(4);`
- ②`IsSelf`（`203-214`）：等于自身 modId 或以 `自身modId + "."` 开头的一律丢弃——**它不会把自己报成嫌疑人**。
- ③`NameOf`（`274-328`）：在 `ModLoader.Mods` 里按 `Manifest.ID` 忽略大小写匹配，或按 `LoadedMod.HarmonyInstance.Id` 精确匹配；两路都失手就 `return modId`（**原样显示 owner 字符串**），并且**不写缓存**（下次重扫）。
- 去重：`Add`（`182-201`）按 `ModId` 忽略大小写去重，**先到的 reason 赢**——所以"直接补丁"作为第一路证据，会盖掉后面"子操作/相关方法/调用方"的原因。

> **重要更正**：摘要常说"映射不上就退化到调用栈"。源码不是这样——**映射失败只退化成"原样显示 owner 字符串"**；调用栈是第四条**加分证据**（`Config.CaptureStack`），不是兜底路径。

### 2.3 四路证据合流（`Collect`，`75-107`）

| 证据 | 来源 | reason 文案 | 触发条件 |
| --- | --- | --- | --- |
| 直接补丁 | `Direct(被计时方法)` | `补丁 PlayerStore.BuyItem` | 永远 |
| 子操作补丁 | `frame.ChildTags`（子帧 `End` 时写入，上限 8 条） | `子操作 整理库存` | `check_related` 且子操作 ≥5ms |
| 相关方法补丁 | `RelatedMethods.For(operation)` 白名单里的每个方法各自 `Direct()` | `相关方法 GameItem.GetNegociatedValue` | `check_related`（默认 true） |
| 调用栈 | `new StackTrace(1, false)` 逐帧 → `ModLoader.ModIdOfAssembly(assembly)` | `调用方` | `capture_stack`（默认 true），且**只在确认慢之后**抓 |

渲染（`Format`，`109-150`）：最多 3 条，形如 `库存大修[pawns.vault]（补丁 PlayerStore.BuyItem）；某模组（子操作 整理库存）；…等 5 项证据`；`ModName` 与 `ModId` 不同才追加 `[id]`；一条都没有则输出 `原版（游戏本体）`。

### 2.4 四个必须知道的缺陷/取舍

1. **粒度到"方法"为止，不到"补丁"**。两个模组同时 prefix 住 `PlayerStore.BuyItem`，只会得到两条 tag，**没法说哪个更慢**；细分里它们被合并成 `mods:A+B` 一个切片。
2. **`Owners` 不含 transpiler/finalizer 的细节，也不含 null-owner**。owner 为空字符串的补丁（自建 Harmony 实例未命名）在 `AddOwner` 里被直接跳过 → 归因静默丢失。
3. **缓存不一致是有意设计**：`Direct()`（归因用）**不缓存空结果**，晚加载的补丁还能被看见；`DirectForProbe()`（细分用）`cacheEmpty: true` **会缓存空结果**——细分切片一旦把某方法记成"原版代码"，**之后永不更新**。热重载/后置补丁场景下这是真坑。
4. **`capture_stack` 抓的是"谁调用了我"，不是"谁变慢了我"**。一个模组只是触发批量购买，也会被标成 `调用方`——误报概率不低，所以它是最后一档、且只做"证据"之一。

---

## 3. 耗时细分（breakdown）

### 3.1 两个池子：操作帧 + 探针

- 20 个 `Probe_*` 补丁把 `GameItem.GetNegociatedValue`、`SlotMarker.AcceptUnchecked`、`GameItemElement.Validate` 等热方法包起来，`ProbeBegin(method)` / `ProbeEnd(id)`。
- `ProbeBegin` 的前置门：`Config.Breakdown` 为真、`method != null`、**当前线程已有帧**（`threadFrames.Count > 0`）、`probeDepth < probeStack.Length`。四条任一不满足 → 返回 0，几乎零开销。
- 探针栈是 `[ThreadStatic] ProbeEntry[64]`，`ProbeEnd` **只在栈顶 id 匹配时**才弹栈（`276-297`）；不匹配就放弃（帧泄漏由 64 上限硬截断）。

### 3.2 残差公式（为什么会有"原版/未细分"）

```text
子帧时间   C = Σ(直接子帧 elapsed)
探针净时间 P = Σ(本帧内探针 net)          // 每个探针 net = elapsed - 自己的 ChildMs
本帧自耗时 S = elapsed - C - P            // 只保留 ≥0，且 ≤0.5ms 直接丢弃
```

- 若 `S > 0.5ms` 且**被计时方法身上没有任何模组补丁** → 记 `unaccounted:vanilla`，标签 `原版/未细分`（`448-453`）。含义：这段时间是原版实现自己烧掉的（或者探针没覆盖到的原版子调用）。
- 若被计时方法**被模组补丁包着** → 记 `unaccounted:mod1+mod2`，标签 `未细分（外层被 A + B 修改）`（`454-466`）。含义：这段残余**无法再切**——因为 `Harmony` 不告诉你 prefix 和 postfix 各占多少，只能整体归给"外层那几个模组"。
- 探针切片本身的归属由 `SourceOf(method)` 决定（`504-544`）：0 个 owner → `vanilla` / `原版代码`；1 个 → `mod:<id>` / 显示名；多个 → `mods:<a>+<b>`（**id 排序后拼接**，保证同 key）。
- 收尾：`BuildBreakdown` 滤掉 <0.5ms 的片、按耗时降序、若总和比 elapsed 少 >0.5ms 补一个 `other` / `其他`；`|总和 - elapsed| > max(20ms, 10%)` 时置 `BreakdownApproximate`，UI/日志显示"近似"并注明"细分存在嵌套/重叠，百分比为近似值"。

### 3.3 嵌套怎么处理

- 子帧自己的 `FinalizeSlices` 在**它自己 `End` 时**就算完（此时它的子帧已合并进它），随后 `MergeSlices(parent.Slices, child.Slices)` 把它整包并到父帧——**一份时间只在一个最内层被切成片，然后向上合并**，不会重复。
- 已知两处会破坏这个不变量的情形（都是代码事实，不是猜）：①子帧没走到 `End`（例如只打了 prefix 而原方法被别的补丁跳过），父帧 `ChildFrameMs` 少算，残余被错误归给"原版/未细分"；②`End` 传入的 id 不是栈顶（乱序收口）时 `Peek()` 拿到的"父帧"其实是最内层仍在跑的帧，`NoteChild` 会挂错人。

### 3.4 细分开关的真实代价

`Config.Breakdown` 默认 **true**。关掉后：`ProbeBegin` 直接返回 0，`FinalizeSlices`/`BuildBreakdown` 全早退——但 **20 个探针补丁依旧挂在原版方法上**，每调用一次仍有 Harmony 前缀/后缀/终结器的跳转成本。想彻底零侵入只能不装这个模组。

## 4. 操作分类清单（17 类操作 ↔ 原版方法）

> 源码实况：**38 个 `[HarmonyPatch]` 类 = 18 个计时入口 + 20 个探针**，映射到 **17 类操作**（`SaveGame` 与 `InitialSave` 共用"保存游戏"）。任务简报上的"33 个 Patch"与源码不符（**（推测）**是别的版本，或只数了计时入口 + 部分探针）。

### 4.1 计时入口（18 → 17）

| 操作（`Ops` 常量） | 拦截的原版方法 | detail 取自 | 文件 |
| --- | --- | --- | --- |
| 购买物品 | `PlayerStore.BuyItem` | `__args[0] as GameItem` → `identifier` | `BuyItemPatch.cs` |
| 批量购买 | `NegociationUIManager.BuyMultipleItem` | `__args[0] as List<GameItem>` → `"N 件"` | `BuyMultiplePatch.cs` |
| 整批买下 | `PlayerStore.BuyAllItemFromTable` | `EmporiumEntry.Instance.backInvinvElement.childItems.Count` | `BuyAllPatch.cs` |
| 批发购买 | `NegociationUIManager.OnWholesaleClick` | 无 | `WholesalePatch.cs` |
| 出售物品 | `PlayerStore.SellItem` | `__args[0] as GameItem` → id | `SellItemPatch.cs` |
| 批量出售 | `NegociationUIManager.SellMultiple` | `List<GameItem>` 计数 | `SellMultiplePatch.cs` |
| 整批卖出 | `PlayerStore.SellAllItemFromTable` | 无 | `SellAllPatch.cs` |
| 物品转移 | `ItemMouseDragHandler.EndDrag`（`bool` 参数为 true 时跳过） | `DragClassifier.Describe(...)`：`仓库/前柜台/后柜台/展示柜/垃圾桶/外勤背包/隐藏库存/已售区` + 容器 id + `→` 方向 | `EndDragPatch.cs`（94 行，唯一带反射字段的） |
| 液体转移 | `LiquidContainerHelper.TransferLiquid` | `源id → 目标id` | `TransferLiquidPatch.cs` |
| 跳过客户 | `PlayerStore.TryPlayerDismissCurrentClient` | `__args[0] as bool` → `并呼叫下一位 / 不呼叫下一位` | `SkipClientPatch.cs` |
| 呼叫下一位 | `PlayerStore.TryCallNextClient` | 无 | `CallNextPatch.cs` |
| 开店 | `PlayerStore.OpenShutter` | 无 | `OpenShutterPatch.cs` |
| 结束一天 | `PlayerStore.EndDay` | 无 | `EndDayPatch.cs` |
| 保存游戏 | `PlayerStore.SaveGame` **+** `PlayerStore.InitialSave` | 无 | `SaveGamePatch.cs` / `InitialSavePatch.cs` |
| 生成顾客 | `StoreClientManager.GenerateClient` | 无 | `GenerateClientPatch.cs` |
| 整理库存 | `InventorySortHelper.Sort` | `__args[0] as GameInventory` → `identifier` | `SortInventoryPatch.cs` |
| 接受以物易物 | `PlayerStore.AcceptBarter` | 无 | `BarterPatch.cs` |

`detail` 一律 `TrimDetail` 截断到 48 字符 + `…`；取 `detail` 的整段都套 try/catch（拿不到就 `null`，绝不影响计时）。

### 4.2 细分探针（20 个，全部无参取 `__originalMethod`）

| 探针目标 | 服务的操作（`RelatedMethods` 白名单） |
| --- | --- |
| `GameItem.GetNegociatedValue` | 买 / 批买 / 整批买 / 批卖 / 整批卖 / 以物易物 |
| `PlayerStore.OnItemBought(GameItem,int)` | 买 / 批买 / 整批买 / 批发 |
| `PlayerStore.MoveDealItemToInventory(GameItem)` | 买 / 批买 / 整批买 / 批发 |
| `GraphUtils.TryAcceptAll(GraphNodeStorage,GameItem,int)` | 买系 / 物品转移 |
| `RunStats.OnItemBought(GameItem,int,StoreClient)` | 买系 |
| `GeneralHelper.TrackBuyPrice(GameItem,int)` | 买系 |
| `StoreReputation.OnItemTraded(string,int,int,StoreClient,bool,GameItem)` | 卖系 |
| `SlotMarker.AcceptUnchecked()` / `SlotMarker.TryAcceptOnce(int)` | 物品转移 |
| `GameItemElement.Validate()` / `GameGridInventory.Validate()` | 物品转移 / 整理库存 |
| `LiquidContainerHelper.AddLiquid(GameItem,int)` / `RemoveVolume(GameItem,int)` | 液体转移 |
| `GameItem.ModifyTag(string,Action<TagState>,bool)` | 液体转移 |
| `PlayerStore.DismissCurrentClient(bool,bool,bool)` | 跳过客户 |
| `StoreClientManager.DismissClientStack()` | 跳过客户 |
| `StoreClientManager.PickClient()` / `ClientGenCheck()` | 生成顾客 |
| `PlayerStore.UpdateServiceCost()` / `RemoveNotOwnedItemFromTable()` | 白名单未引用；但探针本身仍挂在原版方法上，任何被计时帧里调用它都会进 slice |

两个细节：①20 个探针里只有 5 个写了显式类型表（`DismissCurrentClient` / `TryAcceptAll` / `ModifyTag` / `RunStats.OnItemBought` / `StoreReputation.OnItemTraded`），其余 15 个用 `[HarmonyPatch(typeof(X), "名字")]` **裸名字**——能跑通说明那些方法在原版里没有重载；抄的时候一律补类型表（工作区硬规范，否则 `AmbiguousMatchException` 会中断整批补丁）。②探针全都不看参数，`ProbeBegin(__originalMethod)` 只把 `MethodBase` 当缓存键。

### 4.3 `RelatedMethods` 里"只查补丁、不计时"的原版方法（额外情报）

`PlayerStore.GetWholesalePrice()`、`EmporiumEntry.SwapInv()`、`PlayerStore.CanSellThisItem(GameItem,bool)`、`PlayerStore.PerformOnSoldCheck(GameItem,StoreClient)`、`GameItem.TryFindOneValidInventorySlot(GameItem)`、`StoreClientManager.HandleSkipClient()`、`PlayerStore.GetNextClient()`、`PlayerStore.BeginDay()`、`PlayerStore.OnShutterOpened()`、`PlayerStore.EndNight()`、`StoreClientManager.OnNewDay()`、`PlayerStore.WriteSlotFile()`。

这些名字全部来自 `AccessTools.Method(...)` 的**字符串查找**，`Add()` 遇到 null 直接跳过——**任何一个名字写错都不会报错，只是那条归因静默失效**。抄这套表时要从宿主 `Assembly-CSharp` 反编译核实。

---

## 5. UI 与日志

### 5.1 弹窗（`NotificationUI.cs`）

`CustomUIManager.Window("lag_inspector.alert", 标题, "overlay").SetSize(720, 有细分?540:340).Center().SetDraggable(true)`，随后 `AddWarning(耗时/阈值) → AddLabel(操作/来源/位置/最慢子操作) → AddSeparator → AddLabel("耗时细分") → 每片一行 → BeginRow/AddButton(查看全部记录 / 打开日志文件 / 知道了)/AddFlexibleSpace/End → Show()`。

- 三级降级：没有 `CustomUIManager.Instance` → `Fallback` 只写日志；构建抛异常 → `Warn` + `Fallback`；`RunOnMainThread` 抛异常 → 直接 `Fallback`。
- 自动关闭用 `ModHelper.Delay(modId, seconds, () => Close())`，`popup_seconds = 0` 表示不自动关（等玩家点"知道了"）。
- **过期抑制**：`Interlocked.Increment(ref generation)` 拿到 `current`，`Build` 与 `Delay` 的回调都先比对 `current == generation`——弹窗连发时，只有最新那次会真正建窗/关窗，避免"上一条的定时器关掉这一条"。

### 5.2 记录窗口（`HistoryUI.cs`）

`Window("lag_inspector.history", …).SetSize(780,520)`，`BeginScrollFlexible(390)` 内**倒序**列出 `Snapshot()`：每条两行（`HH:mm:ss · 操作（detail） 123 ms @PlayerStore.BuyItem` + `来源：…　最慢子操作：…`），第三行是 `细分：…`（最多 3 片）。按钮：`清空记录`（`ClearHistory` + `RunOnMainThread(Show)` 重开）/ `打开日志文件` / `关闭`。

### 5.3 日志文件（`LogUI.cs`）——它**不是**实时日志

- 路径：`ModHelper.GetModDataFolder(modId)` + `\LagInspector.log`；拿不到就退回裸文件名（`GetFilePath`，`155-174`）。
- 写入时机：**只有打开日志窗口 / 点"刷新" / 点"复制路径"时**才 `ModHelper.WriteModDataFileText(modId, "LagInspector.log", text)` 整文件覆盖（`Reload`，`26-52`）。跑图期间文件内容不更新——看到的是**打开窗口那一刻的快照**。
- 写完之后立刻 `ReadModDataFileText(modId, "LagInspector.log")` 把文件内容读回来显示（读得到就用读到的，读不到就显示内存里那份）——一个小闭环，用来确认"真的落盘了"。
- 格式（`BuildText`，`176-202`）：4 行 `#` 头（`# 卡顿纠察日志` / `# 生成时间：yyyy-MM-dd HH:mm:ss` / `# 阈值：500 ms` / `# 记录条数：N`）+ 空行 + 每条 `[i] 慢操作 …` 一行 + 缩进 4 空格的细分行（最多 6 片）+ 空行。
- "打开日志文件"的真相：**它不调系统外壳**。窗口里明写"游戏禁止模组调用系统外壳打开文件；可点'复制路径'后手动打开"；按钮"复制路径"用 `GUIUtility.systemCopyBuffer = 路径`，然后 `DeferReload` 把整窗重建成"路径已复制到剪贴板"。
- 另一路日志才是实时的：`Log.Warning(slow.ToLogLine())` → `ModLog` → F8 控制台 + `Player.log`（仅 `notify_mode` 为 `Log`/`Both` 时）。`ToLogLine` 的字段顺序：`慢操作 {Title} 耗时 {X} ms（阈值 {Y} ms） 来源=… 位置=… [最慢子操作=…] [未超阈值=是] 细分=片1 12ms(34%) · 片2 …`（`SlowEvent.cs:48-104`）。

### 5.4 用到的 ModHelper / 宿主 API 清单

| API | 用途 | 位置 |
| --- | --- | --- |
| `ModHelper.GetConfig(id)` → `ModConfig.Define(...)` | 13 个设置项；`enabled / threshold_ms / notify_mode / cooldown_seconds / popup_seconds / notify_nested / watch_vanilla / check_related / breakdown / record_all_transfers / history_size / stall_crumb / capture_stack` | `Config.cs:229-259` |
| `ModHelper.AddDebugFunction(id, "Modding", 标签, Action, false)` | 两个调试按钮：打印最近慢操作 / 打开记录窗口；`OnDisable` 里 `RemoveDebugFunctions(id)` | `ModEntry.cs:32-33,48` |
| `ModHelper.RunOnMainThread(id, Action)` | 弹窗、清空后重建、刷新/复制路径的跨线程转换 | `NotificationUI.cs:30`、`HistoryUI.cs:81`、`LogUI.cs:129` |
| `ModHelper.Delay(id, float 秒, Action)` | 弹窗自动关闭 | `NotificationUI.cs:50` |
| `ModHelper.GetModDataFolder` / `WriteModDataFileText` / `ReadModDataFileText` | 日志文件路径、写、回读 | `LogUI.cs:33,42,159` |
| `CustomUIManager.Instance` / `.Window(...).…Show()` / `.IsOpen` / `.CloseWindow` | 三个窗口 | 三个 UI 文件 |
| `ModLoader.Mods[i].Manifest.ID/.Name`、`.HarmonyInstance.Id`；`ModLoader.ModIdOfAssembly(Assembly)` | owner → 模组显示名；调用栈帧 → modId | `SourceResolver.cs:289-315` / `260` |
| `StallWatchdog.Crumb(string)` | 主线程卡死前的"面包屑" | `OperationProfiler.cs:136` |
| `GUIUtility.systemCopyBuffer` | 复制日志路径 | `LogUI.cs:115` |
| `ModHook.OnGameLoadedNormal` | 唯一订阅（读档后打一行日志） | `ModEntry.cs:29` |

**没有** `HotReloadPrepare/HotReloadActivate`；热重载靠 `OnDisable/OnEnable` 整对重来（`OnEnable` 重新订阅 Hook + 重新注册调试按钮）。所有静态状态（`History`/`LastNotify`/`DirectCache`/`ProbeSources`/`threadFrames`）**都不在读档时清空**——它认为采样数据跨读档有效。

---

## 6. 可复用模式（关键代码原样）

### 6.1 范式 A：三件套低成本计时器（`BuyItemPatch.cs:10-39`）

```csharp
	[HarmonyPriority(800)]
	private static void Prefix(object[] __args, out long __state)
	{
		string detail = null;
		try
		{
			if (__args != null && __args.Length != 0)
			{
				object obj = __args[0];
				detail = PatchUtil.Id((GameItem)((obj is GameItem) ? obj : null));
			}
		}
		catch
		{
		}
		__state = OperationProfiler.Begin("购买物品", detail);
	}

	[HarmonyPriority(0)]
	private static void Postfix(long __state, MethodBase __originalMethod)
	{
		OperationProfiler.End(__state, __originalMethod);
	}

	[HarmonyPriority(800)]
	private static Exception Finalizer(long __state, MethodBase __originalMethod, Exception __exception)
	{
		OperationProfiler.End(__state, __originalMethod);
		return __exception;
	}
```

抄的时候注意：`__state` 用 `long` 而不是 `Stopwatch`（跨补丁传值，值类型最省）；`detail` 整段 try/catch 包住且失败置 null；`End` 必须幂等，否则 Postfix + Finalizer 双收会重复记账。

### 6.2 范式 B：Harmony 补丁归属映射（`SourceResolver.cs:46-72`）

```csharp
		lock (DirectGate)
		{
			if (DirectCache.TryGetValue(method, out var value))
			{
				return value;
			}
			List<SourceTag> list = new List<SourceTag>();
			try
			{
				Patches patchInfo = Harmony.GetPatchInfo(method);
				if (patchInfo != null && patchInfo.Owners != null)
				{
					for (int i = 0; i < patchInfo.Owners.Count; i++)
					{
						AddOwner(list, patchInfo.Owners[i], L10n.F("补丁 {0}", "patch {0}", MethodLabel(method)));
					}
				}
			}
			catch
			{
			}
			if ((list.Count > 0) | cacheEmpty)
			{
				DirectCache[method] = list;
			}
			return list;
		}
```

配套的"剥前缀 + 剔自己"（`166-180`）：

```csharp
	private static void AddOwner(List<SourceTag> list, string owner, string reason)
	{
		if (!string.IsNullOrEmpty(owner))
		{
			string text = owner;
			if (text.StartsWith("mod.", StringComparison.OrdinalIgnoreCase))
			{
				text = text.Substring(4);
			}
			if (!IsSelf(text))
			{
				Add(list, text, reason);
			}
		}
	}
```

这是整套归因的**全部秘密**：`Harmony.GetPatchInfo` + 剥 `mod.` + 剔自己 + 缓存（`cacheEmpty` 决定要不要把"没有补丁"也缓存下来）。

### 6.3 范式 C：帧残差与细分归属（`OperationProfiler.cs:428-453`，后半段见 `454-466`）

```csharp
	private static void FinalizeSlices(Frame frame, double elapsedMs, MethodBase originalMethod)
	{
		if (!Config.Breakdown)
		{
			return;
		}
		double num = elapsedMs - frame.ChildFrameMs;
		if (num < 0.0)
		{
			num = 0.0;
		}
		double num2 = num - frame.OwnProbeMs;
		if (num2 < 0.0)
		{
			num2 = 0.0;
		}
		if (num2 <= 0.5)
		{
			return;
		}
		List<SourceTag> list = SourceResolver.DirectForProbe(originalMethod);
		if (list.Count == 0)
		{
			AddSlice(frame, "unaccounted:vanilla", L10n.T("原版/未细分", "vanilla/unaccounted"), num2, 1, null);
			return;
		}
```

一句话记住：**父帧只留"扣掉子帧和探针之后的那点残余"**；残余归属由"外层方法身上有没有模组补丁"决定，有补丁就整体打包成 `unaccounted:mods:A+B`（因为 Harmony 不会告诉你 prefix 占多少）。

### 6.4 范式 D：跨帧弹窗 + 过期抑制（`NotificationUI.cs:21-41`）

```csharp
	public static void Show(SlowEvent slow)
	{
		if (slow == null)
		{
			return;
		}
		int current = Interlocked.Increment(ref generation);
		try
		{
			ModHelper.RunOnMainThread(modId, (Action)(() =>
			{
				if (current == generation)
				{
					Build(slow);
				}
			}));
		}
		catch
		{
			Fallback(slow);
			return;
		}
		int popupSeconds = Config.PopupSeconds;
		if (popupSeconds <= 0)
		{
			return;
		}
```

`generation` 一个字段解决了三个问题：跨线程派发的顺序、定时关闭的竞态、"后到的旧事件不要覆盖新事件"。任何"任何时刻只该有一个 X 窗口"的模组场景都可以照搬。

---

## 7. 对我们的可用点

**先分清"能用"和"能发"**：整套里唯一可能触碰发布红线的只有 `ModHelper.WriteModDataFileText`（写 ModData 文件，**（推测）**需要 `files` 能力）。其余全是 `0Harmony` + 公开 `ModHelper` + 公开原版类型，天生零能力——所以我们完全可以把"归因 + 日志行"这一半搬进正式版，只把"文件导出/复制路径"留在开发版。

### 7.1 归因思路的直接用法（两个模组都适用）

1. **"慢在谁身上"自查器**：给自家最贵的链路挂 `Harmony.GetPatchInfo` 反查，输出一行 `PlayerStore.BuyItem 被 [本模组, 某共存模组] 同时补丁`。我们和 Better Newspapers / 鉴定助手 / Portrait Studio 天然共存（电玩已实测共存），出卡顿时能立刻分清"是我们慢了"还是"别人压在同一条链上"。
2. **`RelatedMethods` 那张表其实是原版调用链地图**（第 4.3 节）：买 → `OnItemBought` → `MoveDealItemToInventory` → `RunStats.OnItemBought` → `TrackBuyPrice`；卖 → `CanSellThisItem` → `PerformOnSoldCheck` → `StoreReputation.OnItemTraded`。写新玩法要"在哪一步插钩"时，直接照这张表挑挂点，比盲猜方法名靠谱。

### 7.2 《深空电玩》（`deepspace_arcade`，45 源文件）

- 最值得装探针的三处：①刻录机过夜循环（`onCycleEnd*SlotItemFunc` 三段回调里我们自己的分支）——用 `Begin/End` 包住"逐件实交与刻录"整段，看 8 列雪碧图动画启停是否把帧吃掉了；②`NegociationUIManager.BuyMultipleItem`（我们加了 11 位来客与禁收逻辑，批量交易链最容易慢）；③报纸/周选生成（`StoreEventManager` 事件 + 配图）。
- 具体动作：把 `OperationProfiler` 精简成"帧栈 + `GetPatchInfo` 归因 + `ModLog.Warning` 一行"，不搬三个窗口，用 `ModHelper.AddDebugFunction(id, "Modding", …)` 挂一个手动 dump 按钮——正式版零能力、可发布。
- 反面清单：**别**照搬 20 个探针。我们不需要对 `GameItem.GetNegociatedValue` 这种每件商品都跑的热方法常驻打补丁，只在怀疑某条链时临时加一个探针、验完删掉。

### 7.3 《深空当铺：可能是群友》（`freya_cornell` / `DeepSpacePawn.dll`，22–25 源文件）

- 我们已经有 `DspHost` 统一守卫（判 `HasInstance`、`IsStoreReady`），非常适合在**守卫边界**上插计时：`DspHost.DismissCurrentClient()` 一个入口就能覆盖全部强制离场路径——这正是 LagInspector 用 `DismissCurrentClient(bool,bool,bool)` 干的事，而且它已经踩过"重载必须写参数类型表"这个坑（`Probe_DismissCurrentClient` 就是显式 `new Type[]{bool,bool,bool}`，照抄）。
- 立绘那条重活（`EnsureCrispPortraitTexture`：`GetPixels32(0)` 重建贴图 + 关 mipmap）是**已知卡顿源**。用 6.1 的三件套把 `SpriteDict` 的 `GetSprite` 前缀包一下，就能知道"换天/读档后立绘自愈"到底花了多少毫秒——比在 `Player.log` 里数时间戳准得多。
- 交易链（`StoreClient.IsClientBuyingThisItem` 顺序判定 + 塞壬水质特判）是第二个候选：我们改的是判定顺序，一旦有人再加一层前缀，很容易变成 O(n²)。做法：只在 `BuyItem/SellItem` 上包计时，把 `breakdown` 关掉（省掉探针常驻开销），只留"总耗时 + 谁的补丁"。

### 7.4 抄的时候必须改的三点

1. **`DirectForProbe` 缓存空结果**这个坑别学。我们要么一律 `cacheEmpty: false`，要么在 `OnGameLoadedNormal` 里 `DirectCache.Clear()`，否则热重载后细分永远显示"原版代码"。
2. **`catch { }` 遍地**——它是"旁观者模组"，坏了也不能影响游戏，这个取舍在它身上成立；但我们要在代价路径上采样，就必须把失败打成 `LogError`（工作区硬规范：内部逻辑不许静默吞异常）。
3. **`StallWatchdog.Crumb` 每次 `Begin` 都调**，嵌套批量操作时会刷屏 GameLog。我们只在大粒度操作的 `Begin` 上开这个开关。
