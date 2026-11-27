---
name: domain-modeling
description: 构建并打磨项目的领域模型（domain model）。当讨论代码库术语、撰写或编辑 GLOSSARY.md、或记录或编辑 ADR 时使用。
---

# 领域建模

设计过程中主动构建并打磨项目的领域模型。这是一项*主动*的实践：质疑术语、构想边界场景，并在术语与决策定型的当口立刻把术语表和决策写下来。（只为取词而*读* `GLOSSARY.md` 不算本技能：那是一行就能做到的习惯，任何技能都能做。本技能用在你要改变模型的时候，而不只是消费模型。）

## 文件结构

多数仓库只有一个上下文：

```
/
├── GLOSSARY.md
├── docs/
│   └── adr/
│       ├── 0001-event-sourced-orders.md
│       └── 0002-postgres-for-write-model.md
└── src/
```

若根目录存在 `GLOSSARY-MAP.md`，说明该仓库有多个上下文。这份地图指出每个上下文位于何处：

```
/
├── GLOSSARY-MAP.md
├── docs/
│   └── adr/                          ← system-wide decisions
├── src/
│   ├── ordering/
│   │   ├── GLOSSARY.md
│   │   └── docs/adr/                 ← context-specific decisions
│   └── billing/
│       ├── GLOSSARY.md
│       └── docs/adr/
```

文件按需创建：只在有东西要写时才建。若 `GLOSSARY.md` 不存在，在第一个术语敲定时创建。若 `docs/adr/` 不存在，在需要第一份 ADR 时创建。

## 会话期间

### 用术语表对质

用户用的术语与 `GLOSSARY.md` 里既有的说法冲突时，立刻点出来。“你的术语表把 ‘cancellation’ 定义成 X，可你说的像是 Y。到底是哪个？”

### 把模糊说法磨准

用户用的词含糊或一词多义时，提出一个精确的规范词。“你说的是 ‘account’：指的是 Customer 还是 User？这两样是不同的东西。”

### 讨论具体场景

讨论领域关系时，用具体场景给它做压力测试。编出能探到边界情况的场景，逼用户把概念之间的界线说准。

### 与代码交叉核对

用户说出某样东西是怎么运作的，就去查代码同不同意。发现矛盾就摆上台面：“你的代码取消的是整张 Order，可你刚说部分取消是可行的。哪个是对的？”

### 就地更新 GLOSSARY.md

一个术语敲定，就地更新 `GLOSSARY.md`。不要攒着批量写：发生一件记一件。格式见 [GLOSSARY-FORMAT.md](./GLOSSARY-FORMAT.md)。

`GLOSSARY.md` 必须完全不沾实现细节。不要把 `GLOSSARY.md` 当成规格说明、草稿纸，或存放实现决策的仓库。它就是一份术语表，仅此而已。

### 少提 ADR

只有以下三条全部成立，才提议创建 ADR：

1. **难以回头**：以后改主意的代价不小
2. **脱离上下文会让人意外**：将来的读者会想“当初为什么这么做？”
3. **真实取舍的产物**：确实存在别的选项，而你是出于具体理由选了这一个

三条里缺任何一条，就跳过 ADR。格式见 [ADR-FORMAT.md](./ADR-FORMAT.md)。
