# Digital Storage: Possibly Hyper-Compatible（数字仓储）蒸馏档案

- 工坊 id：`3845554726`　命名空间：`DigitalVault`　模组 id：`pawns.digital_vault`　版本：`1.0.0`
- 源码：`缓存文件夹\decomp_ws\3845554726`（33 个 `.cs`，约 11389 行）；物品：`hypertech_digital_vault`（仓）/ `_intake`（进料口）/ `_output`（出料口）
- Harmony 补丁**只有 4 个**，全打在 `GameItem` 上：`Target` / `CanTarget` / `MayTarget` / `MayStack`
- 凡属代码里看不到、只能推断的，都标了**（推测）**

## 0. 一句话本质

**它把"仓储"从"物品树上的容器"改成了"物品身上的一个字符串标签"。**

存入 = `SaveManager.EncodeNodes` 把整棵物品树编成 `SaveState` JSON → 连同特性 blob 压成一条自定义分隔符记录 → 塞进仓这个物品自己的 `TagState`（`DIGITAL_VAULT_DATA_STRING`）→ 原物品 `Expel` + `Destroy`。于是物品不再挂在 item tree 上：不占格子、不进全店扫描、不参与渲染、没有数量上限；取出时 `SaveManager.DecodeNodes` 反向重建一个**新** `GameItem`。

| 文件 | 行 | 作用 |
| --- | --- | --- |
| `ModEntry.cs` | 528 | `IMod` 入口、ModHook 订阅表、调试按钮、发放/放置 |
| `VaultData.cs` | 3062 | **全部核心**：读写标签、存/取、签名分堆、重建、图标 |
| `VaultCodec.cs` | 542 | 记录级紧凑编解码（`\u001f/\u001e/\u001d/\u001c/\u001b/\u0001` 分隔） |
| `VaultWindow.cs` / `VaultWindowScale.cs` | 1206 / 642 | `CustomUIManager` 窗口、分页、取料目标、缩放 |
| `VaultIntake.cs` / `VaultOutput.cs` | 741 / 748 | 进料口（仿漏斗）/ 出料口（取料目的地登记） |
| `VaultRecord/Variant/FeatureSet/FeatureBlob/ConditionBlob` | 101/38/12/188/77 | 数据模型（事实上的存档 schema） |
| `Vault*Compat*.cs`×4 + `StackGuard` + `DragCompat` | 46/18/18/24/25/42 | 4 个补丁与判定体 |
| `VaultSelfTest` / `VaultAudit` / `VaultFixtures` / `VaultWildeService` | 205/152/315/445 | 自检、批量审计、原版设施归位、网络升级发货 |

## 1. 入口与生命周期

### 1.1 IMod 三段（`ModEntry.cs`）

- `Init(ModManifest):29-52` —— `Log.Attach(new ModLog(manifest))`，然后 `new GameObject("pawns.digital_vault.directory")` + `DontDestroyOnLoad` + `hideFlags = 61`，挂 `VaultItemDirectory`（注册三个物品）与 `VaultPicker`（全模组唯一 `MonoBehaviour` 心跳）。目录注册在 **Init** 而非 OnEnable，整体 `try/catch`，失败只打日志。
- `OnEnable():54-96` —— `ModConfig.For("pawns.digital_vault")` 定义 7 个设置项（`auto_grant`/`pick_mode_default`/`drag_compat`/`withdraw_high_first`/`category_order`/`category_labels`/`font_size`），再订阅 Hook。
- `OnDisable():129-169` —— 反订阅 + `ModHelper.RemoveNetworkUpgrades` + `VaultWildeService.ResetRegistration()` + `RemoveDebugFunctions` + `VaultWindow.ResetState()`。
- **没有 `HotReloadPrepare/Activate`**（全目录 grep 无匹配）：热重载靠 `OnDisable/OnEnable` 整对重来。

### 1.2 ModHook 订阅表（订阅 `84-93`，反订阅 `142-151`）

| Hook | 处理 | 干什么 |
| --- | --- | --- |
| `OnGameLoadedInit` | `OnGameLoadedInit` | 只做 `VaultOutput.ResetRegistry()`：清空出料口表，记下当前 `PlayerStore` 作 `_boundStore` |
| `OnGameLoadedLate` | `OnGameLoaded` | 读档后总装（见下） |
| `OnShutterOpenedEarly` | `OnGameLoaded`（同一函数） | 卷帘门开再跑一遍；幂等靠各步骤自己判空/判 flag |
| `OnWakingUpEarly` | `OnNewDay` | 升级注册 + 待发货投递 + `EnsureOutputGranted` + `EnsureVaultUsable` |
| `OnLeavingStoreEarly` / `OnGoingSleepEarly` | `OnLeaveStore` | `VaultData.FlushDirty()` + `VaultWindow.CloseQuietly()` |
| `OnCreateTooltipLate` | 三个 `AppendTooltip` | 仓/进料口/出料口 tooltip |
| `OnNetworkUpgradeUnlocked` | `VaultWildeService.OnUpgradeUnlocked` | 买到 `DIGITAL_VAULT_ORDER` 后把运行时条目重置为可重复购买 |
| `OnModConfigApplied` | `OnConfigApplied` | 仅当 `modId == "pawns.digital_vault"` 时 `VaultWindow.OnSettingsChanged()` |

`OnGameLoaded` 用 `VaultPicker.RunAfter(秒, Action)`（协程 + `WaitForSecondsRealtime`）把重活摊开：`8s` 自检、`10s` 审计、`12s` `RebuildStacksInStore()`（受 ModData 键 `stacks_v13` 幂等保护）、`13s` 进料口扫描、`15s` 出料口扫描 + `EnsureOutputGranted`、`16s` 待发货投递；最后同步跑 `VaultFixtures.EnsureVaultUsable()`。

### 1.3 静态状态清理（抄的时候别漏）

| 状态 | 清理点 |
| --- | --- |
| `VaultWindow` 全部静态 UI 引用、`CategoryButtons`、`_targets` | `VaultWindow.ResetState():141-169`，在 `OnDisable`/`Open()` 重开/`CloseQuietly()`/窗口 `OnClose` 都调 |
| 出料口 `_ports`/`_boundStore` | `VaultOutput.ResetRegistry()`（读档前）+ `Tick()` 发现 `PlayerStore.Instance` 变了就整表清空重扫 |
| 网络升级 `_registered/_definition/_tick` | `VaultWildeService.ResetRegistration()` |
| 仓数据 `VaultFile` | **无显式清理**：`ConditionalWeakTable<GameItem, VaultFile>`（`VaultData.cs:28`），物品回收即消失；`DropCache()`/`HasDirty` 定义了但全目录无人调用（死 API） |
| `VaultSelfTest._done` / `VaultAudit._ran` | 都是 `static bool` 且**从不重置**——进程内只跑一次，跨读档也不再跑（代码事实） |
| `LiveIconCache`/`IconMissLogged`/`_payloadSeq` | 跨读档不清（key 是 sig/id，不会误命中） |

心跳只有一处：`VaultPicker.Update():53-104` 依次 `TickPickMode` → `TrackDrag`（拖拽诊断）→ `VaultIntake.Tick` → `VaultOutput.Tick` → `VaultIntakeWindow.TickPick` → `FlushTick`（**0.5 秒节流的 `FlushDirty()`**）→ `VaultWildeService.Tick`，每步各自 `try/catch`（一处炸不拖垮整帧，但也意味着静默）。

## 2. 数据化存储的核心模型

### 2.1 存在哪里：物品自己的 TagState（不是 ModData、不是 ES3）

| 键 | 类型 | 内容 |
| --- | --- | --- |
| `DIGITAL_VAULT_DATA_STRING` | string | 整包数据 `JsonUtility.ToJson(VaultFile)` |
| `DIGITAL_VAULT_KINDS_INT` | int | `records.Count`（tooltip"多少种"） |
| `DIGITAL_VAULT_UNITS_INT` | int | `TotalUnits()`（tooltip"多少件"） |
| `DV_LINK_VAULT_INT` | int | 仅进料口/出料口：绑定的仓 `uniqueId` |

ModData 里只有 4 个**计数器**：`output_granted`、`stacks_v13`、`wilde_pending_day`、`wilde_pending_count`（`ModEntry.cs:257-260,388-403`；`VaultWildeService.cs:180-182,245-246,370-382`）。**物品数据一条都不在 ModData**。写盘是 `machine.ModifyTag("DIGITAL_VAULT_DATA_STRING", st => st.SetString(json), true)`（`VaultData.cs:161-172`）——整包替换，天然原子，没有半包。

### 2.2 磁盘格式：JSON 外壳 + 控制符内芯

`VaultFile { version=2, entries: List<string> }` 只有这两个字段被 `JsonUtility` 序列化；`records`/`broken` 都标 `[NonSerialized]`，反序列化后逐条 `DecodeRecord` 解出。外层交给 Unity 处理 escaping，内层 `\u001f` 分字段、`\u001e` 分列表、`\u001d/\u001c/\u001b/\u0001` 分特性/特性集/变体/变体字段，字符串里出现这些控制字符时用 `\f \l \d \s \b \a` 转义（`VaultCodec.EncodeRecord:21-56`、`E()/U():444-541`）。收益：没有 JSON 键名与引号膨胀，48 个变体也不会变成 48 段 JSON。

### 2.3 记录结构：两层

- `VaultRecord.cs:7-37`：`sig, id, name, atlas, sprite, unitValue, count, leaf, types[], tags[], nodesJson, tooltip, features[], variants[], liquid`
- `VaultVariant.cs:7-21`：`count, fullSig, nodesJson, tooltip, sortValue, price, features[]`
- `FeatureSet`：`uuid`（**物品在 BFS 序里的下标**）+ `List<FeatureBlob>`；`FeatureBlob` 22 字段 + `real`/`fake` 两个 `ConditionBlob`（各 10 字段）

**键既不是 identifier 也不是 uniqueId，而是 16 位十六进制哈希 `sig`**（`Signature:2620-2634`）：

```text
sig = Hash64( identifier | CustomName | PurityKey | GradeKey | "L"/"T" [| "L:"+LiquidKey] )
Hash64 = FNV-1a 64（VaultData.cs:3051-3061，x16 小写十六进制）
```

`StageDeposit:721-730` 先按 `sig` 找桶（要求 `leaf` 也一致），桶内再按 `fullSig`（`VariantKey:2055-2083`）分变体：小容器（`PRE_CONTAINER_TAG` 且非 STORAGE/MACHINE、有内容）→ `bucketSig+"|v"+价格`；种子 → `bucketSig+"|seed:"+植物id/特性/复杂度`；有 `contentWindow` → `bucketSig+"|p:"+Hash64(SaveState 全字段摘要)`；有"有意义内容"（纯度/液体/子物品）→ `TreeSignature`（整棵树+全 state+全特性）；普通叶子 → 直接 `bucketSig`（不再细分）。

### 2.4 数量 / 耐久 / 纯度 / 特性 / 贴图怎么存

| 维度 | 存法 | 依据 |
| --- | --- | --- |
| 数量 | 叶子 `count += Math.Max(1, item.unitCount)`；非叶子（有子物品）强制按 **1 件** | `StageDeposit:710-712` |
| 耐久 / 一切原版状态 | **无专用字段**，全在 `SaveItemNode.itemState` 里随 `nodesJson` 走 | `PayloadSignature:2123` |
| 纯度 / 品级 | 只进 `sig`：`IngotPurityHelper`/`ChemicalProductHelper`/`WaterHelper` + 标签或特性名含 `PURITY\|PURE\|QUALITY\|GRADE\|CONCENTRATION` 的启发式取值 | `PurityKey:2697-2747`、`GradeKey:2958-2993`、`ScanPurityTag:2878-2925` |
| 液体 | `record.liquid` = 主成分 identifier（按 `Liquid.Liquids[].currentPart` 读 int 取最大），显示拼 `名称 · 液体名` | `LiquidKey:2636-2675`、`DisplayName:1929-1942` |
| 特性（含**真伪**） | `features[]`：复制 22 字段 + `real`/`fake` 完整 `ConditionBlob` + `displayReal/displayFake` + `SetExposed/SetFeatureDiscovered` | `FeatureBlob.cs:52-187` |
| 贴图 | `atlas = item.spriteAtlasPath`、`sprite = item.spritePath`（另存 `types/tags` 供兜底） | `StageDeposit:780-781` |
| 自定义名 | `CustomNameOf`：优先 `CUSTOM_NAME_TAG` 串，否则 `item.name` | `2597-2618` |

**真伪链路的要点**：它不去"判断"真伪，而是把 `realCondition` 与 `fakeCondition` **整块拷走**。任何第三方鉴定系统只要字段还落在 `ItemCondition` 那 10 个属性里，就能原样回来——这是它兼容性最强的地方。

### 2.5 取出时怎么重建一个可用的 GameItem

`Withdraw/WithdrawTo` → `PlaceFromVault` → `PickVariant`（按 `sortValue` 高/低优先，受 `withdraw_high_first` 控制）→ `PlaceVariant` → `MaterializeData:1301-1385`：

1. `JsonUtility.FromJson<SaveState>(nodesJson).saveItems`；2. `SaveManager.DecodeNodes(list)` 拿根；3. 用 `SaveItemNode.tempLink` 建 `uuid → GameItem` 字典（根为空时拿 `list[0].uuid` 兜底）；4. 逐 `FeatureSet`：`Clear()` 后把 `FeatureBlob.To()` 塞回（`uuid` 即 BFS 下标）；5. 对每个物品跑 `PostLoadFixups:1424-1444`——`MachineHelper.LoadUpgrade`/`UpdateWaterContainerSprite`/`LoadLockItem`/`LoadEdibleProduce`/`LoadTasteFeature`/`LoadNonOwnedToolTag`/`LoadImportantStoryItemTag`/`LoadHazardousWasteTag`/`LoadScavTokenTag`/`LoadLockedContainer`/`LoadBrokenMachine`/`MachineHelper.OnLoad`/`LoadTurboBoostEligibleTag`/`CurrentConverterHelper.OnLoad`/`onLoaded(item)`；6. `AssignFreshIdentity:1387-1422`：BFS 里每个物品 `uniqueId = PlayerStore.Instance.GetUniqueID()` 并同步改写 `ItemFeature.parentItemUniqueId`（**否则撞存档里已有 ID**）；7. 回 `PlaceVariant:1187-1276`：打 `DV_WITHDRAWING_TAG` → `unitCount = want` → `inventory.TryFindOneValidInventorySlot(val)` → 用 `Math.Min(want, slot.numTransfer)` 二次收敛 → `slot.TryAcceptOnce(n)` → 撤标签；放不下就 `Destroy` 并把数量退回仓。

> 它**不走** `DirectoryMaster.Item(id, true)` 那条"发给玩家"的工厂路径，而走原版存档解码路径——这是"复活带完整历史状态的物品"与"新造一个干净物品"的分水岭。

### 2.6 图标：四级兜底（`LoadIcon:1487-1532`）

只有 `NeedsLiveIcon(rec)` 为真（标签/id 含 `LIQUID|WATER_CONTAINER|FLUID|water|bottle|flask|vial|juice|wine|canister|tank|liquid`）才**实体化一次**取实时 sprite（液面会改贴图），结果按 `fullSig` 缓存进 `LiveIconCache`。其余依次：`ResolveDirectIcon`（模组 `Sprites` 字典 `atlas:sprite` → `RenderHandler.TryLoadFromAtlas` → `PaperAssets.Resolve` → `CustomUIManager.GetSprite` → `RenderHandler.LoadFromFile`）→ `ResolveById`（`rec.atlas` + 4 个原版图集 + `PaperAssets` + `CustomUIManager`）→ `FallbackIcon()`（自家 `icon_fallback`，最终 `RenderHandler.unknownSprite`）；每张都过 `IsBad()`（null 或等于 `unknownSprite`）。

## 3. 四个 Patch 的精确语义

| # | 补丁 | 类型 | 命中条件 | 返回 |
| --- | --- | --- | --- | --- |
| 1 | `GameItem.Target(GameItem)` | **Prefix**（`VaultTargetGuard.cs:9-45`） | `targetItem.identifier == "hypertech_digital_vault"` | 目标是仓则接管（存入/设备绑定/拒绝 → `false`；不可存 → `true` 交还原版，细分见下）；目标不是仓一律 `true` |
| 2 | `GameItem.CanTarget(GameItem)` | **Postfix**（`VaultCanTargetCompat.cs:8-17`） | 原结果为 `false`、目标是仓、源可存入 | 只把 `false` 改 `true`，绝不 `true→false` |
| 3 | `GameItem.MayTarget(GameItem)` | **Postfix**（`VaultMayTargetCompat.cs:8-17`） | 同上，同一个 `DragCompat.TryAllow` | 同上 |
| 4 | `GameItem.MayStack(GameItem)` | **Prefix**（`VaultMayStackGuard.cs:8-23`） | 任一方 `IsTag("DV_WITHDRAWING_TAG")` | `__result = false; return false` |

**为什么必须拦 `Target`**：这是"把 A 拖到 B 身上"的**执行**入口。仓没有 `contentWindow`（它是 `SetGameItemType("STORAGE")` 的纯数据物品，不是容器），让原版跑下去要么毫无反应要么真的搬/用掉物品，而本模组要"编码后销毁"。所以 Prefix 四级分派：目标不是仓 → 放行；源是本模组三个设备 → **不存入**，进料口/出料口被拖上来时执行 `BindDeviceToVault(device, vault)`（把仓 `uniqueId` 写入 `DV_LINK_VAULT_INT`）并提示"已绑定"，其它设备提示不可存，`return false`；`CanDeposit` 不通过 → `return true`（决定权还给原版，行为等同没装模组）；通过 → `DepositDeferred` 后 `return false`。

**为什么必须拦 `CanTarget`/`MayTarget`，且只能加 Postfix**：这两个是预判/高亮入口，仓不带容器语义，原版判定链会否掉。作者没去猜，而是留了诊断日志把原因全列出来（`VaultPicker.cs:169-204`：`force; outsideCombat; srcEffOwned; tgtEffOwned; … may= can= deposit= reason= funcs[srcMit/tgtMit/srcMtit/tgtMtit/srcSlotMit/tgtSlotMits]`）——即被 `forceDisableUse`、`canUseOutsideCombat`、`IsItemEffectivelyOwned` 以及 6 个 `may*Target*Func` 任一处否过。修法就是"只补 true 不补 false"（`TryAllow` 第一行 `if (result …) return;`），保证别人说不行而确实能存时我们收得到，别人说行时我们一个字都不改。`drag_compat` 设置可一键关掉这层放行。

**为什么必须拦 `MayStack`**：取料实例化的是一个**全新** `GameItem`（自带 `features`/`uniqueId`/`unitCount`）；若并进同 identifier 旧堆，原版合并会把特性、真伪、耐久并掉一半还重复计数。所以 `PlaceVariant` 交出前打 `DV_WITHDRAWING_TAG`（`1214`）、`TryAcceptOnce` 后立刻撤（`1254`），期间 `MayStack` 一律 `false`；`StackGuard.ShouldBlock` 异常时返回 `false`（放行），即"宁可让它正常堆叠也不要卡住游戏"。

**这套组合防住的崩坏**：① 不在拖放回调里销毁物品——`DepositDeferred` 用 `LetterboxManager.RunNextUpdate` 推到下一帧，`Pending` 集合去重；② 不半路丢物品——先 `StageDeposit`（只改内存 + 入 `staged`）再 `CommitDeposits`（才 `Expel`+`Destroy`），`Rollback:622-644` 负责把 `count` 减回去；③ 数据坏了不再写——`broken` 一旦为真，`Save`/`StageDeposit`/`Withdraw` 全拒（`148-152,685-690,948-951`）；④ 不留半个物品——失败路径全 `SafeDestroy`，成功但 `parentInventory == null` 也销毁（`1271-1274`）。

## 4. 重量 / 存档 / 过夜 / 扩容

**重量：没有。** 全目录 grep `weight|mass|Encumber|capacity|Capacity|Limit` → 零命中。它不读重量、不设容量体积；物品数据化后不占任何物理预算，取出时才按原版规则重新占格。"不限数量"是字面意思，唯一上限是 `int` 与存档字符串大小。

**存档体积**：① 同签名合并（500 个相同废料 → 1 记录 / 1 变体 / `count=500` / **1 份** `nodesJson`）；② `unitValue/price/count` 是数字字段不是 JSON；③ 记录体用控制符而非 JSON 键名；④ `Normalize()` 丢弃 `count<=0` 或 `nodesJson` 为空的记录/变体（`VaultFile.cs:29`、`VaultRecord.cs:76`）。反面代价（**必须知道**）：单变体记录会把 `nodesJson` 存**两份**（`EncodeRecord:44` 与 `EncodeVariants:78`），`features` 同理，所以"每种独特物品"约等于原版节点的 2 倍开销，上千种独特物品的档会很难看。

**落盘时机**：无即时写入，全先进内存 `VaultFile` 再 `MarkDirty`；`FlushTick` 每 0.5s 刷一次；`OnLeavingStoreEarly`/`OnGoingSleepEarly` 强制刷；界面"存档"按钮走 `PlayerStore.Instance.SaveGame()`（`VaultWindow.cs:1158-1176`）。即崩溃最多丢最后 0.5 秒一批存入，正常离店/睡觉不丢。

**过夜与出售**：它**没有 patch 任何卖出/结算/价格牌入口**，4 个补丁全在目标/堆叠语义上。物品在仓里时不在 item tree 上，因此天然不参与全店扫描、不上架、不被夜售结算——这是"数据化"的副作用也是卖点；代价是**仓里的东西不能直接卖**，要卖必须先取出。过夜只做三件事：`OnNewDay` 注册+投递+`EnsureOutputGranted`+`EnsureVaultUsable`，`OnGoingSleepEarly` 刷盘+关窗。仓本体 `EnableTag("IMPORTANT_TAG")`（`VaultMachine.cs:20`），配合 `GeneralHelper.LoadImportantStoryItemTag` 的存在，**推测**它被原版当作重要物品而不被夜间清理扫走。

**扩容 / 暗格**：扩容不需要——仓没有容量，扩容就是"再订一台"（`VaultWildeService` 的 `DIGITAL_VAULT_ORDER`：2000 恩惠、可重复购买、次日早上送到前台，一次发**仓+进料口+出料口**三件，`DeliverOneOrder:264-296`）。暗格 `hiddenElement` **只被读不被写**：出现在 `VaultFixtures.FindLooseFixture`（找回散落设施）、`VaultAudit`（审计扫描）、`ModEntry.Place`（放不下时的兜底）、`VaultOutput.StoreInventoryName`（标签"隐藏区"）。存入三条途径（界面按钮只给背包/柜台+店内台面/展柜；拖放是玩家自己拖；进料口只吸它所在容器）都不碰暗格；取出落点顺序 `原来的位置（仅原版固定设施）→ 店内台面 → 柜台 → 背包 → 展柜`（`PlacementTargets:1167-1185`）也不含暗格。**结论：暗格进不了仓，仓也取不进暗格**——要取进暗格只能靠把出料口放进暗格（`Snapshot()` 把"出料口所在容器"变成目的地，按容器去重、重名加 `#2`）。

## 5. "Hyper-Compatible" 的真身

**先说结论：代码里完全没有 `ModHelper.IsModRunning` / 模组存在性探测 / `HarmonyAfter("mod.<id>")`**（grep 零命中）。它的兼容是**结构性**的，靠不碰别人 + 走原版通用路径，一共 6 条：

1. **只写自己的键**：持久化只落自己物品的 4 个标签 + 4 个 ModData 计数器，从不改别人物品、不改 `PlayerStore` 结构。
2. **载体无关的快照**：存/取全走原版 `SaveManager.EncodeNodes/DecodeNodes`——任何第三方物品只要原版存得下它就存得下，**没有 id 白名单、没有 per-mod 分支**。
3. **特性整块抄**：`FeatureBlob.From` 用公开 API（`GetActualValueModifier`/`GetBaseRealDisplay`/`GetFakeValueModifier`/`IsExposed`/`GetToolNeeded`…）逐字段取；风险字段各自套一层 `try`（`SafeRealDisplay`/`SafeFakeDisplay`/`SetExposed`），读不到就留空，而不是整条放弃（`FeatureBlob.cs:52-187`）。
4. **启发式识别未知模组的纯度/品级**：标签或特性名里出现 `PURITY|PURE|QUALITY|GRADE|CONCENTRATION` 就当纯度（`LooksLikePurity:2836-2847`、`GenericPurityKey:2749-2834`），并把 int/double/float/string 四种存法归一成 0~100（`ScanPurityTag:2878-2925`）。**这才是"possibly hyper-compatible"的技术底牌**：不认识你的 mod，但认识你的字段名。
5. **品类三级兜底**（`VaultCategories.Resolve:86-161`）：类型精确匹配 → `TypeHelper.GetTypeDisplayName` → 中英关键词模糊匹配（`KeywordsFor:200-263`，如"弹药/子弹/ammo/bullet"）→ 全不成才进 `OTHER`。
6. **复用原版既有机制而非另起一套**：进料口沿用原版漏斗标签体系（`HOPPER_INTAKE_TAG`、`HOPPER_ON_ADD_BOOL`、`HOPPER_FILTER_MODE_INT`、`HOPPER_KEEP_INT`、`HOPPER_LINK_INT`…，`VaultIntake.Create:44-56`）并跳过 `HopperHelper.IsHopper`；原版固定设施（水龙头/下水道/垃圾桶/档案/录音机/售货机…）单独识别、拒存且能"放回原位"（`VaultFixtures.PutBack`）；封条补给箱按"锁定但允许存"放行（`IsSealedCrate:315-333`）；机器/容器一律不存（`IsMachineOrContainer:460-478`）。

**唯一过激的兼容动作**：`DragCompat.TryAllow` 会把别人判 `false` 的拖放强行改 `true`（只要目标是仓且 `CanDeposit` 通过）。若另一个模组用 `CanTarget` 表达"这类物品绝不能给任何人"，会被这层绕过。

## 6. 风险与限制 + 可复用模式

### 6.1 风险与限制

| # | 问题 | 证据 |
| --- | --- | --- |
| 1 | **满目录空 `catch {}`**：实测 **96 处**，连 `Log.cs` 自己都吞 → 与我们"对内暴露、边界捕获"规范直接冲突 | 正则统计 `catch(...){}` = 96 |
| 2 | **存档 2 倍冗余**：`nodesJson`/`features` 在 record 与 variant 各存一份 | `VaultCodec.cs:44,78` |
| 3 | **叶子可能被误并**：`Signature` 只采样 id/名/纯度/品级/液体/leaf，靠"名字不像纯度"的特性区分的物品会被并成同一变体，先入者特征胜出 | `Signature:2620-2634` + `VariantKey:2075`（**推测**） |
| 4 | **条件型特性丢 modifier**：`useCondition==true` 时还原分支只恢复条件，不调 `SetValueModifier`/`SetPreExposedValueMod` | `FeatureBlob.To:156-171` |
| 5 | **数据随物品亡**：数据只在该物品标签里，仓被销毁或卖掉 = 内容蒸发，无导出/备份 | `VaultData.Load:65-92` |
| 6 | **`broken` 只读锁不可自愈**：一次解析失败 → 仓永久只读，玩家只能改档 | `VaultData.cs:148-152` |
| 7 | **0.5s 去抖**：崩溃丢最后半秒的存入，没有"存完即刷" | `VaultPicker.FlushTick:106-114` |
| 8 | **强依赖原版私有流程** `EncodeNodes/DecodeNodes` + `PlayerStore.savedItemFeatureList`（后者会侧漏，靠记录 `count` 再 `RemoveRange` 裁掉） | `EncodeItemTreeJson:825-878` |
| 9 | 无 HotReload 钩子；无条件下编译；文案硬编码中英双份（`L10n.cs` 777 行） | 全目录 |
| 10 | 仓内物品不能直接卖/上架；无重量容量概念 | §4 |
| 11 | 反编译文本里有 GBK 乱码字面量（如 `"璇诲彇鐗╁搧鐗规€уけ璐ワ細"`），说明原作者是 UTF-8 源、反编译按 GBK 解了；引用这些串别当成作者文案 | `FeatureBlob.cs:94`、`VaultMachine.cs:89,119` |

### 6.2 可复用范式

#### 范式 A：跨回调的"下一帧投递"（拖放/点击里绝不销毁物品）

`缓存文件夹\decomp_ws\3845554726\DigitalVault\VaultData.cs:496-522`

```csharp
	internal static void DepositDeferred(GameItem machine, GameItem item)
	{
		if (!CanDeposit(machine, item, out var _) || !Pending.Add(item))
		{
			return;
		}
		LetterboxManager.RunNextUpdate((Action)(() =>
		{
			try
			{
				Pending.Remove(item);
				if (item != null)
				{
					GameItem val = item;
					GameItemElement val2 = (GameItemElement)(object)((val is GameItemElement) ? val : null);
					if (val2 == null || !val2.IsDestroyed())
					{
						DepositNow(machine, item, out var _, out var _);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Warn("拖入数字仓失败：" + ex.Message);
			}
		}));
	}
```

要点：`Pending` 做重入去重（同一物品连点只算一次）；`RunNextUpdate` 把"销毁物品"挪出当前 UI 回调；执行前再验 `IsDestroyed()`；失败至少 `Log.Warn` 留字。

#### 范式 B：单体标签塞整包数据 + "坏档只读锁"

`VaultData.cs:65-92`

```csharp
	private static VaultFile Load(GameItem machine)
	{
		VaultFile vaultFile = new VaultFile();
		string text = null;
		try
		{
			text = ReadTagString(machine, "DIGITAL_VAULT_DATA_STRING");
			if (!string.IsNullOrEmpty(text))
			{
				vaultFile = Deserialize(text);
			}
		}
		catch (Exception ex)
		{
			Log.Err("读取数字存储数据失败，已锁成只读以避免覆盖：" + ex.Message);
			return new VaultFile
			{
				broken = true
			};
		}
		vaultFile.Normalize();
		if (!string.IsNullOrEmpty(text) && text.IndexOf("\"entries\"", StringComparison.Ordinal) < 0 && text.IndexOf("\"records\"", StringComparison.Ordinal) >= 0)
		{
			Log.Err("数字存储数据是旧格式，已锁成只读以避免覆盖。");
			vaultFile.broken = true;
		}
		return vaultFile;
	}
```

要点：解析失败或旧格式 → `broken = true` → 上层 `Save()` 与 `StageDeposit()` 全拒，**玩家原始字符串一个字节都不会被覆盖**。这条"宁可只读也不覆盖"的取舍，比任何 try/catch 都值钱。

#### 范式 C：载体往返自检 + 清掉原版私有列表的侧漏

`VaultData.cs:852-878`（上下文 `825-836`：先 `list = PlayerStore.Instance.savedItemFeatureList; num = list.Count;`）

```csharp
			SaveState val = null;
			try
			{
				val = JsonUtility.FromJson<SaveState>(text);
			}
			catch
			{
				return null;
			}
			if (val == null || val.saveItems == null || val.saveItems.Count == 0)
			{
				return null;
			}
			if (val.saveItems.Count != list2.Count)
			{
				Log.Err("DV-GUARD| json round trip lost nodes: encoded=" + list2.Count + " restored=" + val.saveItems.Count + " id=" + ((item != null) ? item.identifier : "?"));
				return null;
			}
			return text;
		}
		finally
		{
			if (list != null && list.Count > num)
			{
				list.RemoveRange(num, list.Count - num);
			}
		}
	}
```

要点：编码后**立刻 JSON 往返并比对节点数**，不等玩家取出来才发现烂档；`finally` 里把 `savedItemFeatureList` 增长的部分裁掉（原版 `EncodeNodes` 会往这个共享列表塞东西，不清就被下次存档带走）；取出时还有第二道校验（`PlaceVariant:1196-1204` 打 `DV-GUARD| withdraw lost nodes`）。

#### 范式 D：预判钩子只"补 true"，绝不"补 false"

`DigitalVault\DragCompat.cs:12-41`

```csharp
	internal static void TryAllow(GameItem source, GameItem target, ref bool result)
	{
		if (result || !Enabled || source == null || target == null)
		{
			return;
		}
		try
		{
			if (!string.Equals(target.identifier, "hypertech_digital_vault", StringComparison.Ordinal) || source == target)
			{
				return;
			}
			string reason;
			if (VaultData.IsModDevice(source))
			{
				result = true;
			}
			else if (VaultData.CanDeposit(target, source, out reason))
			{
				result = true;
				if (Logged.Add(source.identifier))
				{
					Log.Err("DV-COMPAT| allowed drag of " + source.identifier + " onto vault");
				}
			}
		}
		catch
		{
		}
	}
```

要点：`if (result || …) return;` 决定它**只能放宽不能收紧**——多人模组环境里最安全的姿态；`Logged` 保证每种物品只记一次诊断；`Enabled` 让玩家能一键关掉这层越权放行。（注意它自己也有一处空 `catch`，抄的时候要换成 `LogError`。）

## 对我们的可用点

### 《深空电玩》（`deepspace_arcade` / `DeepSpaceArcade.dll`）

1. **卡带/奖品/机台零件的"数据化库房"可直接照 §2 的模型**：一个自建物品（`EnableTag` 自定义标签 + `SetGameItemType("STORAGE")` 是原版类型，不刷 `[Translation Error]`）+ 一个 `*_DATA_STRING` 标签装整包，就能做到"奖券/卡带不占格子、不限数量、不掉帧"。
2. **范式 A 是我们最缺的**：电玩城"投币→判定→出奖"常发生在 UI/拖放回调里，`RunNextUpdate` + `Pending` 去重 + 执行前 `IsDestroyed()` 复验，可一次消掉"回调里销毁物品导致空引用/幽灵 UI"这一整类崩溃。
3. **范式 C 的"编码后立刻往返比对节点数"**可搬去任何把状态序列化成字符串的地方（高分榜、存档卡、机台快照）：先证明往返无损，再上线。
4. **`VaultWildeService` 的"网络升级 + 次日发货 + ModData 记账 + 投递失败不清 pending"**是一套现成的"订购机台"流程（`EnsureRegistered`/`Deliver`/`TryDeliver`/`EnsureRuntime`），比"买完直接塞背包"更贴合游戏的夜间节奏。

### 《深空当铺：可能是群友》（`freya_cornell` / `DeepSpacePawn.dll`）

1. **`VaultRecord`/`VaultVariant` 双层分堆**正适合群友版的"保险库/寄售仓"：外层按"看起来一样"合并（`sig`），内层按"实质不一样"（纯度/品相/液体/内容物）分变体——刚好对上我们"判定只认 `realCondition`、但要给人看见差异"的诉求。
2. **§2.5 的复活四步（`DecodeNodes` → 按 `tempLink` 建表 → 重挂 `features` → 补跑 `PostLoadFixups` → `AssignFreshIdentity`）是"从数据复活带历史物品"的必备清单**；尤其连 `ItemFeature.parentItemUniqueId` 都要改，漏了会出现"鉴定结果绑到别的物品上"的诡异 bug。
3. **`CanDeposit` 的黑白名单可原样抄**：机器/容器/封条箱/原版固定设施/被锁定/非本人/空物品各有独立 reason 串（`VaultData.cs:249-313`），配 `NoteSkip` 打 `DV-SKIP| id reason=`，排查"这件为什么不能进"极省事。
4. **`MayStack` + `DV_WITHDRAWING_TAG` 这一招要记住**：任何"取出带独立状态的新物品"的流程，都必须先阻止它并进同 ID 旧堆，否则特性/真伪被合并掉。
5. **不要学的部分**：96 处空 `catch`、`Log.Err` 当普通 info 用（大量"成功"日志走 Err 级）、`DropCache`/`HasDirty` 这类没人调的死 API、`_done/_ran` 不重置导致跨读档只跑一次的自检。这些都与 `AGENTS.md` 硬规范正面冲突。
