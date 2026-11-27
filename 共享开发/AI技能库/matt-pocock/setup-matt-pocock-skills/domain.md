# 领域文档

工程类技能在探查代码库时，应当如何消费本仓库的领域文档。

## 探查之前先读这些

- 仓库根目录的 **`GLOSSARY.md`**，或者
- 仓库根目录的 **`GLOSSARY-MAP.md`**（若存在）：它指向每个上下文各一个 `GLOSSARY.md`。把与主题相关的逐个读一遍。
- **`docs/adr/`**：读涉及你即将动手那片区域的 ADR。多上下文仓库里，还要看 `src/<context>/docs/adr/` 里的上下文内决策。

这些文件若不存在，**静默继续**。不要指出它们缺失；不要提前建议创建。`/domain-modeling` 技能（经 `/grill-with-docs` 与 `/improve-codebase-architecture` 进入）会在术语或决策真正定下来时按需创建它们。

## 文件结构

单上下文仓库（大多数仓库）：

```
/
├── GLOSSARY.md
├── docs/adr/
│   ├── 0001-event-sourced-orders.md
│   └── 0002-postgres-for-write-model.md
└── src/
```

多上下文仓库（根目录存在 `GLOSSARY-MAP.md`）：

```
/
├── GLOSSARY-MAP.md
├── docs/adr/                          ← system-wide decisions
└── src/
    ├── ordering/
    │   ├── GLOSSARY.md
    │   └── docs/adr/                  ← context-specific decisions
    └── billing/
        ├── GLOSSARY.md
        └── docs/adr/
```

## 用术语表的词汇

你的产物给某个领域概念命名时（工单标题、重构提案、假设、测试名里），用 `GLOSSARY.md` 里定义的那个术语。不要漂向术语表明确避开的同义词。

你要的概念还不在术语表里，这本身就是信号：要么你在发明项目不用的说法（重新考虑），要么是真的缺口（记下来交给 `/domain-modeling`）。

## 标出 ADR 冲突

你的产物与已有 ADR 冲突时，明确摆出来，而不是悄悄覆盖：

> _与 ADR-0007（事件溯源订单）冲突，但值得重新讨论，因为……_
