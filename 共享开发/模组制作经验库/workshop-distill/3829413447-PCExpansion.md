# 工坊 3829413447 架构蒸馏档案 —— `pcexpansion`《深空装机》v1.5.5（PC Expansion）

> **工坊 id**：`3829413447`｜作者「阿铭」｜`ModVersion 1.5.5`｜目标版本 `0.46D` 与 `1.0.0.16`｜`<Capabilities />` 为空、`<Prerequisites />` 为空（`manifest.xml:2-11`）。
> **反编译目录**：`缓存文件夹\decomp_ws\3829413447`（ILSpy 产物；595 个 `.cs` / **123,278 行**，另带 263 张 PNG、29 个 WAV、3 套 2068 键的本地化 CSV，整包 23 MB）。
> **为什么值得读**：它是本库体量最大、且少见的**在"当铺"这一层之上又长出三个独立经营子系统**（装机工作间 / 服务器机房 / 收藏室）的模组，而且——**它一块地图区域都没加**。全部新空间都是店内自绘的全屏模态 Canvas，全部新物品都寄生在原版工厂骨架上，全部状态塞进 `modData` 的一个 JSON 键里。想学"如何在不动原版结构的前提下塞进一个 200 物品 / 4 台机器 / 3 个房间的产业"，这是范本；想学"如何把 12 万行写成 12 万行"，这也是范本。

> **方法诚实声明**：先做目录/行数分布（§2），再按类型签名扫描全库（`[HarmonyPatch]` 271 处、`new ItemDefinition` 19 处、`modData[...]` 21 个键），**精读 22 个文件**：`ComponentCatalog`、`ComponentRecipes`、`ComponentItemDefinitions`、`ItemSetup`、`ItemFactory`、`ItemCatalog`、`HardwareItemDefinitions`、`WorkroomComponentTemplates`、`ComponentAssemblyAdapter`、`MachineRepairAdapter`、`WorkroomItemCodec`、`NativeItemCodec`（节选）、`FormalStorage`、`FactionCommerce`、`RepairVisitorHooks/Clients`、`MarketPartRequests`、`MarketEventCatalog`、`ProcurementHooks`、`NpcSupplyHooks`、`WorkroomShopWindowPatch`、`WorkroomView`（头部+房间入口）、`StoreEntryKey`、`ModEnvironment`、`HighEndPricing`。**未通读 12.3 万行**。

---

## 1. 一句话结论

**它把"PC 产业"压成了十几张 C# 代码表（数据只占 3.5%）+ 1 套全屏自绘 UI（22.6%）+ 1 个单键 JSON 存档文档；真正的创意逻辑约 7,200 行（6%），其余 11 万行是"把每个操作包成一笔可回滚事务"的脚手架，和一套 7,005 行的机房假浏览器。**

---

## 2. 规模与结构

### 2.1 物理目录 → 行数（按 namespace 聚合，共 595 文件 / 123,278 行）

| 目录（namespace） | 文件 | 行数 | 占比 | 职责 |
| --- | ---: | ---: | ---: | --- |
| `PCExpansion\` | 472 | 115,818 | **94.0%** | 一切：物品表、事务、UI、补丁、编解码全平铺在这一个目录里 |
| `PCExpansion.Logic.Workroom\` | 47 | 3,061 | 2.5% | 纯规则层：配方表、装配/拆解/维修/回收的计划与求解（`AutoFillSolver` 518 行是唯一的"算法"） |
| `PCExpansion.Logic.Npc\` | 17 | 1,663 | 1.3% | 来客规则：分档权重、身份命名、采购单规则 |
| `PCExpansion.Presentation.Workroom\` | 18 | 725 | 0.6% | 工作间的排版常量与文本拼装（不含 Unity 调用） |
| `PCExpansion.Logic.Collection\` | 17 | 546 | 0.4% | 收藏品账本/任务/商店的纯数据规则 |
| `PCExpansion.FormalR02\` | 4 | 477 | 0.4% | 存档写入器 `FormalStorage` + 3 个零碎补丁（`R02` = 第二轮重写） |
| `PCExpansion.Logic.Benchmark\` | 10 | 316 | 0.3% | "跑分"评分模型 |
| `PCExpansion.Presentation.Collection\` | 4 | 306 | 0.2% | 收藏室排版 |
| `PCExpansion.Logic.Items\` | 1 | 220 | 0.2% | 部件数值/容量规格 |
| `PCExpansion.Logic.StartingPerks\` | 1 | 102 | 0.1% | 开局天赋与礼包 |
| `PCExpansion.Presentation.Manual\` | 1 | 28 | 0.0% | 手册排版 |
| `Properties\` / `System.Runtime.CompilerServices\` | 2 | 16 | 0.0% | 反编译残留（`IsExternalInit`）、`--f__AnonymousType2.cs` 为 0 行 |

**"按目录看结构"在这一包里完全失效**：94% 的行挤在同一个 namespace 下，分层只存在于文件名后缀（`*View` / `*Runtime` / `*Adapter` / `*Codec` / `*Facts` / `*Command` / `*Proof`）。

### 2.2 功能簇 → 行数（按文件名前缀聚类，覆盖全部 115,818 行根目录代码）

| 功能簇 | 文件 | 行数 | 占比 | 说明 |
| --- | ---: | ---: | ---: | --- |
| `Workroom*` | 73 | 24,103 | 20.8% | 工作间：视图 + 储物 + 拖拽 + 面板 + 输入租约 + Driver |
| `ServerRoom*` | 45 | 17,956 | 15.5% | 机房：**单文件 `ServerRoomView` 就 7,005 行**（内置假浏览器），加订单/经济/容量 |
| `Collection*` | 51 | 12,625 | 10.9% | 收藏室：账本、任务、商店、生意加成、奖励 |
| `Npc*/Jiangbai*/Luoxi*/Story*` | 38 | 11,402 | 9.8% | 三个 NPC 剧情线（姜白 1,105 行、洛希 919 行）+ 通用小故事 |
| `Production*` | 39 | 9,946 | 8.6% | 四台生产机 + 合同 + 供货 + 夜结 |
| `NPC/Market`（采购/委托/装配工坊/批量服务） | 37 | 9,144 | 7.9% | 来客、委托单、市场事件、批量拆修机 |
| `Codec/Save` | 14 | 7,243 | 6.3% | `NativeItemCodec` 2,413 + `WorkroomStorageState` 832 + `ItemSnapshotCodec` 836 … |
| 基础设施 / 资产 / 杂项 UI（其余） | 65 | 10,592 | 9.1% | 钥匙按钮、音区、性能探针、语言表、贴图缓存、Json 封装 |
| `Component*` | 11 | 1,592 | 1.4% | **整个 9 品类 × 5 档 × 2 状态 = 90 个物品的全部定义**，只用了 1,592 行 |

### 2.3 责任分层（按后缀语义分类，桶间互斥、按文件名判定，误差 ±3%）

| 层 | 文件 | 行数 | 占比 | 判定依据 |
| --- | ---: | ---: | ---: | --- |
| 视图 / 绘制（`*View *Ui *Panel *Browser *Palette *Skin *Chart *Audio *Button`） | 39 | 26,122 | **22.6%** | 全部是 `new GameObject` + TMP + `RectTransform` 的堆砌 |
| 业务运行时（`*Runtime *Driver *Economy *Machine *Catalog *Recipe`） | 237 | 55,106 | 47.6% | 事务编排与状态机 |
| Harmony 接口（`*Hooks *Patch *Adapter *Clients *Rules *Facts *Command *Context`） | 146 | 18,421 | 15.9% | 补丁 + 每笔操作四件套（Facts/Command/Context/Outcome） |
| 存档 / 编解码（`*Codec *Snapshot *Storage *State *Document *Record *Json`） | 31 | 12,155 | 10.5% | 物品快照、文档校验、版本迁移 |
| 数据表（`*Definitions *Catalog *Recipes *Templates *Prices *Content *Handbook`） | 19 | 4,014 | **3.5%** | 声明式表，几乎无分支 |

**这就是"12 万行花在哪"的答案：真正的数据只占 3.5%，真正的创意逻辑（装配/拆解/维修/回收的计划与判定）约 7,200 行（6%），UI 22.6%，剩下 60%+ 是运行时编排 + 事务脚手架 + 存档编解码。**

### 2.4 三个"看着像代码、其实不是"的部分

1. **没有冻结的旧引擎副本。** 全库 0 处重新声明 `PlayerStore`/`GameItem`/`SaveManager`/`StoreClient`，全部 namespace 都是 `PCExpansion*`。上一批（`3841509159`）那种"3.27 万行旧引擎"在这里不存在——这 12 万行是**原生 12 万行**。
2. **长尾极其零碎。** 183 个文件 ≤60 行（合计仅 4,646 行），81 个文件是 `*Context/*Outcome/*Facts/*Command/*Plan/*Token/*Proof/*Snapshot` 这类"一个类型一个文件"的接缝；`CollectionBusinessRuntime` 3,131 行里塞了 **281 个成员**，绝大部分是嵌套的票据类。
3. **性能探针是编译期销号的空壳。** `PerformanceTrace.Begin()` 直接 `return default;`、`Scope.Dispose()` 空实现（`PerformanceTrace.cs:42-59`），但 24 个调用点仍保留，`Part` 枚举里的名字（`Capture`/`Restore`/`JsonWrite`/`UiGap`/`RestoreDecode`…）就是作者当年的热点清单。**发布版零成本、又留下了完整的证据地图**——这一手值得抄。

---

## 3. 它怎么做这件事

### 3.1 200+ 物品 / 45 型号 / 9 品类 / 5 档 / 41 零件：**是代码表，不是数据表**

三层结构，全部是 C# 静态字面量：

| 表 | 位置 | 规模 | 生成什么 |
| --- | --- | --- | --- |
| 品类 × 型号表 | `ComponentCatalog.cs:87-97` | 9 行 × 5 型号名 | 45 个"型号"（`MATREXX 30`→`Corsair 1000D`…） |
| 物品枚举 | `ComponentCatalog.cs:109-124` | 9×5×2 循环 | **90 个整件物品 id**（`pcrepair.gpu_t3_broken` 这种） |
| 零件配方表 | `Logic.Workroom/ComponentRecipes.cs:9-42` | 6 族 × 种类 | 6 主体 + 25 核心件 + 10 通用件 = **41 个零件档** |
| 零件展开 | `ComponentRecipes.cs:62-82` | `Common()` / `Core()` 两个局部函数 | 41 → **82 个零件物品 id**（完好/破损各一） |
| 收藏品表 | `CollectionCatalog.cs:30-49` | 15 行（2 行可修） | 17 个物品 |
| 机器表 | `ServerRoomMachineCatalog.cs:63-77` | 10 矿机/服务器 + 3 博士体 | 14 个硬件（含协处理器） |
| 其它 | `NpcItemDefinitions` / `ProductionCycleMachine` / `BatchServiceMachine` / `ProductionSupplyItems` | 19 处 `new ItemDefinition` | 名片、招牌、3 本手册、2 个罚没箱、4 台生产机、2 台服务机、供货箱… |

**合计约 219 个物品 id**（90 + 82 + 17 + 14 + 8 + 4 + 2 + 2 + 晶粒/材料变体）。它们**全部汇进一张字典**：

```csharp
// ItemSetup.cs:41-60 —— 七组定义 Add 进同一张 Ordinal 字典，key 冲突即崩
private static Dictionary<string, ItemDefinition> BuildDefinitions()
{
    Dictionary<string, ItemDefinition> result = new(StringComparer.Ordinal);
    Add(ComponentItemDefinitions.All());
    Add(HardwareItemDefinitions.All());
    Add(BatchServiceMachine.Definitions());
    Add(ProductionSupplyItems.Definitions());
    Add(ProductionCycleMachine.Definitions());
    Add(CollectionItemDefinitions.All());
    Add(NpcItemDefinitions.All());
    return result;
}
```

**和原版对上的方式（关键）**：不是"新建一个空物品"，而是**拿原版工厂当骨架**再全字段覆写。

```csharp
// ItemFactory.cs:11-23
if (!DirectoryMaster.Has<GameItem>(definition.NativeTemplate))
    throw new InvalidOperationException("缺少物品原生骨架：" + definition.NativeTemplate);
GameItem val = DirectoryMaster.Item(definition.NativeTemplate, definition.NativeNew) ?? throw ...;
definition.InitializeData(val, values);   // 逐字段覆写：名字/描述/价值/形状/贴图/类型/标签/音效
```

`NativeTemplate` 全库只有 5 个取值：`common_electronic`（12 处）、`handnote`（2）、`machine_bay`、`water_guide`、`sign_food`。好处是**原版字段初始化一个都不会漏**（价格、`GridShape`、`soundContainerOpen`、`itemFeatures` 全套现成）；坏处是每加一个新物品都要在 `InitializeData` 里手工覆写十几个字段——`ComponentItemDefinitions.ApplyWhole` 一个函数就 80 行（`ComponentItemDefinitions.cs:122-201`），这解释了为什么"1,592 行只够 90 个物品"。

**注册时机**：`ModHook.OnModItemDirectoryInit` → `ItemCatalog.Register(directory)`，逐 key 做 `Directory.Set(id, factory)` 并**立刻回读校验**（`directory.factoryDictionary.TryGetValue(id) == factory`），任一步不符就抛，并逆序回滚已登记的条目（`ItemCatalog.cs:36-70`）。

### 3.2 三块"新区域"：**不是地图地点，是店内模态的自绘全屏场景**

全库 **0 处** `AddAfterhourLocation` / 地点注册 / `PlaceInventor`；`AfterhourLocation` 只作为参数类型出现在一个"离开店铺就打断当前操作"的补丁里（`CashRepairLeavePatch.cs:15`）。真正的做法：

| 层 | 实现 | 证据 |
| --- | --- | --- |
| 入口 | 在店铺视口 `Rect(836,380)` 画一个 48px 高的**钥匙按钮**，贴图是内嵌 128×128 PNG，**按 alpha 通道扫描算包围盒再 `Sprite.Create`** | `StoreEntryKey.cs:39-80` |
| 场景 | 自建 `Canvas`，1280×720，**sortingOrder 32600**（黑幕 32700），三块区域各有自己的 Canvas/root | `WorkroomView.cs:14-25,31-33` |
| 房间间跳转 | 在工作间里再放两把钥匙（机房 `Rect(426,180)`、收藏室 `Rect(308,250)`），点进去切 root 可见性 | `WorkroomView.cs:724-725` |
| 进入条件 | 要求 `playerLocation == 0`（在店内）、非地图、非运输、非拾荒面板激活 | `WorkroomNativeContext.cs:38-45` |
| 与原版隔离 | 持有"输入租约"：抢 `EventSystem`、置原生 `controls`/`hitTargets` 标志、把店铺 root 加进 `shopRoots` 集合隐藏；**期间拦截所有 `PixelWindow.Show`** | `WorkroomInputLease.cs:686-700`、`WorkroomShopWindowPatch.cs:26-36` |

**结论**：三块区域在世界上不存在，也不是 `AfterhourLocation`。它们是**一个模态子应用**，代价是要自己实现"焦点、输入、渲染顺序、退出、与原版 UI 互斥"。收益是不用碰地图/存档结构，卸载即消失。

### 3.3 拆 / 修 / 装：**快照 → 销毁 → 还原**，不是改状态，也不是简单的销毁重建

物品的"耐久/损坏"**完全自建**，不碰原版 `Condition`（全库只有 `NativeItemCodec` 在快照里"顺带搬运"原版的 `ItemCondition`，`NativeItemCodec.cs:2072-2091`）：

```csharp
// WorkroomComponentParts.cs:35-53 —— 每个零件身上挂一段版本化 JSON
internal sealed class PartInfo {
    public int Version { get; set; }      // =3
    public string Family { get; set; }    // gpu/ram/ssd/psu/fan/cooler
    public string Kind { get; set; }      // body/core/memory/...
    public int Tier { get; set; }
    public int CapacityGB { get; set; }
    public long Intact { get; set; }      // 完好价
    public long Current { get; set; }     // 现值（破损 = ×0.2）
    public bool Broken { get; set; }
}
// 落点：item.state 的同名 tag，值是 JSON 字符串
item.EnableTag("PCREPAIR_COMPONENT_PART_STATE_V1", false);
item.state.GetTag("PCREPAIR_COMPONENT_PART_STATE_V1").SetString(Json.Serialize(info));
```

三条主链（全部走"先在离屏副本上改，再整篇替换存档文档"）：

| 动作 | 机制 | 关键代码 |
| --- | --- | --- |
| **拆机** | 为每个计划中的零件 `ItemSetup.CreateNew()` 造**新物品** → `Capture` 成快照 → 立刻 `Destroy` 临时体；随后把"剩余记录 + 新零件记录"合成一份新文档，`PrepareExternalReplacement` → `ApplyExternalPublication` 一次性发布 | `ComponentAssemblyAdapter.cs:83-146` |
| **装配** | 从主体零件里读出 `PCREPAIR_COMPONENT_ORIGIN_V2`（**整机原快照**）→ `Restore` 回同一件原物 → `ApplyWhole(plan.ProductIdentifier)` → 把每件零件的快照数组写进 `PCREPAIR_COMPONENT_COMPOSITION_V3` | `ComponentAssemblyAdapter.cs:427-490` |
| **修复** | **就地重贴定义**：`ApplyWhole(root, plan.IntactId)` 换 `identifier`，再把旧快照的标量/tag/特性逐项盖回去，最后逐字段断言 `Identifier/Broken/Value/Count/Orientation/Shape/Identities` 全部吻合，否则整笔作废 | `MachineRepairAdapter.cs:303-345` |

**"修复条件卡在哪"**：三处硬闸——① 上锁或有内容的本体必须先掏空（`MachineRepairAdapter.cs:313-316`）；② 材料清单是 6 种原版材料 + `energy_credit`，且模组**反向封锁了原版的"拖材料修物品"通道**（对有破损配件的目标直接 `MayTarget/CanTarget/Target` 返回 false，`ComponentRepair.cs:43-68`），逼你回工作间；③ 高阶机箱要先解锁（`HighTierCaseUnlocked`，`MachineRepairAdapter.cs:19-28`）。

### 3.4 "因为你开的这门生意才来敲门的人"：**招牌物品 + 往原生来客池里插工厂**

这是全档案最值得抄的一段。它不写"每天 X% 概率来客"，而是：

1. 放一件 `pcrepair.sign_computer` 到展柜；
2. 每次原版要造随机来客前（`StoreClientManager.GenerateClient()` 的 Prefix），检查"招牌今天是否在展柜"，是就 `pool.Add(factory, w1, w2, w3)`，不是就 `pool.entries.Remove(...)`；
3. **权重不是自己编的**：遍历池里所有 `Method.Name == "CreateLowerLevelChemist"` 的原生条目，把 `tierWeights` 求和当作自己的权重——**分发曲线和原版同档位来客完全一致**；
4. 来客身份造好后，`StoreClientListDict.CreateStoreClient(identifier)` 的 Prefix 把 `pcrepair.repair_*` 这类 id 换成自己的工厂（`new StoreClient` 的克隆 + 换立绘 + 打 tag）；
5. 到店瞬间 `StoreUIManager.OnNextClientArrived` 的 Postfix 补货（"他要的破件"）。

```csharp
// FactionCommerce.cs:54-99（节选）
internal static void Register(RandomClientPool pool, Func<StoreClient> factory, string reference)
{
    int day = StoreStation.GetDayCounter();
    bool on = SignDisplayed(PlayerStore.Instance, day) && FactionCommerceRules.Available(pool.factionId, day);
    Entry val = pool.entries.FirstOrDefault(e => (Delegate?)e.create == (Delegate?)factory);
    if (!on) { if (val != null) pool.entries.Remove(val); return; }      // 招牌撤了 → 从池里摘掉
    if (val != null) return;                                             // 幂等，不重复插
    int[] w = new int[3]; bool found = false;
    foreach (Entry e in pool.entries)
        if (e.create?.Method.Name == reference) {                        // 抄原生参照条目的分档权重
            for (int i = 0; i < 3; i++) w[i] += e.tierWeights[i];
            found = true;
        }
    if (!found) throw new InvalidOperationException("随机来客缺少原生权重参照。");
    pool.Add(factory, w[0], w[1], w[2]);
}
// 招牌状态存在 modData 里，带"-"前缀表示"今天没摆"
// FactionCommerce.cs:13-25：modData["pcrepair.computer_sign.open_day"] = day / "-" + day
```

同一个杠杆还被复用到**电话**：`pcrepair.phone_assembler` 的号码硬编码 `504`，`UnlockKey`/`LastScheduledDayKey`/`LastVisitedDayKey` 三个 `modData` 键控制解锁与排期（`AssemblerClients.cs:28-34`）。

### 3.5 上层区装配工坊的采购线：**不是商店，是原版"市场委托"**

`pc_market_part_request_upper_requester` = "装配工坊"（`Localization.zh.csv`）。它走原版 `StoreRequest`（柜台委托/悬赏）：

- `StartDay` 的 Postfix 里发布自己的委托（`MarketPartRequests.cs:49-56`）；
- `CanThisItemFillRequest` / `CanFillRequest` 的 Postfix 只在自己的委托上改判（`IsOurs` 守卫，`MarketPartRequests.cs:58-83`）；
- `FillRequest` 前置校验 + `DispenseReward` 用 **Transpiler 替换一个 call**，把声望结算换成自己的（`MarketPartRequests.cs:16-45`）；
- UI 不出新的：`StoreRequestElement.Init` 的 Postfix 只改文案（`MarketPartRequests.cs:84-116`）。

另外三条供给线也全是"寄生原生入口"：`NegociationUIManager.OnWholesaleClick`（批发点击）+ `PlayerStore.BuyItem/SellItem/OnItemBought` 的 Prefix/Postfix 组（`ProcurementHooks.cs:11-33,137-200`、`ProductionSupplyHooks.cs:109-170`、`ServerOrderAdapter.cs:9-70`），以及 `PlayerStore.OnShutterOpened`（开张）触发补货。**没有自建商店 UI，只有自建的"工作间内的取放/装配面板"。**

### 3.6 状态落盘：**一个键、一篇 JSON、版本号 + epoch/revision**

```csharp
// FormalStorage.cs:13 / 196-212（ILSpy 的 (JToken) 强转已去掉）
internal const string Key = "pcrepair.workroom.storage.v2";
...
JObject val = StorageDocumentCodec.WriteDocument(ManagedView(), WorkroomStorageState.ValidateFormal);
if (val2.modData == null) val2.modData = new Dictionary<string, string>();
owner.modData["pcrepair.workroom.storage.v2"] = val.ToString(Formatting.None, Array.Empty<JsonConverter>());
```

文档骨架（`FormalStorage.cs:281-310`）只有三块：`Items[]`（工作间保管的所有物品快照）、`Collection.{Entries[],Progress{...}}`（收藏室账本/任务/币/天数）。**整包 21 个 `modData` 键**里，只有这一个是大文档，其余是标量（招牌日、电话解锁、上次排期、故事进度）。

防坏档的五个机制（都可独立复用）：`Version` 字段 + 读取时 `Version != 2` 直接判废（`NativeItemCodec.cs:730-733`）；`Epoch`（绑定/解绑存档会话各 +1，`WorkroomStorageState.cs:395,411`）与 `Revision`（每次写入 +1）双计数，任何异步回调回来时先比对，不等就放弃（`FormalStorage.cs:24-47`）；`Pending` 闸（文档里有 `cleanup`/`delivery` 记录就拒绝新事务）；`Prepare→Apply` 两段式发布（`FormalStoragePublication` 票据）；以及**快照里硬编码原版指纹** `NativeTarget = "DD313F73…F9152"`，不匹配就拒读（`NativeItemCodec.cs:208,732`）。

---

## 4. 关键代码摘录

### 4.1 把 90 个物品写成 10 行：品类 × 型号表

```csharp
// ComponentCatalog.cs:87-97 —— 唯一的事实源，其余全部由它推导
internal static readonly Type[] All = new Type[9]
{
    new Type("computer_case", "PCREPAIR_CASE", 4, 4, true,  "MATREXX 30", "NZXT H5", "Corsair 4000D", "Lian Li O11 EVO", "Corsair 1000D"),
    new Type("component_psu", "PCREPAIR_PSU", 2, 2, false,    "MWE 450", "Corsair CX650", "Seasonic GX-750", "Corsair RM850x", "Dark Power 13"),
    new Type("component_motherboard", "PCREPAIR_MOTHERBOARD", 4, 4, false, "MSI H610M-G", "ASUS TUF B760", "MSI Z790 Tomahawk", "A-SUS R0G Z790-E", "ASUS R0G Z890 Hero"),
    new Type("component_hdd", "PCREPAIR_HDD", 3, 1, false,    "SSD-1 256GB", "SSD-2 512GB", "SSD-3 1TB", "SSD-4 2TB", "SSD-5 4TB"),
    new Type("component_ram", "PCREPAIR_RAM", 1, 3, false,    "Kingston Beast 8GB", "Corsair Vengeance 16GB", "Kingston Renegade 32GB", "G.SKILL Trident Z5 48GB", "Corsair Dominator 64GB"),
    new Type("component_gpu", "PCREPAIR_GPU", 2, 4, false,    "rux 3050", "rux 4060", "rux 4070", "rux 4080 SUPER", "rux 5090"),
    new Type("component_fan", "PCREPAIR_FAN", 2, 2, false,    "ARCTIC P12", "CM Mobius 120", "be quiet! Silent Wings 4", "Noctua A12x25", "Corsair QX120"),
    new Type("component_cpu", "PCREPAIR_CPU", 2, 2, false,    "Core i3-10100F", "Core i5-12400F", "Core i7-13700K", "Core i9-14900K", "Core Ultra 9 285K"),
    new Type("component_cooler", "PCREPAIR_COOLER", 2, 2, false, "DeepCool AG400", "Thermalright PA120 SE", "Noctua NH-D15", "NZXT Kraken 360", "Corsair TITAN 360 LCD")
};
// 构造器里直接断言：tierModels.Length != 5 就抛（ComponentCatalog.cs:24-29）
```

**为什么这么写**：型号名是**卖点**不是数值，硬编码成 45 个字符串最省事；而尺寸/档位/tag 是可推导的，用构造器断言把"必须 5 档"变成编译期错误。代价是 **加一个品类要改 4 处**（这里、`TierTags`、`ComponentRecipes`、`IsHighEndEligible`），删一个品类要改更多——这就是 12 万行的另一半来源。

### 4.2 拆机是一笔事务，不是一次删除

```csharp
// ComponentAssemblyAdapter.cs:83-146（节选，去掉 IL 噪声）
public bool Commit()
{
    if (attempted) throw new InvalidOperationException("拆装命令不能重放。");  // 票据不可重放
    attempted = true;
    if (!Current()) return false;                       // ① 会话/版本闸

    List<Snapshot> list = new();
    if (plan.Mode == ComponentAssemblyMode.Split) {
        if (plan.ExistingComposition) list.AddRange(facts.Source.Nodes[0].Composition);
        else foreach (PlannedComponentPart p in plan.CreatedParts) {
            list.Add(CreateSplitPart(p, facts.Source)); // ② 造新物品→拍快照→销毁临时体
            if (!NativeCurrent()) return false;         // ③ 每一步后重新验会话
        }
    } else { var s = AssembleNative(..., NativeCurrent); if (s == null) return false; list.Add(s); }

    if (!Current()) return false;
    if (plan.Mode == Split && !plan.ExistingComposition) WorkroomComponentParts.ValidateComposition(list);

    List<Record> next = Remainders(facts);              // ④ 原物扣掉
    if (!WorkroomStorageState.PlaceAll(next, list.Select(s => new Record { Item = s }))) {
        Refusal = "full"; return false;                 // ⑤ 放不下就整笔放弃，不半途而废
    }
    proof = adapter.driver.Storage.PrepareExternalReplacement(next, facts.Context.Epoch, facts.Context.Revision);
    if (!Current()) return false;                       // ⑥ 发布前最后一次验
    adapter.driver.Storage.ApplyExternalPublication(proof);
    BusinessRuntime.Wake();
    return true;
}
```

**为什么这么写**：拆机天然是"1 → N"的爆炸性操作，任何一步失败都会造成物品凭空增减。作者把整个动作收成**一份文档替换**——要么全成功，要么一个字节都没变。代价是每加一个操作就要写一套 `Facts/Command/Plan/Proof/Outcome`（这也是文件数 595 的根因）。

### 4.3 离场物品怎么活下来：把原版 `SaveManager` 借来当序列化器

```csharp
// NativeItemCodec.cs:142-182 —— 借静态容器，静音副作用
private sealed class HeapLease : IDisposable
{
    private readonly object? heap = Heap.GetValue(null);   // SaveManager.itemHeap
    private readonly object? dict = Dict.GetValue(null);   // SaveManager.itemDict
    private readonly object? uuid = Uuid.GetValue(null);   // SaveManager.currentUUID
    private readonly string json = SaveManager.saveJson;
    private readonly PlayerStore owner;
    private readonly List<ItemFeature> features;
    internal HeapLease(PlayerStore store)
    {
        owner = store; features = store.savedItemFeatureList;
        try {
            Heap.SetValue(null, new Dictionary<GameItem, long>());  // 换一张干净的
            Dict.SetValue(null, new Dictionary<long, GameItem>());
            Uuid.SetValue(null, 0L);
            SaveManager.saveJson = "";
            owner.savedItemFeatureList = new List<ItemFeature>();
        } catch { Dispose(); throw; }                            // 半途失败也要还原
    }
    public void Dispose() { /* 四个字段逐一还原 */ }
}
// 真正的编解码直接用原版静态方法（SaveManager.EncodeNodes / DecodeNodes，:322 / :464）
```

**为什么这么写**：物品的"完整身份"散落在 `uniqueId`、`itemShape`、`itemTypes`、`state`/`modifiedState` 的两级 tag、`itemFeatures`（含 `realCondition`/`fakeCondition`）、`effects` 十几处，自己写序列化必然漏字段、必然在版本更新后读歪。**借原版自己的 `EncodeNodes` 拿 `SaveItemNode[]`，再用 `JsonUtility` 序列化**，等于永久免费跟随原版字段演进。风险是全部依赖私有静态字段（`itemHeap`/`itemDict`/`currentUUID`/`TagState.internalValueString`），原版一改名就崩——作者用 `RequiredField` 包装，让"找不到"变成明确的启动期崩溃而不是运行期错数据。

---

## 5. 踩坑与红线

1. **271 个 `[HarmonyPatch]`、9 个文件用 IL Transpiler**（`MarketPartRequests`、`MarketQuoteHooks`、`LuoxiPurchasePricing`、`CollectionCrimeEffects`、`CollectionCraftingProtection`、`ProductionNightHooks`、`ProductionMachineTooltip`、`StartingMerchant`、`NativeSaveHooks`、`NativeUiHooks`）。它的自保手段是**数调用点**：`if (sites != 1) throw new InvalidOperationException("原生委托声望结算入口变化，停止接入。")`（`MarketPartRequests.cs:40-44`）——宁可整模组不加载，也不带病改数值。
2. **同一个原生方法被它自己 Patch 很多次**：`StoreClientListDict.CreateStoreClient` **12 处**、`PlayerStore.OnItemBought` 6 处、`PlayerStore.BuyItem` 5 处、`StoreClient.IsClientRefusingItem` 5 处、`NegociationUIManager.OnWholesaleClick` 4 处。每个都靠 `identifier` 前缀判断「是不是我的客人」再 `return false` 抢结果——**这在 Harmony 里是一条隐式链，任何第三方前缀先 `return false` 就会静默废掉它全部 12 个来客工厂**。我们要跟它共存，必须让自己**永不返回 false**，只用 Postfix 或改引用参数。
3. **补丁归属自检**：启动后遍历 `Harmony.GetAllPatchedMethods()`，要求"本程序集的所有补丁必须属于同一个 Harmony owner id"，否则抛（`ModEnvironment.cs:400-413`）。这能第一时间抓出"另一个模组/加载器抢注了我们的补丁"。
4. **`PixelWindow.Show` 被全量拦截**（`WorkroomShopWindowPatch.cs:26-36`）。工作间打开期间，**任何模组**（包括我们的）调 `PixelWindow.Show()` 都会拿到"原样返回但不显示"。这是本包最硬的跨模组红线。
5. **输入租约 + 商店区域隔离**（`WorkroomInputLease.cs`，816 行）：抢 `EventSystem`、改原生 `controls`/`hitTargets`、把店铺 root 逐个 `SetActive(false)`。任何也依赖"店铺里鼠标射线"的模组会在这里被静默吃掉输入。
6. **`NativeTarget` 指纹是硬编码常量**（`NativeItemCodec.cs:208`）。跨版本读档能不能活，取决于作者有没有重算这个 hash；`manifest.xml` 声称同时支持 `0.46D` 与 `1.0.0.16`，说明两版的原版布局一致——（推测）作者验过，但一旦不一致就是**读档即拒**，而不是静默读歪。
7. **无 `HotReloadPrepare/Activate`**（透视报告已确认）。全模组的静态引用只靠 `OnGameLoadedInit/Late` 成对重置，热重载会让 `WorkroomDriver.Current` 指向已销毁的 Canvas——**别把它当可热更模组对待**。
8. **异常一律中文串直接抛**，且大量出现在 `Commit()` 这种会被 UI 调用的路径上（例：`"拆装命令不能重放。"`、`"修后本体、形状、身份或价值不符合原记录。"`）。好处是日志可读，坏处是任何一次边界时序错误都会变成**红字刷屏**，而且玩家侧看到的是异常而非降级。
9. **单文件 7,005 行的 `ServerRoomView`**：一个类里同时是"假浏览器 chrome + 图表 + 商品卡 + 钱包 + 任务栏 + 详情弹层"。它的 `PanelMode` 只有 4 个值（`Computer/Catalog/Rack/Production`），说明是**四页硬编码在同一文件**里靠 `SetActive` 切换。这是我们明确要避开的形态。

---

## 6. 对我们的可用点

### 6.1 →《深空电玩》（`deepspace_arcade` / `DeepSpaceArcade.dll`）

| # | 可用点 | 落到哪个现有功能 | 怎么做 |
| --- | --- | --- | --- |
| A1 | **招牌物品驱动来客概率**（§3.4 全套） | 展柜摆货识别 `ARC_GAME_GOODS`/`ARC_HARDWARE_GOODS` 与 `arc_gamer`/`arc_ul_game_collector`/`arc_hardware_nerd` 三派客 | 把"摆了什么货"从"给展示柜打标记"升级成"往 `RandomClientPool` 插/摘工厂条目"，权重照抄 `CreateLowerLevelChemist` 一类原生参照条目，得到与原版同档位的到店曲线。现在我们的做法是硬概率，扩大品类时会失衡 |
| A2 | **物品离场寄存 + 原版编解码**（§4.3） | 机台库存、盒装光盘堆存、`ArcMarket` 的跨日货架 | `SaveManager.EncodeNodes` + `HeapLease` 静音，可以把"未上架库存"整批搬出世界当纯数据存，和现有 ES3 写盘剥离天然配套（都是"不碰原版存档结构"的思路） |
| A3 | **用 `ItemFeature` 打加价标记，而不是在每个价格调用点改数**（`HighEndPricing.cs:42-58`：三处 Prefix 只调幂等的 `EnsureMarkup`，真正倍率存在私有 feature `pcrepair.high_end_markup` = 20%） | 翻新/盗版/稀有品的定价 | 我们现在散落在 `GetNegociatedValue`/`GetCurrentValue` 各处的倍率可以收成一个 feature + 一个 `EnsureMarkup`，补丁只剩"确保打好标记"三行，而且 feature 会随快照一起存盘、随物品一起流转 |
| A4 | **编译期销号的性能探针**（§2.4.3） | 门禁里的性能巡检 | 保留 `PerformanceTrace.Begin(Part.X)` 调用点，发布构建让它 `return default`。文档价值极高（枚举名就是热点清单），运行成本为零 |
| A5 | **补丁归属自检**（§5.2） | 与《深空当铺：可能是群友》和第三方模组共存 | 在 `OnEnable` 末尾加一次"本程序集补丁 owner 唯一"断言，冲突扫描之外多一道运行期保险 |

### 6.2 →《深空当铺：可能是群友》（`freya_cornell` / `DeepSpacePawn.dll`）

| # | 可用点 | 落到哪个现有功能 | 怎么做 |
| --- | --- | --- | --- |
| B1 | **电话三键模型**（`pcrepair.phone_assembler.unlocked` / `.last_scheduled_day` / `.last_visited_day` + 硬编码号码 504，`AssemblerClients.cs:28-34`） | 伞萝萝 `7721` / 塞壬 `7734` 的"拨通约次日、7 天 CD" | 我们目前 CD 与排期混在一个状态里；拆成"解锁 / 上次排期 / 上次到访"三键后，读档回放、早退、重入都变成纯幂等判断，不必再写特例 |
| B2 | **`StoreClientList.CreateUpperLowerVisitor()` 当模板克隆**（`AssemblerClients.cs:59-80`：先拿原版访客，再改 `identifier`/`displayName`/立绘/打 tag） | `DspCharacterProfile` / `DspVisitorKit` 的散客脚手架 | 我们自建 `StoreClient` 时要手填十几个字段；改成"克隆一个原生访客再覆写"，能自动继承原版的分档、拒绝规则、对话挂点。这正好是 `EXTENDING.md` 里那层脚手架的现成实现 |
| B3 | **展示柜 gate + 原生权重复制**（同 A1，`FactionCommerce.cs:54-99`） | `ShowcaseHelper.GetDisplayCasePrediction` 的"定向吸引"（纯水/点心 35%） | 我们现在的 35% 是自定常数；若能改成"插入/摘除池条目 + 抄原生权重"，展示柜预测 UI 和实际派客会**天然一致**，也不会因为加角色而破坏原版曲线 |
| B4 | **单键 JSON 文档 + `Version` + `Epoch/Revision` 双计数 + `Pending` 闸**（§3.6） | `DspState` 多角色状态、`DspVisitorKit` 的到访记录 | 把多角色的到访/好感/剧情进度合并成**一篇带 Version 的文档**，所有异步回调（对话结束、电话挂断、离场）回来时先比 `Epoch/Revision`；这是"卸载零污染 + 坏档免疫"最省的写法 |
| B5 | **`NativeItemCodec` 的指纹与拒读**（§3.6 最后一条） | 跨版本读档（工坊包要长期支持 0.46D / 1.0.0.16） | 在我们的持久化文档里加一个"原生版本指纹"字段，不匹配就**拒读并降级到空状态**，而不是硬读出一堆歪字段。代价是每次游戏更新要重算一次常量——比坏档便宜 |

---

## 附录 A 证据索引（可复核）

| 结论 | 文件:行 |
| --- | --- |
| 身份 / 版本 / 双目标 / 空能力 | `manifest.xml:2-11` |
| 595 文件 123,278 行；94% 挤在一个 namespace | 全库 `wc -l` 聚合（§2.1 表） |
| 9 品类 × 5 型号 / 5 档 tag / 45 型号断言 | `ComponentCatalog.cs:87-97`、`:24-29`、`:81` |
| 90 整件 + 82 零件 = 219 个物品 id | `ComponentCatalog.cs:109-124`、`ComponentRecipes.cs:62-82`、`ItemSetup.cs:41-60` |
| 原版工厂当骨架 | `ItemFactory.cs:11-23`、`HardwareItemDefinitions.cs:20-34` |
| 注册 + 回读校验 + 逆序回滚 | `ItemCatalog.cs:28-70`、`ModEnvironment.cs:365-369` |
| 三块区域 = 自绘 Canvas / PNG alpha 按钮 / order 32600 | `WorkroomView.cs:14-25,31-33,724-725`、`StoreEntryKey.cs:39-80` |
| 店内模态的准入条件 | `WorkroomNativeContext.cs:38-45` |
| 输入租约 / 窗口拦截 | `WorkroomInputLease.cs:686-700`、`WorkroomShopWindowPatch.cs:26-36` |
| 零件状态 V3（自建耐久，不用 Condition） | `WorkroomComponentParts.cs:35-53,55-68,157-163` |
| 拆机 = 造新 + 拍快照 + 文档整篇替换 | `ComponentAssemblyAdapter.cs:83-146,407-427` |
| 装配 = 从 `ORIGIN_V2` 还原原机 + 写 `COMPOSITION_V3` | `ComponentAssemblyAdapter.cs:427-490` |
| 修复 = 就地 `ApplyWhole` + 逐字段断言 | `MachineRepairAdapter.cs:303-345`、`ComponentItemDefinitions.cs:122-201` |
| 反向封锁原版"拖材料修" | `ComponentRepair.cs:43-68` |
| 招牌 gate + 抄原生权重 + 动态增删池条目 | `FactionCommerce.cs:13-25,54-99` |
| 克隆原生访客 / 电话三键 | `AssemblerClients.cs:28-34,59-80`、`RepairVisitorHooks.cs:9-28` |
| 委托线寄生原生 `StoreRequest`（含 Transpiler 数点） | `MarketPartRequests.cs:16-45,49-56,58-83,84-116` |
| 单键 JSON 存档 + 文档骨架 | `FormalStorage.cs:13,196-212,230-310` |
| 借 `SaveManager` 编解码 + 静音 | `NativeItemCodec.cs:142-182,269-322,436-464` |
| 硬编码原生指纹，不匹配拒读 | `NativeItemCodec.cs:208,732` |
| 21 个 `modData` 键 | 全库 `modData["..."]` 正则统计 |
| 补丁 owner 唯一自检 | `ModEnvironment.cs:400-413` |
| 探针编译期销号 | `PerformanceTrace.cs:42-59` |
| 定价走私有 `ItemFeature` | `HighEndPricing.cs:42-58`、`ComponentNative.cs:44-48` |

## 附录 B 未验证 / 推测项

* **`NativeTarget` 哈希的来源**：源码里是常量，无计算过程。推测为作者对受支持版本 `Assembly-CSharp.dll` 算的指纹（本机 `725CB999…` 与它不同，故不是当前宿主 DLL 的 SHA-256）→（推测）。
* **型号名是否有版权规避意图**（`rux 3050`、`A-SUS R0G`）：仅见字符串，不做推断。
* **真机帧耗时 / 内存**：`PerformanceTrace` 是空壳，仓库内无 profile 数据；§5 关于输入租约与 7,005 行视图的代价判断来自代码结构，**未实测**。
* **`FormalR02` 的"R02"含义**：只有目录名与 `FormalStorage`/`FormalStoragePublication` 的命名佐证，推测为"第二轮正式存档格式重写"，无历史可证。
* **14 个 `*Codec` 的向前兼容策略**：只抽查了 `WorkroomItemCodec`（`Version` 必等于 3，否则抛）与 `NativeItemCodec`（`Version != 2` 判废），其余是否都做迁移**未逐一核对**。
