# 3837983447 · 储藏区美化 / Storage Bay Appearance —— 反编译蒸馏档案

> **工坊 id** `3837983447`｜`sfi.storageappearance` v1.2.8H2｜作者 Xxuebi｜18.8 万订阅｜`<Capabilities />` **零能力**｜游戏 0.46D
> **反编译目录** `缓存文件夹\decomp_ws\3837983447\`（21 个 `.cs`，2445 行，代码全在 `Snowforge.StorageAppearance\`）
> **为什么值得读**：它是"给**原版家具逐件换装**"的完整教学——全模组只有 **1 处**改动画面（`GameItemElement.ResolveItemSprite` 的 Postfix），其余 12 个补丁全是交互与本地化；外观只写**一个字符串 tag**，不碰 `spritePath`/`spriteAtlasPath`/存档结构，却能做到"换完随存档走"；还给出三个工程答案：和别的美化模组怎么不打架、和"机器运行动画"怎么共存、给原版**私有**方法打补丁时怎么自检失效。

---

## 1. 规模与结构

| 文件 | 行 | 角色 |
| --- | --- | --- |
| `StorageAppearanceMod.cs` | 107 | `IMod` 入口：11 个特性补丁 + 2 个手动补丁，`OnDisable` 对称清理 |
| `SkinCatalog.cs` | 660 | 核心：目录（5 风格 × 32 品类）、白名单 `GetSpriteStem`、取图 `TryGetSprite`、持久化 `Apply/Restore` |
| `AppearanceWindow.cs` | 518 | `CustomUIManager` 窗口：风格页签 + 预览网格 + 选中描边 + 动画预览 |
| `AppearanceShortcut.cs` | 318 | F7 悬浮开窗、键位捕获窗、配置读写 |
| `MachineAnimationFrames.cs` | 201 | 纯数据：机器皮肤帧时长表（24/36/48 帧，100–120ms） |
| `MachineAnimations.cs` / `MachineAnimation.cs` | 133 / 81 | 8 列雪碧图懒切片、每帧驱动、生命周期 |
| `VanillaToggleAppearance.cs` | 102 | 复用原版右键菜单的 **Toggle 按钮**当"切换外观"入口 |
| `AdvancedOptimizationCompatibility.cs` | 65 | 探测另一个模组是否接管了重绘 |
| `AppearanceSpritePatch.cs` | 37 | **全模组唯一改画面的补丁**：`GameItemElement.ResolveItemSprite` 的 Postfix |
| `Skin`/`SkinStyle` + 8 个 12~21 行小补丁 | 87 + 124 | 数据类；`CustomUIManager.Update` / `InputActionManager.Update` / `KeyBindHelper.SuppressHotkeys` / `ModConfigWindow` 三连 / 两处本地化 |

**没有 `HotReloadPrepare/Activate`**；`OnDisable` 是一次性全撤，且顺序固定：`SkinCatalog.Active=false` → 关窗口/关捕获 → `UnpatchAll` → `ResetRendering()`（逐件强制重绘回原版）→ `MachineAnimations.Reset()` → `ReleaseDisplaySizedSprites()`（`StorageAppearanceMod.cs:74-89`）。**"撤补丁前先把画面还原"是本档案第一可抄点。**

---

## 2. 它怎么做这件事

### 2.1 外观 id 存在哪：`GameItem.state` 上的一个字符串 tag（不是 ModData，不是独立表）

- tag 名 `sfi.storageappearance.skin`（`SkinCatalog.cs:44`），**值是 `Skin.Key` 字符串**：`"<style>:<category>"`，自定义来源再前缀 `"custom:<stem>:"`（`Skin.cs:32`）。
- 写：`state.GetTag(tag).SetEnabled(true).SetString(skin.Key)` 后立刻 `item.ValidatePartialSprite()`（`SkinCatalog.cs:597-604`）；读：`state.dict.TryGetValue(...)` → `GetString()` → `ByKey` 查表（`:582-595`）；还原：`state.dict.Remove(tag)` 再重绘（`:606-617`）——**不写 "original" 值**，所以没换过装的存档一个字节都不多。
- **为什么"存档里还有"**：`TagSystem` 本身就是物品存档的一部分——读档时原版 `SaveManager.cs:163` 直接 `gameItem2.state = savedItem.itemState.Clone()`，整张 tag 字典原样搬回；克隆物品时 `GameItem.cs:256` 也 `state.Clone()`，所以**逐件独立**，A 柜不会传染 B 柜。写 `ModData`/独立表就得自己处理"读档后重放"，而写 tag 全部免费；代价只有"别人也看得到这个 tag"，用 `GetTagReadonly` 读即可（值是字符串，`IsTag` 只看 Enabled，不会误触原版逻辑）。

### 2.2 换装替换的是哪一层 sprite —— **`GameItemElement.ResolveItemSprite()` 的返回值**

原版三层（`原版:GameItemElement.cs`）：`item.spritePath`/`spriteAtlasPath`（数据、存盘）→ `ResolveItemSprite()`（**private**，`:235-238`，转 `ResolveSpriteByName`：模组雪碧查 `loadedMod.Sprites[name]`，否则 `RenderHandler.LoadFromAtlas`，`:225-232`）→ `ValidatePartialSprite()`（`:240-247`，写 `background.image` + `fakeBackground.image` 并 `SetNativeSize()`）。

它**恰好只挂中间那层**（`AppearanceSpritePatch.cs:6-15`）：Postfix `(GameItemElement __instance, ref Sprite __result)` 里 `__result = sprite`。于是 **`spritePath` 不变 → 存档不变；`Image.sprite` 由原版自己写 → 尺寸/锚点/`SetNativeSize` 全部沿用原版**。它既不是 `SpriteDict`（立绘字典，与容器无关），也不是 `item.sprite`（原版没有此字段），更不是 `SpriteDict.GetSprite`。

复刻五步（缺一步就出画面问题）：

1. Postfix `GameItemElement.ResolveItemSprite`，签名 `(GameItemElement __instance, ref Sprite __result)`；查不到外观就**什么都不做**（不是设 null）。
2. 从 `item.state` 读自己的 tag → 查表得 `Skin`。
3. 取图：`你的LoadedMod.Sprites[SpriteName]`（`LoadedMod.cs:47` 是 `Dictionary<string,Sprite>`，由 `ModHook.OnModAssetsLoaded` 缓存 `LoadedMod`，`SkinCatalog.cs:436-442`）；**必须校验 `sprite.rect.width == 期望纹理尺寸`**，否则当没找到（`:478-491`）。
4. 尺寸不符时按显示尺寸重建（`Sprite.Create(texture, rect, 归一化pivot, ppu * rect.width / displaySize, 0, SpriteMeshType.FullRect)`，见 §3-A4），`Dictionary<Sprite,Sprite>` 缓存、`OnDisable` 统一 `Destroy`。**pivot 要除以 rect 尺寸、PPU 要乘 `rect.width/displaySize`**，否则物品忽大忽小、锚点漂移。
5. 换装/还原后**主动调一次 `item.ValidatePartialSprite()`**（`SkinCatalog.cs:602`）。

动画皮肤多一步：`Style==Machine` 时返回 8 列 sheet 的一帧（`MachineAnimation.FrameAt`，`:51-68`），把元素登记进每帧驱动表；驱动时**只写 `background.image.sprite`/`fakeBackground.image.sprite`**，绝不回头改 `spritePath`（`MachineAnimations.cs:115-121`）。这与 VMA 的零污染口径一致。

### 2.3 与"我们的 `SpriteDict.GetSprite` 立绘自愈"会不会打架 —— **不会，但有两个前提**

| 检查项 | 结论 | 依据 |
| --- | --- | --- |
| 靶点 | **不重叠**：`SpriteDict.GetSprite(string)` 是**立绘**（`StoreClient.spriteName`）字典；`ResolveItemSprite` 是**物品/容器** atlas 解析，`ResolveSpriteByName` 全程不查 `SpriteDict` | `原版:GameItemElement.cs:225-238`；`深空当铺：可能是群友\src\FreyaPortrait.cs:277` |
| 键空间 | **不冲突**：它方雪碧名是 `style__stem_category` / `machine__machine_bay_<cat>__frames`，只在自己 `LoadedMod.Sprites` 取；我方走 `DspCharacters.IsPortraitKey` 白名单 | `Skin.cs:41-48`；`FreyaPortrait.cs:310-313` |
| **前提①** | 别把容器外观做成 `SpriteDict` key：我方在 `GetSprite` 上是 `Priority.Low` 的"最终确认"Postfix（`ReferenceEquals` 就改写），一旦容器贴图也进这个字典，就变成两套事实源抢同一个 `__result` | `FreyaPortrait.cs:325-347` |
| **前提②** | 抄它的 `Image.sprite` 直写时**必须自己保证 PPU/pivot**——那条路跳过了 `SetNativeSize()` | `原版:GameItemElement.cs:240-247` |

### 2.4 F7 悬浮窗 + 右键"切换"按钮

**窗口**（`AppearanceWindow.cs:47-199`）：`CustomUIManager.Window("sfi.storageappearance.chooser", 标题, "overlay")` → `.SetSize().Center().SetCloseOnEscape(true).SetDraggable(true).OnClose(Clear)`；顶部"当前：X / Y"，第二行**风格页签**，下面是 `BeginGrid(columns,104,90,8)` 预览网格（`AddIconButton(帧或静态图, 64, 选中回调)` + 标签），底部"还原游戏默认 / 关闭"。页签切换 = `buttons[i].SetInteractable(i != 当前)` + `ClearContainer(content)` + `new CustomUIBuilder(instance, window, content)` + `LayoutRebuilder.MarkLayoutForRebuild`（`:145-198`）——**清空重建，不缓存元素句柄**；选中态不用原版控件，自己用 4 个 `Image` 拼描边框（`:402-481`）。

**快捷键**：`ModHelper` **有**现成键位接口 `AddGameOptionKeyBind(modId, sectionId, optionId, label, get, set, tooltip)`（`原版:ModHelper.cs:252`），但**本模组没用**——它要在自己的设置窗里做"按键捕获"按钮，于是：配置项存**字符串** `appearance_hotkey_key`（`ModConfig.Define` + `OnChanged` 重新 `ParseKey`，`:66-84`；默认 F7 = `(KeyCode)288`，F1=282 顺推）；可绑定集 = `Enum.GetValues(typeof(KeyCode))` 里 `0<k<323 && !KeyBindHelper.IsReserved(k)`（`:34`、`:86-98`）；捕获窗自建 390×154 小窗，`Input.GetKeyDown` 遍历命中即存、Esc 取消、**进入捕获当帧不算且必须 `Application.isFocused`**（`:204-242`）；触发靠 `CustomUIManager.Update` 的 Postfix（`AppearanceShortcutUpdatePatch.cs:6-16`）；捕获期间 `SuppressGameHotkeys` 为真，并用 Postfix 原版属性 `KeyBindHelper.get_SuppressHotkeys` 把全游戏热键一起压住（`AppearanceShortcutSuppressPatch.cs:5-11`，原版 `KeyBindHelper.cs:83`）。

开窗前的闸门串（`:259-273`，**这行清单是抄作业重点**）：`enabled && GetKeyDown && isFocused && !KeyBindHelper.SuppressHotkeys` → 设置窗没开 → `!StoreUIManager.IsTextFieldFocused()` → `!ItemPinHelper.BlocksClicks` → 没在拖物品（`ItemMouseDragHandler`）→ 没在装备（`ItemSelectHandler`）→ `backgroundBlocking` 没挡 → 才 `EventSystem.RaycastAll` 找 `TreeNodeRender.FindParentElement<GameItemElement>()`，命中且 `IsAvailable && !GeneralHelper.IsDisplayOnly` 才开窗（raycast 列表在 `finally` 里 `Clear()`）。

**右键菜单**：**不新增菜单项，而是复用原版 Toggle 按钮**（`VanillaToggleAppearance.cs:10-101`）：`TriggerButton(RichTextElement)` Prefix(priority 800) 记 `__state=(当前 GameItemElement, AppearanceOnly: !canToggle)`，条件 `button == ___buttonToggle && ___currentInventory == null`；Postfix 开窗，若 `AppearanceOnly` 且窗口真开了就 `__instance.Stop(false)` 收起菜单——**即"借"了一个原本灰掉的按钮，同时不改变它本来能 Toggle 时的语义**；`SetButtonColor(RichTextElement, RichTextElement, bool)` Prefix 把第三参（是否可点）在可换装时改 `true`，按钮就亮了。

### 2.5 "受支持的容器"白名单：**三连判定，不是 id 列表，也不是按类型**

`SkinCatalog.GetSpriteStem(item)`（`:363-425`）是唯一入口，返回 `null` 即不支持：

| 层 | 判据 | 归一化 stem |
| --- | --- | --- |
| ① 硬 id（5 个） | `identifier ∈ {storage_bay, storage_bay_large, machine_bay, machine_bay_ext, hypertech_bay}` | 原样 |
| ② 图集+id+**`spritePath` 三连** | `spriteAtlasPath=="Items/items_ship"` 且（`makeshift_storage_bay` 且 `spritePath=="storage_bay"`）/（`storage_bay_gun`、`storage_bay_chem` 且 `spritePath==identifier`）/（`smuggler_bay`、`smuggler_bay_mod` 且 `spritePath ∈ {smuggler_bay, smuggler_bay_large}`） | `storage_bay` / `smuggler_bay(_large)` |
| ③ 第三方特例 | Brewing Expansion：`be_xuxu_mini_storage`（图集 `brewing_expansion` 且 `spritePath ∈ be_mini_storage_1..4`）、`be_compact_smuggler_bay` | `be_mini_storage` / `be_compact_smuggler_bay` |

**关键设计：把 `spritePath` 纳入判据**——别的美化包改了 `spritePath` 就自动不匹配 → 本模组**静默让位**，不会两个美化模组抢同一张图。取图侧再补一道：`CanUseCustomSkin` 要求来源与目标 stem **像素尺寸相同**（`GetPixelSize` 32/48/64，`:558-571`），`GetTextureSize` 对 `hypertech_bay` 机器皮肤与 `miscellaneous` 品类特判 96（`:498-527`）。

**品类是内容维度而非白名单**：`Categories` 32 项（food/medical/…，`:104-138`），机器区另有 8 项 `MachineCategories`（water-purification/manufacturing/chemistry/hydroponics/husbandry/brewing/power/fish-farming，`:144-154`）。玩家选的是"**风格 × 这个柜子当作什么品类陈列**"，机器区皮肤名形如 `machine__machine_bay_water-purification`——**同一个机器区，里面是水净化还是酿造，外观不同**。这是最易被忽略的产品设计。

### 2.6 与"隐藏固定纹理 / 原版机器运行动画"的接口约定：**不按 id 探测，约定落在原版状态位**

| 对象 | 探测 | 真实约定 |
| --- | --- | --- |
| Advanced Gameplay（`tinyblack.advancedgameplay.item-runtime`，Advanced Optimization 的独立渲染器） | **唯一显式探测** | `Harmony.GetPatchInfo(ValidatePartialSprite).Prefixes.Any(p => p.owner == "tinyblack.advancedgameplay.item-runtime")`（`:60-64`）。在 → 动画帧走 `ApplyAnimationFrame` 并在 `ValidateSelect` 后补一次 `ValidatePartialSprite`；不在 → 直接写 `background.image`/`fakeBackground.image` |
| 隐藏固定纹理 / Hide Pin Overlay（`3838854624`） | 无 id 探测 | 读同一个原版状态位 `ItemPinHelper.BlocksClicks`（原版 `ItemPinHelper.cs:25`，源自 `PINNED_TAG`）：钉住模式开着时 F7 **让位不抢鼠标**（`AppearanceShortcut.cs:264`） |
| 原版机器运行动画 / VMA（`3839773708`） | 无 id 探测 | 靶点不重叠：VMA 管**机器本体**（`MachineCatalog` 三连 + `OwnedFrames` 白名单让位），本模组管**装机器的那台 bay**。共存靠两条礼节：① 提供 **`machine_static`（静态）风格**——不想动、想交还 VMA 时选它；② 它的帧是自建 `Sprite.Create`，不进 VMA 的 `OwnedFrames`，VMA 会当"外来贴图"跳过（VMA 侧口径见 `报纸配图与机器动画两标杆.md:459-466`） |

**可抄结论：跨模组共存的第一选择不是"按 id 探测"，而是"动手条件绑在原版公开状态位 + 提供让位开关"。** id 探测只留一处（真的会改调用链的那个），且探测对象是**补丁表**而非模组列表——对方改名仍有效。

### 2.7 Patch 靶点表（13 处）

| # | 原版方法 | 补丁 | 目的 | 风险 |
| --- | --- | --- | --- | --- |
| 1 | `GameItemElement.ResolveItemSprite()`（**private**） | Postfix `ref __result` | 换装唯一入口 | 私有，改名即**静默全失效**（无日志）；与也在解析结果上动手的美化模组**无仲裁，最后执行者赢** |
| 2 | `GameItemElement.ValidateSelect(bool)` | Prefix `out __state` + Postfix | Advanced Optimization 夺走重绘时补一次 | 读私有属性 `GameItem.spriteChanged`（反射 getter，取不到退化为总重绘）；`ValidateSelect` 自带 `validateLock` 递归锁 |
| 3 | `GameItemElement.ValidatePartialSprite()` | Postfix `bool __runOriginal` | **只在原方法被别人 Prefix 掐掉时**接管（`!__runOriginal`） | 依赖 Harmony 的 `__runOriginal` |
| 4 | `ItemContextHandler.TriggerButton(RichTextElement)` | Prefix(800) + Postfix | 复用右键 Toggle 开窗 | 依赖 4 个 private 字段名（`buttonToggle/canToggle/currentItem/currentInventory`），改名即抛→被外层 try 吞成一条 Warning |
| 5 | `ItemContextHandler.SetButtonColor(RichTextElement, RichTextElement, bool)` | Prefix `ref __2` | 把灰按钮点亮 | 显式 3 参类型表（我们规范的硬要求）；拿不到就跳过 |
| 6 | `CustomUIManager.Update()` | Postfix | F7 轮询 + 键位捕获 | 每帧一次 `RaycastAll`；`Hits` 在 `finally` 清 |
| 7 | `InputActionManager.Update()` | Postfix | 驱动动画帧 | 每帧遍历所有追踪元素；写 `image.sprite` 前先比引用（`ShowFrame`） |
| 8 | `KeyBindHelper.get_SuppressHotkeys` | Postfix `ref __result` | 捕获期压掉游戏热键 | 属性补丁（`get_` 前缀）；影响**全游戏**热键，务必只在捕获帧为真 |
| 9-11 | `ModConfigWindow.BuildEntry(CustomUIBuilder, ModConfig, ConfigEntry)` / `ReadWidget(ConfigEntry, CustomUIElement)` / `WriteWidget(ConfigEntry, CustomUIElement, object)` | Prefix bool ×3 | 把捕获按钮塞进原版模组设置窗并读写 | 用 `__0/__1/__2` 序数取参（不稳）；`return !Try...` = "接管了就跳过原方法" |
| 12-13 | `ModUIManager.ShowMod(LoadedMod, CustomUIWindow)` / `ModConfigWindow.Open(LoadedMod)` | Postfix | 换简介与 `section_General` 标题 | 靠 `window.Get("detail_description")`/`Get("section_General")` **节点名字符串**定位，改名即静默失效 |

> 13 个补丁里只有 #1 是功能必需，#2/#3 是兼容垫片，#4-#13 全是"让人找得到、看得懂"。**给原版家具加交互时，工作量 90% 在入口与本地化，不在渲染。**

---

## 3. 关键代码摘录

### A1 换装的全部魔法：`AppearanceSpritePatch.cs:6-36`（31 行）

```csharp
[HarmonyPatch(typeof(GameItemElement), "ResolveItemSprite")]
internal static class AppearanceSpritePatch
{
	private static void Postfix(GameItemElement __instance, ref Sprite __result)
	{
		if (TryResolveSprite(__instance, out var sprite)) { __result = sprite; }
	}
	internal static bool TryResolveSprite(GameItemElement item, out Sprite sprite, bool optimized = false)
	{
		if (!SkinCatalog.TryGetCurrent((GameItem)(object)item, out var skin, out sprite))
		{
			MachineAnimations.Forget(item);                 // 没换装 → 顺手摘掉动画追踪
			return false;
		}
		MachineAnimation machineAnimation = SkinCatalog.AnimationFor(skin, (GameItem)(object)item, sprite);
		if (machineAnimation != null)
		{
			sprite = machineAnimation.FrameAt(MachineAnimations.Now);
			MachineAnimations.Track(item, machineAnimation, optimized);
		}
		else { MachineAnimations.Forget(item); }
		SkinCatalog.Track(item);                            // 记进 RenderedItems，卸载时逐件还原
		return true;
	}
}
```

### A2 持久化的写 / 读 / 还原：`SkinCatalog.cs:597-617`

```csharp
	internal static void Apply(GameItemElement item, Skin skin)
	{
		if (IsAvailable(item) && TryGetSprite(skin, (GameItem)(object)item, out var _))
		{
			((GameItem)item).state.GetTag("sfi.storageappearance.skin").SetEnabled(true).SetString(skin.Key);
			item.ValidatePartialSprite();                   // 立刻重绘
		}
	}
	internal static void Restore(GameItemElement item)
	{
		if (IsAvailable(item))
		{
			TagSystem state = ((GameItem)item).state;
			if (state != null && state.dict.Remove("sfi.storageappearance.skin"))
			{
				item.ValidatePartialSprite();
				RenderedItems.Remove(item);
			}
		}
	}
```

### A3 跨模组探测只此一处：`AdvancedOptimizationCompatibility.cs:60-64`

```csharp
	private const string ItemRuntimeOwner = "tinyblack.advancedgameplay.item-runtime";
	private static readonly MethodInfo ValidatePartialSprite = AccessTools.DeclaredMethod(typeof(GameItemElement), "ValidatePartialSprite", (Type[])null, (Type[])null);
	private static bool UsesIndependentRenderer => Harmony.GetPatchInfo((MethodBase)ValidatePartialSprite)?.Prefixes.Any((Patch patch) => patch.owner == "tinyblack.advancedgameplay.item-runtime") ?? false;
```

### A4 尺寸对齐（漏了就会忽大忽小）：`SkinCatalog.cs:529-547`

```csharp
	private static Sprite GetDisplaySizedSprite(Sprite source, int displaySize)
	{
		if (!DisplaySizedSprites.TryGetValue(source, out var value))
		{
			Rect rect = source.rect;
			Vector2 val = new Vector2(source.pivot.x / rect.width, source.pivot.y / rect.height);   // pivot 归一化
			float num = source.pixelsPerUnit * rect.width / (float)displaySize;                      // PPU 按比例
			value = Sprite.Create(source.texture, rect, val, num, 0u, (SpriteMeshType)0);            // FullRect
			DisplaySizedSprites.Add(source, value);
		}
		return value;
	}
```

---

## 4. 踩坑与红线

1. **私有方法上做唯一入口 = 一次原版重构就全体失声**。`ResolveItemSprite` 是 private，Postfix 失败也只是"不改 `__result`"，**没有任何日志告诉你它失效了**。抄这个模式必须自检：启用时 `AccessTools.DeclaredMethod(typeof(GameItemElement), "ResolveItemSprite") == null` → `LogError` 一次。
2. **`OnDisable` 顺序反了会留"幽灵皮肤"**：必须"先停渲染开关 → `ResetRendering()` 逐件还原 → 再 `UnpatchAll`"。反过来就再也调不到 `ResolveItemSprite`，元素会一直显示旧皮肤到场景重载（`StorageAppearanceMod.cs:74-89`）。
3. **自建 `Sprite` 必须登记并统一销毁**：`DisplaySizedSprites`（`Sprite→Sprite`）与 `MachineAnimation.frames`（懒切片）各有一个 `Release*`；漏了就是显存泄漏 + "Destroying object multiple times" 告警。
4. **`Sprite.Create` 一律 `SpriteMeshType.FullRect`（`0u`）**：Tight 会裁透明边，锚点错位、机器"跳格"（与 VMA 档案同一结论）。
5. **每帧写 `Image.sprite` 的实现要精确到元素**（`Dictionary<GameItemElement,…>`），这样才和不相关的动画模组天然不撞；反过来若做成"我全权接管一类物品"，就必然要写让位逻辑。
6. **反编译形态不要照抄**：`goto IL_00xx`（`AdvancedOptimizationCompatibility.cs:15-37`）是 `if/else` 的折平产物；`if (!(mod?.Manifest?.ID != Id))`（`ModDescriptionLocalizationPatch.cs:12`）是双重否定，自己写请正着写。
7. **给原版家具做增强不需要任何能力**：两版 manifest 都是自闭合 `<Capabilities />`，不写 `SetGameItemType`、不改存档结构。

---

## 5. 对我们的可用点

**对《深空当铺：可能是群友》（`freya_cornell`）**

1. **"逐件状态挂 tag"可直接搬到我们的物品上**：`state.dict[tag] → 值是一个 key 字符串 → 目录查表`，比 `ModData` 更抗搬迁、读档零钩子，适合"某件遗物被谁签过名""某台机器当前的装饰档位"这类**跟随物品**的小状态；红线照旧——只放 id/枚举字符串，别塞 JSON 大对象。
2. **立绘自愈与容器美化可放心并存**（§2.3 三条依据），但要守住"容器贴图永不进 `SpriteDict`"。将来做"角色专属柜台/房间外观"，**入口选 `GameItemElement.ResolveItemSprite` 的 Postfix**，不要选 `SpriteDict`。
3. **F7 那串开窗闸门（§2.4）是我们现在缺的**：`IsTextFieldFocused / ItemPinHelper.BlocksClicks / IsDraggingItem / IsEquipped / backgroundBlocking` 五连 + `finally` 清 raycast 列表。我们已有浮窗与放大镜，补这串能直接消掉一类"打字时弹窗"的 bug。
4. **零窗口模组要加面板，最短路径就是 `CustomUIManager.Window(...)` + 清空重建**（§2.4 页签写法）——我们全项目目前没有 `CustomUIManager` 窗口。

**对《深空电玩》（`deepspace_arcade`）**

5. **"逐件外观 = 一个 tag + 一个私有解析方法"可直接做街机柜/展柜外观**：它证明同一套帧切片逻辑可以既喂"物品图标"又喂"机器本体"——`MachineAnimations.Get`（`:32-55`）用 `$"{name}__frames"` 从 `LoadedMod.Sprites` 取整张 sheet、按「8 列 × 行」硬校验尺寸，不符就静默不播。我们做"街机柜主题外观"照这个命名与校验，可省一套自校验代码。
6. **`machine_static` 这个"提供静态替代方案"的产品决定值得抄**：任何会动的美化都要给玩家一个"只要好看不要动"的开关，否则和别人的动画模组必然打架（§2.6）。

---

## 附：取证边界

- 已读全 21 个 `.cs`（2445 行），行号取自 `缓存文件夹\decomp_ws\3837983447\`；原版语义均在 `共享开发\reference\Assembly-CSharp\` 核对（标 `原版:` 处）。
- **读不到**：`lang/*.json`（100+ key 只能从 `StorageAppearanceMod.Text(key, fallback)` 调用处反推）、贴图切片（96/64/48/32 四档来自 `GetPixelSize`/`GetTextureSize`，实际 PNG 未验）、`csproj` 宿主版本。
- **未真机验证（推测）**：与 Advanced Optimization / VMA / Hide Pin Overlay 同开时的实际画面顺序；`MachineAnimations.Update` 每帧遍历在数十个机器区下的开销；`OpenHovered` 每帧 `RaycastAll` 的 GC。
