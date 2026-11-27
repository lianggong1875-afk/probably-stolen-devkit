# 工具库｜长期工具索引

只放跨任务复用的长期工具，一次性脚本用完即删；缓存统一落 `缓存文件夹\`。

---

## build\ —— 构建、能力扫描、发布打包

默认游戏 Managed 目录：`D:\steam\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed`

| 文件 | 用途 | 常用跑法 |
| --- | --- | --- |
| `Build-Mod.ps1` | 统一构建入口（`Arcade` 走 csproj Release，`Freya` 走 Roslyn）。默认输出到 `build\latest`（`-PublicRelease` 输出到 `build\public`），直接覆盖不堆目录；支持 `-Scan` 顺手扫能力、`-Install` 一键同步 DLL/Textures/Books/manifest 到游戏目录 | `.\工具库\build\Build-Mod.ps1 -Mod Arcade`<br>`... -Mod Arcade -PublicRelease -Scan`<br>`... -Mod Arcade -PublicRelease -Install`<br>`... -Mod Freya` |
| `Test-ArcadeLoaderScan.ps1` | 用游戏真实 `ModScanner` 扫候选 DLL，验证无未声明能力（默认临时目录 `缓存文件夹\loader-scan`） | `.\工具库\build\Test-ArcadeLoaderScan.ps1 -Candidate <dll>` |
| `ArcadeLoaderScanProbe.cs` | 上一条的扫描探针源码 | 自动调用 |
| `New-ReleasePackage.ps1` | 统一零能力工坊封装脚本（支持 `-Mod Arcade|Freya|All`：公开版构建 + 封面 1280×720/<1MB 校验 + 扩展名白名单 + `ModScanner` 零能力校验） | `.\工具库\build\New-ReleasePackage.ps1 -Mod All` |
| `New-ArcadeReleasePackage.ps1` | 兼容入口（转发至 `New-ReleasePackage.ps1 -Mod Arcade`） | `.\工具库\build\New-ArcadeReleasePackage.ps1` |
| `Publish-Workshop.ps1` | 一键打包并上传 Steam 创意工坊（先跑 `New-ReleasePackage.ps1`，再编译并调用 `WorkshopUploader`；身份取自本机已登录 Steam 会话，不读密码/API key）。**默认不改线上简介**，要覆盖须显式加 `-SetDescription` | `.\工具库\build\Publish-Workshop.ps1 -Mod Arcade`<br>`... -Mod Freya -Id 3846263756 -Visibility 0 -Note "v0.7.1 ..."`<br>`... -Mod Arcade -VerifyOnly -Id 3846263529` |
| `WorkshopUploader\` | 工坊工具（Steamworks.NET 驱动 `SteamUGC`）。**上传**：CreateItem / StartItemUpdate / SubmitItemUpdate（`--dry-run` 验连通、`--verify` 查服务端物品详情）；**研读别人模组**：`--list-subs` 列已订阅+本地安装路径、`--sub/--unsub` 订阅与取消、`--download` 触发下载、`--fetch` 订阅+下载一条龙（id 列表支持 `@清单文件`） | 由 `Publish-Workshop.ps1` 自动编译调用；研读用 `.\工具库\build\WorkshopUploader\bin\Release\net10.0\WorkshopUploader.exe --fetch "@清单.txt"`（需同目录有 `steam_api64.dll` + `steam_appid.txt`） |
| `ilspycmd\` | 反编译游戏宿主或第三方模组 DLL | `.\工具库\build\ilspycmd\ilspycmd.cmd -p -o <out> <dll>` |

## workshop-tools\ —— 工坊模组研读流水线

| 文件 | 用途 | 跑法 |
| --- | --- | --- |
| `fetch_workshop.py` | 抓创意工坊列表（trend/mostrecent/toprated 多页）→ Steam 公开接口取名称/简介/订阅数/体积 → 落 `缓存文件夹\workshop-scan\items.md`；`brief` 子命令批量导出简介摘要用于筛选；`local` 对比本机已下载 | `python .\工具库\workshop-tools\fetch_workshop.py list --pages 10`<br>`... brief --ids-file 清单.txt --chars 620`<br>`... local` |
| `distill_prep.py` | 对工坊物品目录批量反编译（自动挑主 DLL，排除 Unity/Harmony 等运行时库）+ 调 `inspect_mod.py` 生成架构透视报告到 `缓存文件夹\decomp_ws\_reports\` | `python .\工具库\workshop-tools\distill_prep.py --ids-file 清单.txt` |
| `conflict_scan.py` | **跨模组静态冲突扫描**：把我们自己的源码目录和一批工坊模组的反编译目录放一起扫，报「同一原版方法被谁和谁同时 Patch」「同一注册 id 被重复注册」「同名 DLL 不同哈希」「各自声明的能力」。内置原版热点分级（全局显示层/存档链/过夜链 = ERROR/WARN）。退出码 2=ERROR、3=WARN，可当提交门禁 | `python .\工具库\workshop-tools\conflict_scan.py --dir 深空电玩\src --dir "深空当铺：可能是群友\src" --dir 缓存文件夹\decomp_ws --out 缓存文件夹\conflict-report.md` |

## regression\ —— 离线真测试

| 文件 | 用途 | 跑法 |
| --- | --- | --- |
| `Run-Regression.ps1` | 运行 `深空电玩\tests\` 下的真实行为与不变量测试（当前包含 `appraisal` 142 条 + `texts` 28 条，约 2 秒跑完） | `.\工具库\regression\Run-Regression.ps1`<br>`... -Only appraisal`<br>`... -List` |

## probes\ps-modbridge\ —— 游戏内 MCP 桥接与探针工具

前提：游戏运行中且已启用本地 `ModDevBridgeNext`（`http://127.0.0.1:18774/mcp`）。桥接工具说明见 [10-开发桥接器能力速查](../共享开发/模组制作经验库/10-开发桥接器能力速查.md)。

| 文件 | 用途 | 跑法 |
| --- | --- | --- |
| `snap-runtime.ps1` | 抓当前运行态基线（`get_game_status` / `inspect_runtime` / `get_development_environment`） | `.\snap-runtime.ps1` |
| `bc.py` | 调任意 MCP 工具首选（stdin 传参并剥 BOM，避免 PS 5.1 吞引号） | `'{"view":"overview"}' \| python bc.py query_ui --pretty` |
| `mcp_cli.py` / `mcp.mjs` | 底层 MCP 调用器 | `python mcp_cli.py <tool> '@args.json'` |
| `probe.py` | 自动 `open_test_session` → 加载最新 `ArcSummonProbe*.dll` → `call_probe` | `python probe.py "minspect arc_burner"` |
| `logs.py` | 读桥接日志缓冲；`--atlas <名>` 汇总缺失 sprite | `python logs.py burner` |
| `load_slot.py` | 按卡面文字进档并断言 `saveSlot`（避免点进 Slot 0） | `python load_slot.py --slot 1 --day 12` |
| `fix_index.py` / `slot_index.py` | 修复 `saves_index.es3` 预览漂移 / 临时收窄 `activeSaveSlotId` | `python fix_index.py show` |
| `quit_game.py` | 经「退出游戏」按钮正常退出（不强杀） | `python quit_game.py` |
| `native-input.ps1` | 真实 Windows 键鼠输入（支持 `key/type/click/mousemove/drag/info`，Unity 自绘 `PixelWindow` 面板拖拽专用） | `-Action drag -X 600 -Y 300 -ToX 1000 -ToY 300` |
| `probe_ui.py` / `probe_history.py` / `probe_inspect.py` / `verify_pane*.py` / `verify_expose.py` | UI 节点树与只读验证脚本 | `python <脚本>` |
| `src\ArcSummonProbe.cs` 等 | 探针源码（常用命令：`who/summon/ring/openall/refuse/expose/confront/minspect/feedlive/shot`） | 编后丢 `ModDevBridgeNext\modules` |

## img\ —— 图像处理

`pack-spritesheet.py`（机器动画 8 列雪碧图 `*_running__frames.png` 打包/校验/拆解）、`newsprint-tone.py`（Better Newspapers 风格 9 色新闻纸油墨 + 2×2 半色调网点转换器）、`gen_fz_art.py`（fz 网关辅助生图脚本）、`process-portrait.mjs`、`cutout-bg.mjs`、`trim.mjs`、`check-alpha.mjs`、`inspect-alpha.mjs`、`crop-eyes.mjs`、`recolor-eyes.mjs` 等，用于雪碧图、报纸配图、立绘和图标的像素裁切、透明边缘检查与缩放。

## templates\ —— 通用 C# 模组架构模板

- `templates\mod-patterns\`：从工坊标杆模组蒸馏出的 9 个即插即用零依赖 C# 模式模板（`ReadyChain.cs`、`HdCameraLease.cs`、`CustomInspectLens.cs`、`Es3SafePersistence.cs`、`ModalInputGate.cs`、`WavPcmDecoder.cs`、`SafeSpriteHelper.cs`、`SpriteSheetMachineAnim.cs`、`NewspaperArtHelper.cs`，详见 `templates\mod-patterns\README.md`）。

## skills\ —— 实用外部工具包与开发技能

- `skills\probably-stolen-moddev\`：《深空当铺》全流程模组开发技能（含 `SKILL.md`、`references/` 6 份专题手册（新增 `workshop-reverse-and-distill.md` 工坊蒸馏方法与 `mod-type-standards.md` 各类模组制作规范）、`assets/` 清单与入口骨架、`scripts/inspect_mod.py` + `scripts/conflict_scan.py`，已同步安装到 DSH 全局 skills 目录，哈希已核验一致）。
- `skills\portrait-studio\`：Portrait Studio 立绘工具包与 SDK（详见 `skills\portrait-studio\README.md`）。
- `skills\decomp-api\`：
  - `extract_api.py`：从反编译的 `.cs` 里秒级提取公开 API 清单（`python extract_api.py <file.cs> --summary`）。
  - `inspect_mod.py`：一键透视反编译目录或模组 `.dll` 的 `IMod` 入口、全部 HarmonyPatch、`ModHook/ModHelper/ModConfig/ModReflect` 调用与潜在权限点（`python inspect_mod.py <目录或DLL>`）。

## body-locator\ —— 长文本正文定位器

`code\locate.py`（模型权重在本地，不入 Git），须使用 `D:\0TavernWeave\tools\python314\python.exe` 在 `code` 目录内以相对路径运行，输出写到 `缓存文件夹\`。
