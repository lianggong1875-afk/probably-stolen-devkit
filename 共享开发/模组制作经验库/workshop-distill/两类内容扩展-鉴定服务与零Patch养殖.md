# 两类内容扩展：鉴定服务扩展 + 零 Patch 养殖玩法

> 蒸馏自 `缓存文件夹\decomp_ws\` 下两个模组：
> **Extended Appraisals / 估价扩展**（3836549936，33 个 .cs / 3507 行，20 个 Patch）
> **Hatch Ranching / 哈奇养殖**（3844144965，32 个 .cs / 5792 行，**0 个 Patch**）。
> 前者是"用少量 Patch 把原版服务扩展成一门生意"，后者是"零 Patch 做一整套玩法"——正好一组对照。

---

## 一、Extended Appraisals（鉴定服务扩展）

### 1.1 它做了什么

把"单件鉴定"扩展成一套**批次业务**：密封批次（`ea_sealed_lot`）→ 周期评估单（`ea_weekly_evaluation`）→ 处方指南（`ea_rx_guide`）→ 名片（`ea_vandelay_card`），配一个自有 NPC（`dialogFuncId = "ea_venn"`）与账户记录。

### 1.2 数据放哪：物品标签 + 一个 `Lot` 模型

```csharp
// 注册 4 个自定义物品（原版目录 API）
dir.Add("ea_rx_guide",        () => ...);
dir.Add("ea_sealed_lot",      () => ...);
dir.Add("ea_vandelay_card",   () => ...);
dir.Add("ea_weekly_evaluation", NewSheet);

// 批次的"身份"全写在物品标签里（随物品走，不写 ModData）
held.ModifyTag("EA_BATCH", ts => ts.SetInt(...), true);
held.ModifyTag("EA_DAY",   ts => ts.SetInt(Entry.Day), true);
held.ModifyTag("EA_REV",   ts => ts.SetInt(...), true);
held.ModifyTag("EA_SEED",  ts => ts.SetInt(...), true);
```

`Lot`（542 行）是它的核心模型：一个批次里有哪些件、每件估值多少、报价多少、手续费多少。
`Entry.Read(GameItem)` / `Entry.Record(...)` / `Entry.ReadStatement(...)` 是存取入口；`Entry.AppraisalRow(...)` 负责把一行账目渲染成富文本。

**这个"物品标签承载业务状态"的做法我们已经在 DigitalVault / Container Link 里见过三次**——它是本作最通用的持久化范式：不占 ModData 键、随物品搬迁/掉落/出售走。

### 1.3 20 个 Patch 的靶点表（本次最值钱的产出）

这张表等于"鉴定/服务/过夜这条链上所有能介入的点"：

| 文件 | 靶点 | 干什么 |
| --- | --- | --- |
| `TrueValue.cs` | `AppraisalHelper.GetCorrectBaseValue` | **改写"正确估值"**（只对 `ea_sealed_lot` 生效，其余 `return true`） |
| `Submit.cs` | `AppraisalServiceHelper.OnEstimate` | 提交估价单时插入自己的结算 |
| `PlasmaCompletion.cs` | `AppraisalServiceHelper.OnEstimate` | 同上（第二个补丁，同一方法） |
| `PlasmaQuote.cs` | `EmporiumAnalytics.TrackAppraisalSent` | 记录"发出去的报价"做统计 |
| `DossierSearch.cs` | `ContrabandHelper.StartInspection` | 介入盘查流程 |
| `ReservedPromotions.cs` | `ExchangeBarter.CanAccept` | 交易所以物易物的准入 |
| `NoInsurance.cs` | `StoreService.UpdateCost` | 改服务费 |
| `Repossession.cs` | `PlayerStore.HandleInsurance` | 过夜的保险结算 |
| `TrashSettlement.cs` | `PlayerStore.HandleJanitorial` | 过夜的清洁结算 |
| `ClosingLedger.cs` | `StoreShutterButton.EndAndClose` | 关店结算 |
| `PhoneName.cs` | `StorePhoneClient.GetLocalizedDisplayName` | 电话簿里的显示名 |
| `VennReadingTime.cs` | `DialogUIManager.ScheduleAutoAdvance` | 改对话自动推进节奏 |
| `VennPortrait.cs` | `SpriteDict.GetSprite` | 立绘自愈 |
| `SealedContainerHint.cs` | `ContainerHelper.CreateContainerTooltip` | 密封容器的提示框 |
| `Alarm.cs` / `AlarmRate.cs` / `NoAlarm.cs` | `MachineAlarm.Alarme` / `GetAlarmeStopRate` / `Operate` | 警报机行为 |
| `SmashedAlarm.cs` / `SmashedAlarmPartial.cs` | `GameItemElement.ValidateSelect` / `ValidatePartialSprite` | 砸坏的警报机视觉/可选性 |
| `BoltedAlarm.cs` | `GameItem.MayRemove` | 警报机是否可拆 |

**注意两处同一方法被两个补丁打**（`OnEstimate` 两次）——作者自己就在做"同一靶点多补丁"，说明补丁不是越少越好，但**必须显式定序**（这里没看到 `HarmonyPriority`，是潜在风险）。

### 1.4 值得抄的写法：改写原版计算时的"窄门"

```csharp
[HarmonyPatch(typeof(AppraisalHelper), "GetCorrectBaseValue")]
internal static class TrueValue
{
    private static bool Prefix(GameItem gameItem, ref int __result)
    {
        if (!Entry.Enabled || gameItem.identifier != "ea_sealed_lot") return true;   // ← 只认自己的物品
        __result = Entry.Read(gameItem).Prices(Entry.Day).Values.Sum();
        return false;
    }
}
```

`GetCorrectBaseValue` 是**全店所有物品估价都会走的方法**（我们的 `ItemFeature.realCondition` 判定链也在附近）。它用"identifier 白名单 + 提前 return true"把自己变成一道 1 行的门——这是改原版热路径的正确姿势。

---

## 二、Hatch Ranching（零 Patch 养殖玩法）

### 2.1 它证明了一件事：整套玩法可以零 Patch

- 32 个 .cs / 5792 行，**0 个 HarmonyPatch**、无 reflection、无文件 IO
- 只挂 9 个 ModHook：`OnModItemDirectoryInit`、`OnLootTablesLoaded`、`OnGameLoadedLate`、`OnNewGameStarted`、`OnHandlingNightlyServicesEarly`、`OnAfterhourArriveLate`、`OnCreateTooltipLate`、`OnNetworkUpgradeUnlocked`、`OnGenerateCustomerEarly`
- 只用了 7 个 ModHelper：`SpawnItem`、`SetClientSource`、`GetModDataInt/SetModDataInt`、`GetSoundIdentifier`、`AddDebugFunction/RemoveDebugFunctions`
- 能力扫描：**无明显高权 API（倾向零能力）**

### 2.2 文件分工（这就是"一个玩法模组该有的骨架"）

| 文件 | 职责 |
| --- | --- |
| `HatchDaily.cs`（1650） | 每日模拟核心（最重） |
| `HatchSpecies.cs`（433） | 物种定义（数据表） |
| `ShipScan.cs`（304） | 飞船扫描（外来物种/危机？） |
| `Items.cs`（285） | 自定义物品注册与文本刷新 |
| `Butcher.cs`（243） | 屠宰：给刀挂"处理哈奇"的回调 |
| `CoalGenerator.cs`（223） | 煤发电（产出结算） |
| `Diet.cs`（203） | 饮食/喂养 |
| `Loc.cs`（188） | 本地化（自建 key → 文本） |
| `StartingGift.cs`（180） | 开局赠礼 |
| `HatchTooltips` / `Heartbeat` / `MysteryPet` / `HatchDealer` / `JacksonStock` | 提示框、心跳、神秘宠物、商人、博士货源各自一个 Attach/Detach |

### 2.3 它的 `ModEntry` 是"注册/注销成对"的教科书

```csharp
public void OnEnable()
{
    LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    ModHook.OnLootTablesLoaded  += EnsureTextReady;
    ModHook.OnNewGameStarted    += OnNewGameText;
    HatchSpecies.EnsureRegistered();                       // 先注册数据表
    ModHook.OnModItemDirectoryInit += Items.Register;      // 物品注册挂目录初始化
    ModHook.OnGameLoadedLate       += RepairVisuals;       // 读档后统一修
    ModHook.OnHandlingNightlyServicesEarly += NightSweep;  // 过夜兜底
    HatchTooltips.Attach(); Heartbeat.Attach(); MysteryPet.Attach();
    HatchDealer.Attach(); StartingGift.Attach(); JacksonStock.Attach();
}

public void OnDisable()
{
    // ……上面每一个都成对反订阅/Detach……
    ModHelper.RemoveDebugFunctions("hatch.ranching");
}
```

**每个 `Attach()` 都有对应的 `Detach()`**，连 `LocalizationSettings.SelectedLocaleChanged` 都反订阅。这是"静态状态三处成对重置"的实践样本。

### 2.4 它踩过的三个坑（正好印证我们蒸馏出的结论）

**① 文字表时序**：它没有在 `Init` 里读文案，而是 `Loc.Reload()` + `_textReady` 幂等标志，等 `OnLootTablesLoaded` / `OnGameLoadedLate` / `OnNewGameStarted` 三个时机任一到来才刷：

```csharp
internal static void EnsureTextReady()
{
    if (_textReady) return;
    Loc.Reload();
    if (!Loc.Ready) return;
    _textReady = true;
    int n = Items.RefreshAllText();     // 文本就绪后把已注册物品的名称/描述重设一遍
    HatchConfig.Attach();
    HatchMod.Info($"[loc] 文本就绪（重设 {n} 件物品）");
}
```

这与我们蒸馏出的 `Locale Error Fix`（`invalid operation handle`）是同一个问题的两种解法：**要么拦 `LocHelper.Get` 降级，要么等就绪后重设**。它选了后者，更干净。

**② 换语言要重设物品文本**：`SelectedLocaleChanged` 里 `Loc.Reload()` + `Items.RefreshAllText()` + 记录 `_lastLocaleCode` 防重复。**物品名称是注册时写死的字符串，不重设就会永远停在注册时的语言。**

**③ 读档后要修视觉**：`RepairVisuals()` 做四件事——`Items.RefreshAllText()`、`ItemVisuals.RepairAll()`、`Butcher.WrapAllBlades()`、`TrapPatch.WrapExisting()`。因为"给刀挂回调"这类操作是**在运行时绑到具体物品实例上的**，读档重建物品后绑定全丢，必须重挂。

> 这条对我们特别重要：我们给物品挂 `onLoaded`/委托 时，**读档后必须重新挂**，否则第一晚能用、第二天就哑了。

### 2.5 过夜结算放在哪

```csharp
ModHook.OnHandlingNightlyServicesEarly += NightSweep;   // 注意是 Early

private static void NightSweep()
{
    try { RunDiagnosticsOnce(); HatchDaily.SweepAnimals(); HatchDaily.DumpGrids();
          int n = Butcher.WrapAllBlades(); } 
    catch (Exception e) { HatchMod.Warn($"夜间兜底（哈奇）整体失败：{e}"); }
    try { CoalGenerator.TickAll(); }
    catch (Exception e) { HatchMod.Warn($"夜间兜底（发电机）整体失败：{e}"); }
}
```

两个细节：**分块 try/catch**（哈奇失败不影响发电机）；**挂在 Early 而不是 Patch `EndNight`**（与我们蒸馏出的规矩一致）。

---

## 三、两个模组的对照结论

| 维度 | Extended Appraisals | Hatch Ranching |
| --- | --- | --- |
| Patch 数 | 20 | **0** |
| 能力 | reflection（1 文件） | 倾向零能力 |
| 数据载体 | 物品标签（`EA_*`）+ `Lot` 模型 | ModData int 键 + 物品 |
| 介入原版服务 | 直接改估价/盘查/保险/清洁/服务费 | 只用 ModHook 的过夜/读档/目录钩子 |
| 冲突面 | 较大（20 个靶点，其中一处同方法两补丁且无定序） | 极小（零 Patch） |
| 适合学什么 | "扩展一门生意"要碰哪些原版点 | 整套玩法的最小骨架 |

**给我们的结论**：想加"新玩法"→ 照 Hatch Ranching（零 Patch + 数据驱动 + 成对注册）；想改"原版已有服务的规则"→ 照 Extended Appraisals（窄门 Prefix + 物品标签承载状态），**但每处都要显式 `HarmonyPriority`**（它自己没做，我们别学）。

## 四、对我们的可用点

1. **`Directory<GameItem>.Add(id, factory)`**：注册自定义物品的另一条路（我们之前用的是 `DirectoryMaster` 侧），值得核对两者差异后统一。
2. **物品标签承载业务状态**（`EA_BATCH`/`EA_DAY`/`EA_SEED`）：做"典当合同/批次/票据"这类会流转的物件时直接用。
3. **20 个靶点表**：我们要改"鉴定/盘查/保险/清洁/服务费/电话簿/对话节奏"时，先来这里查该打哪个方法。
4. **Hatch 的 ModEntry 骨架**：`Attach/Detach` 成对、文本就绪幂等、读档 `RepairVisuals` 重挂回调——可以直接当"玩法模组模板"。
5. **`Butcher.WrapAllBlades()` 这个思路**：要给原版已有物品（刀、陷阱）挂自定义回调时，写一个 `WrapAllXxx()` 在开局与读档后各扫一遍，而不是要求玩家重新获得该物品。

## 五、待验证 / 风险

- Extended Appraisals 的 `Lot`/`Reader`/`FieldManual` 三个大文件我只做了结构侦察，内部算法（批次怎么算价、周评估怎么统计）未通读，标「（推测）」。
- Hatch Ranching 的 `HatchDaily`（1650 行）是核心模拟，我未逐行读，只从 `ModEntry` 与文件分工推断其职责。
- Extended Appraisals 两处 `AppraisalServiceHelper.OnEstimate` 补丁没有 `HarmonyPriority`，**它自己就存在顺序不确定**；若玩家另装改估价的模组，表现会飘。
