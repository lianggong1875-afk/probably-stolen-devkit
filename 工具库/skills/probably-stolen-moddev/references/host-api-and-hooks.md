# 原版一等接口与核心系统接缝速查

本文档全部符号已对照工作区 `共享开发/reference/Assembly-CSharp/`（944 个原版 `.cs`）逐项核实。写 Harmony Patch 前必须先在这里找公开口子。

## 1. `ModHook` 完整事件面（实测 44 个 `public static event`）

| 阶段 | 事件名 | 典型用途 |
| --- | --- | --- |
| 资产与配置 | `OnModAssetsLoaded(LoadedMod)`、`OnModItemDirectoryInit(ModItemDirectory)`、`OnModConfigApplied(string)` | 设 `FilterMode.Point`、注册物品工厂（`dir.Has(id) ? dir.Set(...) : dir.Add(...)`）、响应配置保存 |
| 掉落与新档 | `OnLootTablesLoaded`、`OnLootRolled(LootRollContext)`、`OnNewGameStarted(string)`、`OnDifficultyApplied(string)` | 注入掉落表、新档初始化、难度模式总闸 |
| 读档四阶段 | `OnGameLoadedInit` → `Early` → `Normal` → `Late` | `Init` 清缓存；`Early` 还原事件投影；`Normal` 挂科技树/业务；`Late` 完整性自检与刷 UI |
| 访客五阶段 | `OnGenerateCustomerVeryEarly` / `Early` / `Normal` / `Late` / `VeryLate`（`Action<StoreClientManager>`） | 注入模组顾客、交错排队、触发事件访客 |
| 营业日循环 | `OnShutterOpenedEarly/Late`、`OnShutterClosedEarly/Late`、`OnGoingSleepEarly/Late`、`OnWakingUpEarly/Late` | 开门发纸条/货、关门弹服务、睡前清理、次日回店补发 |
| 离店与过夜 | `OnLeavingStoreEarly/Late`、`OnReturningStoreEarly/Late`、`OnHandlingNightlyServicesEarly/Late` | 离店/回店发奖、过夜结算介入 |
| 货架与提示 | `OnPlaceInventorInventoryItemEarly/Late(List<GameItem>)`、`OnCreateTooltipEarly/Late`、`OnCreateDevTooltipEarly/Late` | 零 Harmony 扩展物品 Tooltip（用 `ConditionalWeakTable` 防重） |
| 纸张与科技树 | `OnPaperOpened(string, PaperUI)`、`OnNetworkUpgradeUnlocked(string)` | 交互式 Paper 接线、科技树解锁响应 |
| 夜晚外出 | `OnAfterhourArriveEarly/Late(string)`、`OnAfterhourLeaveEarly/Late(string)`、`OnAfterhourEventStarted/Ended(string, string)` | 夜晚地点进出、打烊活动事件 |

## 2. `ModHelper` 高价值 API（实测 145 个 `public static` 方法，零能力首选）

- **存档私有数据**（随存档走、不污染原版 ES3）：`GetModData` / `SetModData` / `HasModData` / `RemoveModData` / `GetModDataKeys` / `ClearModData` / `GetModDataInt|Float|Bool` / `SetModDataInt|Float|Bool`
- **沙箱文件读写（实测零能力，勿手拼路径）**：`GetModFolder` / `GetModDataFolder` / `ModFileExists` / `ReadModFileText` / `ReadModFileBytes` / `ModDataFileExists` / `ReadModDataFileText` / `WriteModDataFileText` / `WriteModDataFileBytes` / `DeleteModDataFile` / `ListModDataFiles`
  - **实测坑**：加载器对 `ModData` 目录名会跑 `SafeFolderName`（点号转下划线），**必须调 `GetModDataFolder(modId)`，绝不手拼路径**。
- **物品/金钱/搬运**：`SpawnItem` / `SetItemOwnerShip` / `AddCredit` / `MoveItemToFrontCounter` / `BackCounter` / `Inventory` / `Trash` / `DisplayCase` / `Afterhours`
- **鉴定 / 剧本 / 难度 / 关系网**：`AddAppraisalItem` / `RemoveAppraisalItems` / `GetAppraiserRank` / `AddStartingScenario` / `RemoveStartingScenarios` / `IsStartingScenario` / `AddStartingPerk` / `AddDifficulty` / `SetNetworkTabLabel` / `AddNetworkUpgrade` / `RemoveNetworkUpgrades` / `IsNetworkUpgradeUnlocked` / `GetNetworkUpgrade`
- **交互纸张 / 设置菜单**：`CreatePaper` / `RegisterPaperBook` / `OnPaperOpened(modId, bookId, Action<PaperUI>)` / `AddGameOptionSection` / `Toggle` / `Dropdown` / `Slider` / `KeyBind` / `RemoveGameOption`
- **跨模组零依赖通信**：`Expose(modId, key, obj)` / `Lookup<T>(modId, key)` / `IsModRunning(modId)` / `SendMessage` / `OnMessage` / `OffMessage`
- **线程 / 调试 / 本地化**：`RunInBackground` / `RunOnMainThread` / `Delay` / `AddDebugFunction` / `RemoveDebugFunctions` / `GetConfig` / `GetCurrentLocale` / `GetLocalized` / `SetClientSource`

## 3. `GameItemFunc`（51 个委托）与 `ModReflect`（零能力反射）

- `GameItemFunc`：拖拽三组九个 target 委托（`CanTarget`/`MayTarget`/`Target` 等）、容器准入（`mayInventoryAddItemFunc`）、堆叠、槽位优先级、父子形状/贴图/状态修饰、自定义 Tooltip 富文本、每秒 tick、读档回调 `onLoaded`。
- `ModReflect`（零能力读原版私有静态字段，不触发 `reflection-external`）：
  ```csharp
  var table = ModReflect.GetStatic<AppraisalRankHelper.Rank[]>(typeof(AppraisalRankHelper), "Ranks");
  ```

## 4. 物品、容器、机器、顾客与事件关键真相

- **店内就绪链**（模板 `ReadyChain.cs`）：`PlayerStore.HasInstance && EmporiumEntry.Instance != null && EmporiumEntry.Instance.invElement != null && StoreUIManager.Instance != null && PlayerStore.Instance.playerLocation == 0 && (MapUIManager.Instance == null || !MapUIManager.Instance.IsOutside)`
- **目录与全店扫描**：`DirectoryMaster.Has<GameItem>(id)` + 排除 `ItemSpawnerHiddenList.IsHidden(id)`；全店扫描传 `PlayerStore.Instance.FindAllItem(isOwn: false)`（默认 `true` 会漏掉非玩家拥有物）。
- **三段过夜回调与机器加速**：`onCycleEndEarlySlotItemFunc` → `onCycleEndSlotItemFunc` → `onCycleEndLateSlotItemFunc`；进度推进方法全称是 **`MachineProgressHelper.ContinueProgressTypeMachine(machine)`**；改机器速度/产出用 `TransientTagBoost.cs` 临时改数值 + Postfix/Finalizer 还原，不要整段替换入口。
- **电池与交互态**：`PowerHelper.SetPowerSourceAt(item, energy)`；判电池先 `IsTag("power_source_item")` 且排除 `HypertechBatteryHelper` / `CurrentConverterHelper`。拖拽检查用 `ItemMouseDragHandler.current.IsDraggingItem`、`ItemMultiSelectHandler.current.IsGroupDraggingItem(item)`。
- **拒收与识破**：`StoreClient.IsClientRefusingItem` 一票否决；`clientBlackTagList` 比对的是 `itemTypes` 而非 `EnableTag`，自建标签物品须放进 `clientBlackIdList`；阵营初始化走 `StoreClientFactionSetup.InitScav` / `InitSecurity` / `InitBlackMarket` 等官方方法（不要手写阵营字符串）。
- **事件池扩展两条路（Station Events 1.4.0 勘误实证）**：
  1. **原版托管**：向 `StoreEventManager.normalEventBlueprints` / `threatEventBlueprints` 追加 `StoreEventBlueprint`；
  2. **自持蓝图（推荐，可控密度且不污染原版池）**：蓝图放私有 List，仅在 `StoreEventActionDict.actions` 注册 `:start`/`:update`/`:end`/`:clients`，在 `OnDayStart` Postfix 用原版 `RNG.GetOdd`（吃存档种子）抽取后调 `manager.ActivateEvent(ev, false, false)`；价格效果直接写入 `ev.negociationDatas`（零价格 Patch；注意双口径差异：`GameItem.GetCurrentValue()` 走 `IsTag` 可命中自定义标签，但 `NegociationData.IsNegociationDataAffectItem` 走 `GetGameItemType()` 只认原版 `itemTypes`）；保存时用 `Es3SafePersistence.cs` 浅克隆剥离防坏档。
- **调试面板分类坑（真机实测）**：`CustomUIDebugPanel.Add(category, ...)` **只认 14 个原版固定分类**（模组统一放 `"Modding"`），自造分类名会被回退到 `"Special"` 并刷 warning。

## 5. batch-8~16 源码核真增补（高频踩坑正门）

- **贴图解析完整链（互不交叉）**：`GameItemElement.ResolveSpriteByName` 中 `useSpriteFromMod=true` 走 `LoadedMod.Sprites`（不进 `SpriteDict`），`false` 走 `RenderHandler.LoadFromAtlas`。故**物品图标自愈挂 `ResolveSpriteByName` Prefix（声明 `HarmonyBefore`）**，切回原版必清 `useSpriteFromMod`；**立绘仍挂 `SpriteDict.GetSprite`**；角标首选 0-Patch `GameItemFunc.getItemSpriteModifierForItemFunc`。
- **机器永久加成 & 窗口插面板**：永久加成严禁直改 `TOTAL_PERCENTAGE_*`（拔模块会被 `ModuleHelper.ComputeModuleEffect` 冲零），须存私有 tag + `ModuleHelper.ComputeModuleEffect` Postfix 重算；给原版机器窗口加面板 Postfix `MachineHelper.CreateMachineryWindow`（或机器类的 `CreateMachineInventoryWindow`），用 `MachineryPanelRow.GetPanel` 判重后调 `MachineryPanelRow.Add`（零反射）。
- **补货 / 造成品 / 营收 / 堆叠正门**：商人补货与重掷调 `StoreClientList.AddScavGeneralStock` / `PlaceInventorInventory` / `PlaceInventorSpecialStock` / `PlaceSupplierInventory`（不自建 `Random`）；造成品机器/工具走 `PreBuiltItemHelper`；营收统计挂 `RunStats.OnItemSold` Postfix；**`GameItem.GetSplitItem` 不复制私有 `state`**，带状态物品禁开 `mayStackItemFunc` 堆叠；原位换件走 `GraphUtils.SwapItemsInPlace`。
- **液体成分与模态防点穿**：液体即物品 `TagSystem` 的整型微升 tag `CURRENT_PART_<LIQUID>`（键查 `Liquid.GetCurrentPartFromId`），精准分摊搬运用 `WaterHelper.SplitProportionally` / `WaterHelper.TransferLiquid`；模态窗挡背后物品拖拽只需置 `hoveringStoreUI = true`。
- **委托扩展与夜出取消防扣次**：扩展原版委托挂 `StoreRequest.FillRequest` / `StoreRequest.ActivateRequest`；取消夜出行程走 `EmporiumEntry.OnArriveStore(true)`（传 `false` 会扣一次 `visitLeftTonight`）；幸运值总闸在 `HealthData.GetLuck`；枪械订单扩展挂 `StoreClientListGun.GetRandomBasicOrder` / `GetRandomMidOrder` / `GetRandomHighOrder` 与 `GunModHelper.EnableStockSlot` 系列。
