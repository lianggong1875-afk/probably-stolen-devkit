# 工坊 3832805089 蒸馏档案 —— 综合兼容补丁·酿酒与睡觉（PawnshopBrewingSkillCompat）

| 项 | 内容 |
| --- | --- |
| id | `3832805089`；manifest `<ID>PawnshopBrewingSkillCompat</ID>` `<Name>综合兼容补丁·酿酒与睡觉</Name>` v1.1.29，作者 `Custom Mod`，`<Capability>files</Capability>`，**无 `<Prerequisites>` 元素** |
| 反编译目录 | `缓存文件夹\decomp_ws\3832805089`（25 文件；24 个 `.cs` = **9,294 行** + 1 个 `.csproj` = 21 行，对得上任务书的"25 文件 / 9,294 行"） |
| 为什么值得读 | 它是工坊里少见的**"专门写一个模组去补别人模组的洞"的成品**：11 个第三方程序集、30 处手工 Patch、9 处 Transpiler，几乎全是"两边各自都对、同时装就坏"的场景。这是跨模组冲突的**一手案例库**，不是静态扫描能猜出来的 |

> 📄 **关于文件名**：本档案沿用任务书指定的 `3832805089-ElectronicsCompatPatch.md`。按库里 `<id>-<英文名>` 的惯例它其实该叫 `…-CompatibilityHub.md` —— 因为它不是"电子产品的补丁"，见下条口径更正。

> ⚠️ **口径更正（读了包内 manifest 才发现的）**：任务书叫它"电子产品mod冲突兼容补丁"，但它**不是只给电子产品拓展打补丁**。包内真名是「综合兼容补丁·酿酒与睡觉」，Description 里点名了博士翻页、装机（PCExpansion）、农业扩展、水商、图鉴、香烟、水塔、双柜台、猎杀契约、酿酒、电子产品**十条线**。电子产品（`3828902092`）只是其中**最粗的一条**（合同表里 847 条、8 个模块文件里 4 个直接改它）。这一点决定了本档案的写法：把它当"多模组补丁中枢"读，收益比当"单点补丁"读高一个量级。
> 另一条证据：`3828902092` 那份 6,102 条合同表里**收了 `Pawnshop.BrewingSkillCompat` 28 条**（[3828902092-ElectronicsExpansion.md](3828902092-ElectronicsExpansion.md):457），本补丁自己的表里也正好 28 条 —— 两边互相认。**作者极可能同一个（推测：两份 manifest 的 `<Author>` 都是占位式 `Custom Mod`、文案风格一致、命名空间同属 `Pawnshop.*`）**，这解释了"为什么不直接改原模组"（见 §3.5）。

---

## 1. 它到底修了什么冲突 —— 真实冲突案例表（15 条）

| # | 症状（玩家能看到的） | 冲突双方 | 涉及方法 | 修法 |
| ---: | --- | --- | --- | --- |
| 1 | 博士翻页里的暂存供货被电子产品当垃圾销毁 → 缺货 + 返店刷新异常 | JacksonShopPages × CyberElectronics | `PlayerStore.TryAddDirectSellingItemToTable(GameItem,bool,bool,bool,int)`、`EmporiumEntry.GetAllNonOwnedItem()`、`DrJacksonLocation.Leave()` | Postfix 把"已实际收下"改判 `true`（`JacksonPagesCompatibility.cs:187`）；离场只 `Destroy` 未上柜且非已拥有者（`:210`） |
| 2 | 商品全放得下时页栏整条消失，玩家看不到分页 | JacksonShopPages（分页逻辑）× 页面 UI | `JacksonPages.BuildPagesFromCapture()` 里的静态字段 `ForcePaging` | Transpiler 把唯一一处 `ldsfld ForcePaging` 改成 `call ShouldCategorize()`（`:153-159`），只在博士点位且翻页启用时返回 `true` |
| 3 | 材料箱还原旧维修材料后"箱里有材料却修不了、手动也取不回" | PCExpansion × CyberElectronics | `PCExpansion.NativeItemCodec.Restore(JObject)`、`CyberElectronics.Items.Create(string)`、`RepairMaterials.Restore(GameItem)` | `[ThreadStatic] restoring` 深度计数 + 前后缀捕获/回写 `identifier`（`PcMaterialIdentityCompatibility.cs:67-101`） |
| 4 | 旧普通组件 ID 不被新材料系统识别 → 选料、品质比较、材料箱交接全断 | CyberElectronics（新旧 ID 制）× 自己 | `RepairRuntime.MaterialMatches(int,string)`、`RepairMaterials.SelectionPriority(int,int,string)` | 包装层先 `LegacyTarget(id)` 再调对方原函数（`WorkbenchMaterialsCompatibility.cs:279-301`） |
| 5 | 工作间明明已就绪，却先被超时拒绝、手动才能恢复 | PCExpansion（轮询时序） | `WorkroomDriver.WaitForUi()` 里的 `ProbeUiReady()` 与 `throw` 点 | 在 `newobj/throw` 之前插 `ProbeUiReady()` 分支回跳，**保留原期限**（`PcUiReadinessCompatibility.cs:44-75`） |
| 6 | 装机夜间诊断把指令前缀拆散 → 工作间与材料箱**全不可用** | PCExpansion × 所有改 `EndNight` 的模组 | `PlayerStore.EndNight()` + `PCExpansion.ServerRoomDaily+EndNightPatch.LocalPhaseTranspiler` | `Priority.Last(=0)` 的修复 Transpiler，把前缀组搬回被插桩指令之前，连同 `labels` 与 `exception block`（`PcNightDiagnosticsCompatibility.cs:32,58-105`） |
| 7 | 组件强化台无法创建（农业扩展把原生熔炉回调包了一层） | AgriExpansion × CyberElectronics | `AgriExpansion.AgriIronSmelting.Configure(GameItem)`、`CyberElectronics.EnhancementStation.Create()` 内的 `MachineFurnace.Furnace()` | Transpiler 换工厂调用 + `[ThreadStatic] creatingTemplate` 让农业 Prefix 这一次放行（`AgricultureEnhancementCompatibility.cs:34,71-88`） |
| 8 | 主菜单就创建 `PlayerStore` 实例 → 读档前 UI 崩 | PCExpansion | `ServerRoomDaily.LocalIsolated()` 里的 `PlayerStore.Instance` | Transpiler 换成判 `HasInstance` 的 `ExistingPlayer()`（`PcExistingPlayerCompatibility.cs:69-76`） |
| 9 | 新开局买水塔不显示（附属数据还没读） | WaterTowerPlus | `TowerFixture.Ensure(string)` | Prefix：provider 身份 + `ReadState==None` + 非读盘中 → 先 `EnsureLoadedForCurrentSlot()`（`WaterTowerCompatibility.cs:59-77`） |
| 10 | 未启用且零资产的旧档机房行情落后一天 → 过不了夜 | PCExpansion | `ServerRoomDaily.CanBeginSleep()` | 严格特征判定（`DormantCandidate`）+ 备份 + 现金流/资产不变式 + `Commit` 后复核（`DormantRoomCompatibility.cs:145-273`） |
| 11 | 捕获成功却记成"逃脱" → 过夜被猎杀契约拒绝 | XIAOWOTradePerks | `NativeHuntContract._state`（`hunt.day.v1`）、`Escaped()` | 营业结束界面按限定特征改账 + 全档 `.es3` 备份 + 回读校验 + 失败回滚（`OldHuntRepair.cs:159-331`） |
| 12 | 双柜台把床铺交互拦死 → 睡不了床 | ShopExpansionMod | `PawnshopExpansion.ExpansionHooks.AllowWorld(Interactable)` | Transpiler 把 `ldc.i4.0/ret` 换成 `ldarg.0 + BedGate.CanUseBed`（`BedTranspiler.cs:12-36`） |
| 13 | 图鉴清理在主菜单就跑 → 空引用 | MaterialAtlas | `MaterialAtlas.AtlasBook.CleanupIfNeeded()` | Prefix `PlayerStore.HasInstance`（`MenuReadinessCompatibility.cs:35-40`） |
| 14 | 材料被返还后发酵判定认为"没消耗"→ 停产；同时暂存期间存不了档 | BrewingExpansion × CyberElectronics | `BrewingBarrel.StartOne(...)`（内含 `"native fermentation changed state without consuming its staged inputs"`）、`Pulp/Printer SaveBoundary` 四个 Prefix | 替换 `Enumerable.Any` 与 `BrewingBarrel.Live` 两个调用点（`Receipts.cs:114-137`）+ 四个保存边界 Prefix 重写成 `return true`（`CompatibilityIL.cs:62-79`） |
| 15 | 旧 `PawnshopSleepPatch` 与本补丁同装 → 一份活两份补丁 | 自己 × 旧版自己 | — | `OnEnable` 直接抛异常拒绝启动（`Core.cs:36-39`） |

> 这张表的读法：**15 条里只有 5 条（#1/#3/#4/#7/#14）涉及电子产品**；**9 条是与电子产品完全无关的跨模组冲突**（#2/#5/#6/#8/#9/#10/#11/#12/#13）；剩下 1 条（#15）是自己跟旧版自己。所以把它当"电子产品的补丁"读会漏掉三分之二。另 14 条里 **6 条的症状是"功能整块不可用"**（#5/#6/#7/#12/#13/#14），不是数值错乱 —— 这是"两个 Prefix 抢同一入口"的典型后果，和我们 `07-踩坑台账` 里"整段替换原版入口 = 与同类模组互斥"是同一条规律的另一种表现。

---

## 2. 规模与结构

### 2.1 目录 → 行数 → 职责

| 目录 | 文件 | 行数 | 占比 | 职责 |
| --- | ---: | ---: | ---: | --- |
| `Pawnshop.OfficialCompatibility\` | 1 | **5,964** | **64.0%** | **生成代码**：`ReflectionContracts` 反射契约表，5,357 条 `result.Add(...)`，覆盖 23 个程序集 |
| `PawnshopBrewingSkillCompat\`（18 个文件） | 18 | 2,894 | 31.1% | 真正的补丁逻辑：入口/编排、11 个兼容模块、收据簿、IL 改写器、装机储物绑定 |
| `PawnshopSleepPatch\`（5 个文件） | 5 | 430 | 4.6% | 睡觉/床铺/捕获/过夜诊断（上一个独立模组被并进来的遗留命名空间） |
| `Properties\` + `*.csproj` | 2 | 27 | 0.3% | 工程样板 |

> 占比的分母是 **9,315 行**（25 个文件全部，含 `.csproj`）；§2.2 里"真逻辑 35.7%"用的是 **9,294**（只算 `.cs`）。两个口径都列出来，是因为任务书的"9,294 行"正好等于 24 个 `.cs` 之和。

### 2.2 "一个补丁模组要 9 千行"说明什么

- **64% 是生成代码**：5,964 行的表里，它自己只被 28 条引用；`PCExpansion` 1,190、`CyberElectronics` 847、`PSFishingGameplay` 750（**它根本不碰钓鱼**）、`XIAOWOTradePerks` 176、`TobaccoExpansion` 172、`BrewingExpansion` 142、`ShopExpansionMod` 126、`WaterTowerPlus` 59、`MaterialAtlas` 30。=> 这张表是**作者几个项目共用的"官方兼容层"，整份链进来、按构建裁剪**，不是为本补丁逐条生成的。第 `PSFishingGameplay` 750 条就是证据。
- **真逻辑只有 3,324 行（35.7%）**，摊到 11 个兼容模块上平均 300 行/个 —— 一个模块 = 一个 `Enable()`（反射核对签名 + 装补丁）+ 2~5 个钩子 + 一个 `Disable()`。
- 所以**"9 千行"不是复杂度，是"广度 × 生成代码"**：广度 = 11 个第三方程序集、30 个 Patch、9 个 Transpiler；生成代码 = 反射契约表。真要读它，**跳过 `ReflectionContracts.cs` 直接读那 23 个逻辑文件**。

### 2.3 它需要认识谁（11 个程序集 + 1 个旧 id）

`BrewingExpansion`、`CyberElectronics`、`PCExpansion`、`JacksonShopPages`、`AgriExpansion`、`AquaSense`、`MaterialAtlas`、`TobaccoExpansion`、`WaterTowerPlus`、`ShopExpansionMod`、`XIAOWOTradePerks`；外加按 **manifest id** 认的旧模组 `PawnshopSleepPatch`（`Core.cs:36`）。

---

## 3. 它怎么修的：三层插法，全部"软"的

### 3.1 四种手法（**没有一种是把对方 DLL 换掉**）

| 手法 | 用在哪 | 说明 |
| --- | --- | --- |
| ① 改**对方的** Prefix/Postfix/Transpiler 挂点 | 15 条里的绝大多数 | 全部 `harmony.Patch(对方的 MethodInfo, ...)`，一步都不改对方文件 |
| ② 改**自己的**（能力位让路） | #3/#4/#7/#12/#14 | 先问对方"你处理了吗"，处理了就**本项不挂**（`Core.cs:232`、`SleepCompatibility.cs:30`、`CaptureHuntCompatibility.cs:23`、`AgricultureEnhancementCompatibility.cs:34`） |
| ③ 插在中间做**翻译层** | #4、#14 | 包装一层：入参先翻译成对方认的 ID（`LegacyTarget`），出参再翻译回来（`Returned`/`LiveUnconsumed` 收据簿） |
| ④ 纯**观测**不改行为 | 过夜诊断 | `SleepTrace` 只在头 4 次点击里把补丁名单与判定值写日志（`SleepTrace.cs:64-88`），"不改变拦截结果"写在日志里 |

### 3.2 Patch 靶点表（30 处 Patch / 9 处 Transpiler）

| 模块 | 靶点（对方程序集） | 类型 | 位置 |
| --- | --- | --- | --- |
| `Core` 酿酒 | `BrewingExpansion.Formal.BrewingBarrel.StartOne` | Prefix+Postfix(`Receipts.Enter/Exit`)+**Transpiler** | `Core.cs:247` |
| `Core` 酿酒 | `CyberElectronics.SkillBrewingIngredients.Begin` / `SkillIngredientScopePatch.Postfix` | Postfix ×2 | `Core.cs:248-249` |
| `Core` 保存 | `Pulp/PulpSlot/Printer/PrinterSlot SaveBoundary`（4 个 Prefix） | **Transpiler**（独立 Harmony 实例 `…Save`） | `Core.cs:214-231` |
| `WorkbenchMaterials` | `RepairRuntime.RepairClickedPart` / `QuickRepair` / `RepairLoose` | Prefix(`Enter`)+Postfix(`Exit`) | `:179` |
| `WorkbenchMaterials` | `RepairRuntime.RepairMaterial` / `ScreenMaterial` | Prefix | `:181-182` |
| `PcMaterialIdentity` | `PCExpansion.NativeItemCodec.Restore`、`CyberElectronics.Items.Create`、`RepairMaterials.Restore` | Prefix+Postfix ×3 对 | `:39-41` |
| `JacksonPages` | `PlayerStore.TryAddDirectSellingItemToTable`、`DrJacksonLocation.Leave`、`JacksonPages.BuildPagesFromCapture`、`DoctorMachineStock.AddStock`、`EmporiumEntry.GetAllNonOwnedItem` | Postfix/Prefix+Postfix/**Transpiler** | `:102-109` |
| `PcNightDiagnostics` | `PlayerStore.EndNight()` | **Transpiler**，`priority = 0` | `:27-32` |
| `PcUiReadiness` | `WorkroomDriver.WaitForUi()` | **Transpiler** | `:34` |
| `PcExistingPlayer` | `ServerRoomDaily.LocalIsolated()` | **Transpiler** | `:41` |
| `DormantRoom` | `ServerRoomDaily.CanBeginSleep()` | Prefix | `:52` |
| `OldHuntRepair` | `EndOfDayUIManager.OpenUI()` | Postfix | `:81` |
| `AgricultureEnhancement` | `AgriExpansion.AgriIronSmelting.Configure`、`EnhancementStation.Create` | Prefix + **Transpiler** | `:42-43` |
| `TobaccoTerminal` | `TobaccoExpansion.OrderTerminal.Position` | **Transpiler**（改一个 `ldc.r4 0.295` → `0.42`） | `:30` |
| `WaterTower` / `AquaConfig` / `MenuReadiness` | `TowerFixture.Ensure` / `ModConfigWindow.BuildEntry` / `AtlasBook.CleanupIfNeeded` | Prefix | `:49` / `:55` / `:30` |
| `Sleep` | `PawnshopExpansion.ExpansionHooks.AllowWorld`、`EndOfDayUIManager.OnEndDayButtonClick`、`XIAOWOTradePerks.NativeHuntContract.Escaped` | **Transpiler** + Prefix/Prefix | `SleepCompatibility.cs:45`、`SleepTrace.cs:23`、`CaptureHuntCompatibility.cs:37` |

### 3.3 加载顺序：**零 `HarmonyAfter` / `HarmonyBefore`（全库 0 次）**

- 17 个 `new Harmony(...)` 全部是**裸 id**：`"Pawnshop.BrewingSkillCompat"`、`+".Save"`、`+".WorkbenchMaterials"` …（`Core.cs:14,16`）。**不是 `"mod." + manifest.ID`** —— 所以第三方写 `[HarmonyAfter("mod.PawnshopBrewingSkillCompat")]` **永远不命中**，和我们踩过的 `mod.GunsmithExpansion` 是同一个坑的镜像（`07-踩坑台账.md:371-385`）。
- 它靠两个**数值优先级**代替排序声明：`SleepTrace` 的前缀 `priority = 800`（= `Priority.First`，抢在所有人前面读补丁名单，`SleepTrace.cs:30`）、`PcNightDiagnostics` 的 Transpiler `priority = 0`（= `Priority.Last`，等所有人的 Transpiler 都跑完再修指令，`PcNightDiagnosticsCompatibility.cs:32`）。**这两个数字用得是对的**，但用裸数字而非常量，读者很难一眼认出 `First`/`Last`。
- `manifest.xml` **完全没有 `<Prerequisites>`**：不硬依赖任何人，装谁都跑，谁都不在处理就自己停那一项。

### 3.4 怎么检测对方在不在：`ModLoader.Mods` + 程序集名（**不是 `IsModRunning`，不是 `ModReflect.FindType`，全库 0 次**）

三段式：① `ModLoader.Mods` 按 **manifest id** 拒绝旧模组（`Core.cs:36`）；② 按**程序集简名**找唯一在跑的那个（`Core.cs:100-110`，显式过滤 `IsEnabled && LoadError == null && Assembly != null`）；③ 在对方程序集里按**全名字符串 + 精确签名**要类型/成员，任何一处不符就抛 `MissingMemberException`（`Core.cs:281-289`）。

### 3.5 "兼容补丁"这种交付形态，作者为什么这么选

- 它是**独立模组**（玩家要额外订阅），但**内部是"选装包"**：11 个模块各自 `try/catch` + 失败即 `UnpatchAll` 只停自己（`Core.cs:112-258`），没装的人不会看到任何副作用。**订阅一次，自动只启用你装了的那些。**
- 它**不改任何第三方 DLL、不改第三方配置**（manifest 原话），也不要求对方升版 —— 全部走运行时反射契约。
- 为什么不直接改原模组：目标里 8 个不是作者的（推测：`JacksonShopPages`/`AgriExpansion`/`AquaSense`/`MaterialAtlas`/`TobaccoExpansion`/`WaterTowerPlus`/`ShopExpansionMod`/`XIAOWOTradePerks`，均无作者证据）；而**作者自己的那两个（电子产品 + 本补丁）故意分开**，理由是现实可见的：本补丁 v1.1.29 的日志里到处是"本次不重试""完全停用需重启游戏"，说明它迭代频率远高于主模组，`manifest.xml` 的 Description 结尾直接写"**待重启实机验收**"——**它是主模组的热修通道，独立发布才能一天一更而不动 18.7 万行**（推测，基于发布节奏与文案，未验证发布时间线）。

---

## 4. 关键代码摘录

### 4.1 逐项隔离的编排：一项坏，不许拖死其它项（`PawnshopBrewingSkillCompat\Core.cs:112-135`）

```csharp
private void ApplyAvailableModules()
{
	MenuReadinessCompatibility.Enable();
	AquaConfigCompatibility.Enable();
	try
	{
		JacksonPagesCompatibility.Enable();
	}
	catch (Exception ex)
	{
		JacksonPagesCompatibility.Disable();
		Debug.LogError((object)("[博士翻页兼容] 接口核对/挂载失败，本项全部撤销且已停止，本次不重试；其他兼容继续：" + ex));
	}
	try
	{
		PcUiReadinessCompatibility.Enable();
	}
	catch (Exception ex2)
	{
		Debug.LogWarning((object)("[装机工作间就绪] 仅本项停止：" + ex2));
	}
	// ……同形状重复 11 次
```

配套的"失败即整块撤销"写在每个模块的 `Enable()` 里：`catch { Disable(); throw; }`（如 `WorkbenchMaterialsCompatibility.cs:185-189`），`Disable()` 里 `UnpatchAll + 全部静态字段置 null`。

### 4.2 fail-fast Transpiler：形状不对就抛，绝不"猜着改"（`PawnshopBrewingSkillCompat\CompatibilityIL.cs:12-50`，节选）

```csharp
public static IEnumerable<CodeInstruction> Apply(IEnumerable<CodeInstruction> instructions)
{
	List<CodeInstruction> code = instructions.Select(...).ToList();
	var array = (from x in code.Select((CodeInstruction c, int i2) => new { c = c, i = i2 })
		where x.c.opcode == OpCodes.Ldstr && (string)x.c.operand == "native fermentation changed state without consuming its staged inputs"
		select x).ToArray();
	if (array.Length != 1)
	{
		throw new InvalidOperationException("发酵消耗检查不唯一。");
	}
	int i = array[0].i;
	int num = code.FindLastIndex(i, (CodeInstruction c) => c.operand is MethodInfo methodInfo
		&& methodInfo.DeclaringType == typeof(WineHelper) && methodInfo.Name == "TryStartFermentation");
	if (num < 0) { throw new InvalidOperationException("缺少发酵调用边界。"); }
	int[] array2 = /* ...Enumerable.Any<GameItem>(2 参) 的位置... */;
	int[] array3 = /* ...BrewingBarrel.Live(GameItem) 的位置... */;
	if (array2.Length != 1 || array3.Length != 1 || array2[0] >= array3[0])
	{
		throw new InvalidOperationException("发酵校验形状不匹配。");
	}
	code[array2[0]].operand = typeof(Receipts).GetMethod("AnyUnconsumed");
	code[array3[0]].operand = typeof(Receipts).GetMethod("LiveUnconsumed");
	return code;
}
```

四处 `throw` 是这份代码最值钱的部分：**IL 指纹对不上就不干活**，异常冒到 `Core` 的 `catch` → `UnpatchAll + Receipts.Release() + LogError`（`Core.cs:252-257`）。9 个 Transpiler 里 7 个都有这种"唯一性断言 + 抛异常"（`JacksonPagesCompatibility.cs:156`、`BedTranspiler.cs:26,33`、`AgricultureEnhancementCompatibility.cs:65`、`PcUiReadinessCompatibility.cs:67`、`PcExistingPlayerCompatibility.cs:63`、`TobaccoTerminalCompatibility.cs:55`）。

### 4.3 能力位协商：先问"本体处理了吗"，再决定自己挂不挂（`Core.cs:260-272` + `100-110`）

```csharp
internal static bool Provides(Assembly electronics, string feature, string typeName = "CyberElectronics.CompatibilitySupport")
{
	try
	{
		PropertyInfo propertyInfo = ReflectionContracts.Optional(
			ReflectionContracts.GetType(electronics, typeName, throwOnError: false),
			(Type contractReceiver0) => ReflectionContracts.GetProperty(contractReceiver0, feature, BindingFlags.Static | BindingFlags.Public));
		return propertyInfo != null && propertyInfo.PropertyType == typeof(bool) && (bool)propertyInfo.GetValue(null, null);
	}
	catch (Exception ex) { Debug.LogWarning(...); return false; }
}
// 调用点（Core.cs:232）：本体已接管材料事务 → 本补丁不重复挂载
if (Provides(assembly, "BrewingIngredients")) { Debug.Log("...本补丁不重复挂载；保存隔离保持。"); return; }
```

四个能力位的**全部拼写**（对方的 `*CompatibilitySupport` 静态 bool 属性）：`BrewingIngredients`（`CyberElectronics.CompatibilitySupport`）、`NativeFurnaceSlotBinding`（同）、`CapturedDeparture`（同）、`NativeBedInteraction`（`PawnshopExpansion.CompatibilitySupport`）。**"谁在管"由被补的那一方声明，补丁方只读** —— 这比两边各写一个 `if` 可靠。

### 4.4 存档级修复的"备份 → 计划 → 复核"三段（`PawnshopBrewingSkillCompat\OldHuntRepair.cs:259-284`，节选）

```csharp
string path = SaveFiles.AbsolutePath(SaveFiles.Slot(snapshot.Slot));
byte[] array = File.ReadAllBytes(path);
text2 = "old-hunt-repair/slot-" + snapshot.Slot + "-" + Guid.NewGuid().ToString("N");
ModHelper.WriteModDataFileBytes("PawnshopBrewingSkillCompat", text2 + ".es3", array);
if (!array.SequenceEqual(ModHelper.ReadModDataFileBytes("PawnshopBrewingSkillCompat", text2 + ".es3")))
{
	throw new IOException("完整存档备份校验失败。");
}
// ……把 原账/新账/备份名 写进收据键 old-hunt-repair.v1，读回校对……
ModHelper.SetModData("xiaowo_trade_perks", "hunt.day.v1", snapshot.Fixed);
snapshot.Store.SaveGame();
flag2 = ContainsEntry(File.ReadAllText(path), "PawnshopBrewingSkillCompat:old-hunt-repair.v1", text);
if (!flag2 || !ContainsEntry(File.ReadAllText(path), "xiaowo_trade_perks:hunt.day.v1", snapshot.Fixed))
{
	throw new IOException("未确认修复已写入存档；保留备份，不报告成功。");
}
```

注意它**改的是别人的 modData 键**（`xiaowo_trade_perks`），所以备份 + 收据 + 回滚三件套是硬要求；回滚段在 `:288-322`（`flag && !flag2` 才回滚，且逐字段核对后才写回）。

---

## 5. 踩坑与红线

1. **Harmony owner 用裸 id，第三方排不了序**。17 个实例全是 `Pawnshop.*` 裸 id。要跟它排序必须写 `[HarmonyAfter("Pawnshop.BrewingSkillCompat")]`，写 `mod.` 前缀不命中且**不报错**。
2. **它自己的 `Disable()` 会拒绝撤销 Transpiler**。`PcNightDiagnosticsCompatibility.cs:41-53`：只有当 `EndNight` 上已经没有 `PCExpansion.ServerRoomDaily+EndNightPatch.LocalPhaseTranspiler` 时才 `UnpatchAll`，否则打一行"完全停用需重启游戏"。**理由是真的**——别人的 Transpiler 已经按"你的修复后指令序"编译过一次，撤掉你的修复等于把别人的补丁留在半坏状态。我们做任何 Transpiler 都要提前想清楚"能不能撤"。
3. **`Priority.Last` 的修复型 Transpiler 是唯一能救"被别的 Transpiler 拆坏的指令流"的手段**，但它必须**在别人之后**跑，也就是 `priority = 0`。这是本档案里唯一值得无条件照抄的顺序技巧。
4. **替换 `Enumerable.Any` 时把原谓词丢了**。`Receipts.AnyUnconsumed(IEnumerable<GameItem> items, Func<GameItem,bool> original)` 的第二个参数**完全没用**，语义靠 `LiveUnconsumed` 内部再调一次 `BrewingBarrel.Live` 兜住（`Receipts.cs:114-137`）。能跑，但一旦对方改了 `Any` 的谓词语义就静默错。**红线：改写 BCL 枚举方法时，必须保留并转调原谓词。**
5. **`catch { }` 静默吞异常确实存在**：`OldHuntRepair.cs:129-131`（读档快照整段）与 `SleepTrace.Gate` 尾部（`:130-132`）。前者是"读档期探测"、后者是"纯诊断"，按我们 `AGENTS.md` 第 3 条勉强算边界捕获，但**都没有 `LogError`**，坏了查不出来。
6. **`AquaConfig` 靠"形参名"判签名**：`methodInfo2.GetParameters()[0].Name` 必须等于 `"__0"` 或原方法第一个形参名，否则直接放行不挂（`AquaConfigCompatibility.cs:43-46`）。这是"用编译器生成的形参名当契约"，非常脆，但它用 `PatchInfo.Prefixes.Any()` 先确认没有别人抢同一入口（`:51`）——**这两条合起来才是完整的"礼让"**。
7. **改别人 modData 是既成事实**（#11）。它做了备份，但**没有版本协商**：对方若改 `hunt.day.v1` 的字段格式，它按 `array.Length != 6` 直接拒绝（`OldHuntRepair.cs:170`）——这是对的，但意味着**对方一动格式，这条修复就永久失效而玩家不知道**。
8. **`files` 能力是它的功能必需**，不是偷懒：`ModHelper.WriteModDataFileBytes/Text` 用来落存档备份（`OldHuntRepair.cs:262,266`、`DormantRoomCompatibility.cs:264`）。我们要发布零能力版时，**"整档备份"这类功能必须砍掉或改成内存内校验**。

---

## 6. 对我们的可用点

1. **【最高性价比】`FindEnabledAssembly` 软依赖探测**（`Core.cs:100-110`）：按程序集简名找唯一在跑的那个，且显式过滤 `IsEnabled && LoadError == null && Assembly != null`。我们 `ArcAppraisalPane.cs:74-110` 现在是"按 manifest id/Name 里含 `appraisal` 猜 + 扫 `Assembly.FullName` 前缀"，**没有过滤 `LoadError`**；`ArcAssets.cs:468-474` 的注释还记着"热重载后 `ModLoader.Mods` 会留 `Manifest == null` 的登记项"。把这段搬进《深空电玩》的 `ArcInterop` 与《深空当铺：可能是群友》的 `DspInterop`，统一成 `TryFindRunningAssembly(name) → null | Assembly`。
2. **【直接可用】能力位协商**（§4.3）：给两家各加一个 `CompatibilitySupport` 静态 bool 属性（如 `deepspace_arcade.CompatibilitySupport.SpriteSelfHeal`、`freya_cornell.CompatibilitySupport.PortraitOverride`），让**第三方补丁能问"你处理了吗"**再决定挂不挂。我们已经有 `ArcInterop`/`DspInterop` 的 `ModHelper.Expose` 入站通道，缺的正是这条**出站能力位**；两套并存才是完整的（入站给数据，出站给"谁在管"）。
3. **【直接可用】"失败即整块撤销"的模块模板**：`Enable()` 里 `catch { Disable(); throw; }` + 编排层 `try/catch` 只降级本项 + `LogError` 带项目前缀。我们两个项目的 `ArcMachinesPatches.cs` / `FreyaWeaponsPatches.cs` 目前是"一个大 Patch 类 + 静态 Install"，一处反射失败会带掉整组。**按模块切、按模块撤**，热更（`HotReloadPrepare/Activate`）时才有东西可撤。
4. **【谨慎抄】`Priority.Last` 修复型 Transpiler**：只有当我们确认某个原版方法被别的模组 Transpiler 拆坏时才有意义；典型的候选是我们已知的 `SpriteDict.GetSprite`（5 方共用）与 `StoreUIManager.Update`（5 方共用）——**但那是性能热点，不是指令流被拆坏**，不要为了"防身"去挂。真正该挂的地方是 `ChemicalHelper.RunNightlyChemicalSimulation`（已被 3~4 个模组整段替换，见 `07-踩坑台账.md:110,126`）。
5. **【反例，别抄】改别人 modData + 全档备份**：`files` 能力会让我们过不了零能力扫描，且"备份了但没版本协商"照样会静默失效。我们要修《深空当铺》存档里的第三方数据时，第一选择是**请对方提供一次性修复入口**（`ModHelper.OnMessage`），第二选择才是写死特征判定。
6. **【文案模板】manifest Description 写成"逐条症状 + 逐条修法"**：这份 Description 是一份可直接读的 changelog（15 条症状几乎一一对应 §1 的表），而且是中英双语的**玩家可读**版本。我们 `17-工坊发布与SteamUGC上传.md` 的发布清单里可以把这一条加进"Description 自检"：**每条修复都要能指出"哪个模组的哪个功能坏了"**。
