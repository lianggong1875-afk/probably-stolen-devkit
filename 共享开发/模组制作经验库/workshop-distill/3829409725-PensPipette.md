# Pen's Pipette（Pen 的移液枪）—— 用整数零误差地"只取一滴"

`3829409725` · 10.5 万订阅 · 44 文件 / 9,441 行 · v1.1.5 · `Pen_mod_pipette.dll` · 自闭合 `<Capabilities/>` · 0 Transpiler / 6 个 ModHook / 14 个手动 `Harmony.Patch`
`缓存文件夹\decomp_ws\3829409725\`（入口 `PenPipette\PenPipetteMod.cs`）
为什么值得读：**原版"倒水"这个动作根本没有"倒多少"的参数**（`TransferLiquid` 只算"目标还能装多少"），它没有去改那个参数，而是把整条"液体搬运"用 `SplitProportionally` 在**整数微升域**里重写了一遍，并顺手在上面长出一个 5,660 行的数据驱动反应引擎——这是我们做任何"按比例抽取/勾兑/掺假"时的现成教科书。

## 1. 规模与结构

### 1.1 目录 → 行数 → 职责

| 区段 | 文件 | 行 | 职责 |
| --- | --- | --- | --- |
| A 转移/剂量 | 6 | 1,035 | `PipetteTransfer` 605（自己实现的取/倒/排空 + Hydra 分派）+ `PipetteItem` 313（建物品、换委托、自愈）+ `DoseSpec` 61 + 3 个 20 行的"禁倒"补丁 |
| **B 反应/规则引擎** | 15 | **5,660** | `HydraReaction` **2,568** + `PipetteRules` 773 + `Formula` 764 + `ReactionFormulaCompiler` 505 + `HydraFeedback` 400 + `HydraMode` 379 + 9 个规则数据类 271 |
| C 窗口与绘制 | 4 | 1,483 | `PipetteWindow` **1,145**（`CustomUIBuilder` 重建的标定窗）+ `UIHitTest` 195 + `PipetteVisual` 120 + `UIDragBlocker` 23 |
| D 补丁体 | 10 | 542 | `Patches.Apply` 177（14 条 `Try(...)` 逐条包裹）+ 9 个补丁实现 |
| E 内容投放 | 1 | 196 | `PipetteAgent`：7 天一轮（`day % 7 == 2`）的化学商人散客，`StoreClientListDict.storeClientDict` 注册 |
| F 基建 | 8 | 525 | `PenPipetteMod` 205（ModHook 挂卸）、`Log` 109、`Text` 57、`Ids` 52、`Settings` 37、`QolCompat` 20 |

**这组比例是本档案最该记住的**：真正"把 5 mL 从 A 挪到 B"只有 **1,035 行（11%）**；**60% 的行数在"挪过去之后会发生什么"（反应）**；16% 在窗口。所以它不是"一个移液管"，是"一支移液管 + 一台化学引擎"。

### 1.2 骨架

- **14 条补丁全部走 `Patches.Apply()` 里的 `Try(name, action)`**：单条失败只 `Log.Warn`，其余照挂，最后打一行 `patches: N applied, M skipped`（`Patches.cs:29-137`）。这是零 Transpiler、`AccessTools.Method` + 显式 `new Type[]{...}` 参数表的写法（`TransferLiquid`、`HasUseTooltip`、`HandleUseTooltip`、`CreateWaterContainerTooltip`、`FillMedical`、`TryAdd`、`MayTarget`、`CanTarget`、`OnEventPress` 都点了参数类型）。
- **6 个 ModHook**：`OnModItemDirectoryInit`（注册物品）、`OnCreateTooltipLate`（每次摸到移液枪时自愈 + 补提示）、`OnGameLoadedNormal/Late`、`OnNewGameStarted`、`OnGenerateCustomerEarly`（散客）。
- **零能力**：读数据走 `ModHelper.ReadModFileText`（`PipetteRules.cs:68`）、设置走 `GetConfig`（`Settings.cs:15`）、文本走 `GetLocalized`（`Text.cs:11`），没有 `File`/`Directory` 直读；三个新物品 `Pen_mod_pipette.pipette/.superpipette/.minipipette` 全走 `Directory.Add(id, factory)`（`PenPipetteMod.cs:99-101`）。

## 2. 原版"一次移固定体积"的限制在哪，它怎么绕

### 2.1 限制的确切位置

原版没有任何"倒多少"的参数。`WaterHelper.TransferLiquid(source, target)`（`WaterHelper.cs:267-318`）只有一条算法：

```csharp
int num  = targetContainer.GetTagReadonly("LIQUID_CONTAINER_CAPACITY").GetInt();   // 目标容量（μL）
int num2 = 目标各成分之和;
int num3 = num - num2;              // 目标剩余空间
int num4 = 源各成分之和;
int amount = Math.Min(num3, num4);  // ← 能倒多少倒多少：倒满目标或倒空源
int[] array = SplitProportionally(GetLiquidParts(sourceContainer), amount);  // 按比例拆分
```

投放入口是 `InitLiquidContainerItem` 给物品挂的 `onThisTargetItemFunc`（`WaterHelper.cs:98-140`）：拿容器点另一个容器 → `TransferLiquid(s, t)`；连"滴管"（`isDropper`）分支也是**全量**——空的就从对方吸到满，满的就往对方倒空。**所以原版的"一手"最小单位 = 整瓶/整罐，不存在"只取 5 mL"。**

### 2.2 它怎么绕（三步，没有一步是改原版函数体）

| 手段 | 做法 | 出处 |
| --- | --- | --- |
| ① **换委托（主力）** | 建物品时把 `onThisTargetItemFunc` **整个换成** `PipetteTransfer.OnTarget`，原版那条 `(s,t)=>…TransferLiquid…` 委托被覆盖 | `PipetteItem.cs:83-111`（`onThisTargetItemFunc = PipetteTransfer.OnTarget`，`:90`） |
| ② **堵原版路径** | `WaterHelper.TransferLiquid` 前缀：目标是移液枪就 `return false`；同时把 `GameItem.MayTarget / CanTarget` 的后缀改成"不能把容器倒向移液枪"， faucet 除外 | `PatchNoPourIntoPipette.cs:5-19`、`PatchNoPourTargetMay.cs:5-15`、`PatchNoPourTargetCan.cs:5-15`、`PipetteItem.cs:114-145` |
| ③ **剂量存自己的 tag** | 剂量 `PEN_PIPETTE_DOSE_UL`（整数 μL）与容量 `LIQUID_CONTAINER_CAPACITY` 分开；容量仍交给原版 `InitLiquidContainerItem` | `Ids.cs:15`（剂量 tag）、`:29`（`PartsPerMl = 1000`）、`PipetteItem.cs:66-78` |

**结论**：它没有"改字段让固定体积变小"（原版没这个字段可改），也没有改 `TransferLiquid` 的内部逻辑（那样会污染水商/水龙头/净水器），而是**把"移液"这个动词从原版手里整体接管**：交互入口改成右键/窗口，搬运算法自己在整数域重写。这是"重做交互流程"的正确切法——**换委托 = 只影响这一个物品；补全局函数 = 影响所有容器**。

### 2.3 补丁靶点表（14 处，`Patches.Apply` 逐条 `Try` 包裹）

| 靶点 | 类型 | 干什么 | 出处 |
| --- | --- | --- | --- |
| `WaterHelper.TransferLiquid(GameItem, GameItem)` | Pre→false | 目标是移液枪就拒绝原版倒水 | `Patches.cs:84-92`、`PatchNoPourIntoPipette.cs:5-19` |
| `GameItem.MayTarget(GameItem)` / `CanTarget(GameItem)` | Post | 把移液枪从"可倒目标"里摘掉（faucet 另算） | `Patches.cs:94-104`、`PatchNoPourTargetMay/Can.cs:5-15` |
| `ItemContextHandler.UpdateConditions` / `TriggerButton` | Post / Pre→false | 菜单允许打开，且改成"开标定窗" | `Patches.cs:29-39`、`PatchPipetteContextMenu.cs:5-22` |
| `InputActionManager.Update` | Pre | 窗口开着时不吃游戏输入（**每帧**） | `Patches.cs:41-45`、`PatchBlockGameInputOverWindow.cs:5-15` |
| `ItemSelectHandler.OnEventPress(ISet<KeyCode>)` | Pre | 右键装备/使用走自己的路 | `Patches.cs:47-51`、`PatchEquipRightClick.cs` |
| `TooltipUseHelper.HasUseTooltip` / `HandleUseTooltip(GameItem, GameItem, RichTextBuilder)` | Post | 补"使用"提示与体积/剂量行 | `Patches.cs:53-72`、`PatchHasUseTooltip.cs`、`PatchHandleUseTooltip.cs` |
| `WaterHelper.CreateWaterContainerTooltip(RichTextBuilder, GameItem)` | Pre | 抑制原版容器提示 | `Patches.cs:74-82`、`PatchWaterContainerTooltip.cs` |
| `StoreClientList.CreateThief` / `StoreClientListSupplier.CreateThiefMedSeller` | Post | 贼 1% / 医疗贼 2% 塞一支超级移液枪 | `Patches.cs:106-116`、`PatchThiefHydra.cs:62-80` |
| `PreBuiltItemHelper.FillMedical(GameItem, float)` / `TryAdd(GameItem, GameItem)` | Pre+Post | 医疗箱 25%×幸运 出超级移液枪 | `Patches.cs:118-137`、`PatchCrateHydra.cs` |

表里 `ItemContextHandler` 与 `TooltipUseHelper` 两行还顺带补上了交互缺口：`HasUseTooltip` 后缀让"手持移液枪 → 指向容器"能出"使用"提示（原版只对容器-容器，`PatchHasUseTooltip.cs:5-17`）；`ItemContextHandler.UpdateConditions / TriggerButton` 把右键菜单从"打开"换成"打开标定窗"（`PatchPipetteContextMenu.cs:5-22`）。

## 3. 三种移液管：容量 / 精度 / 代价

| | `pipette`（普通） | `minipipette`（迷你） | `superpipette`（超级，内部代号 **Hydra**） |
| --- | --- | --- | --- |
| 容量 | 100 mL（100,000 μL） | **1 mL**（1,000 μL） | 100 mL |
| 剂量范围 | 1,000–100,000 μL | 1–1,000 μL | **1–100,000 μL** |
| 步长/精度 | 1 mL（1000 μL） | **1 μL** | **1 μL** |
| 默认剂量 | 5,000 μL | 500 μL | 5,000 μL |
| 输入单位 | mL | μL | μL（并同时显示两种单位） |
| 价值 | 200₵ | 350₵ | 500₵ |
| 特有能力 | 液面覆盖贴图（`PipetteVisual.Overlay`） | 无 | **倒液即反应**（`HydraMode.IsHydra` 只认它，`HydraMode.cs:31-40`） |
| 获得 | 常规货源 | 常规货源 | 只有贼（1%、医疗贼 2%）与医疗箱（25%×幸运）掉 |

数据组织：上表一行就是 `DoseSpec.cs:22-26` 的一条 `static readonly DoseSpec(minUl, maxUl, roundUl, defaultUl, capacityUl, inputInMl, showBothUnits)`，增删型号只改这张表，不动逻辑。`Clamp` 是"先夹 → 按步长四舍五入 → 再夹"（`DoseSpec.cs:39-45`），保证读回来的旧 tag 值一定落回本型号合法档位。**"精度"的真相**：原版成分标签的单位就是 **μL**（`LIQUID_CONTAINER_CAPACITY` 是 `mL × 1000`，`WaterHelper.cs:73-77`），所以"1 μL 步长"= **正好一个整数单位**，而普通款的 1 mL 步长是产品设计上的取舍，不是精度上限。

## 4. 精确移液的核心算法（全整数，无误差累积）

### 4.1 取：目标剂量 → 实际搬运量（`PipetteTransfer.cs:417-467`）

```
dose = tag PEN_PIPETTE_DOSE_UL（已 Clamp）→ num = min(dose, 枪剩余容量, 源总量)
aliquot[] = WaterHelper.SplitProportionally(源的各成分, num)
逐成分：枪.成分 += aliquot[i]；源.成分 −= aliquot[i]   // 两边都是绝对 set 写回
```

### 4.2 混：浓度永远现算，不存

搬运只动**成分绝对量**，所以混合是"整数加法"，天然守恒；"浓度"从不落盘，需要时才算 `part / total`（`double`），只在**反解体积**时才转回整数并立刻 `RoundAway`（`HydraReaction.cs:113-116`，`Math.Round(…, MidpointRounding.AwayFromZero)`）。**没有一处浮点累加**——这正是它能在千分之一毫升粒度上反复操作几百次而不漂移的原因。

### 4.3 为什么不累积误差：三件事

1. **`SplitProportionally` 是最大余数法**（`WaterHelper.cs:206-260`）：`array[k] = parts[k]*amount/total`，余数按降序补 1，**保证 `Σarray == amount` 精确成立**；`amount >= total` 时直接整份搬走。搬运量绝不会因为取整而少一 μL 或多一 μL。
2. **每次都是"绝对 set"而不是"增量加"**：`s.Enable().SetInt(旧值 ± take)` 的旧值是当场 `GetTagReadonly` 读出来的整数，不存在"累加浮点残差"。
3. **反应阶段整块重写**：`HydraReaction.Apply` 直接 `target.成分 = plan.result[i]`（`Liquid.Liquids` 全部 36 种成分一次性覆盖），末尾 `WaterHelper.RemoveExcessLiquid(target)` 收口容量（`HydraReaction.cs:2378-2415`）。每一步的表示都是自洽的，不依赖上一步的舍入。

### 4.4 反解：给定"目标需要多少主成分"，该搬多少 μL

`PrepareCustomRatio`（`HydraReaction.cs:1486-1560`）是这个模组最漂亮的一段——搬的是**按比例的整份液体**，所以"想给目标补 X μL 主成分"必须先换算体积：

```csharp
int  totalVolume = WaterHelper.GetTotalVolume(pipette);
int  mainInPipette = HydraMode.Part(pipette, rule.MainLiquid);      // num2
int  num4 = mainInPipette;                                          // 需求量（µL），可被限量试剂压小
foreach (consume in stage.Consume)                                  // 目标里别的成分不够 → 降需求
    num4 = Math.Min(num4, (int)Math.Floor((double)array[Idx(consume.Key)] / consume.Value));
num3 = (int)RoundAway((double)num4 * totalVolume / mainInPipette);  // ← 需求 → 体积（浓度反解）
num3 = Math.Clamp(num3, 1, Math.Min(totalVolume, freeCapacity));
int[] array2 = WaterHelper.SplitProportionally(WaterHelper.GetLiquidParts(pipette), num3);
```

要点：**限量试剂用 `Math.Floor` 向下取整**（宁可少反应，绝不过量消耗），只有"需求→体积"这一步用 `RoundAway`；两个方向取整方向相反，这是有意的。

### 4.5 倒：对称，但多一层滤芯

`PourAll`（`PipetteTransfer.cs:468-529`）逐成分先过 `WaterHelper.HandleFilter`（`WaterHelper.cs:333-395`，滤芯按粒径/纯度扣量并吃耐久）。按源码语义写成伪代码（成分读写就是 `ModifyTag` 的绝对 set）：

```csharp
int add = take;
if (filterItem != null) add = WaterHelper.HandleFilter(liquid.identifier, take, pipette, filterItem);
if (add > 0) target.成分 += add;        // 滤掉的那部分进不了目标
pipette.成分 -= take;                   // 但枪里按 take 全额扣掉 → 被滤掉的量就是损耗
```
**这是一个刻意的"不守恒"**：过滤损耗必须由源承担，否则滤芯就成了凭空造水的机器。

## 5. 关键代码摘录

### 5.1 取液（`PipetteTransfer.cs:417-467`，节选）

```csharp
public static int DrawToPipette(GameItem pipette, GameItem source)
{
    if (pipette == null || source == null || PipetteItem.IsPipette(source)) return 0;   // 枪对枪不允许
    if (!pipette.IsTag("LIQUID_CONTAINER_TAG") || !source.IsTag("LIQUID_CONTAINER_TAG")) return 0;
    int dose = PipetteItem.GetDose(pipette);                       // µL，已 DoseSpec.Clamp
    int num = Mathf.Min(dose, Mathf.Min(WaterHelper.GetFreeCapacity(pipette),
                                        WaterHelper.GetTotalVolume(source)));  // 三重上限
    if (num <= 0) return 0;
    bool wasEmpty = WaterHelper.IsEmpty(pipette);
    int[] array = WaterHelper.SplitProportionally(WaterHelper.GetLiquidParts(source), num);  // 精确分摊
    for (int i = 0; i < Liquid.Liquids.Count; i++)
    {
        int take = array[i];  if (take <= 0) continue;
        string part = Liquid.Liquids[i].currentPart;
        int srcAmt = source.GetTagReadonly(part).GetInt();
        int pipAmt = pipette.GetTagReadonly(part).GetInt();
        ChemicalHelper.HandleReceiveLiquidTemp(pipette, take, source);   // 温度混算仍走原版
        pipette.ModifyTag(part, s => s.Enable().SetInt(pipAmt + take), true);   // 绝对写回
        source.ModifyTag(part,  s => s.Enable().SetInt(srcAmt - take), true);
    }
    WaterFeatureHelper.OnLiquidContainerPourInto(pipette, source, wasEmpty);  // 原版副作用照补
    WaterFeatureHelper.OnLiquidContainerPourOutOf(source);
    PipetteVisual.Refresh(pipette); AudioHelper.DropperDraw();
    return num;
}
```

### 5.2 换委托 + 自愈（`PipetteItem.cs:83-111`、`:146-171`）

```csharp
public static void ApplyBehaviour(GameItem item)
{
    QolCompat.MarkNoBoxSelect(item);
    item.onThisTargetItemFunc = PipetteTransfer.OnTarget;      // ← 覆盖原版那条 (s,t)=>TransferLiquid
    item.getItemSpriteModifierForItemFunc = isBase ? PipetteVisual.Overlay : PipetteVisual.NoOverlay;
    item.mayActivateSlotItemFunc = item.canActivateSlotItemFunc = (self, inv, m) => MayActivateWindow(self);
    item.onActivateSlotItemFunc  = (self, inv, m) => PipetteWindow.ToggleFor(self);
}

public static void Repair(GameItem item)      // 由 OnCreateTooltipLate 调用：玩家每摸一次就自检一遍
{
    if (!IsPipette(item)) return;
    bool spriteOk = /* getItemSpriteModifierForItemFunc 是否仍是自己的方法 */;
    HydraMode.SyncIfEmpty(item);
    if (item.onThisTargetItemFunc != (Action<GameItem, GameItem>)PipetteTransfer.OnTarget || !spriteOk)
    {
        Log.Debug("repairing pipette delegates: " + item.uniqueId);
        ApplyBehaviour(item);   // 委托不进存档 → 读档/跨容器后必须重挂
    }
}
```

> 规则引擎那侧（`ReactionFormulaCompiler.cs:7-31`）没有单独摘：它只是一个 5 分支的 `Compile` 派发（`balance` / `extract` / `!Conversion` / 有 `Targets` / 否则普通反应），编译结果挂在 `PipetteRuleStage.WhenCompiled·AmountCompiled·ConsumeCompiled·ProduceCompiled` 上。真正的工作量在 `HydraReaction` 的 8 个 `Prepare*` 与 300 行 `switch(mode)`，见 §6 第 6 条。

## 6. 踩坑与红线

1. **委托不进存档，这是模组物品的头号坑**：读档后 `onThisTargetItemFunc` 回到原版那条 → 移液枪变回"一次倒满"。它的解法值得照抄——**在 `OnCreateTooltipLate`（玩家每次把鼠标放上去都会走）里做引用比较自愈**，而不是只靠 `OnGameLoaded`，因为物品可能来自别的容器、别的存档段、别的模组塞进来的实例。同理 **容量 tag 也会漂**：`RepairCapacity` 发现 `LIQUID_CONTAINER_CAPACITY != DoseSpec.CapacityUl` 就改回来（`PipetteItem.cs:207-230`），任何"用原版 `InitLiquidContainerItem` 建容器又想改容量"的模组都该有这条自检。
2. **`TransferLiquid` 的 Prefix 必须收窄到"目标是移液枪"**。它是全游戏共用的倒水入口，一旦无条件 `false`，水商、水龙头、净水器、浇花全废。
3. **拦 `InputActionManager.Update` 是每帧靶点**（`PatchBlockGameInputOverWindow.cs:5-15`）。它的两层防护是标配：① `Prefix` 返回 `!ShouldBlockGameInput()` 让游戏不收输入；② `PipetteShieldBlocker`——一个只实现 `IPointerDown/Up/Click/IDrag/IScroll` 的**空 Mono**，挂在窗口上吃掉穿透事件；③ `UIHitTest.IsPointerOverCustomWindow()` 用 `EventSystem.RaycastAll` 兜"鼠标到底在不在自己窗口上"（`UIHitTest.cs:13-40`）。三者缺一就会出现"点窗口顺带点了柜台"。
4. **反应结果可能超容量**：`HydraReaction.Apply` 末尾必须 `WaterHelper.RemoveExcessLiquid(target)`，否则成分和 > 容量，原版 UI 与倒水逻辑都会算错。
5. **它只认 `Liquid.Liquids` 里的成分**：`Idx(id)` 找不到返回 `-1`（`HydraReaction.cs:101-111`），搬运循环也只遍历 `Liquid.Liquids`（0.46D 共 36 种，`Liquid.cs:123`）。别的模组往容器里塞的**自定义成分 tag**（不在原版 `Liquid.Liquids` 里）在这套搬运中会被无视——**跨模组做液体联动时这是必须自己补的一段**（`3829298269` 用"1500 ms 配对窗口 + 按比例搬元数据"解决了同一问题，见 `03-原版接口速查.md` 液体成分一节）。
6. **"数据驱动"只到阶段清单为止**：`Rules/hydra.json` 声明 10 个 mode 与阶段（底物/试剂/催化/温区/得率/消耗产出），但每加一个新玩法仍要写 C#：`HydraReaction` 里有 8 个专用 `Prepare*`（Alcohol / DreamLipobond / Extraction / Fanta / Chlorine / FormulaStage / PseudoTransfer / Custom*），`NextStage` 是一个 300 行的 `switch(mode)` 硬编码反应树（`:204-298`）。**别被 `Rules/` 目录骗了，这是"半数据驱动"**——也正因为如此它才 2,568 行。

## 7. 它和《更好的水龙头》（`3833421581`）的区别

同一套液体系统两种做法，对照读最省事（水龙头侧见 `液体系统三例-扫描仪·水商·水龙头.md` §3）：

| | 水龙头 `3833421581` | 移液枪 `3829409725` |
| --- | --- | --- |
| 形态 | **机器**（场景里的固定物体） | **手持工具**（可装进背包、可交易） |
| 12k/9.4k 行花在哪 | 8,903 行单文件全在机器逻辑；**八成在"安全搬物品"与"安全存状态"** | 60% 在反应引擎；转移只有 1,035 行；**没有任何存档代码**（状态全在物品 tag 里，由原版存） |
| UI 接入点 | `gameItem.SetContentWindow(new PixelWindow(…WindowStyle.Machinery))` **整个换掉原版机器窗口** + 6 个 `NewGhostSlot` + 自绘 `PixelElement` 控件 1,646 行 | `CustomUIManager.Window("Pen_mod_pipette.calibration", …, "overlay")` 一个浮窗（`PipetteWindow.cs:544`），`AddInput/AddButton/滑块`，不碰原版窗口 |
| 补丁靶点 | `WaterHelper.AddWater`、`ChemicalHelper.*`、机器生命周期等 14 处 | `WaterHelper.TransferLiquid`（**只为禁止**）、`GameItem.MayTarget/CanTarget`（只为禁止）、`ItemContextHandler`、`InputActionManager.Update` |
| "改多少"怎么实现 | 改**调用次数/顺序**（`number` 哨兵、分块写 `CURRENT_PART_CHLORINE`、临时放大耐久），公式全用原版函数 | 改**动作归属**：换 `onThisTargetItemFunc` 委托，用自己的整数搬运；原版函数一次都不调 |
| 状态 | `SaveData` + ES3 自定义序列化（1,278 行） | 全在物品 tag（`PEN_PIPETTE_DOSE_UL`、`Pen_mod_pipette_hydra_mode_id`），**零存档代码** |
| 失败模式 | 容器被 `Expel` 后压在面板下→永久消失 | 委托丢失→退化成原版"一次倒满"（不丢东西） |

**一句话**：机器类模组的重头在"状态与物品搬运的安全"，工具类模组的重头在"行为接管与自愈"；**同一个需求（"我要倒 5 mL"），机器该改调用参数与流程，手持工具该换委托**。

## 8. 对我们的可用点

1. **《深空当铺：可能是群友》塞壬的净水/验水线**：现在是定点扣重金属（`RemoveContaminantFromContainer(..., "heavy_metal", 200000, 0)`）。若要做"按比例抽/兑"（例如把浑水按比例换成纯水、把 NUDKA 勾兑到目标度数），照 4.1–4.3 的写法：**`SplitProportionally` 分摊 + 逐成分绝对写回 + 浓度现算不落盘**，别用浮点累加，也别自己写"按百分比扣"。同时记住 §6 第 5 条——`Liquid.Liquids` 之外的成分它看不见，我们若加自定义成分必须自己补搬运。
2. **《深空电玩》的手持工具（`arc_refurb_kit` 翻新套件、`arc_disc_polish` 抛光剂）**：这三个委托槽（`onThisTargetItemFunc` / `canActivateSlotItemFunc` / `onActivateSlotItemFunc`）就是"让物品自己决定行为"的官方接缝——**换委托 = 只影响这一件，零 Harmony、零冲突**。配上 `Repair` 那套"每次 `OnCreateTooltipLate` 用引用比较自愈"，能一次解决"读档后工具失灵"这个我们已经踩过的坑。**注意翻转代价**：委托换掉后，原版的"容器互倒/整瓶使用"就完全由我们负责，`WaterFeatureHelper.OnLiquidContainerPourInto/OutOf`、`ChemicalHelper.HandleReceiveLiquidTemp` 这类副作用必须照补，否则纯度/温度/特征会不同步。
