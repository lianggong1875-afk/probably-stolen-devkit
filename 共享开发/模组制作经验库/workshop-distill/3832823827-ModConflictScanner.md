# 3832823827 · 模组冲突检测器 / Mod Conflict Scanner —— 源码蒸馏档案

| 项 | 值 |
| --- | --- |
| 工坊 id | 3832823827 |
| 显示名 | 模组冲突检测器 / Mod Conflict Scanner（`ConflictScannerMod.NameZh/NameEn` 常量，`ConflictScannerMod.cs:13-15`） |
| DLL / 命名空间 | `ConflictScanner.dll` / `ConflictScanner`（`ConflictScanner.csproj:3`，`netstandard2.1`、`LangVersion 14.0`） |
| 源码规模 | 21 个 `.cs`，约 3454 行（反编译产物 `缓存文件夹\decomp_ws\3832823827`，反编译时间 2026-11-27 19:37） |
| 对外接口 | 实现 `IMod`（`ConflictScannerMod.cs:7`）+ 4 个 Harmony Patch + 1 个 `ModHook` 订阅（`OnModConfigApplied`） |
| 声明能力（推测） | 应为 `files`：源码含 `File.ReadAllBytes` / `File.ReadAllText` / `Directory.GetFiles`（`ConflictEngine.cs:266,454,557`、`HotspotScan.cs:348`），而 `ModScanner` 对 `System.IO.*` 调用点一律记 `files`（`ModScanner.cs:681-685`）。反编译目录里没有 `manifest.xml`，**无法直接核对**所以标（推测）。 |
| 外部依赖 | 无自带第三方 DLL。IL 解析用的是**游戏自带的 `DseCecil`**（`DseCecil` 与 `ModScanner.cs` 用的是同一套，`HotspotScan.cs:5-7`），Harmony 用游戏自带的 `0Harmony`（`csproj:18`） |

**证据口径**：本文所有结论均来自上述反编译源码逐行阅读；涉及原版行为处，均用本机 `共享开发\reference\Assembly-CSharp\`（948 个类，反编译原版）交叉核对并标出原版行号。反编译产物**没有注释**，所以第 3 节的"为什么这样分级"是从面向玩家的字符串（`DescZh`、`L.T(...)` 文案、设置项 tooltip、报告正文）反推的，我会明确标这是"文案证据"而不是注释。凡读不出的，标（推测）。

## 0. 先纠正任务书里的一个前提

任务描述说"它没有 HarmonyPatch（只有 `OnModConfigApplied` 一个 ModHook）"——**这条是错的**。实际有 4 个 Harmony Patch，`ModHook` 只有 1 个订阅：

| 文件 | 目标 | 类型 |
| --- | --- | --- |
| `PatchModElementInit.cs:12` | `ModElement.Init(string,string)` | Postfix，染色 |
| `PatchPopulateLayoutList.cs:12` | `ModUIManager.PopulateLayoutList()` | Postfix，重扫 + 全体染色 |
| `PatchOpenPanel.cs:12` | `ModUIManager.OpenPanel()` | Postfix，重扫 + 打日志 |
| `PatchGetModIssue.cs:12` | `ModUIManager.GetModIssue(LoadedMod)`（**private static**） | Postfix 改写 `__result`，悬浮理由 |
| `ConflictScannerMod.cs:64-65` | `ModHook.OnModConfigApplied` | `+= OnConfigApplied` |

原版签名已核对：`ModElement.Init(string id, string name)`（`reference\ModElement.cs:37`）、`ModUIManager.OpenPanel()`（`:122`）、`ModUIManager.PopulateLayoutList()`（`:383`）、`ModUIManager.GetModIssue(LoadedMod)`（private static，`:913`）。四个目标都**无重载**，所以它敢写 `AccessTools.Method(typeof(X), "Init", (Type[])null, (Type[])null)`；我们自己的补丁一旦命中重载方法必须给显式 `Type[]`，不能照抄这个写法。

## 1. 它到底扫描什么

### 1.1 数据来源（四类，都不是"文件哈希全库比对"）

| 来源 | 读法 | 代码位置 |
| --- | --- | --- |
| 内存里的模组清单 | `ModLoader.Mods`（注意：原版**每次访问都 new 一个 List**，`reference\ModLoader.cs:87`） | `ConflictEngine.cs:97,160` |
| `manifest.xml` 原文 | `File.ReadAllText(Path.Combine(mod.FolderPath,"manifest.xml"))`，只用两个正则抠 `<Conflicts><Mod>…</Mod></Conflicts>` | `ConflictEngine.cs:260-282` |
| 模组 DLL 字节 | `File.ReadAllBytes` → `SHA256` → `Convert.ToBase64String` | `ConflictEngine.cs:454-458,600-604` |
| 模组 DLL 的元数据 + IL | `DseCecil` 的 `AssemblyDefinition.ReadAssembly(new MemoryStream(bytes), new ReaderParameters{ InMemory = true, AssemblyResolver = resolver })`，`ReadingMode` 先 1 后 2 兜底 | `HotspotScan.cs:418-433,364-377` |

关键点：**它自己没写 PE/IL 解析器**，而是复用游戏内置的 `DseCecil`（Mono.Cecil 的分叉，游戏自己的 `ModScanner` 也用它）。全过程只做 `Read`，没有一处 `Assembly.Load`，所以"不加载任何模组代码"这句宣传在源码层面成立。同样，它也**不执行**模组的 `Init`，不读 `ModData`，不碰存档。读取失败不崩：`Fail()` 只累加 `LastReadFailures` 并保留前 6 条错误文本（`HotspotScan.cs:435-448`），最终由 `ConflictScannerMod.Report()` 写进日志（`:117-121`）。

### 1.2 冲突类别全集（3 个 `ConflictKind` + 2 个旁路通道）

`ConflictKind` 只有三个值（`ConflictKind.cs`）：`Declared` / `Assembly` / `Hotspot`。另外有两类"不对偶"的信息不进 `ConflictEntry`：单模组自风险 `ModStatus.SelfRisk`，以及载入期错误 `LoadWatchdog`。

**A** 作者声明不兼容（模组对 · `Declared` · 恒 `High` · `ConflictEngine.cs:247-298`）｜**B** 同名 DLL 不同构建（模组对 · `Assembly` · 恒 `Warn` · `:434-518`）｜**C** 高危补丁重叠，25 个固定靶点（模组对 · `Hotspot` · 按类别 `High`/`Warn`/不报 · `:300-370` + `HotspotScan.cs`）
**D** 单模组自风险，存链+全局命名（不进 `All` · `High`/`Warn` · `:372-406`）｜**E** 载入期引擎错误（不进 `All` · UI 直接判红 · `LoadWatchdog.cs`）｜**F** 载入失败，游戏自己归因（不进 `All` · `ConflictEngine.cs:136-139`、`ModMenuHook.cs:47-51`）

### 1.3 每类的判定算法（比较什么、阈值、怎么排误报）

**A. `Declared`（作者声明）**——`ConflictEngine.cs:247-298`
1. 跳过自己（`SelfId` 取 `manifest.ID`，缺省 `"conflict_scanner"`，`:48-59`）；
2. 读该模组 `FolderPath` 下的 `manifest.xml` 原文，先做一次零成本的 `IndexOf("Conflicts")` 预筛，不命中就 `continue`（`:273`）；
3. 正则 `<\s*Conflicts\s*>(.*?)<\s*/\s*Conflicts\s*>`（`IgnoreCase|Singleline`）取块，再用 `<\s*Mod\s*>(.*?)<\s*/\s*Mod\s*>` 逐条取 id，`Trim()`；
4. 排除：空串、与自己相同（`OrdinalIgnoreCase`）、**目标 id 必须已经在 `byId` 里**（即对方确实被加载器发现）。→ 这是主要的误报抑制：声明一个没装的模组不会报。
5. 严重度硬编码 `High`（`:293`）：作者亲自写的"不兼容"，不做折扣。

值得记一笔：`ModManifest` 原版字段只有 `ID/Name/Author/ModVersion/Description/GameVersions/Prerequisites/Assemblies/Capabilities`（`reference\ModManifest.cs:3-21`），**没有 `Conflicts`**。原版解析是 `XDocument.Load` + 按名字取已知节点（`reference\ModLoader.cs:1099-1189`），未出现对未知节点的拒绝逻辑。所以 `<Conflicts>` 是本模组**私有的、事实上的社区约定**，只有装了检测器的玩家能看见（游戏本体忽略它）。这一点对我们极有用，见第 7 节。

**B. `Assembly`（同名 DLL 不同二进制）**——`:434-518`
1. 对每个模组取 DLL 列表 `DllsOf()`：**优先 `Manifest.Assemblies`**，只有该列表为空才 `Directory.GetFiles(folder,"*.dll",AllDirectories)`（`:533-565`）——即优先相信 manifest，避免把 `libs/` 里的私有依赖也当成"和别人撞名"；
2. `ShippedByGame(fileName)` 过滤游戏自带同名程序集：`Application.dataPath/Managed/<name>` 存在，或 `ModLoader.IsGameAssemblyName(去扩展名)` 命中即跳过（`:574-598`，结果按文件名缓存进 `GameAssemblies`，`:38`）→ 防"大家都随手带了 `Newtonsoft.Json.dll`"式刷屏；
3. 同一模组内同名 DLL 只取第一个（`:472-481`）；
4. 逐对比较：**同 size 且同 SHA256 → 不报**；否则报 `Warn`（`:504`）。
5. 阈值/口径：不比较"版本号"，只比较二进制；语义词由文案给出——"带了同名但版本不同的组件（x.dll），一般没事，偶尔会崩"（`:661`）。

**C. `Hotspot`（高危补丁重叠）**——`ConflictEngine.cs:300-370` + `HotspotScan.cs`
这是它唯一"看得懂 IL"的部分，也是最容易被误解的部分：**它不是"任意两个模组 patch 同一个方法就报"，而是拿一张 25 条的固定靶表去匹配**。靶表 `Targets`（`HotspotScan.cs:29-181`）分 5 类：

| CategoryKey | 目标（TypeName.MethodName） | 数量 | `DangerOf` |
| --- | --- | --- | --- |
| `savewrite` | `PlayerStore.SaveGame / LoadGame / WriteSlotFile / InitialSave / StartNewGame / Reset` | 6 | **0（不产生对偶冲突）** |
| `itemname` | `GameItem.get_name / get_shortDescription / GetDisplayName` | 3 | 2 → `High` |
| `loc` | `LocHelper.SetLocale`；`FontLoader.*`；`TMP_Settings.*` | 3 | 2 → `High` |
| `sprite` | `SpriteDict.GetSprite`；`RenderHandler.LoadFromAtlasRaw / LoadFromAtlas` | 3 | 1 → `Warn` |
| `itemmeta` | `GameItem.GetNegociatedValue / GetCurrentValue / GetValue / MayStack / GetSplitItem / ValidateShapeState / MayTarget / CanTarget / Target`；`LocHelper.GetLocalizedClientName` | 10 | 1 → `Warn` |

其中 `FontLoader`、`TMP_Settings` 的 `MethodName = null`，`MatchTargets` 里 `hotspotTarget.MethodName == null` 时**整个类型命中即算**（`HotspotScan.cs:683`）——即这两个是"类级"目标，其余 23 个是"类型+方法名级"。

采集侧（`HotspotScan.Scan`，`:211-261`）：一个共享的 `DefaultAssemblyResolver`，搜索目录 = `Application.dataPath/Managed` + 每个模组 DLL 所在目录（`:263-297`），逐 DLL `Read` 后遍历 `Modules → Types → (CustomAttributes, Methods, NestedTypes)`（`:330-416,450-515`），命中写入 `"category|Type|Method"` 字符串集合（`:685`）。两条识别路径：

- **特性路径**：`attr.Constructor.DeclaringType.Name == "HarmonyPatch"` → 从构造参数/命名参数里抓 `declaringType`（`TypeReference`）与 `methodName`（`string`）（`:517-538,703-802`）。注意它只认**类型名**，不校验命名空间（`HarmonyLib` 或第三方同名特性同样会命中）。
- **IL 路径**：方法体里必须**同时**出现 (a) 对 `HarmonyLib.Harmony.Patch / PatchAll / CreateAndPatchAll` 的调用（`:655-676`），(b) 至少一条**操作数是 `TypeReference`** 的指令（`:591-605`，源码按操作数类型判定，不看具体 opcode），(c) 至少一个**长度 1..120 的字符串操作数**，且该字符串**长得像方法名**（`LooksLikeMethodName`：首字符字母/`_`，其余只允许字母数字 `_` 和 `.`，长度 ≤200，`:804-822`）。三个都满足才把 (b)×(c) 做笛卡尔积去匹配靶表（`:642-652`）——即典型写法 `AccessTools.Method(typeof(Target), "MethodName")` 或 `Harmony.Patch(AccessTools.Method(...))`。

配对侧（`ConflictEngine.CollectHotspots`，`:300-370`）：把"命中 → 模组"翻成"命中 → 模组列表"，**每个命中类目下所有两两组合**都生成一条 `ConflictEntry`，`Severity = DangerOf(cat) < 2 ? Warn : High`；`DangerOf == 0` 直接 `continue`（`:340-344`）——所以 6 个存链方法**永远不会**单独产出红色对偶冲突（见下面 D：它们只用于放大单模组自风险）。三模组同命中一个类目 → 生成 3 条对偶条目。

**D. `SelfRisk`（单模组自风险）**——`:372-406`
按模组汇总它自己命中的类目集合，然后：
- 命中 `savewrite` **且** `itemname` → `SelfRisk = High`；
- 否则命中 `itemname` **或** `loc` → `SelfRisk = Warn`。

对应文案（`:629`）是"此模组同时改写存档读写和全局物品名称——这类组合曾把语言设置和存档一起搞坏（菜单变英文、读不了档）"，日志里这条被直接命名为 `CS-weapon-case profile`（`ConflictScannerMod.cs:162`）。**这是全模组唯一"带经验值"的规则**：它不是对偶冲突，是"这类模组单独装也可能坏档"的先验知识。

**E/F. 载入期**：见第 2 节。

### 1.4 误报抑制手段（可直接抄的清单）

1. 自己永远不和自己配对（`Add` 里 `A == B` 直接丢，`ConflictEngine.cs:203`）。
2. 同一条冲突只留一次：`PairKey = min|max|Kind|Detail`（`ConflictEntry.cs:19-31`），`Add` 里线性查重（`:207-213`）。A→B 与 B→A 视为同一条。
3. 声明冲突要求"对方确实存在"（`:285`）。
4. 游戏自带 DLL 不参与同名冲突（`:574-598`）。
5. 二进制完全一致的同名 DLL 不报（`:504`）。
6. 命中靶表之外的东西一律不报（`DangerOf` 默认 0，`HotspotScan.cs:193-195`）。
7. 只读 `Manifest.Assemblies` 声明的 DLL，减少对 `libs/` 私有依赖的误判（`:533-565`）。
8. UI 层再把"小摩擦"整体折叠掉（第 3 节），日志也只在开启时才展开黄色条目（`ConflictScannerMod.cs:184`）。

### 1.5 它**不**检测什么（对我们最要紧的一节）

源码里既没有对应代码、也没有对应文案，因此可以判定为空白：

| 常见冲突形态 | 本模组是否检测 | 依据 |
| --- | --- | --- |
| 同一 `GameItem` identifier 注册两次 | **否** | 全仓库无 `DirectoryMaster` / `identifier` 相关判定 |
| 同一 manifest ID 重复 | **否** | 原版加载器自己会 `LogWarning("Duplicate mod ID … skipping")`（`reference\ModLoader.cs:1449`），本模组不重复造轮子 |
| 语言表同 key 覆盖 | **仅类别级** | 只有 `LocHelper.SetLocale` + `FontLoader`/`TMP_Settings` 三个靶点，不比对 locale 文件 key |
| 贴图 atlas 同名 | **仅方法级** | 只到 `SpriteDict.GetSprite` / `RenderHandler.LoadFromAtlas*` 三个靶点，不比对 atlas 文件名 |
| 任意方法被两个模组 Patch | **仅 25 个白名单靶点** | `HotspotScan.Targets` |
| Harmony 重载/参数级差异 | 否 | 只比"类型名后缀 + 方法名"，不比签名 |
| 能力声明冲突（caps）/ `Prerequisites` 缺失 | 否 | 原版 `ModTrustList`、`ModUIManager.GetModIssue` 已算（`reference\ModUIManager.cs:938-953`），本模组不读 `Manifest.Capabilities`，只做追加 |
| 存档字段互相覆盖（ES3 key 撞车） | 否 | 无 ES3 键空间分析（`L.cs` 里读 `locale` 只是为了自己中英切换） |

**这一节比"它检测什么"更重要**：我们两个大模组最怕的 identifier 重复、语言 key 覆盖、atlas 同名，它一个都不查。所谓"我们和它兼容"只意味着"不会在它的报告里被标红"，不等于"我们没问题"。

## 2. 载入期错误归因：它到底怎么知道"那一刻在加载谁"

**答案：不拦截任何 `ModLoader` 方法，纯读日志文本。** 实现只有 300 行（`LoadWatchdog.cs`），四个前缀常量硬编码在 `:13-19`：`"[ModLoader] Loading: "`（加载开始）/ `"[ModLoader]   Initialized: "`（成功，后跟 `<Name> v<Version>`）/ `"[ModLoader]   Init failed: "`（失败）/ `"[ModLoader] "`（通用）。

这三条不是猜的，原版确实这样打日志：`Debug.Log("[ModLoader] Loading: " + fileName)`（`reference\ModLoader.cs:1589`，注意**打的是 DLL 文件名，不是模组名**）、`Debug.Log("[ModLoader]   " + (flag ? "Initialized" : "Init failed") + ": " + manifest.Name + " v" + manifest.ModVersion)`（`:1662`）、资源加载完成行 `$"[ModLoader] {mod.Manifest.Name}: {mod.Textures.Count} texture(s), … loaded"`（`:1733`）、`… sound(s) loaded`（`:1875`）。

### 2.1 归因链（状态机）

1. `Arm()` 挂 `Application.logMessageReceived += OnLog`（`:81-90`），`OnDisable` 里 `Disarm()` 摘掉（`:92-103`）。只关心 `LogType` = `Error(0)` / `Exception(4)` / `Warning(1)`，其余类型仍喂给 `TrackMarker` 但不入库（`:105-128`）。
2. 看到 `Loading: X.dll` → `FlushPending(null)`（把上一批取不归属的错误落库为"未归属"）+ 置 `_pendingDll = true`（`:136-141`）。
3. 在 `_pendingDll == true` 期间，任何错误/异常/警告进 **`Pending` 缓冲**而不是立刻入库（`:228-235`）。
4. 看到 `Initialized: <Name> v<Ver>` 或 `Init failed: …` → `TrimVersion()` 砍掉 `" v"` 及后缀（`:170-179`）→ `FlushPending(name)`：用 `ResolveId(name)` 在 `ModLoader.Mods` 里按 **`Manifest.Name` 精确匹配**反查 id（`:191-213`）→ 把缓冲里的每条错误落库并打上该 id（`:181-189`）。
5. 还额外认了两种"资源阶段"行：`[ModLoader] <Name>: … texture(s)` / `… sound(s) loaded` —— 命中就同样 `FlushPending(<Name>)`（`:151-162`）。这条很关键：`Initialized` 之后到资源加载完成之间报的错，也被归给该模组。

于是归因是"**区间归属**"：`Loading:` 与 `Initialized:` 之间的所有错误 = 该 DLL 的初始化期错误。因为 `Loading:` 打的是文件名、`Initialized:` 打的是模组名，中间没有 id，所以必须靠 `Pending` 缓冲把两端缝起来。

### 2.2 清洗与限额

- 只留首行（`FirstLine`，去 `\r\n` 之后的部分，`:295-299`），并截断到 180 字符（`:222-227`）——避免把整个堆栈糊进 UI；同 `(ModId, Message)` 合并计数 `Count++`，`Type` 取更严重的（`Worse()` 排序：`Exception(4) > Error(0) > Warning(1)`，`:270-293`）。
- 全局上限 60 条（`MaxIncidents`，`:9,264`），到顶就丢新条目。
- 未被归因的错误单独统计：`UnattributedCount`（`:31-45`），报告里单列一段"另有 N 条错误出现在无法归属的时刻"（`ReportWindow.cs:325-343`）。
- UI 侧消费：`ModMenuHook.ConflictText` 给每条加 `[载入期错误]` 前缀（`ModMenuHook.cs:36-39`）；`LoadWatchdog.HasHard(modId)` 为真 → 该模组菜单项**直接染红**（`:109-112`）。

### 2.3 这套归因的天然盲区（读代码可证）

1. `ResolveId` 按 `Manifest.Name` 匹配：**两个模组同名时归因给第一个**（`:203`，遍历取首个相等）。
2. `TrimVersion` 用 `LastIndexOf(" v")`：模组名里自带 `" v"` 会被误砍（`:173-178`）。
3. 只认 `[ModLoader] ` 前缀，其它系统（引擎资源、Unity 自身）的错误全部落到"无法归属"。
4. 没有跨帧保护：`_pendingDll` 一旦置位，直到下一个 `Initialized/Init failed/texture(s)` 才清；若某模组 `Init` 抛异常导致后续两个信号都不出现，**后面别的系统报的错也会被算到它头上**（缓冲不清空）。另外 `Application.logMessageReceived` 只能拿到 `Debug.Log*` 通道，原生崩溃（access violation）不经过这里。

报告里自己写了免责（`ReportWindow.cs:200`）："这类引擎错误通常是模组在错误时机调用了游戏的异步资源接口，不一定是两个模组互相冲突……若没有红色对象，就单独禁用它试试"。**这句是它最诚实的地方，也是我们最该抄的态度**：归因 ≠ 定罪。

## 3. 严重度分级与折叠策略

`ConflictSeverity` 只有三值（`ConflictSeverity.cs`）：`None / Warn / High`。UI 用红/橙表达 `High/Warn`。

### 3.1 判定表（每条都能在代码里对上）

| 情形 | 判级 | 位置 |
| --- | --- | --- |
| 作者声明不兼容（对方已装） | `High` | `ConflictEngine.cs:293` |
| 同一类目 ≥2 模组命中 `itemname` / `loc` | `High` | `:345`（`DangerOf == 2`） |
| 同 上 `itemmeta` / `sprite` | `Warn` | `:345`（`DangerOf == 1`） |
| 同名 DLL 二进制不同 | `Warn` | `:512` |
| 单模组 `savewrite ∧ itemname` | `High`（`SelfRisk`） | `:396-399` |
| 单模组 `itemname ∨ loc` | `Warn`（`SelfRisk`） | `:400-403` |
| 有载入期错误被归因 / 有 `LoadError` | `High`（不进 `All`，UI 直判） | `ModMenuHook.cs:109-127` |
| `savewrite` 单类目重叠 | **不生成冲突条目** | `:340-344`（`DangerOf == 0`） |

聚合到"这个模组红不红"是 `ModStatus`（`ModStatus.cs`）：
```csharp
IsConflicted: (Active && SelfRisk >= High) || MaxPair() >= High
IsWarned:     !IsConflicted && ((Active && SelfRisk >= Warn) || MaxPair() >= Warn)
MaxPair():    Entries 里 BothEnabled 且 Severity 最大的那个
```
`BothEnabled` 在 `Add()` 时算一次（`ConflictEngine.cs:214`），`IsEnabledOf` 走 `Status.Active`，没有则回落 `ModLoader.IsModEnabled`（`:220-227`）；`Active` 本身由 `IsRunning()` 决定 —— `IsModEnabled` + `LoadError == null` + `TrustState` 不属于 `{Changed(4), Blocked(6), Paused(7), Stopped(8)}`（`:119-146`；枚举值见 `reference\ModTrustState.cs`）。**即"没被真正加载的模组不参与染色/计数"**，这是很重要的一条降噪：被信任层拦下的模组不会因为它和谁冲突而被标红。

### 3.2 为什么这么分（文案证据，非注释）

反编译产物没有注释，设计理由只能从面向玩家的文本反推，证据如下：

1. 模组简介第一句就定了调："**默认只把会影响游戏运行的严重冲突标红**（崩坏存档、语言或互相覆盖核心功能）。像「都改了物品价格与形状」这类小问题默认不显示"（`ConflictScannerMod.DescZh`，`:17`）；而红 = `High` 的三条判据与简介一一对应：坏档 ← `SelfRisk(savewrite∧itemname)`，坏语言 ← 类目 `loc`，互相覆盖核心功能 ← 类目 `itemname`（全局命名层被两方接管）。
2. 黄 = `Warn` 对应"小摩擦"：`itemmeta`（价格/形状/堆叠，文案：`"都修改了…，可能出现小的显示或价格异常"`，`ConflictEngine.cs:652`）、`sprite`（贴图）、同名 DLL（`"一般没事，偶尔会崩"`，`:661`）。
3. `savewrite` **刻意不进对偶表**（`DangerOf == 0`）：因为现实里大量性能/QoL 模组都会碰 `PlayerStore` 存链，两两配对会瞬间刷满红色，把真正致命的信号淹没。它只保留"存链 + 全局命名同时被同一个模组改写"这一个高价值组合（`:396-399`）——注意这是 `flag & flag2` 的**位与**，两个 bool 用 `&` 而非 `&&`，纯粹是风格，结果等价。
4. 若 UI 里没有任何红也没有任何黄，报告直接说"可以放心一起用。**红色只留给严重影响游玩的组合**"（`ReportWindow.cs:82`）。

### 3.3 折叠链路（一条设置项贯穿三处）

总开关是设置项 `show_minor_issues`，默认 `false`（`Settings.cs:33-46`，`ModConfig.For(id).Define("show_minor_issues", false, "General", …)`，注册在 `Init` 里 `Settings.Register()`，`ConflictScannerMod.cs:30`）。读取走 `ModConfig.Find(id).Get("show_minor_issues") as ConfigEntry<bool>`（`Settings.cs:9-31`）。折叠在三处一致生效：

| 位置 | 折叠行为 | 代码 |
| --- | --- | --- |
| 模组菜单染色 | `Warn` 分支要求 `Settings.ShowMinor` 才上橙色，否则用默认色 | `ModMenuHook.cs:100-103` |
| 悬浮理由 | `ConflictEngine.LinesFor(id)` 默认走 `Settings.ShowMinor`，关闭时 `where includeMinor \|\| e.Severity >= High` 过滤 | `ConflictEngine.cs:606-639` |
| F7 报告 | 黄区和"另有 N 条小问题默认隐藏…"提示段 | `ReportWindow.cs:58-67,135-176` |

改设置 → `ModHook.OnModConfigApplied` 回调 → `Invalidate()` + `RepaintAll()`（`ConflictScannerMod.cs:69-92`），当场生效不用重扫（因为只是显示层过滤）。另外每行最多 6 条，超出折叠成 `"…等 N 条"`（`ConflictEngine.cs:668-673`）。

## 4. UI 层：怎么把冲突塞进原版模组菜单

### 4.1 四条着色/注入路径

1. **行初始化后染色**：`PatchModElementInit` Postfix 拿 `__0`（`id`）调 `ModMenuHook.Apply(element, id)`。
2. **列表重建后全体重染**：`PatchPopulateLayoutList` Postfix → `Invalidate()` + `EnsureBuilt(force:true)` + `RepaintAll()`；`RepaintAll` 用 `Object.FindObjectsOfType<ModElement>()` 遍历所有行（`ModMenuHook.cs:166-186`）。这一步保证"排序/过滤/开关模组后颜色仍正确"。
3. **打开面板即重扫**：`PatchOpenPanel` Postfix → 强制重建 + `ConflictScannerMod.Report()` 打一份摘要到日志（`PatchOpenPanel.cs:15-30`）。
4. **悬浮提示追加**：`PatchGetModIssue` Postfix 把 `"与「X」都接管了全局物品名称…"` 这类文本**追加**到 `__result`（`PatchGetModIssue.cs:15-33`）。原版 `ModUIManager.RefreshRowIssues` 会把非空 issue 文本塞进 `row.warningTooltip.text` 并点亮 `warningIcon`（`reference\ModUIManager.cs:510-521`）——**所以"悬停看原因"和那个小感叹号是白捡的**，它只负责追加字符串。

### 4.2 染色细节

颜色常量：红 `#E84D44`、橙 `#E8A33A`（`ModMenuHook.cs:10-12`）。三种取值：

```csharp
Color val2 = conflictSeverity switch
{
    ConflictSeverity.Warn => WarnColor,
    ConflictSeverity.High => ConflictColor,
    _ => DefaultColor(),
};
```

`DefaultColor()` 不是硬编码白色，而是**从预制体反查**：`ModUIManager.Instance.modElementPrefab.GetComponent<ModElement>().modNameTMP.color`（`:188-226`）。因为行长文本是通过 `ScriptFontSwap.Apply` 上色的（`reference\ModElement.cs:41`），主题/字体可能改掉默认色，所以它选择"读一次预制体的真实颜色"而不是猜。这段带 `_defaultTries > 20` 上限（`:202-205`）：预制体还没就绪时最多重试 20 次，避免在 `Awake` 之前无限抛。写颜色前用 `SameColor()` 做 0.004 容差比较，相同就不写（`:139-142,149-164`）——省掉每帧无意义的 TMP 重绘。

### 4.3 F7 报告窗口（`ReportWindow.cs`）

- 窗口 id `"conflict_scanner.report"`，`CustomUIManager.Window(id, title, "overlay")`（`:74`）；已存在则先 `GetWindow(id).Close()` 再重建（`:53-57`）——单例式，不叠窗；尺寸宽 820、高 `Clamp(320 + min(条目数,12)*22, 300, 620)`（`:77-78`），`SetDraggable(true).SetCloseOnEscape(true)`。
- 用到的 `CustomUIBuilder` 接口全集（均在 `reference\CustomUIBuilder.cs` 核实）：`SetSize / SetDraggable / SetCloseOnEscape / AddHeader / AddLabel / AddSeparator / AddSpace / BeginRow / BeginScrollFlexible / AddButton / End / Show`，逐行上色用 `builder.Last.SetTextColor(...)`（`CustomUIElement.SetTextColor` 存在，`reference\CustomUIElement.cs:143`）。
- 打开即强制全量重建：`Invalidate()` + `EnsureBuilt(force: true)`（`:51-52`）——F7 永远是"现在的真相"，不吃缓存。
- 分区与拷贝同源：红区（`SelfRisk High` 各一行 + 对偶 `High` 各一行）→ 分隔线 → `BeginScrollFlexible` 滚动黄区 → 折叠提示 → `End()` → 载入期错误区（`AddHeader` 红色 + 每条红/灰）→ 免责说明 → 底部按钮行；每处 `AddLabel` 都同时 `report.Add(同样的字符串)`，最终 `report` 就是剪贴板内容（`:75` 起全程成对维护）。

### 4.4 一键复制 / 重扫 / 跳转 / 改键

```csharp
val.AddButton(L.T("复制报告", "Copy report"), (Action)(() =>
{
    GUIUtility.systemCopyBuffer = string.Join("\n", report);
```

（`ReportWindow.cs:211-230`）失败走 `Log.Error("Copy failed: …")`，不吞。其余按钮：`重新检测`（关窗再 `Show()`，`EnsureBuilt(force:true)` 已在 Build 里）、`打开模组菜单`（`ModUIManager.Instance.OpenPanel()`，`:241-254`）、`更改按键`（关窗 → `KeybindUi.Start()`）。

改键窗口是第二个 `CustomUI` 窗口（id `conflict_scanner.rebind`）。捕获方式很朴素但很稳：`KeybindUi.Poll()` 在 Beacon 的 `Update` 里先于其它输入跑，遍历 `Enum.GetValues(typeof(KeyCode))` 找 `Input.GetKeyDown`，排除 `None(0)` / `Escape(27)` / `Mouse0(323)`（`:96-116`）；`_capturing` 为真时 Beacon 直接 return，不响应 F7（`ConflictScannerBeacon.cs:41-44`）。存储用 `PlayerPrefs`（`Keybind.cs:8,35,59`），**不写存档、不写配置文件**。默认键 `(KeyCode)288` = `F7`（`Keybind.cs:10`）。按键显示名对 `Alpha1..9`、`Keypad*`、鼠标键、`Shift/Ctrl/Alt` 做了本地化（`:72-165`）。

### 4.5 常驻组件（`ConflictScannerBeacon`）

`OnEnable` 里 `new GameObject("ConflictScannerBeacon")` + `DontDestroyOnLoad` + `AddComponent`（`ConflictScannerMod.cs:58-63`），`OnDisable` 里 `Destroy` 并把静态引用置空（`:99-103`）。组件只干三件事：`Start()` 里 `WaitForSeconds(3f)` 后首次扫描并打报告（`:17-36`）；`Update` 里轮询按键（带 0.25s 去抖，`:46-50`）；每 2s 检查一次签名/语言是否变化（`:51-67`）。这是它唯一一处 Unity 生命周期代码，且没有改任何游戏对象。

## 5. 自动重扫机制

### 5.1 触发源（不是文件时间戳，是"清单签名"）

核心是 `Signature()`（`ConflictEngine.cs:95-117`）：`mods.Count` + 每个模组 `|ID(是否在跑?+:-)版本号` 拼成一个字符串。`EnsureBuilt` 每次比对签名，变了才重建（`:68-75`）：

```csharp
public static void EnsureBuilt(bool force = false)
{
    string text = Signature();
    if (force || ((!_built || !(text == _signature)) && !(NowSeconds() - _builtAt < 0.5f)))
        Rebuild(text);
}
```

- **"装了新模组"的判定 = 模组数量/ID/启用状态/版本号的组合变了**。装新模组 → `ModLoader.Mods` 多一条 → 签名变 → 下次 `EnsureBuilt` 自动重建。**不读文件时间戳，不 watcher，不比对目录哈希**。
- 轮询点在 Beacon 的 `Update`，每 2s 一次（`ConflictScannerBeacon.cs:51-63`）：`EnsureBuilt()`（便宜，只有签名比对；若 `BuildCount` 变了说明刚重建过）→ 变了就 `Report()` 写一份新日志摘要。
- 另外两个"事件驱动"点：`OnModConfigApplied`（改自己设置 → `Invalidate()`，`ConflictScannerMod.cs:75`）、`OnEnable/OnDisable`（`Invalidate()`，`:66,98`）。
- `PatchPopulateLayoutList` / `PatchOpenPanel` 用 `force: true`（无视签名与 0.5s 闸门）——玩家打开模组菜单或列表重建时**无条件全量重扫**。

### 5.2 成本控制（四处闸门）

1. **0.5 秒闸门**：`!(NowSeconds() - _builtAt < 0.5f)`，`EnsureBuilt()` 高频调用最多每 0.5s 重建一次（`:71`）。
2. **签名前置**：签名没变直接返回，O(n) 字符串比对，不做 IO。
3. **按键去抖**：`_nextAllowed = Time.unscaledTime + 0.25f`（`ConflictScannerBeacon.cs:46-50`）。
4. **展示截断**：每模组 6 行（`ConflictEngine.cs:668`）、载入事件 60 条（`LoadWatchdog.cs:9`）、报告里载入条目最多 10/12 条（`ReportWindow.cs:306,335`）、日志里红色/黄色各 `Take(15)`（`ConflictScannerMod.cs:173,185`）。

### 5.3 成本真相（老实说：它并不便宜）

`Rebuild` 每次都做全套（`:153-199`）：`CollectDeclarations`（每模组读一次 manifest.xml）→ `CollectAssemblyClashes`（**每 DLL 读全字节 + SHA256**）→ `CollectHotspots`（**每 DLL 再用 Cecil 读一遍并遍历全部类型/方法/IL**）→ `CollectSelfRisks`（纯内存）。同一份 DLL 在一次 Rebuild 里被读两次；配 `DseCecil` 的 `InMemory: true` 与共享 resolver。

而 `PatchPopulateLayoutList` 是 `force: true`：只要玩家动一下模组列表（排序、开/关、过滤），就是一次全量重扫。这解释了它为什么把 `EnsureBuilt()` 也放在行染色的最前面（`ModMenuHook.cs:80`）——**染色前必须先保证引擎是新的**，代价是每行一次签名比对（便宜）。结论：模组数 50+ 且 DLL 体积大时，打开模组菜单会有可感知的一次卡顿；`0.5s` 闸门只保护 `Update` 轮询那条路径，保护不了 `force`。

## 6. 可复用模式（4 个）

### 范式 1 · 用"规则表 + 危险度函数"代替散落 if（`HotspotScan.cs:29-36, 183-196`）

```csharp
	private static readonly HotspotTarget[] Targets = new HotspotTarget[25]
	{
		new HotspotTarget
		{
			TypeName = "PlayerStore",
			MethodName = "SaveGame",
			CategoryKey = "savewrite"
		},
```
```csharp
	internal static int DangerOf(string categoryKey)
	{
		switch (categoryKey)
		{
		case "itemname":
		case "loc":
			return 2;
		case "itemmeta":
		case "sprite":
			return 1;
		default:
			return 0;
		}
	}
```

范式要点：**数据（靶表）/ 严重度（DangerOf）/ 匹配（`MatchTargets` + `TypeMatches` 后缀比对）/ 聚合（`CollectHotspots` 的笛卡尔积）四层完全分离**（上块为节选：只留第一条靶点，完整 25 条见 `HotspotScan.cs:29-181`）。加一个新靶点只改数组，加一档危险度只改 `DangerOf`，UI 与文案层完全不用动；`DangerOf` 返回 0 的类目天然"只采集不报警"，这才让 `savewrite` 既能喂给 `SelfRisk` 又不会刷屏。

### 范式 2 · 只读扫描 + 签名节流（`ConflictEngine.cs:95-117`）

```csharp
	private static string Signature()
	{
		List<LoadedMod> mods = ModLoader.Mods;
		if (mods == null)
		{
			return "0";
		}
		StringBuilder stringBuilder = new StringBuilder(mods.Count * 24 + 8);
		stringBuilder.Append(mods.Count);
		for (int i = 0; i < mods.Count; i++)
		{
			LoadedMod val = mods[i];
			if (val == null || val.Manifest == null)
			{
				stringBuilder.Append("|?");
				continue;
			}
			string text = val.Manifest.ID ?? "";
			stringBuilder.Append('|').Append(text).Append(IsRunning(val, text) ? "+" : "-")
				.Append(val.Manifest.ModVersion ?? "");
		}
		return stringBuilder.ToString();
	}
```

范式要点：**用"最小充分指纹"当缓存键**——只要 `count + id + 启用态 + 版本` 变化就必然重算，无需监听文件系统、无需 watcher、也不需要自己维护"已扫过的模组表"。`StringBuilder` 预分配 `mods.Count * 24 + 8`（每模组估算 24 字符），避免循环里扩容。配套三件套：`Invalidate()` 清 `_built/_builtAt/_signature`、`EnsureBuilt()` 做"变化就重建 + 0.5s 防抖"、`BuildCount` 对外暴露代次（供 `Update` 里判断"刚才是不是重建了"）。这套模式可以直接搬到我们自己的缓存/索引层。

### 范式 3 · 用日志区间做归因（`LoadWatchdog.cs:136-167`）

```csharp
		if (message.StartsWith("[ModLoader] Loading: ", StringComparison.Ordinal))
		{
			FlushPending(null);
			_pendingDll = true;
			return;
		}
		string text = null;
		if (message.StartsWith("[ModLoader]   Initialized: ", StringComparison.Ordinal))
		{
			text = TrimVersion(message.Substring("[ModLoader]   Initialized: ".Length));
		}
		else if (message.StartsWith("[ModLoader]   Init failed: ", StringComparison.Ordinal))
		{
			text = TrimVersion(message.Substring("[ModLoader]   Init failed: ".Length));
		}
		else if (message.Length > "[ModLoader] ".Length)
		{
			int num = message.IndexOf(": ", "[ModLoader] ".Length, StringComparison.Ordinal);
			if (num > "[ModLoader] ".Length)
			{
				string text2 = message.Substring(num + 2);
				if (text2.Contains(" texture(s)") || text2.Contains(" sound(s) loaded"))
				{
					text = message.Substring("[ModLoader] ".Length, num - "[ModLoader] ".Length).Trim();
				}
			}
		}
		if (text != null)
		{
			FlushPending(text);
			_pendingDll = false;
		}
```

范式要点：**不侵入对方代码，只在对方已有的日志契约上做括号匹配**。开始标记置位 → 期间的错误进缓冲 → 结束标记到达时统一贴标签。三个设计细节值得抄：(1) 前缀全用 `StringComparison.Ordinal` 且常量提出来复用，避免各处拼字符串拼错；(2) "结束标记"不止一个，把资源加载完成行也算作区间右界，否则资源期错误永远归属不到；(3) 结束标记解析不出 id 时（`FlushPending(null)`）**宁可标"未归属"也不硬猜**，并在 UI 里单列一段，让玩家自己动手二分。

### 范式 4 · 对原版 UI 只做 Postfix 增量（`PatchModElementInit.cs:7-29`）

```csharp
[HarmonyPatch]
internal static class PatchModElementInit
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(ModElement), "Init", (Type[])null, (Type[])null);
	}

	private static void Postfix(ModElement __instance, string __0)
	{
		try
		{
			ModMenuHook.Apply(__instance, __0);
		}
		catch (Exception ex)
		{
			ModLog log = ConflictScannerMod.Log;
			if (log != null)
			{
				log.Warning((object)("Row colouring failed: " + ex.Message));
			}
		}
	}
}
```

范式要点：**永远 `Postfix`、永远不返回 `false`、永远不在补丁里改游戏状态，只在最后"补一刀显示层"**。`ModElement.Init` / `PopulateLayoutList` / `OpenPanel` / `GetModIssue` 是全服最拥挤的补丁面（工作区 `共享开发\historical\COMPAT_MAP.md` 也标了"绝对不要碰"），而它四个补丁全是 Postfix + 追加，最坏情况只是颜色不对，不会让别的模组补丁失效。另外两条工程细节：`try/catch` 只包在补丁边界并 `Log.Warning` 留痕（符合"对内暴露、边界捕获"）；`AccessTools.Method` 用 `MethodBase TargetMethod()` 而不是写死 `[HarmonyPatch(typeof(X), "...")]`，好处是目标缺失时只会**跳过这一个补丁类**，不会让整批 `PatchAll` 抛 `AmbiguousMatchException` ——代价是这里传了 `(Type[])null`，**只在目标无重载时安全**，我们照抄时必须给 `new Type[]{...}`。

## 7. 我们该怎么用（写进《模组兼容性自查清单》）

以下 10 条，每条都锚在上面已核实的源码事实上。第 1~4 条是"别被这个检测器判红"，第 5~10 条是"它查不到的坑，只能我们自己查"。

1. **别让两个模组同时接 `GameItem.get_name` / `get_shortDescription` / `GetDisplayName`，或同时接 `LocHelper.SetLocale` / `FontLoader` / `TMP_Settings`**——这四类（`itemname`、`loc`）是本模组唯一的红色对偶判据（`HotspotScan.DangerOf` 返回 2，`ConflictEngine.cs:345`）。我们要改物品显示名，就走自己物品的字段/`EnableTag`，绝不在全局 `GameItem.get_name` 上挂 Postfix。

2. **不要成为"同时改写存档链 + 全局物品名"的模组**——`savewrite ∧ itemname → SelfRisk High`（`ConflictEngine.cs:396-399`），文案直接点名"这类组合曾把语言设置和存档一起搞坏"。我们要保存数据就用模组私有键 + 自己的 ES3 文件，不接 `PlayerStore.SaveGame/LoadGame/WriteSlotFile/InitialSave/StartNewGame/Reset` 这六个方法（它们恰好就是 `savewrite` 的靶表全集）。

3. **给原版 UI 加东西只允许 `Postfix` + 追加，绝不 `Prefix` 返 `false`**——这是它自己四个补丁的做法（`PatchModElementInit/PopulateLayoutList/OpenPanel/GetModIssue`），也是让"我们的模组 + 别的 UI 模组 + 本检测器"能共存的前提。整批 `PatchAll` 里任何一个补丁抛异常/写错签名都会连累别人。

4. **命中的原版方法有重载时，必须写显式参数类型表**——`AccessTools.Method(typeof(X), "Init", (Type[])null, (Type[])null)` 这种写法只在这个方法唯一时成立（本模组四个目标恰好都无重载，已核对原版签名）。我们一旦给 `DirectoryMaster.Item`、`PlayerStore.SaveGame` 这类重载方法打补丁，必须 `new Type[]{ typeof(string), typeof(bool) }`。

5. **注册自定义 `GameItem` identifier 前先 `DirectoryMaster.Has<GameItem>(id)`**（游戏 API，非本模组源码）。理由来自它**不检测**这一项（第 1.5 节）：identifier 撞车不会被任何红黄提示覆盖，而原版加载器也不会拦，只能靠我们自查。

6. **语言表 key 冲突必须自己查**——本模组对语言只做"两个模组都接 `LocHelper.SetLocale`/`FontLoader`/`TMP_Settings` 就报红"的类别级判断，不比对任何 locale 文件、不比对 key。我们自己的 key 一律带模组前缀（如 `dsp_*`），并在发布前 grep 一遍第三方模组的语言文件是否已占用同名 key。

7. **贴图 atlas 同名只能自己查**——它只到 `SpriteDict.GetSprite` / `RenderHandler.LoadFromAtlas*` 的方法级重叠，不比对 atlas 路径与 sprite 名。自定义图集一律走 `Items/<模组id>_*` 之类的前缀命名。

8. **想主动告知玩家"我们有已知不兼容"时，就在 `manifest.xml` 写 `<Conflicts><Mod>other_mod_id</Mod></Conflicts>`**——只有本检测器读它（正则 `ConflictEngine.cs:40-42`），原版解析器按名字取已知节点、未出现拒绝未知元素的逻辑，所以对游戏本体无副作用；一旦对方也被装且被加载器发现，玩家的模组菜单里就是红色 + 作者声明文案（`:287-295,661`）。这是零成本、零能力的"兼容性声明"通道。

9. **模组初始化阶段（`Init` / `OnEnable`）不要碰需要资源就绪的异步接口或本地化查询**——它自己的报告把这类引擎错误（如 `invalid operation handle`）定性为"模组在错误时机调用了游戏的异步资源接口"（`ReportWindow.cs:200`）。我们要读本地化、贴图、字体，一律等 `ModHook.OnGameLoadedInit` / `OnModAssetsLoaded`。

10. **别把"没被检测器标红"当成"我们没问题"**——它的 `Hotspot` 只看 25 个白名单靶点，`Declared` 只看作者自己写的声明，`Assembly` 只看同名 DLL（第 1.5 节完整清单）。发布前我们必须自己跑一遍：identifier 唯一性、语言 key、atlas 名、存档键空间、以及"是否同时改存链与全局命名"这五件事。

## 8. 附：文件地图与硬常量

| 文件 | 行数级 | 职责 |
| --- | --- | --- |
| `ConflictEngine.cs` | 725 | 引擎：签名/重建/三类冲突/自风险/文案生成/计数 |
| `HotspotScan.cs` | 823 | Cecil 静态扫描：靶表、特性与 IL 两条识别路径、失败统计 |
| `LoadWatchdog.cs` / `ReportWindow.cs` | 300 / 393 | 载入期日志区间归因；F7 报告窗口（`CustomUI`）+ 剪贴板 |
| `ConflictScannerMod.cs` / `ModMenuHook.cs` | 207 / 239 | `IMod` 入口、本地化、日志摘要、Beacon 挂载；行染色、悬浮文本、默认色反查 |
| `Keybind.cs` / `KeybindUi.cs` / `Patch*.cs`×4 | 171 / 118 / 30·32·32·34 | 按键存储（`PlayerPrefs`）与改键窗口；四个 Postfix 补丁 |
| `Settings.cs` / `L.cs` / `ModStatus.cs` / `ConflictEntry.cs` / `ConflictKind.cs` / `ConflictSeverity.cs` / `LoadIncident.cs` / `ConflictScannerBeacon.cs` | 48/99/57/41/8/8/14/69 | 设置、双语（读 ES3 `locale`，`:58-80`）、状态聚合、数据模型、常驻组件 |

硬常量速查：红色 `#E84D44`、橙色 `#E8A33A`、灰色 `#9A9A92`；默认键 `F7`（`KeyCode 288`）；窗口 id `conflict_scanner.report` / `conflict_scanner.rebind`；设置键 `show_minor_issues`（默认 false）；`PlayerPrefs` 键 `conflict_scanner.report_key`；重建闸门 0.5s；轮询 2s；按键去抖 0.25s；每模组展示 6 行；载入事件上限 60 条/单条 180 字符；`SelfId` 缺省 `conflict_scanner`。
