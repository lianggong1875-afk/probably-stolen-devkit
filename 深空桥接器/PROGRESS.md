# 深空桥接器 · 进度台账

游戏内 MCP 开发桥 `ModDevBridgeNext` 的**本地源码工程**。工程说明、构建法、切换步骤见 [README](README.md)。

---

## 第 1 轮（2026-11-26）· 工程落地 + 第一批能力

### 目标

用户提出两件事：① 把 `ProbablyStolen_MCPBridge`（另一个桥）有、我们没有的能力抄过来；② 我们把现在这个桥 `ModDevBridgeNext` 抄成本地模组，才能改造。目标是把「等两晚才知道机器做没做出来」和「调游戏自带调试功能」这两个卡点消掉。

### 完成

**① 发现工坊包自带官方源码，不用反编译**

`D:\steam\steamapps\workshop\content\4348910\3835953432\FullSource\` 里是完整的 32 个 `.cs` + `csproj` + `Directory.Build.props` + `source-hashes.json`。README 原文：*"Workshop ships one Compatible DLL plus readable sources."* 所以**抄源码这一步不需要反编译**，直接取官方原文。

**② 工程落地**

- 位置：本目录。源码全部搬入，`source-hashes.json` **36 个文件逐字节校验通过**（mismatch=0 missing=0）。
- 上游 32 个 `.cs` **一个字节都没改**，我们的改动只新增文件。
- 编译验证：`dotnet build -p:BridgeEdition=Full -p:GameManaged=<游戏 Managed>`，**0 警告 0 错误**。
- **原样编译产物 289280 B，与游戏里正在跑的那份 Full DLL 同尺寸** —— 证明工程跟现装版本对得上，不是拿错源码。
- 工具链：.NET SDK 10.0.404 只提供 Roslyn；运行时引用全部来自游戏 Unity Mono，无第三方 NuGet 包。

**③ 新增 6 个工具（2 个文件）**

`src/MachineSandbox.cs` —— 机器沙盒：

| 工具 | 做法 |
| --- | --- |
| `machine_sandbox` | 列机器：进度/目标/状态/电池/每周期速度。只读 |
| `skip_night_machines` | 直接调游戏自带的 `DebugPanel.SkipNightMachine()` |
| `fast_forward_machines` | `one_cycle` / `nights`(1..30) / `complete` 三种模式 |
| `recharge_all_machines` | `PowerHelper.SetPowerSourceAt` 充满内置电池 |

`src/DebugActions.cs` —— 游戏自带调试动作表：

| 工具 | 做法 |
| --- | --- |
| `debug_actions` | 列 `CustomUIDebugPanel.entries`（category / name / 可调用） |
| `invoke_debug_action` | 按名（可按分类消歧）调那一条 |

**④ 查清并修正了三处一直传错/没注意的事实**

1. **`PlayerStore.FindAllItem(isOwn = true)` 默认只返回带 `IS_OWNED_TAG` 的物品**。之前所有"扫不到东西"的判断都要按这个参数重新理解。扫「店里实际有的」必须传 `false`。
2. **夜晚回调有三个，不止一个**：`onCycleEndEarlySlotItemFunc` / `onCycleEndSlotItemFunc` / `onCycleEndLateSlotItemFunc`。我们一直只调中间那个，等于漏掉早段和晚段逻辑。`PlayerStore.EndNight()`（L1705/1757/1763）和游戏自带的 `DebugPanel.SkipNightMachine()`（L274/279/283）都是三段。
3. **游戏里有现成的调试动作表，且可写**：`CustomUIDebugPanel.Add(category, name, action, tall, source, info)` 与 `Remove(source)` 都是 `public static`。**我们的模组可以往调试面板挂自己的动作**，比自建调试 UI 省事。

**⑤ 没抄的三项，理由写进 README**

`reload_mod_assets`（200+ 行、要反射私有字段并同步场景实例，而我们改 C# 有更强的 `reload_mod`）、`extract_sprites`（我们用不上图集）、`export_visual_catalog`（展示件）。

### 没完成（明确的遗留）

- [ ] **新 DLL 还没装**。`build\full\...\ModDevBridgeNext.dll`（304128 B）编好了，但换装会改变桥的内容哈希、需要玩家在 Modding 界面重新批准，**这一步只能人做**（AGENTS.md 第 4 条）。现装的是 289280 B 的上游原样 Full 版。
- [ ] **6 个新工具一次都没跑过**。全部只是编译通过 + 静态确认工具名进了 DLL。真机行为、返回结构、边界都未验证。
- [ ] `fast_forward_machines` 的 `complete` 模式钉进度后能否真的产出（走原版完成路径），未验证。
- [ ] 分选机（`DESEQUQUENCER_PROGRESS_TAG`，游戏自己拼写双 C）在 `complete` 模式下是否正确，未验证。
- [ ] `invoke_debug_action` 的条目清单没看过 —— 装上后第一件事应该是 `debug_actions` 全列一遍，看游戏到底给了什么。

### 下一步（按顺序）

1. 用户批准后换装 DLL → 重启 → `get_development_environment` 确认仍 `edition=full` → `tools/list` 确认 36 个工具。
2. `debug_actions` 全列一遍，把可用动作记进经验库（这是白捡的能力清单）。
3. 用测试档 slot 1 验 `machine_sandbox`（只读）→ `recharge_all_machines` → `fast_forward_machines mode=one_cycle`，拿真实机器看进度变化。
4. 验证通过后，回电玩项目用它把「刻录机两晚闭环」重跑一遍，确认比等两晚省事。

### 恢复手段

装新 DLL 前先备份现装的那份到 `build\installed-backup\`。要退回：游戏关着 → 换回备份 → 重启 → 确认工具数回到 30。
