# 埃莉诺剧情扩展 / Eleanor Story Expand（工坊 3834588749）

> `manifest.ID = eleanor_romance` ｜ `ModVersion 1.42` ｜ 程序集 `EleanorRomance.dll`（547,840 B）｜ `<Capabilities />` 空（零能力）｜ 游戏版本声明为空并带注释说明「不按版本号停用剧情」（`manifest.xml`）｜ 反编译源码 `缓存文件夹\decomp_ws\3834588749`（125 个 `.cs` / 26,550 行，命名空间 `EleanorRomance`）｜ 透视报告 `缓存文件夹\decomp_ws\_reports\3834588749.txt`（58 处特性式 Patch）
> **为什么值得读**：它是本库唯一一个**把「好感」绑在玩家商业行为上而不是送礼上**的成品。而且它做到这件事的方式极其省事——**没有给价格路径打任何补丁**，全靠原版 `StoreClient` 挂在客人身上的 11 个公共委托/对白字段 + 原版 `ItemFeature` 里「这一单的价格是怎么谈成的」那 8 个 feature id（`StoryEngine.cs:31-156`）。这套结构可以直接搬到我们的顾客/关系系统上。
> 反过来它也有**该反着学的地方**：好感值算出来了却从不读，真正放行剧情的是 `done.<章>` 布尔事实（见 §4.1）。「记账」这一条根本没进好感，而是另一套分红账本（`MaidDividends.cs`）。

## 0. 速览

| 项 | 值 |
| --- | --- |
| 入口 | `EleanorRomance\ModEntry.cs`：`public class ModEntry : IMod`（109 行）；**无热重载钩子**（无 `HotReloadPrepare/Activate`） |
| 观测点 | 6 个 `ModHook`（`OnGameLoadedEarly/Init/Late`、`OnGenerateCustomerEarly`、`OnModItemDirectoryInit`、`OnNewGameStarted`，`ModEntry.cs:33-39`）、29 个 `ModHelper` 方法、**58 处特性式 Patch**、0 处 IL Transpiler |
| 打包 | 32 MB ／ `Localization\` 4 个 CSV（zh/en/ja/ko，各 3,157 行）＋ `Textures\Items\` **155 张 PNG**（30 MB）＋ `preview.png` ＋ 80 KB `README.md`（自带完整变更史，等于半份设计文档） |
| 四条路线 | 共同线（`Chapter` 1→24）＋ 恋爱 / 后宫（Dual）/ 侍从（Maid）/ 革命军（Rev）四选一，各带 v2 重制版状态（`RomanceState.Route`） |
| 存档 | 全部落 `ModHelper.*ModData*` 私有键，前缀分域：`er.`（主线）/ `er.maid.` / `er.dual.` / `er.love.` / `er.core.` / `er.biz.` / `dividend.`；唯一独立文件是结局解锁 `meta.json`（`MetaProgress.cs:24-35`，走 `ModHelper.*ModDataFile*`） |
| 无 `storeClientDict` | 自造 `StoreClient` 后 `manager.AddClient`／`AddNextClient`，`Patch_CheckStoryLineEleanor` 把原版排期整条拦掉 |

## 1. 规模与结构：26,550 行怎么花的

去空行去注释实测 **24,680 行**，按职责三类（脚本自算，非估算）：

| 类别 | 文件 | 行 | 占比 | 说明 |
| --- | --- | ---: | ---: | --- |
| **剧本数据表**（`*Content.cs`） | 15 | 8,014 | 32% | `Dictionary<blockId, Line[]>`，只有「说话人 + 本地化键」 |
| **剧情/交易逻辑**（`*Story/Director/State/Business`） | 52 | 15,639 | 63% | 访客装配、状态机、账本、UI、立绘 |
| **Harmony 补丁**（`*Patch*.cs`，57 个文件） | 57 | 1,027 | 4% | 平均 18 行/个，绝大多数是「关掉原版一件事或转交给自家」 |

**文本 vs 逻辑的真实比例**：代码里的 `Line` 条目共 **2,633 条**（`StoryV06Content` 688 + 其余 Content 1,945），每条对上一个本地化键；文本本体全在 CSV 里，中文 **3,153 键 / 55,806 字**、英文 193,422 字符、日文 85,237、韩文 99,354。也就是说：

> **正文一个字都不在代码里**（代码只有 `er_v06_c09_n102_01` 这种键），**逻辑里最重的是访客装配而不是对话**。`EleanorStory.cs` 单文件 2,066 行进不了「文本」——它 90% 是「第 N 章她怎么登场、拿什么货、预算多少、走的时候说什么」。

单文件前 8 名（非空非注释行）：`EleanorStory.cs` 1,951 ／ `StoryV06Content.cs` 1,686 ／ `MaidV2Content.cs` 1,339 ／ `RomanceState.cs` 1,185 ／ `DualContent.cs` 1,090 ／ `RevolutionV2Content.cs` 1,016 ／ `PortraitCompat.cs` 969 ／ `LoveContent.cs` 957。

**角色职责分层**（这是本模组最值得抄的工程结构）：

```text
ModEntry          只订阅钩子 + 注册（109 行，薄）
StoryDirector     唯一的访客调度总闸（OnGenerateCustomerVeryEarly，优先级链，StoryDirector.cs:124）
{章}Story         一章一个文件：造客人 + 挂委托 + 攒 DealTracker + 选反应块
{线}State         持久化（RomanceState/MaidState/DualState/LoveState，纯 getter/setter 到 ModData）
{线}Content       剧本表（blockId → Line[]）
StoryEngine       对话/演出原语（Build/Append/Tail/AddChoices/InterceptDismiss/DealTracker）
*Patch*.cs        平均 18 行，只做「拦掉原版」或「转交给自家」
```

## 2. 它怎么做这件事

### 2.1 「信任」的数据结构：5 个计数器 + 1 个死值

**没有独立表，没有 JSON**，全在 `ModData` 私有键（`eleanor_romance` 命名空间，`er.` 前缀），`RomanceState.cs:1276-1284`：

```csharp
private static int GetInt(string key, int fallback) => ModHelper.GetModDataInt("eleanor_romance", "er." + key, fallback);
private static void SetInt(string key, int value)      => ModHelper.SetModDataInt("eleanor_romance", "er." + key, value);
```

| 字段 | 键 | 谁加 | 加减 | 上限 |
| --- | --- | --- | --- | --- |
| `FairDeals` | `er.fair_deals` | 公道成交 | +1/次 | 无（`Mathf.Max(0,·)`） |
| `Discounts` | `er.discounts` | **玩家让价给她**（成交价高于市价，或她买时玩家降价） | +1/次 | 同上 |
| `Overcharges` | `er.overcharges` | 玩家把货卖她卖贵了（>市价 125%） | +1/次 | 同上 |
| `Lowballs` | `er.lowballs` | 玩家压她价（砍价/blackmail（敲诈）/威胁成功） | +1/次 | 同上 |
| `NoSaleVisits` | `er.no_sale_visits` | 她来了一趟什么也没成交 | +1/次 | 同上 |
| `Affection` | `er.affection` | 上面五项的派生 | **F+1 / D+2 / O−1 / L−2** | **0–99 硬夹**（`RomanceState.cs:168-179`） |

**关键发现（§4.1 详述）：`Affection` 只写不读。** 全模组对它的读只有 `DebugTools.cs:645` 的一句 `StoryDirector.Notify("Affection: " + ...)`。剧情门槛一律用 `Chapter`／`Route`／`Romance`／`StoryCoreFacts.Done(n)` 这些**布尔事实与章节号**。

`StoryCoreFacts.cs:45-52` 才是真正的"剧情进度账"：

```csharp
public static bool Done(int chapter)  => Flag("done." + chapter);       // er.core.done.<n>
public static void Record(int chapter) => Flag("done." + chapter, true);
```

外加 `er.core.coop.pending` / `perfume.pending` / `locket.refused` 这类**跨路线共享事实**（`StoryCoreFacts.cs:1-84`）。**读档恢复 = 这些键本来就在 ModData 里**，没有任何"回放"代码。

### 2.2 判定「这一单是怎么谈成的」：`StoryEngine.DealTracker`

唯一真相是原版物品上的 `ItemFeature`——原版在砍价 / 威胁 / blackmail（敲诈）/ 对峙时会把「价格是怎么谈成的」写成一条临时 feature 挂在物品上。`DealTracker.Record(item, paid, eleanorIsSeller)` 把它翻译成 4 个布尔：

| 原版 feature identifier | 玩家行为 | 模组判为 | 好感 |
| --- | --- | --- | --- |
| `confrontSucessDiscount` | **检查出假货/缺陷并指出，卖家自降价**（原版 `ItemFeature.FeatureType.TemporaryBuying`，`SetPreExposedValueMod(-15)`，`ItemFeatureList.cs:388-397`） | **Fair**（公道） | **+1** |
| `bargainBuyingDiscount` / `blackmailDiscount` / `threatenDiscount` | 玩家压价成功（砍价/blackmail（敲诈）/威胁） | **Lowball** | **−2** |
| `bargainBuyingMarkup` | 玩家主动加价 | Discount | +2 |
| `bargainMarkup` / `blackmailMarkup` / `threatenMarkup`（玩家是卖方时） | 玩家抬价卖她 | **Overcharge** | **−1** |
| 无 feature，比值 `paid/市价` ∈ [0.85, 1.25] | 正常成交 | **Fair** | +1 |
| 无 feature，比值 >1.25 / <0.85 | 明显贵/便宜 | Overcharge / Discount | −1 / +2 |

市价取原版 `GameItem.GetNegociatedValue()`（`StoryEngine.cs:186`，含 innate/market 倍率与事件加成），退回 `GetBaseValue()` → `unitValue` → `max(1, paid)` 三级兜底。

**这就是卖点里那句"你靠识货…赢得她的信任"的真实实现**：不是"给出正确估价"这种 UI 判定，而是**读原版已经写好的 feature**——「检查出问题 → 卖家自己降价」在原版被记成一条 `-15%` 的临时买入 feature，模组把它解读成"这单是公道价，不是压价"。1.22 的更新日志专门修的就是这个：*「检查出假货/缺陷并成功指出后，卖家会自行降价；这种成交不再被 DealTracker 误判为玩家压价」*；1.24 又修了一次：*「`DealTracker` 不再用『成交价低于市场价 15%』兜底判定玩家压价；压价只来自原生砍价/黑名单/威胁操作」*。

### 2.3 四个动作 → 四个挂点（逐条到方法）

| 动作 | 原版可读信号 | 挂在哪（原版方法 / 委托） | 模组落点 | 结果 |
| --- | --- | --- | --- | --- |
| **识货** | `ItemFeature"confrontSucessDiscount"`（原版对峙/检查成功后的临时折扣） | **不打补丁**：`StoreClient.onItemBought` / `onItemSold` 委托 → `item.FindItemFeatureByID(id)` | `StoryEngine.cs:91,100-103`（判 Fair）、`EleanorStory.cs:203-206`（挂委托） | FairDeals+1、好感+1 |
| **公道价** | `GameItem.GetNegociatedValue()`（市价）、`BargainUIManager.GetPreBargainValue`（可还原砍价前价，`BargainUIManager.cs:1418`） | 同上委托；手镯走 **`GameItem.GetNegociatedValue` 的 Prefix+Postfix 双写**（`Patch_PreludeBraceletPrice.cs:8,34`） | 比值带 `StoryEngine.cs:115-135`（玩家是卖方）、`StoryEngine.cs:98-111`（她卖货，不压价即 Fair）；手镯硬门 `EleanorStory.cs:1103` `price*10 >= market*9`（≥90% 基价 = 公道） | Fair +1／Discount +2 |
| **记账** | **没有原版信号，自己记** | `StoreClient.onItemBought` 逐笔累加 + `onDealAccepted` 收尾（`MaidBusiness.cs:106-131,204-230`） | 每张单一个 receipt 键：`{clientId}.stock`、`order.spent`、`order.tools`、`order.materials`；分红三本账 `pending/earned/paid`（`MaidDividends.cs:20-28,77-110`） | **不进好感**，进「分红率 200→600 / 单子结没结清 / 逾期作废」 |
| **担风险** | `StoreClient.arrestable`、`onItemAddedToCounters`、`PlayerStore.ArrestCurrentClientForStolenGoods()` | 客人实例委托 + 一个 0.3 秒延迟复核（`EleanorStory.cs:1951-1990`） | 接脏货箱：`UncleAccepted`／`CrateKept`／`TrapSprung`／`KnewTrapUnwarned`／`RescueMethod`（`RomanceState.cs:397-540`）；接订单：`order.state` 0→1→2→4（`MaidBusiness.cs:133-151`） | 走**剧情分支 + 逮捕/保释**，不进好感 |

"记账"这一条最值得抄：**它没有把"记账"做成一堆数字，而是做成一串可对话的物件**。任务单收 2 件工具 + 1 件材料（`AcceptsOrderItem`，`MaidBusiness.cs:167-180`），每收一件打 `ER_ORDER_DELIVERED` 标签防重收；凑齐→`order.state=2`→`OverrideBudget(0)` + `CompleteBusiness()` 立刻结束交易；预算花光但没凑齐→`order.state=4`；逾期 4 天→作废并 `Notify`。结束时不是弹数字，而是**念收据**：

```csharp
// MaidBusiness.cs:227-240
c.mainDialogue = Say("order_collect", Number("order.tools"), Number("order.materials"),
                     Math.Max(0, Number("order.budget", 600) - Number("order.spent")));
StoryEngine.InterceptDismiss(c, () =>
{
    string key = (Number("order.state") == 2) ? "order_done"
               : ((Number("order.spent") >= Number("order.budget", 600)) ? "order_budget" : "order_partial");
    return MaidDividends.WithReceipt(Say(key));   // ← 账本数字直接变成台词
}, () => { /* 结清判定 + order.next = today + 2 */ });
```

### 2.4 她怎么作为顾客进来（不用 `storeClientDict`）

```text
ModHook.OnGenerateCustomerEarly  →  StoryDirector.OnGenerateCustomerVeryEarly(manager)   [StoryDirector.cs:124]
  ↓ 依次试：救母/归案 → 闭店日 → 后宫 → 侍从主线 → 侍从生意 → 恋爱 → 路线重置提示 → 尾声 → 结局后日常
  ↓ （任一命中就 return，形成显式优先级链，StoryDirector.cs:162-205）
EleanorStory.CreateClient(chapter)  →  NewEleanorClient(id, sprite, intent)              [EleanorStory.cs:22,89]
  ↓ identifier = "er_story_" + id；dismissable=false、arrestable=false、nonShootable=true、staysAfterFailedBargain=true
  ↓ StoreClientFactionSetup.InitUpperLevelCitizen(val)
ModHelper.SetClientSource(val, "eleanor_romance")                                        [StoryDirector.cs:219]
manager.AddClient(val) 或 manager.AddNextClient(val)   ← 革命军线用 AddNextClient 插队
```

**来客节奏不是"固定第 N 天"，而是"章节 → 日偏移"双轨**：`PendingChapterForDay(day)`（`StoryDirector.cs:273`）把 `Chapter`/`PendingChapter` 翻成今天该来第几章，其中掺了三条硬门（例：`num==12` 要 `Savings>=800` 或侍从线已垫付，否则推迟；`num==18` 母亲线没打通就跳到 19）。每日去重靠两个闸：`RomanceState.LastQueuedDay == today` 与 `MaidState.LastImportantDay == today`（`StoryDirector.cs:171-174`）。

原版排期被整体摘掉：`Patch_CheckStoryLineEleanor` 里 `Prefix` 返回 `false` 且不执行原版，只在 `RescuePending` 时才把原版的保释官/释放来访借回来（`Patch_CheckStoryLineEleanor.cs:8-16` + `VanillaEleanorBridge.cs:39-72`）。`Patch_HandleEleanorEarlyClient` / `HandleEleanorUncleClient` 同理直接 `return !ShouldSuppressOriginal`。

### 2.5 剧情系统：数据表 + 演出 + 存档

**不是硬编码状态机，是"表 + 三行原语"**。剧本表 `StoryV06Content.cs:285` 起：

```csharp
private static readonly Dictionary<string, StoryLine[]> Blocks = new ... {
    { "c09.n102", new StoryLine[10] {
        new StoryLine(StorySpeaker.Narrator, "er_v06_c09_n102_01"),
        new StoryLine(StorySpeaker.Lydia,    "er_v06_c09_n102_02"), ... } },
```

`StoryLine` 只有两个字段（`StoryLine.cs:3`）：`Speaker` + 本地化 `Key`。**分支不是节点图，是 C# 代码里的即时拼装**：

```csharp
// EleanorStory.cs:213-216 —— 结局反应块按 verdict 选一整段，再拼后续
Dialogue val5 = ReactionBlock(tracker.Dominant, "c01.b003", "c01.b004", "c01.b005", "c01.b005", "c01.b006");
StoryEngine.Append(val5, tracker.Any ? "c01.n004" : "c01.n005");
```

原语（`StoryEngine.cs`）：`Build(blockId)` 把 `Line[]` 串成原版 `Dialogue.nextDialogue` 链（`:222`）、`Append/Tail` 追加到链尾（`:296`）、`AddChoices` 给链尾挂原版选项（`:378`）、`InterceptDismiss` 拦截"客人要走"这一刻插入收尾对白（`:394`）。

**"读档后剧情走到哪了"= 三样东西，没有第四样**：① `er.core.done.<章>` 布尔；② `er.<line>.cursor.*` 中途游标（文本重写后按 revision 整批清空，`StoryDirector.cs:240-259`）；③ `Chapter`/`Route`/`Romance` 三个整数。原版 `Dialogue` 对象**从不入档**，所以「中段存档」只能从章节开头重播——README 1.40 的"读档时清理重写文本的未完成对话游标，中段存档从当前场景重新开始"就是这个约束的补丁。

### 2.6 她的"傲"怎么表达

三层，全部外置：

1. **台词**在 `Localization\<locale>.csv`（3,153 键 × 4 语言），代码只持键；`L.Get(key)` → `ModHelper.GetLocalized`（`L.cs:19`）。
2. **按 verdict 换台词集**：每章一次交易都有 `fair / discount / over / low / none` 五个 block id（`EleanorStory.cs:212` 的 `ReactionBlock`），"压这么狠，我捡了一上午"只在 `'L'` 分支出现。
3. **表情**用一张键→表情的表 `ExpressionContent.ByKey`（851 条，`ExpressionContent.cs:7`），值域含 **`tsun`（傲娇）**、`shy`、`smile`、`soft`、`serious`、`angry`、`determined`、`neutral`。挂法是 `StoryEngine.ContentLine` → `PortraitExpression.Apply(node, key, text)`（`StoryEngine.cs:346`），立绘解析走 `SpriteDict.GetSprite` 的合作式补丁（`Patch_SpriteDictGetSprite.cs:9,15`，Prefix 注册 + `HarmonyPriority(0)` Postfix 兜底），**不改别的模组的键**（1.38/1.09 的教训）。

注意 v2 文件换了形状：`MaidV2Content.Line` 直接内嵌 `Expr` 字段（`MaidV2Content.cs:7-21`），不走 `ExpressionContent`。**同一个模组里并存两种剧本模型**，属于历史债（§4.2）。

### 2.7 Patch 靶点表（58 处里能代表 9 类的）

| 原版方法 | 类型 | 目的 | 风险 |
| --- | --- | --- | --- |
| `StoreClientManager.CheckStoryLineEleanor` | Prefix→false | 整条摘掉原版艾莉诺排期，自家接管；救母时再借回归 | 高：原版剧情入口，跨模组（Sakurazaki 也改这条）必冲突 |
| `StoreClientManager.AddClient` / `AddNextClient` | Prefix→bool | 拦下"自家客人在读档后被原版重排"的情况 | 中：所有模组的加客都过这里 |
| `StoreClientManager.HandleNormalClient` | Prefix+Postfix | 抓到今天的"普通散客"，把自己的章节客**插进队列第 i 位**（`ChapterTradeQueuePatch.cs:62-102`） | 高：队列改写，需 `ThreadStatic` + `Finalizer` 清状态 |
| `StoreClient.OnItemBought` | Postfix | 只做展示效果记账（`DualGoodsPaymentPatch.cs`，按 `__instance` 过滤到自家客人） | 低 |
| `PlayerStore.SellItem` | Prefix+Postfix+**Finalizer** | 演出开始/结束成对还原（`DualSalePatch.cs:9-26`） | 中：卖货主路径，Finalizer 兜异常 |
| `GameItem.GetNegociatedValue` | Prefix(可短路)+`[HarmonyPriority(0)]` Postfix | 把手镯钉成剧情精确价，**让自家 Postfix 最后跑**，压过别人的价格倍率 | 高：全局价格函数，1.17 才补上"最后执行" |
| `EndOfDayUIManager.OnEndDayButtonClick` | Prefix→false（可能）+ Postfix(`__runOriginal`) | 睡前插入自家 CG；**只在原版真的跑了以后**才置 `sleepStarting` | 高：睡眠入口，1.42 的卡死事故就出在这 |
| `DialogUIManager.ContinueConversation/EndConversation/RemoveCurrentDialog/StartDialogue` | 四连 Prefix/Postfix | 无客人时也能推进"窗口对白"（独白接管 + 归还） | 高：对话状态机，1.41 一组互补丁 |
| `NetworkUpgrade.GetCost/IsReady/Unlock` + `WildNetworkUI.BuildTabs` | Prefix/Postfix | **借原版"网络升级"UI 当项目商店**（P1→P4 逐级购买，`MaidProjects.cs:51`） | 中：别人也想搭这班车时抢 id |
| `GameOverUIManager.DisplayUI` | Prefix（改 ref 参数） | 把自制结局页塞进原版 GameOver 面板 | 低但会盖掉别人的结局文本 |
| `StoreReputation.CheckReputation` | Postfix | 声望变动即触发路线奖励检查（`DualProgression.Check`） | 低 |
| `SaveManager.Save` / `PlayerStore.SaveGame` | Prefix→bool | 写盘前剥离临时状态，写完恢复（4 个 `*SaveBoundaryPatch`） | 中：写盘边界，漏还原就坏档 |

## 3. 关键代码摘录

**① 判定核心：读原版 feature，不碰价格**（`StoryEngine.cs:71-138`，删 IL 噪声）

```csharp
public void Record(GameItem item, int paid, bool eleanorIsSeller)
{
	Any = true;
	TotalPaid += Mathf.Max(0, paid);
	long num = ((item != null) ? item.GetBaseValue() : 0);
	if (num <= 0 && item != null) { num = item.unitValue; }
	if (num <= 0) { num = Mathf.Max(1, paid); }
	TotalBase += num;
	long num2 = MarketValue(item, num);          // GetNegociatedValue 优先
	float num3 = (float)paid / (float)num2;
	if (eleanorIsSeller)
	{
		bool flag  = HasActiveFeature(item, "bargainBuyingMarkup");
		bool num4  = HasActiveFeature(item, "bargainBuyingDiscount")
		          || HasActiveFeature(item, "blackmailDiscount")
		          || HasActiveFeature(item, "threatenDiscount");
		bool flag2 = HasActiveFeature(item, "confrontSucessDiscount");
		if (num4) { Lowball = true; }            // 玩家压她价
		else if (flag) { Discount = true; }       // 玩家让价
		else if (flag2) { Fair = true; }          // ← 「识货」走这支
		else if (paid > num2) { Discount = true; }
		else { Fair = true; }
	}
	else { /* 玩家是卖方：*Markup → Overcharge，bargainDiscount → Discount，比值 >1.25/<0.85 */ }
}
```

**② 好感经济只有 4 个 case**（`EleanorStory.cs:2036-2064`）

```csharp
internal static void RecordDeal(StoryEngine.DealTracker tracker, bool noSaleIsVisit)
{
	if (!tracker.Any) { if (noSaleIsVisit) { RomanceState.NoSaleVisits++; } return; }
	switch (tracker.Dominant)
	{
	case 'F': RomanceState.RecordDeal('F'); RomanceState.AddAffection( 1); break;
	case 'D': RomanceState.RecordDeal('D'); RomanceState.AddAffection( 2); break;
	case 'O': RomanceState.RecordDeal('O'); RomanceState.AddAffection(-1); break;
	case 'L': RomanceState.RecordDeal('L'); RomanceState.AddAffection(-2); break;
	}
}
```

**③ 一章的完整装配：造客 → 挂委托 → 拼反应块 → 记账**（`EleanorStory.cs:196-222`）

```csharp
private static StoreClient CreateFoodChapter()
{
	StoreClient val = NewEleanorClient("ch1", "eleanor1", (ClientIntent)1);
	val.clientBuyingTagList.Add("FOOD");
	val.SetBudget(300);
	val.SetVariety();
	StoryEngine.DealTracker tracker = new StoryEngine.DealTracker();
	val.onItemBought = (GameItem item, int paid) => { tracker.Record(item, paid, eleanorIsSeller: false); };
	Dialogue val2 = StoryEngine.Build("c01.n001");
	Dialogue val3 = StoryEngine.Build("c01.n002");
	Dialogue val4 = StoryEngine.BuildSelected("c01.n003", (int index) => index != 3);
	StoryEngine.Tail(val3).SetNextDialogue(val4);
	StoryEngine.AddChoices(val2, L.Get("er_op55_c01_prompt"),
		new StoryEngine.Choice(L.Get("er_op55_c01_opt1"), "", val3, null),
		new StoryEngine.Choice(L.Get("er_op55_c01_opt2"), "", val4, null));
	val.mainDialogue = val2;
	FinishWhenLeaving(val, 1, 2, () =>
	{
		Dialogue val5 = ReactionBlock(tracker.Dominant, "c01.b003", "c01.b004", "c01.b005", "c01.b005", "c01.b006");
		StoryEngine.Append(val5, tracker.Any ? "c01.n004" : "c01.n005");
		return Head(val5);
	}, () => { RecordDeal(tracker, noSaleIsVisit: true); });
	val.CompleteClientCreation(false);
	return val;
}
```

**④ 95% 的"剧情插入"靠原版一个字段**（`StoryEngine.cs:394-450` 骨架）

```csharp
public static void InterceptDismiss(StoreClient client, Func<Dialogue> build, Action onComplete)
{
	Func<Dialogue> getCompletion = () => {
		completion = build();
		completion.SetPriority(7);
		completion.SetStartAction(() => {
			client.tryInterceptDismiss = null;      // ← 原版 Func<Dialogue> 字段
			client.dismissable = false;
			if (!client.isBusinessComplete) { client.CompleteBusiness(); }
		});
		OnDialogueFinished(completion, () => { commit(); Dismiss(callNextWhenPossible: true); });
		return completion;
	};
	Dialogue allDoneDialogue = getCompletion();
	client.allDoneDialogue = allDoneDialogue;      // ← 交给原版"都办完了"那一刻
	// 并在 onItemSold / onItemBought / OnArrival 上挂 refreshAllDone，保证只构建一次
}
```

**⑤ 价格打架的正确姿势**（`Patch_PreludeBraceletPrice.cs:8-56`）

```csharp
[HarmonyPrefix] private static bool Prefix(GameItem __instance, ref long __result)
{
	if (__instance.identifier != "eleanor_romance.upper_bracelet") { return true; }
	int num = EleanorStory.PreludeFixedPrice(/* 当前客人 */, __instance);
	if (num <= 0) { return true; }
	__result = num; return false;
}
[HarmonyPriority(0)]   // ← 数值最大 = 最后执行，压过别的模组的价格倍率
[HarmonyPostfix] private static void Postfix(GameItem __instance, ref long __result) { /* 再写一次 */ }
```

**⑥ "只在原版真的跑了"才算数**（`ChapterBedtimePatch.cs:44-51`）

```csharp
[HarmonyPriority(800)]
private static void Postfix(bool __runOriginal)
{
	if (__runOriginal && LoveA2Closing.Enabled && RomanceState.StoryActive)
	{
		sleepStarting = true;      // 1.42 之前写在 Prefix 里 → 被别的模组拦一次就永久卡死
		LoveA2Closing.CancelTimer();
	}
}
```

## 4. 踩坑与红线

**4.1 好感算出来却不读——「关系值」是氛围，不是门槛。** `Affection` 有完整的 0–99 累加与 ±1/±2 权重，但全模组无一处读它做判定。真正放行剧情的是 `er.core.done.<章>`（`StoryCoreFacts.cs:45`）。对我们的含义：**别把"信任等级"做成一个整数然后到处比大小**——整数容易在改数值时全盘崩；用**可枚举的动作事实**（`fair_deals>=5`、`inspected_fake_once`、`order_settled`）做门槛，整数只用来显示。

**4.2 两套剧本模型并存。** `StoryV06Content`（`StoryLine(Speaker,Key)` + 表情另表）与 `MaidV2Content`（`Line(Speaker,Key,Expr)` 内嵌表情）同库共存，1.39 全量重写文本时得两套一起改；1.20 加日韩时还要保证四语言键集对齐（README 反复列"3,157 键"就是因为这个）。**一个模组只允许一种剧本形状**。

**4.3 在自己的 Prefix 里置全局标志 = 跨模组卡死。** 1.42 的头号事故：`sleepStarting` 在 Prefix 置位，别的模组（第 7 天背景提示 / 闭店后剧情 / 床铺确认）把 `OnEndDayButtonClick` 拦掉后，标志永不复原 → 之后再也睡不着。修法是 `Prefix` 只做拦截不再置位、`Postfix(bool __runOriginal)` 里才置位（`ChapterBedtimePatch.cs:16-52`），并加晨间兜底复位（README 1.42）。**同理适用于任何"进入中"锁**：锁的置位必须挂在"确认原版真的执行了"之后。

**4.4 精确价必须抢最后一棒。** 手镯要求"≥90% 基价"才算公道（`EleanorStory.cs:1103`），而别的模组的价格倍率会把 `GetNegociatedValue` 改掉，导致判定永远假。1.17 的修法是 `[HarmonyPriority(0)]` Postfix 再写一次。**凡是用价格做剧情判定的，都要假设价格会被别人改。**

**4.5 别用原版的 `eleanor*` identifier 装自己的客人。** 1.17：保释官/释放来访改用私有 `er_story_bail_officer` / `er_story_freed`，因为"其他 MOD 屏蔽或清理原版 `eleanor*` 客户时，不会误关我们的救援事件"。**但共享世界状态（`storeClientData.eleanorArrested/eleanorArrestDay`、声望、经济价格）仍然照写**——`VanillaEleanorBridge.RecordArrest`（`VanillaEleanorBridge.cs:114-130`）就是刻意写原版字段让原版 UI 认账。**私有 ID + 原生字段混用，这个分界很值钱。**

**4.6 无客人时的"窗口对白"要自己配平独白锁。** 1.41 一组互补丁：`StartDialogue`/`ContinueConversation`/`EndConversation`/`RemoveCurrentDialog` 四处 + `DialogUIManager.EndConversation` 的异常早退保护，否则第一句之后就推不动，连带睡眠/开门/关门全锁死。**原版对话机是"必须有 currentClient"的单例状态机，脱离客人自己放对白必须接管并归还独白标志。**

**4.7 静默 `catch` 是这里唯一的坏习惯（我们不学）。** `MetaProgress.cs:18-21` 读 `meta.json` 用了裸 `catch { return false; }`，失败连日志都没有（写的那半 `:31-34` 反倒记得 `LogError`，同一文件两种风格）；`StoryEngine.HasActiveFeature` 也 `catch { return false; }`。按本工作区规范，这类只在 Harmony/帧回调最外层捕获并 `LogError` 留堆栈。

**4.8 中段存档游标必须随文本版本作废。** `MigrateRewrittenDialogueCursors`（`StoryDirector.cs:240`）按 `er.text_rewrite_rev` 整批清 `er.love.cursor.*` / `er.dual.cursor.*`，否则改写后的对白会从一句不存在的台词继续。**改文案等于改存档兼容性，要带 revision。**

**4.9 4 个补丁抢同一个 `StoreEventManager.OnDayStart`。** `LoveWaterShortageDayPatch` / `MaidDividendDayPatch` / `MaidWorldDayPatch` 都挂在这里做每日结算；能不出事是因为各自只碰私有键。**我们的每日结算也该遵循"一个入口 + 私有键"，而不是多个补丁抢同一天。**

## 5. 对我们的可用点

**5.1 《深空电玩》：把"顾客当场识破"从一次性事件变成一条关系记录。**
我们已有 `ArcClientExpose`（当场识破 → `StoreReputation.ModReputation(faction,-4)` 一击定性）和 `ArcNpcTraits`（四档眼力 `Basic/LL/UL/TradeBoard` + 禁收清单）。Eleanor 的 `DealTracker` 正好补上缺的那一环：**识破之后这一单怎么算**。落地：在 `ArcNpcTraits` 的四档能力旁加一个 `ArcDealLedger`（照 `StoryEngine.cs:31-156` 抄），用 `StoreClient.onItemBought/onItemSold` **按客人实例**挂委托（不要打 `StoreClient.OnItemBought` 全局补丁，`DualGoodsPaymentPatch` 就是那种写法），把每单归成 F/D/O/L 四档写进 `ArcState`。**四档眼力天然就是"她识不识货"的现成维度**——客人识破假货那单判 Fair，客人被唬住买走假货那单可以是 Lowball。

**5.2 《深空当铺：可能是群友》：关系台账直接照抄结构，落到 `DspState`。**
`DspState` 的竖线分隔格式（`met_story` 标记 + `story_step=3` 键值，`__freya_state__`，`FreyaState.cs:1-30`）已经够放 `fair_deals=7|lowballs=1|order_settled=1`。建议照 Eleanor：
- **动作事实**做门槛（`inspected_fake`、`order_settled_3`、`knew_trap`），**不要**再加第 N 个好感整数；
- 好感换算只做「F+1 / D+2 / O−1 / L−2」四支（`EleanorStory.cs:2044-2062` 就 19 行），且**只用于显示与台词变体**；
- 台词按 verdict 分五块（fair/discount/over/low/none），不要为每种组合写一整段剧情（`ReactionBlock`，`EleanorStory.cs:2010-2020`）。

**5.3 原版 `StoreClient` 有约 20 个公共委托字段，等于免费的 hook 面。**
`StoreClient.cs:140-210`：`onItemSold`、`onItemBought`、`onDealAccepted`、`OnArrival`、`OnDismiss`、`tryInterceptDismiss`、`tryInterceptReport`、`onItemAddedToCounters`/`onItemRemovedFromCounters`、`onReportAction`、`onCaptureAction`、`customOnArrestDialogue`、`allDoneDialogue`、`arrestable`、`dismissable`。Eleanor 只用了其中 11 个就把 26,550 行剧情跑起来了（`onItemBought`/`onItemSold`/`onDealAccepted`/`OnArrival`/`OnDismiss`/`tryInterceptDismiss`/`onItemAddedToCounters`/`onReportAction`/`onCaptureAction`/`customOnArrestDialogue`/`allDoneDialogue`），**价格路径零补丁**。我们两款模组里凡是"给自家客人加反应"的地方，都该先问一句：能不能挂在这个实例上，而不是打全局 Harmony。

**5.4 "公道价"不用自己定义——原版给了三个基准。**
`GameItem.GetNegociatedValue()`（市价，含倍率与事件）、`BargainUIManager.GetPreBargainValue(item, value)`（把砍价 feature 逐个除掉，还原"砍价前价"，`BargainUIManager.cs:1418`）、`BargainUIManager.BARGAIN_FEATURE_IDS`（8 个价格类 feature id 的白名单，`BargainUIManager.cs:131`）。深空电玩卖货/收货定价、当铺的"公道价"文案判定都可以直接用这套，不必自建市场价模型。

**5.5 剧本三件套可整包搬给 `FreyaStory`。**
`Dictionary<blockId, Line[]>` + `Localization/<locale>.csv` + 键→表情表。`FreyaStory` 现在还是单文件 23 行起的轻量实现，若要做多章节，建议：剧本表只放 `(Speech, Key)`，正文进 CSV（我们已有 `FreyaLocalization`），表情单独一张键表（`ExpressionContent.ByKey` 的形式），**并且把键集行数做成离线断言**（Eleanor 用"3,157 键 ×4 语言"当发布门禁，正好对上我们 `texts` 回归的口味）。

**5.6 想加"长期投资项目"就搭 `NetworkUpgrade` 的车。**
`MaidProjects`（`MaidProjects.cs:51` 的 `Plan[]`＝4 个项目、成本/折扣/上下层声望门槛）通过 `GetCost`/`IsReady`/`Unlock` 三个 Prefix/Postfix + `WildNetworkUI.BuildTabs/OnUseClicked` 就把原版"网络升级"面板变成了自己的项目商店，还带逐级解锁（P1→P2→P3→P4）与"未付钱不许点"的守卫（`MaidProjectUnlockGuardPatch.cs:8-29`）。深空当铺若要"扩建店面/买设备"，这条路比自己画 UI 便宜一个数量级。

---

## 一句话骨架

> **把"这一单是怎么谈成的"从原版物品的 `ItemFeature` 里读出来（而不是自己算），归成 F/D/O/L 四档，用四个计数器记流水账，每次结账按档位换一段反应台词；好感只是流水账的一个派生显示值，真正放行剧情的是 `done.<章>` 这种布尔事实。**

（补一句同样重要：**"记账"和"担风险"都没进好感体系**——它们各自走独立的账本（`MaidDividends` 的 pending/earned/paid）与独立的剧情标记（`TrapSprung`/`KnewTrapUnwarned`）。一个动作只喂一条管道，这是它 1.42 个版本还没把状态搅成一团的原因。）
