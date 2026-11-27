# Sleep Lag Optimizer（过夜卡顿优化器）｜工坊 3844377158

> 蒸馏自反编译源码 `缓存文件夹\decomp_ws\3844377158`（14 个 .cs / 781 行，零能力模组）。
> 价值：**把原版过夜流程（360 次/秒的模拟循环）完整暴露出来**，是理解"原版夜晚到底在算什么东西"的最好教材。

## 1. 它要解决的问题（作者自述 + 代码证实）

后期存档睡觉时游戏冻结 40+ 秒。作者在 260 天重度模组档上把过夜冻结从 43.9s 降到 12.2s（3.6×），其中化学模拟从 13.4s 降到 1.4s（-89.6%）。

代码证实的四个热点：

| 热点 | 原版行为 | 本模组做法 |
| --- | --- | --- |
| `ChemicalHelper.RunNightlyChemicalSimulation` | 遍历全场物品，对每个有 `onOneSecondItemFunc` 的物品**跑 360 次**（模拟 24 小时×15 次/分） | Prefix 全量替换：先剪枝，再跑 |
| `ChemicalHelper.HandleBoilingReaction` | **原版 bug**：什么都没煮沸也 `return 1`，导致每个非空液体容器每秒触发 `OnLiquidChanged` + UI 重绘（360 次/夜） | Prefix 重写：无蒸发液体或温度未过沸点 → `return 0` |
| `GameGridInventory.Validate` / `GameItem.Validate` / `GameItemElement.HandleModSprite/HandleInnateModSprite` | 过夜期间物品频繁变动，同一对象被反复校验/重画几十上百次 | `PlayerStore.EndNight` 期间**延迟 + 去重**，结束时统一 flush 一次 |
| 白天 `onOneSecondItemFunc` | 温度已恒温（26±0.05℃）的液体容器仍每秒跑 | property getter Postfix 返回 null 节流（hash&7，8 秒一次） |

## 2. 关键机制一：化学模拟的"提前收敛"剪枝（最值得抄）

`ChemicalSimOptimizePatch` 用一个 Prefix 换掉整个 360 次循环，核心是**对温度已经收敛的容器直接算出终温并从循环里剔除**：

```csharp
// 逐个容器跑 1 秒 tick；若这一次没加热、没有液体变化、温度还在 38℃以下
bool heating = item.IsTag("HEATING_TAG");
double before = item.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
ChemicalSimOptimizePatch.LiquidChangedInTick = false;
onOneSecondItemFunc(item, item.parentInventory, null);
if (!heating && !LiquidChangedInTick)
{
    double after = item.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
    if ((!(item.parentInventory is GameSlotInventory) || (tick >= 1 && after <= before + 0.05))
        && before <= 38.0 && after <= 38.0)
    {
        int remaining = 359 - tick;
        if (remaining > 0)
        {
            int volume = WaterHelper.GetTotalVolume(item);
            if (volume > 0)
            {
                // 指数衰减直接解出 360 tick 之后的环境温，一次算完
                double k = 0.05 / (Math.Sqrt(volume) + 11.0);
                double finalTemp = 26.0 + (after - 26.0) * Math.Exp(-k * 125.0 * remaining);
                item.ModifyTag("CONTAINER_TEMP_TAG", ts => ts.SetDouble(finalTemp), true);
            }
        }
        list.RemoveAt(idx);   // 从后续 tick 的列表里剔除
        pruned++;
    }
}
```

可复用点：
- 原版温度模型是**向 26℃ 指数回归**，速率 `0.05 / (sqrt(体积) + 11)`，每秒衰减因子 `exp(-k*125)` 折算（`125` ≈ 每秒温差系数，反推自实测）。
- "提前收敛"这类剪枝对任何"多 tick 累积模拟"都适用：**只要状态单调收敛且无外部扰动，就能一次性解析求解**。
- 判据必须保守：只有 `!heating && !liquidChanged && 温度区间安全` 才剪，否则照旧跑。

## 3. 关键机制二：过夜延迟校验 + 去重（通用范式）

`EndNightDeferPatch` 展示了"批量延迟校验"的完整写法：

```csharp
[HarmonyPatch(typeof(PlayerStore), "EndNight")]
internal static class EndNightDeferPatch
{
    internal static bool IsDeferring;                       // 期内生效标志
    internal static readonly HashSet<GameGridInventory> DirtyGrids = new();
    internal static readonly HashSet<GameItemElement> DirtyElements = new();
    internal static readonly HashSet<GameItemElement> DirtyMod = new();
    internal static readonly HashSet<GameItemElement> DirtyInnate = new();

    private static void Prefix() { IsDeferring = true; /* 全部清空 + 计数归零 */ }

    // ⚠️ 用 Finalizer 而不是 Postfix：原版 EndNight 抛异常时也必须收尾，否则 IsDeferring 永远为 true
    private static Exception Finalizer(Exception __exception)
    {
        IsDeferring = false;
        var validated = new HashSet<GameItemElement>();
        foreach (var grid in DirtyGrids)
        {
            if (grid == null || grid.handler == null) continue;   // Unity 对象用 == null 判销毁
            grid.Validate();
            foreach (var child in grid.childItems)                 // 刚校验过的网格里，元素不必再单独校验
                if (child is GameItemElement e) validated.Add(e);
        }
        foreach (var e in DirtyElements)
            if (e != null && !validated.Contains(e) && e.handler != null) e.Validate();
        foreach (var e in DirtyMod)
            if (e != null && !validated.Contains(e) && e.handler != null) e.HandleModSprite();
        foreach (var e in DirtyInnate)
            if (e != null && !validated.Contains(e) && e.handler != null) e.HandleInnateModSprite();
        return __exception;    // 原样放行，不吞原版异常
    }
}
```

个中要点：
1. **标志只在 Prefix 打开、Finalizer 关闭**（Postfix 不够，异常路径会漏）。
2. 去重集合按"要执行的动作"分开（网格校验 / 元素校验 / 模组贴图 / 原版贴图），因为它们是不同方法。
3. flush 顺序有依赖：先 `Validate` 网格，再把它里面的元素加进"已处理"集合，避免重复。
4. 校验对象用 `handler == null` 判"已被销毁"（Unity 对象不能用 `ReferenceEquals`）。
5. 逐个 try/catch 并 `Debug.LogWarning` 带原因，一项失败不影响其余。

## 4. 关键机制三：白天每秒 tick 的惰性节流

`DaytimeOneSecondThrottlePatch` Patch 的是 `GameItemFunc.onOneSecondItemFunc` 的 getter（Postfix），返回 null 就等于"这一秒没有 tick 逻辑"：

- 两个特例物品直接豁免：`chem_finisher`（只有窗口可见才跑：`item.contentWindow?.IsVisible() == true`）、`arr_meat_output_hopper`（每 4 秒跑一次：`(DaySecondTick & 3) != 0 → null`）。
- 液体容器：`parentInventory is GameSlotInventory` 或 `IsTag("HEATING_TAG")` 的容器不节流；空的（`WaterHelper.IsEmpty`）直接 null。
- 恒温判定：温度在 `[25.95, 26.05]` 之间 → 进 `QuiescentContainers`，之后按 `(item.GetHashCode() ^ DaySecondTick) & 7 != 0` 跳 7/8 的 tick（错峰而不是全局同一步）。
- 刚发生过液体变化（`LastLiquidChangedTick` 在 2 秒内）→ 不节流，避免漏事件。

这套"**观察期 → 静止期 → 错峰节流**"的三态机可以直接搬到我们自己的机器/液体逻辑上。

## 5. 从代码里挖到的原版字段与标签（可直接用）

| 名字 | 类型 | 含义 |
| --- | --- | --- |
| `GameItemFunc.onOneSecondItemFunc` | `Action<GameItem, GameInventory, SlotMarker>` | 每秒 tick 回调（原版模拟的心跳） |
| `GameItemFunc.onLoaded` | `Action<GameItem>` | 读档后每个物品的收尾回调 |
| `CONTAINER_TEMP_TAG` | double | 容器温度（℃），向 26℃ 回归 |
| `HEATING_TAG` | tag | 该容器正在被加热（加热板） |
| `LIQUID_CONTAINER_TAG` | tag | 是液体容器 |
| `Liquid.currentPart` | tag key | 该液体在当前容器里的含量（int，mL） |
| `Liquid.boilingPoint` / `isEvaporative` | 字段 | 沸点 / 是否可蒸发 |
| `WaterHelper.IsEmpty(item)` / `GetTotalVolume(item)` | 静态方法 | 空液体容器判定 / 总容积 |
| `EmporiumEntry.Instance.GetAllItems()` | 方法 | **全场所有物品**（做全局扫描的第一入口） |
| `GameSlotInventory` | 类 | 机器槽位类容器（与 GameGridInventory 区分） |
| `ChemicalHelper.RunNightlyChemicalSimulation()` | 方法 | 过夜化学模拟总入口（360 tick） |
| `ChemicalHelper.HandleBoilingReaction(GameItem)` | 方法（**有重载，必须写显式参数类型表**） | 单容器煮沸结算，原版 bug 在返回值语义 |

## 6. 踩坑与限制（读代码得出的）

1. **`ChemicalHelper.HandleBoilingReaction` 是重载方法**：本模组用 `new Type[] { typeof(GameItem) }` 精确指定，不写就会 `AmbiguousMatchException` 把整批补丁带崩。
2. 它替换了整个 `RunNightlyChemicalSimulation`（Prefix 返回 false），意味着**任何别的模组对过夜化学的改动都会失效/打架**。我们要做类似优化时，宁可在内部方法上做局部优化，也别整段替换原版入口。
3. 它用 `Stopwatch` 打日志但只写 `Debug.Log`，没有写文件；排查时需要开控制台。
4. 它没写 `HotReloadPrepare/Activate`，热重载时静态计数器（DirtyGrids 等）不会主动清空——靠 `EndNight` 的 Finalizer 兜住。我们自己做同类功能时应在热重载钩子里显式 `Reset()`。

## 7. 对我们的可用点

- **深空当铺：可能是群友**：如果我们加"过夜自动照料"类功能（喂鼠、加水、清灰），必须避免在 360 tick 循环里做重活——用本模组的"收敛即剪枝 + 事件驱动"思路，只在状态真变化时动手。
- **深空电玩**：做"实时运行机器"（参考 `3842333957 real-time`）时，`onOneSecondItemFunc` 就是原版给我们的现成每秒心跳，别自己起协程。
- 任何"整场扫描"需求（统计、索引、告警）用 `EmporiumEntry.Instance.GetAllItems()`，别自己爬场景树。
- 我们自己的模组若也做延迟校验，务必照抄 `Prefix 开 / Finalizer 关` 的成对写法。

## 8. 待验证（未真机确认）

- 原版是否真的每秒调用 `onOneSecondItemFunc`（白天），还是只在某些状态下调用——本模组假设是每秒，我们没在真机计时验证。
- 指数衰减公式中的 `125` 系数是作者拟合值，换机器/换液体体积未必精确；照抄前应实测比对。
