---
name: probably-stolen-moddev
description: 《深空当铺》（Probably Stolen）Unity/Mono C# 模组全流程开发、架构选型、原版 API 速查、跨模组兼容排雷与真机验证技能。凡涉及为《深空当铺》编写、设计、重构或审查模组代码（自定义物品/机器/雪碧图动画/鉴定品类/放大镜/交互纸张/空间站事件/Better Newspapers 配图/夜晚外出地点/顾客与立绘/配音/关系网/开局剧本/大型 DLC 与经济/排序架构/游戏内 UI/ES3 防坏档）、排查 ModLoader 零能力扫描或坏档、按标签检索工作区蒸馏知识库、反编译蒸馏工坊模组、跑五道合并门禁或冲突扫描时，必须调用本技能。
---

# 《深空当铺》（Probably Stolen）模组开发技能

本技能整合自 **16 批 170 个工坊标杆模组（约 193 万行反编译源码、99 份 100% 精标档案）**、工作区 `reference/Assembly-CSharp/`（944 个原版类型逐项核对）、16 个经真实编译与 `ModScanner` 零能力验证的 C# 架构模板，以及 15 例真实跨模组冲突案例与本地双模组真机闭环实践。

## 1. 低 Token 渐进路由（严禁整篇硬读百 KB 大手册）

按需查最小颗粒度，不要一次性把 `03-原版接口速查.md`(101KB) / `07-踩坑台账.md`(55KB) / `19`(71KB) 整篇塞进上下文：

| 任务场景 | 先读哪个文件 / 跑哪条命令（附估算 Token） |
| --- | --- |
| **查任意接口 / 踩坑 / 症状（首选最省 Token）** | `python 工具库/kb-tools/kb_pipeline.py lookup "<关键词或类名>" --read`（直出精确行号小节正文 + 零能力模板 + 红线，单次 ~500 tok） |
| **极速扫 16 批 170 模组 12 条通用铁律** | 读 `共享开发/模组制作经验库/DISTILL_DIGEST.md` 前 22 行（~1.9k tok）；需扫 99 份全量独门招式再读全文（~7.9k tok） |
| **按域/可信度筛选知识与模板** | `python 工具库/kb-tools/kb_pipeline.py query --domain <域> [--trust L1,L2]`（默认紧凑单行 ~400 tok）；总路由见 `CATALOG.md` |
| **跨模组真实冲突排障（15 例故障症状）** | `共享开发/模组制作经验库/21-跨模组冲突案例库.md`（~2.8k tok） |
| **开工定规范**：按模组类型查跨类型红线、禁忌、验收清单 | `references/mod-type-standards.md` |
| **大型玩法 / 389 Patch / 生产链自动化 / 经济 / 排序 / 冲突红线** | `references/large-mod-architecture.md` |
| **查原版一等接口**：`ModHelper`(145) / `ModHook`(44) / `GameItemFunc`(51) / 贴图解析链 / 补货正门 | `references/host-api-and-hooks.md` |
| **工程模式与防坏档**：ES3 剥离、模态隔离、分帧索引、回滚队列、热重载 | `references/architecture-patterns.md` |
| **鉴定 / 放大镜 / Paper JSON / 关系网 / 开局剧本** | `references/appraisal-and-paper.md` |
| **打烊后夜晚外出地点**（五档阶梯 / 博士摊位 / 全屏 CG / 取消返店防扣次） | `references/afterhour-locations.md` |
| **立绘 / 高清相机租约 / 道具去边 / WAV 配音 / 8 列机器动画 / BN 兼容** | `references/media-portrait-voice.md` |
| **工坊发布与 SteamUGC 上传** | `references/workshop-publishing.md` |
| **批量反编译与自动化蒸馏流水线**（`distill_queue.py` / `DISTILL_SOP.md`） | `references/workshop-reverse-and-distill.md` |

## 2. 宿主环境与不可破硬底线

1. **运行目标**：Unity `2021.3.45f2` / Mono / `.NET Framework 4.7.2` / C# 9。引用一律取游戏当前 `Managed\` 目录（用 `ES3Types` 需引 `Assembly-CSharp-firstpass.dll`；用 `PixelPerfectCamera` 需引 `Unity.2D.PixelPerfect.dll`）。
2. **正式版零能力（`<Capabilities/>`）与洁净包红线**：
   - `ModScanner` 扫的是 **IL 调用点**——哪怕永不执行的分支出现 `System.IO.File`、`HttpListener`、`Assembly.Load` 或外部反射也会标出能力；**严禁把 `obj/`、`bin/`、`.ps1` 放进 mod 目录**（会判 `payload-file` 拒载）。
   - 读写模组内文件或 `ModData` 走 **`ModHelper.ReadModFileText/Bytes` 与 `ModHelper.*ModDataFile*`**（实测零能力，含 `ModDataInbox.cs`）；读原版私有静态字段走 **`ModReflect.GetStatic<T>`**（或 Harmony `___field` 注入，均零能力）。
   - 严禁调用 `[ModRestricted("loader-target")]`、`ES3.Save("modOrder"/"modTrust", ...)`、拼 `char[]` 绕扫描、或按 `<>c` 编译器生成名打补丁。
3. **扩展优先，热点礼让，绝不盲目旁路**：
   - 先翻原版三扩展面：**`ModHelper` 145 方法 / `GameItemFunc` 51 委托 / `ModHook` 44 事件**；三处都无再写 Harmony `Prefix/Postfix`，慎写 `Transpiler`。
   - **绝不整段替换原版入口**（`RunNightlyChemicalSimulation`、`DecodeSaveItem`、`EndNight`、`SaveGame`），也不在高频热点抢 `Prefix return false`；若接管状态机，必须照写原版状态 tag、走原版暂停与 `EmporiumOption` 音量。
   - **贴图解析链定论**：`GameItemElement.ResolveSpriteByName` 中 `useSpriteFromMod=true` 走 `LoadedMod.Sprites`（不进 `SpriteDict`）；故**物品图标自愈上移挂 `ResolveSpriteByName` Prefix（`HarmonyBefore`）**，切回原版必清 `useSpriteFromMod`；**立绘留 `SpriteDict.GetSprite`（`Priority.Low` 只认己键）**；角标首选 `GameItemFunc.getItemSpriteModifierForItemFunc`。
   - 重载补丁显式传 `new Type[] { ... }`；`HarmonyBefore/After("mod.<ID>")` 必须**逐字核对对方 `manifest.xml` `<ID>` 大小写**（写错不报错但静默失效）。
4. **简单直白、异常暴露与写盘安全**：
   - 拒绝机械分层；内部严禁空 `catch {}` 吞错；访问 `PlayerStore.Instance` 前必判 `PlayerStore.HasInstance`；静态 UI/缓存引用在 `OnDisable`、`OnGameLoadedInit/Normal`、`HotReloadPrepare/Activate` 成对重置。
   - 能存 `GameItem.state` tag 就不建全局表；`PlayerStore.storeService`/`PhoneClientDict` 等 ES3 序列化表必须在 `SaveGame`/`InitialSave`/`WriteSlotFile` 的 **Prefix 摘除 + Finalizer 还原**（模板 `SaveSafeStripPatch.cs`，勿放 Postfix）；旧档改名/清下线 ID 挂 `SaveManager.DecodeNodes` Prefix。
   - 机器临时加速用 `TransientTagBoost.cs`；**永久加成禁直改 `TOTAL_PERCENTAGE_*`**（拔模块会被 `ModuleHelper.ComputeModuleEffect` 冲零，须存私有 tag + Postfix 重算）。
5. **物品、真伪与防剧透铁律**：
   - 判注册用 `DirectoryMaster.Has<GameItem>(id)` 排隐藏；玩家 `Item(id, true)`，NPC `Item(id, false)`；造成品机器/工具走 `PreBuiltItemHelper`；`TryAcceptAllOrDestroy`/`EmporiumTryAdd`/`TryAddDirectSellingItemToTable` 满仓会**静默销毁**，须先 `GraphUtils.CanAccept` 并验 `item.parentInventory == targetInv`；`GameItem.GetSplitItem` **不复制私有 `state`**，有状态物品禁开 `mayStackItemFunc` 堆叠。
   - **真伪与成色唯一真相**：只认 **`ItemFeature.realCondition.identifier`**；反之排序/整理严禁读 `realCondition`；Tooltip 是渲染层可被美化模组折叠，唯一赝品提示必画在自控检视窗/估价单；自定义品类只用 `EnableTag`，不往 `SetGameItemType` 写非原版类型。

## 3. 工作区标准闭环与门禁

```text
1. 建分支（main 不直接提交）：.\工具库\build\New-FeatureBranch.ps1 -Name feat/<域>-<短句>
2. 先用 kb_pipeline.py lookup "<词>" --read 低耗查节 + 复用 16 个零能力模板（工具库/templates/mod-patterns/）
   → 参考 assets/ModEntry.template.cs 与 assets/manifest.zero-cap.xml
3. 合并前过五道门禁：.\工具库\build\Invoke-PreMergeGate.ps1 -Mod <Arcade|Freya|All>
   （①开发版编译 ②-PublicRelease -Scan 零能力 ③离线真回归 ④conflict_scan.py --gate ERROR=0 ⑤工作区洁净）
4. 真机闭环（严禁触碰 Slot 0，只进 Slot 1，核对 saveSlot / worldId / 加载哈希）
   → 改完知识库或新蒸馏后跑：python 工具库/kb-tools/kb_pipeline.py build && audit
```

**本机 Shell 铁律**：仅安装了 Windows PowerShell 5.1（无 `pwsh`）。`.ps1` 保存为 UTF-8 with BOM，JSON 为 UTF-8 无 BOM；子脚本捕获输出用 `*>&1`。

## 4. 随包脚本与资产速用

- 低 Token 极速定位/建库/验真/16 模板实测：`python 工具库/kb-tools/kb_pipeline.py lookup|query|build|audit|verify-templates`
- 静态透视第三方模组或 DLL：`python scripts/inspect_mod.py "<目录或DLL>"`
- 跨模组冲突扫描（41 热点，`--gate` 退出码 2=ERROR / 3=WARN）：`python scripts/conflict_scan.py --dir <我方src> --dir <对照目录> --gate [--out report.md]`
- 零能力清单起点：`assets/manifest.zero-cap.xml` ｜ 标准入口骨架：`assets/ModEntry.template.cs`
