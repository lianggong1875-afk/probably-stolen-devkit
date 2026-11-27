# 鉴定助手 / Appraisal Assistant 蒸馏档案

> 工坊 id `3828921912`｜程序集 `AppraisalAssistant.dll`｜版本 `1.1.1.0`（`Properties/AssemblyInfo.cs:8`）
> 反编译源：`缓存文件夹\decomp_ws\3828921912`（52 个 `.cs` / 约 9165 行，ILSpy 输出，**变量名由反编译器生成**，代码片段原样照抄）
> 订阅量：约 45 万（工坊页面口径，本地不可验证）
> 交叉核对：`共享开发\reference\Assembly-CSharp`（原版反编译，944 个类型）——本文凡涉及原版签名都以此为准
> 读法：**只写源码里有行号的东西**；读不出来的标「（推测）」，绝不编 API。

---

## 0. 一句话结论

它不是"自动鉴定机"，而是一扇**跟着放大镜走的只读答案窗**：玩家自己拿放大镜看货，它在旁边把"真伪 / 正确估价 / 改造体 / 通缉犯"四件事直接念出来；可选一键把正确估价填进原版估价输入框（**默认不自动提交**）。

它**不暴露任何跨模组入口**（无 `ModHelper.Expose`、无 `AppDomain.SetData`、无公开静态服务），也**不 patch 任何鉴定玩法方法**（4 个 patch 全在 UI / 输入 / 本地化）。所以"零反射跟它对接"这件事的正确答案不是"跟它对接"，而是——**它用的那个原版入口我们一条 `using` 就能调，根本不必经过它**。详见 §8。

---

## 1. 它到底做什么

### 1.1 定位：读数 + 讲解，不是代打

| 问题 | 事实 | 证据 |
| --- | --- | --- |
| 自动鉴定？ | **否**。不调 `OnInspectItem`、不代替玩家使用放大镜 | 全目录 grep `OnInspectItem` 零命中 |
| 自动填单？ | **半自动**。有"填入估价"按钮（`fill_estimate` 默认 `true`）与"填入并提交"按钮（`submit_estimate` **默认 `false`**），都要玩家点 | `ModEntry.cs:112-113`、`DemoWindowPlans.cs:279-310` |
| 给玩家提示答案？ | **是，这是它的主体**。四个浮窗同时给"结论 + 明细 + 依据" | `HostDriver.cs:12-18` |
| 覆盖范围 | 任何带 `APPRAISAL_ITEM` 的物品（原版三种 + 别的模组 `ModHelper.AddAppraisalItem` 注册的） | `SafeReadings.cs:92`、`ValueReader.cs:42` |

四个窗（`HostDriver.cs:12-18`，位置是相对屏幕中心的偏移）：

| 窗 id 后缀 | 默认位置 / 尺寸 | 内容 |
| --- | --- | --- |
| `authenticity` | (-280, 60) / 390×300 | 真伪结论 + 5 个检查项逐项红绿 + 每项"为什么" |
| `value` | (-280, -140) / 410×150 | **真实价值** + 可展开的算式明细 + 填单按钮 |
| `augmented` | (280, 160) / 410×145 | 改造体警告 + 压力值 + 观察到的证据 |
| `wanted` | (280, -20) / 370×190 | 通缉警告 + 姓名 + 举报奖励 |

另有荧光莓品质的 tooltip 追加（`HostDriver.cs:233-255`，走原版 `ModHook.OnCreateTooltipLate`）。

### 1.2 核心判定链（一次读数的全貌）

```
0.25s 轮询（HostDriver.Update，HostDriver.cs:127-209）
  └ PlayerStore.HasInstance → isClientArrived → currentClientInstance / isAug / isWanted / augPresence
  └ InspectionUIManager.Instance.panel.activeInHierarchy && currentInspectingItem != null   ← 唯一"门槛"
      └ 真伪：ItemFeature.realCondition.identifier（SafeReadings.cs:192-217）
      └ 瑕疵：5 个 ISSUE_* 布尔 tag（ResultText.cs:5-10）
      └ 估价：AppraisalItemRegistry 里该品类的 getCorrectValue(item)（ValueReader.cs:80-124）
      └ 明细：自己手抄一份原版算式，算完与原生值比对，不等就丢弃明细（DemoWindowPlans.cs:101-110）
  └ 每个窗：structure 不变则只改字，变则重建（DemoContentRenderer.cs:24-58）
```

### 1.3 "正确估价"怎么算出来的（它最硬的一段）

它**不重写算式**——它把原版槽位里的 `Func<GameItem,int> getCorrectValue` 拿出来执行：

1. 反射拿私有静态字段 `AppraisalItemRegistry.types`（`ValueReader.cs:9`，`BindingFlags.Static|NonPublic`，**认准字段类型必须是 `List<AppraisalItemType>`**，否则整条链放弃，`ValueReader.cs:32-36`）；
2. 遍历注册表：`isType == null` 时退化成"比较 `item.identifier == type.itemId`"，否则调 `isType(snapshot)`（`ValueReader.cs:98-110`）；
3. 命中后取 `type.getCorrectValue`，再分两条路：
   - 原版方法（`Method.Module.Assembly == Assembly-CSharp` 且指纹核验通过）→ **直接调用**（`ValueReader.cs:124`）；
   - 外来模组的委托 → 交给自带的 **IL 解释器**执行（`RegisteredPriceReader`，848 行），遇到不支持的指令就整体放弃；
4. `value <= 0` 一律判"read 不到"（`ValueReader.cs:125-129`）——**绝不拿 0 当答案**。

关键设计：它执行时喂进去的不是真物品，而是一个**脱离宿主的克隆**（`PriceSnapshot : GameItem`，`PriceSnapshot.cs:7-14`）：

```csharp
internal sealed class PriceSnapshot : GameItem
{
	internal PriceSnapshot(TagSystem tags)
		: base((string)null, (PixelWindow)null)
	{
		((GameItem)this).state = tags;
		((GameItem)this).modifiedState = tags;
	}
```

`Destroy()` / `Validate()` 直接抛 `NotSupportedException`（`PriceSnapshot.cs:25-33`），意思是"你可以算，但别想动这件货"。原版 `GameItem.Destroy/Validate` 确实是 `abstract`（`reference\Assembly-CSharp\GameItem.cs:648,650`），所以这个子类合法。

### 1.4 "解析"是手抄的原版公式，而且会自校验

`AppraisalValueCalculator`（289 行）等于把 `AppHandHelper.GetCorrectValue` / `PlasmaHelper.GetCorrectValue` / `RibwichHelper.GetCorrectValue` 的加分项**逐条抄成中文人话**：起点 100/300/150、碳复合 +50、镀金 +200、神经接口 +120、磨损 -35、破裂 -150、点火实验室 +750、最低保底 `Finish(50/90/40)`（`AppraisalValueCalculator.cs:8-195`；保底在 `AppraisalValueBreakdown.cs:26-31`）。

抄完之后一定要过一道"对不对"的检查：

```csharp
if (appraisalValueBreakdown != null && !appraisalValueBreakdown.MatchesNative(result))
{
	appraisalValueBreakdown = null;     // 手抄的算式与原版值不符 → 明细整块不显示
}
```

`MatchesNative` 就一句 `Total == nativeValue`（`AppraisalValueBreakdown.cs:20-23`，调用点 `DemoWindowPlans.cs:107-110`）。**抄得不对宁可不说，也不说错的。**

---

## 2. 对外暴露了什么（关键结论）

**零。一条都没有。** 逐条核过：

| 通道 | 有/无 | 证据（全目录 grep 结果） |
| --- | --- | --- |
| `ModHelper.Expose(modId, key, service)` | **无** | grep `Expose` 只命中 `AppraisalValueCalculator.cs:90` 的 `"Exposed screws"`（"螺丝外露"，文案） |
| `ModHelper.OnMessage` / `SendMessage` | **无** | grep 零命中 |
| `AppDomain.SetData` / `GetData` | **无** | grep 零命中 |
| 公开 static 服务类 / 入口 | **无** | 49 个类型里只有 2 个 `public`：`ModEntry : IMod`（`ModEntry.cs:9`）与 `HostDriver : MonoBehaviour`（`HostDriver.cs:8`），其余 47 个全是 `internal`；这两个的成员也只有 `Init/OnEnable/OnDisable`（接口要求）与 `internal` 配置项/方法（`ModEntry.cs:98-123`、`HostDriver.cs:46-116`），**没有任何 `public static` 成员** |
| Patch 别人的方法供别人调用 | **无** | 4 个 patch 全自用（§3） |
| `IsModRunning` 之类互相探测 | **无** | grep 零命中 |
| 写盘 / 写存档结构 | **无** | 只有 `ConfigEntry`（13 个开关 + 4 个窗的 x/y），走原版 `ModConfig`（`ModEntry.cs:100-116`、`ResultWindow.cs:58-59`） |

它对外留下的唯一"地址"是**约定字符串**：
- 程序集简名 `AppraisalAssistant`
- 模组 id `kale.appraisal_assistant`（`ModEntry.cs:11`）
- 四个窗口 id `kale.appraisal_assistant.{authenticity|value|augmented|wanted}`（`ResultWindow.cs:46`）

**这决定了两件事**：
1. 想零反射"调它的判定结果"，没有函数可调——只能①用原版 API 复算（同源，§8-A）、②读它窗口里的文本（同进程 UI 抓取，§8-C）。
2. 它反过来也不需要我们：它全程只碰原版类型，我们装不装、叫什么、patch 什么，它一概不知道（除了下面 §6 的指纹闸门会"看见"我们的 patch）。

---

## 3. 它 Patch 了哪些原版方法（靶点表）

全目录只有 2 个 `new Harmony(...)` 实例、4 次 `harmony.Patch(...)`：

| # | 靶点 | 类型 | 一句话 | 位置 |
| --- | --- | --- | --- | --- |
| 1 | `MainMenuUIController.HandleLocaleDropdown` | Postfix | 玩家点过主菜单语言下拉 → 认为本地化系统就绪（`ready = true`） | `NativeLocalization.cs:48` |
| 2 | `LocHelper.GetCurrentLocaleCode` | Postfix | 拿到非空 locale → 同样置 ready，之后才敢读 locale | `NativeLocalization.cs:49` |
| 3 | `StoreUIManager.ResolveEscape` | Prefix（可跳过原方法） | Esc 先给自己：自家设置窗开着就吃掉 Esc，返回 `true` 拦原版 | `SettingsWindow.cs:55,59-67` |
| 4 | `CustomUIManager.HandleEscape` | Prefix（可跳过原方法） | 同上（`CustomUIManager` 是私有实例方法，用 `BindingFlags.Instance\|NonPublic` 取） | `SettingsWindow.cs:56,69-72` |

**关于题目点名的四个靶点：`InspectionUIManager` / `AppraisalHelper` / `AppraisalServiceHelper` / `PaperUI` —— 一个都没 patch。** 它对它们的全部交互都是"读公开字段/调公开方法"：

| 它读/调的原版成员 | 干什么 | 证据 |
| --- | --- | --- |
| `InspectionUIManager.Instance.panel.activeInHierarchy` | 判断放大镜开没开 | `SafeReadings.cs:49-53` |
| `InspectionUIManager.Instance.currentInspectingItem` | 拿到当前检视的物品 | `SafeReadings.cs:54` |
| `InspectionUIManager.Instance.appraisalPanel` | 判断估价面板开没开 | `PriceInput.cs:59,100` |
| `InspectionUIManager.Instance.apprisalServiceInputField` | 拿估价输入框（注意原版就是这个拼写错误） | `PriceInput.cs:104` |
| `InspectionUIManager.Instance.OnValueChanged(string)` | 填完值后同步 `APPRAISAL_DEFAULT_VALUE` | `PriceInput.cs:144` |
| `InspectionUIManager.Instance.OnAppraisalServiceEstimated()` | 模拟"提交估价"按钮（默认关闭） | `PriceInput.cs:155` |
| `AppraisalHelper.GetCorrectBaseValue` | 只在**指纹核验清单**里出现（不直接调用，是自己走注册表） | `PriceCompatibility.cs:305` |
| `AppraisalItemRegistry.types`（私有） | 反射取注册表 | `ValueReader.cs:9` |
| `AppraisalSubmissionGuard` / `ItemContextHandler.current` 的私有 `currentItem` | 防重复提交 / 右键放大镜识别 | `AppraisalSubmissionGuard.cs:7`、`SettingsWindow.cs:37,204-215` |

副作用清单（它其实会改东西，但都极小）：填单会写 `APPRAISAL_DEFAULT_VALUE` tag（经原版 `OnValueChanged` 落笔，`InspectionUIManager.cs:662-671`）；点"填入并提交"会走原版 `OnAppraisalServiceEstimated` → `AppraisalServiceHelper.OnEstimate` → 物品 `Destroy()`（原版行为）。

---

## 4. 它怎么读真伪 / 有没有泄露

### 4.1 三条独立通道，互不替代

| 通道 | 读什么 | 回答什么 |
| --- | --- | --- |
| 结论 | `item.FindItemFeatureByCategory("CATEGORY_GENUINE_INJECTOR" / "_CIGARETTE" / "_STAMP").realCondition.identifier` → 词表映射成"真品/假品/过期/报废" | **是真是假** |
| 瑕疵 | 5 个 `ISSUE_*` 布尔 tag（注射器 `ISSUE_INDICATOR/LABEL/SERIAL/BODY/COLOR`；香烟与食品券各 5 个） | **哪一项可疑** |
| 为什么 | `INJECTOR_INDICATOR/BODY/COLOR/SERIAL/COMPANY/MODEL`、`CIGARETTE_LETTERING/BOX/SEAL/LOGO`、`STAMP_*` 的原始 int/string → 手写对照表 | **具体哪里不对** |

- 结论只认 `realCondition`，且要求 `!isDisabled && useCondition && realCondition != null`，任何一项不满足就返回"未知"（`SafeReadings.cs:211-216`）。这一点与我们的铁律一致。
- 瑕疵项是"红=有该问题 / 绿=无 / 灰=读不到"三态，不是二态（`ResultText.cs:26-48`）；读不到就打"（未读取）"，**不猜**。
- 讲解表是**按原版枚举值逐条手写**的：例如序列号 7 位、范围 1250000–4559999、前三位批次、`{250,251,301,355,385,386,421}` 召回（`AuthenticityExplainer.cs:126-131, 193-237`）；食品券面值只有 40/80/120/160/200/240 六种、≥160 必须有防伪标（`AuthenticityExplainer.cs:417-452`）。表里的"标准答案"字符串与原版常量一致（`InsInjectorHelper.cs:31-35` 的 `CORRECT_COMPANY/CORRECT_MODEL`）。

### 4.2 有没有 `IsFeatureDiscovered` 门槛？——**没有**

全目录 grep `IsFeatureDiscovered|Discovered` **零业务命中**（只有 1 处 `identified as` 的英文文案）。它的信息门槛是**场景门槛**：放大镜面板开着 + `currentInspectingItem` 非空才显示。

而 `ISSUE_*` 这类 tag 是**物品生成时就写死的**，不是检视后才揭示：

```csharp
public static void SetCounterfeit(GameItem gameItem)
{
	SetGenuine(gameItem);
	if (RNG.Roll(25))
	{
		gameItem.ModifyTag("INJECTOR_SERIAL", ...);
		gameItem.EnableTag("ISSUE_SERIAL");      // ← 造货时就打上了
	}
```

（`reference\Assembly-CSharp\InsInjectorHelper.cs:85-95`，另见 `:82,102,110,143,151`）

**结论（诚实版）**：在"玩家已经把货放进放大镜"这个语境里它的行为可以接受，但严格按我们 `AGENTS.md`/`03-原版接口速查.md:765-785` 的口径，它属于**没有做发现门槛**的一类——放大镜一开就把"这件是假的、假在哪、值多少"全说了。我们若要抄它，这一点不能一起抄。

### 4.3 改造体 / 通缉：直接读客户蓝图真值，门槛更松

`clientBlueprint.isAug / isWanted / realName / reportReward` 全程直读（`SafeReadings.cs:28-38`），`augPresence` 也直接显示。真观察只有"证据链"那层：`IdleAnimator` 的轴/幅度/抑制/抖动（`AugObserver.cs:158-167`）、`SpriteDict` 34 个私有池子判服装风格（`AugObserver.cs:28-44, 173-212`）、打字机完成后的台词关键词（`AugObserver.cs:295-344`）。这部分是**观察**，值得学；结论部分不该学。

---

## 5. UI 形态：浮窗，不是替换面板

- **载体**：原版 `CustomUIManager.Window(id, title, "overlay")` → `CustomUIWindow`；四扇独立的可拖动 overlay 窗，`SetCloseOnEscape(false)`，手动关掉后本件货不再自动弹（换物品才重置，`ResultWindow.cs:78-84, 105-109`）。
- **位置持久化**：每个窗有 `ConfigEntry<float>` 的 x/y，每 5 秒与关窗时写回（`ResultWindow.cs:58-63, 223-239`；节流在 `HostDriver.cs:193-200`）。
- **内容不是塞一段富文本，而是"结构计划"**：`WindowBodyPlan` 只记 `Column/Row/Pair/Text/Button` 节点（`WindowBodyNode.cs:5-27`）；`DemoContentRenderer` 用原版 `CustomUIBuilder` 逐节点建，**结构没变就只改文字**（`plan.SameStructure` → `ApplyContent`，`DemoContentRenderer.cs:30-58`）。
- **高度两遍收敛**：先按宽度算 body → `RebuildLayout` → `GetPreferredHeight`；若超出屏幕且还没用满宽度，就加宽重来一次；拿到无效高度直接抛 `InvalidOperationException`（`ResultWindow.cs:118-158`）。异常时 `Backoff()` 关窗 + 2 秒不重试（`ResultWindow.cs:276-280`）。
- **长相贴近原版**：`WindowChrome` 克隆原版窗自带的关闭按钮、改名 `AppraisalAssistant.Size` 做成"缩放"钮，字体字号抄 `frame.titleText`，找不到图标就退化成 `↗` 文本；三档缩放 0.75/1/1.5（`WindowChrome.cs:22, 51-116`）。
- **触发**：
  - 答案窗 = **自动**（0.25s 轮询，见 §1.2）；
  - 设置窗 = **右键放大镜**：轮询 `ItemContextHandler.current` 的私有字段 `currentItem`（反射一次缓存），`identifier == "magnifier"` 且换了目标就 `Open()`（`SettingsWindow.cs:37, 204-227`）。
- **Esc 权**：两处 Prefix + 五层谦让（文本框聚焦、原生菜单/结束日/网络 UI 开着、有别的 overlay 层排在它后面、`OverlayHandler.currentCancelAction` 非空，`SettingsWindow.cs:86-183`）——这套"抢键前先让一圈"的写法比随便 `Input.GetKeyDown` 拦掉要稳。

---

## 6. 与其它模组的兼容设计

**没有的东西**：`IsModRunning`、`HarmonyAfter/Before/Priority`、`[HarmonyPriority]`、为别人留的开关——一个都没有（grep 零命中）。它的 13 个 `ConfigEntry` 全是功能开关（`ModEntry.cs:117-121`）。

**它有的是三层"宁可不说话"的防御**：

1. **IL 指纹核验**：把原版关键方法编译成 `签名 → SHA256(Base64)` 的硬编码表（`PriceCompatibility.cs:303-348`，`SnapshotSafety.cs:61-111`），逐条比对；指纹不符（游戏更新 / 别的模组改了方法体）→ 整段读数关闭。哈希算法在 `SemanticMethod.cs:340-341`。
2. **Patch 检测**：`Harmony.GetPatchInfo(method)` 有 prefix/postfix/transpiler/finalizer 就算"被动过"，沿依赖图递归（上限 512 个方法）全部检查一遍（`PriceCompatibility.cs:48-60, 79-113`）。被污染就 `Unsupported("patched dependency: …")`。
3. **外来规则不信任执行**：别的模组注册的 `isType`/`getCorrectValue` 委托，它**不用 `Invoke`**，而是用自带 IL 解释器逐条模拟（只放行白名单指令、限制 20000 步、递归 24 层、静态构造函数要过安全检查，`RegisteredPriceReader.cs:147-167, 169-171, 711-847`）。

于是它留给"兼容补丁"的口子不是 API，而是**行为契约**：*别动它核验过的那批方法，它就一直是准的；动了，它就闭嘴显示"当前估价规则尚未核验"，绝不给你一个错的数*（文案见 `SafeReadings.cs:122-133`）。

### ⚠️ 与我们自己的一条硬冲突（可复现的读码结论）

`深空电玩` patch 了 `GameItem.IsTag(string)`（`深空电玩\src\ArcMarket.cs:541-550`，纯增量 Postfix：只在自家 selector 上把 `false` 改成 `true`）。

- `GameItem.IsTag` 正是它指纹表里的 `Common[0]`（`PriceCompatibility.cs:24-30`），也是解释器路径上必须过 `ReadMethodVerified` 的调用（`RegisteredPriceReader.cs:610-622`）；
- 而 `ReadMethodVerified` → `UnpatchedGraph` → `HasPatches(GameItem.IsTag)` → **命中我们的 patch** → `"patched dependency: …"`（`PriceCompatibility.cs:91-95`）；
- 但**原版三个品类不受影响**：它们的 `getCorrectValue` 在 `Assembly-CSharp` 里且 `PriceVerified` 通过，走的是直接调用分支，不查 patch（`ValueReader.cs:124`）；
- **受影响的是"第三方注册的鉴定品类"**（走 `RegisteredPriceReader.Read`）：一旦它的委托里出现 `item.IsTag(...)`，解释器就整体放弃 → 该品类显示"该品类尚未适配"。

缓解（按代价排序）：① 不改我们这条 postfix（原版三品类仍可读，损失有限）；② 真要让自家鉴定品类被助手读懂，注册 `ModHelper.AddAppraisalItem` 时**判定委托改读 `item.modifiedState.dict`**，别直接 `item.IsTag(...)`（解释器对 `get_dict` / `TryGetValue` / `ContainsKey` 有专门放行分支，`RegisteredPriceReader.cs:631-656`）。②是读码推断，需真机验一次（推测）。

---

## 7. 可复用模式（代码原样 + 行号）

### 模式 A｜"读不到就说读不到"的判定数据结构

三态（有/无/读不到）比二态诚实得多。它是这么组织的：**类型下标 + 5 个 `bool?` + 每项独立 try**。

```csharp
		result.Authenticity = ReadPart(() =>
		{
			int num = AuthenticityType(result.Item);
			result.AuthApplicable = num >= 0;
			if (num >= 0)
			{
				bool?[] array = new bool?[5];
				for (int i = 0; i < array.Length; i++)
				{
					try
					{
						array[i] = IsTag(result.Item, ResultText.IssueKeys[num][i]);
					}
					catch (Exception obj3)
					{
						report(obj3);
					}
				}
				result.Issues = ResultText.Issues(num, array, locale);
			}
			return Authenticity(result.Item, locale);
		}, text, report);
```

`SafeReadings.cs:68-89`。配套：结果载体是纯字段结构（`BasicSnapshot.cs:3-40`，含 `AuthApplicable`/`ValueApplicable` 两个"这条线适不适用"的布尔），渲染时按 `bool?` 上三色（`ResultText.cs:26-48`：红 `#BE514C` / 绿 `#00B500` / 灰 `#B8B8B8`）。**能不能显示**与**内容是什么**分开存，界面上就不会出现"没有数据的绿勾"。

### 模式 B｜把答案做成只读浮窗（原版 CustomUIManager，自绘 body）

```csharp
			if (window == null || !window.IsAlive || window.IsVisible)
			{
				Nullable val = ((window != null && window.IsAlive) ? new Nullable(TopLeft(window.Rect)) : ((Nullable)null));
				bool flag = window == null || !window.IsAlive;
				if (flag)
				{
					CustomUIBuilder val2 = CustomUIManager.Window(id, title, "overlay");
					window = val2.Handle;
					val2.SetSize(width, height).SetPosition(position ?? initialPosition).SetDraggable(true)
						.SetCloseOnEscape(false)
						.OnClose((Action)(() =>
						{
							SavePosition();
							dismissed = true;
						}))
						.Show();
					chrome.Attach(window);
					Debug.Log((object)("[Appraisal Assistant] Opened " + id));
				}
```

`ResultWindow.cs:95-113`。要点：① 窗口死掉（`!IsAlive`）或被人看见了才重建，**不做每帧重建**；② 重建前先记下左上角 `TopLeft(rect)`，重建后把位置补回去（`ResultWindow.cs:146-150`），玩家拖过的位置不会因为"换了一件货"跳回原点；③ `OnClose` 里只 `SavePosition + dismissed`，**不碰业务状态**——关窗是纯 UI 事件。

### 模式 C｜不碰别人的方法，但必须确认它还是原来那个（指纹闸门）

这是全文最值得抄的一段，17 行说明"为什么要拦"：

```csharp
	internal static bool ReadMethodVerified(MethodBase method, out string reason)
	{
		reason = null;
		if (!ExactGraphVerified(method))
		{
			reason = "native contract: " + SemanticMethod.Signature(method);
			return false;
		}
		return UnpatchedGraph(method, new HashSet<MethodBase>(), out reason);
	}

	private static bool UnpatchedGraph(MethodBase method, HashSet<MethodBase> visited, out string reason)
	{
		reason = null;
		if (!visited.Add(method))
		{
			return true;
		}
		if (visited.Count > 512)
		{
			reason = "native read dependency limit";
			return false;
		}
		if (HasPatches(method))
		{
			reason = "patched dependency: " + PatchedMethod(method);
			return false;
		}
```

`PriceCompatibility.cs:68-95`。配合 `HasPatches`（`PriceCompatibility.cs:48-60`，`Harmony.GetPatchInfo` 数四类补丁）与硬编码指纹表（`PriceCompatibility.cs:299-350`）。**它的价值不在"防别人"，而在"给自己的输出标注可信度"**：状态不可信时输出"未核验"，而不是输出了一个漂亮但错的数。我们做鉴定/定价类功能，最该抄的就是这个态度。

### 模式 D｜与原版单据/估价框交互：靠"持久监听器"反查按钮，而不是猜对象名

填单前必须确认"那个按钮真的调 `OnAppraisalServiceEstimated`"，否则宁可不让点：

```csharp
		InspectionUIManager instance2 = InspectionUIManager.Instance;
		Button val = null;
		Button[] componentsInChildren = instance2.appraisalPanel.GetComponentsInChildren<Button>(false);
		foreach (Button val2 in componentsInChildren)
		{
			if (!((Behaviour)val2).isActiveAndEnabled || !((Selectable)val2).IsInteractable())
			{
				continue;
			}
			for (int j = 0; j < ((UnityEventBase)val2.onClick).GetPersistentEventCount(); j++)
			{
				if (((UnityEventBase)val2.onClick).GetPersistentTarget(j) == (Object)(object)instance2 && ((UnityEventBase)val2.onClick).GetPersistentMethodName(j) == "OnAppraisalServiceEstimated" && (int)((UnityEventBase)val2.onClick).GetPersistentListenerState(j) != 0)
				{
					if ((Object)(object)val != (Object)null)
					{
						return null;
					}
					val = val2;
				}
			}
		}
		return val;
	}
```

`PriceInput.cs:57-79`。同一个方法里还硬性要求：`PlayerStore.Instance` 引用相等、`clientIntent == 14`（`APPRAISAL_SERVICE`）、`currentInspectingItem` 引用相等、`appraisalPanel` 与输入框都 `activeInHierarchy`、输入框 `readOnly == false`（`PriceInput.cs:81-113`）。填完之后**回读校验**：`input.text != text` 或 `APPRAISAL_DEFAULT_VALUE != value` 就抛 `"Estimate input did not synchronize"`（`PriceInput.cs:142-148`）；提交用 `ConditionalWeakTable<GameItem, object>` 一件货只能提交一次（`AppraisalSubmissionGuard.cs:7-35`）。

---

## 8. 对我们最有用的一条

### 8.1 现状差距（不是"差在哪"，是"我们压根没做那块"）

| 维度 | 我们现在（`ArcAppraisalPane` + `ArcAppraisalReadout`） | Appraisal Assistant |
| --- | --- | --- |
| 覆盖物品 | **只认自家 `gd_*` / `hw_*`**（`ArcAppraisalReadout.cs:70-121`） | 任何 `APPRAISAL_ITEM`，含别的模组注册的（`SafeReadings.cs:92`） |
| 给的结论 | 真伪 / 成色 / 疑点 / 标签是否骗人 | 同上 **+ 正确估价 + 改造体 + 通缉** |
| 估价 | **完全没有**（`src` 全局 grep `GetCorrectBaseValue` 零命中） | 有，且带可展开算式 |
| 挂点 | Harmony `OnInspectItem` Postfix 挂组件（`ArcInspectPatches.cs:7-17`） | 不 patch，纯 0.25s 轮询 |
| 不可信时的表现 | `catch` 一次 + `ArcAppraisalLog.Once` | 指纹/补丁/解释器三道闸门 → 明确文案"尚未核验" |
| 门槛 | 放大镜面板开着（同一个门槛） | 同一个 |

**一句话**：我们做的是"自家货的真伪读数"，它做的是"所有可鉴定货的真伪 + 价格 + 客户风险"。**"正确估价"这一栏我们空着，而那是整个估价玩法里唯一值钱的那个数。**

### 8.2 能不能零反射拿到它的判定结果？

分三层，从最好到最差：

#### A（推荐）不需要它——用同一入口自己算，一行代码，零反射零 patch

它自己也是这么算的（`ValueReader.cs:80-124` 走注册表 → `getCorrectValue`），而这条链在原版里是**公开 API**（`reference\Assembly-CSharp\AppraisalHelper.cs:47-61`，原文照抄）：

```csharp
	public static int GetCorrectBaseValue(GameItem gameItem)
	{
		if (!gameItem.IsTag("APPRAISAL_ITEM"))
		{
			return 0;
		}
		AppraisalItemType appraisalItemType = AppraisalItemRegistry.Find(gameItem);
		int num;
		if (appraisalItemType == null)
		{
			num = 0;
		}
		else
		{
			num = appraisalItemType.getCorrectValue(gameItem);
```

`AppraisalItemRegistry.Find` / `GetAll` / `AppraisalItemType.Matches` / `.getCorrectValue` **全是 public**（`AppraisalItemRegistry.cs:54-67`、`AppraisalItemType.cs:3-50`）。所以我们照它抄也不用反射：

```csharp
// DeepSpaceArcade：零反射拿到"正确估价"（原版公开入口 + 边界捕获）
internal static bool TryTrueValue(GameItem item, out int value)
{
    value = 0;
    if (item == null || !item.IsTag(AppraisalHelper.APPRAISAL_ITEM)) return false;   // 原版就有这个 const
    try
    {
        int v = AppraisalHelper.GetCorrectBaseValue(item);   // 与助手同一个数
        if (v <= 0) return false;                            // 0 = 品类没注册 / 算式返回 0，一律当"读不到"
        value = v;
        return true;
    }
    catch (Exception e)   // 别的模组注册的 getCorrectValue 会在这里执行，异常是它的
    {
        ArcLog.Warn("appraisal true value failed: " + e.Message);
        return false;
    }
}
```

三个必须知道的细节：
1. `GetCorrectBaseValue` 在品类未注册时会 `Debug.Log("Correct Value not Found…")`（`AppraisalHelper.cs:67`）——**先 `IsTag("APPRAISAL_ITEM")` 再调**，别给日志刷屏；
2. 它直接执行**别的模组**注册的 `getCorrectValue` 委托，抛异常就是我们的 `catch`；我们这条 `catch` 属于"跨模组边界"，符合 `AGENTS.md` 的异常策略；
3. 结果与助手**逐位相等**，因为原版自己在 `AppraisalServiceHelper.OnEstimate` 里也是调这一个方法算判定（`AppraisalServiceHelper.cs:160`），精度分档是 `±5%` 完美 / `±10%·15%·25%` 逐级（`AppraisalServiceHelper.cs:70-107`），低于 50% 或高于 150% 直接被客户拒（`AppraisalServiceHelper.cs:140-147`）。

#### B（要更强健）抄它的"快照 + 闸门"，但别抄 IL 解释器

如果以后我们的判定/定价要读**别的模组注册的规则**，或者怕外来委托改我们的物品，`PriceSnapshot : GameItem`（`PriceSnapshot.cs:7-33`）这个"克隆一份 `TagSystem`、`Destroy/Validate` 抛异常"的写法是可以直接用的——`GameItem` 是 `public abstract`、构造函数 `public GameItem(string, PixelWindow)`（`reference\Assembly-CSharp\GameItem.cs:6,162`），子类合法，且不引用任何外部程序集。**唯一别抄的是那 848 行 IL 解释器**（`RegisteredPriceReader`）：那是为了"不信任别的模组的代码"，代价与维护量都极高，对我们没有对等收益。

#### C（能做但不建议）读它窗口里的文字

同进程、纯原版类型、**零反射**——因为它的输出就是一个原版 `CustomUIWindow`，而 `CustomUIManager.GetWindow(string)` 是 public（`CustomUIManager.cs:524`），`CustomUIWindow.ContentRoot` / `IsAlive` / `IsVisible` 也全 public（`CustomUIWindow.cs:12-62`）：

```csharp
// 只是可行性演示：不推荐落到代码里
CustomUIWindow w = CustomUIManager.Instance.GetWindow("kale.appraisal_assistant.value");
if (w != null && w.IsAlive && w.IsVisible)
{
    foreach (TMP_Text t in w.ContentRoot.GetComponentsInChildren<TMP_Text>(true))
    {
        string s = t.text;          // 形如 "真实价值" / "1234"，靠字符串解析
        // ...
    }
}
```

为什么**不建议**：① 文本没有契约（它下个版本改一个字就崩）；② 依赖别人的窗口 id 常量 `kale.appraisal_assistant.value`（`ResultWindow.cs:46`），本质是硬编码耦合；③ 它只在放大镜开着时才有窗，我们照样得自己判断时机——既然要自己判断时机，不如自己算（回到 A）。**唯一合理用途**：做一次性真机验证，拿它的数字反过来校验我们 A 方案算得对不对。

### 8.3 结论

- **能零反射拿到它的"结果"，但拿法不是"从它那里拿"**：它和我们都只是原版 `AppraisalHelper.GetCorrectBaseValue` 的两个消费者。我们补上这一行调用，就自动获得与它完全相同的估价，还顺带覆盖所有第三方注册过的鉴定品类。
- **它没有可对接的服务，所以"共存"成本几乎为零**：装上它，玩家看到两扇窗（它四扇、我们一扇），互不干扰，也不抢同 id 窗口。
- **唯一需要守的共存红线**：别 patch `AppraisalHelper.GetCorrectBaseValue` / `GameItem.IsTag` / `AppHandHelper|RibwichHelper|PlasmaHelper.GetCorrectValue` / `AppraisalItemType.Matches` / `AppraisalItemRegistry.Find`（都在 `PriceCompatibility.cs:303-348` 的指纹表里）。我们现在碰了 `IsTag`（`ArcMarket.cs:541`），只影响它"读第三方注册品类"那条路，原版三品类无损（§6）。

---

## 对我们的可用点

1. **立刻可补**：`ArcAppraisalPane` 里加"正确价值"一行，来源 `AppraisalHelper.GetCorrectBaseValue(item)`（§8-A）。这是全篇最高性价比的一条，零反射、零 patch、与助手逐位一致。
2. **读数结构**：把 `AppraisalReport` 拆成"这条线适不适用（`ValueApplicable`）+ 值 + 读不到的原因"三件（`BasicSnapshot.cs:3-40`），文案层只在适用时渲染，避免出现"没有数据的绿勾"。
3. **三态上色**：有 / 无 / 读不到 → 红 / 绿 / 灰 + "（未读取）"（`ResultText.cs:26-48`）。我们现在 `IssuesKnown` 只做了二态。
4. **可信度标注**：任何"读游戏内部状态"的展示都带一句来源/可信度脚注（它用"未核验"文案，我们用 "<color=#BDB6B0>只读 · 深空电玩</color>"，方向对，可再细化成"已核验 / 待核验"）。
5. **窗口卫生**：`!IsAlive` 才重建、重建前后补位置、`OnClose` 只存位置不碰业务（`ResultWindow.cs:95-155`）——比我们现在"每次 stamp 变了就 Close+重新 Show"更少闪。
6. **两遍布局收敛 + 越界 clamp**（`ResultWindow.cs:118-151`）：我们的浮窗目前是固定尺寸，长文案（疑点很多时）会挤。
7. **别抄的**：848 行 IL 解释器、`ISSUE_*` 无发现门槛直读、`isAug/isWanted/realName` 无门槛直读、抢 Esc 的两处 Prefix（我们已有自己的 Esc 约定）。
8. **可注册鉴定品类的兼容提示**：若《深空电玩》以后走 `ModHelper.AddAppraisalItem` 注册自己的鉴定品类，判定委托**别直接调 `item.IsTag(...)`**，改读 `modifiedState.dict`——否则助手的解释器会因为我们自己的 `IsTag` patch 整体放弃（§6，需真机确认）。

---

## 附：取证方式与未验证项

**怎么读的**：① 全目录 grep 关键词（`Harmony|HarmonyPatch|ModHelper\.|AppDomain|SetData|Expose|IsModRunning|SendMessage|OnMessage|IsFeatureDiscovered|realCondition|fakeCondition`）；② 逐个读 16 个关键文件（`ModEntry` / `HostDriver` / `SafeReadings` / `ValueReader` / `PriceCompatibility` / `RegisteredPriceReader` / `PriceSnapshot` / `SnapshotSafety` / `SemanticMethod` / `ResultWindow` / `SettingsWindow` / `DemoWindowPlans` / `DemoContentRenderer` / `AuthenticityExplainer` / `AugObserver` / `PriceInput`）；③ 与原版反编译交叉核对（`AppraisalHelper` / `AppraisalItemRegistry` / `AppraisalItemType` / `InspectionUIManager` / `ItemFeature` / `InsInjectorHelper` / `AppraisalServiceHelper` / `CustomUIManager` / `CustomUIWindow` / `GameItem`）。

**未验证（一律标推测）**：
1. 它在 ModLoader 清单里声明了哪些能力——反编译产物没有 `manifest.xml`。按 IL 调用点看只反射 vanilla 类型（`typeof(StoreClientMono)` / `typeof(SpriteDict)` / `typeof(ItemContextHandler)` / `typeof(CustomUIManager)` …），理论上不该被判 `reflection-external`；但它对**外来委托**的 `MethodInfo` 做 `GetMethodBody()/Invoke`，扫描器口径未知。（推测）
2. 四个窗的默认位置在 1080p / 4K 下会不会重叠或压住原版估价面板——代码里只有 `WindowChrome.Clamp`，没读到更多线索。（推测）
3. §6 那条"我们的 `IsTag` patch 让它对第三方注册品类闭嘴"，是从 `ValueReader.cs:124` 与 `RegisteredPriceReader.cs:610-622` 读出来的分支判断，**未真机验证**。（推测）
4. `AugExplainer` / `AugDialogueLocalization`（21 KB + 15 KB）只读了 `AugObserver` 的取数与它汇总的部分，具体台词关键词表未逐条核。
