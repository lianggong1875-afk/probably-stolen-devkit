---
name: implement
description: "根据规格说明（spec）或一组工单（tickets）实施一项工作。"
disable-model-invocation: true
---

实施用户在规格说明或工单中描述的工作。

如果用户传入工单引用，先从工单系统取回该工单，并在开始前说明它的标题。引用有歧义就问。

尽可能在事先约定好的接缝上调用 Skill 工具加载 tdd。

定期跑类型检查、定期跑单个测试文件，最后完整跑一遍整套测试。

完成后，调用 Skill 工具加载 code-review 来评审这些工作。

把工作提交到当前分支。
