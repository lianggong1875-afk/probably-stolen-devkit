# 店長可能是智能體（AI 代打）｜工坊 3845754309

> 蒸馏自 `缓存文件夹\decomp_ws\3845754309`（命名空间 `WhiteDragon.Ledger`，mod id `white_dragon_ledger`）。
> **先纠正一个前提**：作者简介说"通过本机 SQLite 账本把店内信息整理给 AI 读"，但**反编译产物里没有任何 SQLite 引用**（全目录 grep `SQLite/sqlite` 零命中）。
> 实际实现是：**原版 `ModHelper` 的 ModData 键值 + ModData 目录下的 JSON 文件"收件箱"轮询**。要么作者记错，要么 SQLite 在未包含的外部程序里。
> 这份档案只写代码里看得见的东西；通读范围有限（`InventoryObserver.cs` 一篇就有 14484 行），未读部分标「（推测）」。

## 1. 文件构成（`WhiteDragon.Ledger\`）

| 文件 | 行数 | 职责（按名字与调用推断） |
| --- | --- | --- |
| `InventoryObserver.cs` | **14484** | 全部业务逻辑的巨无霸：库存盯盘、账本构造、命令执行、各面板数据 |
| `NativeItemPanels.cs` | 639 | 用原版面板显示物品（不是自绘） |
| `StartingPerks.cs` / `StartingDifficulty.cs` / `Specializations.cs` / `TravelOptions.cs` / `ScratchCard.cs` | 102~214 | 开局特性/难度/专精/出行/刮刮乐等"接管玩家操作"的入口 |
| `ItemPanelView.cs` / `InventorySorting.cs` / `LiquidParts.cs` / `NativeButton.cs` | 16~58 | 小工具 |
| `LedgerMod.cs` | 33 | `IMod` 入口 |

## 2. 外部 AI 的接口：**ModData 目录下的 JSON 收件箱**

这是本模组唯一真正"新颖"的机制，也是唯一可复用的架构：

```csharp
private const string ModId = "white_dragon_ledger";

// ① 模组写"账本/状态"给外部读
ModHelper.SetModData(ModId, "last_night",    nightArray.ToString(Formatting.None));   // 昨夜流水
ModHelper.SetModData(ModId, "purchase_lots", lots.ToString(Formatting.None));         // 进货批次
ModHelper.WriteModDataFileText(ModId, deskFile, text);                                // 桌面级账本文件
ModHelper.WriteModDataFileText(ModId, name,     body.ToString(Formatting.None));      // 任意命名文件

// ② 外部 AI 往同名目录写"命令/回执"文件，模组轮询消费
if (ModHelper.ModDataFileExists(ModId, inbox))
{
    var text = ModHelper.ReadModDataFileText(ModId, inbox);
    // …解析并执行…
    ModHelper.DeleteModDataFile(ModId, inbox);      // 消费即删，天然幂等
}
else if (!ModHelper.ModDataFileExists(ModId, "sale-progress-" + generation + ".json")) { /* 等回执 */ }

// ③ 非法命令要被丢掉并留日志，不能执行半截
Debug.LogWarning("[WhiteDragonLedger] Removed malformed command: " + inbox);
```

要点（对我们最有价值）：

1. **文件收件箱 = 天然的跨进程通道，且不需要任何网络能力**——只走 `ModHelper.WriteModDataFileText` / `ReadModDataFileText`，属 `files` 能力，不走 socket。
2. **消费即删**（读完 `DeleteModDataFile`）让重放/幂等变得简单：文件存在 = 有待处理命令。
3. **`sale-progress-<generation>.json`** 用"代号命名"区分每一轮任务，避免上一轮残留下发。
4. **malformed command 日志 + 丢弃**：外部 AI 写错格式不能把游戏带崩。这条是我们做任何"外部输入"功能都该有的。

> 这比"HTTP 轮询"轻得多：不用开端口、不用处理鉴权、不会与别的模组抢端口。**如果将来要让外部工具驱动我们的模组（或做自动化测试夹具），这个 ModData 文件收件箱是首选通道。**

## 3. 它靠什么"连续跑十几天不卡"

- 状态读写集中在 `ModHelper.SetModData/GetModData`（原版实现，不自己开文件流）；
- 数据是**增量 JSON**（`last_night` 数组、`purchase_lots` 数组），不是每次全量重建；
- 观察者是一个 `GameObject`（`"White Dragon Ledger Observer"`）挂在场景里，说明它靠 **Unity 组件驱动**而不是每帧静态轮询（推测其内部有节流）。

## 4. 与"账本"无关但值得抄的两点

1. **用原版面板显示，不重画**：`NativeItemPanels.cs` + `ItemPanelView.cs` 说明它在很多地方直接借原版 UI 结构，而不是自绘一套（对比 DangpuPlus 自绘、Item Details 自绘）。**能用原版就别自绘**，省事且天然贴合游戏风格。
2. **接管玩家操作的入口是分开的**：开局特性/难度/专精/出行/刮刮乐各一个文件，而不是全塞在一个 `Handle` 里。即使主逻辑是个 1.4 万行的巨无霸，**外围操作入口仍是按功能切开的**——这点比 DangpuPlus 的 103 项静态委托表更朴素，但同样解决了"多操作入口"的问题。

## 5. 风险与诚实标注

- **不要把它当安全样本**：外部 AI 写的命令文件直接被解析执行，没有签名/校验（只做了格式校验）。它需要 `files` 能力，且 AI 决策错误可能直接改游戏状态。
- **108 行的 `StartingPerks/StartingDifficulty` 等文件说明它能改开局配置**——这类"代打"能力在正式模组里风险很高（玩家可能不知道自己被改了选择）。
- 本档案**没有**逐行读 `InventoryObserver.cs`（14484 行），其中"具体怎么读店内状态、怎么落动作"的细节未确认，标「（推测）」。
- **未找到 SQLite**：如果读者需要 SQLite 方案（结构化查询、事务），工坊里没有现成样本，得自己引库——但按本模组的经验，**JSON + ModData 已经够用**，不建议为此加原生 DLL。

## 6. 对我们的可用点

1. **ModData 文件收件箱**（§2）→ 给"自动化真机测试"做外部夹具：测试脚本写命令文件，游戏内模组轮询执行并把结果写回文件。比现在用 MCP 桥更轻，也不占端口。
2. **消费即删 + generation 命名 + malformed 丢弃**这三条，是任何"外部输入"功能的标配。
3. **能用原版面板就别自绘**（§4.1），我们做"可能是群友"的顾客面板时可以省一大截 UI 工作。
4. **AI 代打这类功能不要进正式发布**（能力与风险都不合适）；要留就放 companion 模组（参考 Probably Extend Framework 的拆分）。
