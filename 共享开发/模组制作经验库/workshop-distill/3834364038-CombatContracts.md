# 工坊 3834364038 架构蒸馏档案 —— `combat_contracts`《深空当铺：战斗委托 / Combat Contracts》v0.4.18

> **工坊 id**：`3834364038`｜作者「是大帅鸟」｜`ModVersion 0.4.18`｜`GameVersions 0.46D / 1.0.0.14 / 1.0.0.15 / 1.0.0.16`｜`<Capabilities />` 为空、`<Prerequisites />` 为空（`manifest.xml:3-15`）｜装配单个 `CombatContracts.dll`（592,384 B）。5,582 订阅。
> **反编译目录**：`缓存文件夹\decomp_ws\3834364038`（ILSpy 产物：**188 个 `.cs` / 32,727 行**，3 个 namespace + `Properties`）。包体 21.6 MB，构成见 §2.4。
> **为什么值得读**：这是全库**唯一一份把"真战斗引擎"塞进当铺日常流程**的模组，而不是"委托+掷骰"。它同时是三件事的现成答案：① 一个 3,485 行、**零 Unity 依赖**的纯规则战斗内核（可以整块搬）；② 官方 `AddAfterhourLocation` ②′ 档的**完整落地样本**（含"把自制地点挤到夜出地图第一页"这个刚需）；③ 把原版"声望极低直接结局"的 5 个处决 perk **换成可打的战斗**——这是接原版流程最值钱的一招。

> **方法诚实声明**：先做分布表（§2）再定点精读。全库统计口径：`[HarmonyPatch]` **72 处**、`ModHook` 订阅 **14 个**、`ModHelper` 调用 **23 个 API**、**Transpiler 0 个**、`Prefix` 51 / `Postfix` 41、`HarmonyPriority` 15 处、`HarmonyAfter/Before` **0 处**、反射 **9 文件**。精读 `ModEntry` / `CampaignEngine` / `CampaignService` / `BattleRuntime` / `BattleEngine` / `NativeItems`（头部 + `Apply`）/ `EnemyFactory` / `CombatInputGuard` / `CommissionBoardIntegration` / `DefenseDirector` / `NativeReprisal*` / `CombatAfterhourLocation` / `TravelInventoryPersistence`（头部）/ `Progression` / `CombatText` 等 20 余个文件的关键段落，**未通读 32,727 行**。行数一律按 `wc -l` 口径。

---

## 1. 一句话结论

**"战斗"是真战斗：完整回合制，敌我数值俱在，不是"接单→掷骰→结算"。** 判据全部落在源码上，无一条靠宣传词：

- **有回合状态机**：`BattlePhase { PlayerTurn, Intermission, Victory, Defeat, Escaped }`（`Core/BattlePhase.cs:3-10`），`BattleState.Turns / EnemyIndex / Revision` 逐回合推进。
- **有命中率算式**：枪械 `65 + 0.18×Accuracy + 0.08×Ergonomics − 护甲瞄准惩罚`，再 `+ AimBonus − NativeAimPenalty − target.Dodge + 状态修正`，最后夹在 `[25, 97]`（`Core/BattleEngine.cs:565-670`）。
- **有伤害区间与暴击**：`num2 = 0.18 − 0.001×Control`（枪）/ `0.1`（近战），预览给 `MinimumDamage / MaximumDamage` 两端；暴击率 `min(12, 5 + 0.05×Accuracy)`（同上）。
- **有穿甲、护甲耐久与资源约束**：`剩余伤害 = 1 − armorReduction×(1−penetration)`，护甲每次挨打掉 1~2 点耐久、归零即失效（`Core/BattleEngine.cs:821-864`、`Core/CombatMath.cs:46-53`）；口径必须匹配、弹药有剩余次数、电击武器要电池能量、武器有耐久磨损（`CombatMath.GunWear`）。
- **有 15 种状态**：出血/中毒/幻觉/虚弱/疼痛/眩晕/清醒/烟雾/污染/戒断/综合征/治疗/瞄准/疲劳/阿片屏障（`Core/StatusKind.cs:3-19`）。
- **有敌人 AI**：眩晕先嗅盐→血量 ≤35% 用医疗→按条件丢战术品（梦魇尘/闪光/烟雾）→攒重击→选武器开火（`Core/BattleEngine.cs:1123-1220`）。
- **有 20 级成长与伤害乘区**：`1 + 0.02×(level−1)`、`maxHP = 100 + 10×(level−1)`（`Core/Progression.cs:70-111`）。

**"委托"只是这个引擎的入口外壳与结算账本**，不含任何战斗判定。21.6 MB 的体积也印证了这一点：9 张 960×540 战斗背景（8.2 MB）+ 238 个 WAV（11 MB）是真的演出资源，不是虚胖。

**按新玩法五档阶梯：它落在 ②′ 档，而且把 ② 和 ②′ 叠着用**——夜出地点走官方 `ModHelper.AddAfterhourLocation` 注册真子类（`Native/CombatAfterhourLocation.cs:7-85`），战斗本身是 `CustomUIManager` 的 `overlay` 全屏自绘舞台（`Native/CombatWindows.cs:104-173`），**没有新建 Unity 场景、没有反射伪造 `Current`、没有 `AfterhourLocation.Register`**。另有一条 ② 档支线：义体维修师「铆钉」当**原版顾客**到店（`Native/RivetMerchant.cs:13-30` 挂 `PlayerStore.OnShutterOpened`）。

---

## 2. 规模与结构

### 2.1 目录 → 行数（3 namespace + 2 个反编译残留文件）

| 目录（namespace） | 文件 | 行数 | 占比 | 职责 |
| --- | ---: | ---: | ---: | --- |
| `CombatContracts.Native\` | 131 | 27,099 | **82.8%** | 原版互操作：物品图搬运、战斗运行时、UI、补丁壳、存档事务 |
| `CombatContracts.Core\` | 32 | 3,485 | 10.6% | **纯规则战斗内核**（零 `using UnityEngine`） |
| `CombatContracts.Campaign\` | 23 | 2,131 | 6.5% | 委托/声望/经济/刷新内核（纯数据与规则） |
| `Properties\` + `-PrivateImplementationDetails-.cs` | 2 | 12 | 0.0% | ILSpy 标准残留 |

**分层是对的：`Core` 与 `Campaign` 合计 17.1% 且完全不依赖原版类型**，靠 `ICampaignWorld`（16 行接口，`Campaign/ICampaignWorld.cs`）把"今天几号 / 声望多少 / 声望加减"三件事回抛给 `Native/NativeCampaignWorld.cs`。这是全库少见的、能离线单测的形状。

### 2.2 按角色（桶间互斥，按文件名 + 归属目录判定）

| 角色 | 文件 | 行数 | 占比 | 代表 |
| --- | ---: | ---: | ---: | --- |
| D 战斗运行时/动作/表现 | 35 | 10,889 | **33.3%** | `BattleRuntime` 1,765、`CombatSceneView` 1,586、`CombatWindows` 1,302、`CombatActions` 623、`CombatPlayback` 575、`CombatItemEffects` 569、`CombatAudio` 540 |
| C 原生物品图/库存搬运 | 15 | 6,775 | **20.7%** | `NativeItems` 2,283、`TravelInventoryPersistence` 1,509、`PendingStoreDelivery` 803、`ReturnRecoveryArchive` 633、`OptionalAmmoUseCount` 538 |
| F 委托板/UI/本地化/访客 | 24 | 5,153 | 15.7% | `AugmentationReputationIntegration` 1,751、`CustomItemLocalization` 515、`CommissionBoardIntegration` 434、`RivetMerchant` 410 |
| A 战斗内核（`Core\`） | 32 | 3,485 | 10.6% | `BattleEngine` 1,496、`CombatBalance` 201 |
| H 其余原版适配 | 13 | 2,997 | 9.2% | `CampaignService` 1,474、`ModEntry` 426、`NativeCampaignWorld` 139 |
| B 委托内核（`Campaign\`） | 23 | 2,131 | 6.5% | `CampaignEngine` 1,374 |
| E 存档/事务/回程 | 13 | 760 | 2.3% | `CombatSavePersistence` 268、`CampaignSaveTransaction` 157 |
| G 原版守卫补丁壳 ≤40 行 | 31 | 525 | 1.6% | 31 个 12~17 行的 `Prefix` 闸门（`CombatBuyGuard`/`CombatSellGuard`/…） |

**它不属于你给的那六种形态中的任何一种。** 对照：`PCExpansion` 12.3 万里真逻辑 6% —— 这里约 **54%**（见 2.3）；`GoFishing` 单文件 24.6% —— 这里最大文件 `NativeItems.cs` 只占 **7.0%**（188 文件、**中位数 29 行**、73% 的文件 ≤100 行）；`Perks` 41% 括号行 —— 这里 32.4%；`AIEmporium` 18.5% 借来的库 —— 这里**零第三方库**（`Newtonsoft.Json` 由游戏本体提供，DLL 只 592 KB 可证）；`深空诊所` 单文件 54.6% —— 这里没有 >2,300 行的文件；`罗德岛` 35.8% 自检 —— 这里的"自检"只有 `RepairState` + `ReconcileRewardReceipts` 这类**幂等修复**（约 400 行，1.2%），不是验收台。

**新形态：自带引擎的"流程寄生包"。** 真实形状是「一个 3,485 行的纯规则内核 + 6,775 行别人看不见的原版物品图苦力 + 22 行薄接口」，**代码量的 55% 花在"让一个自研战斗能安全地借走原版库存并还回来"**。这是新玩法类模组最诚实的成本结构：玩法的算术很便宜，搬运很贵。

### 2.3 噪声与"真逻辑"占比

| 口径 | 行数 | 占比 |
| --- | ---: | ---: |
| 纯括号行 `{` / `}` | 10,597 | 32.4% |
| `//IL_xxxx` 反编译噪声 | 1,154 | 3.5% |
| 空行 | 2,571 | 7.9% |
| `using` / 特性行 `[...]` | 636 | 1.9% |
| **有效逻辑 + 声明** | **≈17,769** | **54.3%** |

再扣掉 2.1% 的原生 UGUI 拼装行（`new GameObject` / `AddComponent` / `RectTransform` 共 675 行）——因为 UI 全靠游戏的 `CustomUIBuilder` / `PixelWindow` / `PaperUI`，几乎没有手搓 `RectTransform`。

### 2.4 21.6 MB 的构成（1 个 DLL，零额外程序集）

| 项 | 量 | 体积 | 说明 |
| --- | ---: | ---: | --- |
| `Sounds\` | **238 个 WAV** | 约 11 MB | 战斗音效 + UI 音；`CombatAudio` 540 行做映射（`CombatAudio.cs:379-455` 按 `TurnBeatKind` 选 `sfx_defeat` 等） |
| `Textures\` 战斗背景 | **9 张 PNG** | 8.2 MB | 均为 960×540，单张 0.88~1.14 MB（≈2.2 B/px，PNG 压缩很差，换 JPG 能省一半）；键名见 `CombatSceneContent.cs:14`（含 `battle_pawnshop_defense`） |
| `Localization\` | 12 个 CSV × **1,382 键** | 1.4 MB | 11 语言 + 1 冗余 `ptbr.csv`；键是 `cc_<sha1 前 20 位>` |
| `Content\` | `catalog.json` 415 KB + i18n 5 个 JSON | 644 KB | 105 单 + 5 场自卫 + **144 个敌人定义** |
| `CombatContracts.dll` | 1 | 592 KB | **不含美术/音频**，全是 IL |
| `handbook*.md` | 12 份 | 约 180 KB | 游戏内说明书正文（`CombatHandbook.cs:70-74` 用 `ReadModFileText` 读，`PaperUI` 渲染） |

**PNG 只 16 张、音频 238 个**——没有任何角色立绘。敌人头像全部复用原版立绘池（`NativePortraits.cs:11-56` 从 `SpriteDict.GetPool("maleSec"/"femaleRev"/"augLLMale"…)` 里随机取），这是它体积这么"便宜"的直接原因。

### 2.5 冻结副本 / 借来代码

- **无冻结副本、无生成代码**：`Properties/AssemblyInfo.cs` 12 行 + `-PrivateImplementationDetails-.cs` 0 行，共 12 行残留。
- **零第三方库**：`dll` 只有 1 个；`Newtonsoft.Json` 由游戏本体提供（`using Newtonsoft.Json` 全库出现，但未随包分发）。
- **"借来的"只有一样：原版的物品与 UI**。`NativeItems` 2,283 行不是自研库，而是"把 `GameItem` 的标签/耐久/口径/化学浓度翻译成 `BattleItem`"的适配层——**它借的是游戏的数据，不是别人的代码**。

---

## 3. 它怎么做这件事

### 3.1 接在原版哪一层（点名到类与方法）

| 环节 | 原版挂点 | 模组落点 |
| --- | --- | --- |
| 解锁 | `ModHelper.AddNetworkUpgrade(…, 250, "Service", 前置 "MARKETPLACE_REQUESTS")` | `ModEntry.cs:RegisterStore` |
| 委托板入口 | `PCUIManager.OnChange` **Postfix**（人脉面板）+ 自建快捷键 `combat_contracts.board` | `CommissionBoardIntegration.cs:17,168-200` |
| 每日刷新 / 领奖 | `ModHook.OnWakingUpLate` + `OnShutterOpenedLate` + `OnShutterClosedLate` + `OnGoingSleepEarly` + `OnReturningStoreLate` | `CampaignService.cs:175-186` |
| **出发（夜出）** | `ModHelper.AddAfterhourLocation(CombatAfterhourLocation.Instance)` | `ModEntry.cs:RegisterLocation`；`CombatAfterhourLocation.cs:7-85` |
| 夜出地图第一页 | `AfterhourLocation.GetDiscovered` **Postfix**（静态、返回 `List<AfterhourLocation>`），找到自己后 `RemoveAt` + `Insert(4, …)` | `CombatDestinationDiscovery.cs:19-40` |
| 战斗舞台 | `CustomUIManager.Instance.GetLayerRoot("overlay")` + `CustomUIBuilder` | `CombatWindows.cs:104-173` |
| **店内自卫** | `PlayerStore.OnLateShutterOpen` **Postfix**（拉闸过渡结束） | `NativeReprisalPatch.cs:6-43` |
| 自卫前静音原版处决 | `StoreReputation.IsPerkUnlocked` **Prefix**：过渡期内把 `LLLYNCH`/`SEC_IMMEDIATE_ARREST`/`UL_HITMAN`/`REV_HIT`/`CARTEL_RETIRED` 强制 `false` | `NativeReprisalConditionPatch.cs:5-64` |
| 战斗期禁用原版交互 | 一张总闸 `CombatInputGuard.Blocking`（`CombatInputGuard.cs:32-46`）+ 12 个只读它的 Prefix（另 3 个按 `BattleRuntime.Active` 单独判） | `CombatBuyGuard` / `CombatSellGuard` / `CombatTrashGuard` / `CombatShootingGuard` / `CombatBellGuard` / `CombatDialogGuard` / `CombatPhoneGuard` / `CombatNextClientGuard` / `CombatReturnGuard` / `CombatSleepGuard` / `CombatNativeShoot` / `CombatNativeStoreHover` |
| 回店 | `ModHelper.ReturnFromAfterhour(false)` + `ModHook.OnReturningStoreLate` | `BattleRuntime.cs:1079-1124` |

**结论：营业日（解锁/委托板/自卫）+ 夜出（出发/战斗）+ 过夜（刷新/领奖）+ 自绘 overlay 舞台**。它不碰 `StoreStation.HandleStoreRequests`，也**完全不碰 `StoreRequest` 的数据**——只在 `PCUIManager` 里借 `StoreRequestElement` 的 **prefab** 渲染自己的卡（见 §4.5）。

### 3.2 从接一单到交单（完整链路）

1. **解锁**：`AddNetworkUpgrade(250₵, Service, 前置 MARKETPLACE_REQUESTS)` → 回调解 `CampaignService.Unlock()` → `State.unlocked=true` + 发一本战斗手册（`CampaignEngine.cs:218-236`）。
2. **刷板**：打开人脉面板 → `OnChange` Postfix → `CampaignService.RefreshBoard()` → `CampaignEngine.RefreshBoardWithChanges()`（`CampaignEngine.cs:857-997`）。同一 `world.Day` 内只刷一次（`lastRefreshDay` + `refreshEligibility` 双戳，`CampaignEngine.cs:999-1006`）。
3. **接单**：`CanAccept` 七道门（结算冲突 / 已解锁 / 有定义 / 有战斗内容 / 阵营存在 / **声望 ≥ tier** / **同时只能接一份** / 未过期），通过后写 `acceptedDefinition`（整份定义快照）+ `acceptedExpiresDay = day+7` + **当场掷** `baseLootAwarded` 与 `aiRoll`（`CampaignEngine.cs:303-334`）。掷骰在接单时锁定，不是交单时——可对账。
4. **整理装备 → 出发**：`CombatWindows.Supplies()` 选武器/护甲/弹药/电池/**随身背包**（`ExpeditionBackpack`），真库存此时被快照（`TravelInventoryPersistence.Capture`，`:641-671`）；夜出地图第一页点「执行委托」→ `CombatAfterhourLocation.Arrive()` → `BattleRuntime.Arrive()`（`BattleRuntime.cs:497-560`）→ `MarkDeparted` 写 `lastDepartureDay = today`（**每晚只能出发一次**）。
5. **建局**：`BuildBattle()` 把**真实库存**读成 `BattleItem` 列表，敌人由 `EnemyFactory.Create` 从 JSON 定义 + 原生 `GameItem` 公式合成；种子 `day×104729 + id×7919 + eventId.GetHashCode()`（`BattleRuntime.cs:651-694`）。
6. **打**：`CombatActions` 把每一次点击翻成 `BattleEngine` 的一个动作（攻击 / 用医疗 / 防御 / 待机 / 换甲 / 换配件 / 逃跑 / 用战术品 / 引爆 C4 / 涂毒），结果封成 `TurnTrace`，由 `CombatPlayback` 逐拍演出，`NativeItems.Apply(before, after)` **把消耗写回真物品**。
7. **逐个结算**：每杀一个敌人 → `RecordEnemyVictory`：当场对 `target` 阵营扣 `rep_loss`（`internal_cleanup` 类任务豁免），把**实际扣了多少**记进 `enemyActualLosses`（`CampaignEngine.cs:392-431`）。
8. **全灭 → 交单**：`CompleteBattle` 要求**每个敌人都已记录**（幂等：已记录的不会重复扣），然后 `PrepareRewards` 生成收据（`CampaignEngine.cs:432-461`、`1109-1205`）。此时**不直接发钱**，只落 `rewards` 账本。
9. **回店领奖**：`ClaimRewards` 三段幂等提交——`cashClaimed` → `ModCash`，`reputationClaimed` → `ApplyNominalReputation`，`augmentationResolved` → 三选一配件；物资走 `PendingStoreDelivery`（待领库），最后才 `status=Claimed`（`CampaignService.cs:808-950`）。**回归当铺**才能领（`AtStore()` 检查 `playerLocation==0 && !isOutside`）。

### 3.3 委托的数据结构

**一张表 + 每单一个类族**，不是一单一个类：

- `Content/catalog.json`（415 KB）= `CatalogData { List<QuestDefinition> quests; List<DefenseDefinition> defenses; }`（`Campaign/CatalogData.cs:6-12`）。
- **105 单 = 7 阵营 ×（7 档 × 2 单 + 1 终极）**，另有 5 场自卫。层级为 `20/50/70/100/120/150/170` 七档 + `200` 终极档（`CampaignEngine.cs:9`）。
- `QuestDefinition`（85 行，`Campaign/QuestDefinition.cs`）：`id/title/requester/publisher/target/location/reason/task_type`、**难度** `tier`、`enemy_count`、**报酬** `cash/xp/completion_xp/loot/loot_at_80/loot_chance/augmentation_reward/ai_reward`、**风险** `rep_gain/rep_loss_total/rep_losses[]/enemies[].rep_loss`、`ultimate/dark/internal_cleanup` 三个开关。
- `EnemyDefinition`（109 行）：`weapon/ammo/armor/backup/loadout_id/behavior/special/energy_item`、`base_hp/dodge/level/hp`、`penetration/crit_chance/hit/armor_reduction`、`damage=[min,max]`、`stats{firepower,reliability,recoil,ergonomics,accuracy}`、`salvage{policy,rates{weapon,resource,armor,medical}}`。
- `QuestInstance`（72 行 / **32 个字段，其中 11 个是收据或修复标志**）：`enemyRecorded[]`、`enemyActualLosses[]`、`baseLootAwarded`、`rewardPrepared`、`completionXpRecorded`、`cashClaimed`、`reputationClaimed`、`augmentationResolved`、`instanceReceiptConflict`、`rewardLedgerMissing`、`completionRepairFailed`。**幂等是靠这些字段撑起来的，不是靠事务。**
- `CampaignState`（60 行）：`unlocked / boardSlots=24 / lastRefreshDay / refreshEligibility / lastDepartureDay / randomState / nextInstanceId / instances[] / gifts[] / augVisits[] / ultimateQualified[] / ultimateCompleted[]`。
- **时限是"展示期"+"执行期"两段**：刷出来 `+3` 天不看就 `Expired`（`displayExpiresDay`），接了 `+7` 天内必须出发（`acceptedExpiresDay`）。终极单两段都无期限（`int.MaxValue` / `-1`）。

### 3.4 生成与刷新（与 `StoreRequest` 的关系：**没有关系**）

- **不订阅、不修改原版委托**。原版的 `StoreRequest` / `StoreStation.HandleStoreRequests` 全库**零命中**；`PCUIManager` 的 `storeRequestElements` 只被**读**来算"原版占了几格"，并被要求**至少保留 8 格**（`CommissionBoardIntegration.cs:113` `NativeReservedSlots = 8`）。
- **刷新时机**：只在"板被打开"和"跨天钩子"两处惰性触发，靠 `lastRefreshDay == world.Day && refreshEligibility == "阵营|最高档…"` 双戳短路（`CampaignEngine.cs:916-930`）。**没有 `Update` 轮询刷板**。
- **候选池**：`阵营存在 && 声望 ≥ q.tier && 同模板没有在板/在接/在执行/今天已刷过`（`CampaignEngine.cs:949`）。
- **概率**：按"玩家最高可接档 − 该单档位"的**档位距离**取 `1.0 / 0.7 / 0.45 / 0.3 / 0.2 / 0.12 / 0.08`（`CampaignEngine.cs:11` + `1007-1013`）——离你水平越远越难刷出来。
- **保底**：`boardSlots=24` 空位里先**保底塞一张"低于最高档"的单**（`CampaignEngine.cs:965-973`），再按 `factionCursor` **轮转 7 个阵营**填满（`974-995`）——保证不会连续刷同一家。
- **终极单**：某阵营声望 ≥200 → 记入 `ultimateQualified` → 下一刷直接生成该阵营的 200 档唯一单；`Failed` 后 `retryAfterDay = day+3`，`Completed` 后进 `ultimateCompleted` 永不再出（`CampaignEngine.cs:898-908`、`813-828`）。
- **随机源是确定性的**：`randomState` 用 xorshift32（`CampaignEngine.cs:208-218`），**状态存进存档**，读档后重放同一天的刷板结果一致。历史只留 7 天（`PruneHistory`，`1014-1069`，且有 6 道"不许删"的保护：有结算冲突 / 有未交付奖励 / 未结清三段收据的一律不剪）。

### 3.5 失败与惩罚（这是本模组最狠的一处设计）

| 情形 | 结果 | 出处 |
| --- | --- | --- |
| 刷出来 3 天没接 | `Expired`，静默消失 | `CampaignEngine.cs:883-888` |
| 接了 7 天没出发 | `Expired` | `889-894` |
| 主动放弃（未出发） | `Abandoned`；终极单降级为 `Failed` + 冷却 3 天 | `829-851` |
| **逃跑** | 状态 `Escaped` → `FailBattle`；**必须丢一件 `CanEscapeLose` 的随身物品**（随机一件，若是武器/护甲/配件会一并卸下） | `BattleEngine.cs:348-397`；`BattleRuntime.cs:958-965` |
| **打输（HP 归零）** | `BattlePhase.Defeat` → **`PlayerStore.ExecuteGameOver("execution")`** —— 直接游戏结束，**没有复活、没有读档提示** | `BattleRuntime.cs:1126-1158` |
| **店内自卫失败** | 也走 `ExecuteGameOver`，但按阵营给不同结局文本 `mob` / `hitman` / `cartel_retired` / `bombed` / `sec` | 同上 |
| **受伤（活着结束才结算）** | 战斗期间**最低血量比例** ≤0.25 → `HealthData.ReceiveMajorWound()`；≤0.6 → `ReceiveMinorWound()`；由原版健康系统承担后果 | `BattleRuntime.cs:1160-1175` |
| **声望损失（不是失败时扣）** | **每杀一个敌人，当场**对 `questDefinition.target` 阵营扣 `enemies[i].rep_loss`；完成委托给 `publisher` 加 `rep_gain`。**二者可能互抵** | `CampaignEngine.cs:392-431` |
| **报酬随"屠杀量"上涨** | `PublisherReward = max(rep_gain, actualTargetLoss + 1)` —— 你多杀了目标阵营的人，发布方多给声望 | `CampaignEngine.cs:165-178` |
| 物资损耗 | 弹药按次数、医疗品整件消失、武器/护甲耐久磨损，全部 `NativeItems.Apply` 写回真物品 | `NativeItems.cs:919-1188` |

**设计要点**：①"失败"分三层——逾期只是错过、逃跑是丢东西+任务失败、死亡是终局；②死亡惩罚**只在战斗里**发生，且**只能在夜出前用"整理装备"来预防**；③自卫战**不能逃跑**（`BattleEngine.cs:355-357` 明确拒绝），因为触发条件是声望 ≤ −100（`DefenseDirector.cs:20-35`）——**声望烂到底就是必死的仗**。

### 3.6 Patch 靶点表（21 行覆盖 39 处；全库共 72 处，**Transpiler 0 个**）

| 原版方法 | 类型 | 目的 | 风险 / 热点 |
| --- | --- | --- | --- |
| `PCUIManager.OnChange` | Postfix | 在人脉面板克隆 `StoreRequestElement` 渲染委托卡，保住原版 8 格 | **反射读私有 `storeRequestElements`**；面板每次刷新都跑，做了 `rendering` 重入闸和模型比对（`renderedModels.Same`）才重建 |
| `AfterhourLocation.GetDiscovered` | Prefix(`prio 800`) + Postfix(`prio 0`) | 保证自制地点始终在注册表里；把它的 `List` 索引挪到 4（第一页末位） | 静态方法、全局唯一返回列；若别的模组也 `Insert` 有互踩风险 |
| `PlayerStore.OnLateShutterOpen` | Prefix + Postfix + **Finalizer** | 拉闸过渡结束后收集"声望 ≤−100 的阵营"并开自卫战 | 用 `Finalizer` 复位 `FilterNative` 标志，异常安全 ✅ |
| `StoreReputation.IsPerkUnlocked` | Prefix | 过渡期内屏蔽 5 个原版处决 perk | **直接改变原版结局走向**；靠 `FilterNative` 窗口期收窄 |
| `PlayerStore.WriteSlotFile` | Prefix(`prio 0`) | 决定"检查点能不能写盘" | **碰存档管线**；`SuppressHistoryRotation` 会在战斗中压制历史轮转（`CombatSavePersistence.cs:77-107`） |
| `SaveFiles.RotateIntoHistory` | Prefix + Postfix | 战斗中不轮转历史，回到入口时只转一次 | **碰存档管线**；抑制失败会靠 `SuppressedRotations` 计数诊断 |
| `PlayerStore.SaveGame` | Prefix(`prio 0`) | 奖励事务期间延迟原生存盘 | 延迟存盘的失败必须靠 `Checkpoint()` 重试补上 |
| `StoreUIManager.CheckRaycast` | Prefix | 战斗期禁止悬停原版 UI（强行 `hoveringStoreUI=true`） | **每帧**；只读一个布尔总闸 |
| `CustomEventHandler.Update` | Prefix | 战斗期屏蔽双击判定 | **每帧**；`CustomEventHandler.ClearDoubleClickTarget()` 兜底 |
| `KeyBindHelper.GetKey` / `GetKeyDown` | Prefix ×2 | 战斗期吞掉 14 个原版按键（`rotate_cw`/`trash_item`/`call_next_customer`…）与 `toolbox_slot_*` | **每次按键查询都跑**；`RestrictedBinding` 是 HashSet 查询后早退 |
| `StoreUIManager.CanShoot` / `EnterShootingMode` / `InputActionHandler.UpdateUpdate` | Prefix ×3 | 战斗期禁用原版射击模式与物品操作分发 | 每帧 + 每次物品输入；按 `Blocking` 早退 |
| `GameItem.ActivateSlotItem` / `CanActivateSlotItem` / `MayActivateSlotItem` | Prefix ×4 | 观察医疗品使用、让战斗医疗品在原生槽位可点、阻止"回程未完成"时用物品 | 玩家高频入口；会影响原版所有可用性判断 |
| `GameItem.MayStack` / `GetSplitItem` / `SlotMarker.StackItemUnchecked` / `UseCountHelper.*` | Prefix ×6 | 让"部分弹药/涂毒弹药"可堆叠可拆分，弹药按"剩余次数"而非整盒消耗 | 直接改原版堆叠与使用计数语义 |
| `StoreReputation.CheckReputation` | Prefix | 把 6 个原生阵营声望上限抬到 1000 | 反射读写私有 `amount` 字段 |
| `StoreReputation.GetReputationStatus` / `ReputationElement.Init` / `UpdateBar` | Postfix ×4 | 让声望面板显示 0~1000 区间与扩展文案 | UI 刷新热点 |
| `ReputationUIManager.OpenUI` | Prefix + Postfix | **凭空造出第 7 阵营「改造体」一行**（无原生 `StoreReputation`） | 1,751 行投影逻辑；`LayoutDefaults` 缓存原生行布局并在 `Clear()` 时还原 |
| `PlayerStore.GetNextClient` | Prefix + Postfix ×2 | 顾客到店时给「铆钉」记账；战斗期禁止换客 | 与 `CampaignVisitPatch` / `CombatNextClientGuard` **同方法两补丁**，靠 `priority` 与 `__runOriginal` 分工 |
| `PlayerStore.ExecuteGameOver` | Prefix | `ending=="aug"` 时先存盘再拦下（同化结局不吞战斗进度） | 直接改结局流程 |
| `GameItemElement.ResolveSpriteByName` | Postfix(`prio 0`) | 自定义物品图标覆盖 | **`SpriteDict.GetSprite` 的替代路径**，不碰 `SpriteDict` 内部 |
| `MachinePrinter.CaptureIngotQuality` / `HandlePrintedItemQuality` | Prefix/Postfix | 板甲打印按锭纯度出质量 | 机器系统；`GameItem.IsTag("INGOT_PURITY_TAG")` |
| `SaveManager.DecodeNodes(List<SaveItemNode>)` | Postfix ×2 | 读档后修复随身背包的格位与武器姿态 | **读档路径**，异常会毁档，两个补丁都只做"补字段" |

**已知热点的命中情况**：`StoreClientListDict.CreateStoreClient` **0**、`SpriteDict.GetSprite` **0 处打补丁**（只读调用 2 处：`CombatSceneView.cs:473,1105`）、`StoreUIManager.Update` **0**、`PlayerStore.EndNight` **0**、`GameItem.IsTag` **0 处打补丁**（只读调用大量）、`GetNegociatedValue` **0**、`ShowcaseHelper.*` **0**。**七个已知热点，一个都没碰。** 代价是它转而压在了另一批热点上：三个每帧回调（`CustomEventHandler.Update` / `InputActionHandler.UpdateUpdate` / `StoreUIManager.CheckRaycast`）+ 两个"每次按键查询"（`KeyBindHelper`）+ 存档三连（`SaveGame` / `WriteSlotFile` / `RotateIntoHistory`）。

---

## 4. 关键代码摘录

### 4.1 委托生成：保底一张 + 阵营轮转填满（`Campaign/CampaignEngine.cs:944-996`）

```csharp
int num2 = State.boardSlots - num;                     // num = 板上现有非终极单数
if (num2 <= 0) { return UpdateRefreshStamp(text) | flag; }
List<QuestDefinition> source = Catalog.quests.Where(q => !q.ultimate
    && world.HasFaction(q.publisher)
    && world.GetReputation(q.publisher) >= (double)q.tier
    && !State.instances.Any(i => i.templateId == q.id
        && (i.status == QuestStatus.Available || i.status == QuestStatus.Accepted
         || i.status == QuestStatus.Executing || i.createdDay >= world.Day))).ToList();
int randomState = State.randomState;                   // 注意：先存后掷，便于判定"这一步有没有消耗随机"
List<QuestDefinition> list = source.Where(q => NextRandom() < Chance(q)).ToList();
flag |= State.randomState != randomState;
bool flag2 = State.instances.Any(i => { /* 板上已有"低于最高档"的单？ */ });
List<QuestDefinition> list2 = source.Where(q => q.tier < HighestRegularTier(q.publisher)).ToList();
if (!flag2 && list2.Count > 0) {                       // 保底：先塞一张低于水平的
    QuestDefinition pick = WeightedPick(list2);
    CreateInstance(pick); num2--; list.Remove(pick); flag = true;
}
while (num2-- > 0 && list.Count > 0) {                 // 再按 factionCursor 轮转 7 家填满
    QuestDefinition pick = null;
    for (int i = 0; i < Factions.All.Length; i++) {
        int at = (State.factionCursor + i) % Factions.All.Length;
        List<QuestDefinition> ofFaction = list.Where(q => q.publisher == Factions.All[at]).ToList();
        if (ofFaction.Count != 0) {
            pick = WeightedPick(ofFaction);
            State.factionCursor = (at + 1) % Factions.All.Length;
            break;
        }
    }
    if (pick == null) break;
    CreateInstance(pick); list.Remove(pick); flag = true;
}
return UpdateRefreshStamp(text) | flag;
```

`Chance()` 与 `CreateInstance()` 的实质（`CampaignEngine.cs:1007-1013`、`1085-1108`）：`gap = 最高可接档位索引 − 该单档位索引`，查 `RefreshChances[gap]` 得 `1.0/.7/.45/.3/.2/.12/.08`；`CreateInstance` 写 `displayExpiresDay = ultimate ? int.MaxValue : world.Day + 3`。

### 4.2 出战：真库存直接进战斗（`Native/BattleRuntime.cs:651-694`）

```csharp
FighterState player = Copy(runtimeState.player);
player.Inventory = NativeItems.Inventory(defense);      // ← 把真实 GameItem 读成 BattleItem
SyncNativeSymptoms(player);
if (player.Inventory.FirstOrDefault(i => i.Id == player.WeaponId && i.Kind == ItemKind.Weapon) == null)
    player.WeaponId = player.Inventory.FirstOrDefault(i => i.Kind == ItemKind.Weapon)?.Id ?? "";
if (!player.Inventory.Any(i => i.Id == player.ArmorId && i.Kind == ItemKind.Armor))
    player.ArmorId = "";
List<string> acc = player.AccessoryIds.Where(id => player.Inventory.Any(i => i.Id == id && i.Kind == ItemKind.Accessory)).ToList();
BattleEngine.EquipAccessories(player, acc);
runtimeState.battle = BattleEngine.Create(player, EnemyFactory.Create(enemies, eventId), defense,
    (uint)(StoreStation.GetDayCounter() * 104729
         + (runtimeState.nextDefenseId + (defense ? 1 : 0)) * 7919
         + eventId.GetHashCode()),                      // ← 确定性种子：同一天同一单结果可复现
    completionXp, eventId);
runtimeState.battle.Dark = dark;
runtimeState.backgroundKey = (defense ? "battle_pawnshop_defense"
    : CombatSceneContent.ResolveLocation(location, faction));
```

### 4.3 命中与伤害：预览和实打在同一个函数里（`Core/BattleEngine.cs:565-670`，删去分支噪声）

```csharp
else if (battleItem.WeaponType == WeaponType.Gun) {
    if (battleItem2 == null || battleItem2.Kind != ItemKind.Ammunition
        || battleItem2.AmmoRemaining <= 0 || string.IsNullOrEmpty(battleItem.Caliber)
        || battleItem2.Caliber != battleItem.Caliber)
        return new AttackPreview { Reason = "需要匹配口径且有剩余次数的弹药。" };
    num  = 65.0 + 0.18 * Clamp(battleItem.Accuracy, 0, 100)
                + 0.08 * Clamp(battleItem.Ergonomics, 0, 100);
    num -= Find(actor, actor.ArmorId)?.ArmorAimPenalty ?? 0.0;   // 护甲妨碍瞄准
    num2 = 0.18 - 0.001 * Clamp(battleItem.Control, 0, 100);     // 后坐力 → 伤害浮动
    criticalChance = Math.Min(12.0, 5.0 + 0.05 * Clamp(battleItem.Accuracy, 0, 100));
    attackPreview.DurabilityCost = CombatMath.GunWear(battleItem.Reliability);
    if (HasStatus(actor, StatusKind.Smoke)) num -= 30.0 * (flag ? 0.5 : 1.0);
}
num += actor.AimBonus - actor.NativeAimPenalty - target.Dodge + AimStatuses(actor, flag);
if (state.Dark && !actor.Inventory.Exists(i => i.Quantity > 0 && i.FlashlightAvailable)) num -= 10.0;
double dmg = AugmentedWeaponDamage(battleItem, battleItem2, text2)
           * Progression.DamageMultiplier(actor.Level) * actor.DirectDamageMultiplier
           * (HasStatus(actor, StatusKind.Weakness) ? 1.0 - Clamp(Status(actor, StatusKind.Weakness).Potency, 0, 0.9) : 1.0);
double mul = DirectDamageRemaining(state, target, WeaponPenetration(battleItem, battleItem2, text2), out _);
attackPreview.HitChance       = CombatMath.Clamp(num, 25.0, 97.0);
attackPreview.MinimumDamage   = Math.Max(1, Round(CombatBalance.DirectDamageBeforeArmor(actor, dmg * (1 - num2)) * mul));
attackPreview.MaximumDamage   = Math.Max(1, Round(CombatBalance.DirectDamageBeforeArmor(actor, dmg * (1 + num2)) * mul));
```

护甲侧（`Core/BattleEngine.cs:821-864`）：

```csharp
double DirectDamageRemaining(BattleState s, FighterState t, double pen, out bool usesRegulator) {
    double red  = CombatMath.ArmorReduction(Find(t, t.ArmorId));   // 0.15(衬里)/0.25(废铁)/0.4(金属) × 耐久系数
    double eff  = red * (1.0 - Clamp(pen, 0, 1));                  // 穿甲削护甲
    double keep = 1.0 - Math.Min(0.6, 1.0 - (1.0 - eff) * (t.Defending ? 0.6 : 1.0));  // 防御减 40%
    ...
}
// 护甲挨打会掉耐久（raw≥30 掉 2 点，否则 1 点），耐久归零护甲失效 —— BattleEngine.cs:821-840
```

### 4.4 失败与死亡结算（四处，务必连起来读）

**① 结果分类，只分三支**（`Native/BattleRuntime.cs:932-970`）：

```csharp
private static void ProcessResult() {
    if (State.resultProcessed && CampaignResultMatches()) return;
    BattleState battle = State.battle;
    if (battle.Phase == BattlePhase.Victory) {
        if (battle.IsStoreDefense) {
            if (CampaignService.GetReputation(State.faction) != 0.0)
                CampaignService.SetReputationZero(State.faction);      // 自卫成功 → 该阵营声望归零
        } else {
            CampaignService.CompleteBattle(State.questInstanceId);
            if (!CampaignResultMatches()) throw new InvalidOperationException("委托结算尚未完成，战斗进度已保留。");
        }
        State.resultProcessed = true;
    } else if (battle.Phase == BattlePhase.Escaped || battle.Phase == BattlePhase.Defeat) {
        if (!battle.IsStoreDefense) CampaignService.FailBattle(State.questInstanceId);
        if (!CampaignResultMatches()) throw new InvalidOperationException("委托结算尚未完成，战斗进度已保留。");
        State.resultProcessed = true;
    }
}
```

**② 状态落账，只管记账不管惩罚**（`Campaign/CampaignEngine.cs:813-828`）：

```csharp
public void FailBattle(string instanceId) {
    QuestInstance q = Instance(instanceId);
    if (q != null && (q.status == QuestStatus.Executing || q.status == QuestStatus.Accepted)) {
        q.status = QuestStatus.Failed;
        q.failedDay = q.closedDay = world.Day;
        QuestDefinition def = Definition(q);
        if (def != null && def.ultimate) q.retryAfterDay = world.Day + 3;   // 终极单 3 天后可重试
    }
}
```

**③ 真正的惩罚：HP 归零就是终局；活着结束才吃伤**（`Native/BattleRuntime.cs:1126-1175`）：

```csharp
private static void Die() {
    if (Active && TryReconcileSettlement(Time.realtimeSinceStartup, force: true)) {
        CombatWindows.CloseBattle();
        string ending;
        if (StoreDefense) {
            if (State.faction == "LL") ending = "mob";
            else if (State.faction == "UL") ending = "hitman";
            else if (State.faction == "CARTEL") ending = "cartel_retired";
            else ending = (State.faction == "REV") ? "bombed" : "sec";
        } else ending = "execution";                       // ← 委托里战死 = execution 结局
        PlayerStore.Instance.ExecuteGameOver(ending);
        actionPresentationPending = false;
    }
}

private static void ApplyWound() {                          // 只在 Finish()（胜/逃）里调
    if (!State.woundApplied && Active && State.battle.Player.Hp > 0) {
        double ratio = State.battle.MinimumHpRatio;         // 全程最低血量比例，不是结算时血量
        if (ratio <= 0.25) HealthData.ReceiveMajorWound();
        else if (ratio <= 0.6) HealthData.ReceiveMinorWound();
        State.woundApplied = true;
    }
}
```

**④ 逃跑必须丢东西**（`Core/BattleEngine.cs:348-397`）：

```csharp
public static ActionResult Escape(BattleState state) {
    if (state.IsStoreDefense) return ActionResult.Invalid("处决队已堵住当铺，必须击败全部对手，不能逃跑。");
    List<BattleItem> list = state.Player.Inventory.FindAll(i => i.Quantity > 0 && i.CanEscapeLose);
    if (list.Count > 0) {
        BattleItem lost = list[(int)(CombatMath.Next(state) * (double)list.Count)];
        LoseOne(lost); state.EscapeLossId = lost.Id;
        if (state.Player.ArmorId  == lost.Id) state.Player.ArmorId  = "";
        if (state.Player.WeaponId == lost.Id) state.Player.WeaponId = "";
        Log(state, "你逃回当铺，遗失：" + lost.Name + "×1。");
    }
    state.Phase = BattlePhase.Escaped;
    ...
}
```

### 4.5 夜出地点：②′ 档最短骨架 + "挪到第一页"（`Native/CombatAfterhourLocation.cs:7-85`、`CombatDestinationDiscovery.cs:19-40`）

```csharp
public sealed class CombatAfterhourLocation : AfterhourLocation {
    internal static readonly CombatAfterhourLocation Instance = new CombatAfterhourLocation();
    public override string Id => "combat_contracts.mission";
    public override string BackgroundKey => "combat_contracts:" + SceneKey;   // 走 LoadedMod.Sprites
    public override bool IsDiscovered() => ModEntry.CurrentStoreReady
        && (BattleRuntime.Active ? !BattleRuntime.StoreDefense : CampaignService.Active != null);
    public override bool IsAvailable() => IsDiscovered() && BattleRuntime.CanTravel && !IsClosedByEvent();
    public override void Arrive() => BattleRuntime.Arrive();
    public override void Leave()  => BattleRuntime.Leave();
    public override void BuildPanel(CustomUIBuilder b) => b.AddHeader(CombatText.T(BattleRuntime.AcceptedTitle), null)
        .AddLabel(CombatText.T("整理随身装备，准备执行当前战斗委托。"), null)
        .AddButton(CombatText.T("进入战斗"), CombatWindows.Battle, null);
}

// 把自制地点挤到第一页（0..4 是首页）
[HarmonyPatch(typeof(AfterhourLocation), "GetDiscovered")]
static class CombatDestinationDiscovery {
    [HarmonyPriority(800)] static void Prefix() { /* 确保注册表里还是同一个实例 */ }
    [HarmonyPriority(0)]  static void Postfix(List<AfterhourLocation> __result) {
        int i = __result.IndexOf(CombatAfterhourLocation.Instance);
        if (i > 4) { var item = __result[i]; __result.RemoveAt(i); __result.Insert(4, item); }
    }
}
```

背景图注册走的也是官方路径，没碰 `atlasCache`（`Native/CombatSceneContent.cs:25-49`）：`loaded.Sprites[key] = Sprite.Create(tex, …)`，9 个键一次注入，键名与 `catalog.json` 的 `location` 对应。

---

## 5. 踩坑与红线

1. **`ExecuteGameOver` 是它最大的"红线"，也是它最大的卖点。** 委托里战死直接 `execution` 终局。如果我们抄这套，必须把"终局"降级成我们自己的惩罚（掉货/掉声望/当日停业），否则一次误操作毁掉玩家的 80 小时存档。
2. **它改了存档管线的三个点**：`PlayerStore.SaveGame`（延迟存盘）、`WriteSlotFile`（检查点能不能写）、`SaveFiles.RotateIntoHistory`（抑制历史轮转）。`CombatSavePersistence.cs:77-107` 的 `SuppressHistoryRotation` 在战斗中**默认返回 true**——意味着战斗中存档不产生历史备份。README 那句"停用前请先返店结束战斗……并备份存档"就是这个设计的直接代价。**我们不要学这条**：宁可多存一次盘，不要把历史轮转关掉。
3. **道具只做了"差量守恒"，没做"来源审计"。** `NativeItems.Apply` 会拒绝"数量变多/弹药变多/能量变多"（`资源变化不守恒`），也会拒绝一次提交里同时消耗两件整件物品，但它**不去核对"这件物品是不是玩家应该有的"**，只核 `Owned(val)`。`TravelInventoryPersistence` 1,509 行 + `ReturnRecoveryArchive` 633 行全是在补这个洞——**借走真库存再还回来是这类玩法真正的成本**，不是玩法本身。
4. **`AfterhourLocation.GetDiscovered` 是静态全局单点。** 两个模组都用 `Insert(4, …)` 就会互踩顺序（一个把另一个挤回第二页）。要抄就抄"指数 4"这个常量，但**别用 `Insert` 硬插**，改成 `OrderBy` 一个显式优先级更安全。同理 `PCUIManager.storeRequestElements` 是反射读私有字段，游戏一改字段名就整块面板失效——`CommissionBoardIntegration.cs:206` 用了 `?.` + `?? new List<>()` 兜住，这是必须保留的写法。
5. **`Blocking` 总闸的解耦做得对，值得抄**：`CombatInputGuard.Blocking`（`CombatInputGuard.cs:32-46`）只读 `BattleRuntime.Active` / `CombatCompletionView.OwnsInput` / `ShopHealthView.BlocksItemInput` 三个状态，12 个守卫补丁全是 `if (!Blocking) return true;`。**一个状态位管住所有原版入口**，比在每个补丁里各写一遍判断可靠得多。
6. **每帧补丁要审、但优先级串补丁的方式值得抄**：`CustomEventHandler.Update`、`InputActionHandler.UpdateUpdate`、`StoreUIManager.CheckRaycast` 三个 Prefix 每帧都跑（都在第一行早退，属"能接受"）。与此同时**零 Transpiler、零 `HarmonyAfter/Before`、`HarmonyPriority` 只 15 处**——同方法多补丁（`CampaignVisitPatch` / `CombatNextClientGuard` 同挂 `PlayerStore.GetNextClient`）靠 `__runOriginal` 串联而不是靠优先级排序，语义更清晰。
7. **本地化是自建管线，不是原版 `Localization` 表**：`CombatText` 以**中文源码字符串为键**，用正则模板匹配占位符，再查 `Localization/<lang>.csv`（1,382 键 × 11 语）。好处是代码里直接写中文可读性极高；坏处是**改一个字就丢翻译**（键是中文原文的哈希 `cc_<sha1前20>`）。我们如果要抄，至少把键改成显式 id。

---

## 6. 对我们的可用点（点名到功能）

**① 《深空电玩》→ 街机擂台/机台对战：直接搬 `Core\` 这块 3,485 行。**
`BattleEngine` 不 `using UnityEngine`，只依赖 `BattleState`/`FighterState`/`ActionResult`/`TurnTrace` 四个值对象；**"算"与"演"是分开的**（`BattleEngine` 只产出 `TurnTrace` 的 `TurnBeat` 序列，`CombatPlayback` 575 行负责播）。电玩要做的"擂台赛/机台格斗"，正好是"点击 → 出一串可播放的回合节拍"，这块内核可以整目录搬过去，再写自己的 `Playback`。省下的不是 1,496 行，是**一整套数值平衡**（`CombatBalance.cs` 201 行全是调好的常量）。

**② 《深空电玩》→ "每日挑战"要有确定性随机。**
抄 `CampaignState.randomState` + xorshift32（`CampaignEngine.cs:208-218`）与 `BattleRuntime.cs:671` 的三段种子（`day×104729 + id×7919 + eventId.GetHashCode()`）。**随机状态进存档**，读档后同一场战斗/同一天的刷板结果完全一致——这是排行榜和"每日一关"的前置条件，比 `UnityEngine.Random` 强。

**③ 《深空当铺：可能是群友》→ 委托板直接长在人脉面板上。**
`CommissionBoardIntegration`（434 行）给了完整模板：`PCUIManager.OnChange` Postfix → 反射取 `storeRequestElements` → `Instantiate` 一个 `StoreRequestElement` prefab 当行 → **`SetSiblingIndex` 把原版 8 格排在自己前面**（`:259-272`）→ 缓存 `CardModel` 比对，只有内容变了才重建（`:226-233`）。群友的"群友悬赏/人情委托"可以**不新开 UI**，直接接在同一个面板，玩家零学习成本。另外它还给了一个"面板入口按钮"的做法：克隆 `prefab.GetComponent<StoreRequestElement>().acceptButton` 挂在 `panel.transform` 上（`EnsureEntry`，`:275-330`）。

**④ 《深空当铺：可能是群友》→ 无原生数据也能加"第 7 栏声望"。**
`AugmentationReputationIntegration`（1,751 行）在**没有 `StoreReputation` 对象**的前提下，往 `ReputationUIManager` 里投影出一整行「改造体」：`OpenUI` 的 Prefix 记原始布局（`LayoutDefaults`）→ Postfix 造行/改文案/tooltip → `Clear()` 时逐字段还原（`:197-410`、`:828-980`）。群友要做"群友好感度/道义值"这一栏，这就是现成骨架（可裁到 300 行以内，把 `PolicyTables` 那些多语言策略表换成自己的）。配套的 `StoreReputation.CheckReputation` 上限抬升（0→1000，`NativeReputationCap.cs`）也是必需的。

**⑤ 两款共用 → 中文源码字符串 + 11 语言 + `PaperUI` 说明书。**
`CombatText` 的"中文原文作键 + 正则模板 + CSV 覆写"（`Core/CombatText.cs:14-96`）配合 `Localization/*.csv` 与 `Content/i18n/native-keys.json`，让作者全程写中文也能出 11 语言；`CombatHandbook` 用 `PaperUI.Create("combat_contracts:combat_handbook", "notebook_manual", 628f, 804f, …)` + `ModHelper.ReadModFileText("handbook.<lang>.md")` 把 Markdown 渲染成**原版纸张文档**（`CombatHandbook.cs:30,70-74`）。电玩和群友都缺"游戏内说明书/价目表/委托单"的呈现面，这条管线现成、且不违反我们的文案红线（纸张 + 西式工业语感正是我们要的载体）。

> **一句话回答"战斗是真战斗还是委托+判定"**：**是真战斗**——有完整回合制引擎（命中率/暴击/穿甲/护甲耐久/口径匹配/电量/15 种状态/敌人 AI/20 级成长），"委托"只是它的入口与结算账本，唯一的"掷骰"出现在接单时锁定掉落与 AI 奖励，不在战斗判定里。
