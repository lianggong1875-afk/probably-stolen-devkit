# 工单系统：本地 Markdown

本仓库的工单与规格说明以 markdown 文件的形式存放在 `.scratch/`。

## 约定

- 一个功能一个目录：`.scratch/<feature-slug>/`
- 规格说明是 `.scratch/<feature-slug>/spec.md`
- 实施工单一张工单一个文件，放在 `.scratch/<feature-slug>/issues/<NN>-<slug>.md`，从 `01` 起编号，绝不用一个合并的工单文件
- 分诊状态记录在每份工单文件靠近顶部的一行 `Status:` 里（角色字符串见 `triage-labels.md`）
- 评论与会话历史追加到文件底部，放在 `## Comments` 标题下

## 当某个技能说“发布到工单系统”

在 `.scratch/<feature-slug>/` 下新建一个文件（需要时先建目录）。

## 当某个技能说“取出相关工单”

读所指路径处的文件。用户通常会直接给出路径或工单编号。

## 探路操作

`/wayfinder` 使用。**地图**是一个文件，每张工单对应一个**子**文件。

- **地图**：`.scratch/<effort>/map.md`（笔记 / 目前决策 / 迷雾的正文）。
- **子工单**：`.scratch/<effort>/issues/NN-<slug>.md`，从 `01` 起编号，正文里写问题。`Type:` 行记录工单类型（`research`/`prototype`/`grilling`/`task`）；`Status:` 行记录 `claimed`/`resolved`。
- **阻塞关系**：靠近顶部的一行 `Blocked by: NN, NN`。它列出的每个文件都是 `resolved` 时，工单才算解除阻塞。
- **前沿**：扫 `.scratch/<effort>/issues/` 里未关闭、未阻塞、未认领的文件；编号最小的胜出。
- **认领**：动手之前先设 `Status: claimed` 并保存。
- **解决**：把答案追加到 `## Answer` 标题下，设 `Status: resolved`，再把一条上下文指针（要点 + 链接）追加到 `map.md` 里地图的目前决策中。
