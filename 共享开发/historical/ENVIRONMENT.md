# 环境事实（本机实测）

> 建库时间：2026-11-23。所有路径为本机绝对值，换机时需重测。
> 证据等级：全部 `[实测]`。

## 游戏

| 项 | 值 |
|---|---|
| 名称 | Probably Stolen / 深空当铺 |
| App ID | 4348910 |
| 安装 | `D:\steam\steamapps\common\Probably Stolen` |
| 游戏内版本 | 0.46D（mod-audit.log 头行） |
| buildid | 26525552 |
| Unity | 2021.3.45f2 / Mono / .NET 4.x（据知识库；Managed 有 161 个 DLL） |
| Managed | `D:\steam\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed` |
| 0Harmony | 2.4.2.0（游戏自带，直接引用，不要打包） |
| 本地 Mods | `D:\steam\steamapps\common\Probably Stolen\Mods`（当前为空） |
| 工坊内容 | `D:\steam\steamapps\workshop\content\4348910\<workshopId>` |
| 存档 / 日志 | `C:\Users\qianxia1\AppData\LocalLow\Questing Goose Studio\Probably Stolen` |

## 工具链（已装好并做过产物级验证）

| 工具 | 位置 | 验证 |
|---|---|---|
| .NET SDK 10.0.404 | `C:\Program Files\dotnet\sdk\10.0.404` | `dotnet --list-sdks` ✓ |
| Roslyn csc | `C:\Program Files\dotnet\sdk\10.0.404\Roslyn\bincore\csc.dll` | 经 `dotnet <csc.dll>` 调用，编译冒烟通过 |
| ILSpy CLI 11.2.0.10050 | `D:\0TavernWeave\tools\dotnet-tools\ilspycmd.cmd` | 反编译 Assembly-CSharp 947 个 .cs / 3.4 s |
| 构建脚本 | `D:\0TavernWeave\probably-stolen\build.ps1` | 见下 |

**注意**：`ilspycmd` 装出来的是 `.cmd` shim，不是 `.exe`。`dotnet tool install --tool-path` 生成 `ilspycmd.cmd` + `.store/`。

**执行策略**：本机 PowerShell 为 Restricted/AllSigned，不能直接 `& .\build.ps1`。用：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "D:\0TavernWeave\probably-stolen\build.ps1" -Name <Mod> -Source <dir-or-file>
```

`build.ps1` 内部做的事：自动找最新 SDK 的 csc → 用参数数组拼引用（19 个基础引用，缺哪个报哪个）→ `/nostdlib+ /langversion:9.0 /codepage:65001` → 编译后**按字节验 `IMod\0` 标记**（游戏加载器的判据）→ 扫 process/network/steam/文件 IO 符号。脚本本身刻意保持纯 ASCII（PS 5.1 读无 BOM 文件按 GBK 解码，中文会坏）。

## 反编译快照

| 目录 | 内容 |
|---|---|
| `probably-stolen\decomp\Assembly-CSharp\` | 当前游戏程序集，947 个 .cs（**这是唯一可信的 API 事实来源**） |
| `probably-stolen\decomp\workshop-mods\<workshopId>\` | 18 个已装模组的反编译，仅用于兼容性分析 |

游戏更新后必须重跑反编译；旧快照不算数。

## 存档

| 槽 | 文件 | 说明 |
|---|---|---|
| 0 | `save_0.es3`（1.6 MB） | **玩家主档，不动**：第 51 天、credit 3416、开局"学徒"(apprentice)、`customName: null` |
| 1 | `save_1.es3` | **测试档 `AIDEV-TEST`**：第 1 天、credit 550、学徒开局，`preview_1.customName = "AIDEV-TEST"` |
| — | `save_0_day49_*.es3` / `save_0_day50_*.es3` | 玩家手工备份 |
| — | `save_0.es3.bak-20261122-213833` | 早前备份 |
| — | `saves_index.es3.bak-aidev-20261123-151412` | 改测试档名前由本 agent 备份 |

`saves_index.es3` 结构：`slotIdGenerator`（下一个新槽）、`preview_<slot>.customName`（显示名，游戏内点卡片 Title 可改）、`activeSaveSlotId`。

**规矩**：所有实验只在 slot 1 做。主档 save_0 及其历史备份任何情况下不写。

## 已验证的运行时状态（2026-11-23）

MCP 已连通并走完一轮完整闭环：

- 实例 `b967bca5…`，端点 `http://127.0.0.1:18774/mcp`，`edition=compatible`、`dynamicModules=false`、27 个工具
- 游戏 0.46D / Unity 2021.3.45f2，22 个工坊模组全部 `running=True trust=Match`（含本 MOD，caps=files,network）
- `get_game_status` → `ready=true, saveSlot=1, day=1, playerLocation=IN_STORE`
- `open_test_session` → `sessionId=e619…`，baseline `{slot:1, day:1, cash:550, inventory:8, contacts:21, mods:22}`
- `close_test_session` → `cleanupStatus=completed, sessionReleased=true, diff.changed=false`
- `query_items` → 11 个实例，12 个 root 全可用
- `list_configs` → 14 个模组的配置 ID 可读

操作细节与坑见 [MCP-PLAYBOOK.md](MCP-PLAYBOOK.md)。


## 已知环境坑

1. **dsh-dpx 环境收容**：本 agent 子进程的 `LOCALAPPDATA` / `HOME` 被重定向到 `...\eac-beta\localappdata`，而游戏的运行目录、玩家目录在真实 `C:\Users\qianxia1\...`。脚本里凡是要访问游戏侧路径，一律写绝对路径，不要用 `$env:LOCALAPPDATA` / `$HOME`。
2. **PowerShell 5.1 编码**：读无 BOM UTF-8 文件按 GBK 解码。改任何中文文本一律用编辑器/写入工具，不要用 `Get-Content -replace | Set-Content`。读文件要显式 `[System.IO.File]::ReadAllLines($p,[Text.Encoding]::UTF8)`。
3. **MCP 端点只在游戏运行时存在**，且 `%LOCALAPPDATA%\ModDevBridgeNext\.runtime\instances\*.json` 是唯一权威发现源。
