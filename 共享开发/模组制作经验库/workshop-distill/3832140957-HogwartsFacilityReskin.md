# 霍格沃茨当铺·固定设施美化（`tania.hogwarts.approvedshop`）1.0.8｜蒸馏档案

> **工坊 id** `3832140957`｜内部 mod id `tania.hogwarts.approvedshop`｜`HogwartsApprovedShop.dll` 1.0.8｜作者 Tania / Codex｜`GameVersions 0.46D`｜**`<Prerequisites />` 空**、`<Capabilities />` 空（`manifest.xml`）｜1.5 万订阅。
> **反编译目录** `缓存文件夹\decomp_ws\3832140957\`（**34 个 `.cs` / 7,279 行**）＋包内美术实测 **105 张 PNG / 5,325,054 B（5.3 MB）**｜透视报告 `缓存文件夹\decomp_ws\_reports\3832140957.txt`（6 个 ModHook、**20 处手动 `Harmony.Patch`**、零 Transpiler 声明——但实际有 1 处操作数替换，见 §四）。
> **为什么值得读**：这是本库**唯一一份"不做物品、只动场景"的模组**——它换的全是 `SpriteRenderer`/`Image`（卷帘门、墙、床铺、电话、收音机、电脑、面板），并附带一条用 **Post Processing Stack v2 `ColorGrading`** 实现的亮度/饱和度滑轨。它的美术替换**一个补丁都没打**（纯 `LateUpdate` 轮询），却比大多数补丁包更难抄对：**新图尺寸必须与原始 sprite rect 像素级相等**，否则静默保留原图。想复刻"美术包"，本文 §2.6 的规格表就是全部工作量清单。

---

## 一、规模与结构

### 1.1 34 个文件 / 7,279 行：**一半是美术，一半是另一个玩法**

| 角色 | 文件 | 行数 | 占比 | 内容 |
| --- | ---: | ---: | ---: | --- |
| **美术替换与画面调节** | 17 | 3,385 | 46.5% | `ApprovedShopDriver` 587（总调度）、`LedgerThemeController` 419、`RadioInteractionController` 405、`PhoneThemeController` 386、`FixtureRules` 277、`WandTooltipController` 270、`DisplayAdjustmentController` 244、`DisplayGradeController` 216、`DisplayAdjustmentPanel` 175、`ComputerFrameController` 100、`DisplayTuning` 59、`ClickTarget` 59、`DragShield` 58、`DossierSkin` 41、`InteractionGeometry` 21、`DisplayGradeValues` 14、入口 52 |
| **Dobby 袜子/信件玩法**（与美化无关） | 17 | 3,892 | 53.5% | `SockStoragePersistence` **966**、`DobbySockController` 605、`DobbyLetterController` 590、`DobbyReplyController` 372、`DobbySockDropTarget` 287、`DobbySockPresentation` 258、`DobbyLetterAttachment` 240、`DobbyCouponInput` 158、`SockLoadLifecycle` 106、`DobbyLetterMigration` 92、`DobbySockClickTarget` 71、`SockDiagnostics` 47、`DobbyReplyState` 43、`DobbyLetterLayout` 28、`DobbyLetterText` 20、`SockStorageBindResult` 9 |

**这正是本档案第一个要记住的事实**：manifest 的 `Description` 只写了"重新绘制场景元素"，但 DLL 里塞了一整套 **袜子投放 → 被偷 → 信件 → 回信** 的剧情玩法（含 966 行库存持久化）。**订阅者买的是美术，拿到的是两件东西**；我们要么拆成两个模组，要么在页面上明说。

### 1.2 加载路径：普通 `Textures/` 根目录，**不是** `Textures/Items/`

**美术总览：105 张 PNG（41 张是 960×540 整屏层），覆盖 ≈48 个场景节点 + 3 个 `GameItemElement` + 4 处界面；逐张规格见 §2.6。**

原版 `ModLoader.LoadTextures` 把 `Textures/*.png` 载成 `Texture2D` 存在 `mod.Textures[文件名]`，而 `Textures/Items/` 才预先转成 `Sprite`（`共享开发/reference/Assembly-CSharp/ModLoader.cs:1717-1757`）。本包把美术全放在根目录，因此拿到的是**裸 `Texture2D`（Bilinear + 带 mipmap）**，必须在代码里自己 `filterMode = Point` 再 `Sprite.Create`——见 §2.4 的 `Resolve`。

---

## 二、它怎么做这件事

### 2.1 三种替换手法（本档案最核心的技术点）

| 手法 | 用在什么上 | 落地方式 | 代表代码 |
| --- | --- | --- | --- |
| **① 就地换 sprite** | 48 个场景节点（墙、门、床、柜、电话、收音机、按钮…） | 拿到 `SpriteRenderer` → 记原图 → `r.sprite = 自建 Sprite`，**不换 GameObject、不改预制体、不动层级** | `ApprovedShopDriver.cs:226-270` |
| **② 叠子物体** | 电脑边框、袜子、画面调节按钮 | `new GameObject` 挂到原节点下，`sortingOrder = 原+1`，复制 `sharedMaterial`/`layer`/`flip` | `ComputerFrameController.cs:66-80`、`ApprovedShopDriver.cs:300-315` |
| **③ UGUI 重绘 + 染色** | 账簿（`AdvCalendarUIManager`）、电话（`PhoneUIManager`）、纸张按钮 | 遍历窗口下的 `Image`，换 sprite **并** 换 `color`（胡桃/酒红/羊皮纸/黄铜四色板），逐 `Image` 记原值与"我改过没有" | `LedgerThemeController.cs:11-54`、`PhoneThemeController.cs:11-26` |

### 2.2 怎么"找到"这些元素：**transform 路径 + sprite 名，双重白名单**

`FixtureRules.allowed` 是一张 44 条的字典，键是**层级路径字符串**，值是**允许被换的 sprite 名**（`FixtureRules.cs:8-186`）：

```csharp
{ "--Physical Store--/BackgroundGroup/store_back", new[] { "store_background" } },
{ "--Physical Store--/ShutterDoor",                 new[] { "shutter_door" } },
{ "--Physical Store--/EmporiumWall",                new[] { "wall", "wall2" } },
{ "--Interactable/SecButton/Light",                 new[] { "sec lamps red", "sec lamps green" } },
{ "--Physical Store--/Door Lock",                   new[] { "door lock" } },   // 空格后面要换成下划线
```
匹配逻辑（`FixtureRules.SceneKey`，`:188-210`）：路径与 sprite 名**必须同时**在表里命中才返回资源 key；另外 4 组动画帧（`clientele-Sheet_`、`client_counter-Sheet_`、`calendar_top-Sheet_`、`tape anim-Sheet_`）走前缀 + 序号上限判定，序号超上限就**放弃**。路径由 `PathOf(Transform)` 逐级拼 `name + "/"`（`ApprovedShopDriver.cs:84-93`）——**这就是"固定设施"的寻址方式：不挂标签、不做注册表，直接读场景层级名**。

### 2.3 什么时候换：`LateUpdate` 每秒重扫 + 每帧幂等重贴

```csharp
private void LateUpdate()                                   // ApprovedShopDriver.cs:95-152
{
    if (assets == null) { assets = ModLoader.Mods.Find(m => m.Manifest.ID == "tania.hogwarts.approvedshop");
                          if (assets == null) return; }
    if (Time.unscaledTime >= nextFind) { FindFixtures(); nextFind = Time.unscaledTime + 1f; }  // 1 秒重扫
    foreach (var candidate in candidates) if (candidate?.sprite != null) Apply(candidate);     // 每帧重贴
    EnsureSock(); ApplySpecial(...); ApplyShutter(); wandTooltip.Apply(); displayAdjustments.Apply();
}
```
- **扫描**：`Resources.FindObjectsOfTypeAll<SpriteRenderer>()`（含未激活对象），再按 `gameObject.scene.isLoaded` 过滤（`：200-224`）。**不看预制体、不看 asset 名**，只看"活着的场景里有没有这个 sprite"。
- **幂等**：`Ours(sprite)` 判 `sprite.name.StartsWith("hogwartsapproved_")`（`:75-82`），是自己的就跳过——所以每帧重贴的代价只有一次字符串前缀比较。
- **为什么要每帧**：原版自己也会重设 sprite（卷帘门开合、展柜三态、垃圾桶满/空、动画帧推进）。**轮询是这套方案的代价，也是它能不打架的原因**。

### 2.4 几何闸门：**尺寸不等就静默保留原图**（复刻的成败在这 30 行）

```csharp
private Sprite Resolve(string key, Sprite original)          // ApprovedShopDriver.cs:444-500
{
    if (!assets.Textures.TryGetValue(key, out var tex)) { W("Missing texture: " + key); return null; }
    if (tex.width != (int)original.rect.width || tex.height != (int)original.rect.height)
    { W("Geometry mismatch: " + key); return null; }                              // ← 唯一的硬门槛
    tex.filterMode = FilterMode.Point; tex.wrapMode = TextureWrapMode.Clamp;      // 根目录 Textures 默认是 Bilinear
    var sprite = Sprite.Create(tex, new Rect(0,0,tex.width,tex.height),
        new Vector2(original.pivot.x / original.rect.width, original.pivot.y / original.rect.height),
        original.pixelsPerUnit, 0u, SpriteMeshType.FullRect, original.border);    // 轴心/PPU/border 全继承原图
    sprite.name = "hogwartsapproved_" + key;
    cache[key + ":" + original.GetInstanceID()] = sprite;                         // 按原图实例缓存，换图不重建
    return sprite;
}
```
**结论：新图不是"差不多大小"，而是"和原版 sprite 的 rect 像素数完全一致"。** 这条闸门让整个包**不可能画崩版面**，代价是美术必须逐张对齐原图尺寸——这也解释 §2.6 为什么能给出确定规格。

### 2.5 卸载还原：**逐实例记住原图**

`Restore()`（`:502-580`）对每个记过的 `SpriteRenderer`/`Image`：**只有在当前 sprite 仍是 `Ours`** 时才写回原图；同时还原 `alphaHitTestMinimumThreshold`（卡带机为像素级点击改过），最后 `Destroy` 自建 Sprite 与贴图缓存。整套开关是 `OnDisable → ApprovedShopDriver.Restore()`，且 `Restore` 若袜子库存没存成功会**拒绝完成**（`HogwartsApprovedShopMod.cs:34-50` 里 `IsRestored` 为假则推迟销毁）——热重载安全。

### 2.6 美术需求清单：**要自己做同款，就画这 105 张**

> 规格口径：**新图尺寸 = 原图尺寸**（§2.4 几何闸门）；`filterMode` 强制 Point，建议直接按 1:1 像素画，不要放大；透明背景 PNG-32。表格按"元素组 → 规格 → 张数"给出，合计 105 张 / 5.3 MB。

| # | 元素组（场景路径或用途） | 规格（宽×高） | 张数 | 备注 |
| --- | --- | ---: | ---: | --- |
| 1 | 店铺背景层：`store_back`、`store_back_stairs`、`store_mid`、`store_mid_rail`、`store_mid_stairs`、`store_front_floor`、`store_front_wall`、`store_front_lamp`、`EmporiumWall`（`wall`+`wall2`）、`front_counter` | 960×540 | 11 | 整屏图层，**这 11 张就是重画的店铺本体** |
| 2 | 桌面与门面：`Desk`、`Door`、`Door Lock`、`ShutterGlassDoor`、`shutterFrame`、`shutterFrameTop`、`ShutterDoor` | 960×540 | 7 | `door_lock` 里还藏了"画面调节"金色按钮（见 §3.4） |
| 3 | 展柜三态：`showcase`（`display_case`/`_medium`/`_large`） | 960×540 | 3 | 换尺寸档位时同一节点换图 |
| 4 | 电器与交互面：`PC`、`PC/pc_notif`、`NegociationTablet`、`wall_phone`、`Radio`、`CreditDisplay`、`ClientDisplay`、`Calendar`、`_MagnifyingWindow(+Collapsed)` | 960×540 | 9 | 放大镜两个路径共用一张图 |
| 5 | 线缆与叠加：`wires`、`Table Wires`、`Calendar_Wire`、`PC/connecting wire`、`computer_frame_overlay` | 960×540 | 5 | `computer_frame_overlay` 是**叠在电脑上的新层**，不是替换 |
| 6 | 海报与徽记：`poster`、`poster_no_shadow`、`poster_down`、`reputation_box` | 960×540 | 4 | `poster_down` 是挂下/卷边态 |
| 7 | 床铺 `SleepingBag` + 袜子 `dobby_sock` | 960×540 | 2 | 袜子是按床位叠出来的新对象 |
| 8 | 自动售货机 7 档 + 饮水机 7 档 | 64×112 | 14 | `vending0/10/25/50/75/90/100`、`fountain0…`（0/10/25/50/75/90/100 = 库存/水量分档） |
| 9 | 垃圾桶 5 档 + 桶盖 | 96×144 ×5、64×80 ×1 | 6 | 5 档按满溢度；`trashcan` 是 `GameItemElement` 的图标态 |
| 10 | 录音机 6 帧 | 55×40 | 6 | **代码允许 10 帧**（`Frame(..., "tape anim-Sheet_", 10)`），美术只给了 6 帧 |
| 11 | 顾客立绘 4 帧 / 柜台小屏 4 帧 / 日历顶 4 帧 / 档案袋 4 帧 | 25×46、35×21、100×61、112×96 | 16 | 每 4 张一组，`-Sheet_0..3` 命名 |
| 12 | 水龙头 2 张、排水口、手（`_Arm`）、卡带机 | 160×80×2、128×32、160×112、80×80 | 5 | 水龙头/排水口走 `EmporiumEntry` 的 `GameItemElement`（`SpecialKey`） |
| 13 | 霰弹枪阴影 2 张（`_Shotgun`、`_Shotgun/GameObject`） | 49×158 | 2 | 被重绘成"魔杖"外观（文案同步改，§2.9） |
| 14 | 安全按钮 3 + 指示灯 2 + 两封信 | 16×16 ×3、7×16 ×2、32×32 ×2 | 7 | `button_transfer/dismiss/sec`、`sec_lamps_red/green`、`dobby_*_letter` |
| 15 | 账簿框 `ledger_frame` + 电话框 `phone_ui_frame` | 729×496、281×316 | 2 | **UGUI 界面框**，按窗口实际像素画 |
| 16 | 卷帘门放大裁切 + 导师手记 + 纸张按钮 4 | 302×241、255×351、14×15 ×4 | 6 | `Paper/` 子目录走 bilinear `PaperSprites`；`shutter_door_cropped_2x` 是特写/关闭态独立裁切 |

**加总：105 张。** 其中 **41 张是 960×540 整屏层**——这是美化包成本的真实分布：**画一张墙/地面就顶 20 张小图标**。若只想做"低成本同款"，优先顺序是 ②门面 → ①背景 → ④电器 → ⑧售货机，其余可留原版。

### 2.7 亮度/饱和度滑轨：**借原版 Post Processing 的 `ColorGrading`，不碰任何像素**

打开方式：点店铺**左侧金色按钮**（`--Physical Store--/Door Lock` 上的裁切件）→ `CustomUIManager.Window("…:display-adjustments", "画面调节", "front").UseMachineryStyle(true).SetSize(340,235)` 两个 `AddSlider` + "恢复默认/关闭"（`DisplayAdjustmentPanel.cs:75-100`）。

链路是三层，每层都很薄：

1. **数值与换算**：`DisplayTuning` 只存两个**偏移量**（亮度 `[-30, 30]`、饱和度 `[-50, 50]`，`NaN/Inf` 归零，`DisplayTuning.cs:23-46`）；`Grade(...)` 返回"原值 + 偏移"并把亮度百分比换成曝光档 `Exposure = nativeExposure + log2(1 + offset/100)`，同时给出 `BrightnessOverride/SaturationOverride = offset != 0`（`:48-58`）。
2. **施加**：`DisplayGradeController` 对 `StoreUIManager.mainCamera` 与 `shootingCamera` 各建一个 **`PostProcessVolume`（全局、`priority 10000`、`layer 31`）+ 私有 `PostProcessProfile(ColorGrading)`**，把相机的 `PostProcessLayer.volumeLayer` 按位或上 `int.MinValue`（第 31 层）让这个 volume 生效；然后**只对偏移非零的参数打开 `overrideState`**（`DisplayGradeController.cs:118-159`）。→ **默认值时一个 override 都不开，画面与原版逐像素一致**；这是它敢做成"默认即原样"的关键。
3. **记住**：`PlayerPrefs` 两个键 `tania.hogwarts.approvedshop.display.brightness-offset.v1` / `…saturation-offset.v1`（**不是 `ModData`、不进存档**），拖动时只 `SetFloat`，`0.4s` 后再 `PlayerPrefs.Save()`（`DisplayAdjustmentController.cs:56-61`、`:174-189`）。**恢复默认** = `tuning.Reset()` + 两条滑轨 `SetFloat(0)` + 触发一次 `Changed()`（`DisplayAdjustmentPanel.cs:146-155`）。

可用性细节（值得抄）：`CanOpen` 要求"在店内、非拍照模式、非放大镜模式"（`DisplayAdjustmentController.cs:43-54`）；`Grade.Apply` 一旦 `!inStore || tuning.IsDefault` 就 `Release()` 并清空（`DisplayGradeController.cs:51-57`）；面板打开时用 `DisplayDragShield` 把 `ItemMouseDragHandler` 的拖拽整体禁掉，避免"拖滑轨把货拖走"（`:44-51`）。

### 2.8 冲突与"让位"

- **没有任何跨模组探测**：全库只有一处 `ModLoader.Mods.Find`，找的是**自己**（`ApprovedShopDriver.cs:105`）。
- 让位靠**匹配语义**：换图前提是"路径命中 **且** 当前 sprite 名=`store_background` 这类原版名"。**别人先换成自己的图，我们的白名单就不再命中，于是我们静默不动**（`SceneKey` 返回 null）。这是**"谁先换谁占"**，不是协商。
- 幂等靠前缀：自己的图不会被二次包一层（`Ours`）；**风险**是两个都用"原版名→自己的图→每帧重贴"的包之间是**每帧互相覆盖**（各自 `LateUpdate` 谁后跑谁赢），且互不知情。原版没有"美术包优先级"这回事，**我们的美术包必须自带一个显式开关**（哪怕只是 ModConfig 里的一个 bool），否则玩家没法退让。

### 2.9 Patch 靶点表（**20 处，按类型聚合；美术替换 0 处**，全在玩法与拖拽闸门）

| 原版类型 | 方法 | 类型 | 目的 | 风险 |
| --- | --- | --- | --- | --- |
| `ItemMouseDragHandler` | `OnEventPress` / `StartDrag` | Prefix | 画面调节面板打开时禁拖拽 | **高**：这是全店拖拽总闸，误判=玩家拖不动货 |
| `ItemMouseDragHandler` | `Drag`（**操作数级 Transpiler，换 `RaycastElement<GameItem>` 调用目标**）/ `EndDrag` / `OnEventReset` | Prefix/Postfix + Transpiler | 袜子成为世界投放目标并清理状态 | **最高**：泛型方法按名匹配 + 改 IL 操作数，原版改签名即失效；`StartDrag`/`EndDrag` 还被两个补丁同时打 |
| `StoreUIManager` / `HandnoteUIManager` / `PaperUI` | `CheckRaycast` / `OpenPanel(GameItem)` / `Build` | Prefix | 优惠券拾取判定、拦截原版手记面板换成信件纸、改纸张构建内容 | 中 |
| `SoldItem` | `TrackItem`（static） | Postfix | 捕捉售出以推进剧情（`GameItemElement.Destroy` 另挂一对 Prefix/Postfix 追踪销毁） | 低 |
| `PlayerStore` | `EndNight` | Prefix+Postfix+Finalizer | 袜子库存入夜提交 | **高**：夜间提交，失败会卡存档流程（它自己写了 `IsRestored` 守卫推迟卸载） |
| `PlayerStore` | `ClearTrash` / `WriteSlotFile` / `FindAllItem(bool)` | Prefix/Postfix | 保护垃圾箱里的信件、写盘前修正容器、补查询结果 | **高**：其中 `WriteSlotFile` 直接碰存档写入 |
| `EmporiumEntry` | `GetAllItems` / `GetAllOwnedItems(bool,bool)` / `GetAllOwnedItemsShallow(bool)` | Postfix | 同上（三条查询路径都要补） | **高**：四条查询并存，漏一条就"物品消失" |
| `StolenHelper` | `UpdateStolenItem(GameItem)` | Prefix | 袜子不许被偷 | 中 |
| 反射直读（不打补丁） | `PlayerStore.instance`、`SaveManager.itemHeap`、`StoreShutterButton.isClosed`、`ItemMouseDragHandler.lastItem/lastInventory`、`TooltipManager.tooltipOwner` | 字段反射 | 省掉一堆补丁 | 中：字段改名即静默失效 |

---

## 三、关键代码摘录

### 3.1 寻址 = 路径 + sprite 名双重白名单（`ApprovedHogwarts/FixtureRules.cs:188-219`）

```csharp
public static string SceneKey(string path, string sprite)
{
    if (path == null || sprite == null || sprite.StartsWith("hogwartsapproved_", StringComparison.Ordinal)) return null;
    if (allowed.TryGetValue(path, out var names))
        foreach (var name in names)
            if (name == sprite) return name.Replace(' ', '_');       // "door lock" → door_lock（文件名）
    if ((path == "--Interactable/ClienteleDisplay" && Frame(sprite, "clientele-Sheet_", 4))
     || (path == "--Interactable/ClientDisplayTop" && Frame(sprite, "client_counter-Sheet_", 4))
     || (path == "--Interactable/CalendarTop"     && Frame(sprite, "calendar_top-Sheet_", 4))
     || (path == "--Interactable/Recorder"        && Frame(sprite, "tape anim-Sheet_", 10)))
        return sprite.Replace(' ', '_');
    return null;                                                     // 不命中 = 让位给原版/别人
}
private static bool Frame(string sprite, string prefix, int count)   // 只认 0..count-1 帧
    => sprite.StartsWith(prefix, StringComparison.Ordinal)
       && int.TryParse(sprite.Substring(prefix.Length), out var i) && i >= 0 && i < count;
```

### 3.2 应用：记原图 → 几何校验 → 换 → 幂等（`ApprovedHogwarts/ApprovedShopDriver.cs:226-270`，节选）

```csharp
private void Apply(SpriteRenderer r)
{
    if (Ours(r.sprite)) { displayAdjustments?.Bind(r); return; }               // 已是我的图，只补绑定
    string key = FixtureRules.SceneKey(PathOf(r.transform), r.sprite.name);
    if (key == null) return;                                                   // 不归我管
    if (key == "negociation_tablet") { computerFrame.Apply(r, assets); return; } // 特殊件走叠加层
    Sprite mine = Resolve(key, r.sprite);                                      // §2.4 几何闸门
    if (mine == null) return;
    int id = r.GetInstanceID(); originals[id] = r.sprite; renderers[id] = r;   // 按实例记原图供还原
    r.sprite = mine;
    if (key == "sleeping_bag") bedRenderer = r;  if (key == "radio") radioRenderer = r;
    displayAdjustments?.Bind(r);                                               // 只有 door_lock 会真绑
}
```

### 3.3 滑轨如何改整屏颜色（`ApprovedHogwarts/DisplayGradeController.cs:140-159`，节选）

```csharp
binding.profile = ScriptableObject.CreateInstance<PostProcessProfile>();
binding.grade   = binding.profile.AddSettings<ColorGrading>();
binding.grade.SetAllOverridesTo(false, false);                // 先全部关掉
binding.grade.enabled.Override(true);                         // 只开"效果本身"
binding.volumeObject = new GameObject("HogwartsApprovedShop.DisplayGrade");
binding.volumeObject.layer = 31;                              // 私有层，避免污染别人的 volume
binding.volume = binding.volumeObject.AddComponent<PostProcessVolume>();
binding.volume.isGlobal = true;  binding.volume.priority = 10000f;  binding.volume.weight = 1f;
binding.volume.sharedProfile = binding.profile;
// …每个相机把 volumeLayer 或上 int.MinValue（第 31 层）…
var v = tuning.Grade(binding.brightness, binding.exposure, binding.saturation);
binding.grade.brightness.overrideState   = v.BrightnessOverride;   // 偏移≠0 才 override
binding.grade.postExposure.overrideState = v.BrightnessOverride;
binding.grade.saturation.overrideState   = v.SaturationOverride;
binding.grade.brightness.value = v.Brightness; binding.grade.postExposure.value = v.Exposure; // log2(1+offset/100)
binding.grade.saturation.value = v.Saturation;
```
还原同样克制：自己建的 `PostProcessLayer` 就销毁、原生的就**只把 mask 位摘掉并恢复 `enabled`**（`:162-210`），且带"mask 被别人改过就不动"的判断（`:183-190`）。

### 3.4 金色按钮：**从 door_lock 图里裁一块出来当按钮**（`ApprovedHogwarts/DisplayAdjustmentController.cs:83-118`，节选）

```csharp
if (renderer.sprite.name != "hogwartsapproved_door_lock" ||
    PathOf(renderer.transform) != "--Physical Store--/Door Lock") return;     // 只有我的门锁图才挂按钮
Sprite s = renderer.sprite;
target = new GameObject("display-faceplate");
target.transform.SetParent(renderer.transform, false);
target.transform.localPosition = new Vector3((73f - s.pivot.x) / s.pixelsPerUnit,
                                             (s.rect.height - 212.5f - s.pivot.y) / s.pixelsPerUnit, -0.001f);
faceplate = Sprite.Create(s.texture,                                        // 复用同一张图，不额外出 PNG
    new Rect(s.rect.x + 58f, s.rect.y + s.rect.height - 235f, 30f, 45f),    // 30×45 的金色面板
    new Vector2(0.5f, 0.5f), s.pixelsPerUnit);
target.AddComponent<SpriteRenderer>().sprite = faceplate;
target.AddComponent<BoxCollider2D>().size = new Vector2(30f / s.pixelsPerUnit, 45f / s.pixelsPerUnit);
target.AddComponent<DisplayAdjustmentClickTarget>();                        // 点它 = Toggle 面板
```
**这解释了一个美术需求**：`door_lock.png` 的 **右上角 (58, height-235) 起 30×45 像素**必须留一块金色面板位，否则按钮会裁到空白。这条约束在源码里，不在文档里。

---

## 四、踩坑与红线

1. **尺寸不等 = 静默降级**：`Resolve` 尺寸比对失败只打一条 `LogWarning("Geometry mismatch; original retained")`（`ApprovedShopDriver.cs:495-499`），玩家看到的是"某些图没换"。**美术交付必须逐张核 pixel size**，且不同 sprite 的 `rect` 与 `texture` 尺寸可能不同（图集裁切时）。这是本档案最贵的坑。
2. **`Textures/` 根目录拿到的是 Bilinear + mipmap 的裸贴图**（`ModLoader.cs:1741-1754`）。不显式 `filterMode = Point` + `wrapMode = Clamp` 就会糊边。若想省这一步，把图放 `Textures/Items/`（那里会预设 Point + PPU 100），但那条路只给 `Sprite`、轴心固定中心。
3. **每帧重贴 = 每帧与所有其他美化包赛跑**。`LateUpdate` 无优先级、无协商，别人也这套写法就是每帧互刷。**要做可共存，必须给玩家一个显式开关**（本包没有）。
4. **阈值型点击区域被美术改动牵连**：卡带机为了像素级命中改过 `Image.alphaHitTestMinimumThreshold`，还原时必须一并还原（`ApprovedShopDriver.cs:560-563`）。换图包一旦碰 `Image`，就要想到命中阈值、`raycastTarget`、九宫格 border。
5. **20 处补丁里有 1 处是操作数级 Transpiler**（`DobbySockDropTarget.cs:201-204` 按 `DeclaringType == typeof(RenderHandler) && Name == "RaycastElement"` 且泛型参数为 `GameItem` 去改 IL 操作数），**透视报告的最初扫描没把它列为 Transpiler**——说明自动扫描会漏"反射取 MethodInfo 再 Patch"这种形态，我们做冲突扫描时不能只信报告。
6. **`PlayerStore.EndNight` / `WriteSlotFile` 上挂三件套（Prefix/Postfix/Finalizer）**：这是"绝不整段替换夜间提交"的正确姿势，但仍属高危区；它自己用 `IsRestored` 守卫"库存没存成功就拒绝卸载"（`HogwartsApprovedShopMod.cs:43-47`），这个守卫值得抄。
7. **模组页描述与内容不符**：manifest 只宣传美术，DLL 里 53.5% 是另一套剧情玩法。**发布前要么拆包要么写清**，否则玩家会觉得"装了个美化包结果床上有只袜子"（推测玩家反应）。
8. **美术全走替换、0 张新增**（只有电脑边框与袜子是新增对象），所以它**无法替换"原版没有的 sprite 名"**，想做新元素只能走手法②。另外 `tape anim-Sheet_` 代码放行 **10** 帧、美术只给 **6** 帧（`FixtureRules.cs:205`）：放行数量是上限，不是应有数量。

---

## 五、对我们的可用点

1. **"路径 + 原名"双白名单 + `Ours` 幂等 + 实例表还原**这三件套 →《深空电玩》做"街机厅/店面美化"、《深空当铺：可能是群友》做"当铺主题换皮"的**唯一正确骨架**：不打补丁、不改预制体、卸载可逆，可抄成 `ArcFixtureSkin` 把 `FixtureRules` 换成我们的 JSON 表。配套把 **§2.4 的几何闸门写进离线测试**（`tests/integrity`）——我们已有 8 张 `1470×1070` 立绘、6 张 `720×420` 名片、14 张 16px 物品图标，换任何现有 sprite 都必须继承原图 pivot/PPU，否则定位全偏，这些**不能靠人工比对**。
2. **`DisplayGradeController`：用 Post Processing 的 `ColorGrading` 做"氛围/时段"而不改像素** →《深空电玩》的"低迷周/夜间营业"、《群友》的"闭店后/雨天"都能用同一套：私有 `PostProcessVolume`（layer 31 + `priority 10000`）+ **偏移为 0 时不打开任何 override**。这是"默认与原版逐像素一致"的关键纪律，比 tint 整屏 `Image.color` 干净得多。
3. **`PlayerPrefs` + 0.4s 防抖 + 语义化 `…v1` 键**（`DisplayAdjustmentController.cs:174-189`）→ 我们所有"玩家偏好"类设置（画质、音量、UI 缩放、音效开关）都该走这条，而不是塞 `modData` 污染存档。**键名带模组 id 与 `.vN`**，以后改语义直接换键。
4. **`door_lock` 里裁 30×45 当按钮**（§3.4）→《深空电玩》街机的"投币口/开始键"、《群友》的"电话听筒/名片槽"都可以这样做：**把热区画进主美术里，代码按整数像素矩形裁出来挂 `BoxCollider2D`**，省一张图、天然对齐、缩放不错位。
5. **`CustomUIManager.Window(...).AddSlider/AddLabel/BeginRow` + 把窗口 re-parent 到 `StoreUIManager.Instance.primaryCanvas` 并补 `StoreUI` 组件**（`DisplayAdjustmentPanel.cs:100-121`）→ 我们已有浮窗都吃这套 API；这里补上了两个我们常忘的细节：**`SetAsLastSibling()`** 与**透明 `Image` + `raycastTarget = true` 兜底**（否则窗口区域会点穿到店铺）。

---

## 附：待验证 / 风险

- `SockStoragePersistence`（966 行）、`DobbySockController`、`DobbyLetter*`（共约 2,900 行）**只读了补丁装配段与字段反射表**，玩法状态机未通读；本文对它只作"存在 + 侵入了哪些原版方法"的判断。
- `LedgerThemeController`（419）/ `PhoneThemeController`（386）/ `RadioInteractionController`（405）只读了头部与状态结构（`ImageState` 四色板（胡桃/酒红/羊皮纸/黄铜）），**逐控件的路径匹配表未逐条读**，故 §2.6 中"界面框 2 张"只覆盖 `ledger_frame`/`phone_ui_frame` 两张大图，纸张按钮 4 张按文件推得。
- §2.6 的"元素组→张数"是按 **`Textures/` 实测文件名与尺寸**（105 张全量枚举）与 `FixtureRules` 的 48 条路径**交叉对齐**得到的，个别动画帧与状态的对应（如垃圾桶 5 档）为推测；全库**无真机结论**，"能跑/不打架"的判断都来自源码静态阅读。
