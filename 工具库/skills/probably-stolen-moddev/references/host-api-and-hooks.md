# 原版一等接口与核心系统接缝速查

本文档汇总经多个工坊模组与宿主反编译交叉核实的原版公开 API，编码时优先选用，避免重复造轮子或手写脆弱反射。

## 1. `ModHook` 完整事件面

| 阶段 | 事件 | 典型用途 |
| --- | --- | --- |
| 资产与目录 | `OnModAssetsLoaded(LoadedMod)` | 将 `mod.Sprites` / `mod.PaperSprites` 设为 `FilterMode.Point` 防像素发糊 |
| 资产与目录 | `OnModItemDirectoryInit(ModItemDirectory)` | 注册自定义 `GameItem` 工厂（`dir.Has(id) ? dir.Set(...) : dir.Add(...)`） |
| 读档四阶段 | `OnGameLoadedInit` → `Early` → `Normal` → `Late` | `Init` 清缓存；`Early` 还原自定义事件；`Normal` 挂科技树/业务；`Late` 刷新 UI/书架 |
| 开新档 | `OnNewGameStarted(string scenarioId)` | 重置状态并同步新开局资源 |
| 访客生成 | `OnGenerateCustomerVeryEarly` / `OnGenerateCustomerVeryLate` | 插入模组专属访客或鉴定日检 |
| 交互与日常 | `OnCreateTooltipLate(RichTextBuilder, GameItem)` | 零 Harmony 追加物品悬停提示（用 `ConditionalWeakTable` 防重） |
| 交互与日常 | `OnPaperOpened` / `OnShutterOpenedLate` / `OnShutterClosedLate` | 交互书页、卷帘门开闭响应 |
| 营业后 | `OnAfterhourArriveLate` / `OnAfterhourLeaveLate` / `OnReturningStoreLate` | 营业后外出事件响应（详见 `references/afterhour-locations.md`） |

## 2. `ModHelper` 高价值 API 清单

- **存档私有数据**（随存档保存、不污染原版结构）：
  `GetModData` / `SetModData` / `RemoveModData` / `GetModDataKeys` / `GetModDataInt|Float|Bool` / `SetModDataInt|Float|Bool` / `GetModDataFolder`
- **模组目录读写**：`GetModFolder` / `ReadModFileText` / `ReadModFileBytes` / `ModDataFileExists` / `ReadModDataFileText` / `WriteModDataFileText`
- **鉴定系统**：`AddAppraisalItem` / `RemoveAppraisalItems` / `GetAppraiserRank`
- **开局剧本**：`AddStartingScenario` / `RemoveStartingScenarios` / `IsStartingScenario`
- **关系网升级**：`SetNetworkTabLabel` / `AddNetworkUpgrade` / `RemoveNetworkUpgrades` / `IsNetworkUpgradeUnlocked` / `GetNetworkUpgrade`
- **交互纸张**：`CreatePaper` / `OnPaperOpened(modId, bookId, Action<PaperUI>)`
- **游戏设置菜单嵌入**：`AddGameOptionSection` / `AddGameOptionToggle` / `AddGameOptionDropdown` / `AddGameOptionSlider` / `AddGameOptionKeyBind` / `RemoveGameOption`
- **跨模组松耦合通信**：`Expose(modId, key, obj)` / `Lookup<T>(modId, key)` / `SendMessage` / `OnMessage` / `OffMessage`
- **调试与后台**：`AddDebugFunction` / `RemoveDebugFunctions` / `RunInBackground` / `GetConfig` / `GetCurrentLocale` / `GetLocalized`

## 3. `ModReflect`（零能力安全反射）

```csharp
var table = ModReflect.GetStatic<AppraisalRankHelper.Rank[]>(typeof(AppraisalRankHelper), "Ranks");
```
可读取原版内部静态字段而不触发 `reflection-external` 权限要求。

## 4. 物品、容器、机器与顾客关键 API

- **店内就绪链**：`PlayerStore.HasInstance && EmporiumEntry.Instance != null && EmporiumEntry.Instance.invElement != null && StoreUIManager.Instance != null && PlayerStore.Instance.playerLocation == 0 && (MapUIManager.Instance == null || !MapUIManager.Instance.IsOutside)`
- **目录枚举与检验**：`DirectoryMaster.Has<GameItem>(id)`、`DirectoryMaster.GetIdentifierList<GameItem>(null)`、`ItemSpawnerHiddenList.IsHidden(id)`
- **全店扫描**：`PlayerStore.Instance.FindAllItem(isOwn: false)`（默认 `true` 只返拥有物品，扫描全部实体须传 `false`）
- **三段过夜回调**：`onCycleEndEarlySlotItemFunc`（扣费前）→ `onCycleEndSlotItemFunc`（主段）→ `onCycleEndLateSlotItemFunc`（保冷过期前）；模拟过夜优先调 `DebugPanel.SkipNightMachine()`
- **电池**：`PowerHelper.SetPowerSourceAt(item, energy)`；判断电池先 `IsTag("power_source_item")` 且排除 `HypertechBatteryHelper` / `CurrentConverterHelper`
- **交互状态**：`ItemMouseDragHandler.current.IsDraggingItem` / `.GetCurrentItem()`、`ItemMultiSelectHandler.current.IsGroupDraggingItem(item)`、`ItemSelectHandler.current.GetEquippedItem()`
- **拒收与识破**：`StoreClient.IsClientRefusingItem` 先于一切正面接受逻辑；`clientBlackTagList` 匹配的是 `itemTypes` 而非 `EnableTag`，自建标签物品用 `clientBlackIdList` 拒收。
- **事件池扩展**：往 `StoreEventManager.threatEventBlueprints` 添加 `new StoreEventBlueprint(() => CreateEvent(), odd, "modid:key")`，并在 `StoreEventActionDict.actions` 绑定 `:start`/`:update`/`:end`/`:clients` 四个回调；用 `RNG.GetOdd` 保证吃存档种子。
- **调试面板**：`CustomUIDebugPanel.Add(category, name, action, tall, source, info)` / `Remove(source)`（正式版发布前须关闭）。
