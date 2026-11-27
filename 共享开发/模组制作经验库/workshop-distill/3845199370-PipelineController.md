# Pipeline Controller（流水线控制器，工坊 3845199370）蒸馏档案

> 反编译源码：`缓存文件夹/decomp_ws/3845199370/PipelineController/`，54 个 `.cs`、约 2.36 万行，单个 `PipelineCore.cs` 占 11049 行。构建标记 `Pipeline Controller build 2026-11-27.0145`（[ModEntry.cs:20](缓存文件夹/decomp_ws/3845199370/PipelineController/ModEntry.cs#L20)）。
> **一句话**：这是一套塞在"一件物品的两个字符串 Tag"里的节点图脚本系统——节点是店里机器的 uid 引用，边是 `A>B` 文本，执行是每秒一次的有预算拓扑轮询，编辑器是自己画的 Unity UI 画布。
> 本文引用代码均为源码原样（仅去掉反编译器的 `//IL_xxxx` 噪声注释），行号对应当前反编译文件。

---

## 1. 图的模型与执行

### 1.1 存在哪：两个物品 Tag，不是 ModData

全模组**没有** `ModData`、没有 `ES3`、没有独立存档文件（`grep ModData|ES3` 无命中）。整张图塞进控制器这一件 `GameItem` 的 tag 里：

| Tag | 内容 | 定义 |
| --- | --- | --- |
| `PIPELINE_STAGES_STRING` | 全部节点，`\|` 分隔的位置编码串 | [PipelineCore.cs:258](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L258) |
| `PIPELINE_EDGES_STRING` | 全部边，`;` 分隔的 `A>B` 文本 | [PipelineGraph.cs:10](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineGraph.cs#L10) |
| `PIPELINE_BALANCE_STRING` | 均分标记 | [PipelineCore.cs:260](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L260) |
| `PIPELINE_AUTO_BOOL` | 自动开关（默认 `true`） | [PipelineCore.cs:262](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L262) |

写入方式是给 tag 赋值后立刻 `core.ValidateShapeState()`（[PipelineGraph.cs:224-225](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineGraph.cs#L224)），让原版物品的 tag 变更走它自己的脏标记流程。

### 1.2 节点的表示法：**引用型节点**（这是全模组最核心的设计决定）

`PipelineStage` 不是"图里新造的一个对象"，而是**指向店里某件已存在物品的指针 + 一张配置单**：

- `Uid` = 目标物品的 `uniqueId`（`target.GetUniqueID()`，[PipelineCore.cs:1482](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L1482)）。所以节点**不拥有**机器，只引用它；机器被卖掉/搬走，节点就"失踪"（`LogMissingNodes`）。
- `Role` 决定节点类型（[PipelineStage.cs:8-42](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineStage.cs#L8)）：

| Role | 含义 | `IsVirtual` |
| --- | --- | --- |
| 0 | 设备（一台原版机器） | 否 |
| 1 / 2 | 取料 / 存料（箱子、瓶、缸） | 否 |
| 4 | 条件（按物品/液体分流） | 是 |
| 5 / 6 | 水龙头（造水）/ 排水口（倒掉） | 是 |
| 9–12 | 商店主库 / 后仓 / 展示柜 / 垃圾桶 | 是（读 `EmporiumEntry`，[PipelineCore.cs:10978](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L10978)） |
| 13 | 参数（配方/温度，连到机器就"继承"） | 是 |
| 14 | 过滤（锅↔滤壶之间反复滤） | 是 |
| 15 | 配料（按比例投料） | 是 |
| 16 | 动作（抽/倒/滤/投/按/设温/等/分流 八个小步骤） | 是 |
| 17 | 提纯（默认关，需设置里开；见 `PipelineConfig.PurifyEnabled`） | 是 |

- 虚拟节点（`Role >= 4`）的 uid 由 `VirtualUidBase = 900000` 起自增分配（[PipelineStage.cs:24](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineStage.cs#L24)、`AddVirtualNode` [PipelineCore.cs:4431](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L4431)），与真实物品 uid 不冲突。
- 容器类节点用 **case 侧号**表达"第几条出边/第几个分支"：`CaseSideBase = 100`、`OutPortSideBase = 200`（`Ids(side)`/`Mode(side)`，[PipelineStage.cs:485-513](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineStage.cs#L485)）。

### 1.3 边的表示法

```csharp
internal sealed class PipelineEdge
{
	public int From;
	public int To;
	public int Port;      // 源端出端口序号（0 = 默认）
	public int Kind;      // 0 = 物流边；1 = 耗材边（滤芯/试剂）
	public string Only = string.Empty;  // 该边专用：只送这个标识符
	public int ToPort;    // 目标入端口序号
}
```
（[PipelineEdge.cs:3-15](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineEdge.cs#L3)）

序列化成人类可读的单行文本，符号各有含义（[PipelineGraph.cs:217-227](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineGraph.cs#L217)）：

```
5:2>7~1!|water_filter
│   │ │  │ └── Only：这条边只送 water_filter
│   │ │  └──── '!' = Kind 1（耗材边）
│   │ └─────── '~1' = ToPort 1（目标的第 2 个入口）
│   └───────── To uid
└───────────── From uid：':' 后是 Port
```

读取用"降序剥离后缀"的顺序解析（先剥 `!`，再剥 `|Only`，再剥 `~ToPort`，最后剥 `:`Port），并且**去重 + 拒绝自环**：`from != to && !list.Any(...)`（[PipelineGraph.cs:169-213](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineGraph.cs#L169)）。加边时还有一条类型级否决：`Role == 5`（水龙头）不接受入边（[PipelineGraph.cs:255-260](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineGraph.cs#L255)）。

### 1.4 节点的序列化：位置编码 + k=v 尾巴（前向兼容的工程做法）

`PipelineCodec.Encode` 输出 `v1|f:f:f:...|f:f:f:...`，每个节点是 **42 段**（[PipelineCodec.cs:489-507](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCodec.cs#L489)）：前 19 段是热字段（Role / Uid / Tab / X / Y / InMode / InIds / OutMode / OutIds / Observed / 水位阈值 / 水量 / Cases / TabOverflow / 出水质 / 水源 / 水等级 / 产出瓶等级 / OutPorts），中间 5 段是历史遗留的裸 `0` 占位，然后是槽用量 / 槽输出 / 空位 / 槽定量 / 定量上限 / 化学产物 / 批次量 / 过滤参数 / 滴定 / 条件成分 / 成分上限 / 反应温度 / 自动按钮 / 锁定温度 / 自动加热 / 配方量，最后一段是 `?` 开头的 **k=v 扩展区**（`EncodeKv`，[PipelineCodec.cs:509-573](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCodec.cs#L509)），存放后来新增的过滤/配料/动作/提纯字段（`fgate`、`msteps`、`ak`、`pkeep`…）。

三点值得抄：

1. **游标式读取 + 缺字段容忍**：`ReadInt/ReadFloat/ReadString`（[PipelineCodec.cs:452-487](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCodec.cs#L452)）读失败时 `i++` 后返回 `false`，调用方 `if (ReadInt(f, ref i, out value)) {...}` 跳过——旧档少字段不会崩。
2. **版本前缀 + 三重判决**：`CompatibleOldPrefixes` 列了 14 个旧前缀 `v9..v24`，`BestEffortOldPrefixes` 是 `v21,v22`；`Decide()` 返回 `Current / Readable / Newer / Unknown`（[PipelineCodec.cs:22-113](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCodec.cs#L22)）。读到**比自己新**的格式时它选择 `FreezeUnsupported` + **拒绝写回**（[PipelineCore.cs:10800-10814](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L10800)），避免降级版本把玩家图写坏。
3. **空写保护**：`WriteStages` 默认 `allowEmpty: false`，如果传进来的空列表而存档里已有内容，它拒绝覆盖并打警告（[PipelineCore.cs:10883-10891](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L10883)）——因为"空列表"通常意味着读取失败而不是用户清空了画布。

### 1.5 执行频率：**实时 tick（1 秒）+ 夜里兜底**，不是每夜一次

- `PipelineTicker` 是挂在 `DontDestroyOnLoad` GameObject 上的 MonoBehaviour，`Update` 里用 `Time.unscaledDeltaTime` 累加到 1 秒就调 `PipelineCore.TickAll()`（[PipelineTicker.cs:17-32](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineTicker.cs#L17)）。用 unscaled 时间，所以暂停/慢动作不影响。
- 同时 `ModHook.OnHandlingNightlyServicesLate` 与 `OnGoingSleepLate` 都挂 `RunAllNow("night")`（[ModEntry.cs:36-37](缓存文件夹/decomp_ws/3845199370/PipelineController/ModEntry.cs#L36)、[118-121](缓存文件夹/decomp_ws/3845199370/PipelineController/ModEntry.cs#L118)）——玩家不收店也会在过夜时补跑一遍。
- 单控制器也能手动跑："立即运行"按钮直接 `Run(core)` 并 `TraceNextRun()`（[PipelineCore.cs:997-1003](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L997)）。
- 防重入：`Run` 开头 `_running` 为真就直接 return（[PipelineCore.cs:10210](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L10210)）；`SaveManager.IsDecoding` 期间 `TickAll`/`Run` 全部空转。

### 1.6 一个 tick 里怎么决定"哪些节点该动"

`TickAll` 的路径（[PipelineCore.cs:10135-10206](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L10135)）：

1. 遍历 `Instances`（控制器 `GameItem` → `Refs` 的运行时字典）。**这张表只在 `Create()` 里注册**（[PipelineCore.cs:978](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L978)），而 `Create` 是注册给物品工厂的 `Func<GameItem>`（[ModEntry.cs:125](缓存文件夹/decomp_ws/3845199370/PipelineController/ModEntry.cs#L125)）——读档时游戏反序列化会重新调工厂函数，于是**自动重新注册**，不需要扫场景。这是很省事的生命周期设计。
2. 跳过已销毁、`!GeneralHelper.IsItemOwned`、`!IsAuto(core)` 的控制器。
3. **全 tick 共享 450ms 预算**：`_tickDeadline = Stopwatch.GetTimestamp() + 450 * Stopwatch.Frequency / 1000`；预算耗尽的控制器留到下一 tick（[PipelineCore.cs:10153](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L10153)、[10176-10183](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L10176)）。
4. `Run(core)`：`SnapshotForTick()` 拿一份**按帧缓存**的 `uid → GameItem` 全店字典（[PipelineCore.cs:10908-10925](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L10908)）→ `ResolveNodes` → `ReadEdges` → **`OrderByFlow` = Kahn 拓扑排序**。
5. 拓扑序里**环不会被丢弃**：队列排完后剩下的节点（都在环上）按 uid 升序**追加到末尾**（[PipelineCore.cs:10656-10668](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L10656)）。所以"接回上游就是循环"在排序层面是合法的。
6. 然后按**类型分桶**多趟扫，而不是纯拓扑单趟（见 §6 模式 2 的代码）：商店窗口 → 容器/取料/存料 → 配料(15) → 动作(16) → 提纯(17) → 设备(0) → 过滤站(14) → `TriggerInstant`。
7. 每轮末尾 `if (!flag2) break;`：**一整轮没有任何移动就提前结束**。

### 1.7 防无限循环：四道闸

循环是允许的，收敛靠下面四层（全部有源码依据）：

| 闸 | 位置 | 说明 |
| --- | --- | --- |
| 轮数硬上限 **3** | [PipelineCore.cs:10253](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L10253) `for (int i = 0; i < 3; i++)` | 再长的链，一个 tick 也只推 3 轮 |
| **无进展节点抑制** | [PipelineCore.cs:10260-10263](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L10260)、[10503](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L10503) | 第 1 轮里"没搬动东西"的节点进 `hashSet`，第 2/3 轮直接 `continue`；每轮结束清空 |
| **递归推送深度上限 8** | [PipelineCore.cs:9587](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L9587) `if (item == null \|\| depth > 8) return false;` | 条件节点下传 `depth+1`（[9651](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L9651)），水龙头/排水口用 `ForwardThrough` 也是 `depth+1`（[1974](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L1974)）。这是"环"的真正收敛点 |
| **失败/满目标冷却** | `FailedThisTick`（[304](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L304)）、`FullTargets`（[308](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L308)，>256 条清空）、`ShapeBlocked`、`LastGoodInventory` | 同一 tick 内同 (目标,物品) 不重试；满目标带超时窗口，避免每秒都去撞同一面墙 |

另外每条运行路径都带 `Stopwatch` 计时 + 超过 50ms/200ms 打警告（例如 [10390-10394](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L10390)），配合设置里的"调试日志"开关（`PipelineConfig.DebugLog`）输出完整执行轨迹。

---

## 2. 输入输出过滤

### 2.1 三种表达，都不是通用 DSL

**(a) 节点级进出过滤**：每节点两组 `{ InMode, InIds }` / `{ OutMode, OutIds }`，三态模式 `ModeAll=0 / ModeAllow=1 / ModeBlock=2`（[PipelineStage.cs:74-78](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineStage.cs#L74)）。核心判定只有一个函数（[PipelineStage.cs:540-564](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineStage.cs#L540)）：`mode==0` 全放；否则遍历 ids 求 `Matches` 的 OR，`mode==1` 返回命中、`mode==2` 返回取反。

**(b) 谓词表 = "双形态标识符"**（`Matches`，[PipelineStage.cs:566-587](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineStage.cs#L566)）：一个条目要么是物品 `identifier`，要么写成 `liquid/<liquidId>` 或 `liquid:<liquidId>` 表示"该容器里有这种液体"。后者要求目标 `IsTag("LIQUID_CONTAINER_TAG")` 且 `item.GetTagReadonly(liquid.currentPart).GetInt() > 0`。**没有通配符、没有 and/or 嵌套**——多条件就是多条目 OR。
> 存储侧会做净化：`Sanitize` 把 `, ; =` 全替换成 `_`（[PipelineCodec.cs:202-210](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCodec.cs#L202)），所以标识符里不能带这几个字符。

**(c) 条件节点的多路分支**（Role 4）：`Cases` 是"每条分支一张 id 列表"，`OutPorts[i]` 是"每个出端口的 id 列表"。`PortCount() = CaseCount + 1`；`PortFor(item)` 返回**第一个命中的分支下标**，全不命中返回 `CaseCount`，也就是最后那个"兜底/else 端口"（[PipelineStage.cs:460-483](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineStage.cs#L460)）。分支判定 `PassesCase` 硬编码当成 allow-list 用（[PipelineStage.cs:451-458](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineStage.cs#L451)）。

### 2.2 唯一带"表达式"的地方：动作节点的 guard

`ActionGuards` 是一串 `,`/`;` 分隔的 `subject:op:value`，全部命中才执行动作（[PipelineCore.cs:545-611](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L545)）：

| subject | 取值 |
| --- | --- |
| `temp` | 容器温度（`ContainerTemp`） |
| `dry` | 可蒸发余量 ≤100µl 记 1，否则 0 |
| `space` | 目标接收容器的剩余容量 |
| `cart` | 有没有还能用的滤芯（1/0） |
| 其它任意字符串 | 当作液体成分 id，取该成分的体积 |

`op ∈ { lt, le, gt, ge, ne }`，不写 op 或写其它一律按 `eq`（[PipelineCore.cs:613-624](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L613)）。设置界面给了 5 个预设（[PipelineSettings.cs:11-13](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineSettings.cs#L11)）：

```
"可去=0"      → ""
"mush=0"      → "dream_cap_mush:le:0"
"蒸干"        → "dry:eq:1"
"下游有空间"   → "space:gt:0"
"有滤芯"      → "cart:eq:1"
```

### 2.3 与原版容器规则怎么共存

**没有**把一切都收进一个 `GraphUtils.CanAccept/TryAccept` 门面；它是"自己先算容量 → 再让原版拍板"两道关：

1. `PipelineAccept.Check`（[PipelineAccept.cs:8-61](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineAccept.cs#L8)）先做语义级否决：水龙头不能进液体、虚拟节点不能进物品、耗材只能进滤器、`target.PassesIn(payload)` 准入过滤、液体走 `WaterHelper.GetFreeCapacity`、还有节点自己的 `IntakeMax` 定量上限。
2. 物品槽位计数 `CountFreeItemSlots`（[PipelineAccept.cs:76-131](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineAccept.cs#L76)）按容器类型分派：`GameGridInventory.HasFreeCell()` / `GameSlotInventory.childItem == null` / 普通容器 `childItems.Count == 0`。
3. 真正落地只有一处 `PushTo` → **`GraphUtils.TryAcceptAll(destination, item, -1)`**（[PipelineCore.cs:9888-9909](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L9888)）；诊断日志里用 **`GraphUtils.CanAccept`** 解释为什么放不进（[PipelineCore.cs:2994](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L2994)）。
4. **额外尊重目标槽自己的白名单**：`((GameInventoryFunc)inv).mayInventoryAddItemFunc` 为真时才继续（[PipelineCore.cs:3003](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L3003)、[9862-9877](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L9862)）。也就是说"原版机器的槽过滤"优先级高于流水线的过滤——流水线只能**降低**能进的东西，不能**放宽**原版限制。容量上限是编译期常量：`MaxLinks=64 / MaxStages=4 / MaxIds=16 / MaxObserved=12 / MaxCases=5`（[PipelineCodec.cs:32-40](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCodec.cs#L32)），加节点前 `list.Count >= 64` 直接拒（[PipelineCore.cs:1410-1414](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L1410)）。

---

## 3. 画布 UI（本模组最大的工程量：`PipelineGraphWindow.cs` 2542 行 + 8 个输入/图形组件）

### 3.1 用原版 CustomUIManager 开一个"纸窗"，里面全自绘

窗口：`CustomUIManager.Instance.CreateWindow(WindowIdFor(coreUid), "流水线画布 · #"+coreUid, "paper", null, Style 3).SetSize(w,h).SetDraggable(true).SetCloseOnEscape(true)`，窗口 id 是 `pipeline_controller.canvas.<coreUid>`（[PipelineGraphWindow.cs:114-165](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineGraphWindow.cs#L114)）。每个控制器一个独立窗口，`instances` 字典按 coreUid 索引。

### 3.2 四层 RectTransform 结构

```
CustomUIWindow.Rect
└── Viewport        offset(8,8)/(−8,−84)，LayoutElement.ignoreLayout=true，SetAsLastSibling()，挂 RectMask2D
    └── ViewportHit alpha 0.001 的 PipelineSolidGraphic，只为吃射线；挂 Drag/Zoom/Click/Marquee 四个手势组件
        └── Content 锚点与枢轴都在中心；缩放/平移只动 content.localScale 与 anchoredPosition
            ├── Edges     PipelineEdgeGraphic，sizeDelta(20000,20000)，raycastTarget=false
            └── Node_<uid> 宽固定 120、高按端口数算的卡片：PipelineRoundedGraphic + PipelineNodeMarker + PipelineNodeDrag
```

（`BuildInside`，[PipelineGraphWindow.cs:297-348](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineGraphWindow.cs#L297)）

把"海量自绘内容"和"接收输入"分成两个兄弟层是关键：射线只打在 `ViewportHit` 上，线层 `raycastTarget=false`，节点卡片自己 `raycastTarget=true` 抢在中间。

### 3.3 节点卡片与圆点

- 卡片是自绘 `MaskableGraphic`：`PipelineRoundedGraphic`（[161 行](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineRoundedGraphic.cs)，`OnPopulateMesh` 用 4 个角 × 6 段圆弧生成外轮廓，中心做扇形填充，内外两圈之间铺 quad 当描边）。**不用 9-slice 贴图**，所以圆角半径/描边粗细都能随时改。
- 高度随端口数变化：`node.Height = max(116, 34 + portCount*22)`，颜色按 Role 查表（[PipelineGraphWindow.cs:1224-1252](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineGraphWindow.cs#L1224)）。
- 每个节点挂 `PipelineNodeMarker` 记录 `Uid / InPorts / OutPorts`（[PipelineNodeMarker.cs:5-12](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineNodeMarker.cs#L5)）。端口"圆点"就是子 `RectTransform`，登记进 `inputPorts` 列表供命中测试。
- 端口坐标不做手工布局，而是**从节点矩形反算**：`PortPoint(node, output, port)` 用 `PortOffsetY(count, index, height)` 把第 index 个端口均匀铺在左/右边沿，再 `node.TransformPoint → content.InverseTransformPoint` 转成 content 局部坐标（[PipelineGraphWindow.cs:1918-1986](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineGraphWindow.cs#L1918)）。所以拖节点时线自动跟着走。

### 3.4 连线：24 段三次贝塞尔 + 箭头 + 反向边鼓包

`PipelineEdgeGraphic` 只暴露 `SetLines(List<Line>)` / `SetPreview(from,to)`，`OnPopulateMesh` 里逐条画（[PipelineEdgeGraphic.cs:55-77](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineEdgeGraphic.cs#L55)）：

- 控制点：`p = (a.x + max(40, |dx|*0.35), a.y + bulge)`、`p2 = (b.x - 同值, b.y + bulge)`（[149-159](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineEdgeGraphic.cs#L149)）——横向 S 形，符合"从左往右流"的直觉。
- 采样 24 段，每段 `AddQuad` 铺 2px 宽的带子；末尾用切线法线画 9×5 的三角箭头（[161-181](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineEdgeGraphic.cs#L161)）。
- **反向边（目标在源左边）不会重叠**：`RedrawEdges` 给每条 `b.x <= a.x` 的边分配一个递增鼓包 `70 + 18*n`，并存进 `edgeBulges` 供右键删除时做命中测试（[PipelineGraphWindow.cs:1885-1915](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineGraphWindow.cs#L1885)）。
- 虚线预览：`dashed` 模式下只画偶数段（[165-168](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineEdgeGraphic.cs#L165)）。

### 3.5 交互分工：每个手势一个小组件

| 手势 | 组件 | 委派到 |
| --- | --- | --- |
| 拖空白平移（左/右键都可；Alt+左键让给框选） | `PipelineCanvasDrag`（52 行） | `View.Pan(delta)` |
| 滚轮缩放 | `PipelineCanvasZoom`（13 行） | `View.Zoom(scroll, pos)` |
| 点空白取消选择 / 右键删线 | `PipelineCanvasClick`（25 行） | `ClearSelection` / `TryDeleteEdgeAt` |
| Alt+拖框选 | `PipelineCanvasMarquee`（49 行） | `Begin/Update/EndMarquee` |
| 节点左键拖 / 右键拖＝平移 / 双击＝开机器窗 / Ctrl＝多选 / 右键＝设置 | `PipelineNodeDrag`（95 行） | `Begin/Drag/EndNodeDrag`、`OpenSettings`、`ToggleMachine` |

（各文件都在同目录，行数即文件长度。`PipelineNodeDrag.OnDrag` 里 `if (connecting) View.DragPort(...) else View.DragNode(...)` —— 同一个组件既管拖节点也管拖连线。）

- **拖拽坐标系修正**：`delta / max(0.01, content.localScale.x)`，否则缩放后拖动距离会漂（[PipelineGraphWindow.cs:2245](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineGraphWindow.cs#L2245)）。多选时用 `dragStartPositions` 快照整体平移，避免累积误差（[2203-2270](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineGraphWindow.cs#L2203)）。
- **以鼠标为锚点缩放**：夹在 `[0.5, 1.8]`，`pan = -(local - pan) * (zoom/old - 1) + pan`（[PipelineGraphWindow.cs:2456-2485](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineGraphWindow.cs#L2456)）。
- **连线落点判定**：`EndPortDrag` → `NodeUnderCursor`（`EventSystem.RaycastAll` 找 `PipelineNodeMarker`）→ `InputPortAt`（先查 `inputPorts` 里登记的圆点，未命中再按纵向比例 `InverseLerp` 折算到第几个入口；过滤壶的最后一格自动解释成耗材入口 `kind=1`）→ `PipelineGraph.AddEdge`（[2323-2410](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineGraphWindow.cs#L2323)）。
- **落盘**：`SavePositions()` 把画布坐标写回 `stage.X/Y` 再 `WriteStages`（[2487-2509](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineGraphWindow.cs#L2487)）——画布坐标就是节点数据本身。

### 3.6 自动布局（可选，但实现完整）

`PipelineGraph.AutoLayout`（[PipelineGraph.cs:333-528](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineGraph.cs#L333)）：`FindBackEdges` 用 DFS 着色找环，每找一个环就 `ChooseWorstEdge` **丢掉一条边**（按目标入度、目标是否为商店/取料/存料打分，[109-147](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineGraph.cs#L109)）直到无环 → 最长路径分层 → barycenter 上下扫 3 轮取交叉数最少的一次 → 按列 `x = col*290`、行 `y -= 144` 落位。坐标若全为 `NaN`（`PipelineStage.X` 默认就是 NaN）会自动触发一次布局，并且会检测"两节点重叠 <72/69.6px"并挪开（`EnsureLayout`，[285-331](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineGraph.cs#L285)）。

### 3.7 右键设置面板

`PipelineSettings.cs` 3325 行，是另一个 `CustomUIManager` 窗口（id `pipeline_controller.settings`），按节点 Role 分派到 `BuildWater / BuildMerge / BuildAction / BuildReactionLock / ...`，带滚动记忆（`ScrollMemory`）、窗口几何记忆（`RememberGeometry`）、"高级"折叠开关（`AdvancedOverride`）。它负责把 §2 里那些 ids/mode/guard 变成点选式 UI，是"图编辑器"里最容易被低估的工作量。

---

## 4. 与原版的接线

### 4.1 Harmony 只用了 5 处，全在外围

| Patch | 目标 | 作用 |
| --- | --- | --- |
| [PipelineHarvestDrainPatch.cs:6-23](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineHarvestDrainPatch.cs#L6) | `HydroponicHelper.FinishGrowing` Prefix | 水培收获是瞬时的，tick 抓不到 → 收获前先替该水培机 `DrainMachineNow` 把产物抽走 |
| [PipelinePickerInputPatch.cs:6-20](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelinePickerInputPatch.cs#L6) | `HopperHelper.HandleInput` Prefix | "点选模式"（点击店里机器加入画布）的输入 |
| [PipelinePickerEscapePatch.cs:5-15](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelinePickerEscapePatch.cs#L5) | `HopperHelper.ConsumedEscapeThisFrame` Postfix | 点选模式下吞掉 Esc |
| [PipelinePickerBlocksClicksPatch.cs:5-14](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelinePickerBlocksClicksPatch.cs#L5) | **（推测）** 反编译丢参数 | 点选模式下拦截普通点击 |
| [PipelineGameOptionsPatch.cs:5-12](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineGameOptionsPatch.cs#L5) | `GameOptionRegistry.EnsureVanilla` Postfix | 把"分配方式"下拉塞进游戏设置 |

**没有任何 Patch 触碰机器周期、产线计算、过夜模拟。** 这是它与"老派接管型模组"最大的区别。

### 4.2 真正的接管手段：`GameItemFunc` 六个拖拽委托

```csharp
((GameItemFunc)core).mayThisTargetItemFunc   = (GameItem source, GameItem target) => CanLink(source, target);
((GameItemFunc)core).canThisTargetItemFunc   = (GameItem source, GameItem target) => CanLink(source, target);
((GameItemFunc)core).onThisTargetItemFunc    = (GameItem source, GameItem target) => { AddLinkByItem(source, target); };
((GameItemFunc)core).mayItemTargetThisFunc   = (GameItem source, GameItem self)   => CanLink(self, source);
((GameItemFunc)core).canItemTargetThisFunc   = (GameItem source, GameItem self)   => CanLink(self, source);
((GameItemFunc)core).onItemTargetThisFunc    = (GameItem source, GameItem self)   => { AddLinkByItem(self, source); };
```
（[PipelineCore.cs:985-996](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L985)）

原版的手动搬运/合成交互链路本来就是"我把 A 拖到 B 上，问 B 三个函数：允许吗(may)/现在能给反馈吗(can)/确认了(on)"（**推测**：`may` 与 `can` 的精确分工未逐一验证，但本模组给两者填了同一个 `CanLink`）。把控制器的这六个槽填上，就得到了"拖机器到控制器 = 加入图"和"拖控制器到机器 = 加入图"两个方向，**零 Harmony、零 IL**。`CanLink` 里把关（[1385-1421](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L1385)）：必须 owned、目标必须有机器/容器窗口（`IsMachine` 或 `ContainerInventories().Count>0` 或 `LIQUID_CONTAINER_TAG` 或工具物品）、uid>0、未重复、<64 个。

### 4.3 怎么"读懂"一台原版机器：只读面板结构，不碰状态机

`PipelineTraits.Detect`（[PipelineTraits.cs:36-98](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineTraits.cs#L36)）：

1. `machine.contentWindow.childElement as MachineryPanelRow` → 遍历 `row.Panels`；
2. 每个 panel `panel.CollectStorage(list)` 拿第一个 `GameInventory`；
3. 用 `panel.role`（字符串）**关键词猜用途**：`LiquidHints = {liquid,water,bottle,distillate,fluid,drink,container,tank}`、`ProductHints={output,eject}`、`InputHints={input,storage,ingredient,chill,freeze,cage,inventory,wine}`、`UiHints={control,temp,settings,note,label,...}`（[22-32](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineTraits.cs#L22)、`InferKind` [138-228](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineTraits.cs#L138)）；猜不出的再按槽里现有物品的 tag 兜底（`KindByTag`），仍猜不出就留 `Unknown` 并且**不去动它**。
4. 同时把这些当"能力标签"记下来：`HasCycle = onCycleEndSlotItemFunc != null`、`HasEarlyCycle = onCycleEndEarlySlotItemFunc != null`、`HasSecondTick = onOneSecondItemFunc != null`（[46-48](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineTraits.cs#L46)）。

**注意**：这三个委托它只是**读**来判断机器特性（能不能催、要不要手动触发 instant 那一拍），从不赋值、不替换。所以"接管"实际上是：

- **取**：从 `DrainPanels`（输出/产物 panel）里把已产出的物品搬走；
- **喂**：往 `FeedPanels`（输入 panel）里放料；
- **触发**：需要"按一下"的机器（化学成型、按钮型）用 `ModifyTag("CURRENT_TARGET_TEMP")` / `ModifyTag("HEATING_PLATE_STATE")` 和模拟按钮 id（`GameApi.ProduceButtonKey = "mech_chem_finisher_btn_output"`，[GameApi.cs:13](缓存文件夹/decomp_ws/3845199370/PipelineController/GameApi.cs#L13)）来推动，而不是伪造产物。

### 4.4 不与原版打架的五条纪律

1. 只搬"已经存在"的物品，不改机器内部的配方/进度/状态机。
2. 每次落地前四重确认：`IsItemOwned` → 目标 `mayInventoryAddItemFunc` → `GraphUtils.CanAccept` → `GraphUtils.TryAcceptAll` 返回值校验。
3. `IsAuto == false` 的控制器完全不参与 tick；玩家可以"先接线、后开自动"。
4. `SaveManager.IsDecoding` 期间所有入口直接 return（[10137](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L10137)、[10210](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L10210)）。
5. 全 tick 450ms 硬预算 + 单步 50/200ms 慢步警告，把"我的模组拖慢游戏"变成可观测、可回退的问题。

---

## 5. 说明书槽：把整份文档变成游戏内物品

### 5.1 机器窗口的第三块面板就是"说明书槽"

```csharp
PixelWindow val9 = MachineHelper.CreateMachineryWindow(true, new MachineryPanel[3]
{
	MachineHelper.Panel("控制 / Control", (PixelElement)(object)val6, "control", true),
	MachineHelper.NotePanel(val7),                                   // ← 说明书槽
	MachineHelper.Panel("legacy output", (PixelElement)(object)val, "output", true).SetHidden(true)
});
```
（[PipelineCore.cs:956-961](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L956)，`val7` 是一个 `GameSlotInventory`）

`MachineHelper.NotePanel(GameSlotInventory)` 是原版现成的"纸质附注槽"——原版机器里放说明书用的就是它。模组直接复用它，于是**说明书出现在原版 UI 期望的位置**，不用新做界面。

### 5.2 说明书本体就是一张"便利贴物品"

`CreateManual()`（[PipelineCore.cs:1159-1171](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L1159)）：`DirectoryMaster.Item("postit", true)` → 改 `name` 为「流水线控制器 · 说明书」→ **整份文档塞进 `shortDescription`**（[1161 行](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L1161)那一个字符串就是全部说明：最快上手 / 画布操作 / 节点类型 / 最常用设置 / 排错）→ `SetGameItemType("DOCUMENT")` → `EnableTag("MACHINE_MANUAL_TAG")`（让原版认它是说明书）+ `EnableTag("IMPORTANT_TAG", false)`（不会误丢）→ `unitValue = 0`。

### 5.3 塞进去的方式与时机

- **塞**：`((GameInventory)noteSlot).UncheckedAccept(manual); ((GameInventory)noteSlot).Validate();`（[PipelineCore.cs:1089-1092](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L1089)）。用 `UncheckedAccept` 绕过槽的容量/白名单校验，因为这是模组自己造的物品、自己保证不越界。
- **补**：`window.openCallback += () => { EnsureManual(core); Rebuild(core); }`（[1037-1041](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L1037)）——每次开窗口都检查一遍，玩家把说明书丢了/卖了就自动补一张。另外 `OnGameLoadedLate` → `EnsureManualsAll()` 遍历所有控制器补（[ModEntry.cs:112-116](缓存文件夹/decomp_ws/3845199370/PipelineController/ModEntry.cs#L112)），所以**老存档（模组升级前造的控制器）也会自动获得说明书**。
- **双语**：`Text(key, chinese)` 先看 `LocHelper.GetCurrentLocaleCode()`，非 zh 再 `ModHelper.GetLocalized("pipeline_controller", key)`，取不到回退中文（[1173-1189](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L1173)）。同样的键也在语言文件里给英文，`PipelineConfig.Loc(en, zh)` 是同一套思路（[PipelineConfig.cs:98-109](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineConfig.cs#L98)）。
- 控制器自己的 `shortDescription` 只有 4 行"最快上手"，末尾写"详细用法：机器说明书槽里的说明书"（[964 行](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L964)）——**两层文档**：商店里看 4 行，买回去读全文。

---

## 6. 可复用模式

### 模式 1：引用型节点图 + 人类可读文本序列化

**要点**：节点不复制实体，只存 `uid` 引用；边序列化成可肉眼读的 `A:port>B~toPort!|only`；配置项用"位置编码（稳定热字段）+ `?k=v` 尾巴（可增长）"两段式，读出用游标容忍缺字段，并用版本前缀决定"可读 / 只读冻结"。

```csharp
// PipelineGraph.cs:217-227
	public static void WriteEdges(GameItem core, List<PipelineEdge> edges)
	{
		if (core != null)
		{
			string text = string.Join(";", from e in edges
				where e != null && e.From > 0 && e.To > 0 && e.From != e.To
				select ((e.Port > 0) ? (e.From + ":" + e.Port) : e.From.ToString()) + ">" + e.To + ((e.ToPort > 0) ? ("~" + e.ToPort) : string.Empty) + ((e.Kind > 0) ? "!" : string.Empty) + (string.IsNullOrEmpty(e.Only) ? string.Empty : ("|" + e.Only)));
			core.state.GetTag("PIPELINE_EDGES_STRING").Enable().SetString(text);
			core.ValidateShapeState();
		}
	}
```

配套的读端要点（[PipelineGraph.cs:169-213](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineGraph.cs#L169)）：`split('>')` 后**按固定顺序剥后缀**（`!` → `|only` → `~toPort` → `:port`），剥完再 `TryParse`，任何一步失败就跳过这条边而不抛异常——存档永远"坏一点点"而不是全崩。

### 模式 2：拓扑序 + 有限轮次 + 预算的调度器

**要点**：Kahn 排序得到近似执行序（环上节点追加到末尾而不报错），然后**最多 3 轮、每轮按角色分桶**扫描，每轮"零进展"就提前退出；全局 450ms 预算把超时转成"下一 tick 继续"。

```csharp
// PipelineCore.cs:10253-10276
				for (int i = 0; i < 3; i++)
				{
					bool flag2 = false;
					foreach (PipelineStage item2 in list2)
					{
						if (i > 0 && hashSet.Contains(item2.Uid))
						{
							continue;          // 第 2/3 轮跳过"第 1 轮没搬动东西"的节点
						}
						if (PipelineStage.IsStoreRole(item2.Role) && PipelineGraph.HasAnyOutgoing(edges, item2.Uid))
						{
							GameInventory val = StoreWindow(item2.Role);
							if (val != null)
							{
								bool flag3 = PushFromContainer(item2, val, list, edges, dictionary, value);
								flag2 |= flag3;
								if (!flag3 && i == 0)
								{
									hashSet.Add(item2.Uid);
								}
							}
						}
```
（后面紧跟 `else` 分支处理容器/取料/存料；再往后是 4 个独立 `foreach`，分别只处理 Role 15 / 16 / 17 / 0，最后 Role 14。整段在 `for` 里，末尾 `if (!flag2) break;` + `hashSet.Clear();`）

### 模式 3：CustomUIManager 上的自绘画布（三层 + 输入组件分离）

**要点**：`Viewport`（裁剪，`RectMask2D`）→ `ViewportHit`（alpha 0.001 的实心 Graphic 专门吃射线，挂 4 个手势组件）→ `Content`（唯一被 `localScale` 缩放的对象）→ `Edges`（一个超大 Graphic 画所有线，`raycastTarget=false`）+ 每节点一个自绘卡片。所有坐标都在 `Content` 局部空间里算，缩放/平移只动 `Content` 的 `localScale` 和 `anchoredPosition`。

```csharp
// PipelineGraphWindow.cs:316-330, 331-345（删去部分锚点样板行）
		viewport = NewRect("Viewport", (Transform)(object)window.Rect);
		viewport.anchorMin = Vector2.zero;
		viewport.anchorMax = Vector2.one;
		viewport.offsetMin = new Vector2(8f, 8f);
		viewport.offsetMax = new Vector2(-8f, -84f);
		((Component)viewport).gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
		((Transform)viewport).SetAsLastSibling();
		((Component)viewport).gameObject.AddComponent<RectMask2D>();
		RectTransform val = NewRect("ViewportHit", (Transform)(object)viewport);
		Stretch(val);
		((Graphic)((Component)val).gameObject.AddComponent<PipelineSolidGraphic>()).color = new Color(0f, 0f, 0f, 0.001f);
		((Component)val).gameObject.AddComponent<PipelineCanvasDrag>().View = this;
		((Component)val).gameObject.AddComponent<PipelineCanvasZoom>().View = this;
		((Component)val).gameObject.AddComponent<PipelineCanvasClick>().View = this;
		((Component)val).gameObject.AddComponent<PipelineCanvasMarquee>().View = this;
		content = NewRect("Content", (Transform)(object)viewport);   // 中心锚点 + 中心枢轴
		RectTransform val2 = NewRect("Edges", (Transform)(object)content);
		val2.sizeDelta = new Vector2(20000f, 20000f);
		edgeLayer = ((Component)val2).gameObject.AddComponent<PipelineEdgeGraphic>();
		((Graphic)edgeLayer).raycastTarget = false;
```

### 模式 4：把整份文档塞进原版说明书槽

**要点**：`MachineHelper.NotePanel(GameSlotInventory)` 建槽，内容是 `DirectoryMaster.Item("postit", true)` 改造成 `DOCUMENT` + `MACHINE_MANUAL_TAG` + `unitValue=0`，文档正文放 `shortDescription`，用 `UncheckedAccept` 放入，在 `openCallback` 与 `OnGameLoadedLate` 两处 `Ensure` 补发。

```csharp
// PipelineCore.cs:1081-1098（补发；原函数外层有 try/catch 打 Warn，此处略）
	internal static void EnsureManual(GameItem core)
	{
		if (core == null || !Instances.TryGetValue(core, out var value) || value.Note == null || value.Note.childItem != null)
		{
			return;   // 槽已占用就不动它（玩家自己塞了别的东西也尊重）
		}
		GameItem val = CreateManual();
		((GameInventory)value.Note).UncheckedAccept(val);
		((GameInventory)value.Note).Validate();
		PipelineLog.Info("[Pipeline] manual inserted: " +
			((value.Note.childItem != null) ? value.Note.childItem.identifier : "FAILED"));
	}
```
```csharp
// PipelineCore.cs:1162-1170（物品改造，正文略）
		GameItem val = DirectoryMaster.Item("postit", true);
		val.name = Text("core_manual_name", "流水线控制器 · 说明书");
		val.shortDescription = shortDescription;   // 1161 行：整份说明书
		val.flavorText = "";
		val.SetGameItemType("DOCUMENT");
		val.EnableTag("IMPORTANT_TAG", false);
		val.EnableTag("MACHINE_MANUAL_TAG", false);
		val.unitValue = 0L;
```

---

## 7. 对我们的可用点

### 7.1 《深空电玩》`deepspace_arcade`（DeepSpaceArcade.dll）

1. **`GameItemFunc` 六委托接管 = 零 Harmony 的"物品组合"入口**。要给街机做"把两张卡拖到一起合成/把代币拖到机器上投币"，直接填 `may/can/onThisTargetItemFunc`（§4.2），比 Patch 原版方法干净得多，也不会和别人冲突。
2. **模式 2 的预算调度器**可原样搬来做"整晚营业的自动演出循环"：3 轮 + 450ms + 零进展提前退出，是"我不能卡住玩家 1 秒"这个约束下的标准解法。
3. **模式 3 的画布**可以直接做成"关卡/节目节点编排"界面：三层结构 + 每个手势一个 50 行小组件 + 24 段贝塞尔，是这套 UI 里最值得整段抄的部分（`PipelineRoundedGraphic`/`PipelineEdgeGraphic`/`PipelineSolidGraphic` 三个自绘 Graphic 与业务完全解耦，可以逐文件复制）。**模式 1 的"位置编码 + `?k=v` 尾巴 + 版本前缀只读冻结"** 则适合任何要长期演进的物品配置（街机的高分榜/机台设置）。

### 7.2 《深空当铺：可能是群友》`freya_cornell` / `DeepSpacePawn.dll`（最直接受益方）

1. **说明书槽范式（§5）几乎可以立刻用**：任何"需要有说明书的机器/柜台"都能靠 `MachineHelper.NotePanel` + `postit` + `MACHINE_MANUAL_TAG` + `openCallback` 补发，得到"买回去打开就有说明书、丢了自动补"的效果，而且正文可以写很长（`shortDescription` 是长文本字段）。当铺题材尤其契合：**典当合同、操作手册、审计须知都可以这么做**。
2. **多种液体按比例投料的算法**（`PipelineRules.StepNeed / TotalToMove / ClampToFree / ExceedsFraction`，[PipelineRules.cs:144-188](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineRules.cs#L144)）是纯函数、零依赖，要做"按比例配料"的东西可以直接抄这五个函数。
3. **`PipelineAccept` 的容量预估范式**（Grid 用 `HasFreeCell`、Slot 用 `childItem==null`、外加目标自己的 `mayInventoryAddItemFunc`）比"直接 TryAccept 看返回值"更早地知道"能不能放、能放几件"，适合当铺里"自动上架/自动装箱"。
4. **过滤器 `PipelineRules.CanRemove` + `CartridgePoreSize`** 是对原版"孔径 vs 液体颗粒大小"的显式建模（[PipelineRules.cs:76-115](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineRules.cs#L76)），如果我们要做"净水/提纯"类玩法，这张表比读原版源码更省事（但**注意这是它自己的复刻表，需与真机核对**，见文末）。

### 7.3 两条工程结论（负面教训同样值钱）

- **不要为了"能接管原版机器"就写 `onOneSecondItemFunc` 的 Patch。** 这个模组证明了另一条路：只读面板结构 + 只搬物品 + 需要时模拟按钮，就足够串起一条产线，而且天然不与原版逻辑打架（原版机器照常自己跑，流水线只是替玩家做搬运工）。反面教训同时成立：**别用 `Get-Content` 读这批反编译文件判编码**——这批 `.cs` 是无 BOM UTF-8，用 Windows PowerShell 默认 ANSI 读会得到 `姘撮緳澶?` 这类乱码（本文引用文字都用 UTF-8 读取核对过）；做蒸馏脚本时务必显式指定 UTF-8。

---

## 附：未验证 / 推测项

1. **（推测）** `PipelinePickerBlocksClicksPatch` 的 `[HarmonyPatch(/*Could not decode attribute arguments.*/)]` —— 反编译器没能解出特性参数，目标方法与参数类型未知。功能上看是"点选模式下拦截普通点击"，具体打在哪个方法需要 dnSpy 复核。
2. **（推测）** 公开说明里提到的"蒸发器 / 水培等特殊节点"：源码里**没有** `Role` 级别的蒸发器/水培节点类型。它是把这两类机器当普通 `Role 0` 设备处理，另加语义补丁：`IsEvaporator(machine)` 决定"要不要等蒸干才送"（`CondType.Wet` + `EvaporativeUl`，[PipelineCore.cs:3101-3116](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L3101)），水培靠 `HydroponicHelper.GetPlant / GetHarvestItemId / SeedYieldMultiplier` + 一个 `FinishGrowing` Prefix 来抢产物（§4.1）。所以"特殊节点"更准确的说法是"特殊机器的特判"。
3. **（推测）** `PipelineRules.CartridgePoreSize`（`kotton_fabric=150 / water_filter=22 / hydroreed_filter=22`）与 `LiquidInfo.ParticleSize/VolumeRemoved` 是模组自己维护的数值表，由 `SetLiquid` 在运行时填充（`EnsureRulesLoaded`，[PipelineCore.cs:881](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L881)）；未逐项与原版液体定义核对。
4. **未做真机验证**：本文全部结论来自静态源码阅读，没有进游戏跑过。§1.5 的"1 秒一次"来自 `PipelineTicker` 的常量，实际手感（尤其 450ms 预算被多控制器分摊时）需要真机确认。另外 `TryRunRecipe`/`TryRunPurify`/`TryFillProductBottles` 等约 20 个 `Role-0` 专用动作函数只做了签名级浏览，养分配方/化学成型的细节若要用，需再精读 [3411](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L3411)、[3515](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L3515)、[5297](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L5297)、[8955](缓存文件夹/decomp_ws/3845199370/PipelineController/PipelineCore.cs#L8955) 附近 4 段。
