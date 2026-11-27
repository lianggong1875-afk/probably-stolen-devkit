# 工坊模组逆向与持续蒸馏流水线

**什么时候读**：要读新的第三方工坊模组、为某个玩法寻找原版真相、或把新蒸馏的档案纳入工作区打标与检索库。完整档案目录见 `共享开发/模组制作经验库/19-工坊模组逆向与蒸馏.md` 与 `workshop-distill/`。

---

## 1. 六步闭环 + 零侵入自动打标（适配持续并发蒸馏）

```text
① 扫列表   python 工具库\workshop-tools\fetch_workshop.py list --pages 10
           → 缓存文件夹\workshop-scan\items.md（含订阅/体积/本地状态）
② 读简介   python fetch_workshop.py brief --ids-file 候选.txt --chars 620
           → 先读名称与简介再决定订阅
③ 订阅下载 工具库\build\WorkshopUploader\bin\Release\net10.0\WorkshopUploader.exe --fetch "@候选.txt"
           （同目录须有 steam_api64.dll + steam_appid.txt=4348910）
④ 反编译   python 工具库\workshop-tools\distill_prep.py --ids-file 候选.txt
           → 反编译到 缓存文件夹\decomp_ws\<id>\，透视报告到 _reports\<id>.txt
⑤ 精读归档 按 §3 六问写 共享开发\模组制作经验库\workshop-distill\<id>-<短名>.md
⑥ 打标收尾 python 工具库\kb-tools\kb_pipeline.py build && audit   ← 自动打标/更新 CATALOG/核验原版 API
           WorkshopUploader.exe --unsub "@候选.txt"              ← 退订避免堆积，清 缓存文件夹\decomp_ws\
```

**并发蒸馏安全设计**：打标系统完全外置（规则自动分类 + `工具库/kb-tools/tags-override.json` 侧车覆写），**绝不修改 `workshop-distill/*.md` 原文头部**。无论同时有几个会话往 `workshop-distill/` 写新档案，完成后跑一次 `kb_pipeline.py build` 即可自动收录、打上 6 维标签（含 `L1~L4` 可信度与反面教材预警）并刷新 `CATALOG.json` / `CATALOG.md`。

## 2. 筛选判据与第 6 批元教训

1. **优先看技术信号**：简介带 `API / 框架 / 兼容 / 让位 / 协议 / 脚本 / 缓存 / 归因` 的优先；**Patch 为 0 但功能复杂**（Zoh、StorageControlTerminal、Hatch Ranching）说明走通了纯公开 API，价值最高；几 KB 的小模组（SpriteHeal、Locale Error Fix）往往对应最深的原版 bug。
2. **行数会骗人，先看分布再精读**：全工坊最热门的 Smart Inventory Arrange（8.9 万行）里有 3.27 万行是冻结的旧引擎副本；Claims（8.9 万行）里 25 个巨兽文件占了 49% 行数，中位数仅 54 行。先看目录结构与文件规模分布，只定点精读骨架和核心缝。
3. **补丁多 ≠ 结构差**：Claims 233 Patch 但纯补丁代码仅占 1.4%，关键在把判断与状态收敛到少数基础设施里。
4. **强断言必须核对包内 `manifest.xml` 原文**：只看程序集名或 `<ID>` 容易误判（作者改名不改 ID，如 Claims & Uprisings 内部 ID 仍为 `yexiaoyang.InteriorExpansion`）。

## 3. 精读六问与验真纪律

每个模组只回答六件事：① 入口与生命周期 ② 数据结构与持久化 ③ Patch 语义（为什么必须拦） ④ 与原版边界 ⑤ 风险与限制（含"别抄"项） ⑥ 2~4 个带 `文件:行号` 摘录的可复用模式 + 对我们两个项目的可用点。

- 推断必标 `（推测）`，未真机验证单独注明；
- **任何原版 API 结论落库前必须对照 `共享开发/reference/Assembly-CSharp/` 核对**，并跑 `kb_pipeline.py audit` 自动验真；
- 若模组包含反面教材（整段替换入口、空 `catch {}`、绕过扫描、零鉴权 HTTP、重改存档链），务必在 `工具库/kb-tools/tags-override.json` 补一条 `must_avoid`，让 AI 检索时自动标红防误抄。

## 4. 蒸馏产出的五件套去向

1. **档案**：`共享开发/模组制作经验库/workshop-distill/`（原始源码证据）
2. **总索引与打标**：`CATALOG.json` / `CATALOG.md` + `tags-override.json`（自动打标与反面预警）
3. **经验沉淀**：API → `03`、坑 → `07`、架构 → `14`、类型规范 → `20`
4. **即插模板与工具**：零能力 C# 模板进 `工具库/templates/mod-patterns/`（须过 `kb_pipeline.py verify-templates`）；重复排查做成脚本进 `工具库/`
5. **技能提纯**：浓缩进 `工具库/skills/probably-stolen-moddev/`，用 `工具库/kb-tools/Sync-ModdevSkill.ps1` 同步至 `DSH_HOME` 并逐文件核验 SHA-256。

## 5. 工具链避坑速查

| 症状 | 修法 |
| --- | --- |
| `curl.exe` / PS 5.1 `Invoke-RestMethod` 访问 Steam 失败 | 改用 Python `urllib`（`fetch_workshop.py`） |
| pwsh 跑 Python 中文乱码 | `[Console]::OutputEncoding=[Text.Encoding]::UTF8; $env:PYTHONIOENCODING='utf-8'` 或写 UTF-8 文件 |
| `WorkshopUploader` 缺 `steam_api64.dll` | 从 `Managed\..\Plugins\x86_64\` 复制到 exe 同目录并放 `steam_appid.txt`（`4348910`） |
| `ilspycmd` 特性参数解码失败 | 结合 `inspect_mod.py` 报告与上下文源码推断，不凭猜写结论 |
| 已有积累规模（截至 batch-7） | **7 批 74 个模组 / 42 份档案 / ~60 万行源码**（高密度压缩速查见 `DISTILL_DIGEST.md`，总账见 `19-工坊模组逆向与蒸馏.md`，后续新批次由 `kb_pipeline.py build` 自动增量打标并刷新压缩卡） |
