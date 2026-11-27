# Vanilla Food Expansion｜工坊 3830798369

> 反编译源码 `缓存文件夹\decomp_ws\3830798369\`（**38 个 .cs / 9,201 行**：`VisualTestDraft\ModEntry.cs` 77 行是 IMod 入口，其余 37 个在 `ProbableFoodPack\`，外加 `Properties\AssemblyInfo.cs`）。
> 包内 manifest 实测（**先看这个再断言**）：`<ID>visual_probe_20261031_three</ID>`、`<Name>Vanilla Food Expansion</Name>`、`<ModVersion>0.3.10</ModVersion>`、`<Description />`、`<GameVersions />` 全空、`<Assemblies><Assembly>VisualTestDraft.dll</Assembly>`、`<Capabilities />` 自闭零能力。包里另有 `Localization\*.csv`（8 种语言）、`Npc\*.png`（2 张原图，**代码不读**，见 §4.7）、`Textures\Items\*.png`（125 张物品图）。
> **为什么值得读**：**"只加内容"这条路的天花板样本** —— 123 条数据表定义出 111 件在架物品，一个 `Create()` 方法包办全部构造；再加两个 NPC 商人，用"首次来访留名片 → 每 7 天自己来 → 打电话约次日"的链路，把陌生人变成可召回的资源。它证明**加吃喝不一定需要一套玩法**。

## 0. 两条路先选：3 分钟选型对照

| 维度 | 本模组（纯内容扩展） | 兄弟档案 [3830523891-KitchenBusiness.md](3830523891-KitchenBusiness.md) |
| --- | --- | --- |
| 规模 | 9,201 行 / 38 文件 / 111 物品 / 2 个 NPC 商人 | 25,209 行 / 180 文件 / 51 物品 / 6 个顾客 |
| 做什么 | 把原版食物池灌满 + 两个卖货商人 + 一张会过期的名片 | 新增整套后厨经营：门控/设备/批量/订货/行情/结算 |
| 改动面 | 26 处手动 `Harmony.Patch`，集中在顾客生成、对话、物品名/描述、食物卖家换货 | 26 个特性式补丁 + 6 个 ModHook，碰定价/存档/退出/终端 |
| 存档 | 每个 NPC 一条竖线分隔字符串塞 `store.modData` | 一整段 JSON 塞 `PlayerStore.ModData()`，坏档就拒绝存档 |
| 选它当你 | 只想"货架上多东西"、要 NPC 关系但**不想做系统**、能接受 2 周内上线 | 要做**第二条经济线**、要玩家每天有新决策、能接受 2 个月 |

一句话判据：**先写数据表；数据表撑不住的玩法才写系统。** 本模组 9,201 行里 ~3,000 行是纯数据字面量。

---

## 1. 规模与结构

| 文件 | 行 | 角色 |
| --- | ---: | --- |
| `ItemCatalog.cs` | 2,223 | **95 条物品字面量** + `Create()` + `Register()` + 载入后自愈 |
| `NpcRuntime.cs` | 799 | 两个 NPC 的 Install / 排程 / 对话装配 / 立绘解码 |
| `AdditionalDrinkCatalog.cs` | 602 | 追加 27 条饮品（纯数据） |
| `VendorSpawning.cs` | 599 | **换掉原版食杂贩/小偷的食物池**（唯一侵入式改动） |
| `NpcPhone.cs` | 495 | 电话簿注册 + 邀约对话 + 存档期剥离 |
| `NpcWholesale.cs` | 490 | 批发界面里认自家货 |
| `NpcCakeStory.cs` / `NpcCakeTrade.cs` | 487 / 472 | 桂花糕专线（单一高价物品的三段剧情） |
| `NpcLocalization.cs` | 370 | 对话 8 语言查表 |
| `NpcCards.cs` | 288 | 名片：发放 / 找回 / 债务 / 第二张 |
| 其余 28 个 | ≤ 256 | `NpcState` / `NpcStock` / `NpcOptions` / `*Policy` 等单职责小类 |

数据规模：`ItemCatalog.Definitions` 95 条 + `AdditionalDrinkCatalog.Definitions` 27 条 + `CakeItemDefinition` 1 条 = **123 条定义**，其中 12 条 `Retired = true` → **111 件在架**（`ItemCatalog.cs:18, 2008`；`AdditionalDrinkCatalog.cs:5`；`CakeItemDefinition.cs:9`）。

## 2. 它怎么做这件事

### 2.1 111 件物品：一条 `ItemDef` + 一个 `Create()`，没有一件物品有专属类

- **记录**：`ItemDef`（`ItemDef.cs:3-55`）19 个字段 `Key / SourceRef / NameKey / DescriptionKey / FallbackName / FallbackDescription / Types[] / Value / Calories / Width / Height / AlcoholLevel / AudioKind / Canned / Meat / Chill / IsDrink / Weight / Retired`，外加两个计算属性 `SpawnWeight`（酒类 ×1.18 加权）与 `GameId => "visual_probe_20261031_three." + Key`。
- **表和格式**：**不是 JSON、不是 CSV，是 C# 静态初始化器里的对象字面量数组**（`ItemCatalog.cs:18-2007` 一整段 `new ItemDef { ... }`）。好处是编译期检查 + 零解析开销 + 0 外部文件；坏处见表里改一行要重编译，且 2000 行数据混在代码里。
- **注册**：`Register(ModItemDirectory)` 一条循环，`directory.Add(gameId, () => Create(def))`（`ItemCatalog.cs:2032-2040`）；钩在 `ModHook.OnModItemDirectoryInit` 上（`ItemModule.cs:37, 62-76`）。
- **构造**：`Create(ItemDef)`（`ItemCatalog.cs:2051-2100`）是**唯一**的实例化路径：
  `ItemDirectory.CreateEmptyItem` → `name/shortDescription` 走 `Localize(key, fallback)` → `unitValue` → 逐条 `SetGameItemType` → 有酒精走 `ConsumableHelper.InitAlcohol`，否则 `FoodItemHelper.InitFoodItem(cal, canned, meat)` → `SetSpriteAndShapeFromMod(modId, Key)` → 手铺 `GridShape`（`Width × Height` 全 1）→ 按 `AudioKind` 选 `AudioHelper.InitGlass / InitAluminumCan / InitCannedFood / InitCartonBox`。
- **载入后自愈**：`onLoaded += RestoreSnackTypeAfterLoad`（`:2072-2075`），读档时补回 `TREAT` 类型、恢复三格/能量块占格、并把桂花糕单独特判成 `LUXURY_ITEM`（`:2102-2135`）。**这是"数据驱动物品"必须付的税：读档后类型可能被原版洗掉，得自己补。**

### 2.2 两个商人 + 名片邀约：用的全是这套原版接口

| 环节 | 机制 | 代码 |
| --- | --- | --- |
| 登场 | 两个 `StoreClient` 在 `OnGenerateCustomer` 之后由 **`StoreClientManager.GenerateClient` 的 Postfix** 插进 `clientStack` | `NpcRuntime.cs:124, 356-388` |
| 排位 | `InsertionIndex` 找"最后一个受保护条目"之后随机落位，**绝不插到队首**、绝不挪动事件顾客 | `NpcScheduling.cs:8-19`；`NpcRuntime.cs:383` |
| 建档 | 落地那一刻 `TrackClient(val)`，并把 `Arrive(day)` 写进 `NpcState` | `NpcRuntime.cs:385, 403-419` |
| 自然周期 | `NpcState.Due(day, interval)`：`Visits == 0` 时第 2 天就能来；否则 `day >= LastVisitDay + interval`，`interval` 由设置项给 7（可 4/10） | `NpcState.cs:90-101`；`NpcOptions.cs:28, 41-49` |
| 名片 | 首次来访的**某一句对话结束**时 `NpcCards.Give(id)` → 生成原生纸张条目（`SLIP_BODY` 标签里写电话号码 51751 / 85999）塞进柜台 | `NpcRuntime.cs:683-693, 660`；`NpcCards.cs:12-19, 36-43, 104-120` |
| 电话簿 | `store.PhoneClientDict.Add(51751L/85999L, new StorePhoneClient{ locID, phoneState, cooldownDuration=4, currentCooldown, alias })` | `NpcPhone.cs:29-31, 143-197` |
| 拨号 | **`StorePhoneClient.GetCallDialog` 前缀直接换掉返回值** → 返回自定义邀约 `Dialogue`；`GetLocalizedDisplayName` 前缀换名字 | `NpcPhone.cs:90-93, 235-255, 276-349` |
| 邀约 | `NpcState.TryInvite(day)`：`InviteDay = day + 1`、`LastInviteDay = day`；4 天冷却；再 `Write` 回存档并回读校验 | `NpcState.cs:30-52`；`NpcPhone.cs:311-319` |
| 次日到 | `EnsureScheduled` 里 `Create(id, npcState.InviteDay >= 0 && npcState.InviteDay <= day)` → 被邀来的走另一套开场台词；`Arrive` 时清 `InviteDay` | `NpcRuntime.cs:382`；`NpcState.cs:54-67` |
| 存档 | 每个 NPC 一条 `store.modData["visual_probe_20261031_three.npc.<id>.v1"]`，竖线分隔 + 版本前缀 | `NpcRuntime.cs:69-87`；`NpcState.cs:103-152` |

**注意：全程没有用 `StoreClientManager.AddClient`。** 正式排程走 `clientStack.Insert(index, client)` + `TrackClient(client)`；`AddNextClient` 只出现在调试按钮里（`NpcRuntime.cs:434`）。想抄就抄 Insert+TrackClient，`AddClient` 那条路会绕开排位控制。

### 2.3 8 种语言：两条完全不同的通道

- **物品名/描述**：`Localization\<locale>.csv`（zh/en/ja/ru/de/fr/es/pl 共 8 个，每个 **246 行** = 123 定义 × 2 键），键形如 `psfood.buck_a_slice.name`；运行时由 `ModHelper.GetLocalized(modId, key, args)` 取，取不到（返回空或回显键）就回落 `FallbackName`（`ItemCatalog.cs:2214-2221`）。CSV 由 ModLoader 扫进 `LoadedMod.Localization`（`NpcItemResources.cs` 里直接打印 `val.Localization.Keys` 可证）。
- **NPC 对话 / 设置项文案**：**代码内 `string[8]` 位置数组**，键是中文原文，`LocaleIndex(locale)` 把 locale 映到 0/1/2/3/4/5/6/7（`NpcLocalization.cs` 尾部 `LocaleIndex`；`NpcOptions.cs:16-26, 105-119`）。
- 两条通道必须**手工保持同步**：改一个物品的 `NameKey` 要改 8 个 CSV；改一句台词要在 8 列里都填。没有任何编译期或运行时校验（见 §4.4）。

### 2.4 唯一的侵入式改动：把原版食杂贩/小偷的食物换成自家货

`VendorSpawning.Install` 只钩三类"确定是食物进货入口"的静态方法（`StoreClientListSupplier.AddScavFoodStock` / `AddThiefFoodStock`、`StoreClientList.AddScavGeneralStock`），外加 `PlayerStore.TryAddDirectSellingItemToTable` 与 `Dialogue.ExecuteEndAction`（`VendorSpawning.cs:76-90`）。落地时用 `Scope`（栈式当前顾客上下文）+ `OfferState`（原物品/替换物/原参数）记录，`BeforeOffer → AfterOffer → OfferFinally` 三段式回滚（`:13-44, 90`）。**`BalancePolicy` 保证自家货与原版货 1:1 轮换**，不会因为装了模组就把原版食物挤没（`BalancePolicy.cs:10-28`：`ModCount < NativeCount` 就优先选模组，反之选原生，持平随机）。钩子装不上就 `return false`，保留原版货（`ModEntry.cs:34-35` 的日志就是这么写的）。

## 3. 关键代码摘录

**(1) 111 件物品的全部注册逻辑**（`ItemCatalog.cs:2032-2040` + `:2008`）—— 一条循环，一件不多：

```csharp
Definitions = Definitions.Concat(AdditionalDrinkCatalog.Definitions)
                         .Concat(new ItemDef[1] { CakeItemDefinition.Create() }).ToArray();
// ...
public static void Register(ModItemDirectory directory)
{
    ItemDef[] definitions = Definitions;
    foreach (ItemDef itemDef in definitions)
    {
        ItemDef captured = itemDef;                       // 闭包捕获（C# 9 foreach 变量已固定，仍显式复制）
        ((Directory<GameItem>)(object)directory).Add(captured.GameId, (Func<GameItem>)(() => Create(captured)));
    }
}
```

**(2) 一个 `Create()` 吃下 19 个字段**（`ItemCatalog.cs:2051-2076` + `:2082-2098`，中间省 `SetSpriteAndShapeFromMod`）：

```csharp
private static GameItem Create(ItemDef d)
{
    GameItem val = ItemDirectory.CreateEmptyItem(d.GameId);
    val.name = Localize(d.NameKey, d.FallbackName);
    val.shortDescription = Localize(d.DescriptionKey, d.FallbackDescription);
    val.flavorText = "";
    val.unitValue = d.Value;
    foreach (string gameItemType in d.Types) val.SetGameItemType(gameItemType);
    if (d.AlcoholLevel > 0) ConsumableHelper.InitAlcohol(val, d.AlcoholLevel);
    else                    FoodItemHelper.InitFoodItem(val, d.Calories, d.Canned, d.Meat);
    if (d.AlcoholLevel == 0)
        ((GameItemFunc)val).onLoaded = (Action<GameItem>)Delegate.Combine(
            ((GameItemFunc)val).onLoaded, new Action<GameItem>(RestoreSnackTypeAfterLoad));
    val.SetSpriteAndShapeFromMod("visual_probe_20261031_three", d.Key);
    /* 手铺 GridShape：Width*Height 全 1 */
    if (d.AudioKind == 1)      AudioHelper.InitGlass(val);
    else if (d.AudioKind == 2) AudioHelper.InitAluminumCan(val);
    else if (d.Canned)         AudioHelper.InitCannedFood(val);
    else                       AudioHelper.InitCartonBox(val);
    return val;
}
```

**(3) 自然到访的插位**（`NpcRuntime.cs:364-388`，`GenerateClient` 的 Postfix 里跑）：

```csharp
int dayCounter = StoreStation.GetDayCounter();
foreach (string id in new string[2] { "cheng", "yin" })
{
    NpcState npcState = Read(id);
    if (!npcState.Due(dayCounter, NpcOptions.VisitDays)
        || __instance.clientStack.Any(c => c != null && c.identifier == "visual_probe_20261031_three.npc." + id))
        continue;                                        // 今天不该来 / 已在队列里，跳过
    StoreClientInstance current = Store.currentClientInstance;
    if (!((current?.GetClientBlueprint())?.identifier ==       // 正在接待台上的就是本人 → 不重复入队
          "visual_probe_20261031_three.npc." + id))
    {
        StoreClient val = Create(id, npcState.InviteDay >= 0 && npcState.InviteDay <= dayCounter);
        int index = NpcScheduling.InsertionIndex(__instance.clientStack,
            c => c != null && !ordinaryGenerated.Contains(c) && !IsOurClient(c), random);
        __instance.clientStack.Insert(index, val);        // 插入，不替换
        __instance.TrackClient(val);                      // 报给原版做访客追踪
    }
}
```

**(4) 电话簿条目的冷却直接写进原生字段**（`NpcPhone.cs:143-197` 核心段）：

```csharp
if (!store.PhoneClientDict.TryGetValue(Numbers[i], out var value))
{
    value = new StorePhoneClient
    {
        locID = "visual_probe_20261031_three.npc." + text,
        phoneClientType = (PhoneClientType)1,
        phoneState = (PhoneState)4,
        dialogFuncId = "",
        cooldownDuration = 4,
        currentCooldown = 0,
        alias = new List<string> { "visual_probe_20261031_three.npc." + text }
    };
    store.PhoneClientDict.Add(Numbers[i], value);
}
else if (!IsOwned(value, text)) { /* 号码被别的模组占了：跳过一次日志，之后静默 */ continue; }

value.displayName    = NpcRuntime.Name(text);
value.cooldownDuration = 4;
value.currentCooldown  = (int)Math.Max(0L, (npcState.LastInviteDay < 0) ? 0
                       : ((long)npcState.LastInviteDay + 4L - StoreStation.GetDayCounter()));
value.phoneState     = (PhoneState)((value.currentCooldown > 0) ? 5 : 4);   // 4=可拨 5=冷却
value.dialedBefore   = npcState.Dialed;
```

**(5) 按"最后一个受保护条目"之后插入**（`NpcScheduling.cs:8-19`）—— 整套排程的灵魂，19 行：

```csharp
internal static int InsertionIndex<T>(IList<T> queue, Func<T, bool> protectedEntry, Random random)
{
    int minValue = 0;
    for (int i = 0; i < queue.Count; i++)
        if (protectedEntry(queue[i])) minValue = i + 1;
    return random.Next(minValue, queue.Count + 1);
}
```

**(6) 存档编解码：宁可抛异常也不重置进度**（`NpcState.cs:103-119`）：

```csharp
public string Encode() => string.Join("|", "2", Visits, LastVisitDay, InviteDay, AcceptedChats,
        RefusalStreak, ChoiceVisit, ChatClosed ? "1" : "0", CardGiven ? "1" : "0",
        Dialed ? "1" : "0", LastInviteDay);

public static NpcState Decode(string raw)
{
    NpcState npcState = new NpcState();
    if (string.IsNullOrEmpty(raw)) return npcState;
    string[] array = raw.Split('|');
    if ((array.Length != 10 || !(array[0] == "1")) && (array.Length != 11 || !(array[0] == "2")))
        throw new FormatException("Unsupported NPC save state; progression was not reset.");
    for (int i = 7; i <= 9; i++)
        if (array[i] != "0" && array[i] != "1")
            throw new FormatException("Invalid NPC state flag; refusing to reset progression.");
    // ... 再逐字段 int.Parse + 最后一段一致性校验（:147-150）
}
```

## 4. 踩坑与红线

1. **mod id 就是物品 id 前缀，改一次全档报废**：`ItemDef.GameId => "visual_probe_20261031_three." + Key`（`ItemDef.cs:55`），而包内 `<ID>` 也是这个"测试探针"式的占位名。**作者改名的那天，所有存档里的 111 件物品全会变成未知 id**。抄就抄结构，别抄这个 id 习惯——`Key` 与 `<ID>` 一旦发布就是永久的。
2. **`manifest.xml` 是脚手架残留**：`<Description />`、`<GameVersions />` 全空，`<Assembly>VisualTestDraft.dll</Assembly>`，程序集名 `VisualTestDraft`、入口命名空间 `VisualTestDraft`。manifest 记 `0.3.10`，而 DLL 启动日志写的是 `"v0.3.5: ..."` 字样（`ModEntry.cs:35`）——**版本口径自己都对不上**。零能力是真（`<Capabilities />`），但元数据没整理。
3. **手动 `Harmony.Patch` + `AccessTools.Method(..., (Type[])null, ...)`，全靠运行时字符串解析**：26 处全部这样（`NpcRuntime.cs:123-135`、`VendorSpawning.cs:76-90`）。好处是不依赖编译器；代价是原版一改签名就 `MissingMethodException`，靠 `try { Install } catch { Uninstall(); throw; }` 把整个模块回滚（`ItemModule.cs:52-59`、`NpcRuntime.cs:103-115`）。**VendorSpawning 更保守：装不上就 `return false` 保留原版货**（`VendorSpawning.cs:94-99`）。分级降级这个层次值得学。
4. **8 语言位置数组靠人工对齐**：`string[8] { zh, en, ja, ru, de, fr, es, pl }`（`NpcOptions.cs:18-25`），插错一列不会报错，只会让德语玩家看到法语。CSV 与代码表之间也**没有任何一致性校验**（246 行 × 8 文件纯手工）。
5. **`NpcItemResources.Check` 才是真正的冒烟测试**：它同时验"工厂注册了"和"雪碧图在 `LoadedMod.Sprites` 里"（`NpcItemResources.cs:32-51`），缺哪个就打印 `factory:xxx` / `sprite:xxx` 并 dump 全部 mod 状态。**111 件物品的模组，靠这条自检比靠人眼快。**
6. **`StoreClientManager.HandleNormalClient` 前后各拍一次队列快照**（`BeforeOrdinary`/`AfterOrdinary`，`NpcRuntime.cs:333-354`），用差集挑出"这一轮原版新生成的普通顾客"，再在插位时把它们当"可被插到前面"的对象。**靠 diff 而不是靠 tag**，因为原版不给新顾客打标记。
7. **包里 2.3 MB 的 `Npc\*.png` 是死资源**：代码只读 `Assembly.GetManifestResourceStream("VFE.Npc." + id + ".png")`（`NpcRuntime.cs:467`，嵌入资源），包内同名目录从未被读。同理两张 `VFE.Npc.*.png` 与 `Npc\cheng_jianing.png` 是同一张图的两个副本。**（推测）作者留了原图忘了删**，代价是每个订阅者多下 2.3 MB。
8. **`Dialogue.SetStartAction` 里的活必须在"还是同一个顾客"时才算数**：`ForCurrentVisit(client, action)` 包一层校验当前 `currentClientInstance` 仍是这个 client 才执行（`NpcRuntime.cs:640, 653, 687`）。缺这层就会出现"玩家已送客，名片才落地"的幽灵物品。

## 5. 对我们的可用点

1. **→《深空当铺：可能是群友》`DspVisitorPhones` 的直接对照实现**：《群友》v0.7.x 刚做完"名片 + 电话约次日 + 7 天 CD"，VFE 是同一套需求的另一种解法——冷却**写进 `StorePhoneClient.currentCooldown/phoneState`**（§3 摘录 4）而不是自绘，原生电话簿就显示"还剩几天"；号码冲突用 `ReportedCollisions` 只报一次（`NpcPhone.cs:166-174`）。两版并读可校验我们的 CD 语义是否与原生一致。
2. **→ 两项目共用的"物品数据表"模板**：`ItemDef` + 单一 `Create()` + `onLoaded` 自愈（§3 摘录 1/2）就是《深空电玩》"盒装光盘/硬件"与《群友》"角色货品"该有的形状：**表里只放差异，构造只有一个函数**。当前《深空电玩》的 8 张立绘/多品类物品若还在逐个手写构造，可整表迁移。
3. **→《深空当铺：可能是群友》排位防压底**：《群友》"参考 Sakurazaki 熟客范式 + 插位防压底"目前是自研；`NpcScheduling.InsertionIndex`（§3 摘录 5）是 19 行的干净版本，且配套 `ordinaryGenerated` 差集识别"哪些是原版这轮新生成的"（`NpcRuntime.cs:333-354`），可替换我们的私有实现。
4. **→《深空电玩》库存/货源自检**：`NpcItemResources.Check` 那种"工厂 + 雪碧图双验 + 缺项打印 + dump mod 状态"的自检（§4.5），正好补齐《深空电玩》`ArcTypes.Audit()` 之外**素材层**的缺口：编译通过 ≠ 贴图在包里。
5. **→ 两项目的"别把原版挤没"准则**：`BalancePolicy` 的 1:1 轮换（`BalancePolicy.cs:10-28`）与"装不上就保留原版"的分级降级（`VendorSpawning.cs:94-99`），应写进我们所有替换原版池子的逻辑里（《深空电玩》的盗版查获/货源替换、《群友》的货池都适用）：**模组货与原版货等量替换，钩子失败即回退，绝不半替换。**
