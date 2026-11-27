# 深空冰城｜工坊 3829298269

> 反编译源码 `缓存文件夹\decomp_ws\3829298269\`（**142 个 .cs / 25,884 行**，几乎全在 `ProbablySnowKing\`，`Properties\AssemblyInfo.cs` 6 行）。
> 包内 manifest 实测：`<ID>probably_snow_king</ID>`、`<Name>奶茶拓展：深空冰城</Name>`、`<Author>xxy</Author>`、`<ModVersion>3.0.53</ModVersion>`、`<GameVersions>0.46D / 0.46</GameVersions>`、`<Assemblies>ProbablySnowKing.dll</Assemblies>`、`<Capabilities />` **自闭零能力**。入口 `ProbablySnowKing\Core.cs` → `Core : IMod`；**无** `HotReloadPrepare/Activate`（热重载一栏是空的）。
> **为什么值得读**：它是"**把一套新机制硬搭在原版液体系统上**"的完整案例——不新开液体类型、不改任何原版物品定义，靠往原版 `CURRENT_PART_*` 整型标签里塞自定义成分，让"萃取液"在悬停提示 / 水质扫描仪 / 倒水 / 拆水 / 算价 / 存读档里全部表现得像原版水；再叠一台复用原版 `PixelWindow` 的饮品机、一本自绘配方手册、一个订单系统、两位批发员与一位爱好者常客。**25,884 行里真正的饮品定义不到 200 行**，其余全是把这些"内容"挂上去所需的系统税。

---

## 1. 规模与结构

| 块 | 行数 | 主要文件（行） |
| --- | ---: | --- |
| A 萃取 / 液体内核 | 5,651 | `FruitIntoWater.cs` 3302、`FruitIntoWaterQuality.cs` 737、`ModContentGate.cs` 431、`LiquidLedger.cs` 128、`LiquidWatch.cs` 74、`TransferWatch.cs` 84、`PourWatch.cs` 61、`FruitIntoWaterNotice.cs` 91、`FruitIntoWaterTooltip.cs` 136、16 个萃取类 `FruitIntoWater*Patch.cs`（20~46） |
| B 饮品与配方 | 2,654 | `FruitIntoWaterDrink.cs` 2536（配方表 + 调配 + 计价 + 拆杯）、`FruitIntoWaterDrinkDirectoryPatch.cs` 56、两个拆杯补丁 29/33 |
| C 配方手册 | 2,868 | `RecipeBookWindow.cs` 1619、`RecipeBook.cs` 913、12 个 `RecipeBook*Patch.cs`（12~32） |
| D 饮品机 | 1,404 | `DrinkMachine.cs` 1299（**其中 ~424 行是窗口搭建**）、3 个补丁 23~28 |
| E 原料与器具 | 3,204 | `Topping.cs` 690、`CannedSugar.cs` 369、`BoxedMilk.cs` 304、`CannedCoffeeBean.cs` 300、`LemonHammer.cs` 294、`CannedBlackTea.cs` 268、`BrewLinkFruit.cs` 227、`PackContents.cs` 125、`HammerEquip.cs` 69、`ItemCallbacks.cs` 77、其余补丁 |
| F 顾客 / 订单 / 剧情 | 6,489 | `DeepIceCity.cs` 1505、`DeliveryOrder.cs` 1297、`IceCityFan.cs` 1035、`DeliveryOrderWindow.cs` 444、`RuiXueCoffee.cs` 397、`WholesaleStock.cs` 359、`RecruitNote.cs` 353、`FanStock.cs` 331、`RuiXueMaster.cs` 165、16 个补丁 |
| G 本地化 | 1,439 | `LocData.cs` 1056（**中文→英文子串替换表**）、`Loc.cs` 250、5 个文本 setter 补丁 |
| H 底座 | 1,949 | `Core.cs` 503、`SpriteAssets.cs` 326、`PerfProbe.cs` 154、`TemplateScrub.cs` 125、`ExampleSpriteKeys.cs` 56、`ModItemRegistration.cs` 59、各 dispatcher 20~44 |
| I 存档 | 220 | `PerSaveState.cs` |

**归类结论（回答"属于哪种"）**：既不是 `FarmStory` 的四表流水线，也不是 `Vanilla Food Expansion` 的"一条 `ItemDef` + 一个工厂"，而是 **"一张 `Recipe` 表 + 一个工厂 + 六套配套系统"**：
- 39 条 `new Recipe(...)` 一行一款（`FruitIntoWaterDrink.cs:455-505`，**1.3 行/款**，含中文品名、贴图键、风味文案、原料数组）→ 内容成本与 `FarmStory` 同档；
- 但注册出 **78 个物品 id**（39 款 × 大桶/小杯，`FruitIntoWaterDrinkDirectoryPatch.cs:13-37`）；
- 剩下的 25,000 行是 `Recipe` 表撑不住的部分：浓度算法、液体账本、机器、手册、订单、顾客、本地化。
- **UI 占比约 10%**（自绘手册 1619 + 订单面板 444 + 机器窗口 ~424 ≈ 2,487 行）。

## 2. 它怎么做这件事

### 2.1 "萃取"挂在哪：三条链，两个入口，一套去重

| 环节 | 原版方法 | 挂法 | 代码 |
| --- | --- | --- | --- |
| 悬停能不能配对（高亮） | `GameItem.MayTarget` / `CanTarget` | Postfix，`__result` 为 false 时用 `IsEligiblePair` 顶成 true | `FruitIntoWaterItemMayTargetPatch.cs:9-26`、`…CanTargetPatch.cs` 同构 |
| 真的拖上去 | `GameItem.Target(GameItem)` | **Prefix**：`return !FruitIntoWater.TryHandle(__instance, __0, "GameItem.Target")` —— 处理掉就吞掉原版 | `FruitIntoWaterItemTargetPatch.cs:9-33` |
| 自己物品上的同一条链 | `GameItemFunc.onItemTargetItemFunc` / `mayItemTargetItemFunc` / `canItemTargetItemFunc` | 在 **`GameItem.set_identifier` 的 Postfix** 里按 `SnowKing_` 前缀挂委托 | `GameItemIdentifierPatch.cs:33` → `ItemCallbacks.cs:15` → `FruitIntoWater.cs:1236-1252` |
| 柠檬锤"能不能选/装备" | `GameItemFunc.maySelectSlotItemFunc` / `canSelectSlotItemFunc` | 同一处挂，闸门是 `GeneralHelper.IsItemEffectivelyOwned` + `forceDisableUse` | `ItemCallbacks.cs:29-76`、`HammerEquip.cs:11-37` |
| 双击拆杯 / 单击也拆 | `ItemMouseDoubleClickHandler.DoubleClickAction` / `ClickAction` | 两个 Prefix 都挂（原版设置"单击与物品交互"打开时双击不触发） | `…DrinkSplitPatch.cs`、`…DrinkSingleClickSplitPatch.cs` |
| 使用 / 激活（手册、名片、订单、便签、机器） | `ItemBehaviourManager.Use/MayUse/CanUse`、`GameItem.ActivateSlotItem/CanActivateSlotItem/MayActivateSlotItem` | 6 个 **调度器** 补丁，各自 `if (!XxxPatch.TryUse(__0)) return false;` 顺序串起来 | `ItemUseDispatcherPatch.cs:9-43`、`ItemActivateDispatcherPatch.cs:9-43` 等 |

**关键点：`GameItem.Target` 的 Prefix 是全局的**——因为"原版水罐 + 原版水果"两头都没有本模组回调（`FruitIntoWater.cs` 有注释直说这一点被反编译核实过，`readme.txt` v3.0.12 也记了这次返工）。挂点选择是**全局拦截 + 自身闸门**，不是逐物品改定义。

**为什么需要去重**：`TryHandle` 的 `entry` 只有两个取值——`"ItemFunc"`（委托链，`FruitIntoWater.cs:1307`）与 `"GameItem.Target"`（补丁链）。两条链都会到，所以 `_handling` 重入锁（`FruitIntoWater.cs:2427`）+ `IsDuplicateHandle`（**同帧或 0.25s 内同一对引用**，`:2333-2350`）+ `RememberHandled`（`:2352-2366`）三层挡。

### 2.2 "按 7.5% 算份数"到底读什么

**不读任何 `Liquid` 对象的字段，也不存在浮点百分比。** 原版液体成分就是**物品 `TagSystem` 里一组整型标签** `CURRENT_PART_<LIQUID_UPPER>`，单位是**千分之一毫升**（"parts"）：

| 步骤 | 代码 | 说明 |
| --- | --- | --- |
| 成分 → 标签名 | `FruitIntoWater.cs:1440-1486` `PartTag()` | 优先调原版 `Liquid.GetCurrentPartFromId(id)`（`:1465`）；`snowking_` 前缀自造 id 直接大写拼 `CURRENT_PART_SNOWKING_*`；结果进 `PartTagCache` |
| 自定义成分 id | `FruitIntoWater.cs:745-753` `SoluteLiquidId(food)` | **`"snowking_" + foodId`**（`wine_berry` → `snowking_wine_berry`）——不新开液体类型，只借原版命名空间 |
| 读成分 | `:1574-1577` `ContentAmount` = `ReadIntTag(container, PartTag(liquidId))` | 整型，直接 `TagState.GetInt()` |
| 读总体积 | `:1579-1601` `LiquidVolume` = `WaterHelper.GetTotalVolume()` | 千分之一毫升；`FillMl = (currentCapacityML + ModSoluteTotal/1000)`（`:1624-1643`）反证单位 |
| 一颗原料兑多少 | `:133` `ExtractPerFruitRatio = 0.15f`，用点 `:2783` `num3 = (int)(体积 * 0.15f * 件数)` | 满罐 3000 ml（3,000,000 单位）→ 一颗料 450,000 单位 = **15%** |
| 7.5% → 份数 | `FruitIntoWaterDrink.cs:845` `cups = (int)Math.Floor((double)content * 100.0 / volume / 7.5 + 0.001)` | **double 现算、floor 后取整**；`+0.001` 是防 `224999/3000000*100/7.5 = 0.9999` 这类边界掉档 |
| 出杯数 | `:855-916` `MatchByPortions` | 每个原料 `min(份数 / 配方需求)` = 可做批数；再 `min(批数, max(1, 体积/1,500,000))`，**并硬顶 2 杯**（`:907-910`） |
| 常量 | `:254` `PortionPercent = 7.5`、`:256` `CupML = 1500`、`:258` `WaterBaseML = 3000`、`:132` `BottlesPerPitcher = 2` | `PortionPercent` 声明了但调配路径仍写字面量 `7.5`（两处口径一致，属可读性瑕疵） |

**推论（对我们的意义）**：所谓"浓度"是 `成分整数 / 体积整数` 的比值，没有浮点残留、没有精度漂移，读档后只要标签在就一切在。7.5% 之所以能当"一份"，是因为 15% 萃取率让它天然整除——**先定萃取率，再定份数额度**，顺序反了会得到一堆循环小数。

### 2.3 "不改动任何原版道具"靠三件事

1. **原料白名单，不改原版定义**：`SolventFoodIds[]` 25 个字符串（`FruitIntoWater.cs:92-97`，含 `processed_cheese / processed_milk / processed_juice / wine_berry / soda_red / galaxy_blend / coffee_bean / nudka / cat_bar / beis_icecream / cup_noodle / li_eat_snackbar / red_beer / nutrifruit` + 7 个 `be_*_fruit` + 4 个自家物品）；`IsFruit()` 就是 `MatchesAnyId(item, SolventFoodIds)`（`:827-830`），**纯 identifier 字符串比较**。另有 `BERRY_TAG` / `BERRY_CONDITION_INT` / `BERRY_VISUAL_VARIANT_INT` / `BLADE_ITEM_TAG` / `MEAT_TYPE_TAG` 读原版标签取品质与工具角色（`:113-115, 563-566`）。
2. **所有"新物品"都是原版模板改皮**：`DirectoryMaster.Item("processed_cheese", true)` 造饮品/原料/书/锤（`FruitIntoWaterDrink.cs:1558` 等 8 处）、`card_pharma` 造名片（`DeepIceCity.cs:480`）、`handnote` 造订单（`DeliveryOrder.cs:440`）、`mirage_projector`（备胎 `water_purifier`）造饮品机（`DrinkMachine.cs:126-142`）→ 改 `identifier / SetName / spritePath / SetGameItemType`，再 `TemplateScrub.Scrub` 洗掉 **20 个继承标签**（`TemplateScrub.cs:7-11`：热量、amenity、`XIAOWO_*` 保鲜、`CHILLED_*`、`MOISTURE_*`、`BOUGHT_PRICE_TAG` 等）。
3. **改皮挂在自己的 `set_identifier` 上**：`GameItemIdentifierPatch` 对**每一个** `identifier` 赋值跑 4 件事（`TemplateScrubIdentifierPatch.Apply` / `SpritePathBackfillPatch.Run` / `ModContentGate.InvalidateAll` / `ItemCallbacks.Attach`），全部按 `SnowKing_` 前缀早退 → 原版物品零成本。

**但"不改动原版道具"要打引号**：代码确实挂在 `GameItem.GetDisplayName`、`GameItem.Target`、`SpriteDict.GetSprite`、`TypeHelper.GetTypeDisplayName/GetTypeColor`、`WaterHelper.GetContainerPrice/GetLiquidContainerColor`、`TMP_Text.set_text` 这批**全局热点**上。它保证的是"不改原版物品的数据"，不是"零侵入"。这一点在 §4 里是红线。

### 2.4 三十三款饮品的真实数目

- 配方 **39 条**（`FruitIntoWaterDrink.cs:455-505`）：一代 11 款（`:455-465`：奶酪/代乳/果汁/汽水/果糊基底 + 冰鲜柠檬水）、二代红茶 6 款（`:469-474`）、三代咖啡 8 款 × 冷热两版 = 16（`:478-496`，热饮共用一张 `_hot` 贴图）、酿酒拓展联动 6 款（`:500-505`，对方不在则不加载）。11+6+16 = **33 款本体**，+6 = 39。
- `ReadTeaRecipes / CoffeeRecipes / CoffeeHotRecipes / BrewLinkRecipes` 是**同一个 `List<Recipe>` 切出来的分组**（`:230-236`），不是四张表；手册靠 `SectionTitleOf`（`RecipeBook.cs:683`）分组显示。
- 每条 `Recipe` 9 个字段：`Id / Name / BigName("大桶"+Name) / SmallName / SpriteKey / SmallId(Id+"_small") / FlavorText / Foods[] / MilkIsBase`（`:9-49`），`SmallSpriteKey` 由 `SpriteKey` 字符串替换 `_sprite` → `_small_sprite` 自动推（`:47`）。

### 2.5 怎么让新饮品被原版售卖系统接受

**没有自定义品类、没有自定义售卖通道**，只有三个原版字段 + 一个自家标签：

| 做什么 | 值 | 代码 |
| --- | --- | --- |
| 物品类型 | `RemoveAllGameItemType()` → `SetGameItemType("FOOD")` + `SetGameItemType("BREVAGE")`；红茶系再加 `LUXURY_ITEM` | `FruitIntoWaterDrink.cs:1624-1656` |
| 热量 | `CALORIE_VALUE_TAG = 150（小）/ 600（大）` | `:1679` |
| 价格 | `Reprice` → `drink.SetValue(n)` + `drink.unitValue = n` | `:2198-2224` |
| 标记 | `EnableTag("SNOWKING_DRINK_ITEM", false)`（机器输出槽用 `ContainerHelper.AllowOnlyTaggedItem` 只收这个标签） | `:1664`、`DrinkMachine.cs:435` |
| 尺寸 | 大桶 `2×3`、小杯 `1×2`（`ApplyShape`） | `:1596`、`:1818` |

**唯一用到非原版类型的是小料 `SNOWKING_TOPPING`**（`Topping.cs:290`），配套两个补丁把显示名和配色补上：`TypeHelper.GetTypeDisplayName` → `"小料"`、`GetTypeColor` → `Topping.CategoryColor`（`ToppingCategoryNamePatch.cs:6-23`、`…ColorPatch.cs:7-26`），**先比 `Length` 再 `string.CompareOrdinal`，原版类型零分配返回**（`readme.txt` v3.0.11 记了这步优化）。这就是它避开 `[Translation Error]` 的办法——**要么用原版类型，要么自己接管那两个 `TypeHelper` 方法**，而后者与工作区"自定义品类只用 `EnableTag`"的规则是两条不同的路（见 §5）。

### 2.6 UI：两条路线按需要选，不是一套走到底

| 界面 | 做法 | 行数 | 判断依据 |
| --- | --- | --- | --- |
| 饮品配方手册 | **完全自绘 uGUI**：`new GameObject("SnowKingRecipeBookCanvas")` + `Canvas`/`CanvasScaler` + `Image` 纸面 + `TMP_Text` 排版，自己写滚动条/缩放/拖拽 | `RecipeBookWindow.cs:309-420` 起，全文件 1619 | 只读图鉴，要自由排版和 A−/A+ 缩放 |
| 订单提交面板 | **复用原版 `PixelWindow`** + `GridPixelElement` / `TagElement` / `GameGridInventory`，6×6 格子摆货提交 | `DeliveryOrderWindow.cs` 全 444 | 要拖放、要格子、要原版窗口生命周期 |
| 饮品机 | **复用原版 `PixelWindow`**（3×3 机位 + 输入 6 格 / 水槽 2 格 / 输出 6 格 / 启动 / 冷热 / 电池 / 状态 / 手册按钮） | `DrinkMachine.cs:346-770`（~424 行） | 同上，且要电池、要格子 |
| 生命周期 | `EmporiumEntry.Awake` 进店缓存 store / `OnDestroy` 拆窗；`PlayerStore.BeginDay` 每日补货发书 | `RecipeBookStoreAwakePatch.cs:9`、`…DestroyPatch.cs:8`、`…BeginDayPatch.cs:9` | — |
| 挡穿透 | 自绘窗口必须自己拦 `EventSystem.RaycastAll` 与 `ItemMultiSelectHandler.OnEventPress`，否则原版框选/点击照样穿过去 | `RecipeBookRaycastBlockPatch.cs:10`、`…MultiSelectBlockPatch.cs:8` | v3.0.43/44 两次返工的结论 |

### 2.7 存档与迁移

- **位置**：`store.modData["SnowKing_" + key]`，键名 `deep_ice_city.*` / `lemon_hammer.granted` / `recipe_book.granted` / `recruit_note.granted` / `order.cleaned_day`（`PerSaveState.cs:38-41`；键全集见 `grep 'deep_ice_city\.'`）。**萃取进度不进存档**——它在物品自己的 `TagSystem` 上（`SNOWKING_PITCHER_FRUITS` / `SNOWKING_SOLVENT_COUNTS` / `SNOWKING_EXTRACT_QUALITY` / `CURRENT_PART_*`），跟着物品走；饮品库存同理（就是物品本身 + `SNOWKING_DRINK_*` 标签）。
- **两个反直觉设计**：
  1. **营业中不落盘**：`store.storeState > 0` 时只进 `Dirty` 镜像 + `_pendingStore`，等 `ModHook.OnShutterClosedLate` 再 `ReplayInto` + `SaveGame`（`PerSaveState.cs:160-172, 180-219`）。`ReplayInto` 在 `SaveGame` 前把脏值补回 `modData`，因为原版存档时可能读不到刚写的值（推测：作者被"营业中存档丢状态"坑过）。
  2. **dirty 按 `saveSlotId` 分槽**，槽变了整表清空（`:43-55`）——防串档。
- **没有版本号迁移**，替代手段是"读档重刷 + 遗留标签清理"：物品 `onLoaded` 里重跑 `ApplyDrink` / `Apply` / `DrinkMachine.Apply`（`FruitIntoWaterDrink.cs:1571-1574`、`DrinkMachine.cs:159-162`），`CleanLegacyTags` 删 `SNOWKING_FRUIT_INTO_WATER` 等 4 个旧键（`FruitIntoWater.cs:475`），`ClearLegacyLock` 清上一代水罐锁（`:1404-1438`），`CleanPhantomParts` 清"没有体积支撑的成分标签"（`:1523-1574`），`IsLegacyDrink` 靠旧 `SNOWKING_DRINK_BASE_SPRITE` 标签认老饮品（`:746-765`）。

### 2.8 Patch 靶点表（49 个 `[HarmonyPatch]` 类 / 18 Prefix + 34 Postfix / **0 Transpiler**）

| # | 原版靶点 | 类型 | 目的 | 热点 | 风险 |
| --- | --- | --- | --- | --- | --- |
| 1 | `GameItem.Target` | Prefix | 拖放落地总入口 | **高**（每次拖放+每次两两问配对） | 返回值判反会吞掉全游戏拖放；`_handling` 漏放会永久哑 |
| 2 | `GameItem.MayTarget` / `CanTarget` | Postfix | 高亮放行 | **高**（鼠标一动物品两两互问） | 每对都进 `IsEligiblePair`；靠 `ModContentGate` 角色缓存+两次查表早退 |
| 3 | `GameItem.MayHaveValidInventorySlot` | Postfix | 封水罐过滤器槽 | **高** | 直接改 `__result=false`，会顺带挡住别的模组往水罐塞东西 |
| 4 | `GameItem.GetDisplayName` | Postfix | 名字追加内容物/品质/小料 | **高**（每次取名；悬停每帧重建 tooltip） | 与《深空电玩》已清零的同一热点直接叠加 |
| 5 | `GameItem.set_identifier` | Postfix | 改皮后洗标签/挂回调/挂贴图 | **高**（每次生成物品） | 前缀早退是唯一护栏，漏了就是全物品判定 |
| 6 | `GameItem.CanActivateSlotItem` / `MayActivateSlotItem` / `ActivateSlotItem` | Postfix/Postfix/**Prefix** | 手册/名片/订单/机器/便签的"用一下" | 中 | **Prefix 若对非自家物品返回 false = 原版全废**（v3.0.8 事故） |
| 7 | `ItemBehaviourManager.Use` / `MayUse` / `CanUse` | Prefix/Postfix/Postfix | 右键"使用"（盒装奶/罐茶/咖啡豆/糖/名片/手册） | 中 | 同上 |
| 8 | `ItemMouseDoubleClickHandler.DoubleClickAction` / `ClickAction` | Prefix ×2 | 大桶拆 4 小杯 | 中 | 只认自家饮品；原版"单击交互"开关要两条都挂 |
| 9 | `SpriteDict.GetSprite` | **Prefix**，`[HarmonyPriority(200)]` | 自家贴图直接给结果 | **极高**（每个 sprite 每次取图） | 必须先 `IsSpriteKey`（`StartsWith("SnowKing_")` + 字典缓存）再动手；命中即 `return false` |
| 10 | `GameItemElement.ResolveItemSprite` / `ResolveSpriteByName` | Postfix / Prefix | 元素重建时补图 | 高 | 属于"自愈"，非主路径 |
| 11 | `WaterHelper.GetContainerPrice` | Postfix | 给装萃取液的水罐补价 | **高**（递归算价，一帧几十次） | 必须 `ModContentGate` 250ms 缓存 + `PricingSnapshot` 防递归自激 |
| 12 | `WaterHelper.GetLiquidContainerColor` | Postfix | 萃取液染色 | **高**（悬停每帧） | 同上缓存；只对 `IsModContentContainer` 生效 |
| 13 | `WaterHelper.EmptyContainer` / `WaterHelper.TransferLiquid` / `WaterHelper.InitLiquidContainerItem` | Postfix ×3 | 排空 / 转移 / 初始化后清残留、搬运成分、迁移旧标签 | 中 | `TransferLiquid` 有两个同名（`WaterHelper` 与 `LiquidContainerHelper`），重载必须写类型表 |
| 14 | `LiquidContainerHelper.TransferLiquid` / `SplitWater` | Postfix ×2 | 分水 / 转移后重算账本 | 中 | — |
| 15 | `WaterFeatureHelper.OnLiquidChanged` / `OnLiquidContainerPourInto` / `OnLiquidContainerPourOutOf` | Postfix ×3（PourIn 还有 Prefix 记体积） | **调配主入口**、账本搬运、残留清理 | 中高 | 一次倒水会连发多次，`PourWatch` + 1s 去重挡重复成杯 |
| 16 | `WaterScannerUIManager.GetTestedString` | Postfix | 扫描仪报"内含/评价" | 低 | — |
| 17 | `TypeHelper.GetTypeDisplayName` / `GetTypeColor` | Postfix ×2 | 小料品类名与配色 | **高**（每个物品类型显示都要过） | 先比长度再 ordinal，原版类型零分配 |
| 18 | `TMP_Text.set_text` / `Text.set_text` / `RichTextBuilder.Add` / `AddLine` / `Dialogue.SetText` | Prefix ×5 | 全局中文→英文子串替换 | **极高** | `Loc.Active` 在中文环境是 O(1) false；非中文环境 = 每次设文本走 `HasCjk` + 512 条缓存 + 上千次 `IndexOf` |
| 19 | `ModItemDirectory.InitDirectory` | Postfix，`[HarmonyPriority(0)]` | 兜底注册（主路是 `ModHook.OnModItemDirectoryInit`） | 低 | 双路注册靠 `_done` 幂等 |
| 20 | `PlayerStore.BuyItem` / `OnItemBought` | Postfix ×2 | 订单买下后开始计时 | 低 | 两个都要，因为不同购买路径 |
| 21 | `PlayerStore.SellItem` / `AddedItemToClientArea` | Postfix ×2 | 爱好者顾客对白 + 摘"馈赠"折扣 | 中 | 每次成交 / 每次上柜 |
| 22 | `PlayerStore.BeginDay` | Postfix | 每日补发手册 / 清订单 | 低 | — |
| 23 | `StorePhoneClient.GetLocalizedDisplayName` / `GetCallDialog` | Prefix / Postfix | 电话簿里改名 + 换邀约对话 | 低 | 只认自家 id |
| 24 | `EmporiumEntry.Awake` / `OnDestroy` | Postfix ×2 | 进 / 离店建拆窗口、缓存 store | 低 | 静态引用必须成对清，否则换档空引用 |
| 25 | `EventSystem.RaycastAll` / `ItemMultiSelectHandler.OnEventPress` | Postfix / Prefix | 自绘窗口挡穿透 | **高**（每次点击 / 框选） | 只在窗口开着时动手，否则别碰 |
| 26 | `StartingPerk.IsPerkActive` | Prefix | 主菜单期间屏蔽 | 低 | — |

**没碰的已知热点**（一个补丁都没挂）：`StoreUIManager.Update`、`StoreClientListDict.CreateStoreClient`、`GameItem.IsTag`、`Liquid.*` 本体。体积/成分一律**只读不补**（`WaterHelper.GetTotalVolume`、`GetCurrentCapacity*`、`GetCurrentCapacityML` 都在业务代码里直调）。顾客生成走官方钩子 `ModHook.OnGenerateCustomerVeryLate`（`ClientGenerationPatch.cs:18`），不拦原版生成方法。

### 2.9 一份饮品从原料到上架（完整链路）

1. **买下**：水罐 `water_pitcher` 与原料必须是"已拥有"——`IsOwned` 先看 `IS_OWNED_TAG`，再 `GeneralHelper.IsItemOwned`，读不到按已拥有处理（`FruitIntoWater.cs:837-863`）；未拥有一律拒（`:2401-2411`），防在批发摊上现场萃取。
2. **投料**：`GameItem.Target` Prefix → `TryHandle` → `TrySplitFruit` 认出原料 → 顺序检查"过滤器槽空 / 红茶只能一罐 / `PitcherOnly` 只收水罐" → `AddFruit` 写 `SNOWKING_PITCHER_FRUITS`（`;` 与 `|` 分隔的自定义格式）+ `SNOWKING_FOOD_COST_INT` 记原料成本 → `RefreshNativeLabel` 调原版 `WaterHelper.UpdateContainerGoodType` + `WaterFeatureHelper.OnWaterLabelUpdated` 让名字/提示跟上（`:2638-2640`、`:498-517`）。
3. **萃取**：柠檬锤先靠 `maySelectSlotItemFunc` 通过原版装备流程 → 点水罐仍走 `Target` → `TrySplitTool` → `CanExtract`（有料 + 有水 + `RequireFull` 时 ≥2999 ml，`:1383-1402`）→ `TryExtract`。
4. **算账**：先拦三种非法（非配方材料 / 配方拼不拢 / 污水），再 `num3 = 体积 × 0.15 × 件数`，逐条 `InjectSolute` 写 `CURRENT_PART_SNOWKING_*`，同步写 `SNOWKING_EXTRACT_QUALITY`（品质串）、`SNOWKING_EXTRACTED_INT`、`SNOWKING_SOLVENT_COUNTS`（件数账本），最后清空 `SNOWKING_PITCHER_FRUITS` 与原料成本 → 折进 `SNOWKING_LIQUID_VALUE_INT`（`:2707-2830`）。
5. **调配**：水罐倒向 `small_bottled_water / bottled_water / large_bottled_water`（`FruitIntoWaterDrink.cs:449-451`）→ `OnLiquidContainerPourInto` Postfix → `TryPourCraft` → `MatchByPortions` 算份数并严格匹配 39 条配方 → 排 `PendingCraft` → `Core.RequestTick()`。
6. **成杯**：下一帧 `Execute` → `CreateDrink` 造 N 杯（模板 `processed_cheese` 改皮）→ `Decorate` 写 `SNOWKING_DRINK_BASE_PRICE_INT / SHARE_INT / INGREDIENTS` 与品质 → `TryPlaceIntoPublic` 放进瓶子所在容器 → 销毁空瓶 → `EmptyPitcher` 把所有成分标签清零（`FruitIntoWaterDrink.cs:1386-1498, 1516-1551`）。
7. **定价**：`CalculatePrice = 萃取物价值 × 1.2 ÷ 杯数 × 等级倍率(+ 小料 + 加糖)`，小杯再 ÷4（`:2148-2196`）→ `Reprice` 写 `unitValue`。
8. **上柜**：类型 `FOOD`+`BREVAGE`（+红茶系 `LUXURY_ITEM`）已能被原版柜台/交易接受，`CALORIE_VALUE_TAG` 让食品系统不报错；剩下的售卖只是原版流程。小料/加糖拖到杯子上 → `Topping.TryApplyToDrink` / `CannedSugar.TryApplyToDrink` → 改加成标签 → `Reprice`（`FruitIntoWater.cs:2521-2558`）。

## 3. 关键代码摘录

**(1) 浓度 → 份数（回答"7.5% 怎么算"，`FruitIntoWaterDrink.cs:794-853`）**——整型成分、double 现算、floor 取整：

```csharp
internal static Dictionary<string, int> SolutePortions(GameItem? a, GameItem? b = null)
{
    Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.Ordinal);
    if (a == null) return dictionary;
    bool flag = b != null && b != a;
    int num = 0;                                        // 分母：两件容器的液体总量（千分之一毫升）
    try { num += FruitIntoWater.LiquidVolume(a); } catch { }
    if (flag) { try { num += FruitIntoWater.LiquidVolume(b); } catch { } }
    if (num <= 0) return dictionary;
    foreach (string text in FruitIntoWater.SolventFoodIds)
    {
        if (text == "SnowKing_boxed_milk" || text == "SnowKing_canned_coffee_bean") continue;
        string liquidId = FruitIntoWater.SoluteLiquidId(text);          // "snowking_" + foodId
        int num2 = FruitIntoWater.ContentAmount(a, liquidId);           // CURRENT_PART_SNOWKING_* 整数
        if (flag) { try { num2 += FruitIntoWater.ContentAmount(b, liquidId); } catch { } }
        if (num2 > 0)
        {
            int num3 = (int)Math.Floor((double)num2 * 100.0 / (double)num / 7.5 + 0.001);  // ← 7.5% 一份
            if (num3 > 0)
                dictionary[text] = (dictionary.TryGetValue(text, out var value) ? (value + num3) : num3);
        }
    }
    return dictionary;
}
```

出杯封顶（`:863-913`）：`num2 = max(1, BatchUnits / 1500000)`，逐条配方取 `min(份数/需求)`，再 `min(num5, num2)`、`if (num5 > 2) num5 = 2;` → **一罐最多 2 杯**。

**(2) 拖放挂点：全局 `Target` Prefix（`FruitIntoWaterItemTargetPatch.cs:9-33`）**

```csharp
private static bool Prefix(GameItem __instance, GameItem __0)
{
    if (!FruitIntoWater.Enabled) return true;
    if (!Core.Alive(__instance) || !Core.Alive(__0)) return true;
    try
    {
        ConfigEntry<bool>? debugLogging = Core.DebugLogging;
        if (debugLogging != null && debugLogging.Value)
            Core.Log?.Msg("[探针] GameItem.Target：" + Core.Clean(__instance.identifier)
                          + " -> " + Core.Clean(__0.identifier));
        return !FruitIntoWater.TryHandle(__instance, __0, "GameItem.Target");  // 处理掉就吞，否则放行
    }
    catch (Exception ex)
    {
        Core.Log?.Error("GameItem.Target 处理失败：" + ex.Message);
        return true;                                    // 出错一律放行，绝不吞原版
    }
}
```

**(3) 同一对物品的去重（双链合流，`FruitIntoWater.cs:2333-2366`）**

```csharp
private static bool IsDuplicateHandle(GameItem a, GameItem b, string entry)
{
    try
    {
        int frameCount = Time.frameCount;
        float realtimeSinceStartup = Time.realtimeSinceStartup;
        if (a == _lastSource && b == _lastTarget
            && (frameCount == _lastHandledFrame || realtimeSinceStartup - _lastHandledTime < 0.25f))
        {
            Core.Debug("同一次拖放的重复入口，已跳过：" + entry);
            return true;
        }
    }
    catch (Exception ex) { Core.Debug("去重判断失败（按未重复处理）：" + ex.Message); }
    return false;
}
```

**(4) 自定义成分假装成原版液体（`FruitIntoWater.cs:519-530` + `:1440-1486` 关键分支）**

```csharp
private static int InjectSolute(GameItem container, string liquidId, int amount)
{
    if (amount <= 0) return 0;
    string text = PartTag(liquidId);                    // CURRENT_PART_SNOWKING_WINE_BERRY
    int num = ReadIntTag(container, text);
    SetIntTag(container, text, num + amount);           // EnableTag + ModifyTag，读回校验
    Core.Debug("自造溶质写成分标签：" + text + " " + num + "→" + ReadIntTag(container, text) + "。");
    return amount;
}

// PartTag 的三段判定（:1452-1482）
if (PartOverrides.TryGetValue(text, out string value2))      text2 = "CURRENT_PART_" + value2;
else if (text.StartsWith("snowking_", StringComparison.Ordinal)) text2 = "CURRENT_PART_" + text.ToUpperInvariant();
else {
    try { text3 = Core.Clean(Liquid.GetCurrentPartFromId(text)); } catch { }   // ← 原版权威映射
    if (text3.Length > 0) text2 = text3;
    else text2 = "CURRENT_PART_" + /* 去掉 liquid_ 前缀后大写 */;
}
```

**(5) 原版倒水不搬自定义成分 → 自己配对搬运（`LiquidWatch.cs:46-58` + `LiquidLedger.cs:42-48`）**

```csharp
int num = FruitIntoWater.LiquidVolume(item);
if (LastVolume.TryGetValue(item, out var value))
{
    if (num < value)      { FruitIntoWater.TryReleaseFruitOnPour(item, reason);
                            LiquidLedger.OnLost(item, value, value - num); }
    else if (num > value) { LiquidLedger.OnGained(item, num - value); }
}
else if (num > 0)         { LiquidLedger.OnGained(item, num); }
if (LastVolume.Count > 512) LastVolume.Clear();
LastVolume[item] = num;

// Ledger：1500ms 内把"某件少了 X"和"某件多了 ≈X"配成一对，再按比例搬元数据
if (entry.IsLoss != mine.IsLoss && entry.Item != mine.Item
    && Math.Abs(entry.Amount - mine.Amount) <= Math.Max(1000, mine.Amount / 50))
```

**(6) 饮品成型：类型 + 标记 + 热量（`FruitIntoWaterDrink.cs:1624-1680`，省略 try 包裹）**

```csharp
item.RemoveAllGameItemType();
item.SetGameItemType("FOOD");
item.SetGameItemType("BREVAGE");
if (IsReadTeaRecipe(recipe)) item.SetGameItemType("LUXURY_ITEM");
foreach (string key in ContainerTags) DisableTag(item, key);   // 别带着液体容器标签变成"瓶子"
item.EnableTag("SNOWKING_DRINK_ITEM", false);                  // 机器输出槽只认这个
FruitIntoWater.SetIntTag(item, "SNOWKING_DRINK_SMALL_INT", small ? 1 : 0);
FruitIntoWater.SetIntTag(item, "CALORIE_VALUE_TAG", small ? 150 : 600);
```

## 4. 踩坑与红线

作者在 `readme.txt` 里留了 60+ 条版本记录，是本档案最硬的一手证据。按性质归成五类：

**A. 调度器的返回值（最惨的一次，v3.0.6→v3.0.8）**
- 为减补丁层数，把同一方法上的多个补丁合并成一个 dispatcher，结果**把"是否接管"判反**：对**任何不是本模组的东西**也跳过原版动作 → "双击物品没反应、说明书读不了、名片点不了、门出不去"。
- 铁律：`if (!OurHandler.TryX(item)) return false;` 里那个 `TryX` **必须只对自家物品返回 true**；末尾一律 `return true` 放行。本模组现在的写法（`ItemUseDispatcherPatch.cs:9-43`）每个 `TryUse` 内部都先查 `SnowKing_` 前缀。

**B. 判定挂在哪一侧（v3.0.11 / v3.0.12 两次返工）**
- 曾把"能不能放下"只挂在**自家物品的物品回调**上 → 但玩家最常做的是"原版水罐 ⇄ 原版水果"，两头都没回调，判定整条消失，功能全灭。
- 正确做法：**跨模组/跨原版物品的配对必须挂全局补丁**（`GameItem.Target/MayTarget/CanTarget`），自家物品上再挂一份委托当"顺手路径"，两链用 frame+0.25s 去重。
- 同时反编译核实出：**3 个"管理器侧目标判定"补丁里，两个对应的原版方法在全程序集 0 处调用**，在上面改结果等于凭空伪造信号 → 删掉，补丁数 49→39（readme 口径；readme 后来记的 42 与当前源码实测 49 个补丁类口径不同，可能含/不含调度器与兜底补丁）。**补 0 调用的方法是最容易被反编译证伪的浪费**。

**C. 点击命中（v3.0.9 / v3.0.14 / v3.0.17，三次）**
- 原版给物品图设了 `background.image.alphaHitTestMinimumThreshold = 1e-05`，**完全透明像素不算点中**；奶茶窄杯身在 2 格画布中央，**42% 是透明留白** → "只有一小部分能点中"。
- 修法是**只对自家贴图**把阈值归零，原版图一个像素不碰。
- 第一次修错的方向是"给元素里每一个图标都设阈值"——里面含原版玻璃/底板/高光图，等于把原版点击判定一起改了，**半透明原版图变得点不中，拖动整体失灵（冰箱里拖不动、鼠标粘手）**。

**D. 性能（v3.0.6 / 3.0.7 / 3.0.18 / 3.0.21 / 3.0.23 / 3.0.25 / 3.0.27，七轮）**
- 原版**算价是递归的**（父→子→再算价），打开装满东西的容器时同一件一帧被问价几十次；实测单次挂点 **2.275ms**。→ 同帧只算一次 + `ModContentGate` 250ms 缓存。
- 原版**悬停时每次输入事件都重建整份 tooltip** → 取名/算价/染色/内容物判定被高频重入。→ "结论永久记住"（不是本模组的物品一辈子只判一次）+ "拼一次存住，写标签或液量变化才作废"。
- 曾给**最热的算价函数**挂补丁修"售价翻倍"，双倍在根上修好后补丁只剩副作用：**抹掉市场行情与全部折扣**，让饮品成了全店唯一不受行情影响的商品 → 删掉，"这是本模组此前最大的一笔性能开销"。
- 定时重试曾写成"做完立刻再排一轮"，退避期里 60fps 空转 → 改成"到点再叫 + 无待办就 `enabled=false` 整个停用"（`Core.cs:395-434`）。
- **读档安静窗口 3 秒**：读档头几秒物品标签还没恢复完，此时判定/建缓存记下来的都是错的 → `Core.BeginQuiet(3f)` + `Core.Quiet` 在 `GetContainerPrice` 补丁（`FruitIntoWaterQualityPricePatch.cs:13`）与 `LiquidWatch.Observe`（`LiquidWatch.cs:28`）里直接早退。

**E. 与"零能力"和窗口的战斗（v3.0.1 / 3.0.22 / 3.0.41 / 3.0.43 / 3.0.44）**
- 两处**反射写法**被官方加载器的安全扫描判成"使用了清单未声明的能力"，玩家看到授权弹窗/加载被拒 → 改成加载器认得的写法（`Core.MarkSpriteChanged` 那种 `GetProperty("spriteChanged")` 也要小心），`<Capabilities />` 保持自闭。
- 自绘窗口最初是"全屏透明遮罩"——挡得住视觉，挡不住原版点选与**框选**（拖窗口时冒出原版框选框）→ 必须真窗口 + `EventSystem.RaycastAll`/`ItemMultiSelectHandler.OnEventPress` 双拦截。

**对我们直接成立的红线（从上面推出来的）**：
1. **别在 `ItemBehaviourManager.Use` / `GameItem.ActivateSlotItem` 的 Prefix 里对非自家物品返回 false**——一次就够把全游戏双击/使用废掉。
2. **跨模组配对不要只挂自己物品的回调**；但全局补丁必须配 O(1) 前置闸（`identifier.StartsWith` + 引用/角色缓存），否则鼠标一动就在给全店物品两两做字符串比较。
3. **`SpriteDict.GetSprite` / `TMP_Text.set_text` / `Text.set_text` / `TypeHelper.GetType*` 这四个位置，除非必要别碰**；碰了就照它的做法：先做**常数级前缀判定**（`StartsWith` / `Length !=` 比较），命中才进业务，未命中零分配返回。
4. **改原版贴图的点击阈值 = 改原版交互**。要动只能对自己的 sprite 动，而且换图时设一次，别每帧设。
5. **给最热的算价/取名函数加补丁之前，先问"这个 bug 是不是能不从补丁修"**——v3.0.25 是"补丁修好了 bug 却改掉了整个市场规则"的典型。

## 5. 对我们的可用点

**给《深空当铺：可能是群友》**
1. **塞壬验水 / 净水片那条线，可以直接改用这套读法**：液体成分 = `TagSystem` 里 `CURRENT_PART_<LIQUID_UPPER>` 的**整数**（千分之一毫升），读法两行（`ContentAmount` = `ReadIntTag(container, PartTag(liquidId))`，`PartTag` 优先问原版 `Liquid.GetCurrentPartFromId`）。我们现在的 `WaterHelper.RemoveContaminantFromContainer(..., "heavy_metal", 200000, 0)` 走的是原版 API，但**要显示/判定"这瓶水里到底有什么"时**，这套读法比翻 `WaterHelper` 私有字段稳。
2. **"掺假水/混合水"必须抄 `LiquidWatch` + `LiquidLedger`**：原版倒水**只搬它认识的成分**，自定义成分不会跟着走。它的解法是"体积增减 → 1500ms 内配对（容差 `max(1000, 量/50)`）→ 按比例搬元数据"，并且**顺序无关**（原版是先增后减）。我们要做"净水/浑水互相倒"就一定撞上同一个坑。
3. **展柜吸引 + 新货品上架**：塞壬的"展柜有纯水/优质水/饮品 → 35% 吸引买家"需要货品被原版柜台接受；本模组证明 **`FOOD`+`BREVAGE` 两个原版类型 + 一个自家标记标签 + `CALORIE_VALUE_TAG`** 就够了，不需要自定义品类，也不需要碰定价链。我们的"桶装纯净水/净水片盒"照这个三件套走，能省掉一整轮 `[Translation Error]` 排查。

**给《深空电玩》**
4. **自定义品类的两条路，我们现在走的是"只用 `EnableTag`"，它是"改 `SetGameItemType` + 接管 `TypeHelper`"**：`Topping.cs:290` + `ToppingCategoryNamePatch`/`ColorPatch`（先比 `Length` 再 `CompareOrdinal`，原版类型零分配返回）。`ArcTypes.Audit()` 正好可以加一条检查：**若我们哪天要用非原版类型，必须同时存在这两个 `TypeHelper` 补丁**，否则就是蓝字 `[Translation Error]`。反过来，看到别人注册了自定义类型，我们也能判断他有没有接管显示名。
5. **`GameItem.GetDisplayName` 这个热点：本模组常挂着 Postfix，而我们已经清零**。共存时谁挂谁承担"每次取名"的成本，且本模组的实现会往名字后追加"（加珍珠）（精品）"，与《深空电玩》的"公开显示名不泄密"（`ArcMarket.GetPublicDisplay`）口径冲突——这是**跨模组冲突扫描该报的一条**，建议在 `conflict_scan` 里对 `GameItem.GetDisplayName` 的 Postfix 数量做告警。
6. **存档对照**：`PerSaveState` 的"营业中不落盘 + `Dirty` 镜像 + 打烊钩子 `FlushPending` + 按 `saveSlotId` 分槽"（`PerSaveState.cs:43-219`），与《深空电玩》`ArcMarket` 的 `ES3Type.WriteProperties` 写盘剥离、《深空当铺》的 `SaveGame`/`InitialSave` 剥离是同一问题的第三种解。差别在它**不改原版存档结构、只写 `modData`**；如果我们的剥离方案哪天又被 ES3 咬回来，这条"延迟到打烊 + 脏值回灌再存"是现成的备胎（**未真机验证，仅从代码推出**）。

**两边都能用**
7. **顾客注册三段式可直接复用**：`StoreClientListDict.storeClientDict["SnowKing_xxx"] = () => Create(...)`（`DeepIceCity.cs:1449-1455`）注册 → `manager.TryAddClient(client, false)` 失败回落 `manager.AddClient(client)` → 再手动从 `clientStack` 摘出来**随机插位（绝不压队首）**（`:1091-1132`）。这套"注册 / 入队 / 插位"比散客直接 `Insert` 更抗原版冷却表，派客与伞萝萝的周日程池都能换。
8. **顺手可抄的两个小件**：`TemplateScrub.InheritedTags` 20 个标签清单（拿到原版模板物品后必须洗掉的继承标签，`XIAOWO_*` 保鲜 / `CHILLED_*` / `BOUGHT_PRICE_TAG` 这类最容易漏）；`HammerEquip.GatesPass` 的"选中诊断"三行日志（`canUseOutsideCombat / forceDisableUse / parentInventory / IsItemEffectivelyOwned`），专门用来定位"物品选不上"。
