# 3845671416 · 存储控制终端 / Storage Control Terminal —— 反编译蒸馏档案

> 源码：`缓存文件夹\decomp_ws\3845671416`（18 个 `.cs`，5731 行，命名空间 `StorageControlTerminal`，`AssemblyInfo` 之外的代码全在 `StorageControlTerminal\`）
> 静态透视报告：`缓存文件夹\decomp_ws\_reports\3845671416.txt`（ModHook 8 个 / ModHelper 14 个 / ModReflect 3 个 / 核心触点 `CustomUIBuilder, CustomUIManager, PixelWindow`）
> 原版接口引用取自本工作区 `共享开发\reference\Assembly-CSharp\`（下文标 `原版:` 的都是在那份源码里核对过的，不是猜的）
> 标注约定：无标注＝源码可证；「（推测）」＝读不到（例如 manifest、贴图）只能按现有证据推断。

## 0. 本质一句话

**它没有一行 Harmony，用 8 个 `ModHook` 事件 + 原版公开 API + `CustomUIManager` 自绘窗口，把「店里所有带内容窗口的物品」抽象成 `BoundBayInfo → BoundTabInfo → StoredItemEntry` 三级索引，再把所有搬运动作交回原版 `GraphUtils.CanAccept/TryAcceptAll` 执行。** 唯一的反射只有一处，且是对**另一个模组**（Smart Inventory Arrange）的能力探测。

物料清单：

| 文件 | 行数 | 角色 |
| --- | --- | --- |
| `ModMain.cs` | 535 | `IMod` 入口：挂/卸 8 个钩子、`ModConfig`+游戏内选项双配置、狂野网络升级、发明家货源、刷物品投放 |
| `TerminalItem.cs` | 181 | 物品工厂（`Create()`）、`GameItemFunc` 行为声明、tooltip、拖放绑定/入库 |
| `StorageBindingService.cs` | 1821 | 绑定表读写、容器发现、标签页展开、聚合索引、搜索/分类/排序/合并、存入/取出 |
| `TerminalUI.cs` | 2665 | 三页签窗口（浏览/入库/绑定），全部 `CustomUIBuilder` 搭建，含 0.4s 活度轮询与取色拾取模式 |
| `SmartSortBridge.cs` | 276 | 可选跨模组反射桥（Smart Inventory Arrange），拿不到就退原版排序 |
| `TerminalRuntime.cs` | 12 | `MonoBehaviour.Update` → `SmartSortBridge.Tick()` + `TerminalUI.HandleUpdate()` |
| 9 个小文件 | ~200 | 纯数据类/枚举：`BoundBayInfo`、`BoundTabInfo`、`StoredItemEntry`、`GroupedItemEntry`、`DepositOutcome`、`WithdrawOutcome`、`StorageRiskSummary`、`TerminalBindingData`、`StoreAreaTarget`、`ItemSortMode`、`Loc` |

**没有 `HotReloadPrepare/Activate`**（报告原文：热重载钩子 否），`OnDisable` 里是靠 `ModHelper.RemoveNetworkUpgrades/RemoveGameOptions/RemoveDebugFunctions` + `Object.Destroy(runtimeHost)` 做对称清理（`ModMain.cs:74-100`）。

---

## 1. 零 Harmony 的钩子面板与自定义 UI 接口

### 1.1 八个钩子各干什么

挂载点全在 `OnEnable`，`hooksRegistered` 布尔防重复（`ModMain.cs:54-72`）：

```csharp
		if (!hooksRegistered)
		{
			ModHook.OnModItemDirectoryInit += OnRegisterItems;
			ModHook.OnModAssetsLoaded += OnAssetsLoaded;
			ModHook.OnModConfigApplied += OnConfigApplied;
			ModHook.OnPlaceInventorInventoryItemLate += OnInventorStock;
			ModHook.OnCreateTooltipLate += TerminalItem.OnCreateTooltip;
			ModHook.OnNewGameStarted += OnNewGameStarted;
			ModHook.OnGameLoadedLate += OnGameLoaded;
			ModHook.OnLeavingStoreEarly += OnLeavingStore;
			hooksRegistered = true;
		}
```

| 钩子 | 源码位置 | 它做了什么 | 原版触发点 |
| --- | --- | --- | --- |
| `OnModItemDirectoryInit(ModItemDirectory)` | `ModMain.cs:124-130` | 把物品工厂塞进目录：`((Directory<GameItem>)directory).Set("storage_control_terminal", () => TerminalItem.Create())`。**物品在这之前不存在**，这是唯一注册入口 | `ModItemDirectory.InitDirectory()`（原版 `ModItemDirectory.cs:5`） |
| `OnModAssetsLoaded(LoadedMod)` | `ModMain.cs:102-113` | 只在**自己**的资产加载完时动作：反编译出来的条件形如 `if (!(mod.Manifest.ID != MOD_ID))`，语义就是 `mod.Manifest.ID == MOD_ID` 才往下走。往下是注册调试刷物品 + 注册网络升级（两者各自有一次性布尔旗） | `ModLoader.cs:1877` |
| `OnModConfigApplied(string)` | `ModMain.cs:115-122` | 配置落地后 `SyncFromModConfig()` + 把价格推到已注册的网络升级上（`definition.cost = cost`，`ModMain.cs:327-345`） | `ModConfigWindow.cs:233` |
| `OnPlaceInventorInventoryItemLate(List<GameItem>)` | `ModMain.cs:154-181` | 发明家货源：列表里已有终端就改价，没有就 `ModHelper.SpawnItem` 造一台塞进去，`unitValue = TerminalPrice` | `StoreClientList.cs:553`（`PlaceInventorSpecialStock()`，参数就是 `EmporiumEntry.Instance.GetAllOwnedItems()`，原版 `StoreClientList.cs:490-553`） |
| `OnCreateTooltipLate(RichTextBuilder, GameItem)` | `TerminalItem.cs:73-79` → `126-167` | 给终端追加：已绑定仓数/条目数/总数量、容量条、违禁与赃物风险行、操作提示 | `ModInternal.cs:15` |
| `OnNewGameStarted(string)` | `ModMain.cs:132-152` | 选项开启时开局白送一台（`SpawnItem` → `SetItemOwnerShip` → `DeliverSpawnedTerminal`） | `NewGameData.cs:401` |
| `OnGameLoadedLate()` | `ModMain.cs:183-195` | 读档后重建 `runtimeHost`、重读 `PlayerPrefs`、同步配置、补注册调试/升级、刷新价格 | `PlayerStore.cs:1097` |
| `OnLeavingStoreEarly()` | `ModMain.cs:197-200` | 离店直接 `TerminalUI.CloseWindow()`，防止窗口跨场景残留 | `EmporiumEntry.cs:727` |

> 观察：**它把"能在别的钩子里做的事"分得很细**——资产类（图标/升级）等 `OnModAssetsLoaded`，存档相关等 `OnGameLoadedLate`，配置相关等 `OnModConfigApplied`，货源等 `OnPlaceInventorInventoryItemLate`。没有任何一个钩子里做跨阶段假设，这是它零 Harmony 还能稳的前提。

### 1.2 自定义 UI 走的是哪套接口

只用 `CustomUIManager` / `CustomUIBuilder` / `CustomUIWindow`（`window` 字段类型就是 `CustomUIWindow`，`TerminalUI.cs:75`），**完全没有手搓 `PixelWindow`**：

- 建窗：`CustomUIManager.Instance.CreateWindow("sct_storage_control_terminal", title, "paper", null, (Style)3)`（`TerminalUI.cs:330`）。原版枚举 `Style{ Auto, Game, Meta, Machinery }`，**`(Style)3` 就是 `Machinery`**（原版 `CustomUIManager.cs:11-17`），所以窗口底子是原版机器那套像素风；`"paper"` 是层级 id（`LAYER_PAPER`）。
- 链式设定：`.SetSize(980f, 650f).SetDraggable(true).SetCloseOnEscape(true).OnClose(...)`（`TerminalUI.cs:331-341`）。
- 布局：`BeginRow/BeginColumn/BeginPanel/BeginScrollFlexible` + `AddButton/AddLabel/AddHeader/AddImage/AddInput/AddDropdown/AddProgressBar/AddSeparator/AddSpace/AddFlexibleSpace`，`val.Last` 拿刚加的元素，`val.CurrentContainer` 拿容器 `RectTransform` 存成字段（`riskBannerHost/workspaceHost/browseListHost/depositListHost/bindingsListHost`，`TerminalUI.cs:392-401`）。
- **子面板必须再 `new CustomUIBuilder(manager, window, host)` + `UseMachineryStyle(true)`**（`TerminalUI.cs:503-504`、`900-901`）：`UseMachineryStyle` 只是把 `window.useMachineryStyle = true`（原版 `CustomUIBuilder.cs:131-135`），但它决定了刚体元素用机器皮肤 prefab。
- **列表刷新用"清空容器 + 重建"+ `RebuildLayout`**，不缓存元素句柄：`CustomUIBuilder.ClearContainer(host)` → 重新 `Begin*`/`Add*` → `CustomUIBuilder.RebuildLayout(host)`（`TerminalUI.cs:1143-1164`、`491`/`517`）。代价是全量重建，好处是永远不会出现"UI 与数据不同步"的僵尸行。
- 样式落到 TMP 原生字段：`elem.text.fontSize / fontStyle / enableWordWrapping / overflowMode`、`elem.image.color`、`elem.input.textComponent / placeholder`、`elem.dropdown.captionText / itemText`、`btn.SetTextColor / SetInteractable / SetTooltip / SetPreferredSize / SetFlexible / SetProgress`（`TerminalUI.cs:2271-2399`）。
- 三个输入隔离手法值得记：
  1. `suppressCallbacks` 布尔：程序化 `SetString/SetOptions/SetIndex` 时置真，挡掉回调里的重建递归（`TerminalUI.cs:550-553`、`995-998`）；
  2. 拾取模式临时关 `window.closeOnEscape = false`，用 `restoreEscapeFrame` 帧号延后一帧恢复，避免同帧 Esc 直接关窗（`TerminalUI.cs:2102-2143`）；
  3. 热键前判 `KeyBindHelper.IsCapturing || KeyBindHelper.SuppressHotkeys || IsTypingInInputField()`（`isTyping` 靠 `EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>()`，`TerminalUI.cs:2615-2622`）。

---

## 2. 自定义物品与机器怎么定义出来

### 2.1 一台"3×3 机器"的全部声明

`TerminalItem.Create()`（`TerminalItem.cs:17-71`）就是完整答案，逐条对上原版：

| 声明 | 源码 | 原版含义 |
| --- | --- | --- |
| `ItemDirectory.CreateEmptyItem("storage_control_terminal")` | `TerminalItem.cs:19` | 空壳物品，工厂自己被 `Directory<T>.Create` 赋值 `identifier/identifierName`（原版 `Directory.cs:24-38`） |
| `SetSpriteAndShapeFromMod(modId, "storage_terminal")` | `:20` | **占格由贴图的像素尺寸决定**：`rect.width/gridSize × rect.height/gridSize` 张网格，逐格用 `texture.GetPixel(格子中心).a > 0` 判是否占格（原版 `GameItem.cs:386-417`）。所以"3×3"不是写死的数字，而是一张 **3×3 网格对齐**（`UISettings.current.gridSize` 的倍数）且 alpha 覆盖满 9 格的雪碧图；切片不对齐原版会刷 `SPRITE NOT ON GRID` 警告（原版 `GameItem.cs:374-384`） |
| `ValidateShapeState()` | `:21` | 立即算 `modifiedShape` 并 `Validate()`，否则第一次进背包前形状是脏的（原版 `GameItem.cs:1370-1391`） |
| `EnableTag("SCT_STORAGE_TERMINAL_TAG", false)` / `EnableTag("IMPORTANT_TAG", false)` | `:25-26` | 自己的识别标签（给 tooltip 判定用，`TerminalItem.cs:75`）+ 原版"重要物品"标签；`skipValidate=false` 会连带跑形状校验 |
| `SetGameItemType("MACHINE")` | `:27` | 只写原版已有品类，不新增类型（原版 `TypeHelper.cs:140`）——与我们的硬规范一致 |
| `ModHelper.SetSource(val, modId, "Storage Control Terminal")` | `:28` | 标记来源模组（卸载/显示归属用） |
| `AudioHelper.InitMetalMachine(val)` | `:29` | 机器金属音效（搬动/交互） |
| `GameItemFunc` 六个委托 | `:30-69` | **它"能用"的全部行为都在这里**，见下 |
| `contentWindow` **不给** | — | 全程没有 `SetContentWindow`，所以它**不是容器**：不开自己的 `PixelWindow`，只开 `CustomUIWindow`；也正因此 `IsBindableContainer` 会把它排除掉（`StorageBindingService.cs:190-193`） |

`GameItemFunc`（原版 `GameItemFunc.cs`）六个委托的语义：

- `mayInventoryAddItemFunc → GeneralHelper.MayOwnershipAcceptItem(self, inv)`：能不能被塞进某个容器（所有权过滤）。
- `mayActivateSlotItemFunc` / `canActivateSlotItemFunc → GeneralHelper.IsItemOwned(self)`：没买下的终端不能点。
- `onActivateSlotItemFunc → TerminalUI.OpenWindow(self)`：**点击＝开窗**，这就是"机器可用"的实现。
- `mayItemTargetThisFunc` / `canItemTargetThisFunc` / `onItemTargetThisFunc`（`TerminalItem.cs:37-69`）：被别的物品"拖到自己身上"时的判定与执行——拖容器＝绑定，拖普通物品＝入库。

### 2.2 进目录、卖给它、发给玩家

- **进目录**：只有 `OnModItemDirectoryInit` 一条路（§1.1）。全模组没有第二处 `Set(...)`。
- **发明家卖**：`OnPlaceInventorInventoryItemLate` 往货源列表里追加（`ModMain.cs:170-175`），价来自配置。
- **狂野网络买**：`ModHelper.AddNetworkUpgrade(modId, "sct_buy_storage_terminal", price, "storage_automation", title, desc, onUnlock, "Store", "storage_control_terminal:storage_terminal", new string[0], 1, ...)`（`ModMain.cs:355-363`，签名见原版 `ModHelper.cs:274`）——tab 归到 `Store`，图标键与物品雪碧同键。
- **送达**：`DeliverSpawnedTerminal`（`ModMain.cs:448-473`）先试 `EmporiumEntry` 的 5 个盘子（`invElement/backInvinvElement/frontInvinvElement/backInvinvElementCounter/showcaseElement`），逐个 `!IsInsertLocked() && GraphUtils.CanAccept(...) && GraphUtils.TryAcceptAll(..., unitCount) == 0 && item.parentInventory != null`，全失败才 `GraphUtils.EmporiumTryAdd(item)` 兜底。**这是"精确投放 + 校验落点"的标准写法**。

---

## 3. 聚合索引模型

### 3.1 怎么发现"店里所有容器"

起点是**一个原版公开方法**：`PlayerStore.Instance.FindAllItem(true)`（`StorageBindingService.cs:143`、`:214`、`:285`、`:1806`）。
原版实现是 11 个 `EmporiumEntry` 窗口的 `GraphUtils.FindAllChildrenType<GameItem>` 并集，再按 `IS_OWNED_TAG` 过滤并 `Distinct()`（原版 `PlayerStore.cs:1783-1802`）。**没有反射、没有私有字段**。

然后过两道筛：

1. `IsBindableContainer(terminal, candidate)`（`:180-204`）：非自己、非终端、`GeneralHelper.IsItemOwned`、`contentWindow != null && !contentWindow.isLocked`、且 `CollectContainerInventories(candidate).Count > 0`。
2. 分级 `IsPrimaryStorageBay(item)`（`:248-276`）——三级判定，从硬到软：
   - `IsGameItemType("STORAGE")` → 是；
   - `IsTag("STORAGE_EXPANSION_TABS_INT")` → 是；
   - identifier 关键字命中（`storage / bay / vault / cabinet / locker / smuggler / xuxu / med_box / seed_bag / card_folder / keycard_case / dna_storage / crate / shelf / rack`）**且**有 `contentWindow` → 是；
   - `CONTAINER_TAG && !MACHINE && contentWindow != null` → 是。
   "主要储藏区"排在"其他容器"前面（`DiscoverUnboundContainers`，`:243-244`），保证自动扫描优先绑定真正的仓储。

### 3.2 绑定关系存哪：**不是 ModData，是物品 tag 里的一段 JSON**

这是本档案最该纠正的常见误解。全模组**零** `ModHelper.SetModData` / ES3 / 文件 IO 调用，绑定表写在终端物品自己的 tag 里：

- 键名：`SCT_BOUND_STORAGES_JSON`（`StorageBindingService.cs:9`，`const string BINDINGS_TAG`）；
- 结构：`[Serializable] class TerminalBindingData { public List<int> boundItemIds = new List<int>(); }`（`TerminalBindingData.cs:6-10`），元素是容器的 **`uniqueId`**；
- 写：`JsonUtility.ToJson(...)` → `terminal.ModifyTag(BINDINGS_TAG, tag => tag.SetString(json), false)`（`:65-69`）；
- 读：`terminal.GetTagReadonly(BINDINGS_TAG).GetString()` → `JsonUtility.FromJson<TerminalBindingData>`，去重、只留 `> 0`（`:20-45`）；
- 语义：`List` 顺序 = 界面里 `↑/↓` 调整的优先顺序（`MoveBindingOrder` 直接换位，`:112-134`）；
- 解析失败只 `LogWarning` 并返回空表，不抛（`:41-44`）。

原版依据：`TagSystem` 就是 `Dictionary<string, TagState>`，`TagState` 标了 `[Serializable]` 并带 `valueString/valueInt/...` 槽（原版 `TagSystem.cs:6-16`、`TagState.cs:4-42`），`ModifyTag` 写 `state`、`GetTagReadonly` 读 `modifiedState` 的克隆（原版 `GameItem.cs:690-706`）。所以这段 JSON 跟着物品本身走：**终端被搬柜子、被丢地上、被卖掉，绑定关系都不丢**（比存 ModData 一个"终端 id → 容器 id"表更省事，也不会出现"物品没了但表还在"）。具体序列化路径（ES3 反射整个物品）读不到，标 （推测）。

副作用要记：原版 `ModifyTag(tag, action, skipValidate = false)` 默认会在写完后跑一遍 `ValidateShapeState()`（原版 `GameItem.cs:695-706`），而这里第三个参数恰恰传了 `false`（`:66-69`，即**不跳过**）。所以每改一次绑定就会触发一次形状重算 + `parentInventory.Validate()` + `OverlayHandler.current.UpdateInventoryStateUI()`；绑定操作不频繁时无感，仓库极大/连点排序时是个隐性开销点（推测，未实测）。要省就把这里传 `true`，自己在批量操作结束后统一 `ValidateShapeState()`。

### 3.3 容器 → 标签页 → `GameInventory` 的三跳

`CollectContainerInventories(GameItem)`（`:420-505`）是全部索引的地基，四路兜底：

1. `FindAllTabs(contentWindow)`（`:311-349`）：先 `FindTabsElement(childElement)` 递归（穿 `MachineryPanel.content`、`MachineryPanelRow.GetPanel("storage")`），再扫 `window.handler.GetComponentsInChildren<TreeNodeRender>(true)` 里 `element is MachineryTabs` 的节点；
2. 每个 `MachineryTabs` 循环 `j = 0..63`：`GetPage(j)` 为 null 就 break；优先 `GetTabInventory(j)`，否则 `ExtractPageInventory(page)`（`:351-386`：`page as GameInventory` → `MachineryIntake.content` → `MachineryPanel.content` → `MachineryContainerUtils.Collect(page)` 里第一个 `GameInventory`）；
3. `MachineryContainerUtils.Collect(contentWindow.childElement)` 再捞一遍 `GameInventory`；
4. 全空时最后一招 `ExtractPageInventory(childElement)`。

`HashSet/List.Contains` 去重，绝不让同一个 inventory 进两次（`:443-446`、`:562-565`）。
标签页展示名优先级：`MachineryTabs.GetCaption(j)` → `"#n"`（`:565-569`）。

### 3.4 条目字段从哪来（`BuildEntry`，`:748-804`）

| 字段 | 来源 |
| --- | --- |
| `DisplayName` | `item.GetDisplayName(true)`（带真伪修饰） |
| `CustomName` | `GeneralHelper.HasCustomName/GetCustomName` |
| `Identifier` | `item.identifier` |
| `UnitCount` | `Math.Max(1, item.unitCount)` |
| `UnitValue` / `TotalValue` | `item.GetCurrentValue(false, true, false, false, 0L)`，异常退 `item.unitValue`；`TotalValue = UnitValue * UnitCount` |
| `BoughtPrice` | tag `BOUGHT_PRICE_TAG`（`IsEnabled()` 才读 `GetInt()`，否则 -1） |
| `GradeKey` / `GradeLabel` | `ExtractGradeInfo`（`:1064-1147`）：化学品 `ChemicalProductHelper.GetChemicalFeature` + `GetCurrentChemicalConditionId`；液体 `LIQUID_CONTAINER_TAG` + `WaterFeatureHelper.GetCurrentWaterDisplay`；锭 `INGOT_PURITY_TAG` + `IngotPurityHelper.GetPurity`；全部经 `StripRichText` 去富文本标签。`GradeKey` 参与合并键，`GradeLabel` 只显示 |
| `Types` | `item.GetGameItemType()` |
| `IsContraband` / `IsStolen` / `IsPinned` | `ContrabandHelper.IsContraband` / `StolenHelper.IsStolenItem` / `ItemPinHelper.IsPinned` |
| `IsContainer` | `item.contentWindow != null` |
| `Icon` | `ResolveItemSprite`（`:1578-1620`）：`GameItemElement.ResolveSpriteByName` → `CustomUIManager.Instance.GetModSprite(atlas, path, true)` → 去掉 `Items/` 前缀再试 → `RenderHandler.TryLoadFromAtlas` |

### 3.5 搜索 / 分类 / 排序 / 合并

- **搜索**（`MatchesTokens`，`:894-930`）：`searchQuery` 按空格/制表符切词，**全部命中**才算匹配（AND）；语料是 `DisplayName + CustomName + Identifier + GradeLabel + Bay.DisplayName + Tab.TabName + Types + 各 type 的本地化名`，违禁/赃物还会追加 `"contraband 违禁"` / `"stolen 赃物"` 关键词，方便中英双搜。
- **本地化品类名**（`GetLocalizedItemType`，`:1622-1687`）：`TypeHelper.GetTypeDisplayName` → 自己的 `type_fallback_*` 词条 → `LocHelper.HasLocalizedUI("type_*")`，并用 `IsValidLocalizedLabel` 挡掉 `"No translation"`、原样返回 key、`type_` 前缀这三种脏值。**这套"三层回退 + 脏值过滤"值得直接抄。**
- **分类**（`MatchesCategory`，`:864-892`）：伪键 `__ALL__ / __CONTRABAND__ / __STOLEN__ / __CONTAINER__`，其余按 `Types` 精确比较（忽略大小写）；下拉选项是**扫描当前结果动态生成**的（`TerminalUI.cs:1001-1071`），只显示确实存在的分类。
- **排序**（`FilterAndSort`，`:806-862`）：`ItemSortMode{ NameAsc, ValueDesc, ValueAsc, CountDesc, BayOrder }`，稳定收尾：同名按名字、再按 `GradeLabel`、再按 `IsStolen`。
- **合并**（`GroupEntries` + `BuildGroupKey`，`:932-1062`）：键 = `identifier | customName | gradeKey | S0/S1 | C0/C1 | P0/P1`（堆叠真假、违禁、赃物、置顶都区分）。特殊规则：**非空容器永不合并**——`BuildGroupKey` 发现 `IsContainer` 且其任一 inventory 有子物品，就直接返回 `"CONTAINER_NONEMPTY|" + GetHashCode()`（`:1044-1054`），避免把两个装着不同东西的箱子显示成一个"x2"。合并体还维护 `HasUniformUnitPrice/HasUniformBoughtPrice`（价格不齐时界面显示"混合价"，`TerminalUI.cs:2563-2567`）与 `DistinctBayCount`（跨仓提示）。
- **容量统计**（`BuildTabInfo`，`:610-659`）：总格数 = `GameGridInventory.widthPixels/heightPixels ÷ UISettings.current.gridSize`；已用格数 = 每个子物品 `widthPixels/heightPixels ÷ gridSize` 累加（非网格物品记 1 格），`Math.Min` 夹住不超过总格。
- **活度指纹**（`ComputeLiveFingerprint`，`:1744-1798`）：`17 * 31 + Σ` 的滚动哈希，把"绑定 id 列表 + 每容器 inventory 数 + 每 inventory 子物品数 + 每个物品 `GetHashCode()` + `unitCount` + 存货区物品"全揉进一个 int。`TerminalUI.HandleUpdate` 每 **0.4s**（`LIVE_POLL_INTERVAL`，`TerminalUI.cs:33`、`2162-2171`）算一次，变了才 `RefreshAll()`。**这就是"外部搬运了东西，终端界面会自己更新"的实现，成本是一个 int 的遍历。**

---

## 4. 存取落点安全

### 4.1 取出（容器 → 柜台/背包）

`WithdrawItem(item, requestedUnits, preferredArea, allowFallback)`（`:1201-1257`）：

1. 硬门禁：`item.parentInventory != null`、`!ItemPinHelper.IsPinned`、`IsTransferableOwnedItem(item)`（`:700-727`：拥有、`!IsDisplayOnly`、`!IsItemLocked`、`!parentInventory.IsRemoveLocked()`、非 `currentNegociatedItem`）；
2. 目标区枚举 `GetStoreAreaInventory`（`:1177-1189`）：`MainInventory→invElement`、`BackCounter→backInvinvElement`、`FrontCounter→frontInvinvElement`；
3. 每个候选都过三条件：非空、**`storeAreaInventory != item.parentInventory`**（不许原地搬）、`!IsInsertLocked()`；
4. 真正搬运：`TransferUnits` → `SafeCanAccept` 再 `GraphUtils.TryAcceptAll(dest, item, amount)`，**把"未放入量"换成"已搬量"**：
   `moved = amount - TryAcceptAll(...)`（`:1475-1492`）——原版 `TryAcceptAll` 返回的是**剩余没放进的数量**（原版 `GraphUtils.cs:166-204`），这一点搞反就会把"一件都没搬走"报成成功；
5. 首选区失败才按 `Main → Back → Front` 顺序回退（受 `ModMain.FallbackWithdrawArea` 控制）。

### 4.2 存入（存货区 → 绑定容器）

`DepositItem(terminal, item, specificBayUniqueId, prioritizeStacking, maxUnits)`（`:1319-1436`）：

1. 同样的 pinned / `IsTransferableOwnedItem` 门禁；
2. 排除终端自身（`IsSameItem(item, terminal) || item.identifier == 终端id`）；
3. 候选仓 = `ResolveBoundBays` 里 `IsAvailable && BayItem != null`、且 `specificBayUniqueId<=0 || UniqueId==它`、且 `CanDepositIntoBay(item, BayItem)`（`:1458-1473`：不同一物、且 **`!GraphUtils.FindParentEquals(bayItem, item)`**——防止把某个箱子塞进"它自己或它内部"的那个容器，形成环）；
4. 逐 tab：跳过 null、跳过 `inventory == item.parentInventory`（源容器）、跳过 `IsInsertLocked()`；
5. `SafeCanAccept(inventory, item)`（`:1438-1456`）= `inv.MayHaveValidInventorySlot(item) && GraphUtils.CanAccept(inv, item)`，然后 `TryAcceptAll(inv, item, 剩余)` 取差得已搬量，累加 `MovedUnits` 与 `TargetBayName`；
6. 搬够（`num3 <= 0`）立刻 break，绝不横扫全场。

**过滤规则为什么自动被尊重**：模组侧没有任何白名单/黑名单，落点判定 100% 委托原版 `TryFindOneValidInventorySlot`（`CanAccept` 就是它非 null，原版 `GraphUtils.cs:111-118`）。原版那个函数依次做：`item.MaxNumRemove() < 1` 直接 null → 递归问子物品的 `slotSearchPassthroughForItemFunc` / `inventory.slotSearchPassthroughForItemFunc` → `MayHaveValidInventorySlot` → 找 `item.MayStack(item3)` 的既存堆 → 最后才按 4 向旋转 × 2 翻转扫空位（原版 `GameGridInventory.cs:824-905`）。所以"尊重各容器自身过滤"不是它写得好，而是它**没有僭越**。

### 4.3 一处可证的冗余（别照抄）

`prioritizeStacking` 分支用 `inventory.GetIncompleteItemInventorySlot(item) != null` 当"有同类堆可合"的前置（`:1378-1379`）。核对原版三个实现：`GameGridInventory.cs:695-704`、`GameGridScrollableInventory.cs:578-587`、`GameSlotInventory.cs:482-491`——**它们都只是把传入的 item 包成一个 `SlotMarker` 返回，永不为 null**。所以这段前置恒真，第一遍循环与第二遍（`prioritizeStacking=false` 的路径）在行为上等价；真正的"优先合堆"是原版 `TryFindOneValidInventorySlot` 自己先找 `MayStack` 目标做的（原版 `GameGridInventory.cs:868-880`）。要真做"先合堆再占新格"，得自己遍历 `childItems` 找同 identifier 且 `MaxNumAdd()>0` 的目标。

### 4.4 拾取绑定（拖/点容器）的安全细节

`HandlePickFrame`（`TerminalUI.cs:2178-2214`）：`KeyBindHelper.GetBackDown()` 或右键退出；`ItemTooltipHandler.current.Disable()` 屏蔽原版 tooltip；`HoveredStoreItem()` 先判 `StoreUIManager.Instance.hoveringStoreUI` 再 `RenderHandler.RaycastElement<GameItem>`；命中后 `ItemMouseDragHandler.current.Cancel()` 再 `AddBinding`；`EvaluatePickTarget`（`:2227-2253`）四道拒绝理由都给玩家文字提示。

---

## 5. `ModReflect` 清单：只反射了"别人的模组"

全模组 `ModReflect` 只有 3 类调用，集中在 `SmartSortBridge.cs`：

| 调用 | 位置 | 目标成员 | 为什么要反射 |
| --- | --- | --- | --- |
| `ModReflect.FindType("SmartInventoryArrange.SmartArrangeMod")` / `"…ArrangeUndo"` / `"…ArrangeUndo+Group"`（嵌套类型） | `SmartSortBridge.cs:76-78` | 另一个模组的类型 | 原版只有 `InventorySortHelper.Sort`（单次排序），**没有任何"外部排序器"扩展点**；想要 Smart Inventory Arrange 那套动画排序只能自己找类型 |
| `ModReflect.GetStatic<bool>(siaType, "Busy")` / `GetStatic<bool>(undoType, "BlocksNewSort")` / `GetStatic<bool>(undoType, "Running")` | `:123`、`:129`、`:133` | 对方的静态忙标志 | 对方排序是跨帧异步的，不读它的忙标志就会两个排序互相踩 |
| `ModReflect.CallStatic(siaType, "Run", runSignature, new object[6]{ grid, false, Func<bool>, progressIndex, progressTotal, null })` | `:247` | 对方 `Run` 的 6 参重载 | 想复用它的动画/撤销栈；`runSignature` 显式给了 6 个 `Type`（`GameGridInventory / bool / Func<bool> / int / int / ArrangeUndo+Group`），**重载必须显式签名**这一点在这里被遵守了 |

原版门槛（这决定"这条路到底通不通"）：`ModReflect.FindType` 只在 **游戏程序集 / 调用者自己 / 调用者声明的前置模组 / 允许的外部 UI 命名空间** 里解析（原版 `ModReflect.cs:19-61`、`553-606`）；`GetStatic/CallStatic` 每次还会走 `Require` 复核。所以本模组若没在 manifest 里把 `smart_inventory_arrange` 写进 `<Prerequisites>`，`FindType` 会返回 null，`CallStatic` 会抛 `ModReflectAccessException`。**manifest 不在反编译目录里，无法确认它是否声明了前置（推测：它是靠 `ModHelper.IsModRunning("smart_inventory_arrange")` 做软存在性判断、并整套 try/catch 降级，所以有没有前置都不会崩——只是有没有动画的区别）**。降级路径写得完整：`IsSmartArrangeAvailable()` 为假就 `InventorySortHelper.Sort` 逐格原版排序（`:165-196`），`Run` 抛异常就"这一格退原版排序"（`:252-261`）。

一句话总结它为什么能"零 Harmony"：**要改原版行为 → 不改成"读原版公开状态 + 调原版公开方法 + 不可用就降级"；要跟别的模组联动 → 反射 + 存在性判断 + 整体降级。** 代价是它做不了"拦截/替换"，收益是零 `Harmony` 零权限风险、随原版升级基本不炸。

---

## 6. 可复用范式（4 个）

### 范式 A：中央聚合 UI 的"清空—重建"骨架

`TerminalUI.cs:503-518`（`RebuildWorkspace` 核心 16 行，原样）：

```csharp
			CustomUIBuilder val = new CustomUIBuilder(CustomUIManager.Instance, window, workspaceHost);
			val.UseMachineryStyle(true);
			if (activeTab == ModeTab.Browse)
			{
				BuildBrowseWorkspace(val);
			}
			else if (activeTab == ModeTab.Deposit)
			{
				BuildDepositWorkspace(val);
			}
			else
			{
				BuildBindingsWorkspace(val);
			}
			CustomUIBuilder.RebuildLayout(workspaceHost);
			UpdateTabVisuals();
```

配套（`TerminalUI.cs:489-491`）：进函数前 `CustomUIBuilder.ClearContainer(workspaceHost)`，并把所有元素句柄字段（`searchInput/bayFilterDropdown/...`）置 null 再重建；顶部一次 `CreateWindow(..., (Style)3)` 固定窗口，三页签只换 `workspaceHost` 里的内容。**列表区同理**（`RebuildBrowseList`，`:1143-1164`）。适用面：任何"数据量大、随时会变、不值得做虚拟滚动"的面板。

### 范式 B：跨容器安全投放（剩余量语义）

`StorageBindingService.cs:1415-1431`（原样 17 行，注意 `catch{}` 是我们明令禁止的，抄时改 `LogError`）：

```csharp
				try
				{
					if (SafeCanAccept(inventory2, item))
					{
						int num6 = GraphUtils.TryAcceptAll((GraphNodeStorage)(object)inventory2, item, num3);
						int num7 = num3 - num6;
						if (num7 > 0)
						{
							depositOutcome.MovedUnits += num7;
							num3 -= num7;
							targetBayName = boundBayInfo3.DisplayName;
						}
					}
				}
				catch
				{
				}
```

前置那个 `SafeCanAccept`（`:1438-1456`）是这套写法的灵魂：`inv.MayHaveValidInventorySlot(item) && GraphUtils.CanAccept(inv, item)`，外面再包 try/catch（同样建议改成 `LogError` 而非空吞）。三件套记住：**先 `CanAccept` 再 `TryAcceptAll`，成功量 = 请求量 − 返回值，落点用 `item.parentInventory == 目标` 复核**。

### 范式 C：一台机器的最小物品声明

`TerminalItem.cs:19-36`（原样 18 行）：

```csharp
		GameItem val = ItemDirectory.CreateEmptyItem("storage_control_terminal");
		val.SetSpriteAndShapeFromMod("storage_control_terminal", "storage_terminal");
		val.ValidateShapeState();
		val.name = Loc.Get("item_name");
		val.shortDescription = Loc.Get("item_desc");
		val.unitValue = Math.Max(0, ModMain.TerminalPrice);
		val.EnableTag("SCT_STORAGE_TERMINAL_TAG", false);
		val.EnableTag("IMPORTANT_TAG", false);
		val.SetGameItemType("MACHINE");
		ModHelper.SetSource(val, "storage_control_terminal", "Storage Control Terminal");
		AudioHelper.InitMetalMachine(val);
		((GameItemFunc)val).mayInventoryAddItemFunc = (GameItem self, GameInventory inv) => GeneralHelper.MayOwnershipAcceptItem(self, inv);
		((GameItemFunc)val).mayActivateSlotItemFunc = (GameItem self, GameInventory inv, SlotMarker slot) => GeneralHelper.IsItemOwned(self);
		((GameItemFunc)val).canActivateSlotItemFunc = (GameItem self, GameInventory inv, SlotMarker slot) => GeneralHelper.IsItemOwned(self);
		((GameItemFunc)val).onActivateSlotItemFunc = (GameItem self, GameInventory inv, SlotMarker slot) =>
		{
			TerminalUI.OpenWindow(self);
		};
```

要点：**占格＝贴图**（3×3 就是 3×3 格对齐的切片），"机器"＝`SetGameItemType("MACHINE")`，**"能用"＝`onActivateSlotItemFunc` 开自己的窗口**，而不是必须有 `contentWindow`。

### 范式 D：把关系挂在物品 tag 上（随物品走的持久化）

`StorageBindingService.cs:48-70`（原样 23 行）：

```csharp
	public static void SaveBoundIds(GameItem terminal, List<int> ids)
	{
		if (terminal == null)
		{
			return;
		}
		TerminalBindingData terminalBindingData = new TerminalBindingData();
		if (ids != null)
		{
			for (int i = 0; i < ids.Count; i++)
			{
				if (ids[i] > 0 && !terminalBindingData.boundItemIds.Contains(ids[i]))
				{
					terminalBindingData.boundItemIds.Add(ids[i]);
				}
			}
		}
		string json = JsonUtility.ToJson((object)terminalBindingData);
		terminal.ModifyTag("SCT_BOUND_STORAGES_JSON", (Action<TagState>)((TagState tag) =>
		{
			tag.SetString(json);
		}), false);
	}
```

配套读侧（`:20-45`）是 `GetTagReadonly(...).GetString()` + `FromJson` + 去重 + `>0` 过滤，异常只 `LogWarning` 返空表。**优点**：不写存档结构、不动 ModData、物品被搬/被卖关系不丢；**代价**：每次改都跑形状校验，且别人（其它模组/原版）看到的就是一个 `SCT_BOUND_STORAGES_JSON` 标签（不启用 `IsTag`，靠 `GetString` 取值，所以不会误触 `IsTag` 判定）。

---

## 7. 对我们的可用点

**对《深空电玩》（`deepspace_arcade`）**

1. **跨容器安全投放的三件套已经有人替我们踩过**：我们 `ArcDiscOrientation.cs:50-92` 已经抓到"`GameInventory.TryAcceptAll` 把剩余量丢掉、必须直调 `GraphUtils.TryAcceptAll`"这个坑，本模组是同一结论的另一种写法（`moved = amount - TryAcceptAll(...)`，§4.1）。可以把 `SafeCanAccept = MayHaveValidInventorySlot && CanAccept` 补进我们的搬运工具，省掉一批"以为放进去其实没放"的假成功。
2. **多机台统一面板**：我们有刻录机/街机等多台带 `contentWindow` 的机器。范式 A 的"固定窗口 + 三页签 + 清空重建 + 0.4s 指纹轮询"可以直接拿来做一个"全场机台状态板"（哪台在跑、哪台缺电池、盘满没满），不必每台点进去看。
3. **机器占格别手写**：我们已有 `ArcSpriteFix` 在跟 `SetSpriteAndShapeFromMod` 的 alpha 判格打交道；这里的做法是**先保证切片是 gridSize 整数倍**再调它，然后立刻 `ValidateShapeState()`，省掉自算 `GridShape`。

**对《深空当铺：可能是群友》（`freya_cornell`）**

1. **它目前没有任何 `CustomUIManager` 窗口**（全项目只有 `EnableTag`）。要加"角色/关系/物品总览"面板，§1.2 那套 `CreateWindow(..., Style.Machinery)` + `new CustomUIBuilder(manager, window, host).UseMachineryStyle(true)` + `ClearContainer/RebuildLayout` 是最短路径，且天然与原版机器同画风。
2. **把"关系"挂在物品 tag 上**：范式 D 的 JSON-in-tag 模式，适合做"某件遗物绑定了哪个角色/哪段剧情"这类**跟随物品**的状态，比 ModData 表更抗搬迁；注意别把大对象塞 tag（每次 `ModifyTag` 都会 `ValidateShapeState`），只放 id 列表。
3. **软依赖跨模组联动**：§5 那套"`ModHelper.IsModRunning` + `ModReflect.FindType` + 全链路降级"是零强依赖兼容的标准模板；我们若要跟别的模组（含电玩）联动，照这个模板做，**并且记得把对方写进 `<Prerequisites>`，否则 `FindType` 直接 null**。
4. **可抄的判定/文案小件**：`IsValidLocalizedLabel`（挡 `"No translation"`/回退 key/`type_` 前缀，§3.5）、`MatchesTokens` 的中英双关键词搜索、`StorageRiskSummary` 的"违禁/赃物 + 暴露于非走私仓"统计（`AnalyzeStorageRisks`，`:1689-1742`）——后者很适合做我们自己的"风险库存"提示。

**不建议抄的三处**（诚实记录）

- `DepositItem` 内部两处 `try { ... } catch { }` 空吞（`:1376-1393`、`:1415-1431`）：正是我们规范明令禁止的；改成边界处 `LogError` 带堆栈。
- `prioritizeStacking` 的前置恒真（§4.3），属于无效复杂度。
- `OnAssetsLoaded/OnConfigApplied` 里用 `!(mod.Manifest.ID != MOD_ID)` 这种反向条件（反编译产物形态）可读性极差，自己写请正着写 `if (mod.Manifest.ID != MOD_ID) return;`。

---

## 附：本档案的取证边界

- 已读全 18 个 `.cs`（5731 行全文），行号引用均来自本地反编译副本；原版 API 语义均在 `共享开发\reference\Assembly-CSharp\` 对应文件核对过（见各处 `原版:` 标注）。
- **读不到**：`manifest.xml`、`lang/*.json`（词条键名只能从调用处反推，如 `status_bound_ok/status_deposit_ok/...`）、贴图切片（3×3 只能说"必须 3×3 格对齐"，实际像素未验）、`csproj` 引用的宿主版本。
- **未真机验证**：0.4s 轮询在超大仓库下的开销、绑定 tag 反复写导致的形状校验开销、Smart Inventory Arrange 联动在无 `<Prerequisites>` 时的实际日志——这三项是读码推断，标 （推测）。
