# UI 深度扩展八例（原版 UI 能被改到什么程度、每种改法的代价）

> 蒸馏自 `缓存文件夹\decomp_ws\` 下 8 个 UI 类工坊模组（共 386 个 .cs）：
> `3828868413` Item Details、`3828938065` Client Demand Highlight、`3845160523` After-Hours Item Highlights、
> `3842791640` 容器 UI 滚动与缩放、`3841350358` Better Sliders、`3842891745` Window Snapping、
> `3838828332` Toolbox Wheel、`3829012791` Item Spawner。
>
> **证据规则**：方法名/字段名/行号均来自上述反编译源码或本工作区原版参考
> `共享开发\reference\Assembly-CSharp\`。不能从代码直接读出的判断标「（推测）」。
> `(ColorPalette)12615037`、`((Object)(object)x)` 是 IL 还原产物，原样保留以便对号入座。

## 0. 速查：8 个模组的"改法指纹"

| 工坊号 | 名义 | 规模 | 主手法 | 补丁目标 | 一句话代价 |
| --- | --- | --- | --- | --- | --- |
| 3828868413 | Item Details | 144 cs / 840 KB | **官方钩子优先 + 26 个独立小 Patch + 1 处 Transpiler + 自绘覆盖层** | 60+ 方法 | 每块自降级，但覆盖面极大 |
| 3828938065 | Client Demand Highlight | 18 cs / 109 KB | **4 个 Postfix + 自绘 `MaskableGraphic` 边框 + 0.25s 轮询** | 4 方法 | 每拍全店扫描，须自设上限 |
| 3845160523 | After-Hours Highlights | 5 cs / 23 KB | **零 Harmony**，`LateUpdate` + 自绘 `Graphic` | 0 | 只见已渲染且 active 的物品 |
| 3842791640 | 容器 UI 滚动与缩放 | 47 cs / 183 KB | **25 个 `[HarmonyPatch]` + 接管 `widthPixels` 属性 + 自建滚动条/游标** | 25 方法 | 横跨 8 子系统，最重最脆 |
| 3841350358 | Better Sliders | 15 cs / 48 KB | **Postfix 挂载 + 自建 `TMP_InputField` 子节点 + Prefix 吞 Esc** | 8 方法 | 与所有改配置窗口的模组抢方法 |
| 3842891745 | Window Snapping | 3 cs / 9 KB | **单点 Prefix/Postfix 比首尾位移** | 1 方法 | 全窗口拖拽被接管，10px 写死 |
| 3838828332 | Toolbox Wheel | 18 cs / 118 KB | **自建 Canvas + 程序化 `Texture2D` 圆环 + 全局输入屏蔽** | 5 方法 | 输入屏蔽是全局副作用 |
| 3829012791 | Item Spawner | 136 cs / 563 KB | **完全自绘窗口 + 仅 1 个 Postfix** | 1 方法 | 零侵入，UI 全自己造 |

**总判断**：能改多深，取决于原版把逻辑摊在多少个"小而可替换的缝"上——`After-Hours` 0 补丁就做出高亮，
`容器缩放` 25 补丁才做出拖动改尺寸；差别不在能力，而在**改的是"渲染结果"还是"别人的算法"**。

## 1. 逐模组一句话结论

**1.1 `3828868413` Item Details —— 钩子优先，补丁垫底。** 入口不打 Harmony，先挂官方事件
（`ModHook.cs:64` 的 `Action<RichTextBuilder, GameItem> OnCreateTooltipLate`）：

```csharp
// ItemDetails\ModEntry.cs:99-107（节选，略去 LiquidQueries.Enable 一行）
Action<RichTextBuilder, GameItem> cb = OnTooltipLate;
_cb = cb;
Try("TooltipSections", TooltipSections.Enable);
Try("Subscribe", () =>
{
	ModHook.OnCreateTooltipLate += cb;
});
DeferredPatchRunner.Schedule("ItemDetails.Patches", ApplyPatches);
```

补丁全部装进独立 `PatchScope`（`PatchScope.cs:8`），装失败只丢一块。目标覆盖
`GameItemElement.GetTooltipBasic/GetTooltip/ValidateSelect`、`RichTextBuilder.AddLine/Add/AddEmptyLine/Build`、
`OverlayHandler.ShowTooltip(3 参/4 参重载)/HideTooltip`、11 个 `*Helper.Create*Tooltip`、
`ItemMouseDoubleClickHandler.DoubleClickAction`、`InspectionUIManager.*`、`ItemContextHandler.*`、
`ModConfigWindow.Open/ResetWidgets`、`ModConfig.Save`、`ExchangeBarter.Accept/CanAccept/GetRequiredItems`。
**代价**：点最多，但每处都有"找不到方法→Warn→停用该模块"的分支（`TooltipPipeline.cs:90-93`、
`ContentsGrid.cs:126-130`、`TooltipWidthFix.cs:19-28`）。

**1.2 `3828938065` Client Demand Highlight —— 4 个 Postfix 指向同一方法体。**
`GameItemElement.Validate/UpdateRectTransform/RotCW/RotCCW` 的后缀全指向 `ItemOutlinePatch.Postfix`
（`Core.cs:117-120`），后者只有一行 `BorderPainter.Evaluate(__instance)`（`ItemOutlinePatch.cs:5-8`）。
`Validate` 是物品格刷新必经点，等于钩住高频热点，所以内部层层限流：`Registry`/`Verdicts` 各 4096 上限、
TTL 16 拍、同帧去重（`BorderPainter.cs:509-545`）。

**1.3 `3845160523` After-Hours Highlights —— 一行 Harmony 都没写。** `OnEnable` 只
`AddComponent<MaterialHighlightDriver>()`（`ModEntry.cs:25`），驱动在 `LateUpdate` 每 0.12s 扫已渲染物品，
给 `item.rotator` / `item.fakeRotator` 挂自绘 `MaterialFrame`。**代价**：只作用于已渲染元素、靠轮询；
换来全批最低兼容风险。

**1.4 `3842791640` 容器 UI 滚动与缩放 —— 不改窗口 rect，改"内容上报尺寸"。** 25 个 `[HarmonyPatch]`，
核心是 Postfix `GameGridInventory.get_widthPixels/get_heightPixels`（§4）。横跨
`PixelWindow`/`GameGridInventory`/`MachineryTabs`/`MachineryPanel(Row/Intake)`/`TreeNodeRender`/
`InputActionManager`/`InputActionHandler`/`PlayerStore`/`GameMaster`，还须处理重入
（`requestedSizeHost` + `layoutRevision`）与存档生命周期（`LoadGame/SaveGame/StartNewGame/ResetData/QuitToMenu`）。

**1.5 `3841350358` Better Sliders —— Postfix 旁挂，不动原控件。**
`GameOptionUIManager.BuildEntry` 与 `ModConfigWindow.Open` 各一个 `[HarmonyPriority(0)]` Postfix，
在滑条旁找/插数值 TMP 标签并挂 `InlineSliderNumberEditor`；点击时才生成真 `TMP_InputField`。

**1.6 `3842891745` Window Snapping —— 单点吸附。** `TreeNodeRender.OnDrag` 的 Prefix 记
`anchoredPosition`，Postfix 与所有可见窗口四边两两比较，最小偏差 ≤10px 就补位移
（`WindowSnapPatch.cs:9,93`）。

**1.7 `3838828332` Toolbox Wheel —— 自建 Canvas + 程序化圆环。** `WheelView.EnsureBuilt()` 借现有
Overlay Canvas（优先 `"CustomUIOverlayCanvas"`）当父节点；环面是 `Texture2D` 逐像素画的 Sprite
（`WheelArt.BuildRing`），高亮=换一张重画的环；命中纯数学（半径带 + `atan2` 扇区）；
打开期间用 `InputGuard.Blocking` 关掉原版输入（§5.4）。

**1.8 `3829012791` Item Spawner —— 完全自绘。** 自造窗口（`CustomUIBuilder` + `WindowShell`/`UiKit`/
`ItemGrid`/`PresetCard`）与物品索引（`ItemSpawnerLib.ItemIndex`），只用 1 个 Postfix
`StoreUIManager.CheckRaycast`（把"鼠标在我窗口上"报给原版，`StoreHoverPatch.cs:10-16`）。
**代价**：136 个 .cs 换来近零兼容风险。

## 2. 提示框深挖：Item Details 怎么塞进 26 组内容

### 2.1 三层结构：钩子 → 分节 Writer → 26 个 Renderer

它不"插一行"，而是把每类信息做成**具名 Renderer**（`ModEntry.cs:34` 起，`new RendererDef[26]`），
由 `TooltipSections.Writer` 负责"直写"或"先缓冲、由第三方决定是否接管"。三个设计：

1. **`Section` 位掩码**：`ItemDetailsTooltipSection` 是 `[Flags]`（`ItemDetailsTooltipSection.cs:6-28`，
   18 个节 + `All`）。第三方声明"这些节我自己画"，主循环直接跳过（`ModEntry.cs:299`）。
2. **`Group` 字符串**：`"appraisal"/"food"/"condition"/"price"/"liquid"/"machine"/"module"/"contents"/
   "lottery"/"plant"/"brew"`，供第三方按信息大类整块接管。
3. **缓冲 vs 直写**：`Writer` 构造时向 `AppDomain` 查监听者，有监听就进 `Buffer`，监听者返回 `true`
   表示"我接管了，你别写"（`TooltipSections.cs:36-46`、`Finish()` 216-243）。

**失败预算**：每块最多连错 3 次就自我停用（`RendererFailureLimit = 3`，`ModEntry.cs:64`），
这是"26 个模块共存"的关键——一块坏了不拖累其它（代码见 §7 范式 1）。

### 2.2 隐藏属性到底读哪些字段（全部核实）

| 想显示 | 读法 | 出处 |
| --- | --- | --- |
| 真实/假耐久、上限 | `DurabilityHelper.GetRealDurability / GetFakeDurability / GetMaxDurability(item)` | `HiddenInfo.cs:33-35` |
| 是否已被玩家发现 | `item.FindItemFeatureByID(id).IsFeatureDiscovered()` | `HiddenInfo.cs:148-159` |
| 锭纯度 | `GameRead.TagString(item,"INGOT_PURITY_TAG")` + `INGOT_PURITY_FAKE_TAG`；等级查 `IngotPurityHelper.ingotPurities[id].PurityLevel` | `HiddenInfo.cs:55-102` |
| 化学品纯度 | `GameRead.TagDouble(item,"CHEMICAL_PRODUCT_PURITY_TAG")` | `HiddenInfo.cs:123-146` |
| 伪装属性（任意 `ItemFeature`） | `f.useCondition`、`f.realCondition`、`f.GetBaseFakeDisplay()`、`f.GetBaseRealDisplay()`、`f.GetActualValueModifier()`、`f.GetFakeValueModifier()` | `FeatureReveal.cs:39-79` |
| 真假差异显示值 | `ConditionEffects.DisplayAmount(f.realCondition)` | `FeatureReveal.cs:110-123` |
| 遍历全部特征 | `item.itemFeatures`（>128 条直接放弃，防爆） | `FeatureScan.cs:19-27` |
| 真伪结论 | `realCondition.identifier` 前缀 `counterfeit/unusable/expired/genuine` + 缺陷计数 | `AuthenticityRules.cs:10-34` |

真伪链（`Authenticity.cs:204-230`）：按品类找 `Profile`（`INS_INJECTOR_TAG`/`CIGARETTE_TAG`/`FOOD_STAMP_TAG`
对应 `CATEGORY_GENUINE_*` 特征类别）→ 数 `ISSUE_*` / `*_ISSUE` 标签 → `AuthenticityRules.Decide(...)`。
**只认 `realCondition`，不读显示名、不读 `fakeCondition`**，与本工作区铁律一致。

### 2.3 它怎么避免与别的 tooltip 模组打架（按可靠度排序）

1. **追加而非替换**：主路径只往 `RichTextBuilder` 末尾写。只有明确改写原版某行（价格/卡路里/体积）
   才用 `AddLine` Prefix **return false 吞行重写**，且每处先做字符串前缀匹配，匹配不上原样放行
   （`TooltipPipeline.cs:349-377`）。
2. **字符串插槽协作协议（带版本号 `v1`）**：`ItemDetails.Integration.ItemDetailsClient` 公开
   `item_details.tooltip_sections.v1.display / .read / .watch`、`…layout.v1`、`…anchor.v1`、`…badge.v1`；
   别的模组**不引用 DLL**，只按签名往 `AppDomain.SetData` 注册（`ItemDetailsClient.cs:34-56`）。
   与本工作区"约定 GameObject 名 + 带版本字典"同构。
3. **主动让位**：`ThirdPartyProviders.TooltipSuppressions(item)` 汇总声明；第三方有 appraisal provider
   就不写自己的拆解（`ModEntry.cs:459-462`）。
4. **探测别人的补丁**：`Harmony.GetPatchInfo(method)` 遍历 Prefix/Postfix/Transpiler，非自己的就退让（`ValueBreakdown.cs:479-511`、`TrashExchangePreview.cs:104-105`）。

**没做的（风险点）**：Harmony 实例 id 是裸 `"item_details"`（`PatchScope.cs:10,32`），不是约定的
`"mod." + manifest.ID`；全模组**零 `[HarmonyPriority]`**，顺序全靠"钩子晚于原版 + 字符串匹配"。

### 2.4 顺手记下的三个硬骨头

- **宽度**：加 `<>` 富文本后原版 `recommendedWidth` 偏大 → Postfix `RichTextBuilder.Build`，反射写
  `<recommendedWidth>k__BackingField`（`TooltipWidthFix.cs:24-55`）。
- **原版估值调用贵**：对 `GetTooltipBasic` 写 **IL Transpiler**，把 `GameItem.GetCurrentValue(5 参)`/
  `GetBaseValue()` 的调用点换成带缓存的版本（`TooltipPipeline.cs:198-263`）。全批**唯一一处 Transpiler**，
  它自己检查 `foundSell/foundList`，没找到就 Warn 并放弃缓存——照抄必须保留这个自检。
- **双击检视**：优先 Patch 原生 `ItemMouseDoubleClickHandler.DoubleClickAction`，失败退回自建 `Ticker`
  （0.35s 内同物品两次点击）（`DoubleClickInspect.cs:28-63,123-135`）。"原生优先、轮询兜底"值得抄。

## 3. 着色与高亮：两条完全不同的路线

### 3.1 路线 A：自绘 `MaskableGraphic`（两个高亮模组都选这条）

**都没改 `Image.color`，也没用 `Outline` 组件**（`Outline` 是顶点复制，在裁切/旋转的格子里会露馅）。
`After-Hours` 在 `rotator`/`fakeRotator` 下建子节点，`anchorMin/Max = 0/1`、`offset = 0`、
`raycastTarget = false`（`MaterialFrame.cs:36-45`）——**完全不改宿主任何属性**，"不污染存档"是结构成立的。

### 3.2 "两色各占半边"怎么画（容器内容双色）

**同一个环拆成两条半环各用一色**，不是两张图叠、不是 UV 裁切：`AddSplitFrame` 调两次 `AddHalfFrame`，
一次从 `greenStartCorner` 起，一次从 `(greenStartCorner + 2) & 3` 起（`MaterialFrame.cs:104-118`，
代码见 §7 范式 2）。起始角随宿主**旋转与镜像**换算（`GetGreenStartCorner`，`MaterialFrame.cs:58-63`）：
`(2 + Mathf.RoundToInt(rotation / 90f) + (mirrored && 旋转格数为偶数 ? 2 : 0)) & 3` —— 转 90° 配色不跳。
配合 `SetStyle` 仅在 `(style, startCorner)` 变化时 `SetVerticesDirty()`（`MaterialFrame.cs:48-56`），免每帧重建。

### 3.3 路线 B：按物品形状掩码描边（Client Demand Highlight）

物品形状是 `GridShape`（可能非矩形/可旋转）。它把形状读成 `bool[W*H]` 掩码（`ShapeOutline.cs:8-37`，
512 条缓存），逐格判"哪条边是外轮廓"，`FrameGeometry.Build` 生成带状三角形（`FrameGeometry.cs:21-120`），
最后只做两件事：`border.Configure(mask, W, H, thickness, radius)` + `((Graphic)border).color = style.ColorOf()`
（`BorderPainter.cs:1043-1045`）。**不换 sprite、不换材质、一个 draw call 换色**。
`ItemFrameGraphic` 缓存顶点/alpha/索引，`OnPopulateMesh` 一次灌进 `VertexHelper`（`ItemFrameGraphic.cs:29-58`）。
配色：绿 `(0.3,0.9,0.35,.95)` 普通 / 琥珀 `(1,.7,.1,.95)` 赃物 / 红 `(.95,.22,.2,.95)` 违禁品
（`OutlineStyleExtensions.cs:7-31`）。

### 3.4 判定分层（为什么它不会满屏高亮）

`DemandRules.Classify`（`DemandRules.cs:95-140`）先按客户意图分流：`clientIntent == 4` = 盘查 →
**只走 `ClassifyIllegal`（赃物/违禁品），不标普通需求**；`== 6` = 以物易物 → 查
`barterOffer.conditions[i].IsItemValid(item)`；卖货意图下的客户自带货 → 需 `Config.HighlightClientGoods`
才标（`GeneralHelper.IsItemOwned(item)` 为假即客户货）；收 `ALL_TYPE`/`ALL_TYPE_EXCEPT_CONTRABAND`
的客户直接不标（`DemandRules.cs:164-180`）。再叠锁容器内 / `ITEM_BEHIND_LOCK_TAG` / 不可移动
（`DEBRIS_PILE_TAG`、`mayInventoryRemoveItemFunc`）/ `ItemPinHelper.IsPinned` / 机器内容白名单
（`ContentsIgnoredIds` 19 个 id）/ 能量电池 / 模块与节点 / 芯片；盘查时还排除避检容器
（`ContrabandHelper.IsBlockSearchContainer`）。

### 3.5 "不污染存档"的三种做法

`After-Hours` 纯只读，唯一持久化是快捷键 `PlayerPrefs`（`MaterialHighlightDriver.cs:92-111`）；
`Client Demand Highlight` 判定只进内存 `Verdicts`、`Shutdown()` 全清。反例在 `Item Details`：tooltip 路径
只读，但 `ResultPrefill` 会写 `APPRAISAL_DEFAULT_VALUE`（`ResultPrefill.cs:48-51`）、`WaterAdvice` 会写水量——
照抄"零污染"时这两处必须改掉。

## 4. 窗口几何：改 `RectTransform` 是错的做法

### 4.1 先搞清原版尺寸从哪来（本节最重要的一条）

`PixelWindow.Validate()` 每次都**用子元素上报的尺寸反推窗口尺寸**：

```csharp
// 共享开发\reference\Assembly-CSharp\PixelWindow.cs:366-373（节选，略去 num/num2 两行赋值）
int num3 = num + xPadding;
int num4 = num2 + yPadding;
childElement?.ResizePixels(widthPixels - num3, heightPixels - num4);
childElement?.Validate();
widthPixels = (num3 + childElement?.widthPixels).GetValueOrDefault();
heightPixels = (num4 + childElement?.heightPixels).GetValueOrDefault();
```

所以直接改 `rectTransform.sizeDelta` / `widthPixels` 会被下次 `Validate()` 抹掉——"原版重置布局"的真身
不是"每帧重置"，而是**任何一次内容变化或重新显示都会重置**。`容器缩放` 的正解是**接管内容上报值**，
让原版自己算出想要尺寸（代码见 §7 范式 3）；`value.Drawing == 0` 是防重入闩，`ViewGridPatch.cs:9-29`
用 Prefix/Finalizer 维护计数。

### 4.2 尺寸上下限：屏幕、格数、配置三取最小

`ContainerViews.Limits(grid, manual)`（`ContainerViews.cs:509-521`）三者取最小：屏幕可用像素
（手动态 `Mathf.Abs(val.x) * 0.98f - 32f` / 自动态 `* 0.7f - 32f`，高度 `0.65f - 48f`）、配置格数
（`Settings.Columns/Rows * gridSize`，手动时无上限）、下限 5 格（`gridSize * 5`）。
注意用 `Mathf.Abs(val.x)` 而不是 `val.x`：**宿主 Canvas 缩放/镜像会让局部坐标变负**，取绝对值才稳。

### 4.3 尺寸记忆存哪：物品 Tag，不是 ModData

`PersistSize()`（`ContainerView.cs:1038-1062`）把尺寸写成私有 Tag：键名 `EOTN_RETROFIT_VIEW_V1`（全局）
或 `EOTN_RETROFIT_VIEW_V1_PAGE_<pageSizeKey>`（分页，`ContainerView.cs:998-1005`）；值格式
`"1|<列>|<行>"`，**以格子为单位**（换分辨率/改 `gridSize` 后仍合理），`"1|auto"` 表示该页显式回到自动
（`ViewSize.cs:18-35`）；读取先 `state` 再退 `modifiedState`（`ContainerView.cs:1007-1018`）；
写入时 `state` 与 `modifiedState` 双写。存像素会在换分辨率后错位，存 ModData 会在物品转手/走档时丢关联。

### 4.4 谁负责"能拖多大"

窗口整体贴合屏幕仍调原版 `window.FixBounds()`（`ContainerViews.cs:833`，原版实现 `PixelWindow.cs:534-554`），
**不自己 clamp**；自己只维护 `TrimX/TrimY`（被屏幕挤掉多少），`Fit()` 里按屏幕差量回填
（`ContainerViews.cs:794-843`）；每帧驱动器借 `InputActionManager.Update` 的 Prefix 当时钟（`FramePatch.cs:5-14`），
借用时钟而不去改原版输入逻辑。

### 4.5 Window Snapping：只比"拖动前后的位移"

Prefix 只记 `__state = rectTransform.anchoredPosition`；Postfix 位移 `sqrMagnitude < 0.0001f` 直接返回
（**没拖动就不介入**）；取窗口**外框**（反射 `PixelWindow` 的 `outsideBorder` 字段，取不到退回
`rectTransform`，`WindowSnapPatch.cs:11-25`）；用 `RectTransformUtility.WorldToScreenPoint` 换算屏幕矩形
（跨 Canvas 缩放正确）；与每个可见可移动窗口比较四组候选差（左左/右右/右左/左右），取绝对值最小；
最后除 `canvas.scaleFactor` 写回。 "仅角吸附" = 要求横纵**同时**命中
（`if (!flag || (flag4 && flag5))`，`WindowSnapPatch.cs:165`）。

## 5. 输入与控件

### 5.1 Better Sliders：在 TMP 文本上做内联输入框

**不是把 TMP 文本改成输入框，而是在文本节点下挂一个真 `TMP_InputField` 子节点，点开时把原文本变透明。**
结构（`InlineSliderNumberEditor.cs:94-219`）：`edit_hit_bg`（透明 `Image` + `raycastTarget=true` 接点击）
→ `inline_number_input`（边框 `Image`，`LayoutElement.ignoreLayout = true`）→ `bg`（填充 + raycast）
→ `TextArea`（`RectMask2D`）→ `Text`（`TextMeshProUGUI`）→ 同节点 `TMP_InputField`。关键参数：
`characterLimit = 16`、`onFocusSelectAll = true`、`customCaretColor`、`caretWidth = 2`、`lineType = SingleLine`、
`contentType` 按 `slider.wholeNumbers` 切 `Integer/DecimalNumber`。进入编辑依次做：
`SetActive(true)` → `SetTextWithoutNotify(初值)` → `Select()` → `ActivateInputField()` → 设
`caretPosition` 与 `selectionAnchor/FocusPosition`（全选效果）（`InlineSliderNumberEditor.cs:286-311`）。

### 5.2 焦点与 Esc：为什么要"吞掉一帧"

TMP 输入框按 Esc 结束编辑时，游戏的全局 Esc 处理会同时收到这一下，导致"刚输入完就把整个窗口关了"。
解法是**协调器 + 单帧标记 + 一个 Prefix**：`CustomUIManager.HandleEscape` 的前缀在
`SliderEditCoordinator.ConsumedEscapeThisFrame()` 为真时 `return false`（`CustomUIHandleEscapePatch.cs:8-15`，
协调器本体见 §7 范式 4）；`GameOptionUIManager.ConsumedEscapeThisFrame` 有同款补丁。并且**同一时刻只允许
一个编辑框**：`BeginEdit` 会先 `CommitAndExit()` 上一个（`SliderEditCoordinator.cs:31-38`）。
取值不是直接塞 `slider.value`：`SliderValueAdapter.Create` 先从当前显示文本反推"前缀/数字/后缀"，
再用 min/mid/max 三点拟合判断是否成**仿射关系**（容差 `max(0.55, |Δ|*0.02)`），成立才能把输入反算回滑条值
（`SliderValueAdapter.cs:53-90`）——这是它能对任意 `ConfigEntry` 通用的原因。

### 5.3 Toolbox Wheel：像素圆环怎么画、怎么命中

**画**：`WheelArt.BuildRing(texSize, ringCount, visible, unlocked, highlight)` 建 RGBA32 `Texture2D`，
逐像素按"半径落哪个环 / 角度落哪个扇区"上色，再 `Sprite.Create` 并按 key 缓存（`WheelArt.cs:137-272`；
环结构 `RingCells = { 9, 15, 22, 28 }`，比例 `RingInnerRatio/RingOuterRatio`，外沿 `PlateOuterRatio`）。
高亮不改颜色，而是**重画一张环**（`WheelView.cs:736-767`）。**命中**不用 `EventSystem`，纯数学
（代码见 §7 范式 5）。三个工程细节：① 单位全程用屏幕像素（`r1ScreenPx`/`centerScreenPx`），只在布局尺寸
时除 `canvas.scaleFactor`（`WheelView.cs:406-418` vs `CellGeometry` 的 `pos / curCanvasScale`），命中与
Canvas 缩放解耦；② 自带 `SelfTestPick()` 把每格中心点回投一次 `PickCell` 并写日志（`WheelView.cs:684-734`）；
③ `SetDiag` 面板显示屏幕/画布/环半径/中心/ratio。

### 5.4 输入屏蔽与"装备到手上"

`InputActionManager.GetMouseKeyDown`/`GetMouseKeyUp`、`KeyBindHelper.GetKeyDown`、
`InputActionHandler.UpdatePress` 四处 Prefix，`InputGuard.Blocking` 为真就 `return false` / `__result = false`；
配套两条兜底：非持有态每帧 `InputGuard.Clear()`（`WheelDriver.cs:92-95`）、`OnDisable` 里清
（`WheelDriver.cs:747-751`）。**少了这两条，一次异常退出就让游戏永久失去输入。**
装备则 `ToggleEquip`（`WheelDriver.cs:892-944`）不自己造逻辑：`ItemSelectHandler.current` 判空 →
已装备同物品则 `EndSelect(false)` → 否则 `TryEquipItem(item)` → **再用 `GetEquippedItem()` 读一遍**确认，
失败时把 `MaySelectSlotItem/CanSelectSlotItem` 的结论写日志。不信返回值，只信状态。

## 6. 物品浏览器：三级筛选与搜索的数据结构

### 6.1 目录查询 API（只用原版公开静态方法）

只有两个：`DirectoryMaster.GetDictionaryClassNameList<GameItem>()`（一级=目录类名）与
`DirectoryMaster.GetIdentifierList<GameItem>(className)`（该类下所有 id），两者都用 try/catch 包住、
失败返回 `null`（`ItemIndex.cs:488-510`）；类名列表拿不到时退回 `GetIdentifierList(null)` 全量兜底
（`ItemIndex.cs:148-161`）。类名→标签查 `VanillaGroupKeys`（32 个，`BrowseFilterOptions.cs:9-43`），
没映射就剥 `ItemDirectory`/`Directory` 后缀（`BrowseFilterOptions.cs:92-111`）。
`Rebuild()` 用 `HashSet<string>` 去重（同 id 可出现在多目录）再 `Sort(OrdinalIgnoreCase)`（`ItemIndex.cs:118-162`）。

### 6.2 三级筛选各自存在哪里

- **二级 = 品类**：`itemTypes` 的 52 个原版常量 + `@contraband` 特殊项（`ItemCategories.cs:15-27`），
  匹配是 `types[i] == category` 的纯字符串比较（`ItemIndex.cs:454-469`）。
- **三级 = 来源**：`""` 全部 / `"@vanilla"` / 具体 `modId`（`ItemIndex.cs:421-432`）；`modId` 靠
  `ModSourceHelper.FindModByDelegate(DirectoryMaster.GetFactory<GameItem>(key))` 反查，按 key 缓存（`ItemResolver.cs:222-244`）。
- **搜索键**预算好 `SearchKey = key.Replace('_',' ')`（`ItemPrototype.cs:49`），所以输入 `water pitcher`
  能命中 `water_pitcher`；匹配顺序 key → SearchKey → 已解析显示名（`ItemIndex.cs:471-486`）。两级过滤
  各自只扫一次：`scope`（一级+三级）与 `results`（再叠二级+搜索）复用同一 List，切筛选是 O(n) 纯比较、
  无分配（代码见 §7 范式 6）。

### 6.3 大数据列表：分帧解析 + 分页 + 防抖

- **分帧**：`ResolveNames/ResolveMetas(resolver, max)` 带**光标游标**，每次只处理 `max` 条，下次续跑
  （`ItemIndex.cs:286-348`）；`CountPending` 给 UI 报剩余量，`SearchIncomplete`/`CategoryIncomplete`
  让 UI 显示"搜索中 (n/m)"而不是"没有结果"（`BrowserPanel.cs:282-302`）。
- **只读探测**：`DirectoryMaster.Create<GameItem>(key)` 读完在 `finally` 里一定 `Destroy()`（`ItemResolver.cs:88-155`），
  与本工作区三入口铁律一致；**分页** `PageSize => Mathf.Max(1, columns * rows)`（`ItemGrid.cs:38`），
  单元格复用只切 `SetVisible`。
- **防抖 0.2s**（`BrowserPanel.cs:13,370-381`），且输入框每帧自检"文本变了但没提交"
  （`BrowserPanel.cs:221-232`），两条路都通，防 paste/程序改名漏掉。

### 6.4 预设卡：纯数据驱动

抽象基类 `PresetCard`（`Id`/`Title`/`Order`/`AppliesTo`/`BuildBody(IPresetBody)`/`Sync`/`Reset`，
`PresetCard.cs:1-30`）；注册表按 `Id` 去重、按 `Order` 排序（`PresetRegistry.cs:11-26`）。
`BuildBody(IPresetBody)` 是"往抽象面板填控件"，所以**数据与 UI 解耦**；具体卡 16 个
（`BasicPresetCard`/`BatteryPresetCard`/`WinePresetCard`/`LiquidPresetCard`/`FeaturePresetCard`…）。
**一套预设数据，两个 UI**：`Toolbox Wheel` 第二页 `Presets.Load()` 后渲染成圆环格
（`WheelView.SetPagePresets`，`WheelView.cs:811-906`）。这是最值得抄的结构。

## 7. 六个可复用范式

### 范式 1｜tooltip 追加 = 事件钩子 + 具名分节 + 失败预算

```csharp
// ItemDetails\ModEntry.cs:296-312
for (int i = 0; i < Renderers.Length; i++)
{
	RendererDef rendererDef = Renderers[i];
	if (_rendererFailures[i] >= 3 || (itemDetailsTooltipSection & rendererDef.Section) != 0)
	{
		continue;
	}
	long num2 = (debug ? Stopwatch.GetTimestamp() : 0);
	try
	{
		TooltipSections.Writer arg = new TooltipSections.Writer(builder, item, rendererDef.Name, rendererDef.Group);
		if (rendererDef.Append(this, arg, item))
		{
			arg.Finish();
		}
		_rendererFailures[i] = 0;
	}
```

配套 `Try(名字, Action)` / `Patch(名字, Action<PatchScope>)` 两个包装各带 try/catch 打日志
（`ModEntry.cs:218-253`）；26 个模块里任何一个抛异常只死自己。

### 范式 2｜物品着色 = 宿主 RectTransform 上挂自绘 Graphic（不碰宿主）

```csharp
// NightMaterialHighlight\MaterialFrame.cs:125-144
private static void AddHalfFrame(VertexHelper mesh, Rect rect, int firstCorner, float radius, float thickness, Color32 tint)
{
	int currentVertCount = mesh.currentVertCount;
	AddMidpoint(mesh, rect, firstCorner, thickness, tint);
	AddCorner(mesh, rect, firstCorner, radius, thickness, tint);
	AddCorner(mesh, rect, (firstCorner + 1) & 3, radius, thickness, tint);
	AddMidpoint(mesh, rect, (firstCorner + 2) & 3, thickness, tint);
	for (int i = currentVertCount; i < mesh.currentVertCount - 2; i += 2)
	{
		ConnectPairs(mesh, i, i + 2);
	}
}
```

`AddSplitFrame` 用两次 `AddHalfFrame`（`greenStartCorner` 与 `(greenStartCorner+2)&3`）实现两色各半
（`MaterialFrame.cs:104-118`）；`GetRadius = Mathf.Min(2.25f, Mathf.Min(rect.width, rect.height) / 4f)`。

### 范式 3｜窗口几何 = 接管"内容上报尺寸"，别改 rect

```csharp
// ContainerWindowResize\ViewWidthPatch.cs（全部 15 行）
using HarmonyLib;

namespace ContainerWindowResize;

[HarmonyPatch(typeof(GameGridInventory), "get_widthPixels")]
internal static class ViewWidthPatch
{
	private static void Postfix(GameGridInventory __instance, ref int __result)
	{
		if (ContainerViews.Views.TryGetValue(__instance, out var value) && value.Drawing == 0)
		{
			__result = value.DisplayWidth;
		}
	}
}
```

`Drawing` 计数由 `ViewGridPatch.cs:9-29` 的 Prefix/Finalizer 维护，保证"自己绘制时读回自己的值"不发生。

### 范式 4｜内联数字输入 = 真 `TMP_InputField` + 只吞一帧 Esc

```csharp
// SliderNumericInput\SliderEditCoordinator.cs:53-70
internal static bool ConsumedEscapeThisFrame()
{
	if (escapeConsumedFrame == Time.frameCount)
	{
		return true;
	}
	if ((Object)(object)activeEditor != (Object)null && activeEditor.IsEditing)
	{
		if (Input.GetKeyDown((KeyCode)27))
		{
			activeEditor.CancelEdit();
			escapeConsumedFrame = Time.frameCount;
			return true;
		}
		return true;
	}
	return false;
}
```

退出编辑时还必须成对做 `EventSystem.current.SetSelectedGameObject(null)` 与 `inputRoot.SetActive(false)`
（`InlineSliderNumberEditor.cs:395-403`），否则焦点卡住会吃掉原版快捷键。

### 范式 5｜圆环菜单 = 屏幕像素几何 + 纯数学命中

```csharp
// ToolboxWheel\WheelView.cs:529-549（节选，省略 wantDiag 诊断分支）
for (int i = 0; i < shownRings; i++)
{
	float num3 = WheelArt.RingInnerRatio(i);
	float num4 = WheelArt.RingOuterRatio(i);
	if (num < num3 || num > num4)
	{
		continue;
	}
	int num5 = WheelArt.CellsInRing(i);
	float num6 = 360f / (float)num5;
	int num7 = Mathf.RoundToInt(Mathf.Atan2(val.x, val.y) * 57.29578f / num6);
	num7 = (num7 % num5 + num5) % num5;
	if (num7 >= WheelArt.VisibleInRing(i, shownVisible))
	{
		return -1;
	}
	return WheelArt.RingOffset(i) + num7;
}
```

`Atan2(x, y)` 而不是 `(y, x)`：这个环的 0 号扇区在**正上方**、角度顺时针排——照抄时别改成直觉写法。

### 范式 6｜大数据列表 = 游标式分帧解析 + 两级过滤

```csharp
// ItemSpawnerLib\ItemIndex.cs:434-452
private void ApplyFilter()
{
	results.Clear();
	if (!HasFilter && !HasCategory)
	{
		results.AddRange(scope);
		return;
	}
	string text = filter;
	string spaced = ((text.IndexOf('_') >= 0) ? text.Replace('_', ' ') : text);
	for (int i = 0; i < scope.Count; i++)
	{
		ItemPrototype item = scope[i];
		if ((!HasCategory || InCategory(item)) && (!HasFilter || Matches(item, text, spaced)))
		{
			results.Add(item);
		}
	}
}
```

## 8. 谁最危险：同方法叠加矩阵

### 8.1 这 8 个模组之间的真实重叠

| 原版方法 | 谁在改 | 冲突形态 |
| --- | --- | --- |
| `ModConfigWindow.Open` | Item Details（`ConfigSectionRefresh` Prefix+Postfix）、Better Sliders（Postfix，priority 0）、容器缩放（`ConfigLanguagePatch` Postfix） | **三方 Postfix 串行**；Better Sliders 补插的标签可能被 Item Details 的 `ResetWidgets` 重建冲掉 |
| `ModConfigWindow.ResetWidgets` | Item Details（Postfix）、Better Sliders（`ModConfigResetWidgetsPatch`） | 重建控件时谁后跑谁生效，已挂的 `InlineSliderNumberEditor` 可能被销毁 |
| `ModConfig.Save` | Item Details（`SavePrefix`）、Item Details（`TooltipTour.AfterSave`） | 同一模组内两处，顺序由 Harmony 决定（不保证） |
| `GameOptionUIManager.BuildEntry` | Better Sliders（priority 0）、Toolbox Wheel（`SettingsFeedbackPatch`） | 都在 `builder.Last` 之后插东西，后跑者拿到的 `builder.Last` 已变 |
| `InputActionManager.Update` | 容器缩放（`FramePatch`）、容器缩放（`InteractionDiagnostics`，priority 800） | 同模组两补丁 + Toolbox Wheel 又 Patch 其子方法 |
| `TreeNodeRender` 体系 | Window Snapping（`OnDrag`）、容器缩放（`SetParent` 带显式 `Type[]` 重载 / `RemoveParent`） | 一个改位置、一个改父子关系，顺序错就抖 |

### 8.2 照抄时的 5 条硬注意

1. **不要重写整个 `BuildXxx`**。这批模组里**没有一个是整段替换的**：`Item Details` 全用
   `AddLine`/`Add` 追加，`Better Sliders` 只 Postfix 后加节点，`Item Spawner` 干脆自己造窗口。
   唯一"替换"是 `TooltipPipeline` 对**单行**的 `return false` 吞行重写，且每处都能"匹配不上就放行"。
2. **改别人字段/返回值的补丁必须让位**。`ClientDemandHighlight` 的 `Validate` Postfix 与 Item Spawner 的
   `CheckRaycast` Postfix 都是**无条件写**，这类补丁叠两个就互相覆盖。改成"条件成立才写 + 尊重已有值"。
3. **输入屏蔽是全局契约**。Toolbox Wheel 打开时把 4 处输入 API 全关掉，这期间任何依赖它们的模组
   （含原版拖拽）都是瞎的。必须像它一样非持有态每帧 `Clear()`、`OnDisable` 里清，否则一次异常退出
   让游戏永久失去输入。
4. **改 getter 是"接管语义"，不是"扩展语义"**。容器缩放把 `GameGridInventory.get_widthPixels/heightPixels`
   全局替换成自己的视口尺寸；第二个"改格子显示尺寸"的模组装上来**必然只有最后一个生效**
   （两个 Postfix 都写 `__result`）。要叠加只能"命中自己的对象才写，否则原样放行"。
5. **Harmony 实例 id 要统一**。`Item Details` 用裸 `"item_details"`，其余 7 个用 `"mod." + manifest.ID`
   （或 `mod.<id>.treesnap` 这类派生）。统一 id 才能保证热重载 `UnpatchAll` 不误伤，也才能被
   `[HarmonyBefore/After]` 引用。

### 8.3 一个安全的分层顺序

第 0 层 官方 `ModHook` 事件（`OnCreateTooltipLate`/`OnGameLoadedLate`/`OnModAssetsLoaded`…）→
第 1 层 只读渲染（自绘 `Graphic` 子节点，无副作用，可任意叠加）→ 第 2 层 追加式 Postfix（往控件列表加节点，
需"我已存在"判重）→ 第 3 层 改写式 Prefix（改参数/吞行/改返回值，必须"匹配不上就放行"）→
第 4 层 属性 getter 接管（`widthPixels` 之类，独占语义，装两个必冲突）→ 第 5 层 IL Transpiler
（`Item Details` 缓存 `GetCurrentValue` 那一处，原版一变即失效）。
`Item Details` 是唯一跨到第 5 层的，也正好是补丁点最多、维护成本最高的那个。

## 9. 对我们的可用点

1. **tooltip 一律走 `ModHook.OnCreateTooltipLate`，不 Patch tooltip 方法**。我们的"估价拆解/真伪结论"
   照 `RendererDef` 表拆成具名小节 + 每节 try/catch + 连错 3 次自停（`ModEntry.cs:296-312`），
   比"一个大 Postfix 里 if-else 到底"好维护，也天然可被配置逐节关。
2. **隐藏属性照抄 `HiddenInfo` + `FeatureReveal`**：`DurabilityHelper.GetRealDurability`、
   `item.FindItemFeatureByID(id).IsFeatureDiscovered()`、`ItemFeature.realCondition`、
   `GetBaseRealDisplay()/GetBaseFakeDisplay()` 均已核实存在，可直接用。
3. **高亮/描边一律自绘 `MaskableGraphic`**，挂 `item.rotator.rectTransform`（拖拽中的挂 `fakeRotator`），
   `raycastTarget=false`，尺寸靠 `anchorMin/Max=0/1 + offset=0` 跟随。绝不用 `Outline` 组件、
   不改 `Image.color`、不换 `sprite`——这三样都会污染原版绘制状态。
4. **容器窗口尺寸记忆写模组私有 Tag**（前缀换成我们的，如 `DSP_VIEW_V1`），值用**格数**而非像素，
   `state` 与 `modifiedState` 双写。不要新建 ModData 存尺寸——物品一走档就丢关联。
5. **要改窗口尺寸就接管 `widthPixels`，别动 `rectTransform`**。原版 `PixelWindow.Validate()` 用子元素
   尺寸反推窗口尺寸（`PixelWindow.cs:370-373`），直接写 rect 会被下次 Validate 抹掉。这条对我们做
   自定义容器界面是决定性的。
6. **内联输入框**：定价/数量输入直接复用 `TMP_InputField` + 透明 `Image` 接点击 + 单帧 Esc 标记这套组合；
   别忘了 `SetSelectedGameObject(null)` 与 `SetActive(false)` 成对做，否则焦点卡住会吃原版快捷键。
7. **跨模组协作不引用 DLL + 两个可抄的探针**：`AppDomain.SetData("item_details.tooltip_sections.v1.*",
   delegate)` 这套字符串插槽 + 版本号，与本工作区"约定 GameObject 名 + `BroadcastMessage` + 带版本字典"
   完全同构（零能力零引用），要给第三方开 tooltip 扩展点就照这个形状设计；探针方面，
   `WheelView.SelfTestPick()` 做几何自测写日志、`Item Details` 每 2 秒打一次
   `[perf] 构建次数/均次/峰值 + 最贵前 4 个模块`（`ModEntry.cs:344-401`）——都是"画面看不出来、
   日志能看出来"的东西，值得进我们的开发版。

**一句话收尾**：效果最稳、代码最少的两个（`After-Hours` 0 补丁、`Item Spawner` 1 补丁）都是"不碰原版
逻辑、只在渲染层加东西"；问题最多的两种（改 `widthPixels` getter、全局输入屏蔽）都是"接管语义"。
选改法前先问：**我改的是渲染结果，还是别人的算法？**
