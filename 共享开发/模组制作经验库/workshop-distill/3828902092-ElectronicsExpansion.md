# 工坊 3828902092 架构蒸馏档案 —— 电子产品拓展 / Electronics Expansion

> **架构参考，不是功能清单。** 对象：`缓存文件夹\decomp_ws\3828902092`（ILSpy 产物，四程序集展开）。
> 方法：目录/行数侦察 → 5 张分布表 → 定点精读 14 个文件（合计约 1,900 行）→ 全库正则统计。
> **没有通读 18.7 万行**：正文字数是靠"统计口径 + 抽样精读"得出的，凡是抽样推不出来的结论都标「（推测）」。
> 代码块为反编译原文摘录（保留 ILSpy 的强制转换与 `(object)` 之类写法），行号即文件内真实行号。为可读性只删除了摘录**内部**的空行，非空行逐字保留——20 个代码块已用脚本对全库 `.cs` 做非空行序列比对，20/20 逐字连续命中。

---

## 0. 口径核对：18.7 万行是不是真的

| 项 | 任务给的数 | 我实测 | 结论 |
| --- | --- | --- | --- |
| .cs 文件 | 1,199 | **1,199** | ✅ |
| 总行数 | 187,549 | **187,549**（非空 174,098） | ✅ 物理行；`Measure-Object -Line` 会漏空行，差 13,451 行（7.2%），别用那个口径 |
| 程序集 | — | **4 个 DLL + 1 份链入共享文件** | 见 1.2 |
| `[HarmonyPatch]` 特性 | 389 | **478 处特性 / 465 个 `*Patch.cs` / 261 个唯一 (类型.方法) 目标** | 同量级，三种口径都能自洽，见 1.4 |
| 4 个 ModHook | 4 | **4 个事件订阅 + 3 个 `FireOnGameLoadedLate` 补丁** | ✅ 且全是低频加载期钩子，见 §8 |

**它到底是什么**：把"电子产品"这一整条产业链——拾荒/小偷/供货商的一手货源 → 检测定级 → 拆机换件/翻新/3D 打印 → 上架定价（行情波动/委托/批发）→ 售后、图鉴与技能成长——搬进当铺，并额外做了一台**游戏内手机终端**当统一 UI 外壳。附带 6 条 NPC 剧情线（希娅 Sia、CCArc、房东、博士、Georgio、Odin）与一本 84 KB 数据排版的维修手册。

---

## 1. 行数分布表（18.7 万行的真实构成）

### 1.1 按物理目录分组

| 目录 | 文件 | 行数 | 占比 | 它是什么 |
| --- | --- | ---: | ---: | --- |
| `CyberElectronics/` | 829 | 120,167 | 64.1% | 主程序集：逻辑 + UI + 327 个补丁，**完全平铺，无子目录** |
| `_side_CyberElectronics.Npcs/` | 273 | 45,865 | 24.5% | 剧情/联系人程序集（含 131 个补丁、13 张剧情 JSON） |
| `_side_CyberElectronics.Items/` | 52 | 7,847 | 4.2% | 物品目录程序集（Catalog/ItemSpec/工厂，4 张物品 JSON） |
| `_side_Pawnshop.ModRuntime/` | 42 | 6,768 | 3.6% | 运行时底座（生命周期/补丁安装/自研 JSON/Mono 后端） |
| `Pawnshop.OfficialCompatibility/` | 1 | 6,739 | 3.6% | **生成代码**：6,102 条反射契约表（链入每个程序集，见 1.4） |
| `Properties/` + `System.Runtime.CompilerServices/` | 2 | 163 | 0.1% | 工程样板 |

> 目录只有一层。`CyberElectronics/` 里 829 个文件全部同级——**分层信息不在目录，在文件名前缀和程序集边界**。这与它 24 万字节的 JSON 资源、和"一文件一类型"的反编译习惯一起，构成了"看起来像 18 万行"的第一层原因。

### 1.2 按程序集分组（真正的主干）

| 程序集 | 文件 | 行数 | 占比 | 说明 |
| --- | ---: | ---: | ---: | --- |
| `CyberElectronics.dll`（主） | 830 | 126,906 | 67.7% | 含 `OfficialCompatibility` 那份 6,739 行契约 |
| `CyberElectronics.Npcs.dll` | 273 | 45,865 | 24.5% | 剧情内容 + 5,657 行契约副本 |
| `CyberElectronics.Items.dll` | 52 | 7,847 | 4.2% | 物品定义 + 4,892 行契约副本 |
| `Pawnshop.ModRuntime.dll` | 42 | 6,768 | 3.6% | 底座 + 3,808 行契约副本 |
| 样板 | 2 | 163 | 0.1% | AssemblyInfo / IsExternalInit |

**这是本档案第一个重要事实**：它不是"一个 18 万行的大 DLL"，而是**一份内容主程序集 + 三个被切出去的程序集**，切法不是按技术层，是按**内容域**（物品 / 剧情 / 运行时）。切出去的理由写在代码里：`ProductModules.Prepare()` 显式 `ModResources.Register(typeof(Core).Assembly)`、`typeof(Catalog).Assembly`、`typeof(NpcModule).Assembly` —— 三份资源要分别注册，说明它们是三个独立的资源容器。

### 1.3 按"性质"分组（180 万字节里到底有多少是逻辑）

分类规则：`*Patch.cs` → 补丁；文件名含 View/UI/Panel/Page/Overlay/Fx/Art/Style/Tooltip… → UI；含 Rules/Catalog/Spec/Data/Sets/Points/Pricing/Registry… → 数据表；`\Ports\` → 装配端口；`ReflectionContracts.cs` → 生成代码。

| 类别 | 文件 | 行数 | 占比 |
| --- | ---: | ---: | ---: |
| 逻辑 / 内容（业务状态机、剧情、市场） | 438 | 88,332 | 47.1% |
| **UI / 呈现** | 123 | 45,125 | **24.1%** |
| **生成代码（反射契约表，4 份）** | 4 | 21,096 | **11.2%** |
| 逻辑 / 服务（生命周期、安装器、桥接） | 61 | 11,977 | 6.4% |
| 数据 / 规则表（含 switch 表、区间表） | 100 | 10,535 | 5.6% |
| Harmony 补丁（465 个文件） | 465 | 10,286 | 5.5% |
| 工程样板 | 8 | 198 | 0.1% |

**结论：没有"数据表撑起 18 万行"这回事。** 结构化常量只占 5.6%（10,535 行）+ 24 万字节 JSON；真逻辑 47.1%，UI 24.1%，生成代码 11.2%。这个模组是**真的写了很多代码**——但其中 35.3% 是"UI + 生成代码"这种可以直接砍掉的体量（见 §9）。

### 1.4 最大 10 个文件 + 前 30 行判读

| # | 文件 | 行数 | 是什么 |
| ---: | --- | ---: | --- |
| 1 | `CyberElectronics/PhoneTerminalUI.cs` | 12,366 | 手机终端全部页面：immediate-mode `OnGUI` 手绘 + 19 张页面内嵌类 |
| 2 | `Pawnshop.OfficialCompatibility/ReflectionContracts.cs` | 6,739 | **生成**：6,102 条 `assembly:type` 反射契约 |
| 3 | `_side_Npcs/…/ReflectionContracts.cs` | 5,657 | 生成，5,060 条（Npcs 所需子集） |
| 4 | `CyberElectronics/RepairRuntime.cs` | 4,967 | 拆机/检测/换件的运行时状态机 + 显示读取缓存 |
| 5 | `_side_Items/…/ReflectionContracts.cs` | 4,892 | 生成，4,325 条（Items 子集） |
| 6 | `_side_Npcs/…/PhoneContacts.cs` | 4,425 | 通讯录/聊天/好感度剧情引擎 |
| 7 | `_side_Npcs/…/CCArc.cs` | 4,121 | 一条完整剧情线（宣传/来访/赠礼） |
| 8 | `_side_ModRuntime/…/ReflectionContracts.cs` | 3,808 | 生成，3,286 条（只覆盖 5 个程序集） |
| 9 | `CyberElectronics/EnchantmentFx.cs` | 3,599 | 词条/附魔的逐帧特效组合 |
| 10 | `CyberElectronics/RepairBenchView.cs` | 3,118 | 维修台 UI（工位、工具、组件坐标全手绘） |

四份契约表合计 **21,096 行 = 11.2%**，条目数 6,102 / 5,060 / 4,325 / 3,286 = **18,773 条**。

文件头判读证据（`ReflectionContracts.cs:1-33`）：

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Pawnshop.OfficialCompatibility;

internal static class ReflectionContracts
{
	private sealed class Root
	{
		internal string Assembly;
		internal string Name;
		internal Func<Type> Resolve;
		internal Func<BindingFlags, MemberInfo[]> Members;
		internal Type Resolved;
		internal readonly Dictionary<BindingFlags, MemberInfo[]> Cache = new Dictionary<BindingFlags, MemberInfo[]>();
```

条目长这样（`ReflectionContracts.cs:1244` 原文，一行一条，全表按 `assembly:type` 字母序）：

```csharp
result.Add("Assembly-CSharp:ModHook", new Root("Assembly-CSharp", "ModHook", () => KnownAssembly("Assembly-CSharp")?.GetType("ModHook", throwOnError: false), (BindingFlags flags) => KnownAssembly("Assembly-CSharp").GetType("ModHook", throwOnError: false).GetMembers(flags)));
```

`CyberElectronics/PhoneTerminalUI.cs:1-18` 判读：

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using Pawnshop.ModRuntime;
using Pawnshop.ModRuntime.Mono;
using TMPro;
using UnityEngine;

namespace CyberElectronics;
```

——引 `Reflection` + `Regex` + `TMPro` + `ModRuntime.Mono`，12,366 行的单一 `static` 类：**这个文件是"UI 手绘 + 反射读原版控件"的合体**，不是游戏逻辑。

### 1.5 冻结副本 / 生成代码 / 内嵌资源转 C#：三查三否

| 疑点 | 实测 | 判定 |
| --- | --- | --- |
| 把旧引擎整份冻结？ | 无。最大 5 个"非业务"文件是 4 份契约表 + 自研 JSON 库（1,031 行） | **没有**冻结引擎 |
| 生成代码？ | **有，且很大**：4 份契约表 21,096 行 / 18,773 条。证据：按 `assembly:type` 字母序、模板一行一条、连编译器生成名都收了（`ModConfig+<>c__DisplayClass20_0`、`UnityEngine.UI.ScrollRect+ScrollRectEvent`）、每程序集只收自己摸得到的子集（ModRuntime 那份只 5 个程序集）。手写不会这么整齐也不可能收到 BCL 内部类 | **生成**（推测：构建期扫描 IL 的 TypeRef 闭包生成） |
| 内嵌资源转 C#？ | **没有**。29 个 JSON 是 `EmbeddedResource` + `LogicalName` 直挂（`CyberElectronics.csproj` 明写），png/wav 也是二进制资源，没有"资源变 byte[] 常量"那套 | **没有** |
| 小规模冻结副本 | 有 3 处：`Il2Cpp/`、`Il2CppInterop.Runtime/`、`Il2CppInterop.Runtime.InteropTypes*` 共 6 个**空壳命名空间**（合计约 41 行，`namespace Il2Cpp; public static class ManagedBackend { }`）；`Pawnshop.ModRuntime.Json*` 是自研/裁剪的 System.Text.Json 兼容层（1,031 行）；`Pawnshop.ModRuntime.Mono` 是 Mono 后端垫片 | 可接受的**多后端编译垫片**，不是垃圾 |

---

## 2. 它到底加了什么（一句话 + 闭环拆解）

**一句话**：给当铺接了一条完整的电子产品供应链——货源（拾荒者/小偷/定期供货商）→ 店内检测出故障点 → 拆机换件/翻新/3D 打印/解锁充电 → 上架（行情涨跌、委托、批发、供货商）→ 售后与图鉴技能成长，全部 UI 收在一台自绘手机终端里。

闭环上的机械点（全部有文件/JSON 对应，不是猜的）：

| 环节 | 实现 | 证据 |
| --- | --- | --- |
| 货源 | 拾荒 35% / 小偷 45%；供货商 2~5 天一趟，1~3 天一次广告；品组权重 phone 5 / computer 3 / media 2 | `CyberElectronics.Content.features.json:3-17` |
| 成色 | 二手 65%、上锁 30%、碎屏 20%、报废 10%、电量 10~100、日耗 5 | 同上 `:18-26`（`Phone` 段） |
| 检测 | 8 类故障点（板/壳/充电口/排线/摄像头/屏/电池/芯片）、6 级烧蚀贴图、万用表/热风枪/吸锡/喷雾/刷子/螺丝刀六种工具 | `Catalog.IsDetachedPhonePart()`、`RepairPointRules.cs`(50)、`RepairDamageLayout.cs`(153)、`repair_component_burn_0..5.png` |
| 维修 | 拆装台 + 快速拆/装、维修资质证书、退回托盘、客户取件流程 | `RepairBenchView.cs`(3,118)、`RepairBenchStorage.cs`(648)、`RepairOrders.cs`(2,554) |
| 制造 | 3D 打印机（标准打印能耗 4、塑料/金属双料、T1–T4 模块增强）、玻璃熔炉、解锁机、充电机（容量 100） | `CyberElectronics/PrinterFabricationRules.cs:12`、`CyberElectronics/ElectronicsMachines.cs:44-56` |
| 定价 | 行情 +25% / −25%、权重 10、持续 2 天、冷却 5 天；委托价、批价、供货商价表 | `features.json:27-33`、`PhoneShopMarketRules.cs`(264)、`PhoneShopPricing.cs`(36) |
| 销售 | 手机商店（商品页/订单页/供货商页）、委托单、批发、回收、点评（大众点评/小众点评） | `PhoneShopCatalog.cs`(800)、`PhoneCommissions.cs`(1,540)、`PhoneShopOrders.cs`(1,492) |
| 终端 | 19 个页面：home/lock/通讯录/委托/商店/技能/学习/任务/图鉴/相机/设置/聊天/维修师协会/点评… | `PhoneTerminalUI.cs` 的 `_page == "…"` 常量 |
| 成长 | 技能树 84 节点（回收、讨价、容器容量、熔炉产出、寻宝…） | `CyberElectronics.Content.skill-tree.json`（1,450 行 / 84 个 id） |
| 剧情 | 6 条线 + 13 张对话/表情/掉落 JSON | `_side_Npcs/CyberElectronics.Content.*.json` |
| 文档 | 8 版维修手册，84 KB 排版数据 | `CyberElectronics.Content.manual-v8.json`（单行 84,480 B） |

---

## 3. 数据驱动的程度（表能覆盖多少实体，加一个新物品要动几个文件）

### 3.1 三层并行定义，不是纯 JSON

| 层 | 载体 | 覆盖 | 谁读 |
| --- | --- | --- | --- |
| 物品主表 | `Content.catalog.json` 33 条 + `components.json` 6 + `printer-modules.json` 4 + `sia-outfits.json` 2 = **45 条** | 手机/电脑/配件/视听设备/耗材/打印模块/服装 | `Catalog.Load()` |
| 剧情绑定物 | C# 静态 `ItemSpec[] Specs`（`PhoneBoxItems` / `MaintenanceItems` / `EntropyGunItem` / `EntropyStoryItems` / `PipaCassetteItem`） | 包装盒、奥丁剧情物、熵枪、磁带机 | 同 `Catalog.Load()` 里 `Concat` |
| 交易方 | `Content.traders.json` 6 条 | 供货商/联系人 | `Catalog.TraderSource = NpcModule.ReadTraders` |

`ItemSpec` 只有 12 个字段（`ItemSpec.cs:5-32`）：`id/name/group/description/flavor/rarity/value/width/height/iconIndex/contraband/upperDistrictOnly/attachments`。**这就是它的"内容 schema"**——一个物品 = 一行 JSON，恰好够生成一个 `GameItem`（尺寸定格子形状、value 定价、group 定品类、iconIndex 定图集格）。

加载器同时是**校验器**（`Catalog.cs:119-142`），这是这套表能维持 200+ 实体的关键：

```csharp
		foreach (ItemSpec item in Items.Concat(ComponentItems).Concat(EntropyStoryItems.Specs.Where((ItemSpec item) => item.id == "ce_doctor_entropy_lab")))
		{
			if (!item.id.StartsWith("ce_") || item.value < 0 || (item.value == 0 && !EntropyStoryItems.Is(item.id)) || item.width < 1 || item.height < 1)
			{
				throw new InvalidDataException("Invalid electronic item: " + item.id);
			}
			ItemLookup.Add(item.id, item);
		}
```

```csharp
		if (new string[6] { "ce_duanxiang_m1_phone", "ce_gray_shell_rugged_phone", "ce_foldlight_dual_phone", "ce_inkpage_phone", "ce_nightline_encrypted_phone", "ce_silverring_holo_phone" }.Any((string id) => FindItem(id)?.group != "phone"))
		{
			throw new InvalidDataException("The catalog must retain all six original phone items.");
		}
		if (Items.Any((ItemSpec item) => item.upperDistrictOnly && item.value <= 300))
		{
			throw new InvalidDataException("Upper-district-only items must be priced above the lower-district stock ceiling.");
		}
```
—— `_side_CyberElectronics.Items/CyberElectronics/Catalog.cs:135-142`（中间 127-134 行是 `ce_3d_printer`/旧扳手/博士服装的兜底登记，略）

模组启动时不做"尽力而为"，而是**先证伪再开机**：ID 前缀、价格非负、尺寸非零、六个祖传手机不许被挤掉、上城专属必须贵于下城天花板。

### 3.2 一张表覆盖多少实体

- 源码里出现的唯一 `ce_*` 标识符：**204 个**（含品类标签、事件 id、价格键，不等于注册物品数）。
- 按家族：phone 49、sia 22、repair 16、doctor 9、electronics 9、entropy 7、printer 7、maintenance 6、npc 5 …——**内容量集中在"手机"上，其它都是围着手机转的配件与耗材**。
- JSON 直接描述 45 个；其余由 5 个静态 `Specs` 数组 + 工厂补（`CatalogItemFactory`、`ItemAttachmentRegistry`）。注册数（推测）60~70 件。

### 3.3 加一个新电子产品要动几个文件

| 情况 | 要动 | 文件数 |
| --- | --- | --- |
| 新手机/配件，已存在的 group，图集里已有图标格 | `catalog.json` 加 1 条 | **1** |
| 同上，但要新图标 | + 往图集加一张图 / 或用 `iconIndex` 复用 | 1~2 |
| 新 group（如"服务器"） | + `Catalog.GroupName()` 和 `Catalog.TypeFor()` 各加一个 `switch` 分支（含中文品类名） | 2（`Catalog.cs` 308 行里 2 个 switch） |
| 要进商店/委托/图鉴 | + 对应 JSON 各 1 行（`shop-market-items.json` / `commission-native-items.json`） | 3~4 |
| 要新品类标签 | `EnableTag`（源码 71 处；**没有**往 `SetGameItemType` 写自定义类型） | 1 |

**这就是它敢做 200+ 内容实体的底气**：常态 1 个文件、1 行 JSON；异常情况也最多碰同一个 308 行的 `Catalog.cs`。

---

## 4. 465 个补丁怎么组织

### 4.1 数量口径先说清

| 口径 | 数 |
| --- | ---: |
| `*Patch.cs` 文件 | 465（主 327 / Npcs 131 / Items 7） |
| `[HarmonyPatch` 特性出现次数 | 478 |
| 唯一 `(目标类型.目标方法)` | **261** |
| 被 ≥2 个补丁类命中的目标 | **81**（最热 `PlayerStore.GetNextClient` ×11） |
| Prefix 返回 `bool`（可拦断）/ 返回 `void` | **156 / 108** |
| Postfix | 242 |
| Transpiler（真有方法体） | **3**（AugPresenceEnding / BrewingIngredientHost / CapturedAugDismiss，每个 13~25 行） |

> 任务里的"389"最像是**某一程序集或某一类（只算 Prefix）的口径**；478/465/261 三个数都能在你的计数方式下自洽，不必强行对齐。

### 4.2 组织方式：文件名即域，物理仍然平铺

前缀归域（无子目录）：`Phone*` 129 个 / `Skill*` 97 / `Repair*` 96 / `Electronics*` 61 / `Workroom*` 49 / `Manual*` 37 / `Entropy*` 32 / `Maintenance*` 19 / `Shop*` 18 / `Enhancement*` 17 / `Capture*` 14 / `Machine*` 9 / `Component*` 9 / `Toolboard*` 7。**一个补丁类一个文件，文件名 = 域 + 动作 + `Patch`**（`RepairBehaviourCanTargetPatch` / `RepairBehaviourMayTargetPatch` / `RepairBehaviourTargetPatch` 三连即"拖拽三组委托"的标准写法）。

### 4.3 有注册器，且是"清单驱动"

`PatchAll()` 在全局 **0 次**调用。装配流程是：

1. `patch-order.json`（279 条全限定类名）声明**安装顺序**；
2. `ProductModules.PatchTypes()` 把它变成排序字典，未登记的排到 `int.MaxValue`（即"后面补上"）；
3. `PatchInstaller.Install()` 逐个 `harmony.CreateClassProcessor(type).Patch()`，失败只记 `Failure` 不中断；
4. 主程序集自己被**排除**，由加载器那条路装（见下）。

```csharp
	internal static IEnumerable<Type> PatchTypes()
	{
		string[] source = ModResources.ReadJson<string[]>("CyberElectronics.Content.patch-order.json");
		Dictionary<string, int> positions = source.Select((string name, int index) => (name: name, index: index)).ToDictionary(((string name, int index) p) => p.name, ((string name, int index) p) => p.index, StringComparer.Ordinal);
		return from type in ReflectionContracts.GetTypes(Assembly.GetExecutingAssembly()).Concat(Items.PatchTypes()).Concat(NpcModule.PatchTypes())
			orderby positions.TryGetValue(type.FullName ?? "", out var value) ? value : int.MaxValue
			select type;
	}
```
—— `CyberElectronics/ProductModules.cs:34-41`

```csharp
		foreach (Type type in types)
		{
			if (!IsPatchClass(type))
			{
				continue;
			}
			try
			{
				List<MethodInfo> list = harmony.CreateClassProcessor(type).Patch();
				if (list == null || list.Count == 0)
				{
					report.Skipped++;
					continue;
				}
				report.Installed++;
				hashSet.Add(type);
			}
			catch (Exception ex)
			{
				report.Failures.Add(new Failure(type.Name, Describe(ex)));
			}
		}
		foreach (Type item in critical)
		{
			if (!hashSet.Contains(item))
			{
				report.CriticalFailure = item.Name;
				break;
			}
		}
```
—— `_side_Pawnshop.ModRuntime/Pawnshop.ModRuntime/PatchInstaller.cs:33-54`

**两个必须知道的实现细节**（都可在代码里验证）：

- `Core.Init` 里的过滤是 `where type.Assembly != typeof(Core).Assembly` —— **主程序集自己的 327 个补丁不走这条路**（`PatchAll()` 全局为 0，所以只能是被 ModLoader 的装配流程接管；机制未在本目录反编译，标「（推测）」）。
- 结论副作用：`patch-order.json` 的 279 条里，属于主程序集的那些**并未真正生效**——它们只对手动安装的 Items/Npcs 两个程序集有意义。**"声明的顺序"和"实际生效的顺序"存在落差，这是它自己的一处裂缝**。
- `critical` 只有一个调用点，传的是 `Array.Empty<Type>()`（`Core.cs:34-36`），所以 `PatchInstaller` 里那套"关键补丁失败就 `UnpatchSelf()` 保存档"的代码**目前是死安全网**。设计意图值得抄，落地没接上。

### 4.4 同一原版方法被多次 Patch：靠"优先级分相"，不靠互斥

`[HarmonyPriority]` 用了 **54 次**，值域极有规律：

| 优先级 | 次数 | 语义 |
| ---: | ---: | --- |
| 900 / 850 | 1 / 1 | 最先，资源/存档级抢位 |
| **800** | **17** | "准备/入队"相：只做副作用，返回 `void` |
| 600 | 1 | 次高 |
| **0** | **26** | 默认相 |
| −50 / −75 / −100 | 4 / 2 / 2 | **裁决相**：返回 `bool`，最后跑，能看到所有准备结果 |

`HarmonyBefore`/`HarmonyAfter` = **0 次**。也就是说它**从不点名别人的 harmony id**，跨模组顺序完全让位给加载器与优先级数值。

三个真实样本，同一个 `PlayerStore.GetNextClient`（被 11 个补丁类命中）：

```csharp
[HarmonyPatch(typeof(PlayerStore), "GetNextClient")]
internal static class PhoneShopOrderNextClientPatch
{
	[HarmonyPriority(800)]
	private static void Prefix(PlayerStore __instance)
	{
		PhoneShopOrders.PrepareDeliveries(__instance);
	}
}
```
—— `CyberElectronics/PhoneShopOrderNextClientPatch.cs:6-12`（准备相）

```csharp
[HarmonyPatch(typeof(PlayerStore), "GetNextClient")]
public static class SiaLodgingGetNextPatch
{
	[HarmonyPriority(800)]
	private static void Prefix()
	{
		SiaLodging.Guard(() =>
		{
			if (SiaLodging.SuppressVisits)
			{
				SiaLodging.RemoveQueued();
			}
			else
			{
				SiaLodging.QueueGoodbye();
			}
		});
	}
}
```
—— `_side_Npcs/CyberElectronics/SiaLodgingGetNextPatch.cs:5-23`（准备相，注意 `Guard(...)` 把异常收在边界）

```csharp
[HarmonyPatch(typeof(PlayerStore), "GetNextClient")]
internal static class DoctorVisitNextPatch
{
	[HarmonyPriority(-50)]
	private static bool Prefix(PlayerStore __instance)
	{
		return DoctorVisits.AllowNext(__instance);
	}
}
```
—— `_side_Npcs/CyberElectronics/DoctorVisitNextPatch.cs:5-13`（裁决相，`-50` 保证在所有 `800` 之后跑，能读到它们刚写入的队列状态）

**这是可复用的范式**：把"同一个原版钩子上 N 个互不认识的模块"降维成"一条按优先级分相的时间线"，`800` 写状态、`0` 常规、负数读状态做最终裁决。副作用是每个补丁都必须自己判"是不是我的对象"，所以里面有大量 `Xxx.Guard(...)` 与 `AllowNext` 式的准入函数。

### 4.5 3 个 Transpiler 的例外

只用 IL 的地方全部是"目标方法在编译期解析不出来"的场合，用 `[HarmonyPatch]`（无类型参数）+ `TargetMethod()` 动态解析：

```csharp
[HarmonyPatch]
internal static class AugPresenceEndingPatch
{
	private static MethodBase TargetMethod()
	{
		return AugPresenceEnding.NativeCallback();
	}

	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		return AugPresenceEnding.ReplaceFullPresenceEnding(instructions);
	}
}
```
—— `CyberElectronics/AugPresenceEndingPatch.cs:7-19`

### 4.6 11 个独立 Harmony 实例

不是"一个 mod 一个 Harmony"：`CybernElectronics.Official.Supplemental`、`.PhoneTerminal`、`.Workroom.Canvas`、`.Workroom.SharedWarehouse`、`.Workroom.Input`、`.JacksonCostume`、`.NpcExpressions`、`.BrewingIngredients` …… **每块功能岛一个**，可独立 `UnpatchSelf()`。配合 28 处显式 `harmony.Patch(target, prefix)` —— 那些是"运行时才决定要不要补"的补丁（例如 `WorkroomPcWarehouse.cs:735` 动态替换回调）。

---

## 5. 架构分层与依赖方向

### 5.1 五层，全部单向向下

```
Pawnshop.OfficialCompatibility   ← 纯反射契约（0 个游戏类型引用，只有 System.Reflection）
        ↑ 作为链接源码编进每个 DLL（4 份子集）
Pawnshop.ModRuntime              ← 生命周期注册表 / 补丁安装器 / 自研 JSON / Mono 垫片
        ↑
CyberElectronics.Items           ← ItemSpec schema + Catalog 装载 + GameItem 工厂
        ↑
CyberElectronics.Npcs            ← 剧情、联系人、13 张 JSON
        ↑
CyberElectronics（主）           ← Core:IMod、Gameplay 组合根、全部 UI、327 补丁
```

引用方向的证据（`using` 实测）：`Catalog.cs` 只 `using Pawnshop.ModRuntime(.Json)`，**不认识** UI 与剧情；`RepairRuntime.cs` 只 `using Pawnshop.ModRuntime.Json / .Mono / UnityEngine`；`Core.cs` 只 `using HarmonyLib / Pawnshop.ModRuntime(.Mono)`。没有任何反向引用。

### 5.2 Core 层与 Unity 的关系：**基本无关，但边界不干净**

`Pawnshop.ModRuntime` 的 11 个核心文件里：`ModuleLifecycle`、`SafeCall`、`PatchInstaller`、`RuntimeLog`、`DragQueryGuard`、`LifecycleGuard`、`OwnedItemDelivery` 只用 `System.*`；`Pawnshop.ModRuntime.Json*`（1,031 行）是纯 System 的 STJ 兼容实现。

**但**同一个 DLL 里还塞了 `PixelArt.cs`、`ModResources.cs`、`ShopVisitors.cs`、`.Mono.*`（含 `OfficialLifecycleHost : MonoBehaviour`）——它们直接吃 `UnityEngine`。所以准确说法是："业务核心无 Unity 依赖，但**承载它的程序集不是纯的**"。想抄"可离线测试的 Core"，得自己把 `.Mono` 与 `Json` 拆成独立程序集。

### 5.3 组合根只有一个，316 行纯注册

`GameplayModule.Register(lifecycle)` 通篇是这种句式（`GameplayModule.cs:11-24`）：

```csharp
		lifecycle.Add("identity-guide", "scene-ready", 36, "IdentityInspectionGuideView.EnsureAssets", IdentityInspectionGuideView.EnsureAssets);
		lifecycle.Add("identity-guide", "update", 38, "IdentityInspectionGuide.Tick", IdentityInspectionGuide.Tick);
		lifecycle.Add("gameplay", "scene-ready", 34, "ManualPossession.Initialize", ManualPossession.Initialize);
		string[] array = new string[3] { "scene-unloaded", "quit", "release" };
		foreach (string phase in array)
		{
			lifecycle.Add("gameplay", phase, 34, "ManualPossession.Reset", ManualPossession.Reset);
		}
		lifecycle.Add("xia-tutorial", "update", 35, "TeachingSession.Tick", XiaTutorialModule.Tick);
		lifecycle.Add("xia-tutorial", "late-update", 16, "TeachingOverlay.UpdateVisibility", XiaTutorialModule.LateTick);
```

用到的阶段共 8 个：`scene-loaded` / `scene-ready` / `scene-unloaded` / `update` / `late-update` / `gui` / `quit` / `release`。**所有子系统都不认识 Unity 的消息函数，只认识阶段名 + 序号。**

### 5.4 UI 没有 uGUI 预制体，全是 immediate-mode 手绘

`OfficialLifecycleHost.OnGUI() → Draw → ModuleLifecycle.Run("gui")`，然后 12,366 行的 `PhoneTerminalUI` 用 `Texture2D`/`Sprite`/九宫格 + 手算坐标把整个手机画在屏幕上；`RepairBenchView`/`RepairBenchView`/`WarehousePagesView` 同理。`Art`/`Style`/`Geometry` 文件（`RepairArt` 539、`WorkbenchWindowGeometry` 198、`DsTheme` 60）就是这套自绘的"美术层"。这是 **24.1% 的 UI 行数的由来**，也是它的选择：不依赖原版 UI 结构，就不会被原版 UI 改动打断。

---

## 6. 与其它模组的共存设计

**`IsModRunning` = 0 次，`ModHelper.Expose` = 0 次。** 它根本不用原版的消息总线，而是造了一层"反射契约"来替代：

### 6.1 契约表覆盖谁

主程序集那份 6,102 条覆盖 **23 个程序集**，其中外部模组：`PCExpansion` 1,302 条、`PSFishingGameplay` 826、`BrewingExpansion` 194、`ShopExpansionMod` 145、`ElectronicsDebugMod` 98、`DebugConsole` 50、`AutoStorage` 37、`Pawnshop.BrewingSkillCompat` 28。另外还收了 `System.Text.Json` / `System.Memory` / `System.Buffers` / `System.ValueTuple` 各 1 条——说明它**自己带了这些 BCL 的替身**。

### 6.2 三件套：软探针 + 空值降级 + 未登记即拒

```csharp
	private static Assembly KnownAssembly(string name)
	{
		return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault((Assembly a) => a.GetName().Name == name);
	}

	internal static TResult Optional<TReceiver, TResult>(TReceiver receiver, Func<TReceiver, TResult> read) where TReceiver : class where TResult : class
	{
		if (receiver != null)
		{
			return read(receiver);
		}
		return null;
	}

	internal static void Optional<TReceiver>(TReceiver receiver, Action<TReceiver> run) where TReceiver : class
	{
		if (receiver != null)
		{
			run(receiver);
		}
	}
```
—— `Pawnshop.OfficialCompatibility/ReflectionContracts.cs:84-104`

配套的硬约束（`Require`）：反射一个**没登记**的类型会直接 `throw new NotSupportedException("未登记的反射契约：" + type.FullName)`。等于给"随便反射"上了闸：想摸新东西，先把名字写进表里。

### 6.3 能力探测而不是版本判断

`ShopExpansionBridge`（140 行）是典型：启动时 `Resolve()` 一次，把对方的方法抓成 `Func<>`/`FieldInfo` 缓存在静态字段里；`Available` 判 `confirm != null`。对方没装 → 委托是 null → UI 少一页，不报错、不崩。

专用兼容件：`BrewingSkillCompatibility`(317)、`PCExpansionCompatibility`(93)、`ShopExpansionBridge`(140)、`NativeItemFeatureRecovery`(139)、`OfficialLocalizedPatches`(119)、`NativeSupply`(463)、`AutoStorageCustody`(41)。**没有一处引用对方的 DLL**（否则加载顺序一变就 `TypeLoadException`）。

### 6.4 让位策略

- 补丁全部走 `Prefix` + `bool` 的"可让位"写法（156 个），只有确定要拦的才 `return false`；
- `HarmonyPriority` 只用自己的数值，从不 `HarmonyBefore/After("mod.xxx")` —— 不假定别人存在；
- 关键路径失败时**只警告不抛**（`PatchInstaller`），并用 `ItemRegistration.AllowNativeSave()` 在物品注册未回读通过时"暂停依赖它的状态更新、但让原版存档继续"（`ItemRegistration.cs:36-59`）——**这是把"我方坏了"与"玩家存档"解耦的做法，值得抄**。

---

## 7. 性能：4 个钩子都是低频，开销在哪

### 7.1 低频是真的

| 钩子 | 时机 |
| --- | --- |
| `ModHook.OnModItemDirectoryInit` | 物品目录初始化一次（`ItemRegistration.Install`） |
| `ModHook.OnModAssetsLoaded` | 资源加载（`IdentityInspectionGuideView`、`BrewingSkillCompatibility`） |
| `ModHook.OnGameLoadedInit` | 读档一次（`Core.GameReady`） |
| `ModHook.OnNewGameStarted` | 新开局一次 |
| `[HarmonyPatch(typeof(ModHook), "FireOnGameLoadedLate")]` ×3 | 读档末尾一次 |

### 7.2 真实开销：`OnGUI` 每帧 + 反射读取

`Core` 把自己挂到 `host.Tick/LateTick/Draw/Quit`，`Draw` 每帧 `ProductModules.Lifecycle.Run("gui")`；`update` 阶段还排了约 30 个子系统的 Tick。加上 UI 层大量走 `ManagedAccess.Get<T>(native, "text")` 这种**字符串键反射读**，每帧成本不低。

### 7.3 它做的四件缓解

| 手段 | 证据 |
| --- | --- |
| **阶段前置**：重活压在 `scene-loaded`/`scene-ready`，`update` 里只留轻量 | `GameplayModule.cs` 316 行注册表 |
| **同帧去重**：`Time.frameCount` 用了 **132 次**，当作"这一帧已经处理过"的闩 | `RepairBenchView.cs:2614-2618`、`PhoneTerminalUI.cs:10151-10163` |
| **依赖键缓存**：`IntPtr`（原生指针）+ 关键输入做缓存键，输入没变就不重算 | `PhoneTooltipDisplayCache`(63)、`ComponentTooltipTextCache`(79)、`RepairRuntime.DisplayReadCache`、`PortraitCompositeCache`(405) |
| **弱引用绑定**：机器槽位 `WeakReference<GameItem>` / `WeakReference<GameSlotInventory>`，不拖住库存对象 | `ElectronicsMachines.cs:14-18` |

反向证据（说明它**没有**做的）：`StartCoroutine` 仅 1 处、`WaitForSeconds` 0、`GC.Collect` 0、`yield return` 99。**没有分帧器、没有对象池框架**——它靠"缓存 + 阶段"而不是"调度器"。

---

## 8. 可复用范式（5 个，架构级）

### 范式 1：内容表 = JSON + 加载期证伪（200+ 实体的地基）

三层分离：schema（`ItemSpec` 12 字段）→ 数据（45 条 JSON）→ 编译产物（`ItemAssemblyPlan` 一次性编译成附件布局）。加载器兼任校验器，坏数据在 `Init()` 就炸掉，而不是玩到一半 NRE。

```csharp
	public static void Load()
	{
		ItemSpec[] source = Read<ItemSpec[]>("catalog.json").Concat(Read<ItemSpec[]>("printer-modules.json")).ToArray();
		Items = source.Where((ItemSpec item) => item.id != "ce_3d_printer").ToArray();
		ComponentItems = Read<ItemSpec[]>("components.json").Concat(Read<ItemSpec[]>("sia-outfits.json")).Concat(PhoneBoxItems.Specs).Concat(MaintenanceItems.Specs)
			.Concat(EntropyGunItem.Specs)
			.Concat(EntropyStoryItems.Specs.Where((ItemSpec item) => item.id != "ce_doctor_entropy_lab"))
			.Concat(PipaCassetteItem.Specs)
			.ToArray();
		Traders = (TraderSource ?? throw new InvalidOperationException("NPC module is not registered"))();
		ItemLookup.Clear();
		TraderLookup.Clear();
		foreach (ItemSpec item in Items.Concat(ComponentItems).Concat(EntropyStoryItems.Specs.Where((ItemSpec item) => item.id == "ce_doctor_entropy_lab")))
		{
			if (!item.id.StartsWith("ce_") || item.value < 0 || (item.value == 0 && !EntropyStoryItems.Is(item.id)) || item.width < 1 || item.height < 1)
			{
				throw new InvalidDataException("Invalid electronic item: " + item.id);
			}
			ItemLookup.Add(item.id, item);
		}
```
```csharp
	private static T Read<T>(string filename)
	{
		using Stream stream = ModResources.Open("CyberElectronics.Content." + filename) ?? throw new FileNotFoundException(filename);
		T val = JsonSerializer.Deserialize<T>(stream, new JsonSerializerOptions
		{
			IncludeFields = true
		});
		if (val == null)
		{
			throw new InvalidDataException(filename);
		}
		return val;
	}
```
—— `_side_CyberElectronics.Items/CyberElectronics/Catalog.cs:107-126` 与 `:275-287`

**可复用点**：① 资源名统一 `Assembly.Content.<表名>.json`，一份 `Read<T>` 复用到 29 张表；② 校验只写"跨表不变量"（价格/尺寸/祖传清单），不逐字段验；③ `ItemAssembly.Configure()` 把表编译成运行期结构，之后热路径不再查表。

### 范式 2：清单驱动的补丁安装器（顺序声明与执行分离）

顺序写在 JSON 里，代码只负责"按名排序 + 逐个装 + 装不上就记名字"。

```csharp
	internal static IEnumerable<Type> PatchTypes()
	{
		string[] source = ModResources.ReadJson<string[]>("CyberElectronics.Content.patch-order.json");
		Dictionary<string, int> positions = source.Select((string name, int index) => (name: name, index: index)).ToDictionary(((string name, int index) p) => p.name, ((string name, int index) p) => p.index, StringComparer.Ordinal);
		return from type in ReflectionContracts.GetTypes(Assembly.GetExecutingAssembly()).Concat(Items.PatchTypes()).Concat(NpcModule.PatchTypes())
			orderby positions.TryGetValue(type.FullName ?? "", out var value) ? value : int.MaxValue
			select type;
	}
```
—— `CyberElectronics/ProductModules.cs:34-41`

**可复用点**：未登记的补丁不会丢，只是排到最后（`int.MaxValue`）。这是"清单可选、代码必装"的正确默认值。**教训**：如果只有部分程序集走这条路，顺序声明就是半张空头支票（本模组的裂缝，见 §4.3）。

### 范式 3：优先级分相（同一钩子 N 个模块的合并术）

`800` 写状态（`void`）→ `0` 常规 → `-50~-100` 读状态做准入（`bool`）。同一个 `GetNextClient` 上 11 个补丁类共存，靠的不是互斥而是**相位**。

```csharp
	[HarmonyPriority(800)]
	private static void Prefix(PlayerStore __instance)
	{
		PhoneShopOrders.PrepareDeliveries(__instance);
	}
```
```csharp
	[HarmonyPriority(-50)]
	private static bool Prefix(PlayerStore __instance)
	{
		return DoctorVisits.AllowNext(__instance);
	}
```
—— `CyberElectronics/PhoneShopOrderNextClientPatch.cs:8-12` / `_side_Npcs/CyberElectronics/DoctorVisitNextPatch.cs:8-12`

**可复用点**：给自己的每个补丁标注**相位语义**（准备 / 常规 / 裁决），负数只用于"读别人的准备结果"。不点名别人的 harmony id，天然与未知模组共存。

### 范式 4：有序阶段注册表（组合根只注册，不实现）

```csharp
	public void Add(string owner, string phase, int order, string label, Action action)
	{
		if (sealedRegistration)
		{
			throw new InvalidOperationException("Module registration is sealed");
		}
		if (!entries.TryGetValue(phase, out List<Entry> value))
		{
			entries.Add(phase, value = new List<Entry>());
		}
		if (value.Any((Entry e) => e.Order == order))
		{
			throw new InvalidDataException("Duplicate lifecycle order: " + phase + ":" + order);
		}
		value.Add(new Entry(owner, label, order, action));
	}

	public void Seal()
	{
		foreach (List<Entry> value in entries.Values)
		{
			value.Sort((Entry a, Entry b) => a.Order.CompareTo(b.Order));
		}
		sealedRegistration = true;
	}
```
—— `_side_Pawnshop.ModRuntime/Pawnshop.ModRuntime/ModuleLifecycle.cs:16-40`

**可复用点**：① 同阶段**序号重复直接抛**，逼你显式决定先后，杜绝"看运气"；② `Seal()` 后禁止再注册，把"注册期"和"运行期"物理隔开；③ `Run(phase)` 逐个包 `SafeCall.Run`，坏一个不拖垮一帧（异常在边界捕获并记 label）。

### 范式 5：依赖键缓存（UI 热路径的正确缓存姿势）

缓存不是"存结果"，是"存结果 + 存它依赖了什么"，任一依赖变了就重算。

```csharp
	private static readonly Dictionary<IntPtr, Entry> entries = new Dictionary<IntPtr, Entry>();

	private static Entry For(GameItem item)
	{
		if (!entries.TryGetValue(item.ManagedPointer(), out Entry value))
		{
			entries.Add(item.ManagedPointer(), value = new Entry());
		}
		return value;
	}

	internal static RepairRuntime.DisplayReadScope Begin(GameItem item)
	{
		return RepairRuntime.BeginDisplayReads(For(item).Reads, item);
	}

	internal static string Summary(GameItem item, RepairDevice state)
	{
		Entry entry = For(item);
		double num = ElectronicSkillRules.Bonus("sia_resonance");
		string name = CultureInfo.CurrentCulture.Name;
		string text = EnchantmentLoreContentHost.Summary(ComponentEnchantments.PhoneKind(state));
		if (entry.Device != state || entry.Resonance != num || entry.Culture != name || entry.Lore != text)
		{
			string text2 = RepairAttributes.PhoneSummary(state) + (RepairRules.Stripped(state) ? "" : ("\n" + ComponentSets.Summary(state)));
			entry.Device = state;
			entry.Resonance = num;
			entry.Culture = name;
			entry.Lore = text;
			entry.Text = text2;
		}
		return entry.Text;
	}
```
—— `CyberElectronics/PhoneTooltipDisplayCache.cs:25-57`

**可复用点**：① 用 `IntPtr`（原生指针）当身份键，绕开托管包装对象的引用不稳定；② 把"技能加成、当前语言、词条摘要"都算进依赖集——**多语言与技能变化的漏缓存是这类模组最常见的 bug，它在这里躲开了**；③ `Clear()` 由 `release`/`quit`/`scene-unloaded` 阶段统一调（`GameplayModule.cs:29-35`），不留跨档脏数据。

---

## 9. 一句话总结

**做"内容量极大"的模组，最该先钉死的是"一张表的 schema + 一份全局顺序清单"**——物品长什么样（`ItemSpec` 12 字段）、补丁按什么顺序装（`patch-order.json`）、每帧按什么阶段跑（`ModuleLifecycle` 的 phase+order）。这三样定下来之后，200+ 个实体就退化成"加一行 JSON"，465 个补丁退化成"加一个文件 + 一个优先级数字"；定不下来，内容量就会以"每个物品一个类、每个补丁一个特例"的形式反噬成 18 万行。

---

## 10. 附：抄它的时候要避开的 4 个坑

1. **别抄四份 6,739 行的契约表**。18,773 条 `assembly:type` 里绝大多数从没被读取过。它自己承认了代价：每程序集一份、共 21,096 行、11.2%。要用就**按需生成**（只收真正 `ManagedAccess` 摸过的成员），而不是全量 TypeRef 闭包。
2. **别抄"排除自己程序集"的安装器写法**。`PatchInstaller` 只装别人、自己那 327 个补丁交给加载器，结果 `patch-order.json` 对它们无效。要装就全装，别让顺序声明有一条暗缝。
3. **12,366 行单文件 UI 是反面教材**。`PhoneTerminalUI` 一个 `static` 类里塞 19 个页面内嵌类 + 手绘 + 反射读控件；它跑得起来是因为作者写了 4 个缓存和 132 处同帧闩，不是因为结构好。
4. **死安全网要接上**。`PatchInstaller` 的 `critical` 机制（关键补丁失败 → `UnpatchSelf()` 保全存档）是全场最值得抄的一条，但唯一调用点传了空数组，等于没开。抄的时候**替它把 critical 集定义出来**（存读档入口、物品注册回读）。

---

*本档案由结构侦察 + 14 文件定点精读产出，未通读全库。所有行数/计数均可用 `缓存文件夹\_ee_files.csv`（1199 行逐文件清单）复算。*