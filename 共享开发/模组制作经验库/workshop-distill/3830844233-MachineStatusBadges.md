# 3830844233 · Machine Status Badges（机器状态角标）—— 反编译蒸馏档案

## 1. 档案头

> 源码：`缓存文件夹\decomp_ws\3830844233`（39 个 `.cs`，9,013 行；`1.0.26`，`Mod.cs:103` 的 `Enabled 1.0.26` 日志；manifest 空 `<Capabilities/>`，5.8 万订阅）
> 分区：`MachineStatusBadges\` 平铺 38 文件 + `Properties\AssemblyInfo.cs`；入口 `Mod : IMod`（`Mod.cs:4`），运行时 `Runtime : MonoBehaviour`（`Runtime.cs:10`）
> 为什么值得读：**全库把"给原版物品/机器叠一层信息"做得最干净的一份** —— 零 `OnGUI`、不改 `item.name`、不换原贴图，而是把一块**当场烘焙的 `Texture2D→Sprite`** 挂成 `GameItemElement.middle` 的子节点，绘制入口只挂在 `HandleInnateModSprite()` 一个 Postfix 上；配套的分帧预算 + 脏签名缓存，是"给几十台机器同时画角标"的标准答案。

## 2. 规模与结构

| 层 | 文件（行数） | 合计 | 占比 |
| --- | --- | --- | --- |
| **纯数据表** | `MeterFont` 3,695（18 个字形 × 64px 的 alpha 位图）+ `NativeArt` 1,786（444 条 `物品id →(图集,雪碧)` 映射） | 5,481 | 61% |
| **绘制（像素→Sprite）** | `OverlayNodes` 270、`Overlay` 228、`SmoothMeters` 212、`Icons` 92、`BadgePixels` 86、`Alert` 17、`Meter` 28、`Kind` 14、`Corner` 9 | 956 | 11% |
| **读取/判定** | `Inspect` 538、`Production` 190、`Meters` 170、`Food` 140、`OutputShapes` 124、`Feeding` 112、`Rules` 109、`FoodPool` 18 | 1,401 | 16% |
| **调度/生命周期** | `Runtime` 426、`MachineDiscovery` 220、`PlantPollutionHistory` 38、`RefreshGate` 30、`VisualContent` 29 | 743 | 8% |
| **Harmony 补丁** | 11 个文件（最大 `DebugTooltipPatch` 44 行） | 266 | 3% |
| 其余 | `Mod` 117、`L` 23、`Consumer` 16、`AssemblyInfo` 12 | 168 | 2% |

**"9,013 行里 UI 占多少"**：真正画图的只有 956 行（11%）；另有 5,481 行（61%）是字体位图与物品图集映射这种**死数据**（`MeterFont.cs:5-24`、`NativeArt.cs:5`，分层为归类近似，加总 9,015 与 9,013 差 2 行为跨层取整）。逻辑主体在"读机器状态"（16%）而不是"画"（11%）——这条比例是这类模组的通用形状：**画便宜，判贵**。

## 3. 它怎么做这件事

### 2.1 核心答案：角标画在哪一层 —— `GameItemElement.middle` 下的自有 `TreeNodeRender` 子节点

三层都不走"每帧 OnGUI"：全库 **`OnGUI` 命中 0 次**，`Update` 只有 1 处（`Runtime.cs:347`，且只管数据刷新，不管画）。

1. **节点**：`OverlayNodes.Create()` 用 `TreeNodeRender.Instantiate().SetParent(item.middle)` 造一个**普通 UI 节点**（不是 SpriteRenderer），命名 `"MachineStatusBadges overlay"`，`raycastTarget=false`、锚点 `(0,1)`、pivot `(0,1)`（`OverlayNodes.cs:124-143`）。同样的节点再给 `item.fakeMiddle` 造一份孪生 —— 原版"假背景/拖动层"走的是 `fakeMiddle`，两套都要贴，否则拖动时角标消失（`OverlayNodes.cs:49-56`）。
2. **图**：不是预制贴图，而是**每次内容变了就现场烤一张**：`Overlay.Get()` 分配 `uint[]` 像素缓冲 → `SmoothMeters.DrawLayers()` 用 9×9 字符串点阵图标 + 64px 字体把图标和文字"画"进缓冲（4× 超采样，`Scale=4`，`SmoothMeters.cs:8`）→ 翻转行序（Unity 纹理 Y 轴朝上）→ `new Texture2D(...,TextureFormat.RGBA32,false)` + `SetPixels32` + `Sprite.Create(..., 400 PPU)`（`Overlay.cs:131-172`）。
3. **贴**：`OverlayNodes.Apply()` 把 sprite 同时赋给 real/fake 两个 `Image` 并 `SetNativeSize()`；位置由 `Place()` 算：取 `item.rotator.rect` 的左上角 + `background.anchoredPosition`，经 `rotator.TransformPoint → middle.InverseTransformPoint` 反算出 4 个角在父节点局部空间的位置，取 `minX/maxY` 作为锚点（`OverlayNodes.cs:145-185`）—— 这样**物品旋转/翻转/换格尺寸时角标跟着走**，不需要写任何旋转补偿。
4. **触发**：`DrawPatch` = `[HarmonyPatch(typeof(GameItemElement),"HandleInnateModSprite", new Type[]{})]` 的 Postfix（`DrawPatch.cs:4-14`）。原版 `HandleInnateModSprite()` 在 `Validate()` 尾部被调（`GameItemElement.cs:375`，`Validate` 是事件驱动、非每帧），所以角标是**"物品被重新校验时画一次"**，天然跟着 `Validate` 的节奏走。
5. **z 序**：`PinLayerPatch` 挂原版**私有**方法 `GameItemElement.UpdatePinOverlay()` 的 Postfix → `SetAsLastSibling()`（`PinLayerPatch.cs:4-14`、`OverlayNodes.cs:228-243`）。
6. **显隐/染色镜像**：`ToggleBackground(bool,float,float)` 与 `ToggleFakeBackground(bool,Vector4)` 的 Postfix 把原版背景当前的 `enabled`/`color` 抄给角标，保证角标和物品本体同生同灭、同染色（`RealVisibilityPatch.cs:4`、`FakeVisibilityPatch.cs:5`、`OverlayNodes.cs:187-246`）。

> 复刻要点：`TreeNodeRender` 必须给 `SetParent(TreeNodeRender)`（`TreeNodeRender.cs:129`），`middle` 正是 `TreeNodeRender`；`Texture2D` 用 `FilterMode.Point` 才能保住像素感；`Sprite.Create` 的 PPU 要用 `100*Scale` 抵消 4× 超采样（`Overlay.cs:172`）。

### 2.2 「基础状态」从哪读：**没有状态机，只有"缺什么"**

原版**没有 `MachineHelper` 这个类**（全库 0 命中）；它用到的只有 `MachineryHelper` 两个方法（`GetMachinePowerUsage`、`GetCurrentQualityBonus`，各 2 处）。也**不读任何"运行/待机/故障"总状态字段**——"运行中"就是两次字符串判等：`Str(m,"MACHINE_STATE_TAG")=="STATE_WORKING"` 或 `Str(m,"DESEQUENCER_STATE_TAG")=="STATE_DESEQUENCING"`（`Inspect.cs:236`、`Production.cs:21`、`Meters.cs:74`）。

判据全表（`Kind` 枚举 9 种：Power/Water/Food/Nutrient/Pollution/Filter/Material/Output/BoostReady，`Kind.cs:2`）：

| 角标 | 判据（全部来自 tag 或原版 Helper） | 出处 |
| --- | --- | --- |
| 断电/供电不足 | power 槽空 → Level2；`PowerHelper.CanDrawPowerSource` 失败 → Level2；否则 `energy<=2*usage` 或 `<=20% max` → Level1 | `Inspect.cs:342-407` |
| 电网/超导电池 | `CurrentConverterHelper.IsCurrentConverter`（黑市电、欠费、关断）、`HypertechBatteryHelper.GetOutput` | `Inspect.cs:369-401` |
| 缺料/缺件 | 逐机器 if/switch：打字机缺芯片/缺料（读 `REQUIRED_ITEM_ID_n`）、熔炉缺金属或垃圾（`furnace_module_blast/junk`）、搅拌机缺容器/容器满、化工缺容器/太湿/体积不足、测序仪缺芯片或卡、DNA 缺磁盘/被 `blockedReason` 阻塞、除湿机输出满 | `Production.cs:30-180` |
| 输出堵 | `OutputShapes.TryFit(grid, id, count)` 为假 → Level2 | `Production.cs:181-190` |
| 缺水/营养失衡/污染 | `HydroponicHelper.GetWaterUsePerCycle/GetNutrimentLevel/GetContaminantSensitivity/GetContaminantEffect` + `WaterHelper.GetContaminantPercentage` + `Rules.PlantPollution` | `Inspect.cs:408-476` |
| 鱼/畜 | `AquacultureHelper.*`（水量、废物、滤芯）、`AnimalCage.GetCageInventory`、`Food.CheckFish/CheckAnimals` | `Inspect.cs:478-538` |
| 饲料不够吃 | `MachineFeedDispenser.GetTargetRatio` + 单体 `GetMaxHunger`/`GetExpectedDrainTonight` → `Feeding.Forecast` | `Food.cs:122-134` |
| 就绪（绿） | 净水器纯度达 `MachinePurifier.GetMaxPurityCap`、`MachineTurboBoosterAdv.IsTurboReady` | `Inspect.cs:152-170` |
| **不评估** | `BROKEN_MACHINE_TAG`（坏机）、`!GeneralHelper.IsItemOwned`、`modifiedState==null`、无任务 | `Inspect.cs:115`、`Inspect.cs:216-341` |

三条设计差异值得记住：①**故障机不画角标**（不是画个红叉）；②**无任务时整体不出图标**（连"待机"都不说）；③阈值集中在 `Rules.cs`：`Supply(have,need,warn×2)`→0/1/2、`Power` 同构、`Filter` 20% 黄、`BadgePixels.FilterLevel` 20/50 分档（`Rules.cs:18-42,70-81`、`BadgePixels.cs:34-42`）。**Level 0/1/2 = 绿/黄/红**，同时决定图标颜色（`BadgePixels.cs:15-24`）。

### 2.3 「数值状态」从哪读：只有 4 类百分比，没有进度/温度/污染数值

`Meters.Read()`（`Meters.cs:9`）返回的行，全都是 `百分比 + 图标`，左下角（默认 `Corner.BottomLeft`）竖排：

| 行 | 来源 | 出处 |
| --- | --- | --- |
| 电量 | 电池 tag `power_source_item_energy` / `power_source_item_max_energy`；转换器 → `ON/OFF`；超导电池 → `INF` | `Meters.cs:104-124` |
| 水 | `WaterHelper.GetCurrentCapacityUL` / `GetTotalCapacity` | `Meters.cs:145-156` |
| 滤芯 | 多枚滤芯 `DurabilityHelper.GetRealDurability/GetMaxDurability` **求和后**取百分比 | `Meters.cs:125-144` |
| 饲料 | `STORED_KCAL_TAG` / 40000 | `Meters.cs:157-164` |

**进度条/温度/污染浓度一概没有**（污染是图标不是数字）。数值与"基础状态"的边界很干净：**右上回答"缺什么"，左下回答"还剩多少"**，正好对上简介那句话（`Mod.cs:34-36` 的默认值 `TopRight`/`BottomLeft`）。

### 2.4 性能：三段式，全库没有"每帧全量重画"

| 手法 | 做法 | 出处 |
| --- | --- | --- |
| **只在 `Validate` 里画** | 绘制入口挂 `HandleInnateModSprite` Postfix；`Validate` 由机器状态/库存变化驱动 | `DrawPatch.cs:4`、`GameItemElement.cs:375` |
| **数据 0.5s 节流** | `RefreshGate.TryBegin(now, settingsRevision)`：0.5s 内同版本直接 false；`Invalidate()` 只在"由不可见变可见"或改设置时调 | `RefreshGate.cs:18-24`、`Runtime.cs:63-101` |
| **脏签名缓存 sprite** | `Overlay.Get()` 命中 `(dataVersion, SettingsVersion, orientation, flipped, atlas, spritePath, shapeW/H)` 全部相同就返回旧 sprite，**不重建纹理** | `Overlay.cs:124-129` |
| **内容不变不递增版本** | `VisualContent.Equal(旧 alerts/meters, 新)` 相等则 `Version` 不动 → 上层缓存继续命中 | `Runtime.cs:86-102`、`VisualContent.cs:7-28` |
| **分帧预算泵** | `Update()` 每帧最多**检查 64 个**、成功刷新**最多 8 个**、且**超过 2ms 立刻 break**；游标 `cursor` 轮转 | `Runtime.cs:347-386` |
| **弱引用 + 5s 清理** | 绑定存 `ConditionalWeakTable<GameItem,Binding>`，列表存 `WeakReference<GameItemElement>`，每 5s 扫一遍销毁项并 `Destroy` 纹理 | `Runtime.cs:120-128`、`Runtime.cs:390-406` |
| **图标只烤一次** | 9 种 × 3 档 = 27 张 11×11 sprite 在 `Dictionary<int,Sprite>` 里复用 | `Icons.cs:12-80` |

结论：**角标本身不是每帧成本，数据轮询才是**；它把轮询摊到"每帧最多 8 台机器"，30 台机器 ≈ 4 帧一轮 ≈ 0.5s 内全部刷新一次。

### 2.5 设置项：6 个 `ModConfig`，**没有自绘窗口**

简介说"设置里有很多调整项"，源码里就是 **6 个** `ModConfig.For("MachineStatusBadges").Define(...)`：`debugInfo`、`showStatus`、`showMeters`、`statusPosition`、`meterPosition`、`supportModMachines`（`Mod.cs:62-68`）。两个位置项用 4 元素字符串数组当**下拉**（`Mod.cs:24` 的 `Corners`，`右上角`=`Corners[1]`、`左下角`=`Corners[2]`）。改动通过 `OnChanged` → `SettingsVersion++` → `Runtime.RefreshDisplayed()` **强制全量重画**（`Mod.cs:76-95`）。文案全走 `ModHelper.GetLocalized`（`L.cs:9`）。——"很多调整项"是**营销措辞**，真实成本是 6 行 `Define` + 一份 4 角枚举。

### 2.6 共存：**没有协议，只有一个"角落 = 语义"的约定**

- **没有**对外注册入口、没有事件、没有跨模组 tag 协定；`public` 的只有 `Rules`/`SmoothMeters`/`BadgePixels`/`MeterFont`/`VisualContent`/`Meter`/`Alert`（`Rules.cs:7` 等），第三方引用 DLL 能调，但没有"插槽"。
- 简介里链的 *Real-time Pollution*（`3841756565`）按"左下角"对齐，靠的就是**默认角不重叠**：基础状态右上、数值左下。角标本身还做了**同角自动避让**：当 `statusCorner == meterCorner` 时，数值层按告警行数下移 `inset`（`SmoothMeters.cs:41-53`），最多把图标挤到第二行。
- 真正的冲突面不在角落，而在 `ReadGuard`：它给**全局热点** `GameItem.GetTagReadonly(string)` 挂了 Prefix，在 `[ThreadStatic] bool Active` 期间直接从 `modifiedState.dict` 取值并 `return false`，绕过原版读取（`ReadGuard.cs:4-19`）。任何也在改 `GetTagReadonly` 的模组都会与之互相影响 —— 这正是 `07-踩坑台账.md` 第 11 条那条"别给通用方法挂补丁"的同型风险。

### 2.7 Patch 靶点表（11 处，全部 Prefix/Postfix，0 Transpiler）

| 靶点 | 类型 | 目的 | 出处 |
| --- | --- | --- | --- |
| `GameItemElement.HandleInnateModSprite()` | Postfix | 唯一绘制入口 | `DrawPatch.cs:4` |
| `GameItemElement.UpdatePinOverlay()`（private） | Postfix | z 序顶到最前 | `PinLayerPatch.cs:4` |
| `GameItemElement.ToggleBackground(bool,float,float)` | Postfix | 镜像背景显隐/染色 | `RealVisibilityPatch.cs:4` |
| `GameItemElement.ToggleFakeBackground(bool,Vector4)` | Postfix | 拖动层同步 | `FakeVisibilityPatch.cs:5` |
| `DirectoryMaster.Item(string,bool)` | Postfix | 抓新造物品 + 缓存输出形状 | `ShapeCapture.cs:4` |
| `PixelWindow.Show()` / `Show(Vector2,Vector2,bool)` | Prefix→Postfix | 窗口刚打开时补挂内部物品 | `ShowWindowPatch.cs:4`、`ShowPositionedWindowPatch.cs:5` |
| `GameItem.GetTagReadonly(string)` | Prefix | 只读直取，避免读 tag 的副作用 | `ReadGuard.cs:4` |
| `HydroponicHelper.StartGrowCycle(GameItem,GameItem)` | Postfix | 清周期污染记录 | `ResetPlantObservation.cs:4` |
| `HydroponicHelper.HandleContaminant(GameItem)` | Prefix+Postfix | 记"本周期实际掉血"（真值，不是估算） | `ObservePlantPollution.cs:4` |
| `ModSourceHelper.CreateTooltip(RichTextBuilder,GameItem)` | Postfix | debug 模式在 tooltip 打诊断行 | `DebugTooltipPatch.cs:5` |

> 注意它**显式写了重载类型表**：`ToggleFakeBackground` 在原版有两个重载（`GameItemElement.cs:727/769`），不写 `new Type[]{typeof(bool),typeof(Vector4)}` 会补错。

## 4. 关键代码摘录

**① 绘制入口 = 一个 Postfix + 可见性判断（`Runtime.cs:188-222`）**

```csharp
internal static void AfterNativeDraw(GameItemElement item) {
    try { Attach(item);
          if (bindings.TryGetValue(item, out var value)) DrawBound(item, value); }
    catch (Exception e) { Warn("draw:" + item.identifier, e); }
}
private static void DrawBound(GameItemElement item, Binding binding) {
    if (binding.ObserveVisibility(item)) {          // 原版 middle/fakeMiddle 至少一个 active
        if (!binding.HasSnapshot) binding.Refresh(item, Time.unscaledTime);
        binding.Display(item);                      // → Overlay.Get(缓存) → Nodes.Apply
    }
}
private static bool Visible(GameItemElement item) {
    if (!Active(item.middle)) return Active(item.fakeMiddle);
    return true;
}
```

**② 角标定位：反算旋转后的左上角（`OverlayNodes.cs:145-185`）**

```csharp
RectTransform rotator = rotator.rectTransform, parent = parent.rectTransform;
float xMin = rotator.rect.xMin + background.rectTransform.anchoredPosition.x;
float yMax = rotator.rect.yMax + background.rectTransform.anchoredPosition.y;
float minX = float.PositiveInfinity, maxY = float.NegativeInfinity;
for (int i = 0; i < 4; i++) {
    Vector3 p = parent.InverseTransformPoint(
        rotator.TransformPoint(new Vector3(xMin + (i % 2 * w), yMax - (i / 2 * h), 0f)));
    minX = Math.Min(minX, p.x); maxY = Math.Max(maxY, p.y);
}
node.rectTransform.anchoredPosition = new Vector2(minX - parent.rect.xMin, maxY - parent.rect.yMax);
```

**③ 分帧预算泵（`Runtime.cs:347-386`，去掉 IL 噪声）**

```csharp
double now = Time.unscaledTime; long t0 = Stopwatch.GetTimestamp();
int checkedN = 0, refreshed = 0, count = items.Count;
while (count > 0 && checkedN < Math.Min(count, 64) && refreshed < 8) {
    if (cursor >= items.Count) cursor = 0;
    WeakReference<GameItemElement> w = items[cursor++]; checkedN++;
    if (!w.TryGetTarget(out var target) || target.IsDestroyed()
        || !bindings.TryGetValue(target, out var value)) continue;
    try { if (value.ObserveVisibility(target) && value.Refresh(target, now)) {
              refreshed++; value.Display(target); } }
    catch (Exception e) { Warn("refresh:" + target.identifier, e); }
    if ((double)(Stopwatch.GetTimestamp() - t0) / Stopwatch.Frequency >= 0.002) break;  // 2ms 上限
}
```

## 5. 踩坑与红线

1. **双节点是硬要求**：只挂 `middle` 的角标会在拖动/假物品时消失；real 与 fake 必须成对建、成对销毁（`OverlayNodes.cs:49-56,247-261`）。
2. **`UpdatePinOverlay` 是 private**，只能按字符串名补；`ToggleFakeBackground` 有重载，必须写类型表。
3. **纹理必须手动销毁**：`Object.Destroy(value.texture)` 再 `Destroy(sprite)`（`Overlay.cs:195-206`）；漏了就是每台机器攒一张 RGBA 贴图。
4. **BROKEN_MACHINE_TAG 早退**（`Inspect.cs:115`）：坏机不显示"故障"而是**什么都不显示** —— 想做"故障红叉"要自己补，别以为它覆盖了三态。
5. **`ReadGuard` 补全局 `GetTagReadonly`** 是这份模组最脏的一处；同型做法在我们这里应当禁止（见 §6）。
6. `IsModItem` 用 `DirectoryMaster.GetFactory<GameItem>(id).Method.DeclaringType.Assembly` 判"是不是模组物品"，再用 `NativeArt.Map` 做兜底（`MachineDiscovery.cs:20-53`）—— 这是"如何区分原版/模组物品"的一个可用判据，但依赖 1,786 行手维护表，维护成本极高。
7. `Inspect.Supported` 硬编码 26 个原版机器 id + 4 个 extras（`Inspect.cs:9-16`、`MachineDiscovery.cs:7`）；新增机器必须改表，作者的"通用逻辑扫描"其实只覆盖面板角色启发式（`MachineDiscovery.cs:55-111`）。

## 6. 对我们的可用点

1. **《深空电玩》刻录机/机器状态**：把"右上基础状态 + 左下数值"直接搬到我们的机器（刻录机 `STATE_WORKING` 锁盘 24/40、`ARC_BURN_FAULT`、`TURBO_BOOST_ELIGIBLE_TAG`）—— 玩家不用开窗口就知道缺盘还是缺电；判据照抄 `Rules.Supply/Power` 的三档阈值即可，阈值写法与我们的文案口径（不给数值%）兼容。
2. **《深空当铺：可能是群友》展柜/货架**：`TreeNodeRender.Instantiate().SetParent(element.middle)` + `Postfix(HandleInnateModSprite)` 是"**给任意物品加视觉层而不动 `item.name`**"的正确姿势 —— 正是 `07` 第 11 条要求的替代路径（撤 `GetDisplayName` 补丁后，视觉标记就该走这一层）。
3. **原生零 Harmony 方案**：`GameItemFunc.getItemSpriteModifierForItemFunc`（`GameItemFunc.cs:36`）+ `ItemSpriteModifier(sprite, position, orientation, isFlipped, priority, haveMask, color)`。原版自己就这么干（`GunModDirectory.cs:152-165`、`EmporiumEntry.cs:1829`）。比本档案的 Harmony + 手算坐标轻得多，代价是 sprite 要预先存在（不能每帧烤），且只在 `Validate()` 时重取 —— 适合"静态徽记"（正版章、开锁态、成色角标），不适合"实时百分比"。
4. **分帧预算泵 + 0.5s Gate + 脏签名缓存**（`Runtime.cs:347-386`、`RefreshGate.cs:18-24`、`Overlay.cs:124-129`）：任何"逐物品/逐机器状态层"都该抄这三件套；我们的物品量级（79+ 图鉴/展柜货）远小于它假设的机器数，直接可用。
5. **反例清单**（写进我们的 code review 检查项）：不要给 `GameItem.GetTagReadonly` 这类全员热点挂 Prefix；不要 `Debug.Log` 每帧输出；不要忘记 `OnDisable` 里销毁运行时纹理（对照 `Mod.cs:106-116` 与 `Runtime.cs:408-425` 的 `Detach()` 全量清理）。
