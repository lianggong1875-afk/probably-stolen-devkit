# Probably Extend Framework（主框架本体）｜工坊 3841909877

> 蒸馏自反编译源码 `缓存文件夹\decomp_ws\3841909877`（88 个 .cs / 约 2.4 万行），
> 配合透视报告 `缓存文件夹\decomp_ws\_reports\3841909877.txt`。
> 姊妹档案：`3841912659-ExtendFrameworkFiles.md`（伴生模组，负责读任意文件名/文件夹的贴图）。
> 该档案结尾留的"待补：确认 `Painter.UseLocalLister`、`PackShelf.Offer` 的确切契约"——**本文第 5 节回答了它**。

---

## 0. 一句话结论

**它是一个"可被别的模组调用的立绘/贴图扩展层"，外加一整套围绕立绘的玩家向 QoL。**
两件事同时成立，但主次很清楚：**先有库，后来才有玩家向功能**。

- **是库**：`public static class Painter` 一个类就给了约 60 个 public 成员，全部**以 `modId` 为第一参数**做命名空间；另有 `PackShelf` / `Checkpoint` / `Flags` / `DayNight` / `DialogPlacement` / `Sources` / `Regist` 等 public 面。别的模组可以直接 `引用 DLL → 调 Painter.For("我的ID", …)` 注册自己的立绘规则。
- **也是 QoL 集合**：贴图扩展工坊（F9 全屏 UI）、立绘来源一览、高清立绘渲染、对话框位置、立绘自动大小、来客立绘池追加、昼夜差分、右键"选图"、设置窗口滑杆显示数字、lewd_meme 兼容、卸载前清理。

一句话给老板：**"别人家的立绘引擎"**——第三方立绘包只要在自己模组目录里放一个 `TextureExtend.json`（或 `.csv`），或者直接调 C# API，就会被它接管成游戏里能用的立绘/道具图，带缓存、带缩略图、带来源切换。

体量说明：`Entry.cs` 只有 546 行，真正的肉在 `WorkshopWindow.cs`(2403) / `Painter.cs`(1854) / `PortraitLibrary.cs`(1609) / `Workshop.cs`(1209) / `PaintRules.cs`(765) / `DialogPlacement.cs`(710) / `PackShelf.cs`(675)。
**本文覆盖：** 入口/生命周期、16 处 Patch 全部、`Ports` 注册表、`ModData` 全部读写点、伴生注入点全部、`Painter` 的 public 面、标签系统、`Baker` 分帧、`When` 表达式、`Plan` 仲裁。
**本文未覆盖（诚实标注）：** `WorkshopWindow.cs` 的 UI 布局细节、`PortraitLibrary.cs` 的索引数据结构、`Display.cs` 的取图链路内部、`Picker/SourceWindow` 的交互细节、`WorkshopUi` 那 8 个 UI 小件。这些只读了签名与调用点，不是逐行读。

---

## 1. 它对外的能力面

### 1.1 C# API（别的模组可调）

| 类 | 用途 | 代表签名（原样） |
| --- | --- | --- |
| `Painter` 核心 | 取图/换图/清空 | `public static PaintRule For(string modId, PaintTarget target, string id)`、`Paint(item,group,key,cells,variant)`、`Resolve(portraitId,state)`、`Refresh(item)`、`UnregisterAll(modId)` |
| `Painter` 注入点 | 伴生/外部能力 | `UseLocalLister(Func<IList<string[]>>)`、`UseImageReader(Func<string,string,byte[]>)`、`UseCacheStore(ICacheStore)`、`RegisterPack(modId,json)` |
| `Painter` 规则/数据 | 声明式规则与批量表 | `Condition`、`AddPortraitResolver`、`AddItemVariant`、`DynamicTexture`、`Join`、`AutoReplace`、`Expression(string)`、`LoadCsv(modId,"x.csv")`、`Series`、`DefineVanillaGroup`、`DefineTagGroup` |
| `Painter` 状态 | 存档内意愿/禁用 | `Want/WantToday/WantNow`、`Wanted`、`Ban(tag,on)`、`Decide`、`ActiveTag`、`Want` 系列带 `@sleep`/`@visit` 过期 |
| `PackShelf` | 立绘包货架 | `Offer(packModId, IEnumerable<PackEntry>)`、`Announce(packModId)`、`LooksLikePortrait(stem)`、`CanRead(modId)`、`List()` |
| `Checkpoint` | **给别人的时间点总线** | `event Action<string> OnFired`、`Register(name)`、`Exists(name)`、6 个常量 `load/wake/open/close/sleep/visit` |
| `Flags` / `DayNight` | 存档内小状态 | `Flags.Get/IsSet/Set(name,bool\|int\|string)`、`DayNight.IsNight` |
| `DialogPlacement` / `Sources` / `Plan` | 展示与来源仲裁 | `PlayerSetting`/`Place()`/`Use(mode)`；`Sources.Main/Order/Groups/PerPortrait`；`Plan.ForKey(key)` → `Mode ∈ {Vanilla,Original,Framework,Yield}` |
| `Regist` / `TagDef` | 标签注册（fluent） | `Painter.Tag("我的ID", PaintTarget.Portrait, "x").Under("职业").With("female").Group("g")`；`Painter.DebugButton(modId,page,name,Action)` 可把按钮挂进框架调试面板 |

> **设计要点：一律 `modId` 打头。** `Painter`/`PackShelf`/`Regist` 里 20 余处 public 注册方法第一参数都是 `modId`，配 `UnregisterAll(modId)` 一次清空（`Painter.cs:364-377` 连带清 `PortraitLibrary/PaintRules/Packs/PatchPacks/Pulses`）。这是"别人能安全接进来、也能安全退出"的前提。

### 1.2 文件格式契约（给**不做 C# 的**贴图包作者）

框架会**自动**在每一个已启用模组的目录里找这些文件（`CsvLoader.Auto` + `PackShelf.Probe`）：

| 文件名 | 格式 | 备注 |
| --- | --- | --- |
| `TextureExtend.json` | 官方推荐 | `PackJson` 转成 CSV 再吃：`root/pack/name/priority/scale/style/dialog/extends` + `tagGroups[]`/`tags[]`/`portraits[]`/`items[]`；`portraits[].pool` 让立绘直接进随机池，`portraits[].frames{}` 是差分 |
| `TextureExtend.csv` | 表头驱动，25 列 | `kind,target,id,tag,sprite,suffixes,prefix,tags,manual,word,name,priority,when,weight,on,until,group,under,with,without,dialog,scale,style,frame,extends` |
| `portraits.json` | 兼容 Portrait Studio | 读 `["dictionary"]` |
| `retexture.tsv` / `portraits.xml` + `PortraitPackShim.dll` | 兼容第三方与旧 F10 包 | 前者按 `\t` 切；后者有 shim DLL 就认 |

`when` 列是一门**手写递归下降解析的条件 DSL**（`When.cs`），支持 `|` `&` `!` `()` 与比较运算：

```
true / false / night / day>=5 / tag:xxx / tag:xxx>=20 / flag:名字
visitor / visitor:阵营 / rep:阵营>=10 / cond:名字
```

即：**贴图包作者用一行文本写"什么时候换这张图"，不用写代码。** 这是它比"纯 C# API 框架"更能扩散的真正原因。

### 1.3 挂载点

- `IMod`：`Entry.Init/OnEnable/OnDisable`。**没有 HotReload 钩子**（与透视报告一致）——静态状态全靠 `OnDisable` 里那 40 行手工反注册（`Entry.cs:127-175`，是一份很值得抄的"关服清单"）。
- 10 个 `ModHook`（见第 6 节）、16 处 Harmony（见第 2 节）、2 个 `DontDestroyOnLoad` GameObject（`Baker` 后台烘焙协程 + `HdRender` 的 `Watcher`）。
- 存档内状态：**不写自定义存档字段，全部走 `ModHelper.SetModData` 键值 + `GameItem` 标签**（见第 4 节）。

### 1.4 玩家向功能清单

| 功能 | 实现文件 | 一句话 |
| --- | --- | --- |
| 贴图扩展工坊（F9 全屏 UI）+ 立绘来源一览 | `WorkshopWindow.cs`(2403) `Workshop.cs`(1209) `SourceWindow.cs` `Sources.cs` | 缩略图网格浏览所有模组提供的贴图/立绘，改来源、标签、缩放，导出 `TextureExtend.csv`；`Sources.json` + `PortraitPreset/` 存来源设定 |
| 高清立绘渲染 + 立绘自动大小 | `HdRender.cs`(187) `Fit.cs` `FitMath.cs` | 柜台上是高清算立绘时按屏幕分辨率渲染（原版是 960×540 画布）；自动量各包人物占比调到接近原版高（-25%~+10%，底边贴齐） |
| 对话框位置 + 来客立绘池 + 昼夜差分 | `DialogPlacement.cs`(710) `PortraitPools.cs` `DayNight.cs`(55) | auto/vanilla/high/low/mid 五档避脸；抽取池只追加新立绘不改原有抽取；打烊换 `night` 差分 |
| 右键"选图" + 设置窗滑杆显示数字 | `PickButton*.cs` `ConfigNumbers.cs`(66) | 店里右键道具直接换图；只对自己窗口生效，把百分比写在标签上 |
| lewd_meme 兼容 | `LewdMemeCompat.cs`(182) | 少女道具按好感度过夜换情绪立绘（≤-60 angry / ≤-20 unhappy / ≥20 happy / ≥60 delighted） |
| 卸载前清理 + 调试面板 | `Painter.Retire()`（`Painter.cs:1687-1766`）`DevPanel.cs` `Probe.cs` | 把存档里被换过图的物品**还原成不依赖本框架**的样子；面板给状态报告/接触面自检/重建缓存/显示探针 |

---

## 2. 那 17 个手动 Patch 逐条表 ⭐ 本文最重要产出

**先把"17"这个数字说清：** `_reports\3841909877.txt` 数出 17 条，是因为第 17 条是 `Ports.cs:85` 里**真正执行**的 `harmony.Patch(methodBase, port.prefix, port.postfix, null, null)`。
所以准确说法是：**16 处声明式注册 + 1 处执行调用**。下表按 16 条注册列出。

| # | 注册名（`Ports.Patch` 第 1 参） | 目标方法（重载表） | 形态 | 作者说明（原文照抄） | 实际做什么（核对代码后） | 会短路原方法？ |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | `PlayerStore.GetNextClient` | `()` | Prefix `BeforeArrive` | 来客登场时按 visit 标签定图 | 从 `storeClientManager.clientStack[0].GetSprite()` 取来客 key，`Wants.Expire("@visit")`，存 `current`；该 key 有事件差分则 `Wants.SetCounterEvent("visit")`，并把 `shownState` 预置好 | 否（void） |
| 2 | `PlayerStore.SellItem` | `(GameItem)` | Prefix `BeforeSold` + Postfix `AfterTrade` | 卖出时按 sold 标签换来客差分 | **交易前**读物品的议价特征（`bargainDiscount`→bonus / `bargainMarkup`→malus / `blackmailMarkup` / `threatenMarkup`；`bribeDonation` 刻意不标记）存 `pending`；**交易后**按标记切来客差分 | 否 |
| 3 | `PlayerStore.OnItemBought` | `(GameItem,int)` | Prefix `BeforeBought` + Postfix `AfterBought` | 买入时按 bought 标签换来客差分 | 同上，表为 `bargainBuyingMarkup`→bonus / `bargainBuyingDiscount`→malus / `blackmailDiscount` / `threatenDiscount`；Postfix 里顺便 `Painter.Manage/Refresh` 被买进来的物品 | 否 |
| 4 | `PlayerStore.DismissCurrentClient` | `(bool,bool,bool)` | Prefix `BeforeLeave` | 来客离开时按 leave 标签换差分 | 离开前 `Event("leave")` 切差分。**注意：这里显式写了三参类型表**，正是为了避开重载歧义（我们的模组同一方法上是特性式补丁，见第 8 节） | 否 |
| 5 | `ModConfigWindow.Open` | `(LoadedMod)` | Postfix `Postfix` | 设置窗口的滑杆旁显示数字 | **只对自己**（`Manifest.ID != "Lune.ProbablyExtendFramework"` 直接 return）：给 `portraitScale` 滑杆挂 `onValueChanged`，把值写进 `label_portraitScale`，并且**保留标签里 `<link=…>` 的富文本尾巴**（切成 head/tail 再拼）；连 `reset` 按钮也挂一次刷新 | 否 |
| 6 | `DialogUIManager.DisplayClientText` | `()` | Postfix `AfterDisplay` | 来客对话框位置 | 每播一句就按当前模式重摆气泡 | 否 |
| 7 | `DialogUIManager.SetNormalMode` | `()` | Postfix `AfterNormal` | 来客对话框位置 | 回到普通模式后重摆 | 否 |
| 8 | `DialogUIManager.EndConversation` | `()` | Postfix `AfterEnd` | 来客对话框位置 | 清 `forced/forcedFor`（对话结束，恢复自动） | 否 |
| 9 | `GameItem.SetSpriteAndShapeFromMod` | `(string,string)` | Postfix `AfterModSprite` | lewd_meme 换回少女原图后当场接回高清图 | 仅当兼容已开 + `modId=="lewd_meme"` + 是少女道具 + 带 `PEF_CUR` 标签时才 `Painter.Restore(item)` 把高清图抢回来 | 否 |
| 10 | `ItemContextHandler.OnEventPress` | `()` | Postfix `Press` | 右键菜单的"选图"按钮 | 按下 `triggerKey` → `PickButton.ShowFor(__instance)` | 否 |
| 11 | `ItemContextHandler.OnEventRelease` | `()` | Postfix `Release` | 右键菜单的"选图"按钮 | 松开 `key` → `PickButton.Click` | 否 |
| 12 | `ItemContextHandler.OnEventUpdate` | `()` | Postfix `Update` | 右键菜单的"选图"按钮 | 可见时做 hover | 否 |
| 13 | `ItemContextHandler.OnEventReset` | `()` | Postfix `Reset` | 右键菜单的"选图"按钮 | 隐藏按钮 | 否 |
| 14 | `ToggleAppearanceHelper.MakeSpriteCyclable` | `()` | Postfix `NativeLooks` | 原版外观循环道具的手动选图 | 记录**原版道具自带的** `atlasPath` + `spriteVariants`（`PaintRules.AddNativeLook`）；没有这步，"原版外观循环道具"的备选图们在框架里是看不见的 | 否 |
| 15 | `SpriteDict.GetSprite` | `()` | Prefix `Prefix` **+** Postfix `Postfix`，`priority = 0` | 来客等处按选中的来源显示立绘 | **Prefix**：`texext_*` 虚拟 key 现场解析；新增池立绘不在 `spriteDictionary` 里时直接 `__result=…; return false`。**Postfix**：按 `Plan.ForKey(key)` 仲裁——`Original` 强制原图、`Framework` 用框架图（但当图是别人 `res\|` 资源时让位）、`Yield` 不动 | **是**（全套里唯一会 `return false` 的） |
| 16 | `SpriteDict.GetPool` | `(string,int)` | Postfix `Postfix`，`priority = 0` | 新立绘加入来客立绘池 | 把新立绘**追加**到原版池（`Extend(prefix,cap,vanilla)`；`cap != int.MaxValue` 或非 Ready 时一律不动），并缓存合并结果 | 否 |

另有 **4 个 `Ports.Read` 探针**（不 Patch，只做"游戏更新后还认不认得这些字段"）：
`SpriteDict original entries`、`DialogUIManager.clientBubble`、`EmporiumEntry.pixelPerfectCamera`、`StoreClientMono.image`。

### 逐条读出来的三个判断

1. **15 个里 14 个是"只观察、不干预"**：Prefix 一律 `void`，Postfix 一律挂尾巴。作者把"会短路原方法"的补丁压到了**唯一一处**（#15），而且那一处还分了两级（`texext_*` 和 `IsExtra`）。
2. **#1-#4 是一组"来客表情状态机"**，四个补丁共享一个 `pending` 静态字段串起"交易前算标记 → 交易后切图"；#2/#3 的标记表把**原版议价特征**翻译成 `bonus/malus`，说明作者读过议价系统。
3. **#5 是纯粹为了自己 UI 好读**（滑杆显示数字），却仍然走全局 `ModConfigWindow.Open` 补丁——这说明**原版 `CustomUIWindow` 没有"滑杆显示数值"的口子**，只能 Patch。这是本框架里最"不划算"的一个补丁：冲突面（所有模组的设置窗口都会经过）远大于收益。抄的时候可以不要。

---

## 3. Patch 注册表的实现（`Ports.cs`，164 行，全文都值得抄）

### 3.1 结构

```csharp
// Ports.cs:11-32
private sealed class Port
{
    internal string name;        // "PlayerStore.SellItem"
    internal string feature;     // "卖出时按 sold 标签换来客差分"  ← 中文人话
    internal string status;      // "not installed" / "ok" / 异常摘要
    internal Func<MethodBase> target;   // ← 延迟解析！不是 MethodBase
    internal HarmonyMethod prefix;
    internal HarmonyMethod postfix;
    internal Func<string> probe;        // ← Read 型，返回 null 表示健康
    internal bool ok;
}
private static readonly List<Port> ports = new List<Port>();
private static Harmony harmony;
```

```csharp
// Ports.cs:34-48 —— 注册只是"登记"，按 name 去重
internal static void Patch(string name, string feature, Func<MethodBase> target,
                           HarmonyMethod prefix = null, HarmonyMethod postfix = null)
{
    if (!ports.Exists((Port p) => p.name == name))
    {
        ports.Add(new Port { name = name, feature = feature, target = target,
                             prefix = prefix, postfix = postfix, status = "not installed" });
    }
}
```

```csharp
// Ports.cs:64-96 —— 一次性安装，单点失败不拖整批
internal static void InstallAll()
{
    if (harmony == null) harmony = new Harmony("Lune.ProbablyExtendFramework");
    foreach (Port port in ports)
    {
        if (port.target == null || port.ok) continue;
        try
        {
            MethodBase methodBase = port.target();
            if (methodBase == null) throw new MissingMethodException("target not found");
            harmony.Patch(methodBase, port.prefix, port.postfix, null, null);
            port.ok = true; port.status = "ok";
        }
        catch (Exception ex)
        {
            port.ok = false;
            port.status = ex.GetType().Name + ": " + ex.Message;
            Entry.Warn("port " + port.name + " not installed, feature off: "
                       + port.feature + " (" + port.status + ")");
        }
    }
}
```

### 3.2 这套封装解决了四个具体问题

| 问题 | 它怎么解决 |
| --- | --- |
| 游戏更新改了方法签名 → 特性补丁整批炸 | `target` 是 `Func<MethodBase>`，**推迟到 `InstallAll` 才解析**；`AccessTools.Method` 找不到返回 `null` → 抛 `MissingMethodException` 被 catch → **只废掉这一条，其余照装** |
| 玩家/我做兼容排查时不知道哪条生效 | `port.ok` + `status` + `Ports.Report()` 输出 `√/×` 清单（`Ports.cs:127-163`），挂进调试面板"接触面自检（游戏更新后先看这个）" |
| 重载方法歧义 | 每一处都能**按需**写 `new Type[]{…}`（`SellItem` 写了，`GetSprite` 没写=无重载），不是一律靠猜 |
| 关了再开 / 卸载要干净 | `UninstallAll()` 走 `harmony.UnpatchAll("Lune.ProbablyExtendFramework")`，然后把 `ok/status` 复位（`Ports.cs:98-120`） |

### 3.3 为什么用手动 Patch 而不是 `[HarmonyPatch]` 特性

从代码本身能读出来的理由（不是猜）：

1. **方法名字符串就是"说明书的左侧"**。`Ports.Patch("PlayerStore.SellItem", "卖出时按 sold 标签换来客差分", …)` 让"补了什么/为什么补"长在同一行；特性写法要把这句话塞进 `/// <summary>`，跟代码分开，一改就忘。
2. **需要"说明 + 健康度"进 UI**。特性是编译期静态元数据，`Ports.Report()` 那种 `√/× + 中文说明` 的运行时清单，用特性得再写一套反射去扫自己的特性——作者直接选了列表。
3. **需要"能失败"**。特性解析失败发生在 `PatchAll` 里，一个坏补丁会让整批失败；这里刻意逐条 try/catch。
4. **同一条说明要覆盖多条补丁**。`PickButtonHooks` 的 5 条共用一句"右键菜单的'选图'按钮"，`DialogPlacement` 的 3 条共用"来客对话框位置"——`feature` 字段天然是分组的，特性做不到。
5. **顺序可控**。`Entry.cs:106` 是 `Ports.InstallAll()`，在 `Entry.OnEnable` 里所有 `X.Install()` **之后**统一装；`priority = 0` 这种设置可以按条写。

> 代价（要诚实说）：丢掉 `[HarmonyPriority]`/`[HarmonyBefore]` 的编译期检查；`MethodBase` 只在运行时才知道，IDE 不能"转到定义"。**作者接受了这个代价，换的是可诊断性。** 对我们这种"工坊里被十几个模组挤在一起"的场景，这个交易我认为是划算的。

---

## 4. ModData / 文件读写

**它用了两套存储，语义完全不同，别混。**

### 4.1 存档内：`ModHelper.SetModData/GetModData`（跟着存档走）

| 键 | 值 | 用在哪 |
| --- | --- | --- |
| `flag.<名字>` | 字符串（`"1"`/`"0"`/任意） | `Flags.cs:18,53,57` — 给**贴图包的 `when` 表达式**用的持久变量（`flag:xxx`） |
| `want.portrait.<key>` / `want.item.<id>` | 空格分隔的标签 + 可选 `@sleep`/`@visit` 过期标记 | `Wants.cs:106-110` — "今天想让这张图出现" |
| `ban` | 空格分隔的禁用标签 | `Wants.cs:135-139` |
| `used` / `usedLooks` | 换行分隔的已用 key | `Painter.cs:1419,1433,1776`、`ItemArt.cs:460,471` — 剪枝时保护"用过的"图 |

生命周期由 `Entry` 挂到钩子上：`OnLoaded` → `Wants.Reload()/TimeFromStore()`；`OnSleepLate` → `Wants.Expire("@sleep")`；每次 `OnOpen/OnClose/OnWake` → `Wants.SetTime("wakeup"/"openshop"/"endshop")`。

### 4.2 机器上：`ModData\Lune.ProbablyExtendFramework\`（跟着机器走）

全部经 `ICacheStore`（默认实现 `ModDataStore`，8 个方法一对一映射 `ModHelper.ReadModDataFileText/Bytes`、`WriteModDataFileText/Bytes`、`DeleteModDataFile`、`ListModDataFiles`）：

```
ModData\Lune.ProbablyExtendFramework\
    Sources.json / Fit.json / ActiveLibrary.json / PortraitLibrary.json  来源设定 / 占比采样 / 立绘库快照 / 调试导出(只写不读)
    PortraitPreset\<名>.json      整套 Sources 的预设副本
    ItemCache\index.json + *.png  { algo:1, entries:{ "portrait\nkey": {sig,point,kept} } }；png 是烘焙好的道具图（正方形、≥8px 才收）
    Workshop\{Catalog,Workshop}.json + TextureExtend.csv   工坊快照 / 当前状态 / 导出
    Local\                        玩家自助丢图目录（**由伴生模组扫描**，见第 5 节）
```

`index.json` 里那个 `algo: 1` 是**算法版本号**——算法一改，旧索引整体作废重烘（`Cache.cs:65`）。`kept:true` 表示"玩家手动留的，别剪"。

### 4.3 `ReadModFileText` vs `ReadModDataFileText`（关键区别）

| | `ModHelper.ReadModFileText(modId, "相对路径")` | `ModHelper.ReadModDataFileText(自己的modId, "相对路径")` |
| --- | --- | --- |
| 读的是 | **任意已启用模组的目录**（沙盒内，按 modId） | **本模组自己的 ModData 目录** |
| 本框架用它做 | 采集**别人的**贴图包：`TextureExtend.json/.csv`、`portraits.json`、`retexture.tsv`、`portraits.xml`（`CsvLoader.cs:89`、`PackShelf.cs:449,525,585,652`） | 读自己的 `Sources.json`/`Fit.json`/`Workshop/*` |
| 能不能写 | **不能**（只有 `ModFileExists` 做存在性判断） | 能写（`WriteModDataFileText`） |
| 权限意义 | 这是**官方沙盒 API**：只能读"已经装了、已经启用的模组自己的目录"，不是任意文件系统 | 自己家，随便写 |

> **这是整个"零文件权限"架构的地基**：主框架靠 `ReadModFile*` 就能吃遍所有模组的贴图包，**不需要 `files` 能力**。真正需要"读玩家任意文件夹"的部分（`Local\` 下的散图）交给伴生模组——见下节。

---

## 5. 与伴生模组（3841912659）的接线 ⭐ 第二重点

### 5.1 五个注入点（主框架留口，伴生模组塞实现）

| 注入点（`Painter` 的 public 方法） | 伴生模组塞进来的东西 | 主框架内部的落点 | 主框架为什么自己不干 |
| --- | --- | --- | --- |
| `UseLocalLister(Func<IList<string[]>>)` | 一个返回 `[相对路径, 时间戳]` 列表的委托 | `LocalImages.Lister`（`LocalImages.cs:24`） | 扫目录要 `Directory.EnumerateFiles` → 需要 `files` 能力 |
| `UseImageReader(Func<string,string,byte[]>)` | `(modId, path) → bytes` | `ExternalImages.Reader`（`ExternalImages.cs:17`） | 同上 |
| `UseCacheStore(ICacheStore)` | 另一套存储（可换盘、可换目录） | `Cache.UseStore`（`Cache.cs:38-50`） | 让"大缓存搬出 ModData"成为可能 |
| `RegisterPack(modId, json)` | 直接把 `TextureExtend.json` 文本丢进来 | `PackJson.Apply`（`Painter.cs:123`） | 让伴生模组不用落盘也能注册 |
| `PackShelf.Offer(modId, entries)` / `Announce(modId)` | 一批 `PackEntry{Id, Path}`｜"我这个包里有货" | `PackShelf.offers` + `Apply()`（`PackShelf.cs:143-183`） | 避免主框架自己去猜哪个模组是贴图包 |

### 5.2 核心机制：**字符串签名（sig）代替文件句柄**

主框架从不持有 `FileStream`，只持有一根字符串：

```csharp
// ExternalImages.cs:19-37 —— 四种来源，统一成一根 sig
internal static string FileSig(string modId, string relativePath, string stamp = null)
    => "file|" + modId + "|" + VersionOf(modId) + (stamp.IsNullOrEmpty ? "" : "+" + stamp) + "|" + relativePath;
internal static string ResourceSig(string modId, string resource) => "res|" + modId + "|" + VersionOf(modId) + "|" + resource;
internal static string ExtSig(string modId, string resource)      => "ext|" + modId + "|" + VersionOf(modId) + "|" + resource;
internal static string DataSig(string relativePath, string stamp) => "data|Lune.ProbablyExtendFramework|" + stamp + "|" + relativePath;
```

```csharp
// ExternalImages.cs:44-71 —— 解 sig 时才真正取字节，且每条路径各有权限闸门
internal static byte[] Bytes(string sig, bool listed = false)
{
    string[] p = (sig ?? "").Split(new char[1] { '|' }, 4);
    if (p.Length < 4) return null;
    if (p[0] == "file")
    {
        if (!listed && !PackShelf.CanRead(p[1])) return null;   // ← 只认"已启用/已登记"的模组
        array = ModHelper.ReadModFileBytes(p[1], p[3]);
    }
    else if (p[0] == "data") array = ModHelper.ReadModDataFileBytes("Lune.ProbablyExtendFramework", p[3]);
    else if (p[0] == "ext")
    {
        if (Reader == null || (!listed && !PackShelf.CanRead(p[1]))) return null;  // ← 没有伴生模组就没有 ext
        array = Reader(p[1], p[3]);
    }
    ...
}
```

**这根 sig 顺手当缓存键用**：里面带了模组版本号 + 文件时间戳，所以"模组一更新 / 图片文件一改，sig 变，缓存自然失效"。**不需要扫描、不需要比对文件内容**——这是全框架最省事的一处设计。（`Cache.Has(id, signature)` 就是比 sig，`Cache.cs:107-124`。）

### 5.3 伴生模组怎么把 `Local\` 变成立绘（我们已蒸馏的那一半，这里补上主框架侧）

1. 伴生模组 `Painter.UseLocalLister(() => …)` 塞进扫描器。
2. 主框架 `LocalImages.Scan()`（`LocalImages.cs:30-143`）：
   - `Painter.UnregisterAll("local")` → 整批清掉上一轮的 local 注册（**幂等**）；
   - `Lister()` 拿 `[rel, stamp]`，按 `/` 切目录、按 `.` 切 stem；
   - **四条识别路径**（顺序很重要）：① 名字命中游戏原版 key（`Originals.Get`）→ 给那张原版立绘加来源；② 命中 `PortraitTags.TryStructure` 的结构 → 拆 base/差分；③ 命中**道具 ID** → `ItemArt.AddFile`（道具图）；④ 都不中 → `Group()` 找同前缀兄弟，推断 base/差分，找不到 base 就打警告"看着像系列但缺底图"；
   - 目录层级自动变标签（`FolderTags`）：目录名是池名就登记进池，否则小写后当标签。
3. 注册完 `Count > 0` 才打一行 `local images: N registered from ModData/Local/` —— **没扫描器就完全静默**（`Lister == null` 时 `Scan()` 返回 `-1`）。

**这套"留口 + 注入"的要点：主框架对"文件从哪来"零知识，只认 `Func<…>` 和 `sig`。** 所以同一个主框架能同时吃：官方模组目录（`file|`）、DLL 内嵌资源（`res|`）、伴生模组的任意路径（`ext|`）、自己的 ModData（`data|`）——四种来源一套代码。**这条是我们最该抄的架构。**

### 5.4 除了注入，还有一条"约定式发现"（不走 C#）

`PackShelf.Probe(modId)` + `CsvLoader.Auto(mod)` 会在 `OnModAssetsLoaded` 时把每个启用模组的 `TextureExtend.json/.csv/portraits.json/retexture.tsv/portraits.xml` 自动吃掉。
**所以第三方贴图包作者不需要引用主框架 DLL，也不需要伴生模组**——放文件就行。这是"零门槛扩散"和"零能力"两条目标同时达成的关键。

---

## 6. 生命周期与性能

### 6.1 10 个 ModHook 各自做了什么（全部订阅点已核）

| 钩子 | 谁订阅 | 干什么 |
| --- | --- | --- |
| `OnModAssetsLoaded` | `Entry.OnAssetsLoaded`（`Entry.cs:108`） | **整个框架的真入口**。别人的模组：`CsvLoader.Auto` + `PortraitLibrary.Invalidate` + `Painter.NeedScan=true`。自己的模组：`assetsReady/localeReady=true`、`BundledTables.ApplyEnabled`、`Workshop.Apply`、`Cache.LoadIndex`、`RegisterDebug`、`Painter.FireReady` |
| `OnGameLoadedInit` | `DayNight.ToDay`（`DayNight.cs:18`） | 开局强制白天态 |
| `OnGameLoadedNormal` | `Entry.OnSessionStart` + `Painter.Preload` | 清 UI（`Picker.Close`/`PickButton.Hide`/`Notices.Reset`）、`Painter.Unsettle`、`Retired=false`；然后预热 |
| `OnGameLoadedLate` | `Painter.Settle`、`Painter.RestoreAll`、`Entry.OnLoaded`、`Baker.RequestLoadBurst`、`Checkpoint.FireLoad` | 读档后：`Wants.Reload`、`LocalImages.Scan`、`ItemArt.Discover`、`Painter.RefreshAll`，并放行一次**加载 burst**（不分帧，抢速度） |
| `OnGoingSleepEarly` | `Entry.OnSleep`、`Baker.RequestBurst`、`Checkpoint.FireSleep` | 睡前落盘：`Fit.Save`、`Painter.PruneUsed`、`Cache.SaveIndex`；再放一次 burst |
| `OnGoingSleepLate` | `Entry.OnSleepLate`、`DayNight.ToDay` | `Wants.Expire("@sleep")`、刷新全表 |
| `OnHandlingNightlyServicesLate` | `LewdMemeCompat.Overnight`（仅兼容开启时） | 按好感度给少女道具换情绪差分 |
| `OnWakingUpLate` | `Painter.Settle`、`Entry.OnWake`、`DayNight.ToDay`、`Checkpoint.FireWake` | 起床刷新**受管物品**（不是全表） |
| `OnShutterOpenedLate` | `Painter.Settle`、`Entry.OnOpen`、`Checkpoint.FireOpen` | 开店刷新受管物品 |
| `OnShutterClosedLate` | `Entry.OnClose`、`DayNight.ToNight`、`Checkpoint.FireClose` | 打烊刷新 + 转夜景 |

> 注意 `Painter.Settle` 被**三个**钩子共用（`OnGameLoadedLate`/`OnWakingUpLate`/`OnShutterOpenedLate`），内部是幂等的：`if (!Settled) { Settled = true; NeedScan = true; }`（`Painter.cs:674-681`）。而 `Unsettle()` 只在读档开始时调——**"读档 → 表可能还没建好 → 先 unsettle，等 late 再 settle"** 的时序控制。

### 6.2 分帧/节流（`Baker.cs`，371 行）

- **只有一个常驻 MonoBehaviour**：`Baker.Launch()` 造 `DontDestroyOnLoad` 的 `TextureExtend_Baker`（`Baker.cs:42-52`），`Start()` 起一个 `Loop()` 协程，主循环 `yield return idle(1f)` —— **每秒醒一次**，不是每帧。
- 每轮固定做 5 件事，每件都包了 stopwatch：`WorkshopEntry.Tick` / `WatchClient` / `SourceReader.Release` / `Fit.Settle` / `SaveIndexIfDirty`。`Step(what, action)` 统一 try/catch + `Health.Took`（`Baker.cs:186-198`）。
- 真干活时才进 `Scan()` 协程（`Baker.cs:215-364`），**按时间片让出**：
  - 普通模式：每帧预算 `0.003f` 秒；
  - 超过 `0.015s` 就 `yield return rest(0.1s)` 歇一口气；
  - 加载/睡前 burst：**不让出**（`yield return null`），抢完成，并在 0.4s 后弹 `PrepareScreen` 进度条（屏幕预算放宽到 `0.05f`）。
- 脏索引最多 15 秒落一次盘（`SaveIndexIfDirty`，`Baker.cs:177-184`）。
- **变更检测靠"戳"不靠轮询内容**：`Originals.TableStamp()`（对整张表算 31 进制哈希）+ `SpriteDict.Instance` 引用变化 → 变了才 `Invalidate + NeedScan`（`Baker.cs:141-158`）。

### 6.3 静态状态在哪些时机重置

| 时机 | 重置了什么 |
| --- | --- |
| `OnSessionStart`（`OnGameLoadedNormal`） | `Picker/PickButton/Notices`、`Painter.Unsettle`、`Retired=false` |
| `OnDisable`（40 行清单） | 全部静态：`Counter.Reset`、`Notices.Reset`、`ClickShield.ReleaseAll`、`Workshop.Shutdown`、`PackShelf.Reset`、`BundledTables.Reset`、`CsvLoader.Reset`、`Display.Shutdown`、`DevPanel.Clear`、`Health.Clear`、`Baker.Shutdown`、`assetsReady=false`、`Painter.Stop`，并 `Ports.UninstallAll()` |
| `Baker.Scan` 结束时 + `Cache.Prune` | `SourceReader.Release()`（**每轮都把解出来的 Texture2D 释放掉**，防像素常驻）；剪掉不再被任何来源引用的缓存条目并删对应 png |

`Health` 也是性能设施：同类错误只记一次（累加 count）、每个位置最多记 8 类（超出折叠成 `(more kinds of error at this place; latest: …)`）、主线程超过 12ms 的步骤单独统计 `count/worst/total`（`Health.cs:32-36,79-99`）。

---

## 7. 可复用范式（3 个 + 1 条补充，代码取自原文，仅做换行/去 decompiler 注释，逻辑未改）

### 7.1 范式 A：自描述 Patch 注册表

```csharp
// Ports.cs:34-48   登记：一行 = 目标 + 人话说明 + 延迟解析
internal static void Patch(string name, string feature, Func<MethodBase> target,
                           HarmonyMethod prefix = null, HarmonyMethod postfix = null)
{
    if (!ports.Exists((Port p) => p.name == name))
    {
        ports.Add(new Port { name = name, feature = feature, target = target,
                             prefix = prefix, postfix = postfix, status = "not installed" });
    }
}

// 调用点长这样（Counter.cs:50-51；原文单行，此处仅换行）
Ports.Patch("PlayerStore.GetNextClient", "来客登场时按 visit 标签定图",
    () => AccessTools.Method(typeof(PlayerStore), "GetNextClient", (Type[])null, (Type[])null),
    new HarmonyMethod(typeof(Counter), "BeforeArrive", (Type[])null));
Ports.Patch("PlayerStore.SellItem", "卖出时按 sold 标签换来客差分",
    () => AccessTools.Method(typeof(PlayerStore), "SellItem", new Type[1] { typeof(GameItem) }, (Type[])null),
    new HarmonyMethod(typeof(Counter), "BeforeSold", (Type[])null), new HarmonyMethod(typeof(Counter), "AfterTrade", (Type[])null));
```

**抄法**：把 `Port` 里的 `feature` 换成"这个补丁为什么必须存在"的一句话（我们的红线：必须能回答"非拦不可吗"），`Report()` 挂进调试面板。对我们这种要长期跟游戏版本跑的模组，**这 100 行能省掉一半的"更新后哪个补丁失效了"排查**。

### 7.2 范式 B：框架/伴生拆分 + `sig` 字符串注入

```csharp
// ExternalImages.cs:17-22   注入点：一个委托，默认 null
internal static Func<string, string, byte[]> Reader;

internal static string FileSig(string modId, string relativePath, string stamp = null)
    => "file|" + modId + "|" + VersionOf(modId)
     + (string.IsNullOrEmpty(stamp) ? "" : ("+" + stamp.Replace('|', '_'))) + "|" + relativePath;

// ExternalImages.cs:52-71   解引用时才取字节，每类来源各有自己的闸门
if (p[0] == "file") { if (!listed && !PackShelf.CanRead(p[1])) return null;
                      array = ModHelper.ReadModFileBytes(p[1], p[3]); }
else if (p[0] == "data") array = ModHelper.ReadModDataFileBytes("Lune.ProbablyExtendFramework", p[3]);
else if (p[0] == "ext")  { if (Reader == null || (!listed && !PackShelf.CanRead(p[1]))) return null;
                           array = Reader(p[1], p[3]); }   // ← 伴生模组才装得起的那一半
```

**抄法**：任何"需要读外部素材"的功能，主体只存 `sig`（含版本+时间戳，天然当缓存键），取字节留一个委托。要 `files` 的那一半单独成模组，主体零能力。**顺带一个白拿的好处**：sig 里带版本/时间戳 → 素材一改缓存自动失效，不用写"比对文件"。

### 7.3 范式 C：两套存储各管一半（存档内键值 + 机器上文件）

```csharp
// ModDataStore.cs:5-38   机器上：8 个方法一对一映射官方 ModData 文件 API
internal sealed class ModDataStore : ICacheStore
{
    public string Where => "ModData/Lune.ProbablyExtendFramework";
    public string ReadText(string path)  => ModHelper.ReadModDataFileText("Lune.ProbablyExtendFramework", path);
    public byte[] ReadBytes(string path) => ModHelper.ReadModDataFileBytes("Lune.ProbablyExtendFramework", path);
    public void WriteText(string path, string text) => ModHelper.WriteModDataFileText("Lune.ProbablyExtendFramework", path, text);
    public void WriteBytes(string path, byte[] bytes) => ModHelper.WriteModDataFileBytes("Lune.ProbablyExtendFramework", path, bytes);
    public bool Delete(string path) => ModHelper.DeleteModDataFile("Lune.ProbablyExtendFramework", path);   // + List() 同理
}

// Flags.cs:47-61   存档内：跟档走的小键值，写之前先比一次（避免无谓存档脏）
public static void Set(string name, string value)
{
    if (!string.IsNullOrEmpty(name) && PlayerStore.HasInstance && !(Get(name) == value))
    {
        if (value == null) ModHelper.RemoveModData("Lune.ProbablyExtendFramework", "flag." + name);
        else               ModHelper.SetModData("Lune.ProbablyExtendFramework", "flag." + name, value);
        Painter.Notify();
    }
}
```

**判定规则**：`跟这一档剧情/进度有关` → `SetModData`（`flag.*` / `want.*`）；`跟机器/缓存/大文件有关` → ModData 文件（`*.json` / `*.png`）。**并且写之前先比一次**，值没变就不写——`Flags.Set` 那行 `!(Get(name) == value)` 就是这个意思，避免"每帧改一个 flag 把存档写脏"。

### 7.4 一点补充：后台任务不要写在 `Update` 里

`Baker`（`Baker.cs:131-364`）是它唯一的常驻 MonoBehaviour：`DontDestroyOnLoad` + 一个**每秒醒一次**的协程（`yield return idle(1f)`），每轮固定做 5 件小事，每件都包在 `Step(what, action)` 里（try/catch → `Health.Report`，加 `Health.Took` 计时，>12ms 的步骤单独统计）。真干活的 `Scan()` 按 `0.003s/帧` 的时间片让出，超 `0.015s` 就歇 `0.1s`；加载/睡前 burst 不让出、并在 0.4s 后弹进度条。脏索引最多 15s 落一次盘。
**抄法**：1 秒 tick + 每步计时 + 超预算让出，玩家报"卡"时能直接说出卡在哪一步、最坏多少毫秒。

---

## 8. 风险

### 8.1 16 处补丁的冲突面（按危险度排序）

| 危险度 | 目标方法 | 谁还会碰它 | 说明 |
| --- | --- | --- | --- |
| 🔴 高 | `SpriteDict.GetSprite` | **我们自己的 `FreyaPortrait.cs:255`**（Prefix，`Priority.Normal` + `[HarmonyAfter("mod.deepspace_arcade")]`）；Portrait Studio 也认它当"立绘来源"探测点 | 双方都是**会短路**的 Prefix。框架 `PlanPort.Prefix` 在 `texext_*` key 或 `PortraitPools.IsExtra(key)` 时 `return false` → **Harmony 会跳过后面所有 Prefix**；反之我们的 Prefix 若先 `return false`，框架的也跑不到。而框架的 **Postfix** 会在 `Plan.Mode == Original` 时把 `__result` 覆写成**原版图**，等于把别人 Prefix 刚给的自愈图又抹掉（`PlanPort.cs:103-113`） |
| 🟠 中 | `PlayerStore.DismissCurrentClient(bool,bool,bool)` | **我们两家都在打**：`ArcExpressions.cs:529`（`Priority.High` + `HarmonyBefore("mod.freya_cornell")`）、`FreyaExpressions.cs:591`（`Priority.Normal` + `HarmonyAfter("mod.deepspace_arcade")`） | 框架这条是 `void` Prefix，不短路，功能上不冲突；但它**没有写任何 `HarmonyBefore/After`**，插在我们的优先级链中间。三方都"只观察"时无碍；若哪天框架改成短路，我们的顺序假设就崩了 |
| 🟠 中 | `ModConfigWindow.Open(LoadedMod)` | 任何想改设置窗口的模组 | 每个模组的设置窗都会经过；本框架只对自己生效，但**补丁本身是全局的** |
| 🟡 低 | `PlayerStore.SellItem/OnItemBought/GetNextClient`、`DialogUIManager.*`、`ItemContextHandler.*`、`ToggleAppearanceHelper.*` | 目前只有它自己 | 全是 `void` Prefix/Postfix，不改返回值 |
| 🟢 极低 | `GameItem.SetSpriteAndShapeFromMod(string,string)` | **我们的 `ArcSpriteFix.cs:23`**（Prefix，只对自己 modId 生效） | 我们是 Prefix 她 是 Postfix，且她先判 `modId == "lewd_meme"`，互不影响 |

**两个必须记住的技术点：**

1. 框架的 Harmony 实例是 `new Harmony("Lune.ProbablyExtendFramework")`（`Ports.cs:70`）——**不是** `ModLoader` 惯用的 `"mod." + manifest.ID`。所以想跟它排序，`[HarmonyAfter("mod.Lune.ProbablyExtendFramework")]` **匹配不到**，得写裸 id。
   （依据：`Ports.cs:70` 的字符串原文 + 我们现有代码里用的是 `"mod.freya_cornell"` / `"mod.deepspace_arcade"` 这种前缀式；ModLoader 侧约定见 `03-原版接口速查.md`。）
2. 它 `UninstallAll()` 走 `UnpatchAll("Lune.ProbablyExtendFramework")`，只撤自己的，不会误伤我们。

**结论**：真正要盯的只有 `SpriteDict.GetSprite` 一处。如果我们两家同时装，建议**给我们的 Prefix 加 `[HarmonyPriority(Priority.High)]` 并显式 `[HarmonyBefore("Lune.ProbablyExtendFramework")]`**（裸 id），让我们先自愈；框架的 Postfix 仍然可能覆写，那就得实测 `Plan.Mode` 落到哪个分支（`Yield`/`Vanilla` 不动 `__result`，只有 `Original` 会强制原图）。

### 8.2 reflection 用在哪、能不能换 `ModReflect`

全仓只有 **2 类 6 处**真反射：

| 位置 | 用法 | 能不能换 |
| --- | --- | --- |
| 16 处 `AccessTools.Method(typeof(X), "名", Type[]…)` | 产出一个 `MethodBase` 交给 `harmony.Patch` | **不能换 `ModReflect`**——`ModReflect.GetStatic<T>` 是读私有**字段值**的，不产生 `MethodBase`。这里是 Harmony 的前置条件，只能走 `AccessTools`/`Type.GetMethod` |
| `Originals.cs:214` `AccessTools.GetDeclaredFields(typeof(SpriteDict))` + 逐个 `FieldType != typeof(List<SpriteEntry>)` 过滤 | 枚举 `SpriteDict` 上**所有** `List<SpriteEntry>` 字段，把原版立绘 key→Sprite 建表 | **不能换**——它不知道字段名，要的是"所有同类型字段"。`ModReflect` 是按名取单个值。这是全仓最"硬"的一处反射，**游戏一改 `SpriteDict` 的字段类型就会失效**（好在整段包在 try/catch 里，失败只打 `original portraits not read` 警告） |

好消息：**没有一处 `Invoke` 私有方法，没有 `Assembly.Load`，没有动态编译**。反射全部集中在"Harmony 目标解析"和"读原版表"两件事上，且**每一处都能失败降级**。
⚠️ 诚实标注：这 7 个 reflection 文件是否让它在 `ModScanner` 的 `reflection` 能力上被判非零，**我在这份反编译产物里看不到 manifest，无法确认**（推测：`AccessTools` 是对游戏程序集内类型的反射，扫描器很可能记为 `reflection`；姊妹档案写的"主框架零能力"需要真机 `ModScanner` 复核）。

### 8.3 其他风险

- **`PortraitLibrary` 的失效时机**：任何 `OnModAssetsLoaded` 都会 `Invalidate()` + `NeedScan=true`，而 `Scan` 只在 `VanillaReady && Settled` 时才跑。**读档途中若表还没建好，扫描会被无限推迟**（它靠 `Settle/Unsettle` 兜，但这是个隐式时序契约）。
- **没有 HotReload 钩子**：它的静态状态（`Painter.groups`/`PaintRules`/`sprites`/`PortraitLibrary`）在热重载时只能靠 `OnDisable` 那 40 行清。**热重载后如果 `OnDisable` 没跑干净，会出现"图还是旧的但缓存说 sig 一致"**（推测：`sprites` 字典里的 `Sprite` 是 Unity 对象，重载后可能悬空）。
- **`Workshop/*.csv` 回写自己的数据目录**：工坊能把当前状态导出成 `Workshop/TextureExtend.csv`，然后又被 `CsvLoader.Text` 读回来（`Workshop.cs:1174-1177`，owner 写死成 `"Lune.ProbablyExtendFramework.Workshop"`）。这是一条**自己吃自己**的回路，改动时要小心重复注册。
- **`ConfigNumbers` 那条补丁收益/风险比最差**（见 2 节判断 3），抄之前先想清楚。
- **对 `Local\` 目录的时序**：`LocalImages.Scan` 里第一条就是 `Painter.UnregisterAll("local")` ——**每次重扫都会整批撤掉再建**，意味着正在显示的 local 图在重扫瞬间会短暂失去来源（如果 `Retired`/`Running` 判断没接住就会闪一下原图）。

---

## 对我们的可用点

### 给 `deepspace_arcade`（深空电玩）

1. **范式 A 立刻能用，成本 100 行**。我们现在 4 个 Patch（`ArcSpriteFix`/`ArcExpressions` 两处 + 其它）散在特性里，游戏一更新就得翻日志找哪个失效。照抄 `Ports`，把 `feature` 写成"为什么非拦不可"的一句话，调试面板加一条"接触面自检"。**这直接对上工作区规范里"要改返回值必须有一句话理由"那条。**
2. **`Checkpoint` 的设计可以照搬给"卡带/机台状态"**：6 个具名时间点 + 重入保护队列 + 监听器逐个 try/catch（`Checkpoint.cs:49-119`）。我们现在是各处直接挂 `ModHook`，一多就散；集中成"时间点总线"后，卡带的各种刷新逻辑能统一在一个地方注册。
3. **`sig` 缓存键**（版本+时间戳）——街机卡带如果将来要允许玩家放自定义封面/卡面图，这个键能直接复用，**不用写"检测文件变了没"**。

### 给 `freya_cornell` / `DeepSpacePawn`（深空当铺：可能是群友）

1. **立绘自助接入可以直接走它的入口，不必等 Portrait Studio**：我们的 `freya_cornell_*` / `shuangluoluo_*` / `siren_*` 立绘现在是自己往 `SpriteDict` 塞（`FreyaPortrait.cs`）。如果玩家同时装了本框架，可以让我们的立绘**同时**通过 `Painter.AddPortraitResolver` / `RegisterPack` 登记一份，让它在"贴图扩展工坊"里能看见、能被玩家改来源。**代价是必须处理第 8.1 节 `SpriteDict.GetSprite` 的短路顺序。**
2. **`Painter.Want/WantNow` + `flag:*`** 正好能表达"某位群友今天要出示某张差分"，而且**存在存档里、跟档走**，不占我们自己的字段。我们现在的表情切换是硬编码在 `DspExpressions` 里，用它的标签+`when` 表达式可以把"什么条件下换哪张"变成数据。
3. **"卸载前清理"（`Painter.Retire()`）是我们该抄的一条产品线**：它在卸载前把存档里被自己换过图的物品**还原成不依赖框架的样子**（`Painter.cs:1687-1766`：先看 `PEF_BASE`/`PEF_CUR`/`PEF_OG` 三种标签，逐物品还原，再统一关掉 `PEF_LOCK/PEF_AUTO/PEF_PULSE/PEF_LM_MOOD`）。**我们任何一个"改了存档里物品外观/字段"的功能都应该配一个同款按钮**，否则玩家一停用就掉图。这条对两个项目都成立。
4. **发布架构**：如果将来"可能是群友"要做"玩家自己丢立绘文件夹"，**照它的做法拆**——主体零能力 + companion 吃 `files`，用 `Func` 注入 + `sig` 取字节。姊妹档案（3841912659）已经把 companion 侧写完了，本文第 5 节把主框架侧补齐了，**两边合起来是一份完整的可抄模板**。

### 不建议抄的

- `ConfigNumbers` 那条全局 `ModConfigWindow.Open` 补丁（收益/风险比太差）。
- `DialogPlacement` 那种"用中文字符串做 switch 的 `Normal()`"（`DialogPlacement.cs:71-160`，把 `"上方"/"下方"/"原版"/"中"` 硬编码进解析）——我们有 `GLOSSARY.md`，应该走术语表 + 常量，不要散在代码里。
- 它的 `Workshop.cs`/`WorkshopWindow.cs`（3600 行 UI）——体量太大，我们的 UI 需求没那么重；真要抄只抄"缩略图网格 + 来源切换"两块最小的。

---

> 行号复查索引：Patch 注册表 `Ports.cs:11-163`（执行调用 `:85`）｜注入点 `Painter.cs:109,123,220,226`、`PackShelf.cs:143,176,185`｜sig `ExternalImages.cs:19-105`｜缓存 `Cache.cs:15-263`｜机器上存储 `ModDataStore.cs:5-38`｜存档内键值 `Flags.cs:8-61`、`Wants.cs:103-143`｜伴生接线主框架侧 `LocalImages.cs:24-143`｜分帧 `Baker.cs:131-364`｜自诊断 `Health.cs:42-143`｜反射 `Originals.cs:203-233`｜卸载清理 `Painter.cs:1687-1766`｜条件 DSL `When.cs:51-196`｜来源仲裁 `Plan.cs:18-66`、`PlanPort.cs:32-133`
