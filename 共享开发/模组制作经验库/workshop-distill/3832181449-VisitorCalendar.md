# 3832181449 NPC访客日历 / NPC Visitor Calendar

> **id**：`3832181449`｜`npc_visitor_calendar` v1.0.4｜作者 火焰树的种子｜9.7 千订阅｜游戏版本 `0.46D`｜`<Capabilities />` 空（零能力）｜HotReload 钩子：无
> **反编译目录**：`缓存文件夹\decomp_ws\3832181449\`（14 个 `.cs` / 2,988 行）
> **为什么值得读**：全库唯一把「谁哪天来」变成玩家可见信息的模组。它示范了一条我们没走过的路 —— **不建窗口、不建存档、不建客户端，只把原版的两串状态（预约队列 + 剧情表）翻译成伪 `StoreEvent` 塞进原版日历**。想给我们自己的访客做「可见化」，这是最便宜的一张底牌。

## 一、规模与结构

| 文件 | 行数 | 占比 | 角色 |
| --- | --- | --- | --- |
| `VisitorCalendar/VisitorData.cs` | 1,112 | 37.2% | **预测数据层**：三源合流 + 名字解析（含 100 行 `switch` 兜底） |
| `VisitorCalendar/CalendarLayoutOptimizer.cs` | 1,106 | 37.0% | **UI 改造层**：把原版 14 格日历重排成「2 周 + 滚动」 |
| `VisitorCalendar/ShutterVisitorIntegration.cs` | 318 | 10.6% | **外部兼容层**：给「卷帘门今日事件」模组叠一层文字 |
| 8 个补丁壳（`*Patch.cs` / `CalendarEventInjection`） | 318 | 10.6% | 每个 23~95 行，本体逻辑全在 `VisitorData` |
| `VisitorPrediction` + `LocalizationHelper` + 入口 + csproj/AssemblyInfo | 168 | 5.6% | 数据类 38 行、设置文案兜底翻译 |

**一句话结论：UI 占 47.7%**（1,106 + 318 行），真正的「数据」只占 37.2%，补丁壳 10.6%。这是典型的「读得多、画得多、算得少」结构。

## 二、它怎么知道「谁哪天来」

三条数据源**按可信度分级并列**，`VisitorPrediction.IsConfirmed` 就是「这次是排程还是猜测」的标记（`VisitorPrediction.cs:13`）。UI 上不区分，但名字后面要不要加「（可能）」由它决定。

### ① 精确源：原版预约队列（真值，不预测）

`VisitorData.cs:256-279`，读 `PlayerStore.futurStoreClientIdQueue` / `futurStoreClientDayQueue` 这对 public 列表：

```csharp
// VisitorData.cs:264-278
bool flag = (int)store.storeState == 2;                       // StoreState.OPEN
int num3 = ((!flag) ? ((num2 > 0) ? num2 : 0) : ((num2 > 0) ? (num2 - 1) : 0));
if (station.dayCounter + num3 == requestedDay)
    Add(list, seen, requestedDay, text, null, "游戏已排程 / 预约队列", confirmed: true);
```

**要害：`futurStoreClientDayQueue` 是「还有几天」的倒计时，不是绝对日。** 原版 `HandleFutureClientQueue` 每天 `--` 一次（`Assembly-CSharp/PlayerStore.cs:1414-1430`）。所以查某一天必须 `dayCounter + 倒计时`；而当店铺已经是 `OPEN(2)` 时今天的 tick 已经跑过，要再减 1。这一步算错，整个日历会**整体错一天**。

### ② 今日已发生：三处集合差集快照

普通 NPC 是随机生成的（`StoreClientManager.GenerateClient`），模组**根本不去预测随机**，只做事后抓取：

| 补丁 | 手法 | 产出 |
| --- | --- | --- |
| `FutureVisitorTransferPatch` → `HandleFutureClientQueue` Prefix | 扫出倒计时 `<= 1` 的 id | 「今日已转入营业队列」 |
| `GeneratedVisitorSnapshotPatch` → `GenerateClient` Pre/Post | Post 时与 Pre 的 `manager.clientStack` 做差集 | 「开始营业后实际生成的特殊访客」 |
| `NormalVisitorExclusionPatch` → `HandleNormalClient` Pre/Post | 同上，把差集里属于普通顾客的记进黑名单 | 供给上一条**剔除**用 |

三张静态表：`TransferredToday` / `GeneratedToday` / `NormalDuringGeneration`（`VisitorData.cs:17-21`）。第二、三条是**成对**的 —— `GenerateClient` 会用到普通顾客，所以要先把普通顾客标出来，否则「特殊访客」里会混进随机 NPC。第二个数据源默认**关闭**（`show_random_visitors`，`VisitorCalendarMod.cs:27`）。

### ③ 推算源：硬编码剧情表 + 模 7 周期（默认也关）

`show_unknown_visitors` 打开后才跑，六组互相独立的表（`VisitorData.cs:211-237`）：

- **剧情绝对日**：`StorylineSchedule.StartDay("georgio"/"lun"/"eleanor")` + 手写偏移 `+3/+6/+9/+12/+16/+21/+26/+31/+37`（`VisitorData.cs:289-306`），`SerpicoStoryline.Serpico1Day…ConclusionDay` 常量（`:364-381`）。
- **周期访客**：`day % 7 == 0` 商人/房东/收债人，`==2` 水产/化学家，`==3` 退休垃圾商，`==4` 彩票代表，`==5` 农场主/酿酒/牧场主，`==6` 下层居民（`:524-618`）；外加 `day==18` 房东批发、`day==25` 索耶、`day==12` 导师霰弹枪、`day==24` 导师许可证（`:508-523`）。
- **条件链**：`AddChain` 从今天到目标日**逐日**问原版谓词，第一个返回真的那天才算（`VisitorData.cs:670-683`）：

```csharp
for (int i = today; i <= day; i++) {
    if (condition(i)) { if (i == day) Add(list, seen, day, id, name, "剧情条件推算（预测）", confirmed: false); break; }
}
// 调用处一律传原版静态谓词：StoreClientData.CanEleanorWarnedVisit / CanEleanorPlanVisit / CanLouFirstVisit …
```

- **降噪一层**：`Add()` 里只要 `confirmed == false` 且名字含「（可能）」，就直接**丢弃**（`VisitorData.cs:737-747`）。它自己都不信自己的猜测。

**答案：精确 + 推算两套并存，不是读种子、不是读随机源。** 原版特殊/剧情访客本来就是排程的（`QueueFuturClient` + `StorylineSchedule`），所以「预测」本质是**把原版那张剧情表重新算一遍**；真正随机的普通 NPC 它主动放弃。

## 三、它怎么把访客画上日历

### 不自绘窗口 —— 全部寄生在 `AdvCalendarUIManager` 上

| 手法 | 位置 | 说明 |
| --- | --- | --- |
| 伪 `StoreEvent` 注入 | `CalendarEventInjection.cs:49-62` | 往 `GetActiveEventOnDay` 的返回表里加 `identifier="vc_visitor_<id>_<day>"` |
| 事件格不够就补 | `CalendarLayoutOptimizer.cs:123-178` | `Instantiate(day.eventElementPrefab)` 往 `day.eventElements` 追加 |
| 14 格 → 2 周 + 滚动 | `CalendarLayoutOptimizer.cs:216-314` | 见下 |
| 点开详情 | `CalendarDetailsPatch.cs:11-33` | 直接写 `AdvCalendarUIManager.detailNameTMP` / `detailDescriptionTMP` |
| 卷帘门面板叠字 | `ShutterVisitorIntegration.cs:79,147+` | 按**名字** `transform.Find("ShutterEventsPanel")` 找对面 panel |

**`IsKnown = true` 是硬要求**：原版 `GetActiveEventOnDay(int day, bool excludeCosmetic = true, bool excludeUnknown = true)`（`Assembly-CSharp/StoreEventManager.cs:566-585`）会丢掉 `IsKnown == false` 的事件。默认参数就是 `true`，忘了设就一条都画不出来。同时 `eventType = (EventType)0`（NORMALE，躲开 `excludeCosmetic`）、`eventArea = (EventArea)2`、`duration = totalDuration = 1`。

### 布局改造：把原版网格「抬」进自建 ScrollRect

`CalendarLayoutOptimizer.cs:216-314` 是这个模组最有复用价值的一段：

1. 先查 `gridLayoutGroup.GetComponent<ScrollRect>() != null`，**有就整个放弃**并打警告（`:225-229`）；
2. 建 `VC_WeeksContent`（`VerticalLayoutGroup` + `ContentSizeFitter`）挂到原 grid 下；
3. 建 `VC_Week_1/2`（`GridLayoutGroup`，`constraintCount = 7`，格子尺寸抄原 `cellSize`）；
4. 把 14 个 `CalendarDayElement` 逐个 `SetParent` 进周容器（`:288-295`）；
5. 关掉原 `GridLayoutGroup`，加 `RectMask2D`（左侧多放 12px）+ `ScrollRect`（垂直、惯性、灵敏度 30）。

配套还有一整套「临时改原版结构」的记账（`§9.5` 同源）：`RecordChildren` / `PinHeaderChildren` / `originalFrames` / `originalMaskPadding` / `ApplyThreePart`（自己拿 `Image` 拼三段九宫格，`:623-704`）。

### 存档：**零存档**

全库 grep 无 `ES3`、无 `Save`、无私有键写入。状态只有三张静态表 + `snapshotStore` / `snapshotDay` 两个指针，跨日靠**对比 (store, day)** 自动清（`VisitorData.cs:29-39`），`OnDisable` 一把清空（`VisitorCalendarMod.cs:42-48`）。
**代价（推测）**：读档重进后「今日已生成的特殊访客」快照丢失，同一天再进日历里随机访客那一条会不见。

## 四、Patch 靶点表

| # | 靶点 | 位置 | 类型 | 干什么 |
| --- | --- | --- | --- | --- |
| 1 | `StoreEventManager.GetActiveEventOnDay` | `CalendarEventInjection.cs:7-78` | Postfix `(int __0, ref List<StoreEvent> __result)` | 注入伪事件；**新建副本再赋回 `__result`**，不原地改原版表 |
| 2 | `PlayerStore.HandleFutureClientQueue` | `FutureVisitorTransferPatch.cs:9-23` | Prefix | 抓「今日转入」 |
| 3 | `StoreClientManager.GenerateClient` | `GeneratedVisitorSnapshotPatch.cs:10-41` | Pre/Post + `out HashSet<StoreClient>` | 抓特殊访客 |
| 4 | `StoreClientManager.HandleNormalClient` | `NormalVisitorExclusionPatch.cs:10-41` | Pre/Post + `out HashSet<StoreClient>` | 剔除普通顾客 |
| 5 | `AdvCalendarUIManager.OpenUI` | `CalendarLayoutPatch.cs:6-22` | Pre/Post `(___dayElements)` | 建/还原布局 |
| 6 | `CalendarDayElement.SetElement` | `CalendarSlotPatch.cs:7-25` | Prefix `[HarmonyPriority(0)]` | 补事件格 |
| 7 | `CalendarEventElement.OnPointerClick` | `CalendarDetailsPatch.cs:11-33` | Postfix | 详情面板换字 |
| 8 | `StoreShutterButton.Update` | `ShutterVisitorHeartbeatPatch.cs:6-23` | Postfix | 0.4s 节流心跳，驱动卷帘门叠字 |

## 五、关键代码摘录

**A. 伪事件注入（`CalendarEventInjection.cs:42-69`）** —— 注意 `Details` 是「id → 预测」的旁挂表，UI 点开时反查：

```csharp
List<StoreEvent> list2 = new List<StoreEvent>(__result);        // 拷贝，不动原版引用
foreach (VisitorPrediction item2 in list) {
    string key = "vc_visitor_" + item2.ClientId + "_" + item2.Day;
    if (!list2.Exists(e => e != null && e.identifier == key)) {
        list2.Add(new StoreEvent {
            identifier = key, newsName = name, displayName = name,
            eventType = (EventType)0, eventArea = (EventArea)2,
            startDay = item2.Day, duration = 1, totalDuration = 1,
            IsKnown = true                                       // ← 缺这行全部不显示
        });
        Details[key] = item2;
    }
}
if (Details.Count > 2048) Details.Clear();                       // 粗略上限，不做 LRU
__result = list2;
```

**B. 名字解析的降级链（`VisitorData.cs:753-822` + `1000-1019`）**：

```csharp
// 1) 两个特例硬编码 → 2) 原版客户端显示名 → 3) 代表 id（变体折叠）→ 4) 英文兜底表
string text = TryGetClientDisplayName(id);          // StoreClientListDict.GetClientDisplayName(id)
if (IsUsableLocalizedName(text, id)) return text;
// IsResolvedName 的判据：非空、不等于查询键、**不以 "name_" 开头**
private static bool IsResolvedName(string name, string lookup) =>
    !string.IsNullOrWhiteSpace(name) && !string.Equals(name, lookup, StringComparison.Ordinal)
    && !name.StartsWith("name_", StringComparison.Ordinal);
```

## 六、踩坑与红线

1. **倒计时不是绝对日，`OPEN` 还要再减 1**（`VisitorData.cs:264-273`）。这是全档最容易错的一行。
2. **`IsKnown = true` 不可省**，否则被 `excludeUnknown` 默认过滤掉，日历一条都不多。
3. **动原版 UI 前先查有没有被接管**（`CalendarLayoutOptimizer.cs:225-229` 查 `ScrollRect`）。同一 GameObject 上两个 `ScrollRect`/`LayoutGroup` 打架，结果是日历彻底不能滚。
4. **还原必须完整**：`GridLayoutGroup.enabled` / `cellSize` / `padding` / `RectMask2D.padding` / 子物体父级 / 三段九宫格几何 / Today 描边，全部要记原值再还原（`:1000-1090`）。`Restore()` 在 6 处被调用（关设置、切 UI 实例、异常、`OnDisable`、`OpenUI` 前、`UpdateLayout` 早退）。漏一处 = 日历永久变形，必须重进游戏。
5. **补丁要声明 `[HarmonyPriority(0)]`**（`CalendarSlotPatch.cs:11`）—— `SetElement` 由原版日历自己调，别人（事件扩展类模组）也在改 `events`，谁先跑决定看到什么。
6. **`ref __result` 一律新建副本再赋回**（`CalendarEventInjection.cs:42,69`）。原地 `Add` 会污染原版 `activeEvents` / 缓存表，撤销后仍留垃圾。
7. **英文兜底表会丢信息**：`wanted5*` 和 `lou*` 全归 "Lou"，`retired_*` 全归 "Retired Specialist"（`VisitorData.cs:922-970`）。给中文玩家看的场景够用，做「访客档案」类功能不够。
8. **卷帘门兼容靠 GameObject 名字**（`"ShutterEventsPanel"`）。对面改个名 → 静默失效，只走 `Restore()`，不报错。
9. **每帧心跳**：`StoreShutterButton.Update` Postfix 无条件触发 `Poll()`，靠 `Time.unscaledTime` 做 0.4s 闸门（`ShutterVisitorIntegration.cs:65-70`）。闸门写错就是每帧 `transform.Find`。

## 七、对我们的可用点

### 能显示我们的访客 —— 而且**零适配代码**

`ReadCommittedQueue` 读的是原版队列，我们的电话预约已经在往那儿写：

| 我们的调用点 | 现状 |
| --- | --- |
| `深空当铺：可能是群友/src/DspVisitorPhones.cs:207` | `store.QueueFuturClient(spec.SellClientId, 1)`（伞萝萝 / 塞壬「明天带货上门」） |
| `深空当铺：可能是群友/src/FreyaPhone.cs:232` | `QueueFuturClient(FreyaCourier.ClientId, 1)`（送货上门） |

一旦入队，日历**立刻**显示，名字走 `StoreClientListDict.GetClientDisplayName(id)` —— 该方法对 `storeClientDict` 里任何 id 都成立（`Assembly-CSharp/StoreClientListDict.cs:1131-1145`），而我们正是往那张表注册的（`FreyaClients.cs:72-85`，并已调 `ClearDisplayNameCache()`）。

**唯一要守的坑**：我们的 `displayName` 必须是**解析后的中文**，不能是 `name_xxx` 这种 loc 键 —— 否则被 `IsResolvedName` 拒掉，日历显示内部 id（`VisitorData.cs:1013-1019`）。

**它拿不到的东西**（所以别指望它当我们的日程面板）：我们自排的冷却日程（`DspVisitorPhones` 的 7 天 CD）、我们的剧情链，它都不认识 —— 它的推算表全是原版 id。

### 三条可搬的具体做法

1. **《深空当铺：可能是群友》：电话预约可见化**。给「拨通后预约明天上门」加一条日历显示，最省的做法不是写兼容、也不是装这个模组，而是抄 `ReadCommittedQueue` 那 20 行（含 `storeState == OPEN` 的 `-1` 修正）自己塞伪 `StoreEvent` —— 我们已经有 `AdvCalendarUIManager` 的所有公开字段可用。
2. **《深空电玩》：发售日程上日历**。电玩已有「第 14 天 `thatgame` 解锁」这类**绝对日**事件（`深空电玩/PROGRESS.md` 功能 #2）。用同一套伪 `StoreEvent`（`IsKnown=true`、`eventType=NORMALE`、`duration=1`）就能把这些日程摆进原版日历，不用新建任何窗口。
3. **《深空当铺：可能是群友》：访客名字的唯一解析入口**。`VisitorData.ResolveName` 的降级链（原版表 → 代表 id → 英文表 → 原始 id）值得照抄成我们自己的 `DspClientNames.Resolve(id)`，避免各处 `displayName` 直接拼字符串。
