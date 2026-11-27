# Container Link（容器连接）· 工坊 3844123767 蒸馏档案

- 工坊 id：`3844123767`　命名空间：`ContainerLink`　模组 id：`local.storage.containerlink`　版本：`0.7.4`
- 反编译源码：`缓存文件夹\decomp_ws\3844123767`（96 个 `.cs`，约 10 050 行）
- 官方清单：`D:\Steam\steamapps\workshop\content\4348910\3844123767\manifest.xml`，`<Capabilities />` 自闭合 → **零能力模组**
- 实测补丁数：**73 个** `[HarmonyPatch]` 类（每文件一个）；其中 **2 个 Transpiler**（`FridgeRuntimeContentsPatch`、`LinkConnectedClickPatch`），**4 个**用 `TargetMethods()` 一点多打
- 本文所有结论都来自上面这份源码 + 该工坊包的 `manifest.xml` / `使用说明.txt`；凡属"代码里看不到、只能推断"的标**（推测）**

---

## 0. 一句话本质

**它不新增任何容器类型，只做两件事：把原版某一张 `GameGridInventory.inventoryShape` 换成更大的实心矩形，并把组内所有物品的父级统一挂到那一张"主柜页"上。** 于是"两个柜子拼成一张连续网格"= 主柜那一页被放大到 `宽×列数` / `高×行数`，从柜的那一页被换成全 `1`（blocked）的占位形状且必须清空（`LinkCore.ValidateBays` 里 `flag && childItems.Count != 0` 直接抛错）。物品从来没有跨过两个 `GameInventory`，`item.parentInventory` 语义完好无损——它始终指向**真正持有它的那张原版网格**。

### 文件地图（读源码入口顺序）

| 文件 | 行数 | 作用 |
| --- | --- | --- |
| `LinkUI.cs` / `ContainerLinkMod.cs` | 1017 / 34 | 总开关、窗口靠拢手势、提示、窗口缩放兜底、`Guard` 边界捕获；`IMod` 入口只做 `LinkUI.Enable/Disable` |
| `LinkLifecycle.cs` | 603 | **柜体连接图**：标签编码/解码、组重建、从仓页登记、窗口重定向 |
| `LinkCore.cs` | 1285 | 连接/拆分的**事务核心**：`Prepare/Plan/Apply/Verify/RefreshCaches` + `Snapshot` 回滚 |
| `LinkController.cs` / `RectangleLayout.cs` | 397 / 185 | 业务判定；柜阵矩形二叉树 + `L/H/V` 串编解码 + 旧格式兼容 |
| `LinkGroupDrag.cs` / `CrossGroupTransfer.cs` | 553 / 417 | 整组拖动（框选态接管、落点搜索、原子位移）；整组跨网格搬运 + 回滚 |
| `PageFusionCore.cs` / `PageFusionNative.cs` | 1140 / 405 | **分页融合**（并查集 `Forest` + 撤销历史）；页签→网格重定向、缺页遮挡板 |
| `PageFusionUI.cs` / `PageSplitButton.cs` | 850 / 368 | 拖页签拼页的四方向投放面板；往标题栏插原生 `MachineryButton`「分离」 |
| `FridgeRuntime.cs` / `FridgePowerBank.cs` | 480 / 307 | 冰箱温区归属 + 过夜缓存；「冰箱银行」共享供电区 |
| `PhysicalSplitSpill.cs` / `PageSplitSpill.cs` / `LinkSplitUI.cs` | 641 / 251 / 336 | 强拆时跨缝物品的安置、丢失清单与二次确认 |
| 其余 ~70 个 `*Patch.cs` | 12~30 | 一个补丁一个文件，判定体都在上面这些 Core 里 |

---

## 1. 入口与生命周期

### 1.1 IMod 三段（`ContainerLinkMod.cs`）

```text
Init(ModManifest)   → log = new ModLog(manifest)         （只留日志器）
OnEnable()          → LinkUI.Enable("local.storage.containerlink", ex => log.Error(ex))
OnDisable()         → LinkUI.Disable()
```

- **没有任何 `ModHook` 订阅**（全目录 grep 无 `ModHook`），**也没有 `PatchAll`/`new Harmony`**：补丁类由 ModLoader 自己扫 `[HarmonyPatch]` 打上去，模组只负责开关状态。这跟《可能是群友》`FreyaModCore` 里"不要重复执行 loader 已处理的 PatchAll"是同一条约定。
- **也没有 `HotReloadPrepare/Activate`**：热重载靠 `OnDisable/OnEnable` 成对重来，`Disable()` 一次把运行时状态全清。

### 1.2 静态状态在哪里重置

`LinkUI.Reset()`（`LinkUI.cs:338-356`）是唯一的静态清理入口，串联 7 个下游：

```text
FridgePowerBank.Reset()   PageSplitButton.Reset()   PageFusionUI.Reset()
CancelDrag()              CloseFeedback()           LinkSplitUI.Reset()
foreach(WindowFit) target.Restore()   fits = new ConditionalWeakTable<...>()
```

触发点三个 + `OnDisable` 一次，**除场景切换外全是补丁而不是 Hook**：

| 触发点 | 补丁 | 语义 |
| --- | --- | --- |
| 读档 / 新局清档 | `LinkUILoadPatch`、`LinkUIResetPatch` → `PlayerStore.LoadGame`、`PlayerStore.ResetData` **Prefix** | 读档/清档前先把上一局的窗口缩放、电池槽搬移还原 |
| 换场景 | `LinkUI.Enable` 里 `SceneManager.sceneLoaded += SceneLoaded`；`mode == LoadSceneMode.Single && scene.name != "InventoryScene"` 就 `Reset()` | 离开库存场景一律清 |
| 每帧心跳 | `LinkUITickPatch` → `StoreUIManager.Update` **Postfix** → `LinkUI.Tick()` | 只在 `Enabled` 时跑；管拖动会话存活、0.5s 一次的窗口扫描、超时提示框 |

注意 `Enable(string modId, Action<Exception> onError)` 的 `modId` 实参**没被用到**（只赋 `report`/`enabled`），是个可删的尾巴。

---

## 2. 数据结构：怎么表示"已连接"

### 2.1 连接图的存储形态

**没有全局表、没有 ModData、没有磁盘文件**。连接关系存在**每只柜子自己的原版物品状态标签里**，键固定：

```text
item.state.dict["LOCAL_CONTAINER_LINK_V1"]        // 柜体连接（LinkLifecycle.LinkTag）
item.state.dict["LOCAL_CONTAINER_PAGES_V1"]       // 柜内分页融合（PageFusionCore.LayoutTag）
```

值是原版 `TagState`：`Enable()` + `SetString(...)`，**同时写进 `state` 和 `modifiedState` 两份**（`LinkLifecycle.cs:213-217`），这样原版的 `SyncModifiedState` 不会把它洗掉。

柜体连接串：`1|<组guid>|<我的索引>|<组员数>|<各柜页数,逗号>|<列数>|<子组列数>|<矩形树>`

- 组身份 = 一个 `Guid.NewGuid().ToString("N")`，**同一组内每只柜子持有同一 guid、不同索引**；组员关系靠"同 guid + 索引连续"复原。
- 矩形树 = `RectangleLayout.Encode()` 的前序 `L/H/V`（`RectangleLayout.cs:102-128`），`V`=上下拼、`H`=左右拼、`L`=叶子；解码时校验 `count*2-1 == 串长`，再逐节点 `TryJoin` 验矩形。

### 2.2 组（group）的模型与重建

组不是对象，是**一个查询结果**：`LinkLifecycle.TryGetGroup(member, out ordered)`（`:121-153`）

1. 先从标签解出 `Metadata{Group, Index, Count, Columns, SubgroupColumns, Pages[], Layout, Tree}`；
2. 遍历 `member.parentInventory.childItems`，挑出"存活 + 同 guid + `Compatible()`（同 identifier、同 Count/Columns/Tree/Pages）"的对象，按 `Index` 填数组；
3. 任一槽位缺失、索引重复、`array[Index] != member` → **返回 false，叫"组残缺"**，走 `TryRecoverIncomplete`。

运行时缓存两张 `ConditionalWeakTable`，避免每次点东西都解串：

| 缓存 | 键→值 | 用途 |
| --- | --- | --- |
| `LinkLifecycle.Records` / `LinkLifecycle.DonorPages` | `GameItem → Metadata` / `GameInventory → GameItem` | 串没变就复用解码结果；后者回答"这张网格是某柜的从仓页"（`IsDonorInventory`） |
| `PageFusionCore.Owners` / `PageFusionCore.Records` | `GameInventory → PageOwner` / `GameItem → CachedRecord` | 分页融合的页归属与解码缓存 |

### 2.3 分页融合的模型

`PageFusionCore` 把"一个柜子内的多张原生页"建构成**并查集** `Forest`（`int[] Root/X/Y`，`:43-74`）：

- `Root[i]==i` → 该页是宿主页，`Shape()` 会把它扩成 `宽*(最大列号+1) × 高*(最大行号+1)` 的实心矩形，并把组内其它页的位置填成 `1`（遮挡）；
- `Root[i]!=i` → 从属页，`Shape()` 直接返回 `Rectangle(W,H,blocked:true)`，且**必须为空**；
- 只允许"行列相邻 + 组成完整矩形"的合并（`Joined()` 里 `IsRectangle()` 校验，`:769-772`）；`Record.History` 是撤销栈（上限 `count-1` 条，持久化时再限 4 条），提供 `TryUndo/TryUndoGroup`。

编码：`1|<页数>|<W>|<H>|<root,x,y;...>|<历史!历史>`；`Read()` 里 `text.Length > 2048` 或字段数 ≠ 6 直接抛 `InvalidOperationException("分页连接记录无效…")`，**宁可不认也不半信**。

---

## 3. 接管原版的 73 个 HarmonyPatch

必须拦的共同原因只有一条：**这些方法都会"逐个柜体"处理，而模组的对象是"一组柜体"**。逐个体通过 → 组被打散（卖出/回收/扩容一个成员）或格子算错（放置/排序按单页尺寸算）。

73 = 生命周期 3（见 §1.2）+ 下面 A~H 共 70 个。

### A. 打开 / 双击 / 窗口重定向（7 个）

| 补丁 | 原版方法 | 为什么必须拦 |
| --- | --- | --- |
| `LinkConnectedOpenPatch` **Prefix(ref GameItem newItem)** | `ItemMouseDoubleClickHandler.OpenContentAction` | 把参数直接改写成 `LinkConnectedWindow.Host(item)`＝组首柜，`[HarmonyPriority(800)]` 抢在别人前面 |
| `LinkConnectedClickPatch` **Transpiler** | `ItemMouseDoubleClickHandler.ClickAction` | IL 级把 `GameItem.get_contentWindow` 换成 `LinkConnectedWindow.Window`、`CloseContentWindow` 换成 `Close`，这样"双击任一组员开关同一窗口"不用重写整个方法 |
| `LinkDonorWindowPatch` / `LinkDonorPositionedWindowPatch` **Prefix→false** | `PixelWindow.Show()` / `PixelWindow.Show(Vector2,Vector2,bool)` | 从柜的窗口永不允许自己 `Show`；`RedirectDonorWindow` 有宿主就展示宿主窗口，`__result` 直接给宿主。两个重载必须各打一次 |
| `LinkUIShowPatch` / `LinkUIShowAtPatch` **Postfix** | `PixelWindow.Show` ×2 | 窗口一显示就 `LinkUI.WindowShown`：装电池银行、算窗口缩放、刷「分离」按钮 |
| `LinkUIHintPatch` **Postfix** | `GameItemElement.GetTooltipBasic` | 往 `RichTextBuilder` 追加"可与同型号柜体融合"等说明 |

### B. 拖拽：整组框选与单柜拖动桥接（10 个）

| 补丁 | 原版方法 | 为什么必须拦 |
| --- | --- | --- |
| `LinkSingleGroupBridgePatch` **Prefix→false** | `ItemMouseDragHandler.StartDrag` | 点到**已连接**柜体时，原生单物品拖动必须让位；转成 `LinkGroupDrag.BeginFromNativeDrag`（把整组塞进 `ItemMultiSelectHandler` 的 `selectedItems/selectedHomes` 再 `StartGroupDrag`） |
| `LinkGroupStartPatch` Prefix+Finalizer | `ItemMultiSelectHandler.StartGroupDrag` | 选中集合里有组员时，先把组读成"完整合法组"，否则 `Cancel()`；用 `QueryScope`（`[ThreadStatic]` 深度计数）让 `MayRemove/MaxNumRemove` 在查询期间回答"可拿" |
| `LinkGroupFormationPatch` | `IsFormationValid` | 原生按单个物品算落点；这里要求**整组每个成员**都 `CheckShapePlacement` + `MayHaveValidInventorySlot` + 层占用不冲突 |
| `LinkGroupDropTargetPatch` | `UpdateDropTarget` | 原生只亮一个格子；这里 `CrossGroupTransfer.FindFormation` 全网格搜索一块能放下整组的连续空位并高亮全组 |
| `LinkGroupEndPatch` | `EndGroupDrag` | 松手时若目标格放不下就**整组留在原位**（`LinkGroupDrag.Finish` → `TranslateAtomically`） |
| `LinkGroupDeltaPatch` | `TryGetPreciseDelta` | 原生算出的 dx/dy 是给单个物品的，这里只在 `CrossGroupTransfer.Supports(inventory)` 下接管 |
| `StorageTabMarqueeGuard` | `ItemMultiSelectHandler.IsPointerOverDragSurface` | 0.7.1 的修复：从页签起拖不能误触发库存框选，靠 `PageFusionUI.IsPageDragSurface` 补判 |
| `LinkUIBeginDrag/Drag/EndDragPatch` ×3 | `TreeNodeRender.OnBeginDrag/OnDrag/OnEndDrag` | 窗口"靠拢融合"手势：拖动窗口背景（`background` 节点）才起手，靠 `DockScore`/`DockGestureScore` 判接缝 |

### C. 排序 / 锚定 / 拿取 / 放置换算（15 个）

- `AnchorLinkedBaysDuringSort` → `InventorySortHelper.IsAnchored`：**柜体参与排序会被挪走**，直接回答"已连接=锚定"。
- `LinkMayRemovePatch`、`LinkMaxNumRemovePatch` → `GameItem.MayRemove / MaxNumRemove`：默认整组不可拿，只有 `LinkGroupDrag.AllowsRemoval`（正在拖这一组）才放行。
- `LinkSlotAcceptancePatch` → `SlotMarker.TryAcceptOnce / AcceptUnchecked`：从仓页与组员本身一律 `__result = 0`，切断"绕过检查塞进来"。
- `LinkDonorPlacementPatch` → `GameGridInventory.MayHaveValidInventorySlot`：从仓页不提供可用格。
- `LinkedIconAdmissionPatch`、`LinkedIconSlotPatch` → `GameItem.MayHaveValidInventorySlot / TryFindOneValidInventorySlot`：**把物品拖到任一组员图标上都能进共享库存**，最终必须落到 `AllowsOriginalPage`（各柜原有收纳限制仍生效）。
- `PageFusionDonorPlacementPatch`、`PageFusionDonorUncheckedAcceptancePatch`、`PageFusionSlotAcceptancePatch`、`PageFusionSlotValidityPatch`、`PageFusionShapePlacementPatch`、`PageFusionTrySlotPatch` → `GameGridInventory` 的 `MayHaveValidInventorySlot / UncheckedAccept / CheckInventorySlotStillValid / CheckShapePlacement / TryInventorySlot`＋`SlotMarker`：融合页的"画布"是宿主页那块扩大矩形，**从属页不可插入、超出画布不可插入**。
- `PageFusionTabInventoryPatch`、`PageFusionSelectedStoragePatch` → `MachineryTabs.GetTabInventory(int)`、`MachineryContainerUtils.CollectSelectedTabStorage`：页签要返回**宿主页**（`DropInventory`→`ResolveHostIndex`），否则原版各种"取当前页网格"的路径会拿到空壳从属页。

### D. 出售 / 以物易物守卫（6 个）

`LinkedTradeGuards.ContainsLinkedBay` 递归扫 `GraphNodeStorage.children`，只要子树里有一只带 `LOCAL_CONTAINER_LINK_V1` 的柜子就整单拒绝，并 `LinkUI.Notify("请先拆开连接柜，再出售或交换柜体…")`：

| 补丁 | 原版方法 | 判定 |
| --- | --- | --- |
| `LinkedContainerSaleAvailability` | `PlayerStore.CanSellThisItem` | `__result=false`（灰掉按钮） |
| `LinkedContainerSaleGuard` | `PlayerStore.SellItem` | `__result=false` + 提示 |
| `LinkedBulkSaleGuard` | `PlayerStore.SellAllItemFromTable` | 扫 `EmporiumEntry.Instance.backInvinvElement` 整棵 |
| `LinkedBarterConditionGuard` | `BarterCondition.IsItemValid` | 以物易物可选项排除 |
| `LinkedValueBarterAvailability` / `LinkedValueBarterGuard` | `OverlayHandler.GetScore` / `SwapTrade` | 用 `___currentBarterSellInventory/___currentBarterBuyInventory` 两个私有字段判：可交易性返回 `-1`，真正交换时再拦一次 |

### E. 扩容 / 回收 / 冰箱升级（9 个）

成对打"能不能"和"真的做"两个入口，缺一不可：

- 储藏柜：`LinkCanExpandPatch`＋`LinkExpandActionPatch`（`StorageExpansionHelper.CanExpand/Expand`）、`LinkCanSalvagePatch`＋`LinkSalvageActionPatch`（`SalvageHelper.CanSalvage/TrySalvage`）、`PageFusionCanExpandPatch`＋`PageFusionExpandPatch`。
- 冰箱：`LinkedFridgeExpansionAvailability`＋`LinkedFridgeExpansionAction`（`MachineFridge.CanUpgradeStorage/UpgradeStorage`）——**升级会改 `FRIDGE_STORAGE_UPGRADE_COUNT_TAG` 从而改温区尺寸**，必须先拆；`LinkedFridgeStorageRestore` → `MachineFridge.ApplyStorageSize` 还不许原版给已连接冰箱重算尺寸。

理由（`LinkCore.ValidateBays` 里明写）：从仓页的 `inventoryShape` 必须严格等于 `Rectangle(宽,高,blocked:true)`，任何"原版改了尺寸"的操作都会让它与记录不符 → 抛"网格尺寸或遮挡已被其他修改改变，操作已取消"。

### F. UI 边界 / 按钮 / 读档还原（12 个）

| 补丁 | 原版方法 | 干什么 |
| --- | --- | --- |
| `LinkUIBoundsPatch` **Prefix→false** | `PixelWindow.FixBounds` | 完全接管边界修正：超大网格窗口按 `FitRatio` 等比缩到屏幕安全区内再 `Clamp`，原版逻辑被跳过 |
| `PageSplitButtonLayoutPatch` | `PixelWindow.MachineryValidate` | 布局前 `BeforeLayout`（条件不成立就摘掉按钮）、布局后 `AfterLayout`（重排整排 `headerButtons` 的 `anchoredPosition`） |
| `PageSplitButtonHidePatch` / `DestroyPatch` | `PixelWindow.Hide` / `Destroy` | 窗口藏了/销毁了必须把自建 `MachineryButton` 摘掉并 `Destroy`，否则泄漏 |
| `PageFusionRefreshTabsPatch` **Postfix** | `MachineryTabs.RefreshTabs` | 重画页签选中态（融合组内所有成员页签同时高亮） |
| `PageFusionGridOverlayPatch` **Postfix** | `GameGridInventory.Validate` | 重建"缺页"遮挡板（`HoleOverlay`，深灰半透明块） |
| `PageFusionTabClickPatch` / `TabDoubleClickPatch` / `BeginDrag`/`Drag`/`EndDragPatch` | `TreeNodeRender.OnPointerClick/OnPointerDoubleClick/OnBeginDrag/OnDrag/OnEndDrag` | 页签拖拽拼页；用 `suppressClick + suppressUntil = Time.unscaledTime + 0.3f` 吞掉"拖完之后的误点击"，`data.eligibleForClick = false` 再保一道 |
| `LinkRestoreTabsPatch` **Postfix** | `StorageExpansionHelper.RestoreTabs` | 读档后原版刚把页签形状恢复成原生尺寸，立刻 `RestoreLoadedItem` 把记录里的扩大矩形重新铺回去（柜体 + 分页两套） |

### G. 过夜模拟缓存 + 冰箱银行挂接（7 个）

| 补丁 | 原版方法 | 干什么 |
| --- | --- | --- |
| `FridgeNightlySimulationCachePatch` | `ChemicalHelper.RunNightlyChemicalSimulation` | `[HarmonyPriority(800)]` Prefix 开一个 `[ThreadStatic] SimulationCache`，**Finalizer** 里恢复上一层 —— 保证异常也能收口 |
| `FridgeNightlyPlacementCachePatch` / `FridgeNightlyShapeCachePatch` | 特性实参无法解码（见下） | Prefix 存旧 `parentInventory`、Postfix 回灌 `PlacementChanged`；另一个 Postfix 回灌 `ShapeChanged` —— 都是让缓存失效 |
| `FridgeRuntimeContentsPatch` **Transpiler** | `MachineFridge+<>c__DisplayClass26_0.<Fridge>b__4`、`MachineFridge.CoolZoneContainers(GameGridInventory,double,double)` | 把这两处 `GameInventory.get_childItems` 换成 `FridgeRuntime.ContentsForZone`，让"这台冰箱的冷藏区内容"变成"共享大网格里属于我这一格的那部分" |
| `FridgePowerLayoutPatch` | `MachineryIntake.Validate` | Prefix 改 `___requestedWidth/Height`（给电池银行留位），Postfix 重新摆电池槽 |
| `FridgeBankHide` / `FridgeBankDestroy` | `PixelWindow.Hide()` / `PixelWindow.Destroy` | 宿主窗口一藏/一销毁就 `FridgePowerBank.Hide`：电池槽 `SetParent` 回原位、位置、`inner` 尺寸、`vents`、`requestedWidth/Height` 全部还原（`[HarmonyPriority(800)]` 抢在窗口真正销毁前） |

> **两处未解**：`FridgeNightlyPlacementCachePatch` / `FridgeNightlyShapeCachePatch` 的 `[HarmonyPatch(...)]` 实参在反编译产物里是 `/*Could not decode attribute arguments.*/`。从签名（Prefix 只取 `__instance` 与旧 `parentInventory`、Postfix 只取 `__instance`）推断，目标应是 `GameItem` 上"更换归属容器"与"形状/状态刷新"的两个方法（候选 `set_parentInventory`、`SyncModifiedState` 或 `set_modifiedShape`）——**（推测，未验证）**。

### H. 工具使用与右键菜单（4 个）

| 补丁 | 原版方法 | 干什么 |
| --- | --- | --- |
| `LinkTargetQueriesPatch` | `GameItem.MayTarget` / `CanTarget`（`TargetMethods()` 两个） | 工具能不能作用在这一对上由 `LinkLifecycle.BlocksToolInteraction` 统一回答，`__result=false` |
| `LinkTargetActionPatch` | `GameItem.Target` | 同上，真正动手时再拦一次（**能/做成对打**） |
| `LinkedContextAvailability` **Postfix** | `ItemContextHandler.UpdateConditions` | 已连接且已拥有 → 强制 `___canToggle = true`，让原版右键菜单出现「切换」 |
| `LinkedContextToggle` | `ItemContextHandler.TriggerButton` | 认准 `___buttonToggle` 这一个按钮，改走 `LinkSplitUI.Split(currentItem)`（即"右键→切换=沿最近一次接缝拆分"） |

`BlocksToolInteraction`（`LinkLifecycle.cs:562-585`）的判定值得抄：**动从柜 → 一律拒绝；动从柜冰箱 → 只放行 `common_electronic`；动已连接柜 → 只放行 `SALVAGE_TOOL_TAG`（为了给出"先拆开"的提示而不是静默）与 `STORAGE_EXPANSION_KIT_TAG`（同样只为提示）**——用"让工具进入流程再在动作层拒绝"换来可解释的提示，而不是让点击毫无反应。

---

## 4. 与原版容器的边界

### 4.1 `parentInventory` 语义：不回避，而是"每一件都只有一个真父级"

模组确实大量搬动物品，但**每条路径都维持同一个不变式**：`item.parentInventory == 那张真正持有它的 GameGridInventory`。

- `LinkCore.Apply`（`:996-1029`）：先对快照里每张网格 `item.RemoveParent(grid)`，再对 `layout.Placements` 逐件 `item.AddParent(目标网格)` + 写 `modifiedShape` + `SetRenderingParent`。**目标网格永远是 `grids[destination.Start][l]`，即组首柜的那一页**。
- `LinkCore.Verify`（`:1105-1147`）：改完逐项回验 `item.parentInventory == grid`、形状逐格相等、`IsRenderingChildOf(grid)`、`unitCount` 与原点 `modifiedXOrigin/YOrigin` 未变；任一不符 → 抛异常 → `Snapshot.Restore()` 全量回滚。`PhysicalSplitSpill.ValidateGrid`（`:100-135`）则在动手**之前**先验"每件物品父级正确、无重复引用、同层不重叠"，不通过就"未作任何修改"。

所以别的模组读 `item.parentInventory` / `GraphUtils.FindParentEquals` 时看到的一切都是**合法的原版容器**，只是那个容器的格子比别人以为的多。

### 4.2 跨接缝物品怎么处理

三处不同语境，结论一致：

1. **连接后**：长物品"跨缝"在实现上毫无特殊——它落在**同一张被放大的宿主网格**里，接缝只是视觉上两柜的边界。`Plan` 里给每个占用格算 `x + source.X*width` 找目标分区，跨分区的物品本可直接放（`Apply` 前把形状原点整体平移 `(source.X - destination.X) * width`）。
2. **拆分时**：`Plan` 逐件判断"占用格是否落在多个目标分区"→ `LastSplitBlocker/LastSplitPage` 记下第一件跨界物品并抛"跨过左右拆分接缝（第 N / N+1 列之间）。请先移动该物品再拆分"，UI 随后高亮它（`LinkSplitUI.HighlightCells` 逐格画橙块）。
3. **强拆时**：`PhysicalSplitSpill.Complete`（`:137-249`）把跨界物品当"待安置"，候选目标按序：**玩家柜台 `EmporiumEntry.backInvinvElement` → 各物理根的同页码页**；冰箱额外用 `layout.PreservePageRole` 禁止"冷冻食物塞进冷藏室"。`Pack()` 贪心选位后仍放不下的进 `wouldLose`，先 `ValidateLossContents`（连子物品一起评估）再要求二次确认。页签强拆走另一条 `PageSplitSpill.StagePageSplit(layout, crossing, counter, trash)`，顺序是柜台 → 垃圾桶（与作者说明一致）。

### 4.3 怎么和别的模组"静默让位"

代码里**没有任何"检测某模组是否在场"的反射/程序集扫描**（grep `ModLoader/AppDomain/GetTypes` 全空）。它靠四层"让位"：

1. **每一个 Prefix 的第一句都是"不关我事就 `return true`"**：`if (!LinkLifecycle.IsLinked(...)) return true;`、`if (!LinkGroupDrag.HasLinkedSelection(...)) return true;`、`if (!PageFusionNative.TryResolve(...)) return true;`。没连过的柜子，原版行为与别人补丁的行为一字不差。
2. **`LinkUI.Enabled` 大闸**：所有入口先判 `enabled`，`OnDisable` 一关全部退化为原版——补丁留着但全部短路。
3. **只读别人的状态一律走官方 `ModReflect`**（13 个文件、零能力）：`ModReflect.Get<List<GameItemElement>>(handler,"selectedItems")`、`ModReflect.Call(handler,"StartGroupDrag")`、`ModReflect.Set(handler,"precisePlacementActive",true)` …… 不复制、不缓存别人的内部字段语义。
4. **只有一处显式的补丁顺序协商**（`PageSplitButtonLayoutPatch.cs:9-16`）：

```csharp
	[HarmonyPriority(800)]
	[HarmonyBefore(new string[] { "mod.local.machinewindow.fold" })]
	private static void Prefix(PixelWindow __instance)
	...
	[HarmonyPriority(0)]
	[HarmonyAfter(new string[] { "mod.local.machinewindow.fold", "mod.dbfoxtw.window-tabs" })]
	private static void Postfix(PixelWindow __instance)
```

即：**标题栏布局前的"要不要按钮"要排在折叠类模组之前，布局后的"重排按钮"要排在它们之后**。这是"不改别人代码、只声明相对顺序"的最小让位写法——比"检测到某模组就退化"更省事，也更不容易被别人的更新打断。

---

## 5. 风险与限制

1. **停用/卸载前必须拆干净**。`manifest.xml` 与 `使用说明.txt` 都写了"停用前请先拆开融合页和柜体并保存"，理由是"已连接柜子的宽高依赖本模组在读档时还原"——扩大后的 `inventoryShape` 是**读档后由 `LinkRestoreTabsPatch` 重建**的运行时状态（原版 `StorageExpansionHelper.RestoreTabs` 先恢复原生尺寸），记录只剩那条 tag。tag 留着但模组不在 → 柜子回到单柜尺寸，里面跨缝的物品会被原版 `Validate()` 判为越界（**推测**：未在真机试过卸载，但 `RestoreTabs` 的接管顺序与作者说明一致）。
2. **过夜缓存会返回空列表**。`FridgeRuntime.SimulationCache.Contents` 在 `LinkCore.IsMutating || SaveManager.IsDecoding` 时**直接 `return new List<GameItem>()`**（`:218-221`、`:237-240`）。这是刻意的"此刻别算"，但任何在此期间读冷藏内容的逻辑（原版过夜化学模拟、别的模组）会看到**空冰箱**。同理 `FridgeRuntime.FreshContents` 在组不完整/尺寸不符时返回空。
3. **冰箱供电按"第一个实际占用格"归属**。`FreshContents`（`:352-410`）用 `FirstOccupiedCell` 定温区归属→决定哪台冰箱供电；作者说明："缺电的区域不会借用邻机电池"。`FridgePowerBank` 把各台 `MachineHelper.GetBatterySlot` 搬到宿主 `power` 面板的 `MachineryIntake.inner` 下（`Socket` 记原 parent/`anchoredPosition`），`Hide` 时必须还原 parent、位置、`inner.sizeDelta/anchoredPosition`、`vents`，以及 `requestedWidth/requestedHeight` 四项，改漏一项都会让原版窗口错位或下次布局崩。
4. **垃圾桶要手动刷图标**。`CrossGroupTransfer.Commit`（`:388-415`）在整组进出 `trashcanInvElement` 后必须 `SpriteHelper.UpdateTrashcanSprite`，进出 `backInvinvElement` 要 `PlayerStore.Instance.OnWeightedAreaChanged()`，进出 `invElement` 要 `OnMainInvAreaChange()`；这三步包在 `try/catch` 里只 `Debug.LogWarning`——**搬成功了但 UI 没刷**是允许的失败态。
5. **强拆的"确认"必须防过期**。`LinkSplitUI.Attempt` 记 `Group` + `LinkRecord`（组 tag 原文）+ 每件 blocker 的 `ItemStamp{unitCount, parentInventory, modifiedShape}`；点确认时 `Matches()` 全部重验，不一致就"柜子或待处理物品已变，请重新选择拆分"。弹窗还带票据号 `pendingDialog++`，防止两次弹窗串台。
6. **尺寸上限与 schema 兼容**：`ValidateRectangleDimensions` 用 `long` 算 `宽*列 × 高*行`，超 `int.MaxValue` 就"超出原生网格可表示的尺寸"（否则 `GridShape` 的 `byte[]` 会先炸在别处）；柜体标签 `Read()` 接受 5~8 段（老版本无矩形树，走 `RectangleLayout.Legacy` 推断），分页标签只接受 6 段、长度 ≤2048、历史 ≤4 条，不合规就视为"没有连接"。
7. **只有一个"修半残组"的出口**：`LinkLifecycle.TryRecoverIncomplete`（`:260-364`）用 BFS 沿 `parents/children` 找同 guid 幸存者（先判是否在 `EmporiumEntry` 的 17 个根容器子树内），要求"所有现存页都已清空"，然后逐只 `RestoreUnlinkedItem` 回原生尺寸并 `Clear()` 标签；失败就整批 `RestoreLoadedItem` 回滚并 `Debug.LogError`。
8. **异常策略与反编译损失**：Core 层动不动 `throw new InvalidOperationException("…，未作任何修改。")`（全项目最值得学的一点），只有 Unity 帧回调 / Harmony 入口用 `LinkUI.Guard(...)` 接住并 `report?.Invoke(ex)`（`ContainerLinkMod` 传进来的 `ModLog.Error`）；源码本身零注释（release 构建），两处 `[HarmonyPatch]` 实参丢失，语义只能靠异常文案与 `使用说明.txt` 反推。

---

## 6. 可复用模式

### 模式 1：用原版物品 `state` 标签持久化自定义图结构（零 ModData、零文件）

要点：自己的东西只写自己的键；字符串**首段放版本号**；每只成员各存一份"全组描述 + 我的索引"，组由此可重建；写 `state` 的同时写 `modifiedState`，防止原版状态刷新把它洗掉；解码端**任何不合规就整体不认**，绝不半解析。

```csharp
// LinkLifecycle.cs:203-217
		for (int j = 0; j < array2.Length; j++)
		{
			array2[j] = array[j].ToString(CultureInfo.InvariantCulture);
		}
		string text3 = "|" + ordered.Length.ToString(CultureInfo.InvariantCulture) + "|" + string.Join(",", array2) + "|" + layout.Columns.ToString(CultureInfo.InvariantCulture) + "|" + ((layout.Count == 2) ? layout.Columns : layout.First.Columns).ToString(CultureInfo.InvariantCulture) + "|" + layout.Encode();
		TagState[] array3 = new TagState[ordered.Length];
		for (int k = 0; k < array3.Length; k++)
		{
			array3[k] = new TagState("LOCAL_CONTAINER_LINK_V1").Enable().SetString("1|" + text2 + "|" + k.ToString(CultureInfo.InvariantCulture) + text3);
		}
		for (int l = 0; l < ordered.Length; l++)
		{
			ordered[l].state.dict["LOCAL_CONTAINER_LINK_V1"] = array3[l];
			ordered[l].modifiedState.dict["LOCAL_CONTAINER_LINK_V1"] = array3[l].Clone();
		}
```

解码端**一票否决**（`LinkLifecycle.cs:383-387`）：`Split('|')` 后必须 `Length` 在 5~8、首段 `"1"`、guid 可解析且非空、索引与组员数合法（`result2 < result3`），任一条件不成立 `return false`——**当作"没有连接"，保留原样不修**，绝不半解析。

### 模式 2：快照 → 应用 → 三重校验 → 失败整体回滚

任何"会同时改动很多物品"的操作，先把所有涉事网格/物品的 `parentInventory`、`modifiedShape`、`state/modifiedState`、`unitCount`、原点存进 `Snapshot`，改完再 `Verify` 两遍（改完一遍、刷缓存后再一遍），出错 `Snapshot.Restore()` 连 `contentWindow.Show()` 的可见性都还原。这套结构比"到处判空 + 部分修"更容易证明安全。

```csharp
// LinkCore.cs:598-618
		try
		{
			Transition transition = Prepare(ordered, firstCount, stacked, split);
			snapshot = new Snapshot(ordered, transition.Grids);
			Layout layout = Plan(ordered, transition);
			foreach (GridSnapshot grid in snapshot.Grids)
			{
				layout.Raw.Add(grid.Grid, grid.Raw);
			}
			flag = true;
			mutating = true;
			Apply(layout, snapshot);
			Verify(layout, snapshot);
			RefreshCaches(layout, snapshot);
			Verify(layout, snapshot);
			MarkTargets(ordered, transition.Targets);
			for (int i = 0; i < ordered.Length; i++)
			{
				Refresh(ordered[i]);
			}
			Verify(layout, snapshot);
			return true;
		}
```

配套的三条纪律（同文件）：`Prepare()` 第一句就查 `if (mutating || PageFusionCore.IsMutating) throw`（重入保护）；`catch` 里 `snapshot.Restore()` 后把原异常消息**拼上**"恢复原布局失败：…"；`finally` 里按 `flag` 决定是否清 `mutating`。

### 模式 3：Transpiler 自校验——替换点数不对就整批拒绝

改原版 IL 最怕"上游一改，替换一半"。这里给了一个极简且硬气的做法：**统计替换次数，与预期不符直接抛异常**，宁可不生效也不半接线。它还示范了"一点多打"：`TargetMethods()` 里用 `ModReflect.Target(类型名, 方法名, Type[])` 精确取重载（含编译器生成的闭包 `MachineFridge+<>c__DisplayClass26_0.<Fridge>b__4`），避免 `AccessTools` 猜错。

```csharp
// FridgeRuntimeContentsPatch.cs:28-48
	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
	{
		MethodInfo getter = AccessTools.PropertyGetter(typeof(GameInventory), "childItems");
		MethodInfo view = AccessTools.Method(typeof(FridgeRuntime), "ContentsForZone", (Type[])null, (Type[])null);
		int replacements = 0;
		foreach (CodeInstruction instruction in instructions)
		{
			if (CodeInstructionExtensions.Calls(instruction, getter))
			{
				instruction.opcode = OpCodes.Call;
				instruction.operand = view;
				replacements++;
			}
			yield return instruction;
		}
		int num = ((__originalMethod.DeclaringType == typeof(MachineFridge)) ? 1 : 4);
		if (replacements != num)
		{
			throw new InvalidOperationException("The native fridge contents access changed; refusing partial cooling integration.");
		}
	}
```

### 模式 4：改原版网格形状，但不改 `parentInventory` 语义

```csharp
// LinkCore.cs:996-1021
	internal static void Apply(Layout layout, Snapshot before)
	{
		foreach (GridSnapshot grid in before.Grids)
		{
			GameItem[] items = grid.Items;
			for (int i = 0; i < items.Length; i++)
			{
				if (!items[i].RemoveParent((GraphNodeStorage)(object)grid.Grid))
				{
					throw new InvalidOperationException("无法从原生网格解除物品父级。");
				}
			}
			grid.Raw.Clear();
		}
		foreach (GameGridInventory grid2 in layout.Grids)
		{
			SetShape(grid2, layout.Shapes[grid2]);
		}
		foreach (Placement placement in layout.Placements)
		{
			layout.Raw[placement.Grid].Add(placement.Item);
			if (!placement.Item.AddParent((GraphNodeStorage)(object)placement.Grid))
			{
				throw new InvalidOperationException(placement.Label + " 无法接入目标原生网格。");
			}
```

（摘录止于此；后续是 `item.modifiedShape = placement.Shape.Clone()` 与 `SetRenderingParent`，见 `LinkCore.cs:1021-1028`）

`SetShape` 本体只有一句，走官方反射器改私有字段：`ModReflect.Set((object)grid, "inventoryShape", (object)shape)`（`:1154-1157`，读物品列表用 `ModReflect.Get<List<GameItem>>(grid, "items")`）。**关键纪律**：形状只对"宿主页"放大，从属页一律 `Rectangle(w,h,blocked:true)` 且必须为空——这样任何第三方按 `childItems` 遍历的代码都不会遇到"同一件物品在两个容器里"。

### 补充（不展开）：往原生窗口标题栏插一个自己的按钮

`PageSplitButton` 的做法可整段照搬：`new MachineryButton("分离", callback, 32, 16)` → `window.AddHeaderButton(button)` → 在 `MachineryValidate` 的 Prefix 里 `BeforeLayout`（`State.Matches()` 不成立就直接摘掉）、Postfix 里 `AfterLayout`（`MoveFirst(headerButtons)` 后从 `window.widthPixels - 2` 起逐个数减宽度重排 `anchoredPosition`）；`Hide`/`Destroy` 两个补丁负责 `SetCallback(null) + RemoveHeaderButton + Destroy(gameObject)`；状态用 `ConditionalWeakTable<PixelWindow, State>` 存，`State` 记窗口/页签/选中索引/标签原文，任一变化就重建。

---

## 对我们的可用点

1. **《深空电玩》收银盘（6×6）/ 展示柜的"拼台面"**：模式 1+4 可以直接做出"两台机台 + 一个货架拼成一张连续台面，长物品跨接缝摆放"，而且持久化只写自己的 tag，不碰原版存档结构；拆分时用模式 2 的快照回滚兜住"跨缝物品无处安放"。`GridShape` 放大走 `ModReflect.Set(grid,"inventoryShape")`，无需新容器类型。
2. **《深空电玩》机器过夜（刻录机/机台夜跑）**：`FridgeRuntime.SimulationCache` + `ChemicalHelper.RunNightlyChemicalSimulation` 的 Prefix/Finalizer 组合，是"过夜批量模拟里要按共享网格分区读容器内容"的现成骨架——`[ThreadStatic]` 缓存 + 归属校验（`MemberStamp.IsCurrent` 比对 parent/window/tag/升级等级）+ 失效钩子，能避免过夜循环里每台机器重建一遍归属表。
3. **《深空电玩》"改原版 IL 就自校验"**：模式 3 的替换点数断言建议直接抄进我们所有 Transpiler（目前我们 0 Transpiler，一旦为机器内容/冷却做 IL 替换，这是最便宜的保险）。同时 `ModReflect.Target(类型名, 方法名, Type[])` 的写法可以替掉 `AccessTools` 猜重载。
4. **《深空当铺：可能是群友》塞伦娜收购 / 群友交易守卫**：`LinkedTradeGuards.ContainsLinkedBay`（递归扫 `GraphNodeStorage.children`）+ 六处守卫（`PlayerStore.CanSellThisItem/SellItem/SellAllItemFromTable`、`BarterCondition.IsItemValid`、`OverlayHandler.GetScore/SwapTrade`）是"**某个自定义状态一旦在场就整单拒绝，并给出人话提示**"的完整模板，正合我们"塞伦娜只收合格水"这类判定：可用性补丁灰按钮 + 动作补丁兜底拒绝，成对打，别只打一个。
5. **通用 UI**：补充范式里的 `AddHeaderButton` + `MachineryValidate` Prefix/Postfix + `HarmonyBefore/After` 排序，是给原版容器窗口加自定义按钮（深空电玩机台面板、群友客户档案）而不与折叠类模组打架的最省事做法；`LinkUI.Guard` 那套"边界捕获 + 交给 `ModLog.Error`"的写法可以直接替换我们散落的 `catch {}`。
