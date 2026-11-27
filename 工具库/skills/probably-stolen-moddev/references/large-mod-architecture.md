# 大型玩法、经济、整理排序与多模组兼容架构专题

本参考浓缩自工坊第 3~6 批 30 万行标杆源码蒸馏：`Upper Levels`（6.4 万行/267 Patch）、`Claims & Uprisings`（8.9 万行/233 Patch）、`Exchange`（7 万行）+ `CorpPresence`（4.1 万行）、`Smart Inventory Arrange`（61 万订阅）、`Appraisal Assistant`（45 万订阅）、`Fence Career` / `夜间拍卖` / `災變日`、`Pen's QoL` / `DangpuPlus` / `Probably Extend Framework`、`Station Events`（1.4.0）。

## 1. DLC / 新区域单点总闸范式（Upper Levels）

不要新建 Unity 场景或另起存档结构。把"玩家是否在这片新区域"压成**唯一单点布尔总闸**：
```csharp
Difficulty current = Difficulty.Current;
bool inRegion = current != null && current.source == "my_region_id";
```
- **8 层叠加全部挂在这一个开关下**：店面用 sprite 名映射原地换皮、客群在 `AddClient` Postfix 就地改写、夜出地点用 `ModHelper.AddAfterhourLocation` 挂上、事件靠总闸分流。
- 关掉模式时对原版存档、物品目录、夜晚日程**零入侵**。

## 2. 200+ Patch 巨型模组不崩的三件套（Claims & Uprisings）

当补丁面超过几十处时，核心不是硬压 Patch 数量，而是**把判断与状态收敛到三件集中式基础设施**，让每个 Patch 文件退化成 20~30 行的"早退 + 转发"薄片：
1. **作用域查询租约（`IDisposable` 令牌）**：批量扫描全店或执行复合事务前开作用域，集中挂接查询缓存，退出自动释放，避免重复全图扫描。
2. **单点模态仲裁器**：所有自建面板、弹窗、快捷键竞争统一经过唯一仲裁器，不让多个面板各自抢 `EventSystem` 或 `Escape`。
3. **原子代际构建（Fork → 全校验 → Commit）**：加载或重建复杂运行时状态时，先在离线新实例上完整构建并通过完整性/环检测，全部通过才原子切换引用；中途失败直接丢弃新实例，旧状态不受污染。
- **别抄的部分**：不要把 300+ 文件平铺在单个目录里，也不要靠上百处分散的 `Is*` 布尔门替代统一门面。

## 3. 经济系统两条相反路线选型（Exchange vs CorpPresence）

| 维度 | 路线 A：影子账本（Exchange 范式，**优先推荐**） | 路线 B：内容经济（CorpPresence 范式） |
| --- | --- | --- |
| 原版价格侵入 | **零 Patch** `GameItem.GetCurrentValue` / `NegociationData`，只读原版事件冲击，自己维护行情账本 | 叠在原版买方/订购/交易链上，最后一步按档位改价 |
| 历史行情存储 | **只存日种子，按需回放 K 线**，存档体积恒定极小 | 存大量公司亲密度、订单状态与目录进度 |
| 存档风险 | 极低（只用 `ModHelper.SetModData` + `SaveGame` Postfix 提交共享池） | 重度 Patch 存档链，与其它存档/经济模组冲突面大 |
| 实物闭环 | 矿机/配送订单读取原版真实物品并按行情价结算，用 `ModHelper.AddCredit` 闭环 | 品牌货收录 → 攒亲密度 → 解锁许可/机器/配方 |

**若只需做"某品类限时涨跌价"**：更轻量的零 Patch 解法是造一条带 `NegociationData(itemType, percent, display, reason)` 的 `StoreEvent` 并调 `ActivateEvent`——原版 `GameItem.GetCurrentValue()` 会自动读取并叠加进 `marketMult`（Station Events 范式）。

## 4. 新玩法挂接原版三时段选型（职业 / 拍卖 / 灾难三例）

| 玩法形态 | 主挂载点 | 核心机制 | 注意事项 |
| --- | --- | --- | --- |
| **营业日经营/委托**（销赃职业） | `OnShutterOpenedEarly` + `OnGenerateCustomerLate` + `OnShutterClosedEarly` | 开门发纸条/货、白天访客谈单、关门弹面板派单 | 不用夜出地图；切勿把 297 个状态键散写成平铺 ModData（应收纳为结构化 JSON/紧凑编码） |
| **打烊后外场活动**（夜间拍卖） | `ModHelper.AddAfterhourLocation` + `OnReturningStoreLate` | 夜晚进外场竞拍，货记 `pending_deliveries`，回店后再发容器物品 | 发奖一定要等回店验证 `playerLocation == 0` 后幂等投递 |
| **跨日连锁事件**（災變日） | `StoreEventManager.OnDayStart` + `OnGenerateCustomerVeryLate` + `OnHandlingNightlyServicesLate` | 多环 `StoreEvent` 接力 + 改顾客池 + 老存档补前置纸条 | 切勿把 6000 行逻辑塞进单个 `.cs` 文件 |

## 5. 自动整理 / 排序的防剧透铁律（Smart Inventory Arrange，61 万订阅）

- **防剧透红线（`real-state-appraisal-ignored`）**：任何面向玩家的排序、高亮、自动排列、分组功能**严禁读取 `ItemFeature.realCondition` 或内部真实价值**——否则顺序本身就会暴露"这件是假货"，绕过鉴定玩法。只能读玩家已可见信息（显示名、品类、显示价值、`IsFeatureDiscovered()`）。
- **原地重排与整体回滚**：重排格子前缓存原坐标，使用 `modifiedShape` 校验碰撞，任一件放不下立即整体回滚至原状，并接管/清空撤销栈防止回撤出幽灵重叠。

## 6. 与 45 万订阅 `Appraisal Assistant` 共存红线

`Appraisal Assistant` 不暴露外部服务，而是靠 IL 指纹 + `Harmony.GetPatchInfo` 递归 + 自带 IL 解释器校验安全性。以下 **5 处原版方法**在其指纹红线表中，**严禁做破坏性 Prefix 替换**：
1. `AppraisalHelper.GetCorrectBaseValue`
2. `GameItem.IsTag`（若必须增量 Postfix，会影响它读取第三方自定义鉴定品类；自家走 `ModHelper.AddAppraisalItem` 注册品类时，判定委托改读 `item.modifiedState.dict` 可规避其解释器拒识）
3. `AppHandHelper` / `RibwichHelper` / `PlasmaHelper.GetCorrectValue`
4. `AppraisalItemType.Matches`
5. `AppraisalItemRegistry.Find`

## 7. 多功能合集模组的三种组织方式

| 方式 | 代表 | 适用场景 | 核心取舍 |
| --- | --- | --- | --- |
| **(a) 静态委托表** | `DangpuPlus`（103 项 + 熔断） | 大量常驻每帧/GUI 逻辑 | 无法热摘除单功能补丁 |
| **(b) 自描述 Patch 注册表** | `Probably Extend Framework`（`Func<MethodBase>` 延迟解析 + `Report()`） | 大量小补丁、需更新后秒定位失效点 | 排障最强，但 `UnpatchAll` 粒度偏粗 |
| **(c) 特性化补丁对象表** | `Pen's QoL`（方法级 `Unpatch(target, method)`） | 玩家运行中热开关单项功能 | 支持 N 个开关合并 1 条补丁 |

**避坑铁律**：做动态表安装时，**每条补丁的 `AccessTools.Method` 解析与安装必须各自包 `try/catch`（记录 `LogError`）**——绝不可把解析放在大循环外的实参求值里导致一条 `AmbiguousMatchException` 拖垮后续全部补丁；且 `Applied` 标记要对照 `Harmony.GetPatchInfo(target)` 真实状态核实。

## 8. 美术与角色模组三级形态

- **L1 单点装饰**（~3 文件）：`SceneManager.sceneLoaded` + 自建组件，冲突几乎为零。
- **L2 全店换脸**（~15 文件）：只换 `SpriteRenderer.sprite` 引用（**不 Patch `SpriteDict.GetSprite`**），替换前扫 `ModLoader.Mods` 避让其他模组贴图，还原必须条件式 `if (renderer.sprite == mine)`。
- **L3 完整角色模组**（~58 文件）：NPC + 差分立绘 + 外置多语言对话 + 夜出地点 + 配置项。
- **纯资源包极限形态**：空壳 DLL（`IMod` 三方法全空）+ `portrait-studio-provider.json`（零代码、零补丁冲突）。

## 9. 超巨型内容模组三根支柱（电子产品拓展，18.7 万行 / 389 Patch）

内容实体突破 200+、补丁破百时，先钉死三样基础设施，否则必反噬成难以维护的巨石：
1. **数据表 Schema + 加载期不变量证伪**：物品规格用 JSON 声明，加载期先做跨表引用与完整性校验，编译成运行期装配计划，避免“每件物品一个类”。
2. **有序阶段注册表（`ModuleLifecycle`）**：用 `(phase, order)` 登记子系统，**同阶段重复序号直接抛异常**，`Seal()` 之后禁止注册，将注册期与运行期物理隔离；每项包在边界异常捕获中，单子系统坏不拖垮一帧。
3. **补丁优先级分相（不互点对方 Harmony ID）**：同一原版钩子（如 `PlayerStore.GetNextClient`）可共存 10+ 补丁，按相位分配优先级：**正高优（如 `800`）写准备状态（`void`）→ 常规（`0`）→ 负低优（如 `-50~-100`）读准备结果做 `bool` 裁决**。
4. **多依赖 UI 缓存 + 关键补丁自卸安全网**：热路径缓存必须同时存结果与全部依赖（含当前语言 `CultureInfo.CurrentCulture.Name` 与技能加成），并在离场/读档清空；关键补丁失败须立刻 `UnpatchSelf()` 并 `AllowNativeSave()`，把“我方故障”与“玩家存档”解耦。
- **4 个绝不能抄的坑**：①勿生成全量 TypeRef 闭包反射契约表（2.1 万行死代码）；②勿写 1.2 万行单文件 `OnGUI` 巨石；③补丁安装器勿漏掉自己主程序集导致顺序表失效；④`critical` 安全网别传空数组形同虚设。

## 10. 生产链与自动化四形态（酿酒 / 化学 / 水培塔 / Station）

1. **原版标准机器最小四件套（免费白拿电量、涡轮与模块舱）**：
   ```csharp
   MachineHelper.SetupBatterySlot(batterySlot, machine);
   MachineHelper.SetupModuleBay(moduleInv, machine, null, new[] { "MODULE_TYPE_UNIVERSAL" });
   MachineHelper.SetupManualSlot(manualSlot);
   MachineryHelper.InitMachinery(machine, powerDraw, false);
   machine.EnableTag("STANDARD_MACHINE_TAG", false);
   machine.EnableTag("TURBO_BOOST_ELIGIBLE_TAG", false);
   ```
   配套**无产出退电**（记录 `debit`，本周期没产出把电退回电池）与 `SyncPowerTag` + `ValidateShapeState()`。
2. **四挂点铁律（零自建加工 tick）**：过夜批处理挂 `onCycleEndSlotItemFunc`；实时状态/贴图刷新挂 `onOneSecondItemFunc`（注意过夜化学模拟跑 360 次）；即时操作挂 `MachineryButton`；扩展原版机器走**克隆原版机器再改**或**链式包装原委托**，0 处整段替换。
3. **安全存档 ID 迁移**：物品改名/换贴图/改键优先在 `SaveManager.DecodeNodes` Prefix 改 `SaveItemNode`（须先过三重归属校验防误伤他模组），比逐个 `onLoaded` 修补更干净。
4. **事务防半截存档与 19 文件极简自动化**：多件原子增减用 `Busy` 集在事务期拦截 `CanSave`（回滚确认失败即锁定本局禁存）；小体量自动化不用重写原版机器，用一个工作区容器 + `GraphUtils.FindAllChildrenType<GameItem>` 扫内嵌原版机 + 自身 Tag 存开关 + `EndNight`/收获 Postfix 调度即可。
