# 3829415336 · Show Item Names（物品名称常显）—— 反编译蒸馏档案

## 1. 档案头

> 源码：`缓存文件夹\decomp_ws\3829415336`（20 个 `.cs`，5,818 行；`2.5.1`，manifest 空 `<Capabilities/>`，10.8 万订阅；`GameVersions 0.46D`）
> 分区：`ShowItemNames\` 平铺 19 文件 + `Properties\AssemblyInfo.cs`；入口 `ModEntry : IMod`（`ModEntry.cs:4`），运行时 `ModRuntime : MonoBehaviour`（`ModRuntime.cs:3`，`[DefaultExecutionOrder(1000)]`）
> 为什么值得读：**"一个简单需求为什么能写 5,818 行"的最佳样本** —— 真正的"画名字"只有约 200 行，其余全是与既有 UI 打架（拖动层、选中态、别的模组的标签、缺字形、布局适配）＋两套自绘设置界面；而且它**证明了一条我们需要的正解**：给物品加常显文字应该挂 `TreeNodeRender.InstantiateText()`（TMP）到 `element.handler` 下，**既不 `OnGUI`、也不碰 `GameItem.GetDisplayName`**。

## 2. 规模与结构

| 文件 | 行数 | 职责 |
| --- | --- | --- |
| `LabelOverlay.cs` | 2,752 | 全部渲染逻辑：Entry 缓存、建节点/建 TMP、定位适配、字体解析、拖动/锁定/页码角标、每帧 `SyncVisibility` |
| `SettingsWindow.cs` | 808 | F10 自绘设置窗（`CustomUIBuilder`）：窗口尺寸自适应、热键捕获、色板、滑块、清名按钮 |
| `StylePanel.cs` | 696 | 蓝标签打印界面里挂的「单件物品外观」面板 + **手绘 HSV 色轮**（`HueTexture`/`TriangleTexture`，`StylePanel.cs:612-660`） |
| `ItemStyle.cs` | 554 | 单件物品样式：读 `SNDS_POS/TEXT/PLATE/FONT/SIZE/ALPHA` 六个 tag → `Resolved`，附 `PlayerPrefs "snd.styles2"` 记忆与**旧字体值迁移** |
| `Settings.cs` | 322 | 11 个 `PlayerPrefs` 键、2 组 8 色预设、`StyleSignature` 哈希、`Changed` 事件、逐项 Setter |
| `Loc.cs` | 138 | 58 条中英兜底文案表 + `ModHelper.GetLocalized` |
| `BadgeIcons.cs` | 125 | 锁/页码角标图标（`Resources` 载入 `SND_Lock`/`SND_Page`，缺图降级为文字） |
| `Api.cs` | 73 | 对外 API：`QS_LOCK`/`QS_COLOR`/`QS_PAGE_BADGE`/`SNDS_FONT` 协定 tag、`ResolveFont`、`ClearNamesRequested` 事件 |
| `Compat.cs` | 72 | 反射找 `ItemDetails.ItemNameLabel+ItemNameTicker.Update`，手动 Harmony 打 Prefix 直接 `return false` |
| `ModEntry.cs` | 54 | 入口：`Settings.Load()`、挂 `ModRuntime` |
| `LocalNameClear.cs` | 50 | 遍历 `PlayerStore.FindAllItem(false)` 清空全库自定义名 |
| `ModRuntime.cs` | 34 | 每帧驱动：`Compat.Install`/`Tick`/1s `ReapplyAll`/热键/F10/Esc |
| 6 个 patch 文件 | 119 | 见 §3.5 |
| `QsTags.cs` / `AssemblyInfo` | 8 / 13 | — |

**"5,818 行的分布"（近似、有重叠）**：画名字本体 ≈ 200 行（9%）｜排版/自适应 ≈ 400 行（9%）｜字体与 CJK 兜底、渲染自愈 ≈ 640 行（28% of `LabelOverlay`）｜拖动/锁定/页码角标 ≈ 1,160 行（21%）｜两套设置 UI＋文案 ≈ 1,960 行（34%）｜单件样式持久化 554 行（10%）。**UI 装饰性的代码（设置窗 + 自绘色轮 + 角标）占了一半以上。**

## 3. 它怎么做这件事

### 2.1 名字怎么显示的：`handler` 下的一个 TMP 子节点

1. **节点**：`CreateRoot(element.handler,"SND_Label")` = `TreeNodeRender.Instantiate().SetParent(element.handler)`，`raycastTarget=false`、`SetAsLastSibling()`（`LabelOverlay.cs:442-475`）。
2. **文字**：`CreateText` = `TreeNodeRender.InstantiateText()`（原版就有的 TMP 节点工厂，`TreeNodeRender.cs:55`），字号 7、`enableWordWrapping=false`、四边贴满父节点、`raycastTarget=false`（`LabelOverlay.cs:502-544`）。**不用 `Image`+位图字体，不用 Tooltip，不用 OnGUI**（全库 `OnGUI` 命中 0）。
3. **定位**：`TryGetBounds()` 用 `background.rectTransform.GetWorldCorners()` 四角，经 `handler.InverseTransformPoint` 换算成父节点局部矩形（`LabelOverlay.cs:249-333`）—— 与机器角标那份用同一套"世界角点反算"手法。
4. **底板**：`SoftPlate()` 生成一张 1px 圆角柔边 sprite 垫在文字后面（`LabelOverlay.cs:1362-1389`），alpha 由 `snd.plateAlpha` 控制。
5. **触发**：`GameItemElement.ValidateSelect` 的 Postfix → `LabelOverlay.Refresh(this)`（`ValidateSelectPatch.cs:6-16`）；改/删自定义名后 → `ForceRefresh` → `item.Validate()`（`SetCustomNamePatch.cs:5`、`LabelOverlay.cs:1518-1533`）；另有每帧 `Tick()` 收尾。

### 2.2 名字从哪来：**`GeneralHelper.GetCustomName`，不是 `GetDisplayName`**

```csharp
if (Settings.Enabled && GeneralHelper.HasCustomName(element))   // LabelOverlay.cs:212
    text = GeneralHelper.GetCustomName(element);                 // LabelOverlay.cs:214
if (string.IsNullOrEmpty(text)) { Remove(element); return; }     // 没打名字 → 不画
```

- 真值落在 tag **`CUSTOM_NAME_TAG`**（`GeneralHelper.cs:385-397`），也就是**蓝标签打印机**（`LabelerBlueUIManager`）打上去的名字。
- 原版把"自定义名优先"收在 `GameItem.GetDisplayName(bool preferCustomName)` 里（`GameItem.cs:652`），**本模组完全绕开了它**：全库 `GetDisplayName` **0 命中**，甚至连 `item.name` 都不读（唯一 `.name` 用途是比对自己建的 GameObject 名 `"SND_Lock"/"SND_Page"`，`LabelOverlay.cs:2041,2458`）。
- **结论：它没有挂 `GameItem.GetDisplayName` 补丁，不构成 `07-踩坑台账.md` 第 11 条那个热点冲突面。** 它只挂了 `GeneralHelper.SetCustomName/RemoveCustomName`（低频写入点，`SetCustomNamePatch.cs:5`）。

### 2.3 5,818 行的真实成因（逐条可查）

| 成因 | 具体在干什么 | 行数 | 出处 |
| --- | --- | --- | --- |
| **字体缺字形** | 物品名字是玩家自由输入的中文/英文，原版工厂字体没有 CJK；`ItemNameProbe` 是 28 个汉字探针，`ProbeAllFonts()` 遍历所有 `TMP_FontAsset` 试排，`AsStatic()` 把 `Dynamic` 字体 `Instantiate` 成 `Static` 副本（`atlasPopulationMode=Static`，按 instanceID 缓存），避免往原版共享的动态图集里塞字形，`EnableMultiAtlas()` 开多图集，`SimplifiedChineseFont()` 兜底 | ≈ 343 | `LabelOverlay.cs:77,566-908` |
| **渲染自愈** | 文字偶尔渲染成空网格（`TrackEmptyMesh` 记 `BadFrames`），连续 4 帧空 → 换字号/换字体/换图集，最多 2 级（`EmptyFramesBeforeHeal=4`/`MaxHealStages=2`） | ≈ 130 | `LabelOverlay.cs:1069-1201` |
| **自适应排版** | 物品格子只有 16~32px，名字可能 2 个字也可能 20 个字：`WrapToWidth` 按可用宽高反解字号 + 手动断行；`EstimateTextWidth`/`CharClassWidth` 用 **ASCII 0.5 / 其它 0.6 / CJK 1.0** 的宽度模型先算再排，**不靠 TMP 的 layout 往返**；字号钳在 7~30 | ≈ 400 | `LabelOverlay.cs:936-1069,1223-1360` |
| **拖动层** | 物品被拖动时原版切到 `fakeMiddle` + 单独 drag canvas；模组要另建一套 `SND_Label_Drag` 文本、自建 `Canvas` 盖在最上层、逐帧重算位置、还做了页码拖影 | ≈ 650 | `LabelOverlay.cs:476-500,1843-2347,2608-2752` |
| **锁定/页码角标** | 读**别的模组写入**的 `QS_LOCK`/`QS_PAGE_BADGE`（本模组只读不写，见 §3.6），在标签右上角画锁/页码小牌（含扫除、样式、文字降级） | ≈ 520 | `Api.cs:10-16`、`LabelOverlay.cs:2002-2303,2388-2608` |
| **两套设置 UI** | F10 全局窗 + 打印机内的单件面板；每个都要自绘按钮/色板/滑块，`StylePanel` 还手写了 HSV 色轮（`Texture2D` 生成色相条 + 三角取色） | ≈ 1,700 | `SettingsWindow.cs:258-303`、`StylePanel.cs:125-183,472-660` |
| **单件样式持久化** | 六个 tag + `PlayerPrefs` 记忆 + 旧值迁移（`MapLegacyFont`/`TryLegacyFont`），键是 `identifier|GetUniqueID()` | 554 | `ItemStyle.cs:46-107,142-170,450-500` |
| **抗"别的模组"** | 见 §3.6：关掉同文本 TMP、停掉 ItemDetails 的 ticker | ≈ 100 | `LabelOverlay.cs:909-933`、`Compat.cs:9-66` |

### 2.4 性能：靠"只在变化时重建 + 错峰 + 原生批处理"，**没有距离/视锥裁剪**

| 手法 | 做法 | 出处 |
| --- | --- | --- |
| **不重建** | `RefreshCore` 先比 `LastStyle/SameBounds/SameWorld/Text.text`，全同则**一个字段都不写** | `LabelOverlay.cs:199-248` |
| **只标脏不重设** | `MarkTextDirty` 只调 `SetLayoutDirty()+SetVerticesDirty()`，不重建对象 | `LabelOverlay.cs:1206-1221` |
| **错峰** | 样式解析 `(frame + phase) % 10 == 0`，每帧只处理 1/10 的物品 | `LabelOverlay.cs:1637` |
| **抽样扫描** | `HideGameLabel` 每件物品**每 10 帧**才扫一次（`++_hideTick % 10 == 0`），因为要 `GetComponentsInChildren<TMP_Text>(true)` | `LabelOverlay.cs:1687,909-933` |
| **1s 兜底重刷** | `ModRuntime` 每秒 `ReapplyAll()`（鼠标按下/拖动时跳过） | `ModRuntime.cs:12-19` |
| **合批** | 一个物品 = 一个 TMP，同字体/同图集/同材质由 Unity UI 原生合批；字体对象全局共享（`StaticCopies` 按 fontIndex 缓存） | `LabelOverlay.cs:142,703-739` |
| **可见性** | 只用 `background.image.enabled` 当代理（`IconVisible`），**没有**距离/缩放/视锥裁剪 | `LabelOverlay.cs:1730-1737` |

**性能结论**：一屏几十件不掉帧靠的是"**每件一个 TMP + 原生合批 + 变化才重建**"；代价是 `SyncVisibility()` **每帧遍历全部** `Live` 元素、每件还要 `GetComponentsInChildren` 抽样扫描 —— 这是它真正的每帧成本，物品上百时会明显。若要我们自己做，应把 `HideGameLabel` 换成"建一次、记住引用"。

### 2.5 Patch 靶点表（6 处特性补丁 + 1 处手动补丁，0 Transpiler）

| 靶点 | 类型 | 目的 | 出处 |
| --- | --- | --- | --- |
| `GameItemElement.ValidateSelect` | Postfix | 选中/校验后重画名字（主入口） | `ValidateSelectPatch.cs:6` |
| `GeneralHelper.SetCustomName` | Postfix | 打完名字立即刷新 | `SetCustomNamePatch.cs:5` |
| `GeneralHelper.RemoveCustomName` | Postfix | 删名字立即刷新 | `RemoveCustomNamePatch.cs:5` |
| `LabelerBlueUIManager.OpenUI(GameItem)` | Postfix | 打印机界面弹出时挂「单件外观」面板 | `PrinterOpenPatch.cs:7` |
| `LabelerBlueUIManager.CloseUI` | Postfix | 关面板 | `PrinterClosePatch.cs:6` |
| `StoreUIManager.ResolveEscape` | Prefix | 设置窗开着时**抢掉 Esc**（返回 true 表示已处理） | `ResolveEscapePatch.cs:5-22` |
| `ItemDetails.ItemNameLabel+ItemNameTicker.Update`（反射+手动 Patch） | Prefix `return false` | 把别的模组的标签 ticker 停掉 | `Compat.cs:31-47` |

> 它给 `ValidateSelect` 加补丁**没写参数类型表**（该方法有默认参数 `bool updateBackground = true`，只有一个重载，侥幸没出事）；而 `SetCustomName/RemoveCustomName` 也是唯一签名。**这类"没写类型表但碰巧只有一个重载"的运气不能当成规范**。

### 2.6 共存与协议：**有 tag 协定，也有跨模组遮蔽**

- **协议面（正面）**：`Api.cs` 公开四个约定 tag —— `QS_LOCK`（锁定）、`QS_COLOR`、`QS_PAGE_BADGE`（页码牌）、`SNDS_FONT`（字体），加 `Api.IsReady/ResolveFont/RefreshLockBadge/RefreshPageBadge/ClearNamesRequested` 一组静态入口（`Api.cs:4-53`）。别的模组写 tag，它就画角标 —— 这是**用 tag 当跨模组接口**的范例（比事件订阅更耐热重载）。
- **遮蔽面（反面）**：`Settings.HideOtherNameLabels` 默认 **true**；`HideGameLabel()` 扫 `handler` 下所有 TMP，**文字与自定义名完全相同**的直接 `enabled=false`（`LabelOverlay.cs:909-933`）；`Compat` 再反射把 ItemDetails 的标签 ticker 整个停掉（`Compat.cs:9-66`）。这已经不是"共存"而是"我优先"。
- **Esc 主权**：`ResolveEscape` Prefix 返回 `false` 吃掉原版的 Esc 处理（`ResolveEscapePatch.cs:7-21`），任何也在抢 Esc 的模组必须显式 `[HarmonyPriority]` 才能确定顺序。

## 4. 关键代码摘录

**① 名字来源 + 缓存比较（`LabelOverlay.cs:211-233`）**

```csharp
string text = null;
if (Settings.Enabled && GeneralHelper.HasCustomName(element))     // 只认蓝标签机打的名字
    text = GeneralHelper.GetCustomName(element);
if (string.IsNullOrEmpty(text)) { Remove(element); return; }      // 没名字就彻底不建节点
RefreshLockBadge(element);
if (TryGetBounds(element, out var bounds) && !AnyDragging && !Input.GetMouseButton(0)) {
    int style = Settings.StyleSignature ^ ItemStyle.Signature(element);
    Entry e = Cache.GetOrCreateValue(element);
    ...
    if ((e.Root == null || e.Text == null || e.LastStyle != style || e.LastName != text
         || !SameBounds(e.LastBounds, bounds) || !SameWorld(e.LastWorld, element.handler.rectTransform.position)
         || e.Text.text != text) && Build(element, e, text, style, bounds)) ...
}
```

**② 坐标换算：世界四角 → 父节点局部矩形（`LabelOverlay.cs:309-331`）**

```csharp
iconNode.rectTransform.GetWorldCorners(Corners);              // 物品背景的世界四角（含旋转）
Vector2 a = spaceNode.InverseTransformPoint(Corners[0]);      // 换算到 handler 局部
Vector2 c = spaceNode.InverseTransformPoint(Corners[2]);
Vector2 min = Vector2.Min(a, c), max = Vector2.Max(a, c);
float w = max.x - min.x, h = max.y - min.y;
if (w < 1f || h < 1f) { /* 退化：退回父节点全尺寸 */ }
Rect r = spaceNode.rect;
bounds = new Rect(min.x - r.min.x, min.y - r.min.y, w, h);
```

**③ 错峰 + 只在变化时重建（`LabelOverlay.cs:1672-1712` 摘要）**

```csharp
for (int i = 0; i < Live.Count; i++) {               // 每帧遍历全部已挂标签
    ...
    if (!AnyDragging) EnsureStyle(val, value, i);    // 内部 (frame+phase)%10 错峰
    if (Settings.HideOtherNameLabels && ++_hideTick % 10 == 0)
        HideGameLabel(val, value.LastName, value.Text, value.FakeText);   // 10 帧一次的重扫描
    bool drag = BeingDragged(val);                   // 原版 fakeBackground.image.enabled
    bool show = Settings.Enabled && IconVisible(val); // 原版 background.image.enabled
    if (value.Root.gameObject.activeSelf != show) value.Root.gameObject.SetActive(show);
    if (drag) EnsureFake(val, value);
}
```

## 5. 踩坑与红线

1. **`PlayerPrefs` 存设置**：11 个键（`snd.*`）走 `PlayerPrefs`，每次改动直接 `PlayerPrefs.Save()`（`Settings.cs:101-119,234-248`）。→ 全局、不随存档、和我们 `ModConfig` 的两套语义会打架；我们不要学。
2. **`snd.hideotherlabels` 只读不写**：`Settings.Load()` 读它，但全库没有对应 Setter，实际永远是默认 true（`Settings.cs:104`）—— 死设置项。
3. **`catch {}` 空吞**：`LocalNameClear` 的逐件清理、`ApplyAll` 的逐元素刷新都是空 catch（`LocalNameClear.cs:42-45`、`LabelOverlay.cs:1785-1801`），坏了不报错 —— 违反我们的第 2 条硬规范。
4. **`ClearAllCustomNames()`** 遍历 `PlayerStore.FindAllItem(false)` 清空所有自定义名（`LocalNameClear.cs:6-46`）：destructive 且无二次确认，别抄。
5. **`Compat` 反射 `ModReflect.FindType("ItemDetails.ItemNameLabel+ItemNameTicker")`** 依赖别的模组的内部类名，对方一改就静默失效（有 warning 日志），属于"软依赖"的典型脆法。
6. **`HideGameLabel` 用文本全等匹配**判定"这是别人的标签"（`val.text == name`）：我们的展示名一旦与自定义名逐字相同就会被它关掉；带后缀（如「（盒装正版）」）天然安全。
7. **字体静态克隆**（`AsStatic`）会为每个字体复制一张图集纹理；`StaticCopies` 不清会造成图集泄漏（`OnDisable` 只有 `DestroyAll()` 销毁节点，`LabelOverlay.cs:1824-1831`，**没有**销毁克隆字体）。

## 6. 对我们的可用点

1. **《深空当铺：可能是群友》的"玩家自定义标签"可视化**：玩家用蓝标签机给货起名后，名字现在只存在于 tooltip/tag 里。抄 `InstantiateText()` + `handler` 挂载 + `SoftPlate()` 柔底板，就能让名字直接印在货物上；样式走 **tag 协议**（`SNDS_*` 那套思路），而不是 `PlayerPrefs`。
2. **《深空电玩》的公开显示名**：我们已有 `GetPublicDisplay`（不泄密口径）。这份档案给了两条明确边界：①**不要**为它去补 `GameItem.GetDisplayName`（本模组证明了根本不需要 —— 直接读 `GeneralHelper.GetCustomName` 或自己的 tag 即可）；②若把公开名渲染成 TMP，**必须带我们的后缀/前缀**，否则会被这类模组的 `HideGameLabel` 文本全等匹配关掉。
3. **自绘设置窗三件套**（可直接复用）：`CustomUIManager.Instance.CreateWindow(...,Style.Draggable)` + `FitWindowToContent()`（用 `LayoutUtility.GetPreferredHeight` 反算窗口高度）+ `StripFullscreenBackdrops()`（把原生遮罩背景关掉，做成浮窗）—— `SettingsWindow.cs:258-303,305-403`。我们的 `ModConfig` 一旦不够用（如鉴定助手/当铺参数面板），这是现成范式。
4. **热键与 Esc 纪律**：`PollCapture()` 捕获改键状态 → 期间不响应开关；`ResolveEscape` Prefix 抢 Esc 且用 `ClosedByEscapeThisFrame` 防同帧重开（`ModRuntime.cs:20-27`、`ResolveEscapePatch.cs:7-21`）。我们做任何模态 UI 都应显式 `[HarmonyPriority]` 声明 Esc 主权。
5. **反例清单**：不要 `PlayerPrefs` 存模组设置；不要反射 patch 别的模组内部 ticker；不要 `catch {}` 空吞；不要 `GetComponentsInChildren` 每帧扫（改成建时缓存引用）。这四条正好补进我们的 code review 检查项。
