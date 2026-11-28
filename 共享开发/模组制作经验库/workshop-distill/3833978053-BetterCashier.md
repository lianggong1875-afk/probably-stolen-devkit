# 更好的收银台 Better Cashier —— 把交易平板整块重画

`3833978053` · 12.1 万订阅 · 95 文件 / 14,028 行 · v1.8.3 · `BetterCashier.dll` · 自闭合 `<Capabilities/>` · 0 Transpiler / 0 ModHook / 51 个 `[HarmonyPatch]`
`缓存文件夹\decomp_ws\3833978053\`（入口 `BetterCashier\GameMod.cs`）
为什么值得读：**它把"结账平板"这一整屏用"复用原版工厂 + 命名子节点原地重排 + 折叠"三板斧重画，一行 IL 都没改**；而 14,028 行里只有约 8.5% 在算钱——剩下九成一是"怎么摆"，这正好是我们做任何纸面 UI（单据、清单、罚没台账）时的成本比例参照。

## 1. 规模与结构

### 1.1 目录 → 行数 → 职责（按职责归类，源码是扁平的单一命名空间）

| 区段 | 文件 | 行 | 职责 |
| --- | --- | --- | --- |
| A 列表/卡片/窄版重排 | 14 | 3,570 | `CashierItemList` 1,514 + `ExpandedRowLayout` 773 + `TabletBottomBar` 402 + `NarrowLayout` 458；卡片生成、堆叠、排序、0.8 窄版整体缩放 |
| B 议价/预览/声望/罪证 | 19 | 3,511 | `RepMultiplierLine` 938 + `CrimeBar` 587 + `BargainPreviewTicker` 380 + `BargainPreview` 255 + `BargainPersistence` 120；四档快捷、悬停预览、声望仿真、罪证预估 |
| C 设置面板与本地化 | 10 | 2,716 | `SettingsPanel` **1,796** + `ModSettings` 377 + `LocText` 234；19 个开关 + 3 档缩放的自绘面板 |
| D 各面板排布补丁 | 34 | 2,831 | 40 余个 20 行上下的薄补丁：每面板一个"贴字宽、靠右、补高度" |
| E 窗口位置记忆 | 5 | 496 | `CashierPositionPersistence` 327；按上下文（常规 / 博士处 `dr_jackson`）分槽记锚点 |
| F 第三方兼容/批发 | 3 | 526 | `StockingCompat` 400 + `DeliverySingleBuy` 93；与《顾客上货助手》抢同一行时让位 |
| G 基建/自动开合/缩放 | 9 | 372 | `ModLogger`、`AutoPanelOpen` 52、`ZoomScalePatch` 102 |
| 杂项 | 1（`Properties/AssemblyInfo.cs`） | 6 | 程序集元数据 |

**UI 占比的实测结论**：真正参与"算钱/算声望/算罪证/补状态"的代码约 **1,200 行（8.5%）**——集中在 `CashierItemList` 的取值/分组/盈利段、`RepMultiplierLine.Estimate`、`CrimePreview`、`BargainPersistence`；设置面板一项就吃掉 2,243 行（16%）。**收银台是重度 UI，这是对的**：它要解决的是"看不清"，不是"算不对"。

### 1.2 骨架特征

- **51 个特性式补丁、零 Transpiler**：全部 Prefix/Postfix；同方法多补丁靠 `[HarmonyPriority]` 排队（`Rebuild` 上挂了 5 个：`RebuildPatch` 800 / `BargainPersistencePatch` 600 / `AutoExpandSingleCardPatch` 400 / `NarrowLayoutRebuildPatch` / `ExpandedRowScrollPatch`）。
- **0 ModHook**：不订阅任何生命周期钩子。唯一的重置点在 `GameMod.OnDisable`（`GameMod.cs:25-31`），读档/热重载靠"每次 `Rebuild` 全量重建"自愈（`CashierItemList.BeginRebuild` `CashierItemList.cs:95-104`）。
- **1 处反射**：`AccessTools.Method(typeof(BargainUIManager), "ComputePreviewMultipliers")`（`RepMultiplierLine.cs:885-888`），因为原版那个方法是 `private`（`BargainUIManager.cs:1189`）。
- **持久化只碰 `ModData`**：`ModHelper.ReadModDataFileText / WriteModDataFileText`（`ModSettings.cs:248/361`）写 `better_cashier.cfg`，不碰存档、不需要 `files` 能力。

## 2. 它到底改了什么

### 2.1 先看原版：结账这一屏由谁负责

| 原版类/方法 | 行 | 角色 |
| --- | --- | --- |
| `NegociationUIManager.CreateWindow` | `NegociationUIManager.cs:142-185` | 造平板（`CustomUIManager.Instance.CreateWindow("negotiation_tablet", …)`），把缩放硬写成 `1.0 / 0.5 / 0.75` |
| `NegociationUIManager.Update / LateUpdate` | `:120-131`、`:133-141` | 面板开着就每 0.2 s 置 `refreshQueued`，`LateUpdate` 落到 `RefreshNow` |
| `RefreshNow` | `:504-531` | 判 mode → `AutoSelectLoneItem` → 刷客户头 → `BuildSignature` → 变了才 `Rebuild` |
| `Rebuild` | `:688-735` | 清空 info/content/footer/buttonHost，按 mode 分派 `BuildBuying/BuildSelling/BuildBarter/BuildInfoDealer/BuildGunOrder` |
| `BuildBuying / BuildSelling` | `:783-810`、`:852-891` | 逐件 `BuildItemRow`，尾部 `FooterLine` 写"你的资金/总计"，主按钮 `BigButton(153f, 0f, …)` |
| `BuildItemRow` | `:1029-1064` | **30 px 单行**：名字 + 右值；命中 `selectedItem` 就改走 `BuildExpandedRow` |
| `BuildExpandedRow` | `:1166-1258` | 展开三段 `ItemFeature`（成色 / 市场 / 议价），每段 `SubHeader` + 每特性一行可点 |
| `OnRowClick` | `:1152-1165` | 点行 = 选中 / 取消，`currentNegociatedItem` 跟着变 |
| `OnSellAllClick → SellMultiple` | `:831-851`、`:1448-1480` | 一键全卖：逐件 `CanSellThisItem` → `PlayerStore.SellItem` → 响收银机 → `RefreshUI` |
| `OnBuyAllClick → BuyMultipleItem` | `:893-915`、`:1422-1436` | 对称的买 |
| `OpenConfirmPanel / OnConfirmPanelYes` | `:1528-1546`、`:1547-1566` | 客人预算不够时的"差 Y"弹窗，确认后 `SellMultiple(ignoreBudget:true)` |
| `BargainUIManager.CreateWindow / OpenUI` | `BargainUIManager.cs:285-320`、`:1637-1649` | 议价小面板（`DockToTablet` `:1675`） |
| `BargainUIManager.BuildGroups / OnBargainButton` | `:451-470`、`:546-572` | 四类 × 四档百分比，每类 82 px；点档位 → `OfferDiscount/Markup/BuyingMarkup/DemandBuyingDiscount/Blackmail/ThreatenAug` |
| `ApplyOwnedItemFeature / ApplyNonOwnedItemFeature` | `:1441-1479` | 把议价结果做成 `ItemFeature` 贴到柜台物品上（+ 挂 `onItemAddedToWeighted/Counters` 给后放的货补） |
| `StoreUI.ApplyZoom(int)` | `StoreUI.cs:69`（字段 `:15-19`） | 三档缩放，档位存在 `PlayerStore.storeUIZoomLevels`（**进存档**） |

钱是**整数信用点**：`PlayerStore.playerCash` 是 `int`；物品价 `GameItem.GetNegociatedValue()` 返回 **`long`**（`GameItem.cs:1740`），格式化统一走 `TabletUI.Credits(long)` → `value + 图标`（`TabletUI.cs:152-155`）。**全包没有面额/纸币/找零/多币种概念**——只有一条"抹零"相关的分支（见 2.5 第 2 条），它也不改数值，只做提示。

### 2.2 原版流程 vs 它改后（步骤对照）

| # | 原版 | 它改后 | 出处 |
| --- | --- | --- | --- |
| 1 | 货放上柜台 → `PlayerStore.HandleAutoNegoUIDisplay` 自动弹平板 | 可整段拦掉（`Prefix → false`）；另起一套"客人到位就开、走了就关"（读 `StoreClientMono.image.enabled` + `isClientArrived` + `!IsMoving()`） | `PlayerStore.cs:2923`；`BlockAutoOpenPatch.cs`、`AutoPanelOpen.cs:13-47` |
| 2 | `OpenUI` → `RefreshNow` → `AutoSelectLoneItem`（唯一物品自动选中并展开） | `AutoSelectLoneItem` 直接 `Prefix → false` 关掉；仅当显式开"明细"且只有一张卡时，才在 `Rebuild` 前缀里写 `___selectedItem` 强制展开 | `NegociationUIManager.cs:1117`；`AutoSelectLoneItemPatch.cs`、`AutoExpandSingleCardPatch.cs:15-42` |
| 3 | `Rebuild` → 每件 30 px 单行（名字+右值），点行看三段特性明细 | 仍调原版 `TabletUI.ListRow` 造行，Postfix 把行**原地改成 78 px 卡片**（图标 · 名×数量 · 单价 · 右下总价 · 盈亏）；刷新尾段再分组折叠排序；卡片默认 `selectable=false`（点不动、不展开） | `TabletUI.cs:264-273`；`CashierItemList.cs:431-455/456-624/820-1092` |
| 4 | 结账：`OnSellAllClick`/`OnBuyAllClick` → 逐件 `PlayerStore.SellItem/BuyItem` → 收银机音 → `RefreshUI` | **完全没改结算逻辑**，只把主按钮几何改成"按文字宽 + 右/左贴合" | `NegociationUIManager.cs:831-915`；`SellButtonTweak.cs:525-681` |
| 5 | 议价：点平板议价钮 → 四类×四档 → 点档位立刻生效 → 贴 Feature | 四档复制到主面板左下与主按钮同行（卖 -10/-25/-50/-75、买 +10/+25/+50/+75）；面板改挂左侧且外壳镜像；悬停即预览价格与声望；点击前弹自家确认框；`Rebuild` 时幂等补齐原版漏贴的议价 Feature | `BargainUIManager.cs:546`；`BargainShortcutRow.cs`（573）、`NarrowBargainDockPatch.cs`（206）、`BargainPreview.cs`、`BargainConfirmPopup.cs`、`BargainPersistence.cs:68-119` |
| 6 | 预算不够 → `OpenConfirmPanel` 弹"客人只有 X，差 Y" | 保留，改排布；另在卡片上给"会被抹零的那一件"加金色描边高亮 | `NegociationUIManager.cs:1528`；`ConfirmPanelLayoutPatch.cs`、`CashierItemList.cs:1094-1142/1143-1184` |
| 7 | 缩放 1.0 / 0.75 / 0.5（存 `storeUIZoomLevels`） | `ApplyZoom` 前缀只写三个字段 → 1.0 / 0.75 / 0.6 | `StoreUI.cs:69`；`ZoomScalePatch.cs:11-36` |

**一句话**：结算、议价判定、定价公式一个都没动；它动的是"这一屏怎么呈现 + 什么时机弹出来 + 议价按钮放哪儿"。唯一碰到物品数据的只有 `BargainPersistence`（把已生效的议价 Feature 补到漏掉的柜台物品上，幂等，`FindItemFeatureByID` 判存在），所以 README 那句"不改定价"是**语义上成立、实现上略微越界**的一处。

### 2.3 Patch 靶点表（51 处；按方法归并）

| 靶点 | 次 | 类型 | 干什么 | 热点 |
| --- | --- | --- | --- | --- |
| `NegociationUIManager.Rebuild` | 5 | Pre/Post | 重建打点、补议价 Feature、单卡强制展开、套窄版、修展开行滚动 | ⚠️ 每次刷新都跑 |
| `NegociationUIManager.BuildSelling` / `BuildBuying` | 4 / 4 | Pre+Post | 记模式 → 尾部统一重排卡片；插四档快捷行、改主按钮、修捐赠总额 | ⚠️ 同上 |
| `NegociationUIManager.CreateWindow` | 3 | Post | 窄版尺寸、还原窗口位置、插设置按钮 | 一次性 |
| `NegociationUIManager.OpenUI` | 2 | Post | 窄版 + 还原位置 | 低频 |
| `BuildBarter/BuildInfoDealer/BuildGunOrder/BuildInfoBlock/BuildSignature/SectionTitle/RefreshClientHeader/RefreshBargainControl/GunRequirementRow/BuildExpandedRow/BuildItemRow/OpenConfirmPanel/OnBuyAllClick/AutoSelectLoneItem` | 各 1 | 混合 | 逐面板"贴字宽/靠右/补高/关自动选中" | 低频 |
| `StoreUIManager.Update` | 1 | **Post（每帧）** | 客人到/离 → 自动开合平板 | ⚠️⚠️ **帧循环** |
| `PlayerStore.HandleAutoNegoUIDisplay` | 1 | Pre→false | 禁止放货自动弹 | 低频 |
| `StoreUI.ApplyZoom` | 1 | Pre | 改三档缩放字段 | 低频 |
| `BargainUIManager.OpenUI / DockToTablet / GetBargainTooltip` | 2 / 1 / 1 | Pre/Post | 左挂、镜像外壳、改提示文案 | 低频 |
| `BargainButton.Setup / OnPointerEnter` | 1 / 1 | Post | 悬停预览、快捷标记 | 每次悬停 |
| `TabletUI.ListRow / InfoRow / InfoLine / SubHeader / TextRow` | 2/2/1/1/1 | Post | 名字/右值重排、颜色 | ⚠️ 通用工厂，全游戏共用 |
| `TooltipManager.SetAndShowTooltip(string, Component)` | 1 | Pre | 改提示框宽（**显式写了参数类型表**） | 低频 |

**四个指定热点的实测结论**

| 热点 | 碰没碰 | 实情 |
| --- | --- | --- |
| `GameItem.GetNegociatedValue` | **没 Patch，但调用量被放大** | `DisplayValue` 每卡一调（`CashierItemList.cs:728-742`），`GroupKey` 又调一次、`DealProfit` 再一遍；原版是"每行一次"。有 `BargainPreview.cache` 兜预览路径，**没有跨刷新缓存**。物品多 + 0.2 s 刷新周期（`NegociationUIManager.cs:120-131`）时这是它最可能卡的地方 |
| `StoreUIManager.Update` | **Patch 了（每帧）** | 进函数第一件事就是读设置位早退；用 `flag == atCounter` 做边沿触发；整体 O(1)。**可接受的每帧补丁写法** |
| `ItemFeature.*` | 间接 | `AddItemFeature/FindItemFeatureByID`（议价补齐、展开行读列表），只在 `Rebuild` 路径，不在帧循环 |
| `GameItem.IsTag` | 大量**读** | `BOUGHT_PRICE_TAG` / `STOLEN_TAG` / `CONTRABAND_ITEM_TAG` / `LIQUID_CONTAINER_TAG`，全在刷新路径；无写入 |
| `GameItem.GetDisplayName` | 大量**读** | 分组键、排序比较、卡片标题三处都取名字（`CashierItemList.cs:694-705/1185-1240/1398+`）——**正是《深空电玩》刚清零过的那类热点** |

### 2.4 「更好」逐条落地（19 个开关，`ModSettings.All` `ModSettings.cs:14-34`）

| 需求 | 做法 | 开关 |
| --- | --- | --- |
| 一眼看清在卖什么 | 30 px 行 → 78 px 卡片：图标（含其他模组的图集）· 名×数量 · 单价 · 右下总价 | `StackCards` / `ExpandDetail` |
| 同名同价合并 | `GroupKey = 名字 + 图集 + 图 + 单价`，同键折叠成一张（`SetCollapsed` 只关 `Visuals` + `ignoreLayout`，**不销毁**） | `StackCards` |
| 卖得赚不赚 | 总价 − `BOUGHT_PRICE_TAG`（原版买价标签，`PlayerStore.cs:176`/`:2761`）→ 绿/红 + 币图标；自产=0 成本 | `ShowDealPnl` |
| 客人只有这么多钱 | 累计价 > `GetBudget()` 时给"会被砍掉的那件"加金描边 | 常开 |
| 捐赠客户 | 总额改用 `GetDonationCreditValue()` 求和（原版 FooterLine 用的是 `GetNegociatedValue` 的和，口径不同） | 常开 |
| 议价更省事 | 四档搬主面板左下；悬停预览价 + 声望；点击前自家确认 | `QuickBargain` / `BargainConfirm` / `ShowRepMultiplier` |
| 面板别乱弹 | 禁自动弹 / 客人到位自动开合 二选一 | `BlockAutoOpen` / `AutoPanelOpen` |
| 窗口顺手 | 缩放 1.0/0.75/0.6；位置按上下文分槽记忆（常规 / 博士处） | 3× `ScaleKey`、`CashierPosition*` |
| 窄 | 面板宽 = 644 × 0.8，内部 `Screen/Section/Frame/Buttons/ClientName/…` 逐节点按 0.8 重映射（**真重排，不压字**） | `NarrowPanel` |

**没有的东西**：没有批量结账（原版已有"全卖/全买"）、没有自动结算、没有找零/面额、没有"记住上次议价档位"（持久的是 Feature 本身，由 `BargainPersistence` 幂等补）。

### 2.5 栅格化 UI 之外的 9 处（真正动手的地方）

1. 卡片盈亏：`BOUGHT_PRICE_TAG` 读原版买价 → 单价 × 数量 − 买价（`CashierItemList.cs:1296-1310`）。
2. 抹零高亮：累计 > 客人预算时定位"第一件自己拥有的货"（`CashierItemList.cs:1094-1142`）。
3. 捐赠总额：改用 `GetDonationCreditValue` 求和（`DonationTotalPatch.cs:13-51`）。
4. 声望仿真：从 `StoreReputation` 当前值起，逐件累加 `val/400`，再过 `ApplyReputationRules` 与夹取（`RepMultiplierLine.cs:250-332`）——与原版 `StoreReputation.cs:253`(`ModReputation(value/400.0)`)、`BargainUIManager.cs:630-650`(`ComputeRepPer1000Credits = 1000·mult·阵营·外交 / 400`) 同口径。
5. 罪证预估：复刻 `SecData.CheckSoldContraband`（`SecData.cs:585-623`）——`STOLEN_TAG` 按 `STOLEN_VALUE_INT%` 折算 FENCING，`CONTRABAND_ITEM_TAG` 全额（`CrimePreview.cs:16-71`）。
6. 议价 Feature 幂等补齐（`BargainPersistence.cs:19-67`）。
7. 缩放只改字段（`ZoomScalePatch.cs:24-30`）。
8. 窗口位置分上下文记忆 + 2 帧后还原（`CashierPositionPersistence.cs:14-90`）。
9. 跨模组图标解析：`LoadedMod.Sprites` 直取 → 去扩展名 → 忽略大小写兜底，`ModLoader.Mods` 列表 1 s 节流重扫（`CashierItemList.cs:105-224`）；与《顾客上货助手》共存时按**本地化键** `option_stock_button` 找对方按钮再让位（`StockingCompat.cs:308-311/313-337`）。

## 3. 关键代码摘录

### 3.1 复用原版工厂，再把行改成卡片（`CashierItemList.cs:431-455`）

```csharp
internal static TabletListRow BuildCardFor(NegociationUIManager manager, GameItem item,
    string right, bool refused, Func<string> tooltip, Action onClick)
{
    if (manager == null || manager.uiObject == null || item == null) return null;
    var frame = manager.uiObject.GetComponent<NegotiationTabletFrame>();   // 原版平板 frames
    if (frame == null || frame.content == null) return null;
    BeginRow(item);                                                        // 把"这一行属于谁"存进静态字段
    var row = TabletUI.ListRow(frame, frame.content, item.GetDisplayName(false), right, refused, tooltip, onClick);
    EndRow();
    return row;   // 行本身照原版造；改形状在 OnRowCreated 里做（见 3.2）
}
```

### 3.2 原地重排：加边框、加图标、把 Name/Right 重新摆放（`CashierItemList.cs:496-560`，节选）

```csharp
// y = 卡片内容区竖直基准 = (78 − 58) * 0.5（原代码里先算出来再被三处复用）
var rt = (RectTransform)row.transform;
var le = rt.GetComponent<LayoutElement>() ?? rt.gameObject.AddComponent<LayoutElement>();
le.preferredHeight = 78f;  le.minHeight = 78f;            // 30 → 78
rt.sizeDelta = new Vector2(ContentWidth, 78f);            // ContentWidth = 502 × 0.8
var visuals = (RectTransform)rt.Find("Visuals");          // ← 原版 ListRow 建的子节点，按名字找
Place(visuals, 0f, 0f, ContentWidth, 78f);
var icon = LoadIcon(item);                                // 见 3.3
if (icon != null && visuals.Find("Icon") == null)
{
    float ar = icon.rect.width / Mathf.Max(0.01f, icon.rect.height);
    float h = 56f, w = 56f * ar;
    if (w > 60f) { w = 60f; h = 60f / Mathf.Max(0.01f, ar); }   // 保持长宽比，长边封顶 60
    TabletUI.NewImage(visuals, "Icon", 8f + (60f - w) * 0.5f, y + (60f - h) * 0.5f, w, h, icon, Color.white, false);
}
var name = visuals.Find("Name").GetComponent<TextMeshProUGUI>();
Place(name.rectTransform, 76f, y, ContentWidth - 86f, 34f);
name.enableAutoSizing = false; name.fontSize = 22f;
name.enableWordWrapping = false; name.overflowMode = TextOverflowModes.Ellipsis;   // 不压字，超了给省略号
var rightTmp = visuals.Find("Right").GetComponent<TextMeshProUGUI>();
Place(rightTmp.rectTransform, ContentWidth - 168f, 48f, 160f, 24f);               // 总价挪到右下
```

### 3.3 跨模组图标：先查对方 `LoadedMod.Sprites`（`CashierItemList.cs:105-152/192-224`，节选）

```csharp
private static Sprite LoadIcon(GameItem item)
{
    if (item == null || string.IsNullOrEmpty(item.spritePath)) return null;
    if (item.useSpriteFromMod)                       // ← 模组物品才有这个分支
    {
        if (!string.IsNullOrEmpty(item.spriteAtlasPath))
        {
            var mod = FindMod(item.spriteAtlasPath); // spriteAtlasPath 就是对方的 manifest ID
            var s = mod != null ? TryFindModSprite(mod.Sprites, item.spritePath) : null;
            if (s != null) return s;
            s = RenderHandler.TryLoadFromAtlas(item.spriteAtlasPath, item.spritePath);
            if (s != null) return s;
        }
        return RenderHandler.unknownSprite;          // 兜底：问号图，不留空
    }
    ...
}

private static LoadedMod FindMod(string id)
{
    if (modById.TryGetValue(id, out var m)) return m;
    if (Time.unscaledTime < nextModScan) return null;      // 1 秒节流，避免每次刷新遍历模组表
    nextModScan = Time.unscaledTime + 1f;
    modById.Clear();
    foreach (var mod in ModLoader.Mods)
        if (mod?.Manifest != null && !string.IsNullOrEmpty(mod.Manifest.ID))
            modById[mod.Manifest.ID] = mod;
    modById.TryGetValue(id, out m); return m;
}
// TryFindModSprite：精确键 → 去 .png/.jpg → 忽略大小写逐个比（三条兜底，专治各模组键名不统一）
```

### 3.4 折叠而不是销毁（`CashierItemList.cs:1254-1290`）

```csharp
private static void SetCollapsed(CashierItemCard card, bool collapsed)
{
    card.Row.gameObject.SetActive(true);                       // 行永远活着，只关视觉
    var visuals = (RectTransform)card.Row.transform.Find("Visuals");
    if (visuals != null) visuals.gameObject.SetActive(!collapsed);
    var le = card.Layout ?? ((RectTransform)card.Row.transform).GetComponent<LayoutElement>();
    if (le != null) { le.ignoreLayout = collapsed; le.preferredHeight = collapsed ? 0f : 78f; le.minHeight = ...; }
    var g = ((RectTransform)card.Row.transform).GetComponent<Graphic>();
    if (g != null) g.raycastTarget = !collapsed;               // 折叠项不吃射线
    card.Row.selectable = !collapsed;
}
```

## 4. 踩坑与红线

1. **`AutoSelectLoneItem` 直接 `return false` 是最省事的写法，也是最容易被别的模组打架的写法**（`AutoSelectLoneItemPatch.cs`）。任何"唯一物品自动选中"是别人功能前提的模组都会被它静默干掉；它自己用 `AutoExpandSingleCardPatch` 在 `Rebuild` 前缀里补了替代行为。
2. **`StoreUIManager.Update` 每帧 Postfix 不是免费的**。它能这么写是因为函数体只有 3 个空判 + 1 个 bool 边沿触发 + 读一个设置位。要在里面 `GetComponentsInChildren` 或取 `GetDisplayName` 就是灾难。
3. **`TabletUI.ListRow / InfoRow / SubHeader` 是全游戏共用的静态工厂**（`TabletUI.cs:264`）。补它必须靠 `identifier == "negociation_ui"` / 节点名这类**收窄条件**，否则别的纸面 UI（罚没单、鉴定单、报纸）会被一起改形。
4. **硬编码原版像素坐标是这套做法最大的版本风险**：`644 / 562 / 530 / 502 / 48 / 32`（`NarrowLayout.cs:27-37`）、`818 / 764 / 43`（`StockingCompat.cs:15-19`）、`LineY = 772`（`RepMultiplierLine.cs:17`）。原版一改平板尺寸，它整块错位——所以它每次 `Rebuild` 都重新 `Apply`（幂等重排）而不是只在开窗时算一次。**抄就要抄"每次重建都重排"，别抄死的常量当契约**。
5. **声望/罪证这两块是"复制公式"**，不是调原版函数：`/400`、`×0.66/×1.2`、`×1.33`、`STOLEN_VALUE_INT/100`、`isRequireVariety` 去重都靠手抄。原版一改系数它就错，且**错得很安静**（只显示错数字）。真要抄，务必像它一样把口径注释到原版行号，并在原版函数上加一条比对断言（全包搜不到这类代码，它没做）。
6. **静态状态没有读档钩子**：0 ModHook 意味着所有 `static` 缓存（`modById`、`lastWidth`、`builtItems`、位置槽）跨档复用，只靠 `Rebuild` 全量重建和 `OnDisable` 清理兜。我们若要抄结构，**必须补 `OnGameLoadedNormal` + 热重载成对重置**（工作区硬规范第 3 条）。
7. **`try/catch` 包住了几乎所有入口**（`ModLogger.Error` 留痕，做得对），但也意味着 UI 变形失败时玩家只看到"原版样子"，日志里才有一条中文错误——排障时先看 `BetterCashier` 日志再怀疑补丁没命中。

## 5. 对我们的可用点

1. **《深空电玩》刻录待办盘 / 罚没台账 / 翻新整备清单**：现在都是纯文本行，可直接照 3.1–3.3 的三板斧——**调原版 `TabletUI.ListRow` 造行 → 按 `Visuals/Name/Right` 命名找子节点原地重排 → 用 `LayoutElement.ignoreLayout` 折叠重复项**，不写 Transpiler、不碰结算。盈亏列换成"刻录成本 vs 成品估值"，`BOUGHT_PRICE_TAG` 的读法（`PlayerStore.cs:176`）可直接借。**配套纪律**：本档案 2.3 显示一个 14k 行的 UI 模组会把 `GetNegociatedValue` / `GetDisplayName` 的调用量放大 2~3 倍而不自知——我们任何在 `Rebuild`/`Update` 里按名字分组的代码，上线前先做一次每帧调用计数（电玩刚为此清过一次 `GetDisplayName` 热点）。
2. **《深空当铺：可能是群友》名片/电话/验水面板的缩放与位置**：抄 `ZoomScalePatch`（Prefix 里只写 `zoomScaleNormal/Alt/Alt2` 三个字段，`ZoomScalePatch.cs:24-30`，档位本身仍由原版 `PlayerStore.storeUIZoomLevels` 存档）与 `CashierPositionPersistence` 的"分上下文位置槽 + 2 帧后还原 + 不合理坐标丢弃"（`CashierPositionPersistence.cs:14-90`、`MaxReasonableCoord = 10000f`），比自己管窗口稳得多。
3. **跨模组探测与图标解析（两款都适用）**：共存探测别读 `ModLoader` 判 ID，学 `StockingCompat` 用**对方按钮的本地化键**做存在性探测（`option_stock_button`，`StockingCompat.cs:308-311`）；要显示别的模组的物品图标，学 `LoadIcon` 的"`LoadedMod.Sprites` → 去 `.png/.jpg` → 忽略大小写 → `ModLoader.Mods` 1 秒节流重扫"（`CashierItemList.cs:105-224`），别写 `Resources.Load`。
