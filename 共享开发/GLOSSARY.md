# 深空当铺模组开发

《深空当铺》（Probably Stolen）两个模组的工作区：`深空电玩`（`deepspace_arcade`）与 `深空当铺：可能是群友`（`freya_cornell`）。这份术语表统一我们讨论玩法、原版接口和工程流程时用的词，避免同一个概念换着叫法。

## 玩法与世界

**成色**：
物品被玩家看见的实际品相等级。判定只由 **realCondition** 决定，显示名与 **fakeCondition** 都不参与。
_Avoid_: 品级、质量分、稀有度

**真伪**：
一件物品是真品还是赝品。结论同样只认 **realCondition** 的 `identifier` 前缀（`genuine` / `counterfeit` / `unusable` / `expired`）与缺陷计数。
_Avoid_: 真假度、正版判定

**品类**：
物品登记的 `itemTypes` 集合，即 **SetGameItemType** 写入的那套原版类型（`MACHINE`、`DOCUMENT`、`STORAGE` 等）。
_Avoid_: 分类、类型标签

**标签**：
挂在物品实例上的 `EnableTag` 标签，与 **品类** 是两套东西，同名不代表互通。
_Avoid_: tag 类型、特征

**鉴定**：
判定一件物品 **真伪** 与 **成色** 的玩法系统，扩展点见经验库 13。
_Avoid_: 估价、验货

**检视**：
放大镜式的逐项查看界面，与原版 `ItemFeature` 的公开层显示对应。
_Avoid_: 查看、预览

**交互纸张**：
可点按的 Paper JSON 界面（计算器、终端一类），不是静态文档贴图。
_Avoid_: 纸片、便签

**关系网**：
顾客与势力之间的科技树式关系面板。
_Avoid_: 好感度、社交树

**空间站事件**：
原版事件池里的限时事件（涨价、突发、访客上门、报纸新闻、日历预告），可被模组扩展。
_Avoid_: 随机事件、世界事件

**夜晚外出**：
打烊后离开店铺进入的探索场景与地图事件。
_Avoid_: 夜间模式、外出地图

## 原版接口契约

**realCondition**：
`ItemFeature` 上的真实条件对象。它是判定的**唯一**依据，也是模组绝不能读错的那个字段。
_Avoid_: 实际属性、隐藏条件

**fakeCondition**：
印在标签上的那层公开条件，玩家能看见。可以整层改写，因此永远不能拿来做判定。
_Avoid_: 假属性、显示条件

**DirectoryMaster**：
原版物品目录。判注册用 `DirectoryMaster.Has<GameItem>(id)`，发给玩家用 `Item(id, true)`，发给 NPC 用 `Item(id, false)`。
_Avoid_: 物品库、注册表

**EnableTag**：
给物品实例挂自定义标签。自定义品类只走它，绝不往 **SetGameItemType** 写非原版类型。
_Avoid_: 注册标签

**SetGameItemType**：
写物品的 **品类**。只能写原版已有类型，写非原版类型会刷蓝字 `[Translation Error]`。
_Avoid_: 设置类型

**ModHook**：
本工作区挂接原版生命周期的首选通道，优先于裸写 Harmony 补丁。
_Avoid_: 钩子框架、事件总线

**零能力**：
正式发布候选的硬要求：manifest 写自闭合 `<Capabilities/>`，源码层面不依赖任何外部能力，并通过真实 `ModScanner` 扫描。
_Avoid_: 无权限、白名单模式

## 工程流程

**开发桥接器**：
游戏内 MCP 桥，让智能体在真机里读写状态。源码在 `深空桥接器\`，缺工具就改那里。
_Avoid_: MCP 服务、调试接口

**探针**：
临时写进游戏、只为验证一个事实的 C# 代码，用完就删。
_Avoid_: 测试桩、调试代码

**真机验证**：
在真实游戏进程里确认行为。编译通过不算，日志正常也不算。
_Avoid_: 实机测试、跑一遍

**Slot 0 / Slot 1**：
存档槽位。Slot 0 是主档，绝不进入、修改或覆盖；真机测试只进 Slot 1，进档后先确认 `saveSlot` 与 `worldId`。
_Avoid_: 存档位、档案槽

**热重载**：
不重启游戏替换模组 DLL 的机制，静态引用在 `HotReloadPrepare` / `HotReloadActivate` 与读档路径 `OnGameLoadedNormal` 上成对重置。
_Avoid_: 动态加载、热更新
