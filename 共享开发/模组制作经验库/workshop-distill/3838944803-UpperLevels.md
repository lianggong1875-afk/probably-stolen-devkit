# 3838944803 · 上层区 Upper Levels：可能是 DLC —— 架构蒸馏

> 工坊 id `3838944803` ｜ 35,915,682 B ｜ 订阅 27,311 ｜ 作者 Mr.K ｜ 适配 **0.46D**
> 反编译源码：`D:\0深空当铺模组开发\缓存文件夹\decomp_ws\3838944803`
> 规模：188 个 `.cs` / 工坊统计 63,727 行 / 18 个 `ModHook` / 267 处特性式 `HarmonyPatch`
> 本机复测：58,709 个非空行，命中 **78 个不同原版类型**；本文比例以此为准

**读法**：未通读 6.4 万行。做法是「目录 → 文件规模 → hook/patch 分布 → 定点读 20 余个骨架文件」。代码片段为反编译产物**原文照抄**（含反编译器的冗余 `(Func<string>)(() => ...)`、`((Object)(object)x)` 强制转换），只为可核对，不代表作者原始写法；节选处省略日志/通知/异常分支，以 `...` 或注释标明。无实据处标「（推测）」。

## 1. 「加一整片新区域」是怎么做到的

### 1.1 结论先行：没有新建场景

整片"上层区"**不是** Unity 新场景，**也不是**新地图，而是**八层叠加物**，全部挂在同一个总闸之下：

```csharp
// UpperLevels\Upper.cs:70-81  —— 全模组的唯一总闸
private static bool Compute()
{
    try
    {
        Difficulty current = Difficulty.Current;
        return current != null && current.source == "upper_levels";
    }
    catch (Exception)
    {
        return false;
    }
}
```

「我在上层区」= **当前难度档的 `source` 是 `upper_levels`**。玩家在主菜单点一下「上层区模式」（`PlayerPrefs` 键 `upper_levels.mode`，默认开，`UpperModeToggle.cs:242-270`），新建存档时选到 `upper_normal` 之类；进档后所有内容自动出现。关掉模式，整片区域对原版零影响——**因为判断条件只有这一个**。

### 1.2 八层拆解

| 层 | 玩家感知 | 接进原版的手段 | 要 Patch |
| --- | --- | --- | --- |
| A 区域身份 | 一个"上层区"难度档 | `AddDifficulty`（`UpperDifficulties.cs:186`），`upper_cozy/normal/hard/challenge` | 否（但要 Patch 难度说明文本） |
| B 店面视觉 | 黑金/霓虹/复古三套装修、窗外街景飞车、玻璃滑门 | 对原版店铺场景**原地换 sprite**（`StoreSkin`/`PropSkins`/`GlassDoor`/`FlyingCars`/`HoloPoster`） | 否（运行时换图 + 加几个 renderer） |
| C 夜间外出 | 交火区、货运电梯、两处潜入目标 | `AddAfterhourLocation` ×4；`AddAfterhourEvent`+`StartAfterhourEvent` | 只补 MapUIManager 4 处 |
| D 客群 | 拾荒者/矿工不再上门，换工程师、私人医生、研究员、名媛、收藏家 | ① hook 里 `manager.AddClient(...)` 造新客；② `AddClient` Postfix 把原版已生成的客人**就地改写** | 是 |
| E 物品与机器 | 260+ 新物品、多台职业机器 | `OnModItemDirectoryInit` + `Directory.Set(id, 工厂)` | 否（`Directory` 是扩展点） |
| F 成长线 | 人脉网络多一个 `UpperCircle` 页签、38 项升级 | `SetNetworkTabLabel` + `AddNetworkUpgrade` | 否（但要 Patch 布局/前置） |
| G 开局身份 | 8 个新剧本、40 项重做特性、4 档难度 | `AddStartingScenario`/`AddStartingPerk`/`AddDifficulty` | 是（主菜单新游戏三列表） |
| H 界面时代 | 赛博蓝 UI | `UISkin` 常驻 `MonoBehaviour` **持续对抗**原版刷新 | 否（代价见 §6） |

### 1.3 三个关键接入点

**① 夜外出地点**——原版给了完整一等公民扩展点：到达/离开/面板构建/卡片图/背景图/解锁条件/可见条件一次交清。

```csharp
// UpperLevels.Professions\Battlefield.cs:297-305
ModHelper.AddAfterhourLocation("upper_levels", "ul_battlefield", (Func<string>)(() => Upper.L("battle.title")), (Action)LeaveFightCrateOnArrival, (Action)LocationPanel.Reset, (Action<CustomUIBuilder>)BuildPanel, "upper_levels:ul_battle_card", "upper_levels:ul_battle_bg", (string)null, (Func<bool>)(() => Upper.IsActive && !UpperTraits.BattlefieldBlocked), (Func<bool>)(() => Upper.IsActive));
ModHelper.AddAfterhourEvent("upper_levels", "ul_fight_night", "ul_battlefield", (Func<string>)(() => Upper.L("battle.event.title")), 1, (Func<string>)(() => Upper.L("battle.event.desc")), (IEnumerable<AfterhourEffect>)new AfterhourEffect[4]
{
    new AfterhourEffect((Func<string>)(() => Upper.L("battle.event.corpse")), (string)null, 0f, (AfterhourEffectPolarity)1),
    new AfterhourEffect((Func<string>)(() => Upper.L("battle.event.fresh")), (string)null, 0f, (AfterhourEffectPolarity)1),
    new AfterhourEffect((Func<string>)(() => Upper.L("battle.event.injury")), (string)null, 0f, (AfterhourEffectPolarity)2),
    new AfterhourEffect((Func<string>)(() => Upper.L("battle.event.spot")), (string)null, 0f, (AfterhourEffectPolarity)2)
}, 0f, (Nullable)null, (Func<bool>)null, (Action)null, (Action)null, (IEnumerable<AfterhourWeekday>)null, false);
```

两个细节：地点 id 是**硬编码字符串常量**（`FreightLift.cs:23` 的 `ul_freight_lift`、`Salon.cs:11` 的 `ul_friday_salon`），存档里存的就是它；**解锁条件是 `Func<bool>` 不是写死**（`FreightLift` 要求 `Chapter2.Active && Chapter2.HasReport`，`Battlefield` 还叠特性封锁），所以地点能"剧情推进后才出现在地图上"。

**② 新顾客 = 造 + 改，两条腿**。造新客走 hook 里的 `manager.AddClient(...)`；改原版客人走 Postfix 就地洗：`UpperClients.cs:258-262` 里若 `clientFaction == "FACTION_LOWER_LEVEL"` 就 `StoreClientFactionSetup.InitUpperLevelCitizen(storeClient)` 并把 `spriteName` 换成 `SpriteDict.GetRandomUpperSpriteName()`。该 Postfix 维护三张 id 表——`Restyle`（只换皮）、`Replacements`（整条换成 mod 的工厂产物）、`KeepMechanics`（留行为只换身份/名字）——再叠 `KeepDuringTutorial` 白名单，前 13 天不让教学客人被换。另有 `GetUpperLevelClientChance` 的 `+37`（`UpperClientChancePatch.cs:9-14`）抬高基准概率。

**③ 成长线：把原版升级"再锁一层"**。`KaneNetwork.Register` 里 38 处 `Add(...)` 升级，其中 18 处调 `UnlockVanilla(...)` 再包装原版升级，形状如 `KaneNetwork.cs:258-265`：`Add("UL_KAI_SHOWCASE_I", 1000, "SHOWCASE_I", null, 0, () => { UnlockVanilla("SHOWCASE_I"); }, 1);`。上层区模式下原版「展示柜 I」不再凭等级自然出现，必须先在 `UpperCircle` 页签花钱解锁——**不改原版条件，只让原版条件通过 mod 的钩子才被满足**。

### 1.4 不得不 Patch 的清单

| 原版位置 | 补丁数 | 为什么扩展点不够 |
| --- | --- | --- |
| `StartingPerk` | 23 | 40 项特性的互斥、槽位、菜单可见性原版没有配置字段 |
| `PlayerStore` | 20 | 租金上限、清洁结算、买断价要按上层区经济重算 |
| `StoreClientManager` | 18 | 客人身份改写、展示柜客人、常客数量都要改生成链 |
| `EmporiumEntry` | 13 | 桌面"非卖置物格"、水龙头状态、Validate 重跑 |
| `StoreClient` | 13 | 预算/意图/购买标签的字段级改写 |
| `GameItem` | 12 | 新物品形状贴图、放上货架/客人区的副作用 |
| `StoreReputation` | 9 | 上层区声望单独贴现（`value * 15 / 100`）与每日封顶 |
| `LocHelper.Get` | 9 | 原版硬编码文本（治安部、13 区、安全部门）要按模式改名 |
| `NetworkUpgrade` | 8 | 前置判定、隐藏、成本 ×3 |
| `StoreClientListSupplier` | 8 | 拾荒者/小偷货单要加新品类 |
| `MapUIManager` | 7 | 夜间地图面板高度、面板重开、返回逻辑 |

**没有碰的东西同样值得记**：存档系统（一行不写，全走 ModData）、物品目录、夜晚日程驱动（`OnWakingUp`/`OnGoingSleepEarly` 够用）、机器过夜结算。这三处正是最容易把模组做成"装上就坏档"的地方，作者全绕开了。

## 2. 267 个 Patch 怎么管

### 2.1 没有注册器，也没有补丁开关表——组织维度是「功能文件」

全仓**找不到**中央补丁注册表、找不到 `Patches.cs` 大杂烩、找不到按原版类分文件的 `PlayerStorePatches.cs`。补丁**寄生在功能文件里**：一个 `.cs` = 一个功能，功能类自己的 `[HarmonyPatch]` 写成嵌套 `internal static class`。

```
UpperLevels\Cleaning.cs           72 行 = 清洁结算 1 Prefix + 拦截 Sawyer 介绍客 1 Prefix
UpperLevels\RentCapPatch.cs       17 行 = 就一个租金上限 Postfix
UpperLevels\StoreCreatedPatch.cs  10 行 = 就一行 Upper.HasStore = true
UpperLevels\UpperClientChancePatch.cs 16 行 = 就一个 +37
```

267 处散布在约 **85 个文件**：最多的 `UpperPerks.cs` 32 处、`UpperRules.cs` 11 处、`Recycling.cs` 10 处，其余多 1–5 处。**好处**：改功能时补丁在手边，删功能=删文件；**代价**：想知道"`AddClient` 被谁改过"必须全仓 grep（本文正是这么做）。

三层叠加的实际分组：**目录=域**（`UpperLevels` 核心/店面/规则 · `.Professions` 81 文件职业切片 · `.Social` 22 · `.Story` 18 · `.Heist` 7 · `.Machines` 7 · `.Endgame` 2）→ **文件=功能**（`GlassDoor`、`SpringTap`、`ShelfInventory`、`RentCapPatch`…）→ **嵌套类=单个补丁点**。

### 2.2 唯一的"开关表"是内容开关，不是补丁开关

补丁**永远全挂**，是否生效靠体内第一行早退。五个层级：

| 闸门 | 位置 | 作用 |
| --- | --- | --- |
| `Upper.IsActive` | `Upper.cs:23-50`，三元组记忆化 | 运行态总闸（难度 source） |
| `Upper.HasStore` | `StoreCreated/LoadGame/NewGame/ResetPatch` 四个 10 行小补丁 | 在不在店铺场景 |
| `UpperModeToggle.Enabled` | `PlayerPrefs upper_levels.mode` | 主菜单偏好，决定新档列表 |
| `PrefWanted/PrefAug/PrefVanillaProfessions` + `VanillaDecor/DecorInVanilla/VanillaUI` | `PlayerPrefs` | 7 个玩法/表现开关，玩家可关 |
| `comingSoon`、`const bool Open = false` | `ProfessionHub.MarkComingSoon`、`Salon.cs:13` | 未完成职业/地点软下线 |

这就是 267 个补丁还能"装上不崩"的原因：**原版存档下 267 个补丁全部命中、全部立刻返回**。

### 2.3 同一方法被多处 Patch —— 三种处理法

重复最狠的三个：`StoreClientManager.AddClient` **5 个文件**（`Cleaning.cs:51`、`UpperClients.cs:137`、`AppraiserPatches.cs:105`、`CrateTraits.cs:92`）；`GetNormalCustomerCount` **4 个**；`LocHelper.Get` **9 个**。

**法一：id 集合互斥，各写各的域。** `CrateTraits.cs:93-112` 的 `WithdrawalBargaining` 先判 `storeClient == null`，再判 `!VanillaClientIds.IsVanilla` 且 `identifier` 不以 `ul_` 开头就返回，最后只在自己的特性 `ul_trait_crate_n16` 生效时置 `alwaysRejectBadBargain = true`——它只改一个字段，不动返回值。

**法二：拦截型用 `ref` + `return false`，改写型只 mutate 参数。** `Cleaning.BlockSawyerPatch` 能整条替换原版介绍客人：

```csharp
// UpperLevels\Cleaning.cs:54-70
private static bool Prefix(ref StoreClient storeClient)
{
    if (storeClient == null || !Upper.IsActive || !BlockedIntros.Contains(storeClient.identifier ?? "")) { return true; }
    if (storeClient.identifier == "janitorIntroduction" && !KaneNetwork.ShowServices) { return true; }
    if (storeClient.identifier == "janitorUEIntroduction" || storeClient.identifier == "janitorUEIntroductionFence")
    {
        storeClient = PrivateExchange.Intro();
        return storeClient != null;
    }
    return false;
}
```

**法三：按 key 分域，靠 guard 而非优先级。** 9 处 `LocHelper.Get` 各管一段文本域（`type_ul_upper_stock` 前缀、`ClientName/name_the_landlord`、`Hint/UI` 表、全局"治安部→保安局"替换…），互不重叠时顺序无所谓。

**必须诚实标一个隐患**：全仓 **0 处** `[HarmonyPriority]` / `HarmonyBefore` / `HarmonyAfter`。当同一 client 上同时命中"拦截型 Prefix"（`Cleaning`）与"改写型 Prefix/Postfix"（`UpperClients`、`CrateTraits`）时，谁先跑取决于 Harmony 默认顺序。作者靠"目标 id 集合不相交"规避，但这是**约定，不是机制**——这是我通读 267 处后找到的最大结构性风险。

## 3. 18 个 ModHook 的分工

`ModEntry.OnEnable`（`ModEntry.cs:42-92`）是唯一装配点：51 行订阅、51 行反订阅严格成对。

| Hook | 订阅 | 分工 |
| --- | --- | --- |
| `OnModItemDirectoryInit` | 9 | **物品工厂注册唯一入口**：`Directory.Set(id, () => GameItem)`；新物品/钥匙卡/器官/战利品/情报全从这里进目录 |
| `OnGenerateCustomerEarly` | 11 | 抢先占位：剧情调度、职业访客、器官电话、Kai 上门、特性访客。必须在原版抽客前插入，否则被预算重算覆盖 |
| `OnGenerateCustomerNormal` | 1 | `FactionRegulars` 五方派系每周常客——插在"Early 已排、Late 未收"之间 |
| `OnGenerateCustomerLate` | 15 | 收尾：订单、私院单、RevMedic、RichPatient、私人交易所、剧本、人脉解锁、海军剧情访客 |
| `OnGenerateCustomerVeryLate` | 2 | 最后一道：`Act1` 强制第一幕来客、`UpperRules` 全局规则压制 |
| `OnGameLoadedEarly` | 1 | `UpperDifficulties.OnGameLoadedEarly`：**恢复难度档**，必须最早，后续一切都看 `IsActive` |
| `OnGameLoadedInit` | 2 | 会话态清理：`KaneNetwork.EnsureInRun`、`NavyEquipmentFootprints.ClearPending()`、`ResetSessionState()` |
| `OnGameLoadedLate` | 4 | 存档已就位后的活：`StoreSkin.Refresh` 重贴图、`Morning.OnGameLoaded` 补算、海军两处一次性迁移 |
| `OnNetworkUpgradeUnlocked` | 1 | `KaneNetwork.OnUpgradeUnlocked`：解锁副作用（发钱/发物/开地点），判定不在这里 |
| `OnPlaceInventorInventoryItemEarly` | 1 | `DrMiller` **第一趟**：塞自己的货 + 数量上限守卫（已有 2 台写卡机就不再上） |
| `OnPlaceInventorInventoryItemLate` | 1 | `DrMiller` **第二趟**：原版上架后清场/覆盖（删多余背包、补报警器、按是否在工作室决定上不上冰箱） |
| `OnNewGameStarted` | 5 | `UpperRules`/`UpperPerks`/`UpperTraits`/`UpperTraitExtras`/`TestMode`，加 `ModEntry.OnNewGame` 转发 |
| `OnWakingUpEarly` / `OnWakingUpLate` | 1 / 1 | 海军远征结算必须在晨间常规结算前；`Morning` 的晨间补算必须在结算后 |
| `OnGoingSleepEarly` | 2 | `NavyContractor` 装备进仓、`KaiFixer` 收摊前善后 |
| `OnLeavingStoreEarly` | 1 | `NavyContractor`：关远征 UI、收装备，防"带着界面离店"丢东西 |
| `OnCreateTooltipEarly/Late` | — | tooltip 富文本前后两趟 |

**组合用法的三条规律**（最值得抄的部分）：

1. **`Early/Normal/Late/VeryLate` 四槽 = 一个四级优先队列。** 想"插在别人之前"不必抢 HarmonyPriority，直接挑更早的槽。所以 `OnGenerateCustomer*` 有 30 个订阅者也不打架——它们分布在四个槽里。
2. **`Early/Init/Late` 是"依赖就位程度"的三段**，不是随便分：Early 只碰**难度/配置**（此时物品、UI 都还没有）；Init 只碰**容器与缓存**（此时存档物品还没铺回场景）；Late 才碰**物品与一次性迁移**（`DirectoryMaster` 与场景物品都齐了）。`NavyContractor` 是范例：`ClearPending` 在 Init、`ApplyPending` 在 Late，成对拆开。
3. **`OnPlaceInventorInventoryItemEarly/Late` 是"前后夹住一个原版例程"**：Early 先放自己的，Late 在原版放完之后清场。既不用替换原版补货逻辑，又保证自己的货一定在列表里——比 Patch `StoreClientList.CreateInventor` 温和得多。

## 4. 数据与资源

### 4.1 静态数据：没有一行 JSON

全仓 **0 个 JSON、0 个 `TextAsset`、0 个 `Resources.Load`、0 处 `JsonUtility`**。唯一的字节读取是美术（`ArtLoader.cs:44` 的 `ModHelper.ReadModFileBytes("upper_levels", "Art/" + path + ".png")`）。数据一律是**编译进 DLL 的 C# 常量**，四种形状：

```csharp
// ① struct + Dictionary（物品表）—— UpperLevels\NewGoods.cs:10-21, 45-49
private struct Def { public int Value; public string[] Types; public string Audio; public int Kcal; public int Contraband; }
private static readonly Dictionary<string, Def> Defs = new Dictionary<string, Def>
{ { "ul_mil_biscuit", F(30, 500, "FOOD", "PROCESSED_FOOD") }, { "ul_regen_serum", D(1500, "glass", "MEDICAL", "RARE") },
// ② 并行分层表（同一批 id 的不同维度）—— NewGoods.cs:736, 793
public static readonly Dictionary<string, int> NavyArmsTiers = new Dictionary<string, int> { { "ul_navy_pistol", 1 }, ... };
public static readonly Dictionary<string, int> BattleContraband = new Dictionary<string, int> { { "ul_rail_rifle", 4 } };
// ③ 配置数组 —— UpperDifficulties.cs:106-116
private static readonly Spec[] Specs = new Spec[4] { new Spec { BasedOn = "cozy", Order = 1100, PropertyBase = 36000 } };
// ④ 步骤/选项元组 —— FreightLift.cs:37-77
private static readonly List<(string step, Option[] options)> Steps = new List<(string, Option[])> { ("checkpoint", new Option[3] { ... }) };
```

**取舍很明确**：数据不可热改、不可被第三方读取、加 260 个物品要重编译；换来零 IO、零解析、零 schema 漂移、编译期查错。单人维护的 DLC 级内容包，这是对的。

### 4.2 文本与美术：键名即标识，主题即目录

`Upper.L(key, args)` → `ModHelper.GetLocalized("upper_levels", key, args)`（`Upper.cs:83-86`）。每处 UI/物品名/flavor 都走它，等于把文案整层外置。反编译目录不含资源，故 `Localization/*` 的格式（推测：原版 mod 本地化表）。代价是键名散落（`battle.event.corpse`、`diff_restored_guess`、`scenario.soon.desc`…），没有集中清单。

美术目录为 `Art/themes/<lux|cyber|retro>/<name>.png`，主题目录找不到时回落 `Art/<name>.png`（`ArtLoader.cs:15`）。加载器双层记忆（`cache` 路径→Sprite、`failed` 短名单，失败不重试且只 warn 一次），主题切换=换目录前缀，cache key 含主题名，三套图可共存。

**加载时机：不是 `OnModAssetsLoaded`**——全仓 0 处该 hook。22 个 `ArtLoader.Get` 调用点全是**首次用到时才读**（换皮时、开面板时、tooltip 时）。

### 4.3 物品贴图另有通道

新物品不走 `ArtLoader`，走原版：`ItemDirectory.CreateEmptyItem(id).SetSpriteAndShapeFromMod("upper_levels", sprite)`，再 `SetGameItemType` / `EnableTag` / `AudioHelper.InitCartonCard`（`UpperItems.cs:35-43`）。即**物品图标由原版按 `upper_levels:<name>` 去包里找图**，只有"场景/UI 换皮"才需要自己写加载器。这个分工让 260+ 物品不用维护任何加载代码。

## 5. 状态与存档

### 5.1 两层存储，界限干净

| 层 | 载体 | 内容 | 例子 |
| --- | --- | --- | --- |
| 跨档偏好 | `PlayerPrefs` | 模式、主题、三个表现开关、三个玩法开关 | `upper_levels.mode`、`store_theme_v2`、`vanilla_ui` |
| 档内状态 | `ModHelper.Get/SetModData*` | 一切进度/计数/标记 | `run.difficulty`、`salon.visits`、`navy.suspicion`、`freight.lastDay` |

**从不改原版存档结构，不用 ES3，不写自己的文件。** 档里只有本来就在的东西：难度 id、网络升级、物品、地点访问状态。键空间是 `<域>.<字段>` 扁平命名，实测 **120+ 键**，前缀即域（`run.` `salon.` `crate.` `navy.` `organ.` `ice_*` `perfume.` `luxury.` `heist.` `battle.` `appraiser.` `newcomer.` `retire.` `revmed.` `richpat.` `morning.` `shelf.` `spring.` `pex.` `phone.` `line.` `commission.`）。

### 5.2 日戳模式：不写"每日重置"，只比较日期

```csharp
// UpperLevels.Social\Salon.cs:45-56  —— 沙龙每晚 3 次对话额度
public static int TimeLeft
{
    get
    {
        if (SocialState.TestSalonTonight) { return 3; }
        int num = ((ModHelper.GetModDataInt("upper_levels", "salon.time.day", -1) == SocialState.Today) ? ModHelper.GetModDataInt("upper_levels", "salon.time.used", 0) : 0);
        return Math.Max(0, 3 - num);
    }
}
```

消费时 `SpendTime()`（`Salon.cs:60-65`）把 `salon.time.day` 写成今天、`salon.time.used` 写成已用次数。`X.day` + `X.used` 成对写，读时"存的日期 ≠ 今天 → 当 0"。全仓所有每日/每周上限都是这个形状（`battle.day`/`battle.tries`、`crate.traits.open_day`/`open_count`、`navy.board.day`/`navy.board.ref`）。**好处**：不需要"过夜时清一遍"的钩子，也不会漏清。

### 5.3 不存随机种子——用确定性日程函数

```csharp
// UpperLevels.Professions\Battlefield.cs:326-341  —— 交火之夜由天数纯函数决定
public static bool IsFightNight(int day)
{
    int num = (day - 1) / 7;
    int num2 = (num * 37 + 11) % 7;
    int num3 = (num * 53 + 3) % 7;
    if (num3 == num2) { num3 = (num2 + 3) % 7; }
    int num4 = (day - 1) % 7;
    if (num4 != num2) { return num4 == num3; }
    return true;
}
```

`SocialState.IsFriday` 同理（`Today % 7 == 5`）。**同一存档重进游戏事件日一定一致**，无需持久化，也不会因读档时机错位。这是"要随机感但不要状态"的标准解。

### 5.4 访问计数：先用原版已有的槽

`salon.visits`、`battle.tries`、`navy.exp.runs`、`freight.lastDay`。但原版 `PlayerStore` 本身按地点 id 存了访问状态，作者优先用它——`Battlefield.cs:345` 判的是 `PlayerStore.Instance.GetAfterhourState("ul_battlefield").lastVisitDay != Today`。**先去原版找现成的槽，只有原版没记的才落自己的键**。

### 5.5 防坏档 + 版本迁移

四条机制，按强度排：

1. **读数一律带默认值**：`GetModDataInt(id, key, -1)` 这类签名让"键不存在"和"值是 0"永远区分得开。
2. **难度恢复三级 fallback**——最漂亮的一段防御（`UpperDifficulties.cs:238-276`，节选）：原版 `difficultyId` 被别的东西改掉时，先用 `run.difficulty` 的记忆值救回；记忆值也没有但 `run_started` 为真（确认这是上层区档），降级到 `upper_normal` 并弹 `diff_restored_guess` 提示"原始档位未知"；若 `Difficulty.Get(text) == null`（该难度已不存在）则**什么都不改**。三级都不满足时宁可不动存档。

```csharp
if (!Upper.HasStore) { return; }
PlayerStore instance = PlayerStore.Instance;
if (IsUpperId(instance.difficultyId)) { Remember(); return; }
string text = ModHelper.GetModData("upper_levels", "run.difficulty", "");
bool flag = false;
if (!IsUpperId(text))
{
    if (!ModHelper.GetModDataBool("upper_levels", "run_started", false)) { return; }
    text = UpperIdOf("normal");
    flag = true;                                     // ← 原档位未知，降级并如实告知
}
if (Difficulty.Get(text) == null) { return; }         // ← 该难度已不存在：什么都不改
string difficultyId = instance.difficultyId;
instance.difficultyId = text;
instance.isHardMode = Difficulty.IsHardOrAbove();
ModHelper.SetModData("upper_levels", "run.difficulty", text);
string text2 = Upper.L(flag ? "diff_restored_guess" : "diff_restored");
```

3. **一次性迁移 + 幂等标记**（`NavyExpedition.cs:611-638`）：已移除的雇佣兵团若还在外派，按价退款并打 `removed=true`，标记在就永不再跑。另两处物品级迁移在 `OnGameLoadedLate`：`MigrateLegacyMercCrates`、`NavyEquipmentFootprints.ApplyPending`。
4. **键改名当迁移**：`store_theme_v2`、`allow_wanted_v2`、`allow_aug_v2`。语义变了直接换键名，旧值自然失效——**不写迁移代码**，代价是偏好重置一次。对偏好数据这是最省事的正确解。

卸载侧没有专门迁移（档里没有私有结构，所以不需要），但注册类副作用都有反注册：`OnDisable` 51 行成对退订，地点用 `ModHelper.RemoveAfterhourLocations("upper_levels")`（`Salon.cs:77-80`）整体清空。

## 6. 性能

### 6.1 粗比例（58,709 非空行）

| 类别 | 文件 | 行数 | 占比 |
| --- | --- | --- | --- |
| UI（`UISkin` `MainMenuSkin` `NavyExpeditionUI` `LocationPanel` `TalkUI` `ReputationPanel` `StoreSkin` `PropSkins`…） | 21 | ≈ 9,939 | **17%** |
| 静态数据表（`NewGoods` `CrateItems` `BattleLoot` `OrganItems` `AppraiserItems` `MachineItems` `Navy*Def`…） | 31 | ≈ 8,257 | **14%** |
| 逻辑/剧情/经济/规则 | 余下 | ≈ 40,513 | **69%** |

三个最大文件全属同一职业：`NavyExpeditionUI` 3,956 + `NavyExpedition` 2,801 + `NavyGearBox` 2,731 ≈ 9.5k。**作者的模块粒度是"职业=垂直切片"**：一个职业 10–25 文件、1k–15k 行，自带数据表/UI/状态/迁移，切片之间只在 8 个 hook 点汇合。这就是 63k 行还能维护的原因——不是一个大架构，是 8 个互不引用的小模组拼在一张 hook 网里。

### 6.2 分帧、节流与缓存

`UISkin.Keeper` 是挂场景上的 `MonoBehaviour`，因为原版每次刷新都会把 UI 打回原样，它必须持续对抗。这成了唯一性能敏感点，处理得很克制（`UISkin.cs:93-125`）：`Update` 里 `next = Time.unscaledTime + 0.5f` 把全线压到 **2 Hz**；出异常则 `next = Time.unscaledTime + 5f` **退避 5 秒且不刷日志**；`++ticks % 20 == 0` 才 `Purge()` 清死引用。脏窗口队列再叠 0.25s 防抖 + 0.75s 二次扫描（`:548-598`），`lastScanned` 超 200 条直接 `Clear()`（`:606-609`）——用粗暴清表代替 LRU。

其余三条：`ModHelper.Delay` 全仓仅 **10 处**，全是 `0f`/`0.8f`/`8f` 的"等 UI 就绪、等一帧再刷新"，**没有一处用于加载**；缓存靠 `UISkin` 24 个静态字典、`ArtLoader.cache`、`StoreSkin.originalRenderers`+`originalImages`（换皮可逆的关键）、`Upper.IsActive` 的三元组记忆化（`Upper.cs:31-49`）；**没有**资源预加载、对象池、协程加载、异步——因为要加载的只有 PNG，且按需一张。

真正省下来的在 `StoreSkin.TryGetSkin`：换图前先比 `rect.width/height`，不一致就放弃（§8 范式二）。换皮退化成一次字典查表加一次赋值，不需要布局重算、不需要重建 UI。

## 7. 它是怎么避免"装上就崩"的

**① 顶层 `Safe()` 包裹每一个子系统。** `ModEntry` 21 个 `Register`、`ProfessionHub` 7 个职业 `Register`、各 hook 的每个转发，全走同一形状（`ModEntry.cs:168-182`）：`try { action(); } catch (Exception ex) { log.Error(what + " failed: " + ex); }`。一处炸掉只丢一个职业/一个注册项，其余照常——这是 267 个补丁还能进游戏的第一道保险。

**② 每个 hook 处理函数第一行早退。** `if (!Upper.IsActive || !Upper.HasStore) return;` 出现在几乎所有 handler 顶部，原版存档下整条链空转。

**③ 232 处 catch，且都带上下文 `LogError`。** 抽查 `UISkin`/`Battlefield`/`UpperDifficulties`/`NavyContractor`，模式统一为 `log.Error("具体动作 failed: " + ex)`。少数接近静默的（`ModItems.IsModItem` 的裸 `catch` → `return false`、`Upper.IsActive` 的 `catch` → `Compute()`）都是查询函数，属可接受边界。

**④ 版本探测式可选补丁——全仓仅 1 处，但形状值得抄：**

```csharp
// UpperLevels\UpperPerks.cs:350-372（节选，末行后的收尾括号省略）
[HarmonyPatch(typeof(StartingPerkIconLoader), "GetIcon")]
internal static class IconPatch
{
    private static bool Prepare()
    {
        try { return Probe(); }
        catch (Exception)
        {
            Debug.LogWarning((object)"[Upper Levels] StartingPerkIconLoader.GetIcon not found in this game version; perk icons skipped.");
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Probe()
    {
        return new Func<StartingPerk, Sprite>(StartingPerkIconLoader.GetIcon) != null;
    }
```

`Prepare()` 返回 false → 这一个补丁不挂，**不影响其余 266 个**。`NoInlining` 是关键：内联会让 JIT 在 `Prepare` 阶段就去解析缺失成员，探测本身抛异常。游戏版本一变最可能改名的是 `StartingPerkIconLoader`，所以只有这里做了防护——**其余 266 处直接引用原版成员，作者赌的是"其它 API 稳定"**。

**⑤ 资源缺失降级，绝不抛。** 三层：`ArtLoader` 失败入短名单且只 warn 一次 → 尺寸不符拒绝替换（`StoreSkin.cs:463-477`）→ `Skin()` 返回 null 时调用处 `?? original` 保留原图。

**⑥ `Upper.HasStore` 由四个 10 行小补丁维护**（`LoadGame` 的 Prefix 置 true、`StartNewGame` 同、`ResetData` 的 Postfix 置 false 并调 `UISkin.OnLeaveStore()`、场景创建 1 处），避免 `PlayerStore.Instance` 被隐式构造——"访问前先判 HasInstance"纪律的变体，且离店时连界面缓存一起拆。

**⑦ 把逃生口交给玩家。** 工坊页明写"遇到启动/切界面闪退，可先开启「使用原版界面」排查，并携带 `Player.log` 进群反馈"。四个开关（`vanilla_ui`/`vanilla_decor`/`decor_in_vanilla`/`use_vanilla_professions`）就是四条可远程指导的降级路径。对 2.7 万订阅、作者回不过来的模组，这是运维级设计。

**⑧ 原版隔离用"一行补丁"完成。** `VanillaStorylines.cs` 全文 37 行，四个 Prefix 全是同一句 `return !Upper.IsActive;`（`StoreClientManager.CheckStoryLineGeorgio/Nellis/OfficerLun/Serpico`）。配合 `ScenarioFilterPatch`（过滤剧本列表）、`DropMismatchedPerks`（切模式时清不兼容特性），兑现"关模式完全不影响原版游玩与存档"。

## 8. 可复用范式（5 个架构级）

### 范式一：区域 = 一个可存档的开关，其它都是它的装饰

不要新建场景/地图表示"新区域"。找一个**原版已会存进档、且能被全局唯一判定**的字段（这里是 `Difficulty.source`），把"我在不在新区域"压成一次字符串比较，让所有内容以它为总闸。

```csharp
// UpperLevels\UpperDifficulties.cs:180-205（节选：标题/副标题两个 lambda 省略为 ...）
Spec[] specs = Specs;
foreach (Spec spec in specs)
{
    Spec s = spec;
    string text = UpperIdOf(s.BasedOn);
    ById[text] = s;
    ModHelper.AddDifficulty("upper_levels", text, s.Order, (Func<string>)(() =>
    {
        ...
    }), (Func<string>)(() =>
    {
        ...
    }), (Action<DifficultyRules>)((DifficultyRules rules) =>
    {
        rules.propertyBasePrice = s.PropertyBase;
        rules.propertyPriceIncreasePerDay = s.PropertyPerDay;
        rules.propertyPriceIncreaseCap = s.PropertyCap;
        rules.tradeFavorRate = s.FavorRate;
    }), s.BasedOn, new Func<string>[1]
    {
        () => Upper.L("diff_note_start", 3, 900)
    }, (string)null);
}
```

注意 `s.BasedOn` 这一参：新难度**从原版难度派生**（`cozy/normal/hard/challenge` 各一份 `upper_` 版），原版平衡曲线仍被继承，作者只覆盖房产价格与交易好感率两项。配套是**记忆化判定**（`Upper.cs:31-49`）：只有 `PlayerStore` 引用、`difficultyId`、`isHardMode` 三者任一变化才重算，让 267 个补丁里每次 `Upper.IsActive` 都不昂贵。

**为什么是架构级**：它把"区域"从空间概念降维成布尔状态，于是存档、隔离、卸载、兼容四个难题同时消失。

### 范式二：场景重绘而非场景新建（换图、可逆、尺寸校验）

拿现有场景，按 **sprite 名字**做映射替换，记住原图以便还原，**尺寸不符就拒绝**。

```csharp
// UpperLevels\StoreSkin.cs:216-247（节选）
foreach (SpriteRenderer val in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
{
    if ((Object)(object)val == (Object)null) { continue; }
    Scene scene = ((Component)val).gameObject.scene;
    if (!scene.IsValid()) { continue; }
    Sprite sprite = val.sprite;
    bool flag = originalRenderers.TryGetValue(val, out var value);
    Sprite val2 = Original(flag, value, sprite);      // ← 已换过就取原图，避免二次换皮
    if ((Object)(object)val2 == (Object)null) { continue; }
    if (!TryGetSkin(val2, out var skin, out var hide))
    {
        if (flag) { val.sprite = val2; originalRenderers.Remove(val); }
    }
    else
    {
        originalRenderers[val] = val2;               // ← 先记原图
        val.sprite = (hide ? null : skin);           // ← 再换（hide 用置空实现）
    }
}
```

闸门在 `TryGetSkin`（`StoreSkin.cs:458-480`）：按名字查 `Replace`/`UpperOnly`/`Hide` 三张表拿目标图，**校验像素尺寸与原图一致才允许替换**，并用 `pivot: new Vector2(original.pivot.x / rect.width, original.pivot.y / rect.height)`、`pixelsPerUnit: original.pixelsPerUnit` 继承原参数；不通过就 `skin = null` 保留原版。`Restore()`（`:391-425`）逐项还原并清空两个字典，三套主题来回切不漏。

**为什么是架构级**：新区域最贵的从来不是场景，而是场景里每一个交互点（碰撞体、槽位、机器位置、UI 锚点）。换 sprite 把这些全部免费继承。

### 范式三：钩子槽位当优先队列 + 目录工厂注册

**不要用一个 hook 加优先级参数排序**（原版 hook 没有 priority）。把内容按"必须多早"分到四个槽，让槽位承担排序。

```csharp
// UpperLevels\ModEntry.cs:42-51, 67-77（节选：51 行订阅就是全模组的装配图）
ModHook.OnModItemDirectoryInit += ModItems.OnItemDirectoryInit;
ModHook.OnModItemDirectoryInit += StoryEntry.OnItemDirectoryInit;
ModHook.OnModItemDirectoryInit += ProfessionHub.OnItemDirectoryInit;
ModHook.OnGenerateCustomerEarly += ProfessionHub.OnGenerateCustomerEarly;
ModHook.OnGenerateCustomerEarly += StoryScheduler.OnGenerateCustomerEarly;
ModHook.OnGenerateCustomerNormal += FactionRegulars.OnGenerateCustomerNormal;
ModHook.OnGenerateCustomerLate += FactionRegulars.OnGenerateCustomerLate;
ModHook.OnPlaceInventorInventoryItemEarly += DrMiller.OnPlaceInventorInventoryEarly;
ModHook.OnPlaceInventorInventoryItemLate += DrMiller.OnPlaceInventorInventoryLate;
ModHook.OnGenerateCustomerLate += OrganOrders.OnGenerateCustomerLate;
ModHook.OnGenerateCustomerLate += RichPatient.OnGenerateCustomerLate;
ModHook.OnGenerateCustomerVeryLate += Act1.OnGenerateCustomerVeryLate;
ModHook.OnGenerateCustomerVeryLate += UpperRules.OnGenerateCustomerVeryLate;
```

增殖点是**物品用工厂注册，不预建实例**（`UpperItems.cs:22-33`）：`((Directory<GameItem>)directory).Set("ul_roster", (Func<GameItem>)CreateRoster)`，情报类则 `Set(Intel(f), () => CreateIntel(f))` 按派系批量生成。

**为什么是架构级**：它让"30 个功能都往同一条生成链里塞内容"从不产生冲突——槽位已经替你排好了先后。

### 范式四：面板自持重绘（ReversePatch 复用原版开窗）

往原版面板塞内容时最大的坑是"改完不刷新"和"关掉回不去"。用 `HarmonyReversePatch` 直接调原版私有方法，再自己管重开：

```csharp
// UpperLevels.Social\LocationPanel.cs:9-18, 144-161（节选）
[HarmonyPatch]
internal static class OpenPanelStub
{
    [HarmonyReversePatch(/*Could not decode attribute arguments.*/)]
    [HarmonyPatch(typeof(MapUIManager), "OpenLocationPanel")]
    public static void OpenLocationPanel(MapUIManager instance, AfterhourLocation location)
    {
        throw new NotImplementedException("Harmony reverse patch stub");
    }
}
// ...
public static void Refresh()
{
    MapUIManager instance = MapUIManager.Instance;
    if ((Object)(object)instance == (Object)null || instance.Current == null) { return; }
    try
    {
        OpenPanelStub.OpenLocationPanel(instance, instance.Current);   // ← 复用原版开窗
        CustomUIManager instance2 = CustomUIManager.Instance;
        CustomUIWindow window = ((instance2 != null) ? instance2.GetWindow("afterhour_location") : null);
        if (instance.Current.Source == "upper_levels")
        {
            UILayout.FitHeight(window, keepTop: true);                 // ← 内容变长后补高度
        }
        KeepOpen(window, instance.Current);
    }
    // 异常分支略：catch (Exception ex) → log.Error("Location panel refresh failed: " + ex)
}
```

`KeepOpen`（`:172-196`）把 `window.onClose` 包一层：关窗后若玩家还站在上层区地点上，就 `ModHelper.Delay(...,0f,...)` 一帧后重开；`gameClosing` 标志（由 `CloseLocationPanel` 的 Prefix/Finalizer 置位）保证真离店时不会把面板拉回来。

**为什么是架构级**：原版私有方法是最稳的"渲染入口"——它不校验业务状态，只负责把窗摆出来。ReversePatch 让你借它而不重写它。

### 范式五：日戳状态 + 确定性日程，不存种子

```csharp
// UpperLevels.Social\SocialState.cs:131-150（节选）
public static bool IsFriday
{
    get
    {
        if (Today % 7 != 5) { return Today == TestFridayDay; }
        return true;
    }
}

public static bool TalkedTonight(string faction)
{
    if (!TestSalonTonight)
    {
        return ModHelper.GetModDataInt(Id, "salon.talked." + faction, -1) == Today;
    }
    return false;
}
```

`X.day` 与 `X.value` 成对写，"存的日 ≠ 今天"即重置；周期事件用纯函数从 `dayCounter` 推出（§5.3）。

**为什么是架构级**：它把"每天要清哪些状态"从**过夜钩子里一长串清理代码**变成**读取时的一次比较**。漏清、重进档错位、迁移字段对不上——三类 bug 一次消失；而且 `TestFridayDay` 这种测试钩子天然能塞进来。

## 9. 一句话总结

> 做"整片新区域"，最该先想清楚的不是新场景怎么搭、内容有多少，而是**"玩家凭什么算进了这片区域，以及这个判定怎么被存档原样读回来"**——把这句话压成一次字符串比较（这里是 `Difficulty.source == "upper_levels"`），区域就从"一个世界"降级成"一个开关"：场景可以靠换皮白拿、客群可以靠改写原版客人获得、地点能用原版扩展点挂上、卸载与兼容自动成立；压不成一个开关，你就得同时维护两套世界，并永远在追"这一处忘了判模式"的 bug。

## 附：证据边界

- 引用代码逐字取自 `decomp_ws\3838944803`，行号为该文件实际行号；标「节选」处省略日志/通知/异常分支，以 `...` 或注释标明。
- **未能验证**（反编译目录只含 `.cs` 与 `.csproj`，无资源、无 manifest）：本地化表格式、`Art/` 实际文件数与目录深度、manifest 的 `Capabilities`（`inspect_mod.py` 从 IL 调用点判定"无明显高权 API，倾向零能力"，未过真实 `ModScanner`）、包内是否有 `README.md`（工坊描述称开局背包附带）。
- **未验证的运行态行为**：难度恢复三级 fallback 的实际触发率、`UISkin` 2 Hz 对抗在低端机的实测开销、267 处补丁在真实游戏版本上是否 100% 命中。
