# 深空桥接器 —— ModDevBridgeNext 本地源码工程

**这是什么**：游戏内 MCP 开发桥 `ModDevBridgeNext` 的**本地可编译源码工程**，以及我们在它上面加的改造。

**为什么放这里**：工坊只发一个 Compatible 版 DLL；Full 版（能加载探针、能热换模组 DLL）要自己编译。没有源码就没法改造——加一个工具就得写探针绕路。这个工程解决了这件事。

**不是**：别的桥。同游戏还有 `ProbablyStolen_MCPBridge`（工坊 `3834799226`，24 工具），我们对比后决定不换，只把它独占的能力在我们自己这个桥里实现。对比与理由见 [经验库 10 号](../共享开发/模组制作经验库/10-开发桥接器能力速查.md)。

---

## 来源与完整性

| 项 | 值 |
| --- | --- |
| 上游 | 工坊 `3835953432` · Unc1e · `ModDevBridgeNext` 0.6.0 |
| 取自 | `D:\steam\steamapps\workshop\content\4348910\3835953432\FullSource\` |
| 完整性 | `source-hashes.json` 36 个文件**逐字节校验通过**（mismatch=0 missing=0） |
| 未改动 | `src/` 下 32 个 .cs 全部是上游原文；我们的改动**只新增文件**，不改上游文件 |

校验方法（随时可复跑）：

```powershell
# 36 项 sha256 与 source-hashes.json 比对，全绿才说明上游源码没被动过
python - <<'PY'
import hashlib, io, json, os
root = r'D:\0深空当铺模组开发\深空桥接器'
h = json.load(io.open(os.path.join(root, 'source-hashes.json'), encoding='utf-8-sig'))
bad = [k for k, v in h.items() if hashlib.sha256(open(os.path.join(root, k.replace('/', os.sep)), 'rb').read()).hexdigest().lower() != v.lower()]
print('verified=%d bad=%d' % (len(h) - len(bad), len(bad)), bad)
PY
```

⚠️ 我们新增文件后，上游若发布新版，**先把我们的文件挪开再校验**，否则新增文件不在清单里不影响结果、但被改过的上游文件会被抓出来。

---

## 构建

前置：`.NET` SDK（本机 10.0.404）+ 游戏 `Managed` 目录。SDK 只提供 Roslyn 编译器，运行时引用全部来自游戏自带的 Unity Mono，**没有第三方 NuGet 包**。

```powershell
$dst = 'D:\0深空当铺模组开发\深空桥接器'
$man = 'D:\steam\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed'
dotnet build "$dst\ModDevBridgeNext.csproj" --configuration Release --artifacts-path "$dst\build\full" `
    "-p:GameManaged=$man" "-p:BridgeEdition=Full"
```

产物：`build\full\bin\ModDevBridgeNext\release\ModDevBridgeNext.dll`

- `BridgeEdition=Full` 定义 `BRIDGE_FULL`，编出 30+ 工具的 Full 版；不传或用 `Compatible` 编出兼容版。
- `Directory.Build.props` 是空壳，作用是**切断继承上级目录的 MSBuild 设置**，别删。
- 上游原样编译出来是 **289280 B**；我们加了 6 个工具后是 **304128 B**。尺寸对得上不代表行为对得上，见下面「验证」。

---

## 我们加了什么

6 个工具，两个文件。都不改上游那 32 个文件。

### `src/MachineSandbox.cs` —— 机器沙盒（4 个）

把「等两晚才知道机器做没做出来」压成一次调用。

| 工具 | 做什么 |
| --- | --- |
| `machine_sandbox` | 列机器：进度 / 目标 / 状态 / 有没有电池 / 每周期速度。只读，不写 tag、不触发回调 |
| `skip_night_machines` | 调游戏自带的 `DebugPanel.SkipNightMachine()`——原版的「跳过夜晚（机器部分）」 |
| `fast_forward_machines` | `mode=one_cycle` 走一遍夜晚三段回调；`mode=nights` 重复 1..30 次；`mode=complete` 先把进度 tag 钉到机器自己的目标再走一遍，让原版完成路径产出 |
| `recharge_all_machines` | `PowerHelper.SetPowerSourceAt` 把每台机器内置电池充满，顺带刷新原生电量贴图 |

三条实现上的取舍：

1. **优先用原版入口，不自己拼语义**。`skip_night_machines` 直接调 `DebugPanel.SkipNightMachine()`（游戏开发者自己写的 `public static`），比我们自己按 `EndNight` 抄一遍可靠。
2. **`PlayerStore.FindAllItem(isOwn)` 默认是 `true`**，会静默丢掉丢了 `IS_OWNED_TAG` 的物品。要扫「店里实际有的机器」就传 `false` 自己按类型筛。
3. **电池不认 UI 结构**。别人的做法是 `contentWindow.childElement → MachineryPanelRow → Panels[0].content → MachineryIntake → childItems[0]`，只对采用那套面板布局的机器成立。我们走 `GraphUtils.FindAllChildrenType<GameItem>(machine)` 找带 `power_source_item` + `power_source_item_energy` 的子物品，对机器类型不挑。

⚠️ **机器必须在世界里**。`ItemSpawner` 造出来的游离机器没有世界归属，`FindAllChildrenType` 是 0，机器回调也跑不出东西。

### `src/DebugActions.cs` —— 游戏自带调试动作表（2 个）

| 工具 | 做什么 |
| --- | --- |
| `debug_actions` | 列出 `CustomUIDebugPanel.entries`：分类、名字、能不能调 |
| `invoke_debug_action` | 按名字（可按分类消歧）调那一条 |

原理：游戏自带一个调试面板，`CustomUIDebugPanel` 里有个 `private static List<Entry> entries`，每条是 `{ category, name, Action }`。**这些动作全是游戏自己写的**，调它们等于用户自己点调试面板，比自己拼 API 稳。

- `entries` 和 `Entry` 都是 private，所以靠反射读。这是常规手段，桥本身就有 `reflection-external` 能力。
- **副产品**：`CustomUIDebugPanel.Add(category, name, action, tall, source, info)` 和 `Remove(source)` 都是 **public static**。也就是说**我们的模组可以往调试面板里挂自己的动作**，用 `source` 归组、`Remove(source)` 撤掉。要做模组内调试入口时这条路比自建 UI 省事。

### 明确没抄的三项，以及为什么

| 对方有 | 我们的判断 |
| --- | --- |
| `reload_mod_assets`（贴图/本地化/音频/书/物品定义热重载） | 200+ 行，要反射多个私有字段并同步场景实例；我们改的是 C#，收益不抵风险。**改 C# 有更强的 `reload_mod`**（换整个模组 DLL） |
| `extract_sprites`（从不可读图集切 PNG） | 我们用不上图集。真需要时再单独做 |
| `export_visual_catalog`（HTML 图鉴） | 展示件，无开发价值 |

---

## 装与切换（**需要人在场批准**）

`MOD ID` 和 `DLL 名` **必须保持 `ModDevBridgeNext`**：同 ID 本地副本优先于工坊副本，所以可以保留订阅、只换本地这份。

```powershell
# 游戏必须关着。先备份现装的那份。
Copy-Item 'D:\steam\steamapps\common\Probably Stolen\Mods\ModDevBridgeNext\ModDevBridgeNext.dll' `
          'D:\0深空当铺模组开发\深空桥接器\build\installed-backup\' -Force
Copy-Item 'D:\0深空当铺模组开发\深空桥接器\build\full\bin\ModDevBridgeNext\release\ModDevBridgeNext.dll' `
          'D:\steam\steamapps\common\Probably Stolen\Mods\ModDevBridgeNext\ModDevBridgeNext.dll' -Force
```

换完 DLL 后它内容变了，**加载器会判成需要重新批准**——这一步只能由玩家在 Modding 界面点，脚本不许代劳（也不能改写信任记录）。

装完验证（静态不看行为）：

```powershell
cd D:\0深空当铺模组开发\工具库\probes\ps-modbridge
python mcp_cli.py get_development_environment '{}'    # 应仍是 edition=full
& 'C:\Program Files\nodejs\node.exe' mcp.mjs tools --url http://127.0.0.1:18774/mcp   # 应出现 36 个工具
```

**恢复**：换回备份的 DLL，重启，确认 `edition` 与工具数回到 30。两个版本共用同一个端口，别同时装两份。

---

## 目录

```
深空桥接器/
  ModDevBridgeNext.csproj     上游原样
  Directory.Build.props       上游原样（空壳，用来切断上级设置继承）
  manifest.xml                上游 Full 版（含 4 项能力声明）
  source-hashes.json          上游源码校验清单
  BUILD.md                    上游构建说明
  src/                        32 个上游 .cs + 我们新增的 2 个（共 34）
    MachineSandbox.cs         ← 新增
    DebugActions.cs           ← 新增
  build/full/                 编译产物（artifacts）
  上游文档/                    工坊包的 README/SKILL/AI_GUIDE/references，只读参考
```

**别把 `obj`/`bin`/`.deps.json` 或 SDK 引用程序集复制进 MOD 目录**，也别把 Full 的 DLL 放回工坊目录（Steam 会覆盖）。

---

## 相关

- 桥有哪些工具、什么契约、怎么热换模组 DLL：[经验库 10 号](../共享开发/模组制作经验库/10-开发桥接器能力速查.md)
- 真机进档流程、探针命令表：[经验库 04 号](../共享开发/模组制作经验库/04-真机测试与探针.md)
- 客户端工具：[`工具库/probes/ps-modbridge/`](../工具库/probes/ps-modbridge/)
