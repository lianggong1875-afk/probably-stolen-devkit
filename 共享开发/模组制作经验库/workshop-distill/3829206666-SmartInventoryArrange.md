# 智能仓库排列 / Smart Inventory Arrange 蒸馏档案

- 工坊 id：`3829206666`　DLL：`SmartInventoryArrange.Mono.dll`　命名空间：`SmartInventoryArrange[.Core|.Core.BatchRouting|.Core.RowBandPacking|.Migration.*|.Serialization]`
- 入口：`SmartArrangeMonoMod : IMod`（`SmartArrangeMonoMod.cs:9`），`ModHelper.GetConfig(manifest.ID)` 建配置、`ModHelper.RunInBackground` 跑后台预热
- 源码：`缓存文件夹\decomp_ws\3829206666`，**342 个 `.cs` / 89,187 行（含空行；非空 82,941 行）**
- Harmony：**25 个 Patch 类**，落在 **18 个靶点族**上；含 2 个 Transpiler（都带"调用点计数校验失败就整功能自禁用"）
- 凡代码里看不到、只能由行为反推的标**（推测）**

## 0. 一句话本质

**它把"一个网格容器"读成一张布尔棋盘 + 一堆带若干姿态的方块，用"按密度模式重排的 20 项字典序目标"驱动一个位棋盘（`BigInteger`/`ulong`）搜索引擎，逐件贪心播种 → 束搜索（beam）→ 多引擎竞标；胜出的布局不靠"移出再放回"，而是直接改写每件物品的 `modifiedShape`（位置+朝向）并让游戏 `Validate()` 自己收尾。整套东西跑在"帧内按毫秒预算切片捕获 + 后台线程求解 + 提交前快照比对"的三段流水线上，失败必回滚，撤销并进原版 `Ctrl+Z`。**

工坊简介的"同类成排"，在代码里是 `NegativeTypeRuns`（同类型被切成几段）、`SameTypeRowAlignment`（同类型同高成行）、`SameTypeHorizontalContact`（同类型水平贴边）三项，而不是简单的 `OrderBy(名称)`。

## 1. 侦察结论：三个平行命名空间，其中两个是"冻结引擎"

目录里不是 1 份代码，是 **3 份**：

| 树 | 文件 | 行 | 状态 |
| --- | --- | --- | --- |
| `SmartInventoryArrange*` | 219 | 50,256 | **活的**，全部补丁与 UI 都在这 |
| `SiaCanon63*` | 62 | 16,999 | 被活树引用：`Canonical63Bridge.cs:3` `using SiaCanon63.Core;` |
| `SiaTemplate63*` | 59 | 15,670 | 被活树引用：`TemplateAccounting.cs:3` `using SiaTemplate63.Core;` |

- 把命名空间 token 归一化后做 SHA256：`DeterministicSolver.cs` 三份**逐字节相同**；`Native34Solver.cs` 三份**不同**（11,156 / 10,388 / 10,288 行）。
- 结论：`SiaCanon63` 与 `SiaTemplate63` 是作者**同一求解器不同历史版本的冻结副本**，编进同一个 DLL 当"参考解/证书/计量口径"，不参与常规交互。这是本模组最独特的一条组织手法（见 §6、§8）。
- **读这个模组只需读活树**，另外 32k 行是版本存档。

## 2. 它到底在排列什么

### 2.1 排列对象

- 输入是一个 `GameGridInventory`（`InventoryTransaction.CaptureSteps` 的形参，`InventoryTransaction.cs:835`）：`inventory.inventoryShape` 是 `GridShape`，`width/height/minX/minY` 直接当棋盘尺寸与原点（`:842-846`），`usable[j,i] = val.Get(j+originX,i+originY)==0`（`:853`）。
- 生效范围由 `BatchInventoryPolicy` 决定：**排除展示柜与机器模组舱**。
  - `Allows`：`if (!IsShowcase) return !IsMachineModuleArea; return false;`（`BatchInventoryPolicy.cs:10-17`）
  - 展示柜判据：`EmporiumEntry.Instance.showcaseElement == inventory`（`:85-94`）
  - 机器模组舱判据：沿 `inventory.handler` 的 `TreeNodeRender.parent` 上溯找 `MachineryPanel.role == "module"`；找不到再退回 `MachineHelper.GetModuleInv(parentItem) == inventory`（`:19-61`）
- 也就是说：**仓库箱、储藏舱、柜台容器、背包、机器"输出区以外"的格子容器都排，展示柜与模组舱不排**（模组舱交给同作者的 `3834649530` 智能排列模组，见 §8）。
- 可移动/固定的划分：`!NativeRules.IsAnchored(val3) && val3.MayRemove() && parentInventory==inventory`（`InventoryTransaction.cs:894`）；`IsAnchored` 直接反射原版 `InventorySortHelper.IsAnchored(GameItem)`（`NativeRules.cs:9-13`）。不可动的进 `problem.Fixed`，参与占位但不参与搜索。

### 2.2 目标函数：20 项字典序，且**比较项的先后顺序按密度模式换**

`LayoutScore` 是 20 个 `int` 的 `record struct`（`LayoutScore.cs:5`），`CompareTo` 就是逐项比较、全等才比 `StableTie` 字符串（`:19-27`）。

得分怎么算：`DeterministicSolver.cs:1418-1502`（`Score`）。

| 字段 | 含义 | 取值方式 |
| --- | --- | --- |
| `PlacedCount` / `PlacedArea` | 放下件数 / 面积 | `placements.Count`、`Σ Mask.Area` |
| `NegativeTypeRuns` | **同类型被切成几段**（越少越好，取负） | `ShelfMetrics(…, ItemTypeKey)` 的 `extraRuns` 取负（`:1496`） |
| `SameTypeRowAlignment` | 同类型成行对数 | `ShelfMetrics` 的 `rowPairs` |
| `SameTypeHorizontalContact` | 同类型水平贴边数 | `ShelfMetrics` 的 `horizontalContact` |
| `NegativeCohortRuns` / `CohortRowAlignment` / `CohortHorizontalContact` | 同上，但"同类"用 `ShelfCohortKey`＝`品类␞min×max×面积`（`:1239-1244`）——即**"同品类同尺寸同面积"的架层队列** | `:1497` |
| `OrientationConsistency` | 同类型内姿态签名的一致度 | 每类型按其 `Mask.Signature` 分组取最大计数求和（`:1491-1494`） |
| `VerticalPreference` | 竖向（高>宽）件数 | `:1495` |
| `PriorityOrderAlignment` | 视觉优先级是否"左侧/上侧优先" | 两两比较 `VisualPriority` 与线性序（`:1465-1490`），同向 +1 反向 −1 |
| `CategoryOrderAlignment` | 品类次序是否与 `CategoryOrder` 一致 | 同上，用 `p.CategoryOrder` 下标 |
| `LargeFirstAlignment` | 同品类内大件是否在前 | 同上，比较 `Mask.Area` |
| `SameTypeContact` / `SameCategoryContact` | 4-邻接贴边数（同类型 / 同品类） | `:1432-1458` |
| `TopLeftCompactness` | 左上聚拢度 | `-(Σ y*W+x)`（`:1498`） |
| `RowRegularity` | 行整齐度 | `-Raggedness`（`:1499`、`:1505-1538`：每行最右占格左侧的空洞 ×4 + 行尾长度跳变绝对值之和） |
| `NegativeFragments` | 空洞与窄碎片 | `-(holes*8 + narrow)`（`:1502`） |
| `NegativeInterleaving` | 异品类相邻对数 | `-num3`（`:1452-1455` 累计） |
| `NegativeRotations` | 被旋转的件数 | `-count(Rotated)`（`:1500`） |
| `StableTie` | 确定性兜底 | `PlacementKey(placements)` 全量串 |

**关键设计：同一个分数，在不同仓库里按不同顺序比。**

```csharp
// DeterministicSolver.cs:113-125
public static string DensityMode(LayoutProblem p)
{
    double num = OccupancyRatio(p);
    if (!(num >= 0.92))
    {
        if (!(num >= 0.8))
        {
            return "aesthetic";
        }
        return "balanced";
    }
    return "dense";
}
```

`ComparisonFields`（`DeterministicSolver.cs:127-200`）按模式返回三套字段顺序：

- `aesthetic`（占用率 <0.80）：20 项全上，**结构感在前**（typeRegions → typeRows → typeEdges → cohort* → orientation → vertical → priority → category → largeFirst → typeContact → categoryEdges → topLeft → edgeRegularity → fragments → interleaving → rotations）
- `balanced`（0.80–0.92）：17 项，**先保`fragments`与`edgeRegularity`**，把 `cohortRows`、`topLeft` 押到最后
- `dense`（≥0.92）：18 项，`cohortRows` 保留（拥挤时更要"整排放同类"），`SameTypeRowAlignment` 仍在前列

### 2.3 排序键（不是单一比较器，而是四层）

1. **类别标签层**：`Categories(item)` 直接读 `item.itemTypes`（原版标签数组），空则填 `"~unknown"`（`InventoryTransaction.cs:1818-1849`）。
2. **品类优先级层**：`CategoryRank` 对标签做**子串匹配**：

```csharp
// InventoryTransaction.cs:559-563
private static readonly string[] CategoryRanks = new string[20]
{
    "container", "storage", "bag", "backpack", "machine", "device", "equipment", "appliance", "weapon", "ammo",
    "armor", "medical", "med", "food", "drink", "tool", "material", "component", "valuable", "misc"
};
```

　`i<=1 → 0`，`i<=3 → 1`，`i<=7 → 2`，其余 `10+i`，`~unknown → 3`，无命中 `1000`（`:1861-1888`）；再折算成 `VisualPriority = 10+rank`（rank≤3）或 `100+rank`（`VisualPriority`，`:1851-1859`）。**注意这是对本地化后的标签做小写子串匹配，不是精确 ID 匹配——跨模组/多语言时会走 `1000` 兜底。**
3. **几何层**：求解器只认"类型占位符"（`StableId`），先解出**形状与姿态**。
4. **实例层**：`InstanceOrder.Apply` 把"具体哪一件"填进已经定好的槽位（见 §7 模式 3），比较键写在一条**自述式策略串**里：

```csharp
// InstanceOrder.cs:11
public const string Policy = "semantic-slots-v3:real-state-appraisal-ignored;name;empty-last;usable-sealed-rustfree;wine-2-1-0-age-desc-ferment-asc;water-grade-then-volume;food-fresh-first;absolute-remaining-desc;quality-before-quantity;stable-id";
```

　实例排序的完整键序（`InstanceOrder.Compare`，`:81-262`）：`ItemId → (补给箱专用: RemainingUses 降序) → BrewingFruitQuality 降序 → CurrentName → IsEmpty → Purpose → Profile → Expired → Unavailable → Opened → Rusted → Dented → Counterfeit → 按 Profile 分派(wine/water/chemical/ingot/food/…) → StableId`。

> **对《深空当铺》特别重要的一条**：策略串明写 `real-state-appraisal-ignored`，代码里也确实**不读 `realCondition` 判真伪**——`Counterfeit` 取自**可见品类标签** `Rank("CATEGORY_GENUINE_CIGARETTE") ?? … ?? Rank("CATEGORY_REPLICA")`（`InstanceOrderNative.cs:426`），旁边还留了标记串 `"appraisal-ignored:real-state"`（`:428`）。即：**排列顺序不会泄露"这东西是假的"**。

## 3. 算法：不是"排序"，是"多引擎竞标的装箱搜索"

三层，越外层越"全局"，越内层越"精确"。

### 3.1 `DeterministicSolver`（1,676 行）——确定性多启发式竞标

`Solve`（`DeterministicSolver.cs:235-445`）一次跑完下列候选，全部用同一 `Score` 打分，取 `CompareForProblem` 最优：

1. **原子天际线块**：`AtomicSkylineBlocks(portraitOnly: true/false)`（`:262-263`）——把同品类/同尺寸的件先捆成"原子块"再按天际线放，带 `CategoryBlockDistance`（`:572`）做品类靠拢。
2. **束搜索（beam）**：`BeamWidth = 32`、每状态最多 `CandidatesPerState = 24` 个落点、`TimeBudget = 250ms`（`:76-80`）。候选枚举 `EnumerateCandidates`（`:1175-1205`）用 `(竖条带归类, 同行同类数, 水平同类贴边, 同类贴边, 品类贴边)` 五级降序排序后取前 24——**这是剪枝的全部**：不枚举全部合法位，只按"局部更有序"的前 24 个展开。
3. **贪心族**：`GreedyShelfGroups`（不许横放 / 许横放）、`GreedyFirstFit`（左起 / 右起）、`GreedyMaxRects`（3 种物品排序 × 全部 `PackingHeuristic` 枚举 × dense 开关）（`:363-383`）。
4. **dense 专属**：`DenseSlotReassignment`（占用率 ≥0.92 时按原布局原地重分配，`:361`）。
5. **局部改进**：`ImproveCompleteLayout(original)`（`:404-406`）。
6. **兜底**：全失败则原样返回并置 `UsedFallback = true`（`:424-440`）。

超时检查 `OutOfTime()` 散落在每个内层循环（`:308/316/324/376`），超时即 `TimedOut` 并把当前最好结果返回。

### 3.2 `Native34Solver`（11,156 行）——位棋盘精确搜索 + 延迟束

真正干活的引擎。特征：

- **位棋盘**：`SearchState(BigInteger Occupied, BigInteger Remaining)`（`:3094`）、`PackedTemplate(Template T, BigInteger Bits, Cell First)`（`:3092`）、`TryBoundedPack`（`:7395`）、`LowestBit`/`CountBits`（`:9677/9691`）。另有 `ulong[]` 字级快路径：`PlacementProbeBoard`、`PlacementProbeFits`、`RefillWordMask`、`WordCollides`、`WordToggle`、`WordFirstFree`（`:11340-11560`）——**先字级试探，冲突才落 `BigInteger`**。
- **模板/家族化**：`Family(Key, LayoutItem[] Items)`（`:3121`）、`Template(Placement[] Items, int Width, int Height, int Rows, int Landscape)`（`:3123`）、`Templates`/`InterlockRows(beam: 6)`/`TemplatesOriginal`（`:11184/9109/9167`）。同尺寸件被当成可互换单元整体摆放。
- **枪械互锁**：`Interlocking`/`InterlockingCore`（`:9068/9079`）、`RefineDefaultInterlocks`（`:6402`）、`DefaultGunRepairSeeds`（`:6649`）、`GunUnlockBound`（`GunUnlockBound.cs`，180 行）——枪可以竖放互锁，且有一条独立的"可行性上下界证明"。
- **多路线 + 每批限量**：`SearchSession.Advance(nodeBudget = 25000, solutionBatch = 64)`（`:1814`）；`NodeLimit = 250000`、`MaxPasses = 8`（`:5163/5165`）。`Advance` 返回 `SearchProgress(…, NewNodes, TotalNodes, NewCandidates, TotalCandidates, Improved, Exhausted, CanContinue, Reason)`（`:1645`）——**这是"可续跑"的关键：一次不跑完，下一帧/下一次调用接着推进**。
- **播种矩阵**：`Seeds` 里 `layout ∈ {shelf, compact, unit-tail} × order ∈ {item-area, family-area, height, width, cohort, category, constrained, small}`（`:1763-1766`），3×8 = 24 条种子路线，每条一个 `Route`，`Exhausted` 要求所有 route 都 `Done`（`:1708-1718`）。
- **布局去重**：`_seen = HashSet<LayoutPoolKey>`（`:1674`）、`PoolKey`/`MakeLayoutKey`/`TryExactArrayKey`/`Hash`（`:8633/8815/7327/8970`）。
- **合法性终审**：`ValidateLayout(p, q)`（`:8975`），`Apply` 前必跑（`SmartArrangeMod.cs:470`）。

### 3.3 `BlockSegmentArrangeController` + `DefaultBlockSegmentGenerator` + `RowBandPacking`——大仓库分层

- 阈值：`Direct63UsableCellLimit = 300`（`BlockSegmentArrangeController.cs:31`）。**≤300 可用格或有可动枪 → 走 `Canonical63Bridge` 直解**（`:144-154`）；>300 → 分层生成器。
- 分层策略是**高度扫描 + 两层配额**：

```csharp
// BlockSegmentArrangeController.cs:191-213（第一次 slab 层）
for (int num8 = hmin; num8 <= heightLimit && num8 - hmin < budget.MaxHeights; num8++)
{
    cancel.ThrowIfCancellationRequested();
    long used = ledger.Used;
    long num9 = ((num8 < heightLimit && num8 - hmin + 1 < budget.MaxHeights) ? (ledger.Remaining * 88 / 100) : ledger.Remaining);
    long quotaNs = ((num6 <= 0) ? num9 : (num9 * num4 / 100));
    if (num5 > 0)
    {
        attemptedLayer = 1;
        search = defaultBlockSegmentGenerator.Search(num8, num5, columns: false, Certificate, optional: true, quotaNs);
        num5 -= search.Work - num7;
        num7 = search.Work;
        Outcome outcome = Admit(search.Candidates, 1);
        if (outcome != null) { return outcome; }
        if (ledger.Exhausted) { break; }
        defaultBlockSegmentGenerator.Retain((DefaultBlockSegmentGenerator.Candidate _) => false);
    }
    // 紧接着 layer 2：Expand() 后 Search(…, columns: true, …)
```

  - **高度下界** `LowerBound(p)`（`:61-85`）＝按最小面积累计到第几行才有足够格子，且不矮于最高的固定件。
  - **层 1 = slab（横条带）**，**层 2 = columns（整列）**，每层吃配额，超了 `Exhausted`。
  - 每个候选必须先过 `Certificate`（硬下界证明）才被 `Admit`；`Admit` 里先 `candidates.Where(Certificate)`，为空才退回 `.Where(Pass)`（`:266-280`）。
  - 全失败 → `GetReference()` 拿 `Canonical63Bridge`（冻结的 `SiaCanon63` 引擎）的结果当兜底（`:245-265`）。
- `RowBandPacking.Search`（694 行）是内层"一条横带怎么排"的束搜索：`BeamWidth = 20`、`PairLimitPerSize = 5`、**尺寸上限 `W,H ≤ 63` 且分组数 ≤ 63**（`Search.cs:10-12`、`:74-83`），用 `fits[W+1, cap+1]` 的 `ulong` 掩码查表 + 二维前缀 `fits[i,j] |= fits[i-1,j] | fits[i,j-1]`（`:114-124`）把"某宽高能不能塞下某组"变成 O(1) 位运算。

### 3.4 时间/工作量预算（全在这里）

| 预算 | 值 | 位置 |
| --- | --- | --- |
| `DeterministicSolver.TimeBudget` | 250 ms | `DeterministicSolver.cs:80` |
| `DeterministicSolver.BeamWidth` / `CandidatesPerState` | 32 / 24 | `:76/78` |
| `Native34Solver.NodeLimit` / `MaxPasses` | 250,000 节点 / 8 趟 | `Native34Solver.cs:5163/5165` |
| `GlobalArrangeController.InitialNodeBudget` / `ContinueNodeBudget` / `SolutionBatch` | 250,000 / 25,000 / 64 | `GlobalArrangeController.cs:29-33` |
| `SearchSession.Advance` 默认 | `nodeBudget = 25000`、`solutionBatch = 64` | `Native34Solver.cs:1814` |
| `RowBandPacking` 束宽 / 每尺寸对子上限 | 20 / 5 | `RowBandPacking\Search.cs:10-12` |

`BudgetPlan.Research`（`BudgetPlan.cs:10-48`）按"可用格数 × 是否含 `FIREARM`"给四档配额（单位是**工作量**，不是毫秒）：

| 格数 | 有无枪 | Compatibility / Slab / Columns / RootSlice / MaxRootSlice / MaxHeights / CertifiedColumns |
| --- | --- | --- |
| ≤100 | 有 | 800 / 1,600 / 2,500 / 160 / 600 / 3 / 0 |
| ≤100 | 无 | 26,000 / 30,000 / 6,500 / 350 / 2,000 / 3 / 6,500 |
| 101–300 | 有 | 1,800 / 4,200 / 6,000 / 250 / 1,000 / 3 / 2,000 |
| 101–300 | 无 | 100,000 / 118,000 / 16,000 / 1,500 / 9,000 / 4 / 24,000 |
| >300 | 有 | 0 / 500,000 / 500,000 / 700 / 35,000 / 4 / 0 |
| >300 | 无 | 0 / 500,000 / 500,000 / 1,000 / 35,000 / 4 / 0 |

**为什么用"工作量"而不是毫秒**：`GenerationWork`（`Core\GenerationWork.cs`）把每一步按 `WorkTariff` 折算成等效纳秒记账，总预算 `AllowanceNs = 350,000,000`（350 ms 当量，`:21`）：

```csharp
// GenerationWork.cs:47-58
internal GenerationWork(int physicalCells)
{
    bool flag = physicalCells > 512;
    _scale = (flag ? WorkTariff.Wide : WorkTariff.Narrow);
    _activation = WorkTariff.Activation;
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
internal void Debit(WorkStage stage, long ns)
{
    Spend(stage, (int)stage, ns, ns * _scale[(int)stage] / 100);
}
```

`WorkTariff.Narrow`/`Wide` 各 12 项（对应 `WorkStage {Setup, Proof, Library, Template, Slab, SlabResidual, Columns, Graph, CoverPrep, Cover, Witness, Admission}`），**大盘（>512 物理格）用更便宜的汇率**；`Activation` 是每阶段"首次启动"固定开销。超出即 `Exhaustion` 异常 → 整层放弃。**同一份仓库在任何机器上得到同一个布局**（这是它敢自称"deterministic"的地方）。

## 4. 与原版交互：不改存档，改 `modifiedShape`，全程预检 + 回滚

### 4.1 应用布局 = 原地重写形状（既不是 `Expel` 重放，也不是改内部结构）

`InventoryTransaction.ApplyCore`（`InventoryTransaction.cs:1623-1757`）的真身：

1. 先用 `GridShapeBuilder(originalShape).SetOrientation(...).SetPosition(...).Build()` 为每件算出目标 `GridShape`；
2. **回读**目标形状的掩码，与候选的 `Mask.Signature` 比对，不一致即抛（`:1680-1684`）；
3. 逐件 `(GameInventory)inventory.CheckShapePlacement(item, shape, list3)` **整体预检**（`:1686-1690`）；
4. 写 `item.modifiedShape = shape`（`:1707`）＋ `relocation.Item.modifiedShape`（`:1701`）；
5. 逐件 `CheckInventorySlotStillValid(item, shape, item.state)`（`:1716`）；
6. `GameInventory.Validate()` + `inventory.ForceRecomputeDynamicBackground()`（`:1721-1722`）；
7. 再验一遍 `AssertStrictCurrent("after-apply")`、`VerifyUnchanged(ignoreShape: true)`、逐件回读 `modifiedShape` 校验 `minX+offsetX == 目标 X`（`:1723-1743`）。

> 结论：**它不动物品的父子关系、不重建 inventory、不碰存档**。因此容器内物品、容器自身的 `contentWindow`、堆叠数量全部自动保留。这比"逐件 Expel + TryAccept"少了 N 次容器重排，也是它能几十毫秒排完一整箱的根本原因。

### 4.2 失败回滚

```csharp
// InventoryTransaction.cs:1745-1757
catch
{
    try
    {
        Rollback();
    }
    catch (Exception rollbackException)
    {
        RollbackException = rollbackException;
    }
    throw;
}
```

`Rollback`（`:1759-1790`）：把 `_allOriginal` 里每个**输入基线内**的物品 `modifiedShape` 还原成捕获时的 `shape.Clone()` → `Validate()` → `ForceRecomputeDynamicBackground()` → 逐件校验 `parentInventory == inventory && childItems.Contains(item) && Geometry(modifiedShape) == Geometry(shape)`，失败则 `RollbackStatus = "failed"` 并记录 `RollbackException`。状态字段：`RollbackStatus ∈ {not-attempted, in-progress, restored-verified, failed}`（`:600`）。

上层再兜一层：`SmartArrangeMod.cs:547-597` 捕获后若 `applied` 则 `tx.RestoreCapturedSnapshot()`，并把结果分档为 `Cancelled / Failed / InventoryChanged`（后者来自 `"Snapshot guard"` 前缀的 `InvalidOperationException`，`:574`）。`ArrangeOutcome` 有 9 个终态：`Running/Applied/Unchanged/NoCandidate/Empty/InventoryChanged/Cancelled/NotSortable/Failed`（`ArrangeOutcome.cs`）。

### 4.3 过期检测（防"求解期间玩家动了仓库"）

- `ProblemSignature(p)`：把 `usable` 位图 + 固定件 + 物品表 + 品类次序序列化成 JSON 再 SHA256（`GlobalArrangeController.cs:103-146`）。
- `GlobalArrangeController.Begin`：签名/原姿态/上下文/朝向开关任一变化 → 丢弃旧 `SearchSession` 重建，并回报 `Restarted = true`（`:54-69`）；否则**沿用同一 session 续跑 25,000 节点**（`:72`）。
- `Complete`：再验签名 + `ValidateLayout`，不符抛 `"Stale search-session completion."`（`:75-87`）。
- `AssertSnapshotCurrent` / `AssertStrictCurrent` / `StateRejection`（`InventoryTransaction.cs:1537/1571/1528`）用每个物品的 `CapturedNativeState` 逐项比对，并写 `MonoLog` 差异日志。

### 4.4 撤销：接管原版 `Ctrl+Z` 的那一格

- `ArrangeUndo` 直接反射原版字段：`NativeStore = AccessTools.Field(typeof(InventorySortHelper), "undoSnapshots")`（`ArrangeUndo.cs:33`），每次 `Remember` 前先 `ForgetNative(inventory)`（`:96`）——**把原版那一格让位给本次整理**，于是 `Ctrl+Z` 撤销的是"整理"，不是"上一次原版排序"。
- 三个补丁把 `InventorySortHelper` 的 `CanUndo / MayUndo / Undo` 全部改为查 `ArrangeUndo`（`UnifiedCanUndoPatch.cs:22-30`、`UnifiedMayUndoPatch.cs`、`UnifiedUndoPatch.cs:22-39`）。
- 只保留**一组**：`private static Group? _latest`（`:35`），`Remember` 里 `if (_latest != obj) ForgetLatest();`（`:104-107`）；组内可含多张网格（`Group.Inventories`）＋一次插入（`Group.Insertion`）。
- 容量护栏：`if (obj.Count > 8192) { Remove(obj); }`（`:110-113`）。
- 原版排序也被纳管：`NativeArrangeUndoCapture` 在 `InventorySortHelper.Sort` 前后自行快照，`ArrangeUndo.NativeSorting = true` 期间其它入口禁止开新整理（`NativeArrangeUndoCapture.cs:24-51`）。
- **不落盘（推测）**：活树里搜不到 `ES3 / File.Write / SaveGame / DirectoryMaster.Save`；配置走 `ModConfig` + `ConfigEntry`（`SettingsRuntime.cs:416-440`、`SmartArrangeMonoMod.cs:26-30`），只有少量标记文件用 `ModHelper.ModDataFileExists("smart_inventory_arrange", "…txt")`（`SettingsRuntime.cs:399`）。读档/切场景时 `SmartArrangeMod.Invalidate()` + `ArrangeUndo.Clear()`（`SmartArrangeMonoMod.cs:137-167`）。

### 4.5 "腾位"（Make Room）与外页投递

- `SmartInsertController`（1,150 行）＋`ClosedInsertOperation`（3,893 行，最大非求解器文件）实现：拖入放不下 → 主动重排目标容器腾出空间，剩余留在源处。阶段枚举只有四个：`InsertPhase { Queued, Capture, Solve, Commit }`（`InsertPhase.cs`）。
- API 面：`Capture/Queue/QueueContent/Tick/Cancel/Execute/PrepareContent/Eligible/EligibilityFailure`（`SmartInsertController.cs:86-616`）。
- 这是**实验性**开关：`ExperimentalSmartInsert` 默认 `false`（`SettingsRuntime.cs:422`）。
- "闭页投递"：`ClosedInsertScope` 能拿一个**关着的**容器的分页（`Page(GameGridInventory Grid, int Tab)`，`ClosedInsertScope.cs:13`），靠 `AccessTools.Field(typeof(GameGridInventory), "insideBorder")`（`:15`）与遍历 `PixelWindow` 的 `PixelElement` 找页（`:182`，`FindPages`）。开关 `SmartInsertHiddenPagesOptIn` 默认关。

## 5. 18 个靶点表（25 个 Patch 类）

> 表按"靶点族"排 18 行；同一行有多个类时以 `/` 分隔。所有类都有统一骨架：`[HarmonyPrepare] → StartupCompatibility.Prepare(CompatFeature.X, typeof(本类))`、`[HarmonyCleanup] → StartupCompatibility.Cleanup(...)`、方法体整体 `try/catch → InputRecovery.PatchFault("<slug>", error)`。

| # | 靶点 | 补丁类 | 干什么 |
| --- | --- | --- | --- |
| 1 | `ItemContextHandler.OnEventInit` | `ContextInitPatch` | Postfix：右键菜单初始化成功后，若原生 `__result` 为真就把模组菜单项挂上（`NativeSmartMenu.TryOpen`） |
| 2 | `ItemContextHandler.TryOpenSortMenu` | `ContextSortOpenPatch` | Prefix：接管"打开排序菜单"；设置窗占着指针就吞掉，菜单项打不开且原版 `SortMenuDisabled` 为真时把窗口 `Hide()` 返回 false |
| 3 | `ItemContextHandler.TriggerButton` | `ContextTriggerPatch` | Prefix：识别自己加的两个按钮（`NativeSmartMenu.Button` / `PreserveButton`），关窗后调 `SmartArrangeMod.Run(inv, preserve)`，返回 false 吞掉原生动作 |
| 4 | `ItemContextHandler.OnEventPress` | `DisabledSortRightClickPatch` | **Transpiler**：把唯一的 `EmporiumOption.SortMenuDisabled` getter 调用替换为 `BlockRightClickSortMenu()`；**找不到"恰好一处"就 `StartupCompatibility.Disable(...)` 整功能自禁**（`DisabledSortRightClickPatch.cs:44-53`） |
| 5 | `RichTextGenerator.InsertItemInOtherItem(GameItem,GameItem,bool,RichTextBuilder)` | `FacilityDropPreview` | Prefix：拖拽预览时若"内容路由/闭页腾位"能接下这批货，就吞掉原生 tooltip 并 `__result = null`，把提示延后到真正落位后再显示 |
| 6 | `ItemMouseDragHandler.EndDrag`（无参重载） | `SmartInsertSingleDrop` / `PostInsertSingleGesture` | 前者 Prefix 抓 `currentItem/lastInventory/lastItem/currentRemove/lastSlot` 组装 `SmartInsertRequest`、Postfix 入队、Finalizer 清 Active；后者只做"记起点/收终点"交给 `PostInsertGestureFinish` |
| 7 | `ItemMouseDragHandler.EndDrag(bool)` | `PageRouteSingleDrop` / `FixedPlacementDrop` | 前者 `[HarmonyPriority(800)]` 抓裸拖（没有目标 inventory）开 `PageRouteGesture`；后者 Alt 强制放置：预检失败但有 blocker 时抢占，改写 `__args[0]`/私有字段后交 `FixedPlacementController`，Finalizer 还原被清空的 `lastInventory/lastItem/lastSlot` |
| 8 | `ItemMouseDragHandler.StartDrag` + `ItemMultiSelectHandler.StartGroupDrag` | `SmartInsertDragStart`（裸 `[HarmonyPatch]` + `TargetMethods`） | Prefix：新一次拖拽开始 → 清手势生命周期、取消上次的固定放置/闭页路由、必要时取消正在排队的自动腾位 |
| 9 | `ItemMultiSelectHandler.EndGroupDrag` | `SmartInsertGroupDrop` / `PageRouteGroupDrop` / `PostInsertGroupGesture` | 多选拖拽的落点：第一个抓 `selectedItems` + `IsPreciseModifierHeld()`（精确模式就不接管）建请求；第二个抓"鼠标下面那个物品"开内容路由；第三个只记手势 |
| 10 | `SlotMarker.TryAcceptOnce` | `NativeInsertUndoCapture` / `PostInsertNativeAccept` | 唯一真正"接货"的原版方法。前者在 Prefix 拍下源/收货方姿态、Postfix 用 `__result`（实际接收数）与前后 `unitCount` 差**反推是否真进了目标页**，正确才记入撤销，数量不符直接 `undo.Invalidate()`；后者只在成功后触发"放入后自动整理" |
| 11 | `InventorySortHelper.Sort` | `NativeArrangeUndoCapture` | Prefix 拒绝在有整理/撤销进行时开新排序；否则自己拍快照，Postfix 把"原版排序"也记成可撤销组 |
| 12 | `InventorySortHelper.CanUndo` | `UnifiedCanUndoPatch` | Prefix：本模组有可用撤销组时，直接用自己的答案回答"能不能撤" |
| 13 | `InventorySortHelper.MayUndo` | `UnifiedMayUndoPatch` | 同上（MayUndo 语义） |
| 14 | `InventorySortHelper.Undo` | `UnifiedUndoPatch` | Prefix：转给 `ArrangeUndo.Start(inventory)` 执行并返回 false，绕开原版撤销表 |
| 15 | `GameItem.TryFindOneValidInventorySlot(GameItem)` | `PageRouteNativeSlot` | Prefix：当"闭页路由手势"正在同一帧对同一 owner/同一件生效时，返回 `__result = null` 阻止原版自动找槽——**把"物品自己找位置"这一步整个抽掉**，改为模组指定页 |
| 16 | `GameItem.GetSplitItem` | `SmartInsertSplitBegin` / `SmartInsertSplitCreated` | 前者 Prefix 记下 `requestedAmount`（拆几件）；后者 **Transpiler** 把唯一的 `DirectoryMaster.Item(string,bool)` 调用换成自己的 `CreateSplit`，从而抓住新生成的拆分实例引用；同样"不是恰好一处就自禁用"（`SmartInsertSplitCreated.cs:57-67`） |
| 17 | `TreeNodeRender` 的 10 个指针事件（`OnPointerClick/DoubleClick/Down/Up/InitializePotentialDrag/BeginDrag/Drag/EndDrag/Drop/Scroll`） | `SettingsPointerGuard`（裸 `[HarmonyPatch]` + 反射 `GetMethods` 选集） | Prefix：设置窗打开时吞掉 UI 指针事件；对 `Up/Drag/EndDrag/Drop` 这类"收尾型"事件改用 `BlocksPointerTail` 判定——**保证正在拖的物品不会因为开了设置窗而卡在手上** |
| 18 | `ItemQuickTransferHandler.OnEventRelease(ISet<KeyCode>)` + `ItemQuickTransferAltHandler` + `ItemQuickTransferAltHandler1` | `PostInsertQuickGesture`（裸 `[HarmonyPatch]` + `TargetMethods`） | Prefix：原版"快速转移"（快捷键）成功时也走一遍"放入后自动整理"，条件是 `___isQuickTransfer && ___currentItem != null` |

**接管拖放/自动整理的组合拳**（这是它最值得研究的地方）：

```text
StartDrag  ──► 清场（DragLifetime / 固定放置 / 待办腾位）
EndDrag    ──► [Alt?] FixedPlacementDrop 抢占
           └─► [有目标 inventory?] SmartInsertSingleDrop 建请求
               └─► [无目标] PageRouteSingleDrop → 内容路由到"闭着的"容器页
EndGroupDrag ─► 同上（多选版），精确模式一律放手
TryAcceptOnce ─► 观测"到底进了哪一页、几件"，核准后记撤销 + 触发后置整理
TryFindOneValidInventorySlot ─► 路由期间禁止原版自动找位
InventorySortHelper.{Sort,Undo,CanUndo,MayUndo} ─► 撤销栈统一
```

## 6. 性能：8.9 万行里大头是什么，以及怎么不卡

### 6.1 行数分布（按活树 50,256 行统计；另 32,669 行是两份冻结副本）

| 桶 | 行数 | 占比 | 代表文件 |
| --- | --- | --- | --- |
| **求解/布局**（`*Solver`、`*Search`、`*Generator`、`Packing`、`BlockSegment*`、`Canonical*`、`Legacy`、`InsertPlanner`、`FixedPlacementSearch`） | 19,614 | **39%** | `Native34Solver.cs` 11,156 / `DefaultBlockSegmentGenerator.cs` 3,024 / `DeterministicSolver.cs` 1,676 / `BlockSegmentChecks.cs` 992 / `RowBandPacking\Search.cs` 694 / `LocalInsertPlanner.cs` 532 |
| **腾位/路由/事务**（`*Insert*`、`AltCross`、`PageRoute`、`CrossPage`、`Batch*`） | 12,717 | **25%** | `ClosedInsertOperation.cs` 3,893 / `SmartInsertController.cs` 1,150 / `InsertUndo.cs` 566 |
| **UI / 文本 / 提示**（`UnifiedSettingsGui`、`Settings*`、`*Text*`、`*Intro`、`*Notice`、`Feedback`、`Menu`、`ItemNameResolver`、`NamedTab*`） | 7,815 | **16%** | `UnifiedSettingsGui.cs` 2,773 / `ReleaseNotice.cs` 344 / `SettingsIntro.cs` 279 / `ArrangeFeedback.cs` 266 |
| 快照/状态/诊断/杂项 | ~10,110 | 20% | `InventoryTransaction.cs` 1,897 / `InstanceOrderNative.cs` 595 / `SettingsRuntime.cs` 607 / `StartupCompatibility.cs` 622 / `GameWork.cs` 534 |

**所以"8.9 万行大头"= 求解器 39% + 腾位/事务 25% + UI 文本 16%。** 不是 UI 撑起来的，是算法 + 交互事务。多语言**不是**靠大表：活树里双语是**内联参数对**——

```csharp
// SettingsRuntime.cs:418
_afterInsert = config.Define("ArrangeAfterSuccessfulInsert", false, "Arrangement",
    (Func<string>)(() => GameLanguage.Text("放入后自动整理", "Arrange after putting items in")),
    (Func<string>)(() => GameLanguage.Text("只整理实际收到物品的容器或页面，已关闭但仍加载的容器也可整理。",
        "Only arranges the receiving container or page, including closed containers that are still loaded.")), false);
```

`GameLanguage.Text(zh, en)`：`Russian` → `RussianText.Translate(english)`；否则 `Chinese ? zh : en`（`GameLanguage.cs:57-68`），语言码每 30 帧从 `LocalizationSettings.SelectedLocaleAsync` 刷一次（`:76-118`）。**支持 zh / en / ru 三语，ru 由 `RussianText.cs`（188 行）的替换表机械翻译。** 大文件都在"数据资产"里，不在代码里：

| 资源 | 大小 | 形态 |
| --- | --- | --- |
| `SmartInventoryArrange.ItemNameSources.json` | 128 KB | `{ "item_id": ["中文名","English","Русский"] }` 三语物品名表 |
| `SmartInventoryArrange.NamedTabVocabulary.tsv` | 135 KB / 2,098 行 | `kind \t ItemId-or-tag \t source \t language \t word`，用来给"命名页签"造人类可读名（如 `category STORAGE → 背包/容器`） |
| `SmartInventoryArrange.FixedRuleCatalog.json` | 34 KB | 固定规则目录（哪些物品必须保持原位），由 `FixedRules.cs` 读写 |
| `SmartInventoryArrange.directional21.json`（+ canon/template 各一份） | 3 KB ×3 | 方向/朝向规则表 |

### 6.2 不卡的六道手段

1. **帧内按毫秒切片**（最关键）。`CaptureFrameBudget.BeginFrame` 给出本帧预算，`RunSlice` 一直跑迭代器直到超预算：
   - 预算 1/2/3 ms，取决于 `previousFrameMs ≥ 20 / ≥ 15 / 否则`（`CaptureFrameBudget.cs:64`）；
   - 带反馈的构造：保持 9 帧窗口，中位数 > 1.2 × 基线就把预算**减半**（下限 1 ms）（`:35-56`）；
   - **正在拖物品时整帧不做**（`:39-42`）；滚动或上一帧 ≥25 ms 时最多连续让 3 帧（`:59-62`）。
2. **后台线程求解**：`GameWork.Run(work)` 包一层，`ModHelper.RunInBackground("smart_inventory_arrange", action, null)` 起预热（`SmartArrangeMonoMod.cs:119`）；主线程侧用协程等 `Task.IsCompleted`，每帧 `yield return null`（`SmartArrangeMod.cs:360-367`、`:430-437`）。
3. **可续跑而不是长阻塞**：节点预算 250k 首批 / 25k 续批，`SearchSession.Advance` 返回 `CanContinue`，UI 层可"再来一批"（`Native34Solver.cs:1814`、`SmartArrangeMod.cs:460`）。
4. **算力记账替代墙钟**：`GenerationWork` 350 ms 当量总账（§3.4）；这样"预算"与机器速度解耦，布局可复现。
5. **结果缓存**：`FinalLayoutCache` 容量 32、FIFO，key 里含算法版本串 `"block-segment-rc/59fa9a0/fifo32-v1"`（`FinalLayoutCache.cs:14-16`），命中就直接返回并标 `ResultCacheHit = true`（`BlockSegmentArrangeController.cs:96-108`）。`LayeredFinalResults` 挂在 `SmartArrangeMod` 上，`OnGameLoaded`/切场景时清空。
6. **预热 + 位运算 + 池化**：`SolverPrewarmJob` / `StartupPrewarm` / `NativeStartupPrewarm`（123/107/10 行）在游戏加载后先把 JIT/表建好，`Native34Solver` 里还内置了 `WarmGunFixture()`/`WarmRectFixture()`/`WarmBoard()` 三个热身夹具（`:10036-10127`）；捕获路径用 `CaptureList`/`CaptureValueScratch`/`CaptureFrameClock` 做 scratch 池化，避免每帧 GC。

另外还有一条容易被忽略的：**死循环看门狗**——`OwnedRoutine.MissedFrameProgress` 在"应该每帧推进"却超过 8 帧没动时返回 true（`GameWork.cs:52-62`），配合 `_orphanedWorker` 状态把卡死的 worker 复位（`SmartArrangeMod.cs:30-49`）。

## 7. 模块化：342 个文件怎么组织

### 7.1 目录 = 层次

```text
SmartInventoryArrange\                     129 个 .cs   ← 游戏侧：补丁/事务/UI/设置/路由
SmartInventoryArrange.Core\                 57 个 .cs   ← 纯算法与模型（不碰 UnityEngine）
SmartInventoryArrange.Core.BatchRouting\    11 个 .cs   ← 跨页/批量投递规划
SmartInventoryArrange.Core.RowBandPacking\   9 个 .cs   ← 横带装箱子引擎
SmartInventoryArrange.Migration.Runtime\     7 个 .cs   ← 抽象接口 + RuntimeEpoch + 身份注册
SmartInventoryArrange.Migration.Compatibility\ 3 个 .cs ← 哈希/位运算兼容层
SmartInventoryArrange.Serialization\         3 个 .cs   ← 自带的极简 JSON（GameJson 259 行）
SiaCanon63*\                                62 个 .cs   ← 冻结的参考引擎（canon 63）
SiaTemplate63*\                             59 个 .cs   ← 冻结的模板计量引擎（template 63）
```

**分层判据非常硬**：`SmartInventoryArrange.Core` 里没有一个 `using UnityEngine`（`Native34Solver.cs:1-12` 的 using 表可证：只有 `System.*` + `SmartInventoryArrange.Core.RowBandPacking` + `Migration.Compatibility`）。**算法层完全不知道 Unity 存在**，`InventoryTransaction` 负责把 `GameItem/GridShape` 翻译成 `LayoutItem/Orientation/ShapeMask`。这是它 11k 行求解器能离线测、能被冻结副本替换的前提。

### 7.2 命名前缀就是"层号"

- 无前缀小文件 = 值类型/枚举/策略（`Cell`, `Placement`, `LayoutScore`, `Orientation`, `WorkStage`, `InsertPhase`, `ArrangeOutcome`，多数只有 2–20 行）。
- `SmartInsert*` = 拖入腾位链路；`ClosedInsert*` = 关着的容器；`PageRoute*` = 内容路由到指定页；`PostInsert*` = 放入后整理；`FixedPlacement*` = Alt 强制放置；`Native*` = 读/写原版状态（`NativeIdentity`, `NativeRules`, `NativeSmartMenu`, `NativeArrangeUndoCapture`…）；`Settings*` + `UnifiedSettingsGui` = 设置面；`Capture*` = 分帧捕获与快照；`*Undo` = 撤销。
- 文件名与类型名 1:1（反编译同源），无 `Helpers`/`Utils`/`Manager` 这类兜底名。

### 7.3 "核心 + 冻结扩展"的三引擎设计

`Canonical63Bridge`（37 行）只做一件事：把活树的 `LayoutProblem` 逐字段翻译成 `SiaCanon63.Core.LayoutProblem`，调 `CanonicalLegacy.Cold(...)`，再把结果翻译回来（`Canonical63Bridge.cs:16-36`）。`TemplateAccounting`（44 行）同理桥到 `SiaTemplate63` 的计量表。

收益：**新引擎出错时永远有一个"同代但冻结"的对照解**（`BlockSegmentArrangeController.cs:245-265` 的 `GetReference()` 就是拿它兜底），而且对照解是编译期锁定的，不会随着活代码改动而漂移。代价：DLL 里常驻 32k 行死代码。

## 8. 可复用模式（4 个，代码原样）

### 模式 1：按密度换序的字典序目标 + 确定性兜底

把"好不好看"拆成 20 个可加整数，再用**同一个比较函数、不同字段顺序**适配不同拥挤度；最后一级永远是全布局字符串，保证任何输入都有唯一解。

```csharp
// LayoutScore.cs:7-28
	public int CompareTo(LayoutScore o)
	{
		int[] array = new int[20]
		{
			PlacedCount, PlacedArea, NegativeTypeRuns, SameTypeRowAlignment, SameTypeHorizontalContact, NegativeCohortRuns, CohortRowAlignment, CohortHorizontalContact, OrientationConsistency, VerticalPreference,
			PriorityOrderAlignment, CategoryOrderAlignment, LargeFirstAlignment, SameTypeContact, SameCategoryContact, TopLeftCompactness, RowRegularity, NegativeFragments, NegativeInterleaving, NegativeRotations
		};
		int[] array2 = new int[20]
		{
			o.PlacedCount, o.PlacedArea, o.NegativeTypeRuns, o.SameTypeRowAlignment, o.SameTypeHorizontalContact, o.NegativeCohortRuns, o.CohortRowAlignment, o.CohortHorizontalContact, o.OrientationConsistency, o.VerticalPreference,
			o.PriorityOrderAlignment, o.CategoryOrderAlignment, o.LargeFirstAlignment, o.SameTypeContact, o.SameCategoryContact, o.TopLeftCompactness, o.RowRegularity, o.NegativeFragments, o.NegativeInterleaving, o.NegativeRotations
		};
		for (int i = 0; i < array.Length; i++)
		{
			int num = array[i].CompareTo(array2[i]);
			if (num != 0)
			{
				return num;
			}
		}
		return -StringComparer.Ordinal.Compare(StableTie, o.StableTie);
	}
```

配套的"换序"入口是 `CompareForProblem(p, a, b)`（`DeterministicSolver.cs:202-215`）＋`FirstDifferingMetric`（`:217-233`，诊断"我跟基线差在第一项"）。

### 模式 2：网格重排的事务——预检 → 写 → 双重复核 → 失败回滚

要点：**先把所有目标形状算完并整体碰撞预检，再一次性写**；写用 `modifiedShape` 原地改，不搬父子关系；写完立刻回读校验，任何一项不符就整体回滚并保留异常。

```csharp
// InventoryTransaction.cs:1670-1691（算 + 预检）
		foreach (Snap item2 in list.OrderBy((Snap x) => x.StableId, StringComparer.Ordinal))
		{
			Placement placement = dictionary2[item2.StableId];
			GridShapeBuilder val = new GridShapeBuilder(item2.OriginalShape);
			if (!flag)
			{
				val.SetOrientation(placement.GameOrientation);
			}
			val.SetPosition(placement.X + _originX, placement.Y + _originY);
			GridShape val2 = val.Build();
			NativeMask nativeMask = ReadNativeMask(val2);
			if (nativeMask.Mask.Signature != placement.Mask.Signature)
			{
				throw new InvalidOperationException("Native target mask differs from immutable candidate for " + item2.StableId + ".");
			}
			GridShape val3 = ((nativeMask.OffsetX == 0 && nativeMask.OffsetY == 0) ? val2 : val.SetPosition(placement.X + _originX - nativeMask.OffsetX, placement.Y + _originY - nativeMask.OffsetY).Build());
			if (!((GameInventory)_inventory).CheckShapePlacement(item2.Item, val3, list3))
			{
				throw new InvalidOperationException($"Game collision precheck rejected {item2.StableId} at {placement.X},{placement.Y} orientation {placement.GameOrientation}.");
			}
			list2.Add((item2, val3, placement, !flag || val3.minX != item2.OriginalShape.minX || val3.minY != item2.OriginalShape.minY));
		}
```

```csharp
// InventoryTransaction.cs:1759-1782（回滚 + 复核）
	private void Rollback()
	{
		NativeIdentity.AssertMainThread();
		RollbackStatus = "in-progress";
		try
		{
			foreach (var item in _allOriginal)
			{
				if (_inputBaseline.ContainsKey(NativeIdentity.Of(item.item)))
				{
					item.item.modifiedShape = item.shape.Clone();
				}
			}
			((GameInventory)_inventory).Validate();
			_inventory.ForceRecomputeDynamicBackground();
			foreach (var item2 in _allOriginal)
			{
				if ((object)item2.item.parentInventory != _inventory || !((GameInventory)_inventory).childItems.Contains(item2.item) || UndoInventory.Geometry(item2.item.modifiedShape) != UndoInventory.Geometry(item2.shape))
				{
					throw new InvalidOperationException("Rollback verification failed for " + item2.item.identifier);
				}
			}
			RollbackStatus = "restored-verified";
		}
```

### 模式 3：几何与实例解耦——先解形状，再排"哪一件"

求解器只解"类型占位"，实例身份后置。于是同一套几何解可以服务任意实例排序规则（名称/品质/新鲜度/剩余量），而且换排序规则不动算法。

```csharp
// InstanceOrder.cs:285-313
		foreach (IGrouping<(string, string, (string, int, bool, bool, string)?), LayoutItem> item in (from i in problem.Items
			group i by (ItemId: i.ItemId, Shape: ShapeClass(i), Pose: SelectedPose(i))).OrderBy((IGrouping<(string ItemId, string Shape, (string Mask, int Orientation, bool Flipped, bool Rotated, string Category)? Pose), LayoutItem> g) => g.Key.ItemId, StringComparer.Ordinal).ThenBy((IGrouping<(string ItemId, string Shape, (string Mask, int Orientation, bool Flipped, bool Rotated, string Category)? Pose), LayoutItem> g) => g.Key.Shape, StringComparer.Ordinal))
		{
			if (item.Count() < 2)
			{
				continue;
			}
			InstanceOrderInfo[] source = item.Select((LayoutItem i) => info[i.StableId]).ToArray();
			string[] profiles = (from x in source
				select x.Profile into x
				where x != "name"
				select x).Distinct(StringComparer.Ordinal).ToArray();
			bool profileConflict = profiles.Length > 1;
			InstanceOrderInfo[] array = source.Select((InstanceOrderInfo m) =>
			{
				InstanceOrderInfo result = ((!profileConflict) ? ((!(m.Profile == "name") || profiles.Length != 1) ? m : m with
				{
					Profile = profiles[0]
				}) : new InstanceOrderInfo(m.StableId, m.ItemId, m.CurrentName)
				{
					IsUnidentified = m.IsUnidentified,
					BrewingFruitQuality = m.BrewingFruitQuality
				});
				return result;
			}).OrderBy((InstanceOrderInfo i) => i, Comparer<InstanceOrderInfo>.Create(Compare)).ToArray();
			Placement[] array2 = (from i in item
				select bySlot[i.StableId] into q
				orderby q.Y, q.X
				select q).ThenBy((Placement q) => q.StableId, StringComparer.Ordinal).ToArray();
```

注意 `array2` 的排序键是 `(Y, X)`——**槽位按"从上到下、从左到右"读**，所以实例排序键天然对应"左上优先"的阅读顺序。

### 模式 4：帧内自适应切片（同一份代码也能给"非算法"的重活分帧）

```csharp
// CaptureFrameBudget.cs:35-67
	internal bool BeginFrame(double previousFrameMs, bool dragging, bool scrolling)
	{
		if (_recent != null)
		{
			if (dragging)
			{
				return false;
			}
			if (CaptureFrameClock.Valid(previousFrameMs))
			{
				_recent[_count++] = previousFrameMs;
				if (_count == _recent.Length)
				{
					if (_baseline > 0.0 && CaptureFrameClock.Median(_recent, _count) > 1.2 * _baseline)
					{
						_feedbackBudget = Math.Max(1.0, _feedbackBudget / 2.0);
					}
					_count = 0;
				}
			}
			_budgetMs = _feedbackBudget;
			_slice.Restart();
			return true;
		}
		if (dragging || ((scrolling || previousFrameMs >= 25.0) && _deferred++ < 3))
		{
			return false;
		}
		_deferred = 0;
		_budgetMs = ((scrolling || previousFrameMs >= 20.0) ? 1 : ((previousFrameMs >= 15.0) ? 2 : 3));
		_slice.Restart();
		return true;
	}
```

```csharp
// CaptureFrameBudget.cs:69-83
	internal bool RunSlice(IEnumerator<int> steps)
	{
		bool flag;
		do
		{
			flag = !steps.MoveNext();
		}
		while (!flag && steps.Current != 1 && CanContinue());
		return flag;
	}
```

调用侧的宿主循环见 `SmartArrangeMod.cs:163-228`：每帧 `BeginFrame(上一帧耗时, IsDraggingItem, scrolling:false)` → 允许就 `RunSlice(steps)` → `EndFrame()`，不允许就纯 `yield return null`。被切的迭代器用 `yield return 0`（本片内可继续）/`yield return 1`（本片到此为止）表达粒度（`InventoryTransaction.cs:144/244/946/997/1023/1026`）。

## 9. 对我们最有用的

### 9.1 直接可借（不需要碰算法）

1. **`CaptureFrameBudget` 这一套（89 行）**：我们的《可能是群友》有"自动整理/上货"，也会一次动十几件；把"每帧 1–3 ms、拖拽时整帧不动、9 帧中位数反馈减半"照抄，就能把一次性卡顿变成"肉眼看不出的渐进"。**这是本档案里性价比最高的 89 行。**
2. **`ArrangeUndo` 接管原版撤销那一格**：`AccessTools.Field(typeof(InventorySortHelper), "undoSnapshots")` + `ForgetNative` + 只保留一组 + 8192 上限。我们若做上货/整理，玩家第一反应就是 `Ctrl+Z`；接管它比自己做一个撤销按钮体验好得多。
3. **`modifiedShape` 原地重排**（§4.1）：仓库/柜台里的"整理"根本不需要 `Expel` + `TryAccept`。先整体 `CheckShapePlacement` 预检、再逐件写 `modifiedShape`、最后 `Validate()` + `ForceRecomputeDynamicBackground()`。**注意方向**：这条与我们库里 `MODDING_PITFALLS_AND_GUIDE.md:103` 的"改形状必须先 Expel"不冲突——那条讲的是**单件改自身占格（形状本身变了，需要换贴图）**；这里改的是**同一形状在不同坐标的落点**（`SetPosition` 为主，`SetOrientation` 为辅且受朝向策略约束）。两者要区分开，别混用。
4. **`InstanceOrder` 的两段式 + `Policy` 自述串**：如果我们的"上货"要按成色/价格/新鲜度决定"谁摆在前面"，把规则写成一条版本化字符串常量并在日志里打出来，排查时能直接看出"这次用的是哪版排序"。
5. **"排除名单"要先写**（`BatchInventoryPolicy`）：展示柜、机器模组舱这类"有别的语义"的容器必须先排除，否则整理一开就把玩家精心摆的橱窗打散。我们的柜台/展示位同理。

### 9.2 值得借但要改造

6. **按密度换序的字典序目标**：我们不需要 20 项。取 5–6 项就够（`同品类分段数`、`同品类贴边数`、`行整齐度`、`空洞惩罚`、`左上聚拢`、`稳定兜底`），配 `aesthetic/balanced/dense` 三套顺序。关键是**末级必须有确定性 tie-break**，否则同一个仓库两次整理结果不同，玩家会以为坏了。
7. **`GenerationWork` 式的"算力记账"而非墙钟**：只有当我们要"同一输入必得同一布局"（比如做预设/分享/回放）时才值得。否则用毫秒预算更简单（我们的硬规范也反对过度抽象）。
8. **补丁骨架统一化**：`HarmonyPrepare` 里查 `StartupCompatibility.Prepare(feature, typeof(本类))`、`HarmonyCleanup` 统一收口、方法体整体 `try/catch → PatchFault(slug, error)`。我们现在的补丁是"每个自己 try/catch 打 LogError"，可以升级成"带 feature 开关的统一骨架"——**好处是某个靶点方法签名一变，只有那一个 feature 自禁用，不至于整模组红字**。
9. **Transpiler 的"调用点计数"防御**（`DisabledSortRightClickPatch.cs:44-53`、`SmartInsertSplitCreated.cs:57-67`）：如果非要写 Transpiler，先 `Where(i => i.Calls(target))` 找出**恰好一处**，`!= 1` 就整功能自禁用并写清原因。这比"随便替换第一个匹配"安全一个数量级。我们规范里"尽量不写 Transpiler"的立场不变，但这条纪律应写进经验库。

### 9.3 看看就好（成本 > 收益）

10. **`Native34Solver` 那 11k 行**：位棋盘 `BigInteger` + 延迟束 + 24 条种子路线 + 枪械互锁。这是"把仓库排列做成一个产品"的投入量级（作者至少迭代到 63 版，DLL 里还冻着两个旧引擎）。我们不需要复刻，**只需要借用它的两个可摘果实**：`ShapeMask`/`Orientation`/`Fit` 的位掩码碰撞检测（可以只做 `ulong` 一档，仓库均 ≤64 格），以及"候选按 5 级启发式排序后取前 N"的剪枝思路。
11. **三个平行命名空间**：这是为了"冻结对照解"付出的 32k 行代价，只在"算法是核心卖点且需要 A/B"时才值得。我们的规模用 git tag + 一份 `LegacySolver.cs` 就够。

### 9.4 与我们既有经验的交叉验证

- 它同样**不读 `realCondition` 判真伪**（§2.3），而且明写 `real-state-appraisal-ignored`。这佐证我们 `AGENTS.md` 里"真伪/成色只认 `realCondition`"是**读者视角**的规则；**排列/展示视角**必须反过来——只许用玩家可见信息，否则等于用布局泄露鉴定结果。这条应记进术语/规范。
- 它对 `SlotMarker.TryAcceptOnce` 的用法（用 `__result` 与前后的 `unitCount` 差**反推是否真的接收成功**）正是我们"精确投放后必须校验落点"的另一半：我们习惯验 `item.parentInventory == targetInv`，它验的是**数量守恒**（`NativeInsertUndoCapture.cs:76-123`）。两者互补。
- 它把"机器模组舱"让给了 `3834649530`（本档案 §2.1 的 `IsMachineModuleArea` 排除）。**同作者两个模组靠"容器语义"分工而非互相 Patch**，这正是我们想要的跨模组共存姿势。

## 10. 对我们的可用点（落地清单）

按"能直接开工"排序，每条都标了成本与风险：

| # | 借什么 | 落在哪 | 成本 | 主要风险 |
| --- | --- | --- | --- | --- |
| 1 | `CaptureFrameBudget` 帧预算器（89 行） | `DeepSpacePawn` 里所有"一次动 N 件"的路径：自动整理、上货、批量摆柜 | 半天 | 无（纯本地逻辑，不碰原版） |
| 2 | `modifiedShape` 原地重排 + 整体预检 + 回滚 | 柜台/仓库"一键整理" | 1–2 天 | 必须区分"改自身形状"（要先 `Expel`）与"改落点"（可直接写）；写前必须 `CheckShapePlacement` 整体预检 |
| 3 | `ArrangeUndo` 接管 `InventorySortHelper` 撤销格 | 同上，给玩家 `Ctrl+Z` | 1 天 | 反射字段名 `undoSnapshots` 属于原版内部名，**需真机验证**；名变要降级为"自己的撤销按钮" |
| 4 | `BatchInventoryPolicy` 式的排除名单（展示柜/模组舱/输出区） | 上货与整理共用的准入判断 | 半天 | 展示柜判据 `EmporiumEntry.Instance.showcaseElement` 要先 `HasInstance` 式判空 |
| 5 | 5–6 项字典序 + 三档密度换序 + 确定性兜底 | 整理目标函数（替代 `OrderBy` 链） | 1–2 天 | 别照抄 20 项；末级 tie-break 必须有 |
| 6 | `InstanceOrder` 两段式 + `Policy` 版本串 | 上货排序（成色/价格/新鲜度） | 1 天 | **只用玩家可见字段**，别把 `realCondition` 混进排序键（会泄露鉴定） |
| 7 | 补丁骨架 `Prepare/Cleanup/PatchFault(feature)` | 我们全部 Harmony 补丁 | 1 天 | 现有补丁要逐个迁移，注意 `HotReloadPrepare/Activate` 时也要重置 feature 表 |
| 8 | Transpiler 的"恰好一处否则自禁用"纪律 | 写进 `开发流程与经验.md` | 半小时 | 无 |
| 9 | `ShapeMask`/`Orientation` 位掩码碰撞（只做 `ulong` 一档） | 若将来要做真装箱 | 2–3 天 | 仓库超 64 格就要 `BigInteger`，别一开始就上 |

**明确不建议借**：`Native34Solver` 整体、三个平行命名空间、`GenerationWork` 的算力记账（除非要做可复现回放）。

## 11. 未解 / 未验证

- `StartupCompatibility`（622 行）的具体降级矩阵没逐个读完，只确认了 `CompatFeature` 11 个 feature 的枚举与 `Available/Disable/Prepare/Cleanup` 四个入口；`Disable` 会**整功能自禁用并保留原版行为**（`DisabledSortRightClickPatch.cs:47` 的调用可证）。
- `ClosedInsertOperation.cs`（3,893 行）只读了入口与阶段划分，其内部"跨页搬运的原子性"未逐段核对。
- 撤销是否真的不落盘，是基于"活树里搜不到 `ES3/File.Write/SaveGame`"的**反向推断（推测）**；没有真机验证读档后 `Ctrl+Z` 是否仍可用。
- `ArrangeUndo` 反射的 `"undoSnapshots"` 是反编译出的字符串字面量，**未在真机断点确认**该字段在当前游戏版本仍存在。
- `ItemNameSources.json` / `NamedTabVocabulary.tsv` 的完整消费链路（`ItemNameResolver` 345 行 + `NamedTabCatalog` 542 行）只看了 schema 与头部，未逐条核对匹配优先级。
