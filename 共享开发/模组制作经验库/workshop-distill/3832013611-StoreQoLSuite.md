# 3832013611 · 店铺便捷升级：背包挂钩 · 桌面工具箱 · 罪犯与改造人识别（Store QoL Upgrade Suite）—— 四设施常驻补丁 蒸馏档案
> 源码：`缓存文件夹\decomp_ws\3832013611\`（108 个 `.cs`，11,927 行；`manifest.ModVersion=1.7.5`，`<Capabilities />` 自闭合，零能力，6.9 万订阅）
> **为什么值得读**：① 工坊里少见的"**多设施 = 玩家可三态开关**"样本，且开关**完全不碰 Harmony 摘挂**（全库 0 处 `Unpatch`）；② 给齐了"容器型设施不掉件"的三件套——**撤离或拒绝 / 保留筐 / 归位未完成就封存档**；③ 71 条补丁把 `SpriteDict.GetSprite`、`GameItem.IsTag`、`AppraisalHelper` 三个红线全绕干净，是"冲突面最小"的排布示范；④ 11 个 `.rgba` 内嵌资源做到零能力美术，可直接照抄。
> 读码方式：先读 `_reports\3832013611.txt`（暴露 1 个 `IMod` 入口 + 61 个特性补丁 + **0 处 HotReload**），再按行数侦察（`BackpackSlotModEntry.cs` 单文件 5,179 行＝全模组 43%），然后定点读 24 个文件：`FacilityModes`/`FacilityMode`、`Entry` 的 12 个方法段、`WildeServices`、`Patch_HungBackpack_Unload`、9 个 `Cabinet*`、`Patch_EmporiumEntry_TryAddToPlayerInv`、`DragHighlightSafety`、`ArtRuntime`/`AssetCatalog`、`CyborgAlarm`、`Patch_StoreUIManager_*`、`Patch_RenderHandler_LoadFromAtlasRaw`、`Patch_PlayerStore_FindAllItem`、`ReturnAutoUnload`。**未通读 1.2 万行**——`Entry.cs:1874-2450`（挂钩/背包绘制）与 `:3039-3576`（鉴别仪 12 张精灵）那约 1,100 行是纯软件光栅器，只看了签名与入口。
> 标注：无标注＝源码可证（带行号）；「（推测）」＝读不到。代码块按反编译原文照抄，只删空行与 `//IL_xxxx`，行号取 `ops_inspect_local_file` 口径；`// ……（略）` 为我删掉的行。

## 1. 它属于哪种组织方式：**第四种**，不是前三种的变体
先说结论：**它是"常驻全量补丁 + 门卫早退"（gated resident patches）**——`14` 号文档 §八 的三种它一种都不是，因为它连"表"都没有。

| | (a) 静态委托表 | (b) 自描述 Patch 注册表 | (c) 特性化补丁对象表 | **(d) 本模组：常驻全量 + 门卫早退** |
| --- | --- | --- | --- | --- |
| 代表 | DangpuPlus `FeaturePack` 103 项 | Probably Extend Framework `.Patch(...)` + `Report()` | Pen's QoL `FeaturePatch` 表 | **3832013611 四设施** |
| 挂载方式 | 表驱动 `Init()` | 表驱动 `Patch(target)` | 表驱动 `Patch`/`Unpatch` | **加载器扫特性，71 个类各挂各的** |
| 开关落在哪 | 表项的执行期 | 表项 `Applied` | 表项 `Applied` + 方法级摘除 | **一个 `FacilityMode` 三态枚举 + `NetworkUpgrade` 资格** |
| 关掉一个功能 | 跳过委托 | 需要 Unpatch | 方法级 Unpatch | **补丁还在，方法体第一行 `if (!IsXxxEnabled) return;`** |
| 补丁与开关的耦合 | 强 | 强 | 强 | **零耦合**（开关不进补丁对象，补丁不知道开关存在） |

四条硬证据（全库 grep）：

1. `Unpatch` **0 处**、`Harmony.Patch(` **0 处**、`PatchAll(` **0 处** → 挂载全靠 `[HarmonyPatch]` 特性由加载器扫描，且**永不摘除**。71 个文件带 `[HarmonyPatch]`（`BackpackSlotMod` 66 + `BackpackDoorArtTestMod` 5；透视报告能解出实参的是 61 处）。
2. 唯一的"注册表"是 `FacilityModes.cs:10-18` 的**四组平行字符串数组**（Id / Key / LegacyKey / ConfigEntry），不是补丁表：`Ids`/`Keys` 长度硬编码为 4（`FacilityModes.cs:10,12`）。
3. 开关的语义不是"装不装补丁"，而是"这个设施**存不存在**"：它控制建不建窗口/物品/SpriteRenderer，以及补丁体要不要早退。`ApplyFeatureToggles()`（`Entry.cs:1206-1299`）是唯一的总开关函数，被打成**幂等重建**（可反复调用）。
4. 它甚至不是"一个设施一条补丁"：71 个补丁类里大量是**四设施共用**的（拖拽 7 条、高亮 5 条、进出店 19 条），只判 `IsModEnabled`，不判具体设施。所以"设施"是**产品概念**，不是**代码边界**——这正是它 Copy 不到我们代码里的原因（见 §6）。

**一句话**：前三种都在回答"怎么把补丁编成表"，它回答的是"补丁全都挂着，怎么让不该跑的那部分一进门就掉头"。

## 2. 规模与结构
| 目录 | `.cs` | 行数 | 职责 |
| --- | --- | --- | --- |
| `BackpackSlotMod\` | 96 | **10,711** | `IMod` 入口、四设施实现、全部补丁、像素光栅器、存档快照 |
| `BackpackDoorArtTestMod\` | 11 | **1,204** | 门/墙美术运行时（内嵌 RGBA → Sprite）、门的命中过滤、门上的黄色轮廓高亮、门吸附拖放 |
| `Properties\` + `.csproj` | 2 | 61 | `AssemblyInfo` + 11 个 `EmbeddedResource`（`DoorArt.*.rgba`） |
| **合计** | **108 文件**（含 11 个 `.rgba` 资源、1 个 `.csproj`） | **11,976** | 报告口径 11,927（差 49＝csproj） |

行数分布极不均：`Entry.cs` 5,179 行＝全模组 43%，**其中约 1,100 行是手写像素绘制**——`:1874-2450` 的挂钩/背包（`DrawWallHookBase`、`DrawWallHookProng`、`AddBagWallShadow`、`DrawDiagonalStrap`，共 576 行）＋ `:3039-3576` 的鉴别仪 12 张精灵（537 行）。第二梯队 `RackDiagnostics`(440)、`HookDragSafety`(382)、`CabinetArt`(347)、`WildeServiceIconArt`(249)。**没有一层抽象，全是静态类 + 静态字段。**

### 2.1 每个设施占多少行（能测的测，测不到的标估算）
| 设施 | 文件簇 | 文件数 | 行数 | 备注 |
| --- | --- | --- | --- | --- |
| **背包收纳柜**（第 4 个设施，简介里没提但代码里有） | `Cabinet*` | 15 | **1,023** | 唯一有**真子类**（`CabinetSlot : GameSlotInventory`）的设施 |
| **背包挂钩** | `Rack*` `Hook*` `Return*` `ScavengingAutoUnloadMarker` | 16 | **1,734** | `Rack*` 一族全是存档快照/恢复/重置（`RackNode`/`RackSnapshotRules`/`RackReset*`） |
| **桌面工具箱** | `CounterToolbox*` + 3 个 `Patch_ToolboxHelper_*` | 4 | **253** | 四设施里最小，因为它**复用原版 `toolbox` 物品**（§3.3） |
| **NPC 鉴别仪** | `WildeService*`(5) + `CyborgAlarm` | 6 | **632** | 含 `WildeServiceIconArt`(249) 手绘商店图标 |
| 拖拽/高亮 + 进出店时序 + 存档边界（共用） | `*Highlight*` `DragHighlightSafety` `Patch_EmporiumEntry_*` `Outing*` `Rack*Boundary` | 32 | **1,157** | 四设施共用，只判 `IsModEnabled`；存档边界挂 `PlayerStore.SaveGame/LoadGame/StartNewGame` |
| **入口 + 设施开关 + 内联实现** | `BackpackSlotModEntry` + `FacilityMode*` | 4 | **5,374** | 四设施的实现**全部内联在这一个文件里** |

**"每个设施一组文件"边界清晰吗？——不清。** 只有柜子做到了"文件簇＝设施"（`CabinetSlot` 子类 + 7 个 `Cabinet*` 补丁 + 美术/高亮/拖拽），其余三个设施的实现全在 `Entry.cs` 里按方法前缀散落（`EnsureBackpackRackCreated`/`EnsureCounterToolboxCreated`/`EnsureDetectorPanelCreated` 三个 §3 里的"建"函数是唯一可辨认的边界）。**想照抄"设施"这个概念可以，想照抄目录结构不行。**

## 3. 它怎么做这件事
### 3.1 设施开关机制（本档案第一问的答案）
四设施 × 三态，枚举只有 3 个值：`internal enum FacilityMode { Disabled, Wilde, Included }`（`FacilityMode.cs:3-8`）。

| # | Id | Key | Legacy Key | 王尔德价 | 默认 |
| --- | --- | --- | --- | --- | --- |
| 0 | `BSM_STORE_BACKPACK_HOOK` | `backpack_hook_mode` | `enable_backpack_hook` | 200 | `Included` |
| 1 | `BSM_STORE_COUNTER_TOOLBOX` | `counter_toolbox_mode` | `enable_counter_toolbox` | 350 | `Included` |
| 2 | `BSM_STORE_NPC_DETECTOR` | `npc_detector_mode` | `enable_npc_detector` | 800 | `Included` |
| 3 | `BSM_STORE_BACKPACK_CABINET` | `backpack_cabinet_mode` | `enable_backpack_cabinet` | 600 | `Included` |

（出处 `FacilityModes.cs:10-16`；价格 `WildeServices.cs:16-22`；默认值 `FacilityModes.cs:121` 的 `Values[2]`＝`included`）

**三层 AND 才是"能不能用"**（`Entry.cs:251-261`，其余三个同构）：

```csharp
public static bool IsBackpackHookEnabled {
    get {
        if (IsModEnabled && IsBackpackHookRequested)          // ① 模组启用 ② 模式≠Disabled
            return WildeServices.CanDeploy("BSM_STORE_BACKPACK_HOOK");  // ③ 资格
        return false;
    }
}
```

第 ③ 层 `WildeServices.CanDeploy`（`WildeServices.cs:50-61`）：`Included` → 直接 true；`Wilde` → `ModHelper.IsNetworkUpgradeUnlocked(id)`。**`Wilde` 的解锁走原版 `NetworkUpgrade`**（`WildeServices.cs:137-168` 注册成 4 项 `NetworkUpgrade`，价 200/350/600/800，`hidden = !IsOffered(id)`），`Included` 则用 `NetworkUpgrade.MarkUnlockedSilently(id)` 白给（`:90-104`）。

**关掉之后补丁还挂着吗？——挂着，且是早退，不是 Unpatch。** 唯一"真的消失"的是设施实体，走"撤离或拒绝"（`Entry.cs:978-1070`，见 §4.4）：

| 设施 | 关闭时的动作 | 失败时 |
| --- | --- | --- |
| 柜 | `EvacuateCabinet()`（`Entry.cs:669`） | 拒绝，窗口留着并提示 |
| 挂钩 | `EvacuateAndHideBackpackHook()`（`:978`）：包先 `TryAddToPlayerInv`，再把 rack window `SetActive(false)` | `Refuse()` 提示"背包尚未安全移回仓库，挂钩会暂时保留" |
| 工具箱 | `EvacuateAndHideCounterToolbox()`（`:1072`）：先 BFS 枚举箱内工具→移回仓库 | 同上（`:1201`） |
| 鉴别仪 | `SetDetectorPanelActive(false)`（`:2758`） | 无（无物品） |

`ApplyFeatureToggles` 的三处早退很关键（`:1208-1235`）：无 `HasLiveStoreSession` 直接 return；`_loadInProgress` 时**只把四个窗口全部关掉**不重建。配置改动、购买回调、读档、进店都调它（`:852`、`:956`、`:974`、`:4479`、`:4556`、`FacilityModes.cs:132`、`WildeServices.cs:167`、`Patch_EmporiumEntry_{Start,OnArriveStore,InitResponseWindow}`:12）。

### 3.2 背包挂钩
**对象图（三段容器，全是原版类）**：

```
BackpackRackWindow : PixelWindow          ← 挂在墙上的透明窗（无边框、无标题）
└ BackpackHookOuterSlot : GameSlotInventory(1格)   identifier="backpackHookOuterSlot"
  └ BackpackHookItem : GameItemElement   sprite=`bsm_wall_hook_*`  atlas=`Items/items_furniture`
    └ contentWindow = BackpackHookContentWindow : PixelWindow(true,"背包",WindowStyle1)
      └ MachineryPanel("backpack") → BackpackHookInnerSlot : GameSlotInventory(3,3)  identifier="backpackRackInv"
        └ childItem = 被挂上去的那个背包（唯一）
```
（`Entry.cs:1498-1568`；`BackpackHookItem` 打 4 个 tag：`IS_OWNED_TAG`/`BACKPACK_WALL_HOOK_TAG`/`IMPORTANT_TAG`/`IS_TOOLTIP_DISABLED_TAG`，并 `mayInventoryRemoveItemFunc = (…) => false` 让它拔不下来，`:1548-1554`）

- **"挂在墙上"的坐标怎么定的？——不是锚点、不是相对门，是 960×540 设计空间的硬坐标。** `PositionBackpackRack`（`:2638-2702`）把 `windowUICanvasObject` 的 `sizeDelta` 当成缩放基准，先算设计像素再乘比例：
  ```csharp
  float num  = (IsDoorHookDisplay ? ArtRuntime.CenterX() : 128f) - _currentHookPixelWidth * 0.5f;
  float num2 = (IsDoorHookDisplay ? ArtRuntime.TopY()    : 355f);
  float num3 = Mathf.Round(x * (num / 960f));
  float num4 = 0f - Mathf.Round(y * (num2 / 540f));   // 注意 Y 取负
  ```
  （`:2668-2671`）。**旧位置**＝`(128, 355)` 死值（简介说的"排水管上方"）；**新位置**＝`ArtRuntime.CenterX()`/`TopY()`，而这两个也是常量算式：`CenterX = Spec.CropX + Spec.Width*0.5`、`TopY = 352 + Spec.CropY`（`ArtRuntime.cs:41-49`），`CropX/CropY` 是**预先手工量好的**、写在 `AssetCatalog.cs:5-15` 的 8 条 `ArtSpec` 里（每条带 `CropX, CropY`，如 `backpack_large` = `64,128,9,16`）。所以"挂在墙上"的真相是：**美术作者对着店面截图量了 8 个裁剪偏移，写进表里当锚点**。没有 `Transform` 查询、没有门坐标。

- **怎么认出"装的是哪个背包"？——`identifier` 字符串 switch，9 个原版 id → 7 套图**（`:1380-1399`）：

  | `backpack.identifier` | 挂钩 sprite | 实际美术 | 像素尺寸 |
  | --- | --- | --- | --- |
  | `fanny_pack` | `bsm_wall_hook_fanny_pack` | `fanny_pack.rgba` 48×80 | 48×48 |
  | `satchel` | `bsm_wall_hook_satchel` | `satchel.rgba` 48×96 | 48×64 |
  | `backpack_small` | `bsm_wall_hook_backpack_small` | `backpack_small.rgba` 48×96 | 48×80 |
  | `backpack_medium` | `bsm_wall_hook_backpack_medium` | `backpack_medium.rgba` 64×112 | 64×96 |
  | `backpack_large` | `bsm_wall_hook_backpack_large` | `backpack_large.rgba` 64×128 | 64×112 |
  | `kids_backpack` | `bsm_wall_hook_kids_backpack` | `child1.rgba` 48×80 | 48×64 |
  | `kids_backpack_large` | `bsm_wall_hook_kids_backpack_large` | `child2.rgba` 48×96 | 48×80 |
  | **2 个 `_military` 变体 / 其它（模组背包）** | 同 `_medium` / `_large` | 复用中/大背包图 | 64×96 / 64×112 |

  **不看 tag、不看 shape、不看 `unitCount`。** 判"是不是背包"是另一套：`IsBackpackItem`（`:1301-1312`）＝`item.IsTag("BACKPACK_TAG") || item.IsTag("backpack") || item.IsGameItemType("BACKPACK_TAG")`——**三条件宽松或**，所以任何带 `backpack` 小写 tag 的第三方背包都能上钩（这是兼容性设计，也是脏 tag 的来源）。sprite 的**像素尺寸还要再反推占格**：`SetShape` 按 `width/16 × height/16` 铺满（`:1607-1622`），所以 64×112 的图占 4×7 格。

- **它是容器吗？——是容器，而且是三层套娃的真容器**，不是快捷入口。挂上去的包真的坐在 `BackpackHookInnerSlot` 里；丢进挂钩窗的**非背包物品**由 `slotSearchPassthroughForItemFunc = ShouldPassthroughToHungBackpack`（`:1532`、`:1472-1479`）**透传进被挂背包自己的 contentWindow**，所以"往里丢东西"落点是包本身，不是挂钩。`mayInventoryAddItemFunc = CanBackpackHookSlotAcceptItem`（`:1454-1470`）裁决：挂钩空 → 只收背包；挂钩有包 → 收包愿意收的东西、且拒绝第二个背包。

- **"点开、拖走"**：双击走原版 `ItemMouseDoubleClickHandler`，但挂钩本体被**拦掉**（`Patch_ItemMouseDoubleClickHandler_DoubleClickAction.cs:10` 对 `IsBackpackHookItem` 返回 false），真正打开的是包；`ForceSingleClickInteract` 让"单击即开"（`Patch_EmporiumOption_SingleClickInteract.cs:8-14` + `OnEventRelease` 成对置位/复位）。**"拖走"是重定向**：拖挂钩时 `StartDrag` Prefix 把被拖对象 `___currentItem` 换成包里那个包（`Patch_ItemMouseDragHandler_StartDrag.cs:18-38`），`IsRedirectedHookDrag` 再把拖拽位置重投影回鼠标（`:67-77`）——玩家以为自己拎起了挂钩，其实拎起了包。

- **"右键卸载保留背包"**：见 §4.2。包**不离开挂钩**，只有**包里的小孩**被卸到店铺物品区。

- **"归还背包时填满空挂钩、尊重其他挂钩"**：见 §4.3。真相是**按 uid 归位**，不是贪心填空。

### 3.3 桌面工具箱：原版工具箱的"定点+常驻版"
`EnsureCounterToolboxCreated`（`Entry.cs:2450-2499`）：`DirectoryMaster.Item("toolbox", true)` 取原版工具箱，塞进自家 `CounterToolboxSlotElement`（1 格槽），并给 tag 塞 `TOOLBOX_UNLOCKED_SLOTS_INT = 4`（`:2490-2494`），再 `ConfigureCounterToolboxItem` 补 `COUNTER_TOOLBOX_TAG`/`TOOLBOX_TAG`/`CONTAINER_TAG`/`IMPORTANT_TAG` 且**不可移除**（`:2501-2512`）。升级链是原版的：拖废金属进去 → 4 格升到 9 格（`TOOLBOX_UNLOCKED_SLOTS_INT` 由原版工具箱自己管）。外观按槽数切磨损/完好两张内嵌图（`UseWornCounterToolbox` `:2515`、`always_show_worn_toolbox` 配置 `:934`）。

**它给的新功能只有一个：让"桌面上的工具箱"参与快捷键与拾荒被动。** 因为原版 `ToolboxHelper.FindToolboxInReach` 只认"够得着的工具箱"，`Patch_ToolboxHelper_FindToolboxInReach`（122 行，`Prefix`+`Postfix` 双挂）把"桌面上那个"也认成够得着，并且**优先让别的工具箱赢**（先扫 `TOOLBOX_TAG` 的其他箱子、别的可见窗口、`GetAllOwnedItems`，最后才轮到自家，`:24-117`）。`TryToggleEquipSlot` 用 Prefix/Finalizer 记"当前按的是第几格"再清回 -1（`Patch_ToolboxHelper_TryToggleEquipSlot.cs:8-16`）。

### 3.4 罪犯与改造人识别（NPC 鉴别仪）
**画在哪**：`GameObject("CustomerDetectorPanel")` + `SpriteRenderer`，**父级挂到 `StoreUIManager.Instance.wallRenderer.transform`**，`layer` 抄墙、`sharedMaterial` 抄墙（`Entry.cs:2807-2811`）——所以它是**贴墙上的一块牌子**，跟着墙体一起被相机/材质处理。精灵是**运行时画出来的 4×3 共 12 张**（`CreateDetectorSprite` `:3039`，按墙的 `pixelsPerUnit` 生成，`:2785-2800`）。**不是 tooltip、不是角标。**

**怎么识别**（`EvaluateCustomerState` `:2939-2965`）：右键灯＝伪人/人，左键灯＝罪犯状态。

| 读的东西 | 判什么 | 出处 |
| --- | --- | --- |
| `StoreClient.isAug` | `true` → 右灯 2（改造人/伪人）；否则 1（普通人） | `:2951` |
| `StoreClient.isWanted` + `wantedPosterId`/`identifier` ∈ `StoreClientListWanted.POSTER_IDS` | 左灯 3＝**上墙通缉犯** | `:2967-2983` |
| `StoreClient.CanReport()` 且（`isWanted` ∨ `IsSellingContraband()` ∨ `IsSellingStolen()` ∨ `onCaptureAction != null`），或兜底递归扫 `GetAllNonOwnedItem()` 找违禁/赃物 tag（`HasContrabandOrStolenRecursive` `:3013`） | 左灯 2＝**可举报的非通缉犯** | `:2985-3009` |
| `reported` == true | **全部熄灭**（已举报过就不再提示） | `:2949` |

前置守卫（`:2944`）：`!IsClientPresent() || !isClientArrived || isClientBeingArrested || StoreUIManager.isShot` 一律灭灯。报警音 `CyborgAlarm.Observe`（`CyborgAlarm.cs:18-46`）**直接播原版** `EmporiumAudioPlayer.Instance.alarm2`，同一客户只响一次（`_client != client → _alerted = false`），默认关（配置 `enable_cyborg_alarm_sound` `:935`）。刷新入口是 `Patch_StoreUIManager_Update` 的 **Postfix**（`Patch_StoreUIManager_Update.cs:8-14`，每帧，但第一行判 `IsNpcDetectorEnabled`）。

**红线体检**：`AppraisalHelper.*` 全库 **0 处**；`GameItem.IsTag` **被补丁 0 次**（只在 11 处被调用，见 §5）；`SpriteDict.GetSprite` **0 处**；`StoreClientListDict.CreateStoreClient` **0 处**。真正读的字段全是 `StoreClient` 的公开 bool/string，**没有一处读显示名或 `fakeCondition`**。

### 3.5 Patch 靶点表（71 类，按靶点归并；标注是否热点）
| 原版靶点 | 类型 | 目的 | 风险 | 热点？ |
| --- | --- | --- | --- | --- |
| `PlayerStore.SaveGame` | Prefix | `CanSaveRack()` 为 false 时**拒绝写盘** | 高：拦错＝玩家进度丢 | ⚠ 高 |
| `PlayerStore.LoadGame` / `StartNewGame` | Prefix/Postfix | 建设施、读私有快照、新局初始化 | 高：时序错了读档就串 | ⚠ 高 |
| `ModHook.FireOnGameLoadedNormal` / `FireOnGameLoadedLate` | Postfix | 两个读档时机点（原版早/晚） | 低 | 低 |
| `EmporiumEntry.TryAddToPlayerInv` | **Prefix(800)** | 返店归位（柜→挂钩→放行） | 高：返回值一拦错就少一件 | 中 |
| `EmporiumEntry.TransferBackpackToInv` | Prefix + Finalizer | 记"哪个包出去了"（uid/柜格） | 中 | 中 |
| `GameItem.TryUnloadAll(bool)` | Prefix + Finalizer | 右键把包内物品卸到店库存 | **高：GameItem 全类型都进来** | ⚠ 高 |
| `GameItem.MayHaveValidInventorySlot` / `TryFindOneValidInventorySlot` | Prefix | 柜门/柜格的接收裁决 | 中：GameItem 全局 | 中 |
| `GameSlotInventory.UncheckedAccept` / `GameGridInventory.UncheckedAccept` | Prefix | 绕过容量校验时的兜底 | 高 | 中 |
| `PlayerStore.FindAllItem` | Postfix | 把设施内物品并入"全店物品"索引 | 高：调用极频繁 | ⚠ 高 |
| `StoreUIManager.Update` | Postfix | 每帧刷新鉴别仪 | 每帧；靠门卫早退 | ⚠ **热点** |
| `StoreUIManager.IsInnateWindow` | Postfix | 把自家 3 个窗口登记为"店内原生窗" | 低 | 中 |
| `RenderHandler.LoadFromAtlasRaw` | Prefix | 按 sprite 名拦下，返回自绘/内嵌图 | 名字 switch，全游戏 sprite 加载都过 | ⚠ **热点** |
| `CustomUIManager.GetSprite(…)` | Prefix | 王尔德商店图标 | 低 | 中 |
| `ItemMouseDragHandler.{StartDrag,EndDrag,AbortDrag,OnEventReset,OnEventUpdate,Drag,OnEventRelease}` | 7 条 Prefix/Postfix | 拖拽重定向 + 悬空引用清理 | **高：拖拽是全游戏核心路径** | ⚠ 高 |
| `ItemMouseDragHandler.ClearMayHighlights` | **Transpiler** | 把 `Behaviour.set_enabled` 换成空安全版 | 唯一 IL 补丁，带"找不到就原样返回" | ⚠ 高 |
| `GameSlotInventory.HighlightSlot` / `ClearHighlight`、`HighlightManager.Clear` | Postfix/Prefix | 黄色像素轮廓（门/柜/挂钩） | 中 | 中 |
| `GameItemElement.ValidateSelect` / `ToggleBackground` / `ToggleFakeBackground` | Prefix/Postfix | 高亮时重刷子图颜色、隐藏代理美术 | 中 | 中 |
| `ToolboxHelper.FindToolboxInReach` / `TryToggleEquipSlot` / `UpdateToolboxSprite` | Prefix/Postfix | 桌面工具箱参与快捷键与外观 | 中 | 中 |
| `ScavHelper.GetRandomScavengedItem` / `GetMinorWoundChance` / `GetMajorWoundChance` | Postfix + Prefix/Finalizer | 标记"刚拾荒过" + 查询深度计数 | 低 | 中 |
| `NetworkUpgrade.IsReady`、`MapUIManager.OnReturning`、`DrJacksonLocation.Leave`、`ModConfigWindow.BuildEntry` | Prefix/Postfix | 商店可见性、回店/就医失败兜底提示、三态下拉框渲染 | 低 | 低 |

**没碰的**：`SpriteDict.GetSprite`、`StoreClientListDict.CreateStoreClient`、`GameItem.IsTag`、`AppraisalHelper.*`、`StoreUIManager` 里除了 `Update`/`IsInnateWindow` 的其他方法。

## 4. 关键代码摘录
### 4.1 挂钩"挂上去"：三段容器装配
```csharp
// BackpackSlotMod/BackpackSlotModEntry.cs:1527-1568（略去窗口动画与 catch）
BackpackHookContentWindow = new PixelWindow(true, text, (WindowStyle)1);      // text="背包"
BackpackHookInnerSlot = new GameSlotInventory(3, 3, true, false);
((GameInventory)BackpackHookInnerSlot).identifier = "backpackRackInv";
BackpackHookInnerSlot.SetBackgroundFadeSprite("Items/items_backpack2", "simple_backpack_medium", 0.5f, 0.5f);
((GameInventoryFunc)BackpackHookInnerSlot).mayInventoryAddItemFunc = CanBackpackHookSlotAcceptItem;
((GameInventoryFunc)BackpackHookInnerSlot).slotSearchPassthroughForItemFunc = ShouldPassthroughToHungBackpack;
((GameInventoryFunc)BackpackHookInnerSlot).slotPriorityFunc =
    (GameItem item, GameInventory inv, SlotMarker slot) => (!IsBackpackHookEnabled || !IsBackpackItem(item)) ? -1 : 4;
((GameInventoryFunc)BackpackHookInnerSlot).onSlotAddItemEndFunc    += OnBackpackHookAddItemEnd;
((GameInventoryFunc)BackpackHookInnerSlot).onSlotRemoveItemEndFunc += OnBackpackHookRemoveItemEnd;
BackpackHookPanel = MachineHelper.Panel(text, (PixelElement)(object)BackpackHookInnerSlot, "backpack", true);
BackpackHookContentWindow.Attach((PixelElement)(object)BackpackHookPanel);
BackpackHookContentWindow.Validate();  BackpackHookContentWindow.Hide();   // 面板已建好，等包上来
// ……（略）
BackpackHookItem = new GameItemElement("backpack_wall_hook", "Items/items_furniture", "bsm_wall_hook_empty", (PixelWindow)null);
((GameItem)BackpackHookItem).name = name;
((GameItem)BackpackHookItem).SetContentWindow(BackpackHookContentWindow);
ContainerHelper.InitContainerItemTagOnly((GameItem)(object)BackpackHookItem);
GeneralHelper.SetItemOwned((GameItem)(object)BackpackHookItem, true);
((GameItem)BackpackHookItem).EnableTag("BACKPACK_WALL_HOOK_TAG", false);
((GameItemFunc)BackpackHookItem).mayInventoryRemoveItemFunc = (GameItem item, GameInventory inv) => false;  // 拔不下来
((GameInventory)BackpackHookOuterSlot).UncheckedAccept((GameItem)(object)BackpackHookItem);
UpdateBackpackHookVisual();
```

### 4.2 右键卸载：物品归属怎么保证不掉地上
**接管点只有一处，而且不重写卸载逻辑——它挂原版自己的回调 `onUnloadChildItemFunc`。**

```csharp
// BackpackSlotMod/Patch_HungBackpack_Unload.cs:6,16-58
[HarmonyPatch(typeof(GameItem), "TryUnloadAll", new Type[] { typeof(bool) })]
internal static class Patch_HungBackpack_Unload {
    internal static bool Prefix(GameItem __instance, [HarmonyArgument(0)] bool skipPinned,
                                ref GameItem __result, out UnloadState __state) {
        __state = null;
        bool flag = (object)__instance == BackpackSlotModEntry.BackpackHookItem;   // 点的是挂钩本体
        GameItem val = (__instance.parentInventory is CabinetSlot)                  // 点的是柜格里的包
            ? __instance
            : BackpackSlotModEntry.BackpackHookInnerSlot?.childItem;
        if (!flag && (val == null || __instance != val)) return true;               // 与我无关，走原版
        __result = __instance;                                                      // 声称"已卸载"
        if (!BackpackSlotModEntry.CanUnloadHungBackpack(val)) return false;         // 前置不满足 → 什么都不做
        if (flag) {                                                                 // 点挂钩 → 卸包里的东西
            if (!__instance.IsTag("IS_UNLOAD_DISABLED_TAG")) val.TryUnloadAll(skipPinned);
            return false;
        }
        if (val.onUnloadChildItemFunc == null) {                                     // 点包 → 借原版回调改落点
            __state = new UnloadState { Bag = val, Callback = BackpackSlotModEntry.UnloadHungChildToStore };
            val.onUnloadChildItemFunc = __state.Callback;
        }
        return true;                                                                // 让原版正常跑，落点已被换掉
    }
    // Finalizer（:60-67）：无论成败都把自己挂的 onUnloadChildItemFunc 与 __state.Callback 比对后摘掉
}
```

```csharp
// BackpackSlotMod/BackpackSlotModEntry.cs:3868-3891 —— 落点＋落点校验
internal static void UnloadHungChildToStore(GameItem bag, GameItem child) {
    if (!CanUnloadHungBackpack(bag)) return;
    try { GraphUtils.TryAcceptAll((GraphNodeStorage)EmporiumEntry.Instance.invElement, child, -1); }
    catch (Exception ex) {
        RackDiagnostics.Error("hook_unload_transfer", ex);
        if (child != null && child.unitCount > 0
            && (child.parentInventory?.childItems == null || !child.parentInventory.childItems.Contains(child)))
            RetainUnplacedRackItem(child, "hook unload: transfer left an unplaced item");  // 落到"保留区"，不丢
        throw;                                                                     // 对内暴露，不吞
    }
}
```

**归属三原则**：① 目标永远是 `EmporiumEntry.Instance.invElement`（店铺物品区），不是地面；② 用 `GraphUtils.TryAcceptAll` 而不是自己塞 `childItems`；③ 搬完**用 `parentInventory.childItems.Contains(child)` 复核**，没落地就进 `RetainUnplacedRackItem`（`:4620`）——一个"捡不回来的东西都先进保留筐"的兜底。`CanUnloadHungBackpack`（`:3853-3866`）把十来个前置条件串成一行：不能读档中、不能有未归位件、不能在外面、不能正在拖、必须真在包里。

### 4.3 "多挂钩分配"：按 uid 归位 + 让路给别的挂钩模组
**先记出口，再记原格，回来原格优先。**

```csharp
// BackpackSlotMod/CabinetDeparture.cs:16-40 —— 包离店那一刻才记"它原来在第几格"
private static void Prefix(EmporiumEntry __instance, out State __state) {
    GameItem selectedBackpack = __instance.selectedBackpack;
    __state = (BackpackSlotModEntry.IsModEnabled && selectedBackpack?.parentInventory is CabinetSlot slot)
        ? new State { Item = selectedBackpack, Slot = slot } : null;
}
private static void Finalizer(EmporiumEntry __instance, State __state, Exception __exception) {
    if (__state == null) return;
    GameSlotInventory pocket = __instance.afterhourPocketSlotInvBackpack;
    // 三重校验：包真进了外出袋（引用相等）、袋里真装着它、原格真空了
    if (pocket?.childItem == __state.Item && __state.Item.parentInventory == pocket && __state.Slot.childItem == null) {
        BackpackSlotModEntry.CabinetAwayUid  = __state.Item.GetUniqueID();
        BackpackSlotModEntry.CabinetAwaySlot = __state.Slot.Index;
        BackpackSlotModEntry.CabinetAwayItem = __state.Item;
        BackpackSlotModEntry.CabinetContentsChanged();
    }
}
```

```csharp
// BackpackSlotMod/BackpackSlotModEntry.cs:778-828（略去诊断日志；instance＝提前取出的 EmporiumEntry.Instance）
internal static bool TryReturnCabinet(GameItem bag, out bool claimed) {
    claimed = IsCabinetOutgoing(bag);
    if (!claimed || IsBackpackClaimedByOtherHookMod(bag)) return false;   // 不是我的包／别人宣称了 → 放行
    if (IsCabinetEnabled && CabinetSlots != null && CabinetAwaySlot >= 0 && CabinetAwaySlot < 8) {
        CabinetSlot slot = CabinetSlots[CabinetAwaySlot];
        if (slot.childItem == null) {                                     // ① 原格空着 → 回原格
            GraphUtils.TryAccept((GraphNodeStorage)(object)slot, bag);
            if ((object)bag.parentInventory == slot && slot.childItem == bag) {
                ClearCabinetAway(); RestoreItemVisualState(bag); CabinetContentsChanged(); return true;
            }
        }
    }
    GameInventory[] array = { instance?.backInvinvElement, instance?.invElement, instance?.showcaseElement };
    foreach (GameInventory val in array) {                                // ② 后仓 → 物品区 → 展台
        if (val == null) continue;
        GraphUtils.TryAccept((GraphNodeStorage)(object)val, bag);
        if (bag.parentInventory == val && val.childItems.Contains(bag)) {
            ClearCabinetAway(); RestoreItemVisualState(bag); CabinetContentsChanged(); return true;
        }
    }
    if (bag.parentInventory == null) RetainUnplacedRackItem(bag, "cabinet return: no safe destination");
    RackFeedback.ShowMessage("背包暂时无法归位…归位完成前不会覆盖存档。", "…saving stays blocked until the return completes.");
    return true;                                                          // ③ 都不行 → 留在原处 + 封存档
}
```

**"尊重其他挂钩"是读别人的 modData 让路**（`:3670-3705`），三个来源：

| 来源 | 键 / tag | 语义 |
| --- | --- | --- |
| 我们自己的 tag | `OUTING_CARRIED_FROM_WALL_HOOK_TAG` | 有别的挂钩模组已宣称这个包 |
| 模组 id `WallHook` | modData `mount.{0..3}.awayUid` | 它把包带出去了（4 个挂位）。**⚠️ 这个 modId 在 948 项工坊快照里查无对应物品**（可能已下架/改名）；能确认的只有本模组字符串里 `WallHook` / `mount.` / `awayUid` 三者同时存在 |
| 模组 id `doremi_wallhook` | modData `return_state` = `v1\|<uid>\|…` | 同上，字段化。**已核实**：它就是工坊 `3829360387`「挂钩 / WallHook」——那份包的 DLL 里只有 `return_state`、0 处 `mount` |
| 本模组自己 | modData `backpack_wall_slot_mod` / `outgoing_hook_uid` | 我们挂钩带出去的那个包的 uid（`:3604-3653`） |

命中任一 → 直接 `return true` 交还给原版/对方模组，**自己一个都不碰**。

**挂钩只有一个格子，所以"分配"退化成"空着就接"**：

```csharp
// BackpackSlotMod/Patch_EmporiumEntry_TryAddToPlayerInv.cs:8-41（Priority 800，抢在别人前面）
if (IsModEnabled && IsTransferringAfterhourToInv && gameItem != null) {
    if (TryReturnCabinet(gameItem, out var claimed)) return false;   // 柜子认领了 → 吞掉
    if (claimed) return true;                                        // 认领了但没接住 → 交给原版（避免丢件）
}
if (!IsTransferringAfterhourToInv || !IsBackpackHookEnabled || gameItem == null) return true;
if (IsBackpackItem(gameItem)) {
    if (!IsOutgoingFromOurHook(gameItem) && IsBackpackClaimedByOtherHookMod(gameItem)) return true;  // 别人的 → 走
    EnsureBackpackRackCreated(__instance);
    if (BackpackHookInnerSlot?.childItem == null
        && GraphUtils.TryAccept((GraphNodeStorage)(object)BackpackHookInnerSlot, gameItem) != -1) {
        ClearOutgoingHookBackpack(); return false;                   // 空钩 → 挂上，吞掉
    }
}
return true;
```

**所以"填满空挂钩"是逐包归位的结果，不是一次遍历分配**——多背包同批回来时，第一个占掉唯一的挂钩，其余靠原版落回后仓/物品区。

### 4.4 关掉一个设施：撤离或拒绝（不丢件的关键）
```csharp
// BackpackSlotMod/BackpackSlotModEntry.cs:989-1046（略去日志与诊断）
if (val != null) {                                                    // 挂钩上有包
    if (!BelongsTo(val, (GameInventory)(object)BackpackHookInnerSlot))
        { RetainUnplacedRackItem(val, "hook disable: inconsistent original slot"); return Refuse(); }
    try { EmporiumEntry.Instance.TryAddToPlayerInv(val); }             // 塞回玩家背包/店铺
    catch (Exception ex) { RackDiagnostics.Error("hook_disable_transfer", ex); }
    bool flag = backpackHookInnerSlot2.childItems != null && !backpackHookInnerSlot2.childItems.Contains(val);
    if (!flag || val.parentInventory == backpackHookInnerSlot2 || !BelongsTo(val, val.parentInventory)) {
        if (((val.parentInventory == null) & flag) && BackpackHookInnerSlot.childItem == null)
            try { backpackHookInnerSlot2.UncheckedAccept(val); } catch { /* 记日志 */ }
        if (!BelongsTo(val, backpackHookInnerSlot2))
            RetainUnplacedRackItem(val, "hook disable: transfer/restore incomplete");
        return Refuse();                                              // 移不走 → 设施保留
    }
    RestoreItemVisualState(val); UpdateBackpackHookVisual();
}
if (BackpackHookInnerSlot?.childItem != null) return Refuse();         // 还挂着东西 → 拒绝关闭
((GameItem)BackpackHookItem).CloseContentWindow(false);
((Component)BackpackRackWindow.rectTransform).gameObject.SetActive(false);
```

配套的封存档（`RackSaveBoundary.cs:6-29`）：`PlayerStore.SaveGame` 的 Prefix 直接返回 `CanSaveRack()`，Finalizer 把 `RackSaveBlockedException` 转成玩家提示并**吞掉异常**（这是全库唯一允许吞的地方——存档边界）。

### 4.5 内嵌 RGBA → Sprite（零能力美术的真身）
```csharp
// BackpackDoorArtTestMod/ArtRuntime.cs:98-129（略去 Blit 与 mipmap）
using (Stream stream = typeof(ArtRuntime).Assembly.GetManifestResourceStream(artSpec.ResourceName)) {
    if (stream == null) throw new FileNotFoundException(artSpec.ResourceName);
    using MemoryStream memoryStream = new MemoryStream();
    stream.CopyTo(memoryStream);  array = memoryStream.ToArray();
}
if (array.Length != artSpec.Width * artSpec.Height * 4) throw new InvalidDataException(artSpec.Key);  // 尺寸自检
Color32[] array2 = new Color32[artSpec.Width * artSpec.Height];
for (int j = 0; j < array2.Length; j++)
    array2[j] = new Color32(array[4 * j], array[4 * j + 1], array[4 * j + 2], array[4 * j + 3]);
Texture2D val = new Texture2D(artSpec.Width, artSpec.Height, (TextureFormat)4, true);
((Texture)val).filterMode = (FilterMode)0;  ((Texture)val).wrapMode = (TextureWrapMode)1;   // 点采样
val.SetPixels32(array2);  val.Apply(true, false);
Color32[] pixels = val.GetPixels32(4);
for (int k = 0; k < pixels.Length; k++) pixels[k].a = byte.MaxValue;   // alpha 全拉满（画的是遮罩）
val.SetPixels32(pixels, 4);  val.Apply(false, false);
Sprite.Create(val, new Rect(0f, 0f, artSpec.Width, artSpec.Height), new Vector2(0.5f, 0.5f), 100f, 0u, (SpriteMeshType)0);
```

配套：`.csproj` 里 11 个 `<EmbeddedResource Include="DoorArt.*.rgba" LogicalName="DoorArt.*.rgba" />`（`BackpackSlotMod.csproj:27-48`），所以**一个 DLL 装完所有美术，`<Capabilities />` 可以是空的**。

## 5. 踩坑与红线
| # | 坑 | 证据 | 我们的对策 |
| --- | --- | --- | --- |
| 1 | **游戏里塞了一个软件光栅器**：`Entry.cs:1874-2450` 共 576 行手写像素绘制（钩子、背包阴影、斜背带），`:3039-3576` 另有 537 行画鉴别仪精灵，还 `Resources.Load<Texture2D>("Items/items_backpack2")` + `Graphics.Blit` + `ReadPixels` 把原版图集读回 CPU 再合成 | `:1888-1912`、`:1998`、`:2074`、`:2134`、`:2195` | **别抄**。我们走 PNG + `EmbeddedResource`/`Textures\`，美术归美术。`ReadPixels` 那条路在 URP/后处理下会拿到错误内容（推测），且每换一次分辨率就要重读 |
| 2 | **5,179 行单文件 + 四设施全内联**，唯一"设施边界"是三个 `Ensure*Created` 函数 | §2.1 | 我们每个设施一个文件、一个 `IFacility`（`Id/Title/Price/Build/Teardown`），**开关只认 `Id`**，不复制它 |
| 3 | **`GameItem.TryUnloadAll` 是全类型入口**，Prefix 里做的事绕了四层 null 判断才 `return true`；判断一错就是"右键卸不了包" | `Patch_HungBackpack_Unload.cs:16-58` | 若要做同类，**优先找更窄的靶点**（`EmporiumEntry` 一层），或学它用原版 `onUnloadChildItemFunc` 回调改落点 |
| 4 | **唯一的 Transpiler** 把 `Behaviour.set_enabled` 全局换掉；靠"匹配数 ≠ 1 就原样返回"自保 | `DragHighlightSafety.cs:86-137`（`:103` 打印 `expected one …; original method retained`） | 这个"失败即整条还原"写法**值得抄**；但换来的是"游戏一更新就静默不生效"，必须配一条启动自检日志 |
| 5 | **`GameItem.IsTag` 被调 11 次**（`TOOLBOX_TAG`×3、`IS_UNLOAD_DISABLED_TAG`×2，`BACKPACK_TAG`/`backpack`/`COUNTER_TOOLBOX_TAG`/`BACKPACK_WALL_HOOK_TAG`/`MODULE_STUCK_TAG`/`OUTING_CARRIED_FROM_WALL_HOOK_TAG` 各 1）——**没打补丁，只是在热点方法上反复调用**，且 `IsBackpackItem` 是三条件宽松或 | `Entry.cs:1307-1312`、`:3678` | 我们判背包**只认一条 tag 或 `IsGameItemType`**，别写"或"；`backpack` 小写 tag 这种脏兼容会引来第三方脏数据 |
| 6 | **把存档塞进 `PlayerStore.ModData()` 三个私有键**，并在 `OnDisable` 里**删掉它们** | `:4229-4231`、`:887-889` | 卸载即删＝不可逆。我们只用私有键、**不删**（留着变成死数据也比丢包好）；或按 `09` 号文档走 `ES3Type.WriteProperties` 剥离 |
| 7 | **`PlayerStore.Instance` 没先判 `HasInstance`**：`:2929` 用 `?.`、`:2943` 直接 `.Instance` | `:2929`、`:2943` | 我们的硬规矩：`PlayerStore.HasInstance` 先行，不用 `??`/`?.` 兜 Unity 假 null |
| 8 | **每帧 Postfix 挂在 `StoreUIManager.Update` 上**，没有 `HarmonyPriority` | `Patch_StoreUIManager_Update.cs:8` | 我们已有一处 `StoreUIManager.Update` 声明（电玩阶段 2 ），新增必须写 priority + 门卫第一行 + 零 GC |
| 9 | **设施关闭时"拒绝"＝玩家点了开关却没关掉**，只弹一句提示 | `:1067`、`:1201` | 文案上必须说清"暂时保留"，否则就是"开关坏了"的差评 |
| 10 | **靶点不可解码**：`Patch_EmporiumOption_SingleClickInteract` 的 `[HarmonyPatch(...)]` 反编译不出实参（`/*Could not decode attribute arguments.*/`，该文件 `:5`） | 复现"单击即开"要自己重新定位（推测判的是 `EmporiumOption` 的单击判定） | 别照抄，重找靶点 |

**红线体检结论**：`AppraisalHelper.GetCorrectBaseValue` **0 处**；`GameItem.IsTag` **未被 Patch**；`SpriteDict.GetSprite` **0 处**（它用的是隔壁的 `RenderHandler.LoadFromAtlasRaw`）；`StoreClientListDict.CreateStoreClient` **0 处**。真伪/成色判断完全没碰——它压根不判真伪。

## 6. 对我们的可用点
| # | 可用点（源码出处） | 落到我们的哪个功能 | 工作量 |
| --- | --- | --- | --- |
| 1 | **三态设施开关 `Disabled/Wilde/Included` + `NetworkUpgrade` 付费 + `MarkUnlockedSilently` 白给**（`FacilityModes.cs:111-135`、`WildeServices.cs:50-104,137-168`、`WildeServiceReadyPatch.cs:8-16`） | **《深空电玩》**：付费情报（`arc_pga_awards` 那套）与"翻新整备套件"目前是物品/事件驱动，可改成"设置里三选一：始终关 / 在银狼处买 / 开局自带"，价格沿用 `NetworkUpgrade.cost`，资格落存档——直接替掉我们手写的"解锁 flag"。**《群友》**：伞萝萝/塞壬的周常与电话预约（`DspVisitorPhones`）可加"始终关/首次付费/开局解锁"三态，玩家不想见就彻底不出现 | 1 个设施 ≈ 60 行（`FacilityModes` 式数组 + 3 个门卫属性） |
| 2 | **`StoreUIManager.IsInnateWindow` 把自家窗口登记成店内原生窗**（`Patch_StoreUIManager_IsInnateWindow.cs:8-14`） | **《深空电玩》**：鉴定助手浮窗、自检面板、对质窗口目前与原生放大镜共存靠手调层级/`visibleWindows` 过滤；登记为 innate 可让原版 Esc/点击穿透/拖动收尾逻辑正确对待它们（阶段 2 修的"浮窗二次建窗跳锚点"很可能就是这个缺口） | 单条 Postfix，3 行 |
| 3 | **`EmporiumEntry.TryAddToPlayerInv` 的 `Priority(800)` Prefix + "认领但没接住就 `return true`"**（`Patch_EmporiumEntry_TryAddToPlayerInv.cs:8-41`） | **《群友》**：名片/专属道具/清仓箱"带出去再回来"要归位到展示柜或原箱格；**《深空电玩》**：刻录成品、翻新件返店直接回展柜。关键是 `claimed` 双返回值这个防丢件写法 | 20 行 |
| 4 | **`GameSlotInventory` 真子类覆写，而不是 Harmony 补丁**（`CabinetSlot.cs:8-67` 覆写 `MayHaveValidInventorySlot`/`TryFindOneValidInventorySlot`/`UncheckedAccept`） | **《群友》**：4 款清仓箱与专用片盒的"指定格接收"目前靠 `GameItem` 全局补丁；改成 `CrateSlot : GameSlotInventory` 子类可**完全退出一条 `GameItem.MayHaveValidInventorySlot` 补丁**，冲突扫描面立刻变小 | 1 个子类 ≈ 60 行 |
| 5 | **内嵌 RGBA + `Sprite.Create`（零能力美术）**（`ArtRuntime.cs:98-129`、`BackpackSlotMod.csproj:27-48`、`AssetCatalog.cs:5-15` 的 8 条裁剪表） | **两条产品线都能用**：《深空电玩》公开包的 `Textures\` 目录（174 文件）与《群友》14 张物品贴图，若哪天想彻底去掉外部目录依赖可整体内嵌；配套的"`Width*Height*4` 尺寸自检""点采样 `FilterMode.Point`"直接照抄。**反面**：它的 `RenderHandler.LoadFromAtlasRaw` 拦截（`Patch_RenderHandler_LoadFromAtlasRaw.cs:9-27`）虽也能换图且不碰 `SpriteDict.GetSprite`，但同属 sprite 加载咽喉、优先级不可控，**不建议照抄** | 每张图 ≈ 20 行样板，可脚本生成 |
| 6 | **"撤离或拒绝" + 保留筐**（`EvacuateAndHideBackpackHook` `:978-1070`、`RetainUnplacedRackItem` `:4620`、`RackSaveBoundary` `:6-29`） | **热卸载/热重载时的容器物品处置**：《群友》`OnDisable` 已有对称卸载，但容器里的东西没有"移不走就拒绝卸载 + 封存档"这一层；《深空电玩》的收纳盒（6×6）同理 | 引用即可，是我们的"卸载安全检查清单"补强 |
| 7 | **`StoreClient.isAug` / `isWanted` / `CanReport()` 三字段判"伪人/通缉/可举报"**（`Entry.cs:2939-3011`） | **《深空电玩》**：现有"盗版查获/对质"的前置判断可补 `isAug`（伪人客户）与 `CanReport()`，做"该不该举报"的提示气泡；**《群友》**：角色来访时的"身份牌"（伞萝萝/塞壬不是伪人，可做成反差点） | 10 行判定 + 我们自己的文案 |
| 8 | **贴墙牌式状态显示**（`EnsureDetectorPanelCreated` `:2807-2811`：`SpriteRenderer` 父到 `wallRenderer`、抄 layer 与 `sharedMaterial`；12 张程序生成精灵 `:3039`） | **《深空电玩》**：店里已有 8 张立绘与多块浮窗，缺一块"**永远在墙上、不进 UI 栈、不被 Esc 关掉**"的状态牌（当前盗版卡关/检查中/低迷期）。这是比 tooltip 更符合"空间站企业"语感的呈现方式 | 1 个 `GameObject` + 12 张精灵 |
| 9 | **`CyborgAlarm` 直接播原版 `EmporiumAudioPlayer.alarm2` + "同一客户只响一次"**（`CyborgAlarm.cs:18-46`） | **两条线**：任何"提醒一次就够"的音效接入都可照抄这个 `Observe(client, detected, enabled)` 三参 + `_client != client` 去重 | 20 行 |

**一句话回答**：它是**第四种——"常驻全量补丁 + 门卫早退"**：71 个带 `[HarmonyPatch]` 的文件靠加载器特性扫描全量挂上、全库 0 处 `Unpatch`/`Harmony.Patch`，四个设施的三态开关（`Disabled/Wilde/Included`）只决定"设施实体建不建"和"补丁体第一行的 `IsXxxEnabled` 要不要早退"，与前三种"把补丁编成表、按表摘挂"的做法在结构上无关。
