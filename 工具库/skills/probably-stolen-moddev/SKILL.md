---
name: probably-stolen-moddev
description: 《深空当铺》（Probably Stolen）Unity/Mono C# 模组全流程开发、架构选型、原版 API 速查、跨模组兼容排雷与真机验证技能。凡涉及为《深空当铺》编写、设计、重构或审查模组代码（自定义物品/机器/雪碧图动画/鉴定品类/放大镜/交互纸张/空间站事件/Better Newspapers 配图/夜晚外出地点/顾客与立绘/配音/关系网/开局剧本/大型 DLC 与经济/排序架构/游戏内 UI/ES3 防坏档）、排查 ModLoader 零能力扫描或坏档、按标签检索工作区蒸馏知识库、反编译蒸馏工坊模组、跑五道合并门禁或冲突扫描时，必须调用本技能。
---

# 《深空当铺》（Probably Stolen）模组开发技能

本技能整合自 **7 批 74 个工坊标杆模组（约 60 万行反编译源码、42 份蒸馏档案）**、工作区 `reference/Assembly-CSharp/`（944 个原版类型逐项核对）、13 个经真实编译与 `ModScanner` 零能力验证的 C# 架构模板，以及本地双模组真机闭环实践。

## 1. 先查路由再动手（渐进式按需加载）

先根据任务只读对应专题或压缩速查卡，不要把所有参考一次性塞进上下文：

| 任务场景 | 先读哪个文件 / 跑哪条命令 |
| --- | --- |
| **省上下文速扫全工坊经验**（1.3 MB 原始档案 → 100 行压缩卡 + 禁抄红线） | 先读 `共享开发/模组制作经验库/DISTILL_DIGEST.md`<br>精准查卡片：`python 工具库/kb-tools/kb_pipeline.py query --domain <域> [--api <类>] [--trust L1,L2]`<br>总索引：`共享开发/模组制作经验库/CATALOG.md` |
| **开工定规范**：按模组类型查 12 条跨类型红线、禁忌、验收清单 | `references/mod-type-standards.md` |
| **大型玩法 / 超巨型 389 Patch / 生产链自动化 / 经济 / 排序 / 兼容红线**（DLC 总闸、200+ Patch 三件套、补丁分相、标准机器四件套、影子账本、防剧透排序、Appraisal Assistant 5 红线） | `references/large-mod-architecture.md` |
| **查原版一等接口**：`ModHelper`(145) / `ModHook`(44) / `GameItemFunc`(51) / `ModReflect` / 物品机器事件与实测坑 | `references/host-api-and-hooks.md` |
| **工程模式与防坏档**：ES3 写盘剥离、模态 UI 防穿透、分帧预算索引、回滚队列、开发桥接/热重载 | `references/architecture-patterns.md` |
| **鉴定 / 放大镜 / Paper JSON / 关系网 / 开局剧本** | `references/appraisal-and-paper.md` |
| **打烊后夜晚外出地点**（极简菜单 / 博士式摊位防丢货 / 全屏 CG 舞台 / 夜晚地图事件） | `references/afterhour-locations.md` |
| **立绘 / 高清相机租约 / 道具去边 / WAV 配音 / 机器 8 列雪碧图动画 / Better Newspapers 兼容** | `references/media-portrait-voice.md` |
| **工坊发布与 SteamUGC 上传**（独立进程、封面、白名单、可见性核对） | `references/workshop-publishing.md` |
| **批量反编译与持续蒸馏工坊模组**（筛选/下载/六问精读/零侵入增量打标） | `references/workshop-reverse-and-distill.md` |

## 2. 宿主环境与不可破硬底线

1. **运行目标**：Unity `2021.3.45f2` / Mono / `.NET Framework 4.7.2` / C# 9。引用一律取游戏当前 `Managed\` 目录（用 `ES3Types` 需引 `Assembly-CSharp-firstpass.dll`；用 `PixelPerfectCamera` 需引 `Unity.2D.PixelPerfect.dll`）。
2. **正式版零能力（`<Capabilities/>`）**：
   - `ModScanner` 扫的是 **IL 调用点**——哪怕在永不执行的分支里出现 `System.IO.File`、`HttpListener`、`Assembly.Load` 或运行期反射，也会标出能力。
   - 读写模组内文件或 `ModData` 目录走 **`ModHelper.ReadModFileText/Bytes` 与 `ModHelper.*ModDataFile*`**（白名单沙箱 API，经真实 `ModScanner` 实测零能力，包括 `ModDataInbox.cs`）；读原版私有静态字段走 **`ModReflect.GetStatic<T>(typeof(Target), "field")`**（零能力）。
   - 严禁调用带 `[ModRestricted("loader-target")]` 的 `ModLoader.SetModEnabled/ReorderMods/ApproveAndEnable`、`ModTrust.Approve` 或 `ES3.Save("modOrder"/"modTrust", ...)`（编译即 `Refused`、运行即抛 `UnauthorizedAccessException`），严禁拼 `char[]` 绕过扫描。
3. **扩展优先，替换最后**：
   - 先翻原版三大公开扩展面：**`ModHelper` 145 个 public static 方法**、**`GameItemFunc` 51 个委托属性**、**`ModHook` 44 个钩子事件**（完整签名见 `共享开发/模组制作经验库/03-原版接口速查.md`）。三处都找不到再写 Harmony `Prefix/Postfix`，尽量不写 IL `Transpiler`。
   - **绝不整段替换原版入口**（`RunNightlyChemicalSimulation`、`DecodeSaveItem`、`EndNight`、`SaveGame`）；改机器参数用 `TransientTagBoost.cs`（Prefix 暂存 + Postfix/Finalizer 双还原）。
   - 重载方法补丁必须显式传 `new Type[] { ... }`；跨模组顺序必须显式声明 `[HarmonyPriority]` / `[HarmonyBefore("mod.<id>")]` / `[HarmonyAfter("mod.<id>")]`（`ModLoader` 的 Harmony ID 带 `"mod."` 前缀）。
4. **简单直白 + 异常暴露**：
   - 选最少文件的清晰实现，不做无意义 `*Policy/*Seams/*Bridge` 机械分层；内部逻辑严禁空 `catch {}` 吞错，只在 Unity 帧回调与 Harmony 最外层捕获并 `Debug.LogError` 留堆栈。
   - 访问 `PlayerStore.Instance` 前必判 `PlayerStore.HasInstance`；静态 UI 与缓存引用在 `OnDisable`、`OnGameLoadedInit/Normal`、`HotReloadPrepare/Activate` 三处成对重置。
5. **物品、真伪与防剧透铁律**：
   - 判注册用 `DirectoryMaster.Has<GameItem>(id)` 并排除 `ItemSpawnerHiddenList.IsHidden(id)`，不用 `ItemSpawner.Spawn` 试探（只读探针用完必须 `finally { item.Destroy(); }`）。
   - 发放语义分明：玩家 `DirectoryMaster.Item(id, true)`，NPC `DirectoryMaster.Item(id, false)`；精确投放用 `GraphUtils.CanAccept` + `TryAccept`，引用比对 `item.parentInventory == targetInv`。
   - **真伪与成色唯一真相**：只认 **`ItemFeature.realCondition.identifier`**（绝不读显示名或 `fakeCondition`）；反之，**面向玩家的排序/整理功能严禁读取 `realCondition`**（防剧透真假货）。
   - 自定义品类只用 `EnableTag`，不往 `SetGameItemType` 写非原版类型（避免 `[Translation Error]`）。

## 3. 工作区标准闭环与门禁

```text
1. 建分支（main 不直接提交）：.\工具库\build\New-FeatureBranch.ps1 -Name feat/<域>-<短句>
2. 查标签知识 + 复用 13 个已实测零能力模板（工具库/templates/mod-patterns/）
   → 参考 assets/ModEntry.template.cs 与 assets/manifest.zero-cap.xml
3. 合并前过五道门禁：.\工具库\build\Invoke-PreMergeGate.ps1 -Mod <Arcade|Freya|All>
   （①开发版编译 ②-PublicRelease -Scan 零能力 ③离线真回归 ④conflict_scan.py --gate ERROR=0 ⑤工作区洁净）
4. 真机闭环（严禁触碰 Slot 0，只进 Slot 1，核对 saveSlot / worldId / 加载哈希）
   → 改完知识库或新蒸馏后跑：python 工具库/kb-tools/kb_pipeline.py build && audit
```

**本机 Shell 铁律**：仅安装了 Windows PowerShell 5.1（无 `pwsh`）。`.ps1` 保存为 UTF-8 with BOM，JSON 为 UTF-8 无 BOM；子脚本捕获输出用 `*>&1`。

## 4. 随包脚本与资产速用

- 静态透视第三方模组或 DLL：`python scripts/inspect_mod.py "<目录或DLL>"`
- 跨模组冲突扫描（`--gate` 门禁模式，退出码 2=ERROR / 3=WARN）：`python scripts/conflict_scan.py --dir <我方src> --dir <对照目录> --gate [--out report.md]`
- 零侵入全库打标/查询/原版源码验真/13 模板实测：`python 工具库/kb-tools/kb_pipeline.py build|query|audit|verify-templates`
- 零能力清单起点：`assets/manifest.zero-cap.xml` ｜ 标准入口骨架：`assets/ModEntry.template.cs`
