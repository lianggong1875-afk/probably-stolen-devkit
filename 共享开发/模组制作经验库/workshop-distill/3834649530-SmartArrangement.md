# 智能排列 · 模组（Smart Arrangement · Modules）蒸馏档案

- 工坊 id：`3834649530`　命名空间：`PowerLayout`　模组 id：`yu_module_sort`　入口：`ModEntry : IMod`（`ModEntry.cs:7`）
- 源码：`缓存文件夹\decomp_ws\3834649530`（51 个 `.cs`，约 2.4 万行）；主力文件：`PlannerWindow.cs` 11775 行、`Solver.cs` 4020、`ModuleBay.cs` 1568、`MachineModuleValues.cs` 834、`ModuleObjective.cs` 645、`ModulePrioritySettings.cs` 548、`Rating.cs` 533、`ModulePanelEntry.cs` 476
- Harmony 补丁 **13 个**，全部 `Prefix/Postfix`，无 Transpiler；其中 8 个打在 `ItemContextHandler`（含 `TryOpenSortMenu` 双补丁），另外 5 个是 `MachineHelper.SetupModuleBay`、`MachineBlender.SetupChemModuleGrid`、`MachineEvaporator.Evaporator`、`ModuleHelper.ComputeModuleEffect`、`InputActionManager.UpdateKeys`
- 凡属代码里看不到、只能由行为反推的，标**（推测）**

## 0. 一句话本质

**它把"机器模组舱"抽象成一个 ≤64 格的位图棋盘 + 一堆可枚举落点的方块，然后用"字典序目标 + 模拟退火势函数"在棋盘上搜摆放，最后用"四槽候选池"把不同侧重的解并排给你选。**

求解器本体（`Solver`）完全不知道 UI 存在：输入 `byte[] board` + `Piece[] pieces` + `ModuleObjective`，输出 `Plan`（每件物品的落点 `Cells[i]` 与姿态 `Forms[i]` + 前后两份 `Rating`）。UI（`PlannerWindow`）只负责攒输入、抽帧喂 `Step()`、把 `Plan` 渲染成卡、以及把 `Plan` 翻译成一串带回滚的物品移动。

| 文件 | 行 | 职责 |
| --- | --- | --- |
| `PlannerWindow.cs` | 11775 | 窗口/协程/输入采集/装机事务/历史/预设/帮助（把 solver、bay、UI、move 全串起来） |
| `Solver.cs` | 4020 | **算法全部**：落点枚举、位图合法性、退火、精确 DFS、剪枝、评分、预留形状打包 |
| `ModuleBay.cs` | 1568 | **原版读取层**：模组舱识别、模组词条/属性/机器读数、刷新 |
| `MachineModuleValues.cs` | 834 | 机器侧"加成→机器效果"的换算表（按 `MachinePerformance`/`MachineQuality` 分派各自的公式） |
| `ModuleObjective.cs` | 645 | 目标/上下限/优先级 → `Rating` 的打分器 |
| `Rating.cs` | 533 | 评分结构 + **字典序比较器** + 势函数 `SearchValue` |
| `ModulePriorityCards.cs` | 298 | 四张方案卡的候选池、去重、支配、合并 |
| `ModuleBackgroundSearch.cs` / `PowerBackgroundSearch.cs` | 378 / 242 | 多核后台求解（每线程一个 `Solver`，靠 mailbox 交换方案） |
| `ModuleTargetRange/Draft/Unit`、`ModulePrioritySettings` | 117/184/7/548 | 上下限模型 + 设置 JSON 的版本化读写 |
| `Piece.cs` / `Plan.cs` / `ModuleTraits.cs` / `ModuleStats.cs` | 141/63/216/88 | 数据模型 |

## 1. 问题建模

### 1.1 棋盘：`byte[]` 一次，`ulong[]` 位图常驻

- 舱体 = `GameGridInventory`（`inventory.inventoryShape` 是 `GridShape`）。`PlannerWindow.cs:1510-1514` 把它摊成 `byte[] boardCells`：**能放的第 = 1，不能放（形状外的空洞/被锁定格）= 0**。
- `Solver` 构造时转成 `ulong[]` 位图：`words = (width*height+63)/64`（`Solver.cs:316`），并把整块可放区做成 `boardMask`，另预计算 `notLeft` / `notRight` 两张边界掩码（`317-334`）——后者是给"只有 1 个 ulong 的小图"做快速越界判断用的。
- **超过 1 个 ulong（>64 格）时大量优化自动降级**：`CountFreeCells` / `FindReservedCells` / 精确 DFS 都直接 `if (words != 1) return 0/空`（`3941-3946`、`3186` 的前置 `words == 1` 判断）。这就是文档里"预留形状支持最多 64 格模组舱"（`PlannerWindow.cs:1461-1465`）的来源。

### 1.2 格子/形状/姿态怎么表示

原版形状是字符串，读出来是 `GridShape`（`minX/minY/globalWidth/globalHeight/orientation/flipped` + `Get(x,y)`）：

| 层 | 表示 | 代码 |
| --- | --- | --- |
| 原版 | `item.modifiedShape`（字符串）→ `new GridShapeBuilder(str).shape` | `PlannerWindow.cs:1530`、`3117-3130` |
| 模组（第一次） | `byte[] Shape`：`0`=空、`1`=占格；**`>1` = 电力设备的分区标记** | `Piece.cs:31`、`Solver.cs:599-620` |
| 模组（搜索时） | `Position{ Cell, Form, X, Y, Width, Height, Rectangle, ulong[] Shape, ulong[] Border, byte[] Types }` | `Solver.cs:12-33` |
| 姿态 | `Piece.Rotations[]`：`CreateForms` 用 `SetOrientation(0..3) × SetFlipped` 最多试 **8 种**（`limit=8`），去重后存进 `Piece.Rotations`，`Form=0` 是原始姿态 | `PlannerWindow.cs:3080-3115`、`1591-1600` |

- `border` 掩码是关键：`CreatePosition` 在标记占格的同时，把**四邻接的可放格**记进 `Border`（`Solver.cs:622-637`）。相邻判定于是退化为 `Intersects(a.Border, b.Shape)`（`661-691`），不再逐格扫——位置词条（贴顶/贴左/接收器/负反馈/支撑）全靠这张表。
- L 形/凸形/五格都是"同一套 `byte[]` 掩码"，没有专门类型；`Rectangle` 只是一个 `Array.TrueForAll(shape != 0)` 的快路径标志（`Solver.cs:595`）。
- **镜像**：`CreateForms` 里 `j==1` 走 `SetFlipped(!flipped)`；`HasDistinctMirror`（`PlannerWindow.cs:3046-3050`）判掉"镜像后形状一样"的情况，避免把对称件当两种姿态重复枚举。0.32.0 起支持镜像姿态（changelog 自述：`PlannerWindow.cs:3948`）。

### 1.3 效率/性能/质量与"位置词条"从原版哪读

全部集中在 `ModuleBay.Read`（`ModuleBay.cs:812-851`），**只读标签，不读名字**：

| 读什么 | 原版标签 | 行 |
| --- | --- | --- |
| 三类基础加成 | `BONUS_PERCENTAGE_PERFORMANCE_INT` / `_EFFICIENCY_INT` / `_QUALITY_INT` | `847` |
| 词条 1/2 | `MODULE_EFFECT1_TAG` / `MODULE_EFFECT2_TAG`（**字符串值**，如 `MODULE_EFFECT_TOP_MOUNT`） | `635-644` |
| 类别 | `MODULE_TYPE`（`MODULE_TYPE_UNIVERSAL` / `_CHEM` / `_EVAPORATOR`） | `508-521`、`121-133` |
| 等级 | `MODULE_TIER_INT` | `838` |
| 自学算法上限 | `LEARNING_ALGO_MAX_PERFORMANCE_INT` / `_EFFICIENCY_INT` / `_QUALITY_INT` | `843`、`584-591` |
| 节点 | `IsGameItemType("NODE")` 或 `IsTag("NODE")` | `426-433` |
| 当前**实时**贡献 | `TEMP_PERCENTAGE_PERFORMANCE_INT` / `_EFFICIENCY_INT` / `_QUALITY_INT` | `1229-1232` |
| 机器**总**加成 | `TOTAL_PERCENTAGE_PERFORMANCE_BONUS_INT` / `TOTAL_PERCENTAGE_POWER_BONUS_INT` / `TOTAL_PERCENTAGE_QUALITY_BONUS_INT` | `1254-1257` |
| 模组舱/机器 | `STANDARD_MACHINE_TAG`、`MACHINE` 类型、`evaporator`、`NO_HOPPER_INVENTORY_TAG` | `121-169`、`346-363` |

**位置词条的语义**（`Solver.cs:3840-3873`，`ModuleTraits.cs:21-41`）：

| 词条 | 判定 | 效果 |
| --- | --- | --- |
| `MODULE_EFFECT_RECEIVER` | 邻接 `NodeTag` 模组个数 `num5` | `base × num5 × ReceiverPercent/100`（默认 10%，工坊件 `5+5×level`） |
| `MODULE_EFFECT_TOP_MOUNT` | `position.Cell / width == 0`（首行） | `+base × TopPercent/100`（20%，工坊 `20+10×level`） |
| `MODULE_EFFECT_SIDE_MOUNT` | `position.Cell % width == 0`（首列） | `+base × LeftPercent/100`（同上） |
| `MODULE_EFFECT_NEGATIVE_FEEDBACK` | 邻接模组的**负属性**求和 | `+负属性 × FeedbackPercent/100`（25%，工坊 `35-10×level`） |
| `NODE_EFFECT_SUPPORT` | 邻接"施主"（`Donor`=物品类型 `MODULE`）属性求和 | `施主属性 × SupportLayers/5` 或 × `SupportPercent/100`；`AdditiveSupport` 决定是否叠加自身 |

→ **"贴顶/贴左"就是行号/列号，没有任何几何距离计算**；"接收器/负反馈/支撑"靠 `Border` 与占格的交集。

### 1.4 目标函数：两层，别混

**第一层 —— 排序/择优：字典序，达标是硬约束。**

- 每个设了上下限的属性产出一个 `ModuleGoalFit{ Missed, Shortfall }`（`ModuleGoalFit.cs:1-20`），比较器**先比 Missed 再比 Shortfall**（`11-19`）。
- 三个属性按滑块权重排名 `goalRanks[3]`（`ModuleObjective.cs:327-343`，权重相同则同档），三个槽 `FirstGoal/SecondGoal/ThirdGoal` 分别累加，`Rating.BetterThan` 严格按 1→2→3 字典序比（`Rating.cs:302-316`），之后才轮到 `ModuleAllocation`（加权收益）、`Points`、`ModuleExcess`、`FreeSpan`、`Room`。
- **硬约束集中在 `Rating.Valid`**（`Rating.cs:169-183`）：`Collisions==0 && ExcessNodes==0 && !StalledWorkProgress && (!RoomRequired || KeepsRoom)`，垃圾佬模式再加 `UnknownModulePrices==0`。`Valid==false` 一律 `BetterThan → false`（`288-292`），即"违规解永远不选"。
- **节点数上限**就是这么实现的：`score.ExcessNodes = max(0, NodeModulesUsed - MaxNodes)`（`ModuleObjective.cs:469`）——不是拒绝生成，而是生成了判无效。

**第二层 —— 搜索用势函数（可导、可比较大小）。**

- `ModuleSearchPreference = 收益 − 1000×(16×First.SearchCost + 4×Second.SearchCost + Third.SearchCost)`（`ModuleObjective.cs:635-643`），其中 `SearchCost = Missed + 0.25×Shortfall/(1+Shortfall)`（`ModuleGoalFit.cs:9`）——**16/4/1 的权重就是"第一目标压倒性优先"的连续化写法**。
- 真正喂给退火的是 `Rating.SearchValue(progress, broad)`（`Rating.cs:521-532`）：违规直接 `-10000×(Collisions+ExcessNodes)`，模块模式下再减 `StalledWorkProgress?10000:0`，末尾 `+0.01×FreeSpan` 打破平台。
- `broad` 每 3 轮翻一次（`Solver.cs:2347`）：`broad` 时惩罚更平（`17×Missing + 2.1×Shortage + 9×LowLost`），窄时更陡（`25/0.65/38`）——**轮流用两套地形爬山，减少卡在同一个局部最优**。

### 1.5 输入侧：`Piece` 的语义位

`Piece`（`Piece.cs:5-47`）同时描述"电力摆放"和"模组摆放"，靠几个布尔位：

| 位 | 含义 | 赋值处 |
| --- | --- | --- |
| `Optional` | 不在本舱内（来自库存/储藏区），可以被"放进空位"或"不放" | `PlannerWindow.cs:1546` |
| `Fixed` | 不可移动：物品被锚定/锁定、`MODULE_STUCK_TAG`、非 `Counted` 模组、"保护已存在模组"开关 | `1547`、`1296` |
| `ForcedOut` | 被筛掉的机内模组：强制移到 `cell=-1`（换出） | `1563`、`483-492` |
| `Removable` | 可移出到库存（垃圾佬/低配排列时参与"减件"） | `1552` |
| `Filler` | 电力模式下的填充物（非供电非受电） | `1548` |
| `ReserveOnly` | 仅作预留（电力） | `1552` |

## 2. 搜索算法

### 2.1 管线（`BeginInSlices`，`Solver.cs:2060-2282`）

```
① BuildOptions        预枚举每件物品的合法落点(全位置×全姿态)，按形状去重共享
② PolishAssignments   电力模式：先贪心铺一遍(仅电力)
③ SearchExact / SearchPowerExactInSlices   仅 words==1：2e6 节点/40ms 上限的完全 DFS
④ FillIdleSlots       从库存补模组（贪心种子：逐件试"第一个能放的落点"→Evaluate 排序→取前 2 真填）
⑤ TrimModuleStock     垃圾佬：逐个试"拿掉它还能达标吗"，能就减（3111-3141）
⑥ AnnealPass × N      模拟退火主循环，每轮 4500 次迭代，10 轮轮转 4 类种子
⑦ PolishModuleLayout  节点局部修复（40ms / 2048 次评估预算，2575-2820）
⑧ PublishBest         节流发布 + 卡片池/组合池收集
```

- **种子轮转**（`2358-2367`）：`num2 % 5` 决定这一轮从哪儿起步——`ordinaryModuleSeed`（普通模组优先）/ `runBest`（当前最优）/ `flagshipModuleSeed`（旗舰全填）。三档交替，等于"多起点重启"。
- **邻域动作**（`2434-2511`）：
  - 5% 概率**交换**两个物品的落点（`choiceByCell` 直接查表，`2439-2460`）；
  - 25%（模块）**邻接微移**：`PickAdjacentOption` 以 7 分支给"旋转 / 上 / 下 / 左 / 右 / 再旋转 / 放弃"（`759-777`）；
  - 其余 **自由落点**：随机试 8 个候选，第一个不重叠的就用（`744-757`）。
- **接受准则**（`2519`）：`BetterThan` 直接接受；否则 `exp(Δ/T)` 的 Metropolis 接受。`Δ = SearchValue` 的差。
- **温度**：`T = base × 0.035^progress`，`progress = k/4500`，模块 `base=112`、电力 `55~190`（按图大小与 broad 选，`2412-2414`）。

### 2.2 搜索空间怎么被限制住

| 开关 | 值 | 位置 |
| --- | --- | --- |
| 精确 DFS 节点 | `ExactNodeBudget = 2_000_000` | `Solver.cs:117`、`3202` |
| 精确 DFS 墙钟 | `ExactMilliseconds = 40`（每 1024 节点查一次） | `119`、`3206-3213` |
| 精确 DFS 前提 | `words == 1`（≤64 格）且非 seed 复用 | `2165` |
| 单轮退火迭代 | 4500 次 | `2391` |
| 退火切片 | 每 8 次迭代查一次表，`sliceMilliseconds` 默认 5ms | `2393`、`2323` |
| 收敛判据 | `SearchWorkMilliseconds - runLastChange >= 3000` → `Done` | `2549-2558` |
| 发布节流 | 模块 280ms / 电力 1000ms 一次；组合池 750ms | `2307`、`1977` |
| 备用方案池 | 组合候选 6 个、展示过的组合 64 个、预览备选 48 个 | `1959`、`2003`、`ModuleBackgroundSearch.cs:199` |
| 卡片池 | 64（超出后按价格/用件/库存保留三种维度各留 4） | `ModulePriorityCards.cs:255-281` |
| 预留形状 | 每形状 131072 次尝试、每条 fence 8192 次、评分 8 次即止、方案 ≤4、舱 ≤64 格 | `Solver.cs:1456-1493`、`PlannerWindow.cs:1461` |
| 帧预算 | 一帧最多 32 次 `Step`，每次切片 5ms，累计 8ms 就 `yield` | `PlannerWindow.cs:2173-2209` |
| 整体时限 | 非穷举 20s 硬停 | `PlannerWindow.cs:2168`、`2217` |
| 后台总时限 | 持续计算关：20000ms | `ModuleBackgroundSearch.cs:325` |

### 2.3 "持续计算"怎么做的：三层可中断

1. **迭代器即状态机**。`AnnealPass` / `PolishModuleLayout` / `FillIdleSlotsInSlices` / `PackInstalledForRoom` 全是 `IEnumerable<bool>`；`Solver` 把当前轮的枚举器存在字段 `runPassWork` 里，`Step()` 每次只 `MoveNext()` 一步（`2311-2336`）。切片点靠 `stepSliceMilliseconds` 字段跨层传播——所以"5ms 切片"能穿透到最里层的递归打包。
2. **协程驱动**。`PlannerWindow` 用 `StartCoroutine` + `StepSolvers` 循环 `yield return null`，每次 `solver.Step(Math.Min(5, 剩余帧预算))`（`2188-2221`），并用 `Time.realtimeSinceStartup` 算 8ms 帧预算。
3. **真后台线程**。`ModHelper.RunInBackground(modId, Action<CancellationToken>, Action)`（`PlannerWindow.cs:2246`）把 `ModuleBackgroundSearch.Execute(worker, token)` 丢到线程池；`ModuleBackgroundSearch` 内部**每个方向一个 `Solver`**，`WorkerCount = max(1, min(min(4, 处理器数-1), 方向数))`（`135`），第一个 `Solver` 负责枚举落点（`BuildOptions`），其余通过构造函数把 `sharedPlacementIndex` 复用过去（**几何只算一次**，`Solver.cs:343-368` 的 `sharedModuleOptions` 校验）。
   - 解交换：每轮 `Receive()` 从 mailbox 取别的方向的最新方案，`solver.OfferSharedModulePlan(plan)` 塞进自己（`ModuleBackgroundSearch.cs:236-254`、`Solver.cs:1761`）——**共享"排布"，各自评分**，这正是 0.35.1 changelog 说的"各方向共享找到的排布，独立评分"。
   - 主线程只 `Drain()`（`217-234`）取增量 `Plan`，从不阻塞等锁。
   - 前 1 个 worker 就绪前不启动其余（`PlannerWindow.cs:2287-2308`），保证 bootstrap 快照一致。

## 3. 形状与占用

### 3.1 合法性检查（三层，从便宜到贵）

1. **单件落点**：`CreatePosition` 在构造期就把所有越界/压到 `board==0` 的位置丢掉（`Solver.cs:613-616`）——搜索时不存在非法单件。
2. **两件重叠**：`ulong[]` 与运算（`644-659`）。整盘碰撞数就是 `Evaluate` 里的 `powerOccupancy & position.Shape` 计数（`954-961`），模块模式是 O(n²) 两两 `Intersects`（`3774-3781`）——因为模块舱 n 很小（≤40 件）。
3. **整盘一致性复核**：`Piece.CellsConsistent`（`Piece.cs:86-140`）给出 `filled/free/overlaps/invalid` 四元组，用于应用前的人类可读校验。

### 3.2 "预留模组形状"（保住已达标属性的空位）

这是全模组最精巧的一块：`reserveRoom` **与"使用空闲模组"互斥**（`PlannerWindow.cs:1460`、`flag2`），只重排机内现有模组，目的＝**在舱里留出一块指定形状的空地**（给以后的扩容/新模组），同时不破坏已达标属性。

- 形状库只有 **7 个**，硬编码成 ASCII 行（`Solver.cs:231-240`）：`双格 "11"`、`小 L "10/11"`、`2×2`、`凸形 "111/010"`、`大 L "10/10/11"`、`大 b "10/11/11"`、`倒扣锅 "111/101"`。
- 每个形状在 `PrepareRoom`（`3374-3507`）里枚举**所有落点 footprint**（`List<ulong> Footprints`），并按面积降序 / `2×2` 优先排（`1414-1417`）。若已用掩码能证明"连 2×2 都塞不下"就置 `SquareRoomProvenImpossible`（`1413`、`1298-1310`），后续干脆不试方块。
- 主算法 `PackInstalledForRoom`（`1394-1624`）：把每个 footprint 当 **fence（不可侵占区）**，对"可移动且非 Fixed"的模组做 **DFS 装箱**：
  - 每一层取 `boardMask & ~(occupied | reserved)` 的**最低位空格**为锚点（`1536-1541`），只试"覆盖这个格子"的落点（`choicesAt[cell]` 预索引，`1418-1444`）——经典 exact-cover 位序技巧，天然去重、不重复枚举排列。
  - 允许 `spare` 次"故意空出一格"（`1559-1565`），这样能拼出非紧密的留空形状。
  - 每凑满一个合法布局就 `EvaluateModules` 复核：`rating.Valid` + `AllowsRoomReservation(before, rating)`（**不允许为了留空破坏任何一个已达标目标**，`ModuleObjective.cs:96-116`）+ 进度预算。
  - **产出上限 4 个方案**（`usedLayouts.Count == 4` 就停，`1456`），每张卡还能标"同一块空地也可改放哪些 5 格形状"（`AlternativeFiveCellRooms`，`1594-1596`）。
- 达标保护是**双向**的：`Rating.KeepsRoom` 由 `FindReservedCells` 反算（`3933-3937`、`3957-3999`），`ChooseRoomFootprints` 用回溯找一组互不重叠的 footprint（`4001-4019`）；`RoomRequired` 一旦为真且 `!KeepsRoom`，`Valid` 直接 false（`Rating.cs:173`）。

## 4. 结果比较与呈现

### 4.1 四槽候选池 `ModulePriorityCards`

- **方向集合**（`Configure`，`49-66`）：`{-1（优先度综合）} ∪ 有目标/启用的属性索引`。每个方向 = 同一份 `ModuleObjective` 加 `WithPriorityTarget(i)`（把该属性权重顶到 `max+1`，`ModuleObjective.cs:265-282`），于是"同一批排布、换评分尺"就能得到不同侧重的解。
- **入池**（`Accept`，`174-215`）：`Valid` + 方向匹配 → 与池中同布局/同三属性者比 `PreferEquivalent`（`113-134`：先比 `BetterAfterModuleCleanup`，再比总价、用件、`ReservedCells` 数）→ 优则替换、劣则丢弃。
- **出卡**（`Rebuild`，`217-288`）：
  1. 每个方向各挑自己的最优 → 该方案若同时是多个方向的最优，**合并成一张卡**（`winners[plan].Add(direction)`，`238`）；
  2. 剩下的按**帕累托支配**过滤 `Dominates`（`136-172`：任一分向更差即放弃；同时要求总价不更贵、用件不多、库存不更"贵"才算支配）；
  3. 再按优先度方向排序，补到 **4 张**为止（`243-254`），不够就留空位（"没有值得展示的不同方案时保留空位"）。
- **去重口径**：`SameLayout`（每件 `Cells` + `Forms` 全等）或 `SameDisplayedModuleAttributes`（三属性全等）都算重复（`88-111`）。这就是 0.35.4 changelog 说的"四张卡按效率、性能、质量数值去重；三项相同，即使摆法或价格不同，也不新增或替换卡片"。

### 4.2 卡片文案与差异

| 元素 | 逻辑 | 行 |
| --- | --- | --- |
| 标题行 1 | `当前最佳` / `取舍备选` / `预留方案` / `暂无不同方案` | `6737-6752` |
| 标题行 2 | 命中方向（`效率·性能`）/ `预留：小 L（也可改放：…）` / `属性取舍` | `6754-6787` |
| 徽标 | `CardBadges(plan)`（一段多行摘要） | `6694` |
| 主勾 | `ModuleGoalSummary`：`达标 2/3`、`降档达标 …（非原目标）`、`（部分未核验）`、`未设目标`、`预期最大属性 · …` | `6904-6925` |
| 价格 | `模组总价 {N0}` 或 `模组价格未知` | `6895-6902` |
| 悬停差异 | 相对"参考卡"（优先度方向的最优）：`效率 +12，性能 -5，质量 +0；用件 +1`、`总价差 +340` | `6868-6893` |
| 备选称谓 | `省钱备选` / `少件备选` / `保留库存备选` / `取舍备选` | `6845-6866` |

### 4.3 UI 技术形态

- 全是原版 `CustomUIManager`：`CustomUIManager.Instance.CreateWindow(id, title, "paper", null, Style).SetSize(1140,838).SetDraggable(true)`（`4049`）→ `CustomUIBuilder` 的 `AddLabel/AddToggle/AddButton/AddImage/AddInputField/BeginRow/BeginPanel/AddSeparator`，**每个控件带字符串 id**（`"planner.xxx"`）供后续 `GetElement(id).SetText(...)` 局部刷新，不重建整窗（`7682-7730`）。
- 卡片结构固定 6 段（`BuildFrameworkCandidates`，`6651-6725`）：标题(28px) → 副标题(24) → 徽标(48) → 缩略棋盘图(`BuildFrameworkBoard` 生成的 `Sprite`，252×158) + 悬停查模组 → **数据区 3 行 × 4 列** → `选择方案/已应用` 按钮。四卡等高、标题两行、`enableAutoSizing` + `fontSizeMin=12` 防溢出（`6789-6811`）。
- 数据区（`BuildCardData`，`6590-6650`）与左侧 LCD（`BuildFrameworkLcd`，`5644-5723`）同构：每行 = `属性名 | 模组加成% | 机器效果名 | 方案读数`。机器效果名由 `machineValues.EffectLabel(i)` 给出（读机器说明书文本，见 `MachineModuleValues.cs:72-127`、`1182-1200`），读数由 `DescribePlanValue` → `FormatMachineAmount` 带单位（`毫升/天`、`个/轮`、`天/日`、`%/天`）。
- 源不可信时降级而非崩：`Report(code, msg, ex)` 把机器/网格/候选数/权重/目标一大串塞进日志并弹 toast（`3151-3180`）；`AdvancePowerSearch` 捕获协程异常后 `StopSearch()`（`2140-2154`）。

## 5. 与原版的交互

### 5.1 如何被打开

- 补丁 `ItemContextHandler.TryOpenSortMenu` 的 **Prefix**（`OpenSortMenuPatch.cs:7-41`）：先 `SortMenuRow.Hide` 收起原版菜单 → `RenderHandler.RaycastElement<GameGridInventory>(pos)` 拿指着的格子 → `ModuleBay.GetMachine(grid)` 判定是不是模组舱 → 满足 `!InventorySortHelper.MaySort || (!IsInsertLocked && GeneralHelper.MayPlayerInsertInto)` 就 `active.Open(grid)` 并 `__result=true; return false`（吃掉原版排序菜单）。
- 机器窗口里另有"排列"按钮：`ModulePanelEntry` 往 `MachineryPanelRow` 插 `MachineryPanel + MachineryButton`，并用 `ModuleLayoutRequests`（反射 `requestedWidth/requestedHeight`）在插入期间临时改布局、关闭时 `Restore()`（`ModuleLayoutRequests.cs:20-159`）。
- 调试入口 `ModHelper.AddDebugFunction("yu_module_sort", "Special", "ModuleSort", ModulePanelEntry.OpenFromDebugMenu, false)`（`ModEntry.cs:37`）。

### 5.2 真正装进机器：**不用 `GraphUtils`，用四阶段事务 + 回滚**

`MoveTo(moves, undo, restoring)`（`PlannerWindow.cs:11540-11709`）刻意绕开 `GraphUtils.CanAccept/TryAccept`，因为它要**同时移动十几件物品并保证整体可回滚**，而 `TryAccept` 是单件判定：

1. **全部先取出**：`parentInventory.Expel(item)`，失败即抛（`11545-11576`）。
2. **全部写形状**：`item.modifiedShape = new GridShapeBuilder(target.Shape).shape`（`11578-11590`）。
3. **全部再放入**：`((GameInventory)grid).UncheckedAccept(item)`，返回 false 即抛（`11592-11606`）。
4. **全部验形**：`item.ValidateShapeState()`，再 `ModuleBay.Refresh(inventory, machine)` 重算模组效果，最后对涉及的每个 `GameGridInventory` 调 `Validate()` 刷 UI（`11607-11673`）。

回滚就是 `MoveTo(moves, !undo, restoring: true)`：`restoring` 时每个动作被 `Attempt()` 包住收集异常（`11693-11708`），单件失败走 `RestoreDetachedItem`（`11481-11538`，两轮重试 + 落点复核）；**若连回滚都失败，就 `StopSearch()` + 清空候选 + 报 `PL-05`"部分位置未能恢复，请暂停排布"**（`11329-11340`）——这是全模组唯一允许"把问题升级给人"的地方。

### 5.3 应用前的合法性闸门（13 种拒绝理由）

`FindMoveBlockReason`（`11097-11181`）逐条对照**原版规则**，返回值再经 `MoveRefusalMessage`（`11192-11211`）翻成中文：

| 理由 | 判据（原版 API） |
| --- | --- |
| `snapshot` | `At(item, units, grid, shape)`：位置/数量已变 |
| `item_locked` | `IsAnchored(item)`（`2827`） |
| `stuck` | `item.IsTag("MODULE_STUCK_TAG")` |
| `remove_locked` / `insert_locked` | `grid.IsRemoveLocked()` / `IsInsertLocked()` |
| `target_owner` | `GeneralHelper.MayPlayerInsertInto(grid)` |
| `shape` | `grid.CheckShapePlacement(item, shape, 全部移动件)` |
| `slot` | `grid.MayHaveValidInventorySlot(item)` |
| `capacity` | `item.MaxNumAdd(new SlotMarker{...}) < units` |
| `state` | `grid.CheckAcceptParentShapeStateModifier(item, marker, shape, item.modifiedState)` |
| `counter_target` / `counter_source` | `IsCounterInUse(grid)` + 柜台交易中禁止放回/取走 |

另有**快照一致性**前置：`SnapshotUnchanged() && InputsUnchanged()` 不成立就直接要求重开窗口（`11228-11245`）；应用后再 `MovesAtDestination` 复核"游戏有没有偷偷挪动"（`11306-11309`），以及"每日工作进度必须 > 0"的软保护（`11247-11251`、`11302-11305`）。

### 5.4 所有权/锁定/柜台/换出落点

- **已装模组保护**：`protectExisting` 开启时所有机内模组 `Fixed=true`（`1547` 经 `IsProtected`，`1296`），求解器只让它们在原姿态附近"不动"；`protectDedicated` 只保护专机件（`ModuleTraits.MachineDedicated`，`132-143`）。
- **换出的模组去哪**：`ReserveReturnSlots`（`10817`）先给每件可能被换出的模组预留"回原位"的目标，`CandidateMoves`（`11026`）在装不下原位时**找其他合法空位**（跳过 `IsCounterInUse` 的柜台、跳过 `IsLockedContainer` 的锁定容器，`10998-11024`）；一件也放不下就拒绝整个方案（`11258-11262`）。
- **"可用"判定** `IsAvailableIdleModule`（`2803`）：`parentInventory != null && != inventory && !IsLockedContainer && unitCount == 1 && MayBringInAsModule && !IsAnchored && !MODULE_STUCK_TAG && MayUseIdleOwnership(IsItemEffectivelyOwned) && !IsInsideContainerPlacedForSelling()`。锁定容器靠 `LockHelper.IsLockedContainer/IsCurrentlyLocked`（`2778-2812`）。
- **物品不被"凭空生成"**：求解器里 `Optional` 件的 `Piece` 一直握有 `Entry.Item` 引用（`1617-1626`），应用时移动的是真实 `GameItem`；没有任何 `ItemSpawner.Spawn`。

### 5.5 刷新模组效果（必须做，别自己算）

`ModuleBay.Refresh(grid, machine)`（`1549-1566`）严格按原版顺序：`ModuleEffectHelper.ComputeAllModuleTempStat` → `ModuleHelper.ComputeModuleEffect` → `ModuleHelper.ApplyBasicModuleEffect` → `MachineryHelper.UpdatePowerUsageProcessTypeMachine` → 回调 `OnChanged` → `machine.contentWindow.Validate()`。**之后**才用 `ReadCurrentBonuses` 读回真值；`ContributionDifferences(actual, predicted)`（`1234-1252`）能把"游戏=…% 预测=…% 差=…个百分点"打进日志，用于校准自己的模型。

### 5.6 跨模组：软依赖 + 观测式识别

- **兼容探测**：`ModHelper.IsModRunning("chip_slot_expansion")` / `"local.moduleforge"` / `"sdjybn.betternodes"`（`PlannerWindow.cs:1421-1423`）；各自读自己的标签（`gcc.level.<效果>`、`local.moduleforge.nativeEffects`、`BETTERNODES_LAYER_INT`）。
- **规则注册表**：服务键 `yu_module_sort.rules.v1`，`ModuleRuleProviders.Discover` 遍历 `ModLoader.Mods` → `ModHelper.Lookup<Func<GameItem,GameItem,Dictionary<string,int>>>(modId, key)`（`ModuleRuleProviders.cs:30-53`）。对方只需暴露一个"机器+模组 → `{support.layers, top.percent, …}`"的纯函数；本模组做**严格字段白名单 + 范围校验**，非法即抛并被捕获记一次日志（`55-136`）。
- **第三方模组舱识别**（这是全仓最野的一段）：不认识形如 `yexiaoyang.*` 的设施，就用**空壳物品探针**：`AdmissionSample`（`ModuleBay.cs:29-48`，`base(identifier, null)` + `Validate/Destroy` 空实现）冒充"模组"和"非模组"，调 `grid.mayInventoryAddItemFunc(probe, grid)`，若"接受模组、拒绝非模组"就认定这是模组舱（`253-283`）；再加上 `ObserveSettlement`（`285-327`）在别人结算后记录 `Owner/Receiver/Filter` 三元组。`checkingAdmission` 是 `[ThreadStatic]` 重入闸（`58-59`）。
- **重复加载自检**：`WarnAboutLegacyCopy` 扫描 `AppDomain` 里是否还有同名旧 `PowerLayout.dll`（`ModEntry.cs:43-54`）。

## 6. 可复用模式

### 6.1 位图占格 + 邻接预计算（`Solver.cs:644-659`、`726-733`）

小棋盘（≤64 格）用单个 `ulong` 表达占格，重叠=与运算，邻接=`Border` 掩码，全部 O(1)~O(words)。

```csharp
// Solver.cs:644-659
	private static void Mark(ulong[] mask, int cell)
	{
		mask[cell >> 6] |= (ulong)(1L << cell);
	}

	private bool Intersects(ulong[] a, ulong[] b)
	{
		for (int i = 0; i < words; i++)
		{
			if ((a[i] & b[i]) != 0L)
			{
				return true;
			}
		}
		return false;
	}

// Solver.cs:726-733
	private bool FitsSample(Position at)
	{
		if (at.Cell >= 0)
		{
			return !Intersects(at.Shape, sampleOccupancy);
		}
		return true;
	}
```

配套的 `CreatePosition`（`582-642`）在构造每个落点时顺手写 `Border`（四邻接可放格），于是"贴顶/贴左/相邻接收器/负反馈/支撑"全变成一次掩码与——**位置词条不需要几何库**。

### 6.2 分帧可中断：枚举器当状态机（`Solver.cs:2311-2336`）

`Step()` 只推进一轮迭代器一步；切片长度写进字段 `stepSliceMilliseconds`，于是最里层的递归装箱也会在 5ms 处 `yield`。

```csharp
// Solver.cs:2311-2336
	internal void Step(int sliceMilliseconds = int.MaxValue)
	{
		if (runFinished)
		{
			return;
		}
		stepSliceMilliseconds = sliceMilliseconds;
		activeSearchClock.Start();
		try
		{
			if (runPassWork == null)
			{
				runPassWork = AnnealPass((sliceMilliseconds == int.MaxValue) ? int.MaxValue : 5).GetEnumerator();
			}
			if (!runPassWork.MoveNext())
			{
				runPassWork.Dispose();
				runPassWork = null;
			}
		}
		finally
		{
			activeSearchClock.Stop();
			stepSliceMilliseconds = int.MaxValue;
		}
	}
```

调度侧（`PlannerWindow.cs:2188-2209`）：`int val = Math.Max(1, (int)Math.Floor((0.008f - 已用) * 1000f)); solver.Step(Math.Min(5, val));`，一帧累计 8ms 就 `break` 出去 `yield return null`。

### 6.3 退火：梯度用"势函数"，择优用"字典序"（`Solver.cs:2404-2415`、`2517-2535`）

```csharp
// Solver.cs:2404-2415
			if (k % 64 == 0)
			{
				runCancel.ThrowIfCancellationRequested();
				PublishBest(final: false);
				if (runClock.ElapsedMilliseconds >= runDeadline || runPublished >= runLimit)
				{
					break;
				}
				progress = (double)k / 4500.0;
				double num3 = (modules ? 112 : ((!guidePower) ? (broad ? 190 : 145) : (broad ? 95 : 55)));
				temperature = num3 * Math.Pow(0.035, progress);
			}

// Solver.cs:2517-2535
			Rating rating3 = Evaluate(state);
			double num13 = rating3.SearchValue(progress, broad) - rating.SearchValue(progress, broad);
			if (((modules || rating.Missing == 0) && rating3.BetterFrom(rating, runBefore.Missing)) || num13 >= 0.0 || random.NextDouble() < Math.Exp(num13 / temperature))
			{
				rating = rating3;
				if (rating3.BetterFrom(runBestRating, runBefore.Missing))
				{
					runBestRating = rating3;
					Array.Copy(state, runBest, state.Length);
				}
			}
			else
			{
				state[num5] = num6;
				if (num7 >= 0)
				{
					state[num7] = num8;
				}
			}
```

要点：**接受与否看连续势函数，存档只认可字典序更优的解**——既避免"目标达标/未达标"的悬崖导致退火乱走，又保证最终结果严格满足优先级。

### 6.4 多方案池：方向最优 → 合并 → 帕累托 → 截断（`ModulePriorityCards.cs:241-259`）

```csharp
// ModulePriorityCards.cs:241-259
		List<Plan> list = pool.Where((Plan p) => !pool.Any((Plan q) => p != q && Dominates(q, p))).ToList();
		list.Sort((Plan a, Plan b) => (!scores[a][0].BetterThan(scores[b][0])) ? (scores[b][0].BetterThan(scores[a][0]) ? 1 : 0) : (-1));
		foreach (Plan item2 in list)
		{
			if (visible.Count == 4)
			{
				break;
			}
			if (!visible.Contains(item2))
			{
				visible.Add(item2);
				winners[item2] = new List<int>();
			}
		}
		if (pool.Count <= 64)
		{
			return;
		}
		HashSet<Plan> keep = new HashSet<Plan>(visible);
```

前面的 `Rebuild` 循环（`221-240`）先把"每个评分方向自己的最优"塞进 `visible` 并在 `winners` 里记下它命中了哪些方向（一卡多标），再用支配过滤补齐到 4 张——**"哪些卡"由解空间决定，"最多几张"由 UI 决定**，两者解耦。

## 7. 对我们的可用点

**（a）任何"带约束的自动摆放"都能直接抄这套骨架**
`Piece{Shape, Fixed, Optional, ForcedOut}` + 预枚举落点 + `Rating.Valid` 装硬约束 + 字典序 `BetterThan` + 退火 `SearchValue` + `Step()` 切片。我们做**货架/柜台自动摆放、鉴定台排班、展示柜陈列**都属于同一类问题（矩形非规则占格 + 少数带位置词条的物品），不需要新算法。

**（b）`深空当铺：可能是群友`：批量上架/下架的事务封装最值钱**
`MoveTo` 的四阶段（全取出 → 全写形 → 全放回 → 全验形）+ `restoring` 回滚 + `FindMoveBlockReason` 的 13 条前置闸门，正好补上我们"精确投放到目标容器用 `CanAccept/TryAccept`，校验用 `item.parentInventory == targetInv`"的单件范式在**多件原子操作**上的空白。建议在 `DeepSpacePawn` 抽一个 `ItemMoveBatch`：入参 `(item, units, fromGrid, fromShape, toGrid, toShape)` 列表，出口只有 `bool`，失败自愈后要么全成要么全回滚——**别在玩法代码里逐件 TryAccept**。
同时 `ModuleBay.Refresh` 的顺序（TempStat → ComputeModuleEffect → ApplyBasicModuleEffect → UpdatePowerUsage → Validate）是"改完物品树必须走的一遍"，任何改舱内物品的玩法都该照抄。

**（c）`深空电玩`（deepspace_arcade）：分帧求解 + 四卡对比 UI 双份可复用**
- 街机里"AI 找最优落子/生成关卡/刷可通关的牌型"最适合 `IEnumerable<bool>` + `Step(5ms)` 那套：**任何可能超 16ms 的搜索都不要写成一次算完**，用同一个 `stepSliceMilliseconds` 字段把切片传播到递归最深处，UI 侧一帧 8ms 上限、累计超了就 `yield return null`。
- 四卡对比（方向最优 → 合并 → 支配过滤 → 空位而非硬凑）是**通用"给玩家几个有取舍的选择"**的模板，街机的难度/关卡/道具三选一可以直接套，尤其"允许空位""一卡多标""悬停显示相对差异（属性/件数/价格）"这三条，比强行凑满 4 个更可信。

**（d）工程约定值得照搬**
1. `ModHelper.Lookup(modId, "yu_module_sort.rules.v1")` 的服务注册表 + 严格字段白名单（`ModuleRuleProviders.cs:30-136`）：跨模组协作**不引对方 DLL**，对方暴露纯函数，我们做校验和降级。
2. 设置持久化：`ModData` 存 JSON → `SupportedShape` 白名单 + `prioritySettingsVersion`/`rangeSettingsVersion` 双版本号 + `TrySave` 写前备份 `.bak` 并**读回比对**（`ModulePrioritySettings.cs:496-547`）。我们目前的配置读写还没这么严。
3. 人类可读的拒绝理由：不要把 `false` 返回给玩家，返回**枚举/字符串码 → 本地化文案**（`MoveRefusalMessage`）。日志里同时打 `reason/item/from/to/customer`（`11273-11291`），排障成本极低。
4. 求解器与 UI 彻底解耦（`Solver` 不知道 `CustomUIManager` 存在，`Plan` 是唯一契约），因此同一份求解器既能前台协程跑、也能 `ModHelper.RunInBackground` 多核跑——**先定数据契约，再谈并发**。

---

### 附：一句话记住它的取舍

**它不是"最优解求解器"，而是"在可中断的时间预算里持续改进、并把不同侧重的次优解摆到玩家面前"**——`Exact`/`runConverged`/`Fallback` 三个标志就是它诚实的边界声明（`Plan.cs:37-41`、`Solver.cs:2554-2558`、`PlannerWindow.cs:6830-6837`）。
