# 影之诗卡店 / Shadowverse Card Shop（工坊 3843132012）

> `manifest.ID = piao_tcgcardshop` ｜ `ModVersion 1.0.2` ｜ 程序集 `piao_tcgcardshop.dll`（869,376 B）｜ `<Capabilities />` 空（零能力）｜ 声明游戏版本 `0.46D` ｜ 反编译源码 `缓存文件夹\decomp_ws\3843132012`（18 个 `.cs` / 4,758 行，命名空间 `piao_tcgcardshop`）｜ 透视报告 `_reports\3843132012.txt`
> **为什么值得读**：它是本库唯一一个**把 967 条结构化数据硬编进 C# 数组字面量、然后只写 3,700 行逻辑**的成品——"数据放哪"这个问题它给的答案和我们默认的（JSON/CSV）完全相反，而跑得通、492 订阅没炸。第二个看点是**整套「开包演出」是手写的 uGUI 补间**（无 Animator、无 DOTween、无原版动画），一段 `Update()` 里用 `1-(1-t)²` + `sin(πt)` 就做完了 8 张卡飞出、金色闪光和音效对点。
> 反面教材同样清楚：图鉴进度是 **967 个独立布尔键**，包内还躺着 2,527 个**代码从未引用**的贴图变体（见 §5）。

## 1. 规模与结构

| 类别 | 量 | 明细 |
| --- | --- | --- |
| 代码 | 17 个 `.cs` / 4,746 行（另 `Properties\AssemblyInfo.cs` 12 行） | 最大 `OusiMerchant.cs` 680、`ModEntry.cs` 492、`PiaoPackStage.cs` 409 |
| **数据（在代码里）** | `SvaCardsData.cs` **1,042 行 / 822,193 B** | `Row[] All = new Row[967]`，一行一张卡，源码 822 KB 全是长字符串 |
| 美术 | **173.7 MB / 6,068 个 PNG** | `Textures\Paper\` 3,521、`Textures\Items\` 2,547（`du -sb` 实测 165,104,111 + 8,584,360 B） |
| 配置 / 文案 / 音效 | `Config\` 5 个 CSV（441 KB）、`Localization\` zh/en/ja 三个 CSV（各 ~190 行）、`Sounds\` 3 个 WAV | 卡表 CSV 是**生成源**，运行时不读（见 §2.3）；音效 `pack_cut` / `card_draw` / `card_draw_foil` |

**代码 : 数据 : 美术 ≈ 4,746 行 : 1,042 行（822 KB 字面量）: 0 行（173.7 MB 外部资源）**。真正撑起 172 MB 的是 `Textures`，不是任何一份数据。

美术是**一张卡三份**（`Textures\Paper` 后缀直方图，实测）：`_s` 967（开包飞出的小卡图，`PiaoArt.cs:75`）／`_p` 967（详情页立绘，`PiaoArt.cs:85`；图鉴已收集格 `PiaoDex.cs:188`）／`_g` 967（**图鉴未收集的剪影**，同处 else 分支）；另有 `_p_glow`183、`_p_h1`205、`_p_h2`205 三个后缀**代码无引用**（见 §5），以及 `piao_pack_sleeve_00..10`/`piao_pack_seal`/`piao_shimmer`/`seg_cut`/`seg_empty` 各 1（卡包皮、撕口、闪光贴片、撕线）。

## 2. 核心：967 张卡怎么塞进 4,758 行

### 2.1 数据形态：编译期数组字面量，不是 JSON/CSV

`SvaCardsData.cs:5-21` 定义扁平结构体 `Row`，20 个 `public string/int/bool` 字段；`:47` 是全参构造函数；`:72-73` 是 `public static readonly Row[] All = new Row[967] { ... }`，**每个元素一行**：

```csharp
new Row("piao_sv_10001110", "不屈的剑斗士", "bronze", "中立", "sv_piao_sv_10001110", 10,
        token: false, "00", "基础系列", "【爆能强化_4】本随从+3/+3。", 2, 2, 2,
        "在这充满纷争的世界中……", "松冈祯丞", "tricky胡坐",
        "Indomitable Fighter", "不屈のファイター",
        "Enhance (4): Give this follower +3/+3.", "【エンハンス_4】これは+3/+3する。"),
```

字段按用途分四组：**身份**（`Id`/`Sprite`/`Pack`/`Token`）、**玩法**（`Rarity`/`Class`/`Value`/`Cost`/`Atk`/`Life`）、**文案三语**（`Name`/`NameEn`/`NameJa`、`Desc`/`DescEn`/`DescJa`、`Story`）、**版权**（`Cv`/`Illustrator`/`PackName`）。`Value` 直接就是 `unitValue`（`Cards.cs:15`，可被 `cfg_buyer_price_<rarity>` 覆盖）。

### 2.2 加载：懒初始化 + 一次转结构 + 全量注册

`SvaCards.cs:65-101` 的 `Ensure()` 只在首次访问时把 `Row[]` 逐条映射成 `CardDef` 塞进 `List` 与 `Dictionary<string,CardDef>`；上游（`Data`/`Lookup`/`BuildLootEntries`/`PackIds`/`Register`）全部先调 `Ensure()`。`Register`（`:166-174`）把 967 个 id 一次性 `Directory<GameItem>.Add(id, () => Cards.MakeSvaCard(def))`——**卡是惰性工厂，不是预先实例化的 GameItem**，所以 967 个物品注册不占内存。

### 2.3 证据：运行时确实一个字都不读盘

全仓 `grep` 数据文件读取 API，只命中两处，且都是"每日礼物"标记：`OusiMerchant.cs:662/674`（`ReadModDataFileText`/`WriteModDataFileText`，内容 `槽位|天数`）。**没有** `ReadModFileText`、`JsonUtility`、`StreamReader`。包里的 `Config\cards.csv`（968 行、441 KB、字段与 `Row` 一一对应且多一列 `sv_card_id`）只是**作者生成 `SvaCardsData.cs` 的中间产物**，忘在包里没删——不是运行时数据源（推测其生成链路：SVA 站导出 → cards.csv → 脚本拼字面量）。

### 2.4 抽卡权重：先按稀有度分池，再池内均分

`SvaCards.cs:125-138`：按 `Rarity` 分组，每组总权重取 `PiaoConfig.DropWeight(rarity)`（`:59-68`，默认 gold 80 / silver 250 / bronze 650 / 其余 20 = 千分制），**再除以该组卡数**平均分给每张卡；结果交给原版 `ModHelper.RegisterLootTable`（`ModEntry.cs:30-34`，一弹一张表 `piao_table_<pack>`），开包时 `ModHelper.RollLootTable` 逐个抽。于是"稀有度总量固定、卡池越大单卡越低"，改数值只需改 CSV 键 `cfg_drop_*`。

### 2.5 一包 8 张：抽完补一次保底

`ModEntry.cs:133-150`：先连抽 8 次；若 8 张全是铜卡，就**再摇最多 500 次**直到出现非铜，用它顶替第 8 张。保底只动最后一格，不动前 7 格——这是"低配保底"的最省写法。

## 3. 它怎么做这件事（原版类 / 方法 + Patch 靶点）

### 3.1 物品与容器

| 要做的事 | 原版接口 | 位置 |
| --- | --- | --- |
| 造一张卡（无模板物品） | `ItemDirectory.CreateEmptyItem(null).SetName().SetSpriteAndShapeFromMod(modId, spriteKey)` | `Cards.cs:14` |
| 卡片可被柜台激活 → 弹详情 | `GameItemFunc.onActivateSlotItemFunc` | `Cards.cs:23-28` |
| 卡盒（能装卡） | `GameGridInventory(w,h)` + `GameInventoryFunc.mayInventoryAddItemFunc` + `MachineHelper.CreateMachineryWindow` / `Panel` + `SetContentWindow` | `Cards.cs:67-82`（12×6）/`:84-99`（12×12）/`:129-143`（18×18） |
| 图鉴书（纸质交互） | `ModHelper.OnPaperOpened(modId, bookId, Action<PaperUI>)` + `ui.OnAction("open_dex", …)` | `ModEntry.cs:35-41` |
| 图鉴书物品 | `ModHelper.SpawnPaperItem(modId, "piao_dex_book")`，物品 id 由 `Books\piao_dex_book.json` 的 `item.id = piao_dex_book_item` 决定 | `ModEntry.cs:439`；`Books\piao_dex_book.json` |

### 3.2 开包演出：完全自绘，不碰原版动画

`ModEntry.cs:107-127` 只做一件事：开一个 `CustomUIManager.Window(...,"paper")`，把 `ContentRoot` 交给 `PiaoPackStage.Create(...)`。之后全部是 `MonoBehaviour` + uGUI：

1. **撕口是手写手势判定**：`PiaoPackStage.cs:164` 只在左 18% 或右 82% 起手才算开始；`CutComplete`（`:220-231`）要求从一侧划到对侧；`OnDrag`（`:169-202`）边拖边改 "Cut trace" 的 `sizeDelta.x`/`anchoredPosition`。
2. **"飞出"不是 `TreeNodeRender`、不是补间库**，就是 `Update()` 里的解析式（`:366-393`）：第 i 张延迟 `0.16 + i*0.23`s，飞行 `0.6`s，`t=clamp01(...)`，`e=1-(1-t)²` 走 X/Y 位移，`sin(πt)*70` 给一条抛物线弧，scale 从 0.38 长到 1，rotation 按 `(i%4-1.5)*6°*sin(πt)` 散开。落地那帧才 `raycastTarget=true`（可点击看详情）并挂闪光。
3. **金色/传说闪光** = `PiaoShimmer`（`PiaoShimmer.cs:56-79`）：给卡的 `Image` 加一个满铺子 `Image`，`LateUpdate` 里 `sin(unscaledTime*6)` 在青↔金之间 lerp 颜色 + 调制 alpha（`:93-97`），相位用 `GetInstanceID()&0x1F * 0.07f` 打散。
4. **12 颗"Foil glint"**（`PiaoPackStage.cs:288-292` 建、`:396-403` 驱动）：只有最后一格是金/虹时，才绕最后一格圆心 `cos/sin` 散开 84px 并淡出。
5. **音效对点**：撕开时 `PlaySound(modId,"pack_cut")`（`:255`）；逐张卡用 `nextSound` 游标保证"卡飞到位才响"（`:373-377`），金/虹换 `card_draw_foil`（`PlayDrawSound`，`:295-300`）。

整个舞台 409 行，含 `IPointerDownHandler/IDragHandler/IPointerUpHandler/IInitializePotentialDragHandler` 四个 uGUI 接口，**零 Harmony、零原版动画 API**。

### 3.3 图鉴（Cardex）：18 格一页 + 逐卡布尔键

- 网格 **6 列 × 3 行 = 18/页**（`PiaoDex.cs:10-14`），页码由 `Math.Max(1,(count+17)/18)` 算（`:100`），`Skip(page*18).Take(18)` 只实例化当前页（`:174`）——**不是虚拟滚动，是翻页**，所以 967 条也不会卡。
- 每格：已收集 → `_p` 立绘 + 卡名（按语言截断 `PiaoLoc.NameMax`：中文 8 字 / 英文 18 字）；未收集 → `_g` 剪影 + "？？？"，点击只弹稀有度提示（`:186-199`、`Notice` `:291-312`）。
- 进度存 `ModHelper.GetModDataBool/SetModDataBool(modId, "dex_" + cardId)`（`:22-43`）——**967 个独立布尔键**。
- 顶部是**两排弹号 Tab + 一排筛选按钮**：Tab 带百分比（`TabLabel` `:264-276`）；筛选 = 搜索框（三语任一命中，`NameMatches` `:251-262`）+ 持有/未持有循环按钮 + 稀有度/职业循环按钮；筛选条件全部作为参数递归回 `Show(...)`，**用"重建窗口"代替状态管理**。

### 3.4 收藏家上门：原版 `StoreClient`，没有新类

没有自定义顾客类型。`PiaoBuyer.Enable()`（`:46-50`）只是往原版静态表塞一个工厂：

```csharp
StoreClientListDict.storeClientDict["piao_tcgcardshop.card_buyer"] = CreateBuyer;
ModHook.OnGenerateCustomerVeryLate += QueueBuyer;
```

`QueueBuyer`（`:67-107`）负责"该不该来"：见过欧丝、今天没排过（`buyer_day` 键）、`Random(0,100) < cfg_buyer_chance(默认50)`、图鉴非空、不在队里，然后 `manager.AddNextClient(CreateBuyer())`。

"想看某系列"有两级表达，都用原版字段：

| 形态 | 怎么表达 | 位置 |
| --- | --- | --- |
| **点名一张卡** | `c.clientBuyingIdList.Add(def.Id)` + `c.mainDialogue = new Dialogue().SetText(名字, "buyer_ask_named")`；预算不够就抬到 `CardPriceByRarity(rarity, 100+cfg_buyer_premium_named)` | `PiaoBuyer.cs:200-209` |
| **同职业+同稀有度（可加同弹）的组合** | `c.willAcceptItem = item => def.Class==cls && def.Rarity==rar && (pack==null \|\| def.Pack==pack)`，再加 `c.SetVariety()` 表示"要凑齐不同的几张" | `PiaoBuyer.cs:238-259` |

顾客档次用 `clientFaction` 四档（`FACTION_LOWER_LEVEL / MIDDLE / UPPER_LEVEL / BLACK_MARKET`，`:16`）配 `StoreClient` 的**原版立绘池** `SpriteDict.GetRandomLowerLevelSpriteName / GetRevSpriteName / GetUpperSpriteName / GetShadySpriteName`（`:186-195`）——"新顾客"其实只是"原版顾客换个立绘 + 换需求 + 换预算"。额外溢价用原版 `ItemFeature`（`AddPremium`，`:276-298`，`FeatureType` 5、`valueStage` 1、`SetPreExposedValueMod(pct,true)`）挂在被选中的卡上，**不改 `unitValue`**，卡一离开柜台就移除（`:168-177`）。`PiaoTrader`（339 行）是卡商："我店展示柜里的卡"= `EmporiumEntry.Instance.GetAllOwnedItems(false,false)` 里 `IsTag("piao_card")` 且父级链上命中 `emp.showcaseElement` 的（`PiaoTrader.cs:95-150`，向上最多 8 层父物品）。

### 3.5 多语言：卡名/技能 = 同一行三列，界面 = loc CSV

| 层 | 组织方式 | 位置 |
| --- | --- | --- |
| **卡名 / 技能 / 风味** | `Row` 里**并排三列**（`Name`/`NameEn`/`NameJa`、`Desc`/`DescEn`/`DescJa`），运行时按 `LocHelper.GetCurrentLocaleCode()` 取：`zh*`→`Name`、`ja*`→`NameJa`、其余→`NameEn`；空值回落中文 | `PiaoLoc.cs:7-31`、`:71-103` |
| **界面文案** | `Localization\{zh,en,ja}.csv`，`键,值` 一行一条，走 `ModHelper.GetLocalized(modId,key,args)`；键名硬编码在代码里（`pack_stage_header`、`dex_progress`…） | `PiaoConfig.cs:21-43` |
| **随语言变的排版参数** | `NameMax`/`TabMax`/`ShortPackMax` 按语言给不同截断长度（中文 8 / 英文 18） | `PiaoLoc.cs:35-69` |

注意它**没有**把三语拆成三份卡表文件——一张卡的三语在内存里是同一个对象的三个字段，切换语言无需重载。

### 3.6 Patch 靶点表（全模组只有 2 个）

| 靶点 | 类型 | 干什么 | 位置 |
| --- | --- | --- | --- |
| `SpriteDict.GetSprite(string)` | Prefix | 只对 `"piao_tcgcardshop.ousi"` 生效：把 mod 的 `piao_ousi_merchant` 包成世界立绘 Sprite（**借用原版 `default` 精灵的 pivot/PPU 再 `Sprite.Create`**），其余 key 直接 `return true` | `OusiArtPatch.cs:26-51` |
| `StorePhoneClient.GetLocalizedDisplayName()` | Prefix | 只对 `dialogFuncId == "piao_tcgcardshop.ousi"` 生效，返回本地化名；其余放行 | `OusiContactNamePatch.cs:10-16` |

电话这条线一个补丁都没打：`PhoneClientList.DialogFuncs["piao_tcgcardshop.ousi"] = CallOusi` + 把 `StorePhoneClient`（`locID`/`dialogFuncId`/`phoneState=4`/`dialedBefore=true`）直接写进 `PlayerStore.Instance.PhoneClientDict[449517]`（`OusiMerchant.cs:59-61`、`:104-111`），号码被占就只记一条警告（`:97-100`）。

## 4. 关键代码摘录

**① 数据容器与注册（`SvaCards.cs:65-101` 节选，行为核心）**

```csharp
private static void Ensure() {
    if (_data == null) {
        _data = new List<CardDef>();
        _index = new Dictionary<string, CardDef>();
        foreach (SvaCardsData.Row row in SvaCardsData.All) {
            CardDef cardDef = new CardDef { Id = row.Id, Name = row.Name, /* …18 个字段直拷… */ };
            _data.Add(cardDef);
            _index[cardDef.Id] = cardDef;
        }
    }
}
// :119-123  一弹一张表
public static (string, float)[] BuildPackEntries(string packId) {
    Ensure();
    return BuildWeighted(_data.Where(c => !c.Token && c.Pack == packId));
}
```

**② 权重（`SvaCards.cs:125-138`）**

```csharp
private static (string, float)[] BuildWeighted(IEnumerable<CardDef> pool) {
    List<(string, float)> list = new List<(string, float)>();
    foreach (IGrouping<string, CardDef> g in from c in pool group c by c.Rarity) {
        float w = PiaoConfig.DropWeight(g.Key) / (float)g.Count();   // 稀有度总权重 ÷ 张数
        foreach (CardDef c in g) list.Add((c.Id, w));
    }
    return list.ToArray();
}
```

**③ 飞出补间（`PiaoPackStage.cs:366-403`，删 IL 噪声）**

```csharp
for (int i = 0; i < cards.Length; i++) {
    float t = (elapsed - 0.16f - i * 0.23f) / 0.6f;
    if (t < 0f) continue;
    if (i == nextSound) { PlayDrawSound(result[i]); nextSound++; }      // 卡飞到位才响
    float e = Mathf.Clamp01(t), s = 1f - (1f - e) * (1f - e);           // ease-out quad
    Vector2 g = GridPos(i, cards.Length);
    cards[i].rectTransform.anchoredPosition = new Vector2(g.x * s, Mathf.Sin(e * Mathf.PI) * 70f + g.y * s);
    cards[i].rectTransform.localScale   = new Vector3(0.38f + 0.62f * s, 0.55f + 0.45f * s, 1f);
    cards[i].rectTransform.localRotation = Quaternion.Euler(0, 0, (i % 4 - 1.5f) * 6f * Mathf.Sin(e * Mathf.PI));
    cards[i].color = Color.Lerp(new Color(1f, 0.87f, 0.55f), Color.white, e);
    if (e >= 1f && !cards[i].raycastTarget) {
        cards[i].raycastTarget = true;
        if (IsPremium(result[i])) PiaoShimmer.Set(cards[i], PiaoArt.Shimmer);   // 金/虹才有闪光
    }
}
bool flag = IsPremium(result[result.Length - 1]);                        // 最后一格是金/虹才炸星星
```

> 另有一处非动画的短代码值得一读——"需求"就是一个谓词（`PiaoBuyer.cs:238-249`）：`c.willAcceptItem = item => { var d = SvaCards.Lookup(item.identifier); return d != null && d.Class == cls && d.Rarity == rar && (pk == null || d.Pack == pk); }`。"想要某系列"就是 `pk` 非空这条分支，见 §3.4。

## 5. 踩坑与红线

1. **967 个 `dex_<id>` 布尔键**（`PiaoDex.cs:26/38`）。能跑，但等于往 `ModData` 里写 967 个键；`TabLabel` 每次刷新还要对全表跑 3 次 `Enumerable.Count`（`:272-273`），图鉴开一次 = 几千次字典读。**我们要做图鉴就存位图/位集合（`ulong[]` + `SetModData` 单键 base64）**，别照抄。
2. **`SvaCardsData.cs` 会生成巨型 `.cctor`**：967 条 `new Row(...)` 全在一个静态构造里，DLL 869 KB，首次访问 `Data` 时一次性分配 967 个对象（推测：首次开图鉴/开包有一次性卡顿，代码里没有任何预热）。要抄"数据进代码"这个思路的话，宁可抄成 `const string` 大块 + 运行时 `Split`，或 `RuntimeHelpers.InitializeArray`。
3. **`Textures\Items\` 里有 2,527 个 `sv_piao_sv_*` 文件，但全仓 `grep` 不到任何 `_h`/`_h1`/`_h2`/`_glow` 引用**（`Paper` 侧同理 `_p_glow`/`_p_h1`/`_p_h2` 共 593 个）。也就是说**每张卡多带了 2~5 张废图**，白占约 5 MB（Items 总 8.5 MB）。发布前该有一条"资源引用扫描"。
4. **保底是 500 次重摇**（`ModEntry.cs:140-149`）：池子里万一没有非铜卡，就是 500 次 `RollLootTable` 空转后才放弃。正确写法是"从非铜子池直接抽"。
5. **`Pack` 字符串 `"00".."10"` 是排序键也是美术键**：`piao_pack_<code>`、`piao_pack_sleeve_<code>`、`piao_table_<code>`、`cfg_pack_price_<code>` 四套命名全靠这个两位字符串对齐，加一弹必须四处同步。
6. **溢出兜底有两层**（`ModEntry.cs:170-191`）：卡放不进任何柜台 → 销毁 → 记进 `OusiMerchant.AddPendingCards` 的 mod 数据文件，下次找欧丝取。可抄，但"卡被销毁过"对玩家是不可见的。
7. 命名易误读：图鉴**书 id** 是 `piao_dex_book`，**物品 id** 是 `piao_dex_book_item`（来自 `Books\piao_dex_book.json` 的 `item.id`），`ModEntry.cs:469` 扫后者，对，不是笔误。

## 6. 对我们的可用点

> 对应项目：`深空电玩`（`deepspace_arcade`）、`深空当铺：可能是群友`（`freya_cornell`）

| 可用点 | 搬到哪 | 怎么搬（点名到接口） |
| --- | --- | --- |
| **稀有度分池权重 + 一弹一表** | 深空电玩（街机抽卡/扭蛋）、深空当铺（盲盒进货） | `SvaCards.BuildWeighted` 那 12 行 + `ModHelper.RegisterLootTable(modId, "table_<池>", (id,weight)[])`；保底改成"从非低保子池直接抽"，不要重摇 |
| **手写飞出补间 + 金色闪光** | 深空电玩（中奖掉落）、深空当铺（开箱） | 公式照抄 `PiaoPackStage.cs:366-393`（延迟 0.23×i、0.6s、`1-(1-t)²`、`sin(πt)·70` 弧），零依赖、可在 `CustomUIBuilder` 窗口内直接用；金/虹再加 `PiaoShimmer`（100 行）：父 `Image` 上盖满铺子 `Image`，`LateUpdate` 里 `sin(unscaledTime*6)` 做青↔金 lerp，相位用 `InstanceID & 0x1F` 错开，不需要 shader |
| **顾客"点名一张货 / 同品类+同档次组合"** | 深空当铺：可能是群友（顾客点单、关系网） | `StoreClientListDict.storeClientDict[id] = factory` + `ModHook.OnGenerateCustomerVeryLate` + `manager.AddNextClient`；需求用 `clientBuyingIdList`（点名）或 `willAcceptItem` 谓词（组合）+ `SetVariety()`（凑套）；**别新增顾客类** |
| **溢价用 `ItemFeature` 而不改 `unitValue`** | 深空当铺（收藏家出高价、真伪/成色之外的第二层价值） | `AddPremium`（`PiaoBuyer.cs:276-298`）：`new ItemFeature((FeatureType)5)` + `SetPreExposedValueMod(pct,true)` + `SetPublicDisplay/SetActualDisplay`，物品离柜即 `itemFeatures.Remove` |
| **"展示柜里的货才是货" + 图鉴剪影页** | 深空当铺（收购只看你摆出来的）、深空电玩（收集墙） | `PiaoTrader.DisplayStock`：`EmporiumEntry.Instance.GetAllOwnedItems(false,false)` → 逐件向上最多 8 层找 `emp.showcaseElement`，命中才算上架；图鉴未收集用单独一张灰度图 `_g`、收集后换 `_p`，18 格/页翻页而不是虚拟滚动（进度别学它存 967 个 bool） |
