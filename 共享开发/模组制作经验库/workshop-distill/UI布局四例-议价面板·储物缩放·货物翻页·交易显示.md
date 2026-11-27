# UI 布局四例：议价面板左移 / 储物窗口缩放 / 商人货物翻页 / 交易利润显示

> 蒸馏自 `缓存文件夹\decomp_ws\` 下 4 个"改 UI 布局/尺寸"的工坊模组（**67 个 .cs / 6,131 行**）：`3829267478`、`3835141019`、`3835147893`、`3828924760`。
> 原版代码取 `共享开发\reference\Assembly-CSharp\`（0.46D 反编译）；行号 = `文件:行号`；`//IL_xxxx` 噪声已清；追不到的判断标「（推测）」。

## 0. 速查：四种"改布局"的代价指纹

| 工坊号 | 名义 | 订阅 | 规模 | 改的到底是什么 | 补丁 | 一句话代价 |
| --- | --- | --- | --- | --- | --- | --- |
| 3829267478 | 议价面板左移 | **17.4 万** | 8 cs / 515 行 | 一个 `anchoredPosition.x` + 一个私有基准量 | 4 | 抓不住每帧复算的基准就白改 |
| 3835141019 | 储物窗口缩放 | 6.3 万 | 20 cs / 3,171 行 | 窗口 `localScale` 乘百分比（不动尺寸） | 21 | 字一起缩，拖影/弹层/扩容要补 |
| 3835147893 | 商人货物翻页 | 9.8 万 | 17 cs / 862 行 | 货在柜台与"手里"之间搬进搬出 | 11 | 手工搬物品，回滚只能尽力 |
| 3828924760 | 更好的交易显示 | 4 万 | 22 cs / 1,583 行 | 行右侧一个字符串 + 夜间报告一行 | 17 | 每帧钩子里的价格读取极易翻倍 |

**总判断**：便宜的改布局都是找到原版**只在一个地方写**的量（坐标写点 / 缩放基准 / 一个字符串参数 / 一组列表）；凡"接管算法"的（接管 `widthPixels` 上报、重算布局），补丁数立刻从 4 涨到 20 以上。

---

## 一、`3829267478` 议价面板移动到左侧（Bargain Panel On The Left）

> `3829267478` · **17.4 万订阅（本批最高）** · 8 文件 / 515 行 · v1.1.0 · `BargainPanelLeft.dll` · 自闭合 `<Capabilities/>`
> 源码 `缓存文件夹\decomp_ws\3829267478\`（入口 `BargainPanelLeft\BargainPanelLeftMod.cs`，核心 `PanelMirror.cs` 383 行）
> 为什么值得读：**"移 UI"最容易被"原版自己每帧写回"打败**，这 515 行把"谁写坐标、写在哪、怎么抢在它后面"讲透了；订阅最高、代码最少，是本批最好的对照样本。

### 1.1 规模与结构

| 区段 | 行 | 位置 | 职责 |
| --- | --- | --- | --- |
| 坐标迁移 | 94 | `PanelMirror.cs:54-150` | `Apply` 32 + `FlipBaseline` 17 + `MirrorChildren` 28 + `ApplyArtMirror` 17 |
| 关闭按钮 | 166 | `PanelMirror.cs:164-334` | 建 / 摆 / 藏 / 点 / 复制原版贴图 |
| 装饰复位 + 反射 + 查节点 | 83 | `PanelMirror.cs:13-52/152-162/336-382` | `PinTabletArt` 11、3 个 `AccessTools`、按名索引子节点 |
| 4 个补丁 / 日志 / 入口 / 元数据 | 172 | `OpenUIPatch.cs` `CloseUIPatch.cs` `DockToTabletPatch.cs` `KeepTabletArtPatch.cs` 各 21 行；`Log.cs` 23；入口 19；6 | 补丁全 `try/catch + Log.Error`（边界捕获，写得对） |

**"515 行怎么花的"：没有任何配置项、没有 ModHelper、没有 ModHook、没有读档重置**——manifest 的 `<Description>` 就是全部设置。
**166 行（32%）用在"加一个关闭按钮"**，真正搬面板只 94 行；`PanelMirror` 另约 48 行是解编译噪声。

### 1.2 它怎么做：先找"谁在原版里写坐标"

| 原版类 / 方法 | 行 | 角色 |
| --- | --- | --- |
| `BargainUIManager.CreateWindow` | `BargainUIManager.cs:285-320` | 建窗、`uiObject.name="BargainPanel"`（`:306`）、挂 `ZoomFollower`（`:308`）与 `BargainPanelDock`（`:309`） |
| `BargainUIManager.OpenUI` | `:1637-1646` | 每次打开 → 先 `DockToTablet()`（`:1641`）再 `SetActive(true)` |
| **`BargainUIManager.DockToTablet`** | `:1675-1694` | **唯一写面板坐标处**：抄平板 parent/anchor/pivot，`anchoredPosition = 平板 + (476 × 平板.localScale.x, 0)`（`:1691`），末尾 `follower.SetTarget(...)` |
| **`ZoomFollower.SetTarget`** | `ZoomFollower.cs:29-40` | 抓基准 `baseLocalOffset = target.InverseTransformPoint(transform.position)`（`:36`）——此刻面板在**右侧** |
| **`ZoomFollower.LateUpdate`** | `ZoomFollower.cs:42-57` | **每帧** `transform.position = target.TransformPoint(baseLocalOffset)`（`:54`）+ 按 target 缩放同步 localScale |
| `BargainPanelDock.LateUpdate` / `NegociationUIManager.RefreshBargainControl` | `BargainPanelDock.cs:7-22` / `NegociationUIManager.cs:552-586` | 前者只改 `SetSiblingIndex`（不写坐标）；后者由 `RefreshNow`（`:522`）每 0.2 s 调，`:562-565` 把平板 art 在 `artBargainOpen`/`artNormal` 间切 |

**"原版会不会重置位置"：不是每帧重置，而是两个写点叠加**——① `OpenUI → DockToTablet` 每次打开把面板放回**右侧**；② `ZoomFollower.LateUpdate` **每帧**用 `baseLocalOffset` 重算世界坐标。
所以只 Postfix 改 `anchoredPosition`，下一帧就被拉回。`RefreshUI`（`:329-346`）不碰坐标。

| Patch 靶点 | 类型 | 干什么 | 出处 |
| --- | --- | --- | --- |
| `BargainUIManager.DockToTablet` | Postfix | 移左 + 翻转 `ZoomFollower` 基准 | `DockToTabletPatch.cs:6-20` → `PanelMirror.Apply` |
| `BargainUIManager.OpenUI` / `CloseUI` | Postfix | 贴 / 藏关闭按钮 | `OpenUIPatch.cs` / `CloseUIPatch.cs:6-20` |
| `NegociationUIManager.RefreshBargainControl` | Postfix | 平板 art 强回 `artNormal`（0.2 s 一次） | `KeepTabletArtPatch.cs:6-20` |

**分辨率 / 缩放**：偏移写成 `476f × 平板.localScale.x`（`PanelMirror.cs:81`），平板三档缩放时跟着走；基准在**平板局部空间**里翻（`:100`），`LateUpdate` 每帧按当前 scale 重算，缩放变化无需额外代码。
但**面板内部镜像硬编码 400 宽**（`PanelWidth`，`400f - X - Width`，`:124`）+ 一张 6 行"原版右列位置表"（`Children`，`:44-52`）——换分辨率或改面板宽就整块错位：**它不处理分辨率，只处理平板缩放**。

### 1.3 关键代码摘录

```csharp
// 原版：每次打开都把面板钉到平板右侧，并重抓一次基准 —— BargainUIManager.cs:1675-1694（节选）
if (rectTransform2.parent != rectTransform.parent)
    rectTransform2.SetParent(rectTransform.parent, worldPositionStays: false);
float x = rectTransform.localScale.x;  float num = 476f;
rectTransform2.anchorMin = rectTransform.anchorMin;
rectTransform2.anchorMax = rectTransform.anchorMax;
rectTransform2.pivot     = rectTransform.pivot;
rectTransform2.anchoredPosition = rectTransform.anchoredPosition + new Vector2(num * x, 0f);  // ← 写点
rectTransform2.localScale = new Vector3(x, x, 1f);
follower.SetTarget(rectTransform, 1f, x);   // ← 抓基准（此时位置 = 右侧）
dock.tablet = rectTransform;

// 模组：先翻基准，再改坐标（顺序不能反）—— PanelMirror.cs:79-102（节选）
bool flag = FlipBaseline(mgr);
Vector2 anchoredPosition = val.anchoredPosition;
anchoredPosition.x = val2.anchoredPosition.x - 476f * ((Transform)val2).localScale.x;   // 平板左侧
val.anchoredPosition = anchoredPosition;
MirrorChildren(val);
...
ZoomFollower val = FollowerRef.Invoke(mgr);      // FieldRefAccess<BargainUIManager,ZoomFollower>("follower")
Vector3 val2 = (Vector3)BaseOffsetField.GetValue(val);   // ZoomFollower.baseLocalOffset（[SerializeField] private）
val2.x = 0f - val2.x;
BaseOffsetField.SetValue(val, val2);
```

### 1.4 踩坑与红线

1. **只 Postfix 改 `anchoredPosition` 必失败**：`LateUpdate` 每帧拉回；必须反射翻 `baseLocalOffset`（`ZoomFollower.cs:10`）。这是"原版私有字段"，升级即断，它留了 Warn 兜底（`PanelMirror.cs:96`）。
2. **翻转的幂等性靠"原版每次都先写回右侧"**：`DockToTablet` 先写 `+476x` 再 `SetTarget` 抓基准，所以每次打开基准都是右侧值，翻两次不会左右横跳。**若原版哪天改成"记住上次位置"，这里就变抖动**（推测：它对原版最脆的依赖）。
3. **光移位置不够，还得管装饰**：面板打开时平板右侧 art 会切成 `artBargainOpen`（`NegociationUIManager.cs:562-565`），搬到左边后那圈高亮留在右边很怪 → 必须在 `RefreshBargainControl` 后每 0.2 s 强回 `artNormal`。
4. **关面板不能直接 `CloseUI`**：走"模拟点平板议价按钮（`ToggleUI` + 反射 `RefreshBargainControl`），失败才退回 `CloseUI`"（`PanelMirror.cs:203-229`），因为自己在 3 里改了 art。
5. **关闭按钮复制原版视觉**：`sprite`/`transition`/`spriteState`/`colors`/`animationTriggers` 全抄平板的 `closeButton`（`:293-312`），并 `TabletUI.NoKeyboardFocus` 防抢焦点（`:197`）；找不到源按钮退化成 90% 白 `Image`（`:316`）——不崩但难看。
6. 小瑕疵：`CloseButtonLeft=346 / CloseButtonTop=-6`（`:32/34`）没被内联到任何位置，实际写死 `-330/305`（`:328-331`）→ 死常量（推测：挪过位置没删）。

### 1.5 对我们的可用点

1. **"移 UI"四步法**：`grep "anchoredPosition ="` 找写点 → 看是否在 `LateUpdate`/`OnDrag` 里被反复调 → 看有没有 `baseLocalOffset` 这类**基准复算**结构 → 有就"Postfix 改坐标 + 一起改基准"，没有就一个 Postfix。**先认结构，再写补丁。**
2. 我们《深空当铺：可能是群友》的名片/电话/验水面板若要挪位：先确认是否也挂了 `ZoomFollower`（原版纸面 UI 通用件，三档缩放都靠它）；是的话照 1.3 抄两段即可，约 40 行。
3. `MirrorChildren` 的 `400f - X - Width` 是"镜像一张绝对坐标位置表"的通用做法：任何左列/右列面板要翻边，都用这张表 + 一次取反，别去动 LayoutGroup。

---

## 二、`3835141019` 储物窗口拖动缩放（Container Window Scale）

> `3835141019` · 6.3 万订阅 · 20 文件 / 3,171 行 · v1.4.5 · `ContainerWindowScale.dll` · 自闭合 `<Capabilities/>` · 0 特性式补丁 / 21 处手动 `Harmony.Patch`
> 源码 `缓存文件夹\decomp_ws\3835141019\`（入口 `ContainerWindowScale\Mod.cs`，核心 `WindowScale.cs` 801 行）
> 为什么值得读：**"改窗口尺寸"的另一条路**——不接管任何 `widthPixels` getter，只给窗口乘一个 `localScale`，却仍需 21 处补丁把缩放副作用逐个擦掉。**这份副作用清单比尺寸本身值钱。**

### 2.1 规模与结构

| 文件 | 行 | 职责 |
| --- | --- | --- |
| `WindowScale.cs` | **801** | 每窗一个组件：抓原尺寸、算百分比、造手柄、拖拽、标题栏重排、边界夹取、位置记忆、夜间/地面特例 |
| `ItemPreview.cs` | 291 | 拖影（fake 层）尺寸与落点补偿——缩放最大的坑 |
| `BulkUI.cs` + `PauseEntry.cs` | 300 + 217 | Esc 菜单插"统一本局窗口大小"入口 + 自绘滑条窗口（50–150） |
| `Patches.cs` / `Mod.cs` | 269 / 232 | 21 个回调入口层（全 `try/catch + LogError`）/ 21 处手动 `Patch()`、`Attach`、失败即整体 `OnDisable` |
| `LayoutMemory.cs` | 226 | 存档记忆（ModData）+ 会话记忆 + 批量默认值 + 读档生命周期 |
| `ScaleMath.cs` / `Settings.cs` | 181 / 131 | 百分比夹取、拖动换算、屏幕夹取、拖影基准 / 7 个 `ConfigEntry`（中英双语） |
| `LayoutMath.cs` / `WindowIdentity.cs` / `NightRunCompat.cs` | 82 / 75 / 71 | 归一化坐标 / 窗口身份键 / 跨模组兼容 |
| 其余 8 个 | 295 | `DragHandle` 57、`GroundScaleLayer` 52、`ItemLayoutRepair` 52、`RenderScale` 49、`ExpansionScale` 45、`LayoutRecord` 23、`ScreenWorkspace` 13（**空壳**）、`AssemblyInfo` 4 |

**结构特征**：0 个 `[HarmonyPatch]`，全部手动 `harmony.Patch(...)` 并先过 `ValidatePatchTarget`（`Mod.cs:148-165`）——因为靶点含 `Show` 的重载，只能反射遍历（`:110-117`）。`Enabled` 静态位 + `catch{ OnDisable(); }` 保证装到一半失败即整体回滚（`:141-145`）。

### 2.2 它怎么做：原版不能缩放，所以它换了一条路

**先确认原版：容器窗口根本不能缩放。** `PixelWindow` 自带右下角缩放手柄 `bottomResizeButton`，但只在 `canResize` 为真时启用（`PixelWindow.cs:378`）；`canResize => childElement.canResize && canMove`（`:76-84`）；容器是 `GameGridInventory.canResize => false`（`GameGridInventory.cs:53`）、`GameGridScrollableInventory` 同（`:49`）。
尺寸来源是内容上报：`GameGridInventory.Validate()` 每次 `widthPixels = inventoryShape.width * gridSize`（`:255-257`），`ResizePixels` 是**空实现**（`:240-245`）；外层 `PixelWindow.Validate()` 再反推（`PixelWindow.cs:359-373`）→ 直接改 `sizeDelta` / 写 `widthPixels` 会被下次 `Validate()` 抹掉（与 `UI深度扩展八例` §4.1 一致）。

**它选的路：不改尺寸，整窗乘一个百分比。** `Initialize` 抓一次 `originalScale`（`WindowScale.cs:168`）→ `Change()` 只写 `localScale = originalScale * Percent/100`（`:319-354`）→ `ReapplyScale()` 幂等补回（`:369-389`）→ `Remove()` 还原并 `Validate()+FixBounds()`（`:551-616`）。**`widthPixels/heightPixels` 全程不动。**

| 路线 | 得到什么 | 代价 |
| --- | --- | --- |
| 接管 getter（八例 §4） | **看更多格**，字不变 | 改原版算法：重入（`Drawing` 计数）、上下限三取最小、`Validate` 级联；两模组同做必冲突 |
| **本例：乘 localScale** | 格子整体变大变小（**不增格数**） | 字/贴图一起缩；拖影、弹层、扩容、边界、标题栏全要补 |

| Patch 靶点 | 类型 | 干什么 | 出处 |
| --- | --- | --- | --- |
| `PixelWindow.Validate` | Postfix | `RefreshControls()` 重排标题栏（Validate 每次都重置标题/关闭按钮位置） | `Mod.cs:118`、`Patches.cs:226-245` |
| `PixelWindow.FixBounds` | **Prefix→false** | 原版按未缩放尺寸夹边界 → 全接管 `ClampScaledBounds()` | `Mod.cs:120`、`Patches.cs:247-268` |
| `PixelWindow.Show`（重载遍历）/ `Hide` | Post / Pre | 打开时 `Attach` 挂组件；关窗前 `CaptureLayout` 落盘 | `Mod.cs:110-117/107` |
| `PixelWindow.Detach(PixelElement)` / `TreeNodeRender.SetParent` | Pre+Post | 元素被搬进 `tempRoot` 时记 localScale，落地按回 | `Mod.cs:104/99-103`、`RenderScale.cs:8-44` |
| `TreeNodeRender.OnDrag` / `OnEndDrag` | Pre+Post | 记拖动前后位移（>1e-3 才算玩家移动）；松手 `FixBounds` + 落盘 | `Mod.cs:108-109`、`Patches.cs:40-101` |
| `GameItemElement.ToggleFakeBackground` / `MovePosition` / `Get·SetCenteredPosition` | Post / **Pre→false** | 拖影出现即换基准缩放；跟手时按"鼠标下窗口"的百分比重取；居中走自己算的坐标 | `Mod.cs:121-128`、`ItemPreview.cs:21-203` |
| `ItemMouseDragHandler.OnEventReset` / `GameItemElement.ValidateSelectLocked` | Pre+Post / Post | 拖拽结束清状态；把"继承了窗口缩放"的物品根节点拉回 (1,1,1) | `Mod.cs:129-130`、`ItemLayoutRepair.cs:7-41` |
| `StorageExpansionHelper.AddTabs` | Pre+**Finalizer** | 扩容前后临时还原 100%（否则新标签按缩放后尺寸算位置） | `Mod.cs:105`、`ExpansionScale.cs:15-40` |
| `EmporiumEntry.Validate(bool)` | Pre+Post | 地面窗口（拾荒界面）位置另排 | `Mod.cs:119`、`Patches.cs:199-224` |
| `PlayerStore.SaveGame` | Prefix | `LayoutMemory.CaptureAll()` 全量落盘 | `Mod.cs:106`、`Patches.cs:9-19` |
| `EscapeUIManager.OpenEscapeMenu` / `StoreUIManager.ResolveEscape` / `OnMainMenu` / `OnExit` | Post / Pre | 插批量入口；Esc 先关自己的面板；离场清理 | `Mod.cs:95-98`、`BulkUI.cs:44-130` |

**缩放后内部网格怎么重排：不重排。** 格子是 `gridSize` 像素绝对值 + 绝对定位（`GameGridInventory.cs:162-163/478-490/520-526`），没有 `GridLayoutGroup` 可调，`localScale` 把整层视觉等比缩放。真正要"重排"的只有三处：
1. **标题栏**：`RefreshControls` 把标题从 `x=0` 推到 `x=手柄宽+6`，宽度缩到 `min(关闭按钮.x, 所有 headerButtons.x) - 2 - 手柄 - 6`，挤不下就整个关掉 `titleText`（`WindowScale.cs:452-489`）；
2. **拖影**：`PreviewBaseline = realWorldScale / overlayWorldScale × 100/percent`（`ScaleMath.cs:84-87`），`MovePosition` 后按"鼠标下的窗口"重取（`ItemPreview.cs:87-142`，`EventSystem.RaycastAll` 找 `WindowScale`）；
3. **地面面板**：改缩 `childElement.handler` 而非窗口根，按 `GroundOffset` 补位移（`ScaleMath.cs:98-102`、`WindowScale.cs:390-425`）。

**尺寸/位置存哪（每容器一份，进存档）**：

```csharp
// LayoutMemory.cs:204-210 —— 键 = window_layout_v1/<物品GetUniqueID()>/<identifier>
internal static void Put(string key, LayoutRecord record)
{
    if (Ready && key != null)
        ModHelper.SetModData("local.container.window.scale", key, JsonUtility.ToJson((object)record));
}
```
- 键生成 `WindowIdentity.Key`（`WindowIdentity.cs:42-74`）：家具三件套走 `window_layout_v1/fixture/trashcan|dossier|cassette_player`，拾荒地面走 `ground_layout_v1`，其余走 `<uniqueId>/<identifier>`；
- 记录体 `LayoutRecord{schema=1, hasPosition, hasSize, x, y, width, height, percent}`（`LayoutRecord.cs:5-23`），**x/y 是相对 canvas `pixelRect` 的归一化比例**（`LayoutMath.Capture:66-81`）→ 换分辨率不错位；读回先过 `ValidPosition`/`Finite`（`LayoutMemory.cs:186-193`）；
- 三层优先级：会话 `sessionSizes` → 存档记录 → 全局默认 `batch_size_v1`（`InitialSize:63-78`）；
- **不写 `PlayerPrefs`、不写物品 Tag**：`ModData` 挂 `PlayerStore` 跟存档走（见 `03-原版接口速查` §ModData 真相），代价是换档即换一套、物品转手留孤儿键。

### 2.3 关键代码摘录

```csharp
// WindowScale.cs:150-193（节选）：造手柄——借原版"可缩放按钮"的贴图，做成 16px MachineryButton
originalScale = ((Transform)window.rectTransform).localScale;
TreeNodeRender val = (TreeNodeRender)resizeField.GetValue(window);          // PixelWindow.bottomResizeButton（私有）
Sprite val2 = (Object)(object)val == (Object)null ? null : val.image.sprite; // 借它的图标
handle = (Object)(object)val2 != (Object)null
    ? new MachineryButton(val2, (Action)(() => { }), 16)
    : new MachineryButton("%", (Action)(() => { }), 16, 16);
((SelectableElement)handle).handler.SetParent(Window.handler);
((Object)((SelectableElement)handle).handler).name = "ContainerWindowScale_DragHandle";
TreeNodeRender val3 = (TreeNodeRender)plateField.GetValue(handle);           // MachineryButton.plate
((Component)val3).gameObject.AddComponent<DragHandle>().Owner = this;

// WindowScale.cs:319-354：改尺寸的全部逻辑就这几行，其余都是副作用补丁
float num = ScaleMath.ClampPercent(percent);            // :22-29 → Math.Max(50, Math.Min(150, p))
if (!(Math.Abs(num - Percent) < 0.001f))
{
    Percent = num;
    if (!ApplyNightScale()) ((Transform)rectTransform).localScale = originalScale * (Percent / 100f);
    LayoutMemory.RememberSessionSize(LayoutKey, Percent);
    RefreshControls();
    Window.FixBounds();                                  // 已被 Prefix 换成 ClampScaledBounds
}

// WindowScale.cs:427-433 + 492-495：Validate 的 Postfix 里改 UI 必须有重入闩
if (!initialized || removing || refreshing || Window == null || ...) return;
refreshing = true;
try { /* 改标题、改手柄位置、SetAsLastSibling */ } finally { refreshing = false; }

// ScaleMath.cs:64-82：拖动 → 百分比（投影到对角线，天然平滑）+ 标题栏抓取宽度按缩放折算
double num  = (double)width * width + (double)height * height;
double num2 = 1.0 + (dx * width + dy * height) / num;
return ClampPercent((float)(startPercent * num2));
public static float TitleGrip(float screenWidth, float logicalWidth)
    => logicalWidth > 0f ? Math.Max(16f, 32f * Math.Abs(screenWidth / logicalWidth)) : 32f;
```

### 2.4 踩坑与红线

1. **缩放整窗，字也一起缩**：50% 时 18px 标题变 9px。玩家能接受是因为"我要看得更清/更小"，但**任何依赖像素读数的功能都会被一起缩**——`TabletUI.MeasureWidth`、飞字、`RectTransformUtility` 射线都要复核。
2. **`Validate` 是重排总闸**：所有改造都挂它的 Postfix 且自带 `refreshing` 重入闩，否则 `RefreshControls → FixBounds → Validate → RefreshControls` 递归。
3. **`tempRoot`/`overlayRoot` 会重置 `localScale`**：不补 `RenderScale`，拖动/弹出物品时影子大小会跳一下。
4. **扩容必须在未缩放状态下跑**：`ExpansionScale` 用 Pre 还原 `UnscaledRoot`、用 **Finalizer** 无条件还原（`:36-40`）——Finalizer 而非 Postfix，因为要保证原版抛异常时也不留下 100% 状态的窗口。
5. **跨模组兼容是硬编码字符串**：`NightRunCompat` 靠 `MachineryPanel.role == "nightrun_left"/"nightrun_right"/"nightrun_armor"` 认那个模组的窗口，认出来改缩 `childElement` 而非窗口根（`NightRunCompat.cs:42-70`）。**对方一改 role 名就静默失效**，照抄等于把第三方字符串写进自己代码。
6. **`FixBounds` 被整体接管（Prefix→false）**：这是"改语义"级别，原版实现（`PixelWindow.cs:534-554`）不再走。两模组同时接管必冲突，装前跑冲突扫描。
7. **它接管了全局拖拽标志**：`BeginResize` 里 `UIDragHandler.isDraggingAnyUI = true; draggingID = "…resize"`（`:242-243`），`StopResize` 判 `draggingID` 是自己才还原（`:356-368`）——**任何全局输入/拖拽标志都必须这样成对还原**，否则一次异常就永久卡住拖拽。
8. **死代码**：`ScreenWorkspace.GetAreas()` 恒 `null`（`ScreenWorkspace.cs:9-12`），`ScaleMath.FitVisible` 的双屏拼接（`:104-145`）永远跑不到。
9. **0 ModHook 的代价它自己补上了**：`LayoutMemory.Start/Stop` 挂 `OnGameLoadedInit`（清空）/`OnGameLoadedLate`（`Ready=true`）/`OnModConfigApplied`（重放），`Stop` 反向退订（`:21-43`、`:143-172`）——**正是我们硬规范第 3 条要的那一套**，比 BetterCashier 的 0 ModHook 稳。

### 2.5 对我们的可用点

1. **"要更大"和"要更多格"是两件事**：想看更多格子 → 接管 `widthPixels`（八例范式 3）；想整体变大变小 → 抄本例 `localScale`（`Change()` 就 4 行）。我们《储物扩容》《柜台上货》多数属前者，"字太小看不清"属后者。
2. **窗口几何记忆直接抄这一套**：`ModData` + `window_layout_v1/<uniqueId>/<identifier>` + **归一化比例** + `hasPosition/hasSize` 双标志 + `schema` 版本 + 读回校验 + `OnGameLoadedLate` 才 Ready。比 `PlayerPrefs`（不跟存档）和物品 Tag（污染物品数据）都稳。
3. **副作用清单要提前算**：本例真正改尺寸 4 行、擦副作用 21 处。我们做任何 `localScale` 级改动，先照 2.2 的表逐条问"这层会不会被重置/被重复缩放"。
4. **Esc 菜单插入口值得记**：`PauseEntry.Install` 不写死节点名，而是**找 `onClick` 里持久化方法名为 `OnOption` 的按钮**，找不到再退回"名字含 option / 文本是 选项|Options"（`PauseEntry.cs:28-36`），然后 `Instantiate` 一份插在后面、后续按钮整体下移一行、撑高外框（`:40-88`）——插任何菜单项都能用。
5. **`DragHandle` 范式**：`MonoBehaviour` 同时实现 `IPointerDown/Up + IInitializePotentialDrag + IBeginDrag/IDrag/IEndDrag`，其中 `PotentialDrag` 里 `e.useDragThreshold = false`（`WindowScale.cs:196-203`）——想要"按下即跟手"就这一行。

---

## 三、`3835147893` 商人货物翻页（Merchant Stock Pager）

> `3835147893` · 9.8 万订阅 · 17 文件 / 862 行 · v0.2.1 · `MerchantStockPager.dll` · 自闭合 `<Capabilities/>` · 11 个 `[HarmonyPatch]` / 0 Transpiler / 2 ModHook
> 源码 `缓存文件夹\decomp_ws\3835147893\`（入口 `MerchantStockPager\Mod.cs`，核心 `Stock.cs` 395 行）
> 为什么值得读：它把"原版摆不下就销毁"这个**默认行为**完整接管了——不是修 UI，而是**用 UI 换物品的生死**。11 个补丁里只有 1 个跟 UI 有关，其余全是"别让客人走 / 别让货被销毁 / 别让系统以为空了"。

### 3.1 规模与结构

| 文件 | 行 | 职责 |
| --- | --- | --- |
| `Stock.cs` | **395** | 全部状态：`Pages`（每页一组物品）、`Index`、`Pending`、`Generating`、翻页主流程 `Cycle`、分页编排 `PreparePages`、落位 `PlaceOnFront`、支持名单 |
| `PagePlanner.cs` | 99 | 把一堆货装进固定 18×4 网格：2 翻 × 4 旋 × 全格扫描 + 相交检测，算出每件货的 `GridShape` 与页数 |
| `Selector.cs` + `PagerRowLayout.cs` | 65 + 33 | 唯一的 UI 补丁：`Rebuild` 后往平板尾部插一行翻页条；`ILayoutSelfController` 把该行锚点拉成整行 |
| `PreserveOverflow.cs` | 52 | **最关键补丁**：拦下"摆不下就销毁" |
| `Mod.cs` + `Text.cs` + 9 个薄补丁 + 元数据 | 218 | 入口（2 个 ModHook）、本地化、9 个 12–27 行的薄补丁（"别让系统以为客人没货了""别让客人走"）、元数据 |

**结构特征**：单一静态状态类 `Stock` + 11 个"一补丁一文件"的薄壳，所有外部可见状态都从 `Stock` 派生（`HasMore`/`HiddenCount`/`UiSignature`），补丁本身不含逻辑——**这个分工值得抄**：补丁只转发，状态与算法集中在一处。

### 3.2 它怎么做：原版没有滚动，摆不下就**销毁**

| 原版事实 | 行 | 后果 |
| --- | --- | --- |
| 柜台是固定网格 `new GameGridInventory(18, 4)`；商人上货逐件走 `PlayerStore.TryAddDirectSellingItemToTable` | `EmporiumEntry.cs:212`、`PlayerStore.cs:2545-2572` | 前台 18×4 格，**没有滚动条**（滚动版是 `GameGridScrollableInventory`，柜台不用）；一件一件往前台塞 |
| 塞不下走 `GraphUtils.TryAcceptAllOrDestroy(front, item, "front counter full")` | `PlayerStore.cs:2567` | **= `TryAcceptAll` + `DestroyIfUnplaced`（`GraphUtils.cs:96-104`）→ 多出来的货直接销毁**，无提示无回收 |
| `IsCurrentClientStillHaveThingToSell` 只数前台/后台上的东西 | `StoreClientInstance.cs:76-95` | 柜台卖空 → SELL 意图的客人判定"没事了" → 走人，被销毁的货永久消失 |

这就是 `03-原版接口速查` §1095「`EmporiumTryAdd` 六级兜底，全满就销毁且无提示」的同一处坑，本例是它的完整补丁。
**翻页不是新建 UI，而是"把货搬进搬出"**：分页状态就是 `List<List<GameItem>> Pages` + `int Index`（`Stock.cs:14/24`），每页 = 要一起摆在那块 18×4 柜台上的物品集合。`Cycle(direction)`（`:249-394`）把当前页逐个 `parentInventory.Expel(item)` 卸下 → 把目标页逐个 `PlaceOnFront` 放上 → 重算 `Index` → `RequestRefresh()`。
落位不是随机塞：`PagePlanner.Group` 先算好每件货的 `GridShape`，再 `TryInventorySlot(item, shape, null)` 精确落位，失败才退回 `TryAcceptAll`（`Stock.cs:236-247`）；页数稳定靠 `PagePlanner` 拿"柜台上已有的其他物品 shape"当底（`list3[0]`，`PagePlanner.cs:11-27`），所以同一份货每次算出来页数一样。

| Patch 靶点 | 类型 | 干什么 | 出处 |
| --- | --- | --- | --- |
| **`GraphUtils.TryAcceptAllOrDestroy`** | Prefix→false | 只拦"自己刚生成的那件"且目标为前台：改走 `TryAcceptAll`（不销毁）；放不下的进 `Pending`，返回 `false` | `PreserveOverflow.cs:9-51` |
| `PlayerStore.TryAddDirectSellingItemToTable(GameItem,…)` / `(string,…)` | Pre+**Finalizer** / Prefix→false | 记下"这件是我生成的"（`__state` 存旧值、Finalizer 还原）；`string` 重载显式转 `GameItem` 重载，让认领也生效 | `ItemOffer.cs:16-26`、`StringOffer.cs:16-24` |
| `EmporiumEntry.GetAllNonOwendItemCount` / `IsFrontCounterEmptyNonOwned` / `StoreClientInstance.IsCurrentClientStillHaveThingToSell` | Postfix | `+ HiddenCount`；藏着货时不许报"前台空"；柜台空了但 `Pages`/`Pending` 还有货 → 仍算有货 | `CountReserve.cs:8-14`、`ReserveIsNotEmpty.cs:8-14`、`RemainingStock.cs:10-16` |
| `StoreClient.CompleteBusiness` / `PlayerStore.RemoveNotOwnedItemFromTable` | **Prefix→false** / Prefix | 还有藏着货就不许结束本次交易；客人真走了则 `Stock.Reset()` 把藏着的货 `Destroy()` 释放 | `KeepVisit.cs:8-19`、`Leave.cs:8-12` |
| `NegociationUIManager.OnWholesaleClick` / `BuildSignature` | Pre+Post / Post | 批发前后重算页、批完还有剩货就保持 `isOfferWholesale` / 把页码拼进刷新签名 | `Wholesale.cs:8-23`、`InventorySignature.cs:8-15` |

**翻页条怎么造（复用原版控件，但不是原版滚动条）**：`TabletUI.NewRect(content,"MerchantStockPager",0,0,0,36)` + `LayoutElement`（36 高、`flexibleWidth=1`）+ `PagerRowLayout`；左右箭头直接用平板皮肤 `((TabletSkin)frame).prevNormal/prevHover/nextNormal/nextHover` 配 `TabletUI.ArrowButton`；中间标签 `TabletUI.NewText(..., TabletStyle.INK)`，文字 `browse_stock · {页码}/{页数}`，再 `TabletUI.AutoSize(label, 10f)`（`Selector.cs:16-45`）。

### 3.3 关键代码摘录

```csharp
// PreserveOverflow.cs:11-38（节选）—— 整个模组的命门：把"销毁"改成"扣在手里"
if (Stock.Generating != item || !Stock.Target() || (object)currentNode != EmporiumEntry.Instance.frontInvinvElement)
    return true;                                    // 不是我自己生成的货 → 不介入
Stock.Planned = false;
GraphUtils.TryAcceptAll(currentNode, item, -1);     // 注意：不是 …OrDestroy
foreach (GameItem item2 in ((GameInventory)EmporiumEntry.Instance.frontInvinvElement).childItems.Where(Stock.Live))
    if (!Stock.Pages[Stock.Index].Contains(item2)) Stock.Pages[Stock.Index].Add(item2);
if (item.parentInventory == null && item.unitCount > 0) {    // 还是没地方放 → 扣住，不销毁
    if (!Stock.Pending.Contains(item)) { Stock.Pending.Add(item); Stock.DeferredFirstDisplay.Add(item); Stock.RequestRefresh(); }
    __result = false;                               // 原版以为"加失败"，其实我们收下了
}
else { __result = true; }  return false;              // 摆下了：并进当前页；两条路都 return false（原版逻辑已被替换）
```

```csharp
// Stock.cs:282-323（节选）—— 翻页 = 卸旧页 + 装新页，每步复查"客人还在不在"
GameInventory[] array2 = array.Select((GameItem i) => i.parentInventory).ToArray();  // 记原容器，回滚用
int index2 = (Index + ((direction >= 0) ? 1 : (-1)) + Pages.Count) % Pages.Count;
Switching = true;
try
{
    e.DeselectDragItem(true);
    foreach (GameItem val in array) {                      // ① 卸下当前页
        val.parentInventory?.Expel(val);
        if (SceneOwner != e || Owner != owner || val.parentInventory != null) throw new InvalidOperationException("Visit/scene changed or goods stuck on the previous page");
    }
    foreach (GameItem val2 in Pages[index2].Where(Live))   // ② 装上目标页
        if (val2.parentInventory == null) {
            PlaceOnFront(e.frontInvinvElement, val2);
            if (val2.parentInventory != null && DeferredFirstDisplay.Remove(val2))
                StartingPerkContent.HandleGamblingAddictionItemAdded(val2);   // 原版"货上柜"的副作用要补
        }
    Index = index2;
}
catch (Exception ex) { /* ③ 逐件尽力回滚：新页卸下、旧页按 array2 放回，每件单独 try */ }
finally { Switching = false; RequestRefresh(); }
```
所有对外口径都从一处派生（`Stock.cs:54-57`）：`HiddenCount` = `Pages`+`Pending` 中 `Live(i) && i.parentInventory == null` 的去重计数；`UiSignature` = `"|stockpager:" + Index + ":" + Pages.Count + ":" + Pending.Count + ":" + HiddenCount + ":" + Notice`。

### 3.4 踩坑与红线

1. **物品引用靠"物品还活着但没有父容器"保持**（`parentInventory == null`），所有遍历走 `Stock.Live(item)`（`unitCount > 0` 且未被玩家拥有 且 不是已销毁的 `GameItemElement`，`Stock.cs:161-172`）——**绝不缓存 `Transform`/`GameItemElement`**，每次翻页都会 `Expel`/`TryInventorySlot` 重建渲染节点。而回滚只能"尽力"、不是事务：catch 里逐件 `try`、失败只 `Warn`（`:340-381`），会留下"两页货同时在柜台上"的中间态，靠 `Notice = "switch_failed"` 提示玩家。
2. **`Stock.Reset()` 会 `item.Destroy()`**（`:148`）：清缓存 = 销毁物品。本例成立（这些货本就是模组从"将被销毁"里扣下的），**别的场景照抄会直接吞玩家的货**。
3. **支持名单硬编码 20 多个 client identifier + `thiefGeneric*` 前缀**（`:26-77`），其中 `retired_*` 是**反选**（那 8 个"退休"商人不支持）；新商人模组一律不生效。
4. **`Generating` 这个认领标记必须 Pre + Finalizer 成对维护，且两个重载都盖到**（`ItemOffer.cs` / `StringOffer.cs`）：用 Postfix 会在原版抛异常时留下脏标记，之后所有货被误认领；`string` 重载内部会 `DirectoryMaster.Item(identifier)` 再转 `GameItem` 重载（`03-原版接口速查` §1127），所以必须显式转调，否则认领漏一半。
5. **`Rebuild` 每次都新建翻页条节点**，靠原版清 `content` 兜底（`NegociationUIManager.cs:688-735`）；若别的模组改了这个清理顺序，翻页条会重复叠加。
6. `ModHook.OnGameLoadedInit` 只清缓存（`Stock.ForgetScene`），**没有 `OnGameLoadedNormal` 成对重置**，靠 `Target()` 里 `SceneOwner/Owner` 比对自愈（`Stock.cs:96-106`）——能跑但不规范，照抄要补齐。

### 3.5 对我们的可用点

1. **"原版摆不下就销毁"必须有意识接管**：任何"一次发 N 件物品"的功能（活动奖励、罚没、进货），先 `GraphUtils.CanAccept` + `TryAccept` 校验落点，**绝不指望原版兜底**——`TryAcceptAllOrDestroy` 就在 `PlayerStore` 常规路径上。
2. **"扣下一批对象，用 UI 换页"可复用到别的容量问题**：仓储临时区、邮寄箱、待检队列都能照"`List<List<T>> Pages` + `Index` + 逐件 Expel/TrySlot + Live 过滤"做，UI 只要两个箭头 + 一个页码标签。
3. **补丁只转发、状态集中在一个静态类**：除 `PreserveOverflow`（52 行）与 `Selector`（65 行）外，另 9 个补丁文件共 170 行、基本每个只有一行转发调用。跨系统功能照这个分工，比把逻辑散在补丁里好维护。
4. **`BuildSignature` Postfix 是"让原版按你的状态重建 UI"的通用开关**（把页码拼进签名，让原版 0.2 s 的刷新节拍替你干活）；配 `PagerRowLayout`（33 行，"往 `VerticalLayoutGroup` 里插一行绝对坐标控件"的通用件：`anchorMin/Max` 拉成整行、`sizeDelta.x = 0`、`pivot=(0,1)`）就能往任何原版列表里插自定义行。

---

## 四、`3828924760` 更好的交易显示（EnhancedTradeDisplay）

> `3828924760` · 4 万订阅 · 22 文件 / 1,583 行 · v1.0 · `EnhancedTradeDisplay.dll`（程序集名 ≠ 文件名）· 自闭合 `<Capabilities/>` · 17 个 `[HarmonyPatch]` / 0 Transpiler / 0 ModHook
> 源码 `缓存文件夹\decomp_ws\3828924760\`（入口 `Better_trading\BetterTradingMod.cs` 619 行）
> 为什么值得读：**"只往一行字里加信息"的极简派**——UI 一行没重排，全是给原版现成的字符串补内容；`GetNegociatedValue` 的调用量问题与 BetterCashier 同源，只是被一个括号 bug 意外救了。

### 4.1 规模与结构

| 文件 | 行 | 职责 |
| --- | --- | --- |
| `BetterTradingMod.cs` | **619** | 全部取值逻辑：利润/成本/市场价/易物汇总/意图判定 + 2 个 `ConfigEntry` + 配色 |
| `BargainHUD.cs` | 360 | 议价工具提示追加声望得失（含**从原版文本里反解数值**） |
| `DayStats.cs` | 280 | 当日收益统计（`playerCash` 差值 + 嵌套深度）、写夜间报告 |
| `LogOnce.cs` + 17 个补丁 + 元数据 | 324 | 同消息只打一次（`HashSet`，满 100 清空）；17 个补丁 = 11 个"统计边界"各 17 行 + 6 个悬停/提示/利润行 12–20 行 |

**结构特征**：0 ModHook（读档不重置）、全 static、所有价格读取都 `try/catch` 空吞。`LogOnce`（同消息只打一次、上限 100 自动清空）是可留的小工具。

### 4.2 它怎么做

**"交易显示"指哪个界面？** 是**议价平板**（`NegociationUIManager` 的 `negotiation_tablet`），不是收银台。主改点是一个参数：

```csharp
// NegociationUIManager.cs:1029-1056（节选）—— 行右侧那个字符串就是全部可乘之机
private void BuildItemRow(GameItem item, string refusal, string right)
{
    string text = displayName + "\n" + right + "\n" + (flag ? "r" : "-") + ...;   // right 是缓存键的一部分
    if (cachedRows...value.key == text) { ...; return; }                      // 改 right 就等于每行都换键 → 每行重建
    TabletListRow row = TabletUI.ListRow(frame, frame.content, displayName, right, flag, func, () => OnRowClick(item));
}
```
补丁只有一个 Prefix 改 `ref string right`（`P_TradeItemProfit.cs:8-11`）。**没有重排、没有加节点、没有改 ListRow。**

| 多显示什么 | 落点 | 数据来源 |
| --- | --- | --- |
| 逐件利润 `赚 +123€` / `预计赚 +123€`；易物三行汇总（合计进货 / 标签价预计赚 / 市场价预计赚） | 行右侧追加 `right`；易物时在首件货的 `right` 里塞 `\n` 多行 | 卖方向 `BuyPrice`→`OfferValue`、买方向 `GetFinalOfferValue`→`RetailValue`；`ui.GetSellableItem()` × `PlayerStore.FindAllItem(false)` |
| 议价档位声望预览 / 夜间报告两行 | tooltip 尾部（`<size=90%>` 小字）/ `PlayerStore.AddNightLog(text, "B26A00")` | **解析 repLine 文本**（0.46D 上失效，见 4.4）/ `playerCash` 差值 + `BOUGHT_PRICE_TAG` |

| 想看什么 | 原版入口（可直接抄的一张表） | 出处 |
| --- | --- | --- |
| 我的买入成本 | `item.GetTagReadonly("BOUGHT_PRICE_TAG").GetInt()` → `unitBaseValue` → `lateUnitValue` 三级兜底 | `BetterTradingMod.cs:265-309` |
| 当前议价 / 零售价 / 标签价 | `GetNegociatedValue()` / `GetCurrentValue(true,true,true,false,0L)` / `GetBaseValue()` | `:311-365` |
| 本单成交价 / 客人要不要这件 | `ui.GetFinalOfferValue(item)` → `OfferValue` → `BuyPrice` / `client.IsClientBuyingThisItem(item)` → `ui.GetNotBuyingReason(item)` | `:490-519`、`:422-449` |
| 本单物品清单 / 玩家是卖还是买 / 是否玩家拥有 | `ui.GetSellableItem()`、`PlayerStore.FindAllItem(false)` / `client.IsClientBuying()/IsClientSelling()` → 退回 `ClientIntent` 整数 / `GeneralHelper.IsItemEffectivelyOwned(item)` | `:214-254`、`:451-488`、`:367-381` |

**没有 `ItemFeature`、没有 `realCondition`**：全档只读"标签价 / 买价 / 议价 / 市场价"四个玩家可见数——符合我们"排列展示绝不用真伪"的红线。

**统计怎么算**（`DayStats`）：不认卖/买语义，只认 `playerCash` 增量符号。`Enter()` 记 `_cashBefore` 与卖出成本快照，`Exit()` 差值 >0 记销售额、<0 记进货支出；`_depth` + `_depthFrame` 处理 `SellMultiple → SellItem` 嵌套，`_skipTrade` 排除易物（`DayStats.cs:37-117`）。11 个薄补丁就是给这 11 个改钱入口包边界（`SellItem/BuyItem/SellFromTable/BuyFromTable/AcceptBarter/SellMultiple/BuyMultipleItem/OnOfferAcceptClick/OnWholesaleClick/OnConfirmPanelYes/OnYesClick`）。

### 4.3 关键代码摘录

```csharp
// P_TradeItemProfit.cs:6-11 + BetterTradingMod.cs:120-167（节选）—— 全部 UI 改动就是改一个参数
[HarmonyPatch(typeof(NegociationUIManager), "BuildItemRow")]
internal static class P_TradeItemProfit
{
    private static void Prefix(GameItem item, string refusal, ref string right)
        => BetterTradingMod.EnrichItemRow(item, ref right);
}

internal static void EnrichItemRow(GameItem item, ref string right)
{
    ClientIntent currentClientIntent = instance.GetCurrentClientIntent();
    if ((int)currentClientIntent == 6) { AppendBarterSummary(item, instance2, instance, ref right); return; }  // 易物
    if ((int)currentClientIntent == 0) return;                                                               // 信息商人不算
    bool flag = DeterminePlayerSells(instance, instance2, currentClientIntent);   // true = 玩家卖货
    if (BelongsToTradeSide(item, flag)                                           // IsOwnedByPlayer(item) == flag
        && (!flag || ClientWantsItem(instance2, storeClient, item)))             // 买方向还要过"客人要不要"
    {
        GetCostValue(instance2, item, flag, out var cost, out var value);
        right = right + (flag ? " 赚" : " 预计赚") + ColoredSigned(value - cost);
    }
}

// BetterTradingMod.cs:600-613 —— 配色（注意：正数红、负数绿，与直觉相反）
return "<color=" + (value > 0 ? "#FF5252" : (value < 0 ? "#00E676" : "#FFFFFF")) + ">" + Signed(value) + "€" + "</color>";

// BargainHUD.cs:223-262（节选）—— 反解原版文本，本档最脆的一段
string value = (((int)type == 2 || (int)type == 3) ? "购买" : "出售");
int num  = reputationText.IndexOf(value);         // 找中文标签
int num2 = reputationText.IndexOf('(', num);      // ← 原版输出的是 '['，0.46D 上必为 -1
```
对照原版格式：`BuildRepGainLine` 返回 `$"<color={text}>{label}: x{mult:F2} [{repPer1000:F2}]</color>"`（`BargainUIManager.cs:620-628`），数值在**方括号**里。

### 4.4 踩坑与红线

1. **`GetNegociatedValue` 调用量放大（与 BetterCashier 同一个坑）**：每行至少 2 次价格读取；`right` 参与行缓存键（`NegociationUIManager.cs:1040-1041`），**我们改 `right` 就等于每行都换键 → 每行都重建 → 每次刷新重算所有价格**。`AppendBarterSummary` 更狠：本单货 + 客人全部货各扫一遍（`BetterTradingMod.cs:169-212`）。
2. **真正危险的是每帧钩子**：`BargainHUD.Render()` 挂在 `TooltipManager.Update` 的 **Postfix（每帧）**（`P_BargainTooltipUpdate.cs:6-19`），一旦走通 `RepLine → DealValue` 就对本单每件物品调一次 `GetNegociatedValue`（`BargainHUD.cs:282-320`）。**它没炸，纯粹因为 4.3 那个 `(` 让链路提前 return**——"能早退就早退"救了它一命；而这也说明**靠解析原版 UI 文本反推数值必然后悔**：原文连括号都从 `()` 改成 `[]`，整条声望提示静默消失，不报错不警告，玩家只觉得"这功能没有"。
3. **配色反了**：`ColoredSigned` 正数红/负数绿（`:600-613`），夜间报告 `ProfitHex` 同样 `>=0 红 / <0 绿`（`DayStats.cs:162-169`）。推测作者按"支出红/收入绿"的账本口径写，但利润属收入侧，读起来是反的。**照抄必须改成绿赚红亏。**
4. **往 `right` 里塞富文本和 `\n`**：`<color>` 会被 `TabletUI.ListRow` 一起量宽（挤名字），`\n` 不会撑高行（`BuildItemRow` 只当一段文本喂进去）；barter 分支三行汇总大概率溢出（推测：作者期望行自增高）。
5. **统计依赖"11 个入口全包住"**：漏一个就漏一笔且不报错。`DayStats` 纯 static、**0 ModHook → 读档不重置**，只靠 `StoreStation.GetDayCounter()` 变号自愈（`:243-251`）。照抄必须补 `OnGameLoadedNormal`。
6. **价格读取全部 `try/catch { }` 空吞**（`:271-308` 等 6 处）：坏了一律返回 0，玩家看到"赚 0€"，日志什么都没有——我们规范明确禁止，边界捕获要 `LogError` 留堆栈。另：`GetStoreClient` 会退回 `lastVisitedClient`（`:404-415`）→ 客人刚走时还在算"上一个客人的利润"，数字张冠李戴。

### 4.5 对我们的可用点

1. **"只加信息不改布局"的最省力入口就是 `BuildItemRow` 的 `right`**（Prefix 改 `ref` 参数，12 行）；我们要在议价行加"刻录成本 / 翻新成本 / 罚没折价"，照这个 Prefix 抄即可，不碰任何节点。**但必须自带缓存**：`BuildSignature` 变化时才批量重算、结果存 `Dictionary<GameItem, long>`、Prefix 只查表——既避免 4.4 第 1 条的放大，也避免富文本进缓存键导致重复重建。
2. **4.2 那张"想看什么 → 原版入口"表是我们最缺的一张速查**：`BOUGHT_PRICE_TAG`/`GetNegociatedValue`/`GetCurrentValue(5 参)`/`GetBaseValue`/`GetFinalOfferValue`/`GetNotBuyingReason`/`IsItemEffectivelyOwned`，全带行号，可直接复用。
3. **夜间报告插入口 + 日志纪律**：`StoreEventManager.OnDayStart` Postfix + `PlayerStore.AddNightLog(text, hex)`（`PlayerStore.cs:4133-4146`，会自动补 `#`）是我们所有"当日结算/台账"功能最便宜的挂载点；`LogOnce`（每帧/每行路径上的 Warn 不能刷屏，但也不能静默）值得抄进通用工具。

---

## 五、四例合并：五条可用点 + 一句话回答

1. **移 UI 稳不稳，只看"谁是写点"**：`grep "anchoredPosition ="` 找写点 → 看是否在 `LateUpdate`/`OnDrag` 里被反复调 → 看有没有 `baseLocalOffset` 这类**基准复算**结构。有就"Postfix 改坐标 + 一起改基准"（①），没有就一个 Postfix 够。
2. **"要更大"和"要更多格"是两件事**：看更多格 = 接管 `widthPixels` 上报（八例范式 3，25 补丁、必冲突）；整体缩放 = ②的 `localScale` 乘法（4 行核心 + 21 处副作用补丁，字会一起缩）。
3. **窗口几何记忆照抄 ②**：`ModData` + `window_layout_v1/<uniqueId>/<identifier>` + **归一化比例** + `hasPosition/hasSize` 双标志 + `schema` 版本 + 读回校验 + `OnGameLoadedLate` 才 Ready。比 `PlayerPrefs`（不跟存档）和物品 Tag（污染物品）都稳。
4. **"原版摆不下就销毁"必须主动接管**：`TryAcceptAllOrDestroy → DestroyIfUnplaced` 就在 `PlayerStore` 常规路径上（③）。凡是"一次发 N 件物品"，先 `CanAccept`/`TryAccept` 校验落点，别指望原版兜底。
5. **加"多显示"就必须自带缓存**：④与 BetterCashier 都证明 `GetNegociatedValue` 极易翻 2~3 倍（尤其挂到每帧钩子上）。做法：`BuildSignature` 变化时才重算，结果进 `Dictionary<GameItem,long>`，Prefix 只查表。

**一句话回答「移 UI 位置怎么不被原版重置」**

> 找到原版**每次写回位置的那个方法**（这里是 `BargainUIManager.DockToTablet`，由 `OpenUI` 每次打开调一次）并在它的 **Postfix 里立刻改坐标**，同时把 `ZoomFollower.LateUpdate` **每帧复算用的基准量 `baseLocalOffset.x` 一起翻转**——只改一次 `anchoredPosition` 不够，因为下一帧就有人拿基准重算世界坐标把它拉回右侧。
