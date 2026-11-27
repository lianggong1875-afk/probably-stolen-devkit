# 深空罗德岛-可能是制药公司｜3830995342

- **id**：`3830995342` ｜ 包内 ID `rhodes_island`，v1.0.0，`<Capabilities />` 自闭合，单程序集 `RhodesIsland.dll`（489 KB）
- **反编译目录**：`缓存文件夹\decomp_ws\3830995342\RhodesIsland\`（119 文件 / 19,476 行 / 包内 15 张贴图 + 10 本 `Books\*.json` + `Data\content.json`）
- **为什么值得读**：① 全库唯一"**内容全在一张 47 KB JSON 表里**"的样本——43 物品只花 259 行 C# spec 类；② 给了"**不替换化学主循环也能做化学产业链**"的模板：只在 `HandleTemperatureReaction` 挂 Postfix 追加自己的反应表，机器全用原版；③ 自带一台 6,964 行的**验收台**（复制存档槽→重载→断言→JSON 报告→`runtime-command.json` 文件 IPC），是"真机只进测试档"的程序化版本，可直接搬进我们第 ④ 道门禁。

> 反编译的 `val/flag/num` 临时名与 `//IL_xxxx` 注释已删。原版 API 行号以 `共享开发\reference\Assembly-CSharp\` 为准，模组侧以反编译目录为准。追不到源的标「（推测）」。
>
> 与 [机器自动化三例](机器自动化三例-自学习增强·全自动制药·养鼠工厂.md) ②「全自动制药机」、[液体系统三例](液体系统三例-扫描仪·水商·水龙头.md)、[03-原版接口速查](../03-原版接口速查.md)「化学模拟：360 tick 与温度模型」互补：那三份讲"**怎么操作原版化学**"，这份讲"**怎么不操作、只加表**"。

---

## 1. 规模与结构

**19,476 行没有一个"大文件"**：最大文件 `SkadiRoute.cs` 3,593 行也只占 18.4%，`ReleaseGate.cs` 2,353 行占 12.1%，其余 117 文件全部 ≤1,124 行。除 `ReleaseGate`/`ReleaseSmoke` 外，单文件平均只有 137 行。

| 桶 | 行 | 占比 | 职责 | 主要文件 |
|---|---:|---:|---|---|
| **F 自检/验收** | 6,964 | **35.8%** | 游戏内自动验收、场景/存档断言、JSON 报告、文件 IPC 驱动 | `ReleaseGate` 2353、`ReleaseSmoke` 1063、`AcceptanceAssistant` 725、`WorkkitChecks` 439、`FeedbackChecks` 391、`GuideChecks` 377、`SugarPackingChecks` 270、`RuntimeControl` 341、`IndustrialChecks` 188、`EconomyChecks` 173… |
| **D 斯卡蒂线/住客** | 4,161 | 21.4% | 12 场主事件（`Titles[12]`）+ 3 个结局（住下 / 回罗德岛寄信 / 转卖旧合影后离开）+ 旧合影寄卖/赎回/转卖 + "住下"生活面板 + 座位/立绘 | `SkadiRoute` 3593、`SkadiShopSeat` 289 |
| **E 界面/场景** | 3,501 | 18.0% | 救护站自定义场景、加工包面板、夜间地点、笔记窗口、电话、名片 | `AidStation` 1124、`WorkkitUI` 847、`CooperationPhone` 228、`AidStationSceneView` 186、`GuideFolderWindow` 147、`QuarryRules` 122、`QuarryLocation` 119 |
| **A 入口/内容装载** | 2,347 | 12.1% | `IMod` 入口、目录解析/校验、物品工厂、13 个 `[Serializable]` spec | `ModEntry` 840、`ContentRuntime` 740、`ContentCatalog` 358、`ContentRules` 97、spec 类合计 259 |
| **C 采购/商业** | 1,399 | 7.2% | 委托单适配、供货/收购策略、声望曲线、验货扣分、商人定制 | `SupplyPolicy` 241、`CooperationPolicy` 221、`NativeRequests` 192、`ProcurementAudit` 173、`MedicalProcurement` 80 |
| **B 生产/化学** | 1,104 | 5.7% | 纯度/成色、配方规格、溶解拆件、封装机补丁、熔炉、笔记教学 | `ProductionQuality` 277、`IndustrialMaterials` 195、`RecipeTeaching` 119、`FurnaceBinding` 116、`SugarPacking` 64、`ProductionSplit` 48、`NativeFinisher` 43 |
| 合计 | 19,476 | 100% | **真逻辑（B+C）= 2,503 行 = 12.9%** | |

**判断：不属于已知四种，是第五种——"自带验收台的内容包"。**
- 不是 `PCExpansion` 型（12.3 万里真逻辑 6%）：那边大头是数据/表格填充，这边大头是**可执行的测试代码**。
- 不是 `GoFishing` 型（单文件 24.6%）：这边最大文件只有 18.4%，且没有任何文件独占一类逻辑。
- 不是 `Perks` 型（41% 括号行）：`grep //IL_` 全仓只有 567 行 = **2.9%** 反编译噪声，反编译质量干净。
- 不是 `AIEmporium` 型（18.5% 借来的库）：无第三方库，只有原版 API + Newtonsoft.Json。
- 真实形态：**36% 的代码在给另外 64% 兜底**，而兜底压力最大的正是 B+C 那 12.9% 的配方与商业判定（配方一改，水培→提取→批次→封装→交货整条链就变）。作者把"每次都要手点一遍"这件事，写成了一台能在游戏里跑的回归测试台。注意 D 桶里也混着斯卡蒂线的自检（`CheckPhotoResale`/`CheckPhotoDelivery`/`CheckAtomicCapabilities`），所以"业务代码"实际比 64% 更少。

---

## 2. 它怎么做这件事

### 2.0 一句话：不是"玩家开药厂"，是"当铺给罗德岛供货"

包内 `INTRODUCTION.txt` 第一句就是定位：**"在空间站开当铺，顺便给罗德岛供货。"** "制药公司"是《明日方舟》里罗德岛（制药 + 准军事）的设定梗，游戏内**没有任何企业实体**——没有注册公司、没有厂房、没有雇员、没有研发。玩家扮演的仍是当铺老板，只是多了一个叫"罗德岛"的**采购客户**。

### 2.1 核心循环：6 步，每步落在哪个原版系统上

| 步 | 玩家动作 | 落的原版系统 | 模组入口 |
|---|---|---|---|
| ① 种 | 水培槽播 6 种苗（荞花/庭院花/药草/苹果/小麦/甜菜） | `PlantRegistry` + `HydroponicHelper.InitSeedItem` + `PlantGeneticsHelper.ApplyFactoryGenetics` | `ContentRuntime.RegisterSystems`（`:85`） |
| ② 打浆 | 收成物打成 `*_mush` 液体 | 原版搅拌/液体容器 | — |
| ③ 提取 | 加水、控温 45–85 ℃ 出提取物（40% 收率） | `ChemicalHelper.HandleTemperatureReaction`（`ChemicalHelper.cs:242`）的 **Postfix** | `RhodesExtraction` + `ContentRuntime.React`（`:655`） |
| ④ 批次 | 提取物+糖聚块+聚酸酯+水，30–40 ℃ 合成 `ri_sanity_mix` 30,000 mL | 同上，`Catalog.batches` 5 条 | 同上 |
| ⑤ 封装 | 化学收尾机把 30,000 mL 液体压成 `ri_sanity_booster` 实体 | `MachineChemFinisher.ChemFinisher`（`MachineChemFinisher.cs:7`）+ `ChemFinisherRecipe.chemFinisherRecipes` | `NativeFinisher`（Postfix 改输出按钮回调） |
| ⑥ 加工/交货 | 加工包 19 条配方做胶囊/喷雾/抑制剂/滤芯；市场委托交 18 类采购单 | `CraftingRecipe` + `StoreRequest`（`StoreRequest.cs:213/248`） | `ContentRuntime.BuildRecipe`（`:514`）、`NativeRequests` |

旁路两条：**熔炉**（`ri_orirock`→cube→cluster→concentration，3 条 `FurnaceSpec`，复用原版熔炉夜结算）与**夜间采石**（1-7 暴君，复用原版垃圾场搜刮）。

**"公司"层面的抽象只有一条**：`StoreReputation { factionId = "RHODES_ISLAND" }`。6 档阈值 `{20,40,60,80,-40,-80}`（`CooperationPolicy.cs:8`）驱动：采购预算 1200/1600/2000、电话预约冷却 7→3 天、供货货位数 3→7、可否指定补给类别（≥60）、委托解锁（≥60）、-40 只剩可露希尔且加价 25%、-80 永久终止。**没有资金账本**（直接用 `PlayerStore.playerCash` / `ModCash`）、**没有雇工**、**没有产线排程**、**没有研发树**、**没有许可证**。所以它是"小型合作经营"，不是经营模拟。

**tick 只有三处**：
1. `StoreStation.HandleStoreRequests`（每日一次，Postfix → `NativeRequests.Tick`）
2. 容器每秒温度 tick（`ChemicalHelper.containerTempTick`，`ChemicalHelper.cs:24` → `HandleTemperatureReaction` → `React`）
3. `ModHook.OnGenerateCustomerNormal`（来访编排：`CooperationPolicy.ScheduledRole` + `VisitSchedule.RoleAt` 游标 + `QueueFuturClient`）

### 2.2 内容层：一张表，不是每药一个类

`Data\content.json`（47,372 字节，`schemaVersion = 1`）一次声明全部内容，C# 侧只有 13 个 `[Serializable]` 空字段类（合计 259 行）：

| 段 | 条数 | 说明 |
|---|---:|---|
| `items` | 43 | `kind` 分布：material 30 / seed 6 / healing 3 / food 2 / reserved 1 / kit 1 |
| `plants` | 6 | 种苗→收成、生长天数、养分/矿物/水耗 |
| `recipes` | 19 | 加工包配方，`qualityMaterials` 指定哪几味决定成品纯度 |
| `liquids` | 16 | 只给 `id/name/color`，其余字段（沸点 150、不可蒸发）在代码里写死 |
| `reactions` | 5 | 温度窗 + 水比 + 每跳转化 15% + 收率 25–50% |
| `batches` | 5 | 多进多出的化学批次（含 `physical_contaminant` 杂质产出） |
| `finishers` | 9 | 液体→实体的封装规格（体积 + 是否需干燥） |
| `contracts` | 18 | 委托：材料清单、`money`、`reputation`、`cooldownDays`、`minimumReputation`、`recovery` |
| `stock` | 9 | 按 `role` + 声望门槛的供货表 |
| `furnaces` | 3 | 熔炉三段 |
| `guides` | 6 | 制作笔记（PaperUI 书），按 `role` + 声望出售，边读边学 |

**与 `ChemicalHelper` / 品类标签的关系**：物品走原版化学物品路径——`ChemicalHelper.InitChemItem(item, blendable, soluble)` + `SetChemContentUL(item, liquid, volume)`；食物走 `FoodItemHelper`；种苗走 `HydroponicHelper`。**品类全部是原版 `types`**（`CHEMICAL_SUPPLIES` 19 / `SEED` 6 / `MEDICAL` 6 / `HOUSEHOLD_GOOD` 6 / `FOOD` 5 / `MATERIAL` 4 / `TREAT` 1），**不写任何自定义品类**——和我们"只 `EnableTag`，不往 `SetGameItemType` 塞非原版类型"的规矩同向。

反例提醒：`ContentRuntime.cs:266` 在 `kind == "healing"` 里硬写 `item.SetGameItemType("MEDICAL")`——它安全只因为 `MEDICAL` 是原版类型；照抄这个写法前必须先确认类型 id 在原版枚举内。

### 2.3 化学：只挂 Postfix，不碰主循环

- `ChemicalHelper.RunNightlyChemicalSimulation`（`ChemicalHelper.cs:1200`）**全仓 0 引用、0 补丁**。
- 但它挂的 `HandleTemperatureReaction` **就在主循环里**：`RunNightly` 对每个有 `onOneSecondItemFunc` 的物品跑 360 次（`ChemicalHelper.cs:1210`），液体容器的这个 func 就是 `containerTempTick`（`ChemicalHelper.cs:24`）→ 每夜每容器 360 次 `React`。**是"追加"，不是"替换"，但确实在热点里。**
- `React` 自带两级门闩：温度窗（`CONTAINER_TEMP_TAG` 不在窗内直接跳过）+ 数量门（`ContentRules.ReactionAmount` 算出 0 就跳过）。所以常态是每夜 360 次快速返回。
- 小数余量用 **int 标签存定点**：`ri_extract_fraction_<input>`，0–99 表示不足 1 mL 的累积（`ContentRuntime.cs:676-684`）。避免在 `TagState` 里放 float 的精度与序列化问题。

### 2.4 采购单：借 `StoreRequest`，再造一条 `CraftingRecipe` 当交付器

原版 `StoreRequest.CanFillRequest`（`StoreRequest.cs:213`）只认**单一 `requiredItem` + 逐件计数**，且 `FillRequest`（`:248`）对每个命中物品**整件 `Destroy()`**。模组要的是"药 A×3 + 药 B×2 一起交"，于是一手转换：

1. 只填 `requiredItem = materials[0].id`、`requiredItemCount = 1`、`useCustomItemName = true`，把真正的清单塞进**显示用字符串** `requestedItem`。
2. `CanFill` 现场 `new CraftingRecipe(...)` → `AddMaterialIdentifier` 逐条加料 → `CanCraft(全部持有物品)` 当判定器（`CraftingRecipe` 天然支持多物品 + `unitCount` 数量）。
3. `Fill` → `Craft` 消耗 → 对每件被消耗物品反射调 `StoreRequest.CheckDeliveredItem`（保留原版赃物/违禁/错标追踪）→ 移除请求 → `ModCash(spec.money, true)` + 自算声望。
4. 三个 UI/入口补丁把 `duration` 改成"长期采购 · 无截止期限"（`int.MaxValue`）。

**这是"转调原版"的典型样本**：交付**判定**借原版合成引擎，交付**结算**（发钱、发声望）自己复刻。

### 2.5 存档：派系只在内存，落盘只写 ModData

`SaveFaction.cs` 用 `[HarmonyPatch(typeof(PlayerStore), "WriteSlotFile", new Type[] { })]` 的 **Prefix + Finalizer** 成对：写盘前把 `RHODES_ISLAND` 从 `storeReputations` 里**摘掉**（`ModEntry.cs:815`），写盘后按原索引插回（`:832`）。

结果：**原版存档里永远不存在这个派系**，声望只存在 `ModHelper.SetModDataFloat("rhodes_island","reputation")`。读档时 `EnsureFaction()`（`:261`）从 ModData 重建派系 + 6 个 `StoreReputationPerk`（`perkId = RI_COOP_0..5`，`iconName = "Referal"`），三处文案靠 Patch `StoreReputationPerk.GetLocalizedPerkName/Description/FlavorText` 供给，不走本地化表，因此不产生 `[Translation Error]`。

附带好处：退订后存档没有孤儿派系。代价：**模组内任何"派系数据"都不能依赖原版存档字段**，连 `perks[].isUnlocked` 都要靠 `SetReputation` 重算（推测：作者用重设声望触发阈值重算，代码里未见显式回填）。

### 2.6 Patch 靶点表（42 条，全部 Prefix/Postfix，**0 Transpiler**）

| 原版方法 | 类型 | 目的 | 风险 |
|---|---|---|---|
| `ChemicalHelper.HandleTemperatureReaction` | Postfix | 追加 5 条提取反应 + 5 条批次 | **热点**：每容器每夜 360 次；`React` 每批 `new Dictionary` 分配 |
| `ChemicalHelper.DissolveInto` | Prefix | 堆叠化学物只拆 1 件溶解，不整叠化掉 | 条件多（`SOLUBLE_TAG` + 目录内 + 容量够），漏一个就整叠消失 |
| `GameItem.GetSplitItem` | Postfix | 拆件后把液体内容/化学特征/纯度特征逐条复制到新件 | 直接 `JsonConvert` 深拷贝 `ItemFeature`，字段改名即静默丢数据 |
| `MachineChemFinisher.ChemFinisher` | Postfix | 反射取输出按钮 `callback` 重包：容器满则提示、不满才走原逻辑 | **脆弱**：反射私有字段 `MachineryButton.callback` + 按本地化文本 `Single()` 找按钮 |
| `MachineHydroponic.Hydroponic` | Postfix | 水培生长视觉/进度 | 机器重入，需自己判状态 |
| `StoreRequest.CanFillRequest` | Prefix(skip) | 换成合成引擎判定多物品 | 与原版"逐件计数"语义不同，别处调这两个方法会得到不一致结果 |
| `StoreRequest.FillRequest` | Prefix(skip) | 换成 `CraftingRecipe.Craft` + 反射 `CheckDeliveredItem` | **先扣货后结账**：`Craft` 中途抛异常无回滚（只有 `busy` 标志复位） |
| `StoreStation.HandleStoreRequests` | Postfix | 每日刷新/撤回 18 类委托 | 按 03 台账属"过夜路径上的 `Handle*`"，应优先挂 ModHook；此处为加性 Postfix，尚可 |
| `StoreRequestElement.Init` | Postfix | 把"截止期限"改写成"长期采购"，清单换行自适应 | 小字号（8）在长清单上会破版 |
| `StoreReputation.OnItemTraded` ×2 | Postfix | 卖假货 -20 声望、卖低纯医疗 -8 | 依赖 `realCounterfeit` 特征 + 纯度 < 80；原版别的扣分也在同一 Postfix 里，顺序无关但会叠 |
| `StoreReputation.CheckReputation` | Postfix | 声望夹在 [-80, 100] 且 -80 后锁定 | 越界直接改字段，与别的声望模组会互相拉扯 |
| `PlayerStore.WriteSlotFile`（显式 `new Type[]{}`） | Prefix + Finalizer | 写盘前后摘挂自身派系 | 私有方法补丁；异常时 Finalizer 仍插回，但此时盘上已无该派系（这正是目的） |
| `PlayerStore.BuyItem` / `SellItemFromTable` / `SellItem` / `PlacedItemForSelling` | Prefix(存快照)+Postfix | 用 `NativeTrade` 记前后 `playerCash` 差，事后补对话 | 依赖"钱变少 = 买成功"的推断，折扣/退款会算错（推测） |
| `PlayerStore.GetNextClient` / `CanNextClient` / `AddedItemToDisplayCase` / `RemovedItemToDisplayCase` | Postfix | 斯卡蒂来访编排、旧合影上柜/下柜触发 | 顾客队列状态机，重入要自己防 |
| `GameItem.GetNegociatedValue` / `GetCurrentValue` | Postfix | 斯卡蒂线参考价、船板交易价 | 价格链被多家模组改，冲突面最大 |
| `GameItem.GetSplitItem` / `ScavHelper.SpawnDumpingGroundLoot` / `ScavHelper.EventModifier` | Postfix | 采石场掉落换成 `ri_orirock` + 旧货 | 与所有改垃圾场掉落的模组正面冲突 |
| `GeneralHelper.CreateGeneralItemTooltip` | Postfix | 给工业材料加 mL 数值条 | UI 文本拼接，长度溢出 |
| `StoreClientList.CreateInventor` / `StartingPerkContent.HandleNewGamePerk` | Postfix | 商人定制、开局特性"失忆博士" | 单人挂点 |
| 其余 20 条 | Postfix | 名片/电话可见性、`StorePhoneClient` 显示名、立绘 `SpriteDict.GetSprite`、`StoreReputationPerk` 三段文案、`NegociationUIManager.BuildInfoBlock`、`GameItemElement.ValidateSelect`、`ItemMouseDoubleClickHandler.ClickAction`、`CustomUIManager.HandleEscape` / `StoreUIManager.ResolveEscape`（返回键）、`DialogUIManager.DisplayClientText`（验收追踪） | 口径都很小，主要是"让自定义 UI 不误触原版逻辑" |

**热点标注（同时是冲突面最大的四个）**：`ChemicalHelper.HandleTemperatureReaction`（每夜 360×N 容器）、`GameItem.GetNegociatedValue`（价格链）、`ScavHelper.SpawnDumpingGroundLoot`（掉落表）、`PlayerStore.SellItemFromTable`（成交链）。

---

## 3. 关键代码摘录

**① 物品工厂：一个 `ItemSpec` 生成全部原版能力** — `ContentRuntime.cs:202-258`（`Create`，节选）

```csharp
internal static GameItem Create(ItemSpec spec)
{
    Tuple<string, string> tuple = NativeVisual(spec.visual);          // 借一件原版物品的图集/形状
    GameItem item = ItemDirectory.CreateEmptyItem(spec.id);           // 空物品，不是继承新类
    item.SetSpriteAndShape(tuple.Item1, tuple.Item2);
    ApplyIcon(item, spec);                                            // 再盖自己的像素图标 + 占格
    ((GameItemFunc)item).onLoaded += loaded => {                      // 读档钩子恢复私有数据
        loaded.SetValue((long)spec.value);
        ApplyIcon(loaded, spec, loaded: true);
        ApplyDescription(loaded, spec);
        ApplyRockClassification(loaded, spec);
        ProductionQuality.Restore(loaded);
    };
    foreach (string gameItemType in spec.types) item.SetGameItemType(gameItemType);  // 只写原版品类
    if (spec.kind == "seed") {
        HydroponicHelper.InitSeedItem(item, spec.plant);
        PlantGeneticsHelper.ApplyFactoryGenetics(item, spec.plant, new string[0]);
    }
    if (!string.IsNullOrEmpty(spec.liquid)) {
        ChemicalHelper.InitChemItem(item, spec.id != "ri_sugar_pack" && !spec.soluble, spec.soluble);
        ChemicalHelper.SetChemContentUL(item, spec.liquid, spec.volume);
    }
    if (spec.kind == "kit") {                                         // 加工包 = 一台便携合成台
        CraftingRecipeManager manager = CraftingHelper.InitCraftingTable(item);
        NativeCrafting.ProtectInput(item);                            // 只让"自己拥有的"机器收料
        foreach (RecipeSpec recipe in Catalog.recipes) manager.AddRecipe(BuildRecipe(recipe, requireKit: false));
    }
    ModHelper.SetSource(item, "rhodes_island", "罗德岛 · Rhodes Island");
    return item;
}
```

> 关键点：**没有一个药一个类**。43 个物品共用这一条工厂；差异全在 JSON 的 `kind` / `types` / `liquid` / `plant` 字段里。`onLoaded` 挂钩子是个好习惯——和 03 台账"自定义物品要恢复私有数据就挂 `item.onLoaded` 或 Postfix 阶段"一致。

**② 提取反应：在自己的表上跑，用原版同一套标签** — `ContentRuntime.cs:655-726`（`React`，节选）

```csharp
internal static void React(GameItem container)
{
    if (!ModEntry.Enabled || Catalog == null || container == null) return;
    bool changed = false;
    double temp = container.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();   // 原版温度标签
    foreach (ReactionSpec reaction in Catalog.reactions) {
        if (temp < reaction.minimumTemperature || temp > reaction.maximumTemperature) continue;  // 门闩 1
        int amount = Current(container, reaction.input);
        int water  = Current(container, "water");
        int process = ContentRules.ReactionAmount(amount, water, reaction.processPercent, reaction.waterRatio);
        if (process <= 0) continue;                                            // 门闩 2
        changed = true;
        string carryKey = "ri_extract_fraction_" + reaction.input;
        int carry = Math.Max(0, Math.Min(99, container.GetTagReadonly(carryKey).GetInt()));
        long yielded = (long)process * reaction.yieldPercent + carry;           // 整数定点，不用 float
        int output = (int)(yielded / 100);
        container.ModifyTag(carryKey, t => t.SetInt((int)(yielded % 100)), true);
        SetCurrent(container, reaction.input, amount - process);
        SetCurrent(container, reaction.output, Current(container, reaction.output) + output);
        SetCurrent(container, "plant_matter", Current(container, "plant_matter") + process - output);  // 收率差额→渣
    }
    // 之后逐条 batch：TryPlanBatch 只在整个批次配齐时一次性换算，plan 为 null 就跳过
    if (changed) WaterFeatureHelper.OnLiquidChanged(container, WaterFeatureHelper.GetWaterFeature(container).IsExposed());
}
```

> 收率差额不凭空消失，落进 `plant_matter` 渣——**质量守恒**。这一手让"提取 40%"变成可再加工的副产物，而不是数值蒸发。

**③ 配方规格：按"整瓶容量"卡材料，不按 id 卡** — `ContentRuntime.cs:571-590`（`IsFullCraftingMaterial`）

```csharp
internal static bool IsFullCraftingMaterial(GameItem item, string id)
{
    if (item == null || item.identifier != id) return false;
    int standard = StandardCraftingVolume(id);          // 该物品在表里的标称体积（如聚酸酯 4000 mL）
    if (standard > 0) return ChemicalHelper.GetTotalVolume(item) == standard;
    return true;
}
```

> 意思是：**容器里的毫升数就是"规格"**。一瓶只剩 3 mL 的聚酸酯不满足配方，玩家必须先补满或倒掉。配套 `DissolveInto` 的 Prefix 保证"拆 1 件"而不是"整叠溶解"，`IndustrialChecks` 里专门断言"旧 11 mL 聚酸酯不被强制改容量"。

**④ 交付：临时合成一个 `CraftingRecipe` 当多物品交付器** — `NativeRequests.cs:156-192`（`CanFill` / `Fill`）

```csharp
internal static bool CanFill(StoreRequest request, ContractSpec spec)
{
    if (ModEntry.Enabled && PlayerStore.Instance != null && EmporiumEntry.Instance != null
        && PlayerStore.Instance.storeRequests.Contains(request)
        && CooperationPolicy.CanContract(spec, ModEntry.Reputation))
        return Delivery(spec).CanCraft(Materials());
    return false;
}

internal static void Fill(StoreRequest request, ContractSpec spec)
{
    if (busy || !CanFill(request, spec)) return;
    busy = true;
    try {
        List<GameItem> owned = Materials();
        CraftingRecipe delivery = Delivery(spec, request);   // CraftingFunc 内逐件回调 CheckDeliveredItem
        if (delivery.CanCraft(owned)) {
            delivery.Craft(owned);
            PlayerStore.Instance.storeRequests.Remove(request);
            ModHelper.SetModDataInt("rhodes_island", "contract.done." + spec.id, StoreStation.GetDayCounter());
            PlayerStore.Instance.ModCash(spec.money, true);
            ModEntry.AddContractReputation(spec.recovery
                ? CooperationPolicy.RecoveryReward(spec.reputation, ModEntry.Reputation)
                : (double)spec.reputation);
        }
    } finally { busy = false; }        // 只复位标志，不回滚已消耗的物品
}
```

**⑤ 验收台：复制存档槽再验，绝不在玩家档上跑** — `ReleaseGate.cs:274-316`（`Start` / `Reload`，节选）

```csharp
private static void Start()
{
    if (Running) throw new InvalidOperationException("验收正在运行。");
    if (!AcceptanceAssistant.IsTestActive) throw new InvalidOperationException("先在验收助手登记专用测试档。");
    if (store.currentClientInstance != null || ModHelper.IsOutsideStore())
        throw new InvalidOperationException("先让当前顾客离店，并回到当铺，再开始验收。");
    store.SaveGame();
    int slot = SaveFiles.DuplicateSlot(store.saveSlotId, "罗德岛自动验收");   // 复制当前档
    if (slot < 0 || slot == store.saveSlotId) throw new InvalidOperationException("创建测试副本失败，未运行验收。");
    report.sourceSlot = store.saveSlotId;   report.slot = slot;
    report.assembly = Build;                report.phase = "start";        // 记构建指纹
    Write();  Background();  Reload();
}

private static void Reload()
{
    NewGameData.Instance.saveSlot = report.slot;                            // 切到副本
    NewGameData.Instance.saveFile = SaveFiles.Slot(report.slot);
    Time.timeScale = 1f;  SceneManager.LoadScene(report.scene);             // 重载场景，跑真读档
}
```

> 槽号被硬钉住：`ReleaseGate.cs:660` 的断言是 `Store.saveSlotId == 36`——**救护站/自动验收只允许在 36 号副本档跑**，跑完 `:445` 把 `sourceSlot` 写回去。这就是我们"Slot 0 禁碰 / 真机只进 Slot 1"的可执行版本。

---

## 4. 踩坑与红线

1. **`HandleTemperatureReaction` 的 Postfix 在 360 tick 里**。`React` 每个 batch 都 `new Dictionary<string,int>()`（5 条批次 × 360 次 × 每容器 = 每夜 1800 次分配/容器）。抄结构必须换成预分配缓冲或 `ReactionSpec` 上挂复用的临时容器；否则二十几个液体容器的店里，过夜会有可见的 GC 峰值。
2. **整数标签存小数**（`ri_extract_fraction_*`）是个好办法，但标签会**永久留在物品 state 里**，退订后仍随存档走。要么接受脏标签，要么在 `onLoaded` 里清。
3. **`Fill` 先扣货后结账，没有回滚**。`delivery.Craft()` 中途抛异常 → 物品已消耗、请求还在列表里。目前只靠 `CanFill` 前置再判一次降低概率。我们的"寄卖结算 / 打包订单"必须做**先建结果、后消耗**，或至少留可重放的消耗日志。
4. **无 `HotReloadPrepare` / `HotReloadActivate`**（透视报告确认）。`Catalog`、`nativeVisuals`、`Sprites` 三个静态表只在 `OnDisable`/`Disable()` 清；热重载路径未接管。我们要照抄这个结构，必须补上"重载前清、重载后重建"这一对，否则 `Sprite` 引用悬空。
5. **`RegisterSystems()` 挂在 `OnModItemDirectoryInit`**，里面 `PlantRegistry.Add` 没有重复保护（同文件的 `ChemFinisherRecipe` 有 `Exists` 判定）。物品目录重建一次就可能重复登记植物（推测：第二份覆盖第一份或抛异常，取决于原版实现）。
6. **反射碰私有 API 有三处**：`ApplyIcon` 用 `AccessTools.Method(typeof(GameItem), "SetShape")` + `GridShapeBuilder(GridShape)` 重算占格；`NativeFinisher` 反射 `MachineryButton.callback` 并按**本地化文本** `Single()` 找按钮；`NativeRequests` 反射 `StoreRequest.CheckDeliveredItem`。三处都随版本更迭即断——`machinery` 那条尤其脆（换语言/换排版就找不着按钮）。
7. **`NativeTrade` 用"钱变少"推断购买成功**（`Paid => cash - store.playerCash`）。遇到折扣、退款、或同一帧内两笔交易，金额就对不上。
8. **`SetGameItemType` 直接写品类**（`MEDICAL`、`MATERIAL`）而不是只 `EnableTag`。它安全是因为写的全是原版类型；**不要把这个写法推广到自定义品类**——那会出蓝字 `[Translation Error]`。
9. **改掉落表的正面冲突**：`ScavHelper.SpawnDumpingGroundLoot` + `EventModifier` 两个 Postfix 会把 1-7 采石场的掉落换成自己的表，和任何改垃圾场掉落的模组都不能共存。
10. **`duration = int.MaxValue` 的委托永不过期**，只靠 `Tick` 撤回。玩家如果长期不开市场界面，`Tick` 只在 `HandleStoreRequests` 与 `OnGameLoadedNormal` 触发，列表里会积压过期的旧单。

---

## 5. 对我们的可用点

1. **纯度 → 原版成色带的映射表**（`ProductionQuality.cs:173` `GradeCondition`）：把 0–100 分直接映到原版 7 档 `ItemConditionList.CreateLabPure/PharmaGrade/NearPharma/StreetStandard/CutProduct/JunkGrade/StarchGrade`，并 `SetConditions(cond, cond)`。
   → **《深空电玩》**：`arc_refurb_kit` 今天只做"翻新件→全新"，可以改成"按翻新质量给成色带"，同时保留 `counterfeitStamp`（正好对上它"抛光后仍保持 `counterfeitStamp`"的现有行为）。
   → **《深空当铺：可能是群友》**：鉴定输出不要新造成色词，直接复用这 7 个原版 `ItemCondition`，判定仍只认 `realCondition`。

2. **验货扣声望**（`RhodesQualityAudit.cs` + `CooperationQuality.IsFraud`）：`IsContainFeatureWithId("realCounterfeit")` → -20；纯度 < 80 的医疗品 → -8；两者都走 `StoreReputation.ModReputationRaw`。
   → **《深空电玩》**：`ArcClients` 的"识破假货 -4"可以升级成"同派系累计 + 分级扣"，且**用同一套特征判定**（`realCounterfeit`），不要自己再造一个"假货"标志。
   → **《深空当铺：可能是群友》**：芙蕾雅/伞萝萝的关系值可以挂同一条 `StoreReputation` 扣分链，省掉一套自建好感度。

3. **`CraftingRecipe` 当多物品交付判定器**（`NativeRequests.Delivery`）：`new CraftingRecipe(title).AddMaterialIdentifier(id, n)` + `SetCraftingFunc` + `CanCraft` / `Craft`，天然支持"多 id × 多件数 × 部分堆叠"。
   → 两款模组凡是要"整批交货/打包买单/按清单收货"的地方都能直接借，不必自己扫容器 + 逐件 `Destroy`（那正是原版 `FillRequest` 的笨办法）。注意本文 §4.3 的"先建结果后消耗"改造。

4. **自带验收台整段搬**：`ReleaseGate` 的骨架（`DuplicateSlot` → 切 `NewGameData` → `LoadScene` → 逐条断言 → `release-gate.json`）+ `RuntimeControl` 的 `runtime-command.json` 文件 IPC + `Build` 指纹门禁（命令里的 `assembly` 与当前构建不符就拒跑）。
   → 正好补我们五道门禁里**第 ④ 道"真机"**的自动化空缺：外部脚本写命令文件 → 游戏内跑 → 读回 JSON。它连"3 条只能人眼看的项"（人物位置与遮挡 / 对话节奏 / 自然日期下的长期平衡）都单列成 `manualChecks`，这个"自动通过 ≠ 可发布"的双签名（`automatedPassed` + `releaseApproved`）值得直接照搬。

5. **夜间地点借原版搜刮流程**（`QuarryLocation.cs`）：`ModHelper.AddAfterhourLocation(...)` 拿一个地点壳，搜刮直接调 `ScavHelper.ScavengeDumpingGrounds()`，掉落用 `SpawnDumpingGroundLoot` Postfix 替换；`ScavHelper.ResetScavenging()` 在 `OnAfterhourArriveEarly` 重置。
   → **《深空电玩》**：夜间"废站打街机厅/回收站淘卡带"可以零成本落地，连"与垃圾场共享当晚搜刮次数"都是原版白送。
   → **《深空当铺：可能是群友》**：夜间外出点同理（配合 18 号规范）。

6. **供货/收购双清单 + 反套利黑名单**（`ModEntry.cs:32` `BuyingIds` + `SupplyPolicy.SuppliedIds`）：每个商人一张"我收什么"和一张"我带什么"，最后 `clientBuyingIdList.RemoveAll(supplied) + clientBlackIdList.AddRange(supplied)`——**他卖给你的东西他绝不收**。
   → 这是任何"商人货池"模组的硬需求（否则玩家原地套利）。两款模组的访客货池都该补这一条断言。

7. **经济矩阵审计**（`ProcurementAudit.cs`）：生成 20 个样本物品 × 每个商人变体，把 `IsClientBuyingThisItem` / `IsClientRefusingItem` / `HasBudgetLeftToBuy` 打成一张 JSON 表落盘。
   → 我们的 `Run-Regression` 现在是纯离线；这一类"在游戏里跑、把原版黑箱判定结果导成表"的做法，能覆盖"标签 + 预算 + 禁收"三者交互出来的涌现行为——静态代码审不出来。

---

## 6. 一句话回答"它和已有那两个化学/制药模组的根本差别"

`3830572817` ② 是**替玩家操作原版机器**（用 `SimulationItem : GameItem` 假物品把原版反应重算 900 遍，再提交），[液体系统三例](液体系统三例-扫描仪·水商·水龙头.md)（3831472187 / 3828878417 / 3833421581）是**读写原版液体**，而这个模组是**给原版机器加新配方**——它一行都不重算原版工艺，只在 `HandleTemperatureReaction` 的 Postfix 里追加一张自己的反应/批次表，让水培槽、搅拌、加热、纯化、蒸发、封装六种原版机器原样跑一条 43 物品的供应链；它真正新增的东西不是化学，而是**"声望驱动的采购客户 + 委托单 + 五商人供销分工"这一层商业外壳**，以及一条 12 场的住客剧情线。
