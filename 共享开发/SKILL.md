---
name: probably-stolen-local-development
description: Local Unity Mono mod development handbook for Probably Stolen (DeepSpaceArcade and DeepSpacePawn).
---

# 深空当铺本地模组开发指南

开始前先读根目录 [AGENTS.md](../AGENTS.md)，打开目标项目的 `PROGRESS.md`，按 [开发流程与编程规范](开发流程与经验.md) 执行。

## 开发工作流

1. **查接口再写代码**：先看 [原版接口速查](模组制作经验库/03-原版接口速查.md) 或 `reference/Assembly-CSharp/`，确认准确方法名、重载签名和生命周期，不猜 API。
2. **简洁实现**：直接写清晰内聚的 C# 代码，拒绝多余的 Policy/Seam 拆分和静默吞异常 `try/catch`。
3. **双编译 + 扫能力**：用 `工具库\build\Build-Mod.ps1` 验证开发版与 `-PublicRelease` 零能力版均 0 警告 0 错误，跑 `工具库\regression\Run-Regression.ps1`。
4. **实机验证**：只在 Slot 1 验证，核实加载哈希和实际表现；改完用 `git commit` 保存一个干净节点，并更新对应项目 `PROGRESS.md`。
