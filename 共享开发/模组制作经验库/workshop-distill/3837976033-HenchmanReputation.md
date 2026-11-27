# 工坊 3837976033 架构蒸馏档案 —— 声望拓展：帮派与投资 / Reputation Expansion（内部名 `henchman`）

> 工坊 id `3837976033`｜manifest ID **`henchman`**（写 harmony id / 兼容判断用这个，不是标题；`ModEntry` 里日志前缀却是「关系网拓展」）｜程序集 `Henchman.dll`（833 KB）｜`ModVersion 0.18.31`｜`GameVersions 0.46D`｜作者 BUGFx｜`<Capabilities />`（零能力）｜7.7 千订阅｜有 `Localization\` + `Textures\` 两个资源目录
> 反编译源：`缓存文件夹\decomp_ws\3837976033`（271 个 `.cs` / 34,817 行，ILSpy 产物、零有效注释；仅 1 个子目录 `Properties`）
> **为什么值得读**：这是全库**第三条**「声望扩展」路线，而且和前两条正交——`3828971731` 与 `3828949943` 改的是「声望本身的效果」，它改的是「**声望能买到什么**」：25 条声望星只是门牌，后面挂一座实体经济（人 → 派活 → 到货 → 变现 → 放贷/投资 → 抬容量）。它顺手给了我们三件现成手艺：① **到货卡顿的一手性能案例**（O(件×全店) → O(全店)，同一个版本里两种写法并存，反证齐全）；② **双路线共享容量的配额写法**（容量 = min(熟悉度槽, 声望档槽)，扣在全库 6 个不同函数里）；③ **把自家订单寄生进原版市场电脑**（`StoreStation.HandleStoreRequests` + `StoreRequest`），一份 UI 都不用自己画。

---

## 0. 一句话结论

**它把「声望」当门票、把「帮派」当员工编制，真正的状态全在模组私有键里。** 五个势力各给 4~6 条星（共 25 条：23 颗正阈值 + 2 颗负阈值惩罚星），星星只负责 `isUnlocked` 显示；能不能干活由 `HPhase.LL()/UL()`（原版声望值）+ `HCoopPartner.familiarity/completedNodes/finalRoute`（模组私有）两维共同判定，两者**取小**才是容量。核心循环：**入伙 → 招人（瓦砾拾荒 / 沈先生代购代售）→ 派活 → 到货进私仓 → 变现 → 放贷投资 → 抬容量**。

---

## 1. 规模与结构

| 分区 | 文件 | 行数 | 说明 |
| --- | ---: | ---: | --- |
| `Henchman\`（平铺，无子目录） | 270 | 34,812 | 全部业务 |
| `Properties\AssemblyInfo.cs` | 1 | 5 | **只有 `AssemblyVersion("0.0.0.0")`**——作者没维护程序集版本，判版本只能读 manifest（`0.18.31`） |
| **合计** | **271** | **34,817** | |

**按前缀分模块（作者用文件名代替命名空间，`H` 前缀统一）**：

| 前缀 | 文件 | 职责 |
| --- | ---: | --- |
| `HCoop*` / `HPartner*` | 39 | 人（瓦砾/沈先生）：入伙、熟悉度、成长节点、服务台、渠道、库存、报价 |
| `HBusiness*` | 54 | 经营面板：供货、灰市、放贷、投资、传闻、合同、账本、UI |
| `HCommission*` | 16 | 沈先生的「委托」（带手条 `HandnoteUIManager` 的任务线） |
| `HRisk*` / `HSecRev*` / `HNetwork*` / `HBlackMarket*` | 58 | 灰线、治安部/革命军剧情、关系网技能与顾客改造 |
| `HShen*` / `HNeighborhood*` | 19 | 沈先生剧情 + 街坊募集（本档 §3 主角） |
| `HStore` / `HSupplyStore` / `HBox*` / `HConsignmentStore` | 8 | 两个私仓（J 键箱子 / 代购仓库）+ 物品快照编码 |
| 其余（`HKey`/`HRepUI`/`HConfUI`/`HDebug`/`L`…） | 76 | 窗口、声望面板注入、调试、本地化 |

- **补丁规模**：109 处 `[HarmonyPatch]` 特性行（报告口径 107 个独立补丁类）；其中 **3 处 `TargetMethods()` 家族式声明**、**3 处 Transpiler**（`HNetworkInvestigation.cs:18` 直写；`HSecurityProcurementBadge.cs:17` 与 `HSecurityProcurementPlacement` 都转调 `HSecurityProcurement.ReplaceRefusalFlag`）。同一靶点被挂多次是常态：`GameItem.GetNegociatedValue` ×5、`StoreReputation.UpdateReputation` ×4、`PlayerStore.TryAddDirectSellingItemToTable` ×4、`SecData.OnNewDay` ×3、`StoreClient.CompleteSecClient` ×3、`PlayerStore.SellItem` ×2、`HandnoteUIManager.OpenPanel` ×2、`PlayerStore.LoadGame` ×2。
- **一等接口用量**：`ModHook` 只订阅 6 个（`ModEntry.cs:65-73`：`OnGameLoadedInit/Late`、`OnNewGameStarted`、`OnWakingUpEarly`、`OnShutterOpenedLate`、`OnHandlingNightlyServicesLate`）；`ModHelper` 只用 15 个（含 `StartCoroutine/StopCoroutine`，只给 `HKey` 的 1 秒轮询用，`HKey.cs:26,194`）。**没有 `HotReloadPrepare/Activate`**（报告第 4 行），热重载靠 `OnDisable` 里 10 行手动 `Reset` 兜（`ModEntry.cs:92-98`）。
- **持久化：一子系统一个键，列表自己编成字符串**（这是本档最省事的一层设计）。`HBusinessData` 只把标量留在字段里，**所有 List 都是 `[NonSerialized]` + 对应 `xxxRows` 字符串**（`HBusinessData.cs:63-102`），进出由自建 `HBoxCodec/HBusinessCodec/HCoopCodec` 负责。实测键表（`grep '"henchman", "'`）：

| 键 | 内容 |
| --- | --- |
| `phase` | 入伙阶段（0/1/4），`HPhase.HasUpper()` 读它 |
| `partner_cooperation_v1` | `HCoopData`：瓦砾/沈先生、订单、计划、库存、报价、来源 |
| `partner_business_v2` | `HBusinessData`：募集单、供货、灰市、放贷、投资、账本 |
| `box_store_v5` / `business_supply_store_v1` / `consignment_box_v1` | 三个私仓的物品快照（`HStore.Envelope`） |
| `network_skills_v1` / `risk_routes_v1` / `sec_rev_*` / `commission*` | 其余子系统 |
| `perk_off` | 逗号分隔的「关掉的星」，`HPerkSwitch` |

- **`HBusinessState.Save()` 每次写盘前跑一遍编解码自检**：`Encode → Decode → Encode` 不相等就抛（`HBusinessState.cs:60-71`）。不是为了好看——它的编解码是手写行式，写坏了整档灰。
- **五个势力 × 星**（`HRepUI.cs:168-215`，`Make(id, threshold)`）：`FACTION_LOWER_LEVEL` 100/140/160/180/200（5）；`FACTION_UPPER_LEVEL` 100/120/140/160/180/200（6）；`FACTION_REVOLUTION` 100/120/140/160（4）；`FACTION_SECURITY` **−30** + 120/140/160/200（5）；`FACTION_BLACK_MARKET` **−20** + 80/100/120/140（5）。**共 23 颗正阈值 + 2 颗负阈值 = 25 条**，对比 `3828971731` 的 44 条、`3828949943` 的 20 条——它把预算全花在系统上，星星几乎是免费的。

---

## 2. 它怎么做这件事

### 2.1 核心循环（七步，每步落在哪个类）

```text
① 入伙门槛   HPhase.IsJoined/HasUpper/IsFullPartner（HPhase.cs:49-77）读 phase + LL/UL/SEC 声望
② 招人       瓦砾 / 沈先生 = HCoopPartner 数据条目（HCoopPartner.cs），不是 NPC、不是物品
             familiarity 0-100，completedNodes 0-5（每 20 一点），finalRoute 1=互助 / 2=黑帮
③ 派活       夜结算 HScavenge.OnHandlingNightlyServicesLate（ModEntry.cs:70）
             → HScavenge.Settle → HCoopRuntime.Night()（分相 nightStage 0..4，HCoopRuntime.cs:79-130）
④ 到货       拾荒/代购/募集 → 私仓 HSupplyStore（10×14）或 J 键箱 HStore（10×14）
⑤ 变现       柜台卖 / 合同客户上门（StoreClient）/ 市场电脑周单（StoreRequest）/ 灰市收购
⑥ 投资       放贷 200/500/1000/2000（本金上限 6000）+ 投资（上限 10000），
             HBusinessFinance + HBusinessRules.LoanRepayment/InvestmentReturn（HBusinessRules.cs:86-104）
⑦ 回头       HCoopState.Work(shen, instruction) → HCoopRules.AwardWork +3/+1 熟悉度（HCoopRules.cs:152-165）
             → 熟悉度抬槽位 → 抬容量 → 开下一页（HBusinessCaps / HCoopRules）
```

**关键：⑦ 是闭环的钩子。** 每次成功交易、每次放款、每次交货都会回调 `HCoopState.Work(...)`（例：`HBusinessContracts.cs:181`、`HBusinessFinance.cs:100`、`HNeighborhood.cs:301`），所以「干活 → 涨熟悉度 → 能接更多活」不依赖任何定时器，只依赖交易成功这一件事。

### 2.2 「招募订单」全链路：生成 → 接单 → 到货 → 失败

这里要分清**两套**订单，作者在同一次更新里只修了其中一套（见 §3）：

| 系统 | 类 | 玩家看到 | 到货方式 |
| --- | --- | --- | --- |
| **募集单**（简介里「招募订单」） | `HNeighborhood` + `HNeighborhoodOrder` | 街坊募集：选品类 + 数量，报价后扣款 | **指定天数后**在日初 `Tick()` 一次性入仓 |
| 常年募集指令 | `HCoopOrders` + `HCoopOrder` | 给瓦砾下 9 类物资目标，逐件匹配入库 | 靠夜结算逐晚补齐（无固定到货日） |

**募集单的四段（`HNeighborhood.cs`）**：

1. **报价 `Prepare(category, quantity)`（:83-176）**——先按数量**真造一遍物品**（`HBusinessCatalog.Create`），逐件过 `HCoopStock.Legal` 与 `HBusinessPricing.Quote` 算钱，把每件的 `HStore.SnapshotRecord` 塞进 `Envelope`，最后 `val.Destroy()`（:116-141）。也就是说：**报价那一刻，货其实已经"预生成"并存成快照了**，`rows` 字段带着这串快照（:161）。同一天同参数的重复报价直接复用（:104-108）。
2. **接单 `Accept(id)`（:178-211）**——重校验：报价必须是今天的、档位/材料优惠没变（:190）、现金够（:194）；然后在 `HBusinessTransaction.Run` 里扣钱 + 置 `stage=1` + 写 `dueDay=Day+days`（:198-208）。事务失败自动回滚现金（`HBusinessTransaction.cs:24-36`）。
3. **到货 `Tick()`（:213-320）**——`stage==1 && dueDay <= Day` 就解码 `rows`，逐件 `HStore.RestoreRecord` 后塞 `HSupplyStore.Add`；每成功一件 `num++`，结束后把已交付的行从 `envelope.records` 里删掉并回写（:270-286）。**逐件落账是为了中断安全**：写到一半崩了，下次只补剩下的。
4. **失败/超时**——没有"超时作废"，只有三层保护：① `Pending`（`stage==1` 的单）存在时**不许下第二单**（:91-95）；② 交付靠 `uniqueId` 索引去重，重复执行不会复制物品（:229-262）；③ 存档失败只记 `Fault` 并保留剩余行，下次日初/夜里再试（:274-318）。**真正的"失败"只有报价过期**（:190）与现金不足，两者都不扣款。

### 2.3 双路线共享容量：容量 = min(熟悉度槽, 声望档槽)

配额不在一个地方，而是**七个纯函数 + 六处扣减点**。这是本档最像"工程"的一节。

| 函数 | 公式 | 出处 |
| --- | --- | --- |
| `FamiliarSlots(f)` | f≥100→3；≥60→2；≥20→1；否则 0 | `HCoopRules.cs:13-25` |
| `RubbleSlots(f, rep)` | `min(FamiliarSlots, rep≥180?3 : rep≥140?2 : rep≥100?1 : 0)`（rep = **下层区**声望） | `:27-40` |
| `ShenSlots(f, rep)` | `min(FamiliarSlots, rep≥180?3 : rep≥120?2 : rep≥80?1 : 0)`（**上层区**声望） | `:42-55` |
| `PurchaseLimit(f, rep, drive)` | `min(24, SupplyLimit × 2)`（drive 是瓦砾成熟后的「跑一趟」） | `:57-61` |
| `SupplyLimit` | rep≥180→24；≥160→18；≥140→12；否则 8 | `HBusinessRules.cs:25-41` |
| `SourceLimit(f, rep, drive)` | `(f≥100 && rep≥200)?2 : (f≥80 && rep≥160)?1 : 0`，再 +1（drive）；为 0 时恒 0 | `HCoopRules.cs:63-67` |
| `ReserveLimit(f, mutual)` | `(f≥100?3: f≥60?2: f≥40?1:0) + (mutual?1:0)` | `:83-86` |

**扣减点全部是"数一遍已占用的、再比上限"的写法，没有全局计数器**：

- `HCoopOrders.EnabledOrder(o)`：按 `Ordered()` 顺序数到第几个，`num2 < num` 才算启用（`HCoopOrders.cs:42-56`）——**这是"排序即优先级"的实现**，`Prioritize()` 就是改 `priority` 重排队（:107-120）。
- `HCoopPlans.Add(...)`：`plans.FindAll(active).Count >= ShenSlots` 直接拒（`HCoopPlans.cs:22`）。
- `HCoopPlans.For(row, item)`：取前 `num` 个活跃 plan 才匹配（`:78-92`）。
- `HCoopSources.cs:88`（`SourceLimit`）、`HCoopOrders.cs:201`（保留单计数）、`:258`（代购数量）。
- 募集单的容量是另一套「档位表」，不参与上面的槽位：`HNeighborhood.Limit` = 12/20/30（100 档 30）、`Days` = 3/1/1（`HNeighborhood.cs:19-36`）。

**「双路线共享」在哪**：老的长约（`HBusinessContract`，客户上门收周货）**只给互助路线**——`HBusinessRules.ContractSlots(f, nodes, route)` 在 `route != 1` 时恒返回 0（`HBusinessRules.cs:62-65`）；投资只给黑帮路线——`HBusinessCaps.Investments` 要求 `Route(shen)==2`（`HBusinessCaps.cs:26`）；**而 1127 之后新加的「市场周单」两条路线都能用，且每周只有一张**（`HShenMarketplace.cs:37` 文案「两条路线都能使用」；`:133` `marketplacePostedWeek >= 本周` 直接早退）。所以「共享容量」= **去掉按路线分档，改成全局每周 1 张 + 一个"旧约未清则本周顺延"的锁**（`marketplaceBlockedWeek`，`HBusinessContracts.cs:204`）。旧约一旦结束就写这个锁，本周不再发新单（`HShenMarketplace.cs:68-71`）。

### 2.4 「市场电脑」：用原版 `StoreStation` + `StoreRequest`，UI 一行没写

**不是** `market_computer` 那种自建机器，也**不是**自建面板。做法是把一条 `StoreRequest` 塞进原版「店内电脑的市场委托」里：

- 发单：`HShenMarketplace.Publish()` 先判 `PCUIManager.Instance?.storePC != null`（`HShenMarketplace.cs:126`，电脑物件不在就不发），然后 `new StoreRequest{ id="henchman_market_week_"+week, title, description, requesterName, factionId="FACTION_UPPER_LEVEL", requiredItem, requiredItemCount, duration, creditReward }`，再过 `StoreRequest.CanActivateRequest` → `StoreRequest.ActivateRequest(val2, false)`（:163-189）。奖励价 = `HBusinessRules.Price(单价×数量, 160)`（即原价 1.6 倍）。
- 触发：`[HarmonyPatch(typeof(StoreStation), "HandleStoreRequests")] Postfix → TryPublish()`（`HShenMarketplacePosting.cs` 全 12 行）。它同时在建 `HBusinessRuntime.WakeCore()` 里再点一次（`:52`），两个入口互为兜底。
- 交付：`StoreRequest.FillRequest` 与 `CanThisItemFillRequest` 各挂一个补丁（`HShenMarketplaceDelivery.cs` / `HShenMarketplaceGoods.cs`），把自己那一单的资格判定接管；`Ready(request)` 用 `EmporiumEntry.Instance.GetAllOwnedItems(false, true)` + `HashSet` 数够件数（`HShenMarketplace.cs:211-227`）。
- 前置门：`NetworkUpgrade.IsMarketplaceRequestsUnlocked()`（原版「王尔德助手」里 250 信用点的市场升级，`HShenMarketplace.cs:13,37`）——**借原版解锁链当自己的前置任务**。

### 2.5 区域互助折扣：价格写在「报价单」里，根本没碰 `GetNegociatedValue`

这是和前两条声望模组最大的手法差异。**它不 Patch 估价函数，而是自己算一张报价单**：

1. `NeighborhoodPricePercent(stage, weeklySupport)`：`(stage==100 && 本周送够) → 50`；否则 `stage>=80 → 90`；否则 100（`HBusinessRules.cs:48-60`）。
2. `DiscountPrice` = 上面的结果，`Prepare` 时写进 `order.pricePercent`，并用 `HBusinessRules.Price(baseCost, pricePercent)` 定死 `cost`（`HNeighborhood.cs:101,147-160`）——**玩家看到的价和之后扣的价是同一个 int**。
3. 「本周送够」= `HBusinessState.D.supportFood >= 10 && supportHousehold >= 10`，每 7 天由 `Week()` 清零（`HNeighborhood.cs:40-47,63-75`）。
4. 送物资 `Give(...)` 真的从库存里 `Expel`/`ReleaseForCoop` 扣物品（`:412-423`），异常时按 `(item, originGrid, shape, wasInSupplyStore)` 四元组逐个还原（`:441-478`），成功后才 `Destroy()`（`:483-486`）。
5. **改价即时生效靠"让旧报价作废"**：`Accept` 里比对 `order.pricePercent != DiscountPrice` 就要求重报（`:190`）——不是去改已经生成的单，而是拒绝过期单。

也就是说：**这套折扣对原版价格函数是完全不可见的**，`GameItem.GetNegociatedValue` 的 5 个补丁里没有一个是给街坊折扣用的（它们分别服务灰市收购、合同客户、渠道抛售、关系网技能、种子危机剧情，§2.8）。

### 2.6 帮派 / Henchman 的数据结构：数据条目 + 假客户 + 手机联系人

**一个"帮派成员"不是 `GameItem`，也不是场景里的 NPC**，而是三样东西拼起来：

| 层 | 实体 | 存哪 |
| --- | --- | --- |
| 编内数据 | `HCoopPartner{id, familiarity, workDays, completedNodes, finalRoute, ...}`，只有两个实例：`rubble`、`shen`（`HCoopData.cs:107-117`） | `partner_cooperation_v1` |
| 上门实体 | 临时 `StoreClient`：`HNetworkClientHelper.New(id, name, sprite, faction)` 里写死 `clientIntent=5`、`nonShootable=true`、`arrestable=false`、`sourceModId="henchman"`（`HNetworkClientHelper.cs:22-35`） | 不存档，每次现造 |
| 联系方式 | 手机号 `rubblePhone = 7341601`（`HBusinessData.cs:33`）+ `StorePhoneClient` 名字补丁 | `partner_business_v2` |

- **派遣靠"造一个客户"**：合同客户 `HBusinessContracts.Build()` 现造 `StoreClient`，`clientBuyingIdList.Add(row.itemId)`、`InitUpperLevelCitizen(c)`、`SetBudget`、`OverrideBudget`，再用 `Dialogue` 串「交货 / 失约」两条分支（`:50-147`）。排队走 `HVisitQueue.Add(manager, factory, id)`（`:45`），不是直接怼进 `clientStack`。
- **熟悉度只有两个来源**：`AwardWork` 每天 +3（当天干了活）+1（当天给了新指令）（`HCoopRules.cs:152-165`），以及成长节点的对话选择。`completedNodes` 每 20 熟悉度一点（`GrowthDue`，`:142-145`），第 3 点之后定 `finalRoute`（`:147-150`）。
- **门 = 两维同时满足**：`Tier(shen)` 先要求 `completedNodes>=3 && finalRoute∈{1,2}`，再取 `StandingTier(声望)`（0~3）（`HPartnerAbilities.cs:59-67`）；`Personal(shen, n)` 要求 `familiarity>=n && completedNodes >= n/20`（`:49-57`）。**这是"钱买不到"的那一半**：声望只能到 200，熟悉度只能靠日拱。

### 2.7 UI：全走原版 `CustomUIManager.Window`，声望面板往里插星

- 所有窗口都是 `CustomUIManager.Window("henchman.business."+page, 标题, "overlay")` + `CustomUIBuilder`（`HBusinessUI.cs:355`、`HCoopUI.cs:394`、`HConfUI.cs:41`、`HPartnerServiceUI.cs:219` 用原版 `networkUIPrefab`）。**全库零 `OnGUI`、零自建 Canvas**（和 `3828971731` 自绘 uGUI 的路子相反）。
- 页签枚举 `HBusinessPage` 22 页（`HBusinessPage.cs`）；`HCoopUI` 通过 `[HarmonyPatch(typeof(ReputationUIManager), "OpenUI")] Postfix → HCoopUI.Attach(__instance)` 挂进原版声望窗口（`HCoopReputationPage.cs`）。
- **插星的具体手法**（最值得抄的一招，`HRepUI.cs:12-91`）：Postfix `ReputationElement.Init`，先删掉自己上次插的 `HENCH_STAR_*` / `HENCH_SWITCH_*` 子物件（防重复插），再 `Instantiate(instance.reputationPerkPrefab)` 逐颗放，`anchoredPosition = new Vector2(19f, __instance.GetBarHeight(perkThreshold))`——**用原版的 `GetBarHeight(阈值)` 算高度**，所以星星天然落在正确的刻度上；每颗星自带一个开关（`MakeSwitch`），点一下写 `perk_off`，对应效果立刻失效。
- 模态挡穿透：`[HarmonyPatch(typeof(StoreUIManager), "CheckRaycast")] Postfix` 里，若指针在自家窗口上就写 `__instance.hoveringStoreUI = true`（`HModalRaycastPatch.cs:14-36`）——**改原版公有字段**，比拦输入事件便宜得多。

### 2.8 Patch 靶点表（按域归并，热点已标）

| 原版方法 | 形 | 目的 | 风险 / 是否热点 |
| --- | --- | --- | --- |
| `GameItem.GetNegociatedValue` ×5 | Postfix（`HBusinessNativePrice`/`HCoopExactNativeSalePrice` 带 `[HarmonyPriority(0)]`） | 自家交易价、合同价、渠道价、折扣、种子危机溢价 | **热点**：每件每次议价都过。5 个 Postfix 之间**没有跨模组顺序声明**，只靠各自 `return`；`HBusinessPricing.Suppress` 是唯一的重入闸 |
| `GameItem.IsTag` | Patch（`HSecRevShortageTag`） | 缺货期给物品打「短缺」判据 | **极热点**（几乎每次判定都调）；只应读不写 |
| `GameItem.MayStack` / `GetSplitItem` / `GetCurrentValue` / `AddItemFeature` / `OnAddedToWeightedArea` | Postfix | 箱子堆叠规则、拆分、涨价、灰市标记、合同不付小费 | 中；`GetCurrentValue` 也被估价链调用 |
| `StoreStation.HandleStoreRequests` | Postfix | 发市场周单 | 只在电脑前触发，低风险**但每周只该写一次状态**（`marketplacePostedWeek` 是幂等键） |
| `StoreRequest.FillRequest` / `CanThisItemFillRequest` | Postfix/Prefix | 市场单的交付与资格 | 低；**只认 `id.StartsWith("henchman_market_week_")`**，别碰别人的单 |
| `PlayerStore.SellItem` ×2 / `SellAllItemFromTable` / `CanSellThisItem` / `TryAddDirectSellingItemToTable` ×4 | Prefix/Postfix | 手工卖货入账、批量卖、安全货判定、摆台观察 | 中；`TryAddDirectSellingItemToTable` 有重载，本库 4 处都**显式写了参数类型表** |
| `PlayerStore.LoadGame` ×2 / `StartNewGame` / `SaveGame` | Prefix/Postfix | 读档重置静态态、写盘前把货先落盘（`HBoxBeforeSave`→`HOwnedDelivery.BeforeSave`） | 中；`LoadGame` 挂两个类，靠各自职责分（`HLegacyPerkCleanup` / `HNativeLoadGuardLifecycle`） |
| `SecData.OnNewDay` ×3 / `GetCrimeCount` / `GetBiggestCrime` / `GetTotalCrimeValue` / `GetSuspectedCrimeDisplay` / `ResetCrime` / `OnFixerUsed` / `ApplyCrimeModifier` | Postfix/Prefix + 1 Transpiler | 涉案金额增减、衰减、显示、销案 | 中高；**同一晚三个补丁都改涉案值**，顺序敏感 |
| `StoreReputation.UpdateReputation` ×4 / `ModReputation(double)` / `OnItemTraded` | Postfix/Prefix | 声望到账时刷新星、上层交易加成 1.1× | **中**：`ModReputation` 用 `HShenTradeStandingScope.depth` 只对"自己发起的那一笔"加倍（`HShenTradeStandingBonus.cs:9-19`），否则会放大所有来源 |
| `ReputationElement.Init` / `ReputationUIManager.OpenUI` / `StoreReputationPerk.GetLocalized*` ×3 | Postfix / Prefix→false | 插星、插页、本地化 | 显示层；`GetLocalized*` 是 `Prefix→false` 直替，**别的模组读同一对象会拿到自家文案** |
| `StoreClientCounter.Update` | Prefix | 顾客队列准备 | **频率取决于该组件**（推测：计时器驱动），每次调用都做一次队列维护 |
| `StoreUIManager.CheckRaycast` | Postfix（写 `hoveringStoreUI`） | 模组窗口挡物品交互 | 高频；改原版公有字段，**别在同一字段上跟别的模组打架** |
| `GraphUtils.DestroyIfUnplaced` | Prefix | 拒绝入库的物品不销毁 | 低；这是"物品丢失"的最后一道网 |
| `ContrabandHelper.InspectItem` / `StartInspection` / `DisposeContraband` | Prefix/Postfix | 治安检查宽严、扣押 | 中高；**与治安剧情同一晚结算时交叉** |

---

## 3. 关键代码摘录

### 3.1 到货卡顿：**先建一次索引，再逐件比对**（已修的写法）

`HNeighborhood.cs:213-269`（`Tick()` 前半，删了 `//IL_xxxx`）：

```csharp
public static void Tick()
{
    if (!HBusinessState.Loaded || !HSupplyStore.IsLoaded()) return;
    foreach (HNeighborhoodOrder gatherOrder in HBusinessState.D.gatherOrders)
    {
        if (gatherOrder.stage != 1 || gatherOrder.dueDay > HBusinessState.D.Day) continue;
        HStore.Envelope envelope = HBoxCodec.Decode(gatherOrder.rows);
        int count = envelope.records.Count;
        int num = 0;
        bool flag = false;
        HashSet<int> hashSet = null;
        try
        {
            hashSet = new HashSet<int>();
            foreach (GameItem item in HSupplyStore.AllItems())        // 一次
                hashSet.Add(item.uniqueId);
            if (PlayerStore.Instance != null)
            {
                foreach (GameItem item2 in PlayerStore.Instance.FindAllItem(true))  // 一次
                    hashSet.Add(item2.uniqueId);
            }
        }
        catch (Exception e) { hashSet = null; HBusinessState.Fault("neighborhood delivery index", e); }

        for (int i = 0; i < envelope.records.Count; i++)              // ← 循环里只查哈希
        {
            GameItem val = HStore.RestoreRecord(envelope.records[i]);
            if (val == null) break;
            if (hashSet != null && hashSet.Contains(val.uniqueId)) { val.Destroy(); num++; continue; }
            if (!HSupplyStore.Add(val)) { val.Destroy(); break; }
            num++;
        }
        if (num > 0) { envelope.records.RemoveRange(0, num); gatherOrder.rows = HBoxCodec.Encode(envelope); ... }
    }
}
```

### 3.2 到货卡顿：**同一个版本里没修的那一半**（反证，`HBusinessMutual.cs:20-47`）

```csharp
public static void Deliver()
{
    if (!HBusinessState.Loaded || HBusinessState.D.deliveryRows == "" || !HSupplyStore.IsLoaded()) return;
    HStore.Envelope envelope = new HStore.Envelope();
    foreach (HStore.Record record in HBoxCodec.Decode(HBusinessState.D.deliveryRows).records)
    {
        GameItem item = HStore.RestoreRecord(record);
        if (item == null) envelope.records.Add(record);
        else if (HSupplyStore.AllItems().Exists(i => i.uniqueId == item.uniqueId)        // ← 每件都新建一个 List
              || PlayerStore.Instance.FindAllItem(true).Exists(i => i.uniqueId == item.uniqueId)) // ← 每件都扫全店
        {
            item.Destroy();
        }
        else if (!HSupplyStore.Add(item)) { item.Destroy(); envelope.records.Add(record); }
    }
    HBusinessState.D.deliveryRows = ((envelope.records.Count == 0) ? "" : HBoxCodec.Encode(envelope));
    HSupplyStore.Save();
    HBusinessState.Save();
}
```

**根因与修法（这一节是全档最值钱的性能样本）**

- **根因**：`PlayerStore.Instance.FindAllItem(true)` 是一次**全店遍历**（所有容器 × 所有物品，含子物品递归）；`HSupplyStore.AllItems()` 每次调用都 `new List`（`HSupplyStore.cs:76-87`）。把它放进"逐件到货"的循环里，复杂度就是 **O(件数 × 全店物品数)**。募集单满级一单 30 件、日初 `OnWakingUpEarly` 这一帧里还要跑完 `HBusinessRuntime.WakeCore()` 的 13 件事（`HBusinessRuntime.cs:36-52`），于是玩家看到的就是**到货那天早上那一顿**。
- **修法**：把"查重"从「O(全店)/件」改成「O(全店) 一次 + O(1)/件」——循环外建 `HashSet<int>` 收 `uniqueId`，循环内只 `Contains`。同时把**落账单位**从"整单"改成"已交付的前 `num` 行"，于是每次进 `Tick()` 都能接着上次的位置继续，存档失败也只回退一次。
- **作者没做但我们也别做**：他没有用协程分帧（`ModHelper.StartCoroutine` 在全库只给 J 键轮询用，`HKey.cs:26`）。日初这一帧里 13 个服务的总量仍是一次性的，所以**这个修法只是把常数从"件×全店"降到"全店"，瓶颈从此变成"全店遍历本身"**。真要再进一步，得把 `FindAllItem` 的结果做成读档时建立、交易时增量维护的索引。
- **同版本反证**：`HBusinessMutual.Deliver()` 是**同一晚的兄弟路径**，形状一模一样，作者只改了 `HNeighborhood`。这告诉我们：**性能修复要做同形搜索，不能只改报障那一处**。

### 3.3 接单事务：现金 + 两份状态 + 账本，一起成一起败

`HNeighborhood.cs:198-208`：

```csharp
if (!HBusinessTransaction.Run("neighborhood order", () =>
{
    PlayerStore instance = PlayerStore.Instance;
    instance.playerCash -= order.cost;
    order.dueDay = HBusinessState.Day + order.days;
    order.stage = 1;
    HBusinessState.Entry("mutual_order", order.id, /*文案*/, order.cost, 0, 0, order.quantity);
}))
    return Fail(T("save.failed", "订单未能保存，付款已撤回，请稍后重试。", "..."));
```

`HBusinessTransaction.cs:7-41` 的实现要点：进门前先 `Encode` 两份状态做快照 + 记 `playerCash`；`apply()` 后连存两份；任一步抛异常就**恢复现金 + 反序列化回快照 + 再存一次**（把"坏状态"覆盖回好的），并 `Fault` 落日志。没有 `catch{}` 吞错。

### 3.4 容量：两维取小 + 排序即优先级

`HCoopRules.cs:27-40` 与 `HCoopOrders.cs:36-56`：

```csharp
public static int RubbleSlots(int f, int rep)
{
    int val = FamiliarSlots(f);                       // 0..3，熟悉度给的
    int val2 = rep >= 180 ? 3 : (rep >= 140 ? 2 : (rep >= 100 ? 1 : 0)); // 声望给的
    return Math.Min(val, val2);
}
// 扣减点：数到第几个，就在不在容量内
int num = o.reserveOnly ? HCoopRules.ReserveLimit(...) : Slots;
int num2 = 0;
foreach (HCoopOrder item in Ordered())
    if (item.active && item.reserveOnly == o.reserveOnly)
    { if (item == o) return num2 < num; num2++; }
return false;
```

### 3.5 市场周单：一条 `StoreRequest` 就是一张订单

`HShenMarketplace.cs:163-189`：

```csharp
StoreRequest val2 = new StoreRequest
{
    id = "henchman_market_week_" + num,
    title = HBusinessText.T("marketplace.order.title", "沈先生的市场周单", "Shen's weekly market order"),
    description = HBusinessText.T("marketplace.order.description", "按货单交齐后一次结款。...", "..."),
    requesterName = HPartnerServices.Name(shen: true),
    factionId = "FACTION_UPPER_LEVEL",
    requiredItem = product,
    requiredItemCount = Quantity,
    duration = Math.Max(1, num * 7 - day + 1),
    creditReward = HBusinessRules.Price(checked(num2 * Quantity), 160)
};
if (!StoreRequest.CanActivateRequest(val2)) return;
int marketplacePostedWeek = d.marketplacePostedWeek;
List<StoreRequest> storeRequests = PlayerStore.Instance.storeRequests;
try
{
    StoreRequest.ActivateRequest(val2, false);
    if (storeRequests.Contains(val2)) { d.marketplacePostedWeek = num; HBusinessState.Save(); }
}
catch { storeRequests.Remove(val2); d.marketplacePostedWeek = marketplacePostedWeek; throw; }
```

### 3.6 互助折扣：一张报价单，改价靠"作废旧单"

`HBusinessRules.cs:48-60` + `HNeighborhood.cs:189-193`：

```csharp
public static int NeighborhoodPricePercent(int stage, bool weeklySupport)
{
    if ((stage == 100) & weeklySupport) return 50;   // 满级 + 本周送够 → 半价
    return (stage >= 80) ? 90 : 100;                 // 80/100 档 → 九折（满级裸价就是九折）
}
// 接单时：档位或优惠变了，旧报价直接作废，不追改
if (order == null || order.stage != 0 || order.quotedDay != HBusinessState.Day
    || order.quantity > Limit || order.days != Days || order.free
    || order.pricePercent != DiscountPrice || order.materialDiscount != (order.category == "MATERIAL" && ...))
    return Fail(T("expired", "报价已过期...", "..."));
```

### 3.7 物品快照往返：`EncodeItem` + 临时换掉 `savedItemFeatureList`

`HStore.cs:752-784`（`EncodeInto`）：

```csharp
PlayerStore instance = PlayerStore.Instance;
List<ItemFeature> savedItemFeatureList = instance.savedItemFeatureList;
try
{
    instance.savedItemFeatureList = new List<ItemFeature>();   // 借原版的"当前编码上下文"
    string text = instance.EncodeItem(item);                    // 原版物品 → JSON
    if (string.IsNullOrEmpty(text)) throw new InvalidOperationException("Cannot encode " + item.identifier);
    string features = Convert.ToBase64String(ES3.Serialize<List<ItemFeature>>(instance.savedItemFeatureList));
    state.records.Add(new Record { json = text, features = features, pending = pending });
}
finally { instance.savedItemFeatureList = savedItemFeatureList; }   // 一定要还
```

对应 `Decode`（`HStore.cs:527-572`）：`JsonUtility.FromJson<SaveState>` → `SaveManager.DecodeNodes` → **`ApplyLoadFixes`（16 个原版 Load* 帮手 + `onLoaded`，`:573-594`）** → 若带 `features` 就 `ES3.Deserialize` 后按 `parentItemUniqueId` 挂回并 `ValidateShapeState()`。**漏掉 `ApplyLoadFixes` 是"读回来是一堆白板物品"的经典原因。**

### 3.8 Transpiler 的锚点自检（三处之一，值得抄的写法）

`HNetworkInvestigation.cs:9-36`：`[HarmonyPriority(200)]` + 在 `OpCodes.Add` 后紧跟 `StoresField(SecData.evidenceLevel)` 处插 `Call(Reduce)`，**计数命中数，为 0 就打日志「另一个模组重写了 OnNewDay」**。它没有假设"我的锚点一定在"，而是把共存降级写成可见事件。

---

## 4. 踩坑与红线

1. **性能修复只改报障点 = 留了同样的雷**。§3.2 是活证据：`HNeighborhood` 修了，`HBusinessMutual` 没修，两者同在日初链上。我们要修 O(n×m) 时，先全库搜同形的 `FindAllItem / FindAllChildrenType / GetAllOwnedItems`（本档 37 处命中）。
2. **日初那一帧是一条长链，13 件事串行 + 一次总存盘**。`HBusinessRuntime.WakeCore()`（`:28-54`）+ `HCoopRuntime.Wake()` + `HRiskService.Wake()` + `HCommissionService.TickOnWake()` 全在 `OnWakingUpEarly` 里同步跑完，每件都可能碰存档与全店遍历。我们的夜间/日初服务**要么分帧，要么把"重活"挪到打烊后**。
3. **五个 `GetNegociatedValue` Postfix 没有跨模组顺序声明**。它们靠 `HBusinessPricing.Suppress` 与各自的 `return` 互不干扰，但和外部模组（含我们）叠加时**没有任何保证**。要动这个热点必须 `[HarmonyPriority]` + `HarmonyBefore/After`。
4. **直接改 `PlayerStore.Instance.playerCash`（全库 ~20 处）绕过原版入账链**。本档只把 `ModHelper.AddCredit` 用在两处（`HBlackMarket.cs:197`、`HCommissionService.cs:155,431`），其余全是裸加减。**我们不学**：现金增减走 `ModHelper.AddCredit`，否则成就/统计/UI 刷新可能漏。
5. **没有 `HotReloadPrepare/Activate`**，热重载时靠 `OnDisable` 里 10 个 `Reset` 顶。我们在做重载支持时，别拿它当范本；静态字段遍布全库（仅 `HStore` 一个类就 12 个，含 `Overflow`/`Unresolved`/`TakenToday` 三个集合），重载后极易留下上一局的引用。
6. **`HRepUI` 往原版声望面板里 `Instantiate` 自己的 prefab 子物件**，靠 `HENCH_STAR_*` 名字前缀做"先删再插"。名字前缀就是它的幂等键——**一旦别的模组也用同样前缀（或原版改子物件命名），会互相删**。我们插任何原版面板，前缀要带自己的 mod id。
7. **`GetLocalized*` 用 `Prefix → false` 直替文案**，读的是"最后一个补丁赢"。同屏两个模组各挂一次就会有一方文案消失，且**没有任何报错**。
8. **`marketplacePostedWeek` / `marketplaceBlockedWeek` 是"每周一张"的唯一真相**。任何绕过它直接 `ActivateRequest` 的路径都会造成重复发单（`Publish` 里那圈 `try/catch` + 回滚就是为了这个）。
9. **`HSupplyStore` 满格不丢货，进 `overflow` 内存列表**（`HSupplyStore.cs:21,343-356`，`Add` 里 `MoveWhole` 失败就 `overflow.Add`）。**但 `overflow` 只活在内存里，靠 `Save()` 写进 `business_supply_store_v1` 的 Envelope**——如果那一步失败，物品就只剩内存副本。存档失败必须当致命处理（它用 `flag` → `Fault` 做了，但只是记日志）。
10. **零能力是干净的**：`System.Reflection` 只出现在 5 个文件里（3 个 Transpiler + 2 个 `TargetMethods`，全是 `AccessTools` + `OpCodes`），没有 `Assembly.Load`、没有 `File`/`HttpListener`，manifest 是空 `<Capabilities />`。**它证明了"写 Transpiler 也能零能力"**——但代价是锚点脆弱（§3.8）。
11. **`HBusinessState.Save()` 的编解码自检**（`:65-68`）值得抄：`Encode(Decode(text)) != text` 就抛。手写行式编码最容易在"分隔符/空列表/负数字段"上出错，自检是唯一能在写坏档之前发现的办法。
12. **两套订单系统并存**（`HNeighborhoodOrder` 与 `HCoopOrder`）导致"到货"有两个含义（定时入仓 vs 逐晚补齐），文案与 UI 也因此分叉（`HPartnerServices` 讲募集，`HCoopUI` 讲常年指令）。**这是作者自己付的重构债**，我们别复制：一套订单表 + 一个 `dueDay` 语义就够。

---

## 5. 对我们的可用点

### 对《深空电玩》（`deepspace_arcade` / `DeepSpaceArcade.dll`）

1. **街机店的「周单」= 一条 `StoreRequest`，零 UI**：我已经在 `ArcMarket`/`ArcIntel` 里读 `StoreStation.Instance`，但**没有挂 `StoreStation.HandleStoreRequests`**——这是一个我们完全没用过的免费入口。抄 `HShenMarketplace`：id 带 `arcade_market_week_<n>` 前缀、`duration = 本周剩余天数`、`creditReward` 用自家定价、`marketplacePostedWeek` 式幂等键。收益：光盘/机台订单直接出现在原版市场电脑里，不用维护第二套委托板。
2. **「机台排期容量」= 两维取小**：`HCoopRules.RubbleSlots` 的 `min(熟悉度槽, 声望档槽)` 正好套我们的「同时开机台数」——一维是机台改造等级，一维是店内声望。**扣减点用"排序即优先级 + 数到第几个"**（`HCoopOrders.EnabledOrder`），比维护一个 `usedSlots` 计数器安全得多（不会因为异常路径漏减而永久锁死）。
3. **到货/补货一律"先建索引"**：我们的 `ArcSupply`/`ArcStock` 里有若干 `FindAllChildrenType` 调用；任何在循环里查重的写法，照 §3.1 改成一次 `HashSet<int> uniqueId` + 落账单位改为"已处理的前 n 条"。这条能直接进我们的性能 checklist。
4. **`ArcShowcase` 的估价建议别新增 `GetNegociatedValue` 补丁**：本档已经在这个热点上挂了 5 个。若必须挂，先读它的 `HBusinessNativePrice`——它用「`TradingItem == __instance && TradingPrice >= 0` 才改结果」的作用域写法，把影响面缩到"自己发起的那一笔"，**这是在这个热点上唯一体面的写法**。
5. **模态窗挡物品交互，改一个字段就够**：`HModalRaycastPatch` 的 `__instance.hoveringStoreUI = true`。我们的街机 UI 若也有"窗口打开时别让玩家点到柜台物品"，别再写输入拦截。

### 对《深空当铺：可能是群友》（`freya_cornell` / `DeepSpacePawn.dll`）

1. **芙蕾雅的「电话 7712」可以直接扩成"派活/下单"入口**：本档 `HBusinessPhone.Register()` 用 `StorePhoneClient` + `GetLocalizedDisplayName` 补丁把瓦砾/沈先生做成可拨联系人（`HBusinessRubblePhoneName.cs`、`HShenPhoneName.cs`），号码存在 `HBusinessData.rubblePhone`。我们已经有 7712，缺的是"拨通后能下一张有 dueDay 的单"——照 `HNeighborhood.Accept` 的事务写法即可。
2. **睡前渠道 = 分相夜间结算 + 夜报去重**：`HCoopRuntime.Night()` 把一晚切成 `nightStage 0..4` 并**每相存盘**（`HCoopRuntime.cs:79-130`），崩在中途下次从断点继续；夜报只显示一次靠 `reportShownDay` 比对（`:57-65`）。这套结构正好给芙蕾雅的睡前渠道用——**它比"一晚上一个大 try"强的地方就是可恢复**。
3. **把关系星挂进原版声望面板**（`HRepUI` 的插星法）：`ReputationElement.Init` Postfix + `Instantiate(reputationUIPrefab)` + `GetBarHeight(threshold)` 定位 + 自带开关写 `ModHelper` 的 `perk_off`。前缀务必改成 `DSP_STAR_*`（见 §4.6）。
4. **访客寄存/托付物品用 `HStore.SnapshotRecord` 的往返**：`EncodeItem` + 临时换 `savedItemFeatureList` + `SaveManager.DecodeNodes` + `ApplyLoadFixes`（§3.7）。我们已经会写 `ES3Type.WriteProperties` 做写盘剥离，但"**在两次存档之间把一件物品整件搬到模组私仓再搬回来**"这套还没做过——伞萝萝/塞壬的定制道具若要"先收下、以后给"，这就是现成路径。
5. **熟悉度那种"日拱一卒"的门，比声望更适合做角色线**：`AwardWork` 的 +3（干活）+1（给新指令）设计，天然防刷且不需要反刷上限；`completedNodes` 每 20 一点、第 3 点定路线，正好对上芙蕾雅主线的分章节点。**声望给"能进门"，熟悉度给"能干活"**——这个双维门比单声望门槛耐玩。

---

## 附录 A 证伪边界 / 推测项

- **§3.2 的"旧写法"是推测的对照**：反编译源是 0.18.31（含 1127 更新），`HNeighborhood.Tick` 的索引写法是**修好之后**的形态。我把 `HBusinessMutual.Deliver` 当作"修之前长什么样"的**同版本反证**——两者在同一份 DLL 里、都在日初/夜间链上、做的是同一件事（把快照里的物品搬进 `HSupplyStore` 并去重），只差索引位置。作者是否真的在那个函数里改过，源码无法证明（无注释、无 git）。
- **"日初这一帧"的耗时无法从源码量化**：没有 profiler 数据。结论只到"复杂度从 O(件×全店) 降到 O(全店)"这一层；`FindAllItem(true)` 在 500 件/20 容器的存档上具体多少毫秒，未测。
- **`StoreClientCounter.Update` 的调用频率未实测**（推测与"顾客计数器/计时器"绑定）。若它其实是逐帧组件，本档等于每帧做一次队列维护。
- **`HPhase.HasProtection()` 恒返回 false**（`HPhase.cs:69-72`），说明"保护"这一支被砍或未接；不要以为它有实现。
- **`HBusinessMutual.Contribute(first, second)` 恒返回 false**（`:10-13`），是迁移后遗留的空壳；`HBusinessMutual` 现在只剩 `Deliver()` 与转发 `Tick()`。
- 与另两份档案的分工：`3828971731` / `3828949943` 讲**「声望效果怎么表达与生效」**（表 + 门 / 掩码 + 纯函数），本档**不碰这一层**（25 条星只做显示与开关），讲的是**「声望解锁之后那套经济体怎么转」**。三份合起来正好是"效果层 / 判定层 / 生产层"。
