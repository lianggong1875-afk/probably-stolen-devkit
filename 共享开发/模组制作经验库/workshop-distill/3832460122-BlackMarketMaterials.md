# 3832460122 · 地下（黑市）交易料单 / Black Market Materials —— 反编译蒸馏档案

> **id** `chiqingsan.UndergroundExchangeAssistant`（工坊 3832460122，v1.1.20，`GameVersions 0.46D`，manifest 自闭合 `<Capabilities />`）
> **反编译目录** `缓存文件夹\decomp_ws\3832460122`（13 个 `.cs` / 2,556 行，命名空间 `UndergroundExchangeAssistant`；透视报告：ModHook 0 / ModHelper 0 / `ModReflect: Get` / 触点 `CustomUIManager, PixelWindow`）
> **为什么值得读**：**零 Harmony、零 `ModHook`、零能力**，只做三件事——往原版垃圾桶正面挂一个 uGUI 子节点、从原版交易管理器**读**清单、把缺的材料**搬**进垃圾桶。奖励结算、次数限制、声望、夜间清洁全留给原版。它是"只做搬运、不碰结算"的标本，也是"往原版容器上加交互入口"的最小配方。

---

## 1. 规模与结构

| 文件 | 行 | 角色 |
| --- | --- | --- |
| `ExchangeMod.cs` | 31 | `IMod` 入口：建一个 `DontDestroyOnLoad` 的 runner，`AddComponent<ExchangeAssistantUI>()`（`ExchangeMod.cs:13-20`） |
| `ExchangeAssistantUI.cs` | 724 | 全部运行时：每帧找垃圾桶 → 建纸片入口 → 建悬停描边 → 开窗 → 刷新行状态 → 二次确认 |
| `ExchangeWindow.cs` | 361 | 自绘纸质窗口：`CustomUIManager` 建窗 + `TabletUI` 图元 + `Instantiate` 复用原版交易行美术 |
| `ExchangeTransfer.cs` | 310 | 单笔交易的一次性对象（`IDisposable`）：前置条件链 → 材料清单 → `Commit()` 搬运 → 失败回滚 |
| `MaterialPlan.cs` | 199 | 纯函数：算出"要投哪些、投几件、放得下吗" |
| `GameMaterials.cs` | 599 | 只读适配层：原版 `ExchangeBarter` → `Offer`；垃圾桶 → `TrashEntry` 树；玩家 9 个库存根 → 候选材料 |
| `ExchangeSimulation.cs` | 65 | 原版夜间结算的**本地复刻**（排序 + 条件匹配 + 消耗） |
| `PaperArtwork.cs` / `PaperHover.cs` / `PaperPointerBlocker.cs` | 176 / 41 / 22 | 纸片贴图外轮廓生成、悬停开关、吞掉拖拽事件 |
| `NativeOfferRows.cs` / `PlanStatus.cs` | 6 / 10 | 7 个常驻交易 id 常量；5 态枚举 |

**没有 HeatReload 钩子、没有 Patch、没有 `ModHelper`**。唯一的反射是 `ModReflect.Get(inventory, "items")` 作为 `childItems` 异常时的兜底（`GameMaterials.cs:543-565`）。

---

## 2. 它怎么做这件事

### 2.1 往原版容器正面贴一张可点击的纸 —— 不是 Patch，是挂子节点

不 Patch `GetActions`、不接 `OnCreateTooltip`、不给容器加 `Component`。做法是：**每帧从 `EmporiumEntry.Instance.trashcan` 拿原版物品，把自建 uGUI 节点挂到它的 `item.background.rectTransform` 下**（`ExchangeAssistantUI.cs:105-113, 162`）。节点一次性带 6 个组件（`ExchangeAssistantUI.cs:153-161`）：

```csharp
GameObject val = new GameObject("UndergroundExchangePaper", new Type[6]
{
    typeof(RectTransform), typeof(Image), typeof(Button),
    typeof(TreeNodeRender), typeof(PaperPointerBlocker), typeof(PaperHover)
});
val.transform.SetParent((Transform)(object)item.background.rectTransform, false);
component.alphaHitTestMinimumThreshold = 0.1f;   // 纸片透明边缘不吃点击
((Selectable)entrance).transition = (Transition)0;   // 不要原版按钮的变色
TabletUI.NoKeyboardFocus(new Selectable[1] { (Selectable)entrance });  // 别抢手柄焦点
```

- **就绪判定很硬**：一次 `if` 里同时要求 `EmporiumEntry / invElement / ExchangeUIManager / NegociationUIManager.tabletPrefab / RenderHandler / UISettings / CustomUIManager / OverlayHandler / GetTooltipWindow()` 全部非空，否则 `ResetSession()` 直接返回（`ExchangeAssistantUI.cs:64-68`）。这是"等待主界面加载完"的替代品，比猜钩子稳。
- **悬停画描边**：另建一个 `Image`，sprite 是同一张图跑出来的**外轮廓**，`raycastTarget = false`，和纸片同锚点、`sizeDelta` 相同，默认 `SetActive(false)`，靠 `PaperHover` 的 `IPointerEnter/Exit` 开关（`:243-264` + `PaperHover.cs:19-40`）。轮廓不是美术资源，是运行时算的（见 2.2）。
- **不许拖走**：`PaperPointerBlocker` 实现 `IBeginDrag/IDrag/IEndDrag` 并对每个事件调 `data.Use()`（`PaperPointerBlocker.cs:8-21`）——纸片住在原版物品上，不拦住拖拽就会把垃圾桶拖起来。
- **遮挡顺序**：每次 `LayoutEntrance` 都 `SetAsLastSibling()`（纸片与描边各一次，`ExchangeAssistantUI.cs:319-322, 335-340`），保证压在原版贴图上；锚点固定 `(0.44, 0.42)`、宽度取原贴图 `rect.width * 0.4f`（`:305-317`）。

### 2.2 纸片与描边都是运行时生成

内嵌资源 `UndergroundExchangeAssistant.Paper.png` → `Texture2D` → `PaperArtwork.Build(pixels, w, h)`（`ExchangeAssistantUI.cs:189-211`）。`Build` 先按 alpha≥128 求包围盒，等比缩到宽 64px、四周留 3px（`PaperArtwork.cs:83-100`），再 `Outline()` 用**从边缘开始的 flood fill** 求出"外部连通区"，凡是外部区内、四邻接有实心像素的就涂成 `(225,209,156)`（`PaperArtwork.cs:103-175`）。所以描边天然贴合纸片毛边，美术只需一张带透明通道的预览图。

### 2.3 7 项常驻 + 3 项周促销：**只读**，不新建数据源

三段全是读：

```csharp
// 常驻 7 行：原版 UI 里的行节点，按 y 从上到下排序；数量不符就抛
Transform[] array = (from t in ((Component)instance.panel.transform)
        .GetComponentsInChildren<RectTransform>(true).Single(t => ((Object)t).name == "PermaSection")
    where ((Object)t).name.StartsWith("ExchangeElement", StringComparison.Ordinal)
    select t).OrderBy(t => 0f - ((Component)t).GetComponent<RectTransform>().anchoredPosition.y).ToArray();
if (array.Length != NativeOfferRows.PermanentIds.Length)
    throw new InvalidOperationException("原版常驻交易行结构发生变化");
```

（`ExchangeAssistantUI.cs:399-409`；`NativeOfferRows.cs:5` 硬编码 7 个 id：`filter_barter / module_barter / dream_dust_barter / commandKeycardBarter / poison_barter / bullet_barter / smuggler_barter`）

- 促销 3 行：遍历 `instance.promotionalSection` 的子节点，取 `ExchangeElement.identifier` 且属于 `PlayerStore.Instance.exchangeManager.activePromotionalBarterIds` 的（`:414-428`）。
- "要什么材料"从 `ExchangeManager.activeBarter` / `promotionalBarter` 取 `ExchangeBarter.conditions`，把每个条件的 `IsItemValid` 委托原样存进 `Offer.Conditions`（`GameMaterials.cs:11-28, 66-82`）。**模组里没有任何一份自己的材料需求表**。
- 原版底数（`共享开发\reference\Assembly-CSharp\ExchangeManager.cs:28-46`）：`activeBarter` 7 条、`promotionalBarter` 7 条备选，`ResetPromotional()` 用 `Take(3)` 每周抽 3 条（`ExchangeManager.cs:95-113`）。这与"7 + 3"完全对得上。

### 2.4 自绘窗口：`CustomUIManager` + `TabletUI` 图元，不用 `PixelWindow`

```csharp
window = CustomUIManager.Instance.CreateWindow("underground_exchange_sheet", (string)null, "paper", val, (Style)0)
    .SetSize(720f, sheetHeight).Center().SetDraggable(true).SetCloseOnEscape(true).OnClose(...).GetWindow();
```
（`ExchangeWindow.cs:106-123`；第三参 `"paper"` 是**层级 id**，第四参是需要 `CustomUIWindowFrame` 的空模板 prefab，第五参 `Style 0 = Auto`——对照 `共享开发\reference\Assembly-CSharp\CustomUIManager.cs:397`）

- 皮肤不是自画：`skin = NegociationUIManager.Instance.tabletPrefab.GetComponent<NegotiationTabletFrame>()`（`ExchangeAssistantUI.cs:99`），于是 `TabletUI.NewFlat/NewStretched/NewText/SmallButton/AddTooltip` 和纸色 `PaperColor(190,159,158)` / 墨色 `InkColor(69,57,69)` 全按原版谈判平板走（`ExchangeWindow.cs:44-48, 192-230`）。窗口缩放拿 `rootCanvas` 的 `rect` 算 `min(1, (w-32)/720, (h-32)/sheetH)`、下限 `0.35f` 写 `localScale`，并把场景里所有 `StoreUI.zoomable` 关掉防滚轮打架（`:128-147`）。
- **行美术直接 `Instantiate` 原版行**：把 `BuildOffers` 拿到的原版 `Transform` 整个克隆进来，关掉自身 `Graphic`、子孙 `raycastTarget=false`、隐藏 `CountTMP*`、按可用宽度等比缩放，失败退回纯文字行（`ExchangeWindow.cs:255-303`），省掉 7 张行图标重绘。行高自适应：读每行原版 `rect.height`，`Clamp(460, 800)`（`:154-180`）。
- `PixelWindow` 只被**读**：`trashcan.contentWindow.child` 当 `GameGridInventory` 用来算格子（`GameMaterials.cs:84-119`）。

### 2.5 一键投料：需求 → 缺额 → 冲突 → 搬运

`MaterialPlan.Create()` 是纯函数（`MaterialPlan.cs:25-68`），核心是"**反复加一件材料再重跑一遍结算模拟**"，最多 64 轮：

```csharp
HashSet<string> hashSet = new HashSet<string>(settlement.Accepted.Keys) { target.Id };   // 已满足的 + 本次目标
List<GameItem> list = new List<GameItem>();
for (int i = 0; i <= 64; i++)
{
    List<GameMaterials.TrashEntry> list2 = WithAdditions(trash, list);
    ExchangeSimulation.Settlement settled = ExchangeSimulation.Run(offers, list2);
    if (hashSet.All(id => settled.Accepted.ContainsKey(id)))       // 目标+已满足的全部凑齐
    {
        List<GameItem> list3 = ConsumedOnly(consumed, list);       // 只保留"被本次真的吃掉"的那些
        if (list3.Count == list.Count) { ... return Ready(...); }
        list = list3;                                             // 多投了 → 减掉继续
    }
    else if (ChooseFor(offers, hashSet, settled, list2, trash, candidates, list) is GameItem val) list.Add(val);
    else return new MaterialPlan(PlanStatus.MissingMaterials);
}
return new MaterialPlan(PlanStatus.ConflictingOffer);
```

- **只补缺额**：`hashSet` = "已经被别的交易占掉的" ∪ "本次目标"，`ExchangeSimulation.Run` 复刻原版优先级排序（`y.priority.CompareTo(x.priority)`）后逐条匹配消耗；所以"需 3 个、已有 1 个"时循环只加 2 件，第 2 件后 `settled.Accepted` 已包含目标即停（`ExchangeSimulation.cs:20-44` + `MaterialPlan.cs:32-56`）。
- **冲突判定**（`PlanStatus` 5 态 → 按钮文案）映射在 `ExchangeTransfer.cs:93-100`：`AlreadyPrepared→"垃圾桶已备齐"`、`MissingMaterials→"库存材料不足"`、`NoSpace→"垃圾桶空间不足"`、`ConflictingOffer→"材料与其他交易冲突"`、`Ready→""`。前置条件还有一串更早的拒绝理由：垃圾桶未就绪 / 交易未解锁 / `StoreReputation.IsPerkUnlocked("BM_DISTRUSTED")` / 清洁服务没启用 / 手上正拖着物品 / 交易已刷新（`:50-88`）。
- **材料选择规则**（`GameMaterials.Candidates()` 的排序键，`:304, 327-341`）：
  1. `Rank`：`system_module_ruined` 记 0，其余记 1 → **损坏模组最先被用掉**；
  2. `water_filter` 按 `modifiedState["DURABILITY_TAG"]` 升序 → **剩余耐久低的先投**（耗尽的 0 耐久与普通件并列，排在同一 Rank 的最前）；
  3. `Container`（在直连库存里的先于嵌在容器里的）、`Direct`。
- **哪些东西绝不碰**（`SafeKeep`，`:356-367`）：非玩家所有、`!MayRemove()`、`MaxNumRemove() <= 0`、被钉住（`ItemPinHelper.IsPinned`）、上锁、`InMachine()`（父链上有 `MACHINE`/`STANDARD_MACHINE_TAG`）、`MayMove()` 为假（自身有子物品，`smuggler_bay` 例外）。另外 `IsInstalledFilter()` 把**已装进滤芯容器的滤芯**排除（`:438-453`），上锁补给箱整箱跳过（`:397-436`）。
- **空间**：`FindPlacements` 对每件材料做 2 种翻转 × 4 个旋转 × 全网格扫描，用 `GridShapeBuilder(item.modifiedShape).SetTransform(...)` + `((GameInventory)grid).TryInventorySlot(item, 1, shape, null)` 试放，`numTransfer == 1 && targetItem == null` 才算放下（`MaterialPlan.cs:153-198`）。
- **真正搬运**只有一处 `GraphUtils.TryAcceptAll((GraphNodeStorage)trashcan, val2, 1)`（`ExchangeTransfer.cs:158`），逐件投；投完立刻用**原版自己的判定**复核：`val.GetRequiredItems().Count != val.conditions.Count` 就抛 `投放后交易匹配发生变化` 并全量回滚（`:171-176`）。回滚 `ReturnMoves` 逐件 `Expel` → 先试原库存 → 再试 5 个玩家库存 → 都不行就 `LogError`（`:244-304`）。

### 2.6 边界：**不改任何交易数值，奖励仍由原版夜间清洁服务结算**

原版链路（`共享开发\reference\Assembly-CSharp\PlayerStore.cs:1958-1988`）：

```csharp
public void HandleJanitorial()
{
    ...
    if (exchangeManager.isExchangeUnlocked && !StoreReputation.IsPerkUnlocked("BM_DISTRUSTED"))
    {
        exchangeManager.Reset();      // 次数重置
        exchangeManager.HandleExchange();   // 排序 → CanAccept → Accept → onAccept 发奖 → Destroy 材料
    }
    ClearTrash();                     // 没被吃掉的垃圾桶内容全销毁（除了危险废物/暗格模组）
    exchangeManager.TransferBuffer();
}
```

`ExchangeBarter.Accept()` 才是发奖的地方（`ExchangeBarter.cs:75-91`：`onAccept(requiredItems)` 然后 `requiredItems[num].Destroy()`），`HandleExchange()` 顺带给黑市派系加声望（`ExchangeManager.cs:62-70`）。**模组只把物品放进垃圾桶**，既不写 `currentBarterCounter`、不碰 `onAccept`、不碰声望、不碰 `isExchangeUnlocked`——所以它和原版的"每晚一次""BM_DISTRUSTED 停摆""周促销轮换"天然一致，也不会有两套结算打架。

### 2.7 走私者暗格有货时的二次确认

`LostBayContents` 数"会被吃掉、但内容不属于本次匹配"的物品（`GameMaterials.cs:455-475`：只看被消耗条目里 id 为 `smuggler_bay` 的，遍历其子孙，凡不在消耗集合里的计数）。`LostBayContents > 0` 时**先不开火**，弹 `CustomUIManager` 小窗（620×270）+ 纸色底 + 两按钮"取消/确认继续"（`ExchangeAssistantUI.cs:557-561, 576-648`），文案：

> 走私者暗格内还有 N 件物品。／交换暗格会使这些物品丢失。／请先取出物品，或确认继续投放。

**为什么必须确认**：这一投不可逆且不由模组执行——`ExchangeBarter.Accept()` 对暗格本体调 `Destroy()`，暗格里的东西随之消失；`ClearTrash()` 还会把垃圾桶里没被匹配的物品一起销毁（`PlayerStore.cs:1990-2010`）。模组的回滚只能救"自己搬进去的那几件"，救不了"被原版结算吃掉的暗格内容"。凡"点一下会永久丢东西"的按钮，都必须把损失数量摆在按钮旁边让人再点一次。

---

## 3. 关键代码摘录

**① 入口三行 + 换会话整块重建（`ExchangeMod.cs:13-20`；`ExchangeAssistantUI.cs:69-74`，行内摘录）**——`OnEnable` 只做 `new GameObject` → `DontDestroyOnLoad` → `AddComponent<ExchangeAssistantUI>()`；`Update` 一开头 `if (store != PlayerStore.Instance || trashcan != instance.trashcan) { ResetSession(); store = …; trashcan = …; }`，`ResetSession` 里 `Destroy(entrance/hoverVisual)` 并把四个字段全置 `null`（`:685-702`）。没有钩子、没有 Patch，全靠轮询。

**② 变更检测靠"签名哈希 + 1 秒节流"（`ExchangeAssistantUI.cs:129-139, 432-473`）**：`Signature()` 把 `isExchangeUnlocked / 清洁服务 isUsing / BM_DISTRUSTED / activePromotionalBarterIds / 候选材料(identifier+unitCount+parentInventory 哈希) / 垃圾桶内容` 滚成一个 int，变了才 `Refresh()`；候选为 0 时 `scanRetryUntil` 给 0.5s 重试窗口（`:130-137`）。

**③ 只补缺额的模拟结算（`ExchangeSimulation.cs:20-44`）**：

```csharp
list2.Sort((GameMaterials.Offer x, GameMaterials.Offer y) => y.Priority.CompareTo(x.Priority));
foreach (GameMaterials.Offer item in list2)
{
    List<GameMaterials.TrashEntry> list3 = Match(item, list);        // 逐条件找"还没被占用"的条目
    if (item.Id == stopAt) { settlement.Complete = list3.Count == item.Conditions.Length; ... return settlement; }
    if (item.Conditions.Length != 0 && list3.Count == item.Conditions.Length)
    {
        settlement.Accepted[item.Id] = list3;
        Consume(list, list3);                                        // 连同其子孙一起从可匹配池移除
    }
}
```

**④ 窗口里每行一个按钮 + 禁用原因（`ExchangeAssistantUI.cs:522-534`）**：

```csharp
bool flag = exchangeTransfer.Reason.Length == 0;
string label = flag ? "交换" : (exchangeTransfer.Reason == "垃圾桶已备齐" ? "已备齐" : "不可用");
string text = Describe(exchangeTransfer);      // Reason 或默认说明；有暗格损失则追加一行警告
window.SetRowState(rowId, flag, label, text);  // → Button.SetInteractable/SetLabel + TipText
```

---

## 4. 踩坑与红线

| # | 坑 | 证据 | 复刻时怎么办 |
| --- | --- | --- | --- |
| 1 | **原版 UI 结构会被当契约用**：`PermaSection` 里 `ExchangeElement*` 数量必须等于 7，不等就抛异常整窗不可用 | `ExchangeAssistantUI.cs:399-409` | 宁可让它**红着失败**也不用错行；同时保留 `Reason` 文案兜底（`Refresh` 里 try/catch 到"不可用"） |
| 2 | **每帧轮询**：`Update` 每帧做十几次空判 + `LayoutEntrance` 写 `sizeDelta/anchors`；只有 `nextRefresh` 1s 节流限制的是"扫描材料"，不是布局 | `ExchangeAssistantUI.cs:54-140` | 小模组可接受；我们要做同类入口应挂到"垃圾桶出现/存档切换"的时机上，而非每帧 |
| 3 | **`Object.Instantiate` 原版行美术**：克隆体里可能带 `LocHelper` 之类的本地化组件 | `ExchangeWindow.cs:259-297`（对照 3834619049 的 `Gsx.DisableLocalizers`） | 克隆后关 `Graphic`、关 `raycastTarget`、隐藏 `CountTMP*`，必要时再关 `LocHelper` |
| 4 | **材质/贴图泄漏**：`Sprite`、`Texture2D` 都是运行时创建，必须在 `OnDestroy` 显式 `Destroy` | `ExchangeAssistantUI.cs:704-723` | 运行时贴图一律自己持有引用并成对释放 |
| 5 | **吞事件**：不实现 `PaperPointerBlocker` 就会把宿主物品拖起来 | `PaperPointerBlocker.cs:8-21` | 任何"挂在物品上的 UI"都要拦 `IBeginDrag` |
| 6 | **红线：不要替原版结算**。它连"今晚能不能结算"都不判断（那是 `HandleJanitorial` 的事），只判断"能不能投"；且"会丢东西"的操作必须二次确认并把"丢几件"写进文案 | `ExchangeTransfer.cs:50-88` vs `PlayerStore.cs:1958-1988`；`ExchangeAssistantUI.cs:640` | 投料/上架/寄售一律只搬物品，发奖、扣次数、涨声望留在原版；确认窗要能 Esc 关闭（`SetCloseOnEscape(true)`） |

---

## 5. 对我们的可用点

1. **（深空当铺：可能是群友）"贴纸入口"配方直接可用**：`深空当铺` 的赎当箱/寄存箱/信箱需要"点一下开单据"的入口时，照 `ExchangeAssistantUI.BuildEntrance` 的五件套（`RectTransform + Image + Button + PaperPointerBlocker + PaperHover`）+ `alphaHitTestMinimumThreshold=0.1f` + `TabletUI.NoKeyboardFocus`，挂在 `GameItemElement.background.rectTransform` 下即可，**零 Harmony、零能力**，比 Patch `GetActions` 干净得多。
2. **（两者）"读清单不改清单"**：`GameMaterials.ActiveOffers` 只把原版 `ExchangeBarter.conditions[].IsItemValid` 存成委托（`GameMaterials.cs:11-28`）。若《深空当铺》要做"寄售/收购需求行"、《深空电玩》要做"回收清单"，都应读原版数据源而不是复制一张需求表——否则原版改数值我们静默错。
3. **（深空电玩）"只搬不算"的三段式**：`TryAcceptAll` 逐件投 → 投完用原版判定复核（`GetRequiredItems().Count == conditions.Count`）→ 失败 `ReturnMoves` 全量回滚并把失败原因写给玩家。这正是我们"一键上架/一键装机"该抄的安全结构（`ExchangeTransfer.cs:147-184, 244-304`）。
4. **（两者）"预演结算"当 UI 数据源**：`ExchangeSimulation` 是纯函数，能在真正动手前算出"会吃掉什么、还缺什么、会不会抢别的单"。当铺的估价预览、电玩的上架预览都可以先跑一遍纯函数再显示，**UI 永远不猜**。
