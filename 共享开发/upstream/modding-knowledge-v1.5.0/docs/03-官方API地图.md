# 03 · 官方 Mod API 地图

> 目的：**改代码前先回答「官方有没有现成入口」**。
> 证据等级：`ModHook` 事件表为 `[静态]`（直接读 `ModHook.cs`）；其余条目多数在真实模组里用过，个别标注 `[待验]`。方法签名以你本机的 `Assembly-CSharp.dll` 反编译为准。

---

## 1. 决策顺序

```
要做的功能
  ├─ 官方 ModHook 能接？ ────────────→ 订阅钩子（最稳，升级不炸）
  ├─ 官方 Helper 类能调？ ───────────→ 调 API（次稳）
  ├─ 官方有同类实现可照抄？ ─────────→ 抄它的流程，不碰它的数据
  └─ 都没有 ────────────────────────→ Harmony 补丁（最后手段，必须能说清「官方缺哪项语义」）
```

打补丁前问自己三个问题：

1. 补丁的目标方法**是不是每帧/每次点击都在跑**？是 → 入口必须 try/catch，绝不能让异常冒回去。
2. 补丁会不会改变**存档里物品的父子关系/顺序**？会 → 先读坑 #23、#24。
3. 官方未来可能加这个入口吗？可能 → 把补丁写窄（只认自己的 tag/ID），方便以后删除。

---

## 2. `ModHook` 全表（40+ 个事件）

全部是 `public static event`，订阅/退订用 `+=` / `-=`。**每个处理器抛异常会被框架捕获并记录**（`[ModHook] '...' handler ... threw`），不会拖垮别的模组——但也不要依赖这个保护。

### 2.1 物品与资产

| 事件 | 参数 | 典型用途 |
|---|---|---|
| `OnModItemDirectoryInit` | `ModItemDirectory` | **注册物品**（`Set(identifier, factory)`） |
| `OnModAssetsLoaded` | `LoadedMod` | 模组贴图/音效加载完成 |
| `OnModConfigApplied` | `string modId` | 配置应用后 |
| `OnLootTablesLoaded` | — | 战利品表加载完 |
| `OnLootRolled` | `LootRollContext` | 改掉落结果 |

### 2.2 游戏/存档生命周期

| 事件 | 说明 |
|---|---|
| `OnGameLoadedInit` / `Early` / `Normal` / `Late` | 读档流程的四个阶段；**Late 是「读档彻底完成」的可靠点** |
| `OnNewGameStarted(string scenarioId)` | 新开档；注册网络升级、发初始物品的安全时机 |
| `OnDifficultyApplied(string difficultyId)` | 难度应用后 |

### 2.3 每日循环

| 事件 | 说明 |
|---|---|
| `OnShutterOpenedEarly/Late` | 卷帘门开（开店） |
| `OnShutterClosedEarly/Late` | 关店 |
| `OnGoingSleepEarly/Late` | **过夜结算前/后**；`Early` 时 `dayCounter` 还是当天，`Late` 时 `EndNight()` 已执行 |
| `OnWakingUpEarly/Late` | 起床 |
| `OnHandlingNightlyServicesEarly/Late` | 夜间服务（电、保险、清洁……）结算 |
| `OnLeavingStoreEarly/Late`、`OnReturningStoreEarly/Late` | 出门 / 回店 |

### 2.4 客户与商店

| 事件 | 说明 |
|---|---|
| `OnGenerateCustomerVeryEarly/Early/Normal/Late/VeryLate` | 当天客户生成；`Normal` 里 `AddClient` 最常用 |
| `OnPlaceInventorInventoryItemEarly/Late` | 博士（发明家）补货上架前后；**想保证自己的货不被柜台挤掉，用 Early** |
| `OnPaperOpened(string bookId, PaperUI ui)` | 翻开纸张/手册 |
| `OnAfterhourArriveEarly/Late`、`OnAfterhourLeaveEarly/Late` | 夜间地点到达/离开 |
| `OnAfterhourEventStarted/Ended(string locationId, string eventId)` | 夜间事件 |

### 2.5 UI 与文本

| 事件 | 说明 |
|---|---|
| `OnCreateTooltipEarly/Late(RichTextBuilder, GameItem)` | 给物品 tooltip 追加行；**Late 能改原版已经写入的内容** |
| `OnCreateDevTooltipEarly/Late` | 调试 tooltip |

### 2.6 进度

| 事件 | 说明 |
|---|---|
| `OnNetworkUpgradeUnlocked(string upgradeId)` | 网络升级（职业树）解锁 |

> 不要在一次会话里重复订阅同一个处理器；用静态 bool 防重，`OnDisable` 里退订。

---

## 3. 物品

| API | 说明 |
|---|---|
| `directory.Set(identifier, factory)` | 在 `OnModItemDirectoryInit` 里注册物品。`factory` 返回一个配好的 `GameItem` |
| `DirectoryMaster.Item(string id)` | 按 ID 创建物品实例（**不要打补丁**：有泛型/非泛型两个重载，Harmony 会歧义） |
| `item.SetSpriteAndShapeFromMod("<modId>", "<贴图名>")` | 从模组 `Textures/Items/` 取图 |
| `item.SetShape(new GridShapeBuilder(w, h).SetDataFill(w, h, 1))` | 显式设置占地（**别依赖贴图 alpha**）。社区常见做法是包一层 `ForceShape(it, w, h)`，那不是官方方法 |
| `item.SetGameItemType("FOOD")` / `RemoveGameItemType("FOOD")` | 加/去类型标签 |
| `ChemicalHelper.SetChemContentML(item, "zyanide", 3.0)` | 写入药剂含量（单位 ml） |
| `item.SetExamineWindow(win)` | 双击打开的自定义窗口 |
| `item.flavorText` | 悬浮说明；支持 TMP 富文本（`<b>`、`<color>`、换行） |

### 常用 tag（字符串常量）

`LIQUID_CONTAINER_TAG`、`LIQUID_CONTAINER_CAPACITY`、`CURRENT_PART_<液体ID大写>`、`CALORIE_VALUE_TAG`、`FISH_ITEM_TAG`、`FISH_IS_DEAD_TAG`、`FISH_ROE_TAG`、`MEAT_FOOD_TAG`、`MODULE_TAG`、`MODULE_TYPE_<...>`、`IS_OWNED_TAG`、`GROWING_TAG`、`STANDARD_MACHINE_TAG`、`NO_MODULE_INTEGRATION_TAG`、`BONUS_PERCENTAGE_{PERFORMANCE,EFFICIENCY,QUALITY}_INT`、`TEMP_PERCENTAGE_*`、`MODULE_UPGRADE_STAGE_INT`、`LOCKED_WORKING_TAG`、`STATE_READY` / `STATE_WORKING`、`MACHINE_PROGRESS_TARGET_TAG`、`CURRENT_PROCESSING_SPEED_TAG`。

> tag 的读写：`item.IsTag(tag)`、`item.GetTagReadonly(tag)`、`item.ModifyTag(tag, action, skipValidate)`。**注意 `skipValidate:true` 只写 `state`，读的是 `modifiedState`**——写完必须 `ValidateShapeState()` 或 `SyncModifiedState()`（坑 #19）。

---

## 4. 机器

| API | 说明 |
|---|---|
| `MachineHelper.CreateMachineryWindow(isDraggable, panels...)` | 创建机器窗口 |
| `MachineHelper.Panel(label, element, role)` | 一个面板（`GameInventory` / 控件） |
| `MachineHelper.PowerPanel(batterySlot)` | 标准电源面板 |
| `MachineHelper.SetupBatterySlot(slot, machine)` | 绑定电池槽 |
| `MachineHelper.SetupModuleBay(inv, machine, ..., moduleTypes)` | **标准异形模组区**；内部会算模组效果、刷新功耗，**不要再自己叠一遍** |
| `MachineHelper.DEFAULT_MODULE_BAY_CONFIG` / `DEFAULT_MODULE_BAY_WIDTH` | 模组区默认形状 |
| `MachineHelper.GetBatterySlot(machine)` / `GetModuleInv(machine)` / `GetModules(machine)` | ⚠️ 后两个**硬编码取 `GetBodyStorage(machine, 1)`**，即「面板第 2 列」，不是「按 role 找」 |
| `MachineHelper.GetBodyStorage(item, index)` | 取机器第 index 个面板的库存 |
| `MachineHelper.TryDrawCyclePower(machine, batterySlot)` | 正常扣电；返回是否成功 |
| `MachineHelper.OnMachineActioned(machine, moduleGrid)` | 触发模组的 onUse 效果 |
| `MachineryHelper.InitMachinery(machine, powerUsage)` | 机器初始化 |
| `MachineryHelper.CreateMachineryTooltip(...)` | 原版 tooltip（自动输出所需能量/作业速度/模组效果） |
| `MachineryButton(label)` + `.SetCallback(Action)` | 面板按钮 |
| `MachineProgressHelper.StartProgressTypeMachine(machine, target)` | 进度型机器：设置目标 |
| `MachineProgressHelper.InitProgressTypeMachine(machine, perDay)` | 进度型机器：设置每日推进 |
| `MachineProgressHelper.FinishProgressTypeMachine()` | 完成 |
| `MachineHelper.LoadUpgrade(item)` | 读档时应用「模组区扩容」；**对没有 `MODULE_UPGRADE_STAGE_INT` 的机器会空引用**（坑 #3） |

### 面板顺序是「存档格式」

读档回填子物品靠「面板里 inventory 的 **BFS 序号**」。所以：

- 新面板**只能追加在末尾**；
- 绝不能重排/删除已有面板；
- 加按钮不影响序号（按钮不是 inventory），但加/删 inventory 会。

见坑 #24。

---

## 5. 液体

| API | 说明 |
|---|---|
| `Liquid.Liquids` / `Liquid.GetCurrentPartFromId(id)` | 液体表 |
| `WaterHelper.GetLiquidParts(container)` | 各液体含量数组（**会分配数组，不要每帧调**） |
| `WaterHelper.AddLiquid(container, liquidId, amount)` | 加液体（按容量截断） |
| `WaterHelper.AddWater(container, grade, amount)` | 加「水」（`grade` 按水质档位，0 纯水 / 1 优质……；单位 `parts`，1 ml = 1000） |
| `WaterHelper.Remove(container, parts)` | 移除指定量，**保留原价** |
| `WaterHelper.IsEmpty(container)` | 是否空（与水质检测仪同源） |
| `WaterHelper.InitLiquidContainerItem(...)` | ⚠️ 会被水特征/贴图系统牵连，**在物品工厂里调用会 NRE**（坑 #22） |
| 「轻量初始化」——**自己写，不是官方 API** | 只加 `LIQUID_CONTAINER_TAG` + 容量 + 清零 `CURRENT_PART_*`；自定义容器在工厂里用这套，别调上一行的 `InitLiquidContainerItem` |
| `WaterHelper.EmptyContainer(container)` | ⚠️ **会把 `unitValue` 改成水的价格**（坑 #20） |
| `WaterFeatureHelper.GetPurityArrayIndex(int purity)` | 把水质值转成档位下标（0 纯水 … 5 沟渠） |
| `WaterFeatureHelper.OnLiquidChanged(item, bool)` / `OnLiquidContainerPourOutOf(item)` | 通知系统液体变了 |
| `SpriteHelper.UpdateWaterContainerSprite(item)` | 刷新容器图标液面 |

---

## 6. 模组（Module）与属性

| API | 说明 |
|---|---|
| `ModuleHelper.ComputeModuleEffect(inv.childItems, machine)` | 计算一组模组对机器的加成 |
| `ModuleHelper.ApplyBasicModuleEffect(...)` | 把加成写成 tag / 改功耗 |
| `ModuleHelper.BONUS_PERCENTAGE_*_INT` / `TOTAL_PERCENTAGE_*_BONUS_INT` | 属性 tag |
| `ModuleEffectHelper.Degrade(item, n)` | 退化（只对有退化词条的） |
| `ModuleEffectHelper.ModifyTempStatFromBaseByPercentage(item, pct)` | 按基础值百分比改临时属性（**基础值 <10 时取整为 0**，见坑 #19 的说明） |
| `ModuleEffectHelper.OnUsedLearningAlgo(...)` | 自学习成长；**拿缺省上限 0 当上限，负面属性会越学越高**，别直接用于夜间结算 |
| `item.GetTagReadonly("MODULE_EFFECT1_TAG")` / `MODULE_EFFECT2_TAG` | 模组随机效果 id（`MODULE_EFFECT_PREMIUM` 等）；没有就是裸模组 |
| `ModuleEffectHelper.moduleEffects[id].displayName` | 效果中文名（与 tooltip 同源）；官方新增词条自动跟随 |
| `ModuleEffectHelper.OVERVOLTED.onUsedAction` | 超载：`RNG.Roll(10)`（10%）→ `item.Destroy()` + `ItemSpawner.Spawn("system_module_ruined")` + `TryAcceptAll(parentInventory, 新物品)` |

---

## 6.5 交互委托（拖拽判定）

| API | 说明 |
|---|---|
| `GameItem.mayThisTargetItemFunc` / `canThisTargetItemFunc` / `onThisTargetItemFunc` | **源物品侧**的委托（「我能不能拖到别人身上」） |
| `GameItem.mayItemTargetThisFunc` / `canItemTargetThisFunc` / `onItemTargetThisFunc` | **目标物品侧**的委托 |
| `GameItem.MayTarget` / `CanTarget` / `Target` | 原版判定/执行入口；`MayTarget` 的判定是 **AND 聚合**，目标侧一个 false 就整体否决（坑 #42） |
| `GameItem.MayHaveValidInventorySlot` | `EndDrag` 里的「能不能存进这个容器」判定；前缀返回 false 可强制走 `Target`（坑 #47） |

> 做「拖到容器图标上批量处理」时：交互全部注册在**源物品侧**，目标侧不注册；再配合 `MayHaveValidInventorySlot` 前缀、`MayTarget`/`CanTarget` 后置、`Target` 前缀。

---

## 7. 库存与物品搬运

| API | 说明 |
|---|---|
| `EmporiumEntry.Instance.GetAllOwnedItems()` | 全仓库（含机器里嵌套的物品）|
| `EmporiumEntry.Instance.GetAllOwnedItemsByTag(tag)` | 按 tag 过滤 |
| `inventory.TryFindOneValidInventorySlot(item)` | 找能放下的格子 |
| `inventory.TryFindOneValidInventorySlot(item).TryAcceptOnce(n)` | 跨库存搬 n 个（自动拆堆、自动从原库存 Expel） |
| `inventory.TryAcceptAll(item)` / `UncheckedAccept(item)` | 放一个物品；判断成败看 `item.parentInventory != null` |
| `GraphUtils.EmporiumTryAdd(item)` | 原版溢出链：背包 → 后仓 → 货架 → 展示柜 → 前台 → 全满则销毁 |
| `GraphUtils.CanAccept(inventory, item)` | ⚠️ 内部走 `TryFindOneValidInventorySlot`，对**还没进任何库存的新物品**不可靠 |
| `GraphUtils.TryAcceptAllOrDestroy(inv, item, reason)` | ⚠️ **放不下就直接销毁**，没有溢出链（博士柜台吞货就是这么来的） |
| `GraphUtils.TransferLiquidToChem` / `IntroduceImpurity` / `GradeProduct` / `CheckLabPure` | 化学制剂流程的步骤函数（可照抄） |
| `GeneralHelper.IsItemOwned(item)` | 是否「属于玩家」；补回物品时不要漏 `IS_OWNED_TAG` |
| `SaveManager.IsDecoding` | 读档期间为 true；**读档时不要动物品** |
| `PlayerStore.Instance.AddDirectSellingItemToTable(item, ...)` | 让博士/商人上架 |

---

## 8. 客户、夜生活、职业

| API | 说明 |
|---|---|
| `StoreClientList.CreateSupplier()` | 造一个「供货商」客户；可照抄其结构 |
| `StoreClientFactionSetup.InitLowerLevelCitizen(client)` | 设置客户阵营/外观 |
| `SpriteDict.GetRandomMerchantSpriteName()` | 随机商人立绘名 |
| `client.AddBasicDialog()` / `CompleteClientCreation()` / `SetEndAction(Action)` | 对话收尾 |
| `ClientIntent.SELL` / `BUY` | 客户意图 |
| `StoreClientManager.AddClient(client)` | 往当天队列里加客户 |
| `ModHelper.IsNetworkUpgradeUnlocked(id)` | 职业树节点是否解锁 |
| `ModHelper.AddNetworkUpgrade(...)` | 注册职业节点；**不能在 `Init` 里调**（`PlayerStore` 还没构造，坑 #18），放到 `OnGameLoadedNormal` / `OnNewGameStarted` |
| `ModHelper.AddStartingScenario(...)` | 注册开局场景（静态列表，`Init` 里安全） |

---

## 9. UI、文本与 tooltip

| API | 说明 |
|---|---|
| `new RichTextElement(...)` + `SetText(text, width, color)` | **首选**：和原版菜单/窗口同一套渲染 |
| `TreeNodeRender.Instantiate()` / `InstantiateText()` | 底层节点；`InstantiateText` 手搓 TMP 有「框在字没」的坑（坑 #12） |
| `RenderHandler.current.windowUICanvas.scaleFactor` | 游戏 UI 的缩放系数（常见值 2），自建 Canvas 要抄它 |
| `RenderHandler.ColorPalette.*` | 原版配色（`LightCyan`、`LightGray`、`Gray`……） |
| `TooltipManager._instance.SetAndShowTooltip(text, owner)` / `HideTooltip()` | 原版悬浮说明 |
| `RichTextBuilder.Add(...)` | tooltip 里追加行；**会把 `\n` 拆多行、不转义 `<`**，所以可用富文本 |
| `RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, null)` | 手算命中（不想加 `GraphicRaycaster` 时） |
| `LocHelper.GetLocalizedUIOrDefault(key, fallback, ...)` / `GetLocalizedItem("item_<id>_name")` | 本地化 |

---

## 9.5 违禁品与搜查（`ContrabandHelper`）

| API / 事实 | 说明 |
|---|---|
| `ContrabandHelper.IsSmugglerBay(item)` | 原版只认 `smuggler_bay` / `smuggler_bay_mod` / `smuggler_bay_mini` 三个物品 id |
| `ContrabandHelper.IsBlockSearchContainer(item)` | `item.IsTag("ITEM_HIDDEN_TAG") || IsSmugglerBay(item)` |
| `ContrabandHelper.RecursiveInspect(...)` | 命中 `IsBlockSearchContainer` 后 `CollectConcealed` 并 **return，整棵子树不再检查** |
| `ContrabandHelper.FindAllContrabandItem(ship)` | **飞船货舱抽查是独立路径**，原版走私者暗格在这条路径上也不规避（只按 `ITEM_HIDDEN_TAG` 过滤） |
| `ShipManager.InspectCargoHold` | 触发上面那条路径 |
| `EmporiumEntry.hiddenElement.onSlotRemoveItemEndFunc` | 只在 `IsSmugglerBay` 命中时才保留 `ITEM_HIDDEN_TAG` |
| `ITEM_HIDDEN_TAG` | **不要**用它做常驻「暗格」标记：随 `modifiedState` 进存档，卸载模组后残留（坑 #39） |

> 想扩展「哪些容器算暗格」，用纯运行时补丁改上面三个判定函数；父链判定用 `item.parentInventory.GetParentItem()`，带 64 层防死循环。

---

## 9.6 窗口、射线与自绘 UI

| API | 说明 |
|---|---|
| `PixelWindow.Show()` / `Hide()` / `IsVisible()` | `Hide()` 只 `SetActive(false)`，**矩形仍在**（坑 #34） |
| `PixelWindow.handler` / `_inner` | `handler` 的 RectTransform 不是窗口实际大小；命中用 `_inner`（坑 #33） |
| `new PixelElement(...)` 子类 | 自绘面板；卡片按物品形状占格 |
| `RenderHandler.RaycastElement<GameItem>(screenPos)` | 鼠标下的物品（用于「拖到场景里的容器上」） |
| `RenderHandler.RaycastFindParentElement<GameInventory>(screenPos)` | 鼠标下的库存窗口 |
| `LabelerBlueUIManager.Instance.OpenUI(text, onConfirm, onClear, maxLen)` | 原版输入框，支持中文输入法（坑 #37） |
| `TypeHelper.GetTypeDisplayName(type)` | 物品类型的中文名 |
| `GraphUtils.FindChildrenType<T>(node)` / `FindParentType<T>(node)` | 遍历库存子树 / 找父物品 |
| `GraphUtils.FindChildType<T>(node, predicate)` | 只扫第一层、找到第一个就停（拖拽每帧调用时用这个） |
| `GraphUtils.FindAllChildrenType<T>(node)` | 递归全扫（批量执行时用，可进嵌套工具箱） |
| `GraphUtils.TryAcceptAll(node, item, amount)` | 跨库存自动找位（**返回数量，不是 bool**） |
| `HighlightSlot(...)` | 容器元素上的槽位高亮（具体挂在哪个类上按容器类型确认） |
| `GameItemElement.rectTransform` | 物品实际占格矩形（= `middle`）；`handler.rectTransform` 尺寸为 0（坑 #48） |
| `machine.SetShape(string shape, int width)` | 非矩形大形状：`'0'` 可放 / `'1'` 不可放（坑 #49） |

---

## 9.7 输入与原生光标

| API / 事实 | 说明 |
|---|---|
| `Input.mousePosition` / `RectTransformUtility.WorldToScreenPoint` | **窗口客户区、左下原点** |
| `SetCursorPos` / `SendInput`（`user32.dll`） | **屏幕坐标、左上原点**；调用会触发扫描器 `native` 能力，manifest 必须声明 `<Capability>native</Capability>`（坑 #48） |
| `ClientToScreen(GetForegroundWindow())` | 把客户区坐标换成屏幕坐标；再 `Screen.height - y` 翻转 Y |
| `Input.GetAxisRaw("Horizontal"/"Vertical")` | Unity 默认 InputManager 自带 joystick 轴；`KeyCode.JoystickButton0..19` 直接读手柄，不需要 XInput |

> 手柄/光标注入类模组会申请 `native` 能力，玩家启用时要额外信任一次；工坊描述里要写清楚为什么需要。

---

## 10. 存档与读档

| 事实 | 说明 |
|---|---|
| 存档是 ES3 明文 JSON | `save_<slot>.es3`，可直接读来核对 |
| `PlayerStore.mainInvJSON / backInvJSON / hiddenInvJSON / showcaseInvJSON` | **public** 字段，存着各库存的节点 JSON；做「旧档补偿」时用它算期望值 |
| `PlayerStore.itemDisplayNameCache` | **public** `Dictionary<string,string>`，会被 ES3 一起写进存档；可借来存少量状态 |
| `PlayerStore.playerCash` | **public int**；负数就是负债（配合一周结算做「可负债交易」） |
| `SaveManager.DecodeNodes` | 读档回填子物品，按「保存时那份 inventory 的 BFS 索引」 |
| `SaveManager.DecodeSaveItem` | 单个物品解码；顺序是 `LoadUpgrade → ... → onLoaded`，所以 `onLoaded` 能盖掉前面改坏的形状 |
| `ES3.Load/Save` | 全局键值存档（`modOrder` 就是用它） |

> **不要新增存档字段或改既有结构**。官方没有可靠的存档扩展入口，改结构会在版本更新/旧档上炸。替代方案见 [05-代码配方](05-代码配方.md) 第 6 节。

---

## 11. 按功能查表

| 想做的功能 | 官方入口 |
|---|---|
| 新物品 / 新机器 | `OnModItemDirectoryInit` + `dir.Set` + `MachineHelper.*` |
| 改原版机器面板/格子 | 无入口 → 补丁（工厂后置 / `GameGridInventory.Validate` 前缀） |
| 给物品加右键菜单项 | 无入口 → 独立浮窗 + `ItemContextHandler` 的 `OnEventPress/Release/Update/FindContextMenuNode` 补丁 |
| 物品图标上加角标 | 无入口 → `GameItemElement.ValidateSelect` 后置 |
| 给物品 tooltip 加行 | ✅ `OnCreateTooltipLate` |
| 每天推进逻辑 | ✅ `OnGoingSleepEarly/Late` |
| 客户/商人 | ✅ `OnGenerateCustomerNormal` |
| 商店补货 | ✅ `OnPlaceInventorInventoryItemEarly` |
| 开局发东西 | ✅ `OnNewGameStarted` |
| 职业树节点 | ✅ `OnGameLoadedNormal` + `ModHelper.AddNetworkUpgrade` |
| 自定义液体容器 | 无官方「自定义容器 + 拖拽落点」入口 → 轻量初始化 + 拖拽补丁（坑 #21、#22） |
| 新结局 | 无入口 → 自己调 `GameOverUIManager.SetEnding` 等三件事（见 05 第 7 节） |
| 汇总/终端类窗口（列表、搜索、拖出） | 无官方入口 → 自绘 `PixelWindow` + `PixelElement`；见 05 第 13 节、坑 #31～#37 |
| 让某些容器「不被搜查」 | 无官方入口 → `ContrabandHelper` 的 `IsSmugglerBay` / `IsBlockSearchContainer` / `FindAllContrabandItem` 三处后置补丁（见 9.5、坑 #39） |
| 搜索输入框 | ✅ 借原版 `LabelerBlueUIManager.OpenUI`（不要自建 `TMP_InputField`） |
| 拖一个工具到容器/机器图标上批量处理 | 无官方入口 → `MayHaveValidInventorySlot` 前缀 + `MayTarget`/`CanTarget` 后置 + `Target` 前缀（见 6.5、05 第 14 节、坑 #47） |
| 在物品图标上标出「词条」 | 无官方入口 → 读 `MODULE_EFFECT1/2_TAG` + `moduleEffects` 字典，`ValidateSelect` 后置画角标（见 05 第 15 节） |
| 异形大机器（几百格） | 无官方入口 → `SetShape(shapeString, width)`；非矩形成本高（见 05 第 16 节、坑 #49） |
| 手柄 / 光标注入 | Unity 手柄轴直接可读；注入光标要 `native` 能力 + 三套坐标换算（见 9.7、坑 #48） |
