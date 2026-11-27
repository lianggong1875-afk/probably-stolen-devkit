# 范围外知识库

仓库里的 `.out-of-scope/` 目录存放被驳回的功能请求的长期记录。它有两个用途：

1. **组织记忆**：某个功能为何被驳回，这样工单关闭之后理据也不会丢
2. **去重**：当新进来的工单与既往驳回相符时，技能可以列出此前的决定，而不必把同一场争论再打一遍

## 目录结构

```
.out-of-scope/
├── dark-mode.md
├── plugin-system.md
└── graphql-api.md
```

一个**概念**一个文件，不是一个工单一个文件。请求同一件事的多个工单归到一个文件下。

## 文件格式

文件应当写得松弛好读，更像一份简短的设计文档，而不是数据库里的一条记录。用段落、代码样例和示例，把理据讲清楚，让第一次碰到的人也能用得上。

```markdown
# Dark Mode

This project does not support dark mode or user-facing theming.

## Why this is out of scope

The rendering pipeline assumes a single color palette defined in
`ThemeConfig`. Supporting multiple themes would require:

- A theme context provider wrapping the entire component tree
- Per-component theme-aware style resolution
- A persistence layer for user theme preferences

This is a significant architectural change that doesn't align with the
project's focus on content authoring. Theming is a concern for downstream
consumers who embed or redistribute the output.

```ts
// The current ThemeConfig interface is not designed for runtime switching:
interface ThemeConfig {
  colors: ColorPalette; // single palette, resolved at build time
  fonts: FontStack;
}
```

## Prior requests

- #42: "Add dark mode support"
- #87: "Night theme for accessibility"
- #134: "Dark theme option"
```

### 文件命名

给概念起一个简短、能说明问题的 kebab-case 名称：`dark-mode.md`、`plugin-system.md`、`graphql-api.md`。名字要足够可辨，让人浏览目录时不打开文件也能明白被驳回的是什么。

### 写理据

理据要有实质内容：不是“我们不想要这个”，而是要讲清为什么。好的理据会引用：

- 项目范围或理念（“本项目专注于 X；主题化是下游的事”）
- 技术约束（“支持这个需要 Y，而 Y 与我们的 Z 架构冲突”）
- 战略决策（“我们选择用 A 而不是 B，因为……”）

理据要耐久。不要引用临时状况（“我们现在太忙”）；那不是真正的驳回，只是延后。

## 何时检查 `.out-of-scope/`

分诊期间（第 1 步：收集上下文），读 `.out-of-scope/` 下所有文件。评估新工单时：

- 检查请求是否与已有的范围外概念相符
- 匹配按概念相似度，不按关键词：“night theme” 与 `dark-mode.md` 相符
- 若匹配，向维护者指出：“这与 `.out-of-scope/dark-mode.md` 相似。我们此前驳回过，理由是 [reason]。你的看法还是这样吗？”

维护者可以：

- **确认**：把新工单追加到既有文件的“Prior requests”列表，然后关闭
- **重新考虑**：删除或更新该范围外文件，工单走正常分诊流程
- **不同意**：两个工单相关但不同，走正常分诊流程

## 何时写入 `.out-of-scope/`

只有当**enhancement**（不是 bug）被作为 `wontfix` *驳回*时才写。这条对 enhancement 类 PR 与对工单完全一样：被驳回的 PR 也记录在这里，免得同一个请求又披着新代码回来。

若某项是因为**已经实现**而作为 `wontfix` 关闭，**不要**写到这里。那是一个已建成的功能，不是被驳回的；把它记下来会用假驳回污染去重检查。这种情况下，关闭评论应指出该功能已经在哪里。

流程如下：

1. 维护者判定某个功能请求超出范围
2. 检查是否已存在相符的 `.out-of-scope/` 文件
3. 若有：把新工单追加到“Prior requests”列表
4. 若无：新建文件，写入概念名、决定、理据，以及第一条既往请求
5. 在工单上发评论，说明决定并提到该 `.out-of-scope/` 文件
6. 给工单打上 `wontfix` 标签并关闭

## 更新或删除范围外文件

若维护者改变了对某个此前已被驳回的概念的看法：

- 删除该 `.out-of-scope/` 文件
- 技能不需要重开旧工单；它们是历史记录
- 触发这次重新考虑的新工单走正常分诊流程
