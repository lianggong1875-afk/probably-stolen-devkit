# 工坊 3831174434 蒸馏档案 —— 鉴定规则 / Appraisal Rules

| 项 | 内容 |
| --- | --- |
| id | `3831174434`；manifest `<ID>appraisal_rules</ID>` `<Name>鉴定规则 Appraisal Rules</Name>` v1.2.0，作者 `YUKI`，`<Prerequisites />`（空）、`<Capabilities />`（**零能力**） |
| 反编译目录 | `缓存文件夹\decomp_ws\3831174434`（14 文件；13 个 `.cs` = **1,104 行** + 1 个 `.csproj` = 21 行，对得上任务书的"14 文件 / 1,104 行"） |
| 为什么值得读 | 一个 **1,104 行、零 Harmony、零能力**的模组，把"鉴定估价"这件玩家最痛的事做成了**可勾选的规则清单**，而且**没碰鉴定助手（45 万订阅）的任何一条指纹红线方法**。它证明了：在《深空当铺》里加一层玩法，**可以不写一行 Patch** |

---

## 1. 头三行已在上表；补三条定性

1. **它不是"改鉴定规则"，是"给鉴定服务面板加一算盘"**：不改原版算式、不注册品类、不改成色判定，只在估价面板旁边挂一扇"特征清单"，勾完把合计值填进原版输入框。
2. **它的兼容成本是零，因为它的集成面是 4 个只读调用**（§3.2）。全库 `Harmony` / `.Patch(` / `HarmonyPatch` **零命中**。
3. **它是"代码即数据"的**：三个品类的规则写死在 `RuleCatalog.cs` 里（106 行），玩家改规则只能改代码/等作者加 —— 这是它最大的局限，也是它 1,104 行就够的原因（§2.2）。

---

## 2. 规模与结构

### 2.1 14 个文件 → 行数 → 职责

| 文件 | 行数 | 职责 |
| --- | ---: | --- |
| `ProbablyStolen.AppraisalRules\AppraisalPanelDriver.cs` | **555** | **全部 UI**：MonoBehaviour 轮询、建面板、3 列网格、勾选、算式明细、三个按钮。占总行数 50.3% |
| `RuleCatalog.cs` | 106 | 三个品类的规则数据（唯一"内容"文件） |
| `ModEntry.cs` | 108 | `IMod` 入口：3 个 `ConfigEntry` + 2 个关系网升级 + 驱动 GameObject 生命周期 |
| `RuleSet.cs` | 83 | 一套规则的根：基价/最低价/组/派生委托 + `Calculate` |
| `Selection.cs` | 78 | 勾选状态：`Dictionary<组, HashSet<选项>>`，单选组语义 |
| `PointerSwallow.cs` | 43 | 12 个事件接口全空实现——**吃掉鼠标事件**，防面板点穿到身后的 3D 物品 |
| `Estimate` / `RuleGroup` / `Text` / `OptionCell` / `RuleOption` | 22/22/22/21/17 | 结果对象 / 组定义 / 本地化包装 / 单格点击转发 / 选项定义 |
| `Contribution` / `GroupKind` / `AssemblyInfo` / `*.csproj` | 8/7/12/21 | 明细一条（key+value）/ `Single\|Multi` 枚举 / 样板 / 工程文件 |

### 2.2 1,104 行怎么做到的

- **一半是 UI**（555 行）——它抄的是原版 `CustomUIBuilder` 的声明式布局，不是手搓 `OnGUI`。真正"业务"只有 **`RuleSet.Calculate` 35 行 + `RuleCatalog` 106 行 + `Selection` 78 行 ≈ 220 行**。
- **规则是纯数据**：一个品类 = 若干 `RuleGroup( key, Single|Multi, showValues, params RuleOption[] )`，不需要表达式解析器、不需要 JSON schema、不需要反射。
- **不需要"检测别的模组"**：不 Patch、不反射别人的类型，所以没有一行兼容代码。
- **红线方法的代价是"零"**：它只**调用**两个原版方法（不是 Patch），鉴定助手的指纹闸门（IL 哈希 + `Harmony.GetPatchInfo`）**永远看不见它**（§3.1）。

---

## 3. 它怎么做这件事

### 3.1 它一个方法都没 Patch —— 红线方法逐条核对

| 红线方法（Appraisal Assistant 指纹表） | 它有没有 Patch | 有没有**调用/读取** | 证据 |
| --- | --- | --- | --- |
| `AppraisalHelper.GetCorrectBaseValue` | ❌ 无 | ✅ **调用**（"自动估价"按钮） | `AppraisalPanelDriver.cs:514` |
| `AppraisalItemRegistry.Find` | ❌ 无 | ✅ **调用**（`Find(GameItem)` 拿品类） | `:180` |
| `GameItem.IsTag(string)` | ❌ 无 | ❌ 全库零命中 | — |
| `AppHandHelper.GetCorrectValue` / `RibwichHelper.GetCorrectValue` / `PlasmaHelper.GetCorrectValue` | ❌ 无 | ❌ 全库零命中 | — |
| `AppraisalItemType.Matches` | ❌ 无 | ❌ | — |
| （附带）`RibwichHelper.HIGH_QUALITY_FACILITY_NUMBERS` / `LOW_…` | — | ✅ **读静态数组**（速查行） | `:405-406` |

**结论：它没有被鉴定助手判为冲突的可能。** 助手的检测是两道——① 方法体 IL 哈希（只有"改"才会变）② `Harmony.GetPatchInfo` 沿依赖图递归（只有"挂"才看得见）。**调用既不改方法体也不登记补丁**，所以两层都抓不到它。全库 `Harmony` 零命中是硬证据。

> 反过来讲，这条对照表本身就是一条可复用的判断法：**"我给 `X` 挂了补丁吗"** 与 **"我给 `X` 传了实参吗"** 是两件完全不同的事，前者触发兼容闸门，后者不会。

### 3.2 它的全部集成面（8 个，无一个会改原版行为）

| 原版 API | 用法 | 位置 |
| --- | --- | --- |
| `InspectionUIManager.Instance.appraisalPanel` / `.currentInspectingItem` | 判断"估价面板开着" + 拿到正在检视的货 | `:132-139` |
| `InspectionUIManager.Instance.apprisalServiceInputField`（原版就是这个拼写） | 填价：`.text = value.ToString()` | `:525-527` |
| `InspectionUIManager.Instance.ChangeAppraisalServicePrice(0)` | 填完触发原版的刷新/校验 | `:528` |
| `AppraisalItemRegistry.Find(GameItem)` | 物品 → `AppraisalItemType.id` → 找规则集 | `:180-181` |
| `CustomUIManager.Instance.BuildInto(root, false, "appraisal_rules")` | 声明式建 UI | `:341` |
| `CustomUIBuilder.ClearContainer` / `AddToggle` / `AddGrid` / `AddHeader` / `AddButton` | 布局 | `:340-397` |
| `ModHelper.AddNetworkUpgrade` / `SetNetworkTabLabel` / `IsNetworkUpgradeUnlocked` / `RemoveNetworkUpgrades` | 关系网解锁 | `ModEntry.cs:96-105`、`:155,167` |
| `ModHelper.GetLocalized` | 三语 CSV 取词 | `Text.cs:9-12` |

⚠️ 一个新品类**不需要**注册进 `AppraisalItemRegistry`：它按 `type.id` **查表**，表里没有这个 id 就静默不显示（`RuleCatalog.Find` 返回 `null` → 面板不建，`AppraisalPanelDriver.cs:181-184`）。所以**它对"别的模组注册的鉴定品类"零感知、零干扰**。

### 3.3 "规则"是什么形状：三层纯数据 + 一个委托

```
RuleSet( typeId, basePrice, minPrice, RuleGroup[], Func<Selection,IEnumerable<Contribution>> derived )
  └─ RuleGroup( key, GroupKind.Single|Multi, showValues, RuleOption[] )
       └─ RuleOption( key, value, fixedPrice? )        // value 可为负
Selection  ← Dictionary<组key, HashSet<选项key>>；单选组互斥
Estimate( Total, Parts[Contribution], IsFixed, Clamped )
```

- **`value` 是绝对加减量，不是百分比**（Ribwich 基价 150，双份肉 +800）。
- **`fixedPrice: true` 的选项短路整套算式**：勾上"已开封/仿品"直接给 40，和别的特征无关（`RuleSet.cs:34-46`）。UI 上这时会**锁掉其它格**（`Refresh()` 里 `interactable = !flag || option.InFixedGroup`，`:427-436`）。
- **`derived` 委托负责"组合套装奖励"**：单组勾选算不出来的组合（碳纤维+EMG+/完整+装甲+刀刃 → 战术套装 +100）写在委托里（`RuleCatalog.cs:53-64`）。
- **`minPrice` 是地板夹取**：Ribwich 40 / 手 50 / 燃料匣 90，低于地板就 `Clamped` 并在明细里加一句"已按下限"（`RuleSet.cs:65-66`、`:474-477`）。

### 3.4 三个内置品类的真实数值（这就是全部"规则内容"）

| 品类 | 基价/地板 | 组（Single=M，Multi=多） | 关键数值 |
| --- | --- | --- | --- |
| `ribwich` 肋明治 | 150 / 40 | 状态 M（普通 0、已开封 **40 定值**、仿品 **40 定值**）；特征 M（双份肉 +800、豪华 +300、首批 +80、月底 +50）；工厂 M（标准 0、优质 +50、劣质 −50） | 无 `derived`；UI 额外显示年份区间 `425/429/432/438` 与优质/劣质工厂号（硬编码在 `:404`） |
| `hand` 机械手 | 100 / 50 | 材质 M（铝 0、碳 +50、金 +200）；接口 M（EMG 0、EMG+ +45、神经 +120、未标注 −60）；成色 M（完好 0、磨损 −35、损坏 −85）；附件 多（装甲 +50、刀刃 +80） | `derived`：战术套装 +100、豪华套装 +200 |
| `plasma_fuel` 等离子燃料匣 | 300 / 90 | 外壳 M（洁净 0、磨损 −45、重磨 −90）；缺陷 多（破裂 −150、螺丝外露 −30、加固 +60）；颜色 M（标准 0、劣质 −150、优质 +150）；封条 M（不显示数值，仅作 `derived` 条件） | `derived`：产线封条分档 +60 / +120（两处）/ **+750**（点火实验室，需洁净无裂无露、无加固、优质色） |

### 3.5 和原版 `AppraisalItemRegistry` / `AppraisalItemType` 的关系：**只读消费者，不注册不覆盖**

- `AppraisalItemRegistry.Find(item)` → `AppraisalItemType`，只取 `.id` 当查表键（`:180-181`）。
- **不 `AddAppraisalItem`**（全库零命中），**不写 `getCorrectValue` 委托**，所以"覆盖已有品类公式"这件事它根本没做。
- 原版算式仍然是唯一权威：只有玩家点"自动估价"时它才去问 `AppraisalHelper.GetCorrectBaseValue(item)`，**并且拿到的值只用于填格，不参与自己的规则引擎**（`:510-520`）。两条路互不污染。

---

## 4. 关键代码摘录

### 4.1 算式本体（`RuleSet.cs:32-67`，原方法 36 行；下面省掉累加循环 11 行）

```csharp
public Estimate Calculate(Selection selection)
{
	foreach (RuleGroup group in Groups)
	{
		foreach (RuleOption option in group.Options)
		{
			if (option.FixedPrice && selection.Has(group.Key, option.Key))
			{
				return new Estimate(option.Value, new List<Contribution>
				{
					new Contribution(option.Key, option.Value)
				}, isFixed: true, clamped: false);
			}
		}
	}
	List<Contribution> list = new List<Contribution>();
	int num = BasePrice;
	// ……逐组逐项累加（:49-59，此处省略 11 行）：option.Value != 0 且已勾选 → num += value，并记一条 Contribution……
	foreach (Contribution item in derived(selection))
	{
		num += item.Value;
		list.Add(item);
	}
	bool flag = num < MinPrice;
	return new Estimate(flag ? MinPrice : num, list, isFixed: false, flag);
}
```

三段顺序不能换：**定值短路 → 逐项累加 → 派生项 → 地板夹取**。返回的 `Parts` 直接就是 UI 明细的数据源（`Breakdown`，`:461-479`），**算与展示同源**，不存在"面板显示的和填进去的不一致"。

### 4.2 面板的闸门与自毁（`AppraisalPanelDriver.cs:101-121` + `:128-184` 节选）

```csharp
private void Update()
{
	if (failed) { return; }
	try { Tick(); }
	catch (Exception ex)
	{
		failed = true;
		log?.Error((object)("AppraisalRules panel stopped after an error: " + ex));
		RemovePanel();
	}
}
```

两个要点：① **异常一次即自我停用**（`failed` 闩 + `Error` 带堆栈 + 拆面板），不刷日志、不每帧重试；② `Tick()` 的闸门（`:132-147`，本处省略）是"原版 `appraisalPanel.activeInHierarchy` 且有 `currentInspectingItem`"，**物品一换就 `RemovePanel` 再重建**（`:176-183`）。

---

## 5. 踩坑与红线

1. **四个常量声明了却没人用**。`ModEntry.cs:8` `PanelUpgradeId`、`:10` `AutoUpgradeId`、`:12` `NetworkTab`、`:14` `CardSubtitle` —— 调用点全部用字面量 `"APPRAISAL_RULES_PANEL"` / `"AppraisalRules"`（`:97,103-105`），驱动侧也直接写 `IsNetworkUpgradeUnlocked("APPRAISAL_RULES_PANEL")`（`AppraisalPanelDriver.cs:155`）。**改常量不会生效，改字面量又会漏一处** —— 这是"字符串 id 当契约"的典型腐化。
2. **`wild_unlock` 开着时 `auto_appraise` 被静默忽略**（CSV tooltip 明说，代码 `:150-167` 三分支实现）。设置项之间的隐式覆盖关系**只写在 tooltip 里**，玩家看不到就会以为坏了。要么互斥置灰，要么在面板上显式提示。
3. **"自动估价"直接跳过玩法**（`GetCorrectBaseValue` → 填格），默认关、需 800 信用点解锁，这是对的；但它**不校验当前是否真的在鉴定服务流程里**（没有 `clientIntent == APPRAISAL_SERVICE` 这一层，对比 `13-鉴定扩展与交互纸张规范.md:160` 的校验链）——面板只在估价面板开着时才建，算是间接门槛，**但物品被别的方式带进面板时会漏**。
4. **规则内容不可扩展**：`RuleCatalog.sets` 是 `static readonly` 字典 + `private static` 工厂（`RuleCatalog.cs:14-19`），**没有公开注册 API**。想加第四个品类只能改源码。这是它 1,104 行的真正代价 —— 对玩家来说"支持肋明治、机械手、等离子燃料匣"就是它的全部功能边界。
5. **`PointerSwallow` 是必须的，别省**：12 个接口全空实现（`PointerSwallow.cs:6-43`），代价 43 行，作用是把落在面板上的点击/拖拽全吃掉。**少了它，玩家勾选项时会顺手把身后 3D 场景里的物品拖走**——这是我们做任何 `InspectionUIManager` 附属浮窗都要付的"税"。
6. **`Text.ModId` 是可变静态字段**（`Text.cs:7`，默认 `"appraisal_rules"`，由 `Init` 覆写）——零反射取本地化的代价。若 `Init` 没跑就先取词，会拿到空串。
7. **面板宽度靠"父面板左侧剩余空间"算**：`SpaceLeftOf(parent)-8-12` 再 `Clamp(300,500)`（`:230,258-274`）。原版估价面板换分辨率/换布局，这里就贴边或被挤 —— 属于"跟着别人 UI 走"的固有脆弱，**必须 `Clamp` 兜底**（它做了）。

---

## 6. 对我们的可用点

1. **【《深空电玩》立刻可做】把 `ArcAppraisalPane` 的读数变成"可勾选的规则清单"**。我们现在的 `ArcAppraisalReadout` 只出"真伪/成色/疑点"三行结论（`ArcAppraisalReadout.cs:70-121`），**没有一个可玩的交互**；这套 `RuleSet/RuleGroup/RuleOption/Selection` 六件套是现成的骨架，把自家 `gd_*` / `hw_*` 的特征做成组，玩家勾完直接得报价。同时可以把"正确价值"（`AppraisalHelper.GetCorrectBaseValue`）作为**只读第四行**补上——这正是 `3828921912-AppraisalAssistant.md:463` 记的"全篇最高性价比的一条"，两件事可以一次做完。
2. **【《深空电玩》+《深空当铺：可能是群友》共用】"零 Harmony 的 UI 挂载五件套"**：① 先 `CopyBackground`/`CaptureBlockStyle` 偷父面板的底图（`:276-309`）② 自己加 `Canvas{overrideSorting=true, sortingOrder = parent+1}` + `GraphicRaycaster`（`:237-245`）③ 挂 `PointerSwallow` ④ `ContentSizeFitter.verticalFit` ⑤ 异常一次即自毁（`:101-121`）。我们两家都是纯 `CustomUIWindow` 路线，缺的正是这套"贴在原版面板旁边"的做法。
3. **【《深空当铺：可能是群友》关系网】`AddNetworkUpgrade` 的两级解锁写法可以直接照抄**：一级 300（面板）、二级 800（自动估价），二级用 `prerequisites: new[]{"APPRAISAL_RULES_PANEL"}` 串起来（`ModEntry.cs:104-105`）。我们 `FreyaPremium.cs` 已有信用点解锁的内容线，缺的是"**解锁对象是我这扇窗的开关**"这个用法——比 ConfigEntry 开关更有仪式感，也不违反"不白送强力功能"。
4. **【两家共用】"调用 ≠ Patch"的兼容判断法**（§3.1 那张表）：以后要给鉴定/估价类功能加增强，先问自己"我是挂补丁还是传实参"。**能传实参就绝不挂补丁** —— 这一条能让我们永久留在鉴定助手的白名单里。
5. **【工具链】`conflict_scan.py` 的 `REGISTER_CALLS` 该补一行**：`AddNetworkUpgrade` 已登记（`conflict_scan.py:85`），但它只统计"注册了什么 id"；本模组的例子说明**同一个 id 也可能被 `IsNetworkUpgradeUnlocked("字面量")` 检查**（`AppraisalPanelDriver.cs:155,167`），扫描时该把"检查字面量"也算进耦合面，否则 §5-1 那种"常量与字面量分叉"扫不出来。
