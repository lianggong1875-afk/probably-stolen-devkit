# 物品浏览器与物品编辑模型（Item Spawner）｜工坊 3829012791

> 蒸馏自 `缓存文件夹\decomp_ws\3829012791`（约 4 万订阅，"嫌原版调试面板不好用，让 AI 撸了一个"）。
> 价值：**一份现成的"物品目录怎么枚举、物品字段怎么增量编辑"的实现**，以及"库 + 应用"两层的分发架构。

## 1. 结构：Lib 与 App 两层

```
ItemSpawnerLib\            ← 可被别的模组引用的库（DLL）
  ItemIndex.cs (511 行)    ← 物品索引：枚举 / 筛选 / 搜索
ItemSpawnerLib.Extension\
_side_ItemSpawner\         ← 应用层（UI + 编辑逻辑）
  ItemSpawnerMod.Editing\
    ItemEditState.cs (547) ← 待写字段的增量状态
    FeatureCatalog.cs (932)← 特性目录（可挂/已挂/暴露）
    ModuleEditor.cs (793)  ← 机器模组编辑
    .Detail\               ← 细分面板（鉴定/液体/种子基因/动物基因）
```

**架构含义**：把"读物品目录"这种通用能力放进 `*Lib`，单独出一个 DLL；UI 与业务放 `_side_*`。别的模组要枚举物品时引用 Lib 即可，不必复制代码——这是工坊里少见的"可复用库"做法。我们要给第三方提供能力时也建议照这个切分。

## 2. 物品索引：原版目录 API 只有两条就够

```csharp
// 一级：所有"品类类名"（等价于物品分类）
List<string> classes = DirectoryMaster.GetDictionaryClassNameList<GameItem>();

// 二级：某品类下的全部 identifier
List<string> ids = DirectoryMaster.GetIdentifierList<GameItem>(className);
```

三级筛选的第三级不是 API，而是**物品自身的字段**：

| 维度 | 数据来源 |
| --- | --- |
| 品类 | `GameItem.itemTypes`（`List<string>`，即 SetGameItemType/EnableTag 写入的品类集合） |
| 标签 | `item.IsTag("<TAG>")` |
| 来源模组 | 遍历 `ModLoader.Mods` 的 `LoadedMod.Manifest.ID`（与 `LoadedMod.Sprites` 是否含该物品贴图）反查 |
| 显示名 | `item.GetDisplayName()` / `GeneralHelper.GetDisplayNameFromID(id)` |
| 原型 ID / 本地化 ID | identifier 本身 + `LocHelper` 查表 |

> 这条 API 链对我们做任何"物品大全/仓库索引/调试浏览器"都是最短路径，别再自己爬场景树。

## 3. ItemEditState：增量编辑（只记"改过的字段"）

它不复制整个 GameItem，而是给每个待编辑字段配一个 **`HasXxx` 布尔 + 值**：

```csharp
internal sealed class ItemEditState : IDisposable
{
    public string Key { get; private set; }
    public GameItem Item { get; private set; }
    public bool External { get; private set; }        // 外部物品（非本模组生成）标记
    public bool IsAlive => Item != null;
    public Sprite Icon => icon.Icon;

    public IReadOnlyDictionary<string, TagSnapshot?> Tags { get; }   // 标签快照（含 null = 待移除）
    public IReadOnlyCollection<string> EditedFieldSets { get; }      // 改过哪些"字段组"
    public IReadOnlyCollection<string> EditedFeatures { get; }       // 改过哪些特性

    public bool HasShape { get; }  public string ShapeAtlas { get; }  public string ShapeSprite { get; }
    public bool HasValue { get; }   public long Value { get; }
    public bool HasName  { get; }   public string Name { get; }
    public bool HasShortDescription { get; } public string ShortDescription { get; }
    public bool HasFlavorText { get; } public string FlavorText { get; }

    public int  LiquidPresetIndex { get; }  public bool EmptyLiquid { get; }
    public string FillOption { get; }       public bool ClearFill { get; }
    public IReadOnlyCollection<string> ExposureRequests { get; }
}
```

要点：
1. **`HasXxx` 是"用户是否动过这个字段"**，而不是"值是否为默认"。所以"用户把它设成空串"与"用户没动"可以区分——这是编辑器类 UI 的必备设计。
2. `Tags` 是 `Dictionary<string, TagSnapshot?>`，**值为 null 表示"要移除这个标签"**，用类型系统表达三态（保持/设置/移除）。
3. `ExposureRequests` 收集"要暴露的特性"，最后统一提交，避免编辑过程中反复改物品。
4. `IDisposable`：编辑会话结束要释放对物品的引用（防泄漏 / 防幽灵物品）。

## 4. FeatureCatalog：特性的"目录 + 三态条件"

```csharp
public static List<FeatureCategory> ApplicableTo(GameItem item);   // 这件物品能挂哪些
public static List<FeatureCategory> CarriedBy(GameItem item);      // 已挂哪些
public static List<FeatureCategory> AddableTo(GameItem item);      // 还能加哪些
public static bool Set(GameItem item, FeatureCategory category, FeatureCondition fake, FeatureCondition real);
public static bool Remove(GameItem item, string categoryId);
public static bool Expose(GameItem item, string categoryId);       // 暴露（鉴定出来）
public static bool SetExposed(GameItem item, string categoryId, bool exposed);
public static bool IsExposed(GameItem item, string categoryId);
public static TagSpec RealTagSpec(string tag);                     // 原版标签 → 规格
```

**`Set(..., fake, real)` 同时传假与真两套条件**，是本模组对我们最有启发的一处：原版真伪系统的正确用法就是"一个特性带两套条件（玩家看到的 / 实际生效的）"，只改 ever 一套必然被识破。

配套还有业务钩子：`EnsureStampFeature` / `EnsureInjectorFeature` / `SyncInjectorFeature` / `IsRecalledSerial` —— 说明某些特性（印章、注射器、召回序列号）在原版里有**配套的隐藏机制**，不是加个特性就完事。

## 5. 预设卡：声明式（标签 + 控件 + i18n key）

```csharp
AddStatInput(b, state, "preset_module_eff_label", "detail_efficiency",
             "preset_module_eff", "BONUS_PERCENTAGE_EFFICIENCY_INT");
AddFeatureRow(b, I18n.T("feature_current"), "preset_feature_current", list, index, canSwitch);
AddFeatureRow(b, I18n.T("feature_public"),  "preset_feature_public",  list, index, ...);
AddFeatureRow(b, I18n.T("feature_real"),    "preset_feature_real",    list, index, ...);
```

即：**每张预设卡 = (i18n 标签 key, 控件类型, 数值标签名)** 三元组，加进列表就自动生成 UI。想加一类新数值，只加一行声明，不写 UI 代码。

里面出现的原版数值标签（可直接用）：
`BONUS_PERCENTAGE_EFFICIENCY_INT`（模组效率加成）、以及 `preset_durability` / `preset_power` / `preset_wine` / `preset_stamp` / `preset_stolen_heat` 对应的耐久 / 电量 / 酒 / 印章 / 赃物热度数值标签。

## 6. 可复用模式

1. **增量编辑状态对象**（`HasXxx` + 值 + 三态标签 + 最后统一提交）。
2. **声明式预设/控件表**（i18n key + 控件类型 + 标签名），加功能只加声明。
3. **Lib/App 分层分发**（通用能力进 `*Lib` 单独 DLL）。
4. **目录三级索引**（`GetDictionaryClassNameList` → `GetIdentifierList` → `item.itemTypes`/`IsTag`）。
5. **`External` 标记**：区分"本模组生成的物品"与"玩家原有物品"，编辑后者时要更谨慎（这个思路可移植到我们自己任何"改玩家物品"的功能）。

## 7. 待验证 / 风险

- 它只 Patch 了 1 个方法（`StoreUIManager.CheckRaycast`），其余靠 UI 与公开 API——说明**做个功能完整的物品编辑器几乎不需要 Harmony**。
- `FeatureCatalog.Set(fake, real)` 的语义我是从签名与类名推断的（`FeatureCondition fake, FeatureCondition real`），未逐行验证其内部是否写 `ItemFeature.fakeCondition/realCondition`。用前必须在 `共享开发\reference\Assembly-CSharp\ItemFeature.cs` 核对。
- 编辑 `External` 物品时若把 `uniqueId` 之类改掉，可能破坏存档引用——本档案未确认它是否有防护。
