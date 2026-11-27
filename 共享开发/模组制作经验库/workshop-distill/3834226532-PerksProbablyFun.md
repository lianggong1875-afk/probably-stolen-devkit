# 工坊 3834226532 架构蒸馏档案 —— 更多特质：可能是神人 / Perks: Probably Fun

> 工坊 id `3834226532`｜manifest ID **`traits_probably_fun`**（Harmony id、`ModData` 键、本地化命名空间、图标 key 前缀全用它，不是标题）｜历史 id **`deep_space_pawnshop`**（`Plugin.cs:12` 的 `LegacyModId`，旧档的 modData 键还在这个名字下，读旧写新）｜程序集 `TraitsProbablyFun.dll`｜`ModVersion 0.9.784`｜`GameVersions 0.46D`｜作者 ASUS｜4.1 万订阅｜manifest 写 `<Capabilities />` —— 源码侧核对通过：**0 处 `System.IO`、0 处外部反射、0 线程/协程库、0 原生库**，写文件只走 `ModHelper.WriteModDataFileText`
> 反编译源：`缓存文件夹\decomp_ws\3834226532`（159 个 `.cs` / 55,666 行，ILSpy 产物、零注释、变量名由反编译器生成、含 1,080 行 `//IL_` 噪声）
> 原版核对：`共享开发\reference\Assembly-CSharp\`（`ModHelper.cs:407-431`、`NewGamePerksUI.cs:240-257`、`SaveFiles.cs:365-420`、`MindReprogrammerHelper.cs:183-195`、`StartingPerk.cs:227-237`），本文凡断言原版行为都以它为准
> **为什么值得读**：93 条 perk 是这批标杆里最多的一个。它**守住了"没有效果类"**，但代价不是抽象的"设计税"，而是可数的**宽度税**：93 条 = 93 行 `AddPerk` + 108 个实现文件、每条自带补丁体和自己的门、每条都能被玩家局内换掉。另外两件事只有它有：① 把「专精（`StartingScenario`）」当**开局产业解锁器**用（5 个 `NetworkUpgrade.Unlock()` + 供货商电话置位）；② 给原版天赋界面外挂一个**50 槽预设管理器**（存/读/改名/删/预览/导出），且**不写一个 `System.IO`**。

---

## 0. 一句话结论

**它是"扁平注册表 + 每条自带补丁体 + 每个补丁体自己问门"，而不是"表 + 一个门"**：93 条 perk 在 `StartingOptions.Register()` 里是 93 行同构的 `AddPerk(...)`（`StartingOptions.cs:385-477`），但**效果**不在表里——每条 perk 有自己的文件、自己的 `[HarmonyPatch]`、自己第一行的 `if (!Active) return;`（74 条写成 `Active => StartingOptions.Has("dsp_x")` 属性、80 处；19 条内联 `Has()`）。所以**加一条 perk ≈ 加一个 id + 一档成本 + 一个新文件 + N 个补丁靶点**，而不是"加一个 slug + 一个成本 + 一个 `Has()` 分支"。它为此付出的代价是：422 个补丁点、2,107 个 `try`、2,092 个 `catch`、1,773 行日志，以及一条必须靠"局内换天赋"倒逼出来的架构约束（见 §1.4）。

---

## 1. 规模与结构

### 1.1 目录 → 行数 → 职责

单目录 `TraitsProbablyFun\`，158 个 `.cs`，无子目录；`Properties\AssemblyInfo.cs` 12 行。

| 组 | 文件 | 行数 | 占比 | 职责 |
| --- | ---: | ---: | ---: | --- |
| 特性实现 | 108 | 44,967 | **80.8%** | 每条 perk 一个文件（少数 2~5 个文件一条），补丁 + 效果 + 数据写在一起 |
| 预设管理器 | 5 | 2,999 | 5.4% | `PerkPresets`(831) 存取 / `PresetWindow`(2,037) 手搓窗口 / `PresetSettings`(100) 罗马数字开关 / `PresetPatches`(24) / `PresetNumeral`(7) |
| 注册与基座 | 10 | 2,729 | 4.9% | `StartingOptions`(1,407) 93 条注册 + 开局一次性应用 / `Plugin`(649) / `ModOptions`(188) / `ModData`(124) / `Loc`+`LocBridge`+`Txt`(232) / `PerkDefs`(80) / `StartOnlyPerks`(37) |
| 诊断与守卫 | 19 | 1,609 | 2.9% | `Diag*` / `*Diag` / `ItemLoadGuard`(172) / `LouToyBoxGuard`(188) / `PhoneUnstick`(66) / `DspRunner`(66) 协程宿主 |
| 跨模组兼容 | 4 | 1,275 | 2.3% | `CompatHydroHooch`(788) / `CompatBroadcast`(209) / `CompatUndercity`(156) / `MindReprogrammerCompat`(122) |
| 天赋界面 | 7 | 1,086 | 2.0% | `SlotHighlightFix`(308) / `PerkSearch`(249) 搜索框 / `ContextMenuWidthFix`(242) / `PerkPointSign`(119) / `PerkUIScale`(83) / `TraitIcons`(64) / `StartingPerkIconLoaderStartPatch`(21) |
| 文本与本地化 | 6 | 1,001 | 1.8% | `WaterLocaleFix`(440) / `LocaleMatchFix`(181) / `IntelSlipStyle`(124) / `TrashcanText`(118) / `NewWorldDialogueRename`(115) / `WaterLocaleTicker`(23) |
| **合计** | **159** | **55,666** | 100% | |

- **规模形状**：中位数 **186 行**；>800 行 18 个、>1,400 行 6 个；最长的 3 个恰好是三种不同东西——`HydroQuestion` 5,268（**一条 perk 就是一个机器系统**，193 个方法，配 `HydroMachineCode`(1,050) + `HydroNutrientTablet`(27) + `HydroQualityGuard`(20)）、`PresetWindow` 2,037（**纯 UI**）、`SharedCigarette` 1,957（一条 perk 的交易/声望/右键菜单全链）。
- **"一条 perk 一个文件"成立但不严格**：93 条 id ↔ 108 个实现文件。跨文件的（`Active` 门在多个文件里各写一次，说明同一条 perk 被拆成几段）：`dsp_no_trash` 3 处、`dsp_blood_donor` / `dsp_newspaper_recipe` / `dsp_scav_drone` / `dsp_theyre_millions` 各 2 处。一条 perk 摊到多文件最多的是 `dsp_around_world_80`（`AroundTheWorld80` + `Intro` + `Story` + `Week` 共 667 行）与 `dsp_cracker_start` 专精（`CrackerScenario` 1,049 + `CrackerBuddy` 911）。
- **多 perk 挤一个文件**：`NewPerks`(1,154，17 个补丁，6 条 perk 的开局账：`dsp_no_questions`/`dsp_mortgage`/`dsp_wild_favor`/`dsp_self_built`/`dsp_cellar`/`dsp_no_trash`)、`Trait34`(898，`dsp_corner_market`+`dsp_labor_cost`+`dsp_renovation`)、`PerkPatches`(470，`dsp_tacit_understanding`+`dsp_ecommerce`)。**这三个文件是"93 条还能守住无效果类"的缓冲垫**——真正需要跨补丁共享状态的那几条，被塞进了共用文件而不是新建抽象。

### 1.2 成本与槽位：93 条的真实分配

| 成本 | −3 | −2 | −1 | 0 | +1 | +2 | +3 | +4 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 条数 | 1 | 6 | 13 | **30** | **28** | 9 | 4 | 2 |

- 三态用原版枚举裸强转：`(StartingPerkType)0` 正面 / `1` 负面 / `2` 中性（`StartingOptions.cs:385-477` 全篇），**没有自己的 Tone 枚举**。
- **只有 4 条动 `extraPerkSlots`（= 原版 `StartingPerk.maxSlot`）**：`dsp_cyberpsychosis` +3、`dsp_paid_training` +3、`dsp_startrak` −3、`dsp_career_advice` −1（`StartingOptions.cs:407/458/475/477`）。原版预算是 **≤3 点 + (4 + ΣmaxSlot) 个槽**（`NewGamePerksUI.cs:240-257`），所以这三条是"用槽位换点数"的另一条轴——**不是倍率、不是互斥**。
- 互斥全靠原版 `incompatiblePerks` 数组，**没有自建互斥图**：典型 `dsp_mortgage` ↔ {`dsp_self_built`, `prime_real_estate`, `bad_lease`}、`dsp_startrak` ↔ 其余 92 条（`StartrakIncompatibles()` 运行时拼 `PerkIds` + 48 条原版 id，`StartingOptions.cs:499-510`）。唯一一条"互斥条件写在别处"的是 `dsp_poet_admirer` 前置原版 `secret_admirer`（`HawaiiTraining.cs:58` 的 `Prerequisites`）。

### 1.3 冻结副本 / 代码生成 / 重复

- **没有冻结副本**：最长行只有 432 字符（`ZeroCostShopping.cs` 的一条字符串），全库没有内联原版物品表/价格表；一切原版数据运行期现读。158 个文件 md5 无重复。**这与 `3828971731` 的两份 19,381 字符内联 TSV 是相反的选择**，也是它能跟 0.46D 小版本走的原因。
- **没有代码生成痕迹**：无 `TargetMethods()`、无 `MethodInfo` 批量循环挂补丁、无模板生成器；93 条是**手写 93 行**（对，就是复制粘贴 93 次，$9$3 行每行一个）。唯一的"生成"是 `AddPerk` 这个私有壳（`:488-492`）把 10 个参数压成 6 个。
- **重复的是脚手架，不是数据**：`try/catch + Debug.LogWarning` 在 108 个实现文件里模式一致；`Active =>` 属性模板 80 处；`new ItemFeature((FeatureType)(flag?4:5)) + SetValueModifier` 7 处（`DoubleMinded:214`、`OpeningBonus:116`、`SharedCigarette:515`、`ThousandMiles:348`、`Vampire:428`、`Vanessa:599`、`NewspaperRecipe:249`）。

### 1.4 55,666 行的分布真相：属于哪种

**它既不是 `PC expansion` 那种"60% 事务脚手架"，也不是 `GoFishing` 那种"单文件 24.6%"。它是第三种：宽度税 + 每条自带的防御脚手架。** 逐层剥：

| 层 | 行数 | 占 55,666 | 依据 |
| --- | ---: | ---: | --- |
| 纯括号行（ILSpy Allman） | 22,961 | **41.3%** | `grep -c '^\s*[{}]\s*$'` |
| ILSpy `//IL_xxxx` 噪声 | 1,080 | 1.9% | |
| 空行 + `using` | 3,609 | 6.5% | |
| **语句行** | **28,016** | **50.3%** | |
| 其中 `try` + `catch`（2,107 + 2,092） | 4,199 | 7.5%（语句行的 15.0%） | 一份 `try` 对 **26 行代码** |
| 其中 `Debug.Log*` | 1,773 | 3.2%（语句行的 6.3%） | 946 条是 `LogWarning` |
| **≈ 真逻辑** | **≈ 22,000** | **≈ 39%** | 剩下的 `if` 4,126 / `!= null` 2,056 也大半是守卫 |

一句话：**4 成行数是括号，5 成是语句，其中 15% 是 `try/catch`、6% 是日志，真正干活的不到 4 成**。跟 `PC expansion` 的差别是它没有事务/票据栈；跟 `GoFishing` 的差别是它没有单点巨兽（最大单文件 9.5%，且是"一条 perk 真做成机器系统"而非复制粘贴）。**要抄它的组织方式，别抄它的防御密度。**

---

## 2. 它怎么做这件事

### 2.1 93 条 perk 怎么组织：一张"注册表" + 93 个"补丁体文件"

- **表只有一张，且只装元数据**：`StartingOptions.PerkIds`（`string[93]`，`StartingOptions.cs:236-248`）+ `Register()` 里 93 行 `AddPerk`（`:385-477`）。元数据只有 6 项：`id / cost / type / incompatible[] / description(可覆盖) / extraPerkSlots`（`:488`）。名称与描述**不进代码**，`Txt.Get(id+"_name")` 懒取（`:491`）。
- **效果不在表里，在文件里**。全库 **392 个特性式 `[HarmonyPatch]` + 30 个手动 `Harmony.Patch` = 422 个补丁点**，落到 **257 个唯一（类型, 方法）靶点**；补丁方法 **Prefix 161 / Postfix 272 / Finalizer 14 / Transpiler 0**。平均 **每条 perk 4.5 个靶点**。
- **门就是 `StartingOptions.Has(id)`**（`StartingOptions.cs:286-305`）→ `ModHelper.IsStartingPerkActive` → 原版 `StartingPerk.IsPerkActive` 线性扫 `PlayerStore.Instance.startingPerks`（`StartingPerk.cs:227-237`）。
- **为什么必须是"每个补丁自己问门"，而不能像 `3828949943` 那样拼位掩码？** 因为 93 条全部以 `mindEditable: true` 注册（`StartingOptions.cs:491` 最后一位），进原版 `MindReprogrammerHelper.GetEditablePerks()` 名单（`MindReprogrammerHelper.cs:183-195`）——**玩家能局内用心灵重编程器换掉任何一条**。所以效果不能"开局结算一次写进字段"，只能在被调用那一刻问。**这是全库最重要的一条架构约束，其它一切（422 个靶点、2,107 个 try）都是它的下游。**
- **两种时机**：① 持续型——补丁入口 `if (!Active) return;`；② 开局一次性——集中在 `StartingOptions.OnNewGame(string scenarioId)`（`:553-702`）的一条 **29 个 `if (Has(...))` 的直链**里（发道具、改房租、装钩子、改垃圾桶……）。第 ② 类必须自己做幂等，手段统一是"`ModData` 记天/标记"（例：`dsp_trust_fund_last_day`、`dsp_hawaii_injected`、`dsp_wild_deed_done`）。

### 2.2 "2 个专精"是什么：原版 `StartingScenario`，不是自建系统

两个 `ModHelper.AddStartingScenario`（`StartingOptions.cs:368-384`）：

| 专精 | id | 开局补给 | `skipIntro` | onStart |
| --- | --- | --- | --- | --- |
| Distributor 经销商 | `dsp_distributor_start` | 5 条供应说明 | false | `StartingOptions.OnScenarioStart` |
| Cracker 破解者 | `dsp_cracker_start` | 6 条供应说明 | **true** | `CrackerScenario.OnStart` |

**它与 93 条 perk 的关系是"正交"，不是互斥、不是前置、不是倍率**——证据：`AddStartingScenario` 与 `AddStartingPerk` 是两套原版入口，专精只在 3 处被读（`FreeSamplePatch.cs:13` 决定送不送样品、`StartingOptions.cs:1238` 决定重开档要不要重放、`IsDistributorScenario` 自己），**93 条 perk 的门一次都没读专精**。

**简介那 5 个"解锁"= 直接调原版 `NetworkUpgrade.Unlock()`，没有改任何解锁条件**（`StartingOptions.cs:923-949`）：

```csharp
if (ScenarioActive) {
    foreach (string text in UnlockedUpgrades) {              // BREWER / CHEMIST / PHARMA / MINER / SHOWCASE_I
        if (instance.networkUpgrade.TryGetValue(text, out var value) && value != null) {
            if (value.state != 2) { value.Unlock(); EnsureSupplierCardInStorage(text); }
        } else Debug.LogWarning("[DSP] network upgrade not found: " + text);
    }
    MarkSuppliersAsAlreadyIntroduced();                      // 4 个供货商电话置为"已介绍"
}
```

对应简介：`BREWER`=Red Imp Brewery（红魔鬼酿酒厂）、`CHEMIST`=Lower Levels Chemist（下城区化学家）、`PHARMA`=Upper Level Pharmacist（上层区药商）、`MINER`=GP Mining Coop（GP 采矿合作社）、`SHOWCASE_I`=Showcase Expansion I。**"解锁"= 把 vanilla 的升级项置为已购状态（`state==2`）+ 补一张供货商名片进仓库 + 把电话从"未认识"推进到"已介绍"（`StorePhoneClient.phoneState==3`），三件事缺一个玩家就看不到内容**——这是抄这套时最容易漏的地方。触发点有三处，保证老档也吃到：`OnScenarioStart`、`OnNewGame`、`ModHook.OnGameLoaded*` → `Plugin.cs:196/638`。

专精还给：`playerCash = 200`（直接写字段）、1 箱啤酒（8 瓶）、8 份梦尘、8 块原矿、6 支粉色注射器、6 份熔炼废料、`storage_bay`。**注意 `IsDistributorScenario()` 有"档内 vs 静态"两个来源**（`:`348-363`）：先读 `PlayerStore.startScenarioId`，读不到才回落 `ModHelper.IsStartingScenario` 或 modData 标记——因为它要在"新游戏界面"（还没有 `PlayerStore`）和"档内"两种上下文里都能答。

### 2.3 50 槽预设管理器：怎么存、怎么读、怎么导出、怎么保证一致性

| 问题 | 答案 | 证据 |
| --- | --- | --- |
| 多少槽 | 50 = **5 行/页 × 10 页** | `PerkPresets.cs:30-34`、`PresetWindow.cs:451-482` |
| 存在哪 | **原版存档索引 `saves_index.es3`**（`SaveFiles.IndexSave`），不是 `ModData`、不是存档内、不是独立文件 | `PerkPresets.cs:335/356/367/432/448`；`SaveFiles.cs:365-420` |
| 键 | `dsp_preset_<n>`（逗号分隔 id 串）、`dsp_preset_name_<n>`、`dsp_preset_last`、`dsp_preset_seeded_v2` | `PerkPresets.cs:36-64` |
| 作用域 | **全局跨存档**（索引文件是账号级），所以"预设"跟档无关、跟人走 | 同上 |
| 存什么 | 只存 `id` 列表（`string.Join(",", ids)`），不存槽位/成本/图标 | `:515` |
| 三层内置样本 | 首次进界面种 3 套（感谢游玩 / 黑市走私 / 夜间拾荒），用 `dsp_preset_seeded_v2` 标记只种一次，且**只填空槽** | `:42-54`、`:455-484` |
| 导出格式 | **`.txt` 纯文本**，写进 `ModData/traits_probably_fun/dsp_presets/presets_<yyyyMMdd-HHmmss>.txt`；同时把当前槽的 id 串**塞进剪贴板** | `:730-802` |
| 导入 | **没有**。只出不进（这是缺口，见 §4.7） | 全库无 `Import` |
| 改名怎么输入 | 复用原版 `LabelerBlueUIManager.OpenUI(初值, Action<string>)` —— 不自己造输入框 | `PerkPresets.cs:684-728` |
| 预览 | 按槽内 id 查 `PerkDefs.Find`，逐枚 clone 原版槽位图标 + 自绘 tooltip，重建前先 `Destroy` 上一批 | `PresetWindow.cs:1631-1795` |

**"载入预设后原版状态一致"靠四道校验 + 一次原版 Rebuild**（`PerkPresets.cs:526-643`）：

1. **查不到就跳过，不硬塞**：id 找不到（模组卸载/改名）→ 记进"缺 N 个"，不中断；全部查不到 → 报"没有能用的特质，**没有改动当前选择**"。
2. **去重**：同一 id 只加一次（`:558-570`）。
3. **互斥检查**：两两比对 `incompatiblePerks`，命中就**整份放弃**并报"预设里有互斥特质"（`:577-589`）。
4. **原版限制前置**：`NewGamePerksUI.firstRun`（第一局不许选特质）先拦（`:590-601`）。
5. **整体替换而不是追加**：`startingPerks.Clear()` 后逐条 `Add`（`:602-606`），然后反射调原版 `NewGamePerksUI.Rebuild()`（`:608-615`）——**界面的点数/槽位/锁定态全由原版重算，它一个数都不自己算**。最后只做一次提醒：Σcost > 3 点就警告"继续键会锁住"。

### 2.4 它怎么改原版数值：三条路，没有一条是"改原版配置"

| 路 | 手法 | 用在哪 | 次数 |
| --- | --- | --- | --- |
| **A. Postfix 现算** | `GameItem.GetNegociatedValue` Postfix 改 `__result`；`NetworkUpgrade.GetCost`、`StoreService.UpdateCost`、`PlayerStore.GetRentValue`、`GetStoreBuyPrice`、`StoreClient.SetBudget` 同类 | 烟草税、报纸摊、副产品批发、装修/人力成本 | 3 个问价 Postfix + 一批改返回值 Postfix |
| **B. 造一条原版 `ItemFeature` 让原版自己算** | `new ItemFeature((FeatureType)4/5)` → `SetValueModifier(±n)` → `AddItemFeature`，**价格由原版自己求和**，玩家在物品详情里看得见（`SetPublicDisplay`） | 有烟同享、多心、开张红利、千里之行、吸血鬼、梵妮莎、报刊配方 | **7** |
| **C. 直接写原版字段** | `playerCash`、`StoreClient.budget`、`StoreReputation`、物品 tag（`DSP_WHOLESALE_PCT_INT` 等 20 处 `EnableTag`） | 信托、经销商开局、囤货居奇、脏货/违禁改造 | — |
| **D. 极其少见的隐藏 feature** | 反射私有 `ItemFeature.valueModifier / preExposeValueModifier / usePreExposeValue` 造一条"看不见的 +x%" | 只有 `dsp_cellar`（窖藏）两处：`NewPerks.cs:1066-1078`（陈化增值）、`:1093-1100`（把酒的品质修正并入底价后清零） | **2** |

**与两份参照的关键差别**：`3828971731` 是"找到原版那条 feature 改它的 `valueModifier`（tips 15→20）"；`3828949943` 是"完全不碰 `valueModifier`，全靠 Postfix 现算"；**这一份走的是中间那条：自己新建 feature 交给原版管线**——好处是零 Postfix、价格在 UI 里自洽、卸载后 feature 跟着物品留着最多算个多余加成；坏处是**`valueModifier` 一旦要"隐藏"就必须反射私有字段**（public 只有 `SetValueModifier`，会写显示）。9 处用公开 setter、2 处用反射，说明作者自己也知道那条边界在哪。

### 2.5 Patch 靶点表（按域归并；★ = 命中我们列的已知热点）

| 域 | 原版方法（次数） | 手法 | 目的 | 风险 |
| --- | --- | --- | --- | --- |
| **日结算** | `PlayerStore.BeginDay`（**27 处 / 25 文件**）、`EndNight`（**8 处**）★ | Postfix 为主，3 个 Prefix | 每日发钱/发酒/发种子/扣租/收摊 | **全库最热**：25 个 Postfix 全挂在同一天开始，顺序只由 9 处 `HarmonyPriority` 局部约束（`BeerHallOrator` 200 / `BloodDonorCommissary` 600 / 3 处 0），**其余靠加载顺序**；两条 perk 同时改 `playerCash` 时的先后不可控 |
| 问价 | `GameItem.GetNegociatedValue`（**4**）★ | 3 Postfix + **1 Prefix→false** | 改成交价 | 见 §4.2：那条 Prefix 会**递归重入** |
| 交易 | `PlayerStore.SellItem`(5) / `BuyItem`(3) / `GetNextClient`(4) / `DismissCurrentClient`(7) / `AddDirectSellingItemToTable`(3) / `TryAddDirectSellingItemToTable`(3) / `SetupWaitingClient`(3) / `OnItemBought` / `GetWholesalePrice` | Postfix 记账、Prefix 拦 | 囤货、副产品、批发、有烟同享 | 客人链最脆，7 个 `DismissCurrentClient` |
| 客人生成 | `StoreClientManager.GetNormalCustomerCount`(6) / `PickClient`(4) / `AddClient`(4) / `HandleAlwayClient`(2) / `HandleShowCaseSecurity`(2) / `IsClientBlockedByPopCulture`；`StoreClientList.CreateInspectionClient`(3) / `CreateLandlordClient` / `CreateLandlordVendingMachineClient` | Postfix 加预算/改数量、Prefix 拦 | 挑客、安保、房东纠纷 | **没有**挂 `StoreClientListDict.CreateStoreClient`（只调用它建客）✅ |
| 声望 | `StoreReputation.OnItemTraded`(6) / `ModReputation`(5) / `ModSecReputation`(2) | Postfix 加成 | 香烟/共情类 perk | 与声望类模组正面重叠 |
| 鉴定/违禁 | `ContrabandHelper.StartInspection`(4) / `InitContrabandItem`(2)；`StolenHelper.InitStolenItem` / `UpdateStolenItem`(2) | **Prefix→false 直接不打脏标** | "没有垃圾"、"不干不净" | 改的是"物品是否脏"的根，跨模组语义冲突 |
| 天赋界面 | `NewGamePerksUI.Open`(3) / `CanStart`(2)；`PerkUIController.CanStart`；`StartingPerkElement.SetTooltipContent`；反射 `frame` / `firstRun` / `Rebuild` | Postfix 挂按钮/搜索框 | 50 槽预设、搜索、点数符号、UI 放大 | 反射原版私有静态字段，官方改布局即失效 |
| **每帧** | `StoreUIManager.Update`（**2 处**）★、`StoreUIManager.EnterMagnifyingMode` / `ToggleMagnifyZoom` | Postfix | 耐心上限计时、自建槽位高亮清扫、老花镜 | **两条都在 `Active` 之前先跑无条件逻辑**（`PatienceLimit.cs:12-19` 先 `RepairStuckSecurityState()`；`SlotHighlightFix.cs:26-33` 先 `TickWhenNotDragging()`）→ 见 §4.1 |
| 健康 | `HealthData.ReceiveMinorWound` / `ConsumeNarcotic` / `OnSleep`(2) | Prefix/Postfix | 狂战士、成瘾 | 低 |
| 机器/流体 | `WineHelper.StartFermentation`(3) / `GetWaterFeature` / `MachineFurnace.Furnace`(2) / `WaterHelper.AddWater`（显式 7 参签名） | Postfix 加速/换液 | 酿酒、窖藏、万能液、水培 | 与流体类模组重叠高 |
| 存档/加载 | `Directory<GameItem>.Create(string)`（`ItemLoadGuard`）★、`PlayerStore.StartNewGame`(4) | Prefix 兜底返回空壳 | 存档里有"别人模组的物品/已被删的物品"时不炸 | 全局工厂口，改错会让所有物品加载走空壳 |
| 其它 | `EndOfDayUIManager.InitUI`(4)、`ItemContextHandler.OnEventPress`(3)/`TriggerButton`(2)、`ItemMouseDoubleClickHandler.*`(4)、`BargainUIManager.ComputeDirectionalMultiplier`、`StoreEventManager.GetActiveEventOnDay`/`ActivateEvent`、`StartingPerkContent.HandleNewGamePerk(BeforeItems)`、`ShowcaseHelper.HasMerchandise`、`PhoneUIManager.StartPhoneDialog`(3) | 混合 | 右键菜单行、砍价方向、事件、开局发道具 | 中 |

**热点逐条核对**：`GameItem.IsTag` **未挂**（只作为读调用，20 处 `EnableTag` 是写物品 tag 不是改判据）；`GetCorrectBaseValue` **0 命中**；`SpriteDict.GetSprite` **未挂**（只调 `GetCaseEnjoyerSpriteName` / `GetRandomLowerLevelSpriteName`）；`StoreClientListDict.CreateStoreClient` **未挂**；`StoreUIManager.Update` **挂了 2 处** ❌；`PlayerStore.EndNight` **挂了 8 处** ❌。**Transpiler 0 个、`TargetMethods` 0 个**——这是它比另外两份参照更"干净"的地方。

### 2.6 一条 perk 从定义到生效：`dsp_trust_fund`（信托基金，54 行，全库最短的完整链）

1. **定义**：`AddPerk("dsp_trust_fund", 1, (StartingPerkType)0)`（`StartingOptions.cs:462`）；id 同时在 `PerkIds`（`:236-248`）里，供"当前有几个特质"统计（`:328-346`）与"夏威夷之旅"抽签池（`HawaiiTraining.cs:39-53`）。
2. **注册**（`OnEnable` 一次，`Plugin.cs:47`）→ `AddPerk` → `ModHelper.AddStartingPerk("traits_probably_fun", id, 1, POSITIVE, ()=>Txt.Get(id+"_name"), ()=>Txt.Get(id+"_description"), "traits_probably_fun:dsp_trust_fund", null, 0, mindEditable:true)`（`:488-492`）→ 原版 `StartingPerkList.RegisterModPerk`（`ModHelper.cs:407-431`）。
3. **玩家在天赋界面买下** → `NewGameData.startingPerks` → `PlayerStore.startingPerks`（写盘由原版负责，模组零改动）。
4. **门**：`TrustFund.cs:53` 的 `Active => StartingOptions.Has("dsp_trust_fund")` → `ModHelper.IsStartingPerkActive` → 原版线性扫（`StartingPerk.cs:227-237`）。
5. **触发**：`[HarmonyPatch(typeof(PlayerStore), "BeginDay")]` Postfix（`TrustFund.cs:9-12`）。
6. **效果**：`StoreStation.Instance.dayCounter % 7 == 4` 且 `ModData.GetInt("dsp_trust_fund_last_day",-1) != dayCounter` → `PlayerStore.ModCash(50, false)` → 写 `dsp_trust_fund_last_day` 防重 → `AddNightLog(Txt.Fmt(...))` + `Debug.Log`（`:27-40`）。
7. **存档**：只多一个 `int` 键；卸载后原版照常跑，只少一笔周四入账。
8. **局内换**：玩家用心灵重编程器摘掉它 → 下一次 `BeginDay` 第 4 步返回 false → 不再入账；**没有任何"卸载补偿"逻辑**（这是 93 条的共同约定：效果必须"可逆到不再发生"，而不需要"回滚已发生"）。

---

## 3. 关键代码摘录

**① 93 行的表 + 一个私有壳（元数据全在这 6 个字段里）**（`TraitsProbablyFun\StartingOptions.cs:385-397`、`:488-492`）

```csharp
AddPerk("dsp_wanderlust", 0, (StartingPerkType)0);
AddPerk("dsp_smelting", 2, (StartingPerkType)0);
AddPerk("dsp_ecommerce", 0, (StartingPerkType)2);
AddPerk("dsp_built_on_dump", 0, (StartingPerkType)2, new string[2] { "dsp_no_trash", "sawyer_crew" });
AddPerk("dsp_no_trash", 2, (StartingPerkType)0, new string[1] { "dsp_built_on_dump" });
AddPerk("dsp_mortgage", -3, (StartingPerkType)2, new string[3] { "dsp_self_built", "prime_real_estate", "bad_lease" });
AddPerk("dsp_cyberpsychosis", 0, (StartingPerkType)2, null, null, 3);          // +3 槽位
AddPerk("dsp_startrak", 3, (StartingPerkType)0, StartrakIncompatibles(), null, -3); // −3 槽位
// …共 93 行
private static void AddPerk(string id, int cost, StartingPerkType type, string[] incompatible = null,
                            Func<string> description = null, int extraPerkSlots = 0)
{
    ModHelper.AddStartingPerk("traits_probably_fun", id, cost, type,
        () => Txt.Get(id + "_name"), description ?? (() => Txt.Get(id + "_description")),
        IconKey(id), incompatible, extraPerkSlots, true);       // 最后一位 = mindEditable
}
```

**② 专精的"解锁"就是调原版 Unlock（含供货商电话置位）**（`TraitsProbablyFun\StartingOptions.cs:923-967`，节选）

```csharp
public static void EnsureUnlocks() {
    PlayerStore instance = PlayerStore.Instance;
    if (instance?.networkUpgrade == null) return;
    if (ScenarioActive) {
        foreach (string text in UnlockedUpgrades) {          // BREWER/CHEMIST/PHARMA/MINER/SHOWCASE_I
            if (instance.networkUpgrade.TryGetValue(text, out var value) && value != null) {
                if (value.state != 2) { value.Unlock(); EnsureSupplierCardInStorage(text); }
            } else Debug.LogWarning("[DSP] network upgrade not found: " + text);
        }
        MarkSuppliersAsAlreadyIntroduced();                  // 4 个供货商 phoneState → 已介绍
    }
    if (Has("dsp_ecommerce") && instance.networkUpgrade.TryGetValue("MARKETPLACE_REQUESTS", out var v) && v != null) {
        if (v.state != 2) v.Unlock();
        if (instance.marketplaceUnlockedDay < 0 && StoreStation.Instance != null)
            instance.marketplaceUnlockedDay = StoreStation.Instance.dayCounter;
    }
}
```

**③ 最短的完整 perk：门 + 触发 + 幂等 + 提示（54 行全在这里）**（`TraitsProbablyFun\TrustFund.cs:9-46`、`:53`）

```csharp
[HarmonyPatch(typeof(PlayerStore), "BeginDay")]
internal static class BeginDayPatch {
    private static void Postfix() {
        if (!Active) return;                                  // ← 全库统一的第一行
        try {
            StoreStation station = StoreStation.Instance; PlayerStore store = PlayerStore.Instance;
            if (station == null || store == null) return;
            int day = station.dayCounter;
            if (day % 7 != 4 || ModData.GetInt("dsp_trust_fund_last_day", -1) == day) return;
            store.ModCash(50, false);                          // ← 走原版 ModCash，不写 playerCash
            ModData.SetInt("dsp_trust_fund_last_day", day);    // ← 幂等标记
            Debug.Log("[DSP] 信托：周四入账 +50 信用点（第 " + day + " 天）");
            try { store.AddNightLog(Txt.Fmt("dsp_trust_fund_paid", 50), "5c6f46"); } catch { }
        } catch (Exception ex) { Debug.LogWarning("[DSP] 信托：发放失败 " + ex.Message); }
    }
}
internal static bool Active => StartingOptions.Has("dsp_trust_fund");
```

**④ 预设的存 / 取 / 导出（存：只存 id 串）**（`TraitsProbablyFun\PerkPresets.cs:491-524`）

```csharp
internal static void SaveSlot(int slot) {
    try {
        List<StartingPerk> list = NewGameData.Instance?.startingPerks;
        if (list == null) { Say("当前没有可选中的特质列表"); return; }
        List<string> ids = new List<string>();
        foreach (StartingPerk item in list)
            if (item != null && !string.IsNullOrEmpty(item.id)) ids.Add(item.id);
        if (ids.Count == 0) { Say("当前没有选中任何特质，未覆盖「" + NameOf(slot) + "」"); return; }
        SaveFiles.IndexSave<string>(PresetKey(slot), string.Join(",", ids));   // saves_index.es3
        TouchSlot(slot);                                                       // dsp_preset_last
        Say("已存到 " + NumberOf(slot) + "「" + NameOf(slot) + "」：" + ids.Count + " 个特质");
    } catch (Exception ex) { Debug.LogWarning("[DSP] 开局预设：存槽失败 " + ex.Message); }
}
```

**⑤ 预设的取：四道校验 + 整体替换 + 交给原版 Rebuild**（`PerkPresets.cs:577-606`、`:632-637`，省略查表/去重段）

```csharp
for (int j = 0; j < list.Count; j++)                       // ① 互斥两两比对，命中即整份放弃
    for (int k = j + 1; k < list.Count; k++) {
        StartingPerk a = list[j], b = list[k];
        if ((a.incompatiblePerks != null && a.incompatiblePerks.Contains(b.id))
         || (b.incompatiblePerks != null && b.incompatiblePerks.Contains(a.id))) {
            Say("预设里有互斥特质：" + a.id + " ↔ " + b.id); return;
        }
    }
try { if (ModReflect.GetStatic(typeof(NewGamePerksUI), "firstRun") is bool f && f) {   // ② 原版限制
        Say("第一局不能选开局特性（原版限制），先打完一局再来"); return; } } catch { }
NewGameData.Instance.startingPerks.Clear();                 // ③ 整体替换，不追加
foreach (StartingPerk item2 in list) NewGameData.Instance.startingPerks.Add(item2);
TouchSlot(slot);
try { ModReflect.CallStatic(typeof(NewGamePerksUI), "Rebuild", Array.Empty<object>()); }  // ④ 原版重算
catch (Exception ex) { Debug.LogWarning("[DSP] 开局预设：刷新界面失败 " + ex.Message); }
// …
if (num > 3) text3 += "；⚠ 共 " + num + " 点，超过 3 点，继续键会锁住";      // 只提醒，不阻断
```

**⑥ 导出：写 `.txt` 走合规通道 + 顺手写剪贴板（没有任何 System.IO）**（`PerkPresets.cs:734-760`、`:781-795`）

```csharp
StringBuilder sb = new StringBuilder();
sb.AppendLine("# Probably Stolen — starting perk presets (mod: traits_probably_fun)");
sb.AppendLine("# exported " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  slots=" + 50);
sb.AppendLine("# format: [slot] name | id,id,id");
int n = 0, last = LastSlot(); string cur = null;
for (int i = 1; i <= 50; i++) {
    string ids = IdsOf(i);
    if (string.IsNullOrEmpty(ids)) continue;
    n++; if (i == last) cur = ids;
    sb.AppendLine("[" + i + "] " + NameOf(i) + " | " + ids);
}
if (n == 0) { Say("没有可导出的预设"); return; }
string rel = "dsp_presets/presets_" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
ModHelper.WriteModDataFileText("traits_probably_fun", rel, sb.ToString());   // ← 只写 ModData 目录
string folder = ModHelper.GetModDataFolder("traits_probably_fun") ?? "";
string full = folder.Length > 0 ? folder.Replace('\\','/') + "/" + rel : "ModData/traits_probably_fun/" + rel;
bool copied = false;
try { if (!string.IsNullOrEmpty(cur)) { GUIUtility.systemCopyBuffer = cur; copied = true; } }
catch (Exception ex) { Debug.LogWarning("[DSP] 开局预设：写剪贴板失败 " + ex.Message); }
Say("已导出 " + n + " 个槽到 " + full + (copied ? "（当前槽已复制到剪贴板）" : ""));
```

**⑦ 第二条改价路：造一条原版 feature，让原版自己求和**（`TraitsProbablyFun\SharedCigarette.cs:510-527`）

```csharp
if (val != null && IsShared(val)) {                    // val = 当前客人的 StoreClient
    bool buying = !GeneralHelper.IsItemOwned(__instance)
               && ((int)val.clientIntent == 2 || (int)val.clientIntent == 3);
    string id = buying ? "dspShareCigBuyBonus" : "dspShareCigSellBonus";
    __instance.RemoveItemFeatureByID(id);              // ← 先摘旧，幂等
    ItemFeature feat = new ItemFeature((FeatureType)(buying ? 4 : 5));   // 4=买向 5=卖向
    feat.identifier = feat.category = id;
    feat.valueStage = (ValueStage)1;
    int pct = TradePct(val);
    string disp = Txt.Get(buying ? "dsp_share_cigarette_bonus_buy" : "dsp_share_cigarette_bonus_sell")
                + (buying ? " -" : " +") + pct + "%";
    feat.SetPublicDisplay(disp); feat.SetActualDisplay(disp);
    feat.isFeatureMatch = true;
    feat.SetPreExposedValueMod(buying ? -pct : pct, true);
    feat.SetValueModifier(buying ? -pct : pct);        // ← 公开 setter，9 处都这么写
    __instance.AddItemFeature(feat);
}
```

**⑧ 唯一的 Prefix→false 问价：递归调用 + `isDisabled` 临时摘 feature（囤货居奇）**（`Trait34.cs:146-152`、`:218-271`，节选）

```csharp
private static bool Prefix(GameItem __instance, ref long __result) {
    if (__instance == null || _cornerNegDepth > 0) return true;        // ← 重入就放行原版
    if (!CornerMarketActive) return true;
    bool? buying = CornerPlayerBuys(); if (!buying.HasValue) return true;
    _cornerNegDepth++;
    try {
        foreach (ItemFeature f in __instance.itemFeatures) {
            if ((int)f.featureType != 1) continue;
            int mod = f.GetActualValueModifier();
            if (mod == 0 || !(buying.Value ? mod > 0 : mod < 0)) continue;   // 只摘"顺着玩家"的那一侧
            if (!f.isDisabled) { f.isDisabled = true; _cornerFeatSaved.Add(new(f, false)); }
        }
        __result = __instance.GetNegociatedValue();                     // ← 递归调原方法
    } finally {
        foreach (var kv in _cornerFeatSaved) kv.Key.isDisabled = kv.Value;   // 必须还原
        _cornerFeatSaved.Clear(); _cornerNegDepth--;
    }
    return false;                                                       // ← 整段替换
}
```

**⑨ 隐藏 feature 的唯一写法：反射私有 `valueModifier`（窖藏）**（`NewPerks.cs:1066-1079`）

```csharp
ItemFeature feat = new ItemFeature("dspCellarAgingValue");
feat.identifier = "dspCellarAgingValue";
feat.SetValueStage((ValueStage)1);
feat.isPublicHidden = true;  feat.isExposable = false;      // 玩家看不见这条
AccessTools.Field(typeof(ItemFeature), "valueModifier")?.SetValue(feat, pct);
AccessTools.Field(typeof(ItemFeature), "preExposeValueModifier")?.SetValue(feat, pct);
AccessTools.Field(typeof(ItemFeature), "usePreExposeValue")?.SetValue(feat, true);
AccessTools.Field(typeof(ItemFeature), "initiallyShown")?.SetValue(feat, false);
AccessTools.Field(typeof(ItemFeature), "publicDisplay")?.SetValue(feat, "");
AccessTools.Field(typeof(ItemFeature), "actualDisplay")?.SetValue(feat, "");
bottle.AddItemFeature(feat); bottle.Validate();
```

---

## 4. 踩坑与红线

1. **`StoreUIManager.Update` 上挂了 2 个每帧 Postfix，而且都先跑"无条件逻辑"再问门**。`PatienceLimit.cs:12-19` 每帧调 `RepairStuckSecurityState()`（内含 `PlayerStore.Instance` 取用与状态机修复）；`SlotHighlightFix.cs:26-33` 每帧调 `TickWhenNotDragging()`（幸好它第一行 `if (_registry.Count == 0) return;`）。**红线：这条 `Update` 属本作最热帧回调，任何"每帧做一件与当前 perk 无关的事"都应迁到自建 `MonoBehaviour` + 节流（同批 `3828949943` 的做法）或至少排到 `Active` 之后。**
2. **`GetNegociatedValue` 的 Prefix→false 会递归进原方法，导致别人模组的 Postfix 跑两遍**（`Trait34.cs:229`）。它自己用 `_cornerNegDepth` 防了自己的重入，**但防不住别人**：任何在 `GetNegociatedValue` 上挂 Postfix 的模组（我们、`3828949943`、`3828971731` 都挂了）都会在这次嵌套里**再被调用一次**，价格类效果可能被双算。**红线：`GetNegociatedValue` 只能 Postfix；要用 Prefix 必须改成"只调 `GetBaseValue` 一类纯函数"，绝不能回调原方法。**
3. **25 个 `BeginDay` Postfix + 8 个 `EndNight` Postfix，而全库只有 9 处 `HarmonyPriority`、0 处 `HarmonyBefore/After`**。同时选两条改钱的 perk（如信托 + 夏威夷训练 + 开张红利）时，谁先跑取决于 Harmony 的补丁注册顺序（≈ 反射扫描顺序）。**红线：同一入口超过 2~3 个补丁就必须显式 `[HarmonyPriority]` 排好，或并成"一个补丁 + 一张有序动作表"。**
4. **2,107 个 `try` / 2,092 个 `catch`，其中绝大多数是 `catch { }` 或 `catch (Exception ex) { Debug.LogWarning(...Message) }`——吞掉堆栈**。全库 1,773 行日志里 946 行是 `LogWarning`，也说明"静默失败"是常态。**这违反我们"对内暴露、只在边界捕获"的规范；抄它的结构，不要抄它的 catch 密度。** 反过来，它有一处做对了：`Plugin.OnEnable` 把每个子系统单独 `try/catch` 包起来（`Plugin.cs:45-120`），所以**一条 perk 注册失败不会带塌整个模组**——这是"边界捕获"的正确位置。
5. **反射原版私有静态字段是本模组的长期债**：`NewGamePerksUI.frame` / `firstRun` / `Rebuild()`（`PerkPresets.cs:72/592/610`）、`GameSlotInventory` 的 6 个私有 `Image` 字段（`SlotHighlightFix.cs:35-45`）、`ItemFeature.valueModifier` 等 7 个私有字段。全部包在 `try/catch` 里 → 官方一改就是"功能静默消失"，不报错。
6. **预设载入只校验点数，不校验槽位数量**。原版 `CanStart` 是 `UsedPoints()<=3 && SelectedPerks.Count<=MaxPerkCount()`（`NewGamePerksUI.cs:250-257`），而 `LoadSlot` 只提示 `num > 3`（`:632-635`）。**一个"7 个 0 费特质"的预设会静默把继续键锁死**，玩家看到的是"载入了 7 个特质"然后点不动。
7. **导出是只出不进**。`ExportAll` 写了带表头的 `.txt`（`# format: [slot] name | id,id,id`）却没有 `Import`，玩家改完文件无法回灌。要抄成"玩家可编辑配置"，必须补一个解析器（顺带处理：未知 id、重复、超 50 槽、名字里的 `|`）。
8. **`startingPerks` 被三处同时改**：预设载入（`PerkPresets.cs:602`）、夏威夷训练局内追加（`HawaiiTraining.cs:134-140`，54 选 3 直接 `list.Add`）、原版天赋界面。三处都不加锁、不通知彼此；`HawaiiTraining` 用 `dsp_hawaii_injected` 标记防重，但**预设载入会把 `startingPerks` 整体 `Clear()`** → 先点夏威夷注入、后点预设载入，注入的 3 条会被清掉而标记还是 1，**这一局就白丢了**。同类"多方写同一个原版列表"的坑，我们要一开始就定"唯一写入口"。
9. **`playerCash` 仍有 4 处直接赋值**（经销商开局 `=200`、若干备用金/罚金），与 `ModCash` 混用。改名/结算类逻辑一旦在 `BeginDay` 里同时发生，直接赋值会覆盖掉同帧的 `ModCash`。**统一走 `ModCash`（带 UI 刷新与日志），只在"新档初始化"这种无并发的点直接赋值。**
10. **`ItemLoadGuard` patch 的是 `Directory<GameItem>.Create(string)` 这个全局工厂**（`ItemLoadGuard.cs:39`），找不到就返回 `ItemDirectory.CreateEmptyItem(id)` 空壳。它救的是"存档里有已删模组的物品"，但**空壳物品进了容器后，任何读它字段的原版逻辑都可能拿到 null**。要用，必须同时给空壳打标记 tag 并让所有自家 `IsTag` 判据认它。
11. **`ModData` 的旧 id 兼容是"读旧写新 + 吞异常"**（`ModData.cs:16-27`）：每个 `GetInt/GetBool/Get` 都先读 `traits_probably_fun`，读不到再读 `deep_space_pawnshop`，读到就迁过来。**这段 124 行代码是"改名一次，永久付利息"**——但它也是旧档不丢状态的原因。我们若改 modId，必须抄这个"双读单写"，并在下一版加删除旧键的迁移。
12. **中文 UI 必须自己做 TMP 字体回退**：`PresetWindow` 用了 `EnsureFontFallback` / `ApplyFontFallback`（`:1331-1393`）把中文字形补进 TMP 字体资源，否则手搓的 `TextMeshProUGUI` 全是方块。**这条对我们所有在 UI 里写中文的模组都适用。**
13. **`dsp_no_questions`（坦承相待）用 `Prefix→false` 直接把"赃物/违禁"两条初始化整段掐掉**：`StolenHelper.InitStolenItem` 的 Prefix 先 `RemoveStolenStatus` 再 `return false`（`NewPerks.cs:13-30`），`ContrabandHelper.InitContrabandItem` 的两个重载各一句 `return !NoQuestionsActive`（`:36-52`）。这比"改价"深一层——它改的是**物品的身份判定**，与任何鉴定/赃物类模组（含 `3828949943` 的 `StolenHelper.SetHeat`、我们自己的真伪判定）都会正面顶。**我们要"干净货"效果，只能 Postfix 把 heat 打到 0 或加自家 tag，不能拦初始化。**

---

## 5. 对我们的可用点

### 对《深空电玩》（`deep_space_arcade` / `DeepSpaceArcade.dll`）

1. **`src/ArcPerks.cs` 的两条天赋补三样东西（比 `3828949943` 的结论更进一步）**：现在 `new StartingPerk{ cost=3/1, rawName, rawDescription, source }` 手搓（`ArcPerks.cs:31-55`）。要补的是 ① `mindEditable = true` → **进原版心灵重编程器名单，玩家局内能自己换**，我们不必写"重置天赋"；② `cost` 用负值造"挑战位"（本作 20 条负成本，最狠 −3）；③ `extraPerkSlots`（原版 `maxSlot`）做"用槽位换强度"——`dsp_paid_training` 就是"−1 点 + 3 槽"。**这三样都是纯参数，不需要新代码。**
2. **给《深空电玩》开一个"专精"（`AddStartingScenario`），而不是继续在 `OnNewGameStarted` 里塞道具**。本作的 Distributor 给了完整配方（`StartingOptions.cs:923-949`）：`networkUpgrade[key].Unlock()` 解锁产业 + 补供货商名片 + `StorePhoneClient.phoneState` 置"已介绍" + 给启动资金/原料。我们可以照抄一个"下层区二手游戏商"开局：解锁刻录/翻新产线对应的 `NetworkUpgrade`、把唱片供货商电话置为已认识、给 5 张空白盘 + 1 台刻录机。**比"开局往背包塞东西"强的地方是：产业对玩家是"已购"状态，UI/存档/剧情全按原版走。**
3. **50 槽预设管理器整套可以搬成"开局配置方案"**：`PerkPresets` 830 行里真正有用的只有 ~150 行（存 id 串 / 四道校验 / 整体替换 + 原版 Rebuild / 导出 txt），**存储用 `SaveFiles.IndexSave` 而不是 `ModData`，得到"跨存档、跟人不跟档"的语义**——这正是"玩家自建设备布局/开局搭配"想要的。导出 txt 走 `ModHelper.WriteModDataFileText` + 剪贴板，**零能力**，正好符合我们正式版的 `<Capabilities/>` 要求。
4. **`SlotHighlightFix` 的"补丁补丁"要引以为戒，但它的自检有用**：它先反射 6 个私有 `Image` 字段给自建槽位补高亮，再在 `StoreUIManager.Update` 上每帧清扫。我们要做自建容器/槽位，**先按 `3828949943` 的路（只依赖原版 `HighlightSlot`）**，别一上来就 patch `Update`。
5. **中文 UI 的 TMP 字体回退**（`PresetWindow.EnsureFontFallback`）：我们 `ArcConsole` / `ArcLabeler` 的手搓文本一旦换成非默认字体就会出方块，这条是现成写法。

### 对《深空当铺：可能是群友》（`freya_cornell` / `DeepSpacePawn.dll`）

6. **`Has()` 门 + `ModData` 幂等标记 = 我们"群友来访/剧情解锁"该用的形状**（`TrustFund.cs` 全链）：`if (!Active) return;` + `if (已做标记 == 今天的键) return;` + `写标记`，三步。我们 `FreyaNightly` / `FreyaPhone` 现在是"每天跑一遍逻辑"，缺的正是**"今天已经发生过就不再做"的单一 int 键**——本作 95 个 `ModData` 键就是这个模式铺出来的。
7. **`HawaiiTraining` 的"局内往 `PlayerStore.startingPerks` 直接追加 N 条"**（`HawaiiTraining.cs:120-153`）是"送天赋"的最短路径：写进 `startingPerks` 后**原版存档会自动带上，重开档还在**。可用于"群友帮工三次送一个被动"。**但它必须自己解决与预设载入的冲突（§4.8），我们要用就固定"只在 `OnNewGame` 后 1 帧做一次"。**
8. **`ModData` 的双 id 兼容写法**（`ModData.cs`）：如果我们要改 `freya_cornell` 的键前缀或拆分模组，这 124 行是"旧档不丢"的模板——**读旧写新、只在成功迁移后返回**。
9. **`NetworkUpgrade.Unlock()` + `phoneState` 置位的组合**（§2.2）可直接用于《群友》的"关系网解锁"：与其自建"是否认识某商人"的布尔，不如把**原版电话状态**当成唯一真值，模组只负责把它推到"已介绍"。
10. **`ItemLoadGuard`（缺物品兜底）**：《群友》要读别的模组的物品（`DspExclusiveItems` / `FreyaStock` 会扫 `DirectoryMaster`），存档里残留已删模组 id 时最容易炸。它的做法是 patch `Directory<GameItem>.Create(string)` 返回空壳——**我们用之前必须给空壳打 `DSP_SHELL` 类 tag 并在读取处过滤**，否则空壳会流进交易链。

**不建议抄的**：`GetNegociatedValue` 的 Prefix→false 递归（§4.2）、`StoreUIManager.Update` 每帧补丁（§4.1）、2,092 个 `catch`（§4.4）、93 条各写一遍的 422 个靶点（我们两条线加起来不到 30 条效果，宽度税没必要付）。

---

## 附：与两份参照的一句话对照

| 维度 | `3828949943` 小窝（45 条） | `3828971731` 声望（44 条） | **本作（93 条）** |
| --- | --- | --- | --- |
| 效果表达 | 两张表 + 两种门；二代升级成位掩码 + 6 个纯函数 | 一张表 + 一个门 `PerkActive(id)` | **一张元数据表 + 93 个补丁体，每条自己问门** |
| 有没有"效果类" | 没有 | 没有 | **没有**（但每条有一个"补丁体文件"当代替品） |
| 改数值的路线 | 零 `valueModifier`，Postfix 现算 | 直接改原版 `ItemFeature.valueModifier` | **造原版 `ItemFeature`(FeatureType 4/5) 让原版自己算**（7 处）+ 3 个问价 Postfix + 2 处反射隐藏 feature |
| Transpiler | 0 | 1（带降级） | **0** |
| 门为什么必须每次现问 | 二代掩码可缓存 | 声望可随时变 | **93 条全部 `mindEditable` → 玩家局内可换** |
| 文件数 / 行数 | 205 / 18,705 | 178 / 41,112 | **159 / 55,666** |
| 每条效果的代码成本 | ≈ 250 行/条（其中一半不是特性） | ≈ 930 行/条（含 6,537 行巨兽） | **≈ 480 行/条（80.8% 是特性实现，最"纯"）** |
| 额外系统 | 扩舱/皮肤/设置 ≈ 7,300 行 | 改造体/战役/UI ≈ 大半 | **预设管理器 2,999 行 + 专精解锁器 + 天赋界面三件套** |

**一句话回答"93 条 perk 有没有守住无效果类"**：**守住了**——全库没有 `IPerkEffect` / `PerkBehaviour` / 效果注册表或任何按类型分发的效果抽象，93 条就是 93 行 `AddPerk` + 93 个自带补丁的 `static class`；但它为此付出的代价是**从"一个门"退化成"80 处门 + 422 个靶点"**——`mindEditable`（局内可换天赋）逼得每条效果都必须在被调用的那一刻现问 `Has(id)`，于是"加一条 perk"重新变成"加一个文件 + N 个补丁"，只是省掉了"加一个类继承一个基类"。
