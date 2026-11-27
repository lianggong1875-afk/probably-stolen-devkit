# 工坊 3841509159 架构蒸馏档案 —— `yexiaoyang.InteriorExpansion`（室内扩建）

> **架构参考**，不是功能清单。对象：`缓存文件夹\decomp_ws\3841509159`（ILSpy 产物，无源码注释）。
> 方法：目录/命名侦察 → 定点精读 20 个文件 → 全库正则统计。**未通读 8.9 万行**。
> 代码块为反编译原文摘录；`(object)`/`(Type[])null` 等 ILSpy 强制转换保留，个别单行块做了合并以便阅读。

---

## 0. 勘误（2026-11-27 由主模型复核工坊包 manifest 后更正）

**本档案初稿的判断有误，以下是核实过的事实**（工坊包 `manifest.xml` 原文）：

```xml
<ID>yexiaoyang.InteriorExpansion</ID>
<Name>深空当铺：领地与革命 Probably Stolen: Claims &amp; Uprisings</Name>
<Author>yexiaoyang</Author>
<ModVersion>1.2.27</ModVersion>
```

- **它就是 Claims & Uprisings**：`Name` 字段明写「领地与革命 / Probably Stolen: Claims & Uprisings」，Steam 工坊标题也是这个。**作者改过名但没换 manifest ID**（ID 仍沿用旧项目 `yexiaoyang.InteriorExpansion`），这种情况在工坊里很常见。
- 所以"claims / uprisings 在源码里 0 命中"**不能**推出"不是这个模组"——它只是**代码里用的是英文/中文的自有命名**（领地、革命、街区占用这些词），没直接写 claim/uprising。内容也确实对得上：「领地建设 + 下城区地区任务 + 士兵招募 + 回合战斗」就是"claims & uprisings"的实现。
- **ID ≠ 标题**这条本身是个有用的情报：做兼容判断 / 写 `Prerequisites` / `harmony` id 时，**必须用 manifest ID（`yexiaoyang.InteriorExpansion`），不是工坊标题**。

| 项 | 实测 | 结论 |
| --- | --- | --- |
| 413 个 .cs | 413 | ✅ |
| 88,709 行 | 逐行计数 88,709（非空 81,457） | ✅ |
| 11 个 ModHook | 11 个不同事件 / 12 个订阅点 | ✅ |
| 209 个特性式 Patch | 233 个 `[HarmonyPatch]` 块 / 约 228 个补丁类 / 217 个带明确目标 | 同量级，口径不同 |

**它真实是什么**：把「店内空间扩建」做成整层新系统——室内房间建造与过夜生产、指挥室与士兵养成、街区占领与六边形战术战斗、夜市与街区市场、安保局执法、联系人剧情线，另加一套**供别的模组写 XML 扩展的内容框架**。

下面按原定 1~9 结构写（内容全部有效），只需把"claims/uprisings"读作它真实的等价大系统（房间扩建闭环 + 街区占领）。

## 1. 模块划分：物理目录是平的，分层全在文件名里

### 1.1 物理结构（只有 2 层，没有任何子目录）

```
decomp_ws\3841509159\
├─ Yexiaoyang.InteriorExpansion\            395 个 .cs，79,703 行   ← 全部业务
├─ Yexiaoyang.InteriorExpansion.Framework\   17 个 .cs， 1,743 行   ← 内容框架内核
└─ Properties\AssemblyInfo.cs
```

命名空间也只有 2 个（`Yexiaoyang.InteriorExpansion` 395 个 + `.Framework` 17 个）。395 个文件平铺在一个文件夹里，用**二维文件名**替代目录树：

* **前缀 = 域**：`Room` / `District` / `Tactical` / `Soldier` / `Command` / `Yexiaoyang` / `Security` / `Forge` / `Night` / `Purifier` / `Warehouse` / `Framework` / `Pawn`
* **后缀 = 角色**：`Def` `State` `Save` `Repository` `Service` `Controller` `Driver` `Panels` `Window` `View` `Rules` `Math` `Patch` `Migration` `Compatibility` `Regression`

### 1.2 五个顶层域（按行数）

| 域前缀 | 文件 | 行数 | 占比 | 负责什么 |
| --- | ---: | ---: | ---: | --- |
| `Room*` | 114 | 22,701 | 25.6% | 房间定义/状态/存档/建造规则/过夜生产/设施电力水/房间 UI |
| `District*` | 72 | 18,180 | 20.5% | 街区站点、占领与防御、市场库存、战斗结算、区域行动、地图几何 |
| `Tactical*` | 50 | 12,727 | 14.3% | 六边形战术战斗：地图生成/编辑器/AI/瞄准/弹道/技能/战斗视图 |
| `Soldier*` | 46 | 9,336 | 10.5% | 招募、职业、特质、装备槽、机械手、档案 UI、经验升级 |
| `Command*` | 22 | 5,346 | 6.0% | 指挥室：资源配方、教程、据点、声望、早报、原生服务接管、存档前处理 |
| `Framework*` | 15 | 1,725 | 1.9% | **内容框架内核**：Def 引擎、XML Patch、依赖作用域、诊断、外包注册 |
| 其余 74 个 | 74 | 8,694 | 9.8% | 联系人剧情、安保局、锻造、净水集群、夜市、仓库接收器、调试台 |

5 个域吃掉 77% 代码；Framework 仅 1.9% 却是唯一内核。**有分层，但分层靠命名约定，不靠目录**。

### 1.3 逻辑分层（7 层，从下往上）

| 层 | 代表 | 职责 | 规模 |
| --- | --- | --- | --- |
| L0 内容数据 | `DefinitionRegistry`(738) `DefFramework`(1130) `RoomDef` `DistrictCatalog` | XML→强类型 Def，schema 校验、唯一性、来源归属 | 100~1100 |
| L1 纯规则 | `RoomConstructionRules`(30) `RoomPowerRules` `PurificationMath` `TacticalBattleRules`(1629) | 纯函数：入参→枚举/数值，不碰单例 | 30~200 |
| L2 状态所有者 | `RoomRepository`(1928) `DistrictService`(7538) `SoldierService`(1922) | 唯一持有运行态，负责读/写档与迁移 | 500~7500 |
| L3 流程控制 | `RoomController`(2260) `RoomAutomation` `ExpansionDriver` `SaveLoadPreparation` `YexiaoyangVisitSession` | 场景切换、过夜推进、会话状态机 | 200~2300 |
| L4 表现 | `RoomPanels`(2023) `SoldierWindows`(2079) `TacticalBattleView`(2853) `DistrictMarketScene`(2402) | 建窗、渲染、输入 | 300~2900 |
| L5 补丁适配 | 35 个 `*Patch.cs`（合计 1,201 行）+ 功能类内部类 | 只做判断与转发 | 均 34 行 |
| L6 入口 | `ModEntry`(500) `ExpansionDriver`(28) `ModRuntimeGuard`(52) | 生命周期、钩子、全局熔断 | 极小 |

### 1.4 依赖方向是单向的吗？——是"分层"，不是"严格单向"

实测三条逆向依赖：① 补丁层直达任意层（`RoomPageRaycastPatch` 直接读 `RoomPanels.HasVisiblePage` 与 `RoomModalCoordinator.Active`）；② UI 直接读状态所有者（`RoomController.cs:1618` `repository.GetState(room)`，省掉 VM 层，`RoomCardViewModel` 只做数据搬运）；③ 状态所有者反向调 UI 文本（`RoomRepository.ProcessNight` 里几十处 `RoomUiText.Format` + `CommandNightReports.Add`）。

它用 4 条纪律抵住分层泄漏，而不是靠接口隔离：状态只有**一个**所有者且 UI 从不写状态；规则一律**纯函数 + 委托注入**；补丁类**不写业务**只判归属；跨域只走静态门面 `ModEntry.Current?.Repository`。

```csharp
// RoomConstructionRules.cs:7-29（全文）—— 规则层纯度样板：纯函数 + 两个委托注入 + 枚举返回
internal static ConstructionBlock Check(RoomLevelDef level, Func<string, int> roomLevel, Func<string, bool> occupied)
{
    if (level == null) return ConstructionBlock.None;
    if (!level.Available) return ConstructionBlock.Unavailable;
    foreach (RoomLevelRequirement requiredRoom in level.RequiredRooms)
    {
        if (roomLevel(requiredRoom.RoomDefName) < requiredRoom.MinimumLevel) return ConstructionBlock.Room;
    }
    if (!string.IsNullOrEmpty(level.RequiredOccupation) && !occupied(level.RequiredOccupation))
        return ConstructionBlock.Occupation;
    return ConstructionBlock.None;
}
```

### 1.5 规模分布：长尾 + 少数巨兽

| 区间 | 文件 | 行数 | 占比 |
| --- | ---: | ---: | ---: |
| ≤400 行 | **363** | 31,914 | 36.0% |
| 401–800 行 | 25 | 13,199 | 14.9% |
| >800 行 | **25** | **43,596** | **49.1%** |

中位数 **54 行**、均值 202 行。小文件保证"加功能不用改大文件"，25 个巨兽（`DistrictService` 7,538 / `TacticalBattleView` 2,853 / `DistrictMarketScene` 2,402 / `RoomController` 2,260）是已知技术债，见 §7.5。

---

## 2. 233 个 Patch 怎么组织

### 2.1 没有基类、没有注册器、没有开关表

全库实测：**0** 个补丁基类、**0** 个 `PatchAll()`、**0** 个手写补丁登记表；`new Harmony(...)` 只出现 **1** 次且是给第三方做可选适配（`RoomOptionalModCompatibility.cs:152`）。233 个补丁全部靠 `[HarmonyPatch]` 特性由 `ModLoader` 在装档时**一次性全量扫描应用**。

**开关不在"应用期"，在"执行期"**：补丁永远挂着，每个补丁体第一行判自己的门——好处是不需要易失效的"补丁开关表"元数据，代价是失效补丁点仍占派发链（用 §6.3 的零开销返回抵掉）。全库 `Is*` 型门 119 处，典型形态是把三件事折成一个判断：

```csharp
// SecurityAuthority.cs:59-69 —— 模组启用 + 存档已加载 + 本档已解锁
internal static bool IsActive
{
    get
    {
        if (enabled && ModEntry.Current != null && PlayerStore.HasInstance)
            return ModHelper.GetModDataBool("yexiaoyang.InteriorExpansion", "command.securityAuthority.unlocked", false);
        return false;
    }
}
```

### 2.2 两种宿主风格并存，分工清晰

| 风格 | 形态 | 数量 | 用途 |
| --- | --- | --- | --- |
| **独立补丁文件** | `RoomXxxPatch.cs`，一文件一补丁类 | 35 文件 / 1,201 行 | **跨模块穿透**：ESC 仲裁、射线、全店枚举、读档屏蔽——一眼看见"它动了原版哪里" |
| **寄生补丁** | 功能类内 `private static class XxxPatch` | 85 个文件中其余特性块 | **域内自用**：`YexiaoyangCharacter` 嵌 4 个只服务自己；`DistrictMarketScene` 一个文件嵌 **32 个** |

判据干脆：**补丁作用域 == 宿主边界**。改"全店可见的东西"→ 独立文件；改"我这个功能自己的原版方法"→ 就近塞进功能类。粒度有三种：① 单方法（绝大多数）；② **家族式**——14 个文件用 `TargetMethods()` 一次挂一组方法，最猛的是 `RoomWholeStoreQueryScopePatch` **一次包住 14 个原版"全店查询"方法**（§8 P1）；③ **两段式声明**——`[HarmonyPatch(typeof(X))]` + `[HarmonyPatch("M")]` 分开写（16 处）。

### 2.3 同一个原版方法被它自己 Patch 多次？——**24 个方法被 2~7 次，全部刻意**

| 原版方法 | 次数 | 各自负责 |
| --- | ---: | --- |
| `StoreUIManager.ResolveEscape` | **7** | 原生模态 / 辅助面板 / 确认框 / 战斗菜单 / 招募页 / 特质抉择 / 教程 |
| `SaveManager.DecodeNodes` | 3 | 旧物品形态迁移 / 弹药文本 / 估价数值 |
| `PlayerStore.EndNight` | 3 | 原生服务体接管 / 设施计费 / 物品生命周期 |
| `InputActionManager.Update`、`StoreUIManager.CheckRaycast`、`CustomUIManager.HandleEscape` | 各 3 | 互不相干的关注点 |
| 其余 17 个（`PixelWindow.ToFront`、`GameItem.GetNegociatedValue`、`PlayerStore.BuyItem`…） | 各 2 | — |

**靠什么不打架？优先级只排第三：**① **单点仲裁器**——所有 ESC/射线补丁都问同一个 `RoomModalCoordinator.Active`，自己不记状态（§8 P2）；② **"我不接手就 `return true`"**——每个前缀第一分支都是让位，绝不"顺手也处理一下"；③ **优先级分段**，全库只有 **12 个 `HarmonyPriority`、1 个 `HarmonyAfter`、0 个 `HarmonyBefore`**。

`ResolveEscape` 全链实测：`900` = `RoomAuxiliaryStoreEscapePatch:8` / `RoomAuxiliaryCustomEscapePatch:8`；`820` = `SoldierTraitDecisionEscape:8` / `SoldierTraitDecisionInput:8`；`800` = `SoldierRecruitEscape:11` `CommandTutorial:14` `DistrictUI:15,29` `CommandTutorialInput:16` `CommandBeforeNativeSave:10`；`0` = `RoomProduction:251`（配 `[HarmonyAfter(new string[]{"mod.more_device_upgrades"})]`）；未标注(400) = `RoomNativeModalEscapePatch:5` / `RoomConfirmationEscapePatch:5`。

**这里有个真隐患**：`900` 位 2 个、`820` 位 2 个、`800` 位 5 个、默认位 2 个——**同优先级内部顺序未定义**。它能活是因为每个前缀都"独立可判定、不依赖别人先跑"；谁要在同优先级里加一个写状态的前缀，就变成随机 bug。**我们的规矩：同方法多补丁必然显式错开优先级，不留并列。**

---

## 3. 它是怎么"加一个大系统"的——房间扩建闭环

### 3.1 链路总览（数据 → 触发 → 推进 → 播报 → UI → 存档）

```
[数据]  Defs/Rooms/*.xml ─► DefinitionRegistry.RegisterXml ─► RoomDef + List<RoomLevelDef>（构建见 §8 P4）
[触发]  RoomPanels.ShowConfirmation ─► RoomController.ConfirmConstruction (:1686)
                                    └► RoomRepository.StartConstruction   (:672-685)
                                       前置检查(施工中/满级/依赖房间/占领/钱) → owner.PayMoney(cost,true)
                                       → TargetLevel=CompletedLevel+1 → CompleteOnDay=day+Days → Save()
[推进]  ModHook.OnGoingSleepLate ─► ModEntry.EndNight (ModEntry.cs:96,418)
                                 └► RoomRepository.ProcessNight (:1149)
                                    每日去重 state.LastProcessedDay == dayCounter → skip
                                    IsConstructing && day+1>=CompleteOnDay ? CompleteConstruction
                                    : 按 ProcessorType 分派 NativeFacilityRoom / WaterPurifier / 默认礼物
                                      第三方 → ExtensionBehaviors.ProcessNight(room,state)   ← 扩展点 (:1249)
[播报]  CommandNightReports.Add(category, text, color) → 次日早报
[UI]    RoomController.ShowUpgrade / RenderRoomSelection ◄─ repository.GetState(room)
[存档]  RoomRepository.SaveChecked → SerializeObject(RoomSaveDocument) → SetModData(id,"rooms.v1")
```

### 3.2 关键类与调用关系

```
ModEntry(IMod) ─┬─► DefinitionRegistry(Defs) ◄─ FrameworkRuntime(构建/校验/代际)
                │        └─► RoomDef ─► RoomLevelDef / HotspotDef / FacilityDef / StorageGroupDef
                ├─► RoomRepository(状态) ─► Dictionary<string,RoomState>
                │        ├ Machine/Stockpile/Overflow : GameItem（真物件，非镜像）
                │        ├ 读 ReadSavedDocument() → RoomSaveDocument(Version=1)
                │        └ 写 SaveChecked()       → SetModData("rooms.v1")
                ├─► RoomController(流程/导航) ─► RoomPanels(UI 工具箱) ─► RoomClusterWindow / RoomCardHover
                └─► ExpansionDriver(MonoBehaviour，每帧调 ModEntry.Tick())
```

### 3.3 三个设计要点

① **状态即真物件**：`RoomState.Machine/Stockpile` 是真正的 `GameItem` 而非数据镜像，房间存档只存序列化树（`SavedItemTree`），重建时反序列化回物件——房间内物品的拖拽/堆叠/检视天然复用原版。② **每日幂等**：`state.LastProcessedDay` 挡住重复过夜（说明过夜钩子在真实路径里会触发两次，被坑出来的）。③ **三处分派 + 一个扩展点**：内置类型走 `ProcessorType` 字符串分派，第三方走 `ExtensionBehaviors.ProcessNight` 委托注册——**新增一种房间不用改 `ProcessNight`**。

---

## 4. 状态存储：一域一文档、key 带版本、只用私有键

### 4.1 落地位置：`ModHelper.SetModData/GetModData`，34 个 key

全库实测 31 个固定 key + 3 个动态前缀，命名即协议：

| 前缀 | 示例 | 形态 |
| --- | --- | --- |
| `rooms.v1` / `tutorials.v1` | 各单 key | **整包 JSON**（`RoomSaveDocument` / `RoomTutorialProgress`） |
| `command.*.v1` | `command.districts.v1` `command.markets.v1` `command.soldiers.v1` `command.resources.v1` `command.sale_storage.v1` `command.soldier_armory.v1` | 整包 JSON，一域一份 |
| `character.yexiaoyang.*` | `.completed` `.contacts.v1` `.followup_due_day` `.security_visit.pending` | **标量散键** |
| `command.tutorial.*` | `.stage` `.line` `.complete` `.recruit.id` | 标量散键（进度要逐条查） |
| `command.occupied.<site>` / `room.<def>.<key>` / `pawn.<def>.lastVisit` | 动态 key | 每实体一键；扩展数据**挂在提供者自己的 modId 下**（`RoomExtensionContext.cs:19-24`） |

规则：**"实体集合"整包 JSON；"标量事实"单独成键，避免为改一个 bool 重写整个文档；第三方数据由它自己的 modId 托管。** 且 **key 名里带 `.v1`**——版本既在文档字段（`Version=1`）也在 key 名里，换不兼容结构直接换 key，老档留在旧 key 上，不迁移也不丢。

### 4.2 物品怎么存：借原版序列化器，不自己写

```csharp
// RoomRepository.cs:1687-1712（节选）
internal static SavedItemTree EncodeTree(GameItem root)
{
    List<ItemFeature> savedItemFeatureList = PlayerStore.Instance.savedItemFeatureList;   // 原版把 features 塞在全局列表里
    PlayerStore.Instance.savedItemFeatureList = new List<ItemFeature>();
    try
    {
        List<SaveItemNode> list = SaveManager.EncodeNodes((GraphNodeStorage)(object)root);
        FermentationSaveLinks.RemoveBorrowedIngredientLinks(list);                       // 剔除"借来的"跨模组引用
        List<ItemFeature> savedItemFeatureList2 = PlayerStore.Instance.savedItemFeatureList;
        return new SavedItemTree { Nodes = JsonUtility.ToJson((object)new SaveState { saveItems = list }),
            Features = JsonConvert.SerializeObject((object)savedItemFeatureList2, FeatureSettings) };
    }
    finally { PlayerStore.Instance.savedItemFeatureList = savedItemFeatureList; }         // 异常也不泄漏
}
```

三个可抄点：复用原版 `EncodeNodes/DecodeNodes` 天然兼容所有原版字段与后续更新；进出都要**保存/还原全局列表**（`try/finally`）；序列化前剔除交叉引用，防止把别人家的物品 id 写进自己存档。

### 4.3 版本化与迁移：四层机制

| 层 | 机制 | 证据 |
| --- | --- | --- |
| 结构版本 | 每文档 `public int Version = 1`，读取时 **`!= 1` 直接抛**，不做兼容猜测 | `RoomSaveDocument.cs:7`、`DistrictSave.cs:8`、`SoldierSaveDocument.cs:7`、`DistrictMarketSave.cs:7`、`CommandResources.cs:36`、`RoomTutorialProgress.cs:13` |
| 数值迁移 | **`BalanceVersion` 幂等阶梯**：`<1` 修一批置 1，`<2` 再一批…当前到 4 | `DistrictService.cs:5506-5558`（§8 P5） |
| 内容补全 | **声明式补齐**：新 XML 站点若档里没有就补默认态；收尾强制修正不变量 | `DistrictService.cs:5490-5505, 5559-5566` |
| 物品迁移 / 前向兼容 | 读档 Prefix/Postfix 检测旧形态逐件迁移、失败保留重试；`[JsonExtensionData]` 保留未知字段 | `DistrictItemShapeMigration.cs:11-22,216`、`SavedRoomState.cs:30-31` |

### 4.4 坏档怎么防：五道闸（全书最值得抄的部分）

1. **抛而不写**：解析失败 `throw new InvalidOperationException("Unsupported or incomplete room save data.")`（`RoomRepository.cs:391-400`），被 `AdvanceRestoreStep` 捕获后置 `loadFailed` 并打 **`"Room save was retained unchanged"`**（`:411-415`）——**读失败 ≠ 写坏档**。
2. **Save / SaveChecked 双层**：`SaveChecked` 内部全抛，`Save()` 才 catch 并打 `"Room save was not replaced"`（`:1598-1639`）；同族文案在 7 个域重复（Room / Resource / Contact card / Soldier×2 / Tutorial equipment / District / Market / Sale warehouse）。
3. **增量恢复可取消**：`CancelIncrementalRestore` 清空状态并**逐个销毁已恢复物品**（`:307-337`），中途失败不留幽灵物件。
4. **未注册工厂的物品不丢**：`identifier` 找不到工厂（玩家卸了依赖）时整棵子树挪进 `DeferredItemTrees`/`Overflow`，打 `"items retained until their factories are available"`（`:1738-1767`）。
5. **坏房间隔离**：`RoomState.RestorationError` 标记坏房间，后续流程对它走专属分支（跳过生产、早报提示 `dependencies_missing`）——**一个房间坏不拖累全店**。

---

## 5. 与 200 个原版方法共存的手段

### 5.1 靠仲裁，不靠优先级

`HarmonyPriority` 只 12 次（集中在 ESC/输入这类**必须定序**处）；`HarmonyAfter` 只 1 次（给 `mod.more_device_upgrades` 让路）；`HarmonyBefore` **0** 次。其余 200 多个补丁不声明任何顺序，因为它们**互不重叠**：要么目标唯一，要么同一方法上的多个补丁各自能判定自己该不该动。

### 5.2 前缀守卫的三种形态

| 形态 | 写法 | 例子 |
| --- | --- | --- |
| 功能门 | `if (!IsActive) return true;` | `SecurityAuthority.InvestigationDay`（`:21-37`） |
| 归属让位 | `if (owner == null \|\| !owner.Resolve()) return true;` | 7 个 `ResolveEscape` 补丁 |
| 作用域令牌 | `Prefix(out IDisposable __state)` + `Finalizer` 归还 | `RoomWholeStoreQueryScopePatch`（§8 P1） |

第三种最强：**它不判断"要不要拦"，而是标记"现在处在什么语境里"**，让被拦方法内部的所有调用方共享语境——一次挂 14 个方法而不用为每个方法写判断。

### 5.3 跨模组兼容：`IsModRunning` + 反射 + 熔断

10 处 `ModHelper.IsModRunning`，三类用法：**软依赖探测**（`LegendaryModules` `PurifierCluster.cs:110`；`hollow.clientdemandhighlight` `RoomOptionalModCompatibility.cs:36`；`local.better-box-selection` `:137,167`）；**依赖图校验**（框架扩展必须把本体写进 `Prerequisites`，否则拒绝注册，`FrameworkRuntime.cs:106`）；**加载列表快照**（构建内容时只看运行中的模组，`FrameworkRuntime.cs:382`）。

可选模组完整套路（`RoomOptionalModCompatibility.cs:131-211`）：一次性尝试 + 熔断标志（`boxAttempted`/`demandFailed`）→ 反射 `AccessTools.TypeByName` 拿方法，拿不到永久关闭 → 挂到**独立 Harmony 实例**便于单独 `UnpatchAll` → 回调里再确认 `IsModRunning`，防中途禁用。三段式防线的关键在于**失败只允许失败一次**。

### 5.4 版本升级的第一道警报：`Required()`

原版方法一旦改名/删掉，补丁静默失败只会让玩家看到"功能莫名没了"。它对 14 个目标方法全部强制存在；同样的纪律体现在单例访问上：`HasInstance` 出现 **92 次**、`IsDestroyed()` 34 次、`ModRuntimeGuard.Run` 68 次，从不裸读 `PlayerStore.Instance`。

```csharp
// RoomWholeStoreQueryScopePatch.cs:35-38 —— 补丁应用期直接抛，装档就炸出明确日志
private static MethodBase Required(MethodInfo method, string name)
{
    return method ?? throw new MissingMethodException("Native room query target is missing: " + name);
}
```

---

## 6. 性能

### 6.1 11 个 ModHook 按频率分档

| 频率 | 钩子 | 备注 |
| --- | --- | --- |
| 每顾客 | `OnGenerateCustomerVeryLate` | 仅"首次非教程顾客且剧情未完成"时插入（`YexiaoyangCharacter.cs:121`），其余直接返回 |
| 每 tooltip | `OnCreateTooltipLate` ×2 | `PurifierCluster` + `SoldierItemStats`，按物品类型快判 |
| 每夜 ×3 / 离店 | `OnGoingSleepEarly`、`OnGoingSleepLate`、`OnHandlingNightlyServicesLate`、`OnLeavingStoreEarly` | `EndNight` 是重活（房间+士兵+街区），三块各自 `try/catch`；离店复用同一处理器 |
| 每档一次 | `OnGameLoadedInit` `OnGameLoadedLate` `OnNewGameStarted` `OnModAssetsLoaded` `OnModItemDirectoryInit` | 冷路径 |

原版没有 OnUpdate 型钩子，所以它**自建每帧驱动**：`ExpansionDriver`（28 行）挂在 `DontDestroyOnLoad` 上，`Update()` 只调一次 `ModEntry.Tick()`，外面 `try/catch` + **5 秒熔断**：

```csharp
// ExpansionDriver.cs:12-27
private void Update()
{
    if (Entry == null || Time.unscaledTime < retryAt) return;
    try { Entry.Tick(); }
    catch (Exception ex) { retryAt = Time.unscaledTime + 5f; Debug.LogError((object)("[InteriorExpansion] Initialization/scene update: " + ex)); }
}
```

`ModEntry.Tick()`（`:169-275`）是唯一每帧总闸：先判 `PlayerStore.HasInstance`/`EmporiumEntry.Instance`/`StoreUIManager.Instance`/`RenderHandler.current` 四个单例齐不齐，不齐就 `ResetRuntime()` 返回；再串行调 19 个 `*.Tick()`。

### 6.2 分帧：毫秒预算切片

| 位置 | 预算 | 机制 |
| --- | --- | --- |
| `SaveLoadPreparation` | 每阶段 5.0ms / 6 阶段 | `Stage` 枚举 + `Stopwatch`，一帧只推进一阶段（§8 P3） |
| `RoomRepository.AdvanceIncrementalRestore` | 2.5ms / 8 步上限 | `IEnumerator<object>` + 双闸（步数 + 时间） |
| `DistrictService.PreloadStep` / `PreloadLootCatalog` | 4.0~5.0ms | `AdvanceLootCatalog(deadline)`；`SynchronizeDefinitions` 用 `yield return 0` 让帧 |

全库：`Stopwatch` 14 处、`budgetMilliseconds` 参数 5 处、`yield return null` 19 处。**预算可以嵌套，但每层都必须有硬上限。**

### 6.3 节流：时间戳表

`Time.unscaledTime` 74 处、`Time.frameCount` 39 处。典型间隔：**0.1s** 进度条文字（`SaveLoadPreparation.cs:275`）、**0.25s** 第三方需求高亮（`RoomOptionalModCompatibility.cs:35`）、**0.5s** 市场旧格式扫描（`DistrictMarketService.cs:298`）、**2s** Def 检查（`ModEntry.cs:177`）、**5s** 驱动熔断。

另有 62 个 `Dictionary` 字段做缓存（贴图/布局/窗口/物品查找）。最关键的省法是被 14 个"全店查询"共用的 `RoomNativeEnumeration.Append`：第一件事看作用域，不在语境里就**原引用返回**——于是"挂了 200 多个补丁"的每帧成本 ≈ 200 × 一次空判断，而非 200 次业务计算。

```csharp
// RoomNativeEnumeration.cs:40-43（节选）—— 常态路径两次判断、零分配
RoomNativeEnumerationScope.EnumerationKind enumerationKind = (RoomItemLifecycle.IsPlacedDeviceScopeActive
    ? RoomNativeEnumerationScope.EnumerationKind.Lifecycle : RoomNativeEnumerationScope.CurrentKind);
if (enumerationKind == RoomNativeEnumerationScope.EnumerationKind.None) return nativeItems;
```

### 6.4 UI / 数据 / 逻辑的大致比例

三种口径交叉验证（ILSpy 无注释、UI 大量经门面调用，都要打折）：按"文件是否直接操作 RectTransform/TMP/Canvas"分桶 = UI 50.0% / 数据 23.3% / 逻辑 26.7%；按文件名后缀分桶 = UI 24.5% / 数据 7.4% / 逻辑 68.3%；按"逐行是否触碰 UI API" = 仅 3.4% 的行直接碰。

**综合判断（粗）**：**UI 35%~45%**（52 个 UI 后缀文件 14,537 行 + 寄生在服务里的建窗代码；`RoomPanels` 一个文件 2,023 行，所有窗口都由它的 `Rect/ImageAt/Label/ButtonAt/Border` 六个静态工厂搭出来）；**数据/定义/规则 10%~20%**；**逻辑/状态/流程 45%~55%**。另三个硬数字：**纯补丁文件仅占 1.4% 行数**（35 文件/1,201 行）；测试调试 3.1%；框架内核 1.9%。

---

## 7. 它踩过的坑（从日志文案与防御写法反推）

### 7.1 "存档被自己写坏"比"读档失败"更致命 → 抛而不写

7 个域重复同一句兜底：`"<X> save was not replaced"` / `"save preserved"` / `"save was retained unchanged"`。配套写法是 `SaveChecked()` 全抛、`Save()` 才吞并 `LogError`。**读档可以"放弃读取 + 保留旧档"，绝不可以"读出半个状态 + 覆盖写回"。**

### 7.2 模态窗口的原生 ESC / 射线穿透

`ResolveEscape` 被自己 Patch **7 次**、`CheckRaycast` 3 次、`CustomUIManager.HandleEscape` 3 次——这个数量本身就是"我开了大窗口，原版还在底下响应 ESC 和点击"反复踩坑的证据。最终解法是 §8 P2 的仲裁器 + `CanvasGroup` 静音 + **帧内消费标记**（`auxiliaryEscapeFrame == Time.frameCount`，`RoomModalCoordinator.cs:434-444`），保证一次按键只被吃一次。

### 7.3 日志风暴与每帧异常（附：加载时长 = 第一印象）

驱动 5 秒熔断、可选模组 `demandFailed`/`boxAttempted` 永久熔断，都说明踩过"某对象在某场景为 null，于是每帧 LogError 一次、日志几万行、帧率个位数"。**任何每帧入口的 catch 必须自带退避，且失败一次不要再重试同一件事。**

同一思路的另一种形态是 `SaveLoadPreparation` 整套（自绘进度条、6 阶段分片、延迟早报、`"Cooperative preparation failed; reverting to normal initialization"` 降级路径）：房间越多读档越慢，慢到玩家以为卡死。解法不是优化算法，而是**把阻塞工作改成协作式分帧 + 显示进度 + 允许中途取消回退**。

### 7.4 错误路径上物品凭空消失

`ModRuntimeGuard.DestroyItem` 先判 `IsDestroyed()` 再 `Destroy()`（`:41-51`）；未知工厂物品进 `Overflow`；市场开关有 `"Market opening rolled back"` / `"Market rollback items"` / `"Market rollback presentation"` 三段回滚；`DistrictService` 反复出现 `"retained for retry"`（礼物投递、经验、兑换券、凭证、抢劫）。**物品是玩家的钱**：任何"半成功"分支必须留下可重试痕迹，或者先验证后扣费。

### 7.5 巨兽文件的代价（可观测的债）

`DistrictService` 单文件 7,538 行、`try/catch` 31 处、`throw new` 37 处、公开方法 60+，同时扮演状态所有者、战斗引擎、市场宿主、迁移器、测试夹具、物品工厂注册表——连 `override void Validate()` / `Destroy()`（`:257,261`）的 `GameItem` 子类都塞在同一文件。**8.9 万行不会因为"文件小"而失控，却会因为"一个 Service 变成上帝对象"而失控**；49% 代码集中在 25 个文件里是它唯一的结构性弱点。另：全库 **0 条中文注释**，可读性完全由命名承担（`PurifierCluster.ResetLastRemoved`、`RoomItemLifecycle.IsConverterCostScopeActive`），既是优点（可检索）也是风险（命名一偏就没兜底）。

---

## 8. 可复用的 5 个架构级范式

### P1 作用域令牌（Scope Lease）：一次挂 14 个原版方法，而不是逐个写判断

**问题**：要让"全店物品查询"看见你新增的容器，但原版有 14 个方法在做这件事（`HaveItem`、`GetItemCountById`、`IsPlayerOwnThisItem`、`FindOwnedCertificates`…），各有各的语义。**做法**：不判断"要不要拦"，而是**标记语境**；前缀进语境，`Finalizer` 出语境，被拦方法内部的所有调用共享语境。

```csharp
// RoomNativeEnumerationScope.cs:88-94 —— 线程内可重入的租约栈
private static IDisposable Enter(EnumerationKind kind)
{
    ScopeState scopeState = current ?? (current = new ScopeState());
    Lease lease = new Lease(scopeState, kind);
    scopeState.Leases.Push(lease);
    return lease;
}
```

```csharp
// RoomWholeStoreQueryScopePatch.cs:40-49 —— 前缀进、Finalizer 出；抛异常也不会漏
private static void Prefix(out IDisposable __state)
{
    __state = RoomNativeEnumerationScope.EnterQuery();
}

private static Exception Finalizer(Exception __exception, IDisposable __state)
{
    __state?.Dispose();
    return __exception;
}
```

> 为什么是架构级：它把"我要插入原版语义"从 **N 个方法的 N 份胶水** 降为 **1 个语境 + 1 个汇合点**（汇合点即 §6.3 的 `Append`）。`[ThreadStatic]` 是因为主线程外可能还有协程/异步路径进来。注意 `Lease.Dispose` 要**从栈顶连续弹出已释放项**（`:34-48`），否则乱序释放会让语境卡死。

### P2 单点仲裁器：7 个补丁抢一个 ESC，靠"问同一个对象"而不是"排顺序"

```csharp
// RoomModalCoordinator.cs:432-444 —— 唯一实例 + 帧内消费标记
internal static RoomModalCoordinator Active { get; private set; }

internal static bool AuxiliaryEscapeConsumedThisFrame
{
    get
    {
        if (Active != null && !Active.disposed && Active.enabled())
        {
            return Active.auxiliaryEscapeFrame == Time.frameCount;
        }
        return false;
    }
}
```

```csharp
// RoomNativeModalEscapePatch.cs:5-18（全文）—— 补丁只有 14 行，逻辑全是"问 + 让位"
[HarmonyPatch(typeof(StoreUIManager), "ResolveEscape")]
internal static class RoomNativeModalEscapePatch
{
    private static bool Prefix(ref bool __result)
    {
        RoomModalCoordinator active = RoomModalCoordinator.Active;
        if (active == null || !active.ResolveEscape())
        {
            return true;
        }
        __result = true;
        return false;
    }
}
```

> 仲裁器内部按"辅助面板 → 确认框 → 战斗 → 原生 ESC 菜单"的固定顺序串起来（`RoomModalCoordinator.cs:639-693`）——顺序写在**一个方法里**，可读可测，是唯一真相来源；补丁永远不关心顺序。

### P3 分片协作装载：把"读档卡死"变成"分 6 阶段、每帧 5ms、带进度条"

```csharp
// SaveLoadPreparation.cs:16-24 —— 阶段即状态机
private enum Stage { Rooms, Records, Soldiers, District, Loot, Ready }
```

```csharp
// SaveLoadPreparation.cs:197-220 —— 一帧一阶段 + 实测耗时日志（便于定位"哪个阶段慢"）
if (Time.frameCount == openingFrame) return false;
long timestamp = Stopwatch.GetTimestamp();
bool flag = RunStage(entry);
double num = (double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
workMilliseconds += num;
longestSlice = Math.Max(longestSlice, num);
if (!flag) return false;
Debug.Log((object)$"[InteriorExpansion Load] slot={slot} stage={stage} work={workMilliseconds:F0}ms longestSlice={longestSlice:F0}ms elapsed={stageTime.Elapsed.TotalMilliseconds:F0}ms");
if (stage == Stage.Ready) { Debug.Log((object)$"[InteriorExpansion Load] slot={slot} ready elapsed={totalTime.Elapsed.TotalMilliseconds:F0}ms"); return true; }
stage++;
workMilliseconds = (longestSlice = 0.0);
stageTime.Restart();
return false;
```

配套三件套：自绘加载遮罩 + 进度条（`:146-183`）；`[HarmonyPatch]` 屏蔽 `KeyBindHelper.GetKeyDown/GetKey/GetBackDown` **三个方法**，读档期间吞掉所有快捷键（`:26-45`，输入穿透的经典修法）；早报延后到装完再弹（`:289-308`）。`Stage.Rooms` 内部再调 `AdvanceIncrementalRestore(5.0)` 做一层 2.5ms/8 步双闸（`RoomRepository.cs:278-305`）。

### P4 原子代际内容构建：扩展模组写坏了，不拖死本体

```csharp
// FrameworkRuntime.cs:377-388（节选）—— 签名比对：没变就不重建；存档已加载就不重建
if (disposed || building || ModHelper.GetModFolder("yexiaoyang.InteriorExpansion") == null) return;
FlushRegistrations();
LoadedMod[] source = ModLoader.Mods.Where((LoadedMod m) => m.Manifest != null && ModHelper.IsModRunning(m.Manifest.ID)).ToArray();
dependencies = NativeGraph();
LoadedMod[] array = source.Where((LoadedMod m) => FrameworkPackagePolicy.DependsOn(m.Manifest.ID, "yexiaoyang.InteriorExpansion", dependencies)).ToArray();
string text = string.Join("|", array.Select((LoadedMod m) => m.Manifest.ID + "@" + m.Manifest.ModVersion)) + ":" + engine.RegistrationVersion;
if ((!dirty && text == signature) || (Ready && PlayerStore.HasInstance)) return;
```

原子换代只有 5 行（`:480-484`）：`engine = defFramework; Ready = true; Generation++; signature = text; dirty = false;`——指针换掉、代际 +1，订阅者据此判断"内容变了"。

```csharp
// FrameworkRuntime.cs:501-511（节选）—— 全盘失败时旧世代继续用，只记诊断
catch (Exception ex3)
{
    LastError = ex3.Message;
    dirty = false;
    signature = text;
    diagnostics = diagnostics.Concat(new FrameworkDiagnostic[1]
    {
        new FrameworkDiagnostic(FrameworkSeverity.Error, "yexiaoyang.InteriorExpansion", "loading", ex3.Message)
    }).ToArray();
    Debug.LogError((object)("[InteriorExpansion Framework] Previous valid generation retained: " + ex3));
}
```

四道配套纪律：**在 `Fork()` 副本上构建、全过才替换**（`BuildValidated` `:320-373`，失败则逐个包剔除重试）；**补丁作用域强制**（`FrameworkPatches.EnforceScope` 只允许改自己或已声明依赖的 Def，`:211-241`）；**`XElement` 深拷贝**（`new XElement(x)`，避免共享节点被改写）；**注册期拒绝非法提供者**（`RequireProvider` 要求扩展把本体写进 `Prerequisites` `:87-111`）。这是它"既当模组又当框架"的唯一原因：**扩展只能污染自己那一代，不能污染本体。**

### P5 幂等阶梯状态修补：读档时"补差"，不写迁移脚本

```csharp
// DistrictService.cs:5506-5527 —— 阶梯修补：每级幂等、可分帧、有过滤条件
if (data.BalanceVersion < 1)
{
    foreach (DistrictState item in ActiveRegions.Where((DistrictState x) => !x.Owned && DistrictCatalog.Site(x.Id).SourceModId == "yexiaoyang.InteriorExpansion" && string.IsNullOrEmpty(x.PendingBattleId)))
    {
        foreach (DistrictFighter defender in item.Defenders) { DestroyFighter(defender); }
        item.Defenders.Clear();
        item.Firepower = DistrictCatalog.Site(item.Id).Firepower;
    }
    data.BalanceVersion = 1;
}
if (data.BalanceVersion < 2)
{
    foreach (DistrictState item2 in ActiveRegions.Where((DistrictState x) => !x.Owned && DistrictCatalog.Site(x.Id).SourceModId == "yexiaoyang.InteriorExpansion" && string.IsNullOrEmpty(x.PendingBattleId)))
    {
        RebalanceDefenders(item2);
        yield return 0;
    }
    data.BalanceVersion = 2;
}
```

四个细节决定成败：`SourceModId == 本体` 过滤（**别人加的站点不要动**）、`IsNullOrEmpty(PendingBattleId)` 过滤（**战斗打到一半不要动**）、`yield return 0` 分帧、以及收尾必须有一步"无论原来怎样，现在必须成立"的断言式赋值（`:5559-5566`）。

---

## 9. 一句话总结

**最该学的那个决定：把"我改了原版哪里"从散落的补丁逻辑里抽出来，变成三个集中式基础设施——一个作用域令牌（谁在什么语境里调我）、一个仲裁器（多个功能争同一原版入口时谁说了算）、一个原子代际构建（外部内容坏了只废它自己）——剩下的 200 多个补丁全部退化成"判断 + 转发"的 30 行薄片。**

体量相当的模组不会死于"补丁太多"，只会死于"补丁里藏业务"。它用 1.4% 的代码量（35 个补丁文件、1,201 行）控住了 88,709 行系统与原版的边界；代价留在另一处——`DistrictService` 这类上帝对象，这是我们要主动避开的。

---

## 附录 A 证据索引（可复核）

| 结论 | 文件:行 |
| --- | --- |
| 模组身份 / 11 个 ModHook 订阅 | `ModEntry.cs:25` / `ModEntry.cs:88-98`、`YexiaoyangCharacter.cs:121` |
| 233 个 `[HarmonyPatch]`、约 228 补丁类、85 个文件、24 个方法被重复 Patch（最多 7 次） | 全库正则统计；`ResolveEscape`×7、`DecodeNodes`×3、`EndNight`×3 |
| 12 Priority / 1 After / 0 Before / 14 个 `TargetMethods()` / 34 个 ModData key | 全库统计 |
| 存档 fail-safe、版本、迁移 | `RoomRepository.cs:383-417,1598-1639`、`RoomSaveDocument.cs:7`、`DistrictService.cs:5506-5558`、`SavedRoomState.cs:30` |
| 分片装载 / 仲裁器 / 作用域令牌 / 原子代际 | `SaveLoadPreparation.cs:14-263`、`RoomModalCoordinator.cs:432-693`、`RoomNativeEnumerationScope.cs:88-94`、`FrameworkRuntime.cs:375-516` |
| 可选模组适配、规则纯函数、物品序列化借用、巨兽文件 | `RoomOptionalModCompatibility.cs:131-211`、`RoomConstructionRules.cs:7-29`、`RoomRepository.cs:1687-1712`、`DistrictService.cs`(7,538) |

## 附录 B 未验证 / 推测项

* 工坊包内的 `Defs/`、`Patches/`、`Languages/` XML 资源：反编译只导出 `.cs`，XML 内容**未见到**；`FrameworkRuntime` 读 `Defs/index.xml`、`Patches/index.xml`、`Framework.xml`（`:415-462`）表明**存在**，但无法核实清单 →（推测）。
* 200+ 补丁在真机的帧耗时、磁盘包体构成：**未实测**，§6 全部结论来自代码结构（预算、节流、快速返回），不是 profile 数据。
* `DistrictService` 内 `override Validate/Destroy` 的基类归属：仅见签名（`:257,261`），未见其 `class` 声明行 →（推测为 `GameItem` 子类）。
* 简报关键词与实物不符的原因（id 复用 / 转存错 / 简介张冠李戴）：无法从源码判断，**不做推断**。
