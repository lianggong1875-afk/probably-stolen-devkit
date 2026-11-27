---
name: probably-stolen-local-development
description: Local workspace entry pointer for Probably Stolen mods. Redirects to AGENTS.md and probably-stolen-moddev skill.
---

# 深空当铺本地模组开发入口（已收束至主技能）

> 本文件仅作工作区根目录快捷入口，完整架构、原版 API、工坊蒸馏经验与工具链已统一维护在主技能 `probably-stolen-moddev`（[工具库/skills/probably-stolen-moddev/SKILL.md](../工具库/skills/probably-stolen-moddev/SKILL.md)）与带标签总索引 [CATALOG.md](模组制作经验库/CATALOG.md)。

## 最小开工清单

1. **先建分支再查接口**：按 [AGENTS.md](../AGENTS.md) §5 用 `New-FeatureBranch.ps1` 建分支；用 `python 工具库\kb-tools\kb_pipeline.py query --domain <域>` 秒查带可信度标签的经验/模板/原版 API，再核对 `reference/Assembly-CSharp/`，不猜签名。
2. **简洁零能力实现**：优先 `ModHelper`（145 方法）/ `GameItemFunc`（51 委托）/ `ModHook`（44 事件），拒绝多余套娃与内部空 `catch {}`。
3. **五道合并门禁**：合并前必跑 `.\工具库\build\Invoke-PreMergeGate.ps1 -Mod <Arcade|Freya|All>`（编译 + `-PublicRelease -Scan` 零能力 + 回归 + 跨模组冲突扫描 + 工作区洁净）。
4. **真机只进 Slot 1**：核对 `saveSlot`、`worldId` 与加载哈希，更新对应项目 `PROGRESS.md`。
