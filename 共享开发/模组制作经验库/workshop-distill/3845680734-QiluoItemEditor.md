# 绮罗的编辑工具 / Qiluo Item Editor（运行时物品全字段编辑器）｜工坊 3845680734

> 蒸馏自反编译源码 `缓存文件夹\decomp_ws\3845680734`（57 个 .cs / 约 1.6 万行，其中 `ItemInspectorPanel.cs` 单文件 6008 行）。
> 价值定性：**它是一张"`GameItem` 上到底有哪些可改字段"的权威清单**。任何要写物品、克隆物品、改贴图、改判定文字的模组，都可以拿它当数据字典 + 踩坑清单。
> 本档案只写源码里能读到证据的东西；凡标「（推测）」的是我没读到实现、只能从调用面推断的部分。

## 0. 它长什么样（先定位，再拆字段）

| 事实 | 证据 |
| --- | --- |
| 入口极薄：`ModEntry.Init/OnEnable/OnDisable` 全部转发给 `ItemInspectorModule`；进存档后（`ModHook.OnGameLoadedInit`）建常驻 `GameObject` 挂 `ItemInspectorHost`（`[DefaultExecutionOrder(-32000)]`） | `ModEntry.cs:11-31`、`ItemInspectorModule.cs:70-81`、`ItemInspectorHost.cs:5-22` |
| 热键 `Ctrl+J`（`KeyCode 306/305` + `106`），打字时不抢键 | `ItemInspectorPanel.cs:485-488` |
| 面板坐标固定 `Rect(36,36,900,720)`，`GUI.depth = -1000` 压在游戏 UI 上 | `ItemInspectorPanel.cs:5705`、`536` |
| 9 个分区：6 个走 `MainTabs`，另 3 个（字体颜色 / 贴图与占格 / 等价交换）写死索引 6/7/8 | `ItemInspectorPanel.cs:5748`、`1806-1815`、`1852-1884` |
| 配置项只有两个：`debug_logging`、`enable_item_inspector`（`ModHelper.GetConfig`） | `Core.cs:13-19` |

`MainTabs = { "属性/名称", "标签", "数值", "特性/条件", "复制", "内容" }`（`ItemInspectorPanel.cs:5748`）。

面板分派：`_tab` → `DrawProperties / DrawTags / DrawNumericRows / DrawFeatures / DrawCopy / DrawContent / DrawFontColors / DrawAppearance / DrawPrinterRecipe`（`ItemInspectorPanel.cs:1852-1884`）。

## 1. 字段清单表（本文档主菜）

> 列说明：**面板入口** → 原版字段/方法 → 实际写入 API → 证据行号。"写入 API"是我从反编译码里抄的原样调用，不是推测。

### 1.1 分区「属性/名称」第 1 页 = 文本 + 价格 + 数量（`DrawProperties`，`ItemInspectorPanel.cs:2068-2106`）

| 面板项 | 原版字段 | 写入 API | 证据 |
| --- | --- | --- | --- |
| 名称 | `GameItem.name`（getter 被 Patch 用于剥色，所以必须读 `RawName`） | `item.SetName(Name.Trim())` | `ItemDraft.cs:115`、`ItemNameColors.cs:16-23,73` |
| 短说明 | `shortDescription` | 直接赋值 | `ItemDraft.cs:116` |
| 长说明 | `longDescription` | 直接赋值 | `ItemDraft.cs:117` |
| 风味文本 | `flavorText` | 直接赋值 | `ItemDraft.cs:118` |
| 自定义文本 | `customText` | 直接赋值（"内容"页也写这一项） | `ItemDraft.cs:119`、`4239-4274` |
| 公司名 | tag `manufacturer`（+ 私有 tag `deep_space_pawn_framework_company_color` / `..._suffix`） | `ItemCompanyLabel.Write / WriteColor / WriteSuffix`，同时写 `state` 与 `modifiedState` 两份 | `ItemCompanyLabel.cs:124-260` |
| 数量 | `unitCount` | `item.SetUnitCount(value)`，范围 1–999999 | `ItemDraft.cs:81,120` |
| 售价 | `unitValue` | 直接赋值（0–long.Max） | `ItemDraft.cs:85,122` |
| 基础价 | `unitBaseValue` | 直接赋值 | `ItemDraft.cs:121` |
| 迟售价 | `lateUnitValue` | 直接赋值 | `ItemDraft.cs:123` |
| 备份价 | `backupUnitValue` | 直接赋值 | `ItemDraft.cs:124` |

收尾三连（**任何改物品的模组都该照抄**）：`item.ValidateShapeState()` → `item.Validate()` → 刷新柜台/当铺 UI（`ItemDraft.cs:139-141` → `ItemInspectorUtil.cs:147-185`）。

### 1.2 分区「属性/名称」第 2 页 = 数值 + 类型 + 行为开关（`2068-2128`）

| 面板项 | 原版字段 | 写入 API | 证据 |
| --- | --- | --- | --- |
| 命中加成 | `bonusAccuracy` | 直接赋值（-9999..9999） | `ItemDraft.cs:101,125` |
| X / Y 偏移 | `modifiedXOrigin` / `modifiedYOrigin` | 直接赋值 | `ItemDraft.cs:126-127` |
| 物品类型 | `itemTypes`（`List<string>`） | **先 `RemoveAllGameItemType()` 再逐个 `SetGameItemType(x)`**，逗号/分号/竖线/换行分隔 | `ItemDraft.cs:134-138`、`ItemInspectorUtil.cs:43-60` |
| 六个行为开关 | `forceDisableActivate`（禁止激活）/ `forceDisableUse`（禁止使用）/ `activateDefault`（默认激活）/ `isCombatBackpack`（战斗背包）/ `canUseOutsideCombat`（战斗外可用）/ `triggerOverwatch`（触发警戒） | 逐个直接赋值 | `ItemDraft.cs:128-133` |

### 1.3 分区「标签」= 任意 `TagState`（`TagDraft.cs` 是完整的 tag 编辑器）

两套系统并列可选：`item.state`（原始）与 `item.modifiedState`（"修改后/派生"，`ItemInspectorUtil.SelectTagSystem`，`ItemInspectorUtil.cs:120-128`）。

| 面板项 | 原版字段 | 写入 API | 证据 |
| --- | --- | --- | --- |
| 显示名称 | `TagState.identifierName` | `UpdateReadableName(name)`（空则直赋） | `TagDraft.cs:65-77` |
| 启用状态 | `TagState.valueEnabled` | `SetEnabled(bool)` | `66` |
| 字符串值 | `valueString` | `SetString` | `78-81` |
| 整数 / 长整 / 浮点 / 双精度 | `valueInt` / `valueLong` / `valueFloat` / `valueDouble` | `SetInt` / `valueLong=` / `SetFloat` / `SetDouble`（带 ε 比较，没变就不写） | `82-97` |
| 布尔值 | `valueBool` | `SetBool` | `98-101` |
| 删除标签 | —— | `SetEnabled(false)` → `DisableTag(key)` → `dict.Remove(key)`，**并同步移除同名 itemType** | `ItemInspectorPanel.cs:2886-2946`、`2981-3003` |

预设：`ItemInspectorPresets.Tags` 共 **72 条**中文原版标签（其中大量 `isItemType:true`，添加时连 itemType 一起加，`ItemInspectorPanel.cs:2752-2761`）+ 违禁品 5 档（`ItemInspectorPresets.cs:8-91`）。鱼/老鼠两条走 `BiologicalTagPresets.Apply` 补物种与基础数值（`BiologicalTagPresets.cs:17-139`）。

### 1.4 分区「数值」= 184 个原版数值标签（`NumericPresets.cs:14-35` 声明 `new string[184]`）

| 机制 | 说明 | 证据 |
| --- | --- | --- |
| 类型推断 | 后缀 `_LONG/_DOUBLE/_FLOAT`、遗传值、`_BONUS` 等特殊名单决定读 `valueLong/Double/Float/Int` | `NumericPresets.cs:162-193` |
| 分组 | `Classify(id)` 归到"食品与成分 / 使用次数 / 耐久与维修 / 电力与充能 / 机器与生产 / 动物与养殖 / 种植 / 飞船系统 / 价格与交易 / 票券与收藏…" | `NumericPresets.cs:276-359` |
| 每页 12 条 | `ValuePages` 按组切页 | `118-150` |
| 中文名 | `NumericLabels.Get(id)`（如 `AGE_INT`→陈化天数、`PLANT_HEALTH`→植株健康） | `NumericLabels.cs:8-196` |
| 单位缩放特例 | `LIQUID_CONTAINER_CAPACITY` 界面毫升 ↔ 内部 `valueInt` **×1000**；`PLANT_HEALTH_INT` 百分比 ↔ **×100** | `NumericPreset.cs:28-31, 50-67, 89-92` |
| 真值不在 tag 里的三项 | `WATER_USE_RATE` / `NUTRIMENT_USE_RATE` / `PLANT_HEALTH` 原版硬编码在 `HydroponicHelper` → Harmony Postfix + 私有标记 tag `deep_space_pawn_framework_numeric_overrides` | `NumericOverrides.cs:25-125` |

### 1.5 分区「特性/条件」= `ItemFeature` 全字段（`FeatureDraft.cs`）

| 面板项 | 原版成员 | 写入 API | 证据 |
| --- | --- | --- | --- |
| 特性 ID | `identifier` | 直赋（空则不动） | `FeatureDraft.cs:120-123` |
| 分类 | `category` | 直赋 | `124` |
| 公开显示 / 实际显示 | `publicDisplay` / `actualDisplay`（**私有字段**） | `SetPublicDisplay / SetActualDisplay` | `125-126`、`PrivateFieldAccess.cs:7-38` |
| 移除工具 | 内部字段（读 `GetToolNeeded()`） | `SetTool(str)` | `127`、`67` |
| 价值修正 / 暴露前修正 | 私有 `valueModifier` / `preExposeValueModifier` | `SetValueModifier(int)` / `SetPreExposedValueMod(int,bool)` | `128-129` |
| 自定义整数 | `customIntValue1` | 直赋 | `130` |
| 初始显示 | 私有 `initiallyShown` | `PrivateFieldAccess.Write` | `131` |
| 已暴露 / 已发现 / 正在暴露 / 匹配 / 可暴露 / 公开隐藏 / 禁用 | `SetExposed / SetFeatureDiscovered / isExposing / isFeatureMatch / isExposable / SetPublicHidden / isDisabled` | 对应 setter / 直接赋值 | `132-138` |
| 使用条件 | `useCondition` | 直赋（**最后一个写**，见下节坑） | `169` |
| 假条件 / 真条件 | `fakeCondition` / `realCondition`（`ItemCondition`） | 成对 `feature.SetConditions(fake, real)` | `139-168` |
| 类型 / 阶段（只读展示） | `featureType` / `valueStage` + `InspectorNames.FeatureTypeName/ValueStageName` | 不可改 | `61-62`、`3681` |
| 删除特性 | —— | `item.RemoveItemFeatureByID(identifier)`，无 ID 时 `itemFeatures.RemoveAt` | `ItemInspectorPanel.cs:3911-3928` |
| 新增/复制特性 | —— | `new ItemFeature(FeatureType)` + `parentItemUniqueId = item.uniqueId` + `item.AddItemFeature` | `FeatureDraft.cs:191-204`、`ItemInspectorPanel.cs:3943-3974` |

条件本身的 10 个字段（`ConditionDraft.cs:8-28, 105-114`）：`display`、`identifier`、`category`、`onActivateActionId`、`modValue`、`modValueDisplay`、`customValue`、`compareValue`、`isTransformative`、`hiddenAsPublic`。

特性预设 **约 108 条**（92 条手写 + 4×4 注射器"标示/实际"组合由循环生成，`ItemInspectorPresets.cs:192-202`），全部调用原版工厂方法而不是手搓数据：`ItemFeatureList.FreeFeature / ShabbyFeature / ReplicaFeature / SecondHandFeature / CigaretteAuthenticity / StampCounterfeit / EquipementConditionFeature / CreateWineQualityFeature / Bargain* / Contraband*MarkUp / DiscountedBuy* / StolenBonusBuying* / InjectorFeature …` + `DurabilityHelper.FeatureDurability` + `IngotPurityHelper.FeaturePurity` + `MachineFridge.CreateChilledFeature` 等（`ItemInspectorPresets.cs:95-204`）。

### 1.6 分区「内容」= 字条/手记/教程页/回执正文（`ItemContentFields.cs`）

判定规则（不是硬编码列表，是**三条规则叠加**）：

1. `KnownOrder` 19 个键优先排序：`TUTORIAL_PAGE_TITLE/BODY/SIGNATURE`、`SLIP_TITLE/BODY/SIGNATURE/SIGN/DATA`、`HANDNOTE_*`、`CASSETTE_CONTENT`、`CONTENT/TITLE/BODY/SIGNATURE/TEXT/PAGE`（`11-15`）；
2. `KeyTokens` 12 个词命中即算正文（`BODY/TITLE/SIGNATURE/SIGN/CONTENT/TEXT/SLIP/HANDNOTE/TUTORIAL_PAGE/LETTER/PAPER/NOTE`），但先被 `ExcludedTokens` 12 个词排除（`_TAG/MULTIPLIER/PROTECTION/PRIORITY/BUDGET/THRESHOLD/TOLERANCE/ARREST/INSPECTION/VENDOR/REQUISITION/SURPLUS`）（`17-27, 29-62`）；
3. 例外：任意 tag 的 `valueString` **≥60 字**也当正文收录（`LongTextThreshold=60`，`9, 139-147`）。

写入：`TagState.SetString(text)` + `RecordTagEdit`（`149-185`）。`sec_slip`（治安部回执）额外特例：同时写 `state` 与 `modifiedState` 的 `title`/`body`，并用 Harmony 回填视图文字（`ItemRuntimeRepairs.cs:74-116`）。

### 1.7 分区「字体颜色」= 富文本 `<color=#RRGGBB>`

| 面板项 | 实现 | 证据 |
| --- | --- | --- |
| 7 类可改色字段 | 短说明 / 长说明 / 风味文本 / 名称 / 名称标签（`CUSTOM_NAME_TAG`，"标签机"自定义名）/ 特性文字（publicDisplay/actualDisplay/fakeCondition.display/realCondition.display）/ 公司标签 | `ItemColorField.cs:25-144`、`2257-2271` |
| 整段涂色 | `VisibleColorText.Apply(source, 0, plainLength, hex)` | `2329-2333` |
| 局部涂色（拖选） | `VisibleColorText.Apply(source, selStart, selEnd, hex)` | `4967-5005` |
| 42 色调色板 | `DescriptionColorPresets.All` | `DescriptionColorPresets.cs:7-51` |
| HSV 取色器 | 24 色相条 + 6×10 饱和/明度格 + 三滑条 + Hex 预览 | `ItemInspectorPanel.cs:4838-4897` |
| 清色 | `FeatureColorScope.RemoveColorTags` | `2312-2315`、`FeatureColorScope.cs:28-35` |
| 字号（小/中/大三档 → `<size=85%/140%>`） | `ItemTextSize.ApplySelection(source,start,end,size)` | `ItemTextSize.cs:14-113`、`ItemInspectorPanel.cs:4738-4768` |

### 1.8 分区「贴图与占格」

| 面板项 | 实现 | 证据 |
| --- | --- | --- |
| 换贴图 | 写私有 tag `deep_space_pawn_framework_sprite_override` = `"atlas\nname"`，随后 `item.SetSprite(item.spriteAtlasPath, item.spritePath)` 触发重算 | `ItemSpriteOverride.cs:52-55`、`ItemInspectorPanel.cs:2017-2047` |
| 贴图搜索目录 | 37 个原版图集常量表（`items_*` / `water_container*` / `dossier` / `beer_case`…），实际用 `Resources.LoadAll<Sprite>("Items/"+atlas)` 枚举 | `ItemAppearance.cs:31-52`、`54-119` |
| 中文贴图名 | 从 `LocalizationSettings.StringDatabase` 的 `Item` 表按 `atlas/sprite|item_key` 映射表反查 | `ItemAppearance.cs:167-211` |
| 占格长宽（1–128） | `item.shape = new GridShapeBuilder().SetDataFill(w,h,1).SetTransform(source).Build()`，再用 `ComputeModifiedShapeState` 重算 `modifiedShape` 与原点 | `ItemAppearance.cs:284-292`、`342-362` |
| 放不下则整体回滚 | `parentInventory.CheckInventorySlotStillValid(...)` 失败即还原 shape/modifiedShape/原点 | `ItemAppearance.cs:294-340` |

### 1.9 分区「等价交换」= 3D 打印芯片配方（唯一可改的机器数据）

7 个原版键（`PrinterChipRecipe.cs:16`）：`PROGRESS_ITEM_USE_ADDITIONAL_ITEMS`、`REQUIRED_ITEM_TYPE_COUNT_TAG`、`PROGRESS_ITEM_TARGET_TAG`（工作量）、`PROGRESS_ITEM_TARGET_ITEM_ID_TAG`（产物）、`PROGRESS_ITEM_TARGET_ITEM_COUNT_TAG`（产量）、`PRINTER_GUN_PART_CHIP_TAG`、`PRINTER_MELEE_CHIP_TAG`；原料逐条 `REQUIRED_ITEM_ID_{i}` / `REQUIRED_ITEM_COUNT_{i}`。
写入走 `MachineProgressHelper.SetAdditionalRequiredItems(chip, ingredients)` + `chip.ModifyTag(key, tag => tag.Enable().SetInt/SetString(v), false)`，成功后清掉枪械件/近战芯片标记（`51-166`）。

## 2. 安全边界：它到底拦了什么、怎么拦

**唯一的编辑准入**（比"是不是物品"更严）：

```csharp
// ItemEditPolicy.cs:5-26
internal static bool CanEdit(GameItem item)
{
    try
    {
        object obj;
        if (item == null) obj = null;
        else { TagSystem state = item.state; obj = ((state != null) ? state.dict : null); }
        TagState value;
        return obj != null && item.state.dict.TryGetValue("IS_OWNED_TAG", out value)
            && value != null && value.valueEnabled && GeneralHelper.IsItemEffectivelyOwned(item);
    }
    catch { return false; }
}
```

→ 判定链是 **`IS_OWNED_TAG` 标签 + `GeneralHelper.IsItemEffectivelyOwned` 双条件**。复制、属性、标签、数值、特性、内容、贴图全部先过这一关（`RequireEditable` → `CanEditItem`，`ItemInspectorPanel.cs:669-683`）。

**受保护数值黑名单**：题目里"关联剧情交付、许可或身份定位的受保护数值仅展示原因，禁止新增/修改/删除"，落到代码就是**一个 4 条的 `switch`**：

```csharp
// NumericEditPolicy.cs:5-15
internal static string Reason(string id)
{
    return id?.ToUpperInvariant() switch
    {
        "LOU_TOY_COUNT_INT" => "不支持修改：玩具收集数量参与原版剧情交付和后续事件判定。",
        "INTEL_ID_INT"     => "不支持修改：情报编号用于识别原版情报内容，改写可能影响剧情关联。",
        "PERMIT_ITEM_COUNT"=> "不支持修改：许可证数量参与原版许可发放与交付判定，可能影响流程推进。",
        "ROOM_ID_INT"      => "不支持修改：房间编号属于原版场景定位数据，改写可能影响事件目标。",
        _ => null,
    };
}
```

它的**"受保护"不靠猜物品，而靠一张只列 4 个 ID 的禁止表**（黑名单，不是白名单：没列出的一律放行）。关键是：这一个函数被**所有写入口**都调了一次，形成单点权威 —— `DrawNumericRows:3157`（只展示原因、不渲染输入框）、`ApplyNumeric:3233`、`AddNumericTag:3327`、`RemoveNumericTag:3468`、`NumericSupport.AddChoices:246` / `AddHint:256` / `Prepare:282`、`DrawTagDetail:2801`（详情页直接只显示原因 + 返回按钮）、`ApplyTagDraft:2858`、`TryRemoveTag:2893`、`AddPresetTag:2679`。

其余边界（都是硬拦，不是提示）：

| 边界 | 行为 | 证据 |
| --- | --- | --- |
| 原版未启用系统 | `CHARGE_*`（技师背包）、`DRAW_*`（飞船导航充能）明确拒绝并说明 | `FunctionalNumericConversion.cs:64-83` |
| 功能转换前必须单件 | `item.unitCount != 1` 拒绝，避免整叠共用一个机器状态 | `139-164` |
| 功能转换前必须掏空 | 有内部物品就拒绝（"原有内容不会被自动清除或转移"） | `159-163` |
| 目标必须是已发布原版物品 | `DirectoryMaster.GetIdentifierList<GameItem>("UnusedDirectory")` 命中即拒绝 | `178-185` |
| 遗传基础值不可删 | 只允许改值 | `ItemInspectorPanel.cs:3474-3478` |
| 液体容量不可直接删 | 先要求倒空（`WaterHelper.GetCurrentCapacityUL > 0` 拒绝） | `NumericSupport.cs:479-527` |
| 芯片正在打印中不可改配方 | `GeneralHelper.IsItemLocked` | `PrinterChipRecipe.cs:57-60`、`ItemInspectorPanel.cs:5767-5772` |

## 3. 数值与标签的坑：「新增时自动补齐支持的关联标签」到底补什么

`NumericSupport.AddHint` 里那句"自动补齐关联项"（`254-278`）对应 `NumericDependencies.Complete`（`NumericDependencies.cs:34-151`）。**纯写一个 `MAX_USE_COUNT_INT` 是没用的**，原版读的时候还要别的 tag 配合：

| 你加的数值 | 自动补齐的关联标签 | 漏了会怎样 | 证据 |
| --- | --- | --- | --- |
| 任意 `*USE_COUNT*` | `USE_COUNT_ITEM_TAG`；`MAX_USE_COUNT_INT` ↔ `CURRENT_USE_COUNT_INT` 互填；`USE_COUNT_TRANSFERABLE_BOOL / USE_COUNT_DESTROY_ON_EMPTY_BOOL / USE_COUNT_AUTO_VALUE_BOOL` 全设 false；`USE_COUNT_REFILL_DAYS_INT` ↔ `USE_COUNT_REFILL_PROGRESS_INT` 配对 | 物品有数值但没有"使用次数系统"标记，游戏不显示次数、用完也不消耗 | `40-63` |
| 任意 `*DURABILITY*`（`DURABILITY_TAG`/`MAX_`/`DEFAULT_`/`DURABILITY_ZERO_VALUE_TAG`） | 三件套统一取 `max(当前, 最大, 1)`、`DURABILITY_PRICING_MODE_TAG = 1`，并 **`item.AddItemFeature(DurabilityHelper.FeatureDurability())`** | 数值在但没有耐久特性 → 界面不显示耐久、损坏逻辑不触发 | `64-85` |
| `AGE_INT` ↔ `AGE_VALUE_MOD_PER_DAY_INT` | 互相补（陈化：有天数必须有每日价值变化） | 陈化不生效/除零 | `86-93`（`CanAdd` 里也做了前置判定 `92`） |
| 任意 `power_source_item_*` / `POWER_SOURCE_ITEM_VALUE_PERCENT_INT` | `power_source_item` 总标签、`max_energy` ↔ `energy` 互填、`power_source_item_bandwidth = 10` | 放电/充电读不到电量上限 → 电池变砖 | `94-108` |
| 任意 `BONUS_PERCENTAGE_* / TEMP_PERCENTAGE_* / LEARNING_ALGO_MAX_*`（模块类） | `MODULE_TAG`、`MODULE`、`MODULE_TYPE`（默认 `MODULE_TYPE_UNIVERSAL`）、类型同名 tag、`SHIP_MODULE_TAG` 或 `EQUIPMENT_MODULE_TAG`；**并往 `item.itemTypes` 里塞 `"MODULE"`**；`LEARNING_ALGO_*` 还要占一个 `MODULE_EFFECT1/2_TAG` 效果槽（含 `incompatibleWithEffects` 冲突检查） | 模块加成不参与计算；或与已有模组效果冲突 | `ModuleNumericSupport.cs:41-151` |
| 遗传值（`ANIMAL_GROWTH_RATE_TAG` 等） | 写 `*_ALLELE_A` / `*_ALLELE_B` 两条等位基因（数值取两者平均回读） | 动物遗传值为 0 / 表现值不对 | `NumericRules.cs:10-15, 177-187` |
| `USE_COUNT_BASE_VALUE_INT` / `USE_COUNT_VALUE_PER_USE_INT` | 反算 `item.unitValue`，并把 `USE_COUNT_AUTO_VALUE_BOOL` 置 true | 价格与剩余次数价格脱钩 | `NumericRules.cs:154-168` |
| `RAS_*`（循环水养殖） | `ValidateShapeState()` + `MachineRAS.ApplyTankSize(item)` + `MachineRAS.UpdatePowerUsage(item)` | 设备占格/耗电与升级数不一致 | `NumericRules.cs:190-198` |
| `power_source_item_recharge` | 补 `BREEDER_POWER_SOURCE_ITEM_TAG`（回充电池） | 充不进电 | `147-153` |

反过来，**加之前会先判"这个物品配不配"**：`NumericSupport.CanAddNative` 是一棵大 `switch`（`NumericSupport.cs:78-241`），按 id 前缀要求对应 tag 存在（`GUN_` 要 `GUN_CALIBER_TAG`、`FISH_` 要 `FISH_ITEM_TAG`/`FISH_GROUP_TAG`、`PLANT_/NUTRIMENT/WATER_USE_RATE` 要 `SEED_ITEM_TAG`/`GROWING_TAG`、`RAS_` 要 `RAS_TAG`、`MODULE`/`BONUS_PERCENTAGE_`/`LEARNING_ALGO_` 要 `MODULE_TAG`…），不满足就只允许走"功能转换"或直接报错（`Prepare:303-314` 的 `"无法自动补齐…仅添加标签不能建立该功能"`）。`TOTAL_PERCENTAGE_*` 一律只读，提示"游戏自动计算"（`270-272`）。

**删除时也会连带清理**：`CleanupNumericDisplayTags` 发现同组标签全没了，就顺手删掉 `USE_COUNT_ITEM_TAG` / `power_source_item`（`ItemInspectorPanel.cs:3540-3554`），模块类还会还原或移除先前加的 `MODULE_TAG`/`MODULE` 类型（`ModuleNumericSupport.cs:173-242`，靠 `deep_space_pawn_framework_module_added_*` / `_original_*` 这对记账 tag）。

## 4. 特性/真伪条件怎么编才不破判定

编辑契约（这是最值得学的地方）：**就地修改同一个 `ItemFeature` 对象，不重建** ——

1. `FeatureDraft.From(feature)` 一次性把所有字段读成可编辑字符串草稿（含 6 个私有字段，用 `PrivateFieldAccess.Read` 走官方 `ModReflect`，`FeatureDraft.cs:53-84`）；
2. 面板按 5 页改草稿（列表 / 基本信息 / 标记 / 假条件 / 真条件，`ItemInspectorPanel.cs:3661-3703`）；
3. `FeatureDraft.Apply(feature, clearFake, clearReal)` 才写回**原对象**（`100-176`）。

不破坏原版判定的四个细节：

- **`SetConditions(fake, real)` 成对提交**，且只有"勾了清除 / 草稿有数据"才提交，否则完全不动原条件（`139-168`）。这样"只改显示文字"绝不会顺手把 `realCondition` 抹成 null。
- **空条件不造对象**：`ConditionDraft.HasData` 逐字段判空（`ConditionDraft.cs:30-40`），全空就当"没有条件"。
- **`useCondition` 最后写**（`FeatureDraft.cs:169`），且 `ItemColorField` 里用 `useCondition` + `IsExposed()` 判断"当前显示的是公开文字还是实际文字"并打上"（当前显示）"标记（`ItemColorField.cs:101-125`）—— 作者很清楚**显示文字 ≠ 判定依据**：判定永远只认 `realCondition`，`publicDisplay/fakeCondition` 只是给玩家看的假象。
- **复制/新增才 new**：`ToNewFeature(parentUniqueId)` 里 ID 为空会自动生成 `deep_space_pawn_framework_custom_<ticks>`，并把 `parentItemUniqueId` 指向宿主物品；添加后立刻提示"请检查游戏是否接受这个特性 ID"（`ItemInspectorPanel.cs:3956-3968`）。

预设走工厂方法而不是手搓字段，是"不破判定"的根本保证：这批预设（约 108 条）全部调 `ItemFeatureList.*` / `DurabilityHelper.FeatureDurability` / `MachineFridge.CreateChilledFeature` 等原版构造器（`ItemInspectorPresets.cs:95-204`），尤其**注射器 4×4 的"标示 vs 实际"组合**（`192-202`）——这是原版真伪机制的标准用法示范：`InjectorFeature(shown, actual)` 两个参数分别决定"看起来"和"真的"。

## 5. 复制与转换

### 5.1 复制：快照 → 重建 → 校验 → 原件回滚

`ItemDuplicator.CloneRestored`（`ItemDuplicator.cs:69-151`）的顺序值得整套照抄：

1. 先算快照：`state`/`modifiedState` 全 tag `Clone()`、`SnapshotFeatures`（转 `FeatureDraft`）、原件 `uniqueId / name / unitCount`；再用 `DirectoryMaster.Item(identifier, true)` 生成"玩家物品"（失败退 `ItemSpawner.Spawn`，`86-102`）；
2. **补 uniqueId**：`PlayerStore.Instance.GetUniqueID()`（`111-126`）；
3. `CopyScalars`：标识/名称/四条文本/四个价格/六个开关/`spriteAtlasPath+spritePath+useSpriteFromMod`（`SetSprite`）/`shape+modifiedShape+原点`/`itemTypes` 逐条 `SetGameItemType`（`179-243`）；
4. `RestoreFeatureParents` + `RebuildFeatures`（用草稿重放，不用模板，`1130-1207`）；
5. **整体替换 tag 系统**：`copy.state = BuildTags(snapshot)`、`copy.modifiedState = BuildTags(snapshot2)`（`130-139`、`1266-1289`）——比逐 tag 对齐更彻底，代价是要自己保证不漏 key；
6. `RestoreStorageTabs`：`StorageExpansionHelper.RestoreTabs(copy)` 重建储藏分页（`153-167`）；
7. 容器内容递归复制（≤4 层，`CopyAllContents/CopyContents/CopyStorageTabContents`）；
8. **`RestoreSourceIfChanged`**：比对原件特性/标签/名字/数量，被自己改动过就反向修复并 `Log.Warning`（`1323-1371`）。这条"改别人先备份，改坏了自动还原"是防手抖的关键。

容器内容里的三个硬保护（`245-418`）：

- `FindOwnInventory` 只认"父物品就是自己"的那个 `GameInventory`，出现多个候选就不认（`804-856`）；
- 复制体内容区若与原件是**同一个** inventory，立刻停手并提示反馈作者（`282-286`）；
- 内部物品按 `modifiedShape.minX/minY/orientation` 还原格位，`Validate()` 后格位会被游戏重算，所以**再对齐一次**（`576-598`、`356-395`）。

校验用"指纹多重集"而不是逐字段：`Fingerprint = identifier|name|x数量|tag数|内容摘要`（`790-802`），比较原件与复制体的多重集差异（`600-651`）。

### 5.2 一键转换（电池 / 心智重编程器 / 循环水养殖设备 / 刮刮乐 / 电流转换器 / 高级增压器）

**不是换 `itemTypes`，是换整个物品的原版 identifier 并复用同一个对象。** 映射表在 `FunctionalNumericConversion.Factory`（`17-46`）：

| 数值 | 目标原版标识 | 中文标签 |
| --- | --- | --- |
| `power_source_item_energy/max_energy/POWER_SOURCE_ITEM_VALUE_PERCENT_INT` | `energy_credit` | 电池 |
| `power_source_item_recharge` | `energy_credit_breeder` | 回充电池 |
| `HYPERTECH_BATTERY_OUTPUT_INT` / `_UPGRADE_COUNT_INT` | `energy_credit_hyp` | 超科技电池 |
| `MIND_REPROGRAMMER_CHARGE_INT` / `_COOLDOWN_DAYS_INT` | `mind_reprogrammer` | 心智重编程器 |
| `RAS_HEIGHT/WIDTH/POWER_UPGRADE_COUNT_TAG` | `ras` | 循环水养殖设备 |
| `SCRATCHER_LUCK_INT` | `scratcher` | 刮刮乐 |
| `MACHINERY_DRAWN_POWER_BASE` | `current_converter` | 电流转换器 |
| `CURRENT_CHARGE_TAG` | `turbo_booster_adv` | 高级增压器 |

实现手法（`189-224`）：`FunctionalItemSnapshot` 先存全字段 + **`GameItemFunc` 上所有委托**（`23-61`）；设 `[ThreadStatic] pending = item`；`build()` 内部调 `DirectoryMaster.Create<GameItem>(factory)`，而 `ItemDirectory.CreateEmptyItem` 被 Harmony Prefix 拦住**直接返回 pending 这个对象**（`119-128`）；随后 `ModReflect.Set(item, "identifier", factory)`，`PreserveData()` 把展示字段/标签/类型/特性/贴图回填，清掉 `forceDisable*`，`ValidateShapeState()`；任何一步抛异常就 `Restore()` 整单回滚（`202-223`）。

一句话：**"在原对象身上重新跑一遍原版构造逻辑"**，比手工拼 tag 稳，也比 `ItemSpawner` 新建再搬家少一半坑。

## 6. UI 架构：6008 行单文件怎么撑住的

- **框架：纯 IMGUI（`OnGUI`）**，不用 UGUI/UIToolkit。`OnGUI` 统一保存/恢复 `GUI.matrix / color / contentColor / backgroundColor / depth`，`depth=-1000` 盖住游戏 UI（`496-563`）。
- **样式：自建 GUIStyle 树**（`EnsureStyles`，`5489-5700`），字体优先从系统里找 `Microsoft YaHei / SimHei / Noto Sans CJK / Arial Unicode MS` 动态生成（`5590-5598`），背景贴图由 `InspectorTheme.Surface/DrawFrame` 现画。
- **布局：光标式流式排版**。`BeginContent(y)` 设原点 → `NextRow(h)` 返回下一行 Rect → `DrawScrollablePage(drawRows)` 包一层滚动（`4299-4310`、`3068-3110`）。所有复杂页面都是"往下一个 row 里塞控件"。
- **分区不靠一棵大树，靠页码**：每个分区自带二级 `DrawPageNav`（特性 5 页、标签 2 页、数值 2 页）+ `DrawPager` + `DrawPresetGrid`（`5314-5489`）。列表类每页固定条数：特性 8、标签 8、内容 6、数值 12、贴图 5 列虚拟滚动（只画可见行，`1986-1988`）。
- **输入自己管**：不用 `GUI.TextField` 的焦点，自维护 `_focusField / _editText / _caret / _selStart / _selEnd / _selAnchor`，自己画光标、选区、剪贴板（`1194-1530`、`4440-4560`），并显式开关 Unity IME（`EnsureImeOn / RestoreImeMode / SetGuiImeMode`，`5258-5333`）＋ `_hitRects` 命中表 + `Event.Use()` 吃掉面板内事件（`537-543`）。
- **抢输入的两道闸**：`ShouldBlockGameInput()` 判断"鼠标在面板上/正在拖选/正在拖面板/本轮在面板内按下"，`ItemInspectorModule.UpdateInputGate` 直接把 `InputActionManager.current.enabled=false` 并 `Reset()`（`ItemInspectorModule.cs:96-115`），另有 `InspectorPointerShield` 挡世界射线（`ItemInspectorHost.cs:10-34`）。
- **实时预览**：`DrawColorPreview(rect, raw)` 用 `richText=true` 的 GUIStyle 直接渲染带 `<color>` 的字符串（`4446-4486`）；正文编辑器里每帧把当前选区色/字号应用到显示串（`4556-4737`）。
- **崩溃可运维**：`OnGUI` 整体 try/catch + `_debugStep`（"header"/"status"/"target"/"footer"…）记录卡在哪一步 + 2 秒节流 `Log.Error`（`539-553`）；`Tick()` 也整体 try/catch（`490-493`）。

## 7. 可复用范式（4 个，原样代码 + 行号）

### 范式 A · 运行时字段编辑器骨架：Draft ⇄ 原版对象

先 `From` 拍扁成字符串草稿（UI 只跟字符串打交道），校验全部集中在 `Apply`，最后三连刷新。

```csharp
// DeepSpacePawnModFramework.UI.ItemInspector\ItemDraft.cs:48-73
internal static ItemDraft From(GameItem item)
{
    return new ItemDraft
    {
        Name = (ItemNameColors.RawName(item) ?? string.Empty),
        ShortDescription = (ItemNameColors.ReadRaw(() => item.shortDescription) ?? string.Empty),
        FlavorText = (ItemNameColors.ReadRaw(() => item.flavorText) ?? string.Empty),
        UnitCount = item.unitCount.ToString(CultureInfo.InvariantCulture),
        UnitValue = item.unitValue.ToString(CultureInfo.InvariantCulture),
        ItemTypes = ItemInspectorUtil.JoinTypes(item),
        ForceDisableActivate = item.forceDisableActivate,
    };
}
// ItemDraft.cs:113-142（Apply 尾部，校验都在前面）
try
{
    item.SetName(Name.Trim());
    item.SetUnitCount(value);
    item.unitBaseValue = value3;
    item.unitValue = value2;
    foreach (string item2 in ItemInspectorUtil.SplitTypes(ItemTypes))
        item.SetGameItemType(item2);
    item.ValidateShapeState();       // 改完 shape/tag 必须重算占位
    ItemInspectorUtil.ValidateItem(item);   // item.Validate()
    ItemInspectorUtil.RefreshGameUI();      // 柜台 + 当铺界面
    return null;
}
catch (Exception ex) { return "应用属性失败：" + ex.Message; }
```

### 范式 B · 受保护字段：单点策略函数 + 全写路径拦截

```csharp
// NumericEditPolicy.cs:5-15 —— 黑名单只有一处定义
internal static string Reason(string id) => id?.ToUpperInvariant() switch
{
    "LOU_TOY_COUNT_INT"  => "不支持修改：玩具收集数量参与原版剧情交付和后续事件判定。",
    "INTEL_ID_INT"       => "不支持修改：情报编号用于识别原版情报内容，改写可能影响剧情关联。",
    "PERMIT_ITEM_COUNT"  => "不支持修改：许可证数量参与原版许可发放与交付判定，可能影响流程推进。",
    "ROOM_ID_INT"        => "不支持修改：房间编号属于原版场景定位数据，改写可能影响事件目标。",
    _ => null,
};

// NumericSupport.cs:280-313 —— 每个写入口先问策略，再问"配不配"
internal static string? Prepare(GameItem item, NumericPreset preset, bool allowConversion = false)
{
    string text = NumericEditPolicy.Reason(preset.Id);
    if (text != null) return text;                       // 受保护：给原因，不给输入框
    string text2 = FunctionalNumericConversion.Unavailable(preset.Id);
    if (text2 != null) return text2;                     // 原版未启用系统
    if (!CanAdd(item, preset) && !item.state.dict.ContainsKey(preset.Id))
        return "无法自动补齐“" + preset.Label + "”：缺少对应原版物品功能、物种或设备定义；仅添加标签不能建立该功能。";
    return null;
}
```

### 范式 C · 富文本局部改色：保留原 tag、只插新 tag

按"字符序号"遍历、用栈维护当前颜色上下文，出栈入栈时自动闭合 `<color>`，所以**已有颜色不会被破坏**，只在选区多包一层。

```csharp
// VisibleColorText.cs:34-101（核心 28 行）
internal static string Apply(string source, int start, int end, string hex)
{
    StringBuilder output = new StringBuilder();
    Stack<string> stack = new Stack<string>();
    string emitted = string.Empty;
    int num = 0;
    foreach (Match item in Tokens.Matches(source ?? string.Empty))
    {
        string value = item.Value;
        Match match = ColorOpen.Match(value);
        if (match.Success) { stack.Push(match.Groups[1].Value); continue; }   // <color=x> 入栈
        if (ColorClose.IsMatch(value)) { if (stack.Count > 0) stack.Pop(); continue; }
        if (value.StartsWith("<") && value != "<")                              // 其它 tag 原样透传
        { Switch(string.Empty); output.Append(value); if (value.Equals("<br>", StringComparison.OrdinalIgnoreCase)) num++; continue; }
        foreach (char value2 in value)                                          // 逐字符决定当前颜色
        {
            string next = (num >= start && num < end) ? "#" + hex
                        : (stack.Count > 0 ? stack.Peek() : string.Empty);
            Switch(next); output.Append(value2); num++;
        }
    }
    Switch(string.Empty);
    return output.ToString();
}
```

配套：`ItemTextSize.ApplySelection` 用同样的"栈 + 逐字符 + Switch"结构改字号（`ItemTextSize.cs:47-113`），`WithPlainText` 用"公共前缀/后缀 diff"把纯文本编辑结果贴回富文本（`VisibleColorText.cs:103-172`）——**"用户只编辑纯文字，颜色由代码维护"** 是这套方案的核心取舍。

### 范式 D · 带快照回滚的物品深拷贝

```csharp
// ItemDuplicator.cs:69-151（删减注释，保留结构）
private static GameItem? CloneRestored(GameItem source, out string message)
{
    Dictionary<string, TagState> dictionary  = SnapshotTags(TryGet(() => source.state));
    Dictionary<string, TagState> dictionary2 = SnapshotTags(TryGet(() => source.modifiedState));
    List<FeatureDraft> list = SnapshotFeatures(source);
    GameItem copy = DirectoryMaster.Item(source.identifier, true);
    if (copy == null) copy = ItemSpawner.Spawn(source.identifier);
    int num2 = copy.uniqueId;
    if (num2 <= 0 || num2 == source.uniqueId) copy.uniqueId = PlayerStore.Instance.GetUniqueID();
    CopyScalars(source, copy);            // 文本/价格/开关/贴图/shape/itemTypes
    RestoreFeatureParents(copy);
    RebuildFeatures(list, copy);          // 用草稿重放条件，不用模板
    TagSystem val = BuildTags(dictionary);
    if (val != null) copy.state = val;    // 整体替换 tag 系统
    TagSystem val2 = BuildTags(dictionary2);
    if (val2 != null) copy.modifiedState = val2;
    ItemInspectorUtil.ValidateItem(copy);
    RestoreStorageTabs(copy);
    RestoreSourceIfChanged(source, dictionary, dictionary2, list, sourceName, sourceCount);
    return copy;                          // 原件被自己改动过 → 自动反向修复
}
```

再加一层"转换试错"保护（`FunctionalItemSnapshot`）：`NumericAdditionSnapshot` 存 `state/modifiedState/features/types/price/basePrice` 快照（`NumericAdditionSnapshot.cs:23-65`），`FunctionalItemSnapshot` 额外存 18 个展示字段 + 15 个声音/标识字段 + **`GameItemFunc` 上全部委托**（`23-61`），失败即整体 `Restore()`。

**顺带一个重要认知**：这个模组**不是零 Patch 的**。它在 6 个模块里做了 20 多处 Harmony patch 点（多个模块会重复 patch 同一批方法）（`GameItem.name` getter、`shortDescription/longDescription/flavorText/customText` getter、`TagState.valueString` getter 与 `GetString`、`TagState.Set`、`GameItem.DisplayManufacturer`、`GameItemElement.GetTooltipBasic / ValidateSelectLocked / ResolveItemSprite / ApplyAnimationFrame`、`SaveManager.EncodeNode / DecodeNodes`、`PlayerStore.EncodeItem`、`SecuritySlip.Init`、`HandnoteUIManager.OpenPanel`、`GeneralHelper.GetCustomName`、`RichTextBuilder.Build/Add`、`LabelerHelper.HandleLine`、`HydroponicHelper.GetWaterUsePerCycle/GetMineralUseRate/GetStartingHealthPercent`、`ItemDirectory.CreateEmptyItem`）。而且**凡是重载方法都显式写了 `new Type[]{...}`**（`SaveManager.EncodeNode(GameItem)`、`PlayerStore.EncodeItem(GameItem)`、`TagState.Set(TagState)`、`ItemDirectory.CreateEmptyItem(string)`、`RichTextBuilder.Build(int)`、`RichTextBuilder.Add` 10 参数）——这条正好对上我们的编译铁律。它的 `<Capabilities>` 我没读 manifest（**（推测）**：重度改名/改文本 + 贴图读取大概率带 files 能力）。

## 对我们的可用点

### 给《深空当铺：可能是群友》（`freya_cornell` / `DeepSpacePawn`）

1. **物品数据字典**：写自定义物品/收据/许可证时，按 §1 的表确认字段归属，别在 `unitValue` 之外另立价格 tag（原版四个价格字段语义不同：`unitBaseValue` 基础价 / `lateUnitValue`、`backupUnitValue` 各自的结算时机我没读原版（推测：迟售与备份兜底价），照抄字段名即可，别自己发明第 5 个价格 tag）。
2. **`IS_OWNED_TAG` + `GeneralHelper.IsItemEffectivelyOwned` 双判**（`ItemEditPolicy.cs:20`）比只判 `owned` 稳，可作为我们"这件货能不能被玩家处理"的准入模板。
3. **真伪范式可直接抄**：`ItemFeatureList.InjectorFeature(shown, actual)` 的"标示 vs 实际"用法（`ItemInspectorPresets.cs:192-202`）就是我们做赝品/成色标示的标准结构；判定只读 `realCondition`，显示才看 `fakeCondition/publicDisplay`。
4. **富文本工具集**（`VisibleColorText` / `ItemTextSize` / `FeatureColorScope.Close`）几乎可以整包搬去当铺单据、报纸、回执的文本编辑器 —— 当铺单据本来就是 `sec_slip` 那类"标签存正文"的结构。
5. **`NumericOverrides` 范式**（`NumericOverrides.cs:25-125`）：当某个原版数值是**硬编码在 helper 里、改 tag 无效**时（我们的估价/热度/汇率就属于这种），做法是"私有标记 tag 记录被接管过的键 + Harmony Postfix 回读 `modifiedState`"，并在 `Disable()` 成对 `UnpatchAll`。
6. **`CloneLinked` / `EncodeNode` 处不要乱塞富文本**：它 patch 这些点是为了"落盘前剥色、上屏时才上色"（`ItemNameColors.cs:64-83`）。我们如果给物品名加颜色，必须同样在 `SaveManager.EncodeNode` 路径上做纯文本化，否则存档里会存进 `<color>` 标签。

### 给《深空电玩》（`deepspace_arcade`）

1. **贴图白名单 + 渲染 Prefix**（`ItemAppearance.cs:31-52` + `ItemSpriteOverride.cs:107-121`）：给机台/贴纸/卡片换图时，别再写 `spriteAtlasPath` 硬拼，走"私有 tag 存 `atlas\nname` + `ResolveItemSprite` Prefix + `ApplyAnimationFrame` 返回 false 屏蔽动画覆盖"这一套；顺便避开贴图自愈模组（3845751834）修过的 `useSpriteFromMod` 粘滞坑。
2. **占格重算的正确姿势**（`ItemAppearance.cs:284-362`）：改 `shape` 必须配 `ComputeModifiedShapeState` 重算 `modifiedShape` + 原点，并用 `CheckInventorySlotStillValid` 预检、失败整体回滚。
3. **数值配套表**（§3）当"机台参数"设计参考：任何"电力/热量/使用次数"类数值，落地时都要同时给主标记 tag + 上限/当前成对 + 开关 bool，否则原版系统不认。我们做游戏机投币、耗电、过热时同理。
4. **快照+回滚**（范式 D）用于玩家可自定义的街机配置：改坏了能一键还原，且不会污染原件。
5. **IMGUI 大面板的工程化**（§6）：`NextRow` 光标流 + 分区页码 + `_hitRects` + `_debugStep` 节流日志，是 6k 行单文件还能维护的原因；我们做"机台调试面板"可以直接套这个骨架，不必上 UGUI。
6. **§2 的黑名单写法**建议直接抄成我们的"受保护字段表"：一个 `switch` 返回中文原因，所有写入口都先调它 —— 比散落 `if` 好审。

### 共同注意的坑（源码里全是血）

- 标签和 `itemTypes` 是**同名耦合**的：删 `FOOD` 标签时它顺手 `RemoveGameItemType("FOOD")`（`ItemInspectorPanel.cs:2940`、`2981-3003`）。我们做品类时只用 `EnableTag`，别反向删原版类型。
- `RemoveAllGameItemType()` 后按字符串重建会**丢顺序/丢大小写**（`ItemDraft.cs:134-138`）——只有在用户明确清空时才这么做。
- 改完 `state` 里任何影响占位/尺寸的 tag，必须 `ValidateShapeState()`；否则物品在图里位置会错（作者在多处重复这一条：`ItemDraft.cs:139`、`NumericRules.cs:192`、`NumericSupport.cs:427`、`BiologicalTagPresets.cs:90`）。
- 递归容器复制必须有深度上限（这里是 4 层，`ItemDuplicator.cs:10, 249-253`），否则嵌套容器能卡死主线程。
- 面板类 UI 的生命周期要成对：`OnGameLoadedInit` 建 host、`Disable` 里 `Shutdown()` + 恢复 `InputActionManager`（`ItemInspectorModule.cs:46-81`）—— 少了这一步热重载后会永久吞掉游戏输入。
