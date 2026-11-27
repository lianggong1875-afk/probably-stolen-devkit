# 工坊 3828971731 架构蒸馏档案 —— 势力声望扩展 / Faction Reputation Expansion

> 工坊 id `3828971731`｜manifest ID **`faction_reputation_expansion`**（做兼容判断/写 harmony id 用这个，不是标题）｜程序集 `FactionReputationExpansion.dll`｜`ModVersion 1.6`（`AssemblyFileVersion 1.6` 一致）｜`GameVersions 0.46D`｜作者 魃月BaYue｜工坊包 manifest 写 `<Capabilities />`，即**零能力**
> 反编译源：`缓存文件夹\decomp_ws\3828971731`（178 个 `.cs` / 41,112 行，ILSpy 产物、零注释、变量名由反编译器生成）
> 交叉核对：`共享开发\reference\Assembly-CSharp\StoreReputation.cs`（原版反编译）——本文凡断言原版行为，都以它为准，并已逐条对行
> **为什么值得读**：它把「往原版进度系统上挂 44 条声望效果」做成了**一张表 + 一个门**（全库没有任何"效果类"），同时把我们正在踩的三件事一次做完：① 每日上限/递减这类**反刷规则**放哪、② 模组状态怎么进**原版存档事务**并原子提交/回滚、③ 自定义物品怎么在写盘前**从原版存档字段里剥离**。它还是这批标杆里少见的"零能力 + 裸反射读私有字段 + 唯一一个 Transpiler 还自带降级路径"三件齐活的样本。

---

## 0. 一句话结论

**它不改原版声望的数值，而是把原版 `StoreReputation` 变成一块"显示器"**：真值（声望/委托/预约/卡池/改造体栈）全活在模组私有键 `ModHelper.SetModData("faction_reputation_expansion","progress_v1",…)` 里，写盘时投影回原版字段并夹在 ±100；读档时按 `RunId + 原生指纹` 判断"是不是同一个档、原版档有没有被替换"。44 条效果 = 44 条 `PerkInfo` 记录，行为由 200 多个薄补丁各自用 `PerkActive(id)` 认领。

---

## 1. 规模与结构

| 目录 | 文件 | 行数 | 说明 |
| --- | ---: | ---: | --- |
| `FactionReputationExpansion\` | 146 | 37,461 | 全部业务 |
| `FactionReputationExpansion.Patches\` | 30 | 3,634 | 补丁独立层（占 **8.8%** 行数） |
| `Properties\AssemblyInfo.cs` | 1 | 12 | 版本号 |
| `System.Runtime.CompilerServices\IsExternalInit.cs` | 1 | 5 | C# 9 `init` 垫片（net472 必需）→ 原版源码里应有 `init`/`record`（ILSpy 回写成 `set`）（推测） |
| **合计** | **178** | **41,112** | |

- **夹具不用猜**：225 个 `[HarmonyPatch]` 特性 → **147 个不同的（类型,方法）靶点** → **284 个补丁方法**（`Prefix` 135 = bool 59 + void 76 / `Postfix` 128 / `Finalizer` 20 / **`Transpiler` 1**）。同一方法被多次挂是刻意的（`GameItem.GetNegociatedValue` ×3、`PlayerStore.WriteSlotFile` ×3）。
- **按目标类型看它动了谁**：`PlayerStore` **22 个方法**、`StoreReputation` 10 个、`StoreClient` 7 个、`SecData` 5 个、`GameItem` 5 个、`StoreClientManager` 4 个、`StoreRequest` 4 个、`ItemFeature` 2 个。**它几乎把"原版声望/顾客/存档"三条链的入口全挂了一遍**——这也是它敢叫"扩展"的原因，也是它的最大维护成本。
- 规模分布：中位数 **67 行**、p90 674 行、最大 `AugCoreDraft.cs` **6,537 行 / 188 KB**；>800 行 11 个（`AugCoreDraft` 6537 / `GestaltStory` 1922 / `V2Campaign` 1350 / `AugFaction` 1232 / `CartelRoute` 1102 / `ReputationProgression` 1074 / `ProgressionUI` 964 / `RepProfile` 950 / `ScreenToast` 930 / `CounterGiftService` 894 / `CartelExpansion` 854）。
- 模块划分（全在文件名里，无子目录）：`Reputation*` 核心进度、`Rep*` 状态/存档/身份/门禁、`Aug*` 改造体（含 188 KB 巨兽）、`Cartel*` 卡特尔、`Gestalt*` 改造体剧情与物品、`V2*` 二代战役、`*Page`/`ProgressionUI`/`ScreenToast`/`PagedMenu` 表现、`Native*` 原版数据镜像、`Phone*`/`Visitor*` 电话与访客、`Official*` 原版存档手术。
- **冻结副本：有，而且是两份**。`NativeItemIndex.cs` 最长行 **19,381 字符**（`Parse()` 里内联一张 403 条 TSV 原版物品表）；`FixedNativeItemData.cs` 最长行 **19,380 字符**（`internal const string Rows`，**同一个表的另一份拷贝**，整个文件只有 **6 行**）。两份都是 0.46D 的手抄快照，运行期不读原版，代价是官方改价/改标签后**两份一起过期且没有任何检测**。除此之外全库**没有整体重复文件**（去空白后哈希无碰撞）。
- 零注释：可读性完全靠命名 + `ModText` 键（**625 个唯一 `fre.*` 键、710 处调用**）与 `Bilingual` 双语字符串承担。

---

## 2. 它怎么做这件事（七问逐个回答）

### 2.1 40 余种声望效果怎么表达：**一张表 + 一个门，没有"每种效果一个类"**

表在 `RepConfig.ExtendedPerks`（`RepConfig.cs:187-217`，27 条 `NewPerk(...)` + `AugAllianceStages.BlackMarket` 1 条），再 `AddRange(AugAllianceStages.All)`（`:218`，16 条）→ **44 条**。字段只有 7 个：`Id / FactionId / Threshold / NameZh,DescZh,FlavorZh / NameEn,DescEn,FlavorEn / IconName`（`RepConfig.cs:8-65`）。分档覆盖：

| 势力 | 档位（Threshold） | 条数 |
| --- | --- | ---: |
| `FACTION_LOWER_LEVEL` | 120 / 140 / 160 / 180 / 200 | 5 |
| `FACTION_UPPER_LEVEL` | 120 / 140 / 160 / 180 / 200 | 5 |
| `FACTION_SECURITY` | 120 / 140 / 160 / 180 / 200 | 5 |
| `FACTION_BLACK_MARKET` | 80 / 100 / 120 / 140 / 160 / 180 / 200 | 7 |
| `FACTION_REVOLUTION` | 100 / 120 / 140 / 160 / 180 / 200 | 6 |
| `FACTION_AUGMENTED` | +0…+200 十一档、**−20…−100 五档** | 16 |

效果**两种形态，同一张表**，靠"有没有触发点"区分，而不是靠类型：

- **被动数值**：直接改原版 `ItemFeature` 的 valueModifier——`UL_PRIVATE_SALON` 把小费 feature `tips` 从 15 改 20、`LL_SAFEHOUSE` 把 `discountBuy15` 从 −15 改 0（`ItemTradingFeaturePatches.cs:60-88`）；也改顾客预算（`UpperLevelPatches.cs:38-49`，×2.5 并打 `FRE_BUDGET_APPLIED` 防重复）。
- **需要触发的事件**：每条自己认领原生节拍——`StoreClientManager.OnNewDay` 发周分红（`UpperLevelPatches.cs:9-36`）、`StoreStation.HandleStoreRequests` 推早报（`MarketBoardUI.cs:56-63`）、`AdvCalendarUIManager.HandleInnateEvent` 重建缺货行情（`RevolutionShortageRebuild.cs:6-9`）、预约供货/收购走电话与 F6 页。

**唯一的门**是 `ReputationProgression.PerkActive(id)`（`ReputationProgression.cs:524-541`）：先问原生 `StoreReputation.IsPerkUnlocked(id)`，没解锁再按自家表判 `声望 >= Threshold`。所以每个补丁体第一行都是 `if (!PerkActive("XXX")) return true;` —— **加一条效果 = 往表里加一行 + 在一个已有补丁里加一个 id 判断**，不需要新类、不需要注册。注意负阈值那 5 条**不走**这个门（`:531` 对 `Threshold < 0` 直接 `return false`），它们靠原生 `UpdateReputation` 的 `amount <= perkThreshold` 分支自动解锁（原版 `StoreReputation.cs:145-155`）——这是全库最容易改错的一处。

### 2.2 它 Patch 了原版的什么（含红线核对）

**声望获取点**：原生只有一条路——`StoreReputation.ModReputation(double)`（原版体含开局特质与难度乘子，`reference\Assembly-CSharp\StoreReputation.cs:88-116`）与 `ModReputationRaw(double)`。本模组**没有用 Prefix 直接改倍率**：

- `ModReputation/ModReputationRaw` 的 Prefix 只记 `Before` 入栈，`Finalizer` 出栈（`ProgressionHooks.cs:19-47`）；
- 真正的改写发生在 `CheckReputation` 的 Prefix 里：`rep.SetAmountRaw(Before + LimitGain(faction, Before, 原生增量, 是否交易))`（`ProgressionHooks.cs:247-274`）——**让原版先算完，再按绝对值改写这一笔增量**，所以原生乘子、难度、特质全部保留，不会双重打折。

**Appraisal Assistant 五红线核对（按要求逐条 grep）**：

| 红线方法 | 本模组是否 Patch | 证据 |
| --- | --- | --- |
| `GameItem.IsTag` | **否**（0 命中） | 全库无 `[HarmonyPatch(typeof(GameItem),"IsTag")]`；`IsTag` 只作为读取调用（`AugCoreDraft.cs:325-326` 等） |
| `AppraisalHelper.GetCorrectBaseValue` | **否** | `GetCorrectBaseValue` 全库 0 命中 |
| `AppHand/Ribwich/PlasmaHelper.GetCorrectValue` | **否** | `GetCorrectValue` 全库 0 命中 |
| `AppraisalItemType.Matches` / `AppraisalItemRegistry.Find` | **否** | 两个符号全库 0 命中 |

擦边三处，性质不同，务必分清：① `GameItem.GetNegociatedValue` 挂 3 处，但都是**在原结果上按场景重算/打折**，且都带 `__result <= 0` 早退与 `RepRules.ScalePrice` 的下限夹（`Math.Max(0.1, multiplier)`、`price<=0` 原样返回，`RepRules.cs:59-67`）——不是把估价函数整体替换；② `ItemFeature.GetBaseRealDisplay/GetBaseFakeDisplay` 做本地化，但**会写 `condition.display`**（`AuthenticityLocalization.cs:59`），是全库唯一"改原版共享对象字段"的显示层补丁（风险见 §4.3）；③ `StoreReputation.CheckReputation` 用 `Prefix → false` **整段替换**（`ReputationCheckPatch.cs:8-22`），这是全库唯一"改原版行为语义"而非改数值的地方（原生体只有 `clamp + UpdateReputation` 两句，`StoreReputation.cs:118-122`）。

### 2.3 Patch 靶点表（原版方法 → Prefix/Postfix → 目的 → 风险）

| 原版方法 | Patch 形 | 目的 | 风险 |
| --- | --- | --- | --- |
| `StoreReputation.CheckReputation` | **Prefix→false**（整段替换） | 注册扩展 perk + 跑自家封顶/任务锁 + 重刷 UI | **全库最高**：今天与原生等价（clamp+UpdateReputation），官方一旦在此加职责（成就/剧情/事件）就被静默吞掉 |
| `StoreReputation.ModReputation` / `ModReputationRaw` | Prefix 记 before + Finalizer 出栈 | 取得"这一笔原生增量"的基线 | 栈式配对，只有 `Changes.Peek()` 同势力才生效；嵌套/跨势力调用会走 else 分支重算（`ProgressionHooks.cs:253-271`） |
| `StoreReputation.OnItemTraded` | Prefix/Finalizer 数深度 | 判"这笔声望是不是交易产生的"（只有交易计入日上限） | 交易深度内**任何**来源的声望都被算作交易，剧情赠与若嵌在交易里会被误限 |
| `StoreReputation.IsPerkUnlocked` | Postfix 强改 false（任务锁）；另有 Prefix 强改 false（`UL_PETITION`） | 未过委托门的效果不许生效；用"政商特赦"顶掉原版"请愿"加倍 | **跨模组可见**：别的模组读它会拿到 false；同方法两补丁靠 id 分责，无优先级声明 |
| `StoreReputation.InitReputation` | Postfix | 把 44 条 perk 塞进原生 `faction.perks` | 原生只在 `PlayerStore.cs:810` 调一次；读档后新增的 perk 要靠 `CheckReputation` 补登 |
| `StoreReputation.GetReputationStatus` / `GetFactionNameByID` / `UpdateReputation` | Prefix，`amount > 100` 才接管并 `return false`；否则让位 | 6 档头衔表（`RepConfig.cs:234-257`，`ReputationStatusPatch.cs:8-16`）、卡特尔/改造体势力名 | 纯显示+刷新，低风险；100 以内仍走原版文案 |
| `StoreClient.CompleteClientCreation` | Postfix | 上层区预算 ×2.5、改造体预算分层 | 靠 `FRE_BUDGET_APPLIED` 标签防重复；`IsFaction` 严格匹配，误判即全体加预算 |
| `StoreClient.GetBudget` / `OverrideBudget` / `HasBudgetLeftToBuy` / `ApplyBudgetModifier` | 4 处 | 新生核心"预算池"与显示改写 | 预算被多处写，靠标签记账 `FRE_NC_BUDGET_V1:base:grant` 防叠加（`CoreBudgetPool.cs:92,157-190`）；漏一处就双算 |
| `GameItem.GetNegociatedValue` | Postfix ×3 | 折扣/溢价/销赃保底 | 三个补丁各自 `return`，无 `HarmonyPriority`——同一件货可能被叠乘（未实测） |
| `ItemFeature.GetBaseRealDisplay` / `GetBaseFakeDisplay` | Postfix 且写 `condition.display` | 8 个原版成色 identifier 走本地化 | 写的是共享 `ItemCondition` 实例 → 同 condition 的所有物品一起改；只在 `useCondition` 且 identifier+category 双匹配时动 |
| `SecData.OnNewDay` | Prefix/Postfix/Finalizer 三段 | 调查推进 −25%、案卷留白 99 | 自己**重算**原生公式（`UpperLevelPatches.cs:160-201`）→ 官方改公式即失真 |
| `PlayerStore.QueueFuturClient` | Prefix→false | 延后治安官上门，配合特赦 | 只拦 `securityOfficer`，其余 `return true` |
| `PlayerStore.SaveGame` / `WriteSlotFile` | Prefix + Finalizer 成对 `Arm/Disarm` | 写盘前后把被改写的原生 feature 值投影/还原 | 与官方写盘同帧；`_depth` 支持嵌套，归零才还原（`NativeFeatureProjection.cs:55-71`） |
| `PlayerStore.DecodeSaveItem` | Prefix 重写入参字符串 | 读档前把剥离的自定义物品**塞回**原版 inventory JSON | 抛异常即 `LoadFailed` 并进 SafeMode；`Single()` 找不到父节点会抛（`OfficialItemArchive.cs:177-219`） |
| `PlayerStore.StartNewGame` / `LoadGame` | `TargetMethods()` 一次挂两个 | 重置 8 个静态运行态 | 用 `MethodInfo[2]` 声明，是本库唯一的"家族式"声明 |
| `StoreRequest.FillRequest` / `CheckDeliveredItem` / `DispenseReward` / `ActivateRequest` | 4 处 | 让"预约收购/供货"寄生原生委托交付与奖励链 | 原生委托板与自建预约共用同一个类，只能靠 identifier 区分 |
| `PlayerStore.EncodeItem` | Prefix(out `SaveProjection`) + Finalizer | 单件物品编码期间开"新生核心特征投影"作用域 | 作用域式（`AugCoreEffects.SaveProjection`），异常也 Dispose；只覆盖单件编码，不覆盖整档 |
| `NetworkUpgrade.Unlock` | Prefix | 非化学家开局的卡特尔入口 | 只判"退役化学家已解锁"，漏判即入口不开 |
| `DirectoryMaster.Item` | 重载显式类型表 | 拦自建物品的创建 | 本模组**做到了**重载显式签名（`GestaltHooks.cs:55`） |
| `AugHelper.OnAugLeftAlive` | **Transpiler + Prefix/Postfix 双保险** | 把"放走改造体涨在场压力"改成自家规则 | 全库唯一 Transpiler，见 §3 E6 |

### 2.4 每日上限 + 随声望递减：三层数学，全在模组私有键

| 层 | 规则 | 出处 |
| --- | --- | --- |
| 递减速率 `RateAt(rep)` | <100 → 1.0；100-120 → 0.9；120-140 → 0.75；140-160 → 0.6；160-180 → 0.45；≥180 → **0.3** | `ReputationProgression.cs:421-444` |
| 每日硬上限 `CapAt(rep)` | <120 → 6；<140 → 5；<160 → 4；<180 → 3；≥180 → **2** | `:446-449` |
| 软上限 `ApplySoftCap` | 用量的 60% 以内全额，超出部分**打五折**，且硬顶 `cap - used` | `:451-455` |
| 用户覆盖 | 配置项 `daily_trade_reputation_cap`，默认 6、范围 1~200，改了就重建当日 | `ReputationGrowthSettings.cs:7-33` |

**存在哪**：`ProgressionData{Version=1, Revision, RateStartDay, Growth: Dictionary<势力, GrowthDay{Day,Used,Cap,AutomaticCap,Notified}>, Quests}`（`ProgressionData.cs`）→ `ModJson` → `RepState.Write("FRE_V2_PROGRESSION")`（`ReputationProgression.cs:10,176-183`）→ `RepSideState.Data.State` → `RepOfficialStorage` 信封 → **`ModHelper.SetModData("faction_reputation_expansion","progress_v1", base64)`**（`RepOfficialStorage.cs:58,80`）。**不碰原版存档结构**，`Version != 1` 直接抛 `InvalidOperationException`（`ReputationProgression.cs:123-126`）。

**只对交易计上限**：`LimitGain(..., bool trade)`，只有 `trade=true` 才走 `RateAt/ApplySoftCap`（`:558-565`）；`trade` 由 `OnItemTraded` 的深度计数决定。剧情赠与走 `GrantStory`，它**不受日上限约束，但受委托门截断**，余量进 `StoryReserve`（`:392-409`）。

**跨存档怎么迁移**（这是本档最值得抄的一节）：

| 机制 | 做法 | 出处 |
| --- | --- | --- |
| 身份 | `RepSideIdentity{SlotId, RunId, RunNumber, StartingDay, NativeSlot, NativeDigest}` | `RepSideIdentity.cs` |
| 六态读档 | `Missing / Current / Changed / Corrupt / Future / Foreign`，`RunId` 不符 → `Foreign` | `RepOfficialStorage.cs:164-206` |
| 原生指纹 | `NativeStamp = "R1:" + SHA256(runPlayTimeSeconds)`，判"原版档是否被替换" | `RepOfficialStorage.cs:231-234` |
| 分流动作 | `Foreign`→`SeedForNewRun`；`Changed`→`AbandonIncomplete`（丢未完成预约/收购，保留已完成）；`Future/Corrupt`→`EnterSafeMode` **且不写盘** | `RepProfile.cs:404-540, 618-700` |
| 增量对齐 | 回填原版字段用 `Exact + (原生当前值 − Projection)`——**只认外部改动量，不覆盖** | `RepProfile.cs:834` |
| 投影夹取 | 写回原版 `amount` 前夹 ±100（`MaxProjection = 100`），真值留在模组侧 | `RepRules.cs:9-26` |

### 2.5 F6 自绘面板：自研 uGUI/TMP，不碰 `PixelWindow`、也不用 `OnGUI`

- 全库 `OnGUI` / `GUILayout` **0 命中**；`ProgressionUI` 自己建运行时 canvas（`GameObject canvas/body/footer/detail/panel` + `TextMeshProUGUI` + `ScrollRect`，`ProgressionUI.cs:10-96`），取原版纸色板 `Ink/Muted/Paper/Card/Dark/Copper/Teal`（`:22-32`）——**视觉贴合原版、代码自绘**。
- 7 个页签 = 5 个原生势力 + `FACTION_AUGMENTED` + `FACTION_CARTEL`（`:19`），并按内容开关过滤（`:107-114`）。
- **扩展点**：`JournalAddons.Page`（`JournalAddons.cs:9-33`，`title/build/tick/visible/bypass` 五个委托）——`SupplyPage` / `AcquisitionPage` / `UpdateLogPage` 在 `Init` 里 `Register()` 成插页（`RepMod.cs:111-134`）；`Page.Bypass` 还兼做"这页不需要过委托锁"的开关（`ReputationProgression.cs:494` 的 `JournalAddons.BypassesQuestLock`）。**这是"一个面板装多个子系统"的现成骨架。**
- **快捷键**：不注册原版输入系统，`Tick()` 里轮询 `Input.GetKeyDown(JournalKey)`（`RepMod.cs:198-201`）；默认值 `KeyCode 287 = F6`（`RepMod.cs:594`，文本默认 `RepConfig.cs:186`）；改键是"捕获下一键"状态机（`CapturingHotkey`，`:161-197`）+ `HotkeyRules.IsBindableName` 过滤鼠标/手柄键（`:20,592`），配置项持久化名字。
- 模态：自维护 `BlocksBackground`，另有 `JournalInputPatches`（79 行）挡输入穿透。
- 电话入口：`PhoneFactionButton.Show(PhoneUIManager)` 往原生手机界面**插一个按钮**（`PhoneFactionButton.cs:6-14`），`PhoneFactionButtonOpen/Close` 两补丁管显隐。

### 2.6 委托任务锁：**委托是自建的**，只有"交付/奖励"寄生原版

- 15 条委托写死在 `ReputationProgression.Definitions`（`ReputationProgression.cs:28-45`）：5 势力 × (100/140/160/200) 门，`Gate` 就是该势力的关卡值，`AcceptAt => Gate`（`QuestDefinition.cs`）。中文条件直接写在字符串里（"向下层区顾客卖出8件食物或医疗物品…"）。
- **锁的机制**：`LockFor(faction)` 取该势力第一条未完成委托（`:492-503`）→ `LimitGain` 在 `before + gain >= Gate` 时把超出部分截住、塞进 `TrustQuest.Reserve`（上限 +5，`:573-580`）→ `ApplyLocks` 反向把已被推过头的声望**压回 `Gate`**（`:594-627`）。于是**不做完 100 门委托，声望永远过不了 100**。剧情赠与同样被截，余量进 `StoryReserve`。
- **判定用事件流**：8 种 `Kind`（`sale/barter/crate/open/raid/visit/cartel_visit/delegate/nightout`）+ `ProgressEvent` 的 10 个字段（`ProgressEvent.cs`）；`Record` 逐条比对 `TrustQuest.Counts/Sets`（`ReputationProgression.cs:661-709`），达成时一次性把 `Reserve` 补进声望（`:687-694`）。
- **有没有往原版委托界面加条目？没有。** 它没有往 `PlayerStore.storeRequests` 里塞自建委托；委托 UI 是自建（F6 + 早报 + `UiSignal.ShowCard`）。真正寄生原版的是**预约收购/供货**：`BoardOrder.Request = new StoreRequest()`（`BoardOrder.cs:7`）+ 4 个 `StoreRequest` 补丁，让交付、奖励、解锁走原版流程（`MarketDelivery.cs:23,69,85`、`MarketBoardUI.cs:43`）。

### 2.7 两条路线：卡特尔（组织成员 / 独立经营）与改造体（四条分支）

| 线 | 选择枚举 | 存哪 | 后续谁读 |
| --- | --- | --- | --- |
| 卡特尔 | `CartelDecision{Pending, Joined, Independent}` + `Refused` + `NativeStart` + `LeftOrganization` | `RepState.Write("FRE_V2_CARTEL_ROUTE")`（`CartelRoute.cs:308,397`），落在 `RepSideData.State`，即 ModData | `CartelRoute.Data.Decision` 在 `CartelRoute` 内被读 12 处（`CartelRoute.cs`），另有 `CartelBoard`/`CartelExpansion`/`CartelTrade`/`CartelStatus`/`MarketDelivery` 读 `CartelRoute.Data`；`CartelRoute.Refused` 决定 F6 是否显示卡特尔页（`ProgressionUI.cs:107-114`） |
| 改造体 | `GestaltRoute{None, Central, Free, Suppressed, Mutual}` | `GestaltStory.State` 整份 JSON 存 `FRE_AUG_GESTALT`（`GestaltStory.cs:68,90-100`），同样落 ModData | `GestaltStory`/`GestaltHoldingEffects`/`AugPatches` 按 Route 分支 |

**入口挂在原版开局上**：卡特尔线靠 `PlayerStore.IsChemistStart()`（原版化学家开局 = 原版卡特尔线）走 `NativeStart`；非化学家开局要 `NetworkUpgrade.IsRetiredChemistUnlocked()` 才开（`CartelRoute.cs:412,544`）。改造体线靠**对话场景 + 选项**一次定案——`GestaltState.Complete(scene, choice, day)` 里 4 个 if 直接把 `Route` 写死（`GestaltState.cs:458-496`）：`device_inquiry/hand_executor→Central`、`alpha_progress/hand_alpha→Free`、`police/hand_police→Suppressed`、连 4 次 `wait→Mutual`；`Done` 集合保证每场景只结算一次，`Days` 记日期备查。
**只有一处互斥判断**：改造体追踪许可要求 `GestaltStory.State.Route != Mutual`（`AugPatches.cs:25-35`）。

### 2.8 新增物品与"很超模"的数值：**纯数据 + 一条通用 buff 引擎**

- 物品清单：`fre_aug_core`（新生核心，`ItemDirectory.CreateEmptyItem` 建，`AugCoreDraft.cs:2854`）、`FRE_NC_DESTINED` 标签（推测即"命定模组"，`OfficialItemArchive.cs:289-301` 按它挑出要剥离的物品）、`fre_gestalt_alpha/beta/gamma(+bloody_beta/gamma)`（神秘仪器，`GestaltItems.cs:46-50`）、以及一批 `fre_gestalt_*` 剧情物。
- **引擎**：`AugCoreDraft.Buffs` 把一切效果写成**扁平键值**——`FRE_NC_B_<id>[#source]_V`（数值）+ `_U`（到期日），配 `Apply/ApplyStacking/Sum/ActiveFlag/ConsumeCharge/DaysLeft/Active`（`AugCoreDraft.cs:35-173`）；叠层用 `FRE_NC_STACK_<id>_<source>` 计数改名（`:46-57`）。所以**新物品不带逻辑**，只往 `State` 里写几组 buff 键；应用侧全是 Postfix（`ATTRACT`→吸引力、`SELL/SELL_CONTRA`→卖价、`BUDGET_*`→预算、`REP_GAIN/REP_MULT`→声望……）。
- **数值上限保护（三处有、两处没有）**：有——声望域 `ClampReputation` ±200（NaN→0）、投影 ±100（`RepRules.cs:11-26`）；预算域 `ScaleBudget` 夹 `[0,int.MaxValue]`、`ScalePrice` 用 `Math.Max(0.1,multiplier)` 夹下限（`:54-67`）；预算增益用标签 `FRE_NC_BUDGET_V1:base:grant` 记账防重复叠加（`CoreBudgetPool.cs:92,157-190`）。**没有**——`Buffs.Sum("SELL")` 纯求和无总上限，`CoreSellPatch` 直接 `__result * (1 + num/100)`（`AugCoreDraft.cs:320-338`）；也没有"单件物品最多几条 buff"的校验。**超不超模完全由数据里写的 value 决定**，这是它的设计取向，不是防护遗漏（推测作者是有意的）。

---

## 3. 关键代码摘录

**E1 `ReputationProgression.cs:446-455`——反刷三层里的两层（`RateAt` 是同一形态的 if 阶梯，略）**

```csharp
public static double CapAt(double rep)
{
    return (rep < 120.0) ? 6 : ((rep < 140.0) ? 5 : ((rep < 160.0) ? 4 : ((rep < 180.0) ? 3 : 2)));
}

public static double ApplySoftCap(double scaled, double used, double cap)
{
    double num = Math.Min(scaled, Math.Max(0.0, cap * 0.6 - used));
    return Math.Max(0.0, Math.Min(cap - used, num + (scaled - num) * 0.5));
}
```

**E2 `ProgressionHooks.cs:247-274`——不改倍率，改"这一笔增量"的绝对值**

```csharp
public static void Check(StoreReputation rep)
{
    if (!ReputationProgression.Ready) return;
    if (Changes.Count > 0 && Changes.Peek().Rep.factionId == rep.factionId)
    {
        Change change = Changes.Peek();
        if (!change.Done)
        {
            change.Done = true;
            rep.SetAmountRaw(change.Before + ReputationProgression.LimitGain(
                rep.factionId, change.Before, rep.GetReputationExact() - change.Before, TradeDepth > 0));
        }
    }
    else
    {
        QuestDefinition questDefinition = ReputationProgression.LockFor(rep.factionId);
        if (questDefinition != null && rep.GetReputationExact() > (double)questDefinition.Gate)
        {
            double nativeGain = rep.GetReputationExact() - (double)questDefinition.Gate;
            rep.SetAmountRaw(questDefinition.Gate);
            rep.SetAmountRaw(rep.GetReputationExact() + ReputationProgression.LimitGain(
                rep.factionId, rep.GetReputationExact(), nativeGain, trade: false));
        }
    }
    ReputationProgression.SyncAcceptance();
    ReputationProgression.Save();
}
```

**E3 `ReputationInitPatch.cs:89-101`——44 条效果怎么进原版列表（幂等 + 就地更新阈值）**

```csharp
StoreReputationPerk item = new StoreReputationPerk
{
    perkId = info.Id,
    perkThreshold = info.Threshold,
    perkType = 1,
    iconName = info.IconName,
    isUnlocked = false,
    firstTimeUnlockActionDone = false,
    isAlwaysActive = false
};
faction.perks.Add(item);
return true;
```
（同方法 `:79-88` 先找同 id 的 perk，命中就只刷 `perkThreshold/iconName` 并返回 false——所以重复调用不会翻倍。）

**E4 `RepOfficialStorage.cs:73-113`——存档事务三段式（提交 / 标记原生已写 / 否则回滚）**

```csharp
internal void Prepare()
{
    _items?.Dispose();
    _data.State = new Dictionary<string, string>(RepSideState.Data.State, StringComparer.Ordinal);
    _items = new OfficialItemArchive.Projection(_store, _data);
    _data.Identity = RepProfile.CaptureIdentity(_store);
    _written = Encode(_data, _store.runID ?? "", NativeStamp(_store), RepState.Day);
    ModHelper.SetModData("faction_reputation_expansion", "progress_v1", _written);
}

public void Complete() { _committed = _nativeWritten && _written != null; }

internal void NativeWritten() { _nativeWritten = true; }

public void Dispose()
{
    if (_done) return;
    _done = true;
    if (_saving == this) _saving = null;
    _items?.Dispose();
    string text = (_committed ? _written : _old);   // 没走完原生写盘 → 回滚旧值
    if (text == null) ModHelper.RemoveModData("faction_reputation_expansion", "progress_v1");
    else ModHelper.SetModData("faction_reputation_expansion", "progress_v1", text);
}
```

**E5 `OfficialItemArchive.cs:224-228, 289-301`——哪些东西要从原版存档里摘出来**

```csharp
private static readonly string[] Fields = new string[11]
{
    "main", "back", "hidden", "showcase", "sold", "hireling", "vending", "fountain", "trash", "doc",
    "cassette"
};

private static bool Special(SaveItemNode n)
{
    if (!(n.identifier == "fre_aug_core") && !GestaltItems.IsQuest(n.identifier))
    {
        TagSystem itemState = n.itemState;
        if (itemState == null) return false;
        return itemState.dict.ContainsKey("FRE_NC_DESTINED");
    }
    return true;
}
```

**E6 `AugPatches.cs:45-66`——全库唯一 Transpiler：改一处 opcode，找不到就降级**

```csharp
public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
{
    List<CodeInstruction> list = new List<CodeInstruction>(instructions);
    MethodInfo methodInfo = AccessTools.Method(typeof(AugFaction), "ApplyReleasePresence", (Type[])null, (Type[])null);
    if (PresenceRiseField == null || methodInfo == null)
    {
        RepMod.L?.Error("[FRE] release presence patch: target field/method missing; fallback active.");
        return list;
    }
    for (int i = 1; i < list.Count; i++)
    {
        if (!(list[i].opcode != OpCodes.Add) && !(list[i - 1].opcode != OpCodes.Ldfld) && object.Equals(list[i - 1].operand, PresenceRiseField))
        {
            list[i].opcode = OpCodes.Call;
            list[i].operand = methodInfo;
            _presenceNeutralized = true;
            return list;
        }
    }
    RepMod.L?.Error("[FRE] release presence patch: augLeftAlivePresenceRise add not found; fallback active.");
    return list;
}
```
（配套 `Prefix` 存 `augPresence`、`Postfix` 还原，`_presenceNeutralized` 为 true 时跳过还原——**Transpiler 失效时功能靠 Prefix/Postfix 保底**，`:68-90`。）

---

## 4. 踩坑与红线

**4.1 红线核对结论（可直接引用的正面样本）**：44 条效果、147 个靶点，**一个都没碰 Appraisal Assistant 的五红线**，包括最容易被顺手挂上的 `GameItem.IsTag`。价格链只做"结果层打折"，且都带下限夹与早退。**要抄它，先抄这个"干净靶点集"。**

**4.2 唯一"改语义"的补丁是 `CheckReputation` 整段替换**：原生只有 `amount = clamp(amount,-200,200); UpdateReputation();`（`StoreReputation.cs:118-122`）。它替换后多做了"注册扩展 perk / 跑封顶与任务锁 / 再刷 UI"，今天等价；**这是我们自己绝不能照抄的形态**——同样需求应该写成 `Postfix`，把"注册 + 封顶"叠在原生之后。

**4.3 写共享 `ItemCondition.display` 是隐藏地雷**：`AuthenticityLocalization.cs:59` 直接改 `condition.display`。`ItemCondition` 是共享实例，同一 condition 的所有物品会一起变；模组卸载后已写进内存的 display 不会自己回滚（存档里存的是 identifier，所以下次读档会恢复——**内存态污染，不是坏档**）。

**4.4 自算原生公式 = 版本升级即失真**：`GetNativeInvestigationGrowth`（`UpperLevelPatches.cs:160-201`）把原版调查成长六档阈值 + 三个原版 perk 乘子**抄了一遍**，只为在"证据刚好到 100"那一步能算出原生增量。同理 `RestoreNativeRetirement`（`CartelRoute.cs:490-520`）把原版 `cartelExitStage/cartelRequiredCount/cartelRequiredPurity` 抄进自家状态。官方一调参，这两处静默走偏。

**4.5 零能力的真实代价：文件 IO 被编译成"抛异常的桩"**。`FileRepSideIo` 构造函数直接 `throw new NotSupportedException("Use the legacy migration build to import file-based progress.")`（`FileRepSideIo.cs:9-12`）；`LegacyRawFiles` 除 `Exists()` 恒返回 false 外，其余五个方法全抛 `"Legacy file access requires the migration edition."`（`LegacyRawFiles.cs:5-46`）。也就是说**存档格式要大改时它不能就地迁移，只能同时发一个"migration edition"**。我们走零能力时要有同样的心理准备：一旦把 `System.IO` 从 IL 里删干净，迁移工具就必须另做一个构建。

**4.6 全库 0 个 `HotReload` 钩子**：`HotReload` 全库 0 命中，静态 `_data/_raw`、`Changes` 栈、`NativeFeatureProjection.Tracked/Projected`、`ProgressionUI.canvas` 都不在 `HotReloadPrepare/Activate` 里重置。热重载后最可能的表现是"面板空白 / 声望不涨"，只能读档或重启。

**4.7 负阈值效果走两套判定**：`PerkActive` 对 `Threshold < 0` 返回 false（`ReputationProgression.cs:531`），负档靠原生 `UpdateReputation` 的 `amount <= perkThreshold` 解锁（`StoreReputation.cs:145-155`）。两套语义混在一张表里，改表时极易误判"为什么 −40 的减益没生效"。

**4.8 两份冻结的原版物品表**（§1）——官方一改价就双份过期，且没有 `Count` 断言或版本校验兜底，只有 `new List<NativeItemMeta>(403)` 这个容量提示。

**4.9 补丁延迟安装会永久放弃**：`ModuleEffectPatches` 把 6 个补丁类扣住（`Targets`，`:15-23`），等 `Mechanic` 本地化表就绪才 `CreateClassProcessor(type).Patch()`（`:113-118`）；中途失败会逐个 `Unpatch` 再 `throw`，外层 `catch` 置 `failed = true` **永久不再重试**（`:138-144`）。换语言、热更语言包之后不会重装——这是"延迟装补丁"这个技巧的代价。

**4.10 全局闸门是它最被低估的一件基础设施**：`V2Persistence.RuntimeBlocked = Busy(存档中/读档中) || RepProfile.SafeMode`（`V2Persistence.cs:289-301`），被 **22 个文件**引用，绝大多数是补丁体的第一道 guard。存档过程中一切效果补丁自动静默，坏档/未来版本时整模组变只读。**我们的补丁目前没有这个闸门。**

---

## 5. 对我们的可用点（点名到《深空电玩》/《深空当铺：可能是群友》）

1. **「一张表 + 一个门」替掉 if 森林**——《深空电玩》的派客档位/眼力四档、《付费情报》的解锁条件，以及《群友》伞萝萝的 `MinAttractiveness 300 / 450` 两档，都该收敛成 `PerkInfo{Id, FactionId, Threshold, 双语名/描述}` 表 + 一个 `PerkActive(id)` 门；以后加档只加行。**别抄它那批"每周登门/预约收购"效果的实现方式**——它们分散在 `UpperLevelPatches`/`LowerLevelPatches`/`V2Campaign`/`FactionVisitors`/`AugCoreDraft`/`SupplyPage` 等多个文件里，我们该先有《群友》那套"访客调度器"，再往上挂效果。

2. **存档事务三段式 + 六态读档 + 增量对齐公式**——我们现在的 `ES3Type.WriteProperties` 浅克隆写盘剥离（电玩 `ArcMarket`）与 `DspVisitorPhones` 的 `SaveGame` 剥离，只解决"不把自定义字段写进原版档"，没解决"写一半崩了怎么办""玩家禁模组期间交易过怎么办"。直接借三件：`SaveGame/WriteSlotFile` 的 Prefix 提交 + Finalizer 回滚 `_old`（E4）；`Missing/Current/Changed/Corrupt/Future/Foreign` 六态；以及 `Exact + (原生当前值 − Projection)` 的**增量对齐**（`RepProfile.cs:834`）——最后这条正是多模组共存时不被别人覆盖的关键。

3. **自定义物品的"深度剥离"形态**——电玩已有 `ES3Type.WriteProperties`；这份档案给的是更彻底的一层：按 **11 个原版 inventory 字段**（`main/back/hidden/showcase/sold/hireling/vending/fountain/trash/doc/cassette`）做**节点级剥离 + 父子边表重建 + feature 列表分桶**（`OfficialItemArchive.cs:157-219,224-228`）。将来《群友》要把名片/货箱放进原版容器、或电玩要把光盘放进原版展柜存档时，这是现成的做法。

4. **一个人口装多个子系统**——《群友》已有 7721/7734 电话预约；`PhoneFactionButton`（往 `PhoneUIManager` 插一个按钮，`PhoneFactionButton.cs:6-14`）+ `JournalAddons.Page`（F6 可插页，含 `Bypass` 免锁位）正是"多角色共用一个入口"的骨架，比给每个角色单开一个窗口省一半 UI 代码。

5. **反刷规则直接搬**——电玩《逐件实交》/《付费情报》一旦接声望或积分，用 `RateAt`（递减）+ `CapAt`（分档硬上限）+ `ApplySoftCap`（用量 60% 以内全额、超出打五折）+ 只对"交易产生的"计数 + 用户可配上限（默认 6、上限 200）。这套比"每日最多 N 点"耐刷得多，且改动量只有一个 `LimitGain`。

6. **Transpiler 的正确姿势**（若真到了非写不可的地步）——抄 E6：**改一处 opcode + 找不到就 `LogError` 并原样返回 + Prefix/Postfix 存还原做等价保底**。我们目前 0 Transpiler 的纪律不必破，但"有降级路径"这条要写进规范。

7. **`RuntimeBlocked` 全局闸门**——`Busy || SafeMode` 一个属性、22 处第一行引用，换来"存档中不抖、坏档时只读"。我们两个模组的补丁都该有这第一行。

---

## 附录 A 证伪边界 / 推测项

- **未实测**：帧耗时、`GetNegociatedValue` 三个补丁的实际叠加顺序（无 `HarmonyPriority`，静态读不出运行时顺序）、工坊包内资源（反编译只导出 `.cs`，但目录里有 56 张 `FRE.Icons.*.png`，图标是随包的）。
- **（推测）`MaxProjection = 100` 的动机**：原生 `amount` 本就夹 ±200，作者却只投影到 ±100；代码无注释，推测是"让原生 UI 与原版判定只看到前 100，真值只留在模组侧"。
- **（推测）`Buffs.Sum` 无总上限**是有意设计（超模数值由数据把关），而非遗漏——`CoreSellPatch` 连 `num` 的合法性都没判。
- **（推测）`IsExternalInit` 垫片**说明原版源码用过 `init`/`record`，ILSpy 回写成 `set` 后已不可见。
- **不可核**：`ReputationCheckPatch` 替换 `CheckReputation` 是否丢了原生行为——已核到原生体只有两句，**当前等价**；未来版本无法判断。
