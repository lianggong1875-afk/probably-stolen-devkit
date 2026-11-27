# 3829069448 · 水瓶打印机增强 / Better Bottle Printer —— 反编译蒸馏档案

> **工坊 id** `3829069448`｜`better_bottle_printer` v2.0.7｜作者 n1ght 与 好景不长｜5 万订阅｜`<Capabilities />` **零能力**｜游戏 0.46 / 0.46D
> **反编译目录** `缓存文件夹\decomp_ws\3829069448\`（17 个 `.cs`，2252 行，`BetterBottlePrinter\` + `Shared\`）
> **为什么值得读**：它是"**给原版机器加按钮**"的教科书——全模组只在原版**物品工厂** `MachineBottlePrinter.BottlePrinter()` 上打一个 Postfix，然后**遍历原版像素树**找到控制面板的 `MachineryStack` 往里塞按钮，一行不重画窗口、一个原版三键都不替换；同时又示范了怎么"链式包装"原版的槽位/电源判定而不是覆盖它，以及**哪些方法一旦碰就会外溢到全游戏**（本档案第二部分第三节的靶点表是重点）。

---

## 1. 规模与结构

| 文件 | 行 | 角色 |
| --- | --- | --- |
| `ExtraPrintButtonsPatch.cs` | 947 | 全部业务：7 个扩展按钮、像素树遍历、打印流程、显隐同步、输出格扩容、DNA 投料 |
| `Shared/DeferredPatches.cs` | 242 | 延迟打补丁：等游戏表就绪（最少 1s / 最多 30s）才 `Arm`，`[HarmonyPrepare]` 全部走它的 `Gate()` |
| `L10n.cs` / `Shared/L10nCore.cs` | 229 / 28 | **中英双语硬编码**（`L10nCore.Pick(zh,en)`，按 `LocHelper.GetCurrentLocaleCode()` 前缀判断） |
| `Diag.cs` | 193 | 诊断：环境快照（各模组启用/信任/被拒原因 + 本地化与水字段），开关默认关 |
| `PowerDraw.cs` | 175 | 多电源合并供电（按 `HYPERTECH_BATTERY_OUTPUT_INT` 分片扣电） |
| `ModSettings.cs` / `ModEntry.cs` | 82 / 80 | 12 个配置项 / `IMod` 入口（2 个 ModHook + 调度延迟补丁） |
| `Log.cs` | 55 | `WarnOnce` 去重日志 |
| 6 个补丁（`FreeUnlock*` / `NoPower*` / `Upgrade*`）+ `TooltipProgress.cs` | 185 + 24 | 上限 9→16、免费解锁、免电、升级后刷新、tooltip 进度行 |
| `Properties/AssemblyInfo.cs` | 12 | — |

`OnDisable` 只解两个 ModHook 并把回调字段置 null，并明说**"Harmony 补丁需重启游戏后完全卸载"**（`ModEntry.cs:54-79`）——诚实但也是缺口：没有 `UnpatchAll`、没有把已注入的按钮撤掉。

---

## 2. 原版"水瓶打印机"是哪台机器、原版流程是什么

**它是 `bottle_printer`**，注册在 `FurnitureItemDirectory.cs:25` → `Add("bottle_printer", () => MachineBottlePrinter.BottlePrinter())`。原版实现全文 183 行（`原版:MachineBottlePrinter.cs`），流程拆成五段：

| 段 | 原版位置 | 做了什么 |
| --- | --- | --- |
| 建窗 | `:174-182` `CreateMachineInventoryWindow` | `GameSlotInventory`（**唯一**电池槽）+ `GameGridInventory(4,4)` 输出格 + 三个 `MachineryButton`；面板角色 `"output"` / `"control"`，外面套 `MachineHelper.CreateMachineryWindow` + `PowerPanel` |
| 输出白名单 | `:24` | `mayInventoryAddItemFunc = GeneralHelper.IsItemOwned(item) && item.IsTag("LIQUID_CONTAINER_TAG")` |
| 三档按钮 | `:32-49`（回调）→ `:95-106` `TryPrint` | 小 `small_bottled_water` 4 电 / 中 `bottled_water` 6 电（≥3 配件）/ 大 `large_bottled_water` 11 电（≥9 配件） |
| 打印事务 | `:95-106` | `电池槽非空 && PowerHelper.CanDrawPowerSource(电池,cost)` → `DirectoryMaster.Item(id)` → `GraphUtils.CanAccept(outputGrid,item)` → `TryAcceptAll` → `DrawPowerSource`。**先验收再扣电，扣电在落地之后** |
| 升级 | `:63-94` `ApplyUpgradeState` + `:109-146` | tag `BOTTLE_PRINTER_UPGRADE_COUNT_TAG` 计 int；普通档 ≥3、大档 ≥9；**每升一档只加输出格宽度**：`num=4; if normal num++; if large num++; outputGrid.SetShape(num,4)` → 4×4 / 5×4 / 6×4；上限 `CanUpgradePrinter` 要求 `< 9`（`:124-131`）；`UpgradePrinter` 还会 `unitValue += 升级件价值`、`StartingPerkContent.ConsumeUpgradeMaterial`、播棘轮音（`:133-146`） |

三个"重新算档位"的时机：创建后立即、`onItemTargetThisFunc`（把电子元件拖到机器上）、`onLoaded`（读档）（`:51-60`）。tooltip 由 `GameItemElement.cs:1090` 转 `MachineBottlePrinter.CreateTooltip`（`:148-172`），按当前配件数在"还差 N 个"和"可打印 X 电"之间切换。

> **原版它是"瞬时打印"，没有 cycle、没有 `onCycleEnd`。** 所以"加槽位"和"抢 `onCycleEnd`"两种常见做法在这里都不成立——它走的是第三条路：**在物品工厂返回的那一刻往已有窗口里加按钮**。

---

## 3. "增强"具体加了什么 + 怎么不破坏原版流程

### 3.1 加了六件事（不是批量、不是自动补料）

| # | 内容 | 源码 |
| --- | --- | --- |
| 1 | **7 个新产出按钮**：微型水瓶 `mini_bottle` 4电/0配件、酒瓶 `wine_bottle` 14/4、水桶 `water_jug` 18/6、空白识别卡 `blank_keycard` 18/8、循环水箱 `ras_water_tank` 36/12、储水罐 `water_tank` 420/16、DNA 数据盘 `plant_dna_disk` 168电/材料3 | `:36-82`、`:295-379` |
| 2 | **配件上限 9 → 16**，且**解锁门槛与耗电解耦**（阶梯 0/4/6/8/12/16，不再跟原价涨） | `ModSettings.cs:33`、`UpgradeCapPatch.cs:16-37` |
| 3 | **输出格扩容**：`SetShape(num, 8)`（原版 4 行 → 8 行），默认开、可关 | `:890-914` |
| 4 | **多电源合并供电**：按 `HYPERTECH_BATTERY_OUTPUT_INT`（缺省 12）把一次耗电拆成多次 `CanDrawPowerSource`/`DrawPowerSource`；`Capacity` 用二分逼近单块电源的真实可用量 | `PowerDraw.cs:20-56`、`76-158` |
| 5 | **DNA 盘分批投料**：未解锁时每点一次投入库存里的 `printer_plastic`，进度存 `BBP_DNA_DISK_FILL_TAG`，凑 3 个写 `BBP_DNA_DISK_UNLOCKED_TAG`；**永不碰原版升级计数** | `:645-820` |
| 6 | 工程件：tooltip 显示 `已喂 N/16`、免电/直接解锁作弊开关、输出格失败/异常时的环境快照 | `TooltipProgress.cs:7-23`、`ModSettings.cs:64-75`、`Diag.cs:50-70` |

### 3.2 怎么不破坏原版：**一个 Postfix + 一套链式包装 + 全部挂在原版回调上**

1. **入口只有工厂 Postfix**（`:8-109`）：`[HarmonyPatch(typeof(MachineBottlePrinter), "BottlePrinter")]` 的 `Postfix(GameItem __result)` → `Setup(__result)`。原版 `Directory.Create` 每次都是 `factoryDictionary[identifier]()`（`原版:Directory.cs:24-38`），所以**每造一台打印机就跑一次 Setup**，每台拿到自己的窗口（原版 `CloneLinked` 遇到 `contentWindow != null` 直接 `throw NotImplementedException`，容器类物品只能靠工厂重建，`原版:GameItem.cs:238-248`）。
2. **不重画窗口，遍历像素树找挂点**：`Walk(contentWindow.childElement, ref stack, batteries, ref output)`（`:496-562`）按类型递归 `MachineryPanelRow.Panels` → `MachineryPanel`（**靠 `role == "control"` 字符串**，不靠索引）→ `MachineryIntake.content` → `MachineryStack.elements`；`GameSlotInventory` 收进 `batteries`，**第一个** `GameGridInventory` 当 `output`。找不到就 `Log.WarnOnce` 退出（`:127-131`），绝不新建面板。
3. **加/撤按钮只用原版容器**：`MachineryStack.Add(PixelElement)`（原版 `MachineryStack.cs:98`）/ `Detach`（`:119`）；按钮是 `new MachineryButton(文本, 回调, -1, -1)`。
4. **槽位白名单"链式"而不是覆盖**（`:169-188`）：先存 `oldGate`，新委托**先问原版**，原版放行就放行；只有原版拒绝时才追加自己的 7 个 id。这是"两个模组都改 `mayInventoryAddItemFunc` 也不会互相吃掉"的唯一写法。
5. **刷新全挂在原版回调上**：`machine.onLoaded`（`Delegate.Combine` 保留原委托，`:191-200`）、`contentWindow.openCallback`（链式保留 `prev`，`:207-215`）、`UpgradePrinter` 的 Postfix、`ModHook.OnModConfigApplied`（`:381-399` → `SyncShowHide()` 实时显隐，不重启）。
6. **不动原版三键回调、不动 `ApplyUpgradeState`**：解锁门槛用 Postfix 改 `IsNormalUnlocked`/`IsLargeUnlocked` 的返回值，而不是重写 `ApplyUpgradeState`——所以原版"按钮灰/亮 + 输出格加宽"整套逻辑仍然自己跑。

### 3.3 Patch 靶点表（6 个 Harmony + 2 个钩子，含"谁也会碰"）

| # | 原版方法 | 补丁 | 目的 | 风险 / 撞车面 |
| --- | --- | --- | --- | --- |
| 1 | `MachineBottlePrinter.BottlePrinter()` | Postfix `(GameItem __result)` | 注入按钮 | **最容易被同类增强模组同时 Postfix**：多个 Postfix 各加一套按钮 → 面板重复；`_entries.Add` 无去重 |
| 2 | `MachineBottlePrinter.CanUpgradePrinter(GameItem)` | Postfix `ref bool` | 上限 9→16 | 只在 `__result == false` 时动手，所以**会把别人"明确禁止升级"的判定也翻成 true**；`ModuleHelper.cs:648` 也调它 |
| 3 | `MachineBottlePrinter.IsNormalUnlocked(GameItem)` / `IsLargeUnlocked(GameItem)` | Postfix `ref bool` | "直接解锁"作弊 | **外溢最严重的一处**：这两个方法被 `CreateTooltip`、`ApplyUpgradeState`、`ModuleHelper.cs:648`、`MaterialDirectory.cs:328` 共用，改返回值等于改全游戏的判定；免费解锁开着时 `ApplyUpgradeState` 会顺手把输出格加宽 |
| 4 | `MachineBottlePrinter.UpgradePrinter(GameItem, GameItem)` | Postfix | 只调 `RefreshAll()` | 无害；但每次升级遍历全部 entry |
| 5 | `PowerHelper.CanDrawPowerSource(GameItem,int)` | Postfix `ref bool` | 免电 | **全游戏机器共用的热点**；靠 `IsPrinterBattery`（`parentInventory.GetParentItem().identifier == "bottle_printer"`）收窄，每次调用多一次 `GetParentItem()` |
| 6 | `PowerHelper.DrawPowerSource(GameItem,int)` | Prefix `return false` | 免电 | 同上；直接跳过原方法，若原方法将来加了副作用（音效/统计）会被一起跳过 |
| 钩子 | `ModHook.OnCreateTooltipLate` / `OnModConfigApplied` | — | tooltip 进度行 / 设置即时生效 | 无冲突面；后者处理函数里第一句就校验 `modId`（`:381-386`） |
| 底座 | `[HarmonyPrepare] => DeferredPatches.Gate()` | — | 等游戏表就绪再 Arm | 所有 6 个补丁都这么写；**这是"补丁别在 `DirectoryMaster` 还没建好时命中"的现成实现** |

---

## 4. 关键代码摘录

### A1 原版工厂与打印事务：`原版:MachineBottlePrinter.cs:24-49`（**有省略**：略去 `name/flavorText/unitValue` 与 IL 注释）

```csharp
		outputGrid.mayInventoryAddItemFunc = (GameItem item, GameInventory _) => GeneralHelper.IsItemOwned(item) && item.IsTag("LIQUID_CONTAINER_TAG");
		GameItem machine = ItemDirectory.CreateEmptyItem().SetContentWindow(contentWindow).SetSpriteAndShape("Items/items_tool", "bottle_printer");
		MachineHelper.SetupBatterySlot(itemInventory, machine);
		machine.EnableTag("IMPORTANT_TAG"); machine.EnableTag("CONTAINER_TAG");
		machineryButton.SetCallback(() => { TryPrint("small_bottled_water", 4); });
		buttonNormal.SetCallback(() => { if (IsNormalUnlocked(machine)) { TryPrint("bottled_water", 6); } });
		buttonLarge.SetCallback(() => { if (IsLargeUnlocked(machine)) { TryPrint("large_bottled_water", 11); } });
```

### A2 扩展打印事务（**有省略**，注意"先 CanAccept、失败就 Destroy"）：`ExtraPrintButtonsPatch.cs:564-622`

```csharp
	private static void TryPrintExtra(GameItem machine, List<GameSlotInventory> batteries, GameGridInventory output, string itemId, int cost, int needUpgrades)
	{
		int upgradeCount = MachineBottlePrinter.GetUpgradeCount(machine);
		if (!ModSettings.FreeUnlock && upgradeCount < needUpgrades) { Log.Msg("打印被锁定：需要电子元件 ≥" + needUpgrades + "……"); return; }
		GameItem val = DirectoryMaster.Item(itemId, true);
		if (val == null) { Log.WarnOnce("NoItem:" + itemId, "物品目录里不存在：" + itemId); return; }
		List<PowerDraw.Part> list = null;
		if (!ModSettings.NoPowerCost)
		{
			List<GameItem> list2 = new List<GameItem>();          // 收集所有电池槽里现存的电源
			for (int i = 0; i < batteries.Count; i++) { GameItem v3 = batteries[i]?.childItem; if (v3 != null) list2.Add(v3); }
			list = PowerDraw.Plan(list2, cost);                    // 规划扣电方案
		}
		if (!ModSettings.NoPowerCost && list == null) { try { val.Destroy(); } catch { } Log.Msg("电量不足……"); return; }
		if (output == null || !GraphUtils.CanAccept((GraphNodeStorage)(object)output, val)) { try { val.Destroy(); } catch { } Log.Msg("输出格放不下……"); return; }
		int num = GraphUtils.TryAcceptAll((GraphNodeStorage)(object)output, val, -1);   // 返回剩余量，非 0 = 没落地
		if (num != 0) { Diag.Msg(...); Diag.DumpEnvironment("打印失败", force: true); }
		else { if (list != null) { PowerDraw.Execute(list); } Log.Msg("已打印 " + val.name + "……"); }
	}
```

### A3 槽位白名单的"链式包装"（不覆盖别人，**有省略**）：`ExtraPrintButtonsPatch.cs:171-187`

```csharp
		Func<GameItem, GameInventory, bool> oldGate = ((GameInventoryFunc)output).mayInventoryAddItemFunc;
		((GameInventoryFunc)output).mayInventoryAddItemFunc = (GameItem item, GameInventory inv) =>
		{
			if (item == null) { return false; }
			if (oldGate != null && oldGate(item, inv)) { return true; }     // 原版（或别人）放行 → 放行
			if (item.IsID("small_bottled_water") || item.IsID("bottled_water") || item.IsID("large_bottled_water")) { return true; }
			return (GeneralHelper.IsItemOwned(item) && item.IsTag("LIQUID_CONTAINER_TAG"))
				|| item.IsID("water_jug") || item.IsID("ras_water_tank") || item.IsID("mini_bottle")
				|| item.IsID("wine_bottle") || item.IsID("water_tank") || item.IsID("blank_keycard") || item.IsID("plant_dna_disk");
		};
```

### A4 按 `role` 找挂点（不靠索引）：`:515-528`

```csharp
		MachineryPanel val2 = (MachineryPanel)(object)((el is MachineryPanel) ? el : null);
		if (val2 != null)
		{
			if (val2.role == "control")                     // ← 原版 MachineryPanel.role（MachineryPanel.cs:8）
			{
				PixelElement content = val2.content;
				MachineryStack val3 = (MachineryStack)(object)((content is MachineryStack) ? content : null);
				if (val3 != null) { stack = val3; }          // 找到控制按钮堆
			}
			Walk(val2.content, ref stack, batteries, ref output);
			return;
		}
```

### A5 上限补丁（"只在 false 时动手"的语义陷阱）：`UpgradeCapPatch.cs:16-32`

```csharp
	private static void Postfix(GameItem printer, ref bool __result)
	{
		if (__result || printer == null) { return; }          // ← 已经是 true 就不管；false 才可能翻成 true
		try
		{
			if (printer.identifier.Equals("bottle_printer"))
			{
				int upgradeCount = MachineBottlePrinter.GetUpgradeCount(printer);
				if (upgradeCount < ModSettings.MaxUpgradeCount) { __result = true; }
			}
		}
		catch (Exception ex) { Log.WarnOnce("UpgradeCap", ex.GetType().Name + ": " + ex.Message); }
	}
```

---

## 5. 踩坑与红线

1. **在工厂 Postfix 里"改造已存在窗口"是合法的，但入口只有一个**。任何同类模组都会抢同一个 `BottlePrinter()`：两个 Postfix 默认同 priority（400），执行顺序不稳定 → 可能出现"按钮重复""某家按钮被另一家覆盖文案"。我们若要做同类增强，**先探测 `Harmony.GetPatchInfo(BottlePrinter).Postfixes` 里有没有别人**，有就只加自己的、别 `Refresh` 别人的控件。
2. **"只在 `__result == false` 时动手"的 Postfix 看似安全，实则把所有"禁止"也翻掉了**（A5）。想表达"我可以升级"应该用 Prefix + `__runOriginal` 或独立判定，而不是把 `false` 改 `true`。
3. **`IsNormalUnlocked` / `IsLargeUnlocked` 是全游戏共用判定**（tooltip、`ApplyUpgradeState`、`ModuleHelper`、`MaterialDirectory` 都在调）。改它们 = 改全局语义；"免费解锁"这种作弊开关**必须默认关**，且在 tooltip 里说清副作用。
4. **`batteries` 是 `List` 但原版窗口只有一个电池槽**（`MachineHelper.SetupBatterySlot` 只收一个 `GameSlotInventory`，`原版:MachineHelper.cs:41-54`）。所以"多块电池合计供电"在原生界面上不会发生（推测：除非别的模组加了槽）——**`PowerDraw` 的多源合并是给"未来/他模组"留的余量，不是当前生效路径**。
5. **`_entries` 只在 `Machine == null` 时移除**（`:409-412`、`:827-830`）。`GameItem` 不是 `UnityEngine.Object`，物品被销毁/卖掉后引用未必变 null → 死引用长期驻留，每次 `RefreshAll` 都对着它跑一遍 `try/catch`（异常被 `WarnOnce` 吞掉一次）。**抄的时候请加"销毁/离店即摘"的清理。**
6. **满屏 `try { } catch { }`**：`SyncBtn`、`AttachAll`、`onLoaded` 挂载、`contentWindow.Validate` 等处都是空吞——与本工作区"对内暴露、边界捕获并 `LogError`"的硬规范冲突，抄逻辑别抄这个习惯。
7. **`L10n` 是中英双语硬编码**（`L10nCore.Pick(zh,en)` + `LocHelper.GetCurrentLocaleCode()` 前缀判断），绕开了游戏 8 语本地化库；对比 `3837983447` 用 `ModHelper.GetLocalized(key, fallback)`。**我们要发多语言就学后者。**
8. **`OnDisable` 不撤 Harmony**（`ModEntry.cs:78` 自己承认"需重启"）。它至少把两个钩子解了；若我们要求热重载安全，必须补 `UnpatchAll` + 把注入的按钮 `Detach` 掉。
9. **两版互切的存档风险是模组自己写在 manifest 里的**：改窗口结构（输出格 4 行→8 行）后，引擎按窗口结构回填，切版本会"升级清零 / 格内物品错位 / 幽灵物品"。**给原版机器加格子 = 改存档的隐式结构**，这是本档案最重要的一条红线（manifest 原文："切换版本前请清空打印机（输出格+电池槽全部取出）再存档"）。

---

## 6. 对我们的可用点

**对《深空电玩》（`deepspace_arcade`）**

1. **"遍历原版像素树 + `role` 字符串定位 + 往 `MachineryStack` 塞按钮"是给原版机器加交互的标准手法**（§3.2 第 2 点）。我们的刻录机已经在 `MachineHelper.SetupManualSlot/SetupModuleBay` 上打转，若要做"街机柜：投币开一局 / 难度选择 / 排行榜"这类**在原生机器窗里多一排按钮**的界面，照 A4 找 `role=="control"` 的 `MachineryStack` 再 `Add(new MachineryButton(...))`，比自建 `PixelWindow` 省一整套皮肤与布局。
2. **`DeferredPatches` 那套"等游戏表就绪再 Arm 补丁"**（`:27-44` 的 `Ready()` 轮询 + 30s 兜底）可以直接搬进桥接器/机器类补丁，替我们手写的"首次读档后再打补丁"布尔旗。
3. **`Diag.DumpEnvironment` 的"读表前先验本地化就绪"**（`LocalizationSettings.InitializationOperation.IsDone && Status==1`，`Diag.cs:17-40`）——我们在诊断探针里读 `LocHelper`/`DirectoryMaster` 时撞过的空表问题，这就是现成的守卫条件。

**对《深空当铺：可能是群友》（`freya_cornell`）**

4. **"设置改完即时生效"的闭环**：`ModHook.OnModConfigApplied` → 校验 `modId` → `SyncShowHide()` 逐项比对期望/现状再增删控件（`:381-459`），全程不重建窗口、不要求重启。我们若加任何开关（立绘/台词/事件频率），照这个"逐项 sync"写，比"改完提示重启"体验好一个档。
5. **`PowerDraw.Plan/Execute` 的"先规划、后执行"两段式**值得记：`Plan` 只算不动，`Execute` 才真扣；失败路径一律**不扣电**（A2）。任何"扣了资源但产物没落地"的事务都该是这个形状——我们发货/兑换/缴纳保释金这类结算可以直接用同款顺序（先验证落点 → 落地成功 → 再扣资源）。

---

## 附：取证边界

- 已读全 17 个 `.cs`（2252 行）；原版语义在 `共享开发\reference\Assembly-CSharp\` 核对：`MachineBottlePrinter.cs`（全文 183 行）、`MachineHelper.cs:41-54`、`Directory.cs:24-38`、`GameItem.cs:238-248`、`PowerHelper.cs:653/681`、`GameGridInventory.cs:196`、`MachineryStack.cs:98/119`、`MachineryPanel.cs:8`、`GraphUtils.cs:166`。
- **读不到**：`itemId` 的实际注册处（`mini_bottle`/`wine_bottle`/`water_jug`/`ras_water_tank`/`water_tank`/`blank_keycard`/`plant_dna_disk`/`printer_plastic` 都按"原版已有物品"处理，未逐个在目录里验）、按钮贴图、`csproj`。
- **未真机验证（推测）**：`PowerDraw` 分片扣电对"电力转换器/一次性电池"的实际表现；输出格 8 行在 6×8 时的 UI 溢出；`_entries` 死引用在长局中的增长；与别的打印机增强模组同开时的按钮重复。
