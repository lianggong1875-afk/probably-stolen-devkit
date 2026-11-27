# 工坊 3837619429 架构蒸馏档案 —— `fence_case_expansion`《物资箱与销赃人拓展（Fence and Case Expansion）》v0.1.0

> **工坊 id**：`3837619429`｜作者「Kasliding」｜`ModVersion 0.1.0`｜`GameVersions 0.46D`｜`<Capabilities />` 为空、`<Prerequisites />` 无前置（实读 `D:\steam\...\4348910\3837619429\manifest.xml`）｜装配 **`DeepSpacePawn.dll`** —— **与我们自己的《深空当铺：可能是群友》程序集同名，纯属巧合**：它的 namespace 也是 `DeepSpacePawn`、`ModEntry` 也是 `IMod`，但 modId 是 `fence_case_expansion`（前身 `deep_space_pawn`），代码一行都不是我们的。约 2.5 万订阅。
> **反编译目录**：`缓存文件夹\decomp_ws\3837619429\`（26 个 `.cs` / **4,795 行**）；包内另有 `LootTables\lv2_settings.csv` + `lv2_items.csv` + `lv3_settings.csv` + `lv3_items.csv`（**参数与掉落表全在 CSV 里，不在代码里**）、`Dialogue\`、`Localization\`、`Textures\`。
> **为什么值得读**：原版开箱是"26–58 条互相独立的固定概率抽取"，本模组把它换成了**预算制价值抽取**——一次摇预算、分普通/稀有两池、按"买得起且放得下"的候选重新归一权重、稀有链式追加、跨箱保底。这是全库唯一把"箱子"当**经济模型**（而非掉落表）写的样本，且参数全部外置成 CSV + 游戏内配置窗口。副作用是它也踩出了"运行期克隆原版箱 + 靠贴图路径认身份 + 读档重建外观"这一整套必须知道的坑。

---

## 2. 规模与结构

### 2.1 文件 → 行数（26 文件 / 4,795 行）

| 文件 | 行 | 职责 |
| --- | ---: | --- |
| `Tier3CaseRecruitment.cs` | 860 | 三级销赃人 / 掮客电话 / 收藏品买家（声望 40 起的整条线） |
| `LootGenerator.cs` | 837 | **预算制掉落引擎**（本档案核心） |
| `ModEntry.cs` | 427 | 箱子工厂（克隆原版箱 + 换皮 + 8×8 内格）、调试按钮 |
| `ModdedCrateSpriteRestore.cs` | 340 | 读档后重建箱子外观（含中文名兜底匹配） |
| `CaseSellerRecruitment.cs` | 333 | 二级销赃人（声望 20 起） |
| `CollectibleTracker.cs` | 256 | `GameItem.AddParent` 后置记录"遗失物收藏品"到手 |
| `CollectibleBook.cs` 248 / `CollectibleItems.cs` 237 | 485 | `PaperUI` 收藏图鉴（6 格，未得显示剪影）、6 件收藏品注册（自定义品类 `DSP_COLLECTIBLE`） |
| `LootBudgetConfigWindow.cs` | 183 | 让原版 `ModConfigWindow` 变"只读预览 + 预设下拉" |
| `ModdedCrateShapePatch.cs` | 139 | `LockHelper.UnlockContainer` 前后快照与外观恢复 |
| `LootBudgetConfig.cs` | 133 | 预设（标准/充裕/自定义）→ `LootSettings` |
| `CollectibleTradeCrimePatch.cs` | 118 | `SecData.CheckSoldContraband` 后置（卖收藏品算销赃） |
| `BrokerPhoneContact.cs` 169 / `CollectibleBookUnlock.cs` 160 | 329 | 电话簿联系人、图鉴解锁记录 |
| `UpperContactNote.cs` 91 / `LocalizedDialogue.cs` 79 / `CaseSellerVisitControl.cs` 45 / `CounterPlacement.cs` 26 | 241 | 名片纸、台词拼装、来访开关、柜台居中摆放 |
| 4 个 13–18 行补丁壳 + `LootTier.cs`(7) + `ModText.cs`(18) | 83 | 见 §3.8 |

### 2.2 扩展点用量（源码实测）

| 扩展点 | 实测 | 备注 |
| --- | ---: | --- |
| `[HarmonyPatch]` | **11 处** | 透视报告只报 10 处——漏掉的是 `CollectibleTracker.cs:10` 的**嵌套类补丁**（`GameItem.AddParent`） |
| `ModHook` | 5 种 | `OnGameLoadedLate`、`OnModItemDirectoryInit`、`OnModAssetsLoaded`、`OnGenerateCustomerVeryLate`、`OnHandlingNightlyServicesLate` |
| `ModHelper` | 20 种 | 亮点：`ReadModFileText`（零能力读 CSV）、`GetConfig`（`ModConfig`）、`RegisterPaperBook`/`SpawnPaperItem`、`AddDebugFunction`、`SetModDataInt`（保底计数） |
| 外部边界 2 处 | `AccessTools.Field(typeof(PlayerStore),"saveBags")`（`ModdedCrateSpriteRestore.cs:99`）+ 内嵌资源 `StreamReader`（`:791`） | 前者够私有字段扫存档容器；后者只读**程序集内嵌资源**（`GetManifestResourceStream`），不碰磁盘，外部表一律走 `ModHelper.ReadModFileText` |

---

## 3. 它怎么做这件事

### 3.1 先看它改的是什么：原版开箱的权重模型

原版箱子**不是权重表**，是**一长串互相独立的伯努利试验**：

- `PreBuiltItemHelper.CreateLootCrate(crateId, boxId)`：`DirectoryMaster.Item(boxId)` → 打 `LOOT_CRATE_ID_STRING` tag → `LotteryHelper.StampSpawnLuck()` → **`crateFillers[crateId](item, GetLuckMult(item))`** → `LockHelper.LockUpContainer()`（`共享开发/reference/Assembly-CSharp/PreBuiltItemHelper.cs:264-274`）。
- 每条 filler 就是 `if (LuckRoll(chance, luckMult)) TryAdd(crate, DirectoryMaster.Item("x"))`；`LuckRoll = RNG.Roll(chance * luckMult)`，`luckMult = 1 + GetSpawnLuck(crate)/100`（`:277-293`）。
- 实测每箱试验条数与概率区间：**Evidence 58 条（3%–75%）、Medical 42、Service 41、Engineering 36、Security 26**。每条最多进 1 件，满了 `TryAcceptAll` 失败就销毁（`:287-293`）。
- 幸运是**双重**的：造箱时按当时幸运填一次；解锁时若玩家当前幸运更高且箱子没被"扫描"过，**整箱清空重摇**（`TryRefillForBetterLuck`，`:303-329`）。

所以原版的特征是：**项数多、单件概率低但有大量 50–75% 的高概率条目、无权重、无保底、可开出 0 件**。

### 3.2 "新算法"：预算制价值抽取（公式）

`LootGenerator.PopulateCore`（`LootGenerator.cs:150-216`）的完整流程，逐行可对：

```
B     = RollBudget()                    // 50%: U[LowBudgetMin..LowBudgetMax]，否则 U[HighBudgetMin..HighBudgetMax]
S     = floor(B * 0.90)                 // SpendableBudgetFactor：只有 90% 的预算会被花掉
share = U[NormalShareMin .. NormalShareMax]        // 0.30 ~ 0.65
N     = floor(S * share)                // 普通池预算
R     = S - N + pity                   // 稀有池预算；pity 是跨箱累积的保底余额
if (R <= 200) { N = clamp(B + bonus, 400, 500); R = 0 }      // 空箱保护：干脆全给普通池

稀有阶段（仅当 R > 200）：
  ① 放 1 件"全价"稀有（cost = ceil(value * 1.0)）；成功 → R -= cost，N += 100
  ② 最多再链式追加若干件：第 i 次掷 ExtraChance[i]（0.75/0.65/0.55/0.45），成功则 cost = ceil(value * 0.30)
  ③ R 还有剩就继续全价放，直到放不下或件数到 48
pity 记账：一件稀有都没放下 → pity = min(PityCap, pity + PityPerMiss)；放下超过 2 件 → pity = 0
普通阶段：while (N > 0 && 件数 < 48) 挑一件"付得起 + 有格位"的，N -= value
兜底：网格仍为空 → 塞 100 信用点（PreBuiltItemHelper.EnergyCredit(100)）
```

"越贵越稀有"**不是靠调权重**，而是靠**预算剪枝**：`PickCandidate` 先把 `ceil(value * costFactor) > budget` 的候选全部剔除（`LootGenerator.cs:299-303`），只在"买得起且塞得进"的候选里按 `Weight` 归一化抽（`:312`、`:319-327`）。所以贵东西在低预算箱里概率**恰好为 0**，只在预算够的箱里按 CSV 权重正常出现——表里的权重永远是"条件概率"，这是 CSV 头部注释自己也写明的事（`LootTables/lv2_items.csv:2`）。

### 3.3 参数表（随包 CSV 为准，代码里另有一套 fallback）

| 参数 | lv2（`lv2_settings.csv`） | lv3（`lv3_settings.csv`） | 代码内 fallback（`LootGenerator.cs:12-50`） |
| --- | --- | --- | --- |
| `LowBudgetChance` | 0.50 | 0.50 | 0.5 |
| `LowBudgetMin/Max` | **100 / 800** | **200 / 1900** | 400 / 500 ⚠️ |
| `HighBudgetMin/Max` | **2400 / 3000** | **4000 / 8000** | 2200 / 2800 ⚠️ |
| `SpendableBudgetFactor` | 0.90 | 0.90 | 0.9 |
| `NormalShareMin/Max` | 0.30 / 0.65 | 0.30 / 0.65 | 0.3 / 0.65 |
| `RareBudgetStartThreshold` | 200 | 200 | 200 |
| `EmptyProtectionMin/Max` | 400 / 500 | 400 / 500 | 400 / 500 |
| `PityPerMiss` / `PityCap` | **300 / 2000** | **400 / 3000** | 100 / 2000 ⚠️ |
| `ExtraRewardCostFactor` | 0.30 | 0.30 | 0.3 |
| `ExtraChance1..4` | 0.75/0.65/0.55/0.45 | 同左 | 同左 |
| `MaxItemsPerPool` | 48 | 48 | 48 |

⚠️ = **代码 fallback 与随包 CSV 不一致**（CSV 缺失时才用 fallback，所以正常玩家跑的是 CSV 那一列）。照代码默认值算平衡会算错，**必须打开 CSV**。

掉落表规模（`grep -c` 实测）：lv2 共 191 条（security 41 / evidence 23+18 / engineering 21+9 / medical 17+4 / service 11+11 / `all` 6+2），lv3 共 194 条。表结构是 5 列 `case,pool,item_id,weight,budget_value(可选)`，`case=all` 表示"每个箱子都加"（`ParseEntries`，`LootGenerator.cs:750-778`）。

### 3.4 "更高潜在收益 / 更低最低回报 / 更大波动"——三个可量化指标

| 简介说法 | 代码里的对应物 | 是否成立 |
| --- | --- | --- |
| **更高潜在收益** | lv3 高段预算 4000–8000 → 可花 3600–7200；稀有池最多吃掉 ~70% + `pity` 上限 3000，件数上限 48（8×8 内格 64 格） | ✅ 成立：原版单箱最多十几件、且清单固定；这里上限是"预算千位数 + 48 件" |
| **更大波动** | 预算**双峰**：lv2 一半箱子 100–800、另一半 2400–3000（差 3–30 倍）；lv3 一半 200–1900、另一半 4000–8000（差 4–40 倍）；再叠稀有链（0.75/0.65/0.55/0.45）与跨箱 pity | ✅ 成立：比原版"同一组固定试验"的方差高一个量级 |
| **更低最低回报** | lv2 低段只 100–800 预算（≈90–720 可花），普通池里最便宜的条目权重 20、单价几十信用点 → **单箱总价值可以比原版同型箱低** | ⚠️ **方向与代码相反，标为"简介与代码不一致"**：代码同时加了原版**没有**的保底——`EmptyProtection` 400–500 预算 + 真空箱塞 100 信用点（`:167-171`、`:475-483`）。原版的真实下限是**0 件**。所以准确说法是"低段明显更低，但不为空"，不是"最低回报更低" |

结论：三句里两句可量化成立，第三句要在档案里改写口径，否则会误导平衡设计。

### 3.5 箱子怎么注册：**不注册新物品，运行期克隆原版预置箱**

**箱子没有任何物品定义**（包里没有物品 JSON、也没有注册 `loot_crate_*` 变体；注册的物品只有 6 件收藏品，见 §3.8）；`CreateCase`（`ModEntry.cs:236-282`）走的是：

1. `PreBuiltItemHelper.LootCrateService()`（或 Security/Evidence/Medical/Engineering）→ **原版预置箱**，此时原版 filler 已经填过一遍内容、并已 `LockUpContainer` 上锁；
2. 存一份 `ShapeSnapshot`（外格形状 + `modifiedShape` + 原点），改 `name`/`shortDescription`/`flavorText`，把贴图换成 `lv2_service_lock` 之类，再**还原外格形状**（`ApplyStateSprite` + `RestoreOuterShape`，`:320-340`）；
3. 内格 `SetShape(8, 8)`（`:276-281`）；
4. 交给销赃人之前调 `PopulateCaseContents` → `ClearOriginalContents()`（把原版 filler 的货全 `Expel` 掉，`:464-473`）→ 跑本模组的预算引擎。

**身份标记 = 贴图路径**，不是 identifier：`item.spriteAtlasPath == "fence_case_expansion" && spritePath.StartsWith("lv2_"|"lv3_")`（`ModdedCrateShapePatch.cs:127-138`）。原版箱靠 `IsTag("SUPPLY_CRATE")` 区分（`:51`）。

**为什么要这么绕**：往一个"已上锁"的箱子里塞货，会被 `LockUpContainer` 装上的 `mayInventoryAddItemFunc = (_,_) => false` 直接拒绝（`LockHelper.cs:LockUpContainer`）。本模组的解法是**走槽位级 API 绕过库存级谓词**：

- `grid.TryFindOneValidInventorySlot(item, false)` 拿 `SlotMarker`；
- `PendingSlot.TryAcceptOnce(-1)` → 内部 `inventory.UncheckedAccept(item)`（`SlotMarker.cs:94-103`），**不查 `mayInventoryAddItemFunc`**；
- 再补一个 18 行补丁：`ActivePopulationGrid` 期间把 `GameGridInventory.MayHaveValidInventorySlot` 恒真（`ModdedCrateSlotSearchPatch.cs:8-17`），并在 `Populate` 的 `try/finally` 里还原静态开关（`LootGenerator.cs:138-147`）。

### 3.6 和原版箱子是**并列**，不是替换

- 原版 5 种 `loot_crate_*` 仍由原版工厂生成、原版 filler 填充、原版 `LockHelper` 解锁——本模组只在 `LockHelper.UnlockContainer` 上挂 Prefix/Postfix，且 Postfix 里分两支：`IsNativeSupplyCrate` 只打一行诊断日志（`ModdedCrateShapePatch.cs:89-104`），`IsModdedCrate` 才恢复外观并 `CollectibleTracker.ScanOpenedCrate`（`:105-124`）。
- 同一个存档里两种箱子共存，互不知道对方。
- **唯一全局影响面**是 `ModdedCrateSlotSearchPatch`：它是全局 Prefix，靠静态 `ActivePopulationGrid` 收窄到"正在被本引擎填充的那一个 grid"。**无重入保护**——同一帧两次 `Populate` 会互相覆盖该静态字段（`推测`：外层先还原成旧值，内层会被写成内层 grid，最终外层把字段还原为 null 而非外层 grid）。

### 3.7 黑市声望 20–40：**是解锁门槛，不是产出缩放**（★ 简介与代码不一致）

全库"声望"共出现 4 次（2 个阈值常量 + 2 处读取），**全是门槛**：

| 位置 | 阈值 | 含义 |
| --- | ---: | --- |
| `CaseSellerRecruitment.cs:20`、`:161` | `>= 20` | 解锁**二级销赃人**：先在夜里排一个"黑市传闻小偷"到店，次日起二级销赃人开始按 60% + 10%/升级（上限 100%）来访，报价 **600**（`:22-32`） |
| `Tier3CaseRecruitment.cs:21`、`:107` | `>= 40` | 解锁**三级掮客**：先付 **1000** 联络费拿到名片纸，之后三级销赃人按 50% + 10%/升级来访，报价 **1500**（`:23-33`、`:781-794`） |

**没有任何按声望缩放权重、预算、价格或件数的代码。** 简介说"内容集中在黑市声望 20–40+"，读源码应理解为"**玩法在这两个等级点解锁**"，而不是"产出随声望线性/分段增长"。写平衡文档时别把它当曲线用。

### 3.8 Patch 靶点表（11 处）

| 原版靶点 | 文件 | 干了什么 |
| --- | --- | --- |
| `LockHelper.UnlockContainer` | `ModdedCrateShapePatch.cs:6` | 前后快照；只给自家箱恢复 `_u` 贴图/形状；顺带扫收藏品 |
| `GameGridInventory.MayHaveValidInventorySlot` | `ModdedCrateSlotSearchPatch.cs:5` | 填充期间恒真，绕过形状预检 |
| `SaveManager.DecodeNodes` | `ModdedCrateDecodePatch.cs:5` | 解码后立刻重建箱子外观（不等 `OnGameLoadedLate`） |
| `GameItem.AddParent` | `CollectibleTracker.cs:10` | 物品入手即记录收藏品 / 首只三级箱 / 首次到手 |
| `SecData.CheckSoldContraband` | `CollectibleTradeCrimePatch.cs:8` | 卖收藏品按销赃处理 |
| `TypeHelper.GetTypeDisplayName(string)` | `CollectibleItemTypePatch.cs:6` | 给自定义品类 `DSP_COLLECTIBLE` 补显示名（**躲 `[Translation Error]`**） |
| `PlayerStore.OnItemBought` | `Lv3CasePurchasePatch.cs:5` | 买了三级箱 → 记"首次获得" |
| `StorePhoneClient.GetLocalizedDisplayName` | `BrokerPhoneClientNamePatch.cs:5` | 电话簿里显示掮客名字 |
| `ModConfigWindow.Open/Apply/ResetWidgets` | `LootBudgetConfigWindow.cs:129/138/158` | 让原版配置窗变成"预设下拉 + 只读预览"，切预设即时重算 |

---

## 4. 关键代码摘录

### 4.1 预算分配与稀有链（`DeepSpacePawn\LootGenerator.cs:161-215`，删去日志行）

```csharp
int B = RollBudget();
int pity = Clamp(ModHelper.GetModDataInt("fence_case_expansion", pityKey, 0), 0, settings.PityCap);
int spendable = (int)Math.Floor((double)B * settings.SpendableBudgetFactor);
double share  = RandomRange(settings.NormalShareMin, settings.NormalShareMax);
int normalBudget = (int)Math.Floor((double)spendable * share);
int rareBudget   = spendable - normalBudget + pity;
if (rareBudget <= settings.RareBudgetStartThreshold)              // 空箱保护
{ normalBudget = Clamp(B + settings.EmptyProtectionBonus, settings.EmptyProtectionMin, settings.EmptyProtectionMax);
  rareBudget = 0; }
List<Candidate> normal = BuildCandidates(caseKey, "normal");
List<Candidate> rare   = BuildCandidates(caseKey, "rare");
int rareHits = 0, addedItems = 0, allRarePlaced = 0;
if (rareBudget > settings.RareBudgetStartThreshold
    && TryPlaceOne(grid, rare, rareBudget, 1.0, ref addedItems, ref allRarePlaced, out var cost))
{
    rareBudget -= (int)cost; normalBudget += 100; rareHits++;      // 出稀有 → 普通池补 100
    int i = 0;
    while (addedItems < settings.MaxItemsPerPool && TryRollExtraChance(i)           // 0.75/0.65/0.55/0.45
           && TryPlaceOne(grid, rare, rareBudget, settings.ExtraRewardCostFactor,   // 追加件只付三折
                          ref addedItems, ref allRarePlaced, out cost))
    { rareBudget -= (int)cost; normalBudget += 100; rareHits++; i++; }
    while (rareBudget > 0 && addedItems < settings.MaxItemsPerPool
           && TryPlaceOne(grid, rare, rareBudget, 1.0, ref addedItems, ref allRarePlaced, out cost))
    { rareBudget -= (int)cost; normalBudget += 100; rareHits++; }
}
if (rareBudget > 0) normalBudget += rareBudget;                    // 没花完的稀有预算回流普通池
if (rareHits == 0)      pity = Math.Min(settings.PityCap, pity + settings.PityPerMiss);
else if (rareHits > 2)  pity = 0;
ModHelper.SetModDataInt("fence_case_expansion", pityKey, pity);    // 保底跨箱、落 modData
RollNormalPool(grid, normal, normalBudget, addedItems, ref allRarePlaced);
if (((GameInventory)grid).childItems.Count == 0) TryAddEmergencyCredit(grid);
```

### 4.2 候选重归一抽取（`DeepSpacePawn\LootGenerator.cs:287-329`）

```csharp
private Candidate PickCandidate(GameGridInventory grid, List<Candidate> candidates,
                                int budget, double costFactor, int allRareItemsPlaced)
{
    List<Candidate> eligible = new List<Candidate>();
    double totalWeight = 0.0;
    foreach (Candidate c in candidates)
    {
        if (c.Disabled || c.Item == null || c.Value <= 0
            || (allRareItemsPlaced >= 1 && IsAllRareEntry(c.Entry))) continue;   // all/rare 只放一次
        if ((long)Math.Ceiling((double)c.Value * costFactor) > budget) continue;  // ← "越贵越稀有"的真正来源
        SlotMarker slot = grid.TryFindOneValidInventorySlot(c.Item, false);
        if (slot == null) continue;                                              // 塞不进也不算候选
        c.PendingSlot = slot; eligible.Add(c); totalWeight += c.Entry.Weight;
    }
    if (eligible.Count == 0 || totalWeight <= 0.0) return null;                  // 池子空了就停手
    double roll = Random.NextDouble() * totalWeight;
    foreach (Candidate c in eligible)
        if ((roll -= c.Entry.Weight) <= 0.0) return c;
    return eligible[eligible.Count - 1];
}
```

### 4.3 克隆原版箱 + 交给人之前就填好（`DeepSpacePawn\ModEntry.cs:236-282`、`:381-401`）

```csharp
internal static GameItem CreateCase(CaseType type, CaseTier tier)
{
    GameItem val = /* PreBuiltItemHelper.LootCrate{Service,Security,Evidence,Medical,Engineering}() */;
    if (val == null) return null;
    ShapeSnapshot outer = new ShapeSnapshot(val);            // 外格形状 + modifiedShape
    val.name = GetCaseName(type, tier);                      // case_lv2_service_name ...
    val.shortDescription = GetCaseDescription(type, tier); val.flavorText = GetCaseFlavorText(type, tier);
    ApplyStateSprite(val, GetLockSpriteName(type, tier), outer);   // lv2_service_lock + 还原外格
    FindInnerGrid(val)?.SetShape(8, 8);                      // 内格固定 8×8
    return val;
}
internal static bool PopulateCaseContents(GameItem box, CaseType type, CaseTier tier)
{
    GameGridInventory grid = FindInnerGrid(box);
    if (grid == null) return false;
    LootGenerator gen = (tier == CaseTier.Lv3) ? lv3LootGenerator : lv2LootGenerator;
    gen.Populate(grid, type.ToString().ToLowerInvariant());  // caseKey = service/security/...
    return true;
}
```

### 4.4 读档后靠"贴图路径 + 中文名"认亲（`DeepSpacePawn\ModdedCrateSpriteRestore.cs:224-287`）

```csharp
private static bool TryGetSpriteKey(GameItem item, out string spriteKey)
{
    spriteKey = null;
    if (item.spriteAtlasPath != "fence_case_expansion" && item.spriteAtlasPath != "deep_space_pawn") return false;
    if (TryGetSpriteKeyFromSavedSpritePath(item, out spriteKey)) return true;   // 首选：存下来的 spritePath
    if (string.IsNullOrEmpty(item.name)) return false;
    string prefix, rest;
    if (item.name.StartsWith("二级")) { prefix = "lv2_"; rest = item.name.Substring(2); }
    else if (item.name.StartsWith("三级")) { prefix = "lv3_"; rest = item.name.Substring(2); }
    else return false;
    string kind = rest.Contains("后勤物资箱") ? "service" : rest.Contains("安保物证箱") ? "evidence"
                : rest.Contains("安保物资箱") ? "security" : rest.Contains("医疗物资箱") ? "medical"
                : rest.Contains("工程物资箱") ? "engineering" : null;
    if (kind == null) return false;
    bool locked = item.IsTag("LOCKED_CONTAINER_TAG") && item.GetTagReadonly("LOCK_STATUS") != null
                  && item.GetTagReadonly("LOCK_STATUS").GetBool();
    spriteKey = prefix + kind + (locked ? "_lock" : "_lock_u");
    return true;
}
```

---

## 5. 踩坑与红线

1. **程序集同名陷阱（本批最大）**：`3837619429` 的产物叫 `DeepSpacePawn.dll`、namespace `DeepSpacePawn`，我们自己的《深空当铺：可能是群友》也是 `DeepSpacePawn.dll`。**两者装在同一台机器上就是同名程序集**——排查日志/崩溃栈时看到 `[DeepSpacePawn]` 前缀，本模组也用它当日志前缀（如 `ModdedCrateSpriteRestore.cs:109`），**必须靠 modId 区分**（本模组 `fence_case_expansion`，前缀常见 `[DeepSpacePawn]` 与 `[FenceCaseExpansion]` 混用）。
2. **往已上锁的箱子里塞货要绕两层**：库存级 `mayInventoryAddItemFunc` 已被 `LockUpContainer` 设成恒 false；必须走 `SlotMarker.TryAcceptOnce`（→ `UncheckedAccept`）这种槽位级 API，并临时让 `MayHaveValidInventorySlot` 返回 true。用 `AddItem`/`TryAcceptAll` 会静默失败。
3. **箱子身份绑在贴图路径上**：`spriteAtlasPath=="fence_case_expansion" && spritePath.StartsWith("lv2_"/"lv3_")`。任何"整理贴图/换命名/换图集"的改动都会让**老存档里的箱子当场变回原版外观、并且不再被本引擎认领**（读档恢复失败）。想更稳，应该打一个自己的 tag/字符串 tag 当身份，而不是贴图路径。
4. **读档兜底路径硬编码中文名**：`"二级"/"三级"/"后勤物资箱"/"安保物证箱"…`（`ModdedCrateSpriteRestore.cs:245-283`）。**英文/其它语言客户端 + 旧档 = 认不出**（`推测`：只有 `spritePath` 已存盘的档才安全）。这就是"文案即协议"的典型代价。
5. **参数有两份真相**：CSV 与代码 fallback 不一致（§3.3 表里标 ⚠️ 的 4 项）。改平衡只改代码 = 玩家看不到变化；只改 CSV = 缺文件时行为突变。
6. **`ActivePopulationGrid` 是全局可变静态**，靠 `try/finally` 还原（`LootGenerator.cs:138-147`），但**没有重入计数**。同帧并发填充（如一次开两个箱）会互相踩；`推测`目前靠"一次只开一个箱"蒙过去。
7. **原版箱被多填了一遍**：`CreateCase` 先让原版 filler 填满 → `ClearOriginalContents` 全 `Expel` 掉 → 再填自家货。**这一步会白跑一次原版随机数并触发 `TryAcceptAll` 的销毁路径**，量大时是纯浪费；更好的做法是克隆后先把 `contentWindow` 的 filler 谓词摘掉再填。**而 `Expel` 失败只记日志不抛**（`:464-473`），失败的货会**留在箱子里**跟自家货混在一起——排查"箱子里怎么多了个原版物品"先看这行日志。
8. **自定义品类 `DSP_COLLECTIBLE` + 全局 `TypeHelper.GetTypeDisplayName` 补丁**：这是"用 `SetGameItemType` 写非原版类型"的**唯一合法姿势**——必须同时补显示名，否则蓝字 `[Translation Error]`。但它是一条**全局补丁**，别的模组也往 `TypeHelper` 加分支时会打架（`HarmonyPriority` 未声明）。
9. **改过 modId**：前身 `deep_space_pawn`，`OnGameLoadedLate` 里做键迁移（`MigratePreviousModData`，`ModEntry.cs:72-100`，逐个 `HasModData` 后 `SetModData`）。**迁移只在 `OnGameLoadedLate` 跑一次**，且**不删旧键**；玩家回退旧版本会看到两份数据。

---

## 6. 对我们的可用点

### 6.1 →《深空当铺：可能是群友》（`freya_cornell` / `DeepSpacePawn.dll`）★ 最高优先

1. **给我们的 4 款清仓箱（`shuangluoluo_crate_sec/eng/med/ser`）接一套真正的掉落引擎**。我们现在是"按原版部门锁 ID + 箱内过滤违禁品 + 提高高价值出率"（见 `深空当铺：可能是群友/PROGRESS.md` §1），本质仍是原版 filler 的概率堆叠。本档案给的是完整替代方案：**一次摇预算 → 普通/稀有两池 → 预算剪枝决定"贵不贵出" → 稀有链式追加 → 跨箱 pity**。落地时把 `LowBudgetMin/Max` 换成分档：伞萝萝 `MinAttractiveness 300` 一档、`>= 450` 一档，正好对应 lv2/lv3 的双峰预算。
2. **pity 就存 `SetModDataInt`，跨箱跨存档累积**（`LootGenerator.cs:162`、`:203-209`）。这是最便宜的"保底"实现，不需要动存档结构；我们已有 `DspState` 账本可直接加键。
3. **`ModConfigWindow` 三补丁 + 预设下拉 + 只读预览**（`LootBudgetConfigWindow.cs:129-183`）：把我们现在的硬编码门槛（吸引力 300/450、出率）变成玩家可调的三档预设，且**切预设即时重算**、旧预设名自动迁移（`恢复默认→标准`、`更爽→充裕`，`LootBudgetConfig.cs:68-76`）。
4. **`MigratePreviousModData`（旧 modId 键迁移）**：我们要改 `freya_cornell` 之前，先抄这 30 行，玩家存档不丢。

### 6.2 →《深空电玩》（`deepspace_arcade`）

5. **"付费情报 / 盲盒"用同一套预算制**：电玩已有"付费情报"和"概率维修 +5% 试次"，都是"付钱换随机结果"。预算制比固定权重更适合它——因为可以**承诺一个价值区间**（"这箱至少值 400"），而固定权重表做不到。pity 部分尤其适合"连续 N 次没出情报就给保底"。

### 6.3 反向：**不要**抄的两条

- **别用贴图路径当物品身份、别用中文名做读档兜底匹配**。我们已经有 `EnableTag` 体系，箱子该打自己的 tag；文案一改读档就认不出。
- **别把"往锁上的箱子塞货"当常规手法**。它的前提是"箱子里本来是空的/可清空"，一旦我们的箱子有原版货残留（见 §5.7）就会混货。
