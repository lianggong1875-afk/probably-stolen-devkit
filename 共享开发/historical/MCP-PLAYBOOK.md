# MCP 操作手册（Probably Stolen ModDevBridgeNext 0.6.0 Compatible）

> 全部命令都在 `D:\0TavernWeave\tools\ps-modbridge` 下执行。证据等级：`[实测]`（2026-11-23 全程跑通）。

## 0. 一句话状态

游戏必须**运行中且已载入存档**，MCP 才有完整能力。主菜单能连、能查 UI，但 `ready=false`，所有需要 session/world 的工具都会报 `game_not_ready`。

## 1. 连接与验活

```powershell
node mcp.mjs instances    # 发现实例 + GET /bridge/status 验活（比对 instanceId / releaseId）
node mcp.mjs status       # get_game_status：ready / saveSlot / day / worldId / scene
node mcp.mjs env          # get_development_environment：Managed 路径、模块目录、接口约定
node mcp.mjs tools        # 27 个工具 + 入参摘要
```

判断标准：`ready=true` 且 `saveSlot` 是你要的测试档。`ready` 只由 `ModHook.OnGameLoadedLate` 置位 —— **新游戏不算**，见第 5 节。

## 2. 输出控制（很重要）

MCP 的观测结果单次可以 20 KB 以上，大部分是重复的 `interpretation` / `output` / `observation`。工具默认已裁掉，另有三个开关：

| 开关 | 用途 |
|---|---|
| `--brief` | 去噪音字段，保留全部条目 |
| `--summary` | 只列 entries 的 name/role/text/uiRef/ctl（看"界面上有什么"） |
| `--find <文本>` | 只输出标签含该文本且带引用的节点（**拿 targetRef 最省事**） |
| `--save <路径>` | 完整结果落盘成 JSON，便于事后核对 |
| `--full` | 原始返回（含 content 副本），排查用 |

**参数一律写文件再传**：Windows PowerShell 5.1 向原生程序传参会剥掉双引号，`'{"a":1}'` 会被拆坏。用 `@args/foo.json`。

```powershell
node mcp.mjs tool query_ui "@args/ui100.json" --find "载入游戏"
```

## 3. UI 操作：两条完全不同的路径

这套界面混着两套实现，**必须先判断是哪一套**，否则"点了没反应"会浪费大量时间。

### 3.1 Unity UI（Button / TMP_InputField）→ `operate_ui` 可以点

`query_ui` 里 `role` 是 `Button`、`TMP_Dropdown`、`TMP_InputField` 的，走 EventSystem 合成事件，进程内派发，**不需要窗口在前台**。

```powershell
node mcp.mjs click "继续"              # 内部：query_ui → 唯一命中 → operate_ui click
node mcp.mjs click "载入最新" --index 0 # 命中多个时必须指定，否则拒绝（防误点）
```

返回 `dispatched.events = [pointer_down, pointer_up, pointer_click]` 只代表**派发成功**，不代表业务生效。必须再查一次 UI/status 确认。

### 3.2 自绘 UI（PixelWindow，`role=PointerClick`）与键盘 → 必须真实输入

这类节点轮询 `Input.GetMouseButton*`，合成事件到不了。另外 **ESC 这类键根本没有 MCP 接口**。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\native-input.ps1" -Action info
powershell.exe ... -Action key -Key ESC
powershell.exe ... -Action type -Text "AIDEV-TEST"
powershell.exe ... -Action click -X 1280 -Y 654      # -X/-Y 用 Unity 屏幕坐标（左下原点）
```

脚本行为：
- 自动把游戏窗口切前台，用的是 `AttachThreadInput` + ALT 轻敲（单纯 `SetForegroundWindow` 会被 Windows 前台锁忽略）；
- **拿不到焦点就拒绝发送并 exit 2**，绝不把按键打进用户正在用的窗口；
- `-X/-Y` 自动按 `GetClientRect` + `ClientToScreen` 换算，所以游戏窗口化/缩放后不用改命令；
- 支持键：ESC SPACE ENTER TAB BACKSPACE DELETE 方向键 F1-F4、若干字母（要加就改 `vkMap`）。

## 4. 会话与实验

```powershell
node mcp.mjs tool open_test_session '{}'          # 拿 sessionId + baseline 摘要
node mcp.mjs tool get_snapshot "@args/snap.json"  # 只读状态快照（fields 可选 slot/day/cash/inventory/...）
node mcp.mjs tool query_items "@args/items.json"  # 按 ID/名称/root/owned/stolen/违禁品等查实例
node mcp.mjs tool seed_scenario "@args/seed.json" # 造场景（现金/物品/modData），会改状态
node mcp.mjs tool diff_snapshots "@args/diff.json"# 对比两次快照
node mcp.mjs tool close_test_session "@args/close-session.json"  # 释放并输出 diff
```

- `session_id` 必须传给所有写操作；`world_id` 变了会话就失效（`session_world_changed`）。
- `close_test_session` 的 `cleanupStatus=completed` + `sessionReleased=true` + `diff.changed=false` 才算干净收尾。
- 会话清理**不撤销**全部副作用（原生 RNG、工厂计数器、第三方回调、保存写入）。详见 MOD 包 `references/RESULTS.md`。

## 5. 测试存档 AIDEV-TEST

| 项 | 值 |
|---|---|
| 槽位 | slot 1 → `save_1.es3` |
| 显示名 | `AIDEV-TEST`（写在 `saves_index.es3` 的 `preview_1.customName`） |
| 开局 | 学徒（apprentice），第 1 天，550 信用点 |
| 主档 | slot 0 → `save_0.es3`，第 51 天，3416 信用点，**不动** |

### 5.1 关键坑：新游戏不会让 bridge 变 ready

`BridgeRuntime.Ready = loaded && !Menu && PlayerStore.HasInstance && EmporiumEntry.Instance != null`，
其中 `loaded` **只**由 `ModHook.OnGameLoadedLate` 置位。开新档走的是 `OnNewGameStarted`，所以新档能玩、UI 能查，但 `ready=false`，`query_items` / `seed_scenario` 全部 `game_not_ready`。

正确姿势：**建好档之后回主菜单，再从「载入游戏」载入它**。

### 5.2 重命名存档（游戏内正统路径）

`LoadGameUI` 里点存档卡片的 `Title` 会激活 `Rename` 输入框（TMP_InputField），输入后回车提交，写 `SaveFiles.RenameSlot`。全程需要真实键盘：

```
点 Title → native-input -Action type -Text "名字" → native-input -Action key -Key ENTER
```

改完核对文件：

```powershell
[System.IO.File]::ReadAllText("...\saves_index.es3", [System.Text.Encoding]::UTF8) | ConvertFrom-Json
# 看 preview_0.customName / preview_1.customName
```

改名前先备份 `saves_index.es3`（本次已留 `.bak-aidev-*`）。

### 5.3 回主菜单的路径

游戏内没有"回主菜单"的 UI 按钮常驻，必须：`native-input -Action key -Key ESC` → 出现 `_EscapeMenu` → 点 `ButtonMainMenu`（`role=Button`，text 为空，用 `name_contains=ButtonMainMenu` 查）。直接 `query_ui` 默认查不到它（无标签），要带 `name_contains`。

## 6. 每次游戏重启后要重做

| 会变的东西 | 影响 |
|---|---|
| `instanceId` / `bridgeStartedAtUtc` | 旧实例的操作不能续；`instances` 会重新报 |
| `worldId` | 之前拿到的 `itemRef` / `uiRef` / `targetRef` 全部失效（5 分钟寿命 + world 绑定） |
| `targetRef` | 5 分钟过期，且 UI 一变就失效 |

所以：**每次操作前重新 query，不要复用上一轮的 ref**。

## 7. 只读优先原则

能用只读工具就别用写工具：
`get_game_status` → `query_ui` / `get_ui_details` / `raycast_ui` → `query_items` / `get_item_details` → `query_localization` / `inspect_runtime` / `get_game_logs` → `get_snapshot`。
写操作（`operate_ui` / `seed_scenario` / `set_config` / `enqueue_customer` / `operate_inspection`）必须先在测试档、先 `open_test_session`。

## 8. 常用参数文件

`args/` 下已备好：

| 文件 | 内容 |
|---|---|
| `ui40.json` / `ui100.json` | `query_ui` 分页 40 / 100 |
| `menu-search.json` | 带 `include_inactive` + `name_contains=enu` 找隐藏菜单 |
| `snap.json` | `get_snapshot` 基础字段 |
| `items.json` | `query_items` store 前 10 |
| `close-session.json` | 关会话模板（换 session_id） |
