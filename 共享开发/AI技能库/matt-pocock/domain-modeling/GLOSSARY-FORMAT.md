# GLOSSARY.md 格式

## 结构

```md
# {Context Name}

{One or two sentence description of what this context is and why it exists.}

## Language

**Order**:
{A one or two sentence description of the term}
_Avoid_: Purchase, transaction

**Invoice**:
A request for payment for an **Order**, sent to its **Customer** after delivery.
_Avoid_: Bill, payment request

**Customer**:
A person or organization that places an **Order**.
_Avoid_: Client, buyer, account
```

## 规则

- **要有立场。**同一个概念存在多个词时，挑出最好的那个，其余列进 `_Avoid_` 之下。
- **定义要收紧。**最多一两句。定义它**是**什么，而不是它**做**什么。
- **在定义里把其他术语加粗。**定义提到另一个术语时，写成 `**Order**`，并由周围的句子说清两者怎么关联。
- **只收本项目上下文特有的术语。**通用编程概念（超时、错误类型、工具模式）即使项目用得再多也不该收。加一个术语前先问：这是本上下文独有的概念，还是通用编程概念？只有前者该收。
- **出现自然的聚类时，把术语归到子标题下**。若所有术语都属于同一块内聚的领域，平铺一个列表就行。

## 单上下文与多上下文仓库

**单上下文（多数仓库）：**仓库根目录一份 `GLOSSARY.md`。

**多上下文：**仓库根目录的 `GLOSSARY-MAP.md` 列出各个上下文、它们的所在位置，以及彼此如何关联：

```md
# Glossary Map

## Contexts

- [Ordering](./src/ordering/GLOSSARY.md): receives and tracks customer orders
- [Billing](./src/billing/GLOSSARY.md): generates invoices and processes payments
- [Fulfillment](./src/fulfillment/GLOSSARY.md): manages warehouse picking and shipping

## Relationships

- **Ordering → Fulfillment**: Ordering emits `OrderPlaced` events; Fulfillment consumes them to start picking
- **Fulfillment → Billing**: Fulfillment emits `ShipmentDispatched` events; Billing consumes them to generate invoices
- **Ordering ↔ Billing**: Shared types for `CustomerId` and `Money`
```

技能据此推断适用哪种结构：

- `GLOSSARY-MAP.md` 存在，就读它来找出各个上下文
- 只有根目录一份 `GLOSSARY.md`，就是单上下文
- 两者都不存在，在第一个术语敲定时按需创建根目录的 `GLOSSARY.md`

存在多个上下文时，推断当前话题属于哪一个。不清楚就问。
