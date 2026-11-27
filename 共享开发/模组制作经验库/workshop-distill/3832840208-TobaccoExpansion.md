# 香烟拓展 / Tobacco Expansion（3832840208）蒸馏档案

> 工坊 id **3832840208**（manifest：`ID=tobacco_expansion`、`ModVersion=0.5.4.3`、作者 xuxu、`<GameVersions>0.46D`、`<Prerequisites><Mod>brewing_expansion</Mod></Prerequisites>`、**`<Capabilities />` 空 = 零能力**）。
> 反编译目录 `缓存文件夹\decomp_ws\3832840208\`：**172 个 .cs / 16,885 行**，单文件最大 `TobaccoExpansion\Machines.cs` 676 行。
> 为什么值得读：它是同一作者「第二部扩展」的样板——**几乎不新建物品类，全部克隆原版物品再改身份**；三套鉴定分别叠在三个不同的原版层（放大镜 UI / 标签器 / 制造商+价格特征）；以及一套真能跑的跨模组降级协议（终端谁来开、货从哪来、存档键归谁）。

---

## 1. 规模与结构

| 项 | 值 |
| --- | --- |
| 文件 / 行数 | 172 / 16,885（`TobaccoExpansion\` 桶 170 个，另 `Properties\AssemblyInfo.cs`、`System.Runtime.CompilerServices\IsExternalInit.cs`） |
| 最大文件 | `Machines.cs` 676、`OrderWindowArt.cs` 658、`Items.cs` 644、`Visitors.cs` 544、`OrderWindow.cs` 482、`OrderTerminal.cs` 455、`BuyerPreferences.cs` 442 |
| 体量分布 | 业务文件桶 170 个里 **58 个 ≤30 行、101 个 ≤60 行**（约 1/3 是"一个补丁一个文件"） |
| 冻结副本 / 生成代码 | **无**。唯一编译期补丁是 `IsExternalInit.cs`（5 行空壳，为 C# 9 `record`/`init` 提供类型）；没有 CSV/JSON/TSV 数据文件 |
| 声明的外部能力 | 空（`<Capabilities />`）。透视报告里的 `reflection(6文件)` 全是 `AccessTools`/`MethodInfo.CreateDelegate`/`typeof(X).Assembly` —— **Harmony 需要的程序集内反射，不是文件系统能力**，与零能力声明一致 |
| 钩子 / 补丁 | ModHook 13 个；含 `[HarmonyPatch]` 的文件 **61** 个，透视报告按"类+方法"统计到 **50 处靶点**（有的文件里挂着手动 `TargetMethods` 列表或嵌套类，所以文件数 > 靶点数） |
| ModHelper 用量 | 23 个成员（比酿酒的 36 个少，多数存档走自己的 `OrderRepository` + `State` 标签） |

结构分层（读哪几个文件就够）：

| 层 | 文件 | 一句话 |
| --- | --- | --- |
| 启动 | `TobaccoMod.cs`、`StartupReadiness.cs`、`DeferredPatches.cs` | 前置模组就绪 + 本地化就绪 **双等待**，再统一 `PatchAll` |
| 数据 | `Rules.cs`、`ItemDefinitions.cs`、`ItemDefinition.cs`、`BrewingRecipeData.cs` | 16 味型 + 15 种材料 + 18 条单体 / 34 条配对的"借来的"酒配方 |
| 物品 | `Items.cs`、`Overlabels.cs`、`StampMachine.cs`、`MoistureBox.cs`、`Machines.cs` | 克隆原版 → 改身份 → 绑 `onLoaded` |
| 鉴定 | `Packaging.cs`、`ConditionLabels.cs`、`ConditionMenu.cs`、`ProducerLabels.cs`、`RegionalInspection.cs` | 真伪 / 成色 / 贴牌 / 顾客识破 |
| 生产 | `NightProcessing.cs`、`NaturalDrying.cs`、`CigaretteChemistry.cs`、`ProductionTransaction.cs` | 过夜节拍 + 原子支付 |
| 委托 | `OrderTerminal.cs`、`OrderWindow*.cs`、`OrderCatalog.cs`、`OrderGeneration.cs`、`OrderRepository.cs`、`SpecialCommissions.cs` | 与酿酒的**同一台**终端，二选一挂载 |
| 安全 | `OperationScope.cs`、`ActionSafety.cs`、`ActionSession.cs`、`ActionSaveBoundaryPatch.cs`、`NativeCallbackBinding.cs` | 进行中不许存档 / 代际失效 |

---

## 2. 它怎么做这件事（点名原版类与方法）

### 2.1 17 种香烟 = 原版 `packet_red_cigarette` 的克隆体

不是新建物品类，也不是 `ItemDirectory.CreateEmptyItem` 从零拼。`Items.CreateCore()` 的分发顺序是：贴牌 → 印章 → 特种纸盒 → 原版红烟 → 手册/保湿盒 → 故事书 → 机器 → **普通香烟**（`Items.cs:45-107`）。普通香烟这一步：

```csharp
// Items.cs:95-107
if (Rules.IsCustomCigarette(id))
{
    val = DirectoryMaster.Item("packet_red_cigarette", false);
}
else
{
    val = ((id == "te_extract_syringe") ? DirectoryMaster.Item("magnifier", false) : ItemDirectory.CreateEmptyItem(id));
}
if (val == null) { throw new InvalidOperationException("Cannot allocate " + id); }
val.identifier = id;
```

于是 17 种香烟（`Rules.Flavors[16]` + `te_cigarette_regular`，`Rules.cs:52-70`、`Rules.cs:97-115`）**共享原版香烟的全部行为**：可被 `CigaretteUI` 打检、被 `Inspectable`/`CIGARETTE_TAG` 识别、可被原版"抽"（`ConsumableHelper.InitNicotine(item, 3)`，`CigaretteActions.cs:22`）。同理：

| 自建 id | 克隆的原版模板 | 代码 |
| --- | --- | --- |
| `te_cigarette_*` / `te_cigar` | `packet_red_cigarette` | `Items.cs:97` |
| `te_extract_syringe` | `magnifier`（继承"可对目标使用"的工具语义） | `Items.cs:101` |
| `te_producer_stamp_machine` | `magnifier` | `StampMachine.cs:44` |
| `te_moisture_box` | `storage_bay`（连窗口里的 `GameGridInventory` 一起继承） | `MoistureBox.cs:20-38` |

id 规则是纯前缀 + 味型名：`te_` 全局前缀、`te_disposable_producer_label_{1..4}`、`te_producer_stamp_{1..4}` 用序号裸拼（`Overlabels.cs:20-27`、`StampMachine.cs:23-30` 各自 `int.TryParse` 解序号）。注册入口与酿酒完全同构：`ModHook.OnModItemDirectoryInit += Register` + `OnEnable` 里再 `Resources.FindObjectsOfTypeAll<ModItemDirectory>()` 补扫一遍（`TobaccoMod.cs:130`、`TobaccoMod.cs:142-146`），注册表本体是 `Rules.ItemIds()` 一个 `yield` 迭代器（`Rules.cs:190-224`）。

### 2.2 三套鉴定，分别挂在三个不同的原版层

#### ① 真伪：用**原版放大镜 UI**，但把包装图换成自己的

| 原版类:方法 | 补丁 | 干什么 |
| --- | --- | --- |
| `CigaretteUI.OnInit` | Prefix，返回 `!Packaging.Render(...)` | 有自定义包装图时**顶掉原版盒绘**，用 `te_asset_{art}_p{box}_n{lettering}_l{logo}` 换图（`InspectionArtPatch.cs:13-16`、`Packaging.cs:85-120`） |
| `CigaretteUI.DisableAll` | Postfix | 关 UI 时同步隐藏自己实例化的图（`InspectionHidePatch.cs`） |
| `InspectionUIManager.OnInspectItem` | Prefix | 不可检的味型（`Flavor.Inspectable==false`）直接弹出"无需检查"并 `return false`（`NonInspectablePatch.cs:13-21`） |
| `ItemFeature.SetExposed` | Prefix | 每次曝光前把真假条件经 `Items.CigaretteCondition` 归一（伪劣 -50 值修正） |

真伪本体不是自己画的判定，而是原版特征：`ItemFeatureList.CigaretteAuthenticity()` + `InsCigaretteHelper.SetGenuine/SetConterfeit`（`Items.cs:264-314`）。伪造时随机滚三个包装缺陷档位 `CIGARETTE_BOX(0-3)` / `CIGARETTE_LETTERING(0-7)` / `CIGARETTE_LOGO(0-1)`，并保证不会三者全 0（`Items.cs:295-311`）；再映射成原版标签 `CIGARETTE_*_ISSUE`（`Packaging.cs:21-62`）。**"原版放大镜"这句话的具体含义是**：玩家用的仍是原版那套看包装的检视流程，只是被看到的图由 mod 按缺陷档位合成。放大镜本体也不硬编码字符串——第一次用时实例化 `magnifier` 读它的 `identifier` 再销毁（`InspectionTools.cs:7-20`）。

#### ② 成色：给原版**标签器**注册一个新 condition 类别

```csharp
// ConditionLabels.cs:32-39
internal static void Register()
{
    ItemFeatureList.CategoryNames["TE_TOBACCO_CONDITION"] = ModText.Get("te_condition_labels_01");
    List<ItemCondition> list = new List<ItemCondition>();
    list.Add(Condition(defective: true));
    list.Add(Condition(defective: false));
    ItemConditionList.conditionLookup["TE_TOBACCO_CONDITION"] = list;
}
```

`CategoryNames` + `conditionLookup` 一写，原版 `LabelerUIManager` 的下拉里就多了一行"烟况"，不用自建 UI。配套三处修正：`LabelerUIManager.OpenUI` Prefix 补特征（`CigaretteConditionLabelWindowPatch.cs:13-22`）、`LabelerElement.InitElement` Postfix 给下拉装"未检"占位并 `SetValueWithoutNotify(-1)`（`CigaretteConditionLabelPlaceholderPatch.cs:16-36`）、`LabelerUIManager.SaveCurrentLabels` Prefix **阻止原版把 -1 记成"上次选择"**（`CigaretteUncheckedLabelMemoryPatch.cs:14-43`）。玩家自己的检查入口是 `ConditionMenu`（自绘 `PixelWindow`，三行：检查成色 / 剥标签 / 说明），由双击 `ItemMouseDoubleClickHandler.DoubleClickAction` 或右键按钮 `ItemContextHandler.SetButtonColor` 打开（`CigaretteDoubleClickPatch.cs`、`ConditionActionCaption.cs`）。

#### ③ 贴牌（本次最有价值的一套）：一次性标签 → 剥离后恢复**原厂公司 + 原厂价格**

四层结构，缺一不可：

| 层 | 存哪 | 代码 |
| --- | --- | --- |
| 永久身份（印章机打的原厂等级 0~4） | `GameItem.state` 标签 `TE_PRODUCER` | `ProducerLabels.Set`，`ProducerLabels.cs:68-83` |
| 一次性身份（贴纸 1~4） | 标签 `TE_DISPOSABLE_PRODUCER` | `Overlabels.Apply`，`Overlabels.cs:102-120` |
| 生效等级 | `Overlabels.Level() > 0 ? 贴纸 : 印章` | `ProducerLabels.Level`，`ProducerLabels.cs:41-49` |
| 公司名 | `item.EnableManufacturer("pastel"/"te_producer_N")` + `GameItem.DisplayManufacturer` Prefix | `ProducerLabels.cs:136`、`CigaretteCompanyPatch.cs:13-21` |
| 价格 | `ItemFeature("TE_PRODUCER_PRICE")` 的 `SetValueModifier(Bonuses[level])` | `ProducerLabels.cs:100-137` |

**剥离就是"把一次性那一层清零，让生效等级自己回落到永久层"**：

```csharp
// Overlabels.cs:141-159
internal static bool Peel(GameItem item)
{
    if (!Rules.IsCustomCigarette((item != null) ? item.identifier : null) || Level(item) == 0) { return false; }
    State.Set(item, "TE_DISPOSABLE_PRODUCER", 0);
    ProducerLabels.Refresh(item);      // ← 内部按 Level()=BaseLevel 重写 value mod 与制造商
    item.Validate();
    if (PlayerStore.Instance?.currentNegociatedItem?.Key() == item.Key())
    {
        NegociationUIManager instance = NegociationUIManager.Instance;
        if (instance != null) { instance.UpdateUI(item); }
    }
    return true;
}
```

两处防炸设计值得单独记：

1. **贴纸是"原子支付"的**：`Overlabels.Apply` 包在 `ProductionTransaction` 里——`Track(label)` → `WriteInt(target,"TE_DISPOSABLE_PRODUCER")` → `Pay(label,1)`，中途任何一步不满足（目标被卖、被移出背包）就整体回滚，`finally` 里再 `ProducerLabels.Refresh` 一次兜底（`Overlabels.cs:111-130`）。
2. **贴纸骗不了自己的估价**：`ProducerLabels.ActualEstimate` 在 `GetCurrentValue` 前把 `EstimatingBonus` 设成**永久层** `Bonuses[BaseLevel(item)]`，并由 `ItemFeature.GetCurrentValueMod` 的 Postfix 强制返回该值（`ProducerLabels.cs:19-34`、`ActualProducerEstimate.cs:13-19`）。所以委托终端按真值算钱，桌面显示则是虚高值。

风险侧：顾客有 50% 概率被随机指派成"查标签的人"（`TE_LABEL_INSPECT|1`，`Overlabels.cs:161-171`）；卖给他带贴纸的烟 → 自动剥离 + 外环声望 -10 + 一句对话（`Overlabels.cs:191-202`）。

### 2.3 顾客识破层：`bool?` 三元返回

`RegionalInspection.Recognizes()` 是整个鉴定系统的"谁看得穿"总表（`RegionalInspection.cs:22-42`）：

```csharp
// RegionalInspection.cs:29-41
if (Rules.IsCustomCigarette(val.identifier) && (feature.identifier == "TE_TOBACCO_CONDITION" || feature.category == "CATEGORY_GENUINE_CIGARETTE") && (PreviewDepth > 0 || IsBehindLock(val)))
{
    return false;                       // 正在预览/锁在货架里 → 谁都看不见
}
if (feature.identifier == "TE_TOBACCO_CONDITION")
{
    return !feature.isDisabled && !feature.IsExposed() && State.Int(val, "TE_DEFECT") != 0
        && feature.fakeCondition?.identifier != "te_condition_defective"
        && client.clientFaction == "FACTION_UPPER_LEVEL";        // 上层区 → 能识破烟况
}
if (feature.category == "CATEGORY_GENUINE_CIGARETTE" && client.clientFaction == "FACTION_LOWER_LEVEL" && BuyerPreferences.Fake(val, actual: true))
{
    return !feature.isDisabled && !feature.IsExposed() && feature.realCondition?.identifier != feature.GetCurrentItemCondition()?.identifier;  // 下层区 → 能识破假货
}
return null;                            // null = 交还原版判定
```

`null` 就是"不管"，交给 `StoreClient.CanClientExposeThisFeature` 原逻辑——这是它能和任何鉴定类模组共存的关键（见 §4.5）。三个入口挂上去：`CanClientExposeThisFeature` Prefix（识破判定）、`ClientExposeFeature` Prefix（曝光后扣声望 -2、每件只罚一次 `TE_FRAUD_PENALIZED`）、`PlayerStore.SellItem` / `TryReinspectBeforeSale` Prefix（成交前强制重检并弹谈判窗）。

### 2.4 和原版水培怎么对接（与酿酒同一套 API）

```csharp
// TobaccoHydroponics.cs:29-39
if (!PlantRegistry.Contains("te_tobacco_seed"))
{
    plant = PlantRegistry.Add(new PlantDefinition("te_tobacco_seed", ModText.Get("te_item_leaf_name"), "te_tobacco_leaf", 16, 2)
        .Nutriment(150, 75, 50).Water(120).Contaminant(1.0)
        .Innate("complex_genome", 2)
        .Innate("infertile", 10)
        .OnHarvest((Action<PlantHarvestContext>)Harvest));
}
```

- 收获回调里按健康度分流大/小/普通叶，并**只有 `returnsSeeds==true` 且没挂证书锁才回收种子**（`TobaccoHydroponics.cs:125-148`）。
- 证书门槛**直接读酿酒的存档布尔**：`ModHelper.GetModDataBool("brewing_expansion","certificate_issued_1",false)`（`TobaccoHydroponics.cs:12-22`）——第二部不另建一套执照，复用第一部的。
- 种子贴图：Patch `MachineHydroponic.Hydroponic` 的 Postfix 换 `getItemSpriteModifierForItemFunc`（`TobaccoHydroponicSprite.cs:16-37`），和酿酒 `BrewingHydroponicMachine.cs` 同一手法。
- 基因：`PlantGeneticsHelper.IsTraitUsable` Prefix 禁 `perennial`（除非委托解锁）与 `edible`（`TobaccoGeneGate.cs:13-33`）；`InheritTraits` Prefix + 对 `GetComplexity`/`IsModified`/`ExtractionUses`/`CreateSeedGeneticsTooltip` 的 **Transpiler**（4 个方法一起改）让自育种按标签里的基因对算先天等级（`TobaccoSeedStrains.cs:47-137`）。

### 2.5 机器与过夜节拍

两台机器（`te_tobacco_dryer` 烘干 / `te_cigarette_machine` 制烟）走**原版机器外壳**：`MachineHelper.PowerPanel/Panel/CreateMachineryWindow/SetupBatterySlot` + `State.Set(item,"machinery_drawn_power",6|4)` + `onCycleEndSlotItemFunc` 挂 `NightProcessing.Cycle`（`Machines.cs:107-192`）。注意它**没有** `MachineryHelper.InitMachinery` + `STANDARD_MACHINE_TAG`（与化学扩展那套标准骨架相比是"半套"）。

调度节奏三层：

| 时机 | 干什么 | 代码 |
| --- | --- | --- |
| `OnGoingSleepEarly` | 扫全店，把机器、可风干材料装进 `Pending`/`Materials` 两张表 | `NightProcessing.Begin`，`NightProcessing.cs:17-36` |
| 每个 `onCycleEnd` | 单台机器跑一批（`dryAll:false`），用 `Seen` 去重 | `NightProcessing.cs:38-48` |
| `OnGoingSleepLate` | 补跑没被周期碰过的机器 + 统一结算材料 | `NightProcessing.cs:50-105` |

材料结算就是"藏起来的两条规则"：干叶/烟丝每夜 `TE_WET=1`；鲜叶累计两夜自动变干叶（`NaturalDrying.Advance`，`NaturalDrying.cs:11-28`）；雪茄两夜未保湿 → 自动变成"有缺陷"且玩家不知道（`known:false`）。**保湿盒的豁免靠爬父容器链**：`MoistureBox.Protects` 一路 `parentInventory.GetParentItem()` 向上找 `te_moisture_box`（`MoistureBox.cs:76-90`）。

### 2.6 工具三连：`Target` / `CanTarget` / `MayTarget`

三个 Prefix 共用一张准入表 `ToolActions.Handles/Allowed`：贴纸→`Overlabels.Allowed`、印章机→`StampMachine.Allowed`、放大镜→`InspectionTools.Handles`、小刀→`Rules.CutYield`、针管→剂量与风味校验（`ToolTargetPatch.cs`、`ToolCanTargetPatch.cs`、`ToolMayTargetPatch.cs`、`ToolActions.cs:60-118`）。`Handles` 决定"归我管吗"，`Allowed` 决定"允不允许"，两个栅栏分开写，是这类工具模组最省事的骨架。

### 2.7 Patch 靶点分组（透视报告统计 50 处；下表按原版类归并）

> 口径说明：报告按"类+方法"统计到 50 处；实际含 `[HarmonyPatch]` 的文件 61 个，差额来自 `[HarmonyPatch]` + `TargetMethods()` 的多目标补丁（如 `ActionSaveBoundaryPatch` 一次挂 `SaveGame`/`InitialSave`/`WriteSlotFile` 三个方法）与文件内嵌套补丁类。

| 原版类 | 方法（×次数） | 目的 |
| --- | --- | --- |
| `PlantGeneticsHelper` | `IsTraitUsable`、`InheritTraits`、`GetComplexity`/`IsModified`/`ExtractionUses`/`CreateSeedGeneticsTooltip`（Transpiler 一组） | 自育基因与解锁门槛 |
| `MachineHydroponic` | `Hydroponic` | 生长期贴图 |
| `GameItem` | `DisplayManufacturer`、`GetNegociatedValue`、`GetCurrentChildValue`、`Target`、`CanTarget`、`MayTarget` | 公司名 / 买家偏好加价 / 满盒计价 / 工具三连 |
| `ItemFeature` | `GetCurrentValueMod`、`SetExposed` | 真值估价、曝光前归一 |
| `LabelerUIManager` / `LabelerElement` | `OpenUI`、`SaveCurrentLabels` / `InitElement` | 成色类别 + "未检"占位 |
| `CigaretteUI` / `InspectionUIManager` | `OnInit`、`DisableAll` / `OnInspectItem` | 包装图、不可检物品 |
| `ItemContextHandler` / `ItemMouseDoubleClickHandler` | `SetButtonColor` / `DoubleClickAction` | 菜单入口 |
| `CommonTooltipHelper` | `CreateItemTypeTooltip` | 化学值行 |
| `StoreClient` | `ClientExposeFeature`、`CanClientExposeThisFeature`、`OnItemPlaced` | 识破 / 预览深度 |
| `PlayerStore` | `SellItem`、`TryReinspectBeforeSale`、`CanSellThisItem`、`SaveGame`+`InitialSave`+`WriteSlotFile`（`TargetMethods` 组）、`LoadGame`、`OnShutterOpened`、`GetNextClient`、`HandleFutureClientQueue`、`TryCallNextClient` | 成交拦截、存档闸门、来客节奏 |
| `SaveManager` | `DecodeNodes` | 机器说明书解码 |
| `GameItemElement` | `Destroy` | 原版"消耗完成"回执 |
| `GameGridInventory`/`GameSlotInventory` | `UncheckedAccept`、`Expel`（4 个） | 委托提交按钮刷新 |
| `StoreUIManager` / `StoreClient` | `OnNextClientArrived` / `StartMainDialogue` | 访客到店时段 |
| `StoreReputation` / `StoreReputationPerk` | `CheckReputation`、`OnItemTraded` / `GetLocalizedDescription` | 外环声望与外环特质文案 |
| `DialogUIManager` | `DisplayClientText`、`HideAllDialogUI`、`SetNormalMode`、`SetShootingMode`、`PlayClientSingleText` | 陈子 NPC 的对话锚点 |
| `StorageExpansionHelper` / `ModuleDirectory` / `EmporiumEntry` / `TransitUI` | `GetKitTabs` / `ModuleBayExpansionKit` / `Validate`、`UpdateScaleFactor` / `BuildCard` | 储物扩建、模块舱、委托机布局、店铺预览卡 |
| `NewGameData` | `HandleInitialItem` | 新档代际起点 |

---

## 3. 关键代码摘录

### 3.1 启动：前置 + 本地化双等待，然后才 `PatchAll`

```csharp
// TobaccoMod.cs:55-72
public void OnEnable()
{
    if (!Enabled && !waitingForPrerequisite)
    {
        if (!ModLoader.IsModEnabled("brewing_expansion"))
        {
            throw new InvalidOperationException("Tobacco Expansion requires enabled Brewing Expansion (brewing_expansion). Enable the prerequisite first and restart.");
        }
        waitingForPrerequisite = true;
        ModHook.OnModAssetsLoaded += PrerequisiteAssetsLoaded;
        LoadedMod val = ModLoader.Mods.FirstOrDefault((LoadedMod m) => (object)m.Instance == this && m.IsEnabled);
        if (val != null && val.Localization.Count > 0) { PrerequisiteAssetsLoaded(val); }
    }
}
```

```csharp
// DeferredPatches.cs:16-43
internal static void Install()
{
    if (Ready) { return; }
    owner = ModLoader.Mods.FirstOrDefault((LoadedMod m) => m.Instance is TobaccoMod)?.HarmonyInstance;
    if (owner == null || owner.Id != "mod.tobacco_expansion")
    {
        throw new InvalidOperationException("Official Tobacco patch owner is unavailable.");
    }
    Installing = true;
    try
    {
        owner.PatchAll(typeof(TobaccoMod).Assembly);
        Ready = true;
    }
    catch { Remove(); throw; }
    finally { Installing = false; }
}
```

**为什么这么写**：61 个补丁文件每个都有 `private static bool Prepare() => DeferredPatches.Installing;`（实测 61/61 全带）。这样 `PatchAll` 只会在 `Installing==true` 的那一瞬间生效，任何其它时机被扫描（热重载、别的加载器二次 PatchAll）都会被 `Prepare()` 挡掉，不会重复打补丁。`Activate()` 还额外卡了版本号：`result < new Version(3, 6, 7, 5)` 就抛（`TobaccoMod.cs:118-121`）——先用 manifest 的 `Prerequisites` 声明，再在代码里二次校验，双层。

### 3.2 成色特征：一个特征撑起"真值 / 假值 / 未检"

```csharp
// Items.cs:398-428
internal static void ConfigureConditionFeature(ItemFeature feature, bool defective, bool cigar = false)
{
    feature.featureType = (FeatureType)0;
    feature.valueStage = (ValueStage)2;
    ConditionLabels.Register();
    string text = feature.fakeCondition?.identifier;
    feature.useCondition = true;
    feature.isExposable = false;            // ← 关键：不许被"鉴定"曝光，只能靠自建菜单
    string text2 = ModText.Get("te_items__quality_01");     // "未检"
    string actualDisplay = ModText.Get(defective ? "te_quality_irreparable" : "te_quality_good");
    bool flag = ((text == "te_condition_good" || text == "te_condition_defective") ? true : false);
    feature.fakeCondition = (flag ? ConditionLabels.Condition(text == "te_condition_defective") : new ItemCondition
    {
        identifier = "te_condition_unchecked",
        category = "TE_TOBACCO_CONDITION",
        display = text2,
        modValue = ((cigar & defective) ? (-30) : 0)
    });
    feature.realCondition = ConditionLabels.Condition(defective);
    feature.SetPublicDisplay(text2);
    feature.SetActualDisplay(actualDisplay);
    feature.SetPreExposedValueMod(0, true);
    feature.SetValueModifier(defective ? (-30) : 0);
}
```

**为什么这么写**：`isExposable=false` 把"能否被顾客/放大镜曝光"这条路封死，自己用 `ConditionMenu` 控制谁能看、什么时候看；`fakeCondition` 的哨兵值 `te_condition_unchecked` 让"未检"成为一个真实的第三态（而不是 null），于是标签器下拉能显示它、存档能存它、剥离/重检能识别它。

### 3.3 一次性贴纸的原子应用

```csharp
// Overlabels.cs:102-130
internal static void Apply(GameItem label, GameItem target)
{
    if (!Allowed(label, target)) { return; }
    int value = ItemLevel(label.identifier);
    ItemIdentity itemIdentity = new ItemIdentity(target);
    GameInventory parentInventory = target.parentInventory;
    try
    {
        using ProductionTransaction productionTransaction = new ProductionTransaction(null);
        if (!productionTransaction.Track(label)
            || !productionTransaction.WriteInt(target, "TE_DISPOSABLE_PRODUCER", value)
            || !productionTransaction.Pay(label, 1)
            || !itemIdentity.In(parentInventory)
            || !Inventory.Owned(target))
        {
            return;                        // 任一步不成立 → 不 Commit → 全回滚
        }
        ProducerLabels.Refresh(target);
        target.Validate();
        productionTransaction.Commit();
    }
    finally
    {
        GameItemElement val = target.TryCast<GameItemElement>();
        if (val == null || !val.IsDestroyed())
        {
            ProducerLabels.Refresh(target);
            target.Validate();
        }
    }
}
```

**为什么这么写**：贴纸消耗、标签写入、目标仍在原容器，三件事必须原子。`ItemIdentity.In(parentInventory)` 用的是"指针 + uniqueId + identifier"三元比对（`ItemIdentity.cs:13-23`），比 `parentInventory == parent` 更抗 Unity 假 null。`finally` 里无条件刷一次显示，是为了兜住"标签已消耗但刷新被异常打断"导致的价格显示不一致。

### 3.4 贴纸骗不过自己的估价

```csharp
// ProducerLabels.cs:19-34
internal static long ActualEstimate(GameItem item)
{
    ItemFeature estimatingFeature = EstimatingFeature;
    int estimatingBonus = EstimatingBonus;
    try
    {
        EstimatingFeature = (Rules.IsCustomCigarette((item != null) ? item.identifier : null) ? item.FindItemFeatureByID("TE_PRODUCER_PRICE") : null);
        EstimatingBonus = Bonuses[BaseLevel(item)];        // ← 永久层，不看一次性贴纸
        return item.GetCurrentValue(false, false, true, false, 0L);
    }
    finally
    {
        EstimatingFeature = estimatingFeature;
        EstimatingBonus = estimatingBonus;
    }
}
```

```csharp
// ActualProducerEstimate.cs:5-19
[HarmonyPatch(typeof(ItemFeature), "GetCurrentValueMod")]
internal static class ActualProducerEstimate
{
    private static bool Prepare() { return DeferredPatches.Installing; }
    private static void Postfix(ItemFeature __instance, ref int __result)
    {
        if (TobaccoMod.Enabled && __instance == ProducerLabels.EstimatingFeature) { __result = ProducerLabels.EstimatingBonus; }
    }
}
```

**为什么这么写**：它要"临时改变某个 `ItemFeature` 返回值"但又不改物品本身——于是用 `[ThreadStatic]` 两个字段做"当前正在估的是哪个特征、应该报多少"，用 Harmony 在特征的方法出口改结果。比自己算一遍原版价值公式稳，也不会污染存档。**这是把 Harmony 当"作用域内改写"用的漂亮例子。**

### 3.5 存档闸门：进行中不许存档

```csharp
// ActionSaveBoundaryPatch.cs:16-30
private static IEnumerable<MethodBase> TargetMethods()
{
    yield return AccessTools.Method(typeof(PlayerStore), "SaveGame", (Type[])null, (Type[])null);
    yield return AccessTools.Method(typeof(PlayerStore), "InitialSave", (Type[])null, (Type[])null);
    yield return AccessTools.Method(typeof(PlayerStore), "WriteSlotFile", (Type[])null, (Type[])null);
}

private static bool Prefix()
{
    if (!OperationScope.IsActive) { return !ActionSafety.Blocked; }
    return false;                 // 有操作在飞 → 直接不许存档
}
```

配合 `OperationScope`（键去重的 `IDisposable` 作用域，`OperationScope.cs:21-37`）与 `ActionSafety.Generation`（每次 `LoadGame`/新档 `BeginSession` 递增，`ActionSession.cs:9-19` 用它判"我这个回调是不是已经过期"）。**思路与酿酒 `PulpOperations.CanSave` 同源，但实现更彻底**：不是每个调用点自己判断，而是在存档入口一刀切。

### 3.6 委托降级：三条降级线各管一段

```csharp
// OrderCatalog.cs:27-54（货）
if (Bridge.BrewingEnabled)
{
    /* 用 16 味型两两配对，查 CompanionRecipeRules 得到酒 id */
    foreach (KeyValuePair<string, int> item in dictionary)
    {
        OrderProduct orderProduct = CompanionWineNames.Find(item.Key, item.Value);
        if (orderProduct != null && DirectoryMaster.Has<GameItem>(item.Key))     // ← 真的注册了才上架
        {
            list.Add(orderProduct);
        }
    }
}
```

```csharp
// OrderGeneration.cs:71-79（存量订单）
if (!Bridge.BrewingEnabled)
{
    int num3 = book.Available.RemoveAll((TobaccoOrder o) => o.Status == 0 && o.SpecialStamp == 0 && o.Lines.Any((OrderLine line) => line.Kind == "wine"));
    if (num3 > 0) { book.GeneratedCount = Math.Max(0, book.GeneratedCount - num3); result = true; }
}
```

**为什么这么写**：降级不只在"生成时"做，还在**读档时**做——上游模组被关掉后，存档里已经生成的酒类委托会被主动剔除并修正计数，避免出现永远交不掉的订单。见 §6.3。

---

## 4. 踩坑与红线

1. **包装缺陷键位是错位的**：`Packaging.SyncIssues` 里 `"LOGO" => State.Int(item,"CIGARETTE_BOX") != 0`、`"SEAL" => CIGARETTE_LOGO`（`Packaging.cs:35-41`）。三个状态键和三个原版标签不对应，看起来是刻意错位（也可能就是笔误）。**抄这套必须自己重建映射并写测试**，否则"贴纸缺陷"和"盒子缺陷"会互相串。
2. **两个模组写同一个存档键**：`OrderRepository.SaveKey = "TobaccoExpansion.Orders.v1"`（`OrderRepository.cs:8`），而酿酒 `Orders.SaveKey` 也是同一个字符串、schema 却是 `WineOrderBook`（`Accepted`+`Completions`）vs `OrderBook`（`Available`+`Accepted`）。它们能共存**只因为终端的 `Standalone` 互斥**（同一时刻只有一个模组在读写）。先开酿酒终端下过单再装香烟，香烟读档会抛 `Unsupported commission save schema`（被 `Safe()` 吞成日志，见 `TobaccoMod.Safe`）。→ 我们要共存就**必须在键名或 `Schema` 号里带 owner**。
3. **手抄上游数据表**：`BrewingRecipeData.cs:8-66` 是酿酒 `BarrelWine.Resolve` 的 **18 条单体 + 34 条配对**快照。上游加酒这里**不会自动跟上**，且因为酿酒这些类是 `internal`、`ModEntry` 只暴露两个 `Func`（`ModEntry.cs:44-59`），第二部**没有合法 API 可调**。→ 跨模组取数据要么读 `DirectoryMaster` 的物品目录（它确实这么查 `Has<GameItem>`，`OrderCatalog.cs:50`），要么在对方 `IServiceProvider` 上加一个"导出配方表"的服务。
4. **吞异常**：`TobaccoMod.Safe` 把所有回调异常折成 `ModLog.Failure("callback", error)`（`TobaccoMod.cs:290-304`），`NiniCertificates` 式的大段 `try/catch` 在 `Visitors`/`Orders` 里也重复出现。**与我们"对内暴露、边界捕获"的规范相反**：它的做法让"委托状态机写坏"这类问题很难在真机日志里定位。
5. **临时物品试探口味**：`BuyerPreferences.Eligible` 为了问"这个顾客收不收烟"，`ItemDirectory.CreateEmptyItem("te_cigarette_regular")` 造一个临时物品调 `client.willAcceptItem` 再 `Destroy`，并用 `Dictionary<long,bool>` 缓存（`BuyerPreferences.cs:50-66`）。缓存键是 `Key()`（`ConditionalWeakTable` 发号），只在读档时 `Restore()` 里整体 `Clear()`（`BuyerPreferences.cs:277-279`），单次营业内不按实例回收。
6. **补丁全挂但只"半套机器"**：机器只 `SetupBatterySlot` + `machinery_drawn_power` + `onCycleEndSlotItemFunc`，没有 `MachineryHelper.InitMachinery`/`STANDARD_MACHINE_TAG`（对照 `Machines.cs:187-192`）。所以它拿不到原版标准机器的模块舱、涡轮、机器状态 UI；`State.Set(item,"TURBO_BOOST_ELIGIBLE_TAG",1)` 只是写了个标签。
7. **`_003C_003Ec` 缓存类进了反编译**：`OrderTerminal.cs:12-49` 说明匿名委托被 `UnityEvent`/协程长期持有；对应地它写了 `Release()`/`UninstallEvents()` 并在 `OnDisable` 里清 `Renderers`（`Packaging.cs:72-83`）。**这类静态 UI 表必须在读档与禁用时都清**，否则跨档残留 GameObject。

---

## 5. 对我们的可用点（点名到本项目功能）

> 两个项目：`深空电玩`（`deepspace_arcade` / `DeepSpaceArcade.dll`）、`深空当铺：可能是群友`（`freya_cornell` / `DeepSpacePawn.dll`）。

1. **【arcade · 光盘翻新可抄整套"贴纸经济"】** 现有 `arc_refurb_kit`（翻新）/`arc_disc_polish`（抛光）都是"消耗品→改物品属性"。把 `Overlabels.Apply` 的原子模式搬过来做**一次性"重印封面贴纸"**：`ProductionTransaction` 式 `Track/Pay` 保消耗、`ItemFeature.SetValueModifier` 保值、`ProducerLabels.Refresh` 式函数保显示。剥离（`Overlabels.Peel`）对应"贴纸被识破"——正好接 `freya_cornell` 的鉴定/顾客线。
2. **【arcade · 刻录委托按真值结算】** `ProducerLabels.ActualEstimate` + `ItemFeature.GetCurrentValueMod` Postfix 这套"作用域内改值"可以直接用于**委托/订单报价**：玩家贴了假标签，终端仍按 `realCondition` 与永久层算钱，不会出现"贴纸刷钱"。落地：在结算入口包一个 `try/finally` 作用域（设"当前特征 + 应报数值"两个字段）+ 1 个 Postfix，不改物品本身。
3. **【arcade · 光盘外壳的"包装缺陷图"】** `Packaging.Render` + `CigaretteUI.OnInit` Prefix 的做法（`Instantiate(ui.boxes[0])` → 换成 `_p{n}_n{n}_l{n}` 合成图）可复刻成**光盘盒三档瑕疵**（错印/划痕/假标），玩家用原版放大镜看盒。比新画一套 UI 便宜得多，且天然复用原版检视流程。
4. **【freya · 三元返回的"谁看得穿"】** `RegionalInspection.Recognizes` 返回 `bool?`、`CanClientExposeThisFeature` Prefix 只在非 null 时接管——这套写法能让我们的鉴定逻辑与 `AppraisalAssistant` 共存而不互相覆盖。直接用于 freya 的"群友顾客按阵营/关系识破真伪"。
5. **【freya · 执照与准入复用同一批 ModData 布尔】** 香烟的生育解锁直接读 `GetModDataBool("brewing_expansion","certificate_issued_1")`。freya 若要与 arcade 的执照联动，用同样的"跨模组读对方 modData 布尔 + 不存在就当 false"的最小协议即可，不必建公共程序集。
6. **【arcade · 防潮盒 = 克隆 `storage_bay`】** `MoistureBox.Create` 克隆原版储物舱、把内部 grid 改成 12×12、`ToggleAppearanceHelper.MakeSpriteCyclable` 做双外观（`MoistureBox.cs:17-60`）。我们要"光盘防划伤盒/恒温柜"照这个抄：**一次克隆，白拿原版容器的全部行为**，比自己搭 `GameGridInventory` + 窗口省一半代码。
7. **【arcade · 存档闸门】** `ActionSaveBoundaryPatch` 在 `SaveGame`/`InitialSave`/`WriteSlotFile` 三个入口统一挡"操作进行中"（比酿酒逐点判断更彻底）。arcade 的拷贝机是 `STATE_WORKING` 24/40 锁盘，正需要这条。

---

## 6. 与酿酒拓展的复用关系（Outer Ring 系列第二部怎么不重复造轮子）

### 6.1 被**原样复用**的基础设施（同名文件跨两个目录比对）

两个目录下同名文件 **20 个**，逐个 `md5` 比对：**没有一个是逐字节相同的**——全部是"改命名空间 + 换 id 前缀 + 按需增删"的**同源改编**，而不是引用或链接。差异最小的（`CommissionWindowDrag.cs`，两边都 69 行）去掉命名空间后只差一行 GameObject 名（`BE_CommissionDragSurface` → `TE_CommissionDragSurface`）。这说明作者的做法是**复制整套子系统再改**，不是抽公共库。

| 复用面 | 证据 | 改动幅度 |
| --- | --- | --- |
| 委托终端 UI 全套（`OrderTerminal` 524→455、`OrderWindow` 364→482、`OrderWindowArt` 639→658、`OrderSubmission` 198→216、`CommissionWindowDrag/Chrome`、`CommissionSelectionSurface`、`CommissionSceneLifecycle`、`CommissionTerminalHover`、`SubmissionButtonRefresh`） | 同名 + 同结构，`Schedule/Install/Build/Position` 逐段对应 | 中：换 id、换 sprite 键（`be_commission_*` → `te_asset_*`）、加香烟目录 |
| 交付/结算（`DeliveryStock` 103→103、`DeliverySelection` 98→98、`DeliveryChoice` 17→3） | `DeliveryStock` 只把"uid 比对"换成 `ItemIdentity.In(parent)`、`Orders.Value` 换成 `OrderCatalog.ActualEstimate` | 小 |
| 提示与命中（`ItemTooltipLayout` 254→287、`ItemHitArea` 68→42、`ModText` 100→55） | 同名同职责 | 中 |
| 物品注册协议（`OnModItemDirectoryInit` + `Resources.FindObjectsOfTypeAll<ModItemDirectory>` 双路 + `bindings` 精确反注册） | `ModEntry.cs:87,110-114,178-185` vs `TobaccoMod.cs:130,142-146,232-239` | **逐行同构** |
| 存档 id 归一化钩子（`SaveManager.DecodeNodes`） | 酿酒 `ItemIdentity.Normalize`（313 行）→ 香烟 `MachineInstructionsDecode`（44 行，只修说明书） | 香烟大幅缩水 |
| 过夜节拍 / "进行中不许存档" / `[ThreadStatic]` 估价值 / 克隆原版物品当模板 | `PulpOperations.CanSave` ↔ `OperationScope`；`SeedStrains` ↔ `TobaccoSeedStrains`；`RemainingItems.Template` ↔ `packet_red_cigarette`/`magnifier`/`storage_bay` | 概念复用、代码各写 |

**注意 `SeedStrains` 那一对不是简单复制**：香烟的 `TobaccoSeedStrains.GetInnateRank` 结尾是 `return brewingLookup?.Invoke(plant, id, seed) ?? plant.GetInnateRank(id)`（`TobaccoSeedStrains.cs:290`），而 `brewingLookup` 是它在 Transpiler 里**从酿酒的指令流中捕获的委托**（`TobaccoSeedStrains.cs:94-127`，判据是 `DeclaringType.FullName == "BrewingExpansion.Formal.SeedStrains"`）。即：**第二个 Transpiler 主动把第一个 Transpiler 的适配器包起来当 fallback**，而不是覆盖它。这是本批最值得抄的一手"两个模组改同一个原版方法"的解法。

### 6.2 第二部**新写**的部分

- 三套鉴定整体（`Packaging` / `ConditionLabels` / `Overlabels` / `ProducerLabels` / `ConditionMenu` / `RegionalInspection` / `StampMachine`）——酿酒没有鉴定。
- 香烟化学（`CigaretteChemistry` 复用原版 `Liquid.Liquids` + `ChemicalHelper.SetChemContentUL` + `ChemicalFeedbackHelper`，但组合逻辑是新的）。
- 基因育种（`TobaccoSeedStrains.Draw` 的正负基因池 9+6 条、`RollStock`）。
- `NativeCallbackBinding<T>` / `ManagedNative.Key()` / `OperationScope` / `ActionSafety` 这套"委托与代际"基础设施（酿酒没有；酿酒用 `MachineConnections.Live()` 做类似事）。
- `StartupReadiness`（本地化 `StringTable` 逐 fallback 等待）——酿酒没有这一步，这也是香烟"必须等前置模组就绪"的代价。

### 6.3 联合委托终端：依赖在就用它的货，不在就降级

**四个机制，各管一段，缺一条就会出坏档：**

1. **谁开这台终端**（互斥）：
   ```csharp
   // 3829730904 OrderTerminal.cs:80-90
   internal static bool Standalone
   {
       get
       {
           if (ModEntry.Enabled)
           {
               return !ModLoader.Mods.Any((LoadedMod mod) => mod.Manifest?.ID == "tobacco_expansion" && mod.IsEnabled && mod.Instance != null && string.IsNullOrEmpty(mod.LoadError));
           }
           return false;
       }
   }
   ```
   酿酒侧 `Schedule()`、`Safe()`、`Orders.Day()` 全部先过 `Standalone`；香烟装了就整台终端（桌面 Fixture、窗口、订单生成、存档）**都由香烟接管**，酿酒侧只剩一个空注册。香烟侧的对应检查在 `Bridge.BrewingEnabled`（`Bridge.cs:8-18`，同时校验 `IsEnabled`、`LoadError` 为空、`Instance` 类型名为 `BrewingExpansion.Formal.ModEntry`）。
2. **货从哪来**（`OrderCatalog.All()`，见 §3.6）：先无条件上自己的 18 条香烟，`if (Bridge.BrewingEnabled)` 才把酒的 33 组配对结果加进去，并且**每条都要 `DirectoryMaster.Has<GameItem>(id)` 为真**。没有这层过滤，上游某个酒没注册就会生成一张永远交不掉的订单。
3. **存档里已有的上游订单怎么办**（`OrderGeneration.ApplyOfferLimit`）：上游关掉后 `RemoveAll(o => o.Status==0 && o.SpecialStamp==0 && o.Lines.Any(l => l.Kind=="wine"))` 并回退 `GeneratedCount`。**降级发生在读档期，而不是等玩家点开终端才发现**。
4. **存档键归谁**：两边都写 `"TobaccoExpansion.Orders.v1"`（`Orders.cs:10,88,108` / `OrderRepository.cs:8,13,33`），靠 (1) 的互斥保证同一时刻只有一个写入者。**这一条是脆弱点**（见 §4.2），正确做法应该是"键里带 owner + `Schema` 独立"。

额外两条同款降级：
- 种子池 `OrderCatalog.SeedPool()`：无酿酒时只返回 `["te_tobacco_seed"]`，有则把 16 个 `be_*_seed` 里 `DirectoryMaster.Has` 为真的并进来（`OrderCatalog.cs:99-107`）。
- 水培生育门槛 `TobaccoHydroponics.FertilityUnlocked`：**直接读酿酒的证书布尔** `certificate_issued_1`（`TobaccoHydroponics.cs:12-22`），不另建执照体系。

**反向耦合靠"字符串契约"**：酿酒侧也写了一套针对香烟的检查——`FormalTobaccoInspection.cs:11-64` 硬编码 `CIGARETTE_TAG` / `CATEGORY_GENUINE_CIGARETTE` / `TE_TOBACCO_CONDITION` / `TE_FRAUD_PENALIZED` 这些**香烟的私有标签名**，在泥泥交易时罚 -5 声望。也就是说两部之间没有 API，只有**双方都知道的字符串与物品 id**。能用，但改 tag 名就是静默失效——这是 Outer Ring 系列真实的耦合形状。

> 既有的 `生产链与自动化四例.md` 覆盖的是酿酒的生产链/机器/配方/补丁靶点，没覆盖本节的降级协议与"克隆原版物品当模板"这套；本次已在那一篇追加 `## 补遗` 记录酿酒侧的关键实现（物品注册与 id 规则、4 张证书的准入拦截挂点、86 种酒的数据组织与年份/陈化）。
